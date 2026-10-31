# Agent notes

Map of the zombie-loot mod for agents and developers. The player-facing setup is in [README.md](README.md).

The disk folder is `peak-zombie-loot`. The solution is `Peak.ZombieLoot.slnx`. The C# namespace is `Peak.ZombieLoot`. The assembly, BepInEx plugin, and Thunderstore name are `PeakZombieLoot`.

This mod does not add a new item. It waits for a mushroom zombie to be knocked out or marked Dead, then the **host** rolls a chance and spawns an existing PEAK item with `PhotonNetwork.InstantiateItemRoom`.

`GameCompat` looks up `MushroomZombie` by name because the mod does not reference that type directly.

## Where things live

| Path | What it is |
|---|---|
| `src/PeakZombieLoot/` | All gameplay code |
| `src/PeakZombieLoot/thunderstore.toml` | Thunderstore listing. `icon.png` at the repo root is the store icon |
| `artifacts/bin/Peak.ZombieLoot/release/PeakZombieLoot.dll` | Release build |

## Code

`Plugin.Awake` binds config, applies every `[HarmonyPatch]` class in this assembly through `GameCompat.PatchAll` (one class at a time, see [Game updates](#game-updates)), and starts the shore test spawn.

| Type | Role |
|---|---|
| `Plugin` | Config, live `.cfg` reload on map load / file change. `Guard` swallows exceptions from patches |
| `GameCompat` | Game-update protection: per-class Harmony patching, a startup check of names used by string |
| `ZombieDiePatch` | Postfix on private `MushroomZombie.Die` (owner). Host rolls if it owns the zombie |
| `ZombieSyncStatePatch` | Postfix on `RPC_SyncState` when the state is Dead, so a host that does not own the zombie still rolls |
| `ZombiePassOutPatch` | Postfix on `Character.RPCA_PassOut` when `[Drops] DropOnPassOut` is on (default). Shotgun knockdowns land here |
| `ItemDropper` | Host-only, once per Photon view id. Chance roll, pick item (`Items` list or weighted `LootPool`), spawn at the corpse |
| `ShoreTestSpawns` | Only with `[Debug] EnableDebugMode` (default off): the host spawns three zombies near the local player once per Shore load |

Vanilla PEAK only calls `MushroomZombie.Die` after the zombie has been awake longer than `lifetime` (120 s) and then passes out or falls. That is why `DropOnPassOut` defaults on: a knockout is what players treat as a kill. Each zombie is recorded in `DroppedKeys` so the later Dead state does not drop twice.

`Items` default is `FirstAidKit,Bandages` (equal chance). Empty → `LootData.GetAllItemsInPool` + `Item.IsValidToSpawn` + rarity weights (same as luggage). Named items match prefab name or `UIData.itemName`.

Every entry point that runs inside PEAK's own code (a Harmony patch) goes through `Plugin.Guard`, which logs and swallows exceptions. An exception escaping a patch aborts the game method that called it.

### Game updates

`GameCompat.PatchAll` applies each `[HarmonyPatch]` class on its own and logs one summary line (`Harmony patches: N applied, …`). A patch whose game method is gone is logged and skipped, and every other patch still applies; plain `Harmony.PatchAll` stops at the first failure. Give a patch that targets something by string a `Prepare` that returns false when the target is missing, so it is reported as skipped instead of failed.

`GameCompat.CheckNamedMembers` warns at startup when a type or member the mod reaches by string (reflection, RPC names) is gone. Add new string lookups there.

Do not bump `<Version>` unless a release was asked for.

## Build

```bash
dotnet build Peak.ZombieLoot.slnx -c Release
```

`PackTS` (`./build.sh`) writes `artifacts/thunderstore/FakeGameDevelopers-PeakZombieLoot-<version>.zip`. Publishing is described in the player README. Do not bump `<Version>` unless a release was asked for.
