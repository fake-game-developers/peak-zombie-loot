using System.Collections;
using Photon.Pun;
using UnityEngine;
using Zorro.Core;

namespace Peak.ZombieLoot;

internal static class ShoreTestSpawns
{
    private const int ZombieCount = 3;

    internal static IEnumerator WhenTheShoreIsReady()
    {
        if (!Plugin.DebugMode)
        {
            yield break;
        }

        var wait = new WaitForSecondsRealtime(1f);
        bool spawnedShore = false;
        while (!spawnedShore)
        {
            yield return wait;
            // Debug mode can be switched off in the .cfg while this waits for a room or the Shore.
            if (!Plugin.DebugMode)
            {
                yield break;
            }

            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || LoadingScreenHandler.loading)
            {
                continue;
            }

            Character? player = Character.localCharacter;
            if (player == null)
            {
                continue;
            }

            if (MapHandler.ExistsAndInitialized
                && Singleton<MapHandler>.Instance.GetCurrentBiome() == Biome.BiomeType.Shore)
            {
                SpawnNear(player);
                spawnedShore = true;
            }
        }
    }

    private static void SpawnNear(Character player)
    {
        Vector3 center = player.Center;
        int spawned = 0;
        for (int i = 0; i < ZombieCount; i++)
        {
            float angle = i * (360f / ZombieCount);
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * (player.transform.forward * 14f);
            Vector3 point = center + offset;
            Vector3 probe = point + Vector3.up * 30f;
            if (Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 80f, HelperFunctions.terrainMapMask, QueryTriggerInteraction.Ignore))
            {
                point = hit.point + Vector3.up * 0.5f;
            }

            try
            {
                PhotonNetwork.Instantiate("MushroomZombie", point, Quaternion.identity, 0);
                spawned++;
            }
            catch (System.Exception exception)
            {
                Plugin.Log.LogError($"Could not spawn a Shore test zombie: {exception.Message}");
                return;
            }
        }

        Plugin.Log.LogInfo($"Spawned {spawned} test zombies on the Shore.");
    }
}
