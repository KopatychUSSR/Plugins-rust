using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BuildingProtection", "EcoSmile", "1.0.1")]
    class BuildingProtection : RustPlugin
    {
        static BuildingProtection ins;
        PluginConfig config;
        public class PluginConfig
        {
            [JsonProperty("Настройка защитной станции.")]
            public DefStation defStation { get; set; }
            public class DefStation
            {
                [JsonProperty("Блокировать установку защиты во время рейда?")]
                public bool noRaid { get; set; }
                [JsonProperty("Привилегия для использования защиты")]
                public string premis { get; set; }
                [JsonProperty("Включить ДИНАМИЧЕСКУЮ систему оплаты?")]
                public bool dynamic { get; set; }
                [JsonProperty("Процентная ДИНАМИЧНОЙ ставка от обслуживания дома за 1 час на каждый из уровней защиты")]
                public Dictionary<string, int> dynamicprice { get; set; }
                [JsonProperty("Процентная СТАТИЧЕСКАЯ ставка защиты дома за 1 час на каждый уровень (Если динамическая система = false)")]
                public Dictionary<string, RessourcePriceDefencive> staticprice { get; set; }
            }
        }
        public class RessourcePriceDefencive
        {
            [JsonProperty("Количество ресурсов за 1 час")]
            public Dictionary<string, int> resourses { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig()
            {
                defStation = new PluginConfig.DefStation
                {
                    noRaid = true,
                    premis = "buildingprotection.use",
                    dynamic = true,
                    dynamicprice = new Dictionary<string, int>()
                    {
                        ["120"] = 20,
                        ["140"] = 40,
                        ["160"] = 60
                    },
                    staticprice = new Dictionary<string, RessourcePriceDefencive>()
                    {
                        ["120"] = new RessourcePriceDefencive
                        {
                            resourses = new Dictionary<string, int>()
                            {
                                ["wood"] = 2000,
                                ["stones"] = 2000,
                                ["metal.fragments"] = 1500,
                                ["metal.refined"] = 200
                            }
                        },
                        ["140"] = new RessourcePriceDefencive
                        {
                            resourses = new Dictionary<string, int>()
                            {
                                ["wood"] = 4000,
                                ["stones"] = 4000,
                                ["metal.fragments"] = 3000,
                                ["metal.refined"] = 300
                            }
                        },
                        ["160"] = new RessourcePriceDefencive
                        {
                            resourses = new Dictionary<string, int>()
                            {
                                ["wood"] = 6000,
                                ["stones"] = 6000,
                                ["metal.fragments"] = 5000,
                                ["metal.refined"] = 500
                            }
                        }
                    }
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
        void OnServerSave()
        {
            Interface.Oxide.DataFileSystem.WriteObject("BuildingProtection/Protect", buildingprotect);
        }
        public Dictionary<uint, Protect> buildingprotect = new Dictionary<uint, Protect>();

        public class Protect
        {
            public float time { get; set; }
            public int protect { get; set; }
            public int objectcount { get; set; }
        }

        void LoadData()
        {
            try
            {
                buildingprotect = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<uint, Protect>>("BuildingProtection/Protect");
            }
            catch
            {
                buildingprotect = new Dictionary<uint, Protect>();
                Interface.Oxide.DataFileSystem.WriteObject("BuildingProtection/Protect", buildingprotect);
            }

        }
        [PluginReference] Plugin NoEscape;
        void OnServerInitialized()
        {
            ins = this;
            LoadData();
            LoadConfig();
            LoadMessages();
            permission.RegisterPermission(config.defStation.premis, this);
            foreach (var player in BasePlayer.activePlayerList)
                if (player.GetComponent<BuildingPrivlidgeHandler>() == null)
                    player.gameObject.AddComponent<BuildingPrivlidgeHandler>();

            InitTimer();
            DrawUIInfo();
        }

        void Unload()
        {
            var triggerHandler = UnityEngine.Object.FindObjectsOfType<BuildingPrivlidgeHandler>();
            foreach (var handler in triggerHandler)
                UnityEngine.Object.Destroy(handler);

            Interface.Oxide.DataFileSystem.WriteObject("BuildingProtection/Protect", buildingprotect);

        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity is BuildingPrivlidge)
            {
                if (permission.UserHasPermission((entity as BaseEntity).OwnerID.ToString(), config.defStation.premis))
                {
                    SendReply(BasePlayer.FindByID((entity as BaseEntity).OwnerID), "Чтобы установить защиту здания ударьте киянку по шкафу.");
                    SendInfoMessage(BasePlayer.FindByID((entity as BaseEntity).OwnerID), "Чтобы установить защиту здания ударьте киянку по шкафу.");
                }
            }
        }

        void OnHammerHit(BasePlayer player, HitInfo info, ulong playerid = 1)
        {
            if (player == null || info == null) return;
            if (config.defStation.noRaid)
            {
                var isRaid = NoEscape?.Call("IsRaidBlock", player.userID);
                if (isRaid == null)
                {
                    var umodNE = NoEscape?.Call("IsRaidBlocked", player);
                    if (umodNE != null && (bool)umodNE)
                    {
                        SendReply(player, GetMsg("RaidBlock", player));
                        return;
                    }
                }
                else if (isRaid != null && (bool)isRaid)
                {
                    SendReply(player, GetMsg("RaidBlock", player));
                    return;
                }
            }
            if (!permission.UserHasPermission(player.UserIDString, config.defStation.premis)) return;
            var entity = info.HitEntity;
            if (entity == null) return;
            var reply = 1;
            if (reply == 0) { }
            if (entity is BuildingPrivlidge)
                DrawUI(player);
        }

        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity == null) return;
            if (entity is BuildingPrivlidge)
            {
                var BP = entity as BuildingPrivlidge;
                if (buildingprotect.ContainsKey(BP.net.ID))
                    buildingprotect.Remove(BP.net.ID);

                BuildingPrivlidgeHandler.playersHandler.FindAll(x => x.lastBuildingPrivlidge == entity).
                ForEach(x => OnPrivilegeChange(x.player, x.lastBuildingPrivlidge));
            }
        }

        void DrawUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "HomeBenchMain");
            string BenchUI = "[{\"name\":\"HomeBenchMain\",\"parent\":\"Overlay\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"close\":\"HomeBenchMain\",\"sprite\":\"assets/content/ui/ui.background.transparent.radial.psd\",\"material\":\"assets/content/ui/uibackgroundblur.mat\",\"color\":\"0 0 0 0.6117647\"},{\"type\":\"NeedsCursor\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"CuiElement\",\"parent\":\"HomeBenchMain\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0 0 0 0.5\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmax\":\"0 0\"}]}]";
            CuiHelper.AddUi(player, BenchUI);
            float gap = 0.008f;
            float width = 0.2f;
            float height = 0.08f;
            float startxBox = 0.2f;
            float startyBox = 0.55f - height;
            float xmin = startxBox;
            float ymin = startyBox;
            var container = new CuiElementContainer();
            if (config.defStation.dynamic)
                foreach (var check in config.defStation.dynamicprice.Keys.ToList())
                {
                    container.Add(new CuiButton()
                    {
                        Button = { Command = $"defst {check}", Color = "0 0 0 0.5" },
                        RectTransform = {
                        AnchorMin = xmin + " " + ymin,
                        AnchorMax = (xmin + width) + " " + (ymin + height ),
                        OffsetMax = "-1 -1",
                        OffsetMin = "5 5",
                    },
                        Text = { Text = $"<i><b>Усилить защиту на \n{check}%</b></i>\n", Align = UnityEngine.TextAnchor.MiddleCenter, Color = "1 1 1 1", FontSize = 18 },
                    }, "HomeBenchMain", $"ui.menu.{check}");
                    xmin += width + gap;
                    if (xmin + width >= 1)
                    {
                        xmin = startxBox;
                        ymin -= height + gap;
                    }
                }
            else
                foreach (var check in config.defStation.staticprice.Keys.ToList())
                {
                    container.Add(new CuiButton()
                    {
                        Button = { Command = $"defst {check}", Color = "0 0 0 0.5" },
                        RectTransform = {
                        AnchorMin = xmin + " " + ymin,
                        AnchorMax = (xmin + width) + " " + (ymin + height ),
                        OffsetMax = "-1 -1",
                        OffsetMin = "5 5",
                    },
                        Text = { Text = $"<i><b>Усилить защиту на \n{check}%</b></i>\n", Align = UnityEngine.TextAnchor.MiddleCenter, Color = "1 1 1 1", FontSize = 18 },
                    }, "HomeBenchMain", $"ui.menu.{check}");
                    xmin += width + gap;
                    if (xmin + width >= 1)
                    {
                        xmin = startxBox;
                        ymin -= height + gap;
                    }
                }

            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("defst")]
        void defstation_cmd(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            CuiHelper.DestroyUi(player, "HomeBenchMain");
            if (arg.Args.Length < 1) return;
            string BenchUI = "[{\"name\":\"HomeBenchMain\",\"parent\":\"Overlay\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"close\":\"HomeBenchMain\",\"sprite\":\"assets/content/ui/ui.background.transparent.radial.psd\",\"material\":\"assets/content/ui/uibackgroundblur.mat\",\"color\":\"0 0 0 0.6117647\"},{\"type\":\"NeedsCursor\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"CuiElement\",\"parent\":\"HomeBenchMain\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0 0 0 0.5\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmax\":\"0 0\"}]}]";
            CuiHelper.AddUi(player, BenchUI);
            var container = new CuiElementContainer();
            string protect = arg.Args[0];
            container.Add(new CuiLabel()
            {
                Text = { Text = $"Введите количесто часов на которое необходимо увеличить защиту на {protect}% и нажмите Enter", Align = UnityEngine.TextAnchor.MiddleCenter, FontSize = 24 },
                RectTransform = { AnchorMin = "0.5 0.6", AnchorMax = "0.5 0.6", OffsetMin = "-500 -30", OffsetMax = "500 30" }
            }, "HomeBenchMain", "MedicBench");

            container.Add(new CuiButton()
            {
                Button = { Command = "", Color = "1 1 1 0.1", Material = "assets/content/ui/uibackgroundblur.mat" },
                Text = { Text = "", Align = UnityEngine.TextAnchor.MiddleCenter, FontSize = 24 },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150 -30", OffsetMax = "150 30" }
            }, "HomeBenchMain", "InputBG");

            container.Add(new CuiElement()
            {
                Parent = "HomeBenchMain",
                Name = "Input",
                Components = {
                    new CuiInputFieldComponent() { Align = UnityEngine.TextAnchor.MiddleCenter, CharsLimit = 2, FontSize = 32, Command = $"checkorder {protect}" },
                    new CuiRectTransformComponent {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150 -30", OffsetMax = "150 30" }
                }
            });

            container.Add(new CuiButton()
            {
                Button = { Command = $"selectprotect", Color = "1 1 1 0.1", Material = "assets/content/ui/uibackgroundblur.mat" },
                Text = { Text = "Назад", Align = UnityEngine.TextAnchor.MiddleCenter, FontSize = 18 },
                RectTransform = { AnchorMin = "0.1 0.1", AnchorMax = "0.1 0.1", OffsetMin = "-30 00", OffsetMax = "30 30" }
            }, "HomeBenchMain", "protect");

            CuiHelper.AddUi(player, container);
        }

        private List<ItemAmount> itemAmountPrice = new List<ItemAmount>();
        [ConsoleCommand("checkorder")]
        void order_cmd(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            CuiHelper.DestroyUi(player, "ordertext");
            CuiHelper.DestroyUi(player, "protect");
            int res;
            if (arg.Args.Length < 2 || !Int32.TryParse(arg.Args[1], out res)) return;
            int time = int.Parse(arg.Args[1]);
            string protect = arg.Args[0];
            if (time <= 0) return;

            List<ItemAmount> itemlist = new List<ItemAmount>();
            if (config.defStation.dynamic)
            {
                BuildingPrivlidge priv = player.GetBuildingPrivilege();
                var list = GetBuilItemlist(priv);
                foreach (var ent in list)
                    itemlist.Add(new ItemAmount(ent.itemDef, ent.amount + (ent.amount * time * config.defStation.dynamicprice[protect] / 100f)));
                itemAmountPrice = itemlist;
            }
            else
            {
                foreach (var item in config.defStation.staticprice[protect].resourses.Keys.ToList())
                    itemlist.Add(new ItemAmount(ItemManager.CreateByName(item, config.defStation.staticprice[protect].resourses[item]).info, config.defStation.staticprice[protect].resourses[item] * time));
                itemAmountPrice = itemlist;
            }
            var container = new CuiElementContainer();
            container.Add(new CuiLabel()
            {
                Text = { Text = $"Для увеличения защиты дома на {protect}% на {time} часов, вам требуется:\n{GetIngredientList(player, itemlist)}\nСтроительно после установки защиты ЗАПРЕЩЕНО! Иначе защита будет отключена!", Align = UnityEngine.TextAnchor.UpperLeft, FontSize = 18 },
                RectTransform = { AnchorMax = "0.5 0.45", AnchorMin = "0.5 0.45", OffsetMin = "-300 -150", OffsetMax = "300 00" }
            }, "HomeBenchMain", "ordertext");
            if ((bool)GetIngredientList(player, itemlist, true))
            {
                container.Add(new CuiButton()
                {
                    Button = { Command = $"getprotect {protect} {time}", Color = "1 1 1 0.1", Material = "assets/content/ui/uibackgroundblur.mat" },
                    Text = { Text = "Улучшить", Align = UnityEngine.TextAnchor.MiddleCenter, FontSize = 24 },
                    RectTransform = { AnchorMin = "0.5 0.15", AnchorMax = "0.5 0.15", OffsetMin = "-150 00", OffsetMax = "150 60" }
                }, "HomeBenchMain", "protect");
            }
            CuiHelper.AddUi(player, container);

        }

        [ConsoleCommand("getprotect")]
        void GetProtect(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            CuiHelper.DestroyUi(player, "HomeBenchMain");
            if (arg.Args.Length < 2) return;
            foreach (var ingridient in itemAmountPrice)
                player.inventory.containerMain.Take(null, ingridient.itemid, (int)ingridient.amount);
            string BenchUI = "[{\"name\":\"HomeBenchMain\",\"parent\":\"Overlay\",\"components\":[{\"type\":\"UnityEngine.UI.Button\",\"close\":\"HomeBenchMain\",\"sprite\":\"assets/content/ui/ui.background.transparent.radial.psd\",\"material\":\"assets/content/ui/uibackgroundblur.mat\",\"color\":\"0 0 0 0.6117647\"},{\"type\":\"NeedsCursor\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmin\":\"0 0\",\"offsetmax\":\"0 0\"}]},{\"name\":\"CuiElement\",\"parent\":\"HomeBenchMain\",\"components\":[{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0 0 0 0.5\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 1\",\"offsetmax\":\"0 0\"}]}]";
            CuiHelper.AddUi(player, BenchUI);
            int protect = int.Parse(arg.Args[0]);
            int time = int.Parse(arg.Args[1]);
            var container = new CuiElementContainer();
            container.Add(new CuiLabel()
            {
                Text = { Text = $"Вы установили защиту строений на {protect}% на {time} часов.\nСтроительно после установки защиты ЗАПРЕЩЕНО! Иначе защита будет отключена!", Align = UnityEngine.TextAnchor.MiddleCenter, FontSize = 24 },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-500 -30", OffsetMax = "500 30" }
            }, "HomeBenchMain", "ProtectSuc");
            CuiHelper.AddUi(player, container);
            var building = player.GetBuildingPrivilege().net.ID;
            int objamount = 0;
            foreach (var ent in player.GetBuildingPrivilege().GetBuilding().decayEntities)
            {
                if (!(ent is BuildingBlock) && !(ent is Door)) continue;
                objamount++;
            }
            if (!buildingprotect.ContainsKey(building))
                buildingprotect.Add(building, new Protect { time = time * 3600, protect = protect, objectcount = objamount });
            else
            {
                buildingprotect[building].time = time * 3600;
                buildingprotect[building].protect = protect;
            }
            OnPrivilegeEnter(player, player.GetBuildingPrivilege());
        }
        void OnEntityBuilt(Planner plan, GameObject go)
        {
            var building = plan.GetBuildingPrivilege();
            if (building != null)
                if (buildingprotect.ContainsKey(building.net.ID))
                {
                    int objamount = 0;
                    foreach (var ent in building.GetBuilding().decayEntities)
                    {
                        if (!(ent is BuildingBlock) && !(ent is Door)) continue;
                        objamount++;
                    }
                    if (buildingprotect[building.net.ID].objectcount < objamount)
                    {
                        SendReply(plan.GetOwnerPlayer(), "Защита была отключена. \nСтроительство при включенной защите запрещено.");
                        buildingprotect.Remove(building.net.ID);
                    }
                }

        }
        private void SendInfoMessage(BasePlayer player, string message)
        {
            player?.SendConsoleCommand("gametip.showgametip", message);
            timer.Once(3f, () => player?.SendConsoleCommand("gametip.hidegametip"));
        }
        List<string> msglist = new List<string>()
        {
            "ЗАЩИТА ДОМА В {0} <color=#45ed36>АКТИВНА!</color>",
            "Время действия усиления {1}"
        };
        void DrawStatus(BasePlayer player)
        {
            if (player == null) return;
            CuiHelper.DestroyUi(player, "DrawStatusMain");
            var BuildingID = player.GetBuildingPrivilege();
            if (BuildingID == null)
                return;
            var container = new CuiElementContainer();
            if (!buildingprotect.ContainsKey(BuildingID.net.ID))
            {
                container.Add(new CuiButton()
                {
                    Button = { Command = "", Color = "0.66 0.66 0.66 0.1", Material = "assets/content/ui/uibackgroundblur.mat" },
                    Text = { Text = "ЗАЩИТА ДОМА <color=#ff4f4f>НЕ АКТИВНА!</color>", Align = UnityEngine.TextAnchor.MiddleCenter, FontSize = 14 },
                    RectTransform = { AnchorMin = "0.4925 0.11", AnchorMax = "0.4925 0.11", OffsetMin = "-190 00", OffsetMax = "191 20" }
                }, "Hud", "DrawStatusMain");
            }
            else
            {
                string message = getRandomBroadcast();
                message = message.Replace("{0}", $"{buildingprotect[BuildingID.net.ID].protect}%").Replace("{1}", $"{FormatTime(TimeSpan.FromSeconds(buildingprotect[BuildingID.net.ID].time))}");
                container.Add(new CuiButton()
                {
                    Button = { Command = "", Color = "0.66 0.66 0.66 0.1", Material = "assets/content/ui/uibackgroundblur.mat" },
                    Text = { Text = message, Align = UnityEngine.TextAnchor.MiddleCenter, FontSize = 14 },
                    RectTransform = { AnchorMin = "0.4925 0.11", AnchorMax = "0.4925 0.11", OffsetMin = "-190 -00", OffsetMax = "191 20" }
                }, "Hud", "DrawStatusMain");
            }
            CuiHelper.AddUi(player, container);
        }
        int CurrentNum = -1;
        string getRandomBroadcast()
        {
            CurrentNum++;
            if (CurrentNum >= msglist.Count)
                CurrentNum = 0;
            return (string)msglist[CurrentNum];
        }

        List<BasePlayer> drawingUI = new List<BasePlayer>();

        List<ItemAmount> GetBuilItemlist(BuildingPrivlidge priv)
        {
            var building = priv.GetBuilding();
            List<ItemAmount> itemAmounts = new List<ItemAmount>();
            List<ItemAmount> itemAmountList = new List<ItemAmount>();
            foreach (var ent in building.decayEntities)
            {
                if (!(ent is BuildingBlock) && !(ent is Door)) continue;
                foreach (var en in ent.BuildCost())
                    itemAmountList.Add(en);
            }
            if (itemAmountList == null)
                return null;
            foreach (ItemAmount itemAmount1 in itemAmountList)
            {
                if (itemAmount1.itemDef.category == ItemCategory.Resources)
                {
                    float amt = itemAmount1.amount * 0.1f;
                    bool flag = false;
                    foreach (ItemAmount itemAmount2 in itemAmounts)
                    {
                        if (itemAmount2.itemDef == itemAmount1.itemDef)
                        {
                            itemAmount2.amount += amt;
                            flag = true;
                            break;
                        }
                    }
                    if (!flag)
                        itemAmounts.Add(new ItemAmount(itemAmount1.itemDef, amt));
                }
            }
            return itemAmounts;
        }

        public object GetIngredientList(BasePlayer player, List<ItemAmount> list = null, bool checkall = false)
        {
            if (checkall)
            {
                foreach (var ingredient in list)
                {
                    if (player.inventory.GetAmount(ingredient.itemDef.itemid) < ingredient.amount)
                    {
                        return false;
                    }
                }
                return true;
            }
            else
            {
                string component = "";
                foreach (var ingredient in list)
                {
                    var reply = list.Select(x =>
                        GetMsg(player.inventory.GetAmount(x.itemDef.itemid) >= x.amount
                                ? "EnoughtIngridient"
                                : "NotEnoughtIngridient"
                            , player, Translated.ContainsKey(x.itemDef.shortname) ? Translated[x.itemDef.shortname] : x.itemDef.displayName.english, player.inventory.GetAmount(x.itemDef.itemid),
                            Math.Round(x.amount))).ToArray();
                    component = $"{string.Join("\n", reply)}";
                }
                return component;
            }
        }

        void InitTimer()
        {
            timer.Every(1f, () =>
            {
                foreach (var key in buildingprotect.Keys.ToList())
                    if (buildingprotect.ContainsKey(key))
                    {
                        if (buildingprotect[key].time > 0)
                            buildingprotect[key].time--;
                        else buildingprotect.Remove(key);
                    }
            });
        }

        class BuildingPrivlidgeHandler : FacepunchBehaviour
        {
            internal static List<BuildingPrivlidgeHandler> playersHandler = new List<BuildingPrivlidgeHandler>();

            public BasePlayer player;
            public BuildingPrivlidge lastBuildingPrivlidge;

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                InvokeRepeating(Repeater, 1.0f, 1.0f);

                playersHandler.Add(this);
            }

            void Repeater()
            {
                if (player == null || !player.IsConnected)
                {
                    Destroy(this);
                    return;
                }

                if (player.IsSleeping() || player.IsSpectating() || player.IsReceivingSnapshot) return;

                CheckBuildingPrivlidge(player);
            }

            void CheckBuildingPrivlidge(BasePlayer player)
            {
                BuildingPrivlidge buildingPrivlidge = player.GetBuildingPrivilege();

                if (buildingPrivlidge != lastBuildingPrivlidge)
                {
                    if (lastBuildingPrivlidge != null)
                        ins.OnPrivilegeChange(player, lastBuildingPrivlidge);

                    if (buildingPrivlidge != null)
                        ins.OnPrivilegeEnter(player, buildingPrivlidge);

                    lastBuildingPrivlidge = buildingPrivlidge;
                }
            }
            void OnDestroy() => playersHandler.Remove(this);
        }

        void OnPlayerInit(BasePlayer player)
        {
            if (player.GetComponent<BuildingPrivlidgeHandler>() == null)
                player.gameObject.AddComponent<BuildingPrivlidgeHandler>();
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (hitInfo == null || entity == null) return null;
            BuildingBlock block = entity as BuildingBlock;
            Door door = entity as Door;
            if (!(entity is Door) && !(entity is BuildingBlock)) return null;
            var building = entity.GetBuildingPrivilege();
            if (building == null) return null;
            if (buildingprotect.ContainsKey(building.net.ID))
                hitInfo.damageTypes.ScaleAll(1f - ((float)buildingprotect[building.net.ID].protect / 100f - 1f));
            return null;
        }

        void OnPrivilegeChange(BasePlayer player, BuildingPrivlidge privlidge)
        {
            if (player != null)
                if (player.IsBuildingAuthed())
                {
                    if (drawingUI.Contains(player))
                        drawingUI.Remove(player);
                    CuiHelper.DestroyUi(player, "DrawStatusMain");
                }
        }

        void OnPrivilegeEnter(BasePlayer player, BuildingPrivlidge privlidge)
        {
            if (player != null && privlidge != null)
                if (permission.UserHasPermission(player.UserIDString, config.defStation.premis))
                    if (player.IsBuildingAuthed())
                    {
                        if (!drawingUI.Contains(player))
                            drawingUI.Add(player);
                    }
        }

        void DrawUIInfo()
        {
            timer.Every(5.0f, () =>
            {
                if (drawingUI.Count > 0)
                    foreach (var player in drawingUI)
                        if (drawingUI.Contains(player))
                            DrawStatus(player);
            });
        }

        public static string FormatTime(TimeSpan time)
        {
            string result = string.Empty;
            if (time.Days != 0)
                result += $"{Format(time.Days, "дней", "дня", "день")} ";

            if (time.Hours != 0)
                result += $"{Format(time.Hours, "часов", "часа", "час")} ";

            if (time.Minutes != 0)
                result += $"{Format(time.Minutes, "минут", "мин.", "минута")} ";

            if (time.Seconds != 0)
                result += $"{Format(time.Seconds, "секунд", "сек.", "секунда")} ";

            return result;
        }
        private static string Format(int units, string form1, string form2, string form3)
        {
            var tmp = units % 10;

            if (units >= 5 && units <= 20 || tmp >= 5 && tmp <= 9)
                return $"{units} {form1}";

            if (tmp >= 2 && tmp <= 4)
                return $"{units} {form2}";

            return $"{units} {form3}";
        }

        private string GetMsg(string langkey, BasePlayer player, params object[] args)
        {
            string msg = lang.GetMessage(langkey, this, player?.UserIDString);
            if (args.Length > 0)
                msg = string.Format(msg, args);
            return msg;
        }

        void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["EnoughtIngridient"] = "{0} - {1}/{2}",
                ["NotEnoughtIngridient"] = "{0} - <color=#f44141>{1}</color>/{2}",
                ["RaidBlock"] = "Нельзя включить защиту во время рейда.",
            }, this);
        }

        Dictionary<string, string> Translated = new Dictionary<string, string>()
        {
            ["metal.fragments"] = "Металлические фрагменты",
            ["metal.refined"] = "Метал высокого качества",
            ["stones"] = "Камень",
            ["wood"] = "Дерево"
        };
    }
}
                                                                                                                                                                                                                                                                                                                                                                        