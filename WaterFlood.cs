using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("WaterFlood", "EcoSmile", "1.1.3")]
    class WaterFlood : RustPlugin
    {
        static WaterFlood ins;
        PluginConfig config;

        public class PluginConfig
        {
            [JsonProperty("Включить плагин?")]
            public bool PluginEnable;

            [JsonProperty("Выводить сообщение в чат об изменении уровня моря?")]
            public bool BroadcastMessage;

            [JsonProperty("Отображать сообщение об изменении уровня моря в UI?")]
            public bool DrawUI;

            [JsonProperty("Настройка недельного ивента (если суточный отключен)")]
            public WipeEvent wipeEvent;
            [JsonProperty("Включить суточный ивент?")]
            public bool EnableOnceEvent;
            [JsonProperty("Настройка суточного ивента (если суточный включен)")]
            public NightEvent nightEvent;
        }

        public class WipeEvent
        {
            [JsonProperty("Задержка в минутах перед началом увеличения уровня моря после старта сервера (в минутах) (0 - задержка отключена)")]
            public float TimeOut;
            [JsonProperty("Максимальная отметка уровня моря в метрах")]
            public float SeaMaxLevel;
            [JsonProperty("Установить максимальную отметку уровня моря по самой высокой точки карты?")]
            public bool SeaMaxTerrainLevel;
            [JsonProperty("На протяжении скольких дней после вайпа поднимать уровень моря?")]
            public int dayleight;
            [JsonProperty("Часы игрового времени, когда игрокам будет сообщаться о текущем уровне моря")]
            public string[] HourToBroadcast;
            [JsonProperty("Реальное или игровое время вывода сообщений (true - реальное время, false - игровое время)")]
            public bool RealTime;
        }

        public class NightEvent
        {
            [JsonProperty("На сколько метров повышать уровень моря")]
            public float SeaChange;
            [JsonProperty("Часы реального времени, когда уровень моря начинает увеличиваться")]
            public string[] HoursToChange;
            [JsonProperty("Время в секундах, на протяжении которого уровень моря растет")]
            public float ChangeTime;
            [JsonProperty("Снижать уровень моря после подъема уровня воды?")]
            public bool DecreaseeSeaLevel;
            [JsonProperty("Время через которое снижать уровень воды")]
            public float DecristTimeout;
            [JsonProperty("Время на протяжении которого будет снижаться уровень воды")]
            public float DecristTime;
            [JsonProperty("На сколько метров понижать уровень моря после волны")]
            public float SeaChangeDown;
        }

        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig
            {
                PluginEnable = false,
                wipeEvent = new WipeEvent()
                {
                    TimeOut = 0,
                    SeaMaxLevel = 100,
                    SeaMaxTerrainLevel = true,
                    dayleight = 7,
                    HourToBroadcast = new string[] { "08:00", "20:00" },
                    RealTime = true
                },
                BroadcastMessage = true,
                DrawUI = true,
                EnableOnceEvent = false,
                nightEvent = new NightEvent()
                {
                    SeaChange = 5f,
                    HoursToChange = new string[] { "12:00", "00:00" },
                    ChangeTime = 60,
                    DecreaseeSeaLevel = true,
                    DecristTimeout = 30,
                    DecristTime = 60,
                    SeaChangeDown = 4f,
                }
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        public class Data
        {
            [JsonProperty("Отметка текущего уровня моря (не редактировать)")]
            public float CurrentSeaLevel;
            [JsonProperty("Отметка МАКСИМАЛЬНОЙ высоты над уровнем моря (не редактировать)")]
            public float MaxSeaLevel;
        }
        public Data data;
        void LoadData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("WaterFlood/Sealevel"))
                data = Interface.Oxide.DataFileSystem.ReadObject<Data>("WaterFlood/Sealevel");
            else
            {
                data = new Data() { CurrentSeaLevel = WaterSystem.OceanLevel > 0 ? WaterSystem.OceanLevel : 0.0001f, MaxSeaLevel = GetHighestTerrainHeight() };
                Interface.Oxide.DataFileSystem.WriteObject("WaterFlood/Sealevel", data);
            }
        }

        void OnServerSave()
        {
            SaveData();
        }

        private void OnServerInitialized()
        {
            ins = this;
            LoadData();
            LoadConfig();
            LoadMessages();
            if (!config.PluginEnable) return;
            controller = new GameObject().AddComponent<SeaController>();

            if (data.MaxSeaLevel == 0)
            {
                data.MaxSeaLevel = GetHighestTerrainHeight();
                Interface.Oxide.DataFileSystem.WriteObject("WaterFlood/Sealevel", data);
            }
            controller.GenerateSpawnpoints();
        }

        private float GetHighestTerrainHeight()
        {
            var terrain = TerrainMeta.Terrain;
            float height = 0;
            int nx = terrain.terrainData.heightmapWidth;
            int ny = terrain.terrainData.heightmapHeight;
            for (int x = 0; x < nx; x++)
            {
                for (int y = 0; y < ny; y++)
                {
                    var h = TerrainMeta.HeightMap.GetHeight(x, y);
                    if (h > height)
                    {
                        height = h;
                    }
                }
            }
            return height - 5f;
        }

        void Unload()
        {
            var objects = UnityEngine.Object.FindObjectsOfType<SeaController>();
            foreach (var obj in objects)
                UnityEngine.Object.Destroy(obj);

            foreach (var pl in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(pl, "Announce.BG");
            
            data.CurrentSeaLevel = WaterSystem.OceanLevel;
            Interface.Oxide.DataFileSystem.WriteObject("WaterFlood/Sealevel", data);
        }

        enum BiomeType { Arid, Arctic, Temperate, Tundra }
        private Hash<BiomeType, List<Vector3>> spawnPoints = new Hash<BiomeType, List<Vector3>>();
        bool isDisabled = true;
        private object OnPlayerRespawn(BasePlayer player)
        {
            if (isDisabled)
                return null;

            var targetpos = GetSpawnPoint();
            if (targetpos == null)
                return null;

            BasePlayer.SpawnPoint spawnPoint1 = new BasePlayer.SpawnPoint();
            spawnPoint1.pos = (Vector3)targetpos;
            spawnPoint1.rot = new Quaternion(0f, 0f, 0f, 1f);
            return spawnPoint1;
        }

        private object GetSpawnPoint()
        {
            BiomeType biomeType = spawnPoints.ElementAt(UnityEngine.Random.Range(0, spawnPoints.Count - 1)).Key;
            
            Vector3 targetPos = spawnPoints[biomeType].GetRandom();
            if (targetPos == Vector3.zero)
                return null;

            List<BaseEntity> entities = Facepunch.Pool.GetList<BaseEntity>();
            Vis.Entities(targetPos, 15f, entities, LayerMask.GetMask("Construction", "Deployable"));
            int count = entities.Count;
            Facepunch.Pool.FreeList(ref entities);
            if (count > 5)
            {
                spawnPoints[biomeType].Remove(targetPos);
                
                return GetSpawnPoint();
            }
            return targetPos;
        }

        void SaveData()
        {
            data.CurrentSeaLevel = WaterSystem.OceanLevel;
            Interface.Oxide.DataFileSystem.WriteObject("WaterFlood/Sealevel", data);
        }

        void OnNewSave()
        {
            if (config.wipeEvent.SeaMaxTerrainLevel)
            {
                data.MaxSeaLevel = GetHighestTerrainHeight();
                Interface.Oxide.DataFileSystem.WriteObject("WaterFlood/Sealevel", data);
            }
        }

        static void DrawUI(BasePlayer player, string text)
        {
            CuiHelper.DestroyUi(player, "Announce.BG");
            var containe = new CuiElementContainer();
            containe.Add(new CuiElement()
            {
                Parent = "Hud",
                Name = "Announce.BG",
                FadeOut = 1f,
                Components =
                {
                    new CuiImageComponent {Color = "0 0 0 0.3",FadeIn = 1f},
                    new CuiRectTransformComponent { AnchorMin = "0 0.88", AnchorMax = "1 0.95"}
                }
            });
            containe.Add(new CuiLabel()
            {
                Text = { Text = $"<color=#AADDFF>{text}</color>", Align = TextAnchor.MiddleCenter, FontSize = 18, FadeIn = 1f },
                FadeOut = 1f,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, "Announce.BG", "AnnounceText");
            CuiHelper.AddUi(player, containe);
            ins.timer.Once(5f, () => { CuiHelper.DestroyUi(player, "Announce.BG"); });
        }

        SeaController controller;

        public class SeaController : FacepunchBehaviour
        {
            float currentlevel;
            public float step;
            public float MaxLevel;
            public float MinLevel;
            float levelchange;
            float ChangeTime;
            string[] ChangeHour;
            void Awake()
            {
                gameObject.layer = (int)Rust.Layer.Reserved1;
                string text = ConsoleSystem.BuildCommand("env.oceanlevel", $"{ins.data.CurrentSeaLevel}");
                ConsoleSystem.Arg arg = new ConsoleSystem.Arg(ConsoleSystem.Option.Server, text);
                arg.cmd.Call(arg);
                currentlevel = WaterSystem.OceanLevel;
                if (ins.config.EnableOnceEvent)
                {
                    var reply = 3260;
                    step = ins.config.nightEvent.SeaChange;
                    ChangeHour = ins.config.nightEvent.HoursToChange;
                    ChangeTime = ins.config.nightEvent.ChangeTime;
                    levelchange = step / (ChangeTime * 100);
                    MaxLevel = currentlevel + step;
                    if (reply == 0) { }
                    InvokeRepeating(CheckTime, 5f, 5f);
                }
                else
                {
                    GenerateSpawnpoints();
                    MaxLevel = ins.config.wipeEvent.SeaMaxLevel;
                    if (ins.config.wipeEvent.SeaMaxTerrainLevel)
                        MaxLevel = ins.data.MaxSeaLevel;

                    ChangeHour = ins.config.wipeEvent.HourToBroadcast;

                    levelchange = (MaxLevel - currentlevel) / (ins.config.wipeEvent.dayleight * 86400);
                    InvokeRepeating(InfChange, ins.config.wipeEvent.TimeOut > 0 ? ins.config.wipeEvent.TimeOut * 60 : 0, 1f);
                    InvokeRepeating(InfBroadcast, 5f, 5f);
                    InvokeRepeating(GenerateInvoke, 500, 500);
                }
            }

            void GenerateInvoke()
            {
                GenerateSpawnpoints();
            }

            int check = 0;
            void InfChange()
            {
                if (Mathf.Approximately(WaterSystem.OceanLevel, WaterSystem.OceanLevel + levelchange))
                    levelchange += Mathf.Max(1E-06f * Mathf.Max(Mathf.Abs(WaterSystem.OceanLevel), Mathf.Abs(WaterSystem.OceanLevel + levelchange)), Mathf.Epsilon * 8f);

                Change();
                if (WaterSystem.OceanLevel >= MaxLevel)
                    CancelInvoke(InfChange);
            }
            string BroadcastTime = "⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠";
            void InfBroadcast()
            {
                if (ins.config.wipeEvent.RealTime && ChangeHour.Any(x => x == DateTime.Now.ToString("HH:mm")) && DateTime.Now.ToString("HH:mm") != BroadcastTime)
                {
                    Boadcast("EveryDayInfo");
                    BroadcastTime = DateTime.Now.ToString("HH:mm");
                }
                if (!ins.config.wipeEvent.RealTime && ChangeHour.Any(x => ins.covalence.Server.Time.Hour >= int.Parse(x.Split(':')[0])) && DateTime.Now.ToString("HH:mm") != BroadcastTime)
                {
                    Boadcast("EveryDayInfo");
                    BroadcastTime = DateTime.Now.ToString("HH:mm");
                }
            }

            void Boadcast(string key)
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (ins.config.BroadcastMessage)
                        ins.SendReply(player, ins.GetMsg(key, player).Replace("{0}", $"{step}").Replace("{1}", $"{step}").Replace("{2}", $"{Math.Round(WaterSystem.OceanLevel, 1)}"));
                    if (ins.config.DrawUI)
                        DrawUI(player, ins.GetMsg(key, player).Replace("{0}", $"{step}").Replace("{1}", $"{step}").Replace("{2}", $"{Math.Round(WaterSystem.OceanLevel, 1)}"));
                }
            }

            void Change()
            {
                double inc = WaterSystem.OceanLevel + levelchange;
                string text = ConsoleSystem.BuildCommand("env.oceanlevel", $"{inc}");
                ConsoleSystem.Arg arg = new ConsoleSystem.Arg(ConsoleSystem.Option.Server, text);
                arg.cmd.Call(arg);
            }

            void CheckTime()
            {
                if (ChangeHour.Any(x => x == DateTime.Now.ToString("HH:mm")))
                {
                    Boadcast("EventInc");
                    InvokeRepeating(ChangeSeaLevel, 10f, 0.01f);
                    CancelInvoke(CheckTime);
                }
            }

            void ChangeSeaLevel()
            {
                Change();
                if (WaterSystem.OceanLevel >= MaxLevel)
                {
                    ChangeTime = ins.config.nightEvent.ChangeTime;
                    InvokeRepeating(CheckTime, 5f, 5f);
                    CancelInvoke(ChangeSeaLevel);
                    if (ins.config.nightEvent.DecreaseeSeaLevel)
                    {
                        currentlevel = WaterSystem.OceanLevel;
                        step = ins.config.nightEvent.SeaChangeDown;
                        ChangeTime = ins.config.nightEvent.DecristTime;
                        levelchange = -step / (ChangeTime * 100);
                        MinLevel = currentlevel - step;
                        Boadcast("EventDec");
                        InvokeRepeating(DecreaseSeaLevel, ins.config.nightEvent.DecristTimeout, 0.01f);
                        ins.SaveData();
                        return;
                    }
                    ins.SaveData();
                    GenerateSpawnpoints();
                    return;
                }
            }

            public void GenerateSpawnpoints()
            {
                ins.isDisabled = true;
                ins.spawnPoints.Clear();
                for (int i = 0; i < 500; i++)
                {
                    float max = TerrainMeta.Size.x / 2;
                    var success = FindNewPosition(new Vector3(0, 0, 0), max);
                    if (success is Vector3)
                    {
                        Vector3 spawnPoint = (Vector3)success;
                        float height = TerrainMeta.HeightMap.GetHeight(spawnPoint);
                        if (spawnPoint.y >= height && !(spawnPoint.y - height > 1))
                        {
                            BiomeType biome = GetMajorityBiome(spawnPoint);

                            if (!ins.spawnPoints.ContainsKey(biome))
                                ins.spawnPoints.Add(biome, new List<Vector3>());
                            ins.spawnPoints[biome].Add(spawnPoint);

                        }
                    }
                }

                ins.isDisabled = false;
            }

            void DecreaseSeaLevel()
            {
                Change();
                if (WaterSystem.OceanLevel <= MinLevel)
                {
                    CancelInvoke(DecreaseSeaLevel);
                    Boadcast("EventEnd");
                    ins.SaveData();
                    GenerateSpawnpoints();
                }
            }

            void OnDestroy()
            {
                CancelInvoke(GenerateInvoke);
                CancelInvoke(ChangeSeaLevel);
                CancelInvoke(DecreaseSeaLevel);
                CancelInvoke(CheckTime);
                CancelInvoke(InfChange);
                string text = ConsoleSystem.BuildCommand("env.oceanlevel", $"0");
                ConsoleSystem.Arg arg = new ConsoleSystem.Arg(ConsoleSystem.Option.Server, text);
                arg.cmd.Call(arg);
            }

            private BiomeType GetMajorityBiome(Vector3 position)
            {
                Dictionary<BiomeType, float> biomes = new Dictionary<BiomeType, float>
                {
                    {BiomeType.Arctic, TerrainMeta.BiomeMap.GetBiome(position, TerrainBiome.ARCTIC) },
                    {BiomeType.Arid, TerrainMeta.BiomeMap.GetBiome(position, TerrainBiome.ARID) },
                    {BiomeType.Temperate, TerrainMeta.BiomeMap.GetBiome(position, TerrainBiome.TEMPERATE) },
                    {BiomeType.Tundra, TerrainMeta.BiomeMap.GetBiome(position, TerrainBiome.TUNDRA) }
                };
                return biomes.OrderByDescending(x => x.Value).ToArray()[0].Key;
            }

            private object FindNewPosition(Vector3 position, float max, bool failed = false)
            {
                var targetPos = UnityEngine.Random.insideUnitCircle * max;
                var sourcePos = new Vector3(position.x + targetPos.x, 100, position.z + targetPos.y);
                var hitInfo = RayPosition(sourcePos);
                var success = ProcessRay(hitInfo);
                if (success == null)
                {
                    if (failed) return null;
                    else return FindNewPosition(position, max, true);
                }
                else if (success is Vector3)
                {
                    if (failed) return null;
                    else return FindNewPosition(new Vector3(sourcePos.x, ((Vector3)success).y, sourcePos.y), max, true);
                }
                else
                {
                    sourcePos.y = Mathf.Max((float)success, TerrainMeta.HeightMap.GetHeight(sourcePos));
                    return sourcePos;
                }
            }

            private object ProcessRay(RaycastHit hitInfo)
            {
                if (hitInfo.collider != null)
                {
                    if (hitInfo.collider?.gameObject.layer == LayerMask.NameToLayer("Water"))
                        return null;
                    if (hitInfo.collider?.gameObject.layer == LayerMask.NameToLayer("Prevent Building"))
                        return null;
                    if (hitInfo.GetEntity() != null)
                    {
                        return hitInfo.point.y;
                    }
                    if (hitInfo.collider?.name == "areaTrigger")
                        return null;
                    if (hitInfo.collider?.GetComponentInParent<SphereCollider>() || hitInfo.collider?.GetComponentInParent<BoxCollider>())
                    {
                        return hitInfo.collider.transform.position + new Vector3(0, -1, 0);
                    }
                }
                return hitInfo.point.y;
            }

            private RaycastHit RayPosition(Vector3 sourcePos)
            {
                RaycastHit hitInfo;
                Physics.Raycast(sourcePos, Vector3.down, out hitInfo);//, LayerMask.GetMask("Terrain", "World", "Construction"));

                return hitInfo;
            }
        }

        string GetMsg(string key, BasePlayer player = null, ulong playerid = 3338240)
        {
            return lang.GetMessage(key, this, player == null ? null : player.UserIDString);
        }

        void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["EventInc"] = "ATTENTION!!!\nScientists have recorded abnormal activity of the moon!\nThe water level will rise by {0} m!",
                ["EventDec"] = "Recorded a decrease in the activity of the moon!\nThe water is receding!\nThe water level will drop by {1} m.",
                ["EventEnd"] = "The sea level is set at {2} m!",
                ["EveryDayInfo"] = "The sea level reached {2} m!\nBe vigilant, have time to save your property from inundation!",
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["EventInc"] = "ВНИМАНИЕ!!!\nУченые зафиксировали аномальную активность луны!\nОжидается повышение уровня воды на {0} м!",
                ["EventDec"] = "Зафиксировано понижение активности луны!\nВода отступает!\nУровень воды снизится на {1} м.",
                ["EventEnd"] = "Уровень моря установился на отметке {2} м!",
                ["EveryDayInfo"] = "Уровень моря достиг отметки {2} м!\nБудьте бдительны, успейте спасти свое имущество от затопления!",
            }, this, "ru");
        }
    }
}
