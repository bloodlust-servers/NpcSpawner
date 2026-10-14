using BepInEx.Configuration;

namespace NpcSpawner
{
    /// <summary>
    /// Общие настройки мода.
    /// Файл: BepInEx/config/NpcSpawner/NpcSpawner.cfg
    /// Цветной текст: &lt;color=#ff9d00&gt;текст&lt;/color&gt;
    /// </summary>
    internal static class Settings
    {
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<float> IntervalMin;
        public static ConfigEntry<float> IntervalMax;
        public static ConfigEntry<bool> IntervalScalesWithPlayers;
        public static ConfigEntry<float> LifetimeSeconds;

        public static ConfigEntry<bool> BlockNearCastles;
        public static ConfigEntry<float> CastleBlockRadius;
        public static ConfigEntry<float> CastleHeartRadius;

        public static ConfigEntry<bool> SkipPlayersInCombat;
        public static ConfigEntry<string> CombatBuffGuids;

        public static ConfigEntry<bool> Announce;
        public static ConfigEntry<string> AnnounceMessage;

        public static ConfigEntry<bool> GlobalAnnounce;
        public static ConfigEntry<string> GlobalSpawnMessage;

        public static ConfigEntry<bool> LootEnabled;
        public static ConfigEntry<bool> NotifyKiller;
        public static ConfigEntry<string> NotifyMessage;

        public static ConfigEntry<bool> SuccessAnnounce;
        public static ConfigEntry<string> SuccessMessage;

        public static void Init(ConfigFile cfg)
        {
            Enabled = cfg.Bind("General", "Enabled", true,
                "Включён ли автоматический случайный спавн НПС.");

            IntervalMin = cfg.Bind("General", "IntervalMinSeconds", 300f,
                "Минимальный интервал между случайными спавнами, секунды.");

            IntervalMax = cfg.Bind("General", "IntervalMaxSeconds", 600f,
                "Максимальный интервал между случайными спавнами, секунды.");

            IntervalScalesWithPlayers = cfg.Bind("General", "IntervalScalesWithPlayers", false,
                "Если true — интервал делится на число игроков онлайн (больше игроков -> чаще спавн).");

            LifetimeSeconds = cfg.Bind("General", "LifetimeSeconds", 0f,
                "Сколько секунд живёт заспавненный НПС. 0 или меньше — бессрочно (до убийства).");

            BlockNearCastles = cfg.Bind("Spawn", "BlockNearCastles", true,
                "Запрещать спавн в замках игроков и под крышами (рядом со плитами замка).");

            CastleBlockRadius = cfg.Bind("Spawn", "CastleBlockRadius", 4f,
                "Радиус в метрах вокруг плит замка, где спавн запрещён (при BlockNearCastles = true).");

            CastleHeartRadius = cfg.Bind("Spawn", "CastleHeartRadius", 30f,
                "Радиус в метрах вокруг СЕРДЦА замка, где спавн запрещён (любой этаж). Основная защита от спавна в замках.");

            SkipPlayersInCombat = cfg.Bind("Spawn", "SkipPlayersInCombat", true,
                "Не давать спавнить НПС у игрока, который находится в бою.");

            CombatBuffGuids = cfg.Bind("Spawn", "CombatBuffGuids", "581443919,697095869",
                "Список id баффов 'в бою' через запятую (обычный бой и PvP-бой). Менять только если игра обновила их.");

            // ---- сообщения ----

            Announce = cfg.Bind("Messages", "Announce", true,
                "Сообщать ЛИЧНО игроку, что рядом появилась группа НПС.");

            AnnounceMessage = cfg.Bind("Messages", "AnnounceMessage",
                "<color=#ff9d00>Рядом появилась группа «{0}»: врагов {1}. Убей их ради добычи!</color>",
                "Шаблон личного анонса. {0} — имя группы, {1} — число врагов.");

            GlobalAnnounce = cfg.Bind("Messages", "GlobalAnnounce", true,
                "Сообщать ВСЕМ игрокам, что у кого-то произошёл случайный спавн группы.");

            GlobalSpawnMessage = cfg.Bind("Messages", "GlobalSpawnMessage",
                "<color=#ff5a5a>Случайный спавн! У игрока {0} появилась группа «{1}» — врагов {2}.</color>",
                "Шаблон сообщения всем игрокам о случайном спавне. {0} — ник игрока, {1} — имя группы, {2} — число врагов.");

            // ---- лут ----

            LootEnabled = cfg.Bind("Loot", "Enabled", true,
                "Выдавать настраиваемый лут за убийство НПС (см. loot.json).");

            NotifyKiller = cfg.Bind("Loot", "NotifyKiller", true,
                "Сообщать игроку в чат, какой лут он получил.");

            NotifyMessage = cfg.Bind("Loot", "NotifyMessage",
                "<color=#ffd700>Добыча: {0} шт. (id {1}).</color>",
                "Шаблон сообщения о добыче. {0} — количество, {1} — id предмета.");

            SuccessAnnounce = cfg.Bind("Messages", "SuccessAnnounce", true,
                "Сообщать ВСЕМ игрокам, что игрок справился с задачей (убил всю группу).");

            SuccessMessage = cfg.Bind("Messages", "SuccessMessage",
                "<color=#3cff8a>{0} справился с задачей — группа «{1}» уничтожена!</color>",
                "Шаблон сообщения об успехе. {0} — ник игрока, {1} — имя группы.");
        }
    }
}
