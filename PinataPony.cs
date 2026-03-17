using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("PinataPony", "TopPlugin.ru", "0.0.2")]
    [Description("Наипездайтейший кликер 228 1337 аоаоа")]
    class PinataPony : RustPlugin
    {
        #region Reference
        [PluginReference] Plugin ImageLibrary, CorvusStatistick;
        public string GetImage(string shortname, ulong skin = 0) => (string)ImageLibrary?.Call("GetImage", shortname, skin);
        public bool AddImage(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname, skin);

        #endregion

        #region Configuration 
        private static Configuration config = new Configuration();
        private class Configuration
        {
            [JsonProperty("Настройка пиньят")]
            public Dictionary<string, PinataSettings> pinataSettings = new Dictionary<string, PinataSettings>();
            [JsonProperty("Дополнительная настройка пиньят")]
            public MoreSettingsPinata moreSettingsPinata;
            [JsonProperty("Настройка двойного клика")]
            public BoostClass boostClass;
            [JsonProperty("Настройка выпадения из ящиков[PREFABNAME] = CHANCE")]
            public Dictionary<string, int> RareDrop = new Dictionary<string, int>();

            #region PinataSettings
            internal class PinataSettings
            {
                [JsonProperty("Название пиньяты")] public string DisplayName;
                [JsonProperty("Количество кликов на пиньяту")] public int ClickCount;
                [JsonProperty("Ссылка на картинку пиньяты")] public string PNG;
                [JsonProperty("Настройка листа призов с пиньяты")] public List<PrizeSettings> PrizeList;

                internal class PrizeSettings
                {
                    [JsonProperty("Команда или предмет(true - command | false - item)")] public bool Command_Item;
                    [JsonProperty("Название для приза")] public string DisplayName;

                    [JsonProperty("Картинка для команды")] public string PngCommand;
                    [JsonProperty("Команда")] public string Command;
                    [JsonProperty("Шортнейм, если используете команду - это статическое имя команды")] public string Shortname;
                    [JsonProperty("Минимальное количество для выдачи предмета")] public int MinAmount;
                    [JsonProperty("Максимальное количество для выдачи предмета")] public int MaxAmount;
                }
            }
            #endregion

            #region MoreSettingsPinata

            internal class MoreSettingsPinata
            {
               [JsonProperty("Звук при развертывании пинаты(разрушения)")] public string PinataCrush;
               [JsonProperty("Звук когда игрок забирает приз")] public string PrizeTake;
               [JsonProperty("Звук при клике")] public string ClickSound;
            }

            #endregion

            #region BoostSettingsPinata

            internal class BoostClass
            {
                [JsonProperty("Название улучшения")] public string BoostName;
                [JsonProperty("Описание улучшения")] public string BoostDescription;
            }

            #endregion

            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    #region PinataSettings
                    pinataSettings = new Dictionary<string, PinataSettings>
                    {
                        ["default"] = new PinataSettings
                        {
                            DisplayName = "Простая пиньята",
                            ClickCount = 50,
                            PNG = "",
                            PrizeList = new List<PinataSettings.PrizeSettings>
                            {
                                new PinataSettings.PrizeSettings
                                {
                                    Command_Item = false,
                                    PngCommand = "",
                                    DisplayName = "Дерево",
                                    Command = "",
                                    Shortname = "wood",
                                    MinAmount = 2000,
                                    MaxAmount = 5000
                                },
                                new PinataSettings.PrizeSettings
                                {
                                    Command_Item = true,
                                    PngCommand = "https://i.imgur.com/67nMYdA.jpg",
                                    DisplayName = "VIP",
                                    Command = "give vip 7d",
                                    Shortname = "vipone",
                                    MinAmount = 7,
                                    MaxAmount = 7
                                },
                            }
                        }
                    },
                    #endregion

                    #region MoreSettingsPinata

                    moreSettingsPinata = new MoreSettingsPinata
                    {
                        ClickSound = "assets/prefabs/tools/detonator/effects/attack.prefab",
                        PrizeTake = "assets/prefabs/deployable/dropbox/effects/submit_items.prefab",
                        PinataCrush = "assets/prefabs/misc/xmas/presents/effects/unwrap.prefab"
                    },

                    #endregion

                    #region BoostClass
                    boostClass = new BoostClass
                    {
                        BoostName = "<b><size=24>x2</size></b> клик",
                        BoostDescription = "*ваши клики по пиньяте будут удваиваться",
                    },
                    #endregion

                    #region RareDrop
                    RareDrop = new Dictionary<string, int>
                    {
                        ["crate_normal"] = 15,
                        ["crate_normal_2"] = 15,
                        ["crate_basic"] = 10,
                        ["crate_elite"] = 30,
                        ["crate_mine"] = 3,
                        ["crate_tools"] = 8,
                    }
                    #endregion
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
                PrintWarning($"Ошибка чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Data
        [JsonProperty("Пинаты игрока")] public Dictionary<ulong, Dictionary<string, DataClass>> DataPlugins = new Dictionary<ulong, Dictionary<string, DataClass>>();
        [JsonProperty("Инвентарь игрока")] public Dictionary<ulong, List<InventoryClass>> InventoryUser = new Dictionary<ulong, List<InventoryClass>>();
        [JsonProperty("Усиления игрока")] public Dictionary<ulong, int> BoostUser = new Dictionary<ulong, int>();

        public class DataClass
        {
            [JsonProperty("Количество пиньят у игрока")] public int PinataAmount;
            [JsonProperty("Количество сделанных для данной пинаты")] public int PinataClick;
        }

        public class InventoryClass
        {
            [JsonProperty("Команда или предмет(true - command | false - item)")] public bool Command_Item;
            [JsonProperty("Название для приза")] public string DisplayName;

            [JsonProperty("Команда")] public string Command;
            [JsonProperty("Шортнейм")] public string Shortname;
            [JsonProperty("Количество для выдачи предмета")] public int Amount;
        }

        void ReadData()
        {
            DataPlugins = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<string, DataClass>>>("PinataPony/DataPlugins");
            InventoryUser = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<InventoryClass>>>("PinataPony/InventoryPlayers");
            BoostUser = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, int>>("PinataPony/BoostPlayer");
        }
        void WriteData()
        {
            timer.Every(60f, () =>
            {
                Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("PinataPony/DataPlugins", DataPlugins);
                Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("PinataPony/InventoryPlayers", InventoryUser);
                Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("PinataPony/BoostPlayer", BoostUser);
            });
        }

        void RegisteredDataUser(BasePlayer player)
        {
            if (!DataPlugins.ContainsKey(player.userID))
                 DataPlugins.Add(player.userID, new Dictionary<string, DataClass>());
            if (!InventoryUser.ContainsKey(player.userID))
                InventoryUser.Add(player.userID, new List<InventoryClass> { });
            if (!BoostUser.ContainsKey(player.userID))
                BoostUser.Add(player.userID, 0);
        }

        #endregion

        #region Hooks
        private void OnServerInitialized()
        {
            ReadData();
            BasePlayer.activePlayerList.ForEach(p => RegisteredDataUser(p));
            for (int i = 0; i < config.pinataSettings.Count; i++)
            {
                AddImage(config.pinataSettings.ElementAt(i).Value.PNG, config.pinataSettings.ElementAt(i).Key);
                for(int j = 0; j < config.pinataSettings.ElementAt(i).Value.PrizeList.Count; j++)
                {
                    var PrizeList = config.pinataSettings.ElementAt(i).Value.PrizeList[j];
                    if (PrizeList.Command_Item)
                        AddImage(PrizeList.PngCommand, PrizeList.Shortname);
                }
            }
            WriteData();
        }
        private void OnPlayerConnected(BasePlayer player) => RegisteredDataUser(player);
        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (entity is SupplyDrop) return;
            if (entity is LockedByEntCrate) return;
            if (entity is Stocking) return;
            if (entity is HackableLockedCrate) return;

            var lootcont = entity as LootContainer;        
            if (!lootcont) return;
            if (lootcont.OwnerID == 1337228) return;           
            lootcont.OwnerID = 1337228;        

            DropPinataMetods(player, lootcont.ShortPrefabName);
        }
        #endregion

        #region Command

        [ChatCommand("pinata")]
        void OpenMenuPinata(BasePlayer player)
        {
            InterfacePinata(player);
        }

        [ConsoleCommand("pinatapony")]
        void ConsoleCommandPinata(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (args == null || args.Args.Length == 0 || args.Args.Length < 1)
                InterfacePinata(player);

            switch (args.Args[0])
            {
                case "more_detalis":
                    {
                        var IndexPinata = args.Args[1];
                        MoreDetalisPinata(player, IndexPinata);
                        break;
                    }
                case "more_detalis_item":
                    {
                        var IndexPinata = args.Args[1];
                        var IndexItem = Convert.ToInt32(args.Args[2]);
                        bool Status = Convert.ToBoolean(args.Args[3]);
                        MoreDetalisPinataItem(player, IndexPinata,IndexItem, Status);
                        break;
                    }
                case "click_pinata":
                    {
                        var IndexPinata = args.Args[1];
                        var Index = Convert.ToInt32(args.Args[2]);
                        ClickMetods(player, IndexPinata, Index);
                        break;
                    }
                case "take_my_prize":
                    {
                        if (AntiSpam(player))
                        {
                            string IndexPinata = args.Args[1];
                            int PrizeIndex = Convert.ToInt32(args.Args[2]);
                            int AmountPrize = Convert.ToInt32(args.Args[3]);
                            CuiHelper.DestroyUi(player, MAIN_PINATA_PRIZE);
                            MoveToInventoryPrize(player, IndexPinata, PrizeIndex, AmountPrize);
                        }
                        break;
                    }
                case "open_inventory":
                    {
                        InventoryPrizePlayers(player);
                        break;
                    }
                case "take_item_in_inventory":
                    {
                        int Slot = Convert.ToInt32(args.Args[1]);
                        TakeInInventoryPrize(player, Slot);
                        break;
                    }
                case "pp_boost":
                    {
                        ulong userID = ulong.Parse(args.Args[1]);
                        int ClickCount = Convert.ToInt32(args.Args[2]);
                        GiveBoostClick(userID, ClickCount);
                        break;
                    }
            }
        }

        [ChatCommand("pin_pony")]
        void PinataAdminCommand(BasePlayer player,string cmd, string[] args)
        {
            if (!player.IsAdmin) return;
            switch (args[0])
            {
                case "all":
                    {
                        for (int i = 0; i < config.pinataSettings.Count; i++)
                        {
                            var PinataKey = config.pinataSettings.ElementAt(i).Key;
                            if (!DataPlugins[player.userID].ContainsKey(PinataKey))
                            {
                                DataClass dataClass = new DataClass();
                                dataClass.PinataAmount = 99;
                                dataClass.PinataClick = 0;
                                DataPlugins[player.userID].Add(PinataKey, dataClass);
                            }
                        }
                        BoostUser[player.userID] = 100;
                        ReplyWithHelper(player, "Вы успешно получили все пинаты!");
                        break;
                    }
                case "one":
                    {
                        var PinataKey = config.pinataSettings.ElementAt(1).Key;
                        if (!DataPlugins[player.userID].ContainsKey(PinataKey))
                        {
                            DataClass dataClass = new DataClass();
                            dataClass.PinataAmount = 99;
                            dataClass.PinataClick = 0;
                            DataPlugins[player.userID].Add(PinataKey, dataClass);
                        }
                        BoostUser[player.userID] = 100;
                        ReplyWithHelper(player, "Вы успешно получили одну пинату!");
                        break;
                    }             
            }
        }

        #endregion

        #region Interface

        static string MAIN_PARENT = "MAIN_PARENT_UI";
        static string MAIN_MORE_PARENT = "MAIN_MORE_PARENT_UI";
        static string MAIN_PINATA_PRIZE = "MAIN_PINATA_PRIZE_UI";
        static string MAIN_PINATA_PRIZE_INVENTORY = "MAIN_PINATA_PRIZE_INVENTORY_UI";

        #region InterfacePinata

        void InterfacePinata(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, MAIN_PARENT);

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                FadeOut = 0.5f,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { FadeIn = 0.5f, Color = "0 0 0 0.3", Material = "assets/content/ui/uibackgroundblur.mat", Sprite = "assets/content/ui/ui.background.transparent.radial.psd" }
            }, "Overlay", MAIN_PARENT);

            #region BoostLabel

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.003645843 0.8138889", AnchorMax = "0.2125 0.8472223" },
                Text = { Text = lang.GetMessage("UI_BOOST_TITLE", this, player.UserIDString), Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = HexToRustFormat("#FFFFFF8B") }
            }, MAIN_PARENT, "BOOST_TITLE");

            #endregion

            #region Labels

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.9074074", AnchorMax = "1 0.9740741" },
                Text = { Text = lang.GetMessage("UI_TITLE",this, player.UserIDString), Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = HexToRustFormat("#FFFFFF8B") }
            },  MAIN_PARENT);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.8722222", AnchorMax = "1 0.925926" },
                Text = { Text = lang.GetMessage("UI_TWO_TITLE",this,player.UserIDString), Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = HexToRustFormat("#FFFFFF8B") }
            }, MAIN_PARENT);

            container.Add(new CuiLabel
            {
                FadeOut = 0.5f,
                RectTransform = { AnchorMin = "0 0.07685184", AnchorMax = "1 0.3296296" },
                Text = { Text = lang.GetMessage("UI_DESCRIPTION",this,player.UserIDString), Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = HexToRustFormat("#FFFFFF8B") }
            }, MAIN_PARENT,"UI_DESCRIPTION_MAIN");

            #endregion

            #region PinataShow

            if (DataPlugins[player.userID].Count == 0)
            {
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Text = { Text = string.Format(lang.GetMessage("UI_NULL_PINATA", this, player.UserIDString)), Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = HexToRustFormat("#FFFFFF8B") }
                },  MAIN_PARENT, $"PINATA_NULL");            
            }

            #region CenterFunc
            int ItemCount = 0;
            float itemMinPosition = 219f;
            float itemWidth = 0.533646f - 0.351563f;
            float itemMargin = 0.439895f - 0.403646f;
            int itemCount = DataPlugins[player.userID].Count;
            float itemMinHeight = 0.405741f;
            float itemHeight = 0.738333f - 0.315741f;

            if (itemCount > 3)
            {
                itemMinPosition = 0.5f - 3 / 2f * itemWidth - (3 - 1) / 2f * itemMargin;
                itemCount -= 3;
            }
            else itemMinPosition = 0.5f - itemCount / 2f * itemWidth - (itemCount - 1) / 2f * itemMargin;
            #endregion

            for (int i = 0; i < DataPlugins[player.userID].Count; i++) 
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{itemMinPosition} {itemMinHeight}", AnchorMax = $"{itemMinPosition + itemWidth} {itemMinHeight + itemHeight}" },
                    Image = { Color = "0 0 0 0.3", Sprite = "assets/content/ui/ui.background.transparent.linear.psd" }
                }, MAIN_PARENT, $"PINATA_{ItemCount}");

                var DataPlayer = DataPlugins[player.userID].ElementAt(i);
                var ImageKey = DataPlayer.Key;

                container.Add(new CuiElement
                {
                    Parent = $"PINATA_{ItemCount}",
                    Name = $"PINATA_IMG_{ItemCount}",
                    Components =
                        {
                        new CuiRawImageComponent { Png = GetImage(ImageKey),  Color = HexToRustFormat("#FFFFFFFF") },
                        new CuiRectTransformComponent{  AnchorMin = $"0 0", AnchorMax = $"1 1" },
                        }
                });

                #region Labels

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.05602239" },
                    Text = { Text = string.Format(lang.GetMessage("UI_HEALTH", this, player.UserIDString), config.pinataSettings.FirstOrDefault(x => x.Key == DataPlayer.Key).Value.ClickCount - DataPlayer.Value.PinataClick), Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = HexToRustFormat("#FFFFFF") }
                }, $"PINATA_{ItemCount}", $"PINATA_UI_HIT_{ItemCount}");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0.8515407", AnchorMax = "1 0.9243696" },
                    Text = { Text = string.Format(lang.GetMessage("UI_LEFT", this, player.UserIDString), DataPlayer.Value.PinataAmount), Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperCenter, FontSize = 12, Color = HexToRustFormat("#FFFFFF") }
                }, $"PINATA_{ItemCount}", $"PINATA_LEFT_{ItemCount}");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0.9075632", AnchorMax = "1 1" },
                    Text = { Text = config.pinataSettings[DataPlayer.Key].DisplayName, Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperCenter, FontSize = 17, Color = HexToRustFormat("#FFFFFF") }
                }, $"PINATA_{ItemCount}", $"PINATA_UI_NAME_{ItemCount}");

                #endregion

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "0 -30", OffsetMax = "235 -5" },
                    Button = { Command = $"pinatapony more_detalis {DataPlayer.Key}", Color = HexToRustFormat("#3B85F5B1") },
                    Text = { Text = lang.GetMessage("UI_MORE", this, player.UserIDString), Color = HexToRustFormat("#FFFFFFFF"), Align = TextAnchor.MiddleCenter }
                }, $"PINATA_{ItemCount}");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = $"pinatapony click_pinata {DataPlayer.Key} {ItemCount}", Color = "0 0 0 0" },
                    Text = { Text = "", Color = HexToRustFormat("#FFFFFFFF"), Align = TextAnchor.MiddleCenter }
                }, $"PINATA_{ItemCount}");

                #region CenterFunc
                ItemCount++;
                itemMinPosition += (itemWidth + itemMargin);
                if (ItemCount % 3 == 0)
                {
                    itemMinHeight -= (itemHeight + (itemMargin * 2f));

                    if (itemCount > 3)
                    {
                        itemMinPosition = 0.5f - 3 / 2f * itemWidth - (3 - 1) / 2f * itemMargin;
                        itemCount -= 3;
                    }
                    else itemMinPosition = 0.5f - itemCount / 2f * itemWidth - (itemCount - 1) / 2f * itemMargin;
                }
                #endregion
            }

            #endregion

            #region Buttons
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.417708 0.002777847", AnchorMax = "0.5963538 0.06203704" },
                Button = { Command = $"pinatapony open_inventory", Color = "0 0 0 0.65" },
                Text = { Text = lang.GetMessage("UI_INVENTORY_TITLE", this, player.UserIDString), Color = HexToRustFormat("#FFFFFF8B"), Align = TextAnchor.MiddleCenter }
            }, MAIN_PARENT);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.91 0.95", AnchorMax = "1 0.999" },
                Button = { Close = MAIN_PARENT, Color = "0 0 0 0.1" },
                Text = { Text = lang.GetMessage("UI_CLOSE_BUTTON", this, player.UserIDString), Align = TextAnchor.UpperLeft }
            }, MAIN_PARENT);

            #endregion

            CuiHelper.AddUi(player, container);
            ShowMyBoost(player);
        }

        #endregion

        #region BoostUI

        void ShowMyBoost(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, "MODULES_BOOST_TITLE");

            var Boost = config.boostClass;
            string BoostText = IsBoost(player) ? $"{Boost.BoostName} : {BoostUser[player.userID]} кликов\n{Boost.BoostDescription}" : lang.GetMessage("UI_BOOST_NULL", this,player.UserIDString);
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.003645843 0.7425926", AnchorMax = "0.2125 0.8175936" },
                Text = { Text = BoostText, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = HexToRustFormat("#FFFFFF8B") }
            }, MAIN_PARENT, "MODULES_BOOST_TITLE");
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region MoreDetalisPinata

        void MoreDetalisPinata(BasePlayer player,string IndexPinata)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, "UI_DESCRIPTION_MAIN");
            CuiHelper.DestroyUi(player, MAIN_MORE_PARENT);
            var PinataInfo = config.pinataSettings[IndexPinata];

            container.Add(new CuiPanel
            {
                FadeOut = 0.5f,
                RectTransform = { AnchorMin = "0 0.07685184", AnchorMax = "1 0.3296296" },
                Image = {FadeIn = 0.5f, Color = "0 0 0 0.33" }
            }, MAIN_PARENT, MAIN_MORE_PARENT);

            #region Labels

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = { Text = string.Format(lang.GetMessage("UI_MORE_TITLE", this, player.UserIDString)), Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperCenter, Color = HexToRustFormat("#FFFFFF8B") }
            },  MAIN_MORE_PARENT);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.8442031" },
                Text = { Text = string.Format(lang.GetMessage("UI_MORE_DESCRIPTION", this, player.UserIDString)), Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperCenter, Color = HexToRustFormat("#FFFFFF8B") }
            }, MAIN_MORE_PARENT);

            #endregion

            #region ItemListFunc

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.6884059" },
                Image = { Color = "0 0 0 0.5",Sprite = "assets/content/ui/ui.background.transparent.linear.psd" }
            },  MAIN_MORE_PARENT, "ITEM_LIST_MORE");

            #region CenterFunc
            int ItemCount = 0;
            float itemMinPosition = 219;
            float itemWidth = 0.403646f - 0.351563f;
            float itemMargin = 0.411895f - 0.403646f;
            int itemCount = PinataInfo.PrizeList.Count;
            float itemMinHeight = 0.505741f;
            float itemHeight = 0.768333f - 0.315741f;

            if (itemCount > 15)
            {
                itemMinPosition = 0.5f - 15 / 2f * itemWidth - (15 - 1) / 2f * itemMargin;
                itemCount -= 15;
            }
            else itemMinPosition = 0.5f - itemCount / 2f * itemWidth - (itemCount - 1) / 2f * itemMargin;
            #endregion

            for (int i = 0; i < PinataInfo.PrizeList.Count; i++)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{itemMinPosition} {itemMinHeight}", AnchorMax = $"{itemMinPosition + itemWidth} {itemMinHeight + itemHeight}" },
                    Image = { Color = "0 0 0 0.5" }
                }, "ITEM_LIST_MORE", $"ITEM_{ItemCount}");

                container.Add(new CuiElement
                {
                    Parent = $"ITEM_{ItemCount}",
                    Components =
                        {
                        new CuiRawImageComponent { Png = GetImage(PinataInfo.PrizeList[ItemCount].Shortname),  Color = HexToRustFormat("#FFFFFFFF") },
                        new CuiRectTransformComponent{  AnchorMin = $"0 0", AnchorMax = $"1 1" },
                        }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = $"pinatapony more_detalis_item {IndexPinata} {ItemCount} {false}", Color = "0 0 0 0" },
                    Text = { Text = "", Color = HexToRustFormat("#F36E6EFF"), Align = TextAnchor.MiddleCenter }
                },  $"ITEM_{ItemCount}",$"BUTTON_ITEM_{ItemCount}");

                #region CenterFunc
                ItemCount++;
                itemMinPosition += (itemWidth + itemMargin);
                if (ItemCount % 15 == 0)
                {
                    itemMinHeight -= (itemHeight + (itemMargin * 2f));

                    if (itemCount > 15)
                    {
                        itemMinPosition = 0.5f - 10 / 2f * itemWidth - (15 - 1) / 2f * itemMargin;
                        itemCount -= 15;
                    }
                    else itemMinPosition = 0.5f - itemCount / 2f * itemWidth - (itemCount - 1) / 2f * itemMargin;
                }
                #endregion
            }

            #endregion

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region ShowMyPrizeUI

        public Dictionary<ulong, List<int>> PrizeListUserTake = new Dictionary<ulong, List<int>>();
        public void PrizeRandoms(BasePlayer player, string IndexPinata)
        {
            for(int i = 0; i < 3; i++)
            {
                int RandomPrizeIndex = UnityEngine.Random.Range(0, config.pinataSettings[IndexPinata].PrizeList.Count);
                if (!PrizeListUserTake.ContainsKey(player.userID))
                    PrizeListUserTake.Add(player.userID, new List<int> { RandomPrizeIndex });
                else PrizeListUserTake[player.userID].Add(RandomPrizeIndex);
            }
        }
        void ShowMyPrize(BasePlayer player, string IndexPinata)
        {
            RunEffect(player, config.moreSettingsPinata.PinataCrush);
            PrizeRandoms(player, IndexPinata);           
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, MAIN_PARENT);
            CuiHelper.DestroyUi(player, MAIN_PINATA_PRIZE);

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                FadeOut = 0.5f,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { FadeIn = 0.5f, Color = "0 0 0 0.75", Material = "assets/content/ui/uibackgroundblur.mat", Sprite = "assets/content/ui/ui.background.transparent.radial.psd" }
            },  "Overlay", MAIN_PINATA_PRIZE);

            #region StaticLabels

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.9074074", AnchorMax = "1 0.9611111" },
                Text = { Text = string.Format(lang.GetMessage("UI_PRIZE_PINATA_TITLE", this, player.UserIDString)), Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperCenter, Color = HexToRustFormat("#FFFFFF8B") }
            }, MAIN_PINATA_PRIZE);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.8685197", AnchorMax = "1 0.9222234" },
                Text = { Text = string.Format(lang.GetMessage("UI_PRIZE_PINATA_DESCRIPTION", this, player.UserIDString)), Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = HexToRustFormat("#FFFFFF8B") }
            }, MAIN_PINATA_PRIZE);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.3921875 0.7064815", AnchorMax = "0.5994791 0.7361111" },
                Text = { Text = string.Format(lang.GetMessage("UI_PRIZE_PINATA_TEXT", this, player.UserIDString)), Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperCenter, Color = HexToRustFormat("#FFFFFF8B") }
            }, MAIN_PINATA_PRIZE);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.296875 0.1527777", AnchorMax = "0.6864583 0.3444442" },
                Text = { Text = string.Format(lang.GetMessage("UI_PRIZE_PINATA_TWO_TEXT", this, player.UserIDString)), Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = HexToRustFormat("#FFFFFF8B") }
            }, MAIN_PINATA_PRIZE);

            #endregion

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0 0.4564815", AnchorMax = $"1 0.7009268" },
                Image = { Color = "0 0 0 0", Sprite = "assets/content/ui/ui.background.transparent.linear.psd" }
            }, MAIN_PINATA_PRIZE,"PRIZE_PANEL_ELEMENT");

            for (int i = 0,y = 0; i < PrizeListUserTake[player.userID].Count; i++,y++)
            {
                int RandomIndexList = PrizeListUserTake[player.userID][i];
                var InventoryItem = config.pinataSettings[IndexPinata].PrizeList[RandomIndexList];
                int Amount = UnityEngine.Random.Range(InventoryItem.MinAmount, InventoryItem.MaxAmount);
                string MoreText = InventoryItem.Command_Item ? "дней" : "шт";

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{0.2276069 + (i * 0.2)} 0.08333292", AnchorMax = $"{0.359899 + (i * 0.2)} 0.9962082" },
                    Image = { Color = "0 0 0 0.3", Sprite = "assets/content/ui/ui.background.transparent.linear.psd" }
                }, "PRIZE_PANEL_ELEMENT", $"PRIZE_PANEL{i}");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "0 -40", OffsetMax = "185 0" },
                    Text = { Text = $"{InventoryItem.DisplayName}\n{Amount}{MoreText}", Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
                },  $"PRIZE_PANEL{i}");

                container.Add(new CuiElement
                {
                    Parent = $"PRIZE_PANEL{i}",
                    Components =
                        {
                        new CuiRawImageComponent { Png = GetImage(InventoryItem.Shortname), Color = HexToRustFormat("#FFFFFFFF") },
                        new CuiRectTransformComponent{  AnchorMin = $"0 0", AnchorMax = $"1 1" },
                        }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = $"pinatapony take_my_prize {IndexPinata} {RandomIndexList} {Amount}", Color = "0 0 0 0.1" },
                    Text = { Text = "", Color = HexToRustFormat("#FFFFFF8B"), Align = TextAnchor.MiddleCenter }
                },  $"PRIZE_PANEL{i}");
            
            }

            CuiHelper.AddUi(player, container);

            CorvusStatistick?.Call("WritePinataStatistick", player.userID); // Добавляет +1 к статистике по пинатам
        }


        #endregion

        #region UpdateHealthPinata

        void UpdateHealthPinata(BasePlayer player, string PinataIndex, int IndexUI)
        {
            CuiElementContainer container = new CuiElementContainer();
            if (!DataPlugins.ContainsKey(player.userID)) return;
            if (!DataPlugins[player.userID].ContainsKey(PinataIndex)) return;
            CuiHelper.DestroyUi(player, $"PINATA_UI_HIT_{IndexUI}");
            var DataPlayer = DataPlugins[player.userID][PinataIndex];//ElementAt(IndexUI); // FIX

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.05602239" },
                Text = { Text = string.Format(lang.GetMessage("UI_HEALTH", this, player.UserIDString),config.pinataSettings[PinataIndex].ClickCount - DataPlayer.PinataClick ), Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = HexToRustFormat("#FFFFFF") } //config.pinataSettings.FirstOrDefault(x => x.Key == DataPlayer.Key).Value.ClickCount - DataPlayer.Value.PinataClick
            }, $"PINATA_{IndexUI}", $"PINATA_UI_HIT_{IndexUI}");

            CuiHelper.AddUi(player, container);

        }

        #endregion

        #region MoreDetalisPinataItem

        void MoreDetalisPinataItem(BasePlayer player, string PinataIndex, int IndexItem, bool Status = false)
        {
            CuiElementContainer container = new CuiElementContainer();
            var InventoryItem = config.pinataSettings[PinataIndex].PrizeList[IndexItem];
            if (Status)
            {
                CuiHelper.DestroyUi(player, $"ITEM_MORES_INFO_{IndexItem}");
                return;
            }
            container.Add(new CuiPanel
            {
                FadeOut = 0.5f,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = {FadeIn = 0.5f, Color = "0 0 0 0.9", Sprite = "assets/content/ui/ui.background.transparent.linear.psd" }
            },  $"BUTTON_ITEM_{IndexItem}", $"ITEM_MORES_INFO_{IndexItem}");

            string MoreText = InventoryItem.Command_Item ? "дней" : "шт";
            string CountText = InventoryItem.MinAmount == InventoryItem.MaxAmount ? InventoryItem.MinAmount.ToString() : $"<size=12>{InventoryItem.MinAmount}<b>-</b>{ InventoryItem.MaxAmount}</size>";
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Command = $"pinatapony more_detalis_item {PinataIndex} {IndexItem} {true}", Color = "0 0 0 0" },
                Text = { Text = $"{InventoryItem.DisplayName}\n{CountText}{MoreText}", Color = HexToRustFormat("#FFFFFFFF"), Align = TextAnchor.LowerCenter }
            },  $"ITEM_MORES_INFO_{IndexItem}");

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region InventoryPrizesPlayersUI

        void InventoryPrizePlayers(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, MAIN_PARENT);
            CuiHelper.DestroyUi(player, MAIN_PINATA_PRIZE_INVENTORY);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { FadeIn = 1f, Color = "0 0 0 0.75", Material = "assets/content/ui/uibackgroundblur.mat", Sprite = "assets/content/ui/ui.background.transparent.radial.psd" }
            }, "Overlay", MAIN_PINATA_PRIZE_INVENTORY);

            #region Labels

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.9287032", AnchorMax = "1 1" },
                Text = { Text = string.Format(lang.GetMessage("UI_INVENTORY_PRIZE_TITLE", this, player.UserIDString)), Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = HexToRustFormat("#FFFFFF8B") }
            }, MAIN_PINATA_PRIZE_INVENTORY);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.1333333" },
                Text = { Text = string.Format(lang.GetMessage("UI_INVENTORY_PRIZE_WARNING", this, player.UserIDString)), Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperCenter, Color = HexToRustFormat("#FFFFFF8B") }
            }, MAIN_PINATA_PRIZE_INVENTORY);

            #endregion

            #region LoadSlots

            container.Add(new CuiPanel
            {
                FadeOut = 0.5f,
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-600 -300", OffsetMax = "600 300" },
                Image = { FadeIn = 0.5f, Color = "0 0 0 0" }
            }, MAIN_PINATA_PRIZE_INVENTORY, "SLOTS_PANEL");

            for (int i = 0, x = 0, y = 0; i < 36; i++)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{0 + (x * 0.112)} {0.7805594 - (y * 0.23)}", AnchorMax = $"{0.105 + (x * 0.112)} {0.995 - (y * 0.23)}" },
                    Image = { Color = "0 0 0 0.3", Sprite = "assets/content/ui/ui.background.transparent.radial.psd", Material = "assets/content/ui/uibackgroundblur.mat" }
                }, "SLOTS_PANEL", $"SLOT_{i}");

                x++;
                if (x >= 9)
                {
                    x = 0;
                    y++;
                }
                if (x >= 9 && y >= 4) break;
            }

            for(int i = 0; i < InventoryUser[player.userID].Count; i++)
            {
                var InventoryItem = InventoryUser[player.userID][i];
                string MoreText = InventoryItem.Command_Item ? "дней" : "шт";

                container.Add(new CuiElement
                {
                    Parent = $"SLOT_{i}",
                    Name = $"ITEM_{i}",
                    Components =
                        {
                        new CuiRawImageComponent { Png = GetImage(InventoryItem.Shortname), Color = HexToRustFormat("#FFFFFFFF") },
                        new CuiRectTransformComponent{  AnchorMin = $"0 0", AnchorMax = $"1 1" },
                        }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = $"pinatapony take_item_in_inventory {i}", Color = "0 0 0 0" },
                    Text = { Text = $"{InventoryItem.DisplayName}\n{InventoryItem.Amount}{MoreText}", Color = HexToRustFormat("#FFFFFFFF"), Align = TextAnchor.LowerCenter }
                }, $"ITEM_{i}");
            }

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.91 0.95", AnchorMax = "1 0.999" },
                Button = { Close = MAIN_PINATA_PRIZE_INVENTORY, Color = "0 0 0 0.1" },
                Text = { Text = lang.GetMessage("UI_CLOSE_BUTTON", this, player.UserIDString), Align = TextAnchor.UpperLeft }
            },  MAIN_PINATA_PRIZE_INVENTORY);
            #endregion

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #endregion

        #region Metods

        #region ClickMetods
        public Dictionary<ulong, double> AntiSpamDictonary = new Dictionary<ulong, double>();
        static DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        static double CurrentTime() => DateTime.UtcNow.Subtract(epoch).TotalSeconds;

        bool AntiSpam(BasePlayer player)
        {
            if (!AntiSpamDictonary.ContainsKey(player.userID) || AntiSpamDictonary[player.userID] <= CurrentTime()) return true;
            else return false;
        }

        void ClickMetods(BasePlayer player, string IndexPinata,int IndexUI)
        {
            var Data = DataPlugins[player.userID][IndexPinata];
            if (AntiSpam(player))
            {
                Data.PinataClick += ClickCount(player);
                RunEffect(player, config.moreSettingsPinata.ClickSound);
                AntiSpamDictonary[player.userID] = CurrentTime() + 0.3;
                var PinataHealth = config.pinataSettings[IndexPinata].ClickCount - Data.PinataClick; //config.pinataSettings.FirstOrDefault(x => x.Key == IndexPinata).Value.ClickCount - Data.PinataClick; /// FIX
                if (PinataHealth <= 0)
                {
                    if (Data.PinataAmount > 1)
                    {
                        Data.PinataAmount--;
                        Data.PinataClick = 0;
                        ShowMyPrize(player, IndexPinata);
                        AntiSpamDictonary[player.userID] = CurrentTime() + 1;
                        return;
                    }
                    else
                    {
                        DataPlugins[player.userID].Remove(IndexPinata);
                        ShowMyPrize(player, IndexPinata);
                        AntiSpamDictonary[player.userID] = CurrentTime() + 1;
                    }
                }
                UpdateHealthPinata(player, IndexPinata, IndexUI);
            }
        }

        #endregion

        #region TakePrizeMetods

        void MoveToInventoryPrize(BasePlayer player,string IndexPinata, int PrizeIndex, int Amount)
        {
            RunEffect(player, config.moreSettingsPinata.PrizeTake);
            var Prize = config.pinataSettings[IndexPinata].PrizeList[PrizeIndex];
            var Inventory = InventoryUser[player.userID];
            InventoryClass inventoryClass = new InventoryClass();
            inventoryClass.Command_Item = Prize.Command_Item;
            inventoryClass.DisplayName = Prize.DisplayName;
            inventoryClass.Command = Prize.Command;
            inventoryClass.Shortname = Prize.Shortname;
            inventoryClass.Amount = Amount; 
            Inventory.Add(inventoryClass);
            InterfacePinata(player);
            PrizeListUserTake.Remove(player.userID);
        }

        #endregion

        #region TakePrizeInventoryMetods
        void TakeInInventoryPrize(BasePlayer player, int Slot)
        {
            var InventoryItem = InventoryUser[player.userID][Slot];
            string MoreText = InventoryItem.Command_Item ? "дней" : "шт";
            if (!InventoryItem.Command_Item)
            {
                var info = ItemManager.FindItemDefinition(InventoryItem.Shortname);
                if (info == null) return;
                var item = ItemManager.Create(info, InventoryItem.Amount);
                if (player.inventory.GiveItem(item))
                {
                    InventoryUser[player.userID].Remove(InventoryItem);
                    ReplyWithHelper(player, string.Format(lang.GetMessage("MSG_INVENTORY_TAKE_ITEM", this, player.UserIDString), InventoryItem.DisplayName, InventoryItem.Amount, MoreText));
                    if(InventoryUser[player.userID].Count > 0)
                        InventoryPrizePlayers(player);
                    else CuiHelper.DestroyUi(player, MAIN_PINATA_PRIZE_INVENTORY);
                }
                else SelectNoTakeSlot(player, Slot);
            }
            else
            {
                Server.Command(InventoryItem.Command.Replace("%STEAMID%", player.UserIDString));
                InventoryUser[player.userID].Remove(InventoryItem);
                ReplyWithHelper(player,string.Format(lang.GetMessage("MSG_INVENTORY_TAKE_ITEM",this,player.UserIDString), InventoryItem.DisplayName, InventoryItem.Amount, MoreText));
                if (InventoryUser[player.userID].Count > 0)
                    InventoryPrizePlayers(player);
                else CuiHelper.DestroyUi(player, MAIN_PINATA_PRIZE_INVENTORY);
            }
        }

        //TODO : Добавить статистику плагину,кол-во открытых пиньят/кликов ПРИ НАПИСАНИИ СТАТЫ
        void SelectNoTakeSlot(BasePlayer player, int Slot)
        {
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1"},
                Button = { FadeIn = 1f, Color = HexToRustFormat("#EF5A5A91") },
                Text = { Text = "НЕДОСТАТОЧНО\nМЕСТА", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = "0.7 1 0.7 1", FontSize = 18 }
            }, $"SLOT_{Slot}");

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Boost

        void GiveBoostClick(ulong userID, int CountClick)
        {
            if (!BoostUser.ContainsKey(userID))
                BoostUser.Add(userID, CountClick);
            else BoostUser[userID] += CountClick;
        }

        bool IsBoost(BasePlayer player)
        {
            if (BoostUser.ContainsKey(player.userID))
                if (BoostUser[player.userID] >= 1) return true;
                else return false;
            else return false;
        }

        int ClickCount(BasePlayer player)
        {
            if (IsBoost(player))
            {
                BoostUser[player.userID]--;
                ShowMyBoost(player);
                return 2;
            }
            else return 1;
        }

        #endregion

        #region DropPinataMetods    
        public bool GetRandomRareCrate(string ShortNameCrate)
        {
            if (config.RareDrop.ContainsKey(ShortNameCrate))
            {
                bool goodChance = UnityEngine.Random.Range(0, 100) >= (100 - config.RareDrop[ShortNameCrate]);
                if (goodChance) return true;
                else return false;
            }
            else return false;
        }

        void DropPinataMetods(BasePlayer player, string ShortNameCrate)
        {
            if (GetRandomRareCrate(ShortNameCrate))
            {
                int RandomElement = UnityEngine.Random.Range(0, config.pinataSettings.Count);
                var Data = DataPlugins[player.userID];
                if (Data.ContainsKey(config.pinataSettings.ElementAt(RandomElement).Key))
                    Data[config.pinataSettings.ElementAt(RandomElement).Key].PinataAmount++;
                else Data.Add(config.pinataSettings.ElementAt(RandomElement).Key, new DataClass { PinataAmount = 1, PinataClick = 0 });
                ReplyWithHelper(player, "Поздравляем вас,вы нашли пинату!\nИспользуйте команду - </color=lime>/pinata</color>");
            }
        }

        #endregion

        #endregion

        #region Lang
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["UI_TITLE"] = "<b><size=35>PINATA PONY</size></b>",
                ["UI_TWO_TITLE"] = "<b><size=20>Your available piñatas are shown here!</size></b>",
                ["UI_NULL_PINATA"] = "<b><size=45>You have no pinyat!</size></b>",
                ["UI_INVENTORY_TITLE"] = "<b><size=35>Inventory</size></b>",
                ["UI_CLOSE_BUTTON"] = "<b><size=25>Close</size></b>",
                ["UI_DESCRIPTION"] = "" +
                "<b> <size = 20> This is an entertaining mini-game PinataPony - Clicker" +
                "\nYou will be able to receive valuable prizes depending on the piñata you found!" +
                "\nTo break a piñata, click on it! 1 click = 1 health" +
                "\nDon't be lazy, spend some time and get valuable prizes!" +
                "\n\nBall prizes earned by you fall into inventory!\nYou can open it by clicking on the button below!" +
                "\n Good luck! </size> </b>",
                ["UI_LEFT"] = "<b><size=14>Left : {0}</size></b>",
                ["UI_HEALTH"] = "<b><size=12>Health : {0}♥</size></b>",
                ["UI_MORE"] = "<b><size=16>More details</size></b>",

                ["UI_MORE_TITLE"] = " <b><size=25>The prizes you can get from this pinata</size></b>",
                ["UI_MORE_DESCRIPTION"] = "<b><size=14>Choose one of three prizes!</size></b>",

                ["UI_PRIZE_PINATA_TITLE"] = "<b><size=30>Congratulations! You broke a pinata and got a reward!</b></size>",
                ["UI_PRIZE_PINATA_DESCRIPTION"] = "<b><size=18>Your prize will be shown below!You can take your prize!</b></size>",
                ["UI_PRIZE_PINATA_TEXT"] = "<b><size=18>Choose a prize : </b></size>",
                ["UI_PRIZE_PINATA_TWO_TEXT"] = "<b><size=20>After choosing one of the three prizes, he will fall into your inventory!\nOpen the inventory you can in the main window with pirates, from the bottom center of the screen!\nWe wish good luck and a pleasant game!</size></b>",

                ["UI_INVENTORY_PRIZE_WARNING"] = "<b><size=25><color=#FF7900FF>Warning!</color></size>\n <size=18>This inventory is cleared every Global - WIPE!\nTake prizes on time!\nLuck!</size></b>",
                ["UI_INVENTORY_PRIZE_TITLE"] = "<b><size=30>Your inventory with prizes</size></b>",

                ["MSG_INVENTORY_TAKE_ITEM"] = "You have successfully taken the item - <color=#5AEBEF>{0}</color>",

                ["UI_BOOST_TITLE"] = "<b><size=18>Your boosts :</size></b>",
                ["UI_BOOST_NULL"] = "<b><size=16>You have no amplifiers</size></b>",

            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["UI_TITLE"] = "<b><size=35>PINATA PONY</size></b>",
                ["UI_TWO_TITLE"] = "<b><size=20>Здесь показаны ваши доступные пиньяты!Развлекайтесь!</size></b>",
                ["UI_NULL_PINATA"] = "<b><size=45>У вас нет пиньят!</size></b>",
                ["UI_INVENTORY_TITLE"] = "<b><size=35>Инвентарь</size></b>",
                ["UI_CLOSE_BUTTON"] = "<b><size=25>Закрыть</size></b>",
                ["UI_DESCRIPTION"] = "" +
                "<b><size=20>Это развлекательная мини-игра PinataPony - Кликер " +
                "\nТы сможешь получать ценные призы в зависимости от найденной тобой пиньяты!" +
                "\nЧтобы разбить пиньяту,кликайте на нее! 1 клик = 1 здоровью" +
                "\nНе ленись,потрать немного времени и получи ценные призы!" +
                "\n\nBсе призы заработанные тобой,попадают в инвентарь!\nOткрыть его ты сможешь,нажав на кнопку ниже!" +
                "\nУдачи!</size></b>",
                ["UI_LEFT"] = "<b><size=14>Oсталось : {0}</size></b>",
                ["UI_HEALTH"] = "<b><size=12>Здоровье : {0}♥</size></b>",
                ["UI_MORE"] = "<b><size=16>Подробнее</size></b>",

                ["UI_MORE_TITLE"] = " <b><size=25>Призы, которые вы сможете получить с этой пиньяты</size></b>",
                ["UI_MORE_DESCRIPTION"] = "<b><size=14>Чтобы узнать возможное количество,нажмите на предмет в списке!</size></b>",

                ["UI_PRIZE_PINATA_TITLE"] = "<b><size=30>Поздравляем! Вы разбили пиньяту и получили награду!</size></b>",
                ["UI_PRIZE_PINATA_DESCRIPTION"] = "<b><size=18>Bыберите один из трех призов!</size></b>",
                ["UI_PRIZE_PINATA_TEXT"] = "<b><size=18>Выбирайте приз : </size></b>",
                ["UI_PRIZE_PINATA_TWO_TEXT"] = "<b><size=20>После выбора одного из трех призов,он попадет в ваш инвентарь!\nОткрыть инвентарь вы сможете в главном окне с пиньятами,снизу по центру экрана!\n Желаем удачи и приятной игры!</size></b>",

                ["UI_INVENTORY_PRIZE_WARNING"] = "<b><size=25><color=#FF7900FF>Внимание!</color></size>\n<size=18>Данный инвентарь очищается каждый Глобальный - WIPE!\nЗабирайте призы вовремя!\nУдачи!</size></b>",
                ["UI_INVENTORY_PRIZE_TITLE"] = "<b><size=30>Baш инвентарь с призами</size></b>",

                ["MSG_INVENTORY_TAKE_ITEM"] = "Вы успешно забрали предмет - <color=#5AEBEF>{0}</color>",

                ["UI_BOOST_TITLE"] = "<b><size=18>Ваши усилители :</size></b>",
                ["UI_BOOST_NULL"] = "<b><size=16>У вас нет усилителей</size></b>",
            }, this, "ru");
            PrintWarning("Языковой файл загружен успешно");
        }
        #endregion

        #region Utilites
        void RunEffect(BasePlayer player, string path)
        {
            Effect effect = new Effect();
            effect.Init(Effect.Type.Generic, player.transform.position, player.transform.forward, (Network.Connection)null);
            effect.pooledString = path; EffectNetwork.Send(effect, player.net.connection);
        }

        public void ReplyWithHelper(BasePlayer player, string message, string[] args = null)
        {
            if (args != null)
                message = string.Format(message, args);
            player.SendConsoleCommand("chat.add 0", new object[2]
            {
                76561198865777479,
                string.Format("<size=16><color={2}>{0}</color>:</size>\n{1}", "PinataPony", message, "#3B85F5B1")
            });
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
                throw new InvalidOperationException("Cannot convert a wrong format.");
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

            UnityEngine.Color color = new Color32(r, g, b, a);

            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }

        #endregion

        #region API

        void GivePinata(ulong ID, string IndexPinata,int Amount = 1)
        {
            BasePlayer player = BasePlayer.FindByID(ID);
            RunEffect(player, config.moreSettingsPinata.PrizeTake);

            //DataPlugins[ID][IndexPinata] = new DataClass { PinataAmount =+ Amount };

            var Data = DataPlugins[ID];
            if (Data.ContainsKey(IndexPinata))
                Data[IndexPinata].PinataAmount += Amount;
            else Data.Add(IndexPinata, new DataClass { PinataAmount = 1, PinataClick = 0 });


            ReplyWithHelper(player, $"Вы успешно получили {config.pinataSettings[IndexPinata].DisplayName},поздравляем вас!");
        }

        #endregion
    }
}
