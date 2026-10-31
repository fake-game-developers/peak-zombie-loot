using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Peak.ZombieLoot;

/// <summary>
/// Keeps the mod loading after a PEAK update. Harmony's <c>PatchAll</c> throws at the first patch whose game
/// method is gone and skips every patch after it, so patches are applied one class at a time here: a renamed
/// method turns off only the feature that patches it. Game names the mod reaches by string are checked at
/// startup.
/// </summary>
internal static class GameCompat
{
    internal const string ZombieTypeName = "MushroomZombie";

    internal const string DieMethodName = "Die";

    internal const string SyncStateRpc = "RPC_SyncState";

    internal const string PassOutRpc = "RPCA_PassOut";

    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>Applies every <c>[HarmonyPatch]</c> class in this assembly, each on its own.</summary>
    internal static void PatchAll(Harmony harmony)
    {
        int applied = 0;
        var skipped = new List<string>();
        var failed = new List<string>();
        foreach (Type type in AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly))
        {
            if (!type.IsDefined(typeof(HarmonyPatch), inherit: true))
            {
                continue;
            }

            try
            {
                List<MethodInfo>? patched = harmony.CreateClassProcessor(type).Patch();
                if (patched is { Count: > 0 })
                {
                    applied++;
                }
                else
                {
                    // Prepare() returned false: the patch itself found its target missing.
                    skipped.Add(type.Name);
                }
            }
            catch (Exception e)
            {
                failed.Add(type.Name);
                Plugin.Log.LogWarning($"Patch {type.Name} was not applied, so that feature is off: {Unwrap(e).Message}");
            }
        }

        string summary = $"Harmony patches: {applied} applied";
        if (skipped.Count > 0)
        {
            summary += $", {skipped.Count} skipped ({string.Join(", ", skipped)})";
        }

        if (failed.Count > 0)
        {
            summary += $", {failed.Count} failed ({string.Join(", ", failed)}). A PEAK update may have changed the game";
            Plugin.Log.LogWarning(summary + ".");
            return;
        }

        Plugin.Log.LogInfo(summary + ".");
    }

    /// <summary>Logs game types and members the mod uses by name (reflection, RPC strings) that no longer exist.</summary>
    internal static void CheckNamedMembers()
    {
        var missing = new List<string>();
        Type? zombie = FindType(ZombieTypeName);
        if (zombie == null)
        {
            missing.Add($"{ZombieTypeName} (zombie loot drop)");
        }
        else
        {
            if (zombie.GetMethod(DieMethodName, AnyInstance) == null)
            {
                missing.Add($"{ZombieTypeName}.{DieMethodName} (drop on zombie death)");
            }

            if (zombie.GetMethod(SyncStateRpc, AnyInstance) == null)
            {
                missing.Add($"{ZombieTypeName}.{SyncStateRpc} (drop when a remote owner kills a zombie)");
            }

            Type? state = zombie.GetNestedType("State", BindingFlags.Public | BindingFlags.NonPublic);
            if (state == null || !Enum.IsDefined(state, "Dead"))
            {
                missing.Add($"{ZombieTypeName}.State.Dead (recognize a dead zombie)");
            }
        }

        if (AccessTools.Method(typeof(Character), PassOutRpc) == null)
        {
            missing.Add($"Character.{PassOutRpc} (drop on zombie knockout)");
        }

        if (missing.Count > 0)
        {
            Plugin.Log.LogWarning(
                $"These PEAK names were not found; a game update may have changed them: {string.Join("; ", missing)}.");
        }
    }

    /// <summary>
    /// True for what a changed game assembly throws when code built against the old one runs: a type or member
    /// is gone. Not for ordinary runtime errors (a destroyed object, a null reference).
    /// </summary>
    internal static bool IsGameMismatch(Exception e) =>
        Unwrap(e) is MissingMemberException or TypeLoadException or BadImageFormatException;

    /// <summary>Finds a game type by name in any loaded assembly, for types the mod does not reference directly.</summary>
    internal static Type? FindType(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type? type = assembly.GetType(name);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    internal static Type? ZombieType => FindType(ZombieTypeName);

    internal static bool IsZombie(Component component) =>
        ZombieType != null && component != null && component.GetComponent(ZombieType) != null;

    /// <summary><c>MushroomZombie.State.Dead</c> as an int, or -1 if the type is gone.</summary>
    internal static int DeadStateValue
    {
        get
        {
            Type? state = ZombieType?.GetNestedType("State", BindingFlags.Public | BindingFlags.NonPublic);
            if (state == null || !Enum.IsDefined(state, "Dead"))
            {
                return -1;
            }

            return (int)Enum.Parse(state, "Dead");
        }
    }

    private static Exception Unwrap(Exception e)
    {
        while (e is TargetInvocationException or TypeInitializationException or HarmonyException && e.InnerException != null)
        {
            e = e.InnerException;
        }

        return e;
    }
}
