using System;
using System.Collections.Generic;
using ConVar;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using UnityEngine;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("IQHeadReward", "https://topplugin.ru/", "0.0.2")]
    [Description("https://topplugin.ru/")]
    class IQHeadReward : RustPlugin
    {
        #region Vars
        public Dictionary<BasePlayer, List<ItemListReward>> RandomHeadPlayers = new Dictionary<BasePlayer, List<ItemListReward>>();
        #endregion

        #region Reference
        [PluginReference] Plugin ImageLibrary, IQChat, IQEconomic;
        public string GetImage(string shortname, ulong skin = 0) => (string)ImageLibrary?.Call("GetImage", shortname, skin);
        public bool AddImage(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname, skin);
        public void SendImage(BasePlayer player, string imageName, ulong imageId = 0) => ImageLibrary?.Call("SendImage", player, imageName, imageId);
        public bool HasImage(string imageName) => (bool)ImageLibrary?.Call("HasImage", imageName);
        public string IQEcoMoneu => (string)IQEconomic?.Call("API_GET_MONEY_IL");
        public void SetBalance(ulong userID, int Balance) => IQEconomic?.Call("API_SET_BALANCE", userID, Balance);

        void LoadedImage()
        {
            foreach (var Item in config.Setting.itemLists)
                if (!HasImage($"{Item.Shortname}_head"))
                    AddImage($"http://rust.skyplugins.ru/getimage/{Item.Shortname}/64", $"{Item.Shortname}_head");

            if (!HasImage($"WANTED"))
                AddImage("https://i.imgur.com/5vfDpgD.png", "WANTED");

            if (config.Setting.AlertUISetting.UseAlertUI)
                if (!HasImage($"ALERT_UI"))
                    AddImage(config.Setting.AlertUISetting.PNG, "ALERT_UI");

        }
        void CahedImages(BasePlayer player)
        {
            foreach (var Item in config.Setting.itemLists)
                SendImage(player, $"{Item.Shortname}_head");

            SendImage(player, $"WANTED");
            SendImage(player, $"ALERT_UI");
        }

        public void SendChat(BasePlayer player, string Message, Chat.ChatChannel channel = Chat.ChatChannel.Global)
        {
            var Chat = config.Setting.ChatSetting;
            if (IQChat)
                IQChat?.Call("API_ALERT_PLAYER", player, Message, Chat.CustomPrefix, Chat.CustomAvatar);
            else player.SendConsoleCommand("chat.add", channel, 0, Message);
        }
        #endregion

        #region Data
        [JsonProperty("Награды юзеров")]
        public Dictionary<ulong, List<ItemListReward>> DataInformation = new Dictionary<ulong, List<ItemListReward>>();
        public class ItemListReward
        {
            [JsonProperty("Отображаемое имя")]
            public string DisplayName;
            [JsonProperty("Shortname")]
            public string Shortname;
            [JsonProperty("SkinID")]
            public ulong SkinID;
            [JsonProperty("Количество")]
            public int Amount;
            [JsonProperty("IQEconomic : Баланс")]
            public int Balance;
        }
        void ReadData()
        {
            DataInformation = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<ItemListReward>>>("IQHeadReward/RewardsUser");
        }
        void WriteData() => timer.Every(300f, () => {
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQHeadReward/RewardsUser", DataInformation);
        });
        #endregion

        #region Configuration

        private static Configuration config = new Configuration();
        private class Configuration
        {
            [JsonProperty("Настройка плагина")]
            public Settings Setting = new Settings();

            internal class Settings
            {
                [JsonProperty("Настройки IQChat")]
                public ChatSettings ChatSetting = new ChatSettings();
                [JsonProperty("Настройки IQEconomic")]
                public IQEconomics IQEconomic = new IQEconomics();
                [JsonProperty("Настройки UI уведомления")]
                public AlertUI AlertUISetting = new AlertUI();

                [JsonProperty("Оповещать всех игроков о том,что появилась новая награда за голову(ture - да/false - нет)")]
                public bool UseAlertHead;
                [JsonProperty("Максимальное количество наград за голову(Исходя из списка наград,будет выбираться рандомное количество)")]
                public int MaxReward;
                [JsonProperty("Настройка наград за голову(Рандомно будет выбираться N количество)")]
                public List<ItemList> itemLists;
                public class ItemList
                {
                    [JsonProperty("Отображаемое имя")]
                    public string DisplayName;
                    [JsonProperty("Shortname")]
                    public string Shortname;
                    [JsonProperty("SkinID")]
                    public ulong SkinID;
                    [JsonProperty("Минимальное количество")]
                    public int AmountMin;
                    [JsonProperty("Максимальное количество")]
                    public int AmountMax;
                }
                internal class ChatSettings
                {
                    [JsonProperty("IQChat : Кастомный префикс в чате")]
                    public string CustomPrefix;
                    [JsonProperty("IQChat : Кастомный аватар в чате(Если требуется)")]
                    public string CustomAvatar;
                }
                internal class IQEconomics
                {
                    [JsonProperty("IQEconomic : Использовать IQEconomic(true - да/false - нет)")]
                    public bool UseEconomic;
                    [JsonProperty("IQEconomic : Минимальный баланс")]
                    public int MinBalance;
                    [JsonProperty("IQEconomic : Максимальный баланс")]
                    public int MaxBalance;
                    [JsonProperty("IQEconomic : Шанс получить монеты в качестве награды")]
                    public int Rare;
                }
                internal class AlertUI
                {
                    [JsonProperty("Использовать UI уведомление о том,что за голову игрока назначена награда")]
                    public bool UseAlertUI;
                    [JsonProperty("Ссылка на PNG")]
                    public string PNG;
                    [JsonProperty("AnchorMin")]
                    public string AnchorMin;
                    [JsonProperty("AnchorMax")]
                    public string AnchorMax;
                    [JsonProperty("OffsetMin")]
                    public string OffsetMin;
                    [JsonProperty("OffsetMax")]
                    public string OffsetMax;
                }           
            }

            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    Setting = new Settings
                    {
                        UseAlertHead = true,
                        MaxReward = 3,
                        itemLists = new List<Settings.ItemList> 
                        {
                            new Settings.ItemList
                            {
                                DisplayName = "Kalash",
                                Shortname = "rifle.ak",
                                AmountMin = 1,
                                AmountMax = 1,
                                SkinID = 0,
                            },
                            new Settings.ItemList
                            {
                                DisplayName = "",
                                Shortname = "rifle.ak",
                                AmountMin = 1,
                                AmountMax = 1,
                                SkinID = 0,
                            },
                            new Settings.ItemList
                            {
                                DisplayName = "",
                                Shortname = "wood",
                                AmountMin = 3000,
                                AmountMax = 6000,
                                SkinID = 0,
                            },
                            new Settings.ItemList
                            {
                                DisplayName = "",
                                Shortname = "metal.fragments",
                                AmountMin = 100,
                                AmountMax = 2000,
                                SkinID = 0,
                            },
                            new Settings.ItemList
                            {
                                DisplayName = "",
                                Shortname = "skull.human",
                                AmountMin = 1,
                                AmountMax = 10,
                                SkinID = 0,
                            },
                            new Settings.ItemList
                            {
                                DisplayName = "",
                                Shortname = "scrap",
                                AmountMin = 111,
                                AmountMax = 2222,
                                SkinID = 0,
                            },
                            new Settings.ItemList
                            {
                                DisplayName = "",
                                Shortname = "skull.wolf",
                                AmountMin = 1,
                                AmountMax = 15,
                                SkinID = 0,
                            },
                            new Settings.ItemList
                            {
                                DisplayName = "",
                                Shortname = "sulfur",
                                AmountMin = 1333,
                                AmountMax = 1532,
                                SkinID = 0,
                            },
                        },
                        ChatSetting = new Settings.ChatSettings
                        {
                            CustomAvatar = "",
                            CustomPrefix = "[IQHeadReward]\n"
                        },
                        IQEconomic = new Settings.IQEconomics
                        {
                            UseEconomic = true,
                            MinBalance = 1,
                            MaxBalance = 10,
                            Rare = 10
                        },
                        AlertUISetting = new Settings.AlertUI
                        {
                            UseAlertUI = true,
                            PNG = "https://i.imgur.com/LKOL7N8.png",
                            AnchorMin = "0.6385417 0.025",
                            AnchorMax = "0.6906251 0.1092593",
                            OffsetMin = "0 0",
                            OffsetMax = "0 0"
                        }
                    },
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
                PrintWarning("Ошибка #87" + $"чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию! #45");
                LoadDefaultConfig();
            }
            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Metods
        void FillingPlayer()
        {
            if (RandomHeadPlayers.Count < 3)
                NewHead();
        }
        void NewHead()
        {
            var RewardList = config.Setting.itemLists;
            int MaxReward = RewardList.Count <= config.Setting.MaxReward ? RewardList.Count : config.Setting.MaxReward;
            BasePlayer RandomPlayer = BasePlayer.activePlayerList[UnityEngine.Random.Range(0, BasePlayer.activePlayerList.Count)];
            if (RandomPlayer == null) return;
            if (RandomPlayer.IsDead()) return;

            if (!RandomHeadPlayers.ContainsKey(RandomPlayer))
            {
                RandomHeadPlayers.Add(RandomPlayer, new List<ItemListReward> { });
                for (int i = 0; i < MaxReward; i++)
                {
                    var item = config.Setting.itemLists[UnityEngine.Random.Range(0, config.Setting.itemLists.Count)];
                    int Balance = config.Setting.IQEconomic.UseEconomic ? UnityEngine.Random.Range(0, 100) >= (100 - config.Setting.IQEconomic.Rare) ? UnityEngine.Random.Range(config.Setting.IQEconomic.MinBalance, config.Setting.IQEconomic.MaxBalance) : 0 : 0;

                    RandomHeadPlayers[RandomPlayer].Add(new ItemListReward
                    {
                        Amount = UnityEngine.Random.Range(item.AmountMin, item.AmountMax),
                        DisplayName = item.DisplayName,
                        Shortname = item.Shortname,
                        SkinID = item.SkinID,
                        Balance = Balance,
                    });
                }

                if (config.Setting.UseAlertHead)
                    foreach (var player in BasePlayer.activePlayerList)
                        SendChat(player, String.Format(lang.GetMessage("NEW_HEAD", this, player.UserIDString), RandomPlayer.displayName));
                SendChat(RandomPlayer, String.Format(lang.GetMessage("HEAD_ALERT_HEADER", this, RandomPlayer.UserIDString)));

                if (config.Setting.AlertUISetting.UseAlertUI)
                    AlertUI(RandomPlayer);
            }
        }
        void GiveReward(BasePlayer player)
        {
            if (!DataInformation.ContainsKey(player.userID)) return;
            foreach (var Items in DataInformation[player.userID])
            {
                if (Items.Balance > 0)
                    SetBalance(player.userID, Items.Balance);

                Item Item = ItemManager.CreateByName(Items.Shortname, Items.Amount, Items.SkinID);
                if (!string.IsNullOrEmpty(Items.DisplayName))
                    Item.name = Items.DisplayName;
                player.GiveItem(Item);
            }
            DataInformation.Remove(player.userID);
        }
        void KilledHeader(BasePlayer player, BasePlayer Header)
        {
            if (!RandomHeadPlayers.ContainsKey(Header)) return;

            if (!DataInformation.ContainsKey(player.userID))
                DataInformation.Add(player.userID, RandomHeadPlayers[Header]);
            else
                foreach (var Item in RandomHeadPlayers[Header])
                {
                    DataInformation[player.userID].Add(new ItemListReward
                    {
                        Shortname = Item.Shortname,
                        DisplayName = Item.DisplayName,
                        Amount = Item.Amount,
                        SkinID = Item.SkinID,
                        Balance = Item.Balance
                    });
                }
            SendChat(player, String.Format(lang.GetMessage("HEAD_KILLED", this, player.UserIDString)));
            if (config.Setting.UseAlertHead)
                foreach (var p in BasePlayer.activePlayerList)
                    SendChat(p, String.Format(lang.GetMessage("HEAD_KILLED_ALERT", this, player.UserIDString), Header.displayName));

            RandomHeadPlayers.Remove(Header);
            CuiHelper.DestroyUi(Header, ALERT_UI);
        }
        #endregion

        #region Hooks
        private void OnServerInitialized()
        {
            ReadData();
            LoadedImage();
            timer.Every(600f, () => { FillingPlayer(); });
            foreach (var p in BasePlayer.activePlayerList)
                OnPlayerConnected(p);
            WriteData();

            if (config.Setting.IQEconomic.UseEconomic)
                if (!IQEconomic)
                    PrintError("У вас включена опция IQEconomic, но не установлен плагин");
        }
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (RandomHeadPlayers.ContainsKey(player))
                RandomHeadPlayers.Remove(player);
        }
        void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerConnected(player));
                return;
            }
            CahedImages(player);
        }
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (info?.InitiatorPlayer == null || info == null || entity == null) return;
            var Player = info?.InitiatorPlayer;
            var Header = entity.ToPlayer();
            if (Header == null) return;
            if (Player.userID == Header.userID) return;
            KilledHeader(Player, Header);
        }
        void Unload()
        {
            foreach(var p in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(p, ALERT_UI);
                CuiHelper.DestroyUi(p, HEAD_PARENT);
            }
        }
        #endregion

        #region Command
        [ChatCommand("ih")]
        void ChatCommandHeads(BasePlayer player)
        {
            UI_Interface(player);
        }

        [ConsoleCommand("ih.takereward")]
        void ConsoleCommandIH(ConsoleSystem.Arg arg)
        {
            GiveReward(arg.Player());
        }

        #endregion

        #region UI
        public static string HEAD_PARENT = "HEAD_PARENT";
        public static string ALERT_UI = "ALERT_UI";
        void UI_Interface(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, HEAD_PARENT);

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { FadeIn = 0.15f, Color = HexToRustFormat("#00000096"), Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }
            }, "Overlay", HEAD_PARENT);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.8694444", AnchorMax = "1 0.9388733" },
                Text = { Text = lang.GetMessage("UI_TITLE", this, player.UserIDString), Color = HexToRustFormat("#D4BD90D0"), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter}
            },  HEAD_PARENT);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.8416733", AnchorMax = "1 0.887037" },
                Text = { Text = lang.GetMessage("UI_DESCRIPTION", this, player.UserIDString), Color = HexToRustFormat("#D4BD90D0"), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
            }, HEAD_PARENT);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.8942708 0.9435185", AnchorMax = "1 1" },
                Button = { Close = HEAD_PARENT, Color = "0 0 0 0" },
                Text = { Text = lang.GetMessage("UI_CLOSE", this, player.UserIDString), Color = HexToRustFormat("#D4BD90D0"), Align = TextAnchor.MiddleCenter }
            }, HEAD_PARENT);

            #region HeadTask
            int ItemCount = 0;
            float itemMinPosition = 219f;
            float itemWidth = 0.413646f - 0.18f; /// Ширина
            float itemMargin = 0.439895f - 0.405f; /// Отступы
            int itemCount = RandomHeadPlayers.Count;
            float itemMinHeight = 0.2f;
            float itemHeight = 0.6f; /// Высота
            int ItemTarget = 3;

            if (itemCount > ItemTarget)
            {
                itemMinPosition = 0.5f - ItemTarget / 2f * itemWidth - (ItemTarget - 1) / 2f * itemMargin;
                itemCount -= ItemTarget;
            }
            else itemMinPosition = 0.5f - itemCount / 2f * itemWidth - (itemCount - 1) / 2f * itemMargin;

            foreach(var TaskUser in RandomHeadPlayers)
            {
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"{itemMinPosition} {itemMinHeight}", AnchorMax = $"{itemMinPosition + itemWidth} {itemMinHeight + itemHeight}" },
                    Image = { Color = "0 0 0 0" }
                },  HEAD_PARENT, $"HEAD{ItemCount}");

                container.Add(new CuiElement
                {
                    Parent = $"HEAD{ItemCount}",
                    Name = $"HEAD_AVA_{ItemCount}",
                    Components =
                    {
                    new CuiRawImageComponent { Png = GetImage(TaskUser.Key.UserIDString),Color = HexToRustFormat("#FFFFFFFF") },
                    new CuiRectTransformComponent{ AnchorMin = "0.2242798 0.3058033", AnchorMax = $"0.7798353 0.7180173"},
                    }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0.1", AnchorMax = "1 1" },
                    Text = { Text = $"<b>{TaskUser.Key.displayName}</b>", FontSize = 15, Color = HexToRustFormat("#FFFFFFFF"), Font = "robotocondensed-bold.ttf", Align = TextAnchor.LowerCenter }
                },  $"HEAD_AVA_{ItemCount}");

                container.Add(new CuiElement
                {
                    Parent = $"HEAD{ItemCount}",
                    Components =
                {
                    new CuiRawImageComponent { Png = GetImage("WANTED"),Color = HexToRustFormat("#FFFFFFFF") },
                    new CuiRectTransformComponent{ AnchorMin = "0 0", AnchorMax = $"1 1"},
                }
                });

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = $"0.07613169 0.1162123", AnchorMax = $"0.9238685 0.2137733" },
                    Image = { Color = "0 0 0 0" }
                },  $"HEAD{ItemCount}",$"ITEMPANEL_{ItemCount}");

                #region Items
                int TargetMax = TaskUser.Value.Count >= 6 ? 6 : TaskUser.Value.Count;
                int Count = 0;
                float MinPosition = 219f;
                float Width = 0.413646f - 0.25f; /// Ширина
                float Margin = 0.439895f - 0.42f; /// Отступы
                int count = TargetMax;
                float MinHeight = 0f;
                float Height = 1f; /// Высота
                int Target = 6;

                if (count > Target)
                {
                    MinPosition = 0.5f - Target / 2f * Width - (Target - 1) / 2f * Margin;
                    count -= Target;
                }
                else MinPosition = 0.5f - count / 2f * Width - (count - 1) / 2f * Margin;

                foreach (var Item in TaskUser.Value)
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{MinPosition} {MinHeight}", AnchorMax = $"{MinPosition + Width} {MinHeight + Height}" },
                        Image = { Color = HexToRustFormat("#E1940030") }
                    }, $"ITEMPANEL_{ItemCount}", $"ITEM_{Count}");

                    string Ico = Item.Balance == 0 ? $"{Item.Shortname}_head" : IQEcoMoneu;
                    container.Add(new CuiElement
                    {
                        Parent = $"ITEM_{Count}",
                        Components =
                    {
                        new CuiRawImageComponent { Png = GetImage(Ico), Color = HexToRustFormat("#FFFFFFFF") },
                        new CuiRectTransformComponent{ AnchorMin = $"0 0", AnchorMax = $"1 1" },
                    }
                    });

                    string Amount = Item.Balance == 0 ? $"x{Item.Amount}" : $"x{Item.Balance}";

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Text = { Text = Amount, FontSize = 8, Color = HexToRustFormat("#180D00FF"), Font = "robotocondensed-bold.ttf", Align = TextAnchor.LowerCenter }
                    },  $"ITEM_{Count}");

                    Count++;
                    MinPosition += (Width + Margin);
                    if (count % Target == 0)
                    {
                        MinHeight -= (Height + (Margin * 2f));
                        if (count > Target)
                        {
                            MinPosition = 0.5f - Target / 2f * Width - (Target - 1) / 2f * Margin;
                            count -= Target;
                        }
                        else MinPosition = 0.5f - count / 2f * Width - (count - 1) / 2f * Margin;
                    }
                }
                #endregion

                ItemCount++;
                itemMinPosition += (itemWidth + itemMargin);
                if (ItemCount % ItemTarget == 0)
                {
                    itemMinHeight -= (itemHeight + (itemMargin * 2f));
                    if (itemCount > ItemTarget)
                    {
                        itemMinPosition = 0.5f - ItemTarget / 2f * itemWidth - (ItemTarget - 1) / 2f * itemMargin;
                        itemCount -= ItemTarget;
                    }
                    else itemMinPosition = 0.5f - itemCount / 2f * itemWidth - (itemCount - 1) / 2f * itemMargin;
                }
            }
            #endregion

            if (DataInformation.ContainsKey(player.userID))
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.2994792 0.01666733", AnchorMax = "0.6927084 0.08703704" },
                    Button = { Close = HEAD_PARENT, Command = "ih.takereward", Color = HexToRustFormat("#D4BD91FF") },
                    Text = { Text = lang.GetMessage("UI_TAKE_REWARD", this, player.UserIDString), Color = HexToRustFormat("#180B00FF"), Align = TextAnchor.MiddleCenter }
                }, HEAD_PARENT);
            }

            CuiHelper.AddUi(player, container);
        }

        void AlertUI(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, ALERT_UI);
            var Interface = config.Setting.AlertUISetting;

            container.Add(new CuiElement
            {
                Parent = "Overlay",
                Name = ALERT_UI,
                Components =
                    {
                    new CuiRawImageComponent { Png = GetImage("ALERT_UI"),Color = HexToRustFormat("#FFFFFFFF") },
                    new CuiRectTransformComponent{ AnchorMin = Interface.AnchorMin, AnchorMax = Interface.AnchorMax, OffsetMin = Interface.OffsetMin, OffsetMax = Interface.OffsetMax},
                    }
            });

            CuiHelper.AddUi(player, container);
        }

        private static string HexToRustFormat(string hex)
        {
            Color color;
            ColorUtility.TryParseHtmlString(hex, out color);
            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }
        #endregion

        #region Lang
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NEW_HEAD"] = "Attention!\nOn player {0}, a reward has been assigned for his head!\nYou can find out about the reward by the command /ih",
                ["HEAD_KILLED"] = "You have killed the player for whom the reward has been assigned!\nCongratulations!",
                ["HEAD_KILLED_ALERT"] = "The player {0} to whom the award was assigned was killed",
                ["HEAD_ALERT_HEADER"] = "A reward has been awarded for your head!\nBe vigilant, because everyone wants to kill you!",

                ["UI_TITLE"] = "<b><size=40>Bulletin board</size></b>",
                ["UI_DESCRIPTION"] = "<b><size=20>This board displays the players on whom the award is assigned.</size></b>",
                ["UI_CLOSE"] = "<b><size=30>Close</size></b>",
                ["UI_TAKE_REWARD"] = "<b><size=30>TAKE REWARD</size></b>",
                ["UI_TAKED_REWARD"] = "You have successfully collected the award!",
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NEW_HEAD"] = "Внимание!\nНа игрока {0} была назначена награда за его голову!\nУзнать о награде можно по команде /ih",
                ["HEAD_KILLED"] = "Вы убили игрока,на которого была назначена награда!\nПоздравляем!\nВы можете забрать свою награду в меню - /ih",
                ["HEAD_KILLED_ALERT"] = "Игрока {0} на которого была назначена награда - убили",
                ["HEAD_ALERT_HEADER"] = "За вашу голову была назначена награда!\nБудьте бдительны,ведь все хотят убить вас!",

                ["UI_TITLE"] = "<b><size=40>Доска объявлений</size></b>",
                ["UI_DESCRIPTION"] = "<b><size=20>На данной доске отображены игроки,на которых назначена награда</size></b>",
                ["UI_CLOSE"] = "<b><size=30>3акрыть</size></b>",
                ["UI_TAKE_REWARD"] = "<b><size=30>ЗАБРАТЬ НАГРАДУ</size></b>",
                ["UI_TAKED_REWARD"] = "Вы успешно забрали награду!",
            }, this, "ru");
            PrintWarning("Языковой файл загружен успешно");
        }
        #endregion
    }
}
