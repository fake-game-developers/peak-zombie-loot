# PeakZombieLoot

Killed or knocked-out **zombies** have a chance to drop an item.

Only the **host** needs this mod. The drop is spawned for everyone.

Default: **25%** chance, **one** item from the **Roots luggage** pool. A shotgun knockout counts; each zombie rolls at most once.

## Configuration

After one launch, edit `BepInEx/config/PeakZombieLoot.cfg`:

| Key | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | Host only: zombies can drop loot |
| `ChancePercent` | `25` | Percent chance (0–100) |
| `Items` | empty | Comma-separated item names. Empty = roll from `LootPool` |
| `LootPool` | `LuggageRoots` | PEAK spawn pool when `Items` is empty |
| `Count` | `1` | Items spawned on a successful roll |
| `DropOnPassOut` | `true` | Also roll when a zombie is knocked out |

`AlwaysDrop` and `EnableDebugMode` under `[Debug]` are for testing (Shore test zombies, every corpse drops).

Example `Items` value:

```text
Bandage,Energy Drink,Rope,Scout Cookies,Flare
```

## Install

Install with **r2modman** or **Gale** (requires `BepInExPack_PEAK`). Only the host needs it.

## Credits

- Original author: **Arman Ossi Loko**
- This mod belongs to **Fake Game Developers**
