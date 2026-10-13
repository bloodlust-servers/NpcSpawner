using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using BepInEx;

namespace NpcSpawner
{
    /// <summary>
    /// Загрузка пользовательских JSON-конфигов из BepInEx/config/NpcSpawner/.
    /// Если файла нет — распаковываем вшитый в dll дефолт.
    /// </summary>
    internal static class SpawnConfig
    {
        internal static readonly string Dir = Path.Combine(Paths.ConfigPath, "NpcSpawner");
        internal static readonly string NpcsFile = Path.Combine(Dir, "npcs.json");
        internal static readonly string LootFile = Path.Combine(Dir, "loot.json");

        internal static Dictionary<string, SpawnGroup> Groups { get; private set; } = new Dictionary<string, SpawnGroup>();
        internal static Dictionary<string, List<LootEntry>> Loot { get; private set; } = new Dictionary<string, List<LootEntry>>();

        internal static JsonSerializerOptions Options()
        {
            return new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                WriteIndented = true,
            };
        }

        internal static void LoadAll()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                EnsureFile(NpcsFile, "NpcSpawner.npcs.default.json", "{ }");
                EnsureFile(LootFile, "NpcSpawner.loot.default.json", "{ }");

                Groups = LoadGroups();
                Loot = LoadLoot();
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"[NpcSpawner] ошибка загрузки конфигов: {e}");
            }
        }

        private static Dictionary<string, SpawnGroup> LoadGroups()
        {
            try
            {
                var json = File.ReadAllText(NpcsFile);
                var result = JsonSerializer.Deserialize<Dictionary<string, SpawnGroup>>(json, Options());
                return result ?? new Dictionary<string, SpawnGroup>();
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"[NpcSpawner] npcs.json не прочитан: {e.Message}");
                return new Dictionary<string, SpawnGroup>();
            }
        }

        private static Dictionary<string, List<LootEntry>> LoadLoot()
        {
            try
            {
                var json = File.ReadAllText(LootFile);
                var result = JsonSerializer.Deserialize<Dictionary<string, List<LootEntry>>>(json, Options());
                return result ?? new Dictionary<string, List<LootEntry>>();
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"[NpcSpawner] loot.json не прочитан: {e.Message}");
                return new Dictionary<string, List<LootEntry>>();
            }
        }

        /// <summary>Лут по id НПС (ключ — строка PrefabGUID).</summary>
        internal static List<LootEntry> GetLoot(string npcGuid)
        {
            if (npcGuid != null && Loot.TryGetValue(npcGuid, out var list))
                return list;
            return null;
        }

        private static void EnsureFile(string path, string resourceName, string fallback)
        {
            if (File.Exists(path))
                return;

            var content = ReadResource(resourceName) ?? fallback;
            File.WriteAllText(path, content);
            Plugin.Logger.LogInfo($"[NpcSpawner] создан файл по умолчанию: {path}");
        }

        private static string ReadResource(string name)
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                using (var stream = asm.GetManifestResourceStream(name))
                {
                    if (stream == null)
                        return null;
                    using (var reader = new StreamReader(stream))
                        return reader.ReadToEnd();
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
