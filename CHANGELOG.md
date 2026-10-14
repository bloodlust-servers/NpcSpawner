# Changelog

## 1.7.1

- Fixed `.npcs list` (and other long replies) failing: the game chat is limited to **512 bytes**,
  while VCF truncates replies by **characters**, so long lists of groups — especially with Cyrillic
  names — blew the limit and threw an error. Long replies are now split into several chat messages
  and truncated by bytes.
- Every command is now wrapped in a safe handler: instead of a raw error you get a readable message
  and the full details go to the server log.

## 1.7.0

- Fixed: NPCs could spawn on upper castle floors — castle blocking now also uses **castle hearts**
  (`[Spawn] CastleHeartRadius`, default `30` m, ignores height) in addition to castle tiles.
- Fixed: commands could fail to find the sender's character.
- Fixed: chat messages could silently fail when a message exceeded the chat limit — messages are now
  truncated and failures are logged as warnings.
- Added `.npcs spawnme <group>` — explicit "spawn next to yourself" command (same as `.npcs spawn`).
- Added `.npcs debug` — diagnostics: players, who is in combat, how many castles and groups are found.
- Group names and player names now match even with different case, extra spaces, Cyrillic encodings
  from chat, or a unique prefix.
- Combat buff list is validated against the game's prefab database at startup; unknown buff ids are
  skipped with a warning, so the "in combat" check can no longer block every spawn.
- `docs/GUIDE-RU.md` updated for the new settings and commands.

## 1.6.0

- Added `[Spawn] SkipPlayersInCombat` — NPCs no longer spawn around players who are in combat.
- Added `[Spawn] CombatBuffGuids` so the combat buff ids can be adjusted without rebuilding.
- Added a detailed usage guide: `docs/GUIDE-RU.md` (installation, every setting, every command, troubleshooting).

## 1.5.0

- NPCs no longer spawn inside player castles or under roofs.
- Added `[Spawn] BlockNearCastles` and `[Spawn] CastleBlockRadius`.

## 1.4.0

- Chat commands are now admin-only (VCF does not enforce `adminOnly` by itself — the middleware is installed and every command re-checks the admin flag).
- Fixed random spawns never happening and no loot/success messages: the mod tick now runs on `ServerBootstrapSystem.OnUpdate` and the timer uses real time.
- NPC tracking now works by scanning nearby entities instead of relying on an internal game system.
- Added `[Messages] GlobalAnnounce` / `GlobalSpawnMessage` — a message to everyone about a random spawn.

## 1.3.0

- All chat commands restricted to admins.
- Coloured chat messages; added the "task completed" broadcast.
- Removed `SpawnPoints` from the config — NPCs always spawn around a random player.

## 1.2.0

- Chat commands via Vampire Command Framework.
- Configuration file (`NpcSpawner.cfg`) is now created inside `BepInEx/config/NpcSpawner/`.

## 1.1.0

- Removed the Bloody.Core dependency — the mod talks to the game API directly.

## 1.0.0

- First working version: random NPC groups around a random player with configurable loot.
