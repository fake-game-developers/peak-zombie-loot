using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Peak.ZombieLoot;

[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;

    internal static Plugin Instance { get; private set; } = null!;

    private static ConfigEntry<bool>? dropsEnabled;

    private static ConfigEntry<float>? chancePercent;

    private static ConfigEntry<string>? items;

    private static ConfigEntry<string>? lootPool;

    private static ConfigEntry<int>? dropCount;

    private static ConfigEntry<bool>? dropOnPassOut;

    private static ConfigEntry<bool>? alwaysDrop;

    private static ConfigEntry<bool>? debugMode;

    /// <summary>When false the host never rolls a drop.</summary>
    internal static bool DropsEnabled => dropsEnabled?.Value ?? true;

    /// <summary>0–100. Host only. 25 means one in four dead zombies drop loot.</summary>
    internal static float ChancePercent => Mathf.Clamp(chancePercent?.Value ?? 25f, 0f, 100f);

    /// <summary>
    /// Comma-separated item names. Empty = pick from <see cref="LootPoolName"/>.
    /// Matches an item's prefab name or UI name (case-insensitive).
    /// Default: First Aid Kit and Bandages.
    /// </summary>
    internal static string ItemsRaw => items?.Value ?? "FirstAidKit,Bandages";

    /// <summary>PEAK <c>SpawnPool</c> name used when <see cref="ItemsRaw"/> is empty. Default Roots luggage.</summary>
    internal static string LootPoolName => lootPool?.Value ?? "LuggageRoots";

    /// <summary>How many items to spawn on a successful roll (1–8).</summary>
    internal static int DropCount => Mathf.Clamp(dropCount?.Value ?? 1, 1, 8);

    /// <summary>
    /// Also roll when a zombie is knocked out. Each zombie still drops at most once
    /// (the later true-death does not roll again).
    /// </summary>
    internal static bool DropOnPassOut => dropOnPassOut?.Value ?? true;

    /// <summary>Skip the chance roll (still host-only). For testing.</summary>
    internal static bool AlwaysDrop => alwaysDrop?.Value ?? false;

    /// <summary>Host spawns test zombies near the player on the Shore.</summary>
    internal static bool DebugMode => debugMode?.Value ?? false;

    private DateTime lastConfigWriteUtc;

    private float nextConfigCheck;

    private Coroutine? shoreSpawnRoutine;

    private void Awake()
    {
        Instance = this;
        Log = Logger;
        dropsEnabled = Config.Bind(
            "Drops",
            "Enabled",
            true,
            "Host only: dead zombies can drop an item.");
        chancePercent = Config.Bind(
            "Drops",
            "ChancePercent",
            25f,
            new ConfigDescription(
                "Percent chance a dead zombie drops loot (0–100). Ignored when AlwaysDrop is true. Save the .cfg and start a new map (no need to quit PEAK).",
                new AcceptableValueRange<float>(0f, 100f)));
        items = Config.Bind(
            "Drops",
            "Items",
            "FirstAidKit,Bandages",
            "Comma-separated item names to pick from (prefab or UI name). Default is First Aid Kit and Bandages. Empty = roll from LootPool, the same way Roots luggage does.");
        lootPool = Config.Bind(
            "Drops",
            "LootPool",
            "LuggageRoots",
            "PEAK SpawnPool used when Items is empty. Examples: LuggageRoots, LuggageBeach, LuggageJungle, LuggageMesa, LuggageGloom.");
        dropCount = Config.Bind(
            "Drops",
            "Count",
            1,
            new ConfigDescription(
                "How many items to spawn when a drop succeeds.",
                new AcceptableValueRange<int>(1, 8)));
        dropOnPassOut = Config.Bind(
            "Drops",
            "DropOnPassOut",
            true,
            "Also roll when a zombie is knocked out (shotgun, fall, drowsy). Each zombie still drops at most once. Off = wait until PEAK marks the zombie Dead.");
        alwaysDrop = Config.Bind(
            "Debug",
            "AlwaysDrop",
            false,
            "Host only: every dead zombie drops loot (skips ChancePercent).");
        debugMode = Config.Bind(
            "Debug",
            "EnableDebugMode",
            false,
            "Host spawns test zombies near the player on the Shore. Off = spawn nothing.");
        GameCompat.PatchAll(new Harmony(Name ?? "PeakZombieLoot"));
        RunStartupCheck("game name check", GameCompat.CheckNamedMembers);
        ItemDropper.ParseItems(ItemsRaw);
        shoreSpawnRoutine = StartCoroutine(ShoreTestSpawns.WhenTheShoreIsReady());
        SceneManager.sceneLoaded += OnSceneLoaded;
        lastConfigWriteUtc = SafeConfigWriteTime();
        Log.LogInfo($"Plugin {Name} is loaded.");
    }

    private static readonly HashSet<string> reportedGuardFailures = new();

    /// <summary>
    /// Runs optional mod code that is called from inside PEAK's own methods: a patch, an RPC, a character
    /// spawn. An exception there must never escape into the game's call, or the game step it interrupts
    /// (spawning a scout, loading a map) is left half done. Logs the first failure of each kind.
    /// </summary>
    internal static void Guard(string what, Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            if (reportedGuardFailures.Add(what))
            {
                Log.LogError($"{what} failed and was skipped: {e}");
            }
        }
    }

    /// <summary>A startup step that only reports problems must never stop the plugin from loading.</summary>
    private static void RunStartupCheck(string what, Action check)
    {
        try
        {
            check();
        }
        catch (Exception e)
        {
            Log.LogWarning($"Startup {what} failed ({e.GetType().Name}: {e.Message}).");
        }
    }

    private DateTime SafeConfigWriteTime()
    {
        try
        {
            return File.GetLastWriteTimeUtc(Config.ConfigFilePath);
        }
        catch (Exception)
        {
            return DateTime.MinValue;
        }
    }

    /// <summary>
    /// Live tuning: when the .cfg file changes on disk, reload it. Polled twice a second; costs one
    /// file-timestamp read.
    /// </summary>
    private void Update()
    {
        if (Time.unscaledTime < nextConfigCheck)
        {
            return;
        }

        nextConfigCheck = Time.unscaledTime + 0.5f;
        DateTime write = SafeConfigWriteTime();
        if (write == lastConfigWriteUtc)
        {
            return;
        }

        ReloadConfigFromDisk("cfg changed");
    }

    /// <summary>
    /// Re-reads <c>PeakZombieLoot.cfg</c> from disk into the live entries.
    /// Map restart calls this so you do not need to quit PEAK after editing the file.
    /// </summary>
    private void ReloadConfigFromDisk(string reason)
    {
        try
        {
            lastConfigWriteUtc = SafeConfigWriteTime();
            Config.Reload();
            ItemDropper.ParseItems(ItemsRaw);
            Log.LogInfo($"Config reloaded ({reason}); drop chance {ChancePercent:0.#}%, items='{ItemsRaw}', pool={LootPoolName}.");
        }
        catch (Exception e)
        {
            Log.LogWarning($"Config reload failed ({reason}): {e.Message}");
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Always re-read the .cfg here: map restart must see edits made while PEAK stayed open.
        ReloadConfigFromDisk("map load");

        if (mode == LoadSceneMode.Single)
        {
            ItemDropper.ClearRunState();
        }

        if (shoreSpawnRoutine != null)
        {
            StopCoroutine(shoreSpawnRoutine);
        }

        shoreSpawnRoutine = StartCoroutine(ShoreTestSpawns.WhenTheShoreIsReady());
    }
}
