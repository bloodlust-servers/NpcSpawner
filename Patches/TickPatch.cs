using System;
using HarmonyLib;
using ProjectM;

namespace NpcSpawner.Patches
{
    /// <summary>
    /// ServerBootstrapSystem обновляется каждый кадр сервера (это видно в логе игры),
    /// поэтому используем его как надёжный «пульс» мода.
    /// </summary>
    [HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUpdate))]
    internal static class TickPatch
    {
        [HarmonyPostfix]
        internal static void Postfix()
        {
            try { SpawnerService.Tick(); }
            catch (Exception e) { Plugin.Logger.LogError(e); }
        }
    }
}
