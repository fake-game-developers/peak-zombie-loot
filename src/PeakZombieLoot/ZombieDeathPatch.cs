using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Peak.ZombieLoot;

/// <summary>
/// PEAK kills a mushroom zombie with <c>MushroomZombie.Die</c> (owner only). Everyone else hears
/// <c>RPC_SyncState</c>. The host rolls the drop from either path so only the host needs the mod.
/// Looks up <c>MushroomZombie</c> by name because the mod does not reference that type directly.
/// </summary>
[HarmonyPatch]
internal static class ZombieDiePatch
{
    private static bool Prepare() => TargetMethod() != null;

    private static MethodBase? TargetMethod() =>
        GameCompat.ZombieType?.GetMethod(
            GameCompat.DieMethodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static void Postfix(Component __instance) =>
        Plugin.Guard("zombie die drop", () => ItemDropper.OnZombieDied(__instance));
}

[HarmonyPatch]
internal static class ZombieSyncStatePatch
{
    private static bool Prepare() => TargetMethod() != null;

    private static MethodBase? TargetMethod() =>
        GameCompat.ZombieType?.GetMethod(
            GameCompat.SyncStateRpc,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static void Postfix(Component __instance, int state)
    {
        if (state != GameCompat.DeadStateValue)
        {
            return;
        }

        Plugin.Guard("zombie dead-state drop", () => ItemDropper.OnZombieDied(__instance));
    }
}

[HarmonyPatch]
internal static class ZombiePassOutPatch
{
    private static bool Prepare() => TargetMethod() != null;

    private static MethodBase? TargetMethod() =>
        AccessTools.Method(typeof(Character), GameCompat.PassOutRpc);

    private static void Postfix(Character __instance)
    {
        if (!Plugin.DropOnPassOut || __instance == null || !GameCompat.IsZombie(__instance))
        {
            return;
        }

        Plugin.Guard("zombie pass-out drop", () => ItemDropper.OnZombieDied(__instance));
    }
}
