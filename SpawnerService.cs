using System;
using System.Collections.Generic;
using ProjectM;
using ProjectM.CastleBuilding;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace NpcSpawner
{
    /// <summary>
    /// Спавн НПС случайной группы вокруг случайного игрока в случайное время + лут за убийство.
    ///
    /// ВАЖНО про механику:
    /// - таймер считается по системным часам (DateTime.UtcNow), а не по игровому Time;
    /// - заспавненные НПС находятся «сканированием» рядом с точкой спавна через секунду после спавна
    ///   (не зависим от внутренних систем игры, которые могут не тикать).
    /// </summary>
    internal static class SpawnerService
    {
        private static readonly System.Random Rng = new System.Random();

        /// <summary>Одна «волна» спавна: группа, её игрок и ещё живые НПС.</summary>
        internal sealed class Batch
        {
            public string GroupName;
            public string PlayerName;
            public readonly List<Entity> Alive = new List<Entity>();
        }

        private sealed class Spawned
        {
            public string GuidKey;
            public Batch Batch;
        }

        /// <summary>Отложенный поиск только что заспавненных НПС (по id и близости к точке).</summary>
        private sealed class Pickup
        {
            public int Guid;
            public string GuidKey;
            public Batch Batch;
            public float3 Center;
            public float Radius;
            public DateTime DueAt;
        }

        private static readonly Dictionary<Entity, Spawned> Tracked = new Dictionary<Entity, Spawned>();
        private static readonly List<Pickup> _pickups = new List<Pickup>();
        private static readonly List<int> _combatBuffs = new List<int>();

        private static DateTime _nextSpawnAt = DateTime.MinValue;
        private static bool _initialized;
        private static EntityQuery _userQuery;
        private static EntityQuery _prefabQuery;
        private static EntityQuery _castleFloorQuery;
        private static bool _queriesCreated;

        // ------------------------------------------------------------ жизненный цикл

        internal static void Initialize()
        {
            if (_initialized || !Svc.Ready) return;
            _initialized = true;

            SpawnConfig.LoadAll();

            _userQuery = Svc.EM.CreateEntityQuery(ComponentType.ReadOnly<User>());
            _prefabQuery = Svc.EM.CreateEntityQuery(ComponentType.ReadOnly<PrefabGUID>());
            _castleFloorQuery = Svc.EM.CreateEntityQuery(
                ComponentType.ReadOnly<CastleFloor>(),
                ComponentType.ReadOnly<LocalToWorld>());
            _queriesCreated = true;

            ReloadCombatBuffs();
            ScheduleNext("init");

            Plugin.Logger.LogInfo($"[NpcSpawner] ready. groups={SpawnConfig.Groups.Count}, loot={SpawnConfig.Loot.Count}, enabled={Settings.Enabled.Value}");
        }

        internal static void Shutdown()
        {
            try
            {
                if (_queriesCreated && Svc.Ready)
                {
                    _userQuery.Dispose();
                    _prefabQuery.Dispose();
                    _castleFloorQuery.Dispose();
                }
            }
            catch { }

            _queriesCreated = false;
            Tracked.Clear();
            _pickups.Clear();
            _initialized = false;
        }

        // ---------------------------------------------------------------- тик

        /// <summary>Вызывается каждый кадр сервера (патч на ServerBootstrapSystem.OnUpdate).</summary>
        internal static void Tick()
        {
            if (!Svc.Ready) return;

            if (!_initialized)
            {
                Initialize();
                return;
            }

            CollectPickups();
            PruneTracked();

            if (!Settings.Enabled.Value) return;

            DateTime now = DateTime.UtcNow;
            if (now < _nextSpawnAt) return;

            ScheduleNext("auto");
            try { SpawnRandomGroup(); }
            catch (Exception e) { Plugin.Logger.LogError(e); }
        }

        private static void ScheduleNext(string reason)
        {
            float min = Math.Max(5f, Settings.IntervalMin.Value);
            float max = Math.Max(min, Settings.IntervalMax.Value);
            double interval = min + Rng.NextDouble() * (max - min);

            if (Settings.IntervalScalesWithPlayers.Value)
            {
                var players = GetPlayers();
                interval /= Math.Max(1, players.Count);
            }

            _nextSpawnAt = DateTime.UtcNow.AddSeconds(interval);
            Plugin.Logger.LogInfo($"[NpcSpawner] следующий спавн через {interval:0} сек ({reason}).");
        }

        /// <summary>Через ~1.5 сек после спавна находим появившихся НПС рядом с точкой спавна.</summary>
        private static void CollectPickups()
        {
            if (_pickups.Count == 0) return;

            DateTime now = DateTime.UtcNow;
            for (int i = _pickups.Count - 1; i >= 0; i--)
            {
                var p = _pickups[i];
                if (now < p.DueAt) continue;

                _pickups.RemoveAt(i);
                try { CollectOne(p); }
                catch (Exception e) { Plugin.Logger.LogError(e); }
            }
        }

        private static void CollectOne(Pickup p)
        {
            var arr = _prefabQuery.ToEntityArray(Allocator.Temp);
            try
            {
                float limit = (p.Radius + 6f) * (p.Radius + 6f);

                foreach (var e in arr)
                {
                    if (Tracked.ContainsKey(e)) continue;

                    PrefabGUID pg;
                    try { pg = Svc.EM.GetComponentData<PrefabGUID>(e); }
                    catch { continue; }

                    if (pg.GuidHash != p.Guid) continue;
                    if (!TryGetPosition(e, out var pos)) continue;

                    float dx = pos.x - p.Center.x;
                    float dz = pos.z - p.Center.z;
                    if (dx * dx + dz * dz > limit) continue;

                    Tracked[e] = new Spawned { GuidKey = p.GuidKey, Batch = p.Batch };
                    p.Batch?.Alive.Add(e);
                }
            }
            finally { arr.Dispose(); }
        }

        private static void PruneTracked()
        {
            if (Tracked.Count == 0) return;

            List<Entity> gone = null;
            foreach (var e in Tracked.Keys)
            {
                if (!Svc.EM.Exists(e))
                {
                    if (gone == null) gone = new List<Entity>();
                    gone.Add(e);
                }
            }

            if (gone == null) return;

            foreach (var e in gone)
            {
                if (Tracked.TryGetValue(e, out var sp))
                    sp.Batch?.Alive.Remove(e);
                Tracked.Remove(e);
            }
        }

        // ------------------------------------------------------------- игроки

        private struct PlayerRef
        {
            public Entity UserEntity;
            public Entity Character;
            public float3 Position;
        }

        private static List<PlayerRef> GetPlayers()
        {
            var result = new List<PlayerRef>();
            if (!Svc.Ready || !_queriesCreated) return result;

            var arr = _userQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var userEntity in arr)
                {
                    User user;
                    try { user = Svc.EM.GetComponentData<User>(userEntity); }
                    catch { continue; }

                    if (!user.IsConnected || user.IsBot) continue;

                    Entity character = user.LocalCharacter._Entity;
                    if (character == Entity.Null || !Svc.EM.Exists(character)) continue;
                    if (!TryGetPosition(character, out var pos)) continue;

                    result.Add(new PlayerRef { UserEntity = userEntity, Character = character, Position = pos });
                }
            }
            finally { arr.Dispose(); }

            return result;
        }

        private static bool TryGetPosition(Entity e, out float3 pos)
        {
            if (Svc.EM.HasComponent<LocalToWorld>(e))
            {
                pos = Svc.EM.GetComponentData<LocalToWorld>(e).Position;
                return true;
            }
            if (Svc.EM.HasComponent<Translation>(e))
            {
                pos = Svc.EM.GetComponentData<Translation>(e).Value;
                return true;
            }
            pos = default;
            return false;
        }

        /// <summary>Игрок сейчас в бою? Проверяем по баффам «в бою» (обычный и PvP).</summary>
        internal static bool IsInCombat(Entity character)
        {
            if (!Svc.Ready || character == Entity.Null) return false;

            foreach (int guid in _combatBuffs)
            {
                try
                {
                    if (BuffUtility.GetBuffInstanceCount(Svc.EM, character, new PrefabGUID(guid)) > 0)
                        return true;
                }
                catch { }
            }
            return false;
        }

        /// <summary>Перечитать список баффов «в бою» из настроек.</summary>
        internal static void ReloadCombatBuffs()
        {
            _combatBuffs.Clear();
            try
            {
                foreach (var part in (Settings.CombatBuffGuids.Value ?? string.Empty).Split(','))
                    if (int.TryParse(part.Trim(), out int guid))
                        _combatBuffs.Add(guid);
            }
            catch { }
        }

        /// <summary>Игроки, у которых можно спавнить (онлайн и, если включено, не в бою).</summary>
        private static List<PlayerRef> GetSpawnablePlayers()
        {
            var all = GetPlayers();
            if (!Settings.SkipPlayersInCombat.Value) return all;

            var result = new List<PlayerRef>();
            foreach (var p in all)
                if (!IsInCombat(p.Character))
                    result.Add(p);

            return result;
        }

        // ------------------------------------------------------------- спавн

        private static void SpawnRandomGroup()
        {
            var players = GetSpawnablePlayers();
            if (players.Count == 0)
            {
                Plugin.Logger.LogInfo("[NpcSpawner] нет подходящих игроков (онлайн и не в бою) — ждём.");
                return;
            }

            var groupKeys = GroupNames;
            if (groupKeys.Count == 0)
            {
                Plugin.Logger.LogWarning("[NpcSpawner] в npcs.json нет ни одной группы с Prefabs.");
                return;
            }

            string groupName = groupKeys[Rng.Next(groupKeys.Count)];
            SpawnGroup selected = SpawnConfig.Groups[groupName];

            // пробуем нескольких разных игроков: у кого точка не в замке/под крышей
            int attempts = Math.Min(3, players.Count);
            for (int i = 0; i < attempts; i++)
            {
                var player = players[Rng.Next(players.Count)];
                string name = SafeUserName(player.UserEntity);

                int count = SpawnGroupFor(player.UserEntity, player.Character, player.Position, groupName, selected, true);
                if (count <= 0) continue;

                Plugin.Logger.LogInfo($"[NpcSpawner] группа '{groupName}': заспавнено {count} НПС у игрока {name}.");

                if (Settings.GlobalAnnounce.Value)
                    Broadcast(string.Format(Settings.GlobalSpawnMessage.Value, name, groupName, count));

                return;
            }

            Plugin.Logger.LogInfo("[NpcSpawner] не нашлось свободного места (замки/крыши) — пропускаем спавн.");
        }

        internal static int SpawnGroupFor(Entity userEntity, Entity character, float3 center, string groupName, SpawnGroup group, bool announce)
        {
            int spawned = 0;

            float lifetime = Settings.LifetimeSeconds.Value;
            if (lifetime <= 0f) lifetime = -1f; // <=0 -> бессрочно

            var batch = new Batch
            {
                GroupName = groupName,
                PlayerName = SafeUserName(userEntity),
            };

            foreach (var kv in group.Prefabs)
            {
                if (kv.Value <= 0) continue;

                if (!int.TryParse(kv.Key, out int guid))
                {
                    Plugin.Logger.LogWarning($"[NpcSpawner] некорректный id НПС: '{kv.Key}'.");
                    continue;
                }

                for (int i = 0; i < kv.Value; i++)
                {
                    // ищем точку, которая НЕ в замке и НЕ под крышей
                    if (!TryFindFreePosition(center, group.RandomAround, out float2 flat))
                        break;

                    if (SpawnOne(character, guid, kv.Key, flat, lifetime)) spawned++;
                }

                // запомним: через ~1.5 сек найти появившихся НПС этого id рядом с игроком
                _pickups.Add(new Pickup
                {
                    Guid = guid,
                    GuidKey = kv.Key,
                    Batch = batch,
                    Center = center,
                    Radius = Math.Max(1f, group.RandomAround),
                    DueAt = DateTime.UtcNow.AddSeconds(1.5),
                });
            }

            if (spawned > 0 && announce && Settings.Announce.Value)
                SendToUser(userEntity, string.Format(Settings.AnnounceMessage.Value, groupName, spawned));

            return spawned;
        }

        private static bool SpawnOne(Entity character, int guid, string guidKey, float2 flat, float lifetime)
        {
            try
            {
                float y = TryGetPosition(character, out var p) ? p.y : 0f;
                var position = new float3(flat.x, y, flat.y);

                // min/max range = 1 => разброс берём на себя, точку задаём сами
                Svc.UnitSpawner.SpawnUnit(Entity.Null, new PrefabGUID(guid), position, 1, 1f, 1f, lifetime);
                return true;
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"[NpcSpawner] не удалось заспавнить НПС {guid}: {e.Message}");
                return false;
            }
        }

        private static float2 RandomAround(float3 center, float radius)
        {
            if (radius < 1f) radius = 1f;

            double angle = Rng.NextDouble() * Math.PI * 2.0;
            double r = Math.Sqrt(Rng.NextDouble()) * radius;

            return new float2(
                center.x + (float)(Math.Cos(angle) * r),
                center.z + (float)(Math.Sin(angle) * r));
        }

        /// <summary>Подбирает точку вокруг игрока, которая не в замке и не под крышей.</summary>
        private static bool TryFindFreePosition(float3 center, float radius, out float2 flat)
        {
            for (int attempt = 0; attempt < 15; attempt++)
            {
                float2 candidate = RandomAround(center, radius);
                var world = new float3(candidate.x, center.y, candidate.y);

                if (!IsInsideCastleOrRoof(world))
                {
                    flat = candidate;
                    return true;
                }
            }

            flat = default;
            return false;
        }

        /// <summary>
        /// Замок/крыша: рядом (по горизонтали) есть плита замка — CastleFloor.
        /// Крыша в V Rising существует только над плитами замка, поэтому одна проверка закрывает оба случая.
        /// </summary>
        private static bool IsInsideCastleOrRoof(float3 pos)
        {
            if (!Settings.BlockNearCastles.Value) return false;
            if (!_queriesCreated) return false;

            float r = Math.Max(1f, Settings.CastleBlockRadius.Value);
            float r2 = r * r;

            var arr = _castleFloorQuery.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var e in arr)
                {
                    float3 p;
                    try { p = Svc.EM.GetComponentData<LocalToWorld>(e).Position; }
                    catch { continue; }

                    float dx = p.x - pos.x;
                    float dz = p.z - pos.z;
                    if (dx * dx + dz * dz > r2) continue;
                    if (Math.Abs(p.y - pos.y) > 12f) continue;   // близкая высота (этаж/крыша)

                    return true;
                }
            }
            finally { arr.Dispose(); }

            return false;
        }

        // -------------------------------------------------------------- лут

        internal static void OnDeath(DeathEventListenerSystem system)
        {
            if (Tracked.Count == 0) return;

            EntityManager em = system.EntityManager;
            var events = system._DeathEventQuery.ToComponentDataArray<DeathEvent>(Allocator.Temp);
            try
            {
                foreach (var death in events)
                {
                    Entity died = death.Died;
                    if (!Tracked.TryGetValue(died, out var sp)) continue;
                    Tracked.Remove(died);
                    sp.Batch?.Alive.Remove(died);

                    Entity killer = death.Killer;
                    if (killer == Entity.Null || !em.HasComponent<PlayerCharacter>(killer)) continue;

                    if (Settings.LootEnabled.Value)
                        DropLoot(em, killer, sp.GuidKey);

                    if (sp.Batch != null && sp.Batch.Alive.Count == 0 && Settings.SuccessAnnounce.Value)
                        Broadcast(string.Format(Settings.SuccessMessage.Value, sp.Batch.PlayerName, sp.Batch.GroupName));
                }
            }
            finally { events.Dispose(); }
        }

        private static void DropLoot(EntityManager em, Entity killer, string npcGuid)
        {
            List<LootEntry> loot = SpawnConfig.GetLoot(npcGuid);
            if (loot == null || loot.Count == 0) return;

            int total = 0;
            int lastId = 0;

            foreach (LootEntry entry in loot)
            {
                if (entry.ItemId == 0) continue;
                if (entry.Chance < 1f && Rng.NextDouble() > entry.Chance) continue;

                int amount = entry.Amount > 0 ? entry.Amount : 1;
                var item = new PrefabGUID(entry.ItemId);

                bool given = false;
                try { given = Svc.GameManager.TryAddInventoryItem(killer, item, amount).Success; }
                catch (Exception e) { Plugin.Logger.LogError(e); }

                if (!given)
                {
                    try { Svc.GameManager.CreateDroppedItemEntity(killer, item, amount); }
                    catch (Exception e) { Plugin.Logger.LogError(e); }
                }

                total += amount;
                lastId = entry.ItemId;
            }

            if (total > 0 && Settings.NotifyKiller.Value)
            {
                try
                {
                    Entity userEntity = em.GetComponentData<PlayerCharacter>(killer).UserEntity;
                    SendToUser(userEntity, string.Format(Settings.NotifyMessage.Value, total, lastId));
                }
                catch (Exception e)
                {
                    Plugin.Logger.LogDebug($"[NpcSpawner] сообщение о добыче не отправлено: {e.Message}");
                }
            }
        }

        // -------------------------------------------------- помощники для команд

        internal static System.Collections.Generic.IReadOnlyList<string> GroupNames
        {
            get
            {
                var list = new System.Collections.Generic.List<string>();
                if (SpawnConfig.Groups != null)
                    foreach (var kv in SpawnConfig.Groups)
                        if (kv.Value != null && kv.Value.Prefabs != null && kv.Value.Prefabs.Count > 0)
                            list.Add(kv.Key);
                return list;
            }
        }

        internal static bool TrySpawnForCharacter(Entity character, string groupName, out int spawned, out string error)
        {
            spawned = 0;
            error = null;

            if (!Svc.Ready) { error = "мод ещё не готов (мир не загружен)"; return false; }
            if (string.IsNullOrWhiteSpace(groupName)) { error = "не указана группа"; return false; }
            if (SpawnConfig.Groups == null || !SpawnConfig.Groups.TryGetValue(groupName, out var group))
            { error = $"группа '{groupName}' не найдена — смотри .npcs list"; return false; }
            if (character == Entity.Null || !Svc.EM.Exists(character)) { error = "персонаж не найден"; return false; }
            if (!TryGetPosition(character, out var pos)) { error = "не удалось определить позицию игрока"; return false; }
            if (Settings.SkipPlayersInCombat.Value && IsInCombat(character)) { error = "игрок сейчас в бою — спавн запрещён"; return false; }

            Entity userEntity = Entity.Null;
            try
            {
                if (Svc.EM.HasComponent<PlayerCharacter>(character))
                    userEntity = Svc.EM.GetComponentData<PlayerCharacter>(character).UserEntity;
            }
            catch { }

            spawned = SpawnGroupFor(userEntity, character, pos, groupName, group, true);
            if (spawned <= 0)
            {
                error = "рядом нет свободного места — тут замок или крыша";
                return false;
            }
            return true;
        }

        internal static bool TryFindOnlineCharacter(string name, out Entity character)
        {
            character = Entity.Null;
            if (string.IsNullOrWhiteSpace(name)) return false;

            foreach (var p in GetPlayers())
            {
                if (string.Equals(SafeUserName(p.UserEntity), name, StringComparison.OrdinalIgnoreCase))
                {
                    character = p.Character;
                    return true;
                }
            }
            return false;
        }

        internal static bool TrySpawnRandomNow(out string info)
        {
            info = null;

            var players = GetSpawnablePlayers();
            if (players.Count == 0) { info = "нет игроков онлайн (или все в бою)"; return false; }

            var names = GroupNames;
            if (names.Count == 0) { info = "нет групп в npcs.json"; return false; }

            var player = players[Rng.Next(players.Count)];
            string groupName = names[Rng.Next(names.Count)];
            int count = SpawnGroupFor(player.UserEntity, player.Character, player.Position, groupName, SpawnConfig.Groups[groupName], true);

            info = $"группа '{groupName}' ({count} НПС) у игрока {SafeUserName(player.UserEntity)}";
            ScheduleNext("manual");
            return true;
        }

        // ------------------------------------------------------------ утилиты

        private static void SendToUser(Entity userEntity, string message)
        {
            try
            {
                if (userEntity == Entity.Null || !Svc.EM.Exists(userEntity)) return;
                var user = Svc.EM.GetComponentData<User>(userEntity);
                FixedString512Bytes text = message;
                ServerChatUtils.SendSystemMessageToClient(Svc.EM, user, ref text);
            }
            catch (Exception e)
            {
                Plugin.Logger.LogDebug($"[NpcSpawner] сообщение не отправлено: {e.Message}");
            }
        }

        private static void Broadcast(string message)
        {
            foreach (var p in GetPlayers())
                SendToUser(p.UserEntity, message);
        }

        private static string SafeUserName(Entity userEntity)
        {
            try
            {
                if (userEntity == Entity.Null || !Svc.EM.Exists(userEntity)) return "?";
                return Svc.EM.GetComponentData<User>(userEntity).CharacterName.ToString();
            }
            catch { return "?"; }
        }
    }
}
