using System;
using HarmonyLib;
using ProjectM;

namespace NpcSpawner.Patches
{
    /// <summary>События смертей на сервере — отсюда выдаём лут и сообщение об успехе.</summary>
    [HarmonyPatch(typeof(DeathEventListenerSystem), nameof(DeathEventListenerSystem.OnUpdate))]
    internal static class DeathPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(DeathEventListenerSystem __instance)
        {
            try { SpawnerService.OnDeath(__instance); }
            catch (Exception e) { Plugin.Logger.LogError(e); }
        }
    }
}
