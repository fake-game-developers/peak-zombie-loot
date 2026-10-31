using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using Zorro.Core;

namespace Peak.ZombieLoot;

/// <summary>
/// Host-only: roll a drop when a zombie is knocked out or marked Dead, then spawn the item at the corpse.
/// </summary>
internal static class ItemDropper
{
    private static readonly HashSet<int> DroppedKeys = new();

    private static readonly List<string> NamedItems = new();

    internal static void ClearRunState() => DroppedKeys.Clear();

    internal static void ParseItems(string raw)
    {
        NamedItems.Clear();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        foreach (string part in raw.Split(','))
        {
            string trimmed = part.Trim();
            if (trimmed.Length > 0)
            {
                NamedItems.Add(trimmed);
            }
        }
    }

    /// <summary>
    /// <paramref name="source"/> is the zombie script or its <see cref="Character"/>. Host only. Once per view.
    /// </summary>
    internal static void OnZombieDied(Component? source)
    {
        if (source == null || !Plugin.DropsEnabled)
        {
            return;
        }

        if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
        {
            return;
        }

        Component? zombie = GameCompat.ZombieType != null
            ? source.GetComponent(GameCompat.ZombieType) ?? source.GetComponentInParent(GameCompat.ZombieType)
            : null;
        if (zombie == null)
        {
            return;
        }

        int key = DropKey(zombie);
        if (key != 0 && !DroppedKeys.Add(key))
        {
            return;
        }

        if (!Plugin.AlwaysDrop && UnityEngine.Random.value * 100f >= Plugin.ChancePercent)
        {
            Plugin.Log.LogInfo($"Zombie {Describe(zombie)} died; no drop (rolled miss).");
            return;
        }

        Item? prefab = PickItem();
        if (prefab == null)
        {
            Plugin.Log.LogWarning("A zombie died but no drop item could be resolved. Check Items / LootPool.");
            return;
        }

        Vector3 origin = DropOrigin(zombie);
        int count = Plugin.DropCount;
        for (int i = 0; i < count; i++)
        {
            Vector3 point = origin;
            if (count > 1 || i > 0)
            {
                Vector2 scatter = UnityEngine.Random.insideUnitCircle * 0.45f;
                point += new Vector3(scatter.x, 0f, scatter.y);
            }

            SpawnItem(prefab, GroundPoint(point));
        }

        Plugin.Log.LogInfo($"Zombie {Describe(zombie)} dropped {count}× {prefab.name}.");
    }

    private static int DropKey(Component zombie)
    {
        PhotonView? view = zombie.GetComponent<PhotonView>();
        if (view != null && view.ViewID != 0)
        {
            return view.ViewID;
        }

        return zombie.GetInstanceID();
    }

    private static string Describe(Component zombie)
    {
        PhotonView? view = zombie.GetComponent<PhotonView>();
        return view != null && view.ViewID != 0 ? $"#{view.ViewID}" : zombie.name;
    }

    private static Vector3 DropOrigin(Component zombie)
    {
        Character? character = zombie.GetComponent<Character>();
        if (character != null)
        {
            return character.Center;
        }

        return zombie.transform.position + Vector3.up * 0.6f;
    }

    private static Vector3 GroundPoint(Vector3 point)
    {
        Vector3 probe = point + Vector3.up * 30f;
        if (Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 80f, HelperFunctions.terrainMapMask, QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * 0.12f;
        }

        return point;
    }

    private static Item? PickItem()
    {
        if (NamedItems.Count > 0)
        {
            return PickNamedItem();
        }

        return PickFromLootPool();
    }

    private static Item? PickNamedItem()
    {
        ItemDatabase? database = SingletonAsset<ItemDatabase>.Instance;
        if (database?.Objects == null || database.Objects.Count == 0)
        {
            return null;
        }

        var matches = new List<Item>();
        for (int i = 0; i < NamedItems.Count; i++)
        {
            Item? found = FindItem(database, NamedItems[i]);
            if (found != null)
            {
                matches.Add(found);
            }
            else
            {
                Plugin.Log.LogWarning($"Drop item '{NamedItems[i]}' was not found in the item catalog.");
            }
        }

        if (matches.Count == 0)
        {
            return null;
        }

        return matches[UnityEngine.Random.Range(0, matches.Count)];
    }

    private static Item? FindItem(ItemDatabase database, string needle)
    {
        Item? contains = null;
        for (int i = 0; i < database.Objects.Count; i++)
        {
            Item candidate = database.Objects[i];
            if (candidate == null)
            {
                continue;
            }

            string name = candidate.name ?? "";
            string ui = candidate.UIData != null ? candidate.UIData.itemName ?? "" : "";
            if (name.Equals(needle, StringComparison.OrdinalIgnoreCase)
                || ui.Equals(needle, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }

            if (contains == null
                && (ContainsIgnoreCase(name, needle) || ContainsIgnoreCase(ui, needle)))
            {
                contains = candidate;
            }
        }

        return contains;
    }

    private static bool ContainsIgnoreCase(string haystack, string needle) =>
        haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

    private static Item? PickFromLootPool()
    {
        if (!Enum.TryParse(Plugin.LootPoolName.Trim(), ignoreCase: true, out SpawnPool pool) || pool == SpawnPool.None)
        {
            Plugin.Log.LogWarning($"LootPool '{Plugin.LootPoolName}' is not a PEAK SpawnPool name.");
            return null;
        }

        List<Item> items;
        try
        {
            items = LootData.GetAllItemsInPool(pool);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Could not read loot pool {pool}: {e.Message}");
            return null;
        }

        if (items == null || items.Count == 0)
        {
            Plugin.Log.LogWarning($"Loot pool {pool} is empty.");
            return null;
        }

        var weighted = new List<(Item item, int weight)>();
        int total = 0;
        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];
            if (item == null)
            {
                continue;
            }

            try
            {
                if (!item.IsValidToSpawn())
                {
                    continue;
                }
            }
            catch (Exception)
            {
                continue;
            }

            int weight = 1;
            LootData? loot = item.GetComponent<LootData>();
            if (loot != null && LootData.RarityWeights != null
                && LootData.RarityWeights.TryGetValue(loot.Rarity, out int rarityWeight)
                && rarityWeight > 0)
            {
                weight = rarityWeight;
            }

            weighted.Add((item, weight));
            total += weight;
        }

        if (weighted.Count == 0 || total <= 0)
        {
            return items[UnityEngine.Random.Range(0, items.Count)];
        }

        int roll = UnityEngine.Random.Range(0, total);
        for (int i = 0; i < weighted.Count; i++)
        {
            roll -= weighted[i].weight;
            if (roll < 0)
            {
                return weighted[i].item;
            }
        }

        return weighted[weighted.Count - 1].item;
    }

    private static void SpawnItem(Item prefab, Vector3 position)
    {
        string name = prefab.gameObject.name;
        GameObject? spawned;
        if (PhotonNetwork.InRoom)
        {
            spawned = PhotonNetwork.InstantiateItemRoom(name, position, Quaternion.identity, false);
        }
        else
        {
            spawned = UnityEngine.Object.Instantiate(prefab.gameObject, position, Quaternion.identity);
        }

        if (spawned == null)
        {
            Plugin.Log.LogWarning($"Could not spawn drop '{name}'.");
        }
    }
}
