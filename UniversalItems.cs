using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Globalization;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using WebSocketSharp;

namespace Oxide.Plugins
{
    [Info("UniversalItems", "https://topplugin.ru/", "1.5.6")]
    [Description("Универсальные предметы")]
    public class UniversalItems : RustPlugin
    {
        public static UniversalItems instance;
        [PluginReference] private Plugin ImageLibrary;

        public enum Type
        {
            None = 0,
            Переплавить = 1,
            Переработать = 2,
            Потрошить = 3
        }

        public class ItemsDrop
        {
            [JsonProperty("Шорт нейм предмета")]
            public string Shortname;
            [JsonProperty("ID скина для предмета")]
            public ulong SkinID;
            [JsonProperty("Минимальное количество при выпадени")]
            public int MinimalAmount = 0;
            [JsonProperty("Максимальное количество при выпадении")]
            public int MaximumAmount = 0;
        }

        public class Setings
        {
            [JsonProperty("Включить иконку с информацией о предметах в углу экрана ?")]
            public bool GuiInfo;
            [JsonProperty("Включить радиацию при переплавки")]
            public bool Radiation;
            [JsonProperty("Количевство радиации каторое будет даваться каждый тик")]
            public float Radiations;
            [JsonProperty("Радиус радиации в метрах (От печки)")]
            public float RadiationsRadius;
            [JsonProperty("Текст который будет написан в гуи если радиация при переплавке включена")]
            public string RadiationGui;
            [JsonProperty("Титл в гуи")]
            public string TitleGui;
            [JsonProperty("Включить иконку с кнопкой крафта")] public bool ButonOpenMenu;
            [JsonProperty("Картинка для кнопки")] public string PngForButton;
            [JsonProperty("OffsetMin кнопки")] public string Ofssemin;
            [JsonProperty("OffsetMax кнопки")] public string Ofsetmax;
            [JsonProperty("Команда для открытия меню")] public string CommandOpen;
        }

        #region Lang
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TITLE_GUI"] = "UNIQUE ITEMS MENU",
                ["INFO_GUI"] = "TO FIND OUT DETAILED INFORMATION ABOUT THE SUBJECT INTERESTING IN YOU, CLICK ON IT :)",
                ["CLOSE_GUI"] = "CLOSE",
                ["BACK_GUI"] = "BACK",

            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TITLE_GUI"] = "МЕНЮ УНИКАЛЬНЫХ ПРЕДМЕТОВ",
                ["INFO_GUI"] = "ЧТО БЫ УЗНАТЬ ПОДРОБНУЮ ИНФОРМАЦИЮ О ИНТЕРЕСУЮЩЕМ ВАС ПРЕДМЕТЕ, НАЖМИТЕ НА НЕГО :)",
                ["CLOSE_GUI"] = "ЗАКРЫТЬ",
                ["BACK_GUI"] = "НАЗАД",

            }, this, "ru");
        }
        #endregion

        #region Config
        public Configurarion config;

        public class UItems
        {
            [JsonProperty("Названия предмета")]
            public string Name;
            [JsonProperty("Описания предмета")]
            public string Descriptions;
            [JsonProperty("Шорт нейм предмета")]
            public string ShortName;
            [JsonProperty("ID скина для предмета")]
            public ulong SkinIDI;
            [JsonProperty("Шанс выпадения")]
            public int Chance;
            [JsonProperty("Список ящиков в которых предмет будет выпадать")]
            public List<string> CrateDrop;
            [JsonProperty("Предметы которые будут выпадать")]
            public List<ItemsDrop> ItemsDrop;

            public int GetItemId() => ItemManager.FindItemDefinition(ShortName).itemid;
            public int GetItemAmount(BasePlayer player) => player.inventory.GetAmount(GetItemId());

            public Item Copy(int amount = 1)
            {
                Item x = ItemManager.CreateByPartialName(ShortName, amount);
                x.skin = SkinIDI;
                x.name = Name;
                return x;
            }

            public Item CreateItem(int amount)
            {
                Item item = ItemManager.CreateByPartialName(ShortName, amount);
                item.name = Name;
                item.skin = SkinIDI;
                return item;
            }
        }

        List<UItems> uItems = new List<UItems>();

        public class Configurarion
        {
            [JsonProperty("Настройки уникальных предметов")]
            public Dictionary<Type, UItems> ItemsSetings = new Dictionary<Type, UItems>();

            [JsonProperty("Настройки ")]
            public Setings setings;
        }


        protected override void LoadDefaultConfig()
        {
            config = new Configurarion()
            {
                ItemsSetings = new Dictionary<Type, UItems>
                {
                    [Type.Переработать] =
                   new UItems
                   {
                       Name = "ЗОЛОТАЯ ПОДКОВА УДАЧИ",
                       Descriptions = "С ЭТОЙ ПОДКОВОЙ ВАМ УЛЫБНЕТСЯ УДАЧА! ПЕРЕРАБОТАЙТЕ ЕЕ И ПОЛУЧИТЕ ЧТО-ТО ВЗАМЕН",
                       ShortName = "skull.human",
                       SkinIDI = 1742207203,
                       Chance = 30,
                       ItemsDrop = new List<ItemsDrop>
                       {
                            new ItemsDrop{Shortname = "smg.2", SkinID = 0, MinimalAmount = 1, MaximumAmount = 1 },
                            new ItemsDrop{Shortname = "grenade.smoke", SkinID = 0, MinimalAmount = 1, MaximumAmount = 5 },
                            new ItemsDrop{Shortname = "weapon.mod.silencer", SkinID = 0, MinimalAmount = 1, MaximumAmount = 1 },
                            new ItemsDrop{Shortname = "rifle.l96", SkinID = 0, MinimalAmount = 1, MaximumAmount = 1 }
                       },
                       CrateDrop = new List<string>
                       {
                            "bradley_crate", "crate_elite",
                       }
                   },
                    [Type.Потрошить] =
                   new UItems
                   {
                       Name = "ЗОЛОТОЙ ЧЕРЕП",
                       Descriptions = "ЗОЛОТОЙ ЧЕРЕП ЛУЧШАЯ ДОБЫЧА МАРОДЕРА! ПОТРОШИ ЭТОТ ЧЕРЕП ЧТОБЫ ЗАБРАТЬ ИЗ НЕГО ЧТО-ТО!",
                       ShortName = "skull.human",
                       SkinIDI = 1683645276,
                       Chance = 40,
                       ItemsDrop = new List<ItemsDrop>
                       {
                            new ItemsDrop{Shortname = "jackhammer", SkinID = 0, MinimalAmount = 1, MaximumAmount = 1 },
                            new ItemsDrop{Shortname = "explosive.timed", SkinID = 0, MinimalAmount = 1, MaximumAmount = 2 },
                            new ItemsDrop{Shortname = "supply.signal", SkinID = 0, MinimalAmount = 1, MaximumAmount = 1 },
                            new ItemsDrop{Shortname = "flare", SkinID = 0, MinimalAmount = 1, MaximumAmount = 15 },
                       },
                       CrateDrop = new List<string>
                       {
                            "heli_crate", "crate_tools"
                       }
                   },
                    [Type.Переплавить] =
                   new UItems
                   {
                       Name = "АРТЕФАКТ",
                       Descriptions = "НЕОБЫЧНЫЙ АРТЕФАКТ,ТАКИХ НЕТ НИГДЕ! ПРИ ПЕРЕПЛАВКИ ПРЕВРАЩАЕТСЯ В КАКОЙ-ТО ПРЕДМЕТ!",
                       ShortName = "glue",
                       SkinIDI = 1714466074,
                       Chance = 10,
                       ItemsDrop = new List<ItemsDrop>
                        {
                            new ItemsDrop{Shortname = "sulfur", SkinID = 0, MinimalAmount = 200, MaximumAmount = 300 },
                            new ItemsDrop{Shortname = "metal.refined", SkinID = 0, MinimalAmount = 10, MaximumAmount = 100 },
                            new ItemsDrop{Shortname = "stones", SkinID = 0, MinimalAmount = 1000, MaximumAmount = 5000 },
                            new ItemsDrop{Shortname = "scrap", SkinID = 0, MinimalAmount = 100, MaximumAmount = 300 },
                        },
                       CrateDrop = new List<string>
                        {
                            "crate_underwater_basic", "supply_drop"
                        }
                   },

                },
                setings = new Setings
                {
                    Radiation = true,
                    GuiInfo = true,
                    Radiations = 10f,
                    RadiationsRadius = 15f,
                    RadiationGui = "<color=#ffca29>ОСТОРОЖНО!</color>\nПри переплавке этого предмета могут распространяться частицы радиации",
                    TitleGui = "МЕНЮ УНИКАЛЬНЫХ ПРЕДМЕТОВ",
                    ButonOpenMenu = true,
                    PngForButton = "https://i.imgur.com/ffy28FG.png",
                    Ofssemin = "3 -50",
                    Ofsetmax = "50 -3",
                    CommandOpen = "Uitem"
                }

            };
            SaveConfig(config);
        }

        void SaveConfig(Configurarion config)
        {
            Config.WriteObject(config, true);
            SaveConfig();
        }

        public void LoadConfigVars()
        {
            config = Config.ReadObject<Configurarion>();
            Config.WriteObject(config, true);
        }
        #endregion

        #region Hooks

        [ChatCommand("Give.Utest")]
        private void CmdChatDebugGoldfSpawn(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            foreach (var Items in config.ItemsSetings)
            {
                var item = Items.Value.CreateItem(10);
                item.MoveToContainer(player.inventory.containerMain);
            }
        }

        bool furnacec = false;
        void OnServerInitialized()
        {
            LoadConfigVars();
            instance = this;
            furnacec = true;

            if (!ImageLibrary)
            {
                PrintError("Не найден ImageLibrary, плагин не будет работать!");
                return;
            }
            cmd.AddChatCommand(config.setings.CommandOpen, this, nameof(ChatInfoMenu));
            if (config.setings.ButonOpenMenu)
                ImageLibrary.Call("AddImage", config.setings.PngForButton, config.setings.CommandOpen);

            foreach (var cfg in config.ItemsSetings.Values)
            {
                if (cfg.ShortName.IsNullOrEmpty())
                {
                    ImageLibrary.Call("AddImage", $"http://rust.skyplugins.ru/getimage/{cfg.ShortName}/128", cfg.ShortName + 128, cfg.SkinIDI);
                }
                if (cfg.SkinIDI != 0)
                {
                    ImageLibrary.Call("AddImage", $"http://rust.skyplugins.ru/getskin/{cfg.SkinIDI}/", cfg.ShortName, cfg.SkinIDI);
                }
            }
            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                OnPlayerConnected(BasePlayer.activePlayerList[i]);
            }

            List<BaseOven> baseOvens = UnityEngine.Object.FindObjectsOfType<BaseOven>().ToList();
            baseOvens.ForEach(baseOven =>
            {
                if (!(baseOven is BaseFuelLightSource))
                {
                    OnEntitySpawned(baseOven);
                }
            });
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerConnected(player));
                return;
            }

            if (config.setings.ButonOpenMenu)
                InitializeIconButton(player);
        }

        void Unload()
        {
            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                CuiHelper.DestroyUi(BasePlayer.activePlayerList[i], IconLayer);
                CuiHelper.DestroyUi(BasePlayer.activePlayerList[i], MAINMENU);
            }
        }

        private Item CreateItem(string Index)
        {
            int RandomItem = UnityEngine.Random.Range(0, config.ItemsSetings.Count);

            var cfg = config.ItemsSetings.ElementAt(RandomItem).Value;
            if (cfg.CrateDrop.Contains(Index))
            {
                bool goodChance = UnityEngine.Random.Range(0, 100) >= (100 - cfg.Chance);
                if (goodChance)
                {
                    Item itemS = ItemManager.CreateByName(cfg.ShortName, 1, cfg.SkinIDI);
                    itemS.name = cfg.Name;
                    return itemS;
                }
            }
            return null;
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity == null || config==null ||config.ItemsSetings ==null || config.ItemsSetings.Count<1) return;			
            if (entity.GetComponent<LootContainer>())
            {
                int RandomItem = UnityEngine.Random.Range(0, config.ItemsSetings.Count);

                var cfg = config.ItemsSetings.ElementAt(RandomItem).Value;
				if (cfg==null || cfg.CrateDrop==null) return;
				if (cfg.Chance>100){
					PrintError("The chance can't be more than 100%");
					return;
				}
                if (cfg.CrateDrop.Contains(entity.ShortPrefabName))
                {
                    bool goodChance = UnityEngine.Random.Range(0, 100) >= (100 - cfg.Chance);
                    if (goodChance)
                    {
                        var item = cfg.Copy();
						if (item==null) return;
                        item?.MoveToContainer(entity.GetComponent<LootContainer>().inventory);
                    }
                }
            }
            if (!furnacec) return;
            if (entity is BaseOven && !(entity is BaseFuelLightSource))
            {
                BaseOven baseOven = entity as BaseOven;
                if (baseOven == null) return;
                FurnaceBurn fBurn = new FurnaceBurn();
                fBurn.OvenTogle(baseOven);
            }
        }

        object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (item == null || action == null || action == "")
                return null;
            if (action != "crush")
                return null;
            if (player == null)
                return null;
            var cfg = config.ItemsSetings[Type.Потрошить];
            if (cfg.SkinIDI != item.skin) return null;

            int RandomItem = UnityEngine.Random.Range(0, cfg.ItemsDrop.Count);
            Item itemS = ItemManager.CreateByName(cfg.ItemsDrop[RandomItem].Shortname, UnityEngine.Random.Range(cfg.ItemsDrop[RandomItem].MinimalAmount, cfg.ItemsDrop[RandomItem].MaximumAmount), cfg.ItemsDrop[RandomItem].SkinID);
            if (!player.inventory.containerMain.IsFull()) itemS.MoveToContainer(player.inventory.containerMain);
            else player.GiveItem(itemS, BaseEntity.GiveItemReason.PickedUp);
            ItemRemovalThink(item, player, 1);
            return false;
        }

        object CanRecycle(Recycler recycler, Item item)
        {
            var cfg = config.ItemsSetings[Type.Переработать];

            if (item.info.shortname == cfg.ShortName && item.skin == cfg.SkinIDI)
            {
                var itemInfo = ItemManager.FindItemDefinition(cfg.ShortName);
                if (itemInfo.Blueprint == null) itemInfo.gameObject.AddComponent<ItemBlueprint>();
                itemInfo.Blueprint.amountToCreate = 1;
                return true;
            }
            return null;
        }

        private object OnRecycleItem(Recycler recycler, Item item)
        {
            var cfg = config.ItemsSetings[Type.Переработать];

            if (cfg.SkinIDI == item.skin)
            {
                int RandomItem = UnityEngine.Random.Range(0, cfg.ItemsDrop.Count);
                recycler.MoveItemToOutput(ItemManager.CreateByName(cfg.ItemsDrop[RandomItem].Shortname, UnityEngine.Random.Range(cfg.ItemsDrop[RandomItem].MinimalAmount, cfg.ItemsDrop[RandomItem].MaximumAmount), cfg.ItemsDrop[RandomItem].SkinID));
            }
            return null;
        }

        object CanCombineDroppedItem(DroppedItem item, DroppedItem targetItem)
        {
            if (item.GetItem().skin != targetItem.GetItem().skin) return false;
            return null;
        }

        object CanStackItem(Item item, Item targetItem)
        {
            if (item.skin != targetItem.skin) return false;
            return null;
        }

        private Item OnItemSplit(Item item, int amount)
        {
            if (plugins.Find("Stacks") || plugins.Find("CustomSkinsStacksFix") || plugins.Find("SkinBox")) return null;

            var customItem = config.ItemsSetings.FirstOrDefault(p => item.skin == p.Value.SkinIDI);
            if (customItem.Value != null)
            {
                Item x = ItemManager.CreateByPartialName(customItem.Value.ShortName, amount);
                x.name = customItem.Value.Name;
                x.skin = customItem.Value.SkinIDI;
                x.amount = amount;

                item.amount -= amount;
                return x;
            }
            return null;
        }

        private static void ItemRemovalThink(Item item, BasePlayer player, int itemsToTake)
        {
            if (item.amount == itemsToTake)
            {
                item.RemoveFromContainer();
                item.Remove();
            }
            else
            {
                item.amount = item.amount - itemsToTake;
                player.inventory.SendSnapshot();
            }
        }

        #endregion

        #region Class 

        public class FurnaceBurn
        {
            BaseOven oven;
            StorageContainer storageContainer;
            Timer timer;

            public void OvenTogle(BaseOven oven)
            {
                this.oven = oven;
                storageContainer = oven.GetComponent<StorageContainer>();
                timertick();
            }

            void timertick()
            {
                if (timer == null)
                {
                    timer = instance.timer.Once(5f, CheckRadOres);
                }
                else
                {
                    timer.Destroy();
                    timer = instance.timer.Once(5f, CheckRadOres);
                }
            }

            void CheckRadOres()
            {
                if (oven == null)
                {
                    timer.Destroy();
                    return;
                }
                if (oven.IsOn())
                {
                    foreach (var item in storageContainer.inventory.itemList)
                    {
                        if (instance.config.ItemsSetings[Type.Переплавить].SkinIDI == item.skin)
                        {
                            var cfg = instance.config.ItemsSetings[Type.Переплавить];

                            instance.NextTick(() =>
                            {
                                if (instance.config.setings.Radiation)
                                {
                                    List<BasePlayer> players = new List<BasePlayer>();
                                    Vis.Entities<BasePlayer>(oven.transform.position, instance.config.setings.RadiationsRadius, players);
                                    players.ForEach(p => p.metabolism.radiation_poison.value += instance.config.setings.Radiations);
                                }

                                if (item.amount > 1) item.amount--;
                                else item.RemoveFromContainer();

                                int RandomItem = UnityEngine.Random.Range(0, cfg.ItemsDrop.Count);
                                Item newItem = ItemManager.CreateByName(cfg.ItemsDrop[RandomItem].Shortname, UnityEngine.Random.Range(cfg.ItemsDrop[RandomItem].MinimalAmount, cfg.ItemsDrop[RandomItem].MaximumAmount), cfg.ItemsDrop[RandomItem].SkinID);
                                if (!newItem.MoveToContainer(storageContainer.inventory))
                                    newItem.Drop(oven.transform.position, Vector3.up);
                            });
                        }
                    }
                }
                timertick();
            }
        }
        #endregion
        public static string MAINMENU = "MAIN_MENU";
        public static string IconLayer = "MAIN_MENU_ICON";

        private void InitializeIconButton(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, IconLayer);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Parent = "Overlay",
                Name = IconLayer,
                Components =
                {
                      new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", config.setings.CommandOpen) },
                      new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = config.setings.Ofssemin, OffsetMax = config.setings.Ofsetmax}
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = $"chat.say /{config.setings.CommandOpen}" },
                Text = { Text = "" }
            }, IconLayer);

            CuiHelper.AddUi(player, container);
        }
        private void GUI_MAIN(BasePlayer player, Type type)
        {
            var Gui = new CuiElementContainer();

            if (type == Type.None)
            {
                CuiHelper.DestroyUi(player, MAINMENU);
                CuiHelper.DestroyUi(player, "ElementInfo");

                Gui.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Image = { Color = "0 0 0 0.8", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }
                }, "Overlay", MAINMENU);

                Gui.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "-100 -100", AnchorMax = "100 100" },
                    Button = { Close = MAINMENU, Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, MAINMENU);

                Gui.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.8796875 0.9425926", AnchorMax = "0.9947916 0.9953704" },
                    Button = { Close = MAINMENU, Color = "0 0 0 0" },
                    Text = { Text = lang.GetMessage("CLOSE_GUI", this, player.UserIDString), FontSize = 24, Align = TextAnchor.MiddleCenter }
                }, MAINMENU);

                Gui.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.0989586 0.2009263", AnchorMax = "0.8859376 0.942593" },
                    Image = { Color = "0 0 0 0" }
                }, MAINMENU, "main");

                Gui.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.433208", OffsetMax = "0 0" },
                    Text = { Text = lang.GetMessage("INFO_GUI", this, player.UserIDString), FontSize = 23, Font = "RobotoCondensed-Regular.ttf", Align = TextAnchor.MiddleCenter }
                }, "main");

                Gui.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0.8589257", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = lang.GetMessage("TITLE_GUI", this, player.UserIDString), FontSize = 23, Font = "RobotoCondensed-Regular.ttf", Align = TextAnchor.MiddleCenter }
                }, "main");

                int x = 0;

                foreach (var element in config.ItemsSetings)
                {
                    Gui.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{0.08934465 + (x * 0.33)} 0.3770282", AnchorMax = $"{0.3011248 + (x * 0.33)} 0.7765288" },
                        Image = { Color = HexToRustFormat("#5A5A5A71") }
                    }, "main", "elements");

                    Gui.Add(new CuiElement
                    {
                        Parent = "elements",
                        Components = {
                    new CuiRawImageComponent
                    {
                        Png = (string)ImageLibrary.Call("GetImage", element.Value.ShortName, element.Value.SkinIDI), Color = "1 1 1 1",
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.016949 0.01910812",
                        AnchorMax = "0.983051 0.9840761"
                    },
                    }
                    });
                    int en = Convert.ToInt32(element.Key);
                    Gui.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Button = { Color = "0 0 0 0", Command = $"GoToInfo {en}" },
                        Text = { Text = "", Color = "0 0 0 0" }
                    }, "elements");
                    x++;
                }
            }

            Type types = type == Type.Переплавить ? Type.Переплавить : type == Type.Переработать ? Type.Переработать : type == Type.Потрошить ? Type.Потрошить : Type.None;
            if (types != Type.None)
            {
                var cfg = config.ItemsSetings[types];

                CuiHelper.DestroyUi(player, "main");

                Gui.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.0989586 0.2009263", AnchorMax = "0.8859376 0.942593" },
                    Image = { Color = "0 0 0 0" }
                }, MAINMENU, "ElementInfo");

                Gui.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0.8589257", AnchorMax = "1 1", OffsetMax = "0 0" },
                    Text = { Text = cfg.Name, FontSize = 25, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
                }, "ElementInfo");

                Gui.Add(new CuiElement
                {
                    Parent = "ElementInfo",
                    Components = {
                    new CuiRawImageComponent
                    {
                        Png = (string)ImageLibrary.Call("GetImage", cfg.ShortName, cfg.SkinIDI), Color = "1 1 1 1",
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.4003971 0.4519346",
                        AnchorMax = "0.5989414 0.8264664"
                    },
                    }
                });

                Gui.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.4157298" },
                    Text = { Text = cfg.Descriptions, FontSize = 21, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
                }, "ElementInfo");

                if (types == Type.Переплавить && config.setings.Radiation == true)
                {
                    Gui.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.1410731" },
                        Text = { Text = config.setings.RadiationGui.ToUpper(), FontSize = 19, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
                    }, "ElementInfo");
                }

                Gui.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0.9425926", AnchorMax = "0.1052083 1" },
                    Button = { Command = "GoToInfo 0", Color = "0 0 0 0" },
                    Text = { Text = lang.GetMessage("BACK_GUI", this, player.UserIDString), FontSize = 24, Align = TextAnchor.MiddleCenter }
                }, MAINMENU);
            }

            CuiHelper.AddUi(player, Gui);

        }
        #region Help

        void ChatInfoMenu(BasePlayer player)
        {
            GUI_MAIN(player, 0);
        }

        [ConsoleCommand("GoToInfo")]
        void CommandOpenInfo(ConsoleSystem.Arg arg)
        {
            int enums = Convert.ToInt32(arg.Args[0]);
            Type types = (Type)enums;
            GUI_MAIN(arg.Player(), types);
        }

        private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                hex = "#FFFFFFFF";
            }

            var str = hex.Trim('#');
            if (str.Length == 6)
                str += "FF";
            if (str.Length != 8)
            {
                throw new Exception(hex);
                throw new InvalidOperationException("Cannot convert a wrong format #868.");
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);
            Color color = new Color32(r, g, b, a);
            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }
        #endregion
    }
}
