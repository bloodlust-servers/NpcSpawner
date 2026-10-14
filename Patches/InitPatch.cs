using System;
using HarmonyLib;
using ProjectM;

namespace NpcSpawner.Patches
{
    /// <summary>Как только мир сервера готов и первый игрок подключился — инициализируем сервис.</summary>
    [HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUserConnected))]
    internal static class InitPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(ServerBootstrapSystem __instance)
        {
            try
            {
                if (!Svc.Ready)
                    Svc.Init(__instance.World);

                SpawnerService.Initialize();
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError(e);
            }
        }
    }
}
