# peak-zombie-loot

A PEAK mod that gives a **killed or knocked-out zombie** a chance to drop an item.

Only the **host** needs this mod. The item is spawned for everyone.

By default each zombie rolls once: **25%** chance to drop **one** item from the same pool as **Roots luggage**. A shotgun knockout counts (the later true-death does not roll again).

## Configuration

After one launch, edit `BepInEx/config/PeakZombieLoot.cfg`:

| Section | Key | Default | Meaning |
|---|---|---|---|
| `[Drops]` | `Enabled` | `true` | Host only: dead / knocked-out zombies can drop loot |
| `[Drops]` | `ChancePercent` | `25` | Percent chance a zombie drops loot (0–100) |
| `[Drops]` | `Items` | empty | Comma-separated item names (prefab or UI name). Empty = roll from `LootPool` |
| `[Drops]` | `LootPool` | `LuggageRoots` | PEAK spawn pool used when `Items` is empty |
| `[Drops]` | `Count` | `1` | How many items to spawn on a successful roll (1–8) |
| `[Drops]` | `DropOnPassOut` | `true` | Also roll when a zombie is knocked out. Off = wait until PEAK marks it Dead |
| `[Debug]` | `AlwaysDrop` | `false` | Skip the chance roll (every zombie drops) |
| `[Debug]` | `EnableDebugMode` | `false` | Host spawns three test zombies near you on the Shore |

Save the `.cfg` and start a new map (no need to quit PEAK). The host's values are the ones that matter.

### Item names (`Items`)

Comma-separated. Matches a prefab name or the item's UI name, case-insensitive. Exact match wins; otherwise the first name that contains the text is used.

```text
Bandage,Energy Drink,Rope,Scout Cookies,Flare
```

Leave `Items` empty to pick from `LootPool` with the same rarity weights as luggage.

Known `LootPool` names: `LuggageRoots`, `LuggageBeach`, `LuggageJungle`, `LuggageTundra`, `LuggageCaldera`, `LuggageMesa`, `LuggageGloom`, `LuggageCitadel`, `LuggageAncient`, `LuggageCursed`, `LuggageClown`, `LuggageClimber`. Custom runs that disable an item still will not drop it.

## Install

1. Install the package with r2modman / Gale, **or** drop `PeakZombieLoot.dll` into `BepInEx/plugins/PeakZombieLoot/`.
2. Launch PEAK from r2modman. Starting the game from Steam skips BepInEx.

Works with [peak-shotgun](https://github.com/fake-game-developers/peak-shotgun): knock a zombie down, and it may drop loot next to the body.

## How to test

1. Set `AlwaysDrop = true` and `EnableDebugMode = true` under `[Debug]` (the file appears after one launch). Host a run.
2. On the Shore, three zombies appear near you. Knock one out (shotgun, fall, or wait). An item should land by the body.
3. Turn `AlwaysDrop` off and use `ChancePercent` for the real roll.

## Build

Requires [PEAK](https://store.steampowered.com/app/3527290/PEAK/) installed (auto-detected under common Steam paths), or set `PEAK_GAME_DIR` / `-p:PeakGameRootDir=`.

```bash
dotnet build Peak.ZombieLoot.slnx -c Release
```

Optional deploy: `-p:DeployToPeak=true -p:PeakPluginsDir="/path/to/BepInEx/plugins/PeakZombieLoot"`  
Optional overrides: copy `Config.Build.user.props.example` → `Config.Build.user.props` (gitignored).

The DLL is `artifacts/bin/Peak.ZombieLoot/release/PeakZombieLoot.dll`.

## Developers

[AGENTS.md](AGENTS.md) is the code map: what each type does, and how zombie death and item drops work.

## Thunderstore packaging (CI)

Every push to `master` runs [.github/workflows/thunderstore.yml](.github/workflows/thunderstore.yml):

1. Builds a Thunderstore ZIP (using [FakeGameDevelopers.PEAKGameLibs](https://github.com/fake-game-developers/PEAKGameLibs) stripped refs for compile references)
2. Uploads a workflow artifact named **`FakeGameDevelopers-PeakZombieLoot`**

Every push still builds that zip. Thunderstore publish runs when `<Version>` in `src/PeakZombieLoot/Peak.ZombieLoot.csproj` is not yet the live Thunderstore release, and the organization secret `TCLI_AUTH_TOKEN` is set. The publish step copies the categories already on the package, so a new version keeps the same tags. A commit that leaves the version matching live only builds the artifact.

### Local package build

```bash
./build.sh
# or: dotnet build Peak.ZombieLoot.slnx -c Release -target:PackTS
# zip lands in artifacts/thunderstore/
```

## Credits

- Original author: **Arman Ossi Loko**
- This mod belongs to **Fake Game Developers**

Work based on this mod must credit Arman Ossi Loko and Fake Game Developers. See [LICENSE](LICENSE).
