using System.Text;
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System.Linq;
using Oxide.Core.Plugins;
using Oxide.Core;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("IQBoss", "BadMandarin & Mercury", "1.5.14")]
    [Description("A great event for your players")]
    public class IQBoss : RustPlugin
    {

        
        
        private Configuration.NPCSettings GetRandomPattern() => _config.HelpersPreset.GetRandom(); 

        private MonumentInfo FindClosestMonument(Vector3 position)
        {
            MonumentInfo info = null;
            float minDistance = float.MaxValue;
            var monuments = TerrainMeta.Path.Monuments;

            foreach (var monument in monuments)
            {
                float distance = Vector3.Distance(position, monument.transform.position);
                if (distance > minDistance)
                    continue;

                minDistance = distance;
                info = monument;
            }

            return info;
        }

        private const ulong CRATEID = 600151799;
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["SPAWNED_BOSS"] = "<color=#f25b4b>Survivors, attention!</color>\nAn extremely dangerous boss has appeared on <color=#6ebfe5>{0}</color>, you will need a lot of preparation to defeat him!",
                ["DISTANCE_BIG"] = "You can't hurt the boss like that!\nHe directed the burning at you!\n<color=#f25b4b>Come closer!</color>!",
		   		 		  						  	   		  	   		  	 				  	  			  	   
		   		 		  						  	   		  	   		  	 				  	  			  	   
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["SPAWNED_BOSS"] = "<color=#f25b4b>Выжившие, внимание!</color>\nНа <color=#6ebfe5>{0}</color> появился крайне опасный босс, чтобы сразить его вам потребуется большая подготовка!",
                ["DISTANCE_BIG"] = "Вы не сможете поранить так босса!\nОн направил на вас обжигание!\n<color=#f25b4b>Подойдите ближе!</color>!",

            }, this, "ru");
            PrintWarning(LanguageEn ? "The language file was uploaded successfully" : "Языковой файл загружен успешно");
        }
        private void OnEntityTakeDamage(BaseEntity npc, HitInfo info) 
        {
            if (npc == null || info == null) return;
            BasePlayer player = info.InitiatorPlayer;
            if (player == null) return;
            if (!isPluginNpc(npc)) return;

            if (_config.GeneralPlugin.ControllerLocationPlayerSetting.UseTeleportPlayer)
            {
                if (!DamagesListPlayers.Contains(player))
                    DamagesListPlayers.Add(player);
            }

            if ((Vector3.Distance(_event.Position, npc.transform.position) + 10f >=
                 _config.BossPreset.ChaseRange) ||
                (Vector3.Distance(player.transform.position, npc.transform.position) + 10f >=
                 _config.BossPreset.RadiusVisBots))
            {
                BaseEntity entity = GameManager.server.CreateEntity(Fireballs.GetRandom(), player.transform.position);
                entity.Spawn();
                SendChat(GetLang("DISTANCE_BIG", player.UserIDString), player);
            }
		   		 		  						  	   		  	   		  	 				  	  			  	   
            SendAttackPatrol(_event.Position, player);
        }

        
        
        private static Configuration _config;

        private void OnEntityDeath(ScientistNPC npc, HitInfo info) ///
        {
            if (isPluginNpc(npc))
            {
                BasePlayer player = info.InitiatorPlayer;
                if (player == null)
                    return;
                
                if (npc.net != null) _bots.Remove(npc.net.ID.Value);
                _event.OnNpcDeath(npc, player);

                Interface.Call("OnNpcKilled", npc, player);

                switch (npc.OwnerID)
                {
                    case BOTID:
                        Interface.Call("OnBossKilled", npc, player);
                        break;
                    case HELPERID:
                        Interface.Call("OnHelperKilled", npc, player);
                        break;
                }
            }
        }
		   		 		  						  	   		  	   		  	 				  	  			  	   
        private object CanHelicopterTarget(PatrolHelicopterAI heli, BasePlayer target)
        {
            if (isPluginNpc(target))
                return false;

            return null;
        }

        private string GetShortMonumentName(string fullName)
        {
            var split = fullName.Split('/');
            return split[split.Length - 1].Replace(".prefab", string.Empty);
        }

                private class ActConf
        {
            [JsonProperty(LanguageEn ? "Specify one of the event types : Smoke/RocketsRain/FireRain" : "Укажите один из типов событий : Smoke/RocketsRain/FireRain")]
            [JsonConverter(typeof(StringEnumConverter))]
            public ActType Type;

            [JsonProperty(LanguageEn ? "Event range" : "Радиус действия события")]
            public Single Radius;
            [JsonProperty(LanguageEn ? "Event action distance" : "Дистанция действия события")]
            public Single Distance;
            [JsonProperty(LanguageEn ? "Cooldown before the event action" : "Перезарядка перед действием события")]
            public Single Cooldown;

            public void Get(Vector3 pos, HumanNPC player = null)
            {
                switch (Type)
                {
                    case ActType.Smoke:
                        {
                            var length = 2 * Math.PI * Radius;

                            var elements = (int)(length / Distance);

                            for (var i = 0; i < elements; i++)
                            {
                                var position = RandomCircle(360f / elements * i, pos, Radius);

                                var grenade = GameManager.server.CreateEntity(
                                    "assets/prefabs/tools/smoke grenade/grenade.smoke.deployed.prefab", position,
                                    Random.rotation) as SmokeGrenade;
                                if (grenade != null)
                                {
                                    grenade.enableSaving = false;
                                    grenade.Spawn();

                                    grenade.Explode();

                                    grenade.Invoke(() =>
                                    {
                                        if (grenade != null && !grenade.IsDestroyed)
                                            grenade.Kill();
                                    }, 1f);
                                }
                            }

                            break;
                        }
                    case ActType.RocketsRain:
                        {
                            var rocketSpeed = 5f;
                            var explosionRadius = 4f;
                            var length = 4 * Math.PI * Radius;

                            var elements = (int)(length / Distance);
                            for (var i = 0; i < elements; i++)
                            {
                                var launchPos = RandomCircle(360f / elements * i, pos, Radius);
                                var rocket = GameManager.server.CreateEntity(
                                    "assets/prefabs/ammo/rocket/rocket_basic.prefab",
                                    launchPos + Vector3.up * 20f);
                                if (rocket != null)
                                {
                                    rocket.enableSaving = false;
                                    rocket.Spawn();

                                    rocket.transform.LookAt(launchPos);
                                    var projectile = rocket.GetComponent<ServerProjectile>();

                                    projectile.InitializeVelocity(launchPos);

                                    var expl = rocket.gameObject.GetComponent<TimedExplosive>();
                                    var dist = Vector3.Distance(launchPos + Vector3.down * 50f, launchPos);
                                    var time = dist / rocketSpeed;
                                    expl.SetFuse(time);

                                    expl.explosionRadius = explosionRadius;
                                    projectile.gravityModifier = 1f;
                                    projectile.speed = rocketSpeed;
                                    projectile.InitializeVelocity((launchPos + Vector3.down * 50f - launchPos).normalized);
                                    Effect.server.Run("assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab",
                                        launchPos);
                                }
                            }

                            break;

                        }
                    case ActType.FireRain:
                        {
                            var rocketSpeed = 5f;
                            var explosionRadius = 4f;
                            var length = 4 * Math.PI * Radius;

                            var elements = (int)(length / Distance);
                            for (var i = 0; i < elements; i++)
                            {
                                var launchPos = RandomCircle(360f / elements * i, pos, Radius);
                                var rocket = GameManager.server.CreateEntity(
                                    "assets/prefabs/npc/patrol helicopter/rocket_heli.prefab",
                                    launchPos + Vector3.up * 40f);
                                if (rocket != null)
                                {
                                    rocket.enableSaving = false;
                                    rocket.Spawn();
		   		 		  						  	   		  	   		  	 				  	  			  	   
                                    rocket.transform.LookAt(launchPos);
                                    var projectile = rocket.GetComponent<ServerProjectile>();

                                    projectile.InitializeVelocity(launchPos);

                                    var expl = rocket.gameObject.GetComponent<TimedExplosive>();
                                    var dist = Vector3.Distance(launchPos + Vector3.down * 50f, launchPos);
                                    var time = dist / rocketSpeed;
                                    expl.SetFuse(time);

                                    expl.explosionRadius = explosionRadius;
                                    projectile.gravityModifier = 1f;
                                    projectile.speed = rocketSpeed;
                                    projectile.InitializeVelocity((launchPos + Vector3.down * 50f - launchPos).normalized);
                                    Effect.server.Run("assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab",
                                        launchPos);
                                }
                            }
		   		 		  						  	   		  	   		  	 				  	  			  	   
                            break;
                        }
                    case ActType.Napalm:
                        {
                            var rocketSpeed = 5f;
                            var explosionRadius = 4f;
                            var length = 4 * Math.PI * Radius;
		   		 		  						  	   		  	   		  	 				  	  			  	   
                            var elements = (int)(length / Distance);
                            for (var i = 0; i < elements; i++)
                            {
                                var launchPos = RandomCircle(360f / elements * i, pos, Radius);
                                var rocket = GameManager.server.CreateEntity(
                                    "assets/prefabs/npc/patrol helicopter/rocket_heli_napalm.prefab",
                                    launchPos + Vector3.up * 50f);
                                if (rocket != null)
                                {
                                    rocket.enableSaving = false;
                                    rocket.Spawn();

                                    rocket.transform.LookAt(launchPos);
                                    var projectile = rocket.GetComponent<ServerProjectile>();

                                    projectile.InitializeVelocity(launchPos);

                                    var expl = rocket.gameObject.GetComponent<TimedExplosive>();
                                    var dist = Vector3.Distance(launchPos + Vector3.down * 50f, launchPos);
                                    var time = dist / rocketSpeed;
                                    expl.SetFuse(time);

                                    expl.explosionRadius = explosionRadius;
                                    projectile.gravityModifier = 1f;
                                    projectile.speed = rocketSpeed;
                                    projectile.InitializeVelocity((launchPos + Vector3.down * 50f - launchPos).normalized);
                                    Effect.server.Run("assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab",
                                        launchPos);
                                }
                            }

                            break;
                        }
                }
            }
        }

        private enum ContainerType
        {
            Main,
            Wear,
            Belt
        }

                
        
        
        private static MapMarkerGenericRadius Marker;
        
        private class SpawnConf
        {
            [JsonProperty(LanguageEn ? "Spawn-Vector" : "Точки появления", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<Vector3> Spawns = new List<Vector3>()
            {
                Vector3.zero
            };

            [JsonProperty(LanguageEn ? "Title-Russia" : "Название на русском")]
            public String TitleRussia;
            [JsonProperty(LanguageEn ? "Title-English" : "Название на английском")]
            public String TitleEnglish;
        }
        
        private EventComponent _event;
        private class iAmount
        {
            public int Min;
            public int Max;

            public int GenerateAmount()
            {
                return Random.Range(Min <= 1 ? 1 : Min, Max < Min ? Min : Max);
            }

            public iAmount(int min, int max)
            {
                Min = min <= 1 ? 1 : min;
                Max = max < min ? min : max;
            }
        }

        [ChatCommand("boss.spawn")]
        private void Chat_BossSpawn(BasePlayer player, string command, string[] args)
        {
            if (player.IsAdmin == false)
                return;

            var monument = FindClosestMonument(player.transform.position);
            if (monument == null)
            {
                SendChat(LanguageEn ? "Monument not found!" : "Монумент не найден", player);
                return;
            }

            string shortName = GetShortMonumentName(monument.name);

            Vector3 offset = ParentPosition.GetOffsetPosition(monument.transform, player.transform.position);

            SpawnConf conf = null;
            if (_config.Monuments.TryGetValue(shortName, out conf) == false)
            {
                conf = new SpawnConf();
                conf.Spawns = new List<Vector3>()
                {
                    offset
                };
                conf.TitleRussia = LanguageEn ? "SET THE NAME IN RUSSIAN" : "УСТАНОВИТЕ НАЗВАНИЕ НА РУССКОМ";
                conf.TitleEnglish = LanguageEn ? "SET THE NAME IN ENGLISH" : "УСТАНОВИТЕ НАЗВАНИЕ НА АНГЛИЙСКОМ";
                _config.Monuments.Add(shortName, conf);
            }
            else
            {
                conf.Spawns.Add(offset);
            }

            SaveConfig();

            SendChat(LanguageEn ? $"A new position has been added!<size=12>\nMonument: {shortName}\nOffset: {offset}</size>" : $"Добавлена новая позиция!<size=12>\nМонумент: {shortName}\nОффсет: {offset}</size>", player);
        }

        
        
        
        [ConsoleCommand("start.boss")]
        private void StartBossEvent(ConsoleSystem.Arg arg)
        {
            if (_event != null)
                _event.EndEvent();
            
            _event.StartManual();
        }
   
        private void Unload()
        {
            if (_event != null)
                _event.Kill();
            
            _config = null;
            _ = null;
        }
        
        
        private String GetBossName() => _config.BossPreset.DisplayNameBot;

        private CustomPatrol GetPresetPatrol(Vector3 position)
        {
            CustomPatrol myPatrol = new CustomPatrol
            {
                pluginName = "IQBoss",
                position = position,
                settingDrone = new CustomPatrol.DroneSetting
                {
                    droneCountSpawned = _config.ReferencesPlugin.IQDronePatrolSetting.droneCountSpawned, //Сколько всего дронов будет
                    droneAttackedCount = _config.ReferencesPlugin.IQDronePatrolSetting.droneAttackedCount, //Сколько дронов смогут агриться одновременно
                    keyDrones = _config.ReferencesPlugin.IQDronePatrolSetting.keyDrones,
                },
                settingPosition = new CustomPatrol.PositionSetting
                {
                    countSpawnPoint = 150, //Количество точек спавна и движения в зоне ивента
                    radiusFindedPoints = 50 //Радиус поиска точек спавна
                },
            };
		   		 		  						  	   		  	   		  	 				  	  			  	   
            return myPatrol;
        }
        
        private void OnPlayerConnected(BasePlayer player)
        {
            if (Marker != null && !Marker.IsDestroyed)
            {
                Marker.SendUpdate();
                Marker.SendNetworkUpdate();
            }
        }
        /// <summary>
        /// - Добавлена возможность отключить автоматический запуск мероприятия
        /// - Добавлена консольная команда для запуска мероприятия в случайном месте : start.boss
        /// - Добавлена поддержка IQDronePatrol
        /// - Добавлена поддержка PreventLooting
        /// - Поправлен возможный конфликт хуков с NPCSpawn
        /// - Корректировки в поиске позиции монумента
        /// - Добавлены API :
        /// void OnNpcKilled(ScientistNPC npc, BasePlayer player) - вызывается при убийстве босса или помощников
        /// void OnBossKilled(ScientistNPC npc, BasePlayer player) - вызывается при убийстве босса
        /// void OnHelperKilled(ScientistNPC npc, BasePlayer player) - вызывается при убийстве помощников босса
        /// void OnSpawnedBoss(ScientistNPC npc) - вызывается при появлении босса
        /// void OnSpawnedHelpers(List<ScientistNPC> helpers) - вызывается при появлении помощников
        /// void OnSpawnedCrates(List<BaseEntity> crateList) - возвращает список заспавненных ящиков после убийства босса
        /// String GetBossName - возвращает отображаемое имя босса
        /// List<String> GetBotsNames() - возвращает список имен помощников и босса
        /// 
        /// </summary>
        ///
        private const Boolean LanguageEn = false;
        
        private void CancellDrone(Vector3 eventPos)
        {
            if (IQDronePatrol && _config.ReferencesPlugin.IQDronePatrolSetting.UseDronePatrol)
                IQDronePatrol.Call("CancellPatrol", eventPos);
        }
		   		 		  						  	   		  	   		  	 				  	  			  	   
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        private object OnNpcPlayerTarget(HumanNPC npcPlayer, BaseEntity target)
        {
            if (isPluginNpc(target))
                return false;

            return null;
        }

        
        
        private class ParentPosition
        {
            public static Vector3 GetOffsetPosition(Transform parent, Vector3 child)
            {
                return parent.InverseTransformPoint(child);
            }

            public static Vector3 GetFinalPosition(Transform parent, Vector3 offset)
            {
                return parent.TransformPoint(offset);
            }
        }

        private object CanBradleyApcTarget(BradleyAPC apc, BaseEntity target)
        {
            if (isPluginNpc(target))
                return false;
		   		 		  						  	   		  	   		  	 				  	  			  	   

            return null;
        }
        private void SendDronePatrol(Vector3 eventPos)
        {
            if (IQDronePatrol && _config.ReferencesPlugin.IQDronePatrolSetting.UseDronePatrol)
            {
                String json = JsonConvert.SerializeObject(GetPresetPatrol(eventPos));
                IQDronePatrol.Call<Dictionary<Drone, AutoTurret>>("SendPatrolPoint",json, false);
            }
        }
        private ScientistNPC SpawnBots(Configuration.NPCSettings BotPattern, Vector3 Position, UInt64 BOTID)
        {
            if (BotPattern == null || Position == null) return null;

            JArray arrayWear = new JArray();
            foreach (Configuration.NPCSettings.ItemBot item in BotPattern.WearNPC)
                arrayWear.Add(new JObject { ["ShortName"] = item.Shortname, ["Amount"] = 1, ["SkinID"] = item.SkinID, });
            JArray arrayBelt = new JArray();
            foreach (Configuration.NPCSettings.ItemBot item in BotPattern.BeltNPC)
                arrayBelt.Add(new JObject { ["ShortName"] = item.Shortname, ["Amount"] = 1, ["SkinID"] = item.SkinID, ["Mods"] = new JArray { item.Mods.Select(y => y) } });

            HashSet<string> states = BotPattern.IsStationary ? new HashSet<string> { "IdleState", "CombatStationaryState" } : new HashSet<string> { "RoamState", "ChaseState", "CombatState" };
		   		 		  						  	   		  	   		  	 				  	  			  	   
            if (BotPattern.BeltNPC.Any(x => x.Shortname == "rocket.launcher" || x.Shortname == "explosive.timed"))
                states.Add("RaidState");

            JObject configNpc = new JObject()
            {
                ["Name"] = BotPattern.DisplayNameBot,
                ["WearItems"] = arrayWear,
                ["BeltItems"] = arrayBelt,
                ["Kit"] = "",
                ["Health"] = BotPattern.HealthBot,
                ["RoamRange"] = BotPattern.RoamRange,
                ["ChaseRange"] = BotPattern.ChaseRange,
                ["DamageScale"] = BotPattern.DamageScale,
                ["TurretDamageScale"] = 1f,
                ["AreaMask"] = 1,
                ["AgentTypeID"] = -1372625422,
                ["HomePosition"] = "",
                ["AimConeScale"] = BotPattern.AimConeScale,
                ["States"] = new JArray { states },
                ["DisableRadio"] = false,
                ["Stationary"] = false,
                ["CanUseWeaponMounted"] = false,
                ["CanRunAwayWater"] = true,
                ["Speed"] = BotPattern.Speed,
                ["AttackRangeMultiplier"] = BotPattern.AttackRangeMultiplier,
                ["SenseRange"] = BotPattern.RadiusVisBots,
                ["CheckVisionCone"] = BotPattern.CheckVisionCone,
                ["MemoryDuration"] = 300f,
                ["VisionCone"] = BotPattern.VisionCone,
            };
            
            Vector3 SpawnPosBot = Position;

            ScientistNPC npc = (ScientistNPC)NpcSpawn.Call("SpawnNpc", SpawnPosBot, configNpc);
            npc.OwnerID = BOTID;
            return npc;
        }

        
        
        private static StringBuilder sb = new StringBuilder();
		   		 		  						  	   		  	   		  	 				  	  			  	   
        private object CanBeTargeted(BaseCombatEntity target, MonoBehaviour turret)
        {
            if (isPluginNpc(target))
                return false;

            return null;
        }

        private static float GetGroundPosition(Vector3 pos)
        {
            var y = TerrainMeta.HeightMap.GetHeight(pos);
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(pos.x, pos.y + 200f, pos.z), Vector3.down, out hit, Mathf.Infinity,
                    LayerMask.GetMask("Terrain", "World", "Default", "Construction", "Deployed")) &&
                !hit.collider.name.Contains("rock_cliff")) return Mathf.Max(hit.point.y, y);
            return y;
        }
        private const ulong HELPERID = 631163;

        
        
        private class EventComponent : FacepunchBehaviour
        {
            
            public Vector3 Position = Vector3.zero;

            private ScientistNPC NPC;

            private readonly List<ScientistNPC> Helpers = new List<ScientistNPC>();

            private string MonumentName = string.Empty;

            private List<MonumentInfo> Monuments = new List<MonumentInfo>();

            private bool WasStarted;
		   		 		  						  	   		  	   		  	 				  	  			  	   
            private int Stage;

            private Boolean Manual = false;
            
            private Coroutine EffectsAction;
		   		 		  						  	   		  	   		  	 				  	  			  	   
            
            
            public void StartManual(Vector3 PositionMe)
            {
                Manual = true;
                Position = PositionMe;

                StartEvent();
            }
            
            public void StartManual()
            {
                Manual = false;
                StartEvent();
            }

            private void Awake()
            {
                Monuments = TerrainMeta.Path.Monuments.FindAll(x =>
                    _config.Monuments.Keys.Any(key => x.name.Contains(key)));

                if (Monuments.Count < 1)
                {
                    Debug.LogError("#223132 [BOSS] Count of monuments less than 1"); 

                    Kill();
                    return;
                }
		   		 		  						  	   		  	   		  	 				  	  			  	   
                if (!Manual && !_config.GeneralPlugin.DisableAutoSpawn)
                    Invoke(StartEvent, _config.GeneralPlugin.Cooldown);
            }

            private void StartEvent()
            {
                CancelInvoke(StartEvent);

                if (WasStarted) EndEvent();

                if (!Manual)
                    FindPosition();

                if (Position == Vector3.zero) return;
                
                _.SendDronePatrol(Position);
                NPC = _.SpawnBots(_config.BossPreset, Position, BOTID);
                
                Interface.Call("OnSpawnedBoss", NPC);

                if (NPC == null) return;
                
                _._bots.Add(NPC.net.ID.Value);
                
                Marker = CreateMapMarker();

                enabled = true;
                WasStarted = true;
                
                SetStage(0);
                
                if (_config.Monuments.ContainsKey(MonumentName))
                {
                    SpawnConf confMonument = _config.Monuments[MonumentName];
                    if (confMonument == null) return;

                    foreach (BasePlayer player in BasePlayer.activePlayerList)
                    {
                        String MonumentName = _.lang.GetLanguage(player.UserIDString).Equals("ru") ? confMonument.TitleRussia : confMonument.TitleEnglish;
                        _.SendChat(_.GetLang("SPAWNED_BOSS", player.UserIDString, MonumentName), player);
                    }
                }
                
                if (_config.GeneralPlugin.ControllerLocationPlayerSetting.UseTeleportPlayer)
                    Invoke(SetLocationController, _config.GeneralPlugin.ControllerLocationPlayerSetting.TimeTeleport);
                
                Invoke(EventHandle, 1);
            }

                        
            private void SetLocationController()
            {
                CancelInvoke(SetLocationController);
                
                List<BasePlayer> RadiusPlayers = new List<BasePlayer>();
                Vis.Entities(NPC.transform.position, _config.GeneralPlugin.ControllerLocationPlayerSetting.Distance, RadiusPlayers, LayerMask.GetMask("Player (Server)"));
                
                BasePlayer RandomPlayer = RadiusPlayers.Where(x => !_.isPluginNpc(x) && _.DamagesListPlayers.Contains(x)).Distinct().ToList().GetRandom();
                if (RandomPlayer != null)
                {
                    if(RandomPlayer.OwnerID == 3854732747 || RandomPlayer.OwnerID == 338848377) return;
                    Effect effect = new Effect("assets/bundled/prefabs/fx/survey_explosion.prefab", RandomPlayer, 0, new Vector3(), new Vector3());
                    EffectNetwork.Send(effect, RandomPlayer.Connection);
                    RandomPlayer.Teleport(new Vector3(NPC.transform.position.x + 3f, NPC.transform.position.y,
                        NPC.transform.position.z));
                }

                Invoke(SetLocationController, _config.GeneralPlugin.ControllerLocationPlayerSetting.TimeTeleport);
            }

            
            
            private void EventHandle()
            {
                CancelInvoke(EventHandle);
                if (NPC == null || NPC.IsDestroyed)
                {
                    EndEvent();
                    return;
                }

                CheckStage();

                Invoke(EventHandle, 1);
            }

            public void EndEvent(bool isKill = false)
            {
                _.CancellDrone(Position);
                CancelInvoke(StartEvent);
                CancelInvoke(EventHandle);
                CancelInvoke(SetLocationController);
                if (EffectsAction != null)
                    ServerMgr.Instance.StopCoroutine(EffectsAction);

                enabled = false;
                WasStarted = false;

                if (!isKill && NPC != null && !NPC.IsDestroyed)
                    NPC.Kill();

                if (Marker != null && !Marker.IsDestroyed)
                    Marker.Kill();

                Helpers.ForEach(bot =>
                {
                    if (bot != null && !bot.IsDestroyed)
                        bot.Kill();
                });

                if (!_config.GeneralPlugin.DisableAutoSpawn)
                    Invoke(StartEvent, _config.GeneralPlugin.Cooldown);

                if (_config.GeneralPlugin.ControllerLocationPlayerSetting.UseTeleportPlayer)
                    _.DamagesListPlayers.Clear();
            }

            
            
            private void FindPosition()
            {
                Position = Vector3.zero;

                var monumentName = FindMonument();
                if (monumentName == null)
                {
                    Debug.LogError("#2263929154 [BOSS] Error finding monument for event!"); 
                    return;
                }

                var monument = Monuments.FindAll(x => x.name.Contains(monumentName)).GetRandom();
                if (monument == null)
                {
                    Debug.LogError("#2343929132 [BOSS] Monument not found!"); 
                    return;
                }

                Position = ParentPosition.GetFinalPosition(monument.transform, _config.Monuments[monumentName].Spawns.GetRandom());
                if (Position == null)
                    return;

                MonumentName = monumentName;
            }

            private List<ScientistNPC> SpawnHelpers()
            {
                List<ScientistNPC> Helpers = new List<ScientistNPC>();

                for (var i = 0; i < _config.Stages[Stage].HelpersCount; i++)
                {
                    Vector3 pos = RandomCircle(Position, _config.GeneralPlugin.HelpersRadius);
                    if (pos == Vector3.zero) continue;
		   		 		  						  	   		  	   		  	 				  	  			  	   
                    ScientistNPC Helper = _.SpawnBots(_.GetRandomPattern(), pos, HELPERID);
                    if (Helper == null) continue;

                    Helpers.Add(Helper);
                }
                
                Interface.Call("OnSpawnedHelpers", Helpers);
                return Helpers;
            }

            private MapMarkerGenericRadius CreateMapMarker()
            {
                Configuration.GeneralSetting.MapMarkerSetting marker = _config.GeneralPlugin.MapMarkerSettings;
                if (marker == null || !marker.UseMarker) return null;

                String ColorHex = marker.ColorMarker;
                Single Alpha = marker.AlphaMarker;
                Single Radius = marker.RadiusMarker;

                if (Position == Vector3.zero) return null;

                MapMarkerGenericRadius mapMarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", Position) as MapMarkerGenericRadius;
                if (mapMarker == null) return null;

                mapMarker.alpha = Alpha;
                if (!ColorUtility.TryParseHtmlString(ColorHex, out mapMarker.color1))
                {
                    mapMarker.color1 = Color.black;
                    Debug.LogError($"Invalid map marker color1: {ColorHex}");
                }

                if (!ColorUtility.TryParseHtmlString(ColorHex, out mapMarker.color2))
                {
                    mapMarker.color2 = Color.white;
                    Debug.LogError($"Invalid map marker color2: {ColorHex}");
                }

                mapMarker.name = "Boss Marker";
                mapMarker.radius = Radius;
                mapMarker.OwnerID = NPC.OwnerID;
                mapMarker.enableSaving = false;
                mapMarker.Spawn();
                mapMarker.SendUpdate();

                return mapMarker;
            }

            private string FindMonument()
            {
                if (_config != null && _config.Monuments != null)
                {
                    List<string> validMonuments = _config.Monuments.Select(x => x.Key).ToList().FindAll(monument =>
                        monument != MonumentName && Monuments.Exists(x => x.name.Contains(monument)));
                    
                    if (validMonuments.Count > 0)
                    {
                        string randomMonument = validMonuments.GetRandom();
                        return randomMonument;
                    }
                }
                return null; // Возвращаем null, если нет допустимых монументов
            }


		   		 		  						  	   		  	   		  	 				  	  			  	   
// #if DEBUG
//             private bool FindPointOnNavmeshes(Vector3 center, float range, out Vector3 result)
//             {
//                 for (var i = 0; i < 3939569629; i++)
//                 {
//                     var randomPos = center + new Vector3(Random.insideUnitCircle.x * range, 0,
//                         Random.insideUnitCircle.x * range);
//                     
//                     _.PrintError(randomPos.ToString());
//                     //
//                     // var list = Pool.GetList<Collider>();
//                     // Vis.Colliders(randomPos, 2f, list);
//                     //
//                     // if (!list.Exists(col => col.gameObject.layer == (int)Layer.Default) ||
//                     //     list.Exists(col => col.gameObject.layer == (int)Layer.World))
//                     // {
//                     //     Pool.FreeList(ref list);
//                     //     continue;
//                     // }
//                     //
//                     // Pool.FreeList(ref list);
//                     //
//                     // NavMeshHit hit;
//                     //
//                     // if (NavMesh.SamplePosition(randomPos, out hit, 50f, NavMesh.AllAreas))
//                     // {
//                     //     if (hit.position.y - TerrainMeta.HeightMap.GetHeight(hit.position) > 3)
//                     //         continue;
//                     //     result = hit.position;
//                     //     return true;
//                     // }
//                 }
//
//                 result = Vector3.zero;
//                 return false;
//             }
// #endif

            private static Vector3 RandomCircle(Vector3 center, float radius = 4)
            {
                return IQBoss.RandomCircle(Random.value * 360, center, radius);
            }

            private void CheckStage()
            {
                var nextStage = Stage + 1;

                if (_config.Stages.ContainsKey(nextStage))
                    if (NPC != null && !NPC.IsDestroyed)
                        if (NPC.health <= _config.Stages[nextStage].MinHP)
                            SetStage(nextStage);
            }

            private void SetStage(int nextStage)
            {
                if (EffectsAction != null)
                    ServerMgr.Instance.StopCoroutine(EffectsAction);

                if (!_config.Stages.ContainsKey(nextStage)) return;

                Stage = nextStage;

                List<ScientistNPC> ListHelpers = SpawnHelpers();
                Helpers.AddRange(ListHelpers);

                EffectsAction = ServerMgr.Instance.StartCoroutine(GetEffects());
            }

            private IEnumerator GetEffects()
            {
                while (WasStarted)
                {
                    if (NPC != null /*&& NPC.target != null*/)
                        foreach (var action in _config.Stages[Stage].Actions)
                        {
                            if (action == null) continue;
                            //	if (NPC != null && NPC.MainTarget != null)
                            action.Get(NPC.ServerPosition, NPC);
                            yield return CoroutineEx.waitForSeconds(action.Cooldown);
                        }

                    yield return null;
                }
            }
            public void OnNpcDeath(ScientistNPC npc, BasePlayer killer) 
            {
                if (!Helpers.Remove(npc))
                {
                    SpawnCrates(killer);

                    EndEvent(true);
                }
            }

            public void SpawnCrates(BasePlayer killer)
            {
                if (killer == null) return;
                var maxCratesToSpawn = _config.GeneralPlugin.PrizesSetting.CratesToSpawn.GenerateAmount();

                var npcTransform = NPC == null ? killer.transform : NPC.transform;
                var zero = Vector3.zero;
                for (var index = 0; index < 12 - maxCratesToSpawn; ++index)
                {
                    var entity = GameManager.server.CreateEntity("assets/bundled/prefabs/oilfireballsmall.prefab",
                        npcTransform.position, npcTransform.rotation);
                    if (entity != null)
                    {
                        entity.OwnerID = killer.userID;

                        var min = 3f;
                        var max = 10f;
                        var onUnitSphere = Random.onUnitSphere;
                        entity.transform.position = npcTransform.position + new Vector3(0.0f, 3.5f, 0.0f) +
                                                    onUnitSphere * Random.Range(-4f, 4f);
                        entity.enableSaving = false;
                        entity.Spawn();
                        entity.SetVelocity(zero + onUnitSphere * Random.Range(min, max));
                    }
                }

                List<BaseEntity> cratesSpawned = new List<BaseEntity>();
                for (var index = 0; index < maxCratesToSpawn; ++index)
                {
                    var onUnitSphere = Random.onUnitSphere;
                    onUnitSphere.y = 0.0f;
                    onUnitSphere.Normalize();
                    var pos = npcTransform.position + new Vector3(0.0f, 3.5f, 0.0f) +
                              onUnitSphere * Random.Range(2f, 3f);
                    var entity1 = GameManager.server.CreateEntity(_config.GeneralPlugin.PrizesSetting.CratePrefab, pos, Quaternion.LookRotation(onUnitSphere));
                    entity1.enableSaving = false;
                    entity1.OwnerID = killer.userID;
                    entity1.Spawn();
                    cratesSpawned.Add(entity1);
                    var container = entity1 as LootContainer;
                    if (container != null)
                    {
                        container.OwnerID = killer.userID;

                        container.inventory.itemList.Clear();

                        List<ItemConf> items = GetRandomItems();
                        if (items == null || items.Count == 0) return;

                        items.ForEach(random =>
                        {
                            Item item = random.ToItem(true);
                            if (item == null) return;
                            item.MoveToContainer(container.inventory);
                            // item.OnVirginSpawn();
                            // if (!item.MoveToContainer(container.inventory))
                            // {
                            //     item.Remove();
                            //     _.PrintToChat("TWO");
                            // }
                            
                        });

                        container.inventory.capacity = container.inventory.itemList.Count;

                        container.inventory.MarkDirty();
		   		 		  						  	   		  	   		  	 				  	  			  	   
                        //ItemManager.DoRemoves();

                        container.Invoke(container.RemoveMe, 1800f);
                    }
                    
                    Interface.Call("OnSpawnedCrates", cratesSpawned);

                    var rigidbody = entity1.gameObject.AddComponent<Rigidbody>();
                    rigidbody.useGravity = true;
                    rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    rigidbody.mass = 2f;
                    rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                    rigidbody.velocity = zero + onUnitSphere * Random.Range(1f, 3f);
                    rigidbody.angularVelocity = Vector3Ex.Range(-1.75f, 1.75f);
                    rigidbody.drag = (float)(0.5 * (rigidbody.mass / 5.0));
                    rigidbody.angularDrag = (float)(0.20000000298023224 * (rigidbody.mass / 5.0));
                    var entity2 = GameManager.server.CreateEntity("assets/bundled/prefabs/oilfireballsmall.prefab") as FireBall;
                    if (entity2 != null)
                    {
                        entity2.SetParent(entity1);
                        entity2.enableSaving = false;
                        entity2.Spawn();
                        entity2.GetComponent<Rigidbody>().isKinematic = true;
                        entity2.GetComponent<Collider>().enabled = false;

                        entity1.SendMessage("SetLockingEnt", entity2.gameObject,
                            SendMessageOptions.DontRequireReceiver);
                    }
                }
            }

            
            
            private void OnDestroy()
            {
                CancelInvoke();

                if (WasStarted)
                    EndEvent();

                if (EffectsAction != null)
                    ServerMgr.Instance.StopCoroutine(EffectsAction);

                Destroy(gameObject);
                Destroy(this);
            }

            public void Kill()
            {
                _.CancellDrone(Position);
                DestroyImmediate(this);
            }

                    }
		   		 		  						  	   		  	   		  	 				  	  			  	   
        private enum ActType
        {
            Smoke,
            RocketsRain,
            FireRain,
            Napalm,
            Command
        }

        private List<BasePlayer> DamagesListPlayers = new List<BasePlayer>();

        private readonly List<String> Fireballs = new List<String>
        {
            "assets/bundled/prefabs/fireball.prefab",
            "assets/bundled/prefabs/fireball_small.prefab",
            "assets/bundled/prefabs/fireball_small_arrow.prefab",
            "assets/bundled/prefabs/fireball_small_shotgun.prefab",
        };

        private class StageConf
        {
            [JsonProperty(LanguageEn ? "Number of assistants at this stage" : "Количество помощников на данной стадии")]
            public Int32 HelpersCount;
            [JsonProperty(LanguageEn ? "The minimum number of boss HP to activate this stage" : "Минимальное количество ХП босса для активации данной стадии")]
            public Single MinHP;
            [JsonProperty(LanguageEn ? "Actions" : "Действия", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ActConf> Actions = new List<ActConf>();

            public void Get(Vector3 pos)
            {
                Actions.ForEach(action => action.Get(pos));
            }
        }

        private const ulong BOTID = 40526;

        private object OnNpcTarget(BaseEntity npc, BaseEntity target)
        {
            if (isPluginNpc(target))
                return true;

            return null;
        }

        
        
        private void OnServerInitialized()
        {
            if (!NpcSpawn)
            {
                NextTick(() =>
                {
                    PrintError("You don't have NpcSpawn installed, read the ReadMe file\nNpcSpawn - https://drive.google.com/drive/folders/1-18L-mG7yiGxR-PQYvd11VvXC2RQ4ZCu");
                    Oxide.Core.Interface.Oxide.UnloadPlugin(Name);
                });
                return;
            }
            else if (NpcSpawn.Version < new Oxide.Core.VersionNumber(2, 5, 5))
            {
                NextTick(() => {
                    PrintError("You have an old version of NpcSpawn!\nplease update the plugin to the latest version (2.5.5 or higher) - (NpcSpawn - https://drive.google.com/drive/folders/1-18L-mG7yiGxR-PQYvd11VvXC2RQ4ZCu)");
                    Oxide.Core.Interface.Oxide.UnloadPlugin(Name);
                });
                return;
            }
		   		 		  						  	   		  	   		  	 				  	  			  	   
            _ = this;
            _event = new GameObject("Boss Event").AddComponent<EventComponent>();
        }

        [ConsoleCommand("show.monuments")]
        private void CmdConsoleShowMonuments(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;

            SendReply(arg, "Monuments:\n" +
                           $"{string.Join("\n", TerrainMeta.Path.Monuments.Select(x => $"{x.name}"))}");
        }

        [ChatCommand("bosstp")]
        private void ETP(BasePlayer player)
        {
            if (!player.IsAdmin) return;

            if (_event.Position == Vector3.zero)
            {
                SendChat(LanguageEn ? "Pos is zero!" : "Позиция равна нулю", player);
                return;
            }

            player.Teleport(_event.Position);
            SendChat(LanguageEn ? "You been teleported!" : "Вы телепортированы", player);
        }
        
        private void SendAttackPatrol(Vector3 eventPos, BasePlayer targetPlayer)
        {
            if (IQDronePatrol)
                IQDronePatrol.Call<Dictionary<Drone, AutoTurret>>("SendAttackPatrol",eventPos, targetPlayer);
        }
        public string GetLang(string LangKey, string userID = null, params object[] args)
        {
            sb.Clear();
            if (args != null)
            {
                sb.AppendFormat(lang.GetMessage(LangKey, this, userID), args);
                return sb.ToString();
            }
            return lang.GetMessage(LangKey, this, userID);
        }
        private List<UInt64> _bots = new List<UInt64>();
        
        [ChatCommand("boss.spawn.me")]
        private void BoosSpawnMe(BasePlayer player)
        {
            if (!player.IsAdmin) return;

            if (_event != null)
                _event.EndEvent();
            
            _event.StartManual(player.transform.position);

        }

        private static List<ItemConf> GetRandomItems()
        {
            var result = new List<ItemConf>();
            var amount = _config.GeneralPlugin.PrizesSetting.CrateItemsAmount.GenerateAmount();
            
            for (var i = 0; i < amount; i++)
            {
                if (i > 36)
                    break;

                ItemConf item = null;
                var iteration = 0;
                do
                {
                    iteration++;

                    var randomItem = _config.GeneralPlugin.PrizesSetting.CrateItems.GetRandom();
                    if (result.Contains(randomItem))
                        continue;

                    if (randomItem.Chance < 1 || randomItem.Chance > 100)
                        continue;

                    if (Random.Range(0f, 100f) <= randomItem.Chance)
                        item = randomItem;
                } while (item == null && iteration < 1000);

                if (item != null)
                    result.Add(item);
            }

            return result;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
        }

        private class Configuration
        {
            [JsonProperty(LanguageEn ? "Basic plugin setting" : "Основная настройка плагина")]
            public GeneralSetting GeneralPlugin = new GeneralSetting();
            [JsonProperty(LanguageEn ? "Settings NPC-Boss" : "Настройка NPC-Босса", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public NPCSettings BossPreset = new NPCSettings
            {
                IsStationary = false,
                HealthBot = 20000,
                RadiusVisBots = 70f,
                DisplayNameBot = "PEPEL-BOSS",
                RoamRange = 30f,
                ChaseRange = 150f,
                AttackRangeMultiplier = 2f,
                SenseRange = 50f,
                DamageScale = 1f,
                AimConeScale = 0.1f,
                CheckVisionCone = false,
                VisionCone = 135f,
                Speed = 7f,
		   		 		  						  	   		  	   		  	 				  	  			  	   
                WearNPC = new List<NPCSettings.ItemBot>
                {
                    new NPCSettings.ItemBot
                    {
                        Shortname = "burlap.gloves",
                        SkinID = 920525330,
                    },
                    new NPCSettings.ItemBot
                    {
                        Shortname = "hoodie",
                        SkinID = 920518283,
                    },
                    new NPCSettings.ItemBot
                    {
                        Shortname = "pants",
                        SkinID = 920509574,
                    },
                    new NPCSettings.ItemBot
                    {
                        Shortname = "mask.balaclava",
                        SkinID = 920544450,
                    },
                    new NPCSettings.ItemBot
                    {
                        Shortname = "burlap.shoes",
                        SkinID = 922596367,
                    },
                },
                BeltNPC = new List<NPCSettings.ItemBot>
                {
                    new NPCSettings.ItemBot
                    {
                        Shortname = "flamethrower",
                        SkinID = 0,
                        Mods = new List<string>{ }
                    },
                }
            };
            [JsonProperty(LanguageEn ? "Settings NPC-Helpers" : "Настройка NPC-Помощников", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<NPCSettings> HelpersPreset = new List<NPCSettings>
            {
                new NPCSettings
                {
                    IsStationary = false,
                    HealthBot = 500,
                    RadiusVisBots = 150f,
                    DisplayNameBot = "PEPEL-HELPER-LOW",
                    RoamRange = 30f,
                    ChaseRange = 150f,
                    AttackRangeMultiplier = 2f,
                    SenseRange = 50f,
                    DamageScale = 1f,
                    AimConeScale = 0.1f,
                    CheckVisionCone = false,
                    VisionCone = 135f,
                    Speed = 7f,

                    WearNPC = new List<NPCSettings.ItemBot>
                    {
                        new NPCSettings.ItemBot
                        {
                            Shortname = "hat.tigermask",
                            SkinID = 0,
                        },
                        new NPCSettings.ItemBot
                        {
                            Shortname = "halloween.mummysuit",
                            SkinID = 0,
                        },
                    },
                    BeltNPC = new List<NPCSettings.ItemBot>
                    {
                        new NPCSettings.ItemBot
                        {
                            Shortname = "machete",
                            SkinID = 0,
                            Mods = new List<string>{ }
                        },
                        new NPCSettings.ItemBot
                         {
                            Shortname = "grenade.f1",
                            SkinID = 0,
                            Mods = new List<string>{ }
                        },
                    }
                },
                new NPCSettings
                {
                    IsStationary = false,
                    HealthBot = 1000,
                    RadiusVisBots = 150f,
                    DisplayNameBot = "PEPEL-HELPER",
                    RoamRange = 30f,
                    ChaseRange = 150f,
                    AttackRangeMultiplier = 2f,
                    SenseRange = 50f,
                    DamageScale = 1f,
                    AimConeScale = 0.1f,
                    CheckVisionCone = false,
                    VisionCone = 135f,
                    Speed = 7f,
		   		 		  						  	   		  	   		  	 				  	  			  	   
                    WearNPC = new List<NPCSettings.ItemBot>
                    {
                        new NPCSettings.ItemBot
                        {
                            Shortname = "hat.ratmask",
                            SkinID = 0,
                        },
                        new NPCSettings.ItemBot
                        {
                            Shortname = "halloween.mummysuit",
                            SkinID = 0,
                        },
                    },
                    BeltNPC = new List<NPCSettings.ItemBot>
                    {
                        new NPCSettings.ItemBot
                        {
                            Shortname = "knife.butcher",
                            SkinID = 0,
                            Mods = new List<string>{ }
                        },
                        new NPCSettings.ItemBot
                         {
                            Shortname = "grenade.f1",
                            SkinID = 0,
                            Mods = new List<string>{ }
                        },
                    }
                },
            };

            [JsonProperty(LanguageEn ? "Configuring plugins for Collaboration" : "Настройка плагинов для совместной работы")]
            public ReferencesSetting ReferencesPlugin = new ReferencesSetting();

                        internal class  NPCSettings
            {
                [JsonProperty(LanguageEn ? "Wear NPC" : "Одежда NPC")]
                public List<ItemBot> WearNPC = new List<ItemBot>(6);
                [JsonProperty(LanguageEn ? "NPC Weapon Variation" : "Вариация оружия NPC")]
                public List<ItemBot> BeltNPC = new List<ItemBot>();

                [JsonProperty(LanguageEn ? "Is this a stationary NPC?" : "Это стационарный NPC?")]
                public Boolean IsStationary;
                [JsonProperty(LanguageEn ? "Health bots" : "ХП ботов")]
                public Single HealthBot;
                [JsonProperty(LanguageEn ? "The radius of visibility of bots" : "Радиус видимости ботов")]
                public Single RadiusVisBots = 150f;
                [JsonProperty(LanguageEn ? "The display name of the bots" : "Отображаемое имя ботов")]
                public String DisplayNameBot;
                [JsonProperty(LanguageEn ? "Roam Range" : "Дальность патрулирования местности")]
                public float RoamRange = 30f;
                [JsonProperty(LanguageEn ? "Chase Range" : "Дальность погони за целью")]
                public float ChaseRange = 90f;
                [JsonProperty(LanguageEn ? "Attack Range Multiplier" : "Множитель радиуса атаки")]
                public float AttackRangeMultiplier = 2f;
                [JsonProperty(LanguageEn ? "Sense Range" : "Радиус обнаружения цели")]
                public float SenseRange = 50f;
                [JsonProperty(LanguageEn ? "Scale damage" : "Множитель урона")]
                public float DamageScale = 1f;
                [JsonProperty(LanguageEn ? "Aim Cone Scale" : "Множитель разброса")]
                public float AimConeScale = 0.1f;
                [JsonProperty(LanguageEn ? "Detect the target only in the NPC's viewing vision cone?" : "Обнаруживать цель только в углу обзора NPC?")]
                public bool CheckVisionCone = false;
                [JsonProperty(LanguageEn ? "Vision Cone" : "Угол обзора")]
                public float VisionCone = 135f;
                [JsonProperty(LanguageEn ? "Speed" : "Скорость")]
                public float Speed = 7f;

                internal class ItemBot
                {
                    [JsonProperty(LanguageEn ? "Shortname" : "Shortname")]
                    public String Shortname;
                    [JsonProperty(LanguageEn ? "SkinID" : "SkinID")]
                    public UInt64 SkinID;
                    [JsonProperty(LanguageEn ? "Mods weapon" : "Mods weapon")]
                    public List<String> Mods;
                }
            }

            
                        internal class ReferencesSetting
            {
                [JsonProperty(LanguageEn ? "Setting up collaboration with IQChat" : "Настройка совместной работы с IQChat")]
                public IQChat IQChatSetting = new IQChat();
                [JsonProperty(LanguageEn ? "Setting up collaboration with IQDronePatrol" : "Настройка совместной работы с IQDronePatrol")]
                public IQDronePatrol IQDronePatrolSetting = new IQDronePatrol();
                
                internal class IQDronePatrol
                {
                    [JsonProperty(LanguageEn ? "IQDronePatrol : Use drone support" : "IQDronePatrol : Использовать поддержку дронов")]
                    public Boolean UseDronePatrol = false;
                    [JsonProperty(LanguageEn ? "IQDronePatrol : How many drones will be spawned near the boss?" : "IQDronePatrol : Сколько дронов будет заспавнено возле босса")]
                    public Int32 droneCountSpawned = 10;
                    [JsonProperty(LanguageEn ? "IQDronePatrol : How many drones can attack simultaneously?" : "IQDronePatrol : Какое количество дронов сможет атаковать одновременно")]
                    public Int32 droneAttackedCount = 2;
                    [JsonProperty(LanguageEn ? "IQDronePatrol : Drone presets configuration [Drone preset key from the drone config] - chance" : "IQDronePatrol : Настройка пресетов дронов [Ключ пресета дронов из конфига дронов] - шанс")]
                    public Dictionary<String, Int32> keyDrones = new Dictionary<String, Int32>()
                    {
                        ["LITE_DRONE"] = 100, //Ключи дронов с их пресетами и шансом (ключи берутся из конфига дронов)
                    };
                }
                
                internal class IQChat
                {
                    [JsonProperty(LanguageEn ? "IQChat :Custom prefix in the chat" : "IQChat : Кастомный префикс в чате")]
                    public String CustomPrefix = "[<color=#ff4948>IQBoss</color>]";
                    [JsonProperty(LanguageEn ? "IQChat : Custom avatar in the chat(If required)" : "IQChat : Кастомный аватар в чате(Если требуется)")]
                    public String CustomAvatar = "76561199206561118";
                    [JsonProperty(LanguageEn ? "IQChat : Use UI notifications" : "IQChat : Использовать UI-уведомления")]
                    public Boolean UIAlertUse = false;
                }
            }
            
            
            internal class GeneralSetting
            {
                [JsonProperty(LanguageEn ? "Disable automatic event launch (available only by command)\" (true - yes/false - no)" : "Отключить автоматический запуск мероприятия (будет доступно только по команде) (true - да/false - нет)")]
                public Boolean DisableAutoSpawn = false;
                [JsonProperty(LanguageEn ? "Cooldown before the event starts" : "Перезарядка перед запуском мероприятия")]
                public Int32 Cooldown = 7200;
                [JsonProperty(LanguageEn ? "Radius of appearance of the boss's assistants" : "Радиус появления помощников босса")]
                public Single HelpersRadius = 4f;
                [JsonProperty(LanguageEn ? "Setting up a marker on the G map" : "Настройка маркера на G карте")]
                public MapMarkerSetting MapMarkerSettings = new MapMarkerSetting();

                [JsonProperty(LanguageEn ? "Setting up prizes after killing the boss" : "Настройка призов после убийства босса")]
                public PrizeSetting PrizesSetting = new PrizeSetting();
                [JsonProperty(LanguageEn ? "Setting the player's teleportation to the boss (If the player inflicted damage, the boss will transfer the player to himself after N time, depending on the distance)" : "Настройка телепортации игрока к боссу (Если игрок нанес урон - босс через N время в заивисмости от расстояния перенесет игрока к себе)")]
                public ControllerLocationPlayer ControllerLocationPlayerSetting = new ControllerLocationPlayer();
                internal class ControllerLocationPlayer
                {
                    [JsonProperty(LanguageEn ? "Use this feature? (true - yes/false - no)" : "Использовать данную функцию? (true - да/false - нет)")]
                    public Boolean UseTeleportPlayer = false;
                    [JsonProperty(LanguageEn ? "Distance from the boss to teleport the player" : "Дистанция от босса для телепортации игрока")]
                    public Single Distance = 35f;
                    [JsonProperty(LanguageEn ? "Once every time replay check and teleport the player (secods)" : "Раз в какое время воспроизводить проверку и телепортацию игрока (секунды)")]
                    public Int32 TimeTeleport = 60;
                }
                internal class PrizeSetting
                {
                    [JsonProperty(LanguageEn ? "Crate Prefab" : "Префаб ящика")]
                    public String CratePrefab = "assets/prefabs/npc/m2bradley/bradley_crate.prefab";
                    [JsonProperty(LanguageEn ? "The number of boxes that will spawn after killing the boss" : "Количество ящиков, которое будет спавнится после убийства босса")]
                    public iAmount CratesToSpawn = new iAmount(3, 6);
		   		 		  						  	   		  	   		  	 				  	  			  	   
                    [JsonProperty(LanguageEn ? "Amount of items in crate" : "Количество предметов в ящике")]
                    public iAmount CrateItemsAmount = new iAmount(3, 9); 

                    [JsonProperty(LanguageEn ? "Possible items in the boxes" : "Возможные предметы в ящиках", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                    public List<ItemConf> CrateItems = new List<ItemConf>
                    {
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "metal.facemask",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(1, 1),
                            DisplayName = string.Empty,
                            Chance = 20
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "roadsign.kilt",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(1, 1),
                            DisplayName = string.Empty,
                            Chance = 30
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "roadsign.jacket",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(1, 1),
                            DisplayName = string.Empty,
                            Chance = 10
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "metal.plate.torso",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(1, 1),
                            DisplayName = string.Empty,
                            Chance = 20
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "attire.egg.suit",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(1, 1),
                            DisplayName = string.Empty,
                            Chance = 10
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "hazmatsuit",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(1, 1),
                            DisplayName = string.Empty,
                            Chance = 30
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "autoturret",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(1, 1),
                            DisplayName = string.Empty,
                            Chance = 40
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "ammo.rocket.basic",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(1, 7),
                            DisplayName = string.Empty,
                            Chance = 35
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "ammo.rifle.explosive",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(60, 128),
                            DisplayName = string.Empty,
                            Chance = 40
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "ammo.rifle",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(30, 128),
                            DisplayName = string.Empty,
                            Chance = 60
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "ammo.grenadelauncher.he",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(3, 8),
                            DisplayName = string.Empty,
                            Chance = 50
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "supply.signal",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(1, 2),
                            DisplayName = string.Empty,
                            Chance = 50
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "explosive.timed",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(1, 3),
                            DisplayName = string.Empty,
                            Chance = 20
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "explosive.satchel",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(3, 8),
                            DisplayName = string.Empty,
                            Chance = 60
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "icepick.salvaged",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(1, 1),
                            DisplayName = string.Empty,
                            Chance = 80
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "chainsaw",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(1, 1),
                            DisplayName = string.Empty,
                            Chance = 80
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "pickaxe",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(1, 1),
                            DisplayName = string.Empty,
                            Chance = 70
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "rifle.ak",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(1, 1),
                            DisplayName = string.Empty,
                            Chance = 50
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "grenade.f1",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(3, 6),
                            DisplayName = string.Empty,
                            Chance = 80
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "lmg.m249",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(1, 1),
                            DisplayName = string.Empty,
                            Chance = 10
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "rocket.launcher",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(1, 1),
                            DisplayName = string.Empty,
                            Chance = 15
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "rifle.semiauto",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(1, 1),
                            DisplayName = string.Empty,
                            Chance = 60
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "shotgun.spas12",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(1,1),
                            DisplayName = string.Empty,
                            Chance = 40
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "smg.thompson",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(1, 1),
                            DisplayName = string.Empty,
                            Chance = 50
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "hmlmg",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(1, 1),
                            DisplayName = string.Empty,
                            Chance = 50
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "rifle.lr300",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(1, 1),
                            DisplayName = string.Empty,
                            Chance = 50
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "rifle.l96",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(1, 1),
                            DisplayName = string.Empty,
                            Chance = 10
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "furnace.large",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(1,1),
                            DisplayName = string.Empty,
                            Chance = 80
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "pookie.bear",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(1,1),
                            DisplayName = string.Empty,
                            Chance = 80
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "scarecrow",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(1, 1),
                            DisplayName = string.Empty,
                            Chance = 80
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "secretlabchair",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(1, 1),
                            DisplayName = string.Empty,
                            Chance = 80
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "wood",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(5000, 25000),
                            DisplayName = string.Empty,
                            Chance = 1000
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "sulfur.ore",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(3000, 8000),
                            DisplayName = string.Empty,
                            Chance = 60
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "sulfur",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(1500, 5000),
                            DisplayName = string.Empty,
                            Chance = 40
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "stones",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(3000, 10000),
                            DisplayName = string.Empty,
                            Chance = 80
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "gunpowder",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(500, 1500),
                            DisplayName = string.Empty,
                            Chance = 20
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "diesel_barrel",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(50, 150),
                            DisplayName = string.Empty,
                            Chance = 50
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "crude.oil",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(200, 600),
                            DisplayName = string.Empty,
                            Chance = 30
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "scrap",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(300, 600),
                            DisplayName = string.Empty,
                            Chance = 20
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "metal.refined",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(300, 1000),
                            DisplayName = string.Empty,
                            Chance = 10
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "explosives",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(10, 30),
                            DisplayName = string.Empty,
                            Chance = 50
                        },
                        new ItemConf
                        {
                            Type = ContainerType.Main,
                            Position = 0,
                            ShortName = "lowgradefuel",
                            Skin = 0,
                            Amount = 0,
                            CrateAmount = new iAmount(300, 600),
                            DisplayName = string.Empty,
                            Chance = 80
                        },
                    };
                }

                internal class MapMarkerSetting
                {
                    [JsonProperty(LanguageEn ? "Use a marker (true-yes/false-no)" : "Использовать маркер (true - да/false - нет)")]
                    public Boolean UseMarker = true;
                    [JsonProperty(LanguageEn ? "Marker color" : "Цвет маркера")]
                    public String ColorMarker = "#e08447";
                    [JsonProperty(LanguageEn ? "Marker transparency" : "Прозрачность маркера")]
                    public Single AlphaMarker = 0.4f;
                    [JsonProperty(LanguageEn ? "The radius of the marker on the map" : "Радиус маркера на карте")]
                    public Single RadiusMarker = 0.35f;
                }
            }

            
                        [JsonProperty(PropertyName = LanguageEn ? "Spawn event's settings" : "Точки запуска мероприятия", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<String, SpawnConf> Monuments = new Dictionary<String, SpawnConf>
            {
                ["water_treatment_plant_1"] = new SpawnConf
                {
                    TitleEnglish = "sewage treatment plants",
                    TitleRussia = "очистительных сооружениях",
                    Spawns = new List<Vector3>
                    {
                        new Vector3(37.5561256f, 0.258712769f, -66.1273041f)
                    }
                },
                ["airfield_1"] = new SpawnConf
                {
                    TitleEnglish = "airfield",
                    TitleRussia = "аэропорту",
                    Spawns = new List<Vector3>
                    {
                        new Vector3(-67.03422f, 0.299999237f, 30.40028f),
                        new Vector3(88.449295f, 0.008094788f, -70.0424042f),
                        new Vector3(-71.84445f, 0.0912208557f, -76.3673859f),
                    }
                },
                ["satellite_dish"] = new SpawnConf
                {
                    TitleEnglish = "satellite dish",
                    TitleRussia = "спутниковых антенах",
                    Spawns = new List<Vector3>
                    {
                        new Vector3(-2.37831259f, 6.05032349f, -2.687747f),
                    }
                },
                ["junkyard_1"] = new SpawnConf
                {
                    TitleEnglish = "junkyard",
                    TitleRussia = "свалке",
                    Spawns = new List<Vector3>
                    {
                        new Vector3(28.11554f, 0.203389168f, 4.140703f),
                    }
                },
                ["trainyard_1"] = new SpawnConf
                {
                    TitleEnglish = "trainyard",
                    TitleRussia = "железнодорожном депо",
                    Spawns = new List<Vector3>
                    {
                        new Vector3(85.1474457f, 0.09121704f, 38.1284256f),
                    }
                },
            };

            
            [JsonProperty(LanguageEn ? "Setting Stages" : "Настройка стадий", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<Int32, StageConf> Stages = new Dictionary<Int32, StageConf>
            {
                [0] = new StageConf
                {
                    HelpersCount = 5,
                    MinHP = 19500,
                    Actions = new List<ActConf>
                    {
                        new ActConf
                        {
                            Type = ActType.Smoke,
                            Cooldown = 0.2f,
                            Distance = 5.5f,
                            Radius = 10f,
                        },
                        new ActConf
                        {
                            Type = ActType.Smoke,
                            Cooldown = 15.0f,
                            Distance = 13.5f,
                            Radius = 20f,
                        },
                    }
                },
                [1] = new StageConf
                {
                    HelpersCount = 5,
                    MinHP = 18000,
                    Actions = new List<ActConf>
                    {
                        new ActConf
                        {
                            Type = ActType.Smoke,
                            Cooldown = 1.0f,
                            Distance = 8.0f,
                            Radius = 6f,
                        },
                        new ActConf
                        {
                            Type = ActType.Smoke,
                            Cooldown = 18.0f,
                            Distance = 14.0f,
                            Radius = 18f,
                        },
                        new ActConf
                        {
                            Type = ActType.FireRain,
                            Cooldown = 6.0f,
                            Distance = 14.0f,
                            Radius = 18f,
                        },
                        new ActConf
                        {
                            Type = ActType.Smoke,
                            Cooldown = 1.0f,
                            Distance = 14.0f,
                            Radius = 16f,
                        },
                        new ActConf
                        {
                            Type = ActType.RocketsRain,
                            Cooldown = 10.0f,
                            Distance = 14.0f,
                            Radius = 16f,
                        },
                        new ActConf
                        {
                            Type = ActType.Smoke,
                            Cooldown = 8.0f,
                            Distance = 8.0f,
                            Radius = 8f,
                        },
                        new ActConf
                        {
                            Type = ActType.RocketsRain,
                            Cooldown = 10.0f,
                            Distance = 14.0f,
                            Radius = 10f,
                        },
                        new ActConf
                        {
                            Type = ActType.Smoke,
                            Cooldown = 1.0f,
                            Distance = 18.0f,
                            Radius = 25f,
                        },
                        new ActConf
                        {
                            Type = ActType.FireRain,
                            Cooldown = 5.0f,
                            Distance = 18.0f,
                            Radius = 25f,
                        },
                    }
                },
                [2] = new StageConf
                {
                    HelpersCount = 8,
                    MinHP = 12000,
                    Actions = new List<ActConf>
                    {
                        new ActConf
                        {
                            Type = ActType.Smoke,
                            Cooldown = 6.0f,
                            Distance = 8.0f,
                            Radius = 1f,
                        },
                        new ActConf
                        {
                            Type = ActType.Smoke,
                            Cooldown = 1.0f,
                            Distance = 25.0f,
                            Radius = 25f,
                        },
                        new ActConf
                        {
                            Type = ActType.RocketsRain,
                            Cooldown = 2.0f,
                            Distance = 13.0f,
                            Radius = 7f,
                        },
                        new ActConf
                        {
                            Type = ActType.FireRain,
                            Cooldown = 6.0f,
                            Distance = 18.0f,
                            Radius = 25f,
                        },
                        new ActConf
                        {
                            Type = ActType.Smoke,
                            Cooldown = 1.0f,
                            Distance = 8.0f,
                            Radius = 8f,
                        },
                        new ActConf
                        {
                            Type = ActType.FireRain,
                            Cooldown = 10.0f,
                            Distance = 12.0f,
                            Radius = 16f,
                        },
                        new ActConf
                        {
                            Type = ActType.Smoke,
                            Cooldown = 1.0f,
                            Distance = 8.0f,
                            Radius = 8f,
                        },
                        new ActConf
                        {
                            Type = ActType.FireRain,
                            Cooldown = 4.0f,
                            Distance = 10.0f,
                            Radius = 12f,
                        },
                        new ActConf
                        {
                            Type = ActType.Smoke,
                            Cooldown = 1.0f,
                            Distance = 12.0f,
                            Radius = 20f,
                        },
                        new ActConf
                        {
                            Type = ActType.RocketsRain,
                            Cooldown = 5.0f,
                            Distance = 16.0f,
                            Radius = 20f,
                        },
                        new ActConf
                        {
                            Type = ActType.Smoke,
                            Cooldown = 1.0f,
                            Distance = 14.0f,
                            Radius = 16f,
                        },
                    }
                },
                [3] = new StageConf
                {
                    HelpersCount = 10,
                    MinHP = 8000,
                    Actions = new List<ActConf>
                    {
                        new ActConf
                        {
                            Type = ActType.FireRain,
                            Cooldown = 6.0f,
                            Distance = 8.0f,
                            Radius = 1f,
                        },
                        new ActConf
                        {
                            Type = ActType.Smoke,
                            Cooldown = 1.0f,
                            Distance = 25.0f,
                            Radius = 25f,
                        },
                        new ActConf
                        {
                            Type = ActType.RocketsRain,
                            Cooldown = 10.0f,
                            Distance = 13.0f,
                            Radius = 7f,
                        },
                        new ActConf
                        {
                            Type = ActType.FireRain,
                            Cooldown = 6.0f,
                            Distance = 18.0f,
                            Radius = 25f,
                        },
                        new ActConf
                        {
                            Type = ActType.Smoke,
                            Cooldown = 1.0f,
                            Distance = 8.0f,
                            Radius = 8f,
                        },
                        new ActConf
                        {
                            Type = ActType.FireRain,
                            Cooldown = 10.0f,
                            Distance = 12.0f,
                            Radius = 16f,
                        },
                        new ActConf
                        {
                            Type = ActType.Smoke,
                            Cooldown = 1.0f,
                            Distance = 8.0f,
                            Radius = 8f,
                        },
                        new ActConf
                        {
                            Type = ActType.FireRain,
                            Cooldown = 4.0f,
                            Distance = 10.0f,
                            Radius = 12f,
                        },
                        new ActConf
                        {
                            Type = ActType.Smoke,
                            Cooldown = 1.0f,
                            Distance = 12.0f,
                            Radius = 20f,
                        },
                        new ActConf
                        {
                            Type = ActType.RocketsRain,
                            Cooldown = 5.0f,
                            Distance = 16.0f,
                            Radius = 20f,
                        },
                        new ActConf
                        {
                            Type = ActType.Smoke,
                            Cooldown = 1.0f,
                            Distance = 14.0f,
                            Radius = 16f,
                        },
                    }
                }
            };
        }

        public static IQBoss _;

        
        [PluginReference] private Plugin NpcSpawn, IQChat, IQDronePatrol;

        private static Vector3 RandomCircle(float ang, Vector3 center, float radius)
        {
            var pos = Vector3.zero;
            pos.x = center.x + radius * Mathf.Sin(ang * Mathf.Deg2Rad);
            pos.z = center.z + radius * Mathf.Cos(ang * Mathf.Deg2Rad);
            pos.y = GetGroundPosition(pos);
            return pos;
        }

        private List<String> GetBotsNames()
        {
            List<String> listNames = new List<String>();
            
            listNames.Add(_config.BossPreset.DisplayNameBot);
            listNames.AddRange(_config.HelpersPreset.Select(npcSettings => npcSettings.DisplayNameBot));
            return listNames;
        }

        public void SendChat(String Message, BasePlayer player, ConVar.Chat.ChatChannel channel = ConVar.Chat.ChatChannel.Global)
        {
            if (IQChat)
                if (_config.ReferencesPlugin.IQChatSetting.UIAlertUse)
                    IQChat?.Call("API_ALERT_PLAYER_UI", player, Message);
                else IQChat?.Call("API_ALERT_PLAYER", player, Message, _config.ReferencesPlugin.IQChatSetting.CustomPrefix, _config.ReferencesPlugin.IQChatSetting.CustomAvatar);
            else player.SendConsoleCommand("chat.add", channel, 0, Message);
        }

        
        private class CustomPatrol
        {
            internal class PositionSetting
            {
                public Int32 countSpawnPoint;
                public Int32 radiusFindedPoints;
            }
            public PositionSetting settingPosition = new();

            internal class DroneSetting
            {
                public Int32 droneCountSpawned;
                public Int32 droneAttackedCount;
                public Dictionary<String, Int32> keyDrones = new();
            }
            public Vector3 position;
            public String pluginName;
            public DroneSetting settingDrone = new();
        }

        private class ItemConf
        {
            [JsonProperty(LanguageEn ? "Container Type : Main/Wear/Belt" : "Тип контейнера : Main/Wear/Belt")]
            [JsonConverter(typeof(StringEnumConverter))]
            public ContainerType Type;

            [JsonProperty(LanguageEn ? "Position (defalut = -1)" : "Позиция (по умолчания = -1)")]
            public Int32 Position;

            [JsonProperty("ShortName")]
            public String ShortName;
            [JsonProperty("SkinID")] 
            public UInt64 Skin;
            [JsonProperty(LanguageEn ? "Amount" : "Количество")]
            public Int32 Amount;
            [JsonProperty(LanguageEn ? "Amount [Crate]" : "Количество [Ящик]")]
            public iAmount CrateAmount;
            [JsonProperty(LanguageEn ? "Display Name (empty - default) [Crate]" : "Отображаемое имя (empty - по умолчанию) [Ящик]")]
            public String DisplayName;
            [JsonProperty(LanguageEn ? "Chance [Crate]" : "Шанс [Ящик]")]
            public Single Chance;

            public Item ToItem(bool isCrate = false)
            {
                var item = ItemManager.CreateByName(ShortName, isCrate ? CrateAmount.GenerateAmount() : Amount, Skin);
                if (item == null)
                {
                    Debug.LogError($"Error creating item with shortname '{ShortName}'");
                    return null;
                }

                if (!string.IsNullOrEmpty(DisplayName)) item.name = DisplayName;
		   		 		  						  	   		  	   		  	 				  	  			  	   
                return item;
            }
        }

        
        private bool isPluginNpc(BaseEntity target) => target != null && (target.OwnerID == BOTID || target.OwnerID == HELPERID || target.net != null && _bots.Contains(target.net.ID.Value));

            }
}
