# DEV-JOURNAL — NpcSpawner (V Rising)

## Что это
Серверный мод: в случайное время спавнит НПС случайной группы вокруг случайного игрока
и выдаёт настраиваемый лут за убийство. Команды — только для админов, сообщения цветные.
Спавн запрещён в замках (любой этаж), под крышами и рядом с игроками в бою.

## Статус
- [x] Сборка v1.7.1 — 0 ошибок / 0 предупреждений
- [x] Загрузка, автоспавн, лут, цвета, админ-ограничение — подтверждены на живом сервере
- [x] Репозиторий для GitHub + инструкции (публикация и пользовательский гайд)
- [ ] Подтверждение от заказчика: спавн на этажах замка, команды, уведомления

## v1.7.1 — `.npcs list` падал с ошибкой
Причина: **чат игры ограничен 512 БАЙТАМИ, а VCF обрезает ответ по СИМВОЛАМ**
(`v[..509]`). Список групп (особенно с русскими названиями) не влезал в байты, и отправка бросала
исключение. Исправление: длинные ответы режутся на несколько сообщений с подсчётом **байт**
(функции `BuildChunks` / `ReplyList` / `TruncateBytes`, лимит 300 байт на сообщение).
Плюс каждая команда обёрнута в `Run()`: вместо «сырой» ошибки — понятный текст, а полное
исключение пишется в лог.

## v1.7.0 — по жалобе заказчика
1. **НПС спавнились на высоких этажах замка.** Причина: у компонента `CastleFloor`
   **нет координат вообще** (только данные о полу/стенах/соседях) — значит старая проверка
   «плиты замка + LocalToWorld» почти наверняка не находила ничего.
   Решение: добавлена вторая, надёжная проверка — **сердце замка** (`CastleHeart` + `LocalToWorld`,
   радиус `CastleHeartRadius` = 30 м, без учёта высоты → закрывает любой этаж). Проверка по плитам
   оставлена как дополнительная.
2. **Половина команд не работала + не было уведомлений.** Наиболее вероятная причина — проверка
   «в бою» срабатывала всегда: тогда автоспавн молчит, а `spawn`/`spawnfor`/`random` отказывают.
   Решение: список баффов боя теперь **проверяется по базе префабов** (`PrefabCollectionSystem._PrefabDataLookup`)
   и неизвестные id отбрасываются с предупреждением; имена баффов пишутся в лог при старте.
3. **Команды:** персонаж отправителя теперь берётся из `User.LocalCharacter` (надёжнее, чем
   `Event.SenderCharacterEntity`); добавлена команда **`.npcs spawnme`** (заспавнить у себя)
   и **`.npcs debug`** (диагностика: кто в бою, сколько замков, сколько групп).
4. **Русские названия групп:** добавлен `TryResolveGroup` — совпадение без учёта регистра,
   с обрезкой пробелов, с «починкой» кракозябр (latin1↔utf8) и по уникальному началу названия.
   То же для ников игроков в `.npcs spawnfor`.
5. **Уведомления:** сообщения обрезаются под лимит чата (480 байт) и ошибки отправки пишутся
   предупреждением в лог вместо тихого проглатывания.

## Предыдущие версии
- **1.6.0** — не спавнить у игрока в бою (`SkipPlayersInCombat`, `CombatBuffGuids`);
  репозиторий для GitHub + гайд `docs/GUIDE-RU.md` + инструкция публикации `docs/PUBLISH-RU.md`.
- **1.5.0** — запрет спавна в замках/под крышами (по плитам замка).
- **1.4.0** — админ-команды (VCF сам не проверяет `adminOnly`); «пульс» на `ServerBootstrapSystem.OnUpdate`,
  таймер на `DateTime.UtcNow`; учёт НПС сканированием по `PrefabGUID.GuidHash`; общее оповещение о спавне.
- **1.3.0** — админ-команды, цветные сообщения, сообщение об уничтожении группы, убран `SpawnPoints`.
- **1.2.0** — команды VCF, `.cfg` внутри `config/NpcSpawner/`.
- **1.1.0** — убрана зависимость от Bloody.Core.

## Версии
- V Rising 1.1.x (`VRising.Unhollowed.Client 1.1.9`).
- BepInEx 6.0.0-be.733; VampireCommandFramework 0.11.0 (обязателен).

## Где что лежит
- Рабочая копия проекта: `C:\Users\Oks\Documents\AgentHere\NpcSpawner`
- Комплект для заказчика: `C:\Users\Oks\Desktop\свои моды\NpcSpawner`
  (`1. Для сервера`, `2. Набор для раздела Моды.zip`, `3. Исходники`, `4. Репозиторий (GitHub)`)

## Архитектура
- `Plugin.cs` — ConfigFile в `config/NpcSpawner/`, json-конфиги, Harmony-патчи, VCF-команды + middleware.
- `SpawnerService.cs` — игроки, таймер (UTC), выбор точки (замки/крыши/бой), «пикапы», волны (`Batch`),
  лут, рассылки, подбор группы/ника, диагностика.
- `Patches/` — Init (`ServerBootstrapSystem.OnUserConnected`), Tick (`ServerBootstrapSystem.OnUpdate`),
  Death (`DeathEventListenerSystem.OnUpdate`).
- `Commands/NpcCommands.cs` — `.npcs help|list|spawn|spawnme|spawnfor|random|loot|reload|debug`.

## Ключевые сигнатуры игрового API (из сборок через Mono.Cecil-дампер)
- `UnitSpawnerUpdateSystem.SpawnUnit(Entity, PrefabGUID, float3, int, float, float, float)`
- `DeathEventListenerSystem._DeathEventQuery.ToComponentDataArray<DeathEvent>()`, `DeathEvent.Died/.Killer`
- `ServerGameManager.TryAddInventoryItem(Entity, PrefabGUID, int)` → `.Success`; фоллбэк `CreateDroppedItemEntity`
- `ServerChatUtils.SendSystemMessageToClient(EntityManager, User, ref FixedString512Bytes)` (рендерит `<color>`)
- `Stunlock.Core.PrefabGUID.GuidHash`
- `ProjectM.CastleBuilding.CastleFloor` (без координат!), `CastleHeart` + `LocalToWorld`
- `ProjectM.BuffUtility.GetBuffInstanceCount(EntityManager, Entity, PrefabGUID)`
- `PrefabCollectionSystem._PrefabDataLookup[guid].AssetName`
- VCF: `ChatCommandContext.Reply/User/Event`, `ctx.User.IsAdmin`, `CommandRegistry.RegisterAll()/Middlewares`

## Правила работы над проектом (мои же грабли)
- Удалять файлы только явными путями — без рекурсивных масок (дважды снёс корень папки).
- Не править файлы через `Get-Content`/`Set-Content` без `-Encoding UTF8` — портит кириллицу
  (один раз испортил комментарии в `Plugin.cs`). Писать файлы только инструментом записи.
- `.ps1`-скрипты держать в ASCII: PowerShell 5.1 без BOM читает UTF-8 как CP1251 и падает на парсинге.

## Дальше (идеи)
- Фильтр по уровню игрока, скейл уровня НПС, лимит одновременных групп.
