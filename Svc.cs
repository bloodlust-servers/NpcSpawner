using System;
using ProjectM;
using ProjectM.Scripting;
using Stunlock.Core;
using Unity.Entities;

namespace NpcSpawner
{
    /// <summary>Ссылки на мир сервера и нужные игровые системы (после инициализации мира).</summary>
    internal static class Svc
    {
        internal static World Server;
        internal static EntityManager EM;
        internal static UnitSpawnerUpdateSystem UnitSpawner;
        internal static ServerGameManager GameManager;
        internal static PrefabCollectionSystem Prefabs;

        internal static bool Ready => Server != null && Server.IsCreated;

        internal static void Init(World world)
        {
            Server = world;
            EM = world.EntityManager;
            UnitSpawner = world.GetExistingSystemManaged<UnitSpawnerUpdateSystem>();

            var mapper = world.GetExistingSystemManaged<ServerScriptMapper>();
            GameManager = mapper.GetServerGameManager();

            try { Prefabs = world.GetExistingSystemManaged<PrefabCollectionSystem>(); }
            catch { Prefabs = null; }
        }

        /// <summary>Имя префаба по его id (для логов и диагностики). null — если не найден.</summary>
        internal static string PrefabName(int guid)
        {
            try
            {
                if (Prefabs == null) return null;
                var name = Prefabs._PrefabDataLookup[new PrefabGUID(guid)].AssetName;
                return name.ToString();
            }
            catch { return null; }
        }
    }
}
