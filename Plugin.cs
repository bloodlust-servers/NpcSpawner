using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using VampireCommandFramework;

namespace NpcSpawner
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(VcfGuid)]
    public class Plugin : BasePlugin
    {
        public const string PluginGuid = "agenthere.npcspawner";
        public const string PluginName = "NPC Spawner";
        public const string PluginVersion = "1.7.1";
        public const string VcfGuid = "gg.deca.VampireCommandFramework";

        internal static Plugin Instance;
        internal static Harmony Harmony;
        internal static ManualLogSource Logger;

        public override void Load()
        {
            Instance = this;
            Logger = base.Log;

            Logger.LogInfo($"[NpcSpawner] {PluginName} v{PluginVersion} loading...");

            // 1) настройки — файл .cfg кладём внутрь BepInEx/config/NpcSpawner/
            try
            {
                Directory.CreateDirectory(SpawnConfig.Dir);
                var cfgFile = new ConfigFile(Path.Combine(SpawnConfig.Dir, "NpcSpawner.cfg"), true);
                Settings.Init(cfgFile);
            }
            catch (Exception e) { Logger.LogError($"[NpcSpawner] settings: {e}"); }

            // 2) конфиги npcs.json / loot.json
            try
            {
                SpawnConfig.LoadAll();
                Logger.LogInfo($"[NpcSpawner] config dir: {SpawnConfig.Dir}");
                Logger.LogInfo($"[NpcSpawner] configs: npcs={SpawnConfig.Groups.Count} groups, loot={SpawnConfig.Loot.Count} entries");
            }
            catch (Exception e) { Logger.LogError($"[NpcSpawner] config: {e}"); }

            // 3) Harmony-патчи
            try
            {
                Harmony = new Harmony(PluginGuid);
                Harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception e) { Logger.LogError($"[NpcSpawner] patches failed: {e}"); }

            // 4) чат-команды + включаем у VCF проверку «только для админов»
            try
            {
                CommandRegistry.Middlewares.Add(new VCF.Core.Basics.BasicAdminCheck());
                Logger.LogInfo("[NpcSpawner] VCF admin-only middleware installed.");
            }
            catch (Exception e) { Logger.LogWarning($"[NpcSpawner] middleware not installed: {e.Message}"); }

            try
            {
                CommandRegistry.RegisterAll();
                Logger.LogInfo("[NpcSpawner] chat commands registered (.npcs help)");
            }
            catch (Exception e) { Logger.LogError($"[NpcSpawner] commands failed: {e}"); }

            Logger.LogInfo($"[NpcSpawner] {PluginName} v{PluginVersion} loaded.");
        }

        public override bool Unload()
        {
            try { SpawnerService.Shutdown(); }
            catch (Exception e) { Logger.LogError(e); }

            try { CommandRegistry.UnregisterAssembly(); }
            catch (Exception e) { Logger.LogError(e); }

            try { Harmony?.UnpatchSelf(); }
            catch (Exception e) { Logger.LogError(e); }

            return true;
        }
    }
}
