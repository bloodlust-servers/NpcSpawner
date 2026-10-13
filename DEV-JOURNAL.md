# DEV-JOURNAL — NpcSpawner (V Rising)

## Что это
Серверный мод: в случайное время спавнит НПС случайной группы вокруг случайного игрока
и выдаёт настраиваемый лут за убийство. Команды — только для админов, сообщения цветные.
Спавн запрещён в замках, под крышами и рядом с игроками в бою.

## Статус
- [x] Сборка v1.6.0 — 0 ошибок / 0 предупреждений
- [x] Мод работает на живом сервере (хостинг Servarium): автоспавн, лут, цвета, админ-ограничение
- [x] Собран GitHub-репозиторий (`4. Репозиторий (GitHub)`) + инструкция публикации на Thunderstore
- [ ] Проверка: пропуск игроков в бою (ждём отзыв)

## v1.6.0 — не спавнить у игрока в бою
- Новые настройки `[Spawn] SkipPlayersInCombat` (true) и `CombatBuffGuids`
  (по умолчанию `581443919,697095869` — баффы обычного и PvP-боя).
- Определение боя: `ProjectM.BuffUtility.GetBuffInstanceCount(EntityManager, Entity, PrefabGUID) > 0`
  (подпись вытащена из игровых сборок; списки баффов кэшируются, перечитываются в `.npcs reload`).
- Автоспавн использует `GetSpawnablePlayers()` (онлайн и не в бою);
  `TrySpawnForCharacter` отказывает с текстом «игрок сейчас в бою — спавн запрещён».

## v1.6.0 — репозиторий и публикация
- `4. Репозиторий (GitHub)` — полный репозиторий: код, `README.md` (англ., для Thunderstore),
  `LICENSE` (MIT), `CHANGELOG.md`, `manifest.json`, `icon.png` (256×256, сгенерирована),
  `.gitignore`, `.github/workflows/build.yml`, `tools\pack.ps1` + `pack.cmd`.
- `docs\PUBLISH-RU.md` — пошагово: GitHub через GitHub Desktop (git на ПК не установлен),
  сборка пакета, загрузка на https://thunderstore.io/c/v-rising/create/, правила Thunderstore.
- `csproj` стал двухрежимным: если рядом есть `libs\BepInEx.Core.dll` — используются локальные DLL,
  иначе пакеты из NuGet (как на GitHub Actions). Проверено: сборка в offline-режиме проходит.
- Проверено, что `tools\pack.ps1` создаёт корректный пакет
  (`plugins/NpcSpawner.dll` + manifest/icon/README/CHANGELOG в корне архива).
- Скрипт `pack.ps1` переписан в ASCII: PowerShell 5.1 без BOM читает UTF-8 как CP1251 и ломает парсер.

## Предыдущие версии
- **1.5.0** — запрет спавна в замках/под крышами (`CastleFloor` + `LocalToWorld`, до 15 попыток).
- **1.4.0** — админ-команды (VCF сам не проверяет `adminOnly` — подключён `BasicAdminCheck`
  и добавлена явная проверка `ctx.User.IsAdmin`); «пульс» перенесён на `ServerBootstrapSystem.OnUpdate`,
  таймер на `DateTime.UtcNow`; учёт НПС сканированием по `PrefabGUID.GuidHash`;
  общее оповещение о случайном спавне.
- **1.3.0** — админ-команды, цветные сообщения, сообщение об уничтожении группы, убран `SpawnPoints`.
- **1.2.0** — команды VCF, `.cfg` внутри `config/NpcSpawner/`.
- **1.1.0** — убрана зависимость от Bloody.Core.

## Версии
- V Rising 1.1.x (сборка против `VRising.Unhollowed.Client 1.1.9`).
- BepInEx 6.0.0-be.733; VampireCommandFramework 0.11.0 (обязателен).

## Архитектура
- `Plugin.cs` — ConfigFile в `config/NpcSpawner/`, json-конфиги, Harmony-патчи, VCF-команды + middleware.
- `SpawnerService.cs` — игроки, таймер (UTC), выбор точки (замки/крыши/бой), «пикапы», волны (`Batch`),
  лут, рассылки.
- `Patches/` — Init (`ServerBootstrapSystem.OnUserConnected`), Tick (`ServerBootstrapSystem.OnUpdate`),
  Death (`DeathEventListenerSystem.OnUpdate`).
- `Commands/NpcCommands.cs` — `.npcs help|list|spawn|spawnfor|random|loot|reload`.

## Ключевые сигнатуры игрового API (из сборок через Mono.Cecil-дампер)
- `UnitSpawnerUpdateSystem.SpawnUnit(Entity, PrefabGUID, float3, int, float, float, float)`
- `DeathEventListenerSystem._DeathEventQuery.ToComponentDataArray<DeathEvent>()`, `DeathEvent.Died/.Killer`
- `ServerGameManager.TryAddInventoryItem(Entity, PrefabGUID, int)` → `.Success`; фоллбэк `CreateDroppedItemEntity`
- `ServerChatUtils.SendSystemMessageToClient(EntityManager, User, ref FixedString512Bytes)` (рендерит `<color>`)
- `Stunlock.Core.PrefabGUID.GuidHash`; `ProjectM.CastleBuilding.CastleFloor` + `LocalToWorld`
- `ProjectM.BuffUtility.GetBuffInstanceCount(EntityManager, Entity, PrefabGUID)`
- VCF: `ChatCommandContext.Reply/User/Event`, `ctx.User.IsAdmin`, `CommandRegistry.RegisterAll()/Middlewares`

## Потери/восстановление
Папку проекта дважды удалял мой же скрипт чистки (маска `NpcSpawner*` совпадала с корнем папки).
Восстановлено из исходников. Правило: удалять только явно перечисленные файлы, без рекурсивных масок.

## Дальше (идеи)
- Фильтр по уровню игрока, скейл уровня НПС под игрока.
- Настройка «сколько групп максимум одновременно».
