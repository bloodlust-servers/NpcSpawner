using System;
using System.Collections.Generic;
using System.Text;
using ProjectM.Network;
using Unity.Entities;
using VampireCommandFramework;

namespace NpcSpawner.Commands
{
    /// <summary>
    /// Чат-команды мода. Префикс: .npcs (или .npcspawner).
    /// Доступ только для администраторов.
    ///
    /// Важно: чат игры ограничен 512 БАЙТАМИ, а VCF режет ответ по СИМВОЛАМ.
    /// Поэтому длинные ответы отправляем частями и сами считаем байты.
    /// </summary>
    [CommandGroup("npcs", "npcspawner")]
    internal static class NpcCommands
    {
        private const string DenyText = "<color=#ff4d4d>Команда доступна только администраторам.</color>";
        private const int MaxMessageBytes = 300;   // с запасом до лимита чата (512 байт)

        // ------------------------------------------------------------------ утилиты

        /// <summary>true — доступ запрещён (сообщение уже отправлено).</summary>
        private static bool Deny(ChatCommandContext ctx)
        {
            bool isAdmin = false;
            try { isAdmin = ctx.User.IsAdmin; } catch { }
            if (isAdmin) return false;

            ctx.Reply(DenyText);
            return true;
        }

        /// <summary>Безопасная отправка: обрезаем по байтам, а ошибку пишем в лог.</summary>
        private static void Reply(ChatCommandContext ctx, string text)
        {
            try { ctx.Reply(TruncateBytes(text, MaxMessageBytes + 150)); }
            catch (Exception e) { Plugin.Logger.LogError($"[NpcSpawner] reply failed: {e}"); }
        }

        /// <summary>Оборачивает тело команды: любая ошибка — понятное сообщение + запись в лог.</summary>
        private static void Run(ChatCommandContext ctx, string command, Action body)
        {
            try
            {
                body();
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"[NpcSpawner] команда '{command}': {e}");
                Reply(ctx, $"<color=#ff8080>Ошибка команды: {e.Message} (подробности в логе)</color>");
            }
        }

        private static string TruncateBytes(string s, int maxBytes)
        {
            try
            {
                if (string.IsNullOrEmpty(s)) return s;
                if (Encoding.UTF8.GetByteCount(s) <= maxBytes) return s;

                int len = s.Length;
                while (len > 0 && Encoding.UTF8.GetByteCount(s.Substring(0, len)) > maxBytes) len--;
                return s.Substring(0, len);
            }
            catch { return s; }
        }

        /// <summary>Режет длинный перечень на части, каждая влезает в чат.</summary>
        private static List<string> BuildChunks(string header, IReadOnlyList<string> items, int maxBytes)
        {
            var chunks = new List<string>();
            var sb = new StringBuilder(header);
            bool hasItem = false;

            foreach (var item in items)
            {
                string candidate = hasItem ? sb.ToString() + ", " + item : sb.ToString() + item;

                if (Encoding.UTF8.GetByteCount(candidate) > maxBytes && hasItem)
                {
                    chunks.Add(sb.ToString());
                    sb.Clear();
                    sb.Append(item);
                    hasItem = true;
                    continue;
                }

                if (hasItem) sb.Append(", ");
                sb.Append(item);
                hasItem = true;
            }

            if (hasItem || sb.Length > 0) chunks.Add(sb.ToString());
            if (chunks.Count == 0) chunks.Add(header);
            return chunks;
        }

        private static void ReplyList(ChatCommandContext ctx, string header, IReadOnlyList<string> items)
        {
            var chunks = BuildChunks(header, items, MaxMessageBytes);
            for (int i = 0; i < chunks.Count; i++)
            {
                string page = chunks.Count > 1 ? $"({i + 1}/{chunks.Count}) " : string.Empty;
                Reply(ctx, page + chunks[i]);
            }
        }

        /// <summary>Персонаж отправителя. Берём из User (надёжнее, чем SenderCharacterEntity).</summary>
        private static Entity ResolveCharacter(ChatCommandContext ctx)
        {
            try
            {
                Entity userEntity = ctx.Event.SenderUserEntity;
                if (userEntity != Entity.Null && Svc.Ready && Svc.EM.Exists(userEntity) && Svc.EM.HasComponent<User>(userEntity))
                {
                    Entity character = Svc.EM.GetComponentData<User>(userEntity).LocalCharacter._Entity;
                    if (character != Entity.Null && Svc.EM.Exists(character)) return character;
                }
            }
            catch { }

            return ctx.Event.SenderCharacterEntity;
        }

        // ------------------------------------------------------------------ команды

        [Command("help", description: "Справка по командам NPC Spawner", adminOnly: true)]
        public static void Help(ChatCommandContext ctx)
        {
            if (Deny(ctx)) return;
            Run(ctx, "help", () =>
            {
                Reply(ctx, "--- NPC Spawner (только для админов) ---");
                Reply(ctx, ".npcs list — список групп из npcs.json");
                Reply(ctx, ".npcs spawn <группа> — заспавнить у себя");
                Reply(ctx, ".npcs spawnme <группа> — то же самое");
                Reply(ctx, ".npcs spawnfor <ник> <группа> — заспавнить у игрока");
                Reply(ctx, ".npcs random — случайный спавн сейчас");
                Reply(ctx, ".npcs loot <id НПС> — лут для НПС");
                Reply(ctx, ".npcs reload — перечитать конфиги");
                Reply(ctx, ".npcs debug — диагностика");
            });
        }

        [Command("list", description: "Список групп НПС", adminOnly: true)]
        public static void List(ChatCommandContext ctx)
        {
            if (Deny(ctx)) return;
            Run(ctx, "list", () =>
            {
                var names = SpawnerService.GroupNames;
                if (names.Count == 0)
                {
                    Reply(ctx, "Групп нет. Проверь BepInEx/config/NpcSpawner/npcs.json");
                    return;
                }

                ReplyList(ctx, $"Группы ({names.Count}): ", names);
            });
        }

        [Command("spawn", usage: "<группа>", description: "Заспавнить группу НПС рядом с собой", adminOnly: true)]
        public static void Spawn(ChatCommandContext ctx, string group)
        {
            if (Deny(ctx)) return;
            SpawnSelf(ctx, group);
        }

        [Command("spawnme", usage: "<группа>", description: "Заспавнить группу НПС рядом с собой", adminOnly: true)]
        public static void SpawnMe(ChatCommandContext ctx, string group)
        {
            if (Deny(ctx)) return;
            SpawnSelf(ctx, group);
        }

        private static void SpawnSelf(ChatCommandContext ctx, string group)
        {
            Run(ctx, "spawn", () =>
            {
                Plugin.Logger.LogInfo($"[NpcSpawner] команда spawn '{group}' от {SafeName(ctx)}");
                Entity character = ResolveCharacter(ctx);

                if (SpawnerService.TrySpawnForCharacter(character, group, out int spawned, out string error))
                    Reply(ctx, $"<color=#3cff8a>Заспавнено {spawned} НПС из группы '{group}'.</color>");
                else
                    Reply(ctx, $"<color=#ff8080>Не вышло: {error}</color>");
            });
        }

        [Command("spawnfor", usage: "<ник> <группа>", description: "Заспавнить группу НПС у другого игрока", adminOnly: true)]
        public static void SpawnFor(ChatCommandContext ctx, string playerName, string group)
        {
            if (Deny(ctx)) return;
            Run(ctx, "spawnfor", () =>
            {
                Plugin.Logger.LogInfo($"[NpcSpawner] команда spawnfor '{playerName}' '{group}' от {SafeName(ctx)}");

                if (!SpawnerService.TryFindOnlineCharacter(playerName, out Entity character))
                {
                    Reply(ctx, $"<color=#ff8080>Игрок '{playerName}' не в сети.</color>");
                    return;
                }

                if (SpawnerService.TrySpawnForCharacter(character, group, out int spawned, out string error))
                    Reply(ctx, $"<color=#3cff8a>Заспавнено {spawned} НПС из группы '{group}' у {playerName}.</color>");
                else
                    Reply(ctx, $"<color=#ff8080>Не вышло: {error}</color>");
            });
        }

        [Command("random", description: "Случайный спавн как по таймеру (сейчас)", adminOnly: true)]
        public static void Random(ChatCommandContext ctx)
        {
            if (Deny(ctx)) return;
            Run(ctx, "random", () =>
            {
                if (SpawnerService.TrySpawnRandomNow(out string info))
                    Reply(ctx, $"<color=#3cff8a>Случайный спавн: {info}</color>");
                else
                    Reply(ctx, $"<color=#ff8080>Не вышло: {info}</color>");
            });
        }

        [Command("loot", usage: "<id НПС>", description: "Показать настроенный лут для НПС", adminOnly: true)]
        public static void Loot(ChatCommandContext ctx, string npcId)
        {
            if (Deny(ctx)) return;
            Run(ctx, "loot", () =>
            {
                var list = SpawnConfig.GetLoot(npcId);
                if (list == null || list.Count == 0)
                {
                    Reply(ctx, $"Для '{npcId}' лут не настроен (см. BepInEx/config/NpcSpawner/loot.json)");
                    return;
                }

                var parts = new List<string>();
                foreach (var e in list)
                    parts.Add($"{e.ItemId} x{e.Amount} ({e.Chance * 100f:0.#}%)");

                ReplyList(ctx, $"{npcId}: ", parts);
            });
        }

        [Command("reload", description: "Перечитать npcs.json и loot.json без рестарта", adminOnly: true)]
        public static void Reload(ChatCommandContext ctx)
        {
            if (Deny(ctx)) return;
            Run(ctx, "reload", () =>
            {
                SpawnConfig.LoadAll();
                SpawnerService.ReloadCombatBuffs();
                Reply(ctx, $"<color=#3cff8a>Перечитано: групп {SpawnConfig.Groups.Count}, записей лута {SpawnConfig.Loot.Count}.</color>");
            });
        }

        [Command("debug", description: "Диагностика: кто в бою, где найдены замки", adminOnly: true)]
        public static void Debug(ChatCommandContext ctx)
        {
            if (Deny(ctx)) return;
            Run(ctx, "debug", () =>
            {
                foreach (var line in SpawnerService.DebugSnapshot())
                    Reply(ctx, line);
            });
        }

        private static string SafeName(ChatCommandContext ctx)
        {
            try { return ctx.Name; } catch { return "?"; }
        }
    }
}
