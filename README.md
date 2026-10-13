# NPC Spawner — V Rising

Server-side mod for **V Rising 1.1.x** that brings random encounters to your world:
at random moments it spawns a configured group of NPCs around a **random online player**,
and hands out **configurable loot** when a player kills them.

Everything is configured with plain JSON files — no rebuilding required.

- Never spawns inside **player castles**, **under roofs**, or near players who are **in combat**
- Colour-coded chat messages (spawn announcement, loot, "task completed")
- Admin-only chat commands for live testing
- Works on **servers only** — clients need nothing installed

> ### ⚠️ Server-side mod
> Install this on the **server only**. Players do **not** need to download or install anything —
> they just join and play. The mod changes no client files and gives no advantage to players.

**📖 Detailed guide (Russian): [docs/GUIDE-RU.md](docs/GUIDE-RU.md)** —
how to install, what is configured where, what every command does, and troubleshooting.

---

## Features

| Feature | Description |
|---|---|
| Random encounters | Picks a random online player and a random NPC group, spawns it around them |
| Random timing | Configurable min/max interval between spawns |
| Smart spawn points | Re-rolls the position if it lands in a castle or under a roof |
| Combat-safe | Skips players who are currently in combat |
| Loot on kill | Per-NPC loot tables with item id, amount and drop chance |
| Coloured messages | Spawn announcement, personal loot message, global "task completed" message |
| Admin commands | `.npcs help` — list, spawn, random, reload, inspect loot |

---

## Requirements

| Component | Link |
|---|---|
| **BepInExPack V Rising** (BepInEx 6 IL2CPP) | https://thunderstore.io/c/v-rising/p/BepInEx/BepInExPack_V_Rising/ |
| **Vampire Command Framework** | https://thunderstore.io/c/v-rising/p/deca/VampireCommandFramework/ |

Both are installed automatically when using a mod manager.

---

## Installation

1. Install **BepInExPack V Rising** and **Vampire Command Framework**.
2. Put `NpcSpawner.dll` into `BepInEx/plugins/`.
3. Start the server once. Configuration files are created automatically:
   ```
   BepInEx/config/NpcSpawner/
       npcs.json
       loot.json
       NpcSpawner.cfg
   ```
4. Edit the files and restart the server (or run `.npcs reload` in game).

> **Note:** BepInEx never overwrites values that already exist in a `.cfg` file.
> To apply new defaults after an update, delete `NpcSpawner.cfg` and restart the server.

---

## How it works

Every `IntervalMinSeconds`–`IntervalMaxSeconds` seconds:

1. The mod collects online players who are **not in combat** (if `SkipPlayersInCombat = true`).
2. It picks a random player and a random group from `npcs.json`.
3. For every NPC in that group it picks a random point within `RandomAround` metres of the player.
4. The point is **rejected** if it is inside a player castle or under a roof
   (checked against the game's castle floor tiles). Up to 15 attempts per NPC.
5. If a player has no free space (standing inside their castle), up to 3 other players are tried.
6. When a spawned NPC is killed **by a player**, that player receives the loot configured in `loot.json`.
7. When the **last** NPC of the group is killed, everyone online gets a "task completed" message.

---

## Configuration

### `npcs.json` — what to spawn

A dictionary of groups. Group names are arbitrary.

```json
{
  "night": {
    "RandomAround": 8,
    "Prefabs": {
      "2111003460": 2,
      "-494298686": 2,
      "1980594081": 2
    }
  }
}
```

| Field | Meaning |
|---|---|
| `RandomAround` | Radius in metres around the chosen player where NPCs appear |
| `Prefabs` | `{ "npc prefab guid": how many }` — up to you how many of each |

Prefab GUIDs can be found on the [V Rising Mod Wiki](https://wiki.vrisingmods.com/prefabs/CHAR)
or with the **KindredExtract** mod.

### `loot.json` — rewards

Keyed by the **NPC prefab GUID** from `Prefabs`.

```json
{
  "2111003460": [
    { "ItemId": 429052660, "Amount": 1, "Chance": 0.5 },
    { "ItemId": -77477508, "Amount": 2, "Chance": 1.0 }
  ]
}
```

| Field | Meaning |
|---|---|
| `ItemId` | Item prefab GUID |
| `Amount` | How many to give |
| `Chance` | Drop chance from `0` to `1` (`1` = always) |

Loot is added to the killer's inventory; if the inventory is full the item is dropped nearby.

### `NpcSpawner.cfg` — general settings

**`[General]`**

| Key | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | Enable automatic random spawns |
| `IntervalMinSeconds` | `300` | Minimum seconds between spawns |
| `IntervalMaxSeconds` | `600` | Maximum seconds between spawns |
| `IntervalScalesWithPlayers` | `false` | Divide the interval by the number of online players |
| `LifetimeSeconds` | `0` | How long an NPC lives (`0` = until killed) |

**`[Spawn]`**

| Key | Default | Meaning |
|---|---|---|
| `BlockNearCastles` | `true` | Never spawn inside castles / under roofs |
| `CastleBlockRadius` | `4` | Radius in metres around castle tiles where spawning is blocked |
| `SkipPlayersInCombat` | `true` | Never spawn around a player who is in combat |
| `CombatBuffGuids` | `581443919,697095869` | Comma-separated buff ids that mean "in combat" |

**`[Messages]`** — every message supports Unity rich text colour tags:

| Key | Default |
|---|---|
| `Announce` / `AnnounceMessage` | personal message to the player when a group appears |
| `GlobalAnnounce` / `GlobalSpawnMessage` | message to **everyone** about a random spawn |
| `SuccessAnnounce` / `SuccessMessage` | message to everyone when a group is fully wiped |
| `NotifyKiller` / `NotifyMessage` | personal message about received loot |

Default templates use `<color=#RRGGBB>...</color>`, for example:

```
GlobalSpawnMessage = <color=#ff5a5a>Random spawn! Player {0} got group "{1}" — {2} enemies.</color>
```

Placeholders: `{0}` player name, `{1}` group name, `{2}` enemy count
(loot message: `{0}` amount, `{1}` item id).

---

## Chat commands (admins only)

> Commands work for **server administrators only**. Other players get a refusal message.
> Command prefix: `.npcs` (alias `.npcspawner`).

| Command | Description |
|---|---|
| `.npcs help` | Show the command list |
| `.npcs list` | List all groups defined in `npcs.json` |
| `.npcs spawn <group>` | Spawn a group next to **yourself** |
| `.npcs spawnfor <player> <group>` | Spawn a group next to **another player** |
| `.npcs random` | Trigger a random spawn right now (picks a player and a group for you) |
| `.npcs loot <npc id>` | Show the loot configured for an NPC |
| `.npcs reload` | Re-read `npcs.json` and `loot.json` without restarting the server |

Example: `.npcs spawn night`

`spawn` / `spawnfor` follow the same rules as automatic spawning: if the player is in combat,
or there is no free space (castle, roof), the command reports why it failed.

---

## Documentation

- **[Detailed usage guide (Russian)](docs/GUIDE-RU.md)** — installation, every config option,
  every command, how it works, and troubleshooting.
- **[How to publish to GitHub and Thunderstore (Russian)](docs/PUBLISH-RU.md)**

---

## Building from source

Requires the **.NET SDK 6.0** or newer.

```
dotnet build -c Release
```

The DLL ends up in `bin/Release/NpcSpawner.dll`.

- By default the project restores BepInEx and game assemblies from NuGet
  (`nuget.org` + `nuget.bepinex.dev`).
- If a `libs/` folder containing `BepInEx.Core.dll` exists next to the project,
  those local assemblies are used instead (handy for offline builds).

To build a ready-to-upload Thunderstore package:

```powershell
powershell -ExecutionPolicy Bypass -File tools/pack.ps1
```

The package is written to `dist/`.

---

## Credits

- [BepInEx](https://github.com/BepInEx/BepInEx) — plugin loader
- [Vampire Command Framework](https://github.com/decaprime/VampireCommandFramework) by deca — chat commands
- [VRising.Unhollowed.Client](https://www.nuget.org/packages/VRising.Unhollowed.Client) — game assemblies for compiling

Not affiliated with Stunlock Studios. V Rising is a trademark of Stunlock Studios.

## License

MIT — see [LICENSE](LICENSE).
