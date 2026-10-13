using System;
using ProjectM;
using ProjectM.Scripting;
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

        internal static bool Ready => Server != null && Server.IsCreated;

        internal static void Init(World world)
        {
            Server = world;
            EM = world.EntityManager;
            UnitSpawner = world.GetExistingSystemManaged<UnitSpawnerUpdateSystem>();

            var mapper = world.GetExistingSystemManaged<ServerScriptMapper>();
            GameManager = mapper.GetServerGameManager();
        }
    }
}
