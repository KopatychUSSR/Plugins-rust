using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ZombieSpawn", "TopPlugin.ru", "0.0.1")]
    [Description("TopPlugin.ru")]
    class ZombieSpawn : RustPlugin
    {
        public string Prefab = "assets/prefabs/npc/scarecrow/scarecrow.prefab";

        #region Configuration

        private static Configuration config = new Configuration();
        private class Configuration
        {
            [JsonProperty("Процент с которым будет появлятся зомби,вместо трупа")]
            public int PercentMutation;
            [JsonProperty("С каким интервалом возраждать зомби на РТ")]
            public int SecondRespawn;
            [JsonProperty("Сколько зомби спавнить на карте")]
            public int SpawnAllMapCount;
            [JsonProperty("РТ и КОЛИЧЕСТВО зомби")]
            public Dictionary<string, int> MonumentsListZombie = new Dictionary<string, int>();

            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    SpawnAllMapCount = 30,
                    SecondRespawn = 1200,
                    PercentMutation = 10,
                    MonumentsListZombie = new Dictionary<string, int>
                    {
                        ["lighthouse"] = 1,
                        ["powerplant_1"] = 1,
                        ["military_tunnel_1"] = 1,
                        ["harbor_1"] = 1,
                        ["harbor_2"] = 1,
                        ["airfield_1"] = 1,
                        ["trainyard_1"] = 1,
                        ["water_treatment_plant_1"] = 1,
                        ["warehouse"] = 1,
                        ["satellite_dish"] = 1,
                        ["sphere_tank"] = 1,
                        ["radtown_small_3"] = 1,
                        ["launch_site_1"] = 1,
                        ["gas_station_1"] = 1,
                        ["supermarket_1"] = 1,
                        ["mining_quarry_c"] = 1,
                        ["mining_quarry_a"] = 1,
                        ["mining_quarry_b"] = 1,
                        ["junkyard_1"] = 1
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                LoadDefaultConfig();
            }
            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Hooks
        void OnServerInitialized()
        {
            ParseObjectRT();
            timer.Every(config.SecondRespawn, () =>
            {
                SpawnZombieRT();
                SpawnZombieAllMap();
            });
        }

        void OnPlayerDie(BasePlayer player, HitInfo info)
        {
            if (player != null)
                if (Oxide.Core.Random.Range(0, 100) >= (100 - config.PercentMutation))
                {
                    BaseEntity zombie = new BaseEntity();
                    zombie = GameManager.server.CreateEntity(Prefab, player.transform.position);
                    zombie.Spawn();
                }
        }

        #endregion

        #region Metods
        void MutationCorpse(Vector3 PosCorpse)
        {
            BaseEntity zombie = new BaseEntity();
            zombie = GameManager.server.CreateEntity(Prefab, PosCorpse);
            zombie.Spawn();
        }

        void SpawnZombieAllMap()
        {
            for (int i = 0; i < config.SpawnAllMapCount; i++)
                MutationCorpse(GetRandomPosition());
        }

        void SpawnZombieRT()
        {
            for (int j = 0; j < MonumentsPos.Count; j++)
            {
                var pos = MonumentsPos[j];
                for (int i = 0; i < config.MonumentsListZombie.Count; i++)
                {
                    Vector3 Pos = new Vector3(pos.x + pos.z - pos.y - UnityEngine.Random.Range(0, 30), pos.y, pos.z + pos.y);
                    MutationCorpse(Pos);
                }
            }
        }

        public List<Vector3> MonumentsPos = new List<Vector3>();
        public void ParseObjectRT()
        {
            var allobjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            foreach (var gobject in allobjects)
                if (gobject.name.Contains("autospawn/monument"))
                    for (int i = 0; i < config.MonumentsListZombie.Count; i++)
                        if (gobject.name.Contains(config.MonumentsListZombie.ElementAt(i).Key))
                        {
                            var ParsePos = gobject.transform.position;
                            MonumentsPos.Add(ParsePos);
                        }
        }

        public Vector3 GetRandomPosition()
        {
            var originPosition = Vector3.zero;

            for (int i = 0; i < 150; i++)
            {
                var newPosition = Vector3.zero;

                newPosition.x += Oxide.Core.Random.Range(-World.Size / 2f, World.Size / 2f);
                newPosition.z += Oxide.Core.Random.Range(-World.Size / 2f, World.Size / 2f);
                newPosition.y = TerrainMeta.HeightMap.GetHeight(newPosition);

                var colliders = new List<Collider>();
                Vis.Colliders(newPosition, 100, colliders);

                if (colliders.Where(col => col.name.ToLower().Contains("prevent") && col.name.ToLower().Contains("building")).Count() > 0)
                    continue;

                if (WaterLevel.Test(newPosition + new Vector3(0, 0.1f, 0)))
                    continue;

                var entities = new List<BaseEntity>();
                Vis.Entities(newPosition, 100, entities);
                if (entities.Where(ent => ent is BaseVehicle || ent is CargoShip || ent is BaseHelicopter || ent is BradleyAPC).Count() > 0)
                    continue;

                if (newPosition.y < 0)
                    continue;

                RaycastHit hitInfo;
                if (UnityEngine.Physics.Raycast(newPosition, Vector3.up, out hitInfo) || UnityEngine.Physics.Raycast(newPosition, Vector3.down, out hitInfo))
                {
                    if (hitInfo.GetEntity() == null)
                    {
                        return newPosition;
                    }
                }
                else continue;
            }

            return originPosition;
        }
        #endregion
    }
}
