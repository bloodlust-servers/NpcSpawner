# Changelog

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
