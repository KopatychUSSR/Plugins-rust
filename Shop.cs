// #define TESTING

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Plugins.ShopExtensionMethods;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using Global = Rust.Global;
using Random = UnityEngine.Random;
using Time = UnityEngine.Time;

#if CARBON
using Carbon.Base;
using Carbon.Modules;
#endif

namespace Oxide.Plugins
{
    [Info("Shop", "Mevent", "2.4.15")]
    public class Shop : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin
            ImageLibrary = null,
            ServerPanel = null,
            ItemCostCalculator = null,
            LangAPI = null,
            Notify = null,
            UINotify = null,
            NoEscape = null,
            Duel = null,
            Duelist = null;

        private static Shop Instance;

        private (bool spStatus, int categoryID)
            _serverPanelCategory = (false, -1); // key - use serverPanel, value - category id

#if CARBON
        private ImageDatabaseModule imageDatabase;
#endif

        private const bool LangRu = false;

        private readonly Dictionary<int, ShopItem> _shopItems = new();

        private readonly Dictionary<ulong, Coroutine> _coroutines = new();

        private readonly Dictionary<ulong, float> _lastCommandTime = new();

        private readonly Dictionary<string, List<(int itemID, string shortName)>> _itemsCategories = new();

        private readonly HashSet<string> _images = new();

        private const string
            Layer = "UI.Shop",
            ModalLayer = "UI.Shop.Modal",
            EditingLayer = "UI.Shop.Editing",
            CmdMainConsole = "UI_Shop";

        private const string
            PERM_ADMIN = "shop.admin",
            PERM_FREE_BYPASS = "shop.free",
            PERM_SET_VM = "shop.setvm",
            PERM_SET_NPC = "shop.setnpc",
            PERM_BYPASS_DLC = "shop.bypass.dlc";

        private const int _itemsPerTick = 10;

        private readonly Dictionary<ulong, NPCShop> _openedShopsNPC = new();

        private readonly HashSet<ulong> _adminModeUsers = new();

        private bool _isEnabledFavorites;

        private (bool status, string message) _initializedStatus = (false, string.Empty);

        private bool _templateMigrationInProgress;

        #endregion

        #region Config

        private static Configuration _config;

        private class Configuration
        {
            #region Fields

            [JsonProperty(PropertyName =
                LangRu
                    ? "Разрешение для использования плагина (прим: shop.use)"
                    : "Permission to use plugin (ex: shop.use)")]
            public string Permission = string.Empty;

            [JsonProperty(PropertyName = LangRu ? "Команды" : "Commands",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] Commands = {"shop", "shops"};

            [JsonProperty(PropertyName =
                LangRu ? "Включить переводы денег между игроками?" : "Enable money transfers between players?")]
            public bool Transfer = true;

            [JsonProperty(PropertyName =
                LangRu ? "Разрешить переводы денег оффлайн игрокам?" : "Allow money transfers to offline players?")]
            public bool TransferToOfflinePlayers = false;

            [JsonProperty(PropertyName = LangRu ? "Включить продажу предметов?" : "Enable selling items?")]
            public bool EnableSelling = true;

            [JsonProperty(PropertyName = LangRu ? "Включить логирование в консоль?" : "Enable logging to the console?")]
            public bool LogToConsole = true;

            [JsonProperty(PropertyName = LangRu ? "Включить логирование в файл?" : "Enable logging to the file?")]
            public bool LogToFile = true;

            [JsonProperty(PropertyName =
                LangRu
                    ? "Загружать изображения при подключении к серверу?"
                    : "Load images when logging into the server?")]
            public bool LoginImages = true;

            [JsonProperty(PropertyName = LangRu ? "Работать с Notify?" : "Work with Notify?")]
            public bool UseNotify = true;

            [JsonProperty(PropertyName = LangRu ? "Включить режим оффлайн изображений" : "Enable Offline Image Mode")]
            public bool EnableOfflineImageMode = false;

            [JsonProperty(PropertyName = LangRu ? "Включить работу с LangAPI?" : "Work with LangAPI?")]
            public bool UseLangAPI = true;

            [JsonProperty(PropertyName =
                LangRu ? "Могут ли админы редактировать предметы? (флаг)" : "Can admins edit? (by flag)")]
            public bool FlagAdmin = false;

            [JsonProperty(PropertyName = LangRu ? "Поддержка NoEscape" : "Block (NoEscape)")]
            public bool BlockNoEscape = false;

            [JsonProperty(PropertyName = LangRu ? "Включить блокировку после вайпа" : "Wipe Block")]
            public bool WipeCooldown = false;

            [JsonProperty(PropertyName = LangRu ? "Длительность блокировки после вайпа" : "Wipe Cooldown")]
            public float WipeCooldownTimer = 3600;

            [JsonProperty(PropertyName = LangRu ? "Включить блокировку после респавна" : "Respawn Block")]
            public bool RespawnCooldown = true;

            [JsonProperty(PropertyName = LangRu ? "Длительность блокировки после респавна" : "Respawn Cooldown")]
            public float RespawnCooldownTimer = 60;

            [JsonProperty(PropertyName = LangRu ? "Запрещать открытие на дуэлях?" : "Blocking the opening in duels?")]
            public bool UseDuels = false;

            [JsonProperty(PropertyName =
                LangRu ? "Задержка между действиями (в секундах)" : "Cooldown between actions (in seconds)")]
            public float CooldownBetweenActions = 0.1f;

            [JsonProperty(PropertyName =
                LangRu ? "Задержка между загрузкой изображений" : "Delay between loading images")]
            public float ImagesDelay = 1f;

            [JsonProperty(PropertyName = LangRu ? "Экономика" : "Economy")]
            public EconomyEntry Economy = new()
            {
                Type = EconomyType.Plugin,
                AddHook = "Deposit",
                BalanceHook = "Balance",
                RemoveHook = "Withdraw",
                Plug = "Economics",
                ShortName = "scrap",
                DisplayName = string.Empty,
                Skin = 0,
                EconomyTitle = new EconomyTitle("Economics"),
                EconomyBalance = new EconomyTitle("${0}"),
                EconomyPrice = new EconomyTitle("${0}"),
                EconomyFooterPrice = new EconomyTitle("${0}")
            };

            [JsonProperty(PropertyName = LangRu ? "Дополнительная экономика" : "Additional Economics",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<AdditionalEconomy> AdditionalEconomics = new()
            {
                new AdditionalEconomy
                {
                    ID = 1,
                    Enabled = true,
                    Type = EconomyType.Plugin,
                    AddHook = "AddPoints",
                    BalanceHook = "CheckPoints",
                    RemoveHook = "TakePoints",
                    Plug = "ServerRewards",
                    ShortName = "scrap",
                    DisplayName = string.Empty,
                    Skin = 0,
                    EconomyTitle = new EconomyTitle("Server Rewards"),
                    EconomyBalance = new EconomyTitle("{0} RP"),
                    EconomyPrice = new EconomyTitle("{0} RP"),
                    EconomyFooterPrice = new EconomyTitle("{0} RP")
                }
            };

            [JsonProperty(
                PropertyName = LangRu
                    ? "Магазины NPC (NPC ID - категории магазина)"
                    : "NPC Shops (NPC ID - shop categories)",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, NPCShop> NPCs = new()
            {
                ["1234567"] = new NPCShop
                {
                    Permission = string.Empty,
                    Categories = new List<string>
                    {
                        "Tool",
                        "Food"
                    }
                },
                ["7654321"] = new NPCShop
                {
                    Permission = string.Empty,
                    Categories = new List<string>
                    {
                        "Weapon",
                        "Ammunition"
                    }
                },
                ["4644687478"] = new NPCShop
                {
                    Permission = "shop.usenpc",
                    Categories = new List<string>
                    {
                        "*"
                    }
                }
            };

            [JsonProperty(PropertyName = LangRu ? "Интерфейс" : "Interface")]
            public UserInterface UI = new()
            {
                DisplayType = "OverlayNonScaled",
                RoundDigits = 5,
                Color1 = IColor.Create("#161617"),
                Color2 = IColor.Create("#4B68FF"),
                Color3 = IColor.Create("#0E0E10"),
                Color5 = IColor.Create("#FF4B4B"),
                Color7 = IColor.Create("#CD3838"),
                Color8 = IColor.Create("#FFFFFF"),
                Color9 = IColor.Create("#4B68FF", 33),
                Color10 = IColor.Create("#4B68FF", 50),
                Color11 = IColor.Create("#161617", 95),
                Color12 = IColor.Create("#161617", 80),
                Color13 = IColor.Create("#0E0E10", 98)
            };

            [JsonProperty(PropertyName = LangRu ? "Заблокированные скины для продажи" : "Blocked skins for sell",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, List<ulong>> BlockedSkins = new()
            {
                ["short name"] = new List<ulong>
                {
                    52,
                    25
                },

                ["short name 2"] = new List<ulong>
                {
                    52,
                    25
                }
            };

            [JsonProperty(PropertyName = LangRu ? "Auto-Wipe настройки" : "Auto-Wipe Settings")]
            public WipeSettings Wipe = new()
            {
                Cooldown = true,
                Players = true,
                Limits = true
            };

            [JsonProperty(
                PropertyName = LangRu
                    ? "Кастомные Торговые Автоматы (Entity ID - settings)"
                    : "Custom Vending Machines (Entity ID - settings)",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<ulong, CustomVendingEntry> CustomVending =
                new()
                {
                    [123343941] = new CustomVendingEntry
                    {
                        Permission = string.Empty,
                        Categories = new List<string>
                        {
                            "Cars", "Misc"
                        }
                    }
                };

            [JsonProperty(PropertyName =
                LangRu
                    ? "Настройки контейнеров для продажи товаров"
                    : "Settings available containers for selling item")]
            public SellContainers SellContainers = new()
            {
                Enabled = true,
                Containers = new List<string>
                {
                    "main",
                    "belt"
                }
            };

            [JsonProperty(PropertyName = LangRu ? "Настройки повторной покупки" : "Buy Again Settings")]
            public BuyAgainConf BuyAgain = new()
            {
                Enabled = false,
                Permission = string.Empty,
                Image = "assets/icons/history_servers.png"
            };

            [JsonProperty(PropertyName = LangRu ? "Настройки форматирования" : "Formatting Settings")]
            public FormattingConf Formatting = new()
            {
                BuyPriceFormat = "G",
                SellPriceFormat = "G",
                ShoppingBagCostFormat = "G",
                BalanceFormat = "G"
            };

            [JsonProperty(PropertyName = LangRu ? "Скидка" : "Discount")]
            public DiscountConf Discount = new()
            {
                Enabled = true,
                Discount = new Dictionary<string, int>
                {
                    ["shop.default"] = 0,
                    ["shop.vip"] = 10
                }
            };

            [JsonProperty(PropertyName = LangRu ? "Уведомления" : "Notifications")]
            public NotificationsConf Notifications = new()
            {
                GeneralNotifications = new Dictionary<string, NotificationsConf.BaseNotification>
                {
                    [NoPermission] = NotificationsConf.BaseNotification.Create(1),
                    [ErrorSyntax] = NotificationsConf.BaseNotification.Create(1),
                    [VMNotFoundCategories] = NotificationsConf.BaseNotification.Create(1),
                    [VMNotFound] = NotificationsConf.BaseNotification.Create(1),
                    [VMExists] = NotificationsConf.BaseNotification.Create(1),
                    [VMInstalled] = NotificationsConf.BaseNotification.Create(0),
                    [NPCNotFound] = NotificationsConf.BaseNotification.Create(1),
                    [NPCInstalled] = NotificationsConf.BaseNotification.Create(0),
                    [BuyCooldownMessage] = NotificationsConf.BaseNotification.Create(1),
                    [ReceivedItems] = NotificationsConf.BaseNotification.Create(0),
                    [SellNotify] = NotificationsConf.BaseNotification.Create(0),
                    [SuccessfulTransfer] = NotificationsConf.BaseNotification.Create(0),
                    [MsgIsFavoriteItem] = NotificationsConf.BaseNotification.Create(1),
                    [MsgAddedToFavoriteItem] = NotificationsConf.BaseNotification.Create(0),
                    [MsgNoFavoriteItem] = NotificationsConf.BaseNotification.Create(1),
                    [MsgRemovedFromFavoriteItem] = NotificationsConf.BaseNotification.Create(0),
                    [BuyLimitReached] = NotificationsConf.BaseNotification.Create(1),
                    [DailyBuyLimitReached] = NotificationsConf.BaseNotification.Create(1),
                    [UIMsgShopInInitialization] = NotificationsConf.BaseNotification.Create(1),
                    [NoILError] = NotificationsConf.BaseNotification.Create(1),
                    [NoUseDuel] = NotificationsConf.BaseNotification.Create(1),
                    [UIDLCItem] = NotificationsConf.BaseNotification.Create(1)
                }
            };

            [JsonProperty(PropertyName = LangRu ? "Настройки Discord логирования" : "Discord Logging Settings")]
            public DiscordSettings DiscordConfig = new();

            public VersionNumber Version;

            #endregion

            #region Classes

            public class NotificationsConf
            {
                #region Fields

                [JsonProperty(
                    PropertyName = LangRu
                        ? "Основные уведомления (сообщение – настройки оповещения)"
                        : "General Notifications (message - settings notification)",
                    ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public Dictionary<string, BaseNotification> GeneralNotifications = new();

                #endregion

                #region Public Methods

                public void ShowNotify(BasePlayer player, string key, int type, params object[] obj)
                {
                    if (GeneralNotifications.TryGetValue(key, out var targetNotify))
                        targetNotify.Show(player, key, obj);
                    else
                        Instance?.SendNotify(player, key, type, obj);
                }

                #endregion Public Method

                #region Classes

                public class BaseNotification
                {
                    #region Fields

                    [JsonProperty(PropertyName = LangRu ? "Тип" : "Type")]
                    public int Type;

                    [JsonProperty(PropertyName = LangRu ? "Показывать уведомление?" : "Show notify?")]
                    public bool ShowNotify = true;

                    #endregion

                    #region Public Methods

                    public void Show(BasePlayer player, string key, params object[] args)
                    {
                        if (ShowNotify)
                            Instance.SendNotify(player, key, Type, args);
                        else
                            Instance.Reply(player, key, args);
                    }

                    #endregion

                    #region Constructors

                    public static BaseNotification Create(int type)
                    {
                        return new BaseNotification
                        {
                            Type = type,
                            ShowNotify = true
                        };
                    }

                    #endregion
                }

                #endregion
            }

            public class DiscountConf
            {
                [JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
                public bool Enabled;

                [JsonProperty(PropertyName = LangRu ? "Скидка (%)" : "Discount (%)",
                    ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public Dictionary<string, int> Discount = new();

                public int GetDiscount(BasePlayer player)
                {
                    if (!Enabled || Discount == null || Discount.Count == 0) return 0;

                    var result = 0;
                    foreach (var (perm, discount) in Discount)
                        if (discount > result && player.HasPermission(perm))
                            result = discount;

                    return result;
                }
            }

            public class FormattingConf
            {
                [JsonProperty(PropertyName = LangRu ? "Формат цены покупки" : "Buy Price Format")]
                public string BuyPriceFormat;

                [JsonProperty(PropertyName = LangRu ? "Формат цены продажи" : "Sell Price Format")]
                public string SellPriceFormat;

                [JsonProperty(PropertyName = LangRu ? "Формат стоимости в корзине" : "Shopping Bag Cost Format")]
                public string ShoppingBagCostFormat;

                [JsonProperty(PropertyName = LangRu ? "Формат баланса" : "Balance Format")]
                public string BalanceFormat;
            }

            public class BuyAgainConf
            {
                [JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
                public bool Enabled;

                [JsonProperty(PropertyName =
                    LangRu ? "Разрешение (пример: shopru.buyagain)" : "Permission (ex: shop.buyagain)")]
                public string Permission;

                [JsonProperty(PropertyName = LangRu ? "Изображение" : "Image")]
                public string Image;

                public bool HasAccess(BasePlayer player)
                {
                    return Enabled && (string.IsNullOrEmpty(Permission) || player.HasPermission(Permission));
                }
            }

            public class DiscordSettings
            {
                [JsonProperty(PropertyName = LangRu ? "Настройки покупки" : "Buy Settings")]
                public DiscordLogSettings Buy = new()
                {
                    Enabled = false,
                    Webhook = "",
                    Color = 3066993, // Green
                    Title = "Shop Purchase",
                    FooterIconUrl = string.Empty,
                    FooterText = "{username} bought {item} x{amount} for {price}"
                };

                [JsonProperty(PropertyName = LangRu ? "Настройки продажи" : "Sell Settings")]
                public DiscordLogSettings Sell = new()
                {
                    Enabled = false,
                    Webhook = "",
                    Color = 15158332, // Red
                    Title = "Shop Sale",
                    FooterIconUrl = string.Empty,
                    FooterText = "{username} sold {item} x{amount} for {price}"
                };

                [JsonProperty(PropertyName = LangRu ? "Настройки переводов" : "Transfer Settings")]
                public DiscordLogSettings Transfer = new()
                {
                    Enabled = false,
                    Webhook = "",
                    Color = 3447003, // Blue
                    Title = "Money Transfer",
                    FooterIconUrl = string.Empty,
                    FooterText = "Transferred {amount} from {username} to {targetname}"
                };

                public class DiscordLogSettings
                {
                    [JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
                    public bool Enabled;

                    [JsonProperty(PropertyName = LangRu ? "Discord Webhook URL" : "Discord Webhook URL")]
                    public string Webhook;

                    [JsonProperty(PropertyName = LangRu ? "Цвет (Decimal формат)" : "Color (Decimal format)")]
                    public int Color;

                    [JsonProperty(PropertyName = LangRu ? "Заголовок" : "Title")]
                    public string Title;

                    [JsonProperty(PropertyName =
                        LangRu
                            ? "URL иконки футера (для Buy/Sell используется иконка предмета автоматически)"
                            : "Footer icon URL (for Buy/Sell item icon is used automatically)")]
                    public string FooterIconUrl;

                    [JsonProperty(PropertyName =
                        LangRu
                            ? "Текст футера. Переменные Buy/Sell: {username}, {steamid}, {item}, {amount}, {price}. Transfer: {username}, {steamid}, {targetname}, {targetid}, {amount}"
                            : "Footer text. Variables Buy/Sell: {username}, {steamid}, {item}, {amount}, {price}. Transfer: {username}, {steamid}, {targetname}, {targetid}, {amount})")]
                    public string FooterText;
                }
            }

            public enum SortType
            {
                None,
                Name,
                Amount,
                PriceDecrease,
                PriceIncrease
            }

            #endregion
        }

        #region Classes

        public class Localization
        {
            #region Fields

            [JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
            public bool Enabled;

            [JsonProperty(PropertyName = LangRu ? "Текст (язык - текст)" : "Text (language - text)",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, string> Messages = new();

            #endregion

            #region Helpers

            public string GetMessage(BasePlayer player = null)
            {
                if (Messages.Count == 0)
                    throw new Exception("The use of localization is enabled, but there are no messages!");

                var userLang = "en";
                if (player != null) userLang = Instance.lang.GetLanguage(player.UserIDString);

                if (Messages.TryGetValue(userLang, out var message))
                    return message;

                return Messages.TryGetValue("en", out message) ? message : Messages.ElementAt(0).Value;
            }

            #endregion
        }

        public class SellContainers
        {
            #region Fields

            [JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
            public bool Enabled;

            [JsonProperty(
                PropertyName = LangRu
                    ? "Доступные Контейнеры (main, belt, wear)"
                    : "Available Containers (main, belt, wear)",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Containers = new();

            #endregion

            #region Helpers

            public List<ItemContainer> GetContainers(BasePlayer player)
            {
                if (player == null || player.inventory == null)
                    return Pool.Get<List<ItemContainer>>();

                var list = Pool.Get<List<ItemContainer>>();

                foreach (var cont in Containers)
                    switch (cont)
                    {
                        case "main":
                        {
                            list.Add(player.inventory.containerMain);
                            break;
                        }
                        case "belt":
                        {
                            list.Add(player.inventory.containerBelt);
                            break;
                        }
                        case "wear":
                        {
                            list.Add(player.inventory.containerWear);
                            break;
                        }
                    }

                return list;
            }

            public Item[] AllItems(BasePlayer player)
            {
                var containers = GetContainers(player);
                try
                {
                    var items = new List<Item>();
                    foreach (var itemContainer in containers) items.AddRange(itemContainer.itemList);
                    return items.ToArray();
                }
                finally
                {
                    Pool.FreeUnmanaged(ref containers);
                }
            }

            public int AllItems(BasePlayer player, List<Item> items)
            {
                items.Clear();

                var containers = GetContainers(player);
                try
                {
                    foreach (var itemContainer in containers) items.AddRange(itemContainer.itemList);
                }
                finally
                {
                    Pool.FreeUnmanaged(ref containers);
                }

                return items.Count;
            }

            #endregion
        }

        public class SelectCurrencyUI
        {
            #region Fields

            [JsonProperty(PropertyName = "Use Fullscreen Background (relative to Overlay)?")]
            public bool UseFullscreenBackground;

            [JsonProperty(PropertyName = "Background")]
            public UiElement Background;

            [JsonProperty(PropertyName = "Use Vertical Layout?")]
            public bool UseVerticalLayout;

            [JsonProperty(PropertyName = "Title")] public UiElement Title;

            [JsonProperty(PropertyName = "Economy Title")]
            public UiElement EconomyTitle;

            [JsonProperty(PropertyName = "Economy Panel Material")]
            public string EconomyPanelMaterial = string.Empty;

            [JsonProperty(PropertyName = "Economy Panel Sprite")]
            public string EconomyPanelSprite = string.Empty;

            [JsonProperty(PropertyName = "Selected Economy Color")]
            public IColor SelectedEconomyColor = IColor.CreateWhite();

            [JsonProperty(PropertyName = "Unselected Economy Color")]
            public IColor UnselectedEconomyColor = IColor.CreateWhite();

            [JsonProperty(PropertyName = "Economy Width")]
            public float EconomyWidth;

            [JsonProperty(PropertyName = "Economy Height")]
            public float EconomyHeight;

            [JsonProperty(PropertyName = "Economy Margin")]
            public float EconomyMargin;

            [JsonProperty(PropertyName = "Economy Indent")]
            public float EconomyIndent;

            [JsonProperty(PropertyName = "Frame Width")]
            public float FrameWidth;

            [JsonProperty(PropertyName = "Frame Indent")]
            public float FrameIndent;

            [JsonProperty(PropertyName = "Frame Header")]
            public float FrameHeader;

            [JsonProperty(PropertyName = "Close the menu after a currency change?")]
            public bool CloseAfterChange;

            #endregion
        }

        #region UI.Classes

        public class InterfacePosition
        {
            #region Fields

            [JsonProperty(PropertyName = "AnchorMin (X)")]
            public float AnchorMinX;

            [JsonProperty(PropertyName = "AnchorMin (Y)")]
            public float AnchorMinY;

            [JsonProperty(PropertyName = "AnchorMax (X)")]
            public float AnchorMaxX = 1;

            [JsonProperty(PropertyName = "AnchorMax (Y)")]
            public float AnchorMaxY = 1;

            [JsonProperty(PropertyName = "OffsetMin (X)")]
            public float OffsetMinX;

            [JsonProperty(PropertyName = "OffsetMin (Y)")]
            public float OffsetMinY;

            [JsonProperty(PropertyName = "OffsetMax (X)")]
            public float OffsetMaxX;

            [JsonProperty(PropertyName = "OffsetMax (Y)")]
            public float OffsetMaxY;

            #endregion Fields

            #region Public Methods

            public float GetAxis(bool isX)
            {
                if (isX) return OffsetMinX;

                return -OffsetMaxY;
            }

            public void SetVerticalAxis(VerticalConstraint constraint)
            {
                switch (constraint)
                {
                    case VerticalConstraint.Center:
                        AnchorMinY = AnchorMaxY = 0.5f;
                        break;
                    case VerticalConstraint.Bottom:
                        AnchorMinY = AnchorMaxY = 0f;
                        break;
                    case VerticalConstraint.Top:
                        AnchorMinY = AnchorMaxY = 1f;
                        break;
                    case VerticalConstraint.Scale:
                        AnchorMinY = 0f;
                        AnchorMaxY = 1f;
                        break;
                }
            }

            public VerticalConstraint GetVerticalAxis()
            {
                if (Mathf.Approximately(AnchorMinY, AnchorMaxY))
                {
                    if (Mathf.Approximately(AnchorMinY, 0.5f))
                        return VerticalConstraint.Center;
                    if (Mathf.Approximately(AnchorMinY, 0f))
                        return VerticalConstraint.Bottom;
                    if (Mathf.Approximately(AnchorMinY, 1f))
                        return VerticalConstraint.Top;
                }

                if (Mathf.Approximately(AnchorMinY, 0) && Mathf.Approximately(AnchorMaxY, 1))
                    return VerticalConstraint.Scale;

                return VerticalConstraint.Custom;
            }

            public void SetHorizontalAxis(HorizontalConstraint constraint)
            {
                switch (constraint)
                {
                    case HorizontalConstraint.Center:
                        AnchorMinX = AnchorMaxX = 0.5f;
                        break;
                    case HorizontalConstraint.Left:
                        AnchorMinX = AnchorMaxX = 0f;
                        break;
                    case HorizontalConstraint.Right:
                        AnchorMinX = AnchorMaxX = 1f;
                        break;
                    case HorizontalConstraint.Scale:
                        AnchorMinX = 0f;
                        AnchorMaxX = 1f;
                        break;
                }
            }

            public HorizontalConstraint GetHorizontalAxis()
            {
                if (Mathf.Approximately(AnchorMinX, AnchorMaxX))
                {
                    if (Mathf.Approximately(AnchorMinX, 0.5f))
                        return HorizontalConstraint.Center;
                    if (Mathf.Approximately(AnchorMinX, 0f))
                        return HorizontalConstraint.Left;
                    if (Mathf.Approximately(AnchorMinX, 1f))
                        return HorizontalConstraint.Right;
                }

                if (Mathf.Approximately(AnchorMinX, 0) && Mathf.Approximately(AnchorMaxX, 1))
                    return HorizontalConstraint.Scale;

                return HorizontalConstraint.Custom;
            }

            public enum HorizontalConstraint
            {
                Left,
                Center,
                Right,
                Scale,
                Custom
            }

            public enum VerticalConstraint
            {
                Bottom,
                Center,
                Top,
                Scale,
                Custom
            }

            public void SetAxis(bool isX, float value)
            {
                if (isX)
                {
                    var oldX = OffsetMinX;

                    OffsetMinX = value;
                    OffsetMaxX = OffsetMaxX - oldX + value;
                }
                else
                {
                    var oldY = -OffsetMaxY;

                    OffsetMaxY = -value;
                    OffsetMinY = OffsetMinY + oldY - value;
                }
            }

            public void MoveX(float value)
            {
                OffsetMinX += value;
                OffsetMaxX += value;
            }

            public void MoveY(float value)
            {
                OffsetMinY += value;
                OffsetMaxY += value;
            }

            public float GetPadding(int type = 0) // 0 - left, 1 - right, 2 - top, 3 - bottom
            {
                switch (type)
                {
                    case 0: return OffsetMinX;
                    case 1: return -OffsetMaxX;
                    case 2: return -OffsetMaxY;
                    case 3: return OffsetMinY;
                    default: return OffsetMinX;
                }
            }

            public void SetPadding(
                float? left = null,
                float? top = null,
                float? right = null,
                float? bottom = null)
            {
                if (left.HasValue) OffsetMinX = left.Value;
                if (right.HasValue) OffsetMaxX = -right.Value;

                if (bottom.HasValue) OffsetMinY = bottom.Value;
                if (top.HasValue) OffsetMaxY = -top.Value;
            }

            public float GetWidth()
            {
                return OffsetMaxX - OffsetMinX;
            }

            public void SetWidth(float width)
            {
                if (GetHorizontalAxis() == HorizontalConstraint.Center)
                {
                    var half = (float) Math.Round(width / 2f, 2);

                    OffsetMinX = -half;
                    OffsetMaxX = half;
                    return;
                }

                OffsetMaxX = OffsetMinX + width;
            }

            public float GetHeight()
            {
                return OffsetMaxY - OffsetMinY;
            }

            public void SetHeight(float height)
            {
                if (GetVerticalAxis() == VerticalConstraint.Center)
                {
                    var half = (float) Math.Round(height / 2f, 2);

                    OffsetMinY = -half;
                    OffsetMaxY = half;
                    return;
                }

                OffsetMaxY = OffsetMinY + height;
            }

            public Rect GetRect()
            {
                var rect = new Rect();

                ManipulateRect(GetPivot(), rectTransform => rect = rectTransform.rect);

                return rect;
            }

            private Vector2 GetPivot()
            {
                return Mathf.Approximately(AnchorMinX, 0.5f) ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f);
            }

            private void ManipulateRect(Vector2 pivot, Action<RectTransform> callback)
            {
                var rect = new GameObject().AddComponent<RectTransform>();
                try
                {
                    rect.pivot = pivot;
                    rect.anchorMin = new Vector2(AnchorMinX, AnchorMinY);
                    rect.anchorMax = new Vector2(AnchorMaxX, AnchorMaxY);
                    rect.offsetMin = new Vector2(OffsetMinX, OffsetMinY);
                    rect.offsetMax = new Vector2(OffsetMaxX, OffsetMaxY);

                    callback?.Invoke(rect);

                    AnchorMinX = rect.anchorMin.x;
                    AnchorMinY = rect.anchorMin.y;
                    AnchorMaxX = rect.anchorMax.x;
                    AnchorMaxY = rect.anchorMax.y;
                    OffsetMinX = rect.offsetMin.x;
                    OffsetMinY = rect.offsetMin.y;
                    OffsetMaxX = rect.offsetMax.x;
                    OffsetMaxY = rect.offsetMax.y;
                }
                finally
                {
                    UnityEngine.Object.Destroy(rect.gameObject);
                }
            }

            #region CuiRectTransformComponent

            [JsonIgnore] private CuiRectTransformComponent _cachedRectTransform;

            public CuiRectTransformComponent GetRectTransform()
            {
                if (_cachedRectTransform != null)
                    return _cachedRectTransform;

                _cachedRectTransform = new CuiRectTransformComponent
                {
                    AnchorMin = $"{AnchorMinX} {AnchorMinY}",
                    AnchorMax = $"{AnchorMaxX} {AnchorMaxY}",
                    OffsetMin = $"{OffsetMinX} {OffsetMinY}",
                    OffsetMax = $"{OffsetMaxX} {OffsetMaxY}"
                };

                return _cachedRectTransform;
            }

            public void InvalidateCache()
            {
                _cachedRectTransform = null;
            }

            #endregion

            public CuiRectTransformComponent GetRectTransform(Func<float, float> formatterOffMaxX,
                Func<float, float> formatterOffMaxY)
            {
                var oMaxX = OffsetMaxX;
                if (formatterOffMaxX != null) oMaxX = formatterOffMaxX(OffsetMaxX);

                var oMaxY = OffsetMaxY;
                if (formatterOffMaxY != null) oMaxY = formatterOffMaxY(OffsetMaxY);

                return new CuiRectTransformComponent
                {
                    AnchorMin = $"{AnchorMinX} {AnchorMinY}",
                    AnchorMax = $"{AnchorMaxX} {AnchorMaxY}",
                    OffsetMin = $"{OffsetMinX} {OffsetMinY}",
                    OffsetMax = $"{oMaxX} {oMaxY}"
                };
            }

            public override string ToString()
            {
                return JsonConvert.SerializeObject(GetRectTransform(), 0, new JsonSerializerSettings
                {
                    DefaultValueHandling = DefaultValueHandling.Ignore
                }).Replace("\\n", "\n");
            }

            #endregion

            #region Constructors

            public static InterfacePosition CreatePosition(float aMinX, float aMinY, float aMaxX, float aMaxY,
                float oMinX, float oMinY, float oMaxX, float oMaxY)
            {
                return new InterfacePosition
                {
                    AnchorMinX = aMinX,
                    AnchorMinY = aMinY,
                    AnchorMaxX = aMaxX,
                    AnchorMaxY = aMaxY,
                    OffsetMinX = oMinX,
                    OffsetMinY = oMinY,
                    OffsetMaxX = oMaxX,
                    OffsetMaxY = oMaxY
                };
            }

            public static InterfacePosition CreatePosition(
                string anchorMin = "0 0",
                string anchorMax = "1 1",
                string offsetMin = "0 0",
                string offsetMax = "0 0")
            {
                var aMinX = float.Parse(anchorMin.Split(' ')[0]);
                var aMinY = float.Parse(anchorMin.Split(' ')[1]);
                var aMaxX = float.Parse(anchorMax.Split(' ')[0]);
                var aMaxY = float.Parse(anchorMax.Split(' ')[1]);
                var oMinX = float.Parse(offsetMin.Split(' ')[0]);
                var oMinY = float.Parse(offsetMin.Split(' ')[1]);
                var oMaxX = float.Parse(offsetMax.Split(' ')[0]);
                var oMaxY = float.Parse(offsetMax.Split(' ')[1]);

                return new InterfacePosition
                {
                    AnchorMinX = aMinX,
                    AnchorMinY = aMinY,
                    AnchorMaxX = aMaxX,
                    AnchorMaxY = aMaxY,
                    OffsetMinX = oMinX,
                    OffsetMinY = oMinY,
                    OffsetMaxX = oMaxX,
                    OffsetMaxY = oMaxY
                };
            }

            public static InterfacePosition CreatePosition(CuiRectTransform rectTransform)
            {
                var aMinX = float.Parse(rectTransform.AnchorMin.Split(' ')[0]);
                var aMinY = float.Parse(rectTransform.AnchorMin.Split(' ')[1]);
                var aMaxX = float.Parse(rectTransform.AnchorMax.Split(' ')[0]);
                var aMaxY = float.Parse(rectTransform.AnchorMax.Split(' ')[1]);
                var oMinX = float.Parse(rectTransform.OffsetMin.Split(' ')[0]);
                var oMinY = float.Parse(rectTransform.OffsetMin.Split(' ')[1]);
                var oMaxX = float.Parse(rectTransform.OffsetMax.Split(' ')[0]);
                var oMaxY = float.Parse(rectTransform.OffsetMax.Split(' ')[1]);

                return new InterfacePosition
                {
                    AnchorMinX = aMinX,
                    AnchorMinY = aMinY,
                    AnchorMaxX = aMaxX,
                    AnchorMaxY = aMaxY,
                    OffsetMinX = oMinX,
                    OffsetMinY = oMinY,
                    OffsetMaxX = oMaxX,
                    OffsetMaxY = oMaxY
                };
            }

            #endregion Constructors
        }

        public enum CuiElementType
        {
            Label,
            Panel,
            Button,
            Image,
            InputField
        }

        [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
        private sealed class TextEditableAttribute : Attribute
        {
        }

        public class UiElement : InterfacePosition
        {
            #region Fields

            [JsonProperty(PropertyName = "Enabled?")]
            public bool Enabled;

            [JsonProperty(PropertyName = "Visible")]
            public bool Visible = true;

            [JsonProperty(PropertyName = "Name")] public string Name = string.Empty;

            [JsonProperty(PropertyName = "Type (Label/Panel/Button/Image)")]
            [JsonConverter(typeof(StringEnumConverter))]
            public CuiElementType Type;

            [JsonProperty(PropertyName = "Color")] public IColor Color = new("#FFFFFF", 100);

            [TextEditable]
            [JsonProperty(PropertyName = "Text", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Text = new();

            [JsonProperty(PropertyName = "Font Size")]
            public int FontSize;

            [JsonProperty(PropertyName = "Font")] public CuiElementFont Font = CuiElementFont.RobotoCondensedBold;

            [JsonProperty(PropertyName = "Align")] [JsonConverter(typeof(StringEnumConverter))]
            public TextAnchor Align;

            [JsonProperty(PropertyName = "Text Color")]
            public IColor TextColor = new("#FFFFFF", 100);

            [JsonProperty(PropertyName = "Command ({user} - user steamid)")]
            public string Command = string.Empty;

            [JsonProperty(PropertyName = "Image")] public string Image = string.Empty;

            [JsonProperty(PropertyName = "Cursor Enabled")]
            public bool CursorEnabled;

            [JsonProperty(PropertyName = "Keyboard Enabled")]
            public bool KeyboardEnabled;

            [JsonProperty(PropertyName = "Sprite")]
            public string Sprite = string.Empty;

            [JsonProperty(PropertyName = "Material")]
            public string Material = string.Empty;

            #endregion Fields

            #region Public Methods

            public new void InvalidateCache()
            {
                base.InvalidateCache();

                Color?.InvalidateCache();
                TextColor?.InvalidateCache();
            }

            public bool TryGetImage(out string image)
            {
                if (Type == CuiElementType.Image)
                    if (!string.IsNullOrEmpty(Image))
                        if (Image.IsURL() || Image.StartsWith("TheMevent/"))
                        {
                            image = Image;
                            return true;
                        }

                image = null;
                return false;
            }

            public void Get(ref CuiElementContainer container, BasePlayer player,
                string parent,
                string name = null,
                string destroy = "",
                string close = "",
                Func<string, string> textFormatter = null,
                Func<string, string> cmdFormatter = null,
                bool needUpdate = false,
                CuiRectTransformComponent customRectTransform = null)
            {
                if (!Enabled) return;

                if (string.IsNullOrEmpty(name))
                    name = CuiHelper.GetGuid();

                if (needUpdate) destroy = string.Empty;

                switch (Type)
                {
                    case CuiElementType.Label:
                    {
                        var targetText = GetLocalizedText(player);

                        var text = string.Join("\n", targetText).Replace("<br>", "\n");

                        if (textFormatter != null)
                            text = textFormatter(text);

                        container.Add(new CuiElement
                        {
                            Name = name,
                            Parent = parent,
                            DestroyUi = destroy,
                            Update = needUpdate,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = Visible ? text : string.Empty,
                                    Align = Align,
                                    Font = GetFontByType(Font),
                                    FontSize = FontSize,
                                    Color = Visible ? TextColor.Get() : "0 0 0 0"
                                },
                                customRectTransform ?? GetRectTransform()
                            }
                        });
                        break;
                    }

                    case CuiElementType.InputField:
                    {
                        var targetText = GetLocalizedText(player);

                        var text = string.Join("\n", targetText).Replace("<br>", "\n");

                        if (textFormatter != null)
                            text = textFormatter(text);

                        container.Add(new CuiElement
                        {
                            Name = name,
                            Parent = parent,
                            DestroyUi = destroy,
                            Update = needUpdate,
                            Components =
                            {
                                new CuiInputFieldComponent
                                {
                                    Text = Visible ? text : string.Empty,
                                    Align = Align,
                                    Font = GetFontByType(Font),
                                    FontSize = FontSize,
                                    Color = Visible ? TextColor.Get() : "0 0 0 0",
                                    HudMenuInput = true,
                                    ReadOnly = true
                                },
                                customRectTransform ?? GetRectTransform()
                            }
                        });
                        break;
                    }

                    case CuiElementType.Panel:
                    {
                        var imageElement = new CuiImageComponent
                        {
                            Color = Visible ? Color.Get() : "0 0 0 0"
                        };

                        if (!string.IsNullOrEmpty(Sprite)) imageElement.Sprite = Sprite;
                        if (!string.IsNullOrEmpty(Material)) imageElement.Material = Material;

                        var cuiElement = new CuiElement
                        {
                            Name = name,
                            Parent = parent,
                            DestroyUi = destroy,
                            Update = needUpdate,
                            Components =
                            {
                                imageElement,
                                customRectTransform ?? GetRectTransform()
                            }
                        };

                        if (CursorEnabled)
                            cuiElement.Components.Add(new CuiNeedsCursorComponent());

                        if (KeyboardEnabled)
                            cuiElement.Components.Add(new CuiNeedsKeyboardComponent());

                        container.Add(cuiElement);
                        break;
                    }

                    case CuiElementType.Button:
                    {
                        var targetCommand = $"{Command}".Replace("{user}", player.UserIDString);

                        if (cmdFormatter != null)
                            targetCommand = cmdFormatter(targetCommand);

                        var btnElement = new CuiButtonComponent
                        {
                            Command = targetCommand,
                            Color = Visible ? Color.Get() : "0 0 0 0",
                            Close = close
                        };

                        if (!string.IsNullOrEmpty(Sprite)) btnElement.Sprite = Sprite;
                        if (!string.IsNullOrEmpty(Material)) btnElement.Material = Material;

                        container.Add(new CuiElement
                        {
                            Name = name,
                            Parent = parent,
                            DestroyUi = destroy,
                            Update = needUpdate,
                            Components =
                            {
                                btnElement,
                                customRectTransform ?? GetRectTransform()
                            }
                        });

                        var targetText = GetLocalizedText(player);
                        var message = string.Join("\n", targetText)?.Replace("<br>", "\n") ?? string.Empty;

                        if (textFormatter != null)
                            message = textFormatter(message);

                        if (!string.IsNullOrEmpty(message))
                            container.Add(new CuiElement
                            {
                                Parent = name,
                                Components =
                                {
                                    new CuiTextComponent
                                    {
                                        Text = Visible ? message : string.Empty,
                                        Align = Align,
                                        Font = GetFontByType(Font),
                                        FontSize = FontSize,
                                        Color = Visible ? TextColor.Get() : "0 0 0 0"
                                    },
                                    customRectTransform ?? new CuiRectTransformComponent()
                                }
                            });

                        break;
                    }

                    case CuiElementType.Image:
                    {
                        if (string.IsNullOrEmpty(Image)) return;

                        ICuiComponent imageElement;
                        if (Image == "{player_avatar}")
                        {
                            var image = Image;
                            if (textFormatter != null)
                                image = textFormatter(image);

                            imageElement = new CuiRawImageComponent
                            {
                                SteamId = image,
                                Color = Visible ? Color.Get() : "0 0 0 0"
                            };
                        }
                        else
                        {
                            if (Image.StartsWith("assets/"))
                            {
                                if (Image.Contains("Linear"))
                                    imageElement = new CuiRawImageComponent
                                    {
                                        Color = Visible ? Color.Get() : "0 0 0 0",
                                        Sprite = Image
                                    };
                                else
                                    imageElement = new CuiImageComponent
                                    {
                                        Color = Enabled ? Color.Get() : "0 0 0 0",
                                        Sprite = Image
                                    };
                            }
                            else if (Image.IsURL())
                            {
                                imageElement = new CuiRawImageComponent
                                {
                                    Png = Instance?.GetImage(Image),
                                    Color = Visible ? Color.Get() : "0 0 0 0"
                                };
                            }
                            else
                            {
                                var image = Image;
                                if (textFormatter != null)
                                    image = textFormatter(image);

                                imageElement = new CuiRawImageComponent
                                {
                                    Png = Instance?.GetImage(image),
                                    Color = Visible ? Color.Get() : "0 0 0 0"
                                };
                            }
                        }

                        var cuiElement = new CuiElement
                        {
                            Name = name,
                            Parent = parent,
                            DestroyUi = destroy,
                            Update = needUpdate,
                            Components =
                            {
                                imageElement,
                                customRectTransform ?? GetRectTransform()
                            }
                        };

                        if (CursorEnabled)
                            cuiElement.Components.Add(new CuiNeedsCursorComponent());

                        if (KeyboardEnabled)
                            cuiElement.Components.Add(new CuiNeedsKeyboardComponent());

                        container.Add(cuiElement);
                        break;
                    }
                }
            }

            #region Serialization

            public string GetSerialized(BasePlayer player,
                string parent,
                string name = null,
                string destroy = "",
                string close = "",
                Func<string, string> textFormatter = null,
                Func<string, string> cmdFormatter = null,
                bool needUpdate = false,
                (string aMin, string aMax, string oMin, string oMax)? customRect = null,
                (bool raw, string steamId, string png, string sprite, string imageColor, int? itemId, ulong? skinID)?
                    customImageSettings = null,
                (int? charsLimits, bool readOnly, bool needKeyboard, bool hudMenuInput, bool password, bool autofocus,
                    InputField.LineType? lineType)? inputFieldSettings = null,
                (float endTime, float startTime, float step, TimerFormat timerFormat, bool destroyIfDone, string command
                    )? countdownSettings = null)
            {
                if (!Enabled) return string.Empty;

                if (string.IsNullOrEmpty(name))
                    name = CuiHelper.GetGuid();

                if (needUpdate) destroy = string.Empty;

                var sb = Pool.Get<StringBuilder>();
                try
                {
                    switch (Type)
                    {
                        case CuiElementType.Label:
                            SerializeLabel(sb, player, parent, name, destroy, needUpdate, textFormatter, customRect,
                                countdownSettings);
                            break;

                        case CuiElementType.InputField:
                            SerializeInputField(sb, player, parent, name, destroy, needUpdate, textFormatter,
                                cmdFormatter, customRect, inputFieldSettings);
                            break;

                        case CuiElementType.Panel:
                            SerializePanel(sb, parent, name, destroy, needUpdate, customRect, customImageSettings);
                            break;

                        case CuiElementType.Button:
                            SerializeButton(sb, player, parent, name, destroy, close, needUpdate, textFormatter,
                                cmdFormatter,
                                customRect,
                                customImageSettings);
                            break;

                        case CuiElementType.Image:
                            SerializeImage(sb, player, parent, name, destroy, needUpdate, textFormatter, customRect,
                                customImageSettings);
                            break;
                    }

                    return sb.ToString();
                }
                finally
                {
                    Pool.FreeUnmanaged(ref sb);
                }
            }

            private void SerializeLabel(StringBuilder sb,
                BasePlayer player,
                string parent,
                string name,
                string destroy,
                bool needUpdate,
                Func<string, string> textFormatter,
                (string aMin, string aMax, string oMin, string oMax)? customRect = null,
                (float endTime, float startTime, float step, TimerFormat timerFormat, bool destroyIfDone, string command
                    )? countdownSettings = null)
            {
                var targetText = GetLocalizedText(player);
                var text = string.Join("\n", targetText).Replace("<br>", "\n");

                if (textFormatter != null)
                    text = textFormatter(text);

                var displayText = Visible ? text : string.Empty;
                var textColor = Visible ? TextColor.Get() : "0 0 0 0";
                var rectTransform = GetRectTransform();

                if (customRect.HasValue)
                {
                    var (aMin, aMax, oMin, oMax) = customRect.Value;
                    rectTransform.AnchorMin = aMin;
                    rectTransform.AnchorMax = aMax;
                    rectTransform.OffsetMin = oMin;
                    rectTransform.OffsetMax = oMax;
                }

                sb.Append('{');
                sb.Append("\"name\":\"").Append(name).Append("\",");
                sb.Append("\"parent\":\"").Append(parent).Append("\",");
                if (needUpdate) sb.Append("\"update\":true,");
                sb.Append("\"components\":[{");
                sb.Append("\"type\":\"UnityEngine.UI.Text\",");
                sb.Append("\"text\":\"").Append((displayText ?? string.Empty).Replace("\"", "\\\"")).Append("\",");
                sb.Append("\"align\":\"").Append(Align.ToString()).Append("\",");
                sb.Append("\"font\":\"").Append(GetFontByType(Font)).Append("\",");
                sb.Append("\"fontSize\":").Append(FontSize).Append(",");
                sb.Append("\"color\":\"").Append(textColor).Append('\"');
                sb.Append("},{");
                sb.Append("\"type\":\"RectTransform\",");
                sb.Append("\"anchormin\":\"").Append(rectTransform.AnchorMin).Append("\",");
                sb.Append("\"anchormax\":\"").Append(rectTransform.AnchorMax).Append("\",");
                sb.Append("\"offsetmin\":\"").Append(rectTransform.OffsetMin).Append("\",");
                sb.Append("\"offsetmax\":\"").Append(rectTransform.OffsetMax).Append('\"');
                sb.Append("}");

                if (countdownSettings.HasValue)
                {
                    var (endTime, startTime, step, timerFormat, destroyIfDone, command) = countdownSettings.Value;
                    sb.Append(",{");
                    sb.Append("\"type\":\"Countdown\",");
                    sb.Append("\"endTime\":").Append(endTime).Append(",");
                    sb.Append("\"startTime\":").Append(startTime).Append(",");
                    sb.Append("\"step\":").Append(step).Append(",");
                    sb.Append("\"timerFormat\":\"").Append(timerFormat.ToString()).Append("\",");
                    sb.Append("\"destroyIfDone\":").Append(destroyIfDone ? "true" : "false");
                    if (!string.IsNullOrEmpty(command))
                        sb.Append(",\"command\":\"").Append(command).Append("\"");
                    sb.Append("}");
                }

                sb.Append("]");

                if (!string.IsNullOrEmpty(destroy))
                    sb.Append(",\"destroyUi\":\"").Append(destroy).Append('\"');

                sb.Append('}');
            }

            private void SerializeInputField(StringBuilder sb, BasePlayer player, string parent, string name,
                string destroy, bool needUpdate, Func<string, string> textFormatter,
                Func<string, string> cmdFormatter = null,
                (string aMin, string aMax, string oMin, string oMax)? customRect = null,
                (int? charsLimits, bool readOnly, bool needKeyboard, bool hudMenuInput, bool password, bool autofocus,
                    InputField.LineType? lineType)? inputFieldSettings = null)
            {
                var targetCommand = Command.Replace("{user}", player.UserIDString);
                if (cmdFormatter != null)
                    targetCommand = cmdFormatter(targetCommand);

                var targetText = GetLocalizedText(player);
                var text = string.Join("\n", targetText).Replace("<br>", "\n");

                if (textFormatter != null)
                    text = textFormatter(text);

                var displayText = Visible ? text : string.Empty;
                var textColor = Visible ? TextColor.Get() : "0 0 0 0";
                var rectTransform = GetRectTransform();

                if (customRect.HasValue)
                {
                    var (aMin, aMax, oMin, oMax) = customRect.Value;
                    rectTransform.AnchorMin = aMin;
                    rectTransform.AnchorMax = aMax;
                    rectTransform.OffsetMin = oMin;
                    rectTransform.OffsetMax = oMax;
                }

                sb.Append('{');
                sb.Append("\"name\":\"").Append(name).Append("\",");
                sb.Append("\"parent\":\"").Append(parent).Append("\",");
                if (needUpdate) sb.Append("\"update\":true,");
                sb.Append("\"components\":[{");
                sb.Append("\"type\":\"UnityEngine.UI.InputField\",");
                sb.Append("\"text\":\"").Append(displayText.Replace("\"", "\\\"")).Append("\",");
                sb.Append("\"align\":\"").Append(Align.ToString()).Append("\",");
                sb.Append("\"font\":\"").Append(GetFontByType(Font)).Append("\",");
                sb.Append("\"fontSize\":").Append(FontSize).Append(",");
                sb.Append("\"color\":\"").Append(textColor).Append("\"");

                if (!string.IsNullOrEmpty(targetCommand))
                    sb.Append(",\"command\":\"").Append(targetCommand).Append("\"");

                if (inputFieldSettings.HasValue)
                {
                    var (charsLimits, readOnly, needKeyboard, hudMenuInput, password, autofocus, lineType) =
                        inputFieldSettings.Value;

                    if (charsLimits.HasValue)
                        sb.Append(",\"charsLimit\":").Append(charsLimits.Value);

                    if (readOnly)
                        sb.Append(",\"readOnly\":true");

                    if (needKeyboard)
                        sb.Append(",\"needsKeyboard\":true");

                    if (hudMenuInput)
                        sb.Append(",\"hudMenuInput\":true");

                    if (password)
                        sb.Append(",\"password\":true");

                    if (autofocus)
                        sb.Append(",\"autofocus\":true");

                    if (lineType.HasValue)
                        sb.Append(",\"lineType\":\"").Append(lineType.Value.ToString()).Append("\"");
                    ;
                }

                sb.Append("},{");
                sb.Append("\"type\":\"RectTransform\",");
                sb.Append("\"anchormin\":\"").Append(rectTransform.AnchorMin).Append("\",");
                sb.Append("\"anchormax\":\"").Append(rectTransform.AnchorMax).Append("\",");
                sb.Append("\"offsetmin\":\"").Append(rectTransform.OffsetMin).Append("\",");
                sb.Append("\"offsetmax\":\"").Append(rectTransform.OffsetMax).Append('\"');
                sb.Append("}],");

                sb.Append("\"destroyUi\":\"");
                if (!string.IsNullOrEmpty(destroy))
                    sb.Append(destroy);
                sb.Append('\"');

                sb.Append('}');
            }

            private void SerializePanel(StringBuilder sb, string parent, string name, string destroy, bool needUpdate,
                (string aMin, string aMax, string oMin, string oMax)? customRect = null,
                (bool raw, string steamId, string png, string sprite, string imageColor, int? itemId, ulong? skinID)?
                    customImageSettings = null)
            {
                var color = Visible ? Color.Get() : "0 0 0 0";
                var rectTransform = GetRectTransform();

                if (customRect.HasValue)
                {
                    var (aMin, aMax, oMin, oMax) = customRect.Value;
                    rectTransform.AnchorMin = aMin;
                    rectTransform.AnchorMax = aMax;
                    rectTransform.OffsetMin = oMin;
                    rectTransform.OffsetMax = oMax;
                }

                sb.Append('{');
                sb.Append("\"name\":\"").Append(name).Append("\",");
                sb.Append("\"parent\":\"").Append(parent).Append("\",");
                if (needUpdate) sb.Append("\"update\":true,");
                sb.Append("\"components\":[{");
                sb.Append("\"type\":\"UnityEngine.UI.Image\",");

                if (customImageSettings.HasValue)
                {
                    var (raw, steamID, png, sprite, imageColor, itemID, skinID) = customImageSettings.Value;

                    sb.Append("\"color\":\"").Append(imageColor ?? color).Append('\"');

                    if (!string.IsNullOrEmpty(sprite))
                        sb.Append(",\"sprite\":\"").Append(sprite).Append('\"');
                    else if (!string.IsNullOrEmpty(Sprite))
                        sb.Append(",\"sprite\":\"").Append(Sprite).Append('\"');

                    if (!string.IsNullOrEmpty(png))
                        sb.Append(",\"png\":\"").Append(png).Append('\"');

                    if (!string.IsNullOrEmpty(steamID))
                        sb.Append(",\"steamid\":\"").Append(steamID).Append('\"');

                    if (itemID.HasValue)
                        sb.Append(",\"itemid\":\"").Append(itemID.Value).Append('\"');

                    if (skinID.HasValue && skinID.Value != 0)
                        sb.Append(",\"skinid\":\"").Append(skinID.Value).Append('\"');

                    if (!string.IsNullOrEmpty(Material))
                        sb.Append(",\"material\":\"").Append(Material).Append('\"');
                }
                else
                {
                    sb.Append("\"color\":\"").Append(color).Append('\"');

                    if (!string.IsNullOrEmpty(Sprite))
                        sb.Append(",\"sprite\":\"").Append(Sprite).Append('\"');

                    if (!string.IsNullOrEmpty(Material))
                        sb.Append(",\"material\":\"").Append(Material).Append('\"');
                }

                sb.Append("},{");
                sb.Append("\"type\":\"RectTransform\",");
                sb.Append("\"anchormin\":\"").Append(rectTransform.AnchorMin).Append("\",");
                sb.Append("\"anchormax\":\"").Append(rectTransform.AnchorMax).Append("\",");
                sb.Append("\"offsetmin\":\"").Append(rectTransform.OffsetMin).Append("\",");
                sb.Append("\"offsetmax\":\"").Append(rectTransform.OffsetMax).Append('\"');
                sb.Append("}");

                if (CursorEnabled) sb.Append(",{\"type\":\"NeedsCursor\"}");

                if (KeyboardEnabled) sb.Append(",{\"type\":\"NeedsKeyboard\"}");

                sb.Append("],");

                sb.Append("\"destroyUi\":\"");
                if (!string.IsNullOrEmpty(destroy))
                    sb.Append(destroy);
                sb.Append('\"');

                sb.Append('}');
            }

            private void SerializeButton(StringBuilder sb, BasePlayer player, string parent, string name,
                string destroy, string close, bool needUpdate, Func<string, string> textFormatter,
                Func<string, string> cmdFormatter,
                (string aMin, string aMax, string oMin, string oMax)? customRect = null,
                (bool raw, string steamId, string png, string sprite, string imageColor, int? itemId, ulong? skinID)?
                    customImageSettings = null)
            {
                var targetCommand = Command.Replace("{user}", player.UserIDString);
                if (cmdFormatter != null)
                    targetCommand = cmdFormatter(targetCommand);

                var color = Visible ? Color.Get() : "0 0 0 0";
                var rectTransform = GetRectTransform();

                if (customRect.HasValue)
                {
                    var (aMin, aMax, oMin, oMax) = customRect.Value;
                    rectTransform.AnchorMin = aMin;
                    rectTransform.AnchorMax = aMax;
                    rectTransform.OffsetMin = oMin;
                    rectTransform.OffsetMax = oMax;
                }

                // Main button
                sb.Append('{');
                sb.Append("\"name\":\"").Append(name).Append("\",");
                sb.Append("\"parent\":\"").Append(parent).Append("\",");
                if (needUpdate) sb.Append("\"update\":true,");
                sb.Append("\"components\":[{");
                sb.Append("\"type\":\"UnityEngine.UI.Button\",");
                sb.Append("\"command\":\"").Append(targetCommand).Append("\",");

                if (customImageSettings.HasValue)
                {
                    var (raw, steamID, png, sprite, imageColor, itemID, skinID) = customImageSettings.Value;

                    sb.Append("\"color\":\"").Append(imageColor ?? color).Append('\"');

                    if (!string.IsNullOrEmpty(sprite))
                        sb.Append(",\"sprite\":\"").Append(sprite).Append('\"');
                    else if (!string.IsNullOrEmpty(Sprite))
                        sb.Append(",\"sprite\":\"").Append(Sprite).Append('\"');
                }
                else
                {
                    sb.Append("\"color\":\"").Append(color).Append('\"');

                    if (!string.IsNullOrEmpty(close))
                        sb.Append(",\"close\":\"").Append(close).Append('\"');

                    if (!string.IsNullOrEmpty(Sprite))
                        sb.Append(",\"sprite\":\"").Append(Sprite).Append('\"');

                    if (!string.IsNullOrEmpty(Material))
                        sb.Append(",\"material\":\"").Append(Material).Append('\"');
                }

                sb.Append("},{");
                sb.Append("\"type\":\"RectTransform\",");
                sb.Append("\"anchormin\":\"").Append(rectTransform.AnchorMin).Append("\",");
                sb.Append("\"anchormax\":\"").Append(rectTransform.AnchorMax).Append("\",");
                sb.Append("\"offsetmin\":\"").Append(rectTransform.OffsetMin).Append("\",");
                sb.Append("\"offsetmax\":\"").Append(rectTransform.OffsetMax).Append('\"');
                sb.Append("}],");

                sb.Append("\"destroyUi\":\"");
                if (!string.IsNullOrEmpty(destroy))
                    sb.Append(destroy);
                sb.Append('\"');

                sb.Append('}');

                // Text for button (if exists)
                var targetText = GetLocalizedText(player);
                var message = string.Join("\n", targetText).Replace("<br>", "\n");

                if (textFormatter != null)
                    message = textFormatter(message);

                if (!string.IsNullOrEmpty(message))
                {
                    sb.Append(",{\"parent\":\"").Append(name).Append("\",");
                    sb.Append("\"components\":[{");
                    sb.Append("\"type\":\"UnityEngine.UI.Text\",");
                    sb.Append("\"text\":\"").Append((Visible ? message : string.Empty).Replace("\"", "\\\""))
                        .Append("\",");
                    sb.Append("\"align\":\"").Append(Align.ToString()).Append("\",");
                    sb.Append("\"font\":\"").Append(GetFontByType(Font)).Append("\",");
                    sb.Append("\"fontSize\":").Append(FontSize).Append(",");
                    sb.Append("\"color\":\"").Append(Visible ? TextColor.Get() : "0 0 0 0").Append('\"');
                    sb.Append("},{");
                    sb.Append("\"type\":\"RectTransform\"");
                    sb.Append("}]}");
                }
            }

            private void SerializeImage(StringBuilder sb, BasePlayer player, string parent, string name,
                string destroy, bool needUpdate, Func<string, string> textFormatter,
                (string aMin, string aMax, string oMin, string oMax)? customRect = null,
                (bool raw, string steamId, string png, string sprite, string imageColor, int? itemId, ulong? skinID)?
                    customImageSettings = null)
            {
                // if (string.IsNullOrEmpty(Image)) return;

                var color = Visible ? Color.Get() : "0 0 0 0";
                var rectTransform = GetRectTransform();

                if (customRect.HasValue)
                {
                    var (aMin, aMax, oMin, oMax) = customRect.Value;
                    rectTransform.AnchorMin = aMin;
                    rectTransform.AnchorMax = aMax;
                    rectTransform.OffsetMin = oMin;
                    rectTransform.OffsetMax = oMax;
                }

                sb.Append('{');
                sb.Append("\"name\":\"").Append(name).Append("\",");
                sb.Append("\"parent\":\"").Append(parent).Append("\",");
                if (needUpdate) sb.Append("\"update\":true,");
                sb.Append("\"components\":[{");

                if (customImageSettings.HasValue)
                {
                    var (raw, steamID, png, sprite, imageColor, itemID, skinID) = customImageSettings.Value;

                    if (raw)
                    {
                        sb.Append("\"type\":\"UnityEngine.UI.RawImage\",");

                        if (!string.IsNullOrEmpty(steamID))
                            sb.Append("\"steamid\":\"").Append(steamID).Append("\",");

                        if (!string.IsNullOrEmpty(png))
                            sb.Append("\"png\":\"").Append(png ?? "").Append("\",");

                        if (!string.IsNullOrEmpty(sprite))
                            sb.Append("\"sprite\":\"").Append(sprite).Append("\",");
                    }
                    else
                    {
                        sb.Append("\"type\":\"UnityEngine.UI.Image\",");

                        if (itemID.HasValue)
                            sb.Append("\"itemid\":\"").Append(itemID.Value).Append("\",");

                        if (skinID.HasValue && skinID.Value != 0)
                            sb.Append("\"skinid\":\"").Append(skinID.Value).Append("\",");

                        if (!string.IsNullOrEmpty(sprite))
                            sb.Append("\"sprite\":\"").Append(sprite).Append("\",");
                    }

                    sb.Append("\"color\":\"").Append(imageColor ?? "1 1 1 1").Append("\"");
                }
                else
                {
                    if (Image == "{player_avatar}")
                    {
                        var image = textFormatter != null ? textFormatter(Image) : Image;
                        sb.Append("\"type\":\"UnityEngine.UI.RawImage\",");
                        sb.Append("\"steamid\":\"").Append(image).Append("\",");
                        sb.Append("\"color\":\"").Append(color).Append('\"');
                    }
                    else if (Image.StartsWith("assets/"))
                    {
                        if (Image.Contains("Linear"))
                        {
                            sb.Append("\"type\":\"UnityEngine.UI.RawImage\",");
                            sb.Append("\"color\":\"").Append(color).Append("\",");
                            sb.Append("\"sprite\":\"").Append(Image).Append('\"');
                        }
                        else
                        {
                            sb.Append("\"type\":\"UnityEngine.UI.Image\",");
                            sb.Append("\"color\":\"").Append(Enabled ? Color.Get() : "0 0 0 0").Append("\",");
                            sb.Append("\"sprite\":\"").Append(Image).Append('\"');
                        }
                    }
                    else if (Image.IsURL())
                    {
                        sb.Append("\"type\":\"UnityEngine.UI.RawImage\",");
                        sb.Append("\"png\":\"").Append(Instance?.GetImage(Image) ?? "").Append("\",");
                        sb.Append("\"color\":\"").Append(color).Append('\"');
                    }
                    else
                    {
                        var image = textFormatter != null ? textFormatter(Image) : Image;
                        sb.Append("\"type\":\"UnityEngine.UI.RawImage\",");
                        sb.Append("\"png\":\"").Append(Instance?.GetImage(image) ?? "").Append("\",");
                        sb.Append("\"color\":\"").Append(color).Append('\"');
                    }
                }

                sb.Append("},{");
                sb.Append("\"type\":\"RectTransform\",");
                sb.Append("\"anchormin\":\"").Append(rectTransform.AnchorMin).Append("\",");
                sb.Append("\"anchormax\":\"").Append(rectTransform.AnchorMax).Append("\",");
                sb.Append("\"offsetmin\":\"").Append(rectTransform.OffsetMin).Append("\",");
                sb.Append("\"offsetmax\":\"").Append(rectTransform.OffsetMax).Append('\"');
                sb.Append("}");

                if (CursorEnabled) sb.Append(",{\"type\":\"NeedsCursor\"}");

                if (KeyboardEnabled) sb.Append(",{\"type\":\"NeedsKeyboard\"}");

                sb.Append("]");

                if (!string.IsNullOrEmpty(destroy))
                    sb.Append(",\"destroyUi\":\"").Append(destroy).Append('\"');

                sb.Append('}');
            }

            #endregion Serialization

            private List<string> GetLocalizedText(BasePlayer player)
            {
                var playerLang = Instance?.lang?.GetLanguage(player.UserIDString);
                if (string.IsNullOrWhiteSpace(playerLang))
                    return Text;

                return Text;
            }

            private static string GenerateElementGUID(CuiElementType elementType)
            {
                return $"{elementType}_{CuiHelper.GetGuid().Substring(0, 10)}";
            }

            #endregion Public Methods

            #region Constructors

            public UiElement()
            {
            }

            public UiElement(UiElement other)
            {
                if (other == null) return;

                AnchorMinX = other.AnchorMinX;
                AnchorMinY = other.AnchorMinY;
                AnchorMaxX = other.AnchorMaxX;
                AnchorMaxY = other.AnchorMaxY;
                OffsetMinX = other.OffsetMinX;
                OffsetMinY = other.OffsetMinY;
                OffsetMaxX = other.OffsetMaxX;
                OffsetMaxY = other.OffsetMaxY;
                Enabled = other.Enabled;
                Visible = other.Visible;
                Name = other.Name;
                Type = other.Type;
                Color = other.Color != null ? new IColor(other.Color.Hex, other.Color.Alpha) : null;
                Text = other.Text?.Count > 0 ? new List<string>(other.Text) : new List<string>();
                FontSize = other.FontSize;
                Font = other.Font;
                Align = other.Align;
                TextColor = other.TextColor != null ? new IColor(other.TextColor.Hex, other.TextColor.Alpha) : null;
                Command = other.Command;
                Image = other.Image;
                CursorEnabled = other.CursorEnabled;
                KeyboardEnabled = other.KeyboardEnabled;
                Sprite = other.Sprite;
                Material = other.Material;
            }

            public UiElement Clone()
            {
                return new UiElement(this);
            }

            public static UiElement CreatePanel(
                InterfacePosition position,
                IColor color,
                bool cursorEnabled = false,
                bool keyboardEnabled = false,
                string sprite = "",
                string material = "",
                string randomName = "")
            {
                if (string.IsNullOrWhiteSpace(randomName)) randomName = GenerateElementGUID(CuiElementType.Panel);

                return new UiElement
                {
                    Name = randomName,
                    AnchorMinX = position.AnchorMinX,
                    AnchorMinY = position.AnchorMinY,
                    AnchorMaxX = position.AnchorMaxX,
                    AnchorMaxY = position.AnchorMaxY,
                    OffsetMinX = position.OffsetMinX,
                    OffsetMinY = position.OffsetMinY,
                    OffsetMaxX = position.OffsetMaxX,
                    OffsetMaxY = position.OffsetMaxY,
                    Enabled = true,
                    Visible = true,
                    Type = CuiElementType.Panel,
                    Color = color,
                    Text = new List<string>(),
                    FontSize = 14,
                    Font = CuiElementFont.RobotoCondensedBold,
                    Align = TextAnchor.UpperLeft,
                    TextColor = new IColor("#FFFFFF", 100),
                    Command = string.Empty,
                    Image = string.Empty,
                    CursorEnabled = cursorEnabled,
                    KeyboardEnabled = keyboardEnabled,
                    Sprite = sprite,
                    Material = material
                };
            }

            public static UiElement CreateImage(
                InterfacePosition position,
                string image,
                IColor color = null,
                bool cursorEnabled = false,
                bool keyboardEnabled = false,
                string sprite = "",
                string material = "",
                string randomName = "")
            {
                if (string.IsNullOrWhiteSpace(randomName)) randomName = GenerateElementGUID(CuiElementType.Image);

                color ??= new IColor("#FFFFFF", 100);

                return new UiElement
                {
                    Name = randomName,
                    AnchorMinX = position.AnchorMinX,
                    AnchorMinY = position.AnchorMinY,
                    AnchorMaxX = position.AnchorMaxX,
                    AnchorMaxY = position.AnchorMaxY,
                    OffsetMinX = position.OffsetMinX,
                    OffsetMinY = position.OffsetMinY,
                    OffsetMaxX = position.OffsetMaxX,
                    OffsetMaxY = position.OffsetMaxY,
                    Enabled = true,
                    Visible = true,
                    Type = CuiElementType.Image,
                    Color = color,
                    Text = new List<string>(),
                    FontSize = 14,
                    Font = CuiElementFont.RobotoCondensedBold,
                    Align = TextAnchor.UpperLeft,
                    TextColor = new IColor("#FFFFFF", 100),
                    Command = string.Empty,
                    Image = image,
                    CursorEnabled = cursorEnabled,
                    KeyboardEnabled = keyboardEnabled,
                    Sprite = sprite,
                    Material = material
                };
            }

            public static UiElement CreateLabel(
                InterfacePosition position,
                IColor textColor,
                List<string> text,
                int fontSize = 14,
                string font = "robotocondensed-bold.ttf",
                TextAnchor align = TextAnchor.UpperLeft,
                string randomName = "",
                bool cursorEnabled = false,
                bool keyboardEnabled = false)
            {
                if (string.IsNullOrWhiteSpace(randomName)) randomName = GenerateElementGUID(CuiElementType.Label);

                return new UiElement
                {
                    Name = randomName,
                    AnchorMinX = position.AnchorMinX,
                    AnchorMinY = position.AnchorMinY,
                    AnchorMaxX = position.AnchorMaxX,
                    AnchorMaxY = position.AnchorMaxY,
                    OffsetMinX = position.OffsetMinX,
                    OffsetMinY = position.OffsetMinY,
                    OffsetMaxX = position.OffsetMaxX,
                    OffsetMaxY = position.OffsetMaxY,
                    Enabled = true,
                    Visible = true,
                    Type = CuiElementType.Label,
                    Color = new IColor("#FFFFFF", 100),
                    Text = text,
                    FontSize = fontSize,
                    Font = GetFontTypeByFont(font),
                    Align = align,
                    TextColor = textColor,
                    Command = string.Empty,
                    Image = string.Empty,
                    CursorEnabled = cursorEnabled,
                    KeyboardEnabled = keyboardEnabled
                };
            }

            public static UiElement CreateLabel(
                InterfacePosition position,
                IColor textColor,
                string text,
                int fontSize = 14,
                string font = "robotocondensed-bold.ttf",
                TextAnchor align = TextAnchor.UpperLeft,
                string randomName = "",
                bool cursorEnabled = false,
                bool keyboardEnabled = false)
            {
                if (string.IsNullOrWhiteSpace(randomName)) randomName = GenerateElementGUID(CuiElementType.Label);

                return new UiElement
                {
                    Name = randomName,
                    AnchorMinX = position.AnchorMinX,
                    AnchorMinY = position.AnchorMinY,
                    AnchorMaxX = position.AnchorMaxX,
                    AnchorMaxY = position.AnchorMaxY,
                    OffsetMinX = position.OffsetMinX,
                    OffsetMinY = position.OffsetMinY,
                    OffsetMaxX = position.OffsetMaxX,
                    OffsetMaxY = position.OffsetMaxY,
                    Enabled = true,
                    Visible = true,
                    Type = CuiElementType.Label,
                    Color = new IColor("#FFFFFF", 100),
                    Text = new List<string> {text},
                    FontSize = fontSize,
                    Font = GetFontTypeByFont(font),
                    Align = align,
                    TextColor = textColor,
                    Command = string.Empty,
                    Image = string.Empty,
                    CursorEnabled = cursorEnabled,
                    KeyboardEnabled = keyboardEnabled
                };
            }

            public static UiElement CreateButton(
                InterfacePosition position,
                IColor color,
                IColor textColor,
                string text = "",
                bool cursorEnabled = false,
                bool keyboardEnabled = false,
                string sprite = "",
                string material = "",
                int fontSize = 14,
                string font = "robotocondensed-bold.ttf",
                TextAnchor align = TextAnchor.UpperLeft,
                string command = "",
                string randomName = "")
            {
                if (string.IsNullOrWhiteSpace(randomName)) randomName = GenerateElementGUID(CuiElementType.Button);

                return new UiElement
                {
                    Name = randomName,
                    AnchorMinX = position.AnchorMinX,
                    AnchorMinY = position.AnchorMinY,
                    AnchorMaxX = position.AnchorMaxX,
                    AnchorMaxY = position.AnchorMaxY,
                    OffsetMinX = position.OffsetMinX,
                    OffsetMinY = position.OffsetMinY,
                    OffsetMaxX = position.OffsetMaxX,
                    OffsetMaxY = position.OffsetMaxY,
                    Enabled = true,
                    Visible = true,
                    Type = CuiElementType.Button,
                    Color = color,
                    Text = new List<string> {text},
                    FontSize = fontSize,
                    Font = GetFontTypeByFont(font),
                    Align = align,
                    TextColor = textColor,
                    Command = command ?? string.Empty,
                    Image = string.Empty,
                    CursorEnabled = cursorEnabled,
                    KeyboardEnabled = keyboardEnabled,
                    Sprite = sprite,
                    Material = material
                };
            }

            public static UiElement CreateInputField(
                InterfacePosition position,
                IColor textColor,
                string text,
                int fontSize = 14,
                string font = "robotocondensed-bold.ttf",
                TextAnchor align = TextAnchor.UpperLeft,
                string randomName = "",
                bool cursorEnabled = false,
                bool keyboardEnabled = false)
            {
                if (string.IsNullOrWhiteSpace(randomName)) randomName = GenerateElementGUID(CuiElementType.InputField);

                return new UiElement
                {
                    Name = randomName,
                    AnchorMinX = position.AnchorMinX,
                    AnchorMinY = position.AnchorMinY,
                    AnchorMaxX = position.AnchorMaxX,
                    AnchorMaxY = position.AnchorMaxY,
                    OffsetMinX = position.OffsetMinX,
                    OffsetMinY = position.OffsetMinY,
                    OffsetMaxX = position.OffsetMaxX,
                    OffsetMaxY = position.OffsetMaxY,
                    Enabled = true,
                    Visible = true,
                    Type = CuiElementType.InputField,
                    Color = new IColor("#FFFFFF", 100),
                    Text = new List<string> {text},
                    FontSize = fontSize,
                    Font = GetFontTypeByFont(font),
                    Align = align,
                    TextColor = textColor,
                    Command = string.Empty,
                    Image = string.Empty,
                    CursorEnabled = cursorEnabled,
                    KeyboardEnabled = keyboardEnabled
                };
            }

            #endregion Constructors
        }

        #endregion UI.Classes

        #region UI Settings

        public class ScrollViewUI : InterfacePosition
        {
            #region Fields

            [JsonProperty(PropertyName = "Scroll Type")] [JsonConverter(typeof(StringEnumConverter))]
            public ScrollType ScrollType;

            [JsonProperty(PropertyName = "Movement Type")] [JsonConverter(typeof(StringEnumConverter))]
            public ScrollRect.MovementType MovementType;

            [JsonProperty(PropertyName = "Elasticity")]
            public float Elasticity;

            [JsonProperty(PropertyName = "Deceleration Rate")]
            public float DecelerationRate;

            [JsonProperty(PropertyName = "Scroll Sensitivity")]
            public float ScrollSensitivity;

            [JsonProperty(PropertyName = "Minimal Height")]
            public float MinHeight;

            [JsonProperty(PropertyName = "Additional Height")]
            public float AdditionalHeight;

            [JsonProperty(PropertyName = "Scrollbar Settings")]
            public ScrollBarSettings Scrollbar = new();

            #endregion

            #region Public Methods

            public CuiScrollViewComponent GetScrollView(float totalWidth)
            {
                return GetScrollView(CalculateContentRectTransform(totalWidth));
            }

            public CuiScrollViewComponent GetScrollView(CuiRectTransform contentTransform)
            {
                var cuiScrollView = new CuiScrollViewComponent
                {
                    MovementType = MovementType,
                    Elasticity = Elasticity,
                    DecelerationRate = DecelerationRate,
                    ScrollSensitivity = ScrollSensitivity,
                    ContentTransform = contentTransform,
                    Inertia = true
                };

                switch (ScrollType)
                {
                    case ScrollType.Vertical:
                    {
                        cuiScrollView.Vertical = true;
                        cuiScrollView.Horizontal = false;

                        cuiScrollView.VerticalScrollbar = Scrollbar.Get();
                        break;
                    }

                    case ScrollType.Horizontal:
                    {
                        cuiScrollView.Horizontal = true;
                        cuiScrollView.Vertical = false;

                        cuiScrollView.HorizontalScrollbar = Scrollbar.Get();
                        break;
                    }
                }

                return cuiScrollView;
            }

            public CuiRectTransform CalculateContentRectTransform(float totalWidth)
            {
                CuiRectTransform contentRect;
                if (ScrollType == ScrollType.Horizontal)
                    contentRect = new CuiRectTransform
                    {
                        AnchorMin = "0 0", AnchorMax = "0 1",
                        OffsetMin = "0 0",
                        OffsetMax = $"{totalWidth} 0"
                    };
                else
                    contentRect = new CuiRectTransform
                    {
                        AnchorMin = "0 1", AnchorMax = "1 1",
                        OffsetMin = $"0 -{totalWidth}",
                        OffsetMax = "0 0"
                    };

                return contentRect;
            }

            #endregion

            #region Classes

            public class ScrollBarSettings
            {
                #region Fields

                [JsonProperty(PropertyName = "Invert")]
                public bool Invert;

                [JsonProperty(PropertyName = "Auto Hide")]
                public bool AutoHide;

                [JsonProperty(PropertyName = "Handle Sprite")]
                public string HandleSprite = string.Empty;

                [JsonProperty(PropertyName = "Size")] public float Size;

                [JsonProperty(PropertyName = "Handle Color")]
                public IColor HandleColor = IColor.CreateWhite();

                [JsonProperty(PropertyName = "Highlight Color")]
                public IColor HighlightColor = IColor.CreateWhite();

                [JsonProperty(PropertyName = "Pressed Color")]
                public IColor PressedColor = IColor.CreateWhite();

                [JsonProperty(PropertyName = "Track Sprite")]
                public string TrackSprite = string.Empty;

                [JsonProperty(PropertyName = "Track Color")]
                public IColor TrackColor = IColor.CreateWhite();

                #endregion

                #region Public Methods

                public CuiScrollbar Get()
                {
                    var cuiScrollbar = new CuiScrollbar
                    {
                        Size = Size
                    };

                    if (Invert) cuiScrollbar.Invert = Invert;
                    if (AutoHide) cuiScrollbar.AutoHide = AutoHide;
                    if (!string.IsNullOrEmpty(HandleSprite)) cuiScrollbar.HandleSprite = HandleSprite;
                    if (!string.IsNullOrEmpty(TrackSprite)) cuiScrollbar.TrackSprite = TrackSprite;

                    if (HandleColor != null) cuiScrollbar.HandleColor = HandleColor.Get();
                    if (HighlightColor != null) cuiScrollbar.HighlightColor = HighlightColor.Get();
                    if (PressedColor != null) cuiScrollbar.PressedColor = PressedColor.Get();
                    if (TrackColor != null) cuiScrollbar.TrackColor = TrackColor.Get();

                    return cuiScrollbar;
                }

                #endregion
            }

            #endregion
        }

        public class ImageSettings : PositionSettings
        {
            #region Fields

            [JsonProperty(PropertyName = "Sprite")]
            public string Sprite = string.Empty;

            [JsonProperty(PropertyName = "Material")]
            public string Material = string.Empty;

            [JsonProperty(PropertyName = "Image")] public string Image = string.Empty;

            [JsonProperty(PropertyName = "Color")] public IColor Color = IColor.CreateTransparent();

            [JsonProperty(PropertyName = "Cursor Enabled")]
            public bool CursorEnabled = false;

            [JsonProperty(PropertyName = "Keyboard Enabled")]
            public bool KeyboardEnabled = false;

            #endregion

            #region Private Methods

            public ICuiComponent GetImageComponent()
            {
                if (!string.IsNullOrEmpty(Image))
                {
                    var rawImage = new CuiRawImageComponent
                    {
                        Png = Instance.GetImage(Image),
                        Color = Color.Get()
                    };

                    if (!string.IsNullOrEmpty(Sprite))
                        rawImage.Sprite = Sprite;

                    if (!string.IsNullOrEmpty(Material))
                        rawImage.Material = Material;

                    return rawImage;
                }

                var image = new CuiImageComponent
                {
                    Color = Color.Get()
                };

                if (!string.IsNullOrEmpty(Sprite))
                    image.Sprite = Sprite;

                if (!string.IsNullOrEmpty(Material))
                    image.Material = Material;

                return image;
            }

            #endregion

            #region Public Methods

            public bool TryGetImageURL(out string url)
            {
                if (!string.IsNullOrWhiteSpace(Image) && Image.IsURL())
                {
                    url = Image;
                    return true;
                }

                url = null;
                return false;
            }

            public CuiElement GetImage(string parent,
                string name = null,
                string destroyUI = null)
            {
                if (string.IsNullOrEmpty(name))
                    name = CuiHelper.GetGuid();

                var element = new CuiElement
                {
                    Name = name,
                    Parent = parent,
                    DestroyUi = destroyUI,
                    Components =
                    {
                        GetImageComponent(),
                        GetRectTransform()
                    }
                };

                if (CursorEnabled)
                    element.Components.Add(new CuiNeedsCursorComponent());

                if (KeyboardEnabled)
                    element.Components.Add(new CuiNeedsKeyboardComponent());

                return element;
            }

            #endregion

            #region Constructors

            public ImageSettings()
            {
            }

            public ImageSettings(string imageURL, IColor color, PositionSettings position) : base(position)
            {
                Image = imageURL;
                Color = color;
            }

            #endregion
        }

        public class IconButtonUI
        {
            #region Fields

            [JsonProperty(PropertyName = "Background")]
            public UiElement Background;

            [JsonProperty(PropertyName = "Title")] public UiElement Title;

            [JsonProperty(PropertyName = "Use Icon?")]
            public bool UseIcon;

            [JsonProperty(PropertyName = "Icon")] public UiElement Icon;

            #endregion

            #region Public Methods

            public void GetButton(BasePlayer player, ref List<string> allElements,
                string parent,
                string name = "",
                string titleText = "", string command = "", string close = "")
            {
                if (string.IsNullOrEmpty(name)) name = CuiHelper.GetGuid();

                allElements.Add(Background?.GetSerialized(player, parent, name, name));
                allElements.Add(Title?.GetSerialized(player, name, name + ".Title",
                    textFormatter: t => !string.IsNullOrEmpty(titleText) ? titleText : t));

                if (UseIcon && Icon != null)
                    allElements.Add(Icon?.GetSerialized(player, name, name + ".Icon"));

                allElements.Add(CuiJsonFactory.CreateButton(
                    parent: name, name: name + ".Button", destroy: name + ".Button", close: close,
                    command: command));
            }

            #endregion
        }

        public class ButtonSettings : TextSettings
        {
            #region Fields

            [JsonProperty(PropertyName = "Button Color")]
            public IColor ButtonColor = IColor.CreateWhite();

            [JsonProperty(PropertyName = "Sprite")]
            public string Sprite = string.Empty;

            [JsonProperty(PropertyName = "Material")]
            public string Material = string.Empty;

            [JsonProperty(PropertyName = "Image")] public string Image = string.Empty;

            [JsonProperty(PropertyName = "Image Color")]
            public IColor ImageColor = IColor.CreateWhite();

            [JsonProperty(PropertyName = "Use custom image position settings?")]
            public bool UseCustomPositionImage = false;

            [JsonProperty(PropertyName = "Custom image position settings")]
            public PositionSettings ImagePosition = CreateFullStretch();

            #endregion

            #region Public Methods

            public bool TryGetImageURL(out string url)
            {
                if (!string.IsNullOrWhiteSpace(Image) && Image.IsURL())
                {
                    url = Image;
                    return true;
                }

                url = null;
                return false;
            }

            public List<CuiElement> GetButton(
                string msg,
                string cmd,
                string parent,
                string buttonName = null,
                string destroyUI = null,
                string close = null)
            {
                if (string.IsNullOrEmpty(buttonName))
                    buttonName = CuiHelper.GetGuid();

                var elements = new List<CuiElement>();

                var btn = new CuiButtonComponent
                {
                    Color = ButtonColor.Get()
                };

                if (!string.IsNullOrEmpty(cmd))
                    btn.Command = cmd;

                if (!string.IsNullOrEmpty(close))
                    btn.Close = close;

                if (!string.IsNullOrEmpty(Sprite))
                    btn.Sprite = Sprite;

                if (!string.IsNullOrEmpty(Material))
                    btn.Material = Material;

                elements.Add(new CuiElement
                {
                    Name = buttonName,
                    Parent = parent,
                    DestroyUi = destroyUI,
                    Components =
                    {
                        btn,
                        GetRectTransform()
                    }
                });

                if (!string.IsNullOrEmpty(Image))
                {
                    elements.Add(new CuiElement
                    {
                        Parent = buttonName,
                        Components =
                        {
                            Image.StartsWith("assets/")
                                ? new CuiImageComponent {Color = ImageColor.Get(), Sprite = Image}
                                : new CuiRawImageComponent {Color = ImageColor.Get(), Png = Instance.GetImage(Image)},

                            UseCustomPositionImage && ImagePosition != null
                                ? ImagePosition?.GetRectTransform()
                                : new CuiRectTransformComponent()
                        }
                    });
                }
                else
                {
                    if (!string.IsNullOrEmpty(msg))
                        elements.Add(new CuiElement
                        {
                            Parent = buttonName,
                            Components =
                            {
                                GetTextComponent(msg),
                                new CuiRectTransformComponent()
                            }
                        });
                }

                return elements;
            }

            #endregion
        }

        public class TextSettings : PositionSettings
        {
            #region Fields

            [JsonProperty(PropertyName = "Font Size")]
            public int FontSize = 12;

            [JsonProperty(PropertyName = "Is Bold?")]
            public bool IsBold = false;

            [JsonProperty(PropertyName = "Align")] [JsonConverter(typeof(StringEnumConverter))]
            public TextAnchor Align = TextAnchor.UpperLeft;

            [JsonProperty(PropertyName = "Color")] public IColor Color = IColor.CreateWhite();

            #endregion Fields

            #region Public Methods

            public CuiTextComponent GetTextComponent(string msg)
            {
                return new CuiTextComponent
                {
                    Text = msg ?? string.Empty,
                    FontSize = FontSize,
                    Font = IsBold ? "robotocondensed-bold.ttf" : "robotocondensed-regular.ttf",
                    Align = Align,
                    Color = Color.Get()
                };
            }

            public CuiElement GetText(string msg,
                string parent,
                string name = null,
                string destroyUI = null,
                params ICuiComponent[] components)
            {
                if (string.IsNullOrEmpty(name))
                    name = CuiHelper.GetGuid();

                var element = new CuiElement
                {
                    Name = name,
                    Parent = parent,
                    DestroyUi = destroyUI,
                    Components =
                    {
                        GetTextComponent(msg),
                        GetRectTransform()
                    }
                };

                if (components.Length > 0)
                    element.Components.AddRange(components);

                return element;
            }

            #endregion
        }

        public class PositionSettings
        {
            #region Fields

            [JsonProperty(PropertyName = "AnchorMin")]
            public string AnchorMin = "0 0";

            [JsonProperty(PropertyName = "AnchorMax")]
            public string AnchorMax = "1 1";

            [JsonProperty(PropertyName = "OffsetMin")]
            public string OffsetMin = "0 0";

            [JsonProperty(PropertyName = "OffsetMax")]
            public string OffsetMax = "0 0";

            #endregion

            #region Cache

            [JsonIgnore] private CuiRectTransformComponent _position;

            #endregion

            #region Public Methods

            public CuiRectTransformComponent GetRectTransform()
            {
                if (_position != null) return _position;

                var rect = new CuiRectTransformComponent();

                if (!string.IsNullOrEmpty(AnchorMin))
                    rect.AnchorMin = AnchorMin;

                if (!string.IsNullOrEmpty(AnchorMax))
                    rect.AnchorMax = AnchorMax;

                if (!string.IsNullOrEmpty(OffsetMin))
                    rect.OffsetMin = OffsetMin;

                if (!string.IsNullOrEmpty(OffsetMax))
                    rect.OffsetMax = OffsetMax;

                _position = rect;

                return _position;
            }

            #endregion

            #region Constructors

            public PositionSettings()
            {
            }

            public PositionSettings(PositionSettings other)
            {
                AnchorMin = other.AnchorMin;
                AnchorMax = other.AnchorMin;
                OffsetMin = other.AnchorMin;
                OffsetMax = other.AnchorMin;
            }

            public static PositionSettings CreatePosition(float aMinX, float aMinY, float aMaxX, float aMaxY,
                float oMinX, float oMinY, float oMaxX, float oMaxY)
            {
                return new PositionSettings
                {
                    AnchorMin = $"{aMinX} {aMinY}",
                    AnchorMax = $"{aMaxX} {aMaxY}",
                    OffsetMin = $"{oMinX} {oMinY}",
                    OffsetMax = $"{oMaxX} {oMaxY}"
                };
            }

            public static PositionSettings CreatePosition(
                string anchorMin = "0 0",
                string anchorMax = "1 1",
                string offsetMin = "0 0",
                string offsetMax = "0 0")
            {
                return new PositionSettings
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax,
                    OffsetMin = offsetMin,
                    OffsetMax = offsetMax
                };
            }

            public static PositionSettings CreatePosition(CuiRectTransform rectTransform)
            {
                return new PositionSettings
                {
                    AnchorMin = rectTransform.AnchorMin,
                    AnchorMax = rectTransform.AnchorMax,
                    OffsetMin = rectTransform.OffsetMin,
                    OffsetMax = rectTransform.OffsetMax
                };
            }

            public static PositionSettings CreateFullStretch()
            {
                return new PositionSettings
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1",
                    OffsetMin = "0 0",
                    OffsetMax = "0 0"
                };
            }

            public static PositionSettings CreateCenter()
            {
                return new PositionSettings
                {
                    AnchorMin = "0.5 0.5",
                    AnchorMax = "0.5 0.5",
                    OffsetMin = "0 0",
                    OffsetMax = "0 0"
                };
            }

            #endregion Constructors
        }

        public class CheckBoxSettings
        {
            [JsonProperty(PropertyName = "Background")]
            public UiElement Background;

            [JsonProperty(PropertyName = "Checkbox")]
            public UiElement CheckboxButton;

            [JsonProperty(PropertyName = "Checkbox Size")]
            public float CheckboxSize;

            [JsonProperty(PropertyName = "Checkbox Color")]
            public IColor CheckboxColor;

            [JsonProperty(PropertyName = "Title")] public UiElement Title;
        }

        public class IColor
        {
            #region Fields

            [JsonProperty(PropertyName = "HEX", NullValueHandling = NullValueHandling.Include)]
            public string Hex;

            [JsonProperty(PropertyName = "Opacity (0 - 100)", NullValueHandling = NullValueHandling.Include)]
            public float Alpha;

            #endregion

            #region Public Methods

            [JsonIgnore] private string _cachedColorString;

            public string Get()
            {
                if (_cachedColorString != null)
                    return _cachedColorString;

                _cachedColorString = GetNotCachedColor();
                return _cachedColorString;
            }

            public string GetNotCachedColor()
            {
                if (string.IsNullOrEmpty(Hex)) Hex = "#FFFFFF";

                var hexValue = Hex.Trim('#');
                if (hexValue.Length != 6)
                    throw new ArgumentException(
                        $"Invalid HEX color format. Must be 6 characters (e.g., #RRGGBB). Hex: {Hex}", nameof(Hex));

                var r = byte.Parse(hexValue.Substring(0, 2), NumberStyles.HexNumber);
                var g = byte.Parse(hexValue.Substring(2, 2), NumberStyles.HexNumber);
                var b = byte.Parse(hexValue.Substring(4, 2), NumberStyles.HexNumber);

                return
                    $"{Math.Round((double) r / 255, 3)} {Math.Round((double) g / 255, 3)} {Math.Round((double) b / 255, 3)} {Math.Round(Alpha / 100, 3)}";
            }

            public void LoadColor()
            {
                _cachedColorString = GetNotCachedColor();
            }

            public void InvalidateCache()
            {
                _cachedColorString = null;
            }

            #endregion

            #region Constructors

            public IColor(string hex, float alpha)
            {
                Hex = hex;
                Alpha = alpha;
            }

            public static IColor Create(string hex, float alpha = 100)
            {
                return new IColor(hex, alpha);
            }

            public static IColor CreateTransparent()
            {
                return new IColor("#000000", 0);
            }

            public static IColor CreateWhite()
            {
                return new IColor("#FFFFFF", 100);
            }

            public static IColor CreateBlack()
            {
                return new IColor("#000000", 100);
            }

            #endregion
        }

        #endregion

        private class CustomVendingEntry
        {
            #region Fields

            [JsonProperty(PropertyName = LangRu ? "Разрешение" : "Permissions")]
            public string Permission;

            [JsonProperty(PropertyName = LangRu ? "Категории (Названия) [* - все]" : "Categories (Titles) [* - all]",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Categories;

            #endregion
        }

        private class WipeSettings
        {
            #region Fields

            [JsonProperty(PropertyName = LangRu ? "Сброс задержек?" : "Wipe Cooldowns?")]
            public bool Cooldown;

            [JsonProperty(PropertyName = LangRu ? "Сброс игроков?" : "Wipe Players?")]
            public bool Players;

            [JsonProperty(PropertyName = LangRu ? "Сброс лимитов" : "Wipe Limits?")]
            public bool Limits;

            #endregion
        }

        private class UserInterface
        {
            #region Fields

            [JsonProperty(PropertyName = LangRu ? "Тип отображения (Overlay/Hud)" : "Display type (Overlay/Hud)")]
            public string DisplayType;

            [JsonProperty(PropertyName =
                LangRu
                    ? "Количество цифр после десятичной точки для округления цен"
                    : "Number of digits after decimal point for rounding prices")]
            public int RoundDigits;

            [JsonProperty(PropertyName = LangRu ? "Цвет 1" : "Color 1")]
            public IColor Color1;

            [JsonProperty(PropertyName = LangRu ? "Цвет 2" : "Color 2")]
            public IColor Color2;

            [JsonProperty(PropertyName = LangRu ? "Цвет 3" : "Color 3")]
            public IColor Color3;

            [JsonProperty(PropertyName = LangRu ? "Цвет 5" : "Color 5")]
            public IColor Color5;

            [JsonProperty(PropertyName = LangRu ? "Цвет 7" : "Color 7")]
            public IColor Color7;

            [JsonProperty(PropertyName = LangRu ? "Цвет 8" : "Color 8")]
            public IColor Color8;

            [JsonProperty(PropertyName = LangRu ? "Цвет 9" : "Color 9")]
            public IColor Color9;

            [JsonProperty(PropertyName = LangRu ? "Цвет 10" : "Color 10")]
            public IColor Color10;

            [JsonProperty(PropertyName = LangRu ? "Цвет 11" : "Color 11")]
            public IColor Color11;

            [JsonProperty(PropertyName = LangRu ? "Цвет 12" : "Color 12")]
            public IColor Color12;

            [JsonProperty(PropertyName = LangRu ? "Цвет 13" : "Color 13")]
            public IColor Color13;

            #endregion
        }

        public enum ScrollType
        {
            Horizontal,
            Vertical
        }

        #region Font

        public enum CuiElementFont
        {
            RobotoCondensedBold,
            RobotoCondensedRegular,
            DroidSansMono,
            PermanentMarker
        }

        private static string GetFontByType(CuiElementFont fontType)
        {
            switch (fontType)
            {
                case CuiElementFont.RobotoCondensedBold:
                    return "robotocondensed-bold.ttf";
                case CuiElementFont.RobotoCondensedRegular:
                    return "robotocondensed-regular.ttf";
                case CuiElementFont.DroidSansMono:
                    return "droidsansmono.ttf";
                case CuiElementFont.PermanentMarker:
                    return "permanentmarker.ttf";
                default:
                    throw new ArgumentOutOfRangeException(nameof(fontType), fontType, null);
            }
        }

        private static CuiElementFont GetFontTypeByFont(string font)
        {
            switch (font)
            {
                case "robotocondensed-bold.ttf":
                    return CuiElementFont.RobotoCondensedBold;
                case "robotocondensed-regular.ttf":
                    return CuiElementFont.RobotoCondensedRegular;
                case "droidsansmono.ttf":
                    return CuiElementFont.DroidSansMono;
                case "permanentmarker.ttf":
                    return CuiElementFont.PermanentMarker;
                default:
                    throw new ArgumentOutOfRangeException(nameof(font), font, null);
            }
        }

        #endregion

        private class NPCShop
        {
            #region Fields

            [JsonProperty(PropertyName = LangRu ? "Разрешение" : "Permission")]
            public string Permission;

            [JsonProperty(PropertyName = LangRu ? "Категории [* - все]" : "Categories (Titles) [* - all]",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Categories;

            #endregion

            #region Cache

            [JsonIgnore] public string BotID;

            #endregion
        }

        private class ShopCategory
        {
            #region Fields

            [JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
            public bool Enabled;

            [JsonProperty(PropertyName = LangRu ? "Тип категории" : "Category Type")]
            [JsonConverter(typeof(StringEnumConverter))]
            public Type CategoryType = Type.None;

            [JsonProperty(PropertyName = LangRu ? "Название" : "Title")]
            public string Title;

            [JsonProperty(PropertyName = LangRu ? "Разрешение" : "Permission")]
            public string Permission;

            [JsonProperty(PropertyName = LangRu ? "Тип сортировки" : "Sort Type")]
            [JsonConverter(typeof(StringEnumConverter))]
            public Configuration.SortType SortType = Configuration.SortType.None;

            [JsonProperty(PropertyName = LangRu ? "Предметы" : "Items",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ShopItem> Items = new();

            [JsonProperty(PropertyName = LangRu ? "Локализация" : "Localization")]
            public Localization Localization = new();

            #endregion

            #region Cache

            [JsonIgnore] private int _id = -1;

            [JsonIgnore]
            public int ID
            {
                get
                {
                    if (_id == -1)
                        _id = Random.Range(0, int.MaxValue);

                    return _id;
                }
            }

            [JsonIgnore]
            public List<ShopItem> GetItems
            {
                get
                {
                    switch (SortType)
                    {
                        case Configuration.SortType.None:
                            return Items;

                        default:
                            SortItems(ref Items);
                            return Items;
                    }
                }
            }

            #endregion

            #region Helpers

            #region Moving

            #region Categories

            public void MoveCategoryUp()
            {
                var index = _itemsData.Shop.IndexOf(this);
                if (index > 0 && index < _itemsData.Shop.Count)
                    (_itemsData.Shop[index], _itemsData.Shop[index - 1]) =
                        (_itemsData.Shop[index - 1], _itemsData.Shop[index]); // Swap
            }

            public void MoveCategoryDown()
            {
                var index = _itemsData.Shop.IndexOf(this);
                if (index >= 0 && index < _itemsData.Shop.Count - 1)
                    (_itemsData.Shop[index], _itemsData.Shop[index + 1]) =
                        (_itemsData.Shop[index + 1], _itemsData.Shop[index]);
            }

            #endregion

            #region Items

            public void MoveItemRight(ShopItem item)
            {
                var index = Items.IndexOf(item);
                if (index >= 0 && index < Items.Count - 1)
                    (Items[index], Items[index + 1]) = (Items[index + 1], Items[index]); // Swap
            }

            public void MoveItemLeft(ShopItem item)
            {
                var index = Items.IndexOf(item);
                if (index > 0 && index < Items.Count)
                    (Items[index], Items[index - 1]) = (Items[index - 1], Items[index]); // Swap
            }

            #endregion

            #endregion

            public int GetShopItemsCount(BasePlayer player)
            {
                var list = GetShopItems(player);
                try
                {
                    return list.Count;
                }
                finally
                {
                    Pool.FreeUnmanaged(ref list);
                }
            }

            public List<ShopItem> GetShopItems(BasePlayer player, int skip = 0, int take = -1)
            {
                switch (CategoryType)
                {
                    case Type.Favorite:
                    {
                        if (Instance?.GetPlayerCart(player.userID) is not PlayerCartData playerCart)
                            return Pool.Get<List<ShopItem>>();

                        var items = playerCart.GetFavoriteItems(player.userID, skip, take);

                        if (SortType != Configuration.SortType.None)
                            SortItems(ref items);

                        return items;
                    }

                    default:
                    {
                        var items = Pool.Get<List<ShopItem>>();
                        var index = 0;
                        var taken = 0;

                        foreach (var item in Items)
                        {
                            if (!item.IsAvailableForPlayer(player.userID))
                                continue;

                            if (index++ < skip)
                                continue;

                            items.Add(item);
                            taken++;

                            if (take > 0 && taken >= take)
                                break;
                        }

                        if (SortType != Configuration.SortType.None)
                            SortItems(ref items);

                        return items;
                    }
                }
            }

            public string GetTitle(BasePlayer player)
            {
                if (Localization is {Enabled: true})
                    return Localization.GetMessage(player);

                return Title;
            }

            public void SortItems(ref List<ShopItem> sortedItems)
            {
                switch (SortType)
                {
                    case Configuration.SortType.Name:
                        sortedItems.Sort((x, y) =>
                            string.Compare(x.PublicTitle ?? string.Empty, y.PublicTitle ?? string.Empty,
                                StringComparison.Ordinal));
                        break;
                    case Configuration.SortType.Amount:
                        sortedItems.Sort((x, y) => x.Amount.CompareTo(y.Amount));
                        break;
                    case Configuration.SortType.PriceIncrease:
                        sortedItems.Sort((x, y) => x.Price.CompareTo(y.Price));
                        break;
                    case Configuration.SortType.PriceDecrease:
                        sortedItems.Sort((x, y) => y.Price.CompareTo(x.Price));
                        break;
                }
            }

            public void LoadIDs(bool sort = false)
            {
                if (sort)
                    Items.ForEach(item => Instance._shopItems.Remove(item.ID));

                GetItems.ForEach(item =>
                {
                    var id = item.ID;
                    if (Instance._shopItems.ContainsKey(item.ID))
                        id = Instance.GetId();
                    Instance._shopItems.Add(id, item);

                    if (item.Discount != null)
                        foreach (var check in item.Discount)
                            if (!string.IsNullOrEmpty(check.Key) && !Instance.permission.PermissionExists(check.Key))
                                Instance.permission.RegisterPermission(check.Key, Instance);

                    if (item.BuyCooldowns != null)
                        foreach (var check in item.BuyCooldowns)
                            if (!string.IsNullOrEmpty(check.Key) && !Instance.permission.PermissionExists(check.Key))
                                Instance.permission.RegisterPermission(check.Key, Instance);

                    if (item.SellCooldowns != null)
                        foreach (var check in item.SellCooldowns)
                            if (!string.IsNullOrEmpty(check.Key) && !Instance.permission.PermissionExists(check.Key))
                                Instance.permission.RegisterPermission(check.Key, Instance);

                    if (item.Genes is {Enabled: true})
                        if (!item.Genes.TryInit())
                            Instance?.PrintError(
                                $"Can't load the item with the ID {item.ID}. The number of genes in the item is incorrect ({item.Genes.GeneTypes.Count} genes, but it should be 6).");
                });
            }

            public ShopCategory Clone()
            {
                return JsonConvert.DeserializeObject<ShopCategory>(JsonConvert.SerializeObject(this));
            }

            #endregion

            #region Classes

            public enum Type
            {
                None,
                Favorite,
                Hided
            }

            #endregion
        }

        public enum ItemType
        {
            Item,
            Command,
            Plugin,
            Kit
        }

        [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
        private sealed class ItemNeedsPreviewAttribute : Attribute
        {
        }

        [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
        private sealed class ShortNameAttribute : Attribute
        {
        }

        public class ShopItem
        {
            #region Fields

            [JsonProperty(PropertyName = LangRu ? "Тип" : "Type")] [JsonConverter(typeof(StringEnumConverter))]
            public ItemType Type;

            [JsonProperty(PropertyName = "ID")] public int ID;

            [JsonProperty(PropertyName = LangRu ? "Изображение" : "Image")] [ItemNeedsPreview]
            public string Image;

            [JsonProperty(PropertyName = LangRu ? "Разрешение" : "Permission")]
            public string Permission = string.Empty;

            [JsonProperty(PropertyName = LangRu ? "Название" : "Title")]
            public string Title;

            [JsonProperty(PropertyName = LangRu ? "Описание" : "Description")]
            public string Description = string.Empty;

            [JsonProperty(PropertyName = LangRu ? "Команда (%steamid%)" : "Command (%steamid%)")]
            public string Command;

            [JsonProperty(PropertyName = "Kit")] public string Kit = string.Empty;

            [JsonProperty(PropertyName = LangRu ? "Плагин" : "Plugin")]
            public PluginItem Plugin;

            [JsonProperty(PropertyName =
                LangRu ? "DisplayName (пусто - по умолчанию)" : "DisplayName (empty - default)")]
            public string DisplayName;

            [JsonProperty(PropertyName = "ShortName")] [ShortName]
            public string ShortName;

            [JsonProperty(PropertyName = LangRu ? "Скин" : "Skin")]
            public ulong Skin;

            [JsonProperty(PropertyName = LangRu ? "Это чертёж" : "Is Blueprint")]
            public bool Blueprint;

            [JsonProperty(PropertyName = LangRu ? "Количество" : "Amount")]
            public int Amount;

            [JsonProperty(PropertyName = LangRu ? "Включить возможность покупать предмет?" : "Enable item buying?")]
            public bool CanBuy = true;

            [JsonProperty(PropertyName = LangRu ? "Цена" : "Price")]
            public double Price;

            [JsonProperty(PropertyName = LangRu ? "Включить возможность продавать предмет?" : "Enable item selling?")]
            public bool CanSell = true;

            [JsonProperty(PropertyName = LangRu ? "Цена продажи" : "Sell Price")]
            public double SellPrice;

            [JsonProperty(PropertyName = LangRu ? "Задержка покупки (0 - отключить)" : "Buy Cooldown (0 - disable)")]
            public float BuyCooldown;

            [JsonProperty(PropertyName = LangRu ? "Задержки покупки (0 - отключить)" : "Buy Cooldowns (0 - no limit)",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, float> BuyCooldowns = new();

            [JsonProperty(PropertyName = LangRu ? "Задержка продажи (0 - отключить)" : "Sell Cooldown (0 - disable)")]
            public float SellCooldown;

            [JsonProperty(PropertyName = LangRu ? "Задержки продажи (0 - no limit)" : "Sell Cooldowns (0 - no limit)",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, float> SellCooldowns = new();

            [JsonProperty(PropertyName = LangRu ? "Использовать пользовательскую скидку?" : "Use custom discount?")]
            public bool UseCustomDiscount;

            [JsonProperty(PropertyName = LangRu ? "Скидка (%)" : "Discount (%)",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, int> Discount = new();

            [JsonProperty(PropertyName = LangRu ? "Лимит продаж (0 - без лимита)" : "Sell Limits (0 - no limit)",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, int> SellLimits = new();

            [JsonProperty(PropertyName = LangRu ? "Лимит покупок (0 - без лимита)" : "Buy Limits (0 - no limit)",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, int> BuyLimits = new();

            [JsonProperty(
                PropertyName = LangRu ? "Дневной лимит покупок (0 - без лимита)" : "Daily Buy Limits (0 - no limit)",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, int> DailyBuyLimits = new();

            [JsonProperty(
                PropertyName = LangRu ? "Дневной лимит продаж (0 - без лимита)" : "Daily Sell Limits (0 - no limit)",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, int> DailySellLimits = new();

            [JsonProperty(PropertyName =
                LangRu ? "Максимальное покупаемое количество (0 - отключить)" : "Max Buy Amount (0 - disable)")]
            public int BuyMaxAmount;

            [JsonProperty(PropertyName =
                LangRu ? "Максимальное продаваемое количество (0 - отключить)" : "Max Sell Amount (0 - disable)")]
            public int SellMaxAmount;

            [JsonProperty(PropertyName = LangRu ? "Быстрая Покупка" : "Force Buy")]
            public bool ForceBuy;

            [JsonProperty(PropertyName =
                LangRu ? "Запрещать разделение предмета на стаки?" : "Prohibit splitting item into stacks?")]
            public bool ProhibitSplit;

            [JsonProperty(PropertyName =
                LangRu ? "Продолжительность блокировки покупки после вайпа" : "Purchase block duration after wipe")]
            public int BuyBlockDurationAfterWipe;

            [JsonProperty(PropertyName =
                LangRu ? "Продолжительность блокировки продажи после вайпа" : "Sale block duration after wipe")]
            public int SellBlockDurationAfterWipe;

            [JsonProperty(PropertyName = LangRu ? "Локализация" : "Localization")]
            public Localization Localization = new();

            [JsonProperty(PropertyName = LangRu ? "Содержимое" : "Content")]
            public ItemContent Content = new();

            [JsonProperty(PropertyName = LangRu ? "Оружие" : "Weapon")]
            public ItemWeapon Weapon = new();

            [JsonProperty(PropertyName = LangRu ? "Гены" : "Genes")]
            public ItemGenes Genes = new();

            [JsonProperty(PropertyName = LangRu ? "Валюты" : "Currencies")]
            public ItemCurrency Currencies = new();

            #endregion

            #region Cache

            [JsonIgnore] public int itemId => ItemManager.FindItemDefinition(ShortName ?? string.Empty)?.itemid ?? -1;

            [JsonIgnore] private string _publicTitle;

            [JsonIgnore]
            public string PublicTitle
            {
                get
                {
                    if (string.IsNullOrEmpty(_publicTitle))
                        _publicTitle = GetName();

                    return _publicTitle;
                }
            }

            public (bool raw, string steamId, string png, string sprite, string imageColor, int? itemId, ulong? skinID)
                GetImage()
            {
                if (!string.IsNullOrEmpty(Image))
                {
                    if (Image?.StartsWith("assets/") == true)
                        return (true, null, null, Image, null, null, null);
                    else
                        return (true, null, Instance.GetImage(Image), null, null, null, null);
                }

                if (!string.IsNullOrEmpty(ShortName))
                {
                    if (Skin != 0)
                        return (false, null, null, null, null, ItemManager.blueprintBaseDef.itemid, Skin);

                    var id = itemId;
                    if (id != -1)
                        return (false, null, null, null, null, itemId, Skin);
                }

                return (false, null, null, null, null, null, null);
            }

            public string GetPublicTitle(BasePlayer player)
            {
                if (Localization is {Enabled: true})
                {
                    var msg = Localization.GetMessage(player);
                    if (!string.IsNullOrEmpty(msg))
                        return msg;
                }

                return GetName(player.UserIDString);
            }

            public string GetName(string userId = null)
            {
                if (!string.IsNullOrEmpty(Title))
                    return Title;

                if (!string.IsNullOrEmpty(DisplayName))
                    return DisplayName;

                if (!string.IsNullOrEmpty(ShortName))
                {
                    var def = ItemManager.FindItemDefinition(ShortName);
                    if (def != null)
                    {
                        var displayName = def.displayName.translated;

                        if (_config.UseLangAPI)
                            displayName = Instance?.GetItemDisplayNameFromLangAPI(ShortName, displayName, userId);

                        return displayName;
                    }
                }

                return string.Empty;
            }

            [JsonIgnore]
            public ItemDefinition ItemDefinition => ItemManager.FindItemDefinition(ShortName ?? string.Empty);

            #endregion

            #region Helpers

            public ICuiComponent GetPublicImage()
            {
                if (!string.IsNullOrEmpty(Image)) return new CuiRawImageComponent {Png = Instance?.GetImage(Image)};

                return new CuiImageComponent {ItemId = ItemDefinition?.itemid ?? 0, SkinId = Skin};
            }

            #region Favorite

            public bool IsFavorite(ulong playerID)
            {
                var playerCart = Instance?.GetPlayerCart(playerID) as PlayerCartData;
                return playerCart != null && playerCart.IsFavoriteItem(ID);
            }

            public bool CanBeFavorite(ulong playerID)
            {
                if (Instance?.TryGetNPCShop(playerID, out _) == true)
                    return false;

                if (Instance?.TryGetCustomVending(playerID, out _) == true)
                    return false;

                return true;
            }

            #endregion

            #region Same

            public bool CanTake(Item item)
            {
                return !item.isBroken && item.IsSameItem(ShortName, Skin) &&
                       (!Genes.Enabled || Genes.IsSameGenes(item));
            }

            #endregion

            public bool IsAvailableForPlayer(ulong userID)
            {
                var selectedEconomy = Instance.API_GetShopPlayerSelectedEconomy(userID);

                return userID.HasPermission(Permission) &&
                       (!Currencies.Enabled ||
                        (CanBuy &&
                         Currencies.HasCurrency(true, selectedEconomy)) ||
                        (CanBeSold() &&
                         Currencies.HasCurrency(false, selectedEconomy)));
            }

            public bool CanBeSold()
            {
                return _config.EnableSelling && CanSell && SellPrice >= 0.0;
            }

            public bool CanBePurchased()
            {
                return CanBuy && Price >= 0.0;
            }

            public float GetCooldown(string player, bool buy = true)
            {
                var result = buy ? BuyCooldown : SellCooldown;

                var targetCooldowns = buy ? BuyCooldowns : SellCooldowns;

                foreach (var (perm, cooldown) in targetCooldowns)
                    if (player.HasPermission(perm))
                        if (cooldown < result)
                            result = cooldown;

                return result;
            }

            public double GetPrice(BasePlayer player, int selectedEconomy)
            {
                var discount = GetDiscount(player);

                var priceValue = Price;
                if (Currencies is {Enabled: true} && Currencies.TryGetCurrency(true, selectedEconomy, out var currency))
                    priceValue = currency.Price;

                return Math.Round(discount != 0 ? priceValue * (1f - discount / 100f) : priceValue,
                    _config.UI.RoundDigits);
            }

            public double GetSellPrice(BasePlayer player, int selectedEconomy = 0)
            {
                var priceValue = SellPrice;

                if (Currencies is {Enabled: true} &&
                    Currencies.TryGetCurrency(false, selectedEconomy, out var currency)) priceValue = currency.Price;

                return priceValue;
            }

            public int GetDiscount(BasePlayer player)
            {
                if (!UseCustomDiscount) return _config.Discount.GetDiscount(player);

                var result = 0;

                foreach (var (perm, discount) in Discount)
                    if (player.HasPermission(perm))
                        if (discount > result)
                            result = discount;

                return result;
            }

            public int GetLimit(BasePlayer player, bool buy = true, bool daily = false)
            {
                var limits = GetLimitsDictionary(buy, daily);
                if (limits == null || limits.Count == 0)
                    return 0;

                var maxLimit = 0;

                foreach (var (permission, limit) in limits)
                    if (limit > maxLimit && player.HasPermission(permission))
                        maxLimit = limit;

                return maxLimit;
            }

            private Dictionary<string, int> GetLimitsDictionary(bool buy, bool daily)
            {
                if (daily)
                    return buy ? DailyBuyLimits : DailySellLimits;

                return buy ? BuyLimits : SellLimits;
            }

            public void Get(BasePlayer player, int count = 1)
            {
                switch (Type)
                {
                    case ItemType.Item:
                        ToItem(player, count);
                        break;
                    case ItemType.Command:
                        ToCommand(player, count);
                        break;
                    case ItemType.Plugin:
                        Plugin.Get(player, count);
                        break;
                    case ItemType.Kit:
                        ToKit(player, count);
                        break;
                }
            }

            private void ToKit(BasePlayer player, int count)
            {
                if (string.IsNullOrEmpty(Kit)) return;

                for (var i = 0; i < count; i++)
                    Interface.Oxide.CallHook("GiveKit", player, Kit);
            }

            private void ToItem(BasePlayer player, int count)
            {
                if (ItemDefinition == null)
                {
                    Debug.LogError($"Error creating item with ShortName '{ShortName}'");
                    return;
                }

                if (Blueprint)
                {
                    GiveBlueprint(Amount * count, player);
                }
                else
                {
                    if (ProhibitSplit)
                        GiveItem(Amount * count, player);
                    else
                        GetStacks(count)?.ForEach(stack => GiveItem(stack, player));
                }
            }

            private void GiveBlueprint(int count, BasePlayer player)
            {
                for (var i = 0; i < count; i++) GiveBlueprint(player);
            }

            private void GiveBlueprint(BasePlayer player)
            {
                var bp = ItemManager.Create(ItemManager.blueprintBaseDef);
                if (bp == null)
                {
                    Instance?.PrintError("Error creating blueprintbase");
                    return;
                }

                bp.blueprintTarget = ItemManager.FindItemDefinition(ShortName).itemid;

                if (!string.IsNullOrEmpty(DisplayName)) bp.name = DisplayName;

                player.GiveItem(bp, BaseEntity.GiveItemReason.PickedUp);
            }

            private void GiveItem(int amount, BasePlayer player)
            {
                var newItem = ItemManager.Create(ItemDefinition, amount, Skin);
                if (newItem == null)
                {
                    Instance?.PrintError($"Error creating item with ShortName '{ShortName}'");
                    return;
                }

                if (!string.IsNullOrEmpty(DisplayName)) newItem.name = DisplayName;

                if (Weapon is {Enabled: true})
                    Weapon.Build(newItem);

                if (Content is {Enabled: true})
                    Content.Build(newItem);

                if (Genes is {Enabled: true})
                    Genes.Build(newItem);

                player.GiveItem(newItem, BaseEntity.GiveItemReason.PickedUp);
            }

            private void ToCommand(BasePlayer player, int count)
            {
                var pos = GetLookPoint(player);

                for (var i = 0; i < count; i++)
                {
                    var command = Command.Replace("\n", "|")
                        .Replace("%steamid%", player.UserIDString, StringComparison.OrdinalIgnoreCase)
                        .Replace("%username%", player.displayName, StringComparison.OrdinalIgnoreCase)
                        .Replace("%player.z%", pos.z.ToString(CultureInfo.InvariantCulture),
                            StringComparison.OrdinalIgnoreCase)
                        .Replace("%player.x%", pos.x.ToString(CultureInfo.InvariantCulture),
                            StringComparison.OrdinalIgnoreCase)
                        .Replace("%player.y%", pos.y.ToString(CultureInfo.InvariantCulture),
                            StringComparison.OrdinalIgnoreCase);

                    foreach (var check in command.Split('|'))
                        if (check.Contains("chat.say"))
                        {
                            var args = check.Split(' ');
                            player.SendConsoleCommand(
                                $"{args[0]}  \" {string.Join(" ", args.ToList().GetRange(1, args.Length - 1))}\" 0");
                        }
                        else
                        {
                            Instance?.Server.Command(check);
                        }
                }
            }

            public List<int> GetStacks(int amount)
            {
                amount *= Amount;

                var maxStack = ItemDefinition.stackable;

                var list = new List<int>();

                if (maxStack == 0) maxStack = 1;

                while (amount > maxStack)
                {
                    amount -= maxStack;
                    list.Add(maxStack);
                }

                list.Add(amount);

                return list;
            }

            public override string ToString()
            {
                switch (Type)
                {
                    case ItemType.Item:
                        return $"[ITEM-{ID}] {ShortName}x{Amount}(DN: {DisplayName}, SKIN: {Skin})";
                    case ItemType.Command:
                        return $"[COMMAND-{ID}] {Command}";
                    case ItemType.Plugin:
                        return
                            $"[PLUGIN-{ID}] Name: {Plugin?.Plugin}, Hook: {Plugin?.Hook}, Amount: {Plugin?.Amount ?? 0}";
                    case ItemType.Kit:
                        return $"[KIT-{ID}] {Kit}";
                    default:
                        return base.ToString();
                }
            }

            #endregion

            #region Constructor

            public ShopItem()
            {
            }

            public ShopItem(Dictionary<string, object> dictionary)
            {
                var price = (double) dictionary["Price"];
                var sellPrice = (double) dictionary["SellPrice"];

                ID = (int) dictionary["ID"];
                Type = (ItemType) dictionary["Type"];
                Image = (string) dictionary["Image"];
                Title = (string) dictionary["Title"];
                Command = (string) dictionary["Command"];
                DisplayName = (string) dictionary["DisplayName"];
                ShortName = (string) dictionary["ShortName"];
                Skin = (ulong) dictionary["Skin"];
                Blueprint = (bool) dictionary["Blueprint"];
                CanBuy = (bool) dictionary["Buying"];
                CanSell = (bool) dictionary["Selling"];
                Amount = (int) dictionary["Amount"];
                Price = price;
                SellPrice = sellPrice;
                Plugin = new PluginItem
                {
                    Hook = (string) dictionary["Plugin_Hook"],
                    Plugin = (string) dictionary["Plugin_Name"],
                    Amount = (int) dictionary["Plugin_Amount"]
                };
                Discount = new Dictionary<string, int>
                {
                    ["shop.default"] = 0,
                    ["shop.vip"] = 10
                };
                SellLimits = new Dictionary<string, int>
                {
                    ["shop.default"] = 0,
                    ["shop.vip"] = 0
                };
                BuyLimits = new Dictionary<string, int>
                {
                    ["shop.default"] = 0,
                    ["shop.vip"] = 0
                };
                DailyBuyLimits = new Dictionary<string, int>
                {
                    ["shop.default"] = 0,
                    ["shop.vip"] = 0
                };
                DailySellLimits = new Dictionary<string, int>
                {
                    ["shop.default"] = 0,
                    ["shop.vip"] = 0
                };
                BuyCooldowns = new Dictionary<string, float>
                {
                    ["shop.default"] = 0f,
                    ["shop.vip"] = 0f
                };

                SellCooldowns = new Dictionary<string, float>
                {
                    ["shop.default"] = 0f,
                    ["shop.vip"] = 0f
                };

                Content = new ItemContent
                {
                    Enabled = false,
                    Contents = new List<ItemContent.ContentInfo>
                    {
                        new()
                        {
                            ShortName = string.Empty,
                            Condition = 100,
                            Amount = 1,
                            Position = -1
                        }
                    }
                };

                Weapon = new ItemWeapon
                {
                    Enabled = false,
                    AmmoType = string.Empty,
                    AmmoAmount = 1
                };

                Genes = new ItemGenes
                {
                    Enabled = false,
                    GeneTypes = new List<char>
                    {
                        'X', 'Y', 'G', 'W', 'H', 'W'
                    }
                };

                Currencies = new ItemCurrency
                {
                    Enabled = false,
                    Currencies = new Dictionary<int, CurrencyInfo>
                    {
                        [0] = new() {Price = price},
                        [1] = new() {Price = price}
                    },
                    SellCurrencies = new Dictionary<int, CurrencyInfo>
                    {
                        [0] = new() {Price = sellPrice},
                        [1] = new() {Price = sellPrice}
                    }
                };
            }

            public static ShopItem GetDefault(int id, double itemCost, string shortName)
            {
                return new ShopItem
                {
                    Type = ItemType.Item,
                    ID = id,
                    Price = itemCost,
                    SellPrice = itemCost,
                    Image = string.Empty,
                    Title = string.Empty,
                    Command = string.Empty,
                    Plugin = new PluginItem(),
                    DisplayName = string.Empty,
                    ShortName = shortName,
                    Skin = 0,
                    Blueprint = false,
                    Amount = 1,
                    Discount = new Dictionary<string, int>
                    {
                        ["shop.default"] = 0,
                        ["shop.vip"] = 10
                    },
                    SellLimits = new Dictionary<string, int>
                    {
                        ["shop.default"] = 0,
                        ["shop.vip"] = 0
                    },
                    BuyLimits = new Dictionary<string, int>
                    {
                        ["shop.default"] = 0,
                        ["shop.vip"] = 0
                    },
                    DailyBuyLimits = new Dictionary<string, int>
                    {
                        ["shop.default"] = 0,
                        ["shop.vip"] = 0
                    },
                    DailySellLimits = new Dictionary<string, int>
                    {
                        ["shop.default"] = 0,
                        ["shop.vip"] = 0
                    },
                    BuyMaxAmount = 0,
                    SellMaxAmount = 0,
                    ForceBuy = false,
                    ProhibitSplit = false,
                    Localization = new Localization
                    {
                        Enabled = false,
                        Messages = new Dictionary<string, string>
                        {
                            ["en"] = string.Empty,
                            ["fr"] = string.Empty
                        }
                    },
                    BuyCooldowns = new Dictionary<string, float>
                    {
                        ["shop.default"] = 0f,
                        ["shop.vip"] = 0f
                    },
                    SellCooldowns = new Dictionary<string, float>
                    {
                        ["shop.default"] = 0f,
                        ["shop.vip"] = 0f
                    },
                    Content = new ItemContent
                    {
                        Enabled = false,
                        Contents = new List<ItemContent.ContentInfo>
                        {
                            new()
                            {
                                ShortName = string.Empty,
                                Condition = 100,
                                Amount = 1,
                                Position = -1
                            }
                        }
                    },
                    Weapon = new ItemWeapon
                    {
                        Enabled = false,
                        AmmoType = string.Empty,
                        AmmoAmount = 1
                    },
                    Genes = new ItemGenes
                    {
                        Enabled = false,
                        GeneTypes = new List<char>
                        {
                            'X', 'Y', 'G', 'W', 'H', 'W'
                        }
                    },
                    Currencies = new ItemCurrency
                    {
                        Enabled = false,
                        Currencies = new Dictionary<int, CurrencyInfo>
                        {
                            [0] = new() {Price = itemCost},
                            [1] = new() {Price = itemCost}
                        },
                        SellCurrencies = new Dictionary<int, CurrencyInfo>
                        {
                            [0] = new() {Price = itemCost},
                            [1] = new() {Price = itemCost}
                        }
                    }
                };
            }

            #endregion

            #region Classes

            public class ItemCurrency
            {
                #region Fields

                [JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
                public bool Enabled;

                [JsonProperty(
                    PropertyName = LangRu
                        ? "Валюты для покупки предметов (ключ – ID экономики, при использовании экономики по умолчанию используйте 0)"
                        : "Enabled currency for buying items (key - economy ID, if you use economy by default use 0)",
                    ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public Dictionary<int, CurrencyInfo> Currencies = new();

                [JsonProperty(
                    PropertyName = LangRu
                        ? "Валюты для продажи предметов (ключ – ID экономики, при использовании экономики по умолчанию используйте 0)"
                        : "Currency for selling items (key - economy ID, if you use economy by default use 0)",
                    ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public Dictionary<int, CurrencyInfo> SellCurrencies = new();

                #endregion

                #region Public Methods

                #region Buy or Sell

                public bool TryGetCurrency(bool buy, int playerCurrency, out CurrencyInfo currency)
                {
                    var currencies = buy ? Currencies : SellCurrencies;
                    return currencies.TryGetValue(playerCurrency, out currency);
                }

                public bool HasCurrency(bool buy, int playerCurrency)
                {
                    var currencies = buy ? Currencies : SellCurrencies;
                    return currencies.ContainsKey(playerCurrency);
                }

                #endregion

                #region Global

                public bool HasCurrency(int playerCurrency)
                {
                    return Currencies.ContainsKey(playerCurrency) || SellCurrencies.ContainsKey(playerCurrency);
                }

                #endregion

                #endregion
            }

            public class CurrencyInfo
            {
                [JsonProperty(PropertyName = LangRu ? "Цена" : "Price")]
                public double Price;
            }

            #endregion
        }

        public class ItemGenes
        {
            #region Fields

            [JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
            public bool Enabled;

            [JsonProperty(PropertyName = LangRu ? "Типы генов" : "Gene types",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<char> GeneTypes = new();

            #endregion

            #region Cache

            [JsonIgnore] private int _encodedGenes;

            public bool TryInit()
            {
                if (GeneTypes is not {Count: 6})
                    return false;

                var genes = new GrowableGenes();
                for (var i = 0; i < GeneTypes.Count; i++) genes.Genes[i].Set(ConvertGeneType(GeneTypes[i]));

                _encodedGenes = GrowableGeneEncoding.EncodeGenesToInt(genes);
                return true;
            }

            #endregion

            #region Helpers

            public void Build(Item item)
            {
                if (GeneTypes is not {Count: 6})
                    return;

                GrowableGeneEncoding.EncodeGenesToItem(_encodedGenes, item);
            }

            private static GrowableGenetics.GeneType ConvertGeneType(char geneType)
            {
                return char.ToLower(geneType) switch
                {
                    'g' => GrowableGenetics.GeneType.GrowthSpeed,
                    'y' => GrowableGenetics.GeneType.Yield,
                    'h' => GrowableGenetics.GeneType.Hardiness,
                    'x' => GrowableGenetics.GeneType.Empty,
                    'w' => GrowableGenetics.GeneType.WaterRequirement,
                    _ => GrowableGenetics.GeneType.Empty
                };
            }

            public bool IsSameGenes(Item item)
            {
                return item.instanceData != null && _encodedGenes == item.instanceData.dataInt;
            }

            #endregion
        }

        public class ItemContent
        {
            #region Fields

            [JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
            public bool Enabled;

            [JsonProperty(PropertyName = LangRu ? "Содержимое" : "Contents",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ContentInfo> Contents = new();

            #endregion

            #region Helpers

            public void Build(Item item)
            {
                Contents?.ForEach(content => content?.Build(item));
            }

            #endregion

            #region Classes

            public class ContentInfo
            {
                [JsonProperty(PropertyName = "ShortName")]
                public string ShortName;

                [JsonProperty(PropertyName = LangRu ? "Состояние" : "Condition")]
                public float Condition;

                [JsonProperty(PropertyName = LangRu ? "Количество" : "Amount")]
                public int Amount;

                [JsonProperty(PropertyName = LangRu ? "Позиция" : "Position")]
                public int Position = -1;

                #region Helpers

                public void Build(Item item)
                {
                    var content = ItemManager.CreateByName(ShortName, Mathf.Max(Amount, 1));
                    if (content == null) return;
                    content.condition = Condition;
                    content.MoveToContainer(item.contents, Position);
                }

                #endregion
            }

            #endregion
        }

        public class ItemWeapon
        {
            #region Fields

            [JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
            public bool Enabled;

            [JsonProperty(PropertyName = LangRu ? "Тип боеприпасов" : "Ammo Type")]
            public string AmmoType;

            [JsonProperty(PropertyName = LangRu ? "Количество боеприпасов" : "Ammo Amount")]
            public int AmmoAmount;

            #endregion

            #region Helpers

            public void Build(Item item)
            {
                var heldEntity = item.GetHeldEntity();
                if (heldEntity != null)
                {
                    heldEntity.skinID = item.skin;

                    var baseProjectile = heldEntity as BaseProjectile;
                    if (baseProjectile != null && !string.IsNullOrEmpty(AmmoType))
                    {
                        baseProjectile.primaryMagazine.contents = Mathf.Max(AmmoAmount, 0);
                        baseProjectile.primaryMagazine.ammoType =
                            ItemManager.FindItemDefinition(AmmoType);
                    }

                    heldEntity.SendNetworkUpdate();
                }
            }

            #endregion
        }

        public class PluginItem
        {
            [JsonProperty(PropertyName = "Hook")] public string Hook = string.Empty;

            [JsonProperty(PropertyName = "Plugin Name")]
            public string Plugin = string.Empty;

            [JsonProperty(PropertyName = "Amount")]
            public int Amount;

            public void Get(BasePlayer player, int count = 1)
            {
                var plug = Instance?.plugins.Find(Plugin);
                if (plug == null)
                {
                    Instance?.PrintError($"Plugin '{Plugin}' not found !!! ");
                    return;
                }

                switch (Plugin)
                {
                    case "Economics":
                    {
                        plug.Call(Hook, player.userID.Get(), (double) Amount * count);
                        break;
                    }
                    default:
                    {
                        plug.Call(Hook, player.userID.Get(), Amount * count);
                        break;
                    }
                }
            }
        }

        private class AdditionalEconomy : EconomyEntry
        {
            [JsonProperty(PropertyName = "ID")] public int ID;

            [JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
            public bool Enabled;

            public bool IsSame(EconomyEntry configEconomy)
            {
                return Type == configEconomy.Type &&
                       Plug == configEconomy.Plug &&
                       ShortName == configEconomy.ShortName &&
                       Skin == configEconomy.Skin;
            }

            public AdditionalEconomy(EconomyEntry configEconomy)
            {
                Type = configEconomy.Type;
                Plug = configEconomy.Plug;
                AddHook = configEconomy.AddHook;
                RemoveHook = configEconomy.RemoveHook;
                BalanceHook = configEconomy.BalanceHook;
                ShortName = configEconomy.ShortName;
                DisplayName = configEconomy.DisplayName;
                Skin = configEconomy.Skin;
                EconomyTitle = configEconomy.EconomyTitle;
                EconomyBalance = configEconomy.EconomyBalance;
                EconomyPrice = configEconomy.EconomyPrice;
                EconomyFooterPrice = configEconomy.EconomyFooterPrice;
                ID = 0;
                Enabled = true;
            }

            [JsonConstructor]
            public AdditionalEconomy()
            {
            }
        }

        private enum EconomyType
        {
            Plugin,
            Item
        }

        private class EconomyEntry
        {
            #region Fields

            [JsonProperty(PropertyName = LangRu ? "Тип (Plugin/Item)" : "Type (Plugin/Item)")]
            [JsonConverter(typeof(StringEnumConverter))]
            public EconomyType Type;

            [JsonProperty(PropertyName = "Plugin name")]
            public string Plug;

            [JsonProperty(PropertyName = "Balance add hook")]
            public string AddHook;

            [JsonProperty(PropertyName = "Balance remove hook")]
            public string RemoveHook;

            [JsonProperty(PropertyName = "Balance show hook")]
            public string BalanceHook;

            [JsonProperty(PropertyName = "ShortName")]
            public string ShortName;

            [JsonProperty(PropertyName = "Display Name (empty - default)")]
            public string DisplayName;

            [JsonProperty(PropertyName = "Skin")] public ulong Skin;

            [JsonProperty(PropertyName = LangRu ? "Название" : "Title")]
            public EconomyTitle EconomyTitle;

            [JsonProperty(PropertyName = LangRu ? "Баланс" : "Balance")]
            public EconomyTitle EconomyBalance;

            [JsonProperty(PropertyName = LangRu ? "Стоимость" : "Price")]
            public EconomyTitle EconomyPrice;

            [JsonProperty(PropertyName = LangRu ? "Стоимость предметов в футере" : "Footer Items Price")]
            public EconomyTitle EconomyFooterPrice;

            #endregion Fields

            #region Public Methods

            public string GetTitle(BasePlayer player)
            {
                return EconomyTitle?.Get(player) ?? $"Error: Title not found for player {player.UserIDString}.";
            }

            public string GetBalanceTitle(BasePlayer player)
            {
                return EconomyBalance?.Get(player, ShowBalance(player).ToString(_config.Formatting.BalanceFormat))
                       ?? $"Error: Balance title not found for player {player.UserIDString}.";
            }

            public string GetPriceTitle(BasePlayer player, string formattedPrice)
            {
                return EconomyPrice?.Get(player, formattedPrice)
                       ?? $"Error: Price title not found for player {player.UserIDString} with price {formattedPrice}.";
            }

            public string GetFooterPriceTitle(BasePlayer player, string formattedPrice)
            {
                return EconomyFooterPrice?.Get(player, formattedPrice)
                       ??
                       $"Error: Footer price title not found for player {player.UserIDString} with price {formattedPrice}.";
            }

            public string GetDebugInfo()
            {
                switch (Type)
                {
                    case EconomyType.Plugin:
                        return $"{_config.Economy.Plug} – {_config.Economy.Type}";
                    case EconomyType.Item:
                        return $"{_config.Economy.ShortName} ({_config.Economy.Skin}) – {_config.Economy.Type}";
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            #endregion Public Methods

            #region Economy Methods

            public double ShowBalance(BasePlayer player)
            {
                switch (Type)
                {
                    case EconomyType.Plugin:
                    {
                        var plugin = Instance?.plugins?.Find(Plug);
                        if (plugin == null) return 0;

                        return Convert.ToDouble(plugin.Call(BalanceHook, player.userID.Get()));
                    }
                    case EconomyType.Item:
                    {
                        return PlayerItemsCount(player, ShortName, Skin);
                    }
                    default:
                        return 0;
                }
            }

            public void AddBalance(BasePlayer player, double amount)
            {
                switch (Type)
                {
                    case EconomyType.Plugin:
                    {
                        var plugin = Instance?.plugins?.Find(Plug);
                        if (plugin == null) return;

                        switch (Plug)
                        {
                            case "BankSystem":
                            case "ServerRewards":
                            case "IQEconomic":
                                plugin.Call(AddHook, player.userID.Get(), (int) amount);
                                break;
                            default:
                                plugin.Call(AddHook, player.userID.Get(), amount);
                                break;
                        }

                        break;
                    }
                    case EconomyType.Item:
                    {
                        var am = (int) amount;

                        var item = ToItem(am);
                        if (item == null) return;

                        player.GiveItem(item);
                        break;
                    }
                }
            }

            public bool RemoveBalance(BasePlayer player, double amount)
            {
                switch (Type)
                {
                    case EconomyType.Plugin:
                    {
                        if (ShowBalance(player) < amount) return false;

                        var plugin = Instance?.plugins.Find(Plug);
                        if (plugin == null) return false;

                        switch (Plug)
                        {
                            case "BankSystem":
                            case "ServerRewards":
                            case "IQEconomic":
                                plugin.Call(RemoveHook, player.userID.Get(), (int) amount);
                                break;
                            default:
                                plugin.Call(RemoveHook, player.userID.Get(), amount);
                                break;
                        }

                        return true;
                    }
                    case EconomyType.Item:
                    {
                        var playerItems = Pool.Get<List<Item>>();
                        player.inventory.GetAllItems(playerItems);

                        var am = (int) amount;

                        if (ItemCount(playerItems, ShortName, Skin) < am)
                        {
                            Pool.Free(ref playerItems);
                            return false;
                        }

                        Take(playerItems, ShortName, Skin, am);
                        Pool.Free(ref playerItems);
                        return true;
                    }
                    default:
                        return false;
                }
            }

            public bool Transfer(BasePlayer player, BasePlayer targetPlayer, double amount)
            {
                if (!RemoveBalance(player, amount))
                    return false;

                AddBalance(targetPlayer, amount);
                return true;
            }

            private Item ToItem(int amount)
            {
                var item = ItemManager.CreateByName(ShortName, amount, Skin);
                if (item == null)
                {
                    Debug.LogError($"Error creating item with ShortName: '{ShortName}'");
                    return null;
                }

                if (!string.IsNullOrEmpty(DisplayName)) item.name = DisplayName;

                return item;
            }

            #endregion
        }

        private class EconomyTitle
        {
            #region Fields

            [JsonProperty(PropertyName = LangRu ? "Сообщение" : "Message")]
            public string Message = string.Empty;

            [JsonProperty(PropertyName = LangRu ? "Использовать локализованные сообщения?" : "Use localized messages?")]
            public bool UseLocalizedMessages;

            [JsonProperty(PropertyName = LangRu ? "Локализованные сообщения" : "Localized messages",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, string> LocalizedMessages = new();

            #endregion

            #region Public Methods

            public string Get(BasePlayer player, params object[] args)
            {
                return string.Format(GetMessage(player), args);
            }

            public string GetMessage(BasePlayer player)
            {
                if (UseLocalizedMessages && player != null)
                {
                    var language = Instance?.lang?.GetLanguage(player.UserIDString);
                    if (!string.IsNullOrWhiteSpace(language) &&
                        LocalizedMessages.TryGetValue(language, out var message))
                        return message;
                }

                return Message;
            }

            #endregion

            #region Constructors

            public EconomyTitle()
            {
            }

            public EconomyTitle(string message)
            {
                Message = message;
                UseLocalizedMessages = false;
                LocalizedMessages = new Dictionary<string, string>
                {
                    ["en"] = message,
                    ["fr"] = message
                };
            }

            public EconomyTitle(string message, Dictionary<string, string> localizedMessages)
            {
                Message = message;
                UseLocalizedMessages = localizedMessages is {Count: > 0};
                LocalizedMessages = localizedMessages ?? new Dictionary<string, string>();
            }

            public EconomyTitle(Dictionary<string, string> localizedMessages)
            {
                if (localizedMessages is {Count: > 0})
                {
                    if (localizedMessages.TryGetValue("en", out var defaultMsg))
                    {
                        Message = defaultMsg;
                        localizedMessages.Remove("en");
                    }
                    else
                    {
                        Message = string.Empty;
                    }

                    UseLocalizedMessages = localizedMessages is {Count: > 0};
                    LocalizedMessages = localizedMessages ?? new Dictionary<string, string>();
                }
                else
                {
                    Message = string.Empty;
                    UseLocalizedMessages = false;
                    LocalizedMessages = new Dictionary<string, string>
                    {
                        ["en"] = string.Empty,
                        ["fr"] = string.Empty
                    };
                }
            }

            public EconomyTitle(string en, string ru, string znCN)
            {
                Message = en;
                UseLocalizedMessages = true;
                LocalizedMessages = new Dictionary<string, string>
                {
                    ["ru"] = ru,
                    ["zh-CN"] = znCN
                };
            }

            #endregion
        }

        #endregion

        private bool _canSaveConfig;

        protected override void LoadConfig()
        {
            _canSaveConfig = false;
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null)
                    throw new Exception();

                _canSaveConfig = true;

                if (_config.Version < Version)
                    UpdateConfigValues();

                SaveConfig();
            }
            catch (Exception ex)
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
                Debug.LogException(ex);
            }
        }

        protected override void SaveConfig()
        {
            if (!_canSaveConfig) return;

            Config.WriteObject(_config);
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
        }

        private void UpdateConfigValues()
        {
            var baseConfig = new Configuration();

            if (_config.Version != default)
            {
                PrintWarning("Config update detected! Updating config values...");

                if (_config.Version >= new VersionNumber(2, 0, 0))
                {
                    if (_config.Version < new VersionNumber(2, 0, 2))
                    {
                        if (_uiData == null)
                            LoadTemplate();

                        if (_uiData != null)
                        {
                            if (_uiData.IsFullscreenUISet && _uiData.FullscreenUI?.SelectCurrency?.FrameIndent is -105)
                                _uiData.FullscreenUI.SelectCurrency.FrameIndent = 15;

                            if (_uiData.IsInMenuUISet && _uiData.InMenuUI?.SelectCurrency?.FrameIndent is -105)
                                _uiData.InMenuUI.SelectCurrency.FrameIndent = 15;
                        }
                    }

                    if (_config.Version < new VersionNumber(2, 1, 0))
                    {
                        if (_config.Version == new VersionNumber(2, 0, 0))
                        {
                            #region Load Config Variables

                            (int ID, string TitleLangKey, string BalanceLangKey, string PriceLangKey)
                                mainEconomy =
                                    new(
                                        0,
                                        Convert.ToString(Config.Get(LangRu ? "Экономика" : "Economy",
                                            LangRu ? "Lang Ключ (для названия)" : "Lang Key (for Title)")),
                                        Convert.ToString(Config.Get(LangRu ? "Экономика" : "Economy",
                                            LangRu ? "Lang Ключ (для баланса)" : "Lang Key (for Balance)")),
                                        Convert.ToString(Config.Get(LangRu ? "Экономика" : "Economy",
                                            LangRu ? "Lang Ключ (для стоимости)" : "Lang Key (for Price)")));

                            var additionalEconomyTitles =
                                new List<(int ID, string TitleLangKey, string BalanceLangKey, string PriceLangKey)>();

                            if (Config.Get(LangRu ? "Дополнительная экономика" : "Additional Economics") is List<object>
                                additionalEcoObj)
                                foreach (var obj in additionalEcoObj)
                                {
                                    if (obj is not Dictionary<string, object> jsonObj) continue;
                                    if (!jsonObj.TryGetValue("ID", out var idObj)) continue;

                                    var id = Convert.ToInt32(idObj);
                                    var titleLangKey =
                                        Convert.ToString(
                                            jsonObj[LangRu ? "Lang Ключ (для названия)" : "Lang Key (for Title)"]);
                                    var balanceLangKey = Convert.ToString(
                                        jsonObj[LangRu ? "Lang Ключ (для баланса)" : "Lang Key (for Balance)"]);
                                    var priceLangKey = Convert.ToString(
                                        jsonObj[LangRu ? "Lang Ключ (для стоимости)" : "Lang Key (for Price)"]);

                                    additionalEconomyTitles.Add((id, titleLangKey, balanceLangKey, priceLangKey));
                                }

                            #endregion

                            var langToUpdate =
                                new Dictionary<int, Dictionary<string, (string TitleLangKey, string BalanceLangKey,
                                    string PriceLangKey)>>();

                            foreach (var targetLang in lang.GetLanguages(this))
                            {
                                var messageFile = GetMessageFile(Name, targetLang);
                                if (messageFile is not null)
                                {
                                    LoadEconomyUpdateMessages(mainEconomy);
                                    additionalEconomyTitles?.ForEach(LoadEconomyUpdateMessages);
                                }

                                void LoadEconomyUpdateMessages(
                                    (int ID, string TitleLangKey, string BalanceLangKey, string PriceLangKey)
                                        targetEconomy)
                                {
                                    if (messageFile.TryGetValue(targetEconomy.TitleLangKey, out var msgTitle) &&
                                        messageFile.TryGetValue(targetEconomy.BalanceLangKey, out var msgBalance) &&
                                        messageFile.TryGetValue(targetEconomy.PriceLangKey, out var msgPrice))
                                    {
                                        msgBalance = msgBalance.Replace("{1}", "RP");
                                        msgPrice = msgPrice.Replace("{1}", "RP");

                                        (string TitleLangKey, string BalanceLangKey, string PriceLangKey) newMessages =
                                            new(msgTitle, msgBalance, msgPrice);

                                        if (langToUpdate.TryGetValue(targetEconomy.ID, out var economyLangs))
                                            economyLangs[targetLang] = newMessages;
                                        else
                                            langToUpdate.TryAdd(targetEconomy.ID,
                                                new Dictionary<string, (string TitleLangKey, string BalanceLangKey,
                                                    string PriceLangKey)>
                                                {
                                                    [targetLang] = newMessages
                                                });
                                    }
                                }
                            }

                            foreach (var (economyID, economyLangs) in langToUpdate)
                            {
                                var targetEconomy = economyID == 0
                                    ? _config.Economy
                                    : _config.AdditionalEconomics.Find(ec => ec.ID == economyID);
                                if (targetEconomy == null) continue;

                                var dict = new Dictionary<string, Dictionary<string, string>>();

                                foreach (var (targetLang, messages) in economyLangs)
                                {
                                    if (dict.TryGetValue("TitleLangKey", out var titleLang))
                                        titleLang[targetLang] = messages.TitleLangKey;
                                    else
                                        dict.TryAdd("TitleLangKey", new Dictionary<string, string>
                                        {
                                            [targetLang] = messages.TitleLangKey
                                        });

                                    if (dict.TryGetValue("BalanceLangKey", out var balanceLang))
                                        balanceLang[targetLang] = messages.BalanceLangKey;
                                    else
                                        dict.TryAdd("BalanceLangKey", new Dictionary<string, string>
                                        {
                                            [targetLang] = messages.BalanceLangKey
                                        });

                                    if (dict.TryGetValue("PriceLangKey", out var priceLang))
                                        priceLang[targetLang] = messages.PriceLangKey;
                                    else
                                        dict.TryAdd("PriceLangKey", new Dictionary<string, string>
                                        {
                                            [targetLang] = messages.PriceLangKey
                                        });
                                }

                                if (dict.Count == 0) continue;

                                foreach (var (msgkey, messages) in dict)
                                    switch (msgkey)
                                    {
                                        case "TitleLangKey":
                                            targetEconomy.EconomyTitle = new EconomyTitle(messages);
                                            break;
                                        case "BalanceLangKey":
                                            targetEconomy.EconomyBalance = new EconomyTitle(messages);
                                            break;
                                        case "PriceLangKey":
                                            var newEconomyPrice = new EconomyTitle(messages);
                                            targetEconomy.EconomyPrice = newEconomyPrice;
                                            targetEconomy.EconomyFooterPrice = newEconomyPrice;
                                            break;
                                    }
                            }
                        }
                        else if (_config.Version >= new VersionNumber(2, 0, 1))
                        {
                            #region Load Config Variables

                            (int ID, string Title, string TitleLangKey, string BalanceLangKey, string PriceLangKey)
                                mainEconomy =
                                    new(
                                        0,
                                        Convert.ToString(Config.Get(LangRu ? "Экономика" : "Economy",
                                            LangRu ? "Название экономики" : "Economy Title")),
                                        Convert.ToString(Config.Get(LangRu ? "Экономика" : "Economy",
                                            LangRu ? "Lang Ключ (для названия)" : "Lang Key (for Title)")),
                                        Convert.ToString(Config.Get(LangRu ? "Экономика" : "Economy",
                                            LangRu ? "Lang Ключ (для баланса)" : "Lang Key (for Balance)")),
                                        Convert.ToString(Config.Get(LangRu ? "Экономика" : "Economy",
                                            LangRu ? "Lang Ключ (для стоимости)" : "Lang Key (for Price)")));

                            var additionalEconomyTitles =
                                new List<(int ID, string Title, string TitleLangKey, string BalanceLangKey, string
                                    PriceLangKey)>();

                            if (Config.Get(LangRu ? "Дополнительная экономика" : "Additional Economics") is List<object>
                                additionalEcoObj)
                                foreach (var obj in additionalEcoObj)
                                {
                                    if (obj is not Dictionary<string, object> jsonObj) continue;
                                    if (!jsonObj.TryGetValue(LangRu ? "Название экономики" : "Economy Title",
                                            out var economyTitle)) continue;

                                    var id = Convert.ToInt32(jsonObj["ID"]);
                                    var titleLangKey =
                                        Convert.ToString(
                                            jsonObj[LangRu ? "Lang Ключ (для названия)" : "Lang Key (for Title)"]);
                                    var balanceLangKey = Convert.ToString(
                                        jsonObj[LangRu ? "Lang Ключ (для баланса)" : "Lang Key (for Balance)"]);
                                    var priceLangKey = Convert.ToString(
                                        jsonObj[LangRu ? "Lang Ключ (для стоимости)" : "Lang Key (for Price)"]);

                                    additionalEconomyTitles.Add((id, Convert.ToString(economyTitle), titleLangKey,
                                        balanceLangKey, priceLangKey));
                                }

                            #endregion

                            var langToUpdate =
                                new Dictionary<int, Dictionary<string, (string TitleLangKey, string BalanceLangKey,
                                    string PriceLangKey)>>();

                            foreach (var targetLang in lang.GetLanguages(this))
                            {
                                var messageFile = GetMessageFile(Name, targetLang);
                                if (messageFile is not null)
                                {
                                    LoadEconomyUpdateMessages(mainEconomy);
                                    additionalEconomyTitles?.ForEach(LoadEconomyUpdateMessages);
                                }

                                void LoadEconomyUpdateMessages(
                                    (int ID, string Title, string TitleLangKey, string BalanceLangKey, string
                                        PriceLangKey) targetEconomy)
                                {
                                    if (messageFile.TryGetValue(targetEconomy.TitleLangKey, out var msgTitle) &&
                                        messageFile.TryGetValue(targetEconomy.BalanceLangKey, out var msgBalance) &&
                                        messageFile.TryGetValue(targetEconomy.PriceLangKey, out var msgPrice))
                                    {
                                        var economyTitle = targetEconomy.Title ?? "RP";

                                        msgBalance = msgBalance.Replace("{1}", economyTitle);
                                        msgPrice = msgPrice.Replace("{1}", economyTitle);

                                        (string TitleLangKey, string BalanceLangKey, string PriceLangKey) newMessages =
                                            new(msgTitle, msgBalance, msgPrice);

                                        if (langToUpdate.TryGetValue(targetEconomy.ID, out var economyLangs))
                                            economyLangs[targetLang] = newMessages;
                                        else
                                            langToUpdate.TryAdd(targetEconomy.ID,
                                                new Dictionary<string, (string TitleLangKey, string BalanceLangKey,
                                                    string PriceLangKey)>
                                                {
                                                    [targetLang] = newMessages
                                                });
                                    }
                                }
                            }

                            foreach (var (economyID, economyLangs) in langToUpdate)
                            {
                                var targetEconomy = economyID == 0
                                    ? _config.Economy
                                    : _config.AdditionalEconomics.Find(ec => ec.ID == economyID);
                                if (targetEconomy == null) continue;

                                var dict = new Dictionary<string, Dictionary<string, string>>();

                                foreach (var (targetLang, messages) in economyLangs)
                                {
                                    if (dict.TryGetValue("TitleLangKey", out var titleLang))
                                        titleLang[targetLang] = messages.TitleLangKey;
                                    else
                                        dict.TryAdd("TitleLangKey", new Dictionary<string, string>
                                        {
                                            [targetLang] = messages.TitleLangKey
                                        });

                                    if (dict.TryGetValue("BalanceLangKey", out var balanceLang))
                                        balanceLang[targetLang] = messages.BalanceLangKey;
                                    else
                                        dict.TryAdd("BalanceLangKey", new Dictionary<string, string>
                                        {
                                            [targetLang] = messages.BalanceLangKey
                                        });

                                    if (dict.TryGetValue("PriceLangKey", out var priceLang))
                                        priceLang[targetLang] = messages.PriceLangKey;
                                    else
                                        dict.TryAdd("PriceLangKey", new Dictionary<string, string>
                                        {
                                            [targetLang] = messages.PriceLangKey
                                        });
                                }

                                if (dict.Count == 0) continue;

                                foreach (var (msgkey, messages) in dict)
                                    switch (msgkey)
                                    {
                                        case "TitleLangKey":
                                            targetEconomy.EconomyTitle = new EconomyTitle(messages);
                                            break;
                                        case "BalanceLangKey":
                                            targetEconomy.EconomyBalance = new EconomyTitle(messages);
                                            break;
                                        case "PriceLangKey":
                                            var newEconomyPrice = new EconomyTitle(messages);
                                            targetEconomy.EconomyPrice = newEconomyPrice;
                                            targetEconomy.EconomyFooterPrice = newEconomyPrice;
                                            break;
                                    }
                            }
                        }
                    }

                    if (_config.Version < new VersionNumber(2, 1, 1))
                    {
                        FixEmptyEconomyFooterPrice(_config.Economy);

                        _config.AdditionalEconomics?.ForEach(FixEmptyEconomyFooterPrice);

                        void FixEmptyEconomyFooterPrice(EconomyEntry economyEntry)
                        {
                            if (economyEntry != null && string.IsNullOrEmpty(economyEntry.EconomyFooterPrice.Message) &&
                                !string.IsNullOrEmpty(economyEntry.EconomyPrice.Message))
                                economyEntry.EconomyFooterPrice.Message = economyEntry.EconomyPrice.Message;
                        }
                    }

                    if (_config.Version < new VersionNumber(2, 3, 23))
                        if (_config.UI.DisplayType == "Overlay")
                            _config.UI.DisplayType = "OverlayNonScaled";

                    if (_config.Version < new VersionNumber(2, 4, 0))
                    {
                        var (expertFullscreen, expertInMenu, fullscreenName, inMenuName, hasFullscreen, hasInMenu) =
                            LoadTemplateForMigration();

                        var needsFullscreen =
                            hasFullscreen && !expertFullscreen && !string.IsNullOrEmpty(fullscreenName);
                        var needsInMenu = hasInMenu && !expertInMenu && !string.IsNullOrEmpty(inMenuName);

                        if (needsFullscreen || needsInMenu)
                        {
                            _templateMigrationInProgress = true;

                            PrintWarning("  TEMPLATE MIGRATION TO v2.4.0");
                            PrintWarning("  Templates without Expert Mode will be updated.");
                            PrintWarning("  Backup will be created. Please wait...");

                            BackupTemplateData();
                            MigrateTemplates(needsFullscreen, needsInMenu);
                            return;
                        }
                    }
                }
            }

            _config.Version = Version;
            PrintWarning("Config update completed!");
        }

        private (bool expertFullscreen, bool expertInMenu, string fullscreenName, string inMenuName, bool hasFullscreen,
            bool hasInMenu) LoadTemplateForMigration()
        {
            var filePath = Path.Combine(Interface.Oxide.DataDirectory, Name, "UI.json");
            if (!File.Exists(filePath))
                return (false, false, null, null, false, false);

            try
            {
                var jObject = JObject.Parse(File.ReadAllText(filePath));

                return (
                    jObject["Use expert mode for Fullscreen template?"]?.Value<bool>() ?? false,
                    jObject["Use expert mode for In-Menu template?"]?.Value<bool>() ?? false,
                    jObject["Full Screen UI"]?["Template Name"]?.Value<string>(),
                    jObject["In-Menu UI"]?["Template Name"]?.Value<string>(),
                    jObject["Is Full Screen UI Set"]?.Value<bool>() ?? false,
                    jObject["Is In-Menu UI Set"]?.Value<bool>() ?? false
                );
            }
            catch
            {
                return (false, false, null, null, false, false);
            }
        }

        private void MigrateTemplates(bool migrateFullscreen, bool migrateInMenu)
        {
            LoadShopInstallerData(data =>
            {
                if (data == null)
                {
                    PrintError("Migration failed: Could not download templates!");
                    _templateMigrationInProgress = false;
                    return;
                }

                var filePath = Path.Combine(Interface.Oxide.DataDirectory, Name, "UI.json");

                if (!File.Exists(filePath))
                {
                    PrintError("Migration failed: UI.json not found!");
                    _templateMigrationInProgress = false;
                    return;
                }

                var json = File.ReadAllText(filePath);
                var jObject = JObject.Parse(json);

                _uiData = new UIData
                {
                    UseExpertModeForFullscreenUI =
                        jObject["Use expert mode for Fullscreen template?"]?.Value<bool>() ?? false,
                    IsFullscreenUISet = jObject["Is Full Screen UI Set"]?.Value<bool>() ?? false,
                    UseExpertModeForInMenuUI = jObject["Use expert mode for In-Menu template?"]?.Value<bool>() ?? false,
                    IsInMenuUISet = jObject["Is In-Menu UI Set"]?.Value<bool>() ?? false
                };

                var fullscreenTemplate = jObject["Full Screen UI"]?["Template Name"]?.Value<string>();
                var inMenuTemplate = jObject["In-Menu UI"]?["Template Name"]?.Value<string>();

                var migrated = 0;

                if (migrateFullscreen && fullscreenTemplate != null)
                {
                    var template = FindTemplate(data.FullScreenTemplates, fullscreenTemplate);
                    if (template != null)
                    {
                        _uiData.IsFullscreenUISet = true;
                        _uiData.FullscreenUI = template.SettingsUI;
                        RegisterTemplateMessages(template.TemplateLang);
                        migrated++;
                    }
                }

                if (migrateInMenu && inMenuTemplate != null)
                {
                    var template = FindTemplate(data.InMenuTemplates, inMenuTemplate);
                    if (template != null)
                    {
                        _uiData.IsInMenuUISet = true;
                        _uiData.InMenuUI = template.SettingsUI;
                        RegisterTemplateMessages(template.TemplateLang);
                        migrated++;
                    }
                }

                if (migrated <= 0)
                {
                    PrintError("Migration failed: No templates migrated!");
                    _templateMigrationInProgress = false;
                    return;
                }

                SaveTemplate();
                _config.Version = Version;
                SaveConfig();

                PrintWarning($"Migration completed! Templates migrated: {migrated}");

                _templateMigrationInProgress = false;

                timer.In(1f, () => Interface.Oxide.ReloadPlugin(Name));
            });
        }

        private static ShopTemplate FindTemplate(ShopTemplate[] templates, string name)
        {
            if (templates == null) return null;

            for (var i = 0; i < templates.Length; i++)
                if (string.Equals(templates[i].SettingsUI.TemplateName, name, StringComparison.OrdinalIgnoreCase))
                    return templates[i];

            return null;
        }

        #endregion

        #region Data

        #region Players Data

        private Dictionary<ulong, PlayerData> _usersData = new();

        private class PlayerData
        {
            #region Fields

            [JsonProperty(PropertyName = "Cart")] public PlayerCartData PlayerCart = new();

            [JsonProperty(PropertyName = "NPC Cart")]
            public PlayerNPCCart NPCCart = new();

            [JsonProperty(PropertyName = "Limits")]
            public LimitData Limits = new();

            [JsonProperty(PropertyName = "Cooldowns")]
            public CooldownData Cooldowns = new();

            [JsonProperty(PropertyName = "Selected Economy")]
            public int SelectedEconomy;

            #endregion

            #region Helpers

            #region Economy

            public void SelectEconomy(int id)
            {
                SelectedEconomy = id;
            }

            public EconomyEntry GetEconomy()
            {
                if (Instance._economics.Count <= 1) return _config.Economy;

                return Instance._additionalEconomics.TryGetValue(SelectedEconomy, out var economyConf)
                    ? economyConf
                    : _config.Economy;
            }

            #endregion

            #endregion

            #region Classes

            public class CooldownData
            {
                #region Fields

                [JsonProperty(PropertyName = "Cooldowns", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public Dictionary<int, ItemData> Items = new();

                #endregion

                #region Helpers

                public ItemData GetCooldown(ShopItem item)
                {
                    return Items.GetValueOrDefault(item.ID);
                }

                public int GetCooldownTime(string player, ShopItem item, bool buy)
                {
                    var data = GetCooldown(item);
                    if (data == null) return -1;

                    return Convert.ToInt32(data.GetTime(buy).AddSeconds(item.GetCooldown(player, buy))
                        .Subtract(DateTime.UtcNow).TotalSeconds);
                }

                public bool HasCooldown(ShopItem item, bool buy)
                {
                    var data = GetCooldown(item);
                    if (data == null) return false;

                    return Convert.ToInt32(data.GetTime(buy).AddSeconds(buy ? item.BuyCooldown : item.SellCooldown)
                        .Subtract(DateTime.UtcNow).TotalSeconds) <= 0;
                }

                public void RemoveCooldown(ShopItem item)
                {
                    Items.Remove(item.ID);
                }

                public void SetCooldown(ShopItem item, bool buy)
                {
                    Items.TryAdd(item.ID, new ItemData());

                    if (buy)
                        Items[item.ID].LastBuyTime = DateTime.UtcNow;
                    else
                        Items[item.ID].LastSellTime = DateTime.UtcNow;
                }

                #endregion

                #region Classes

                public class ItemData
                {
                    public DateTime LastBuyTime = new(1970, 1, 1, 0, 0, 0);

                    public DateTime LastSellTime = new(1970, 1, 1, 0, 0, 0);

                    public DateTime GetTime(bool buy)
                    {
                        return buy ? LastBuyTime : LastSellTime;
                    }
                }

                #endregion
            }

            public class LimitData
            {
                #region Fields

                [JsonProperty(PropertyName = "Limits", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public Dictionary<int, ItemData> ItemsLimits = new();

                [JsonProperty(PropertyName = "Last Update Time")]
                public DateTime LastUpdate;

                [JsonProperty(PropertyName = "Daily Limits", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public Dictionary<int, ItemData> DailyItemsLimits = new();

                #endregion

                #region Helpers

                public void AddItem(ShopItem item, bool buy, int amount, bool daily = false)
                {
                    var totalAmount = item.Amount * amount;

                    var dict = daily ? DailyItemsLimits : ItemsLimits;

                    dict.TryAdd(item.ID, new ItemData());

                    if (buy)
                        dict[item.ID].Buy += totalAmount;
                    else
                        dict[item.ID].Sell += totalAmount;
                }

                public int GetLimit(ShopItem item, bool buy, bool daily = false)
                {
                    if (daily && DateTime.UtcNow.Date != LastUpdate.Date) // auto wipe
                    {
                        LastUpdate = DateTime.UtcNow;
                        DailyItemsLimits.Clear();
                    }

                    return (daily ? DailyItemsLimits : ItemsLimits).TryGetValue(item.ID, out var data)
                        ? buy ? data.Buy : data.Sell
                        : 0;
                }

                #endregion

                #region Classes

                public class ItemData
                {
                    public int Sell;

                    public int Buy;
                }

                #endregion
            }

            #endregion

            #region Storage

            private static string BaseFolder()
            {
                return "Shop" + Path.DirectorySeparatorChar + "Players" + Path.DirectorySeparatorChar;
            }

            public static PlayerData GetOrLoad(ulong userId)
            {
                if (!userId.IsSteamId()) return null;

                return GetOrLoad(BaseFolder(), userId);
            }

            public static PlayerData GetNotLoad(ulong userId)
            {
                if (!userId.IsSteamId()) return null;

                var data = GetOrLoad(BaseFolder(), userId, false);

                return data;
            }


            public static PlayerData GetOrLoad(string baseFolder, ulong userId, bool load = true)
            {
                if (Instance._usersData.TryGetValue(userId, out var data)) return data;

                try
                {
                    data = ReadOnlyObject(baseFolder + userId);
                }
                catch (Exception e)
                {
                    Interface.Oxide.LogError(e.ToString());
                }

                return load
                    ? Instance._usersData[userId] = data
                    : data;
            }

            public static PlayerData GetOrCreate(ulong userId)
            {
                if (!userId.IsSteamId()) return null;

                return GetOrLoad(userId) ?? (Instance._usersData[userId] = new PlayerData());
            }

            public static bool IsLoaded(ulong userId)
            {
                return Instance._usersData.ContainsKey(userId);
            }

            public static void Save()
            {
                Instance?._usersData?.Keys.ToList().ForEach(Save);
            }

            public static void Save(ulong userId)
            {
                if (!Instance._usersData.TryGetValue(userId, out var data))
                    return;

                Interface.Oxide.DataFileSystem.WriteObject(BaseFolder() + userId, data);
            }

            public static void SaveAndUnload(ulong userId)
            {
                Save(userId);

                Unload(userId);
            }

            public static void Unload(ulong userId)
            {
                Instance._usersData.Remove(userId);
            }

            #region Helpers

            public static string[] GetFiles()
            {
                return GetFiles(BaseFolder());
            }

            public static string[] GetFiles(string baseFolder)
            {
                try
                {
                    var json = ".json".Length;
                    var paths = Interface.Oxide.DataFileSystem.GetFiles(baseFolder);
                    for (var i = 0; i < paths.Length; i++)
                    {
                        var path = paths[i];
                        var separatorIndex = path.LastIndexOf(Path.DirectorySeparatorChar);

                        // We have to do this since GetFiles returns paths instead of filenames
                        // And other methods require filenames
                        paths[i] = path.Substring(separatorIndex + 1, path.Length - separatorIndex - 1 - json);
                    }

                    return paths;
                }
                catch
                {
                    return Array.Empty<string>();
                }
            }

            private static PlayerData ReadOnlyObject(string userId)
            {
                return Interface.Oxide.DataFileSystem.ExistsDatafile(userId)
                    ? Interface.Oxide.DataFileSystem.GetFile(userId).ReadObject<PlayerData>()
                    : null;
            }

            public static void DoWipe(string userId, bool carts, bool cooldowns, bool limits)
            {
                if (carts && cooldowns && limits)
                {
                    Interface.Oxide.DataFileSystem.DeleteDataFile(BaseFolder() + userId);
                }
                else
                {
                    if (!ulong.TryParse(userId, out var userID)) return;

                    var data = GetOrLoad(userID);
                    if (data == null) return;

                    if (carts)
                    {
                        data.PlayerCart = new PlayerCartData();
                        data.NPCCart = new PlayerNPCCart();
                    }

                    if (limits) data.Limits = new LimitData();

                    if (cooldowns) data.Cooldowns = new CooldownData();

                    SaveAndUnload(userID);
                }
            }

            #endregion

            #endregion
        }

        #endregion Players Data

        #region Carts

        private class CartData
        {
            #region Fields

            [JsonProperty(PropertyName = "Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<int, int> Items = new();

            #endregion

            #region Helpers

            #region Cart

            public void AddCartItem(ShopItem item, BasePlayer player)
            {
                int result;
                if (Items.TryGetValue(item.ID, out var amount))
                {
                    if (item.BuyMaxAmount > 0 && amount >= item.BuyMaxAmount) return;

                    if (!CanAddItemToCart(player, item, amount + 1, out result))
                    {
                        _config?.Notifications?.ShowNotify(player, result == 1 ? BuyLimitReached : DailyBuyLimitReached,
                            1,
                            item.GetPublicTitle(player));
                        return;
                    }

                    AddCartItemAmount(item, 1);
                }
                else
                {
                    if (!CanAddItemToCart(player, item, 1, out result))
                    {
                        _config?.Notifications?.ShowNotify(player, result == 1 ? BuyLimitReached : DailyBuyLimitReached,
                            1,
                            item.GetPublicTitle(player));
                        return;
                    }

                    AddCartItem(item, 1);
                }
            }

            private bool CanAddItemToCart(BasePlayer player, ShopItem item, int amount, out int result)
            {
                if (HasLimit(player, item, true, out var leftAmount) && amount > leftAmount) //total Limit
                {
                    result = 1;
                    return false;
                }

                if (HasLimit(player, item, true, out leftAmount, true) && amount > leftAmount) //daily Limit
                {
                    result = 2;
                    return false;
                }

                result = 0;
                return true;
            }


            public void ChangeAmountItem(BasePlayer player, ShopItem item, int amount)
            {
                if (amount > 0)
                {
                    if (HasLimit(player, item, true, out var totalLimit) && amount >= totalLimit)
                        amount = Math.Min(totalLimit, amount);

                    if (HasLimit(player, item, true, out var dailyLimit, true) && amount >= dailyLimit)
                        amount = Math.Min(dailyLimit, amount);

                    if (amount <= 0) return;

                    SetCartItemAmount(item, amount);
                }
                else
                {
                    RemoveCartItem(item);
                }
            }

            public int GetCartItemsAmount()
            {
                return SumShopItems(selector: (item, amount) => item.Amount * amount);
            }

            public double GetCartPrice(BasePlayer player, bool again = false)
            {
                var selectedEconomy = Instance.API_GetShopPlayerSelectedEconomy(player.userID);
                return SumShopItems(again, (item, amount) => item.GetPrice(player, selectedEconomy) * amount);
            }

            public int GetCartItemsStacksAmount()
            {
                return SumShopItems(selector: (item, amount) =>
                    item.Type == ItemType.Item && item.ItemDefinition != null
                        ? item.ProhibitSplit
                            ? 1
                            : item.GetStacks(amount).Count
                        : 0);
            }

            public void ClearCartItems()
            {
                Items.Clear();
            }

            public void RemoveCartItem(ShopItem item)
            {
                Items.Remove(item.ID);
            }

            public void AddCartItem(ShopItem item, int id)
            {
                Items.Add(item.ID, id);
            }

            public void AddCartItemAmount(ShopItem item, int amount)
            {
                Items[item.ID] += amount;
            }

            public void SetCartItemAmount(ShopItem item, int amount)
            {
                Items[item.ID] = amount;
            }

            #endregion

            public List<(ShopItem, int)> GetShopItemsList(bool lastItems = false, int skip = 0, int take = -1)
            {
                var list = Pool.Get<List<(ShopItem, int)>>();
                var index = 0;
                var taken = 0;

                foreach (var kvp in Items)
                {
                    if (Instance?._shopItems.TryGetValue(kvp.Key, out var shopItem) != true)
                        continue;

                    if (index++ < skip)
                        continue;

                    list.Add((shopItem, kvp.Value));
                    taken++;

                    if (take > 0 && taken >= take)
                        break;
                }

                return list;
            }

            public bool AnyShopItems(bool lastItems = false, Func<ShopItem, int, bool> selector = null)
            {
                if (selector == null) return false;

                var list = GetShopItemsList(lastItems);
                try
                {
                    foreach (var l in list)
                        if (selector(l.Item1, l.Item2))
                            return true;

                    return false;
                }
                finally
                {
                    Pool.FreeUnmanaged(ref list);
                }
            }

            public int SumShopItems(bool lastItems = false, Func<ShopItem, int, int> selector = null)
            {
                if (selector == null) return 0;

                var list = GetShopItemsList(lastItems);
                try
                {
                    var sum = 0;

                    foreach (var l in list) sum += selector(l.Item1, l.Item2);

                    return sum;
                }
                finally
                {
                    Pool.FreeUnmanaged(ref list);
                }
            }

            public double SumShopItems(bool lastItems = false, Func<ShopItem, int, double> selector = null)
            {
                if (selector == null) return 0.0;

                var list = GetShopItemsList(lastItems);
                try
                {
                    var sum = 0.0;

                    foreach (var l in list) sum += selector(l.Item1, l.Item2);

                    return sum;
                }
                finally
                {
                    Pool.FreeUnmanaged(ref list);
                }
            }

            public Dictionary<ShopItem, int> GetShopItems(bool lastItems = false)
            {
                var dict = new Dictionary<ShopItem, int>();
                foreach (var check in Items)
                {
                    var shopItem = Instance?.FindItemById(check.Key);
                    if (shopItem != null) dict.TryAdd(shopItem, check.Value);
                }

                return dict;
            }

            #endregion
        }

        private class PlayerNPCCart
        {
            [JsonProperty(PropertyName = "NPC Carts", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, CartData> Carts = new();
        }

        private class PlayerCartData : CartData
        {
            #region Fields

            [JsonProperty(PropertyName = "Last Purchase Items",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<int, int> LastPurchaseItems = new();

            [JsonProperty(PropertyName = "Favorite Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public HashSet<int> FavoriteItems = new();

            #endregion

            #region Helpers

            #region Favorite

            public bool AddItemToFavorite(int itemID)
            {
                return FavoriteItems.Add(itemID);
            }

            public bool IsFavoriteItem(int itemID)
            {
                return FavoriteItems.Contains(itemID);
            }

            public bool RemoveItemFromFavorites(int itemID)
            {
                return FavoriteItems.Remove(itemID);
            }

            public List<ShopItem> GetFavoriteItems(ulong userID, int skip = 0, int take = -1)
            {
                var list = Pool.Get<List<ShopItem>>();
                var index = 0;
                var taken = 0;

                foreach (var itemID in FavoriteItems)
                {
                    var shopItem = Instance?.FindItemById(itemID);
                    if (shopItem == null)
                        continue;

                    if (!shopItem.IsAvailableForPlayer(userID))
                        continue;

                    if (index++ < skip)
                        continue;

                    list.Add(shopItem);
                    taken++;

                    if (take > 0 && taken >= take)
                        break;
                }

                return list;
            }

            #endregion

            public void SaveLastPurchaseItems()
            {
                LastPurchaseItems = new Dictionary<int, int>(Items);
            }

            #endregion
        }

        #endregion

        #region Items Data

        private static ItemsData _itemsData;

        private class ItemsData
        {
            [JsonProperty(PropertyName = LangRu ? "Категории Магазина" : "Shop Categories",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ShopCategory> Shop = new();
        }

        private void SaveItemsData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Path.Combine(Name, "Shops", "Default"), _itemsData);
        }

        private void LoadItemsData()
        {
            try
            {
                _itemsData =
                    Interface.Oxide.DataFileSystem.ReadObject<ItemsData>(Path.Combine(Name, "Shops", "Default"));
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            _itemsData ??= new ItemsData();
        }

        #endregion Items Data

        #region UI Data

        private static UIData _uiData;

        private void SaveTemplate()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Path.Combine(Name, "UI"), _uiData);
        }

        private void LoadTemplate()
        {
            try
            {
                _uiData = ReadOnlyDataObject<UIData>(Path.Combine(Name, "UI"));
            }
            catch (Exception e)
            {
                PrintError("Loading UI Data exception: " + e);
            }

            _uiData ??= new UIData();

            if (!_uiData.IsFullscreenUISet || _uiData.FullscreenUI == null)
            {
                _initializedStatus = (false, "not_installed_template");
            }
            else
            {
                _uiData?.FullscreenUI?.LoadAllElements();

                _uiData?.InMenuUI?.LoadAllElements();
            }
        }

        private void BackupTemplateData()
        {
            var sourcePath = Path.Combine(Interface.Oxide.DataDirectory, Name, "UI.json");
            if (!File.Exists(sourcePath)) return;

            var backupDir = Path.Combine(Interface.Oxide.DataDirectory, Name, "Backups");
            Directory.CreateDirectory(backupDir);

            var backupPath = Path.Combine(backupDir, $"UI_backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json");
            File.Copy(sourcePath, backupPath);

            PrintWarning($"Template backup created: {backupPath}");
        }

        private T ReadOnlyDataObject<T>(string name)
        {
            var targetFile = Interface.Oxide.DataFileSystem.GetFile(name);
            if (targetFile == null || !targetFile.Exists()) return default;

            var settings = new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Error
            };
            var deserializedObject = JsonConvert.DeserializeObject<T>(File.ReadAllText(targetFile.Filename), settings);
            return deserializedObject ?? default;
        }

        private void RegisterImagesFromUI(ref Dictionary<string, string> imagesList)
        {
            if (_uiData == null) return;

            if (_uiData.IsFullscreenUISet && _uiData?.FullscreenUI?.templateImages != null)
                foreach (var image in _uiData?.FullscreenUI?.templateImages)
                    RegisterImage(image, ref imagesList);

            if (_uiData.IsInMenuUISet && _uiData?.InMenuUI?.templateImages != null)
                foreach (var image in _uiData?.InMenuUI?.templateImages)
                    RegisterImage(image, ref imagesList);
        }

        private class UIData
        {
            [JsonProperty(PropertyName = "Use expert mode for Fullscreen template?")]
            public bool UseExpertModeForFullscreenUI;

            [JsonProperty(PropertyName = "Is Full Screen UI Set")]
            public bool IsFullscreenUISet;

            [JsonProperty(PropertyName = "Full Screen UI")]
            public ShopUI FullscreenUI;

            [JsonProperty(PropertyName = "Use expert mode for In-Menu template?")]
            public bool UseExpertModeForInMenuUI;

            [JsonProperty(PropertyName = "Is In-Menu UI Set")]
            public bool IsInMenuUISet;

            [JsonProperty(PropertyName = "In-Menu UI")]
            public ShopUI InMenuUI;
        }

        public class ShopUI
        {
            #region Fields

            [JsonProperty(PropertyName = "Template Name")]
            public string TemplateName = null;

            [JsonProperty(PropertyName = "Select Currency Settings")]
            public SelectCurrencyUI SelectCurrency = new();

            [JsonProperty(PropertyName = "Background Settings")]
            public ShopBackgroundUI ShopBackground = new();

            [JsonProperty(PropertyName = "Shop Item Settings")]
            public ShopItemUI ShopItem = new();

            [JsonProperty(PropertyName = "Categories Settings")]
            public CategoriesUI ShopCategories = new();

            [JsonProperty(PropertyName = "Main Panel Settings")]
            public ShopContentUI ShopContent = new();

            [JsonProperty(PropertyName = "Basket Settings")]
            public ShopBasketUI ShopBasket = new();

            [JsonProperty(PropertyName = "No Items title")]
            public UiElement NoItems = new();

            [JsonProperty(PropertyName = "Shop Buy Modal Settings")]
            public ShopActionModalUI ShopBuyModal = new();

            [JsonProperty(PropertyName = "Shop Sell Modal Settings")]
            public ShopActionModalUI ShopSellModal = new();

            [JsonProperty(PropertyName = "Shop Item Description Modal Settings")]
            public ShopItemDescriptionModalUI ShopItemDescriptionModal = new();

            [JsonProperty(PropertyName = "Shop Confirmation Modal Settings")]
            public ShopConfirmationModalUI ShopConfirmationModal = new();

            #endregion

            #region Classes

            public class ShopBackgroundUI
            {
                #region Fields

                [JsonProperty(PropertyName = "Use background?")]
                public bool UseBackground;

                [JsonProperty(PropertyName = "Display type (Overlay/Hud)")]
                public string DisplayType;

                [JsonProperty(PropertyName = "Background")]
                public UiElement Background;

                [JsonProperty(PropertyName = "Close on click?")]
                public bool CloseOnClick;

                #endregion
            }

            public class ShopItemUI
            {
                #region Fields

                [JsonProperty(PropertyName = "Items On String")]
                public int ItemsOnString;

                [JsonProperty(PropertyName = "Strings")]
                public int Strings;

                [JsonProperty(PropertyName = "Item Height")]
                public float ItemHeight;

                [JsonProperty(PropertyName = "Item Width")]
                public float ItemWidth;

                [JsonProperty(PropertyName = "Margin")]
                public float Margin;

                [JsonProperty(PropertyName = "Background")]
                public UiElement Background;

                [JsonProperty(PropertyName = "Title")] public UiElement Title;

                [JsonProperty(PropertyName = "Blueprint")]
                public UiElement Blueprint;

                [JsonProperty(PropertyName = "Image")] public UiElement Image;

                [JsonProperty(PropertyName = "Amount")]
                public AmountUI Amount;

                [JsonProperty(PropertyName = "Favorite")]
                public FavoriteUI Favorite;

                [JsonProperty(PropertyName = "Info")] public IconButtonUI Info;

                [JsonProperty(PropertyName = "Discount")]
                public DiscountUI Discount;

                [JsonProperty(PropertyName = "Buy Button")]
                public ActionButtonUI BuyButton;

                [JsonProperty(PropertyName = "Buy Button (if there is no Sell button)")]
                public ActionButtonUI BuyButtonIfNoSell;

                [JsonProperty(PropertyName = "Sell Button")]
                public ActionButtonUI SellButton;

                [JsonProperty(PropertyName = "Sell Button (if there is no Buy button)")]
                public ActionButtonUI SellButtonIfNoBuy;

                [JsonProperty(PropertyName = "Admin Panel")]
                public AdminUI AdminPanel = new();

                #endregion

                #region Classes

                public class AdminUI
                {
                    [JsonProperty(PropertyName = "Additional margin to the item panel")]
                    public float AdditionalMargin;

                    [JsonProperty(PropertyName = "Background")]
                    public UiElement Background = new();

                    [JsonProperty(PropertyName = "Edit Button")]
                    public UiElement ButtonEdit = new();

                    [JsonProperty(PropertyName = "Move Right Button")]
                    public UiElement ButtonMoveRight = new();

                    [JsonProperty(PropertyName = "Move Left Button")]
                    public UiElement ButtonMoveLeft = new();
                }

                public class AmountUI
                {
                    [JsonProperty(PropertyName = "Title")] public UiElement Title;

                    [JsonProperty(PropertyName = "Background")]
                    public UiElement Background;

                    [JsonProperty(PropertyName = "Value")] public UiElement Value;
                }

                public class FavoriteUI
                {
                    [JsonProperty(PropertyName = "Add To Favorites")]
                    public IconButtonUI AddToFavorites;

                    [JsonProperty(PropertyName = "Remove From Favorites")]
                    public IconButtonUI RemoveFromFavorites;
                }

                public class DiscountUI
                {
                    [JsonProperty(PropertyName = "Background")]
                    public UiElement Background;

                    [JsonProperty(PropertyName = "Value")] public UiElement Value;
                }

                public class ActionButtonUI
                {
                    #region Fields

                    [JsonProperty(PropertyName = "Cooldown")]
                    public CooldownUI Cooldown;

                    [JsonProperty(PropertyName = "Background")]
                    public UiElement Background;

                    [JsonProperty(PropertyName = "Title")] public UiElement Title;

                    [JsonProperty(PropertyName = "Price")] public UiElement Price;

                    #endregion

                    #region Classes

                    public class CooldownUI
                    {
                        [JsonProperty(PropertyName = "Background")]
                        public UiElement Background;

                        [JsonProperty(PropertyName = "Title")] public UiElement Title;

                        [JsonProperty(PropertyName = "Left Time")]
                        public UiElement LeftTime;
                    }

                    #endregion
                }

                #endregion
            }

            public class CategoriesUI
            {
                #region Fields

                [JsonProperty(PropertyName = "Background")]
                public UiElement Background = new();

                [JsonProperty(PropertyName = "Header")]
                public HeaderUI Header = new();

                [JsonProperty(PropertyName = "Categories panel")]
                public UiElement CategoriesPanel = new();

                [JsonProperty(PropertyName = "Category Settings")]
                public ShopCategoryUI ShopCategory = new();

                [JsonProperty(PropertyName = "Use scroll in categories?")]
                public bool UseScrollCategories;

                [JsonProperty(PropertyName = "Scroll in categories")]
                public ScrollViewUI CategoriesScrollView = new();

                [JsonProperty(PropertyName = "Back Button")]
                public UiElement BackButton = new();

                [JsonProperty(PropertyName = "Next Button")]
                public UiElement NextButton = new();

                [JsonProperty(PropertyName = "Admin Panel")]
                public ShopCategoryAdminPanelUI CategoryAdminPanel = new();

                #endregion

                #region Classes

                public class ShopCategoryAdminPanelUI
                {
                    [JsonProperty(PropertyName = "Add Category Button")]
                    public UiElement ButtonAddCategory = new();

                    [JsonProperty(PropertyName = "Category Display Checkbox")]
                    public CheckBoxSettings CheckboxCategoriesDisplay = new();
                }

                public class ShopCategoryUI
                {
                    #region Fields

                    [JsonProperty(PropertyName = "Top indent for categories")]
                    public float TopIndent;

                    [JsonProperty(PropertyName = "Categories On Page")]
                    public int CategoriesOnPage;

                    [JsonProperty(PropertyName = "Left indent")]
                    public float LeftIndent;

                    [JsonProperty(PropertyName = "Width")] public float Width;

                    [JsonProperty(PropertyName = "Height")]
                    public float Height;

                    [JsonProperty(PropertyName = "Margin")]
                    public float Margin;

                    [JsonProperty(PropertyName = "Selected Category")]
                    public CategoryUI SelectedCategory = new();

                    [JsonProperty(PropertyName = "Category")]
                    public CategoryUI Category = new();

                    [JsonProperty(PropertyName = "Admin Panel")]
                    public AdminUI AdminPanel = new();

                    #endregion Fields

                    #region Classes

                    public class AdminUI
                    {
                        [JsonProperty(PropertyName = "Additional margin to the item panel")]
                        public float AdditionalMargin;

                        [JsonProperty(PropertyName = "Background")]
                        public UiElement Background = new();

                        [JsonProperty(PropertyName = "Edit Button")]
                        public UiElement ButtonEdit = new();

                        [JsonProperty(PropertyName = "Move UP Button")]
                        public IconButtonUI ButtonMoveUp = new();

                        [JsonProperty(PropertyName = "Move DOWN Button")]
                        public IconButtonUI ButtonMoveDown = new();
                    }

                    #endregion Classes
                }

                public class CategoryUI
                {
                    [JsonProperty(PropertyName = "Background")]
                    public UiElement Background = new();

                    [JsonProperty(PropertyName = "Title")] public UiElement Title = new();
                }

                public class HeaderUI
                {
                    [JsonProperty(PropertyName = "Header background")]
                    public UiElement Background = new();

                    [JsonProperty(PropertyName = "Title")] public UiElement Title = new();
                }

                #endregion
            }

            public class ShopBasketUI
            {
                #region Fields

                [JsonProperty(PropertyName = "Use Shop Basket?")]
                public bool UseShopBasket;

                [JsonProperty(PropertyName = "Background")]
                public UiElement Background = new();

                [JsonProperty(PropertyName = "Header")]
                public HeaderUI Header = new();

                [JsonProperty(PropertyName = "Content")]
                public ContentUI Content = new();

                [JsonProperty(PropertyName = "Footer")]
                public FooterUI Footer = new();

                [JsonProperty(PropertyName = "Basket Item")]
                public BasketItemUI BasketItem = new();

                [JsonProperty(PropertyName = "Show confirmation menu?")]
                public bool ShowConfirmMenu;

                #endregion

                #region Classes

                public class BasketItemUI
                {
                    [JsonProperty(PropertyName = "Items On Page")]
                    public int ItemsOnPage;

                    [JsonProperty(PropertyName = "Top indent")]
                    public float TopIndent;

                    [JsonProperty(PropertyName = "Left indent")]
                    public float LeftIndent;

                    [JsonProperty(PropertyName = "Width")] public float Width;

                    [JsonProperty(PropertyName = "Height")]
                    public float Height;

                    [JsonProperty(PropertyName = "Margin")]
                    public float Margin;

                    [JsonProperty(PropertyName = "Background")]
                    public UiElement Background = new();

                    [JsonProperty(PropertyName = "Show background for image?")]
                    public bool ShowImageBackground;

                    [JsonProperty(PropertyName = "Image Background")]
                    public UiElement ImageBackground = new();

                    [JsonProperty(PropertyName = "Blueprint Image")]
                    public UiElement ImageBlueprint = new();

                    [JsonProperty(PropertyName = "Item Image")]
                    public UiElement ImageItem = new();

                    [JsonProperty(PropertyName = "Title")] public UiElement Title = new();

                    [JsonProperty(PropertyName = "Item Amount")]
                    public UiElement ItemAmount = new();

                    [JsonProperty(PropertyName = "Remove Item Button")]
                    public UiElement ButtonRemoveItem = new();

                    [JsonProperty(PropertyName = "Plus Amount Button")]
                    public UiElement ButtonPlusAmount = new();

                    [JsonProperty(PropertyName = "Minus Amount Button")]
                    public UiElement ButtonMinusAmount = new();

                    [JsonProperty(PropertyName = "Amount input field")]
                    public UiElement InputAmount = new();
                }

                public class HeaderUI
                {
                    [JsonProperty(PropertyName = "Header background")]
                    public UiElement Background;

                    [JsonProperty(PropertyName = "Title")] public UiElement Title;

                    [JsonProperty(PropertyName = "Back button (used when scrolling is disabled)")]
                    public UiElement BackButton;

                    [JsonProperty(PropertyName = "Next button (used when scrolling is disabled)")]
                    public UiElement NextButton;
                }

                public class ContentUI
                {
                    #region Fields

                    [JsonProperty(PropertyName = "Background")]
                    public UiElement Background = new();

                    [JsonProperty(PropertyName = "Use scroll in shopping bag?")]
                    public bool UseScrollShoppingBag;

                    [JsonProperty(PropertyName = "Scroll in shopping bag")]
                    public ScrollViewUI ShoppingBagScrollView = new();

                    #endregion
                }

                public class FooterUI
                {
                    [JsonProperty(PropertyName = "Background")]
                    public UiElement Background;

                    [JsonProperty(PropertyName = "Buy Button (when the Buy Again button is available)")]
                    public UiElement BuyButtonWhenBuyAgain;

                    [JsonProperty(PropertyName = "Buy Button")]
                    public UiElement BuyButton;

                    [JsonProperty(PropertyName = "Buy Again Button")]
                    public IconButtonUI BuyAgainButton;

                    [JsonProperty(PropertyName = "Items Count (Title)")]
                    public UiElement ItemsCountTitle;

                    [JsonProperty(PropertyName = "Items Count (Value)")]
                    public UiElement ItemsCountValue;

                    [JsonProperty(PropertyName = "Items Cost (Title)")]
                    public UiElement ItemsCostTitle;

                    [JsonProperty(PropertyName = "Items Cost (Value)")]
                    public UiElement ItemsCostValue;
                }

                #endregion
            }

            public class ShopContentUI
            {
                #region Fields

                [JsonProperty(PropertyName = "Background")]
                public UiElement Background = new();

                [JsonProperty(PropertyName = "Header")]
                public HeaderUI Header = new();

                [JsonProperty(PropertyName = "Content")]
                public ContentUI Content = new();

                [JsonProperty(PropertyName = "Items Left Indent")]
                public float ItemsLeftIndent;

                [JsonProperty(PropertyName = "Items Top Indent")]
                public float ItemsTopIndent;

                #endregion

                #region Classes

                public class HeaderUI
                {
                    [JsonProperty(PropertyName = "Header background")]
                    public UiElement Background;

                    [JsonProperty(PropertyName = "Title")] public UiElement Title;

                    [JsonProperty(PropertyName = "Open Transfer Menu button")]
                    public UiElement ButtonTransfer;

                    [JsonProperty(PropertyName = "Toggle Economy Button")]
                    public UiElement ButtonToggleEconomy;

                    [JsonProperty(PropertyName = "Balance")]
                    public BalanceUI Balance;

                    [JsonProperty(PropertyName = "Search")]
                    public SearchUI Search;

                    [JsonProperty(PropertyName = "Use close button?")]
                    public bool UseCloseButton;

                    [JsonProperty(PropertyName = "Close button")]
                    public UiElement ButtonClose;

                    [JsonProperty(PropertyName = "Add Item Button")]
                    public UiElement ButtonAddItem;
                }

                public class SearchUI
                {
                    [JsonProperty(PropertyName = "Enabled")]
                    public bool Enabled;

                    [JsonProperty(PropertyName = "Background")]
                    public UiElement Background;

                    [JsonProperty(PropertyName = "Input field")]
                    public UiElement InputField;
                }

                public class BalanceUI
                {
                    [JsonProperty(PropertyName = "Background")]
                    public UiElement Background;

                    [JsonProperty(PropertyName = "Title")] public UiElement Title;

                    [JsonProperty(PropertyName = "Value")] public UiElement Value;
                }

                public class ContentUI
                {
                    #region Fields

                    [JsonProperty(PropertyName = "Use scroll to list items?")]
                    public bool UseScrollToListItems;

                    [JsonProperty(PropertyName = "Background")]
                    public UiElement Background;

                    [JsonProperty(PropertyName = "Scroll to list items")]
                    public ScrollViewUI ListItemsScrollView = new();

                    [JsonProperty(PropertyName = "Back button (when scrolling is off)")]
                    public UiElement ButtonBack;

                    [JsonProperty(PropertyName = "Next button (when scrolling is off)")]
                    public UiElement ButtonNext;

                    #endregion
                }

                #endregion
            }

            public class ShopActionModalUI
            {
                #region Fields

                [JsonProperty(PropertyName = "Display type (Overlay/Hud)")]
                public string DisplayType;

                [JsonProperty(PropertyName = "Background")]
                public UiElement Background;

                [JsonProperty(PropertyName = "Modal Panel")]
                public UiElement ModalPanel;

                [JsonProperty(PropertyName = "Header")]
                public UiElement Header;

                [JsonProperty(PropertyName = "Header Title")]
                public UiElement HeaderTitle;

                [JsonProperty(PropertyName = "Item Background")]
                public UiElement ItemBackground;

                [JsonProperty(PropertyName = "Item Icon")]
                public UiElement ItemIcon;

                [JsonProperty(PropertyName = "Item Name")]
                public UiElement ItemName;

                [JsonProperty(PropertyName = "Description Title")]
                public UiElement DescriptionTitle;

                [JsonProperty(PropertyName = "Item Description")]
                public UiElement ItemDescription;

                [JsonProperty(PropertyName = "Amount Title")]
                public UiElement AmountTitle;

                [JsonProperty(PropertyName = "Amount Value")]
                public UiElement AmountValue;

                [JsonProperty(PropertyName = "Price")] public UiElement Price;

                [JsonProperty(PropertyName = "Minus Amount Button")]
                public UiElement ButtonMinusAmount;

                [JsonProperty(PropertyName = "Plus Amount Button")]
                public UiElement ButtonPlusAmount;

                [JsonProperty(PropertyName = "Set Max Amount Button")]
                public UiElement ButtonSetMaxAmount;

                [JsonProperty(PropertyName = "Action Button (Buy/Sell)")]
                public UiElement ButtonAction;

                [JsonProperty(PropertyName = "Action Button With Price (optional)")]
                public ActionButtonWithPriceUI ButtonActionWithPrice;

                [JsonProperty(PropertyName = "Close Button (optional)")]
                public IconButtonUI CloseButton;

                #endregion

                #region Public Methods

                public void GetModal(BasePlayer player, ref List<string> elements, ShopItem item,
                    int amount,
                    string itemPrice,
                    string modalTitle,
                    string btnActionTitle,
                    string cmdInput = "",
                    string cmdMinus = "",
                    string cmdPlus = "",
                    string cmdMax = "",
                    string cmdAction = "",
                    string cmdClose = "")
                {
                    elements.Add(Background.GetSerialized(player, DisplayType, ModalLayer, ModalLayer));
                    elements.Add(ModalPanel.GetSerialized(player, ModalLayer, ModalLayer + ".Main",
                        ModalLayer + ".Main"));

                    elements.Add(ItemBackground.GetSerialized(player, ModalLayer + ".Main",
                        ModalLayer + ".Main.Item.Background"));

                    if (Header != null) elements.Add(Header.GetSerialized(player, ModalLayer + ".Main"));

                    elements.Add(HeaderTitle?.GetSerialized(player, ModalLayer + ".Main",
                        textFormatter: t => modalTitle));

                    elements.Add(ItemIcon?.GetSerialized(player, ModalLayer + ".Main.Item.Background",
                        customImageSettings: item.GetImage()));

                    elements.Add(ItemName?.GetSerialized(player, ModalLayer + ".Main",
                        textFormatter: t => item.GetPublicTitle(player)));

                    if (!string.IsNullOrWhiteSpace(item.Description))
                    {
                        elements.Add(DescriptionTitle?.GetSerialized(player, ModalLayer + ".Main",
                            textFormatter: t => Instance?.Msg(player, UIShopActionDescriptionTitle)));
                        elements.Add(ItemDescription?.GetSerialized(player, ModalLayer + ".Main",
                            textFormatter: t => Instance?.Msg(player, item.Description)));
                    }

                    elements.Add(AmountTitle?.GetSerialized(player, ModalLayer + ".Main",
                        textFormatter: t => Instance?.Msg(player, UIShopActionAmountTitle)));

                    elements.Add(AmountValue?.GetSerialized(player, ModalLayer + ".Main",
                        textFormatter: t => $"{amount * item.Amount}", cmdFormatter: t => cmdInput));

                    elements.Add(ButtonMinusAmount?.GetSerialized(player, ModalLayer + ".Main",
                        textFormatter: t => Instance?.Msg(player, MinusTitle), cmdFormatter: cmd => cmdMinus));
                    elements.Add(ButtonPlusAmount?.GetSerialized(player, ModalLayer + ".Main",
                        textFormatter: t => Instance?.Msg(player, PlusTitle), cmdFormatter: cmd => cmdPlus));
                    elements.Add(ButtonSetMaxAmount?.GetSerialized(player, ModalLayer + ".Main",
                        textFormatter: t => Instance?.Msg(player, TitleMax), cmdFormatter: cmd => cmdMax));

                    var formattedPrice = GetPlayerEconomy(player).GetPriceTitle(player, itemPrice);
                    if (ButtonActionWithPrice?.Enabled == true)
                    {
                        ButtonActionWithPrice.GetButton(player, ref elements, ModalLayer + ".Main", "ActionButton",
                            btnActionTitle,
                            formattedPrice,
                            cmdAction,
                            ModalLayer);
                    }
                    else
                    {
                        elements.Add(Price?.GetSerialized(player, ModalLayer + ".Main",
                            textFormatter: t => formattedPrice));

                        elements.Add(ButtonAction?.GetSerialized(player, ModalLayer + ".Main",
                            textFormatter: t => btnActionTitle, cmdFormatter: cmd => cmdAction, close: ModalLayer));
                    }

                    CloseButton?.GetButton(player, ref elements,
                        ModalLayer + ".Main",
                        ModalLayer + ".CloseButton",
                        Instance?.Msg(player, "CLOSE"),
                        cmdClose,
                        ModalLayer);
                }

                #endregion

                #region Classes

                public class ActionButtonWithPriceUI
                {
                    #region Fields

                    [JsonProperty(PropertyName = "Enabled")]
                    public bool Enabled;

                    [JsonProperty(PropertyName = "Background")]
                    public UiElement Background;

                    [JsonProperty(PropertyName = "Title")] public UiElement Title;

                    [JsonProperty(PropertyName = "Price")] public UiElement Price;

                    #endregion

                    #region Public Methods

                    public void GetButton(BasePlayer player, ref List<string> elements, string parent,
                        string buttonName,
                        string titleText, string priceText, string command, string close = "")
                    {
                        if (!Enabled) return;

                        var fullButtonName = parent + "." + buttonName;

                        elements.Add(Background?.GetSerialized(player, parent, fullButtonName, fullButtonName,
                            cmdFormatter: cmd => command, close: close));

                        elements.Add(Title?.GetSerialized(player, fullButtonName, textFormatter: t => titleText));

                        elements.Add(Price?.GetSerialized(player, fullButtonName, textFormatter: t => priceText));
                    }

                    #endregion
                }

                #endregion Classes
            }

            public class ShopConfirmationModalUI
            {
                #region Fields

                [JsonProperty(PropertyName = "Display type (Overlay/Hud)")]
                public string DisplayType;

                [JsonProperty(PropertyName = "Background")]
                public UiElement Background;

                [JsonProperty(PropertyName = "Close on click?")]
                public bool CloseOnClick;

                [JsonProperty(PropertyName = "Main Panel (optional)")]
                public UiElement MainPanel;

                [JsonProperty(PropertyName = "Title")] public UiElement Title;

                [JsonProperty(PropertyName = "Subtitle (optional)")]
                public UiElement Subtitle;

                [JsonProperty(PropertyName = "Warning Icon (optional)")]
                public UiElement WarningIcon;

                [JsonProperty(PropertyName = "Confirm Button")]
                public UiElement ButtonConfirm;

                [JsonProperty(PropertyName = "Cancel Button")]
                public UiElement ButtonCancel;

                #endregion Fields

                #region Public Methods

                public void GetModal(BasePlayer player,
                    ref List<string> elements,
                    (string title, string cmd) confirm,
                    (string title, string cmd) cancel)
                {
                    elements.Add(Background.GetSerialized(player, DisplayType, ModalLayer, ModalLayer));

                    if (CloseOnClick)
                        elements.Add(CuiJsonFactory.CreateButton(
                            parent: ModalLayer,
                            color: "0 0 0 0",
                            command: cancel.cmd ?? string.Empty,
                            close: ModalLayer));

                    var contentParent = ModalLayer;
                    if (MainPanel != null)
                    {
                        contentParent = ModalLayer + ".Main";
                        elements.Add(MainPanel.GetSerialized(player, ModalLayer, contentParent, contentParent));
                    }

                    elements.Add(WarningIcon?.GetSerialized(player, contentParent));

                    if (Title != null)
                        elements.Add(Title?.GetSerialized(player, contentParent,
                            textFormatter: t => Instance?.Msg(player, PurchaseConfirmation)));

                    if (Subtitle != null)
                        elements.Add(Subtitle?.GetSerialized(player, contentParent,
                            textFormatter: t => Instance?.Msg(player, PurchaseConfirmationSubtitle)));

                    elements.Add(ButtonConfirm?.GetSerialized(player, contentParent, textFormatter: t => confirm.title,
                        cmdFormatter: cmd => confirm.cmd, close: ModalLayer));
                    elements.Add(ButtonCancel?.GetSerialized(player, contentParent, textFormatter: t => cancel.title,
                        cmdFormatter: cmd => cancel.cmd, close: ModalLayer));
                }

                #endregion Public Methods
            }

            public class ShopItemDescriptionModalUI
            {
                #region Fields

                [JsonProperty(PropertyName = "Display type (Overlay/Hud)")]
                public string DisplayType;

                [JsonProperty(PropertyName = "Background")]
                public UiElement Background;

                [JsonProperty(PropertyName = "Close on click?")]
                public bool CloseOnClick;

                [JsonProperty(PropertyName = "Modal Panel")]
                public UiElement ModalPanel;

                [JsonProperty(PropertyName = "Title")] public UiElement Title;

                [JsonProperty(PropertyName = "Description")]
                public UiElement Description;

                [JsonProperty(PropertyName = "Close Button Icon")]
                public IconButtonUI ButtonClose;

                #endregion

                #region Public Methods

                public void GetModal(BasePlayer player, ref List<string> elements, ShopItem item,
                    string cmdClose = "")
                {
                    elements.Add(Background.GetSerialized(player, DisplayType, ModalLayer, ModalLayer));

                    if (CloseOnClick)
                        elements.Add(CuiJsonFactory.CreateButton(
                            parent: ModalLayer,
                            color: "0 0 0 0",
                            command: cmdClose,
                            close: ModalLayer));

                    elements.Add(ModalPanel.GetSerialized(player, ModalLayer, ModalLayer + ".Main",
                        ModalLayer + ".Main"));

                    elements.Add(Title?.GetSerialized(player, ModalLayer + ".Main",
                        textFormatter: t => Instance?.Msg(player, UIShopItemDescriptionTitle)));

                    elements.Add(Description?.GetSerialized(player, ModalLayer + ".Main",
                        textFormatter: t => Instance?.Msg(player, item.Description)));

                    ButtonClose?.GetButton(player, ref elements, ModalLayer + ".Main", ModalLayer + ".CloseButton",
                        Instance?.Msg(player, "CLOSE"), cmdClose, ModalLayer);
                }

                #endregion
            }

            #endregion

            #region Public Methods

            [JsonIgnore] public HashSet<string> templateImages = new();

            public void LoadAllElements()
            {
                templateImages?.Clear();
                var visited = new HashSet<object>();

                LoadElementsRecursive(this, ref visited, obj =>
                {
                    if (obj is IColor color)
                    {
                        color.InvalidateCache();
                        return true; // stop recursion for this branch
                    }

                    if (obj is UiElement uiElement && uiElement.TryGetImage(out var image))
                    {
                        templateImages.Add(image);
                        return true;
                    }

                    return false; // continue recursion
                });
            }

            public List<UiElement> GetAllUiElements()
            {
                var allUiElements = new List<UiElement>();
                var visited = new HashSet<object>();

                LoadElementsRecursive(this, ref visited, obj =>
                {
                    if (obj is UiElement element)
                    {
                        allUiElements.Add(element);
                        return true;
                    }

                    return false;
                });

                return allUiElements;
            }

            private static void LoadElementsRecursive(object obj, ref HashSet<object> visited,
                Func<object, bool> processor)
            {
                if (obj == null || visited.Contains(obj)) return;

                if (processor(obj)) return;

                var type = obj.GetType();
                if (type.IsPrimitive || type == typeof(string)) return;

                visited.Add(obj);

                foreach (var field in type.GetFields())
                    try
                    {
                        var value = field.GetValue(obj);
                        if (value == null) continue;

                        LoadElementsRecursive(value, ref visited, processor);
                    }
                    catch
                    {
                        // ignore
                    }
            }

            #endregion
        }

        #endregion UI Data

        #region Helpers

        #region Players

        private static IEnumerator StartOnPlayers(string[] players, Action<string> callback = null)
        {
            for (var i = 0; i < players.Length; i++)
            {
                callback?.Invoke(players[i]);

                if (i % 10 == 0)
                    yield return CoroutineEx.waitForFixedUpdate;
            }
        }

        private static IEnumerator StartOnPlayers(List<ulong> players, Action<ulong> callback = null)
        {
            for (var i = 0; i < players.Count; i++)
            {
                callback?.Invoke(players[i]);

                if (i % 10 == 0)
                    yield return CoroutineEx.waitForFixedUpdate;
            }
        }

        #endregion

        #region All Players Data

        private IEnumerator SaveCachedPlayersData()
        {
            var players = _usersData.Keys.ToList();
            if (players.Count > 0)
                yield return StartOnPlayers(players, PlayerData.Save);
            else
                yield return null;
        }

        private IEnumerator UnloadOfflinePlayersData()
        {
            foreach (var userID in _usersData.Keys.ToList())
                if (!BasePlayer.TryFindByID(userID, out _))
                    PlayerData.Unload(userID);

            yield return null;
        }

        #endregion

        #region Wipe Players

        private Coroutine _wipePlayers;

        private void StartWipePlayers(bool carts, bool cooldowns, bool limits)
        {
            try
            {
                var players = PlayerData.GetFiles();
                if (players is not {Length: > 0})
                {
                    PrintError("[On Server Wipe] in wipe players, no players found!");
                    return;
                }

                _wipePlayers =
                    Global.Runner.StartCoroutine(StartOnPlayers(players,
                        userID => PlayerData.DoWipe(userID, carts, cooldowns, limits)));

                _usersData?.Clear();

                Puts("You have wiped player data!");
            }
            catch (Exception e)
            {
                PrintError($"[On Server Wipe] in wipe players, error: {e.Message}");
            }
        }

        private void StopWipePlayers()
        {
            if (_wipePlayers != null)
                Global.Runner.StopCoroutine(_wipePlayers);
        }

        #endregion

        #region Limits

        private void UseLimit(BasePlayer player, ShopItem item, bool buy, int amount, bool daily = false)
        {
            PlayerData.GetOrCreate(player.userID).Limits.AddItem(item, buy, amount, daily);
        }

        private int GetLimit(BasePlayer player, ShopItem item, bool buy, bool daily = false)
        {
            var hasLimit = item.GetLimit(player, buy, daily);
            if (hasLimit == 0)
                return 1;

            var used = PlayerData.GetOrCreate(player.userID).Limits.GetLimit(item, buy, daily);
            return hasLimit - used;
        }

        private static bool HasLimit(BasePlayer player, ShopItem item, bool buy, out int leftAmount, bool daily = false)
        {
            var hasLimit = item.GetLimit(player, buy, daily);
            if (hasLimit == 0)
            {
                leftAmount = 0;
                return false;
            }

            var used = PlayerData.GetOrCreate(player.userID).Limits.GetLimit(item, buy, daily);
            leftAmount = hasLimit - used;
            return true;
        }

        #endregion

        #region Cooldowns

        private PlayerData.CooldownData GetCooldown(ulong player)
        {
            return PlayerData.GetOrCreate(player).Cooldowns;
        }

        private PlayerData.CooldownData.ItemData GetCooldown(ulong player, ShopItem item)
        {
            return GetCooldown(player)?.GetCooldown(item);
        }

        private int GetCooldownTime(ulong player, ShopItem item, bool buy)
        {
            return GetCooldown(player)?.GetCooldownTime(player.ToString(), item, buy) ?? -1;
        }

        private bool HasCooldown(ulong player, ShopItem item, bool buy)
        {
            return GetCooldown(player)?.HasCooldown(item, buy) ?? false;
        }

        private void SetCooldown(BasePlayer player, ShopItem item, bool buy)
        {
            if (item.GetCooldown(player.UserIDString, buy) <= 0) return;

            GetCooldown(player.userID)?.SetCooldown(item, buy);
        }

        private void RemoveCooldown(ulong player, ShopItem item)
        {
            var data = PlayerData.GetOrLoad(player);
            if (data == null || data.Cooldowns.Items.Count == 0) return;

            data.Cooldowns.RemoveCooldown(item);
        }

        #endregion

        #region Economy

        private static EconomyEntry GetPlayerEconomy(BasePlayer player)
        {
            return PlayerData.GetOrCreate(player.userID)?.GetEconomy();
        }

        private static void SelectPlayerEconomy(BasePlayer player, int id)
        {
            PlayerData.GetOrCreate(player.userID)?.SelectEconomy(id);
        }

        #endregion

        #endregion

        #endregion

        #region Hooks

        private void Init()
        {
            Instance = this;

            Unsubscribe(nameof(CanLootEntity));
            Unsubscribe(nameof(OnUseNPC));

#if TESTING
            StopwatchWrapper.OnComplete = DebugMessage;
#endif
        }

        private void OnServerInitialized()
        {
            if (_templateMigrationInProgress)
            {
                PrintWarning("Waiting for template migration...");
                return;
            }

            LoadItemsData();

            RegisterPermissions();

            CheckOnDuplicates();

            LoadEconomics();

            if (_config.CustomVending.Count > 0)
                Subscribe(nameof(CanLootEntity));

            if (_config.NPCs.Count > 0)
                Subscribe(nameof(OnUseNPC));

            LoadTemplate();

            LoadNPCs();

            LoadImages();

            LoadItems();

            CacheImages();

            LoadPlayers();

            LoadCustomVMs();

            RegisterCommands();

            CheckInitializationStatus();
        }

        private void Unload()
        {
            try
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    CuiHelper.DestroyUi(player, Layer);
                    CuiHelper.DestroyUi(player, Layer + ".Background");
                    CuiHelper.DestroyUi(player, ModalLayer);
                    CuiHelper.DestroyUi(player, EditingLayer);

                    OnPlayerDisconnected(player);
                }

                StopWipePlayers();
            }
            finally
            {
                _config = null;

                Instance = null;

                _itemsData = null;

                _uiData = null;
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (_templateMigrationInProgress) return;

            if (player == null || player.IsNpc) return;

            if (_config.LoginImages)
                _coroutines[player.userID] = ServerMgr.Instance.StartCoroutine(LoadImages(player));

            PlayerData.GetOrLoad(player.userID);
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (_templateMigrationInProgress) return;

            if (player == null) return;

            PlayerData.SaveAndUnload(player.userID);

            CloseShopUI(player);

            _lastCommandTime.Remove(player.userID);

            if (_coroutines.TryGetValue(player.userID, out var coroutine) && coroutine != null)
            {
                ServerMgr.Instance.StopCoroutine(coroutine);

                _coroutines.Remove(player.userID);
            }
        }

        private void OnNewSave()
        {
            if (_config.Wipe.Players || _config.Wipe.Cooldown || _config.Wipe.Limits)
                StartWipePlayers(_config.Wipe.Players, _config.Wipe.Cooldown, _config.Wipe.Limits);
        }

        #region Server Panel

        private void OnServerPanelCategoryPage(BasePlayer player, int category, int page)
        {
            if (_templateMigrationInProgress) return;

            CloseShopUI(player);
        }

        private void OnServerPanelClosed(BasePlayer player)
        {
            if (_templateMigrationInProgress) return;

            CloseShopUI(player);
        }

        private void OnReceiveCategoryInfo(int categoryID)
        {
            if (_templateMigrationInProgress) return;

            _serverPanelCategory.categoryID = categoryID;
        }

        #endregion

        #region Image Library

        private void OnPluginLoaded(Plugin plugin)
        {
            if (_templateMigrationInProgress) return;

            switch (plugin.Name)
            {
                case nameof(ImageLibrary):
                    timer.In(1, LoadImages);
                    break;
                case nameof(ServerPanel):
                    timer.In(1, LoadServerPanel);
                    break;
            }
        }

        private void OnPluginUnloaded(Plugin plugin)
        {
            if (_templateMigrationInProgress) return;

            switch (plugin.Name)
            {
                case nameof(ServerPanel):
                    _serverPanelCategory.spStatus = false;
                    break;
            }
        }

        #endregion

        #region Vending Machine

        private object CanLootEntity(BasePlayer player, VendingMachine vendingMachine)
        {
            if (_templateMigrationInProgress) return null;

            if (player == null || vendingMachine == null ||
                !_config.CustomVending.TryGetValue(vendingMachine.net.ID.Value, out var customVending)) return null;

            if (!string.IsNullOrEmpty(customVending.Permission) && !player.HasPermission(customVending.Permission))
            {
                _config?.Notifications?.ShowNotify(player, NoPermission, 1);
                return false;
            }

            _openedCustomVending[player.userID] = customVending;

            ShowShopUI(player, true);
            return false;
        }

        #endregion

        #region NPC

        private void OnUseNPC(BasePlayer npc, BasePlayer player)
        {
            if (_templateMigrationInProgress) return;

            if (npc == null || player == null) return;

            if (!_config.NPCs.TryGetValue(npc.UserIDString, out var npcShop) || npcShop == null) return;

            if (!string.IsNullOrEmpty(npcShop.Permission) && !player.HasPermission(npcShop.Permission))
            {
                _config?.Notifications?.ShowNotify(player, NoPermission, 1);
                return;
            }

            _openedShopsNPC[player.userID] = npcShop;

            ShowShopUI(player, true);
        }

        #endregion

        #endregion

        #region Commands

        [ConsoleCommand("openshopUI")]
        private void OpenShopUI(ConsoleSystem.Arg arg)
        {
            if (_templateMigrationInProgress) return;

            var player = arg?.Player();
            if (player == null) return;

            if (_openSHOP.ContainsKey(player.userID))
            {
                CuiHelper.DestroyUi(player, Layer);

                CloseShopUI(player);
            }
            else
            {
                ShowShopUI(player, true);
            }
        }

        private void CmdShopOpen(IPlayer cov, string command, string[] args)
        {
            if (_templateMigrationInProgress) return;

            var player = cov?.Object as BasePlayer;
            if (player == null) return;

            if (_initializedStatus.status is false)
            {
                if (_initializedStatus.message == null)
                {
                    _config?.Notifications?.ShowNotify(player, UIMsgShopInInitialization, 1);
                    return;
                }

                _config?.Notifications?.ShowNotify(player, NoILError, 1);

                var adminMSG = ConvertInitializedStatus();

                if (IsAdmin(player))
                    _config?.Notifications?.ShowNotify(player, adminMSG, 1);

                PrintError(adminMSG);
                return;
            }

            if (!string.IsNullOrWhiteSpace(_config.Permission) && !cov.HasPermission(_config.Permission))
            {
                _config?.Notifications?.ShowNotify(player, NoPermission, 1);
                return;
            }

            if (_config.UseDuels && InDuel(player))
            {
                _config?.Notifications?.ShowNotify(player, NoUseDuel, 1);
                return;
            }

            if (_serverPanelCategory.spStatus && _serverPanelCategory.categoryID != -1)
                ServerPanel?.Call("API_OnServerPanelOpenCategoryByID", player, _serverPanelCategory.categoryID);
            else
                ShowShopUI(player, true);
        }

        private void CmdSetCustomVM(IPlayer cov, string command, string[] args)
        {
            if (_templateMigrationInProgress) return;

            var player = cov?.Object as BasePlayer;
            if (player == null) return;

            if (!player.HasPermission(PERM_SET_VM))
            {
                _config?.Notifications?.ShowNotify(player, NoPermission, 1);
                return;
            }

            if (args.Length == 0)
            {
                _config?.Notifications?.ShowNotify(player, ErrorSyntax, 1, $"{command} [categories: cat1 cat2 ...]");
                return;
            }

            var categories = args.ToList();
            categories.RemoveAll(cat => !_itemsData.Shop.Exists(confCat => confCat.GetTitle(player) == cat));
            if (categories.Count == 0)
            {
                _config?.Notifications?.ShowNotify(player, VMNotFoundCategories, 1);
                return;
            }

            var workbench = GetLookVM(player);
            if (workbench == null)
            {
                _config?.Notifications?.ShowNotify(player, VMNotFound, 1);
                return;
            }

            if (_config.CustomVending.ContainsKey(workbench.net.ID.Value))
            {
                _config?.Notifications?.ShowNotify(player, VMExists, 1);
                return;
            }

            var conf = new CustomVendingEntry
            {
                Categories = categories
            };

            _config.CustomVending[workbench.net.ID.Value] = conf;

            SaveConfig();

            _config?.Notifications?.ShowNotify(player, VMInstalled, 0);

            Subscribe(nameof(CanLootEntity));
        }

        [ConsoleCommand("shop.item")]
        private void CmdShopItem(ConsoleSystem.Arg arg)
        {
            if (_templateMigrationInProgress) return;

            if (arg.Player() != null || !arg.IsServerside)
            {
                if (IsAdmin(arg.Player())) SendReply(arg, "This command can only be run from the SERVER CONSOLE.");

                return;
            }

            void defaultInfo()
            {
                SendReply(arg, "shop.item itemShopID(-1073461450) price type(sell,buy) action(-|=|+) amount(100)");
            }

            if (!arg.HasArgs() || arg.Args.Length < 2)
            {
                defaultInfo();
                return;
            }

            if (!int.TryParse(arg.GetString(0), out var itemID))
            {
                SendReply(arg, $"{arg.GetString(0)} is not itemID");
                return;
            }

            var item = FindItemById(itemID);

            if (item == null)
            {
                SendReply(arg, $"Could not found item with itemID : {itemID}");
                return;
            }

            switch (arg.Args[1])
            {
                case "price":
                {
                    if (arg.Args.Length < 5)
                    {
                        defaultInfo();
                        break;
                    }

                    switch (arg.Args[2])
                    {
                        case "sell":
                        {
                            if (!int.TryParse(arg.GetString(4), out var price) || price <= 0)
                            {
                                SendReply(arg, $"{arg.GetString(4)} is not number or less then 1");
                                return;
                            }

                            switch (arg.GetString(3))
                            {
                                case "+":
                                {
                                    item.SellPrice += price;
                                    break;
                                }

                                case "-":
                                {
                                    if (item.SellPrice - price < 0)
                                        return;

                                    item.SellPrice -= price;
                                    break;
                                }

                                case "=":
                                {
                                    item.SellPrice = price;
                                    break;
                                }

                                default:
                                {
                                    defaultInfo();
                                    return;
                                }
                            }

                            LoadItems();
                            SaveItemsData();
                            SendReply(arg, $"{item.GetName()} set sell price to {item.SellPrice}");
                            break;
                        }

                        case "buy":
                        {
                            if (!int.TryParse(arg.GetString(4), out var price) || price <= 0)
                            {
                                SendReply(arg, $"{arg.GetString(4)} is not number or less then 1");
                                return;
                            }

                            switch (arg.GetString(3))
                            {
                                case "+":
                                {
                                    item.Price += price;
                                    break;
                                }

                                case "-":
                                {
                                    if (item.Price - price < 0)
                                        return;

                                    item.Price -= price;
                                    break;
                                }

                                case "=":
                                {
                                    item.Price = price;
                                    break;
                                }

                                default:
                                {
                                    defaultInfo();
                                    return;
                                }
                            }

                            LoadItems();
                            SaveItemsData();
                            SendReply(arg, $"{item.GetName()} set price to {item.Price}");
                            break;
                        }

                        default:
                        {
                            defaultInfo();
                            break;
                        }
                    }

                    break;
                }
                default:
                {
                    defaultInfo();
                    break;
                }
            }
        }

        private void CmdSetShopNPC(IPlayer cov, string command, string[] args)
        {
            if (_templateMigrationInProgress) return;

            var player = cov?.Object as BasePlayer;
            if (player == null) return;

            if (!player.HasPermission(PERM_SET_NPC))
            {
                _config?.Notifications?.ShowNotify(player, NoPermission, 1);
                return;
            }

            if (args.Length == 0)
            {
                _config?.Notifications?.ShowNotify(player, ErrorSyntax, 1, $"{command} [categories: cat1 cat2 ...]");
                return;
            }

            var categories = args.ToList();

            for (var i = 0; i < categories.Count; i++)
                categories[i] = categories[i].TrimEnd(',');

            categories.RemoveAll(cat => !_itemsData.Shop.Exists(confCat => confCat.GetTitle(player) == cat));
            if (categories.Count == 0)
            {
                _config?.Notifications?.ShowNotify(player, VMNotFoundCategories, 1);
                return;
            }

            var npc = GetLookNPC(player);
            if (npc == null)
            {
                _config?.Notifications?.ShowNotify(player, NPCNotFound, 1);
                return;
            }

            if (_config.NPCs.ContainsKey(npc.UserIDString))
            {
                _config?.Notifications?.ShowNotify(player, VMExists, 1);
                return;
            }

            var conf = new NPCShop
            {
                Categories = categories,
                BotID = npc.UserIDString
            };

            _config.NPCs[npc.UserIDString] = conf;

            SaveConfig();

            _config?.Notifications?.ShowNotify(player, NPCInstalled, 0);
        }

        [ConsoleCommand(CmdMainConsole)]
        private void CmdConsoleShop(ConsoleSystem.Arg arg)
        {
            if (_templateMigrationInProgress) return;

            var player = arg?.Player();
            if (player == null || !arg.HasArgs()) return;

            if (IsRateLimited(player)) return;

#if TESTING
            try
            {
#endif
            switch (arg.Args[0])
            {
                case "closeui":
                {
                    CloseShopUI(player);
                    break;
                }

                case "shop_search_input":
                {
                    var search = string.Empty;
                    if (arg.HasArgs(2)) search = string.Join(" ", arg.Args.Skip(1));

                    if (search == Msg(player, SearchTitle))
                        return;

                    var shop = GetShop(player);
                    shop.OnChangeSearch(search, 0);

                    UpdateUI(player, (List<string> container) =>
                    {
                        CategoriesListUI(ref container, player);

                        ShopContentUI(player, ref container);

                        ShopHeaderUI(ref container, player);
                    });
                    break;
                }

                case "shop_search_page":
                {
                    if (!arg.HasArgs(2)) return;

                    var searchPage = arg.GetInt(1);

                    var shop = GetShop(player);
                    shop.OnChangeSearch(shop.search, searchPage);

                    UpdateUI(player, (List<string> container) => { ShopContentUI(player, ref container); });
                    break;
                }

                case "shop_page":
                {
                    if (!arg.HasArgs(2)) return;

                    var shopPage = arg.GetInt(1);

                    var shop = GetShop(player);
                    shop.OnChangeShopPage(shopPage);

                    UpdateUI(player, (List<string> container) => { ShopContentUI(player, ref container); });
                    break;
                }

                case "main_page":
                {
                    if (!arg.HasArgs(3) ||
                        !int.TryParse(arg.Args[1], out var categoryPage) ||
                        !int.TryParse(arg.Args[2], out var targetShopPage)) return;

                    var search = string.Empty;
                    if (arg.HasArgs(4)) search = string.Join(" ", arg.Args.Skip(3));

                    if (string.IsNullOrEmpty(search) && categoryPage == -1)
                        categoryPage = 0;

                    var shop = GetShop(player);
                    shop.OnChangeCategory(categoryPage, targetShopPage);

                    UpdateUI(player, (List<string> container) => { ShopContentUI(player, ref container); });
                    break;
                }

                case "buyitem":
                {
                    if (!arg.HasArgs(2) ||
                        !int.TryParse(arg.Args[1], out var id)) return;

                    if (!TryFindItemById(id, out var shopItem))
                        return;

                    if (!CanAccesToItem(player, shopItem))
                    {
                        _config?.Notifications?.ShowNotify(player, UIDLCItem, 1);
                        return;
                    }

                    var playerCart = GetPlayerCart(player.userID);
                    if (playerCart == null) return;


                    playerCart.AddCartItem(shopItem, player);

                    var cooldownTime = GetCooldownTime(player.userID, shopItem, true);
                    if (cooldownTime > 0)
                    {
                        _config?.Notifications?.ShowNotify(player, BuyCooldownMessage, 1,
                            shopItem.GetPublicTitle(player),
                            FormatShortTime(player, TimeSpan.FromSeconds(cooldownTime)));
                        return;
                    }

                    UpdateUI(player, (List<string> container) => { ShopBasketUI(ref container, player); });
                    break;
                }

                case "categories_change_local_page":
                {
                    if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out var targetCategoriesPage)) return;

                    var shop = GetShop(player);
                    shop?.Update();
                    shop?.OnChangeCategoriesPage(targetCategoriesPage);

                    UpdateUI(player, (List<string> container) => { ShopCategoriesUI(ref container, player); });
                    break;
                }

                case "categories_change":
                {
                    if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out var targetCategoryIndex)) return;

                    var shop = GetShop(player);

                    shop.OnChangeCategory(targetCategoryIndex, 0);

                    UpdateUI(player, (List<string> container) =>
                    {
                        CategoriesListUI(ref container, player);

                        ShopContentUI(player, ref container);

                        ShopHeaderUI(ref container, player);
                    });
                    break;
                }

                case "cart_page":
                {
                    if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out var page)) return;

                    GetShop(player)?.OnChangeBasketPage(page);

                    UpdateUI(player, (List<string> container) => { ShopBasketUI(ref container, player); });
                    break;
                }

                case "cart_item_remove":
                {
                    if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out var id)) return;

                    if (!TryFindItemById(id, out var shopItem))
                        return;

                    var playerCart = GetPlayerCart(player.userID);
                    if (playerCart == null) return;

                    playerCart.RemoveCartItem(shopItem);

                    UpdateUI(player, (List<string> container) => { ShopBasketUI(ref container, player); });
                    break;
                }

                case "cart_item_change":
                {
                    if (!arg.HasArgs(3) ||
                        !int.TryParse(arg.Args[1], out var index) ||
                        !int.TryParse(arg.Args[2], out var itemID) ||
                        !int.TryParse(arg.Args[3], out var amount)) return;

                    if (!TryFindItemById(itemID, out var shopItem))
                        return;

                    var playerCart = GetPlayerCart(player.userID);
                    if (playerCart == null) return;

                    if (shopItem.BuyMaxAmount > 0 && amount > shopItem.BuyMaxAmount)
                        amount = shopItem.BuyMaxAmount;

                    playerCart.ChangeAmountItem(player, shopItem, amount);

                    UpdateUI(player, (List<string> container) =>
                    {
                        if (amount > 0)
                        {
                            ShopCartItemUI(player, ref container, index, shopItem, amount);

                            UpdateShopCartFooterUI(ref container, player, playerCart);
                        }
                        else
                        {
                            ShopBasketUI(ref container, player);
                        }
                    });
                    break;
                }

                case "cart_try_buyitems":
                {
                    UpdateUI(player, container =>
                    {
                        GetShop(player)?.GetUI()?.ShopConfirmationModal?.GetModal(player, ref container,
                            (Msg(player, BuyTitle), $"{CmdMainConsole} cart_buyitems"),
                            (Msg(player, CancelTitle), string.Empty));
                    });
                    break;
                }

                case "cart_buyitems":
                {
                    TryBuyItems(player);
                    break;
                }

                case "fastbuyitem":
                {
                    if (!arg.HasArgs(3) ||
                        !int.TryParse(arg.Args[1], out var itemId) ||
                        !int.TryParse(arg.Args[2], out var amount) ||
                        amount <= 0) return;

                    var item = FindItemById(itemId);
                    if (item == null || !item.CanBuy ||
                        !(item.ForceBuy || !GetShop(player).GetUI().ShopBasket.UseShopBasket)) return;

                    if (_config.BlockNoEscape)
                        if (NoEscape_IsBlocked(player))
                        {
                            ErrorUi(player, Msg(player, BuyRaidBlocked));
                            return;
                        }

                    var secondsFromWipe = GetSecondsFromWipe();
                    if (_config.WipeCooldown)
                    {
                        var timeLeft = Mathf.RoundToInt(_config.WipeCooldownTimer - secondsFromWipe);
                        if (timeLeft > 0)
                        {
                            ErrorUi(player, Msg(player, BuyWipeCooldown, FormatShortTime(timeLeft)));
                            return;
                        }
                    }

                    if (item.BuyBlockDurationAfterWipe > 0)
                    {
                        var timeLeft = Mathf.RoundToInt(item.BuyBlockDurationAfterWipe - secondsFromWipe);
                        if (timeLeft > 0)
                        {
                            ErrorUi(player, Msg(player, BuyWipeCooldown, FormatShortTime(timeLeft)));
                            return;
                        }
                    }

                    if (_config.RespawnCooldown)
                    {
                        var timeLeft = Mathf.RoundToInt(_config.RespawnCooldownTimer - player.TimeAlive());
                        if (timeLeft > 0)
                        {
                            ErrorUi(player,
                                Msg(player, BuyRespawnCooldown,
                                    FormatShortTime(timeLeft)));
                            return;
                        }
                    }


                    if (!CanAccesToItem(player, item))
                    {
                        _config?.Notifications?.ShowNotify(player, UIDLCItem, 1);
                        return;
                    }

                    if (item.Type == ItemType.Item)
                    {
                        var totalAmount = item.GetStacks(amount).Count;

                        var slots = player.inventory.containerBelt.capacity -
                                    player.inventory.containerBelt.itemList.Count +
                                    (player.inventory.containerMain.capacity -
                                     player.inventory.containerMain.itemList.Count);
                        if (slots < totalAmount)
                        {
                            ErrorUi(player, Msg(player, NotEnoughSpace));
                            return;
                        }
                    }

                    var limit = GetLimit(player, item, true);
                    if (limit <= 0)
                    {
                        ErrorUi(player, Msg(player, BuyLimitReached, item.GetPublicTitle(player)));
                        return;
                    }

                    limit = GetLimit(player, item, true, true);
                    if (limit <= 0)
                    {
                        ErrorUi(player, Msg(player, DailyBuyLimitReached, item.GetPublicTitle(player)));
                        return;
                    }

                    var selectedEconomy = API_GetShopPlayerSelectedEconomy(player.userID);

                    var price = item.GetPrice(player, selectedEconomy) * amount;
                    var playerEconomy = GetPlayerEconomy(player);
                    if (!player.HasPermission(PERM_FREE_BYPASS) &&
                        !playerEconomy.RemoveBalance(player, price))
                    {
                        ErrorUi(player, Msg(player, NotMoney));
                        return;
                    }

                    item.Get(player, amount);

                    SetCooldown(player, item, true);
                    UseLimit(player, item, true, amount);
                    UseLimit(player, item, true, amount, true);

                    LogBuySell(player, item, amount, price, true);

                    UpdateUI(player, (List<string> container) =>
                    {
                        BalanceUi(ref container, player);

                        ItemUI(player, item, ref container);
                    });

                    _config?.Notifications?.ShowNotify(player, ReceivedItems, 0);
                    break;
                }

                case "try_buy_item":
                {
                    if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out var itemId)) return;

                    var item = FindItemById(itemId);
                    if (item == null || !item.CanBuy) return;

                    var amount = 1;
                    if (arg.HasArgs(3))
                    {
                        if (arg.GetString(2) == "all")
                            amount = Mathf.FloorToInt((float) (GetPlayerEconomy(player).ShowBalance(player) /
                                                               item.GetPrice(player,
                                                                   API_GetShopPlayerSelectedEconomy(
                                                                       player.userID))));
                        else
                            amount = arg.GetInt(2);

                        if (amount < 1) return;
                    }

                    var maxAmount = item.BuyMaxAmount;
                    if (maxAmount > 0) amount = Mathf.Min(amount, maxAmount);

                    ModalShopItemActionUI(player, item, true, amount);
                    break;
                }

                case "try_sell_item":
                {
                    if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out var itemId))
                        return;

                    var item = FindItemById(itemId);
                    if (item == null || !item.CanSell) return;

                    var amount = 1;
                    if (arg.HasArgs(3))
                    {
                        if (arg.GetString(2) == "all")
                            amount = Mathf.FloorToInt(PlayerItemsCount(player, item.ShortName, item.Skin) /
                                                      (float) item.Amount);
                        else
                            amount = arg.GetInt(2);

                        if (amount < 1) return;
                    }

                    amount = Mathf.Max(1,
                        Mathf.Min(amount,
                            Mathf.CeilToInt(PlayerItemsCount(player, item.ShortName, item.Skin) /
                                            (float) item.Amount)));

                    var maxAmount = item.SellMaxAmount;
                    if (maxAmount > 0) amount = Mathf.Min(amount, maxAmount);

                    ModalShopItemActionUI(player, item, false, amount);
                    break;
                }

                case "sellitem":
                {
                    if (!arg.HasArgs(3) ||
                        !int.TryParse(arg.Args[1], out var itemId) ||
                        !int.TryParse(arg.Args[2], out var amount) ||
                        amount <= 0) return;

                    var item = FindItemById(itemId);
                    if (item == null) return;

                    var cooldownTime = GetCooldownTime(player.userID, item, false);
                    if (cooldownTime > 0)
                    {
                        ErrorUi(player, Msg(player, SellCooldownMessage));
                        return;
                    }

                    if (_config.BlockNoEscape && NoEscape != null)
                        if (Convert.ToBoolean(NoEscape?.Call("IsBlocked", player)))
                        {
                            ErrorUi(player, Msg(player, SellRaidBlocked));
                            return;
                        }

                    var secondsFromWipe = GetSecondsFromWipe();
                    if (_config.WipeCooldown)
                    {
                        var timeLeft = Mathf.RoundToInt(_config.WipeCooldownTimer - secondsFromWipe);
                        if (timeLeft > 0)
                        {
                            ErrorUi(player,
                                Msg(player, SellWipeCooldown,
                                    FormatShortTime(timeLeft)));
                            return;
                        }
                    }

                    if (item.SellBlockDurationAfterWipe > 0)
                    {
                        var timeLeft = Mathf.RoundToInt(item.SellBlockDurationAfterWipe - secondsFromWipe);
                        if (timeLeft > 0)
                        {
                            ErrorUi(player, Msg(player, SellWipeCooldown, FormatShortTime(timeLeft)));
                            return;
                        }
                    }

                    if (_config.RespawnCooldown)
                    {
                        var timeLeft = Mathf.RoundToInt(_config.RespawnCooldownTimer - player.TimeAlive());
                        if (timeLeft > 0)
                        {
                            ErrorUi(player,
                                Msg(player, SellRespawnCooldown,
                                    FormatShortTime(timeLeft)));
                            return;
                        }
                    }

                    var limit = GetLimit(player, item, false);
                    if (limit <= 0)
                    {
                        ErrorUi(player, Msg(player, SellLimitReached, item.GetPublicTitle(player)));
                        return;
                    }

                    limit = GetLimit(player, item, false, true);
                    if (limit <= 0)
                    {
                        ErrorUi(player, Msg(player, DailySellLimitReached, item.GetPublicTitle(player)));
                        return;
                    }

                    if (_config.BlockedSkins.TryGetValue(item.ShortName, out var blockedSkins))
                        if (blockedSkins.Contains(item.Skin))
                        {
                            ErrorUi(player, Msg(player, SkinBlocked));
                            return;
                        }

                    var totalAmount = item.Amount * amount;

                    var selectedEconomy = API_GetShopPlayerSelectedEconomy(player.userID);

                    var playerItems = Pool.Get<List<Item>>();
                    try
                    {
                        GetPlayerItems(player, playerItems);

                        if (ItemCount(playerItems, item) >= totalAmount)
                        {
                            var playerEconomy = GetPlayerEconomy(player);
                            var sellPrice = item.GetSellPrice(player, selectedEconomy) * amount;

                            LogBuySell(player, item, amount, sellPrice, false);

                            Take(playerItems, item, totalAmount);
                            playerEconomy.AddBalance(player, sellPrice);

                            SetCooldown(player, item, false);
                            UseLimit(player, item, false, amount);
                            UseLimit(player, item, false, amount, true);

                            UpdateUI(player, (List<string> container) =>
                            {
                                ShopItemButtonsUI(player, ref container, item, selectedEconomy);

                                BalanceUi(ref container, player);
                            });

                            _config?.Notifications?.ShowNotify(player, SellNotify, 0, totalAmount,
                                item.GetPublicTitle(player));
                        }
                        else
                        {
                            ErrorUi(player, Msg(player, NotEnough));
                        }
                    }
                    finally
                    {
                        Pool.FreeUnmanaged(ref playerItems);
                    }

                    break;
                }

                case "edit_item":
                {
                    if (!IsAdmin(player)) return;

                    switch (arg.GetString(1))
                    {
                        case "start":
                        {
                            if (!arg.HasArgs(2)) return;

                            var isNew = arg.GetString(2) == "new";

                            var itemID = isNew ? GetId() : arg.GetInt(2);

                            EditItemData.Remove(player.userID);

                            var editData = EditItemData.Create(player, itemID, isNew);
                            if (editData == null) return;

                            ShowEditItemPanel(player);
                            break;
                        }

                        case "cancel":
                        {
                            var editData = EditItemData.Get(player.userID);
                            editData?.Cancel();

                            CuiHelper.DestroyUi(player, EditingLayer);
                            break;
                        }

                        case "save":
                        {
                            CuiHelper.DestroyUi(player, EditingLayer);

                            var editData = EditItemData.Get(player.userID);
                            if (editData == null) return;

                            editData.Save();

                            UpdateUI(player, (List<string> container) => ShopContentUI(player, ref container));
                            break;
                        }

                        case "remove":
                        {
                            CuiHelper.DestroyUi(player, EditingLayer);

                            var editData = EditItemData.Get(player.userID);
                            if (editData == null || editData.isNew) return;

                            if (TryFindItemById(editData.itemID, out var shopItem))
                                _itemsData.Shop.ForEach(category =>
                                {
                                    if (category.Items.Remove(shopItem))
                                        category.LoadIDs(true);
                                });

                            editData.Cancel();

                            SaveItemsData();
                            LoadItems();

                            UpdateUI(player, (List<string> container) => ShopContentUI(player, ref container));
                            break;
                        }

                        case "select_item":
                        {
                            var editData = EditItemData.Get(player.userID);
                            if (editData == null) return;

                            switch (arg.GetString(2))
                            {
                                case "open":
                                {
                                    editData.ResetSelectItemState();
                                    SelectItemModal(player);
                                    break;
                                }

                                case "category":
                                {
                                    editData.SetSelectCategory(arg.GetString(3));
                                    SelectItemModal(player);
                                    break;
                                }

                                case "take":
                                {
                                    var shortName = string.Join(" ", arg.Args.Skip(3));
                                    editData.SetFieldValue("ShortName", shortName);

                                    CuiHelper.DestroyUi(player, ModalLayer);
                                    ShowEditItemPanel(player);
                                    break;
                                }

                                case "close":
                                {
                                    CuiHelper.DestroyUi(player, ModalLayer);
                                    break;
                                }
                            }

                            break;
                        }

                        case "field":
                        {
                            var editData = EditItemData.Get(player.userID);
                            if (editData == null) return;

                            var fieldName = arg.GetString(2);
                            var parent = arg.GetString(3);

                            var targetField = editData.GetField(fieldName);
                            if (targetField == null) return;

                            if (targetField.FieldType.IsEnum)
                            {
                                if (targetField.GetValue(editData.editingItem) is not Enum nowEnum) return;

                                var targetEnum = arg.GetString(4) switch
                                {
                                    "prev" => nowEnum.Previous(),
                                    "next" => nowEnum.Next(),
                                    _ => null
                                };

                                if (targetEnum != null)
                                {
                                    targetField.SetValue(editData.editingItem, targetEnum);
                                    editData.RefreshEditableFields();
                                    ShowEditItemPanel(player);
                                    return;
                                }
                            }
                            else if (targetField.FieldType == typeof(bool))
                            {
                                var currentValue = (bool) targetField.GetValue(editData.editingItem);
                                targetField.SetValue(editData.editingItem, !currentValue);
                            }
                            else
                            {
                                var newValue = string.Join(" ", arg.Args.Skip(4));
                                editData.SetFieldValue(fieldName, newValue);
                            }

                            UpdateUI(player, container =>
                            {
                                FieldItemUI(player, container, parent, targetField,
                                    targetField.GetValue(editData.editingItem),
                                    arg.GetString(0), editData.editingItem);
                            });
                            break;
                        }
                    }

                    break;
                }

                case "item_info":
                {
                    if (!arg.HasArgs(2) ||
                        !int.TryParse(arg.Args[1], out var itemId)) return;

                    var item = FindItemById(itemId);
                    if (item == null) return;

                    var selectedEconomy = API_GetShopPlayerSelectedEconomy(player.userID);

                    UpdateUI(player,
                        container =>
                        {
                            GetShop(player)?.GetUI()?.ShopItemDescriptionModal
                                .GetModal(player, ref container, item);
                        });
                    break;
                }

                case "transfer_start":
                {
                    SelectTransferPlayerUI(player);
                    break;
                }

                case "transfer_page":
                {
                    var newTransferPage = arg.GetInt(1);

                    SelectTransferPlayerUI(player, newTransferPage);
                    break;
                }

                case "transfer_set_target":
                {
                    if (!arg.HasArgs(2) || !ulong.TryParse(arg.Args[1], out var targetId)) return;

                    TransferUi(player, targetId);
                    break;
                }

                case "transfer_set_amount":
                {
                    if (!arg.HasArgs(2) || !ulong.TryParse(arg.Args[1], out var targetId)) return;

                    var amount = arg.GetFloat(2);

                    amount = Mathf.Max(amount, 0);

                    TransferUi(player, targetId, amount);
                    break;
                }

                case "transfer_send":
                {
                    if (!arg.HasArgs(3) ||
                        !ulong.TryParse(arg.Args[1], out var targetId) ||
                        !float.TryParse(arg.Args[2], out var amount) ||
                        amount <= 0) return;

                    var targetPlayer = BasePlayer.FindAwakeOrSleeping(targetId.ToString());
                    if (targetPlayer == null)
                    {
                        ErrorUi(player, Msg(player, PlayerNotFound));
                        return;
                    }

                    var selectedEconomy = GetPlayerEconomy(player);
                    if (!selectedEconomy.Transfer(player, targetPlayer, amount))
                    {
                        ErrorUi(player, Msg(player, NotMoney));
                        return;
                    }

                    var formattedAmount = selectedEconomy.GetPriceTitle(player,
                        amount.ToString(_config.Formatting.BuyPriceFormat));

                    _config?.Notifications?.ShowNotify(player, SuccessfulTransfer, 0,
                        formattedAmount,
                        targetPlayer.displayName);

                    LogTransfer(player.displayName, player.UserIDString, formattedAmount, targetPlayer.displayName,
                        targetPlayer.UserIDString);
                    break;
                }

                case "economy_try_change":
                {
                    if (!arg.HasArgs(2) ||
                        !bool.TryParse(arg.Args[1], out var selected))
                        return;

                    UpdateUI(player,
                        (List<string> container) => { ShowChoiceEconomyUI(player, ref container, selected); });
                    break;
                }

                case "economy_set":
                {
                    if (!arg.HasArgs(2) ||
                        !int.TryParse(arg.Args[1], out var economyID))
                        return;

                    SelectPlayerEconomy(player, economyID);

                    UpdateUI(player, (List<string> container) =>
                    {
                        ShopContentUI(player, ref container);

                        ShopBasketUI(ref container, player);

                        ShopHeaderUI(ref container, player);
                    });
                    break;
                }

                case "edit_category":
                {
                    if (!IsAdmin(player)) return;

                    switch (arg.GetString(1))
                    {
                        case "start":
                        {
                            if (!arg.HasArgs(3) || !int.TryParse(arg.Args[2], out var categoryID)) return;

                            var isNew = categoryID == -1;

                            EditCategoryData.Remove(player.userID);

                            var editData = EditCategoryData.Create(player, categoryID, isNew);
                            if (editData == null) return;

                            ShowEditCategoryPanel(player, true);
                            break;
                        }

                        case "cancel":
                        {
                            var editData = EditCategoryData.Get(player.userID);
                            editData?.Cancel();

                            CuiHelper.DestroyUi(player, EditingLayer);
                            break;
                        }

                        case "save":
                        {
                            CuiHelper.DestroyUi(player, EditingLayer);

                            var editData = EditCategoryData.Get(player.userID);
                            if (editData == null) return;

                            editData.Save();

                            UpdateUI(player, (List<string> container) => ShopCategoriesUI(ref container, player));
                            break;
                        }

                        case "remove":
                        {
                            CuiHelper.DestroyUi(player, EditingLayer);

                            var editData = EditCategoryData.Get(player.userID);
                            if (editData == null || editData.isNew) return;

                            editData.Delete();

                            UpdateUI(player, (List<string> container) =>
                            {
                                ShopCategoriesUI(ref container, player);
                                ShopContentUI(player, ref container);
                            });
                            break;
                        }

                        case "field":
                        {
                            var editData = EditCategoryData.Get(player.userID);
                            if (editData == null) return;

                            var fieldName = arg.GetString(2);
                            var parent = arg.GetString(3);

                            var targetField = editData.GetField(fieldName);
                            if (targetField == null) return;

                            if (targetField.FieldType.IsEnum)
                            {
                                if (targetField.GetValue(editData.editingCategory) is not Enum nowEnum) return;

                                var targetEnum = arg.GetString(4) switch
                                {
                                    "prev" => nowEnum.Previous(),
                                    "next" => nowEnum.Next(),
                                    _ => null
                                };

                                if (targetEnum != null)
                                    targetField.SetValue(editData.editingCategory, targetEnum);
                            }
                            else if (targetField.FieldType == typeof(bool))
                            {
                                var currentValue = (bool) targetField.GetValue(editData.editingCategory);
                                targetField.SetValue(editData.editingCategory, !currentValue);
                            }
                            else
                            {
                                var newValue = string.Join(" ", arg.Args.Skip(4));
                                editData.SetFieldValue(fieldName, newValue);
                            }

                            UpdateUI(player, container =>
                            {
                                FieldItemUI(player, container, parent, targetField,
                                    targetField.GetValue(editData.editingCategory),
                                    "edit_category", editData.editingCategory);
                            });
                            break;
                        }

                        case "localize_text":
                        {
                            var editData = EditCategoryData.Get(player.userID);
                            if (editData == null) return;

                            var langKey = arg.GetString(2);

                            var localizations = editData.editingCategory.Localization.Messages;

                            switch (arg.GetString(3))
                            {
                                case "text":
                                {
                                    var text = string.Join(" ", arg.Args.Skip(4));
                                    if (string.IsNullOrEmpty(text))
                                    {
                                        localizations.Remove(langKey);
                                    }
                                    else
                                    {
                                        if (!localizations.TryGetValue(langKey, out var localization))
                                            localizations.Add(langKey, text);
                                        else
                                            localizations[langKey] = text;
                                    }

                                    break;
                                }
                            }

                            break;
                        }

                        case "move":
                        {
                            var editData = EditCategoryData.Get(player.userID);
                            if (editData == null || editData.isNew) return;

                            var targetIndex = _itemsData.Shop.FindIndex(x => x.ID == editData.categoryID);

                            switch (arg.GetString(2))
                            {
                                case "up":
                                    _itemsData.Shop.MoveUp(targetIndex);
                                    break;
                                case "down":
                                    _itemsData.Shop.MoveDown(targetIndex);
                                    break;
                            }

                            SaveItemsData();

                            UpdateUI(player, (List<string> container) => ShopCategoriesUI(ref container, player));
                            break;
                        }
                    }

                    break;
                }

                case "change_admin_mode":
                {
                    if (!IsAdmin(player)) return;

                    if (!_adminModeUsers.Add(player.userID))
                        _adminModeUsers.Remove(player.userID);

                    var shop = GetShop(player);
                    if (shop == null) return;

                    var oldCategory = shop.GetSelectedShopCategory();
                    var oldCategoryPage = shop.currentCategoriesPage;

                    shop.Update();

                    shop.OnChangeCategory(shop.Categories.IndexOf(oldCategory), oldCategoryPage);

                    UpdateUI(player, (List<string> container) =>
                    {
                        ShopCategoriesUI(ref container, player);
                        ShopContentUI(player, ref container);
                        ShopHeaderUI(ref container, player);
                    });
                    break;
                }

                case "edit_category_move":
                {
                    if (!IsAdmin(player) || !arg.HasArgs(3)) return;

                    var categoryID = arg.GetInt(1);
                    var moveType = arg.GetString(2);

                    var category = FindCategoryById(categoryID);
                    if (category == null) return;

                    switch (moveType)
                    {
                        case "up":
                            category.MoveCategoryUp();
                            break;
                        case "down":
                            category.MoveCategoryDown();
                            break;
                        default:
                            PrintError("Unknown move type: {0}", moveType);
                            return;
                    }

                    SaveItemsData();

                    GetShop(player)?.Update();

                    UpdateUI(player, (List<string> container) => { ShopCategoriesUI(ref container, player); });
                    break;
                }

                case "edit_item_move":
                {
                    if (!IsAdmin(player) || !arg.HasArgs(3)) return;

                    var itemID = arg.GetInt(1);
                    var moveType = arg.GetString(2);

                    var shop = GetShop(player);
                    if (shop.currentCategoryIndex < 0) return;

                    var list = GetCategories(player);
                    try
                    {
                        if (shop.currentCategoryIndex >= list.Count) return;
                        var shopCategory = list[shop.currentCategoryIndex];
                        if (shopCategory == null) return;
                        if (!TryFindItemById(itemID, out var shopItem))
                            return;
                        switch (moveType)
                        {
                            case "right":
                                shopCategory.MoveItemRight(shopItem);
                                break;
                            case "left":
                                shopCategory.MoveItemLeft(shopItem);
                                break;
                            default:
                                PrintError("Unknown move type: {0}", moveType);
                                return;
                        }

                        SaveItemsData();
                        UpdateUI(player, (List<string> container) => { ShopContentUI(player, ref container); });
                    }
                    finally
                    {
                        Pool.FreeUnmanaged(ref list);
                    }

                    break;
                }

                case "cart_try_buy_again":
                {
                    UpdateUI(player, container =>
                    {
                        GetShop(player)?.GetUI()?.ShopConfirmationModal?.GetModal(player, ref container,
                            (Msg(player, BuyTitle), $"{CmdMainConsole} cart_buy_again"),
                            (Msg(player, CancelTitle), string.Empty));
                    });
                    break;
                }

                case "cart_buy_again":
                {
                    TryBuyItems(player, true);
                    break;
                }

                case "favorites":
                {
                    if (!arg.HasArgs(2))
                        return;

                    switch (arg.GetString(1))
                    {
                        case "item":
                        {
                            var itemID = arg.GetInt(3);

                            if (!TryFindItemById(itemID, out var shopItem))
                                return;

                            if (GetPlayerCart(player.userID) is not PlayerCartData playerCart) return;

                            switch (arg.GetString(2))
                            {
                                case "add":
                                {
                                    if (!playerCart.AddItemToFavorite(itemID))
                                    {
                                        _config?.Notifications?.ShowNotify(player, MsgIsFavoriteItem, 1);
                                        return;
                                    }

                                    UpdateUI(player, (List<string> container) =>
                                    {
                                        ItemFavoriteUI(player, ref container, GetShop(player).GetUI(),
                                            shopItem);
                                    });

                                    _config?.Notifications?.ShowNotify(player, MsgAddedToFavoriteItem, 0,
                                        shopItem.GetPublicTitle(player));
                                    break;
                                }

                                case "remove":
                                {
                                    if (!playerCart.RemoveItemFromFavorites(itemID))
                                    {
                                        _config?.Notifications?.ShowNotify(player, MsgNoFavoriteItem, 1);
                                        return;
                                    }

                                    var shop = GetShop(player);

                                    ShopCategory shopCategory = null;

                                    var categories = GetCategories(player);
                                    try
                                    {
                                        shopCategory = categories[shop.currentCategoryIndex];
                                    }
                                    finally
                                    {
                                        Pool.FreeUnmanaged(ref categories);
                                    }

                                    if (shopCategory == null) return;

                                    var countItems = shopCategory.GetShopItemsCount(player);
                                    if (countItems - shop.currentShopPage * GetShopTotalItemsAmount(player) <= 0 &&
                                        shop.currentShopPage > 0)
                                    {
                                        shop.OnChangeShopPage(shop.currentShopPage - 1);

                                        UpdateUI(player,
                                            (List<string> container) => { ShopContentUI(player, ref container); });
                                    }
                                    else
                                    {
                                        UpdateUI(player, (List<string> container) =>
                                        {
                                            ItemFavoriteUI(player, ref container, GetShop(player).GetUI(),
                                                shopItem);
                                        });
                                    }

                                    _config?.Notifications?.ShowNotify(player, MsgRemovedFromFavoriteItem, 0,
                                        shopItem.GetPublicTitle(player));
                                    break;
                                }
                            }

                            break;
                        }
                    }

                    break;
                }

                case "refresh_shop_item":
                {
                    var itemID = arg.GetInt(1);
                    if (!TryFindItemById(itemID, out var item)) return;

                    var selectedEconomy = API_GetShopPlayerSelectedEconomy(player.userID);

                    UpdateUI(player,
                        (List<string> container) =>
                        {
                            ShopItemButtonsUI(player, ref container, item, selectedEconomy);
                        });
                    break;
                }
            }

#if TESTING
            }
            catch (Exception ex)
            {
                PrintError($"In the command '{CmdMainConsole}' there was an error:\n{ex}");

                Debug.LogException(ex);
            }

            Puts($"Main command used with: {string.Join(", ", arg.Args)}");
#endif
        }

        private int GetLastCategoriesPage(BasePlayer player, List<ShopCategory> categories)
        {
            var shopUI = GetShop(player).GetUI();
            var targetCategoriesPage =
                Mathf.FloorToInt((float) categories.Count / shopUI.ShopCategories.ShopCategory.CategoriesOnPage);

            if (categories.Count % shopUI.ShopCategories.ShopCategory.CategoriesOnPage == 0)
                targetCategoriesPage--;
            return targetCategoriesPage;
        }

        [ConsoleCommand("shop.change")]
        private void CmdChangeItemCategory(ConsoleSystem.Arg arg)
        {
            if (_templateMigrationInProgress) return;

            if (!arg.IsAdmin) return;

            if (arg.Args == null || arg.Args.Length < 2)
            {
                SendReply(arg, "/shop.change itemId newCategory");
                return;
            }

            var itemId = Convert.ToInt32(arg.Args[0]);
            var item = FindItemById(itemId);
            if (item == null)
            {
                SendReply(arg, $"Item \"{itemId}\" not found!");
                return;
            }

            var name = string.Join(" ", arg.Args.Skip(1));
            var category = FindCategoryByName(name);
            if (category == null)
            {
                SendReply(arg, $"Category \"{name}\" not found!");
                return;
            }

            foreach (var shopCategory in _itemsData.Shop)
                if (shopCategory.Items.Remove(item))
                {
                    category.Items.Add(item);
                    SaveItemsData();
                    LoadItems();


                    SendReply(arg, $"Item \"{itemId}\" move from \"{shopCategory.Title}\" to \"{category.Title}\"");
                    return;
                }
        }

        [ConsoleCommand("shop.refill")]
        private void CmdConsoleRefill(ConsoleSystem.Arg arg)
        {
            if (_templateMigrationInProgress) return;

            if (!arg.IsAdmin) return;

            FillCategories(arg.GetFloat(0));

            LoadItems();
        }

        [ConsoleCommand("shop.wipe")]
        private void CmdConsoleWipe(ConsoleSystem.Arg arg)
        {
            if (_templateMigrationInProgress) return;

            if (!arg.IsAdmin) return;

            if (_config.Wipe.Players || _config.Wipe.Cooldown || _config.Wipe.Limits)
                StartWipePlayers(_config.Wipe.Players, _config.Wipe.Cooldown, _config.Wipe.Limits);

            PrintWarning($"{Name} wiped!");
        }

        [ConsoleCommand("shop.reset")]
        private void CmdConsoleReset(ConsoleSystem.Arg arg)
        {
            if (_templateMigrationInProgress) return;

            if (arg.Player() != null || !arg.IsServerside)
            {
                if (IsAdmin(arg.Player())) SendReply(arg, "This command can only be run from the SERVER CONSOLE.");

                return;
            }

            switch (arg.GetString(0))
            {
                case "template":
                {
                    _uiData = new UIData();
                    SaveTemplate();

                    PrintWarning("Template was reset!");
                    break;
                }

                case "config":
                {
                    _config = new Configuration();
                    SaveConfig();

                    PrintWarning("Config was reset!");
                    break;
                }

                case "items":
                {
                    _itemsData = new ItemsData();
                    SaveItemsData();

                    PrintWarning("Items was reset!");
                    break;
                }

                case "full":
                {
                    _uiData = new UIData();
                    SaveTemplate();

                    _config = new Configuration();
                    SaveConfig();

                    _itemsData = new ItemsData();
                    SaveItemsData();

                    PrintWarning("Shop was reset!");
                    break;
                }

                default:
                {
                    SendReply(arg, $"Error syntax! Usage: {arg.cmd.FullName} [template/config/items/full] ");
                    return;
                }
            }

            Interface.Oxide.ReloadPlugin("Shop");
        }

        [ConsoleCommand("shop.remove")]
        private void CmdConsoleRemoveItem(ConsoleSystem.Arg arg)
        {
            if (_templateMigrationInProgress) return;

            if (!arg.IsAdmin) return;

            if (!arg.HasArgs())
            {
                SendReply(arg, $"Error syntax! Usage: /{arg.cmd.FullName} [item/category] [item id/category name/all]");
                return;
            }

            var index = arg.Args[0];
            switch (index)
            {
                case "all":
                    _itemsData.Shop.ForEach(shopCategory => shopCategory.Items.Clear());

                    SendReply(arg, "All items from categories have been removed!");

                    SaveItemsData();
                    break;
                case "cat":
                case "cats":
                case "category":
                {
                    if (arg.Args.Length < 2)
                    {
                        SendReply(arg, $"Error syntax! Usage: /{arg.cmd.FullName} {index} [category name/all]");
                        return;
                    }

                    if (arg.Args[1] == "all")
                    {
                        _itemsData.Shop.Clear();

                        var testCategory = new ShopCategory
                        {
                            Enabled = true,
                            CategoryType = ShopCategory.Type.None,
                            Title = "Test",
                            Localization = new Localization
                            {
                                Enabled = false,
                                Messages = new Dictionary<string, string>
                                {
                                    ["en"] = "Test",
                                    ["fr"] = "Test"
                                }
                            },
                            Permission = string.Empty,
                            SortType = Configuration.SortType.None,
                            Items = new List<ShopItem>
                            {
                                ShopItem.GetDefault(0, 100, "stones"),
                                ShopItem.GetDefault(0, 100, "wood")
                            }
                        };

                        _itemsData.Shop.Add(testCategory);

                        SendReply(arg,
                            "All categories were removed and one \"Test\" category was added with a couple of test items");

                        SaveItemsData();
                    }
                    else
                    {
                        var catName = arg.Args[1];
                        var category = FindCategoryByName(catName);
                        if (category == null)
                        {
                            SendReply(arg, $"Category \"{catName}\" not found!");
                            return;
                        }

                        _itemsData.Shop.Remove(category);

                        SendReply(arg, $"Category \"{catName}\" successfully deleted!");

                        SaveItemsData();
                    }

                    break;
                }

                case "item":
                case "items":
                {
                    if (arg.Args.Length < 2)
                    {
                        SendReply(arg, $"Error syntax! Usage: /{arg.cmd.FullName} {index} [item id/all]");
                        return;
                    }

                    var itemId = Convert.ToInt32(arg.Args[1]);
                    var item = FindItemById(itemId);
                    if (item == null)
                    {
                        SendReply(arg, $"Item \"{itemId}\" not found!");
                        return;
                    }

                    _itemsData.Shop.ForEach(shopCategory =>
                    {
                        if (shopCategory.Items.Remove(item))
                            shopCategory.LoadIDs(true);
                    });

                    SendReply(arg, $"Item \"{itemId}\" successfully deleted!");

                    SaveItemsData();
                    break;
                }
            }
        }

        [ConsoleCommand("shop.fill.icc")]
        private void CmdConsoleFillICC(ConsoleSystem.Arg arg)
        {
            if (_templateMigrationInProgress) return;

            if (!arg.IsAdmin) return;

            if (!arg.HasArgs())
            {
                SendReply(arg, "Error syntax! Usegae: shop.fill.icc [all/buy/sell]");
                return;
            }

            var type = -1;
            switch (arg.Args[0].ToLower())
            {
                case "buy":
                {
                    type = 0;
                    break;
                }
                case "sell":
                {
                    type = 1;
                    break;
                }
            }

            _itemsData.Shop.ForEach(category =>
            {
                if (category.CategoryType == ShopCategory.Type.Favorite) return;

                category.Items.ForEach(item =>
                {
                    if (item.Type != ItemType.Item || item.ItemDefinition == null) return;

                    var price = GetItemCost(item.ItemDefinition) * item.Amount;
                    if (price <= 0) return;

                    switch (type)
                    {
                        case 0:
                        {
                            item.Price = price;
                            break;
                        }
                        case 1:
                        {
                            item.SellPrice = price;
                            break;
                        }
                        default:
                        {
                            item.Price = price;
                            item.SellPrice = price;
                            break;
                        }
                    }
                });
            });

            Puts(
                $"The price has been updated for all items! Price type: {type switch {0 => "buy", 1 => "sell", _ => "all"}}");

            SaveItemsData();
        }

        #endregion

        #region Interface

        #region Shop

        #region UI Fields

        private int GetShopTotalItemsAmount(BasePlayer player)
        {
            var shopUI = GetShop(player).GetUI();
            return shopUI.ShopItem.ItemsOnString * shopUI.ShopItem.Strings;
        }

        #endregion

        #region UI Shop

        private void ShowShopUI(BasePlayer player,
            bool first = false,
            bool categories = false)
        {
            #region Fields

            var shop = GetShop(player);

            var shopUI = shop.GetUI();

            #endregion

            if (first && shopUI.ShopBackground.UseBackground)
                UpdateUI(player,
                    elements =>
                    {
                        elements.Add(CuiJsonFactory.CreatePanel(anchorMin: "0 0", anchorMax: "0 0",
                            offsetMin: "-100 -100", offsetMax: "-100 -100", color: "0 0 0 0",
                            parent: shopUI?.ShopBackground?.DisplayType ?? "Overlay",
                            name: "Mevent.ScrollFix.Mock", destroy: "Mevent.ScrollFix.Mock"));
                    });

            UpdateUI(player, elements =>
            {
                #region Background

                if (first)
                    if (shopUI.ShopBackground.UseBackground)
                    {
                        elements.Add(shopUI.ShopBackground.Background.GetSerialized(player,
                            shopUI.ShopBackground.DisplayType, Layer, Layer));

                        elements.Add(CuiJsonFactory.CreatePanel(parent: Layer,
                            name: Layer + ".Background",
                            destroy: Layer + ".Background", anchorMin: "0 0", anchorMax: "1 1", color: "0 0 0 0"));

                        elements.Add(CuiJsonFactory.CreateButton(parent: Layer + ".Background",
                            name: Layer + ".Background.Button", destroy: Layer + ".Background.Button", close: Layer,
                            command: $"{CmdMainConsole} closeui"));
                    }

                #endregion

                #region Main

                elements.Add(shopUI.ShopContent.Background.GetSerialized(player, Layer + ".Background", Layer + ".Main",
                    Layer + ".Main"));

                ShopContentUI(player, ref elements);

                #endregion

                #region Categories

                if (first || categories) ShopCategoriesUI(ref elements, player);

                #endregion

                #region Cart

                if (first) ShopBasketUI(ref elements, player);

                #endregion

                ShopHeaderUI(ref elements, player);
            });
        }

        #endregion

        #region UI Components

        private static void ShowGridUI(CuiElementContainer container,
            int startIndex,
            int count,
            int itemsOnString,
            float marginX,
            float marginY,
            float itemWidth,
            float itemHeight,
            float offsetX,
            float offsetY,
            float aMinX, float aMaxX, float aMinY, float aMaxY,
            string backgroundColor,
            string parent,
            Func<int, string> panelName = null,
            Func<int, string> destroyName = null,
            Action<int> callback = null)
        {
            var xSwitch = offsetX;
            var ySwitch = offsetY;

            for (var i = startIndex; i < count; i++)
            {
                container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = $"{aMinX} {aMinY}", AnchorMax = $"{aMaxX} {aMaxY}",
                            OffsetMin = $"{xSwitch} {ySwitch - itemHeight}",
                            OffsetMax = $"{xSwitch + itemWidth} {ySwitch}"
                        },
                        Image = {Color = backgroundColor}
                    }, parent, panelName != null ? panelName(i) : CuiHelper.GetGuid(),
                    destroyName != null ? destroyName(i) : string.Empty);

                callback?.Invoke(i);

                if ((i + 1) % itemsOnString == 0)
                {
                    xSwitch = offsetX;
                    ySwitch = ySwitch - itemHeight - marginY;
                }
                else
                {
                    xSwitch += itemWidth + marginX;
                }
            }
        }

        private void ShopHeaderUI(ref List<string> elements, BasePlayer player)
        {
            var shop = GetShop(player);

            var shopUI = shop.GetUI();

            elements.Add(shopUI.ShopContent.Header.Background.GetSerialized(player, Layer + ".Main", Layer + ".Header",
                Layer + ".Header"));

            elements.Add(shopUI.ShopContent.Header.Title.GetSerialized(player, Layer + ".Header",
                textFormatter: t => Msg(player, MainTitle)));

            if (_config.Transfer)
                elements.Add(shopUI.ShopContent.Header.ButtonTransfer.GetSerialized(player, Layer + ".Header",
                    textFormatter: t => Msg(player, TransferTitle),
                    cmdFormatter: t => $"{CmdMainConsole} transfer_start"));

            if (_economics.Count > 1)
                ShowChoiceEconomyUI(player, ref elements);

            elements.Add(shopUI.ShopContent.Header.Balance.Background.GetSerialized(player, Layer + ".Header",
                Layer + ".Balance", Layer + ".Balance"));

            elements.Add(shopUI.ShopContent.Header.Balance.Title.GetSerialized(player, Layer + ".Balance",
                textFormatter: t => Msg(player, YourBalance)));

            BalanceUi(ref elements, player);

            #region Search

            if (shopUI.ShopContent.Header.Search.Enabled)
            {
                elements.Add(shopUI.ShopContent.Header.Search.Background.GetSerialized(player, Layer + ".Header",
                    Layer + ".Search", Layer + ".Search"));

                elements.Add(shopUI.ShopContent.Header.Search.InputField.GetSerialized(player, Layer + ".Search",
                    textFormatter: t => string.IsNullOrEmpty(shop.search)
                        ? Msg(player, SearchTitle)
                        : $"{shop.search}", cmdFormatter: t => $"{CmdMainConsole} shop_search_input",
                    inputFieldSettings: (null, false, true, false, false, false, null)));
            }

            #endregion Search

            if (IsAdminMode(player) && shop.canShowAddItemButton)
                elements.Add(shopUI.ShopContent.Header.ButtonAddItem.GetSerialized(player, Layer + ".Header",
                    textFormatter: t => Msg(player, BtnAddItem),
                    cmdFormatter: t => $"{CmdMainConsole} edit_item start new"));

            if (shopUI.ShopContent.Header.UseCloseButton)
                elements.Add(shopUI.ShopContent.Header.ButtonClose.GetSerialized(player, Layer + ".Header",
                    textFormatter: t => Msg(player, CloseButton), cmdFormatter: t => $"{CmdMainConsole} closeui",
                    close: Layer));
        }

        #region Shop Categories

        private void ShopCategoriesUI(ref List<string> elements,
            BasePlayer player)
        {
            var shop = GetShop(player);
            var shopUI = shop.GetUI();

            elements.Add(shopUI.ShopCategories.Background.GetSerialized(player, Layer + ".Main", Layer + ".Categories",
                Layer + ".Categories"));

            #region Header

            elements.Add(shopUI.ShopCategories.Header.Background.GetSerialized(player, Layer + ".Categories",
                Layer + ".Categories.Header", Layer + ".Categories.Header"));

            elements.Add(shopUI.ShopCategories.Header.Title.GetSerialized(player, Layer + ".Categories.Header",
                textFormatter: t => Msg(player, CategoriesTitle)));

            #endregion

            #region Loop

            elements.Add(shopUI.ShopCategories.CategoriesPanel.GetSerialized(player, Layer + ".Categories",
                Layer + ".Categories.Content", Layer + ".Categories.Content"));

            if (shopUI.ShopCategories.UseScrollCategories)
            {
                var totalHeight = CalculateTotalHeight((shopUI.ShopCategories.UseScrollCategories
                    ? shop.Categories
                    : shop.Categories.SkipAndTake(
                        shop.currentCategoriesPage * shopUI.ShopCategories.ShopCategory.CategoriesOnPage,
                        shopUI.ShopCategories.ShopCategory.CategoriesOnPage)).Count);

                elements.Add(CuiJsonFactory.CreateScrollView(
                    parent: Layer + ".Categories.Content",
                    name: Layer + ".Categories.Scroll",
                    destroy: Layer + ".Categories.Scroll",
                    anchorMin: shopUI.ShopCategories.CategoriesScrollView?.AnchorMinX + " " +
                               shopUI.ShopCategories.CategoriesScrollView?.AnchorMinY,
                    anchorMax: shopUI.ShopCategories.CategoriesScrollView?.AnchorMaxX + " " +
                               shopUI.ShopCategories.CategoriesScrollView?.AnchorMaxY,
                    offsetMin: shopUI.ShopCategories.CategoriesScrollView?.OffsetMinX + " " +
                               shopUI.ShopCategories.CategoriesScrollView?.OffsetMinY,
                    offsetMax: shopUI.ShopCategories.CategoriesScrollView?.OffsetMaxX + " " +
                               shopUI.ShopCategories.CategoriesScrollView?.OffsetMaxY,
                    horizontal: false, vertical: true,
                    movementType: ScrollRect.MovementType.Clamped,
                    inertia: true,
                    elasticity: 0.25f,
                    decelerationRate: 0.3f,
                    scrollSensitivity: 24f,
                    verticalScrollbar: CuiJsonFactory.CreateScrollBar(
                        size: shopUI.ShopCategories.CategoriesScrollView.Scrollbar?.Size ?? 3f,
                        autoHide: shopUI.ShopCategories.CategoriesScrollView.Scrollbar?.AutoHide ?? true,
                        highlightColor: shopUI.ShopCategories.CategoriesScrollView.Scrollbar?.HighlightColor?.Get() ??
                                        HexToCuiColor("#D74933"),
                        handleColor: shopUI.ShopCategories.CategoriesScrollView.Scrollbar?.HandleColor?.Get() ??
                                     HexToCuiColor("#D74933"),
                        pressedColor: shopUI.ShopCategories.CategoriesScrollView.Scrollbar?.PressedColor?.Get() ??
                                      HexToCuiColor("#D74933"),
                        trackColor: shopUI.ShopCategories.CategoriesScrollView.Scrollbar?.TrackColor?.Get() ??
                                    HexToCuiColor("#373737")
                    ),
                    contentAnchorMin: "0 1",
                    contentAnchorMax: "1 1",
                    contentOffsetMin: $"0 -{totalHeight}",
                    contentOffsetMax: "0 0"));
            }

            CategoriesListUI(ref elements, player);

            #endregion

            #region Pages

            if (!shopUI.ShopCategories.UseScrollCategories)
            {
                elements.Add(shopUI.ShopCategories.BackButton.GetSerialized(player, Layer + ".Categories.Content",
                    textFormatter: t => Msg(player, BtnBack), cmdFormatter: t => shop.currentCategoriesPage != 0
                        ? $"{CmdMainConsole} categories_change_local_page {shop.currentCategoriesPage - 1}"
                        : string.Empty));

                elements.Add(shopUI.ShopCategories.NextButton.GetSerialized(player, Layer + ".Categories.Content",
                    textFormatter: t => Msg(player, BtnNext), cmdFormatter: t =>
                        shop.Categories.Count > (shop.currentCategoriesPage + 1) *
                        shopUI.ShopCategories.ShopCategory.CategoriesOnPage
                            ? $"{CmdMainConsole} categories_change_local_page {shop.currentCategoriesPage + 1}"
                            : string.Empty));
            }

            #endregion

            float CalculateTotalHeight(int pageCategoriesCount)
            {
                var targetMargin = shopUI.ShopCategories.ShopCategory.Margin;

                var totalHeight = pageCategoriesCount * shopUI.ShopCategories.ShopCategory.Height +
                                  (pageCategoriesCount - 1) *
                                  targetMargin;

                totalHeight += shopUI.ShopCategories.CategoriesScrollView.AdditionalHeight;

                if (IsAdmin(player)) totalHeight += shopUI.ShopCategories.ShopCategory.AdminPanel.AdditionalMargin;

                if (IsAdminMode(player)) totalHeight += targetMargin + shopUI.ShopCategories.ShopCategory.Height;

                totalHeight = Math.Max(totalHeight, shopUI.ShopCategories.CategoriesScrollView.MinHeight);

                return totalHeight;
            }
        }

        private void CategoriesListUI(ref List<string> elements, BasePlayer player)
        {
            var shop = GetShop(player);

            var shopUI = shop.GetUI();

            var offsetY = -shopUI.ShopCategories.ShopCategory.TopIndent;

            var categoryIndex = shop.currentCategoriesPage * shopUI.ShopCategories.ShopCategory.CategoriesOnPage;

            var targetMargin = shopUI.ShopCategories.ShopCategory.Margin;

            var targetLayer = shopUI.ShopCategories.UseScrollCategories
                ? Layer + ".Categories.Scroll"
                : Layer + ".Categories.Content";

            var pageCategories = shopUI.ShopCategories.UseScrollCategories
                ? shop.Categories
                : shop.Categories.SkipAndTake(
                    shop.currentCategoriesPage * shopUI.ShopCategories.ShopCategory.CategoriesOnPage,
                    shopUI.ShopCategories.ShopCategory.CategoriesOnPage);

            if (IsAdmin(player))
            {
                offsetY -= shopUI.ShopCategories.ShopCategory.AdminPanel.AdditionalMargin;

                #region Check Show All

                elements.Add(
                    shopUI.ShopCategories.CategoryAdminPanel.CheckboxCategoriesDisplay.Background.GetSerialized(player,
                        targetLayer, targetLayer + ".Admin.Show.All", targetLayer + ".Admin.Show.All"));

                elements.Add(shopUI.ShopCategories.CategoryAdminPanel.CheckboxCategoriesDisplay.Title.GetSerialized(
                    player, targetLayer + ".Admin.Show.All",
                    textFormatter: t => Msg(player, UICategoriesAdminShowAllTitle)));

                elements.Add(
                    shopUI.ShopCategories.CategoryAdminPanel.CheckboxCategoriesDisplay.CheckboxButton.GetSerialized(
                        player, targetLayer + ".Admin.Show.All", targetLayer + ".Admin.Show.All.Check",
                        targetLayer + ".Admin.Show.All.Check",
                        textFormatter: t => _adminModeUsers.Contains(player.userID) ? "■" : string.Empty,
                        cmdFormatter: t => $"{CmdMainConsole} change_admin_mode"));

                CreateOutLine(ref elements, targetLayer + ".Admin.Show.All.Check",
                    shopUI.ShopCategories.CategoryAdminPanel.CheckboxCategoriesDisplay.CheckboxColor.Get(),
                    shopUI.ShopCategories.CategoryAdminPanel.CheckboxCategoriesDisplay.CheckboxSize);

                #endregion Check Show All
            }

            for (var i = 0; i < pageCategories.Count; i++)
            {
                var category = pageCategories[i];

                elements.Add(CuiJsonFactory.CreatePanel(parent: targetLayer,
                    name: Layer + $".Category.{category.ID}.Background",
                    destroy: Layer + $".Category.{category.ID}.Background", color: "0 0 0 0", anchorMin: "0 1",
                    anchorMax: "0 1",
                    offsetMin:
                    $"{shopUI.ShopCategories.ShopCategory.LeftIndent} {offsetY - shopUI.ShopCategories.ShopCategory.Height}",
                    offsetMax:
                    $"{shopUI.ShopCategories.ShopCategory.LeftIndent + shopUI.ShopCategories.ShopCategory.Width} {offsetY}"));

                ShowCategoryUI(player, ref elements, shop.currentCategoryIndex, category, categoryIndex + i, shopUI);

                offsetY -= shopUI.ShopCategories.ShopCategory.Height;

                if (i != pageCategories.Count - 1)
                    offsetY -= targetMargin;
            }

            #region Add Category Button

            if (IsAdminMode(player))
            {
                offsetY -= targetMargin;

                elements.Add(CuiJsonFactory.CreatePanel(parent: targetLayer, name: Layer + ".Category.Add",
                    destroy: Layer + ".Category.Add", color: "0 0 0 0", anchorMin: "0 1", anchorMax: "0 1",
                    offsetMin:
                    $"{shopUI.ShopCategories.ShopCategory.LeftIndent} {offsetY - shopUI.ShopCategories.ShopCategory.Height}",
                    offsetMax:
                    $"{shopUI.ShopCategories.ShopCategory.LeftIndent + shopUI.ShopCategories.ShopCategory.Width} {offsetY}"));

                elements.Add(shopUI.ShopCategories.CategoryAdminPanel.ButtonAddCategory.GetSerialized(player,
                    Layer + ".Category.Add", targetLayer + ".Admin.Add.Category", targetLayer + ".Admin.Add.Category",
                    textFormatter: t => Msg(player, BtnAddCategory),
                    cmdFormatter: t => $"{CmdMainConsole} edit_category start -1"));
            }

            #endregion Add Category Button
        }

        private void ShowCategoryUI(BasePlayer player, ref List<string> elements,
            int currentCategoryIndex,
            ShopCategory category,
            int categoryId,
            ShopUI shopUI)
        {
            var title = category.GetTitle(player) ?? string.Empty;

            if (!category.Enabled)
                title = $"<color=red>[DISABLED]</color> {title}";

            var isSelectedCategory = categoryId == currentCategoryIndex;

            elements.Add((isSelectedCategory
                    ? shopUI.ShopCategories.ShopCategory.SelectedCategory
                    : shopUI.ShopCategories.ShopCategory.Category).Background
                .GetSerialized(player, Layer + $".Category.{category.ID}.Background",
                    Layer + $".Category.{category.ID}", Layer + $".Category.{category.ID}"));

            elements.Add((isSelectedCategory
                    ? shopUI.ShopCategories.ShopCategory.SelectedCategory
                    : shopUI.ShopCategories.ShopCategory.Category).Title
                .GetSerialized(player, Layer + $".Category.{category.ID}", textFormatter: t => title));

            elements.Add(CuiJsonFactory.CreateButton(parent: Layer + $".Category.{category.ID}",
                command: !isSelectedCategory
                    ? $"{CmdMainConsole} categories_change {categoryId}"
                    : string.Empty));

            if (IsAdminMode(player))
            {
                elements.Add(shopUI.ShopCategories.ShopCategory.AdminPanel.Background.GetSerialized(player,
                    Layer + $".Category.{category.ID}", Layer + $".Category.{category.ID}.Settings",
                    Layer + $".Category.{category.ID}.Settings"));

                elements.Add(shopUI.ShopCategories.ShopCategory.AdminPanel.ButtonEdit.GetSerialized(player,
                    Layer + $".Category.{category.ID}.Settings", Layer + $".Category.{category.ID}.Settings.Edit",
                    Layer + $".Category.{category.ID}.Settings.Edit", textFormatter: t => Msg(player, BtnEditCategory),
                    cmdFormatter: t => $"{CmdMainConsole} edit_category start {category.ID}"));
                shopUI.ShopCategories.ShopCategory.AdminPanel.ButtonMoveUp.GetButton(player, ref elements,
                    Layer + $".Category.{category.ID}.Settings", Layer + $".Category.{category.ID}.Settings.MoveUp",
                    string.Empty, $"{CmdMainConsole} edit_category_move {category.ID} up");
                shopUI.ShopCategories.ShopCategory.AdminPanel.ButtonMoveDown.GetButton(player, ref elements,
                    Layer + $".Category.{category.ID}.Settings", Layer + $".Category.{category.ID}.Settings.MoveDown",
                    string.Empty, $"{CmdMainConsole} edit_category_move {category.ID} down");
            }
        }

        #endregion

        private void ShopContentUI(BasePlayer player,
            ref List<string> elements)
        {
            var shop = GetShop(player);

            var shopUI = shop.GetUI();

            elements.Add(shopUI.ShopContent.Content.Background.GetSerialized(player, Layer + ".Main",
                Layer + ".Shop.Content", Layer + ".Shop.Content"));

            if (shop.Categories.Count > 0)
            {
                var inPageItems = GetPaginationShopItems(player);
                try
                {
                    if (inPageItems.Count > 0)
                    {
                        var targetMarginY = shopUI.ShopItem.Margin;
                        var targetTopIndent = -shopUI.ShopContent.ItemsTopIndent;

                        if (IsAdmin(player))
                        {
                            targetMarginY += shopUI.ShopItem.AdminPanel.AdditionalMargin;

                            targetTopIndent -= shopUI.ShopItem.AdminPanel.AdditionalMargin;
                        }

                        if (shopUI.ShopContent.Content.UseScrollToListItems)
                        {
                            var maxLines =
                                Mathf.CeilToInt((float) shop.categoryItemsCount / shopUI.ShopItem.ItemsOnString);
                            var totalHeight = maxLines * shopUI.ShopItem.ItemHeight + (maxLines - 1) * targetMarginY;

                            totalHeight += Mathf.Abs(targetTopIndent);

                            totalHeight += shopUI.ShopContent.Content.ListItemsScrollView.AdditionalHeight;

                            totalHeight = Math.Max(totalHeight,
                                shopUI.ShopContent.Content.ListItemsScrollView.MinHeight);

                            elements.Add(CuiJsonFactory.CreateScrollView(
                                parent: Layer + ".Shop.Content",
                                name: Layer + ".Shop.Scroll",
                                destroy: Layer + ".Shop.Scroll",
                                anchorMin: shopUI.ShopContent.Content.ListItemsScrollView?.AnchorMinX + " " +
                                           shopUI.ShopContent.Content.ListItemsScrollView?.AnchorMinY,
                                anchorMax: shopUI.ShopContent.Content.ListItemsScrollView?.AnchorMaxX + " " +
                                           shopUI.ShopContent.Content.ListItemsScrollView?.AnchorMaxY,
                                offsetMin: shopUI.ShopContent.Content.ListItemsScrollView?.OffsetMinX + " " +
                                           shopUI.ShopContent.Content.ListItemsScrollView?.OffsetMinY,
                                offsetMax: shopUI.ShopContent.Content.ListItemsScrollView?.OffsetMaxX + " " +
                                           shopUI.ShopContent.Content.ListItemsScrollView?.OffsetMaxY,
                                horizontal: false,
                                vertical: true,
                                movementType: ScrollRect.MovementType.Clamped,
                                inertia: true,
                                elasticity: 0.25f,
                                decelerationRate: 0.3f,
                                scrollSensitivity: 24f,
                                verticalScrollbar: CuiJsonFactory.CreateScrollBar(
                                    size: shopUI.ShopContent.Content.ListItemsScrollView.Scrollbar?.Size ?? 3f,
                                    autoHide: shopUI.ShopContent.Content.ListItemsScrollView.Scrollbar?.AutoHide ??
                                              true,
                                    highlightColor: shopUI.ShopContent.Content.ListItemsScrollView.Scrollbar
                                        ?.HighlightColor
                                        ?.Get() ?? HexToCuiColor("#D74933"),
                                    handleColor:
                                    shopUI.ShopContent.Content.ListItemsScrollView.Scrollbar?.HandleColor?.Get() ??
                                    HexToCuiColor("#D74933"),
                                    pressedColor: shopUI.ShopContent.Content.ListItemsScrollView.Scrollbar?.PressedColor
                                        ?.Get() ?? HexToCuiColor("#D74933"),
                                    trackColor: shopUI.ShopContent.Content.ListItemsScrollView.Scrollbar?.TrackColor
                                        ?.Get() ?? HexToCuiColor("#373737")
                                ),
                                contentAnchorMin: "0 1",
                                contentAnchorMax: "1 1",
                                contentOffsetMin: $"0 -{totalHeight}",
                                contentOffsetMax: "0 0"));
                        }

                        #region Items

                        var targetShopLayer = shopUI.ShopContent.Content.UseScrollToListItems
                            ? Layer + ".Shop.Scroll"
                            : Layer + ".Shop.Content";

                        var xSwitch = shopUI.ShopContent.ItemsLeftIndent;
                        var ySwitch = targetTopIndent;

                        for (var i = 0; i < inPageItems.Count; i++)
                        {
                            elements.Add(CuiJsonFactory.CreatePanel(parent: targetShopLayer,
                                name: Layer + $".Item.{inPageItems[i].ID}.Background",
                                destroy: Layer + $".Item.{inPageItems[i].ID}.Background", anchorMin: "0 1",
                                anchorMax: "0 1", offsetMin: $"{xSwitch} {ySwitch - shopUI.ShopItem.ItemHeight}",
                                offsetMax: $"{xSwitch + shopUI.ShopItem.ItemWidth} {ySwitch}", color: "0 0 0 0"));

                            ItemUI(player, inPageItems[i], ref elements);

                            if ((i + 1) % shopUI.ShopItem.ItemsOnString == 0)
                            {
                                xSwitch = shopUI.ShopContent.ItemsLeftIndent;
                                ySwitch = ySwitch - shopUI.ShopItem.ItemHeight - targetMarginY;
                            }
                            else
                            {
                                xSwitch += shopUI.ShopItem.ItemWidth + shopUI.ShopItem.Margin;
                            }
                        }

                        #endregion

                        #region Pages

                        if (!shopUI.ShopContent.Content.UseScrollToListItems)
                        {
                            var isSearch = shop.HasSearch();

                            var shopTotalItemsAmount = GetShopTotalItemsAmount(player);

                            var hasPages = isSearch
                                ? shop.categoryItemsCount > (shop.currentSearchPage + 1) * shopTotalItemsAmount ||
                                  shop.currentSearchPage != 0
                                : shop.categoryItemsCount > (shop.currentShopPage + 1) * shopTotalItemsAmount ||
                                  shop.currentShopPage != 0;
                            if (hasPages)
                            {
                                elements.Add(shopUI.ShopContent.Content.ButtonBack.GetSerialized(player,
                                    Layer + ".Shop.Content", textFormatter: t => Msg(player, BackPage),
                                    cmdFormatter: t =>
                                        isSearch
                                            ? shop.currentSearchPage != 0
                                                ? $"{CmdMainConsole} shop_search_page {shop.currentSearchPage - 1}"
                                                : string.Empty
                                            : shop.currentShopPage != 0
                                                ? $"{CmdMainConsole} shop_page {shop.currentShopPage - 1}"
                                                : string.Empty));

                                elements.Add(shopUI.ShopContent.Content.ButtonNext.GetSerialized(player,
                                    Layer + ".Shop.Content", textFormatter: t => Msg(player, NextPage),
                                    cmdFormatter: t =>
                                        isSearch
                                            ? shop.categoryItemsCount >
                                              (shop.currentSearchPage + 1) * shopTotalItemsAmount
                                                ? $"{CmdMainConsole} shop_search_page {shop.currentSearchPage + 1}"
                                                : string.Empty
                                            : shop.categoryItemsCount >
                                              (shop.currentShopPage + 1) * shopTotalItemsAmount
                                                ? $"{CmdMainConsole} shop_page {shop.currentShopPage + 1}"
                                                : string.Empty));
                            }
                        }

                        #endregion
                    }
                    else
                    {
                        elements.Add(shopUI.NoItems.GetSerialized(player, Layer + ".Shop.Content",
                            textFormatter: t => Msg(player, UIMsgNoItems)));
                    }
                }
                finally
                {
                    Pool.FreeUnmanaged(ref inPageItems);
                }
            }
        }

        #region Shop Cart

        private void ShopBasketUI(ref List<string> elements, BasePlayer player)
        {
            var shop = GetShop(player);
            var shopUI = shop.GetUI();
            if (!shopUI.ShopBasket.UseShopBasket) return;

            var playerCartData = GetPlayerCart(player.userID);

            var pageShopItems = playerCartData.GetShopItemsList();
            try
            {
                var shopItemsCount = pageShopItems.Count;
                var hasPages = shopItemsCount > shopUI.ShopBasket.BasketItem.ItemsOnPage;

                elements.Add(shopUI.ShopBasket.Background.GetSerialized(player, Layer + ".Main", Layer + ".PlayerCart",
                    Layer + ".PlayerCart"));

                #region Header

                elements.Add(shopUI.ShopBasket.Header.Background.GetSerialized(player, Layer + ".PlayerCart",
                    Layer + ".PlayerCart.Header", Layer + ".PlayerCart.Header"));

                elements.Add(shopUI.ShopBasket.Header.Title.GetSerialized(player, Layer + ".PlayerCart.Header",
                    textFormatter: t => Msg(player, ShoppingBag)));

                #region Pages

                if (!shopUI.ShopBasket.Content.UseScrollShoppingBag && hasPages)
                {
                    elements.Add(shopUI.ShopBasket.Header.BackButton.GetSerialized(player, Layer + ".PlayerCart.Header",
                        textFormatter: t => Msg(player, BackTitle), cmdFormatter: t => shop.currentBasketPage != 0
                            ? $"{CmdMainConsole} cart_page {shop.currentBasketPage - 1}"
                            : string.Empty));

                    elements.Add(shopUI.ShopBasket.Header.NextButton.GetSerialized(player, Layer + ".PlayerCart.Header",
                        textFormatter: t => Msg(player, NextTitle), cmdFormatter: t =>
                            playerCartData.Items.Count > (shop.currentBasketPage + 1) *
                            shopUI.ShopCategories.ShopCategory.CategoriesOnPage
                                ? $"{CmdMainConsole} cart_page {shop.currentBasketPage + 1}"
                                : string.Empty));
                }

                #endregion

                #endregion

                #region Items

                elements.Add(shopUI.ShopBasket.Content.Background.GetSerialized(player, Layer + ".PlayerCart",
                    Layer + ".PlayerCart.Content", Layer + ".PlayerCart.Content"));

                if (shopUI.ShopBasket.Content.UseScrollShoppingBag && hasPages)
                {
                    var totalHeight = shopItemsCount * shopUI.ShopBasket.BasketItem.Height +
                                      (shopItemsCount - 1) * shopUI.ShopBasket.BasketItem.Margin;

                    totalHeight += shopUI.ShopBasket.BasketItem.TopIndent;

                    totalHeight += shopUI.ShopBasket.Content.ShoppingBagScrollView.AdditionalHeight;

                    totalHeight = Math.Max(totalHeight, shopUI.ShopBasket.Content.ShoppingBagScrollView.MinHeight);

                    elements.Add(CuiJsonFactory.CreateScrollView(
                        parent: Layer + ".PlayerCart.Content",
                        name: Layer + ".PlayerCart.Scroll",
                        destroy: Layer + ".PlayerCart.Scroll",
                        anchorMin: shopUI.ShopBasket.Content.ShoppingBagScrollView?.AnchorMinX + " " +
                                   shopUI.ShopBasket.Content.ShoppingBagScrollView?.AnchorMinY,
                        anchorMax: shopUI.ShopBasket.Content.ShoppingBagScrollView?.AnchorMaxX + " " +
                                   shopUI.ShopBasket.Content.ShoppingBagScrollView?.AnchorMaxY,
                        offsetMin: shopUI.ShopBasket.Content.ShoppingBagScrollView?.OffsetMinX + " " +
                                   shopUI.ShopBasket.Content.ShoppingBagScrollView?.OffsetMinY,
                        offsetMax: shopUI.ShopBasket.Content.ShoppingBagScrollView?.OffsetMaxX + " " +
                                   shopUI.ShopBasket.Content.ShoppingBagScrollView?.OffsetMaxY,
                        horizontal: false,
                        vertical: true,
                        movementType: ScrollRect.MovementType.Clamped,
                        inertia: true,
                        elasticity: 0.25f,
                        decelerationRate: 0.3f,
                        scrollSensitivity: 24f,
                        verticalScrollbar: CuiJsonFactory.CreateScrollBar(
                            size: shopUI.ShopBasket.Content.ShoppingBagScrollView.Scrollbar?.Size ?? 3f,
                            autoHide: shopUI.ShopBasket.Content.ShoppingBagScrollView.Scrollbar?.AutoHide ?? true,
                            highlightColor:
                            shopUI.ShopBasket.Content.ShoppingBagScrollView.Scrollbar?.HighlightColor?.Get() ??
                            HexToCuiColor("#D74933"),
                            handleColor: shopUI.ShopBasket.Content.ShoppingBagScrollView.Scrollbar?.HandleColor
                                             ?.Get() ??
                                         HexToCuiColor("#D74933"),
                            pressedColor: shopUI.ShopBasket.Content.ShoppingBagScrollView.Scrollbar?.PressedColor
                                              ?.Get() ??
                                          HexToCuiColor("#D74933"),
                            trackColor: shopUI.ShopBasket.Content.ShoppingBagScrollView.Scrollbar?.TrackColor?.Get() ??
                                        HexToCuiColor("#373737")
                        ),
                        contentAnchorMin: "0 1",
                        contentAnchorMax: "1 1",
                        contentOffsetMin: $"0 -{totalHeight}",
                        contentOffsetMax: "0 0"));
                }

                ShopCartItemsListUI(ref elements, player, ref pageShopItems, hasPages,
                    shopUI.ShopBasket.Content.UseScrollShoppingBag
                        ? 0
                        : shop.currentBasketPage * shopUI.ShopCategories.ShopCategory.CategoriesOnPage,
                    shopUI.ShopBasket.Content.UseScrollShoppingBag
                        ? -1
                        : shopUI.ShopCategories.ShopCategory.CategoriesOnPage);

                #endregion

                #region Footer

                elements.Add(shopUI.ShopBasket.Footer.Background.GetSerialized(player, Layer + ".PlayerCart",
                    Layer + ".PlayerCart.Footer.Background", Layer + ".PlayerCart.Footer.Background"));

                UpdateShopCartFooterUI(ref elements, player, playerCartData);

                #endregion
            }
            finally
            {
                Pool.FreeUnmanaged(ref pageShopItems);
            }
        }

        private void UpdateShopCartFooterUI(ref List<string> elements, BasePlayer player, CartData playerCartData)
        {
            var shopUI = GetShop(player).GetUI();

            elements.Add(CuiJsonFactory.CreatePanel(parent: Layer + ".PlayerCart.Footer.Background",
                name: Layer + ".PlayerCart.Footer", destroy: Layer + ".PlayerCart.Footer",
                anchorMin: "0 0", anchorMax: "1 1", color: "0 0 0 0"));

            var useBuyAgain = _config.BuyAgain.HasAccess(player) &&
                              (playerCartData as PlayerCartData)?.LastPurchaseItems.Count > 0;

            elements.Add(
                (useBuyAgain ? shopUI.ShopBasket.Footer.BuyButtonWhenBuyAgain : shopUI.ShopBasket.Footer.BuyButton)
                ?.GetSerialized(player, Layer + ".PlayerCart.Footer", textFormatter: t => Msg(player, BuyTitle),
                    cmdFormatter: t => shopUI.ShopBasket.ShowConfirmMenu
                        ? $"{CmdMainConsole} cart_try_buyitems"
                        : $"{CmdMainConsole} cart_buyitems"));

            if (useBuyAgain)
                shopUI.ShopBasket.Footer.BuyAgainButton.GetButton(player, ref elements, Layer + ".PlayerCart.Footer",
                    Layer + ".PlayerCart.Footer.BuyAgain", string.Empty, $"{CmdMainConsole} cart_try_buy_again");

            elements.Add(shopUI.ShopBasket.Footer.ItemsCountTitle.GetSerialized(player, Layer + ".PlayerCart.Footer",
                textFormatter: t => Msg(player, UIBasketFooterItemsCountTitle)));
            elements.Add(shopUI.ShopBasket.Footer.ItemsCountValue.GetSerialized(player, Layer + ".PlayerCart.Footer",
                textFormatter: t => Msg(player, UIBasketFooterItemsCountValue, playerCartData.GetCartItemsAmount())));

            elements.Add(shopUI.ShopBasket.Footer.ItemsCostTitle.GetSerialized(player, Layer + ".PlayerCart.Footer",
                textFormatter: t => Msg(player, UIBasketFooterItemsCostTitle)));
            elements.Add(shopUI.ShopBasket.Footer.ItemsCostValue.GetSerialized(player, Layer + ".PlayerCart.Footer",
                textFormatter: t => GetPlayerEconomy(player).GetFooterPriceTitle(player,
                    playerCartData.GetCartPrice(player, !(playerCartData.Items.Count > 0))
                        .ToString(_config.Formatting.ShoppingBagCostFormat))));
        }

        private void ShopCartItemsListUI(ref List<string> elements, BasePlayer player,
            ref List<(ShopItem, int)> pageShopItems,
            bool hasPages,
            int skip = 0,
            int take = -1)
        {
            var count = pageShopItems.Count;
            if (count == 0 || skip < 0) return;

            if (skip >= count) return;

            var shopUI = GetShop(player).GetUI();

            var startIndex = skip;
            var endIndex = take > 0
                ? Math.Min(startIndex + take, count)
                : count;

            var offsetY = -shopUI.ShopBasket.BasketItem.TopIndent;

            for (var i = startIndex; i < endIndex; i++)
            {
                var (item, amount) = pageShopItems[i];
                if (item == null) continue;

                var displayIndex = i - startIndex;

                elements.Add(CuiJsonFactory.CreatePanel(
                    parent: shopUI.ShopBasket.Content.UseScrollShoppingBag && hasPages
                        ? Layer + ".PlayerCart.Scroll"
                        : Layer + ".PlayerCart.Content",
                    name: Layer + $".PlayerCart.Item.{displayIndex}.Background",
                    destroy: Layer + $".PlayerCart.Item.{displayIndex}.Background",
                    anchorMin: "0 1", anchorMax: "0 1",
                    offsetMin:
                    $"{shopUI.ShopBasket.BasketItem.LeftIndent} {offsetY - shopUI.ShopBasket.BasketItem.Height}",
                    offsetMax:
                    $"{shopUI.ShopBasket.BasketItem.LeftIndent + shopUI.ShopBasket.BasketItem.Width} {offsetY}",
                    color: "0 0 0 0"));

                elements.Add(shopUI.ShopBasket.BasketItem.Background.GetSerialized(player,
                    Layer + $".PlayerCart.Item.{displayIndex}.Background",
                    Layer + $".PlayerCart.Item.{displayIndex}",
                    Layer + $".PlayerCart.Item.{displayIndex}"));

                if (shopUI.ShopBasket.BasketItem.ShowImageBackground)
                    elements.Add(
                        shopUI.ShopBasket.BasketItem.ImageBackground.GetSerialized(player,
                            Layer + $".PlayerCart.Item.{displayIndex}"));

                ShopCartItemUI(player, ref elements, displayIndex, item, amount);

                offsetY = offsetY - shopUI.ShopBasket.BasketItem.Height - shopUI.ShopBasket.BasketItem.Margin;
            }
        }

        private void ShopCartItemUI(BasePlayer player, ref List<string> elements, int index, ShopItem item,
            int amount)
        {
            var shopUI = GetShop(player).GetUI();

            elements.Add(CuiJsonFactory.CreatePanel(parent: Layer + $".PlayerCart.Item.{index}.Background",
                name: Layer + $".PlayerCart.Item.{index}.Update",
                destroy: Layer + $".PlayerCart.Item.{index}.Update", anchorMin: "0 0", anchorMax: "1 1",
                color: "0 0 0 0"));

            if (item.Blueprint)
                elements.Add(shopUI.ShopBasket.BasketItem.ImageBlueprint.GetSerialized(player,
                    Layer + $".PlayerCart.Item.{index}.Update",
                    customImageSettings: (false, string.Empty, string.Empty, string.Empty, string.Empty,
                        ItemManager.blueprintBaseDef.itemid, null)));

            elements.Add(shopUI.ShopBasket.BasketItem.ImageItem.GetSerialized(player,
                Layer + $".PlayerCart.Item.{index}.Update", customImageSettings: item.GetImage()));

            elements.Add(shopUI.ShopBasket.BasketItem.Title.GetSerialized(player,
                Layer + $".PlayerCart.Item.{index}.Update",
                textFormatter: t => item.GetPublicTitle(player)));

            elements.Add(shopUI.ShopBasket.BasketItem.ItemAmount.GetSerialized(player,
                Layer + $".PlayerCart.Item.{index}.Update",
                textFormatter: t => Msg(player, AmountTitle, item.Amount * amount)));

            #region Amount

            elements.Add(shopUI.ShopBasket.BasketItem.ButtonRemoveItem.GetSerialized(player,
                Layer + $".PlayerCart.Item.{index}.Update", textFormatter: t => Msg(player, RemoveTitle),
                cmdFormatter: t => $"{CmdMainConsole} cart_item_remove {item.ID}"));

            elements.Add(shopUI.ShopBasket.BasketItem.ButtonMinusAmount.GetSerialized(player,
                Layer + $".PlayerCart.Item.{index}.Update", textFormatter: t => Msg(player, MinusTitle),
                cmdFormatter: t => $"{CmdMainConsole} cart_item_change {index} {item.ID} {amount - 1}"));

            elements.Add(shopUI.ShopBasket.BasketItem.ButtonPlusAmount.GetSerialized(player,
                Layer + $".PlayerCart.Item.{index}.Update", textFormatter: t => Msg(player, PlusTitle),
                cmdFormatter: t => $"{CmdMainConsole} cart_item_change {index} {item.ID} {amount + 1}"));

            elements.Add(shopUI.ShopBasket.BasketItem.InputAmount.GetSerialized(player,
                Layer + $".PlayerCart.Item.{index}.Update", textFormatter: t => $"{amount}",
                cmdFormatter: t => $"{CmdMainConsole} cart_item_change {index} {item.ID}"));

            #endregion
        }

        #endregion Shop Cart

        #endregion

        #region UI Modals

        private void ModalShopItemActionUI(BasePlayer player, ShopItem item, bool buy,
            int amount = 1)
        {
            UpdateUI(player, elements =>
            {
                var selectedEconomy = API_GetShopPlayerSelectedEconomy(player.userID);

                var targetModalTemplate =
                    buy ? GetShop(player).GetUI().ShopBuyModal : GetShop(player).GetUI().ShopSellModal;

                targetModalTemplate.GetModal(player, ref elements, item,
                    amount,
                    buy
                        ? (item.GetPrice(player, selectedEconomy) * amount).ToString(_config.Formatting.BuyPriceFormat)
                        : (item.GetSellPrice(player, selectedEconomy) * amount).ToString(_config.Formatting
                            .SellPriceFormat),
                    buy ? Msg(player, BuyTitle) : Msg(player, SellTitle),
                    buy
                        ? Msg(player, BuyTitle)
                        : Msg(player, SellTitle),
                    buy
                        ? $"{CmdMainConsole} try_buy_item {item.ID}"
                        : $"{CmdMainConsole} try_sell_item {item.ID}",
                    buy
                        ? $"{CmdMainConsole} try_buy_item {item.ID} {amount - 1}"
                        : $"{CmdMainConsole} try_sell_item {item.ID} {amount - 1}",
                    buy
                        ? $"{CmdMainConsole} try_buy_item {item.ID} {amount + 1}"
                        : $"{CmdMainConsole} try_sell_item {item.ID} {amount + 1}",
                    buy
                        ? $"{CmdMainConsole} try_buy_item {item.ID} all"
                        : $"{CmdMainConsole} try_sell_item {item.ID} all",
                    buy
                        ? $"{CmdMainConsole} fastbuyitem {item.ID} {amount}"
                        : $"{CmdMainConsole} sellitem {item.ID} {amount}",
                    $"{CmdMainConsole} shop_buy {item.ID}");
            });
        }

        private void ErrorUi(BasePlayer player, string msg)
        {
            var container = new CuiElementContainer
            {
                {
                    new CuiPanel
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                        Image = {Color = _config.UI.Color13.Get()},
                        CursorEnabled = true
                    },
                    _config.UI.DisplayType, ModalLayer, ModalLayer
                },
                {
                    new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-127.5 -75",
                            OffsetMax = "127.5 140"
                        },
                        Image = {Color = _config.UI.Color5.Get()}
                    },
                    ModalLayer, ModalLayer + ".Main"
                },
                {
                    new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -165", OffsetMax = "0 0"
                        },
                        Text =
                        {
                            Text = Msg(player, ErrorMsg),
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 120,
                            Color = _config.UI.Color8.Get()
                        }
                    },
                    ModalLayer + ".Main"
                },
                {
                    new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -175", OffsetMax = "0 -135"
                        },
                        Text =
                        {
                            Text = $"{msg}",
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = _config.UI.Color8.Get()
                        }
                    },
                    ModalLayer + ".Main"
                },
                {
                    new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 30"
                        },
                        Text =
                        {
                            Text = Msg(player, ErrorClose),
                            Align = TextAnchor.MiddleCenter,
                            Font = "robotocondensed-regular.ttf",
                            FontSize = 12,
                            Color = _config.UI.Color8.Get()
                        },
                        Button = {Color = _config.UI.Color7.Get(), Close = ModalLayer}
                    },
                    ModalLayer + ".Main"
                }
            };

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region UI Shop Item

        private void BuyButtonUI(BasePlayer player, ref List<string> elements, ShopItem shopItem,
            int selectedEconomy)
        {
            var shopUI = GetShop(player).GetUI();

            var hasSell = shopItem.CanBeSold();

            var buttonTemplate = hasSell ? shopUI.ShopItem.BuyButton : shopUI.ShopItem.BuyButtonIfNoSell;

            var cooldownTime = GetCooldownTime(player.userID, shopItem, true);
            if (cooldownTime > 0)
            {
                elements.Add(buttonTemplate.Cooldown.Background.GetSerialized(player,
                    Layer + $".Item.{shopItem.ID}", Layer + $".Item.{shopItem.ID}.Buy",
                    Layer + $".Item.{shopItem.ID}.Buy"));

                elements.Add(buttonTemplate.Cooldown.Title.GetSerialized(player, Layer + $".Item.{shopItem.ID}.Buy",
                    textFormatter: t => Msg(player, BuyCooldownTitle)));

                elements.Add(buttonTemplate.Cooldown.LeftTime?.GetSerialized(player, Layer + $".Item.{shopItem.ID}.Buy",
                    textFormatter: t => "%TIME_LEFT%",
                    countdownSettings: (0, cooldownTime, 1, TimerFormat.HoursMinutesSeconds, true,
                        $"{CmdMainConsole} refresh_shop_item {shopItem.ID}")));
            }
            else
            {
                elements.Add(buttonTemplate.Background.GetSerialized(player, Layer + $".Item.{shopItem.ID}",
                    Layer + $".Item.{shopItem.ID}.Buy", Layer + $".Item.{shopItem.ID}.Buy"));

                elements.Add(buttonTemplate.Title.GetSerialized(player, Layer + $".Item.{shopItem.ID}.Buy",
                    textFormatter: t => Msg(player, BuyTitle)));

                elements.Add(buttonTemplate.Price.GetSerialized(player, Layer + $".Item.{shopItem.ID}.Buy",
                    textFormatter: t => shopItem.Price <= 0.0
                        ? Msg(player, ItemPriceFree)
                        : GetPlayerEconomy(player).GetPriceTitle(player,
                            shopItem.GetPrice(player, selectedEconomy).ToString(_config.Formatting.BuyPriceFormat))));

                elements.Add(CuiJsonFactory.CreateButton(parent: Layer + $".Item.{shopItem.ID}.Buy",
                    anchorMin: "0 0", anchorMax: "1 1", color: "0 0 0 0", text: string.Empty,
                    command: !shopUI.ShopBasket.UseShopBasket || shopItem.ForceBuy
                        ? $"{CmdMainConsole} try_buy_item {shopItem.ID}"
                        : $"{CmdMainConsole} buyitem {shopItem.ID}"));
            }
        }

        private void SellButtonUI(BasePlayer player, ref List<string> elements, ShopItem shopItem,
            int selectedEconomy)
        {
            var shopUI = GetShop(player).GetUI();

            var hasBuy = shopItem.CanBePurchased();

            var buttonTemplate = hasBuy ? shopUI.ShopItem.SellButton : shopUI.ShopItem.SellButtonIfNoBuy;

            var cooldownTime = GetCooldownTime(player.userID, shopItem, false);
            if (cooldownTime > 0)
            {
                elements.Add(buttonTemplate.Cooldown.Background.GetSerialized(player,
                    Layer + $".Item.{shopItem.ID}", Layer + $".Item.{shopItem.ID}.Sell",
                    Layer + $".Item.{shopItem.ID}.Sell"));

                elements.Add(buttonTemplate.Cooldown.Title.GetSerialized(player, Layer + $".Item.{shopItem.ID}.Sell",
                    textFormatter: t => Msg(player, SellCooldownTitle)));

                elements.Add(buttonTemplate.Cooldown.LeftTime?.GetSerialized(player,
                    Layer + $".Item.{shopItem.ID}.Sell",
                    textFormatter: t => "%TIME_LEFT%",
                    countdownSettings: (0, cooldownTime, 1, TimerFormat.HoursMinutesSeconds, true,
                        $"{CmdMainConsole} refresh_shop_item {shopItem.ID}")));
            }
            else
            {
                elements.Add(buttonTemplate.Background.GetSerialized(player, Layer + $".Item.{shopItem.ID}",
                    Layer + $".Item.{shopItem.ID}.Sell", Layer + $".Item.{shopItem.ID}.Sell"));

                elements.Add(buttonTemplate.Title.GetSerialized(player, Layer + $".Item.{shopItem.ID}.Sell",
                    textFormatter: t => Msg(player, SellTitle)));

                elements.Add(buttonTemplate.Price.GetSerialized(player, Layer + $".Item.{shopItem.ID}.Sell",
                    textFormatter: t => shopItem.SellPrice <= 0.0
                        ? Msg(player, ItemPriceFree)
                        : GetPlayerEconomy(player).GetPriceTitle(player,
                            shopItem.GetSellPrice(player, selectedEconomy)
                                .ToString(_config.Formatting.SellPriceFormat))));

                elements.Add(CuiJsonFactory.CreateButton(parent: Layer + $".Item.{shopItem.ID}.Sell",
                    anchorMin: "0 0", anchorMax: "1 1", color: "0 0 0 0", text: string.Empty,
                    command: $"{CmdMainConsole} try_sell_item {shopItem.ID}"));
            }
        }

        private void ShopItemButtonsUI(BasePlayer player, ref List<string> elements, ShopItem shopItem,
            int selectedEconomy)
        {
            if (shopItem.CanBePurchased())
                BuyButtonUI(player, ref elements, shopItem, selectedEconomy);

            if (shopItem.CanBeSold())
                SellButtonUI(player, ref elements, shopItem, selectedEconomy);
        }

        private void ItemUI(BasePlayer player,
            ShopItem shopItem,
            ref List<string> elements)
        {
            var selectedEconomy = API_GetShopPlayerSelectedEconomy(player.userID);

            var shop = GetShop(player);

            var shopUI = shop.GetUI();

            elements.Add(shopUI.ShopItem.Background.GetSerialized(player, Layer + $".Item.{shopItem.ID}.Background",
                Layer + $".Item.{shopItem.ID}", Layer + $".Item.{shopItem.ID}"));

            #region Blueprint

            if (shopItem.Blueprint)
                elements.Add(shopUI.ShopItem.Blueprint.GetSerialized(player, Layer + $".Item.{shopItem.ID}",
                    customImageSettings: (false, string.Empty, string.Empty, string.Empty, string.Empty,
                        ItemManager.blueprintBaseDef.itemid, null)));

            #endregion

            elements.Add(shopUI.ShopItem.Image.GetSerialized(player, Layer + $".Item.{shopItem.ID}",
                customImageSettings: shopItem.GetImage()));

            #region Title

            elements.Add(shopUI.ShopItem.Title.GetSerialized(player, Layer + $".Item.{shopItem.ID}",
                textFormatter: t =>
                    shopItem.GetPublicTitle(player) +
                    (CanAccesToItem(player, shopItem) ? "" : " <color=red>*</color>")));

            #endregion

            #region Favorite

            if (_isEnabledFavorites && shopItem.CanBeFavorite(player.userID))
                ItemFavoriteUI(player, ref elements, shopUI, shopItem);

            #endregion

            #region Discount

            var discount = shopItem.GetDiscount(player);
            if (discount > 0)
            {
                elements.Add(shopUI.ShopItem.Discount.Background.GetSerialized(player, Layer + $".Item.{shopItem.ID}",
                    Layer + $".Item.{shopItem.ID}.Discount", Layer + $".Item.{shopItem.ID}.Discount"));

                elements.Add(shopUI.ShopItem.Discount.Value.GetSerialized(player,
                    Layer + $".Item.{shopItem.ID}.Discount", textFormatter: t => $"-{discount}%"));
            }

            #endregion

            #region Amount

            elements.Add(shopUI.ShopItem.Amount.Title.GetSerialized(player, Layer + $".Item.{shopItem.ID}",
                textFormatter: t => Msg(player, ItemAmount)));

            elements.Add(shopUI.ShopItem.Amount.Background.GetSerialized(player, Layer + $".Item.{shopItem.ID}",
                Layer + $".Item.{shopItem.ID}.Amount", Layer + $".Item.{shopItem.ID}.Amount"));

            elements.Add(shopUI.ShopItem.Amount.Value.GetSerialized(player, Layer + $".Item.{shopItem.ID}.Amount",
                textFormatter: t => Msg(player, UIShopItemAmount, shopItem.Amount)));

            #endregion

            #region Info

            if (!string.IsNullOrEmpty(shopItem.Description))
                shopUI.ShopItem.Info.GetButton(player, ref elements, Layer + $".Item.{shopItem.ID}",
                    titleText: Msg(player, InfoTitle),
                    command: $"{CmdMainConsole} item_info {shopItem.ID}");

            #endregion

            #region Buttons

            ShopItemButtonsUI(player, ref elements, shopItem, selectedEconomy);

            #endregion

            #region Edit

            if (IsAdminMode(player) && shop?.GetSelectedShopCategory()?.CategoryType != ShopCategory.Type.Favorite)
            {
                elements.Add(shopUI.ShopItem.AdminPanel.Background.GetSerialized(player, Layer + $".Item.{shopItem.ID}",
                    Layer + $".Item.{shopItem.ID}.Settings", Layer + $".Item.{shopItem.ID}.Settings"));

                elements.Add(shopUI.ShopItem.AdminPanel.ButtonEdit.GetSerialized(player,
                    Layer + $".Item.{shopItem.ID}.Settings", textFormatter: t => Msg(player, BtnEditCategory),
                    cmdFormatter: t => $"{CmdMainConsole} edit_item start {shopItem.ID}"));

                if (shop.canShowCategoriesMoveButtons)
                {
                    elements.Add(shopUI.ShopItem.AdminPanel.ButtonMoveRight.GetSerialized(player,
                        Layer + $".Item.{shopItem.ID}.Settings", textFormatter: t => "▶",
                        cmdFormatter: t => $"{CmdMainConsole} edit_item_move {shopItem.ID} right"));

                    elements.Add(shopUI.ShopItem.AdminPanel.ButtonMoveLeft.GetSerialized(player,
                        Layer + $".Item.{shopItem.ID}.Settings", textFormatter: t => "◀",
                        cmdFormatter: t => $"{CmdMainConsole} edit_item_move {shopItem.ID} left"));
                }
            }

            #endregion
        }

        private void ItemFavoriteUI(BasePlayer player, ref List<string> elements, ShopUI shopUI, ShopItem shopItem)
        {
            var isFavorite = shopItem.IsFavorite(player.userID);

            (isFavorite
                    ? shopUI.ShopItem.Favorite.RemoveFromFavorites
                    : shopUI.ShopItem.Favorite.AddToFavorites)
                .GetButton(player, ref elements, Layer + $".Item.{shopItem.ID}",
                    Layer + $".Item.{shopItem.ID}.Favorite", string.Empty, isFavorite
                        ? $"{CmdMainConsole} favorites item remove {shopItem.ID}"
                        : $"{CmdMainConsole} favorites item add {shopItem.ID}");
        }

        #endregion

        private void BalanceUi(ref List<string> elements, BasePlayer player)
        {
            var shopUI = GetShop(player).GetUI();

            var nowEconomy = GetPlayerEconomy(player);

            elements.Add(shopUI.ShopContent.Header.Balance.Value.GetSerialized(player, Layer + ".Balance",
                Layer + ".Balance.Value", Layer + ".Balance.Value",
                textFormatter: t => nowEconomy.GetBalanceTitle(player)));
        }

        private void ShowChoiceEconomyUI(BasePlayer player, ref List<string> elements, bool selected = false)
        {
            CuiHelper.DestroyUi(player, ModalLayer);

            var shopUI = GetShop(player).GetUI();

            elements.Add(shopUI.ShopContent.Header.ButtonToggleEconomy.GetSerialized(player, Layer + ".Header",
                Layer + ".Change.Economy", Layer + ".Change.Economy",
                textFormatter: t => Msg(player, UIContentHeaderButtonToggleEconomy),
                cmdFormatter: t => $"{CmdMainConsole} economy_try_change {!selected}"));

            #region Selection

            if (selected)
            {
                var nowEconomy = GetPlayerEconomy(player);
                var isVertical = shopUI.SelectCurrency.UseVerticalLayout;

                var economyCount = _economics.Count;

                float panelWidth;
                if (isVertical)
                    panelWidth = shopUI.SelectCurrency.EconomyWidth;
                else
                    panelWidth = economyCount * shopUI.SelectCurrency.EconomyWidth +
                                 (economyCount - 1) * shopUI.SelectCurrency.EconomyMargin;

                var halfWidth = panelWidth / 2f;

                #region Background

                var parent = shopUI.SelectCurrency.UseFullscreenBackground
                    ? "OverlayNonScaled"
                    : Layer + ".Change.Economy";

                elements.Add(shopUI.SelectCurrency.Background.GetSerialized(player, parent, ModalLayer, ModalLayer,
                    customRect: shopUI.SelectCurrency.UseFullscreenBackground
                        ? null
                        : ("0.5 1", "0.5 1",
                            $"-{halfWidth + shopUI.SelectCurrency.FrameWidth} {shopUI.SelectCurrency.FrameIndent}",
                            $"{halfWidth + shopUI.SelectCurrency.FrameWidth} {shopUI.SelectCurrency.FrameIndent + shopUI.SelectCurrency.EconomyHeight + shopUI.SelectCurrency.FrameHeader}")));

                elements.Add(shopUI.SelectCurrency.Title.GetSerialized(player, ModalLayer,
                    textFormatter: t => Msg(player, ChoiceEconomy)));

                #endregion

                #region Economics

                var xOffset = isVertical ? 0f : -halfWidth;
                var yOffset = isVertical ? -shopUI.SelectCurrency.FrameIndent : 0f;

                foreach (var economyConf in _economics)
                {
                    var (panelAnchorMin, panelAnchorMax, panelOffsetMin, panelOffsetMax) = isVertical
                        ? ("0.5 1", "0.5 1",
                            $"{-shopUI.SelectCurrency.EconomyWidth / 2f} {-yOffset - shopUI.SelectCurrency.EconomyHeight - shopUI.SelectCurrency.EconomyIndent}",
                            $"{shopUI.SelectCurrency.EconomyWidth / 2f} {-yOffset - shopUI.SelectCurrency.EconomyIndent}")
                        : ("0.5 0", "0.5 0", $"{xOffset} {shopUI.SelectCurrency.EconomyIndent}",
                            $"{xOffset + shopUI.SelectCurrency.EconomyWidth} {shopUI.SelectCurrency.EconomyIndent + shopUI.SelectCurrency.EconomyHeight}");

                    elements.Add(CuiJsonFactory.CreatePanel(parent: ModalLayer,
                        name: Layer + $".Change.Economy.Panel.{economyConf.ID}",
                        destroy: Layer + $".Change.Economy.Panel.{economyConf.ID}", anchorMin: panelAnchorMin,
                        anchorMax: panelAnchorMax, offsetMin: panelOffsetMin, offsetMax: panelOffsetMax,
                        color: economyConf.IsSame(nowEconomy)
                            ? shopUI.SelectCurrency.SelectedEconomyColor.Get()
                            : shopUI.SelectCurrency.UnselectedEconomyColor.Get(),
                        material: shopUI.SelectCurrency.EconomyPanelMaterial,
                        sprite: shopUI.SelectCurrency.EconomyPanelSprite));

                    elements.Add(shopUI.SelectCurrency.EconomyTitle.GetSerialized(player,
                        Layer + $".Change.Economy.Panel.{economyConf.ID}",
                        textFormatter: t => economyConf.GetTitle(player)));

                    elements.Add(CuiJsonFactory.CreateButton(parent: Layer + $".Change.Economy.Panel.{economyConf.ID}",
                        anchorMin: "0 0", anchorMax: "1 1", color: "0 0 0 0", text: string.Empty,
                        command: $"{CmdMainConsole} economy_set {economyConf.ID}"));

                    if (isVertical)
                        yOffset += shopUI.SelectCurrency.EconomyHeight + shopUI.SelectCurrency.EconomyMargin;
                    else
                        xOffset += shopUI.SelectCurrency.EconomyWidth + shopUI.SelectCurrency.EconomyMargin;
                }

                #endregion
            }

            #endregion
        }

        #endregion

        #region Transfer

        private const float
            SELECT_PLAYER_WIDTH = 180f,
            SELECT_PLAYER_HEIGHT = 50f,
            SELECT_PLAYER_MARGIN_X = 20f,
            SELECT_PLAYER_MARGIN_Y = 30f,
            SELECT_PLAYER_CONST_SWITCH_Y = -180f,
            SELECT_PLAYER_CONST_SWITCH_X = -(SELECT_PLAYER_AMOUNT_ON_STRING * SELECT_PLAYER_WIDTH +
                                             (SELECT_PLAYER_AMOUNT_ON_STRING - 1) * SELECT_PLAYER_MARGIN_X) / 2f,
            SELECT_PLAYER_PAGE_SIZE = 25f,
            SELECT_PLAYER_SELECTED_PAGE_SIZE = 40f,
            SELECT_PLAYER_PAGES_MARGIN = 5f;

        private const int
            SELECT_PLAYER_AMOUNT_ON_STRING = 4,
            SELECT_PLAYER_STRINGS = 5,
            SELECT_PLAYER_TOTAL_AMOUNT = SELECT_PLAYER_AMOUNT_ON_STRING * SELECT_PLAYER_STRINGS;

        private void SelectTransferPlayerUI(BasePlayer player, int selectPage = 0)
        {
            #region Fields

            var players = GetAvailablePlayersCount(player.userID);

            var container = new CuiElementContainer();

            #endregion

            #region Background

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image =
                {
                    Color = "0.19 0.19 0.18 0.65",
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                CursorEnabled = true
            }, _config.UI.DisplayType, ModalLayer, ModalLayer);

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Text = {Text = string.Empty},
                Button =
                {
                    Color = "0 0 0 0",
                    Close = ModalLayer
                }
            }, ModalLayer);

            #endregion

            if (players > 0)
            {
                #region Title

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "1 1",
                        OffsetMin = "0 -140",
                        OffsetMax = "0 -100"
                    },
                    Text =
                    {
                        Text = Msg(player, SelectPlayerTitle),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 32,
                        Color = _config.UI.Color8.Get()
                    }
                }, ModalLayer);

                #endregion

                #region Players

                var playersToShow = GetAvailablePlayersToTransfer(player.userID,
                    selectPage * SELECT_PLAYER_TOTAL_AMOUNT, SELECT_PLAYER_TOTAL_AMOUNT);

                try
                {
                    ShowGridUI(container, 0, playersToShow.Count, SELECT_PLAYER_AMOUNT_ON_STRING,
                        SELECT_PLAYER_MARGIN_X, SELECT_PLAYER_MARGIN_Y, SELECT_PLAYER_WIDTH, SELECT_PLAYER_HEIGHT,
                        SELECT_PLAYER_CONST_SWITCH_X, SELECT_PLAYER_CONST_SWITCH_Y, 0.5f, 0.5f, 1f, 1f, "0 0 0 0",
                        ModalLayer, i => ModalLayer + $".Player.{i}", null,
                        index =>
                        {
                            var member = playersToShow[index];

                            container.Add(new CuiElement
                            {
                                Parent = ModalLayer + $".Player.{index}",
                                Components =
                                {
                                    new CuiRawImageComponent
                                    {
                                        SteamId = member.UserIDString
                                    },
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0 0", AnchorMax = "0 0",
                                        OffsetMin = "0 0", OffsetMax = "50 50"
                                    }
                                }
                            });

                            container.Add(new CuiLabel
                                {
                                    RectTransform =
                                    {
                                        AnchorMin = "0 0.5", AnchorMax = "1 1",
                                        OffsetMin = "55 0", OffsetMax = "0 0"
                                    },
                                    Text =
                                    {
                                        Text = $"{member.displayName}",
                                        Align = TextAnchor.LowerLeft,
                                        Font = "robotocondensed-regular.ttf",
                                        FontSize = 18,
                                        Color = _config.UI.Color8.Get()
                                    }
                                }, ModalLayer + $".Player.{index}");

                            container.Add(new CuiButton
                                {
                                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                                    Text = {Text = string.Empty},
                                    Button =
                                    {
                                        Color = "0 0 0 0",
                                        Command =
                                            $"{CmdMainConsole} transfer_set_target {member.userID}"
                                    }
                                }, ModalLayer + $".Player.{index}");
                        });
                }
                finally
                {
                    Pool.FreeUnmanaged(ref playersToShow);
                }

                #endregion

                #region Pages

                var pages = (int) Math.Ceiling((double) players / SELECT_PLAYER_TOTAL_AMOUNT);
                if (pages > 1)
                {
                    var xSwitch = -((pages - 1) * SELECT_PLAYER_PAGE_SIZE + (pages - 1) * SELECT_PLAYER_PAGES_MARGIN +
                                    SELECT_PLAYER_SELECTED_PAGE_SIZE) / 2f;

                    for (var j = 0; j < pages; j++)
                    {
                        var selected = selectPage == j;

                        container.Add(new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                                OffsetMin = $"{xSwitch} 60",
                                OffsetMax =
                                    $"{xSwitch + (selected ? SELECT_PLAYER_SELECTED_PAGE_SIZE : SELECT_PLAYER_PAGE_SIZE)} {60 + (selected ? SELECT_PLAYER_SELECTED_PAGE_SIZE : SELECT_PLAYER_PAGE_SIZE)}"
                            },
                            Text =
                            {
                                Text = $"{j + 1}",
                                Align = TextAnchor.MiddleCenter,
                                Font = "robotocondensed-bold.ttf",
                                FontSize = selected ? 18 : 12,
                                Color = _config.UI.Color8.Get()
                            },
                            Button =
                            {
                                Color = _config.UI.Color2.Get(),
                                Command =
                                    $"{CmdMainConsole} transfer_page {j}"
                            }
                        }, ModalLayer);

                        xSwitch += (selected ? SELECT_PLAYER_SELECTED_PAGE_SIZE : SELECT_PLAYER_PAGE_SIZE) +
                                   SELECT_PLAYER_PAGES_MARGIN;
                    }
                }

                #endregion
            }
            else
            {
                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1"
                    },
                    Text =
                    {
                        Text = Msg(player, NoTransferPlayers),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 28,
                        Color = "1 1 1 0.85"
                    }
                }, ModalLayer);

                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                    Text = {Text = string.Empty},
                    Button =
                    {
                        Color = "0 0 0 0",
                        Close = ModalLayer
                    }
                }, ModalLayer);
            }

            CuiHelper.AddUi(player, container);
        }

        private void TransferUi(BasePlayer player,
            ulong targetId,
            float amount = 0)
        {
            var target = BasePlayer.FindAwakeOrSleepingByID(targetId);
            if (target == null) return;

            var container = new CuiElementContainer();

            #region Background

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image =
                {
                    Color = "0 0 0 0.9",
                    Material = "assets/content/ui/uibackgroundblur.mat"
                },
                CursorEnabled = true
            }, "OverlayNonScaled", ModalLayer, ModalLayer);

            container.Add(new CuiButton
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Text = {Text = string.Empty},
                Button =
                {
                    Color = "0 0 0 0",
                    Close = ModalLayer
                }
            }, ModalLayer);

            #endregion

            #region Main

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                    OffsetMin = "-125 -100",
                    OffsetMax = "125 75"
                },
                Image =
                {
                    Color = _config.UI.Color3.Get()
                }
            }, ModalLayer, ModalLayer + ".Main");

            #region Header

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = "0 -50", OffsetMax = "0 0"
                },
                Image =
                {
                    Color = _config.UI.Color1.Get()
                }
            }, ModalLayer + ".Main", ModalLayer + ".Header");

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1",
                    OffsetMin = "20 0", OffsetMax = "0 0"
                },
                Text =
                {
                    Text = Msg(player, TransferTitle),
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 14,
                    Color = _config.UI.Color8.Get()
                }
            }, ModalLayer + ".Header");

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = "-35 -37.5",
                    OffsetMax = "-10 -12.5"
                },
                Text =
                {
                    Text = Msg(player, CloseButton),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 10,
                    Color = _config.UI.Color8.Get()
                },
                Button =
                {
                    Close = ModalLayer,
                    Color = _config.UI.Color2.Get()
                }
            }, ModalLayer + ".Header");

            #endregion

            #region Player

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "-105 -110",
                    OffsetMax = "105 -60"
                },
                Image =
                {
                    Color = _config.UI.Color1.Get()
                }
            }, ModalLayer + ".Main", ModalLayer + ".Player");

            #region Avatar

            container.Add(new CuiElement
            {
                Parent = ModalLayer + ".Player",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        SteamId = target.UserIDString
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "0 0",
                        OffsetMin = "5 5",
                        OffsetMax = "45 45"
                    }
                }
            });

            #endregion

            #region Name

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1",
                    OffsetMin = "50 0", OffsetMax = "0 0"
                },
                Text =
                {
                    Text = $"{target.displayName}",
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 20,
                    Color = _config.UI.Color8.Get()
                }
            }, ModalLayer + ".Player");

            #endregion

            #endregion

            #region Send

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                    OffsetMin = "-105 -160",
                    OffsetMax = "105 -120"
                },
                Image =
                {
                    Color = _config.UI.Color1.Get()
                }
            }, ModalLayer + ".Main", ModalLayer + ".Send");

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 0.5", AnchorMax = "1 0.5",
                    OffsetMin = "-85 -12.5",
                    OffsetMax = "-5 12.5"
                },
                Text =
                {
                    Text = Msg(player, TransferButton),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 10,
                    Color = _config.UI.Color8.Get()
                },
                Button =
                {
                    Color = _config.UI.Color2.Get(),
                    Close = ModalLayer,
                    Command =
                        $"{CmdMainConsole} transfer_send {targetId} {amount}"
                }
            }, ModalLayer + ".Send");

            container.Add(new CuiElement
            {
                Parent = ModalLayer + ".Send",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        FontSize = 12,
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        Command =
                            $"{CmdMainConsole} transfer_set_amount {targetId}",
                        NeedsKeyboard = true,
                        Color = "1 1 1 0.75",
                        CharsLimit = 32,
                        Text = $"{amount}",
                        HudMenuInput = true
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "0 0", OffsetMax = "-90 0"
                    }
                }
            });

            #endregion

            #endregion

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Editor

        private List<(string FlagPath, string LangKey, string LangName)> _langList = new()
        {
            ("assets/icons/flags/af.png", "af", "Afrikaans"),
            ("assets/icons/flags/ar.png", "ar", "العربية"),
            ("assets/icons/flags/ca.png", "ca", "Català"),
            ("assets/icons/flags/cs.png", "cs", "Čeština"),
            ("assets/icons/flags/da.png", "da", "Dansk"),
            ("assets/icons/flags/de.png", "de", "Deutsch"),
            ("assets/icons/flags/el.png", "el", "Ελληνικά"),
            ("assets/icons/flags/en-pt.png", "en-PT", "Portuguese (Portugal)"),
            ("assets/icons/flags/en.png", "en", "English"),
            ("assets/icons/flags/es-es.png", "es-ES", "Español (España)"),
            ("assets/icons/flags/fi.png", "fi", "Suomi"),
            ("assets/icons/flags/fr.png", "fr", "Français"),
            ("assets/icons/flags/he.png", "he", "עברית"),
            ("assets/icons/flags/hu.png", "hu", "Magyar"),
            ("assets/icons/flags/it.png", "it", "Italiano"),
            ("assets/icons/flags/ja.png", "ja", "日本語"),
            ("assets/icons/flags/ko.png", "ko", "한국어"),
            ("assets/icons/flags/nl.png", "nl", "Nederlands"),
            ("assets/icons/flags/no.png", "no", "Norsk"),
            ("assets/icons/flags/pl.png", "pl", "Polski"),
            ("assets/icons/flags/pt-br.png", "pt-BR", "Português (Brasil)"),
            ("assets/icons/flags/pt-pt.png", "pt-PT", "Português (Portugal)"),
            ("assets/icons/flags/ro.png", "ro", "Română"),
            ("assets/icons/flags/ru.png", "ru", "Русский"),
            ("assets/icons/flags/sr.png", "sr", "Српски"),
            ("assets/icons/flags/sv-se.png", "sv-SE", "Svenska"),
            ("assets/icons/flags/tr.png", "tr", "Türkçe"),
            ("assets/icons/flags/uk.png", "uk", "Українська"),
            ("assets/icons/flags/vi.png", "vi", "Tiếng Việt"),
            ("assets/icons/flags/zh-cn.png", "zh-CN", "中文 (简体)"),
            ("assets/icons/flags/zh-tw.png", "zh-TW", "中文 (繁體)")
        };

        #region Categories.Editor

        private void ShowEditCategoryPanel(BasePlayer player, bool first = false)
        {
            var editData = EditCategoryData.Get(player.userID);
            if (editData == null) return;

            var container = new CuiElementContainer();

            #region Background

            container.Add(new CuiElement
            {
                Parent = "OverlayNonScaled",
                Name = EditingLayer,
                DestroyUi = EditingLayer,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0.9",
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = "0 0",
                        OffsetMax = "0 0"
                    }
                }
            });

            #endregion Background

            #region Content

            container.Add(new CuiElement
            {
                Name = EditingLayer + ".Content",
                Parent = EditingLayer,
                Components =
                {
                    new CuiImageComponent {Color = HexToCuiColor("#222222")},
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-250 -320",
                        OffsetMax = "250 320"
                    }
                }
            });

            #region Header

            container.Add(new CuiElement
            {
                Name = EditingLayer + ".Header",
                Parent = EditingLayer + ".Content",
                Components =
                {
                    new CuiImageComponent {Color = HexToCuiColor("#181819")},
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "1 1",
                        OffsetMin = "0 -47",
                        OffsetMax = "0 0"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Name = EditingLayer + ".Header.Title",
                Parent = EditingLayer + ".Header",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "EDIT CATEGORY",
                        Font = "robotocondensed-bold.ttf", FontSize = 20,
                        Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#FFFFFF", 80)
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = "20 0",
                        OffsetMax = "0 0"
                    }
                }
            });

            #region Close Button

            container.Add(new CuiElement
            {
                Name = EditingLayer + ".Header.CloseButton",
                Parent = EditingLayer + ".Header",
                Components =
                {
                    new CuiButtonComponent
                        {Color = HexToCuiColor("#222222"), Command = $"{CmdMainConsole} edit_category cancel"},
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "1 0.5",
                        AnchorMax = "1 0.5",
                        OffsetMin = "-90 -13.5",
                        OffsetMax = "-20 13.5"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = EditingLayer + ".Header.CloseButton",
                Components =
                {
                    new CuiImageComponent {Color = HexToCuiColor("#FFFFFF", 60), Sprite = "assets/icons/exit.png"},
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-26 -6.5",
                        OffsetMax = "-13 6.5"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = EditingLayer + ".Header.CloseButton",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "CLOSE", Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft,
                        Color = HexToCuiColor("#FFFFFF", 60)
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "27 -27",
                        OffsetMax = "70 0"
                    }
                }
            });

            #endregion Close Button

            #endregion Header

            #region Scroll

            var totalHeight = 0f;

            foreach (var (name, field, value) in editData.GetEditableFields()) totalHeight += 40f + 4f;

            #region Scroll View

            var scrollContent = new CuiRectTransform
            {
                AnchorMin = "0 1",
                AnchorMax = "1 1",
                OffsetMin = $"0 -{totalHeight}",
                OffsetMax = "0 0"
            };

            container.Add(new CuiElement
            {
                Parent = EditingLayer + ".Content",
                Name = EditingLayer + ".Content.View",
                DestroyUi = EditingLayer + ".Content.View",
                Components =
                {
                    new CuiImageComponent {Color = "0 0 0 0"},
                    new CuiScrollViewComponent
                    {
                        MovementType = ScrollRect.MovementType.Clamped,
                        Vertical = true,
                        Inertia = true,
                        Horizontal = false,
                        Elasticity = 0.25f,
                        DecelerationRate = 0.3f,
                        ScrollSensitivity = 24f,
                        ContentTransform = scrollContent,
                        VerticalScrollbar = new CuiScrollbar
                        {
                            Invert = false,
                            Size = 5f, AutoHide = true,
                            HandleColor = HexToCuiColor("#AA4735"),
                            TrackColor = HexToCuiColor("#000000", 50),
                            HandleSprite = "assets/content/ui/UI.Background.TileTex.psd",
                            TrackSprite = "assets/content/ui/UI.Background.TileTex.psd"
                        }
                    },
                    new CuiRectTransformComponent
                        {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "20 68", OffsetMax = "-8 -60"}
                }
            });

            #endregion Scroll View

            #region Scroll Content

            var offsetY = 0f;

            #region Header

            container.Add(new CuiElement
            {
                Parent = EditingLayer + ".Content.View",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "GENERAL",
                        Font = "robotocondensed-bold.ttf", FontSize = 20,
                        Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#FFFFFF", 80)
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = $"0 {offsetY - 25}",
                        OffsetMax = $"300 {offsetY}"
                    }
                }
            });

            offsetY = offsetY - 25 - 20;

            #endregion Header

            #region Loop

            var editableFieldsCount = editData.GetEditableFieldsCount();

            var editableFieldsIndex = 0;

            foreach (var (name, field, value) in editData.GetEditableFields())
            {
                var fieldLayer = CuiHelper.GetGuid();

                container.Add(new CuiPanel
                {
                    Image = {Color = HexToCuiColor("#000000", 30)},
                    RectTransform =
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = $"0 {offsetY - 40}",
                        OffsetMax = $"460 {offsetY}"
                    }
                }, EditingLayer + ".Content.View", fieldLayer + ".Background", fieldLayer + ".Background");

                FieldItemUI(player, container, fieldLayer, field, value, "edit_category", editData.editingCategory);

                #region Calculate Position

                offsetY -= 40f;

                if (editableFieldsIndex != editableFieldsCount - 1)
                    offsetY -= 4f;

                editableFieldsIndex++;

                #endregion
            }

            #endregion Loop

            offsetY -= 20f;

            #region Localization

            #region Header

            container.Add(new CuiElement
            {
                Parent = EditingLayer + ".Content.View",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "LOCALIZATION",
                        Font = "robotocondensed-bold.ttf", FontSize = 20,
                        Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#FFFFFF", 80)
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = $"0 {offsetY - 25}",
                        OffsetMax = $"300 {offsetY}"
                    }
                }
            });

            offsetY = offsetY - 25 - 20;

            #endregion Header

            #region Lines

            for (var i = 0; i < _langList.Count; i++)
            {
                var (_, langKey, _) = _langList[i];
                var lineLayer = EditingLayer + ".Content.View.Localization.Line." + langKey;

                container.Add(new CuiPanel
                    {
                        Image = {Color = HexToCuiColor("#000000", 30)},
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"0 {offsetY - 40}",
                            OffsetMax = $"460 {offsetY}"
                        }
                    }, EditingLayer + ".Content.View", lineLayer + ".Background",
                    lineLayer + ".Background");

                FieldLocalizationUI(player, container, editData.editingCategory.Localization.Messages, langKey,
                    "edit_category");

                offsetY -= 40;

                if (i < _langList.Count - 1)
                    offsetY -= 4;
            }

            #endregion Lines

            #endregion Localization

            #endregion Scroll Content

            scrollContent.OffsetMin = $"0 -{Mathf.Abs(offsetY)}";

            #endregion Scroll

            #region Buttons

            if (!editData.isNew)
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0",
                        AnchorMax = "0.5 0",
                        OffsetMin = "-230 20",
                        OffsetMax = "-38 48"
                    },
                    Text =
                    {
                        Text = "DELETE", Font = "robotocondensed-bold.ttf", FontSize = 12,
                        Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#FFFFFF", 60)
                    },
                    Button =
                    {
                        Color = HexToCuiColor("#AA4735"),
                        Command = $"{CmdMainConsole} edit_category remove"
                    }
                }, EditingLayer + ".Content");

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0",
                        AnchorMax = "0.5 0",
                        OffsetMin = "-34 20",
                        OffsetMax = "230 48"
                    },
                    Text =
                    {
                        Text = "SAVE", Font = "robotocondensed-bold.ttf", FontSize = 12,
                        Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#FFFFFF", 60)
                    },
                    Button =
                    {
                        Color = HexToCuiColor("#5D7238"),
                        Command = $"{CmdMainConsole} edit_category save"
                    }
                }, EditingLayer + ".Content");
            }
            else
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0",
                        AnchorMax = "0.5 0",
                        OffsetMin = "-230 20",
                        OffsetMax = "230 48"
                    },
                    Text =
                    {
                        Text = "SAVE", Font = "robotocondensed-bold.ttf", FontSize = 12,
                        Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#FFFFFF", 60)
                    },
                    Button =
                    {
                        Color = HexToCuiColor("#5D7238"),
                        Command = $"{CmdMainConsole} edit_category save"
                    }
                }, EditingLayer + ".Content");
            }

            #endregion Buttons

            #endregion Content

            CuiHelper.AddUi(player, container);
        }

        #endregion Categories.Editor

        #region Items.Editor

        private void ShowEditItemPanel(BasePlayer player)
        {
            var editData = EditItemData.Get(player.userID);
            if (editData == null) return;

            var container = new CuiElementContainer();

            #region Background

            container.Add(new CuiElement
            {
                Parent = "OverlayNonScaled",
                Name = EditingLayer,
                DestroyUi = EditingLayer,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0.9",
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = "0 0",
                        OffsetMax = "0 0"
                    }
                }
            });

            #endregion Background

            #region Content

            container.Add(new CuiElement
            {
                Name = EditingLayer + ".Content",
                Parent = EditingLayer,
                Components =
                {
                    new CuiImageComponent {Color = HexToCuiColor("#222222")},
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-250 -320",
                        OffsetMax = "250 320"
                    }
                }
            });

            #region Header

            container.Add(new CuiElement
            {
                Name = EditingLayer + ".Header",
                Parent = EditingLayer + ".Content",
                Components =
                {
                    new CuiImageComponent {Color = HexToCuiColor("#181819")},
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "1 1",
                        OffsetMin = "0 -47",
                        OffsetMax = "0 0"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Name = EditingLayer + ".Header.Title",
                Parent = EditingLayer + ".Header",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "EDIT ITEM",
                        Font = "robotocondensed-bold.ttf", FontSize = 20,
                        Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#FFFFFF", 80)
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = "20 0",
                        OffsetMax = "0 0"
                    }
                }
            });

            #region Close Button

            container.Add(new CuiElement
            {
                Name = EditingLayer + ".Header.CloseButton",
                Parent = EditingLayer + ".Header",
                Components =
                {
                    new CuiButtonComponent
                        {Color = HexToCuiColor("#222222"), Command = $"{CmdMainConsole} edit_item cancel"},
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "1 0.5",
                        AnchorMax = "1 0.5",
                        OffsetMin = "-90 -13.5",
                        OffsetMax = "-20 13.5"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = EditingLayer + ".Header.CloseButton",
                Components =
                {
                    new CuiImageComponent {Color = HexToCuiColor("#FFFFFF", 60), Sprite = "assets/icons/exit.png"},
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-26 -6.5",
                        OffsetMax = "-13 6.5"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = EditingLayer + ".Header.CloseButton",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "CLOSE", Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft,
                        Color = HexToCuiColor("#FFFFFF", 60)
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "27 -27",
                        OffsetMax = "70 0"
                    }
                }
            });

            #endregion Close Button

            #endregion Header

            #region Scroll

            var totalHeight = 0f;

            foreach (var (name, field, value) in editData.GetEditableFields()) totalHeight += 40f + 4f;

            totalHeight += 116 + 4;

            totalHeight = Mathf.Max(totalHeight, 512);

            #region Scroll View

            var scrollContent = new CuiRectTransform
            {
                AnchorMin = "0 1",
                AnchorMax = "1 1",
                OffsetMin = $"0 -{totalHeight}",
                OffsetMax = "0 0"
            };

            container.Add(new CuiElement
            {
                Parent = EditingLayer + ".Content",
                Name = EditingLayer + ".Content.View",
                DestroyUi = EditingLayer + ".Content.View",
                Components =
                {
                    new CuiImageComponent {Color = "0 0 0 0"},
                    new CuiScrollViewComponent
                    {
                        MovementType = ScrollRect.MovementType.Clamped,
                        Vertical = true,
                        Inertia = true,
                        Horizontal = false,
                        Elasticity = 0.25f,
                        DecelerationRate = 0.3f,
                        ScrollSensitivity = 24f,
                        ContentTransform = scrollContent,
                        VerticalScrollbar = new CuiScrollbar
                        {
                            Invert = false,
                            Size = 5f, AutoHide = true,
                            HandleColor = HexToCuiColor("#AA4735"),
                            TrackColor = HexToCuiColor("#000000", 50),
                            HandleSprite = "assets/content/ui/UI.Background.TileTex.psd",
                            TrackSprite = "assets/content/ui/UI.Background.TileTex.psd"
                        }
                    },
                    new CuiRectTransformComponent
                        {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "20 68", OffsetMax = "-8 -60"}
                }
            });

            #endregion Scroll View

            #region Scroll Content

            var offsetY = 0f;

            #region Header

            container.Add(new CuiElement
            {
                Parent = EditingLayer + ".Content.View",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "GENERAL",
                        Font = "robotocondensed-bold.ttf", FontSize = 20,
                        Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#FFFFFF", 80)
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = $"0 {offsetY - 25}",
                        OffsetMax = $"300 {offsetY}"
                    }
                }
            });

            offsetY = offsetY - 25 - 20;

            #endregion Header

            #region Loop

            var editableFieldsCount = editData.GetEditableFieldsCount();

            var editableFieldsIndex = 0;

            foreach (var (name, field, value) in editData.GetEditableFields())
            {
                var fieldLayer = CuiHelper.GetGuid();

                container.Add(new CuiPanel
                {
                    Image = {Color = HexToCuiColor("#000000", 30)},
                    RectTransform =
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = $"0 {offsetY - 40}",
                        OffsetMax = $"460 {offsetY}"
                    }
                }, EditingLayer + ".Content.View", fieldLayer + ".Background", fieldLayer + ".Background");

                FieldItemUI(player, container, fieldLayer, field, value, "edit_item", editData.editingItem);

                #region Calculate Position

                offsetY -= 40f;

                if (editableFieldsIndex != editableFieldsCount - 1)
                    offsetY -= 4f;

                editableFieldsIndex++;

                #endregion

                #region Preview Field

                if (name == nameof(ShopItem.Image))
                {
                    offsetY -= 112;
                    offsetY -= 4;
                }

                #endregion Preview Field
            }

            #endregion Loop

            #endregion Scroll Content

            #endregion Scroll

            #region Buttons

            if (!editData.isNew)
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0",
                        AnchorMax = "0.5 0",
                        OffsetMin = "-230 20",
                        OffsetMax = "-38 48"
                    },
                    Text =
                    {
                        Text = "DELETE", Font = "robotocondensed-bold.ttf", FontSize = 12,
                        Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#FFFFFF", 60)
                    },
                    Button =
                    {
                        Color = HexToCuiColor("#AA4735"),
                        Command = $"{CmdMainConsole} edit_item remove"
                    }
                }, EditingLayer + ".Content");

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0",
                        AnchorMax = "0.5 0",
                        OffsetMin = "-34 20",
                        OffsetMax = "230 48"
                    },
                    Text =
                    {
                        Text = "SAVE", Font = "robotocondensed-bold.ttf", FontSize = 12,
                        Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#FFFFFF", 60)
                    },
                    Button =
                    {
                        Color = HexToCuiColor("#5D7238"),
                        Command = $"{CmdMainConsole} edit_item save"
                    }
                }, EditingLayer + ".Content");
            }
            else
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0",
                        AnchorMax = "0.5 0",
                        OffsetMin = "-230 20",
                        OffsetMax = "230 48"
                    },
                    Text =
                    {
                        Text = "SAVE", Font = "robotocondensed-bold.ttf", FontSize = 12,
                        Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#FFFFFF", 60)
                    },
                    Button =
                    {
                        Color = HexToCuiColor("#5D7238"),
                        Command = $"{CmdMainConsole} edit_item save"
                    }
                }, EditingLayer + ".Content");
            }

            #endregion Buttons

            #endregion Content

            CuiHelper.AddUi(player, container);
        }

        private void SelectItemModal(BasePlayer player)
        {
            var editData = EditItemData.Get(player.userID);
            if (editData == null) return;

            var selectedCategory = editData.selectItemCategory;
            var selectedItem = editData.editingItem?.ShortName;

            if (!_itemsCategories.TryGetValue(selectedCategory, out var items))
                items = new List<(int itemID, string shortName)>();

            var container = new CuiElementContainer();

            #region Background

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image =
                {
                    Color = "0 0 0 0.9",
                    Material = "assets/content/ui/uibackgroundblur.mat"
                },
                CursorEnabled = true
            }, "OverlayNonScaled", ModalLayer, ModalLayer);

            #endregion Background

            #region Content

            container.Add(new CuiElement
            {
                Name = ModalLayer + ".Content",
                DestroyUi = ModalLayer + ".Content",
                Parent = ModalLayer,
                Components =
                {
                    new CuiImageComponent {Color = HexToCuiColor("#222222")},
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-590 -335",
                        OffsetMax = "590 335"
                    }
                }
            });

            #region Header

            container.Add(new CuiElement
            {
                Name = ModalLayer + ".Header",
                Parent = ModalLayer + ".Content",
                Components =
                {
                    new CuiImageComponent {Color = HexToCuiColor("#181819")},
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "1 1",
                        OffsetMin = "0 -47",
                        OffsetMax = "0 0"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Name = ModalLayer + ".Header.Title",
                Parent = ModalLayer + ".Header",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "SELECT ITEM",
                        Font = "robotocondensed-bold.ttf", FontSize = 20,
                        Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#FFFFFF", 80)
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = "20 0",
                        OffsetMax = "0 0"
                    }
                }
            });

            #region Close Button

            container.Add(new CuiElement
            {
                Name = ModalLayer + ".Header.CloseButton",
                Parent = ModalLayer + ".Header",
                Components =
                {
                    new CuiButtonComponent
                        {Color = HexToCuiColor("#222222"), Command = $"{CmdMainConsole} edit_item select_item close"},
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "1 0.5",
                        AnchorMax = "1 0.5",
                        OffsetMin = "-90 -13.5",
                        OffsetMax = "-20 13.5"
                    }
                }
            });
            container.Add(new CuiElement
            {
                Parent = ModalLayer + ".Header.CloseButton",
                Components =
                {
                    new CuiImageComponent {Color = HexToCuiColor("#FFFFFF", 60), Sprite = "assets/icons/exit.png"},
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-26 -6.5",
                        OffsetMax = "-13 6.5"
                    }
                }
            });

            container.Add(new CuiElement
            {
                Parent = ModalLayer + ".Header.CloseButton",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "CLOSE", Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft,
                        Color = HexToCuiColor("#FFFFFF", 60)
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "27 -27",
                        OffsetMax = "70 0"
                    }
                }
            });

            #endregion Close Button

            #endregion Header

            #region Categories

            var categoryWidth = 100;
            var categoryHeight = 18;
            var categoryMargin = 4;
            var categoriesOnString = 11;
            var categoriesLeftIndent = 20;

            var categoriesOffsetY = -60f;
            var categoriesOffsetX = categoriesLeftIndent;

            var categoryIndex = 0;
            foreach (var category in _itemsCategories.Keys)
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = $"{categoriesOffsetX} {categoriesOffsetY - categoryHeight}",
                        OffsetMax = $"{categoriesOffsetX + categoryWidth} {categoriesOffsetY}"
                    },
                    Text =
                    {
                        Text = category, Font = "robotocondensed-bold.ttf", FontSize = 10,
                        Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#FFFFFF", 60)
                    },
                    Button =
                    {
                        Color = selectedCategory == category ? HexToCuiColor("#5D7238") : HexToCuiColor("#000000", 50),
                        Command = $"{CmdMainConsole} edit_item select_item category {category}"
                    }
                }, ModalLayer + ".Content");

                if ((categoryIndex + 1) % categoriesOnString == 0)
                {
                    categoriesOffsetY = categoriesOffsetY - categoryHeight - categoryMargin;
                    categoriesOffsetX = categoriesLeftIndent;
                }
                else
                {
                    categoriesOffsetX += categoryWidth + categoryMargin;
                }

                categoryIndex++;
            }

            #endregion Categories

            #region Items

            var itemsHeight = 120;
            var itemsWidth = 100;
            var itemsMarginX = 4;
            var itemsMarginY = 10;
            var itemsOnString = 11;
            var itemsLeftIndent = 0;
            var itemsTopIndent = 0;

            var itemsOffsetY = -itemsTopIndent;
            var itemsOffsetX = itemsLeftIndent;

            var lines = Mathf.CeilToInt((float) items.Count / itemsOnString);
            var itemsTotalHeight = lines * itemsHeight + (lines - 1) * itemsMarginY;

            #region Scroll View

            var scrollContent = new CuiRectTransform
            {
                AnchorMin = "0 1",
                AnchorMax = "1 1",
                OffsetMin = $"0 -{itemsTotalHeight}",
                OffsetMax = "0 0"
            };

            container.Add(new CuiElement
            {
                Parent = ModalLayer + ".Content",
                Name = ModalLayer + ".Content.View",
                DestroyUi = ModalLayer + ".Content.View",
                Components =
                {
                    new CuiImageComponent {Color = "0 0 0 0"},
                    new CuiScrollViewComponent
                    {
                        MovementType = ScrollRect.MovementType.Clamped,
                        Vertical = true,
                        Inertia = true,
                        Horizontal = false,
                        Elasticity = 0.25f,
                        DecelerationRate = 0.3f,
                        ScrollSensitivity = 24f,
                        ContentTransform = scrollContent,
                        VerticalScrollbar = new CuiScrollbar
                        {
                            Invert = false,
                            Size = 5f, AutoHide = true,
                            HandleColor = HexToCuiColor("#AA4735"),
                            TrackColor = HexToCuiColor("#000000", 50),
                            HandleSprite = "assets/content/ui/UI.Background.TileTex.psd",
                            TrackSprite = "assets/content/ui/UI.Background.TileTex.psd"
                        }
                    },
                    new CuiRectTransformComponent
                        {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "20 20", OffsetMax = "-6 -110"}
                }
            });

            #endregion Scroll View

            #region Loop

            for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
            {
                var item = items[itemIndex];

                var itemDefinition = ItemManager.FindItemDefinition(item.itemID);

                var itemLayer = CuiHelper.GetGuid();

                container.Add(new CuiElement
                {
                    Name = itemLayer,
                    Parent = ModalLayer + ".Content.View",
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Color =
                                selectedItem == item.shortName ? HexToCuiColor("#5D7238") : HexToCuiColor("#0F0F0F"),
                            Command = $"{CmdMainConsole} edit_item select_item take {itemDefinition.shortname}"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "0 1",
                            OffsetMin = $"{itemsOffsetX} {itemsOffsetY - itemsHeight}",
                            OffsetMax = $"{itemsOffsetX + itemsWidth} {itemsOffsetY}"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = itemLayer,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = itemDefinition.displayName.translated, Font = "robotocondensed-regular.ttf",
                            FontSize = 10, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#FFFFFF")
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 0",
                            OffsetMin = "0 0",
                            OffsetMax = "0 20"
                        }
                    }
                });

                #region Item Icon

                container.Add(new CuiElement
                {
                    Name = itemLayer + ".Background",
                    Parent = itemLayer,
                    Components =
                    {
                        new CuiImageComponent {Color = HexToCuiColor("#363636")},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 1",
                            AnchorMax = "0.5 1",
                            OffsetMin = "-50 -100",
                            OffsetMax = "50 0"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = itemLayer + ".Background",
                    Components =
                    {
                        new CuiImageComponent {ItemId = itemDefinition.itemid},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-50 -50",
                            OffsetMax = "50 50"
                        }
                    }
                });

                #endregion Item Icon

                if ((itemIndex + 1) % itemsOnString == 0)
                {
                    itemsOffsetY = itemsOffsetY - itemsHeight - itemsMarginY;
                    itemsOffsetX = itemsLeftIndent;
                }
                else
                {
                    itemsOffsetX += itemsWidth + itemsMarginX;
                }
            }

            #endregion Loop

            #endregion Items

            #endregion Content

            CuiHelper.AddUi(player, container);
        }

        private void FieldLocalizationUI(BasePlayer player,
            CuiElementContainer container,
            Dictionary<string, string> localizations,
            string langKey,
            string commandPrefix)
        {
            var (flag, _, langName) = _langList.Find(l => l.LangKey == langKey);
            if (flag == null) return;

            var lineLayer = EditingLayer + ".Content.View.Localization.Line." + langKey;

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image = {Color = "0 0 0 0"}
            }, lineLayer + ".Background", lineLayer, lineLayer);

            container.Add(new CuiElement
            {
                Parent = lineLayer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = langName, Font = "robotocondensed-bold.ttf", FontSize = 12,
                        Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#FFFFFF", 80)
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = "12 0",
                        OffsetMax = "-268 0"
                    }
                }
            });

            #region TEXT

            container.Add(new CuiElement
            {
                Name = lineLayer + ".Text",
                Parent = lineLayer,
                Components =
                {
                    new CuiImageComponent {Color = HexToCuiColor("#000000", 50)},
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "1 0.5",
                        AnchorMax = "1 0.5",
                        OffsetMin = "-264 -14",
                        OffsetMax = "-4 14"
                    }
                }
            });

            var textValue = localizations.TryGetValue(langKey, out var text) ? text : string.Empty;

            container.Add(new CuiElement
            {
                Name = lineLayer + ".Text.Value",
                Parent = lineLayer + ".Text",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Text = textValue ?? string.Empty, Font = "robotocondensed-bold.ttf",
                        FontSize = 12, Align = TextAnchor.MiddleLeft,
                        Color = HexToCuiColor("#FFFFFF", 60),
                        Command = $"{CmdMainConsole} {commandPrefix} localize_text {langKey} text",
                        NeedsKeyboard = true
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = "12 0",
                        OffsetMax = "-12 0"
                    }
                }
            });

            #endregion TEXT
        }

        private static void FieldItemUI(BasePlayer player,
            CuiElementContainer container,
            string targetFieldLayer,
            FieldInfo targetField,
            object fieldValue,
            string commandPrefix,
            object originalObject = null)
        {
            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image = {Color = "0 0 0 0"}
            }, targetFieldLayer + ".Background", targetFieldLayer, targetFieldLayer);

            container.Add(new CuiElement
            {
                Parent = targetFieldLayer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = targetField.GetFieldTitle() ?? string.Empty, Font = "robotocondensed-bold.ttf",
                        FontSize = 12, Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#FFFFFF", 80)
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = "10 0",
                        OffsetMax = "-264 0"
                    }
                }
            });

            if (targetField.GetCustomAttribute<ShortNameAttribute>() != null)
            {
                #region Value

                var fieldValueText = fieldValue?.ToString() ?? string.Empty;

                container.Add(new CuiPanel
                {
                    Image = {Color = HexToCuiColor("#000000", 50)},
                    RectTransform =
                        {AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-264 -14", OffsetMax = "-4 14"}
                }, targetFieldLayer, targetFieldLayer + ".Value");

                container.Add(new CuiElement
                {
                    Parent = targetFieldLayer + ".Value",
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 12,
                            Align = TextAnchor.MiddleLeft,
                            Color = HexToCuiColor("#FFFFFF", 60),
                            Text = fieldValueText,
                            NeedsKeyboard = true,
                            Command = $"{CmdMainConsole} {commandPrefix} field {targetField.Name} {targetFieldLayer}"
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "10 0", OffsetMax = "-10 0"}
                    }
                });

                #endregion

                #region Select Item

                container.Add(new CuiButton
                {
                    Text =
                    {
                        Text = "SELECT",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 12,
                        Align = TextAnchor.MiddleCenter,
                        Color = HexToCuiColor("#FFFFFF", 60)
                    },
                    Button =
                    {
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                        Color = HexToCuiColor("#40403D"),
                        Command = $"{CmdMainConsole} {commandPrefix} select_item open"
                    },
                    RectTransform =
                    {
                        AnchorMin = "1 0.5",
                        AnchorMax = "1 0.5",
                        OffsetMin = "-131 -14",
                        OffsetMax = "-4 14"
                    }
                }, targetFieldLayer);

                #endregion Select Item
            }
            else if (targetField.FieldType == typeof(IColor)) // IColor
            {
                var colorValue = fieldValue as IColor ?? IColor.CreateWhite();

                #region Input Color

                var hexVal = colorValue.Hex ?? string.Empty;
                var opacityVal = colorValue.Alpha.ToString() ?? string.Empty;

                container.Add(new CuiPanel
                {
                    Image = {Color = HexToCuiColor("#000000", 50)},
                    RectTransform =
                        {AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-264 -14", OffsetMax = "-60 14"}
                }, targetFieldLayer, targetFieldLayer + ".Value");

                container.Add(new CuiElement
                {
                    Parent = targetFieldLayer + ".Value",
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 12,
                            Align = TextAnchor.MiddleCenter,
                            Color = HexToCuiColor("#FFFFFF", 60),
                            Text = hexVal,
                            NeedsKeyboard = true,
                            Command =
                                $"{CmdMainConsole} {commandPrefix} color hex {targetField.Name} {targetFieldLayer}"
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "30 0", OffsetMax = "-10 0"}
                    }
                });

                #endregion Input Color

                #region Color Preview

                container.Add(new CuiPanel
                {
                    Image = {Color = colorValue.Get() ?? HexToCuiColor("#FFFFFF")},
                    RectTransform =
                        {AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "0 -14", OffsetMax = "28 14"}
                }, targetFieldLayer + ".Value", targetFieldLayer + ".Value.Color");

                #endregion Color Preview

                #region Input Opacity

                container.Add(new CuiElement
                {
                    Name = targetFieldLayer + ".Opacity",
                    Parent = targetFieldLayer,
                    Components =
                    {
                        new CuiImageComponent {Color = HexToCuiColor("#000000", 50)},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "1 0.5",
                            AnchorMax = "1 0.5",
                            OffsetMin = "-58 -14",
                            OffsetMax = "-4 14"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = targetFieldLayer + ".Opacity",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "%", Font = "robotocondensed-bold.ttf", FontSize = 12,
                            Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#FFFFFF", 80)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "1 0",
                            AnchorMax = "1 1",
                            OffsetMin = "-20 0",
                            OffsetMax = "0 0"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Name = targetFieldLayer + ".Opacity.Value",
                    Parent = targetFieldLayer + ".Opacity",
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 12,
                            Align = TextAnchor.MiddleCenter,
                            Color = HexToCuiColor("#FFFFFF", 60),
                            Text = opacityVal,
                            NeedsKeyboard = true,
                            Command =
                                $"{CmdMainConsole} {commandPrefix} color opacity {targetField.Name} {targetFieldLayer}"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                            OffsetMin = "4 0",
                            OffsetMax = "-20 0"
                        }
                    }
                });

                #endregion Input Opacity
            }
            else if (targetField.FieldType.IsArray || typeof(IList).IsAssignableFrom(targetField.FieldType))
            {
                container.Add(new CuiButton
                {
                    Text =
                    {
                        Text = "EDIT",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 12,
                        Align = TextAnchor.MiddleCenter,
                        Color = HexToCuiColor("#FFFFFF", 60)
                    },
                    Button =
                    {
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                        Color = HexToCuiColor("#40403D"),
                        Command = $"{CmdMainConsole} {commandPrefix} array start {targetField.Name} {targetFieldLayer}"
                    },
                    RectTransform =
                        {AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-264 -14", OffsetMax = "-4 14"}
                }, targetFieldLayer);
            }
            else if (targetField.FieldType.IsEnum)
            {
                EnumSelectorUI(player, container, targetFieldLayer, "Value",
                    fieldValue?.ToString() ?? string.Empty,
                    $"{CmdMainConsole} {commandPrefix} field {targetField.Name} {targetFieldLayer} prev",
                    $"{CmdMainConsole} {commandPrefix} field {targetField.Name} {targetFieldLayer} next");
            }
            else if (fieldValue is bool boolValue)
            {
                container.Add(new CuiButton
                {
                    Text =
                    {
                        Text = boolValue ? "ON" : "OFF",
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 12,
                        Align = TextAnchor.MiddleCenter,
                        Color = boolValue ? HexToCuiColor("#68C2FF") : HexToCuiColor("#FFFFFF", 60)
                    },
                    Button =
                    {
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                        Color = boolValue ? HexToCuiColor("#175782") : HexToCuiColor("#40403D"),
                        Command =
                            $"{CmdMainConsole} {commandPrefix} field {targetField.Name} {targetFieldLayer} {!boolValue}"
                    },
                    RectTransform =
                        {AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-264 -14", OffsetMax = "-4 14"}
                }, targetFieldLayer);
            }
            else
            {
                #region Value

                var fieldValueText = fieldValue?.ToString() ?? string.Empty;

                container.Add(new CuiPanel
                {
                    Image = {Color = HexToCuiColor("#000000", 50)},
                    RectTransform =
                        {AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-264 -14", OffsetMax = "-4 14"}
                }, targetFieldLayer, targetFieldLayer + ".Value");

                container.Add(new CuiElement
                {
                    Parent = targetFieldLayer + ".Value",
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Font = "robotocondensed-bold.ttf",
                            FontSize = 12,
                            Align = TextAnchor.MiddleLeft,
                            Color = HexToCuiColor("#FFFFFF", 60),
                            Text = fieldValueText,
                            NeedsKeyboard = true,
                            Command = $"{CmdMainConsole} {commandPrefix} field {targetField.Name} {targetFieldLayer}"
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "10 0", OffsetMax = "-10 0"}
                    }
                });

                #endregion

                #region Preview

                if (targetField.GetCustomAttribute<ItemNeedsPreviewAttribute>() != null)
                    if (originalObject != null && originalObject is ShopItem shopItem)
                    {
                        container.Add(new CuiPanel
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 0", AnchorMax = "1 0",
                                OffsetMin = "0 -116", OffsetMax = "0 -4"
                            },
                            Image = {Color = HexToCuiColor("#000000", 30)}
                        }, targetFieldLayer, targetFieldLayer + ".Preview", targetFieldLayer + ".Preview");

                        container.Add(new CuiElement
                        {
                            Parent = targetFieldLayer + ".Preview",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = "PREVIEW", Font = "robotocondensed-bold.ttf",
                                    FontSize = 12, Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#FFFFFF", 80)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0",
                                    AnchorMax = "1 1",
                                    OffsetMin = "10 0",
                                    OffsetMax = "-170 0"
                                }
                            }
                        });

                        container.Add(new CuiPanel
                        {
                            Image = {Color = HexToCuiColor("#000000", 50)},
                            RectTransform =
                                {AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-264 -50", OffsetMax = "-4 50"}
                        }, targetFieldLayer + ".Preview", targetFieldLayer + ".Preview" + ".Value");

                        container.Add(new CuiElement
                        {
                            Parent = targetFieldLayer + ".Preview" + ".Value",
                            Components =
                            {
                                shopItem.GetPublicImage(),
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0.5 0.5",
                                    AnchorMax = "0.5 0.5",
                                    OffsetMin = "-50 -50",
                                    OffsetMax = "50 50"
                                }
                            }
                        });
                    }

                #endregion Preview
            }
        }

        private static void EnumSelectorUI(
            BasePlayer player,
            CuiElementContainer container,
            string parentLayer,
            string selectorName,
            string currentValue,
            string commandPrev,
            string commandNext,
            string anchorMin = "1 0.5",
            string anchorMax = "1 0.5",
            string offsetMin = "-264 -14",
            string offsetMax = "-4 14",
            string backgroundColor = "#40403D",
            int backgroundAlpha = 100,
            string textColor = "#FFFFFF",
            int textAlpha = 60,
            int fontSize = 12)
        {
            var selectorLayer = parentLayer + "." + selectorName;

            container.Add(new CuiPanel
            {
                Image =
                {
                    Color = HexToCuiColor(backgroundColor, backgroundAlpha),
                    Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                },
                RectTransform =
                {
                    AnchorMin = anchorMin, AnchorMax = anchorMax,
                    OffsetMin = offsetMin, OffsetMax = offsetMax
                }
            }, parentLayer, selectorLayer, selectorLayer);

            container.Add(new CuiElement
            {
                Parent = selectorLayer,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = Instance.Msg(player, currentValue ?? string.Empty),
                        Font = "robotocondensed-bold.ttf",
                        FontSize = fontSize,
                        Align = TextAnchor.MiddleCenter,
                        Color = HexToCuiColor(textColor, textAlpha)
                    },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "0 1",
                    OffsetMin = "0 0", OffsetMax = "28 0"
                },
                Text =
                {
                    Text = "<",
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 20,
                    Color = HexToCuiColor("#FFFFFF", 60),
                    VerticalOverflow = VerticalWrapMode.Overflow
                },
                Button =
                {
                    Command = commandPrev,
                    Color = HexToCuiColor("#000000", 0)
                }
            }, selectorLayer);

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 0", AnchorMax = "1 1",
                    OffsetMin = "-28 0", OffsetMax = "0 0"
                },
                Text =
                {
                    Text = ">",
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 20,
                    Color = HexToCuiColor("#FFFFFF", 60),
                    VerticalOverflow = VerticalWrapMode.Overflow
                },
                Button =
                {
                    Command = commandNext,
                    Color = HexToCuiColor("#000000", 0)
                }
            }, selectorLayer);
        }

        #endregion Items.Editor

        #endregion Editor

        #region Helpers

        private static void UpdateUI(BasePlayer player, Action<CuiElementContainer> callback)
        {
            if (player == null) return;

            Instance?.NextTick(() =>
            {
                var container = Pool.Get<CuiElementContainer>();
                try
                {
                    callback?.Invoke(container);

                    CuiHelper.AddUi(player, container);
                }
                finally
                {
                    container.Clear();
                    Pool.FreeUnsafe(ref container);
                }
            });
        }

        private static void UpdateUI(BasePlayer player, Action<List<string>> callback = null)
        {
            Instance?.NextTick(() =>
            {
                var sb = Pool.Get<StringBuilder>();
                var allElements = Pool.Get<List<string>>();
                try
                {
                    callback?.Invoke(allElements);

                    #region Merge Elements

                    if (allElements.Count > 0)
                    {
                        sb.Append('[');
                        for (var i = 0; i < allElements.Count; i++)
                        {
                            if (string.IsNullOrEmpty(allElements[i])) continue;

                            if (i > 0) sb.Append(',');

                            sb.Append(allElements[i]);
                        }

                        sb.Append(']');
                    }

                    #endregion Merge Elements

                    CuiHelper.AddUi(player, sb.ToString());
                }
                finally
                {
                    Pool.FreeUnmanaged(ref allElements);
                    Pool.FreeUnmanaged(ref sb);
                }
            });
        }

        #endregion

        #endregion

        #region Utils

        private bool CanAccesToItem(BasePlayer player, ShopItem item)
        {
            if (player == null || item == null || item.Type != ItemType.Item || player.HasPermission(PERM_BYPASS_DLC) ||
                !player.IsHuman())
                return true;

            return Native_IsOwnedOrFreeItem(player, item.itemId, item.Skin);
        }

        private bool Native_IsOwnedOrFreeItem(BasePlayer player, int itemId, ulong skin)
        {
            var itemDefinition = ItemManager.FindItemDefinition(itemId);
            if (itemDefinition == null) return true;

            if (itemDefinition.steamDlc != null || itemDefinition.steamItem != null)
                return player.blueprints.HasUnlocked(itemDefinition);

            if (skin != 0 && itemDefinition.isRedirectOf != null)
                return player.blueprints.CheckSkinOwnership((int) skin, player);

            return true;
        }

        private void LoadServerPanel()
        {
            _serverPanelCategory.spStatus = ServerPanel is {IsLoaded: true};

            ServerPanel?.Call("API_OnServerPanelProcessCategory", Name);
        }

        private void CheckInitializationStatus()
        {
            LoadServerPanel();

            if (!_initializedStatus.status && string.IsNullOrWhiteSpace(_initializedStatus.message))
            {
                _initializedStatus = (true, null);
            }
            else
            {
                PrintError(ConvertInitializedStatus());

                InitInstaller();
            }
        }

        private string ConvertInitializedStatus()
        {
            switch (_initializedStatus.message)
            {
                case "not_installed_template":
                    return
                        $"No template is installed in the plugin. To install the plugin, run the command /shop.install. You must have the '{PERM_ADMIN}' permission to execute this command.";

                case "not_installed_image_library":
                    return
                        "There is no image library installed in the plugin! Install the \"ImageLibrary\" plugin on the server! URL: https://umod.org/plugins/image-library";

                default:
                    return $"Unknown error: {_initializedStatus.message}";
            }
        }

        private class EncryptDecrypt
        {
            public static string Decrypt(string cipherText, string key)
            {
                var iv = new byte[16];
                var buffer = Convert.FromBase64String(cipherText);

                using var aes = Aes.Create();
                aes.Key = Convert.FromBase64String(key);
                aes.IV = iv;

                var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                using var memoryStream = new MemoryStream(buffer);
                using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);

                using var resultStream = new MemoryStream();
                cryptoStream.CopyTo(resultStream);
                return Encoding.UTF8.GetString(resultStream.ToArray());
            }
        }

        private string GetItemDisplayNameFromLangAPI(string shortName, string displayName, string userID)
        {
            if (Convert.ToBoolean(LangAPI?.Call("IsDefaultDisplayName", shortName)))
                return Convert.ToString(LangAPI.Call("GetItemDisplayName", shortName, displayName, userID)) ??
                       displayName;

            return displayName;
        }

        #region Transfer Helpers

        private int GetAvailablePlayersCount(ulong player)
        {
            if (_config.TransferToOfflinePlayers)
            {
                var count = 0;
                foreach (var p in BasePlayer.allPlayerList)
                    if (p != null && p.userID != player)
                        count++;
                return count;
            }

            return Mathf.Max(BasePlayer.activePlayerList.Count - 1, 0);
        }

        private List<BasePlayer> GetAvailablePlayersToTransfer(ulong player, int skip, int take)
        {
            var list = Pool.Get<List<BasePlayer>>();

            if (_config.TransferToOfflinePlayers)
            {
                var index = 0;
                foreach (var targetPlayer in BasePlayer.allPlayerList)
                {
                    if (targetPlayer == null || targetPlayer.userID == player) continue;

                    if (index >= skip)
                    {
                        list.Add(targetPlayer);
                        if (list.Count >= take) break;
                    }

                    index++;
                }
            }
            else
            {
                for (var index = 0; index < BasePlayer.activePlayerList.Count; index++)
                {
                    if (index < skip) continue;

                    var targetPlayer = BasePlayer.activePlayerList[index];
                    if (targetPlayer == null || targetPlayer.userID == player || !targetPlayer.IsConnected) continue;

                    list.Add(targetPlayer);

                    if (list.Count >= take) break;
                }
            }

            return list;
        }

        #endregion

        private void CloseShopUI(BasePlayer player)
        {
            _openedShopsNPC.Remove(player.userID);
            _openedCustomVending.Remove(player.userID);

            RemoveOpenedShop(player.userID);

            EditCategoryData.Remove(player.userID);
            EditItemData.Remove(player.userID);
        }

        private void TryBuyItems(BasePlayer player, bool again = false)
        {
            var playerCart = GetPlayerCart(player.userID);
            if (playerCart == null) return;

            var price = playerCart.GetCartPrice(player, again);
            if (price < 0.0) return;

            if (_config.BlockNoEscape && NoEscape_IsBlocked(player))
            {
                ErrorUi(player, Msg(player, BuyRaidBlocked));
                return;
            }

            var secondsFromWipe = GetSecondsFromWipe();
            if (_config.WipeCooldown)
            {
                var timeLeft = Mathf.RoundToInt(_config.WipeCooldownTimer - secondsFromWipe);
                if (timeLeft > 0)
                {
                    ErrorUi(player,
                        Msg(player, BuyWipeCooldown,
                            FormatShortTime(timeLeft)));
                    return;
                }
            }

            if (_config.RespawnCooldown)
            {
                var timeLeft = Mathf.RoundToInt(_config.RespawnCooldownTimer - player.TimeAlive());
                if (timeLeft > 0)
                {
                    ErrorUi(player,
                        Msg(player, BuyRespawnCooldown,
                            FormatShortTime(timeLeft)));
                    return;
                }
            }

            var totalAmount = playerCart.GetCartItemsStacksAmount();
            var slots = player.inventory.containerBelt.capacity -
                        player.inventory.containerBelt.itemList.Count +
                        (player.inventory.containerMain.capacity -
                         player.inventory.containerMain.itemList.Count);
            if (slots < totalAmount)
            {
                ErrorUi(player, Msg(player, NotEnoughSpace));
                return;
            }

            if (playerCart.AnyShopItems(again, (item, amount) =>
                {
                    if (amount <= 0) return false;

                    if (item.BuyBlockDurationAfterWipe > 0)
                    {
                        var timeLeft = Mathf.RoundToInt(item.BuyBlockDurationAfterWipe - secondsFromWipe);
                        if (timeLeft > 0)
                        {
                            ErrorUi(player, Msg(player, BuyWipeCooldown, FormatShortTime(timeLeft)));
                            return true;
                        }
                    }

                    var limit = GetLimit(player, item, true);
                    if (limit <= 0)
                    {
                        ErrorUi(player, Msg(player, BuyLimitReached, item.GetPublicTitle(player)));
                        return true;
                    }

                    limit = GetLimit(player, item, true, true);
                    if (limit <= 0)
                    {
                        ErrorUi(player, Msg(player, DailyBuyLimitReached, item.GetPublicTitle(player)));
                        return true;
                    }

                    return false;
                }))
                return;

            if (!player.HasPermission(PERM_FREE_BYPASS) &&
                !GetPlayerEconomy(player).RemoveBalance(player, price))
            {
                ErrorUi(player, Msg(player, NotMoney));
                return;
            }

            ServerMgr.Instance.StartCoroutine(GiveCartItems(player, again, price));

            if (!again)
            {
                (playerCart as PlayerCartData)?.SaveLastPurchaseItems();

                playerCart.ClearCartItems();
            }

            CuiHelper.DestroyUi(player, Layer);

            if (!again)
                if (!_config.BuyAgain.Enabled)
                    playerCart.ClearCartItems();

            if (_serverPanelCategory.spStatus) ServerPanel?.Call("API_OnServerPanelCallClose", player);

            CloseShopUI(player);

            _config?.Notifications?.ShowNotify(player, ReceivedItems, 0);
        }

        private static int GetPlayerItems(BasePlayer player, List<Item> items)
        {
            return _config.SellContainers.Enabled
                ? _config.SellContainers.AllItems(player, items)
                : player.inventory.GetAllItems(items);
        }

        private void LoadNPCs()
        {
            foreach (var check in _config.NPCs) check.Value.BotID = check.Key;
        }

        private readonly Dictionary<int, EconomyEntry> _additionalEconomics = new();

        private readonly List<AdditionalEconomy> _economics = new();

        private void LoadEconomics()
        {
            _economics.Clear();

            _config.AdditionalEconomics.FindAll(x => x.Enabled)
                .ForEach(x =>
                {
                    if (x.ID == 0 || !_additionalEconomics.TryAdd(x.ID, x))
                        PrintError($"Additional economy caching error. There are several economies with ID {x.ID}");
                });

            _economics.Add(new AdditionalEconomy(_config.Economy));
            _economics.AddRange(_config.AdditionalEconomics.FindAll(x => x.Enabled));
        }

        private bool NoEscape_IsBlocked(BasePlayer player)
        {
            return Convert.ToBoolean(NoEscape?.Call("IsBlocked", player));
        }

        private IEnumerator GiveCartItems(BasePlayer player, bool again, double price)
        {
            var playerCart = GetPlayerCart(player.userID);
            if (playerCart == null) yield break;

            var items = playerCart.GetShopItemsList(again);
            try
            {
                var logItems = Pool.Get<List<string>>();
                var selectedEconomy = API_GetShopPlayerSelectedEconomy(player.userID);

                try
                {
                    for (var i = 0; i < items.Count; i++)
                    {
                        var (item, amount) = items[i];

                        logItems.Add(item.GetPublicTitle(player) + $"[x{amount}]");

                        item?.Get(player, amount);

                        SetCooldown(player, item, true);
                        UseLimit(player, item, true, amount);
                        UseLimit(player, item, true, amount, true);

                        var itemPrice = item.GetPrice(player, selectedEconomy) * amount;
                        LogBuySell(player, item, amount, itemPrice, true);

                        if (i % _itemsPerTick == 0)
                            yield return CoroutineEx.waitForEndOfFrame;
                    }
                }
                finally
                {
                    Pool.FreeUnmanaged(ref logItems);
                }
            }
            finally
            {
                Pool.FreeUnmanaged(ref items);
            }
        }

        private NPCShop GetNPCShop(ulong playerID)
        {
            return _openedShopsNPC.GetValueOrDefault(playerID);
        }

        private bool TryGetNPCShop(ulong playerID, out NPCShop npcShop)
        {
            return _openedShopsNPC.TryGetValue(playerID, out npcShop);
        }

        private static RaycastHit? GetLookHitLayer(BasePlayer player, float maxDistance = 5f, int layerMask = -5)
        {
            return !Physics.Raycast(player.eyes.HeadRay(), out var hitInfo, maxDistance, layerMask,
                QueryTriggerInteraction.UseGlobal)
                ? null
                : hitInfo;
        }

        private static RaycastHit? GetLookHit(BasePlayer player, float maxDistance = 5f)
        {
            return !Physics.Raycast(player.eyes.HeadRay(), out var hitInfo, maxDistance)
                ? null
                : hitInfo;
        }

        private static VendingMachine GetLookVM(BasePlayer player)
        {
            return GetLookHit(player)?.GetEntity() as VendingMachine;
        }

        private static BasePlayer GetLookNPC(BasePlayer player)
        {
            return GetLookHitLayer(player, layerMask: LayerMask.GetMask("Player (Server)"))?.GetEntity() as BasePlayer;
        }

        private static Vector3 GetLookPoint(BasePlayer player)
        {
            return GetLookHit(player, 10f)?.point ?? player.ServerPosition;
        }

        private void RegisterPermissions()
        {
            var permissions = new HashSet<string>();

            _itemsData.Shop.ForEach(category =>
            {
                if (!string.IsNullOrEmpty(category.Permission))
                    permissions.Add(category.Permission);

                foreach (var item in category.Items)
                {
                    if (!string.IsNullOrEmpty(item.Permission))
                        permissions.Add(item.Permission);

                    if (item.UseCustomDiscount)
                        foreach (var discountPermission in item.Discount.Keys)
                            if (!string.IsNullOrEmpty(discountPermission))
                                permissions.Add(discountPermission);
                }
            });

            foreach (var shop in _config.NPCs.Values)
                if (!string.IsNullOrEmpty(shop.Permission))
                    permissions.Add(shop.Permission);

            foreach (var shop in _config.CustomVending.Values)
                if (!string.IsNullOrEmpty(shop.Permission))
                    permissions.Add(shop.Permission);

            foreach (var discountPermission in _config.Discount.Discount.Keys)
                if (!string.IsNullOrEmpty(discountPermission))
                    permissions.Add(discountPermission);

            if (!string.IsNullOrEmpty(_config.Permission))
                permissions.Add(_config.Permission);

            permissions.Add(PERM_ADMIN);
            permissions.Add(PERM_FREE_BYPASS);
            permissions.Add(PERM_SET_VM);
            permissions.Add(PERM_SET_NPC);
            permissions.Add(PERM_BYPASS_DLC);

            if (!string.IsNullOrEmpty(_config.BuyAgain.Permission))
                permissions.Add(_config.BuyAgain.Permission);

            foreach (var perm in permissions)
            {
                var lowerPerm = perm.ToLower();
                if (permission.PermissionExists(lowerPerm)) continue;

                permission.RegisterPermission(lowerPerm, this);
            }

            permissions.Clear();
        }

        private void RegisterCommands()
        {
            AddCovalenceCommand(_config.Commands, nameof(CmdShopOpen));

            AddCovalenceCommand("shop.setvm", nameof(CmdSetCustomVM));

            AddCovalenceCommand("shop.setnpc", nameof(CmdSetShopNPC));
        }

        private void CacheImages()
        {
            foreach (var image in _shopItems.Values
                         .Select(shopItem =>
                             !string.IsNullOrEmpty(shopItem.Image) ? shopItem.Image : shopItem.ShortName))
                _images.Add(image);
        }

        private void LoadPlayers()
        {
            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
        }

        #region Custom Vending

        private readonly Dictionary<ulong, CustomVendingEntry> _openedCustomVending = new();

        private CustomVendingEntry GetCustomVending(ulong playerId)
        {
            return _openedCustomVending.GetValueOrDefault(playerId);
        }

        private bool TryGetCustomVending(ulong playerId, out CustomVendingEntry customVM)
        {
            return _openedCustomVending.TryGetValue(playerId, out customVM);
        }

        private void LoadCustomVMs()
        {
            var anyRemoved = false;

            _config.CustomVending.Keys.ToList().ForEach(wb =>
            {
                if (CheckCustomVending(wb))
                    anyRemoved = true;
            });

            if (anyRemoved)
                SaveConfig();

            Subscribe(nameof(CanLootEntity));
        }

        private bool CheckCustomVending(ulong netId)
        {
            return BaseNetworkable.serverEntities.Find(new NetworkableId(netId)) as VendingMachine == null &&
                   _config.CustomVending.Remove(netId);
        }

        #endregion

        private int GetSecondsFromWipe()
        {
            return (int) DateTime.UtcNow
                .Subtract(SaveRestore.SaveCreatedTime.ToUniversalTime()).TotalSeconds;
        }

        private static string FormatShortTime(int seconds)
        {
            return TimeSpan.FromSeconds(seconds).ToShortString();
        }

        private bool InDuel(BasePlayer player)
        {
            return Convert.ToBoolean(Duel?.Call("IsPlayerOnActiveDuel", player)) ||
                   Convert.ToBoolean(Duelist?.Call("inEvent", player));
        }

        private bool IsAdmin(BasePlayer player)
        {
            return player != null && ((player.IsAdmin && _config.FlagAdmin) || player.HasPermission(PERM_ADMIN));
        }

        private bool IsAdminMode(BasePlayer player)
        {
            return IsAdmin(player) && _adminModeUsers.Contains(player.userID);
        }

        private bool IsRateLimited(BasePlayer player)
        {
            if (_lastCommandTime.TryGetValue(player.userID, out var lastTime))
            {
                var timeSinceLastCommand = Time.time - lastTime;
                if (timeSinceLastCommand < _config.CooldownBetweenActions)
                    return true;
            }

            _lastCommandTime[player.userID] = Time.time;
            return false;
        }

        private int GetId()
        {
            var result = -1;

            do
            {
                var val = Random.Range(int.MinValue, int.MaxValue);

                if (!_shopItems.ContainsKey(val))
                    result = val;
            } while (result == -1);

            return result;
        }

        private static void CreateOutLine(ref List<string> elements, string parent, string color,
            float size = 2)
        {
            elements.Add(CuiJsonFactory.CreatePanel(parent: parent, name: parent + ".Outline",
                destroy: parent + ".Outline", anchorMin: "0 0", anchorMax: "1 0", offsetMin: $"{size} 0",
                offsetMax: $"-{size} {size}", color: color));
            elements.Add(CuiJsonFactory.CreatePanel(parent: parent, name: parent + ".Outline",
                destroy: parent + ".Outline", anchorMin: "0 1", anchorMax: "1 1", offsetMin: $"{size} -{size}",
                offsetMax: $"-{size} 0", color: color));
            elements.Add(CuiJsonFactory.CreatePanel(parent: parent, name: parent + ".Outline",
                destroy: parent + ".Outline", anchorMin: "1 0", anchorMax: "1 1", offsetMin: $"-{size} 0",
                offsetMax: "0 0", color: color));
            elements.Add(CuiJsonFactory.CreatePanel(parent: parent, name: parent + ".Outline",
                destroy: parent + ".Outline", anchorMin: "1 0", anchorMax: "1 1", offsetMin: $"-{size} 0",
                offsetMax: "0 0", color: color));
        }

        private IEnumerator LoadImages(BasePlayer player)
        {
            foreach (var image in _images)
            {
                if (player == null || !player.IsConnected) continue;

                SendImage(player, image);

                yield return CoroutineEx.waitForSeconds(_config.ImagesDelay);
            }
        }

        private ShopCategory FindCategoryByName(string name)
        {
            return _itemsData.Shop.Find(cat => cat.Title == name);
        }

        private ShopCategory FindCategoryById(int id)
        {
            return _itemsData.Shop.Find(cat => cat.ID == id);
        }

        private ShopItem FindItemById(int id)
        {
            return _shopItems.GetValueOrDefault(id);
        }

        private bool TryFindItemById(int id, out ShopItem shopItem)
        {
            return _shopItems.TryGetValue(id, out shopItem);
        }

        private void FillCategories(float recoveryRate = 0.5f)
        {
            _itemsData.Shop.Clear();

            var sw = Stopwatch.StartNew();

            var dict = new Dictionary<string, List<ItemDefinition>>();

            ItemManager.itemList.FindAll(item => item.Blueprint != null && item.Blueprint.userCraftable).ForEach(item =>
            {
                var itemCategory = item.category.ToString();

                if (dict.TryGetValue(itemCategory, out var definitions))
                    definitions.Add(item);
                else
                    dict.Add(itemCategory, new List<ItemDefinition> {item});
            });

            var id = 0;

            var category = new ShopCategory
            {
                Enabled = true,
                CategoryType = ShopCategory.Type.Favorite,
                Title = "Favorites",
                Localization = new Localization
                {
                    Enabled = false,
                    Messages = new Dictionary<string, string>
                    {
                        ["en"] = "Favorites",
                        ["fr"] = "Favoris"
                    }
                },
                Permission = string.Empty,
                SortType = Configuration.SortType.None,
                Items = new List<ShopItem>()
            };

            _itemsData.Shop.Add(category);

            foreach (var check in dict)
            {
                category = new ShopCategory
                {
                    Enabled = true,
                    CategoryType = ShopCategory.Type.None,
                    Title = check.Key,
                    Localization = new Localization
                    {
                        Enabled = false,
                        Messages = new Dictionary<string, string>
                        {
                            ["en"] = check.Key,
                            ["fr"] = check.Key
                        }
                    },
                    Permission = string.Empty,
                    SortType = Configuration.SortType.None,
                    Items = new List<ShopItem>()
                };

                check.Value
                    .FindAll(itemDefinition => itemDefinition.shortname != "blueprintbase")
                    .ForEach(itemDefinition =>
                    {
                        var itemCost = GetItemCost(itemDefinition);

                        var item = ShopItem.GetDefault(id++, itemCost, itemDefinition.shortname);

                        item.Price = Math.Round(itemCost, 2);
                        item.SellPrice = Math.Round(itemCost * recoveryRate, 2);

                        category.Items.Add(item);
                    });

                category.LoadIDs(true);

                _itemsData.Shop.Add(category);
            }

            SaveItemsData();

            sw.Stop();
            PrintWarning($"Shop was filled with items in {sw.ElapsedMilliseconds} ms!");
        }

        private double GetItemCost(ItemDefinition itemDefinition)
        {
            return ItemCostCalculator != null
                ? Convert.ToDouble(ItemCostCalculator?.Call("GetItemCost", itemDefinition))
                : 100;
        }

        private void CheckOnDuplicates()
        {
            if (_itemsData.Shop.Count == 0) return;

            var seen = new HashSet<int>();

            var fixedCount = 0;

            for (var i = 0; i < _itemsData.Shop.Count; i++)
            {
                var items = _itemsData.Shop[i].Items;
                for (var j = 0; j < items.Count; j++)
                {
                    var item = items[j];
                    if (!seen.Add(item.ID))
                    {
                        item.ID = GetId();
                        seen.Add(item.ID);
                        fixedCount++;
                    }
                }
            }

            if (fixedCount > 0)
                PrintError($"Fixed {fixedCount} duplicate ID(s)");
        }

        private void LoadItems()
        {
            _shopItems.Clear();

            #region Default Items

            ItemManager.itemList.ForEach(item =>
            {
                var itemCategory = item.category.ToString();

                if (_itemsCategories.ContainsKey(itemCategory))
                {
                    if (!_itemsCategories[itemCategory].Contains((item.itemid, item.shortname)))
                        _itemsCategories[itemCategory].Add((item.itemid, item.shortname));
                }
                else
                {
                    _itemsCategories.Add(itemCategory, new List<(int itemID, string shortName)>
                    {
                        (item.itemid, item.shortname)
                    });
                }
            });

            #endregion

            _itemsData.Shop.ForEach(category =>
            {
                if (category.Enabled && category.CategoryType == ShopCategory.Type.Favorite)
                    _isEnabledFavorites = true;

                category.LoadIDs();
            });
        }

        private List<ShopCategory> GetCategories(BasePlayer player)
        {
            var npcShop = GetNPCShop(player.userID);

            var customVM = GetCustomVending(player.userID);

            var categories = Pool.Get<List<ShopCategory>>();

            for (var i = 0; i < _itemsData.Shop.Count; i++)
            {
                var shopCategory = _itemsData.Shop[i];

                var enabled = shopCategory.Enabled || _adminModeUsers.Contains(player.userID);
                if (!enabled)
                    continue;

                var hasPermissions = string.IsNullOrEmpty(shopCategory.Permission) ||
                                     player.HasPermission(shopCategory.Permission);
                if (!hasPermissions)
                    continue;

                if (npcShop != null)
                {
                    if (!npcShop.Categories.Contains("*") && !npcShop.Categories.Contains(shopCategory.Title))
                        continue;
                }
                else if (customVM != null)
                {
                    if (!customVM.Categories.Contains("*") &&
                        !customVM.Categories.Contains(shopCategory.GetTitle(player)))
                        continue;
                }
                else if (shopCategory.CategoryType == ShopCategory.Type.Hided)
                {
                    continue;
                }

                categories.Add(shopCategory);
            }

            return categories;
        }

        private List<ShopItem> GetPaginationShopItems(BasePlayer player)
        {
            var shop = GetShop(player);

            var isSearch = shop.HasSearch();

            var shopTotalItemsAmount = GetShopTotalItemsAmount(player);

            var useScrollToListItems = shop.GetUI().ShopContent.Content.UseScrollToListItems;

            var skipAmount = !useScrollToListItems
                ? (isSearch ? shop.currentSearchPage : shop.currentShopPage) * shopTotalItemsAmount
                : 0;
            var takeAmount = !useScrollToListItems ? shopTotalItemsAmount : -1;

            return isSearch
                ? SearchItem(player, shop.search, skipAmount, takeAmount)
                : shop.GetSelectedShopCategory()?.GetShopItems(player, skipAmount, takeAmount) ??
                  Pool.Get<List<ShopItem>>();
        }

        private static string HexToCuiColor(string hex, float alpha = 100)
        {
            if (string.IsNullOrEmpty(hex)) hex = "#FFFFFF";

            var str = hex.Trim('#');
            if (str.Length != 6) throw new Exception(hex);
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

            return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {alpha / 100f}";
        }

        private static int PlayerItemsCount(BasePlayer player, string shortname, ulong skin)
        {
            var items = Pool.Get<List<Item>>();

            try
            {
                player.inventory.GetAllItems(items);

                return ItemCount(items, shortname, skin);
            }
            finally
            {
                Pool.FreeUnmanaged(ref items);
            }
        }

        private static int ItemCount(List<Item> items, string shortname, ulong skin)
        {
            var result = 0;

            for (var i = 0; i < items.Count; i++)
                if (items[i].IsSameItem(shortname, skin))
                    result += items[i].amount;

            return result;
        }

        private static int ItemCount(List<Item> items, ShopItem shopItem)
        {
            var result = 0;

            for (var i = 0; i < items.Count; i++)
                if (shopItem.CanTake(items[i]))
                    result += items[i].amount;

            return result;
        }

        private static void Take(List<Item> itemList, ShopItem shopItem, int amountToTake)
        {
            if (amountToTake == 0) return;
            var takenAmount = 0;

            var itemsToTake = Pool.Get<List<Item>>();

            try
            {
                foreach (var item in itemList)
                {
                    if (!shopItem.CanTake(item)) continue;

                    var remainingAmount = amountToTake - takenAmount;
                    if (remainingAmount <= 0) break;

                    if (item.amount > remainingAmount)
                    {
                        item.MarkDirty();
                        item.amount -= remainingAmount;
                        break;
                    }

                    if (item.amount <= remainingAmount)
                    {
                        takenAmount += item.amount;
                        itemsToTake.Add(item);
                    }

                    if (takenAmount == amountToTake)
                        break;
                }

                foreach (var itemToTake in itemsToTake)
                    itemToTake.RemoveFromContainer();
            }
            finally
            {
                Pool.FreeUnmanaged(ref itemsToTake);
            }
        }

        private static void Take(List<Item> itemList, string shortname, ulong skinId, int iAmount)
        {
            if (iAmount == 0) return;

            var list = Pool.Get<List<Item>>();

            try
            {
                var num1 = 0;
                foreach (var item in itemList)
                {
                    if (!item.IsSameItem(shortname, skinId)) continue;

                    var num2 = iAmount - num1;
                    if (num2 <= 0) continue;
                    if (item.amount > num2)
                    {
                        item.MarkDirty();
                        item.amount -= num2;
                        break;
                    }

                    if (item.amount <= num2)
                    {
                        num1 += item.amount;
                        list.Add(item);
                    }

                    if (num1 == iAmount)
                        break;
                }

                foreach (var obj in list)
                    obj.RemoveFromContainer();
            }
            finally
            {
                Pool.FreeUnmanaged(ref list);
            }
        }

        private CartData GetPlayerCart(ulong playerID)
        {
            var data = PlayerData.GetOrCreate(playerID);

            if (TryGetNPCShop(playerID, out var npcShop))
            {
                if (!data.NPCCart.Carts.TryGetValue(npcShop.BotID, out var cartData))
                    data.NPCCart.Carts.TryAdd(npcShop.BotID, cartData = new CartData());

                return cartData;
            }

            return data.PlayerCart ?? (data.PlayerCart = new PlayerCartData());
        }

        private string FormatShortTime(BasePlayer player, TimeSpan time)
        {
            if (time.Days != 0)
                return Msg(player, DaysFormat, time.Days);

            if (time.Hours != 0)
                return Msg(player, HoursFormat, time.Hours);

            if (time.Minutes != 0)
                return Msg(player, MinutesFormat, time.Minutes);

            if (time.Seconds != 0)
                return Msg(player, SecondsFormat, time.Seconds);

            return string.Empty;
        }

        #region Images

        private Dictionary<string, string> _loadedImages = new();

        private void AddImage(string url, string fileName, ulong imageId = 0)
        {
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(fileName)) return;

            if (url.StartsWith("TheMevent/"))
            {
                LoadImageFromFS(fileName, url);
                return;
            }

#if CARBON
			imageDatabase.Queue(true, new Dictionary<string, string>
			{
				[fileName] = url
			});
#else
            ImageLibrary?.Call("AddImage", url, fileName, imageId);
#endif
        }

        private string GetImage(string name)
        {
            if (_loadedImages.TryGetValue(name, out var imageID)) return imageID;

#if CARBON
			return imageDatabase.GetImageString(name);
#else
            return Convert.ToString(ImageLibrary.Call("GetImage", name));
#endif
        }

        private bool HasImage(string name)
        {
            if (_loadedImages.TryGetValue(name, out var imageID)) return true;

#if CARBON
			return Convert.ToBoolean(imageDatabase?.HasImage(name));
#else
            return Convert.ToBoolean(ImageLibrary?.Call("HasImage", name));
#endif
        }

        private void SendImage(BasePlayer player, string imageName)
        {
#if CARBON
			if (!HasImage(imageName) || player?.net?.connection == null)
				return;

			var crc = uint.Parse(GetImage(imageName));
			var array = FileStorage.server.Get(crc, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);
			if (array == null)
				return;

			CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo(player.net.connection)
			{
				channel = 2,
				method = Network.SendMethod.Reliable
			}, null, "CL_ReceiveFilePng", crc, (uint)array.Length, array);
#else
            ImageLibrary?.Call("SendImage", player, imageName);
#endif
        }

        private void LoadImages()
        {
#if CARBON
            if (imageDatabase == null)
			    imageDatabase = BaseModule.GetModule<ImageDatabaseModule>();
#endif
            var imagesList = new Dictionary<string, string>();

            _itemsData.Shop.ForEach(category =>
            {
                category.Items.ForEach(item => { RegisterImage(item.Image, ref imagesList); });
            });

            if (_config.BuyAgain.Enabled)
                RegisterImage(_config.BuyAgain.Image, ref imagesList);

            RegisterImagesFromUI(ref imagesList);

            foreach (var (name, url) in imagesList.ToArray())
            {
                if (url.IsURL()) continue;

                if (url.StartsWith("TheMevent/"))
                {
                    imagesList.Remove(name);

                    LoadImageFromFS(name, url);
                }
            }

            if (imagesList.Count == 0) return;

#if CARBON
            imageDatabase.Queue(false, imagesList);
#else
            timer.In(1f, () =>
            {
                if (ImageLibrary is not {IsLoaded: true})
                {
                    _initializedStatus = (false, "not_installed_image_library");
                    return;
                }

                ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
            });
#endif
        }

        private static void RegisterImage(string image, ref Dictionary<string, string> imagesList)
        {
            if (string.IsNullOrEmpty(image)) return;

            if (_config.EnableOfflineImageMode &&
                image.Contains("https://gitlab.com/TheMevent/PluginsStorage/raw/main"))
                image = image.Replace("https://gitlab.com/TheMevent/PluginsStorage/raw/main", "TheMevent")
                    .Replace("?raw=true", string.Empty);

            imagesList.TryAdd(image, image);
        }

        private void LoadImageFromFS(string name, string path)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path)) return;

            Global.Runner.StartCoroutine(LoadImage(name, path));
        }

        private IEnumerator LoadImage(string name, string path)
        {
            var url = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + path;
            using var www = UnityWebRequestTexture.GetTexture(url);

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
#if CARBON
                Instance?.PrintError($"Image not found at '{path}'. Verify that the file exists and that all UI images are placed under 'oxide/data/TheMevent'.");
#else
                Instance?.PrintError(
                    $"Image not found at '{path}'. Verify that the file exists and that all UI images are placed under 'carbon/data/TheMevent'.");
#endif
            }
            else
            {
                var texture = DownloadHandlerTexture.GetContent(www);
                try
                {
                    var image = texture.EncodeToPNG();

                    _loadedImages.TryAdd(name,
                        FileStorage.server.Store(image, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID)
                            .ToString());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
        }

        #endregion Images

        #endregion

        #region Edit Item

        private Dictionary<ulong, EditItemData> editItemData = new();

        private class EditItemData
        {
            #region Fields

            public ulong playerID;
            public int itemID;
            public bool isNew;

            public ShopItem editingItem;

            private Dictionary<string, FieldInfo> _editableFields = new();

            #endregion

            #region Factory Methods

            public static EditItemData Create(BasePlayer player, int itemID, bool isNew = false)
            {
                var data = new EditItemData
                {
                    playerID = player.userID,
                    itemID = itemID,
                    isNew = isNew
                };

                if (isNew)
                {
                    data.editingItem = new ShopItem
                    {
                        ID = itemID,
                        Type = ItemType.Item,
                        Amount = 1,
                        Price = 100,
                        SellPrice = 100,
                        CanBuy = true,
                        CanSell = true
                    };
                }
                else
                {
                    var original = Instance?.FindItemById(itemID);
                    if (original == null) return null;

                    data.editingItem = JsonConvert.DeserializeObject<ShopItem>(
                        JsonConvert.SerializeObject(original));
                }

                data.CollectEditableFields();

                Instance?.editItemData.TryAdd(player.userID, data);
                return data;
            }

            public static EditItemData Get(ulong playerID)
            {
                return Instance?.editItemData.TryGetValue(playerID, out var data) == true ? data : null;
            }

            public static void Remove(ulong playerID)
            {
                Instance?.editItemData?.Remove(playerID);
            }

            #endregion

            #region Dynamic Fields

            private void CollectEditableFields()
            {
                _editableFields.Clear();

                var fields = typeof(ShopItem).GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (var field in fields)
                    if (ShouldShowField(field))
                        _editableFields[field.Name] = field;
            }

            private bool ShouldShowField(FieldInfo field)
            {
                switch (editingItem.Type)
                {
                    case ItemType.Item:
                        return field.Name switch
                        {
                            nameof(ShopItem.Type) or
                                nameof(ShopItem.Permission) or
                                nameof(ShopItem.Title) or
                                nameof(ShopItem.Description) or
                                nameof(ShopItem.ShortName) or
                                nameof(ShopItem.Skin) or
                                nameof(ShopItem.Image) or
                                nameof(ShopItem.Blueprint) or
                                nameof(ShopItem.DisplayName) or
                                nameof(ShopItem.Amount) or
                                nameof(ShopItem.BuyCooldown) or
                                nameof(ShopItem.SellCooldown) or
                                nameof(ShopItem.BuyMaxAmount) or
                                nameof(ShopItem.SellMaxAmount) or
                                nameof(ShopItem.ForceBuy) or
                                nameof(ShopItem.ProhibitSplit) or
                                nameof(ShopItem.BuyBlockDurationAfterWipe) or
                                nameof(ShopItem.SellBlockDurationAfterWipe) or
                                nameof(ShopItem.Price) or
                                nameof(ShopItem.SellPrice) or
                                nameof(ShopItem.CanBuy) or
                                nameof(ShopItem.CanSell) => true,
                            _ => false
                        };

                    case ItemType.Command:
                        return field.Name switch
                        {
                            nameof(ShopItem.Type) or
                                nameof(ShopItem.Permission) or
                                nameof(ShopItem.Title) or
                                nameof(ShopItem.Description) or
                                nameof(ShopItem.Command) or
                                nameof(ShopItem.Image) or
                                nameof(ShopItem.Amount) or
                                nameof(ShopItem.BuyCooldown) or
                                nameof(ShopItem.SellCooldown) or
                                nameof(ShopItem.BuyMaxAmount) or
                                nameof(ShopItem.SellMaxAmount) or
                                nameof(ShopItem.ForceBuy) or
                                nameof(ShopItem.BuyBlockDurationAfterWipe) or
                                nameof(ShopItem.SellBlockDurationAfterWipe) or
                                nameof(ShopItem.Price) or
                                nameof(ShopItem.SellPrice) or
                                nameof(ShopItem.CanBuy) or
                                nameof(ShopItem.CanSell) => true,
                            _ => false
                        };

                    case ItemType.Plugin:
                        return field.Name switch
                        {
                            nameof(ShopItem.Type) or
                                nameof(ShopItem.Permission) or
                                nameof(ShopItem.Title) or
                                nameof(ShopItem.Description) or
                                // nameof(ShopItem.Plugin) or
                                nameof(ShopItem.Image) or
                                nameof(ShopItem.Amount) or
                                nameof(ShopItem.BuyCooldown) or
                                nameof(ShopItem.SellCooldown) or
                                nameof(ShopItem.BuyMaxAmount) or
                                nameof(ShopItem.SellMaxAmount) or
                                nameof(ShopItem.ForceBuy) or
                                nameof(ShopItem.BuyBlockDurationAfterWipe) or
                                nameof(ShopItem.SellBlockDurationAfterWipe) or
                                nameof(ShopItem.Price) or
                                nameof(ShopItem.SellPrice) or
                                nameof(ShopItem.CanBuy) or
                                nameof(ShopItem.CanSell) => true,
                            _ => false
                        };
                    case ItemType.Kit:
                        return field.Name switch
                        {
                            nameof(ShopItem.Type) or
                                nameof(ShopItem.Permission) or
                                nameof(ShopItem.Title) or
                                nameof(ShopItem.Description) or
                                nameof(ShopItem.Kit) or
                                nameof(ShopItem.Image) or
                                nameof(ShopItem.Amount) or
                                nameof(ShopItem.BuyCooldown) or
                                nameof(ShopItem.SellCooldown) or
                                nameof(ShopItem.BuyMaxAmount) or
                                nameof(ShopItem.SellMaxAmount) or
                                nameof(ShopItem.ForceBuy) or
                                nameof(ShopItem.BuyBlockDurationAfterWipe) or
                                nameof(ShopItem.SellBlockDurationAfterWipe) or
                                nameof(ShopItem.Price) or
                                nameof(ShopItem.SellPrice) or
                                nameof(ShopItem.CanBuy) or
                                nameof(ShopItem.CanSell) => true,
                            _ => false
                        };
                }

                return false;
            }

            public IEnumerable<(string name, FieldInfo field, object value)> GetEditableFields()
            {
                foreach (var kvp in _editableFields) yield return (kvp.Key, kvp.Value, kvp.Value.GetValue(editingItem));
            }

            public void SetFieldValue(string fieldName, object value)
            {
                if (!_editableFields.TryGetValue(fieldName, out var field)) return;

                try
                {
                    var convertedValue = ConvertValue(value, field.FieldType);
                    field.SetValue(editingItem, convertedValue);
                }
                catch (Exception ex)
                {
                    Instance?.Puts($"Error setting field '{fieldName}': {ex.Message}");
                }
            }

            public int GetEditableFieldsCount()
            {
                return _editableFields.Count;
            }

            private object ConvertValue(object value, Type targetType)
            {
                if (value == null) return null;

                if (targetType.IsEnum)
                    return Enum.Parse(targetType, value.ToString());

                return Convert.ChangeType(value, targetType);
            }

            public void RefreshEditableFields()
            {
                CollectEditableFields();
            }

            public FieldInfo GetField(string fieldName)
            {
                return _editableFields.TryGetValue(fieldName, out var field) ? field : null;
            }

            #endregion

            #region Select Item State

            public string selectItemCategory = string.Empty;

            public void ResetSelectItemState()
            {
                selectItemCategory = Instance?._itemsCategories?.FirstOrDefault().Key ?? string.Empty;
            }

            public void SetSelectCategory(string category)
            {
                selectItemCategory = category ?? string.Empty;
            }

            #endregion

            #region Save/Cancel

            public void Save()
            {
                if (isNew)
                {
                    var player = BasePlayer.FindByID(playerID);
                    var shop = Instance?.GetShop(player);
                    var category = shop?.GetSelectedShopCategory();
                    category?.Items.Add(editingItem);
                    category?.LoadIDs(true);
                }
                else
                {
                    var original = Instance?.FindItemById(itemID);
                    if (original != null) CopyTo(original);
                }

                Instance?.SaveItemsData();
                Instance?.LoadItems();

                Remove(playerID);
            }

            public void Cancel()
            {
                Remove(playerID);
            }

            private void CopyTo(ShopItem target)
            {
                foreach (var field in _editableFields.Values) field.SetValue(target, field.GetValue(editingItem));
            }

            #endregion
        }

        #endregion

        #region Edit Category

        private Dictionary<ulong, EditCategoryData> editCategoryData = new();

        private class EditCategoryData
        {
            #region Fields

            public ulong playerID;
            public int categoryID;
            public bool isNew;

            public ShopCategory editingCategory;

            private Dictionary<string, FieldInfo> _editableFields = new();

            #endregion

            #region Localization State

            public string selectedLocalizationKey = "en";

            public void SetLocalizationKey(string key)
            {
                selectedLocalizationKey = key ?? "en";
            }

            #endregion

            #region Factory Methods

            public static EditCategoryData Create(BasePlayer player, int categoryID, bool isNew = false)
            {
                var data = new EditCategoryData
                {
                    playerID = player.userID,
                    categoryID = categoryID,
                    isNew = isNew
                };

                if (isNew)
                {
                    data.editingCategory = new ShopCategory
                    {
                        Enabled = false,
                        CategoryType = ShopCategory.Type.None,
                        Title = string.Empty,
                        Permission = string.Empty,
                        SortType = Configuration.SortType.None,
                        Items = new List<ShopItem>(),
                        Localization = new Localization
                        {
                            Enabled = false,
                            Messages = new Dictionary<string, string>
                            {
                                ["en"] = string.Empty
                            }
                        }
                    };
                }
                else
                {
                    var original = Instance?.FindCategoryById(categoryID);
                    if (original == null) return null;

                    data.editingCategory = original.Clone();
                }

                data.CollectEditableFields();

                Instance?.editCategoryData.TryAdd(player.userID, data);
                return data;
            }

            public static EditCategoryData Get(ulong playerID)
            {
                return Instance?.editCategoryData.TryGetValue(playerID, out var data) == true ? data : null;
            }

            public static void Remove(ulong playerID)
            {
                Instance?.editCategoryData?.Remove(playerID);
            }

            #endregion

            #region Dynamic Fields

            public void CollectEditableFields()
            {
                _editableFields.Clear();

                var fields = typeof(ShopCategory).GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (var field in fields)
                    if (ShouldShowField(field))
                        _editableFields[field.Name] = field;
            }

            private bool ShouldShowField(FieldInfo field)
            {
                return field.Name switch
                {
                    nameof(ShopCategory.Localization) => false,
                    nameof(ShopCategory.Items) => false,
                    _ => true
                };
            }

            public IEnumerable<(string name, FieldInfo field, object value)> GetEditableFields()
            {
                foreach (var kvp in _editableFields)
                    yield return (kvp.Key, kvp.Value, kvp.Value.GetValue(editingCategory));
            }

            public int GetEditableFieldsCount()
            {
                return _editableFields.Count;
            }

            public FieldInfo GetField(string fieldName)
            {
                return _editableFields.TryGetValue(fieldName, out var field) ? field : null;
            }

            public void SetFieldValue(string fieldName, object value)
            {
                var field = typeof(ShopCategory).GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
                if (field == null) return;

                try
                {
                    var convertedValue = ConvertValue(value, field.FieldType);
                    field.SetValue(editingCategory, convertedValue);
                }
                catch (Exception ex)
                {
                    Instance?.Puts($"Error setting field '{fieldName}': {ex.Message}");
                }
            }

            private object ConvertValue(object value, Type targetType)
            {
                if (value == null) return null;

                var strValue = value.ToString();

                if (targetType.IsEnum)
                    return Enum.Parse(targetType, strValue);

                if (targetType == typeof(bool))
                    return bool.TryParse(strValue, out var b) && b;

                if (targetType == typeof(int))
                    return int.TryParse(strValue, out var i) ? i : 0;

                if (targetType == typeof(string))
                    return strValue;

                return Convert.ChangeType(value, targetType);
            }

            #endregion

            #region Localization

            public void SetLocalizationEnabled(bool enabled)
            {
                if (editingCategory.Localization == null)
                    editingCategory.Localization = new Localization();

                editingCategory.Localization.Enabled = enabled;
            }

            public void SetLocalizationMessage(string key, string value)
            {
                if (editingCategory.Localization == null)
                    editingCategory.Localization = new Localization();

                if (editingCategory.Localization.Messages == null)
                    editingCategory.Localization.Messages = new Dictionary<string, string>();

                editingCategory.Localization.Messages[key] = value;
            }

            public void RemoveLocalizationMessage(string key)
            {
                editingCategory.Localization?.Messages?.Remove(key);
            }

            #endregion

            #region Save/Cancel

            public void Save()
            {
                var player = BasePlayer.FindByID(playerID);
                var shop = Instance?.GetShop(player);

                if (isNew)
                {
                    _itemsData.Shop.Add(editingCategory);

                    shop?.Update();
                }
                else
                {
                    var original = Instance?.FindCategoryById(categoryID);
                    if (original != null)
                    {
                        var index = _itemsData.Shop.IndexOf(original);
                        if (index != -1) _itemsData.Shop[index] = editingCategory;
                    }

                    shop?.Update();
                }

                if (editingCategory.CategoryType == ShopCategory.Type.Favorite)
                    Instance._isEnabledFavorites = editingCategory.Enabled;

                Instance?.SaveItemsData();

                Remove(playerID);
            }

            public void Cancel()
            {
                Remove(playerID);
            }

            public void Delete()
            {
                if (isNew) return;

                var original = Instance?.FindCategoryById(categoryID);
                if (original != null) _itemsData.Shop.Remove(original);

                var player = BasePlayer.FindByID(playerID);
                Instance?.GetShop(player)?.Update();

                Instance?.SaveItemsData();

                Remove(playerID);
            }

            #endregion

            #region Command Prefix

            public string GetFieldCommandPrefix()
            {
                return "edit_category field";
            }

            #endregion
        }

        #endregion

        #region Log

        private enum LogType
        {
            Buy,
            Sell,
            Transfer
        }

        private void Log(LogType type, string key, params object[] obj)
        {
            Log(type.ToString(), key, obj);
        }

        private void LogBuySell(BasePlayer player, ShopItem item, int amount, double price, bool isBuy)
        {
            var formattedPrice = GetPlayerEconomy(player).GetPriceTitle(player,
                price.ToString(isBuy ? _config.Formatting.BuyPriceFormat : _config.Formatting.SellPriceFormat));

            var itemText = item + $"[x{amount}]";
            var text = string.Format(lang.GetMessage(isBuy ? LogBuyItems : LogSellItem, this),
                player.displayName, player.UserIDString, formattedPrice, itemText);

            if (_config.LogToConsole) Puts(text);

            if (_config.LogToFile) LogToFile(isBuy ? "Buy" : "Sell", $"[{DateTime.Now}] {text}", this);

            var discordSettings = isBuy ? _config.DiscordConfig.Buy : _config.DiscordConfig.Sell;
            if (discordSettings != null && discordSettings.Enabled && !string.IsNullOrEmpty(discordSettings.Webhook))
                SendDiscordMessageBuySell(discordSettings.Webhook, isBuy ? LogType.Buy : LogType.Sell,
                    discordSettings, player.displayName, player.UserIDString, item.PublicTitle, item.ShortName,
                    item.Skin, item.Amount * amount, formattedPrice, item.Image);
        }

        private void LogTransfer(string playerName, string steamId, string amount, string targetName, string targetId)
        {
            var text = $"Player {playerName} ({steamId}) transferred {amount} to player {targetName} ({targetId}).";

            if (_config.LogToConsole) Puts(text);

            if (_config.LogToFile) LogToFile("Transfer", $"[{DateTime.Now}] {text}", this);

            var discordSettings = _config.DiscordConfig.Transfer;
            if (discordSettings != null && discordSettings.Enabled && !string.IsNullOrEmpty(discordSettings.Webhook))
                SendDiscordMessage(discordSettings.Webhook, text, LogType.Transfer, discordSettings,
                    playerName, steamId, amount, targetName, targetId);
        }

        private void Log(string filename, string key, params object[] obj)
        {
            var text = string.Format(lang.GetMessage(key, this), obj);

            if (_config.LogToConsole) Puts(text);

            if (_config.LogToFile) LogToFile(filename, $"[{DateTime.Now}] {text}", this);

            LogType? logType = null;
            Configuration.DiscordSettings.DiscordLogSettings discordSettings = null;

            if (filename == "Buy")
            {
                logType = LogType.Buy;
                discordSettings = _config.DiscordConfig.Buy;
            }
            else if (filename == "Sell")
            {
                logType = LogType.Sell;
                discordSettings = _config.DiscordConfig.Sell;
            }
            else if (filename == "Transfer")
            {
                logType = LogType.Transfer;
                discordSettings = _config.DiscordConfig.Transfer;
            }

            if (logType.HasValue && discordSettings != null && discordSettings.Enabled &&
                !string.IsNullOrEmpty(discordSettings.Webhook))
                SendDiscordMessage(discordSettings.Webhook, text, logType.Value, discordSettings, obj);
        }

        private void SendDiscordMessageBuySell(string webhook, LogType logType,
            Configuration.DiscordSettings.DiscordLogSettings settings,
            string playerName, string steamId, string itemDisplayName,
            string itemShortName, ulong itemSkin, int amount, string formattedPrice, string itemImage = null)
        {
            var embed = new Embed();

            embed.color = settings.Color;

            if (!string.IsNullOrEmpty(settings.Title))
                embed.title = settings.Title
                    .Replace("{username}", playerName)
                    .Replace("{steamid}", steamId);

            if (!string.IsNullOrEmpty(settings.FooterText))
            {
                var footerText = settings.FooterText
                    .Replace("{username}", playerName)
                    .Replace("{steamid}", steamId)
                    .Replace("{item}", itemDisplayName)
                    .Replace("{amount}", amount.ToString())
                    .Replace("{price}", formattedPrice);

                embed.footer = new Footer
                {
                    text = footerText,
                    icon_url = !string.IsNullOrEmpty(itemImage) ? itemImage : GetItemIconUrl(itemShortName, itemSkin)
                };
            }

            var discordMessageObj = new DiscordMessage("", embed);

            webrequest.Enqueue(webhook, discordMessageObj.ToJson(), (code, response) => { },
                this,
                RequestMethod.POST,
                new Dictionary<string, string>
                {
                    {"Content-Type", "application/json"}
                });
        }

        private string GetItemIconUrl(string shortName, ulong skin)
        {
            if (skin != 0)
                return $"https://www.rustedit.io/images/imagelibrary/{shortName}_{skin}.png";

            return $"https://www.rustedit.io/images/imagelibrary/{shortName}.png";
        }

        private void SendDiscordMessage(string webhook, string message, LogType logType,
            Configuration.DiscordSettings.DiscordLogSettings settings, params object[] parameters)
        {
            var embed = new Embed();

            var playerName = parameters.Length > 0 ? parameters[0]?.ToString() ?? "Unknown" : "Unknown";
            var steamId = parameters.Length > 1 ? parameters[1]?.ToString() ?? "" : "";
            var amount = parameters.Length > 2 ? parameters[2]?.ToString() ?? "" : "";
            var targetName = parameters.Length > 3 ? parameters[3]?.ToString() ?? "" : "";
            var targetId = parameters.Length > 4 ? parameters[4]?.ToString() ?? "" : "";

            embed.color = settings.Color;

            if (!string.IsNullOrEmpty(settings.Title))
                embed.title = settings.Title
                    .Replace("{username}", playerName)
                    .Replace("{steamid}", steamId)
                    .Replace("{targetname}", targetName)
                    .Replace("{targetid}", targetId);

            if (!string.IsNullOrEmpty(settings.FooterText))
            {
                var footerText = settings.FooterText
                    .Replace("{username}", playerName)
                    .Replace("{steamid}", steamId)
                    .Replace("{amount}", amount)
                    .Replace("{targetname}", targetName)
                    .Replace("{targetid}", targetId);

                embed.footer = new Footer
                {
                    text = footerText,
                    icon_url = settings.FooterIconUrl
                };
            }

            var discordMessageObj = new DiscordMessage("", embed);

            webrequest.Enqueue(webhook, discordMessageObj.ToJson(), (code, response) => { },
                this,
                RequestMethod.POST,
                new Dictionary<string, string>
                {
                    {"Content-Type", "application/json"}
                });
        }

        public class Embed
        {
            [JsonProperty("title")] public string title { get; set; }

            [JsonProperty("color")] public int color { get; set; }

            [JsonProperty("fields")] public List<Field> Fields { get; set; } = new();

            [JsonProperty("footer")] public Footer footer { get; set; }

            public Embed AddField(string name, string value, bool inline)
            {
                Fields.Add(new Field(name, Regex.Replace(value, "<.*?>", string.Empty), inline));
                return this;
            }
        }

        public class Field
        {
            public Field(string name, string value, bool inline)
            {
                Name = name;
                Value = value;
                Inline = inline;
            }

            [JsonProperty("name")] public string Name { get; set; }

            [JsonProperty("value")] public string Value { get; set; }

            [JsonProperty("inline")] public bool Inline { get; set; }
        }

        public class Footer
        {
            [JsonProperty("text")] public string text { get; set; }

            [JsonProperty("icon_url")] public string icon_url { get; set; }
        }

        public class DiscordMessage
        {
            [JsonProperty("content")] public string Content { get; set; }

            [JsonProperty("embeds")] public List<Embed> Embeds { get; set; } = new();

            public DiscordMessage(string content, Embed embed)
            {
                Content = content;
                Embeds.Add(embed);
            }

            public string ToJson()
            {
                return JsonConvert.SerializeObject(this);
            }
        }

        #endregion

        #region API

        private int API_GetShopPlayerSelectedEconomy(ulong playerID)
        {
            if (_templateMigrationInProgress) return 0;

            return PlayerData.GetOrCreate(playerID)?.SelectedEconomy ?? 0;
        }

        private string API_OpenPlugin(BasePlayer player)
        {
            if (_templateMigrationInProgress) return string.Empty;

            if (_initializedStatus.status is false)
            {
                if (_initializedStatus.message == null)
                {
                    _config?.Notifications?.ShowNotify(player, UIMsgShopInInitialization, 1);
                    return string.Empty;
                }

                _config?.Notifications?.ShowNotify(player, NoILError, 1);

                PrintError(ConvertInitializedStatus());
                return string.Empty;
            }

            if (_uiData is not {IsInMenuUISet: true})
            {
                var reason = _uiData == null ? "UI data is missing." : "Menu UI is not initialized.";
                var msg = $"Error: UI is unavailable for player {player.UserIDString}. Reason: {reason}";
                PrintError(msg);
                _config?.Notifications?.ShowNotify(player,
                    player.IsAdmin ? msg : "The UI is currently unavailable. Please try again later.", 1);
                return string.Empty;
            }

            var elements = Pool.Get<List<string>>();
            var sb = Pool.Get<StringBuilder>();
            try
            {
                RemoveOpenedShop(player.userID);

                var shop = GetShop(player, false);

                var shopUI = shop.GetUI();

                #region Background

                elements.Add(CuiJsonFactory.CreatePanel(parent: "UI.Server.Panel.Content",
                    name: "UI.Server.Panel.Content.Plugin", destroy: "UI.Server.Panel.Content.Plugin",
                    color: HexToCuiColor("#000000", 0),
                    anchorMin: "0 0", anchorMax: "1 1", offsetMin: "0 0", offsetMax: "0 0"));

                elements.Add(CuiJsonFactory.CreatePanel(parent: "UI.Server.Panel.Content.Plugin",
                    name: Layer + ".Background",
                    destroy: Layer + ".Background",
                    color: HexToCuiColor("#000000", 0),
                    anchorMin: "0 0", anchorMax: "1 1", offsetMin: "0 0", offsetMax: "0 0"));

                #endregion Background

                elements.Add(shopUI.ShopContent.Background.GetSerialized(player, Layer + ".Background", Layer + ".Main",
                    Layer + ".Main"));

                ShopCategoriesUI(ref elements, player);

                ShopHeaderUI(ref elements, player);

                ShopContentUI(player, ref elements);

                ShopBasketUI(ref elements, player);

                #region Merge Elements

                if (elements.Count > 0)
                    for (var i = 0; i < elements.Count; i++)
                    {
                        if (string.IsNullOrEmpty(elements[i])) continue;

                        if (i > 0) sb.Append(',');

                        sb.Append(elements[i]);
                    }

                #endregion Merge Elements

                return sb.ToString();
            }
            finally
            {
                Pool.FreeUnmanaged(ref elements);
                Pool.FreeUnmanaged(ref sb);
            }
        }

#if TESTING
        public void API_SetShopTemplate(ShopUI shopUI, bool ifFullscreen = true)
        {
            if (_uiData == null) _uiData = new UIData();

            if (ifFullscreen)
            {
                _uiData.IsFullscreenUISet = true;
                _uiData.FullscreenUI = shopUI;
                _uiData?.FullscreenUI?.LoadAllElements();
            }
            else
            {
                _uiData.IsInMenuUISet = true;
                _uiData.InMenuUI = shopUI;
                _uiData?.InMenuUI?.LoadAllElements();
            }

            SaveTemplate();

            LoadImages();

            foreach (var p in BasePlayer.activePlayerList) p.SendConsoleCommand("chat.say /shop");
        }
#endif

        #endregion

        #region Lang

        private const string
            UIDLCItem = "UIDLCItem",
            UIMsgNoItems = "UIMsgNoItems",
            UIBasketFooterItemsCountTitle = "UIBasketFooterItemsCountTitle",
            UIBasketFooterItemsCountValue = "UIBasketFooterItemsCountValue",
            UIBasketFooterItemsCostTitle = "UIBasketFooterItemsCostTitle",
            UIContentHeaderButtonToggleEconomy = "UIContentHeaderButtonToggleEconomy",
            UIShopActionAmountTitle = "UIShopActionAmountTitle",
            UIShopActionDescriptionTitle = "UIShopActionDescriptionTitle",
            UIShopItemDescriptionTitle = "UIShopItemDescriptionTitle",
            UIShopItemAmount = "UIShopItemAmount",
            UICategoriesAdminShowAllTitle = "UICategoriesAdminShowAllTitle",
            UIMsgShopInInitialization = "UIMsgShopInInitialization",
            MsgRemovedFromFavoriteItem = "MsgRemovedFromFavoriteItem",
            MsgAddedToFavoriteItem = "MsgAddedToFavoriteItem",
            MsgIsFavoriteItem = "MsgIsFavoriteItem",
            MsgNoFavoriteItem = "MsgNoFavoriteItem",
            NoILError = "NoILError",
            BtnBoolON = "BtnBoolON",
            BtnBoolOFF = "BtnBoolOFF",
            BtnEditCategory = "BtnEditCategory",
            BtnAddCategory = "BtnAddCategory",
            EditingCategoryTitle = "EditingCategoryTitle",
            BtnCalculate = "BtnCalculate",
            ItemPriceFree = "ItemPriceFree",
            NPCInstalled = "NPCInstalled",
            NPCNotFound = "NPCNotFound",
            EditBlueprint = "EditBlueprint",
            ChoiceEconomy = "ChoiceEconomy",
            VMInstalled = "VMInstalled",
            VMExists = "VMExists",
            VMNotFound = "VMNotFound",
            VMNotFoundCategories = "VMNotFoundCategories",
            ErrorSyntax = "ErrorSyntax",
            NoPermission = "NoPermission",
            NoTransferPlayers = "NoTransferPlayers",
            TitleMax = "TitleMax",
            TransferButton = "TransferButton",
            TransferTitle = "TransferTitle",
            SuccessfulTransfer = "SuccessfulTransfer",
            PlayerNotFound = "PlayerNotFound",
            SelectPlayerTitle = "SelectPlayerTitle",
            BuyWipeCooldown = "BuyWipeCooldown",
            SellWipeCooldown = "SellWipeCooldown",
            BuyRespawnCooldown = "BuyRespawnCooldown",
            SellRespawnCooldown = "SellRespawnCooldown",
            LogSellItem = "LogSellItem",
            LogBuyItems = "LogBuyItems",
            SkinBlocked = "SkinBlocked",
            NoUseDuel = "NoUseDuel",
            DailySellLimitReached = "DailySellLimitReached",
            DailyBuyLimitReached = "DailyBuyLimitReached",
            SellLimitReached = "SellLimitReached",
            BuyLimitReached = "BuyLimitReached",
            InfoTitle = "InfoTitle",
            BuyRaidBlocked = "BuyRaidBlocked",
            SellRaidBlocked = "SellRaidBlocked",
            DaysFormat = "DaysFormat",
            HoursFormat = "HoursFormat",
            MinutesFormat = "MinutesFormat",
            SecondsFormat = "SecondsFormat",
            NotEnoughSpace = "NotEnoughtSpace",
            NotMoney = "NotMoney",
            ReceivedItems = "GiveItem",
            BuyTitle = "BuyTitle",
            SellTitle = "SellTitle",
            PlusTitle = "PlusTitle",
            MinusTitle = "MinusTitle",
            RemoveTitle = "RemoveTitle",
            AmountTitle = "AmountTitle",
            NextTitle = "NextTitle",
            BackTitle = "BackTitle",
            ItemAmount = "ItemAmount",
            CloseButton = "CloseButton",
            YourBalance = "YourBalance",
            MainTitle = "MainTitle",
            CategoriesTitle = "CategoriesTitle",
            ShoppingBag = "ShoppingBag",
            PurchaseConfirmation = "PurchaseConfirmation",
            PurchaseConfirmationSubtitle = "PurchaseConfirmationSubtitle",
            CancelTitle = "CancelTitle",
            ErrorClose = "ErrorClose",
            BtnSave = "BtnSave",
            ErrorMsg = "ErrorMsg",
            NotEnough = "NotEnough",
            Back = "Back",
            Next = "Next",
            ItemName = "ItemName",
            CmdName = "CmdName",
            RemoveItem = "RemoveItem",
            ItemSearch = "ItemSearch",
            PluginName = "PluginName",
            BtnSelect = "BtnSelect",
            BtnAddItem = "AddItem",
            EditingTitle = "EditingTitle",
            SearchTitle = "SearchTitle",
            BackPage = "BackPage",
            NextPage = "NextPage",
            SellCooldownTitle = "SellCooldownTitle",
            BuyCooldownTitle = "BuyCooldownTitle",
            BuyCooldownMessage = "BuyCooldownMessage",
            SellCooldownMessage = "SellCooldownMessage",
            BtnNext = "BtnNext",
            BtnBack = "BtnBack",
            SellNotify = "SellNotify";

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [DaysFormat] = " {0} d. ",
                [HoursFormat] = " {0} h. ",
                [MinutesFormat] = " {0} m. ",
                [SecondsFormat] = " {0} s. ",
                [NotEnoughSpace] = "Not enought space",
                [NotMoney] = "You don't have enough money!",
                [ReceivedItems] = "All items received!",
                [BuyTitle] = "Buy",
                [SellTitle] = "Sell",
                [PlusTitle] = "+",
                [MinusTitle] = "-",
                [RemoveTitle] = "Remove",
                [AmountTitle] = "x{0}",
                [BackTitle] = "Back",
                [NextTitle] = "Next",
                [ItemAmount] = "Amt.",
                [CloseButton] = "✕",
                [YourBalance] = "Your Balance",
                [MainTitle] = "Shop",
                [CategoriesTitle] = "Categories",
                [ShoppingBag] = "Basket",
                [PurchaseConfirmation] = "Purchase confirmation",
                [PurchaseConfirmationSubtitle] = "THIS ACTION CANNOT BE UNDONE",
                [CancelTitle] = "Cancel",
                [ErrorClose] = "CLOSE",
                [ErrorMsg] = "XXX",
                [NotEnough] = "You don't have enough item!",
                [BtnSelect] = "Select",
                [EditingTitle] = "Item editing",
                [ItemSearch] = "Item search",
                [Back] = "Back",
                [Next] = "Next",
                [RemoveItem] = "✕",
                [BtnSave] = "Save",
                [ItemName] = "Item",
                [CmdName] = "Command",
                [PluginName] = "Plugin",
                [SearchTitle] = "Search...",
                [BackPage] = "<",
                [NextPage] = ">",
                [SellCooldownTitle] = "Sell wait",
                [BuyCooldownTitle] = "Buy wait",
                [BuyCooldownMessage] = "You cannot buy the '{0}' item! Wait {1}",
                [SellCooldownMessage] = "You cannot sell the '{0}' item! Wait {1}",
                [BtnBack] = "▲",
                [BtnNext] = "▼",
                [SellNotify] = "You have successfully sold {0} pcs of {1}",
                [BuyRaidBlocked] = "You can't buy while blocked!",
                [SellRaidBlocked] = "You can't sell while blocked!",
                [BuyWipeCooldown] = "You can't buy for another {0}!",
                [SellWipeCooldown] = "You can't sell for another {0}!",
                [BuyRespawnCooldown] = "You can't buy for another {0}!",
                [SellRespawnCooldown] = "You can't sell for another {0}!",
                [InfoTitle] = "i",
                [DailyBuyLimitReached] =
                    "You cannot buy the '{0}'. You have reached the daily limit. Come back tomorrow!",
                [DailySellLimitReached] =
                    "You cannot buy the '{0}'. You have reached the daily limit. Come back tomorrow!",
                [BuyLimitReached] = "You cannot buy the '{0}'. You have reached the limit",
                [SellLimitReached] = "You cannot sell the '{0}'. You have reached the limit",
                [NoUseDuel] = "You are in a duel. The use of the shop is blocked.",
                [SkinBlocked] = "Skin is blocked for sale",
                [LogBuyItems] = "Player {0} ({1}) bought items for {2}$: {3}.",
                [LogSellItem] = "Player {0} ({1}) sold item for {2}$: {3}.",
                [SelectPlayerTitle] = "Select player to transfer",
                [PlayerNotFound] = "Player not found",
                [SuccessfulTransfer] = "Transferred {0} to player '{1}'",
                [TransferTitle] = "Transfer",
                [TransferButton] = "Send money",
                [TitleMax] = "MAX",
                [NoTransferPlayers] = "Unfortunately, there are currently no players available for transfer",
                [NoPermission] = "You don't have the required permission",
                [ErrorSyntax] = "Syntax error! Use: /{0}",
                [VMNotFoundCategories] = "Categories not found!",
                [VMNotFound] = "Vending Machine not found!",
                [VMExists] = "This Vending Machine is already in the config!",
                [VMInstalled] = "You have successfully installed the custom Vending Machine!",
                [ChoiceEconomy] = "Choice of currency",
                [EditBlueprint] = "Blueprint",
                [NPCNotFound] = "NPC not found!",
                [NPCInstalled] = "You have successfully installed the custom NPC!",
                [ItemPriceFree] = "FREE",
                [BtnCalculate] = "Calculate",
                [EditingCategoryTitle] = "Category editing",
                [BtnBoolOFF] = "OFF",
                [BtnBoolON] = "ON",
                [NoILError] = "The plugin does not work correctly, contact the administrator!",
                [MsgNoFavoriteItem] = "You can't remove this item from favorites because it is not a favorite",
                [MsgIsFavoriteItem] = "You cannot add this item to favorites because it is already a favorite",
                [MsgAddedToFavoriteItem] = "Item '{0}' has been added to your favorites!",
                [MsgRemovedFromFavoriteItem] = "Item '{0}' has been removed from favorites!",
                [UIMsgNoItems] = "Sorry, there are currently no items available",
                [UIBasketFooterItemsCountTitle] = "Items",
                [UIBasketFooterItemsCountValue] = "{0} pcs",
                [UIBasketFooterItemsCostTitle] = "Cost",
                [UIContentHeaderButtonToggleEconomy] = "▲",
                [UIShopActionAmountTitle] = "AMOUNT",
                [UIShopActionDescriptionTitle] = "DESCRIPTION",
                [UIShopItemDescriptionTitle] = "DESCRIPTION",
                [UIShopItemAmount] = "x{0}",
                [UIMsgShopInInitialization] =
                    "The plugin is currently initializing. Please wait a moment while the process completes.",
                [UIDLCItem] = "You can't buy this item",
                [BtnAddItem] = "ADD ITEM",
                [UICategoriesAdminShowAllTitle] = "ADMIN MODE",
                [BtnAddCategory] = "ADD CATEGORY",
                [BtnEditCategory] = "EDIT"
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                [DaysFormat] = " {0} д. ",
                [HoursFormat] = " {0} ч. ",
                [MinutesFormat] = " {0} м. ",
                [SecondsFormat] = " {0} с. ",
                [NotEnoughSpace] = "Недостаточно места",
                [NotMoney] = "У вас недостаточно денег!",
                [ReceivedItems] = "Все предметы получены!",
                [BuyTitle] = "Купить",
                [SellTitle] = "Продать",
                [PlusTitle] = "+",
                [MinusTitle] = "-",
                [RemoveTitle] = "Удалить",
                [AmountTitle] = "x{0}",
                [BackTitle] = "Назад",
                [NextTitle] = "Вперёд",
                [ItemAmount] = "Кол.",
                [CloseButton] = "✕",
                [YourBalance] = "Ваш Баланс",
                [MainTitle] = "Магазин",
                [CategoriesTitle] = "Категории",
                [ShoppingBag] = "Корзина",
                [PurchaseConfirmation] = "Подтверждение покупки",
                [PurchaseConfirmationSubtitle] = "ЭТО ДЕЙСТВИЕ НЕ МОЖЕТ БЫТЬ ОТМЕНЕНО",
                [CancelTitle] = "Отменить",
                [ErrorClose] = "ЗАКРЫТЬ",
                [ErrorMsg] = "XXX",
                [NotEnough] = "У вас недостаточно предметов!",
                [BtnSelect] = "Выбрать",
                [EditingTitle] = "Редактирование предмета",
                [ItemSearch] = "Поиск предмета",
                [Back] = "Назад",
                [Next] = "Вперёд",
                [RemoveItem] = "✕",
                [BtnSave] = "Сохранить",
                [ItemName] = "Предмет",
                [CmdName] = "Команда",
                [PluginName] = "Плагин",
                [BtnAddItem] = "+ ПРЕДМЕТ",
                [SearchTitle] = "Поиск...",
                [BackPage] = "<",
                [NextPage] = ">",
                [SellCooldownTitle] = "КД продажи",
                [BuyCooldownTitle] = "КД покупки",
                [BuyCooldownMessage] = "Вы не можете купить '{0}'! Подождите {1}",
                [SellCooldownMessage] = "Вы не можете продать '{0}'! Подождите {1}",
                [BtnBack] = "▲",
                [BtnNext] = "▼",
                [SellNotify] = "Вы успешно продали {0} шт за {1}",
                [BuyRaidBlocked] = "Вы не можете покупать во время блокировки рейда!",
                [SellRaidBlocked] = "Вы не можете продавать во время блокировки рейда!",
                [BuyWipeCooldown] = "Вы не можете покупать ещё {0}!",
                [SellWipeCooldown] = "Вы не можете продавать ещё  {0}!",
                [BuyRespawnCooldown] = "Вы не можете покупать ещё  {0}!",
                [SellRespawnCooldown] = "Вы не можете продавать ещё  {0}!",
                [InfoTitle] = "i",
                [DailyBuyLimitReached] =
                    "Вы не можете купить '{0}'. Вы достигли дневного лимита. Возвращайтесь завтра!",
                [DailySellLimitReached] =
                    "Вы не можете продать '{0}'. Вы достигли дневного лимита. Возвращайтесь завтра!",
                [BuyLimitReached] = "Вы не можете купить '{0}'. Вы достигли лимита",
                [SellLimitReached] = "Вы не можете продать '{0}'. Вы достигли лимита",
                [NoUseDuel] = "Вы на дуэли. Использование магазина запрещено.",
                [SkinBlocked] = "Скин запрещён для продажи",
                [LogBuyItems] = "Player {0} ({1}) bought items for {2}$: {3}.",
                [LogSellItem] = "Player {0} ({1}) sold item for {2}$: {3}.",
                [SelectPlayerTitle] = "Выберите игрока для перевода",
                [PlayerNotFound] = "Игрок не найден",
                [SuccessfulTransfer] = "Переведено {0} игроку '{1}'",
                [TransferTitle] = "Переводы",
                [TransferButton] = "Отправить",
                [TitleMax] = "MAX",
                [NoTransferPlayers] = "К сожалению, в настоящее время нет игроков, доступных для перевода",
                [NoPermission] = "У вас нет необходимого разрешения",
                [ErrorSyntax] = "Syntax error! Use: /{0}",
                [VMNotFoundCategories] = "Категории не найдены!",
                [VMNotFound] = "Торговый Автомат не найден!",
                [VMExists] = "Этот торговый автомат уже в конфиге!",
                [VMInstalled] = "Вы успешно установили кастомный торговый автомат!",
                [ChoiceEconomy] = "Выбор валюты",
                [EditBlueprint] = "Чертёж",
                [NPCNotFound] = "NPC не найден!",
                [NPCInstalled] = "Вы успешно установили магазин NPC!",
                [ItemPriceFree] = "FREE",
                [BtnCalculate] = "Рассчитать",
                [EditingCategoryTitle] = "Редактирование категории",
                [BtnAddCategory] = "+ КАТЕГОРИЯ",
                [BtnEditCategory] = "ИЗМЕНИТЬ",
                [BtnBoolOFF] = "ВЫКЛ",
                [BtnBoolON] = "ВКЛ",
                [NoILError] = "Плагин работает некорректно, свяжитесь с администратором!",
                [MsgNoFavoriteItem] =
                    "Вы не можете удалить этот предмет из избранного, потому что он не является избранным",
                [MsgIsFavoriteItem] =
                    "Вы не можете добавить этот предмет в избранное, потому что он уже является избранным",
                [MsgAddedToFavoriteItem] = "Предмет '{0}' добавлен в избранное!",
                [MsgRemovedFromFavoriteItem] = "Предмет '{0}' удалён из избранного!",
                [UIMsgNoItems] = "К сожалению, в данный момент товаров в наличии нет",
                [UIBasketFooterItemsCountTitle] = "Предметы",
                [UIBasketFooterItemsCountValue] = "{0} шт",
                [UIBasketFooterItemsCostTitle] = "Цена",
                [UIContentHeaderButtonToggleEconomy] = "▲",
                [UIShopActionAmountTitle] = "КОЛИЧЕСТВО",
                [UIShopActionDescriptionTitle] = "ОПИСАНИЕ",
                [UIShopItemDescriptionTitle] = "ОПИСАНИЕ",
                [UIShopItemAmount] = "x{0}",
                [UICategoriesAdminShowAllTitle] = "АДМИН РЕЖИМ",
                [UIMsgShopInInitialization] =
                    "В настоящее время плагин инициализируется. Пожалуйста, подождите немного, пока процесс завершится.",
                [UIDLCItem] = "Вы не можете купить этот предмет"
            }, this, "ru");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                [DaysFormat] = "{0}天",
                [HoursFormat] = "{0}小时",
                [MinutesFormat] = "{0}分",
                [SecondsFormat] = "{0}秒",
                [NotEnoughSpace] = "背包空间不足",
                [NotMoney] = "资金不够！",
                [ReceivedItems] = "已收到所有的物品！",
                [BuyTitle] = "购买",
                [SellTitle] = "售卖",
                [PlusTitle] = "+",
                [MinusTitle] = "-",
                [RemoveTitle] = "移除",
                [AmountTitle] = "x{0}",
                [BackTitle] = "上一页",
                [NextTitle] = "下一页",
                [ItemAmount] = "数量",
                [CloseButton] = "✕",
                [YourBalance] = "您的余额",
                [MainTitle] = "商店",
                [CategoriesTitle] = "类别",
                [ShoppingBag] = "购物车",
                [PurchaseConfirmation] = "确认购买",
                [PurchaseConfirmationSubtitle] = "此操作无法撤销",
                [CancelTitle] = "取消",
                [ErrorClose] = "关闭",
                [ErrorMsg] = "XXX",
                [NotEnough] = "您没有足够的物品！",
                [BtnSelect] = "选择",
                [EditingTitle] = "物品编辑",
                [ItemSearch] = "物品搜索",
                [Back] = "上一页",
                [Next] = "下一页",
                [RemoveItem] = "✕",
                [BtnSave] = "保存",
                [ItemName] = "物品",
                [CmdName] = "指令",
                [PluginName] = "插件",
                [BtnAddItem] = "添加物品",
                [SearchTitle] = "搜索...",
                [BackPage] = "<",
                [NextPage] = ">",
                [SellCooldownTitle] = "卖冷却",
                [BuyCooldownTitle] = "买冷却",
                [BuyCooldownMessage] = "您无法购买“{0}”商品！等待{1}",
                [SellCooldownMessage] = "您不能出售“{0}”商品！等待{1}",
                [BtnBack] = "▲",
                [BtnNext] = "▼",
                [SellNotify] = "您已成功售出 {0} 件 {1}",
                [BuyRaidBlocked] = "炸家封锁状态中无法购买！",
                [SellRaidBlocked] = "炸家封锁状态中不能出售！",
                [BuyWipeCooldown] = "您无法再购买{0}！",
                [SellWipeCooldown] = "您不能再售卖 {0}！",
                [BuyRespawnCooldown] = "您无法再购买{0}！",
                [SellRespawnCooldown] = "您不能再出售{0}！",
                [InfoTitle] = "我",
                [DailyBuyLimitReached] = "已达到每日限额，无法购买“{0}”",
                [DailySellLimitReached] = "已达到每日限额，无法售卖“{0}”",
                [BuyLimitReached] = "已达到最大限额，无法购买“{0}”",
                [SellLimitReached] = "已达到最大限额，无法出售“{0}”",
                [NoUseDuel] = "决斗中无法使用商店",
                [SkinBlocked] = "皮肤被无法出售",
                [LogBuyItems] = "玩家 {0} ({1}) 以 {2}$ 购买了物品：{3}",
                [LogSellItem] = "玩家 {0} ({1}) 以 {2}$ 的价格出售了物品：{3}",
                [SelectPlayerTitle] = "选择要转帐的玩家",
                [PlayerNotFound] = "未找到玩家",
                [SuccessfulTransfer] = "已将 {0} 转帐给玩家“{1}”",
                [TransferTitle] = "转帐",
                [TransferButton] = "寄钱",
                [TitleMax] = "最大上限",
                [NoTransferPlayers] = "目前没有玩家可以转帐",
                [NoPermission] = "您没有所需的权限",
                [ErrorSyntax] = "语法错误！使用：/{0}",
                [VMNotFoundCategories] = "未找到类别！",
                [VMNotFound] = "未找到自动售货机！",
                [VMExists] = "该自动售货机已在配置中！",
                [VMInstalled] = "您已成功安装客制的自动售货机！",
                [ChoiceEconomy] = "选择货币",
                [EditBlueprint] = "蓝图",
                [NPCNotFound] = "未找到NPC！",
                [NPCInstalled] = "您已成功安装自定义的NPC！",
                [ItemPriceFree] = "免费",
                [BtnCalculate] = "计算",
                [EditingCategoryTitle] = "类别编辑",
                [BtnAddCategory] = "添加分类",
                [BtnEditCategory] = "编辑",
                [BtnBoolOFF] = "关闭",
                [BtnBoolON] = "开启",
                [NoILError] = "插件无法正常使用，请联系管理员！",
                [MsgNoFavoriteItem] = "您无法从收藏夹中删除该项目，因为它不是收藏物品",
                [MsgIsFavoriteItem] = "您无法将此物件添加到收藏夹，因为它已经是收藏物品",
                [MsgAddedToFavoriteItem] = "物件“{0}”已添加到您的收藏夹！",
                [MsgRemovedFromFavoriteItem] = "物件“{0}”已从收藏夹中删除！",
                [UIMsgNoItems] = "抱歉，目前没有商品",
                [UIBasketFooterItemsCountTitle] = "商品",
                [UIBasketFooterItemsCountValue] = "{0} RP",
                [UIBasketFooterItemsCostTitle] = "费用",
                [UIContentHeaderButtonToggleEconomy] = "▲",
                [UIShopActionAmountTitle] = "金额",
                [UIShopActionDescriptionTitle] = "说明",
                [UIShopItemDescriptionTitle] = "说明",
                [UIShopItemAmount] = "x{0}",
                [UICategoriesAdminShowAllTitle] = "管理员模式",
                [UIMsgShopInInitialization] = "插件正在初始化。请稍候。",
                [UIDLCItem] = "您无法购买此物品"
            }, this, "zh-CN");
        }

        private string Msg(BasePlayer player, string key, params object[] obj)
        {
            return string.Format(lang.GetMessage(key, this, player.UserIDString), obj);
        }

        private void Reply(BasePlayer player, string key, params object[] obj)
        {
            SendReply(player, Msg(player, key, obj));
        }

        private void SendNotify(BasePlayer player, string key, int type, params object[] obj)
        {
            if (_config.UseNotify && (Notify != null || UINotify != null))
                Interface.Oxide.CallHook("SendNotify", player, type, Msg(player, key, obj));
            else
                Reply(player, key, obj);
        }

        #endregion

        #region Cache

        #region Search

        private readonly Dictionary<string, HashSet<SearchInfo>> _searchCache = new();

        private class SearchInfo
        {
            public string Permission;

            public ShopItem Item;

            public SearchInfo(string permission, ShopItem item)
            {
                Permission = permission;
                Item = item;
            }
        }

        private List<ShopItem> SearchItem(BasePlayer player, string search, int skip = 0, int take = -1)
        {
            var index = 0;
            var taken = 0;

            var items = Pool.Get<List<ShopItem>>();

            if (_searchCache.TryGetValue(search, out var searchInfo))
            {
                foreach (var info in searchInfo)
                    if (string.IsNullOrEmpty(info.Permission) || player.HasPermission(info.Permission))
                    {
                        if (index++ < skip)
                            continue;

                        items.Add(info.Item);
                        taken++;

                        if (take > 0 && taken >= take)
                            break;
                    }

                return items;
            }

            var shop = GetShop(player);

            foreach (var category in shop.Categories)
            {
                var categoryItems = category.GetShopItems(player);
                try
                {
                    foreach (var item in categoryItems)
                    {
                        var itemTitle = item?.GetPublicTitle(player);
                        if (itemTitle == null)
                            continue;

                        if (itemTitle.StartsWith(search) ||
                            itemTitle.Contains(search) ||
                            item.ShortName?.StartsWith(search) == true || item.ShortName?.Contains(search) == true)
                        {
                            if (index++ < skip)
                                continue;

                            items.Add(item);
                            taken++;

                            if (take > 0 && taken >= take)
                                break;

                            if (_searchCache.TryGetValue(search, out var cache))
                                cache.Add(new SearchInfo(category.Permission, item));
                            else
                                _searchCache.Add(search, new HashSet<SearchInfo>
                                {
                                    new(category.Permission, item)
                                });
                        }
                    }
                }
                finally
                {
                    Pool.FreeUnmanaged(ref categoryItems);
                }
            }

            return items;
        }

        private int SearchItemCount(BasePlayer player, string search)
        {
            var items = SearchItem(player, search);
            try
            {
                return items.Count;
            }
            finally
            {
                Pool.FreeUnmanaged(ref items);
            }
        }

        #endregion

        #region Fields

        private readonly Dictionary<ulong, OpenedShop> _openSHOP = new();

        private class OpenedShop
        {
            #region Fields

            public BasePlayer Player;

            public List<ShopCategory> Categories = Pool.Get<List<ShopCategory>>();

            private bool useMainUI;

            #endregion

            public OpenedShop(BasePlayer player, bool mainUI = true)
            {
                search = null;

                Player = player;

                useMainUI = mainUI;

                Update();

                UpdateAvailableShowCategoriesMoveButtons();

                var selectedCategory = GetSelectedShopCategory();
                if (selectedCategory != null)
                {
                    if (HasSearch())
                        categoryItemsCount = Instance.SearchItemCount(Player, search);
                    else
                        categoryItemsCount = selectedCategory.GetShopItemsCount(Player);
                }
                else
                {
                    categoryItemsCount = 0;
                }
            }

            #region UI

            public ShopUI GetUI()
            {
                return useMainUI ? _uiData.FullscreenUI : _uiData.InMenuUI;
            }

            #endregion

            #region Updates

            public void Update()
            {
                Pool.FreeUnmanaged(ref Categories);

                Categories = Instance.GetCategories(Player);
            }

            #endregion

            #region Categories

            public int
                categoryItemsCount,
                currentCategoryIndex,
                currentCategoriesPage,
                currentShopPage,
                currentSearchPage,
                currentBasketPage;

            public string search;

            public bool canShowCategoriesMoveButtons = true, canShowAddItemButton = true;

            public void OnChangeCategory(int newCategoryIndex, int newCategoriesPage)
            {
                search = null;
                currentCategoryIndex = newCategoryIndex;
                currentCategoriesPage = newCategoriesPage;

                OnChangeShopPage(0);

                search = null;
                currentSearchPage = 0;

                UpdateAvailableShowCategoriesMoveButtons();

                var selectedCategory = GetSelectedShopCategory();
                if (selectedCategory != null)
                {
                    if (HasSearch())
                        categoryItemsCount = Instance.SearchItemCount(Player, search);
                    else
                        categoryItemsCount = selectedCategory.GetShopItemsCount(Player);
                }
                else
                {
                    categoryItemsCount = 0;
                }
            }

            public void OnChangeSearch(string newSearch, int newSearchPage)
            {
                search = newSearch;
                currentSearchPage = newSearchPage;

                UpdateAvailableShowCategoriesMoveButtons();

                currentCategoryIndex = -1;
                currentCategoriesPage = 0;

                if (HasSearch())
                    categoryItemsCount = Instance.SearchItemCount(Player, search);
            }

            public void OnChangeShopPage(int newShopPage)
            {
                currentShopPage = newShopPage;
            }

            public void OnChangeCategoriesPage(int newCategoriesPage)
            {
                currentCategoriesPage = newCategoriesPage;
            }

            public void OnChangeBasketPage(int newBasketPage)
            {
                currentBasketPage = newBasketPage;
            }

            public bool HasSearch()
            {
                return GetUI().ShopContent.Header.Search.Enabled && !string.IsNullOrEmpty(search);
            }

            public ShopCategory GetSelectedShopCategory()
            {
                if (HasSearch())
                    return null;

                return currentCategoryIndex >= 0 && currentCategoryIndex < Categories.Count
                    ? Categories[currentCategoryIndex]
                    : null;
            }

            private void UpdateAvailableShowCategoriesMoveButtons()
            {
                var shopCategory = GetSelectedShopCategory();

                canShowCategoriesMoveButtons = !HasSearch() && shopCategory is {SortType: Configuration.SortType.None}
                    and not {CategoryType: ShopCategory.Type.Favorite};

                canShowAddItemButton = !HasSearch() && shopCategory is not {CategoryType: ShopCategory.Type.Favorite};
            }

            #endregion Categories
        }

        private OpenedShop GetShop(BasePlayer player, bool mainUI = true)
        {
            if (!TryGetShop(player.userID, out var shop))
                _openSHOP.TryAdd(player.userID, shop = new OpenedShop(player, mainUI));

            return shop;
        }

        private OpenedShop GetShop(ulong player)
        {
            return _openSHOP.GetValueOrDefault(player);
        }

        private bool TryGetShop(ulong player, out OpenedShop shop)
        {
            return _openSHOP.TryGetValue(player, out shop);
        }

        private bool RemoveOpenedShop(ulong player)
        {
            return _openSHOP.Remove(player);
        }

        #endregion

        #endregion

        #region Testing functions

#if TESTING
        private static void SayDebug(string message)
        {
            Debug.Log($"[Shop.Debug] {message}");
        }

        private void DebugMessage(string format, long time)
        {
            PrintWarning(format, time);
        }

        private class StopwatchWrapper : IDisposable
        {
            public StopwatchWrapper(string format)
            {
                Sw = Stopwatch.StartNew();
                Format = format;
            }

            public static Action<string, long> OnComplete { private get; set; }

            private string Format { get; }
            private Stopwatch Sw { get; }

            public long Time { get; private set; }

            public void Dispose()
            {
                Sw.Stop();
                Time = Sw.ElapsedMilliseconds;
                OnComplete(Format, Time);
            }
        }

        private void API_SetShopTemplate(object data, bool isFullscreen)
        {
            if (isFullscreen)
                _uiData.FullscreenUI = (ShopUI) data;
            else
                _uiData.InMenuUI = (ShopUI) data;
        }

#endif

        #endregion

        #region Installer

        private ShopInstaller _shopInstaller;

        #region Installer Classes

        public class ShopTemplates
        {
            public ShopTemplate[] FullScreenTemplates;
            public ShopTemplate[] InMenuTemplates;
            public ShopDependency[] CarbonDependencies;
            public ShopDependency[] Dependencies;
            public Dictionary<string, ShopInstallerLang> InstallerLang;
            public Dictionary<string, string> Images;
        }

        public class ShopTemplate
        {
            public string Title;

            public string BannerURL;

            public string VideoURL;

            public ShopUI SettingsUI;

            public Dictionary<string, ShopInstallerLang> TemplateLang;
        }

        public class ShopInstallerLang
        {
            public Dictionary<string, string> Messages;
        }

        public class ShopDependency
        {
            public string PluginName;
            public string PluginAuthor;
            public bool IsRequired;

            public Dictionary<string, (string Title, string Description)> Messages = new(); // status – message

            public string GetStatus()
            {
                var plugin = Instance?.plugins.Find(PluginName);
                if (plugin == null) return IsRequired ? "install" : "missing";

                if (!string.IsNullOrEmpty(PluginAuthor))
                    return "missing";

                if (!IsVersionInRange(plugin.Version))
                    return "wrong_version";

                return "ok";
            }

            #region Version

            public VersionNumber versionFrom = default;

            public VersionNumber versionTo = default;

            public bool IsVersionInRange(VersionNumber version)
            {
                return versionFrom == versionTo ||
                       (versionFrom == default && versionTo == default) ||
                       ((versionFrom == default || version >= versionFrom) &&
                        (versionTo == default || version < versionTo));
            }

            #endregion
        }

        private class ShopInstaller
        {
            #region Installing

            public ulong Player;

            public int step, targetTemplateIndex, targetInMenuTemplateIndex;

            public ShopTemplates shopData;

            public void StartInstall(ulong userID = 0UL)
            {
                Player = userID;

                step = 1;
            }

            public void SetStep(int newStep)
            {
                step = newStep;
            }

            public void SelectTemplate(int newTemplateIndex, bool isFullScreen)
            {
                if (isFullScreen)
                    targetTemplateIndex = newTemplateIndex;
                else
                    targetInMenuTemplateIndex = newTemplateIndex;
            }

            public void Finish()
            {
                _uiData = new UIData();

                var fullscreenTemplate = GetSelectedTemplate(true);
                if (fullscreenTemplate != null)
                {
                    _uiData.FullscreenUI = fullscreenTemplate.SettingsUI;

                    _uiData.IsFullscreenUISet = true;

                    if (fullscreenTemplate.TemplateLang != null)
                        Instance.RegisterTemplateMessages(fullscreenTemplate.TemplateLang);
                }

                if (Instance.ServerPanel != null)
                {
                    var menuTemplate = GetSelectedTemplate(false);
                    if (menuTemplate != null)
                    {
                        _uiData.InMenuUI = menuTemplate.SettingsUI;

                        _uiData.IsInMenuUISet = true;

                        if (menuTemplate.TemplateLang != null)
                            Instance.RegisterTemplateMessages(menuTemplate.TemplateLang);
                    }
                }

                if (BasePlayer.TryFindByID(Player, out var player))
                    player?.ChatMessage(
                        "You have successfully completed the installation of the plugin! Now the plugin will reload (usually takes 5-10 seconds) and you will be able to use it!");
                else
                    Instance.Puts(
                        "You have successfully completed the installation of the plugin! Now the plugin will reload (usually takes 5-10 seconds) and you will be able to use it!");

                Instance.SaveTemplate();

                if (_itemsData.Shop.Count == 0)
                    Instance.FillCategories();

                Instance.LoadImages();

                Interface.Oxide.ReloadPlugin("Shop");
            }

            #endregion Installing

            #region Syncing

            private Hash<string, string> imageIds = new();

            public void LoadImages()
            {
                if (shopData.Images != null)
                    foreach (var (name, base64) in shopData.Images)
                        LoadImage(name, base64);

                foreach (var shopTemplate in shopData.FullScreenTemplates)
                    Instance.AddImage(shopTemplate.BannerURL, shopTemplate.BannerURL);

                if (Instance.ServerPanel != null)
                    foreach (var shopTemplate in shopData.InMenuTemplates)
                        Instance.AddImage(shopTemplate.BannerURL, shopTemplate.BannerURL);
            }

            public string GetImage(string name)
            {
                if (imageIds.TryGetValue(name, out var value))
                    return value;

                return string.Empty;
            }

            private void LoadImage(string name, string base64)
            {
                var bytes = Convert.FromBase64String(base64);
                if (bytes.Length == 0) return;

                imageIds[name] = FileStorage.server
                    .Store(bytes, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
            }

            public string GetColorFromDependencyStatus(string status)
            {
                return status switch
                {
                    "ok" => HexToCuiColor("#78CF69"),
                    "missing" or "wrong_version" => HexToCuiColor("#F8AB39"),
                    "todo" => HexToCuiColor("#71B8ED"),
                    _ => HexToCuiColor("#E44028")
                };
            }

            public ShopDependency[] GetSortedShopDependencies()
            {
                var dependencies = GetShopDependencies();
                Array.Sort(dependencies,
                    (a, b) => string.Compare(b.GetStatus(), a.GetStatus(), StringComparison.Ordinal));
                return dependencies;
            }

            public ShopDependency[] GetShopDependencies()
            {
#if CARBON
				return shopData.CarbonDependencies;
#else
                return shopData.Dependencies;
#endif
            }

            public ShopTemplate GetTemplate(int index, bool isFullScreen)
            {
                return (isFullScreen ? shopData.FullScreenTemplates : shopData.InMenuTemplates)[index];
            }

            public ShopTemplate GetSelectedTemplate(bool isFullScreen)
            {
                var templates = isFullScreen ? shopData.FullScreenTemplates : shopData.InMenuTemplates;
                var targetIndex = isFullScreen ? targetTemplateIndex : targetInMenuTemplateIndex;

                return targetIndex >= 0 && targetIndex < templates.Length ? templates[targetIndex] : null;
            }

            #endregion Syncing

            #region Lang

            public string GetMessage(BasePlayer player, string key)
            {
                if (shopData.InstallerLang.Count == 0)
                    throw new Exception("There are no messages!");

                var userLang = "en";
                if (player != null) userLang = Instance.lang.GetLanguage(player.UserIDString);

                if (shopData.InstallerLang.TryGetValue(userLang, out var messages))
                    if (messages.Messages.TryGetValue(key, out var msg))
                        return msg;

                if (shopData.InstallerLang.TryGetValue("en", out messages))
                    if (messages.Messages.TryGetValue(key, out var msg))
                        return msg;

                return key;
            }

            #endregion Lang
        }

        #endregion Classes

        #region Installer Init

        private void InitInstaller()
        {
            _shopInstaller = new ShopInstaller();

            LoadShopInstallerData(data =>
            {
                _shopInstaller.shopData = data;

                Puts("Shop data loaded successfully.");

                _shopInstaller.LoadImages();
            });

            cmd.AddConsoleCommand("UI_Shop_Installer", this, nameof(CmdConsoleShopInstaller));
        }

        private void LoadShopInstallerData(Action<ShopTemplates> callback = null)
        {
#if TESTING
            if (Directory.Exists(Path.Combine(Interface.Oxide.DataDirectory, Name, "Templates")))
            {
                var templateFiles = Interface.Oxide.DataFileSystem.GetFiles(Path.Combine(Name, "Templates"));
                if (templateFiles != null && templateFiles.Length > 0)
                    foreach (var filePath in templateFiles)
                        try
                        {
                            var fileName = Path.GetFileNameWithoutExtension(filePath);
                            var localFilePath = Path.Combine(Name, "Templates", fileName);
                            if (!Interface.Oxide.DataFileSystem.ExistsDatafile(localFilePath)) continue;
                            var template = Interface.Oxide.DataFileSystem.ReadObject<ShopTemplates>(localFilePath);
                            if (template != null)
                            {
                                callback?.Invoke(template);
                                return;
                            }
                        }
                        catch
                        {
                            // ignore
                        }
            }
#endif

            webrequest.Enqueue(
                "https://gitlab.com/TheMevent/PluginsStorage/raw/main/ce7bd40bd7affedef6b91e0146f3d2ef_Codefling.json",
                null, (code, response) =>
                {
                    if (code != 200)
                    {
                        PrintError($"Failed to load shop data. HTTP status code: {code}");
                        return;
                    }

                    if (string.IsNullOrEmpty(response))
                    {
                        PrintError("Failed to load shop data. Response is null or empty.");
                        return;
                    }

                    var jsonData = JObject.Parse(response)?["CipherShopData"]?.ToString();
                    if (string.IsNullOrWhiteSpace(jsonData))
                    {
                        PrintError("Failed to load shop data. Response is not in the expected format.");
                        return;
                    }

                    var shopDataResponse = EncryptDecrypt.Decrypt(jsonData, "ektYMzlVOVN0M3lwbnc3OA==");
                    if (string.IsNullOrWhiteSpace(shopDataResponse))
                    {
                        PrintError("Failed to decrypt shop data. Response is not in the expected format.");
                        return;
                    }

                    try
                    {
                        var data = JsonConvert.DeserializeObject<ShopTemplates>(shopDataResponse);
                        if (data == null)
                        {
                            PrintError(
                                "Failed to deserialize shop data. Response is not in the expected format.");
                            return;
                        }

                        callback?.Invoke(data);
                    }
                    catch (Exception ex)
                    {
                        PrintError($"Error loading shop data: {ex.Message}");
                    }
                }, this);
        }

        #endregion

        #region Installer Commands

        [ChatCommand("shop.install")]
        private void CmdChatShopInstaller(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;

            if (!IsAdmin(player))
            {
                SendReply(player, "You don't have permission!");

                var adminMSG =
                    $"Player {player.UserIDString} tried to run the installer, but he doesn't have {PERM_ADMIN} permission";

                if (IsAdmin(player))
                    _config?.Notifications?.ShowNotify(player, adminMSG, 1);

                PrintError(adminMSG);
                return;
            }

            if (_shopInstaller == null)
            {
                SendReply(player,
                    "Shop has already been installed! To run the installer again, you need to reset Shop. To reset, use the 'shop.reset' command (only console)!");
                return;
            }

            _shopInstaller.StartInstall(player.userID);

            ShowInstallerUI(player);
        }

        [ConsoleCommand("shop.install")]
        private void CmdConsoleShopInstall(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null || !arg.IsServerside)
            {
                if (IsAdmin(arg.Player())) SendReply(arg, "This command can only be run from the SERVER CONSOLE.");

                return;
            }

            if (_shopInstaller == null)
            {
                SendReply(arg,
                    "Shop has already been installed! To run the installer again, you need to reset Shop. To reset, use the 'shop.reset' command (only console)!");
                return;
            }

            SendReply(arg, DisplayTemplatesAndDependencies());

            if (arg.HasArgs())
            {
                _shopInstaller.StartInstall();

                var templateIndex = arg.GetInt(0);

                if (templateIndex < 0 || templateIndex >= _shopInstaller.shopData.FullScreenTemplates.Length)
                {
                    SendReply(arg, $"Invalid fullscreen template index: {templateIndex}");
                    return;
                }

                _shopInstaller.SelectTemplate(templateIndex, true);

                if (arg.HasArgs(2))
                {
                    var inMenuTemplateIndex = arg.GetInt(1);

                    if (inMenuTemplateIndex < 0 ||
                        inMenuTemplateIndex >= _shopInstaller.shopData.InMenuTemplates.Length)
                    {
                        SendReply(arg, $"Invalid in-menu template index: {inMenuTemplateIndex}");
                        return;
                    }

                    _shopInstaller.SelectTemplate(inMenuTemplateIndex, false);
                }

                _shopInstaller.Finish();

                SendReply(arg, "Shop installation completed successfully!");
            }
            else
            {
                SendReply(arg,
                    "Please specify the template index and in-menu template index. Example: shop.install <templateIndex> <inMenuTemplateIndex>");
            }
        }

        private string DisplayTemplatesAndDependencies()
        {
            var sb = new StringBuilder();

            sb.AppendLine("Available Fullscreen Templates:");
            for (var i = 0; i < _shopInstaller.shopData.FullScreenTemplates.Length; i++)
                sb.AppendLine($"{i}: {_shopInstaller.shopData.FullScreenTemplates[i].Title}");

            sb.AppendLine("Available In-Menu Templates:");
            for (var i = 0; i < _shopInstaller.shopData.InMenuTemplates.Length; i++)
                sb.AppendLine($"{i}: {_shopInstaller.shopData.InMenuTemplates[i].Title}");

            var dependencies = _shopInstaller.GetSortedShopDependencies();
            sb.AppendLine("Dependencies:");
            foreach (var dependency in dependencies)
                sb.AppendLine(
                    $"{dependency.PluginName} by {dependency.PluginAuthor} - Status: {dependency.GetStatus()}");

            return sb.ToString();
        }

        [ConsoleCommand("shop.manage")]
        private void CmdConsoleShopManage(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null || !arg.IsServerside)
            {
                if (IsAdmin(arg.Player())) SendReply(arg, "This command can only be run from the SERVER CONSOLE.");

                return;
            }

            switch (arg.GetString(0))
            {
                case "economy":
                {
                    switch (arg.GetString(1))
                    {
                        case "list":
                        {
                            var sb = Pool.Get<StringBuilder>();
                            try
                            {
                                sb.Append("Available Economies:");
                                sb.AppendLine($"0: {_config.Economy.GetDebugInfo()} (Enabled)");

                                foreach (var additionalEconomy in _config.AdditionalEconomics)
                                    sb.AppendLine(
                                        $"{additionalEconomy.ID}: {_config.Economy.GetDebugInfo()} ({(additionalEconomy.Enabled ? "Enabled" : "Disabled")})");

                                SendReply(arg, sb.ToString());
                            }
                            finally
                            {
                                Pool.FreeUnmanaged(ref sb);
                            }

                            break;
                        }

                        case "set":
                        {
                            var economyID = arg.GetInt(2);
                            var targetEconomyPlugin = arg.GetString(3);

                            var economy = economyID == 0
                                ? _config.Economy
                                : _config.AdditionalEconomics.Find(x => x.ID == economyID);
                            if (economy == null)
                            {
                                SendReply(arg, $"Invalid economy ID: {economyID}");
                                return;
                            }

                            if (ConfigureEconomy(economy, targetEconomyPlugin, arg.GetUInt64(3)))
                            {
                                SaveConfig();
                                LoadEconomics();
                                SendReply(arg, $"Economy {economyID} successfully updated to {targetEconomyPlugin}.");
                            }
                            else
                            {
                                SendReply(arg, $"Failed to update economy {economyID} to {targetEconomyPlugin}.");
                            }

                            break;
                        }
                    }

                    break;
                }

                case "togglesell":
                {
                    _config.EnableSelling = !_config.EnableSelling;
                    SaveConfig();
                    SendReply(arg, $"Selling is now {(_config.EnableSelling ? "enabled" : "disabled")}.");
                    break;
                }

                default:
                {
                    var sb = Pool.Get<StringBuilder>();
                    try
                    {
                        sb.AppendLine("Available commands:");
                        sb.AppendLine("shop.manage economy list – Displays a list of all available economies.");
                        sb.AppendLine("shop.manage economy set <economy_ID> <name> – Set an economy.");
                        sb.AppendLine("shop.manage togglesell – Toggle selling.");

                        SendReply(arg, sb.ToString());
                    }
                    finally
                    {
                        Pool.FreeUnmanaged(ref sb);
                    }

                    break;
                }
            }

            #region Methods

            bool ConfigureEconomy(EconomyEntry economy, string targetEconomyPlugin, ulong targetSkin = 0UL)
            {
                switch (targetEconomyPlugin)
                {
                    case "Economics":
                        ConfigureEconomyPlugin(economy, "Economics", "Deposit", "Balance", "Withdraw");
                        return true;

                    case "ServerRewards":
                        ConfigureEconomyPlugin(economy, "ServerRewards", "AddPoints", "CheckPoints", "TakePoints");
                        return true;

                    case "BankSystem":
                        ConfigureEconomyPlugin(economy, "BankSystem", "API_BankSystemDeposit", "API_BankSystemBalance",
                            "API_BankSystemWithdraw");
                        return true;

                    case "IQEconomic":
                        ConfigureEconomyPlugin(economy, "IQEconomic", "API_SET_BALANCE", "API_GET_BALANCE",
                            "API_REMOVE_BALANCE");
                        return true;

                    default:
                        return ConfigureItemEconomy(economy, targetEconomyPlugin, targetSkin);
                }
            }

            void ConfigureEconomyPlugin(EconomyEntry economy, string pluginName, string addHook, string balanceHook,
                string removeHook)
            {
                economy.Type = EconomyType.Plugin;
                economy.Plug = pluginName;
                economy.AddHook = addHook;
                economy.BalanceHook = balanceHook;
                economy.RemoveHook = removeHook;
            }

            bool ConfigureItemEconomy(EconomyEntry economy, string itemName, ulong skin = 0UL)
            {
                var economyDef = ItemManager.FindItemDefinition(itemName);
                if (economyDef != null)
                {
                    economy.Type = EconomyType.Item;
                    economy.ShortName = economyDef.shortname;
                    economy.Skin = skin;
                    return true;
                }

                return false;
            }

            #endregion
        }

        private void CmdConsoleShopInstaller(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null || !IsAdmin(player)) return;

            switch (arg.GetString(0))
            {
                case "change_step":
                {
                    var newStep = arg.GetInt(1);

                    _shopInstaller.SetStep(newStep);

                    ShowInstallerUI(player);
                    break;
                }

                case "select_template":
                {
                    var newTemplateIndex = arg.GetInt(1);
                    var isFullScreen = arg.GetBool(2);

                    _shopInstaller.SelectTemplate(newTemplateIndex, isFullScreen);

                    UpdateUI(player, (CuiElementContainer container) => { LoopTemplates(container, isFullScreen); });
                    break;
                }

                case "finish":
                {
                    _shopInstaller.Finish();
                    break;
                }

                case "template_preview":
                {
                    var templateIndex = arg.GetInt(1);
                    var isFullScreen = arg.GetBool(2);

                    var template = _shopInstaller.GetTemplate(templateIndex, isFullScreen);
                    if (template == null || string.IsNullOrWhiteSpace(template.BannerURL) ||
                        !template.BannerURL.IsURL())
                        return;

                    player.Command("client.playvideo", template.BannerURL);
                    break;
                }
            }
        }

        #endregion

        #region Installer UI

        private const int Dependency_Height = 58,
            Dependency_Margin_Y = 12,
            UI_Installer_Template_Margin_Y = 20,
            UI_Installer_Template_Margin_X = 19,
            UI_Installer_Template_Width = 350,
            UI_Installer_Template_Height = 192;

        private void ShowInstallerUI(BasePlayer player)
        {
            var container = new CuiElementContainer();

            #region Background

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image =
                {
                    Material = "assets/content/ui/namefontmaterial.mat",
                    Color = "0 0 0 1"
                },
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"}
            }, "OverlayNonScaled", Layer, Layer);

            #endregion Background

            #region Header

            container.Add(new CuiPanel
            {
                Image =
                {
                    Color = HexToCuiColor("#929292", 5),
                    Material = "assets/content/ui/namefontmaterial.mat"
                },
                RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -112", OffsetMax = "0 0"}
            }, Layer, Layer + ".Header");

            #region Title

            container.Add(new CuiElement
            {
                Parent = Layer + ".Header",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = _shopInstaller.GetMessage(player, "UIHeaderTitle"),
                        Font = "robotocondensed-bold.ttf", FontSize = 32,
                        Align = TextAnchor.LowerLeft, Color = "0.6 0.6078432 0.6117647 1"
                    },
                    new CuiRectTransformComponent
                        {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "90 46", OffsetMax = "0 0"}
                }
            });

            #endregion Title

            #region Description

            container.Add(new CuiElement
            {
                Parent = Layer + ".Header",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = _shopInstaller.GetMessage(player, "UIHeaderDescription"),
                        Font = "robotocondensed-regular.ttf", FontSize = 14,
                        Align = TextAnchor.UpperLeft, Color = "0.6 0.6078432 0.6117647 1"
                    },
                    new CuiRectTransformComponent
                        {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "90 0", OffsetMax = "0 -68"}
                }
            });

            #endregion Description

            #region Icon

            container.Add(new CuiElement
            {
                Parent = Layer + ".Header",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = _shopInstaller.GetImage("Shop_Installer_HeaderIcon")
                    },
                    new CuiRectTransformComponent
                        {AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "45 -20", OffsetMax = "80 20"}
                }
            });

            #endregion Icon

            #region Button.Close

            container.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0.8941177 0.2509804 0.1568628 1",
                    Material = "assets/content/ui/namefontmaterial.mat"
                },
                RectTransform = {AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-40 -40", OffsetMax = "0 0"}
            }, Layer + ".Header", Layer + ".Header.Button.Close");

            #region Icon

            container.Add(new CuiPanel
            {
                Image = {Color = "1 1 1 0.9", Sprite = "assets/icons/close.png"},
                RectTransform =
                    {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-11 -11", OffsetMax = "11 11"}
            }, Layer + ".Header.Button.Close");

            #endregion Icon

            container.Add(new CuiElement
            {
                Parent = Layer + ".Header.Button.Close",
                Components =
                {
                    new CuiButtonComponent
                    {
                        Close = Layer,
                        Color = "0 0 0 0"
                    },
                    new CuiRectTransformComponent()
                }
            });

            #endregion

            #endregion Header

            #region Steps

            if (ServerPanel != null)
                switch (_shopInstaller.step)
                {
                    case 1:
                        ShowWelcomeStep(player, container);
                        break;

                    case 2:
                        ShowDependenciesStep(player, container);
                        break;

                    case 3:
                        ShowSelectTemplateStep(player, container);
                        break;

                    case 4:
                        ShowSelectTemplateStep(player, container, false);
                        break;

                    case 5:
                        ShowFinishStep(player, container);
                        break;
                }
            else
                switch (_shopInstaller.step)
                {
                    case 1:
                        ShowWelcomeStep(player, container);
                        break;

                    case 2:
                        ShowDependenciesStep(player, container);
                        break;

                    case 3:
                        ShowSelectTemplateStep(player, container);
                        break;

                    case 4:
                        ShowFinishStep(player, container);
                        break;
                }

            #endregion

            CuiHelper.AddUi(player, container);
        }

        private void ShowFinishStep(BasePlayer player, CuiElementContainer container)
        {
            #region Background

            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0"},
                RectTransform = {AnchorMin = "0 0.5", AnchorMax = "1 0.5", OffsetMin = "0 -360", OffsetMax = "0 248"}
            }, Layer, Layer + ".Main", Layer + ".Main");

            #endregion

            #region Label.Welcome

            container.Add(new CuiElement
            {
                Parent = Layer + ".Main",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = _shopInstaller.GetMessage(player, "UIFinishTitle"),
                        Font = "robotocondensed-regular.ttf", FontSize = 32,
                        Align = TextAnchor.MiddleCenter, Color = "0.8862745 0.8588235 0.827451 1"
                    },
                    new CuiRectTransformComponent
                        {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-400 196", OffsetMax = "400 252"}
                }
            });

            #endregion Label.Welcome

            #region Label.Thank.For.Buy

            container.Add(new CuiElement
            {
                Parent = Layer + ".Main",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = _shopInstaller.GetMessage(player, "UIFinishDescription"),
                        Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.UpperCenter,
                        Color = "0.8862745 0.8588235 0.827451 1"
                    },
                    new CuiRectTransformComponent
                        {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-400 -114", OffsetMax = "400 151"}
                }
            });

            #endregion Label.Thank.For.Buy

            #region QR.Panel

            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0"},
                RectTransform = {AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-120 63", OffsetMax = "120 137"}
            }, Layer + ".Main", Layer + ".QR.Panel");

            #region qr code

            container.Add(new CuiElement
            {
                Parent = Layer + ".QR.Panel",
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Png = _shopInstaller.GetImage("Shop_Installer_Mevent_Discord_QR")
                    },
                    new CuiRectTransformComponent
                        {AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "0 -37", OffsetMax = "74 37"}
                }
            });

            #endregion qr code

            #region title

            container.Add(new CuiElement
            {
                Parent = Layer + ".QR.Panel",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = _shopInstaller.GetMessage(player, "UIQRMeventDiscordTitle"),
                        Font = "robotocondensed-regular.ttf", FontSize = 14,
                        Align = TextAnchor.MiddleLeft, Color = "0.8862745 0.8588235 0.827451 1"
                    },
                    new CuiRectTransformComponent
                        {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "90 40", OffsetMax = "0 -12"}
                }
            });

            #endregion title

            #region description

            container.Add(new CuiElement
            {
                Parent = Layer + ".QR.Panel",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        Text = "https://discord.gg/kWtvUaTyBh",
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 11, Align = TextAnchor.UpperLeft,
                        Color = "0.8862745 0.8588235 0.827451 0.5019608",
                        HudMenuInput = true
                    },
                    new CuiRectTransformComponent
                        {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "90 0", OffsetMax = "0 -35"}
                }
            });

            #endregion description

            #endregion QR.Panel

            #region Btn.Start.Install

            container.Add(new CuiButton
            {
                Button =
                {
                    Material = "assets/content/ui/namefontmaterial.mat",
                    Color = "0 0.372549 0.7176471 1",
                    Command = "UI_Shop_Installer finish",
                    Close = Layer
                },
                Text =
                {
                    Text = _shopInstaller.GetMessage(player, "BtnFinish"),
                    Font = "robotocondensed-regular.ttf", FontSize = 15,
                    Align = TextAnchor.MiddleCenter, Color = "0.8862745 0.8588235 0.827451 1"
                },
                RectTransform =
                    {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-120 -114", OffsetMax = "120 -54"}
            }, Layer + ".Main");

            #endregion Btn.Start.Install

            #region Line

            container.Add(new CuiPanel
            {
                Image = {Color = "0.2156863 0.2156863 0.2156863 0.5019608"},
                RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "360 -127", OffsetMax = "-360 -125"}
            }, Layer + ".Main");

            #endregion Line
        }

        private void ShowSelectTemplateStep(BasePlayer player, CuiElementContainer container, bool isFullScreen = true)
        {
            #region Background

            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0"},
                RectTransform = {AnchorMin = "0 0.5", AnchorMax = "1 0.5", OffsetMin = "0 -360", OffsetMax = "0 248"}
            }, Layer, Layer + ".Main", Layer + ".Main");

            #endregion

            #region Title

            container.Add(new CuiElement
            {
                Parent = Layer + ".Main",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = _shopInstaller.GetMessage(player,
                            isFullScreen ? "UISelectTemplateTitleFullscreen" : "UISelectTemplateTitleInMenu"),
                        Font = "robotocondensed-regular.ttf", FontSize = 32, Align = TextAnchor.MiddleCenter,
                        Color = "0.8862745 0.8588235 0.827451 1"
                    },
                    new CuiRectTransformComponent
                        {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-400 234", OffsetMax = "400 289"}
                }
            });

            #endregion Title

            #region Label.Message

            container.Add(new CuiElement
            {
                Parent = Layer + ".Main",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = _shopInstaller.GetMessage(player, "UISelectTemplateDescription"),
                        Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.UpperCenter,
                        Color = "0.8862745 0.8588235 0.827451 1"
                    },
                    new CuiRectTransformComponent
                        {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-400 147", OffsetMax = "400 234"}
                }
            });

            #endregion Label.Message

            #region Line

            container.Add(new CuiPanel
            {
                Image = {Color = HexToCuiColor("#373737", 50)},
                RectTransform =
                    {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-360 145", OffsetMax = "360 147"}
            }, Layer + ".Main");

            #endregion Line

            #region ScrollView

            var targetTemplate = isFullScreen
                ? _shopInstaller.shopData.FullScreenTemplates
                : _shopInstaller.shopData.InMenuTemplates;

            var templateLines = Mathf.CeilToInt(targetTemplate.Length / 2f);

            var totalHeight = templateLines * UI_Installer_Template_Height +
                              (templateLines - 1) * UI_Installer_Template_Margin_Y;

            totalHeight += 100;

            totalHeight = Math.Max(totalHeight, 410);

            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0"},
                RectTransform =
                    {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-360 -293", OffsetMax = "377 123"}
            }, Layer + ".Main", Layer + ".ScrollBackground");

            container.Add(new CuiElement
            {
                Parent = Layer + ".ScrollBackground",
                Name = Layer + ".ScrollView",
                DestroyUi = Layer + ".ScrollView",
                Components =
                {
                    new CuiScrollViewComponent
                    {
                        MovementType = ScrollRect.MovementType.Elastic,
                        Vertical = true,
                        Inertia = true,
                        Horizontal = false,
                        Elasticity = 0.25f,
                        DecelerationRate = 0.3f,
                        ScrollSensitivity = 24f,
                        ContentTransform = new CuiRectTransform
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "1 1",
                            OffsetMin = $"0 -{totalHeight}",
                            OffsetMax = "0 0"
                        },
                        VerticalScrollbar = new CuiScrollbar
                        {
                            Size = 3f, AutoHide = false,
                            HandleColor = HexToCuiColor("#D74933")
                        }
                    }
                }
            });

            #endregion ScrollView

            #region Templates

            LoopTemplates(container, isFullScreen);

            #endregion

            #region Hover

            container.Add(new CuiElement
            {
                Name = Layer + ".Hover",
                Parent = Layer + ".Main",
                Components =
                {
                    new CuiRawImageComponent
                        {Color = "0 0 0 1", Sprite = "assets/content/ui/UI.Background.Transparent.Linear.psd"},
                    new CuiRectTransformComponent
                        {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 105"}
                }
            });

            #endregion Hover

            #region Btn.Start.Install

            container.Add(new CuiButton
            {
                Button =
                {
                    Material = "assets/content/ui/namefontmaterial.mat",
                    Color = "0 0.372549 0.7176471 1",
                    Command = $"UI_Shop_Installer change_step {_shopInstaller.step + 1}"
                },
                Text =
                {
                    Text = _shopInstaller.GetMessage(player, "BtnContinueInstalling"),
                    Font = "robotocondensed-regular.ttf", FontSize = 15,
                    Align = TextAnchor.MiddleCenter, Color = "0.8862745 0.8588235 0.827451 1"
                },
                RectTransform = {AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "10 20", OffsetMax = "250 62"}
            }, Layer + ".Main");

            #endregion Btn.Start.Install

            #region Btn.Go.Back

            container.Add(new CuiButton
            {
                RectTransform =
                    {AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-250 20", OffsetMax = "-10 62"},
                Text =
                {
                    Text = _shopInstaller.GetMessage(player, "BtnGoBack"),
                    Font = "robotocondensed-regular.ttf", FontSize = 15,
                    Align = TextAnchor.MiddleCenter, Color = "0.8862745 0.8588235 0.827451 0.6"
                },
                Button =
                {
                    Material = "assets/content/ui/namefontmaterial.mat",
                    Color = "0.145098 0.145098 0.145098 1",
                    Command = $"UI_Shop_Installer change_step {_shopInstaller.step - 1}"
                }
            }, Layer + ".Main");

            #endregion Btn.Go.Back
        }

        private void LoopTemplates(CuiElementContainer container, bool isFullScreen)
        {
            var offsetX = 0;
            var offsetY = 0;

            var targetTemplate = isFullScreen
                ? _shopInstaller.shopData.FullScreenTemplates
                : _shopInstaller.shopData.InMenuTemplates;

            for (var i = 0; i < targetTemplate.Length; i++)
            {
                var panelTemplate = targetTemplate[i];
                var isSelected =
                    (isFullScreen ? _shopInstaller.targetTemplateIndex : _shopInstaller.targetInMenuTemplateIndex) == i;

                container.Add(new CuiElement
                {
                    Name = Layer + $".Templates.{i}",
                    DestroyUi = Layer + $".Templates.{i}",
                    Parent = Layer + ".ScrollView",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0 0 0 0"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"{offsetX} {offsetY - UI_Installer_Template_Height}",
                            OffsetMax = $"{offsetX + UI_Installer_Template_Width} {offsetY}"
                        }
                    }
                });

                #region Banner Image

                container.Add(new CuiElement
                {
                    Parent = Layer + $".Templates.{i}",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = GetImage(panelTemplate.BannerURL)
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-175 0", OffsetMax = "175 160"}
                    }
                });

                #endregion

                #region Outline

                container.Add(new CuiElement
                {
                    Parent = Layer + $".Templates.{i}",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Png = _shopInstaller.GetImage(isSelected
                                ? "Shop_Installer_BannerOutline_Selected"
                                : "Shop_Installer_BannerOutline")
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "0 0",
                            OffsetMin = "0 0", OffsetMax = "350 198"
                        }
                    }
                });

                #endregion

                #region Title

                container.Add(new CuiElement
                {
                    Parent = Layer + $".Templates.{i}",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = panelTemplate.Title, Font = "robotocondensed-regular.ttf", FontSize = 15,
                            Align = TextAnchor.MiddleLeft, Color = "0.8862745 0.8588235 0.827451 1"
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "58 160", OffsetMax = "0 0"}
                    }
                });

                #endregion

                #region Button

                container.Add(new CuiElement
                {
                    Parent = Layer + $".Templates.{i}",
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Color = "0 0 0 0",
                            Command = $"UI_Shop_Installer select_template {i} {isFullScreen}"
                        },
                        new CuiRectTransformComponent()
                    }
                });

                #endregion

                #region Show Video Button

                if (!string.IsNullOrWhiteSpace(panelTemplate.VideoURL))
                    container.Add(new CuiElement
                    {
                        Parent = Layer + $".Templates.{i}",
                        Components =
                        {
                            new CuiButtonComponent
                            {
                                Color = "0 0 0 0",
                                Command = $"UI_Shop_Installer template_preview {i} {isFullScreen}"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 1", AnchorMax = "0 1",
                                OffsetMin = "0 -43", OffsetMax = "43 0"
                            }
                        }
                    });

                #endregion Show Video Button

                #region Calculate Position

                if ((i + 1) % 2 == 0)
                {
                    offsetX = 0;
                    offsetY = offsetY - UI_Installer_Template_Height - UI_Installer_Template_Margin_Y;
                }
                else
                {
                    offsetX += UI_Installer_Template_Width + UI_Installer_Template_Margin_X;
                }

                #endregion
            }
        }

        private void ShowDependenciesStep(BasePlayer player, CuiElementContainer container)
        {
            #region Background

            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0"},
                RectTransform = {AnchorMin = "0 0.5", AnchorMax = "1 0.5", OffsetMin = "0 -360", OffsetMax = "0 248"}
            }, Layer, Layer + ".Main", Layer + ".Main");

            #endregion

            #region Label.Message

            container.Add(new CuiElement
            {
                Parent = Layer + ".Main",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = _shopInstaller.GetMessage(player, "UIDependenciesDescription"),
                        Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.UpperCenter,
                        Color = "0.8862745 0.8588235 0.827451 1"
                    },
                    new CuiRectTransformComponent
                        {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-400 171", OffsetMax = "400 244"}
                }
            });

            #endregion Label.Message

            #region Line

            container.Add(new CuiPanel
            {
                Image = {Color = "0.2156863 0.2156863 0.2156863 0.5019608"},
                RectTransform =
                    {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-360 169", OffsetMax = "360 171"}
            }, Layer + ".Main");

            #endregion Line

            #region ScrollView

            var totalHeight = _shopInstaller.GetShopDependencies().Length * Dependency_Height +
                              (_shopInstaller.GetShopDependencies().Length - 1) * Dependency_Margin_Y;

            totalHeight += 100;

            totalHeight = Math.Max(totalHeight, 410);

            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0"},
                RectTransform =
                    {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-360 -262", OffsetMax = "377 148"}
            }, Layer + ".Main", Layer + ".ScrollBackground", Layer + ".ScrollBackground");

            container.Add(new CuiElement
            {
                Parent = Layer + ".ScrollBackground",
                Name = Layer + ".ScrollView",
                DestroyUi = Layer + ".ScrollView",
                Components =
                {
                    new CuiScrollViewComponent
                    {
                        MovementType = ScrollRect.MovementType.Elastic,
                        Vertical = true,
                        Inertia = true,
                        Horizontal = false,
                        Elasticity = 0.25f,
                        DecelerationRate = 0.3f,
                        ScrollSensitivity = 24f,
                        ContentTransform = new CuiRectTransform
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "1 1",
                            OffsetMin = $"0 -{totalHeight}",
                            OffsetMax = "0 0"
                        },
                        VerticalScrollbar = new CuiScrollbar
                        {
                            Size = 5f, AutoHide = false,
                            HandleColor = HexToCuiColor("#D74933")
                        }
                    }
                }
            });

            #endregion ScrollView

            #region Dependencies

            var mainOffset = 0;
            foreach (var panelDependency in _shopInstaller.GetSortedShopDependencies())
            {
                var status = panelDependency.GetStatus();

                container.Add(new CuiPanel
                    {
                        Image =
                        {
                            Color = "0.572549 0.572549 0.572549 0.2"
                        },
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"0 {mainOffset - Dependency_Height}", OffsetMax = $"720 {mainOffset}"
                        }
                    }, Layer + ".ScrollView", Layer + $".Dependencies.{panelDependency.PluginName}",
                    Layer + $".Dependencies.{panelDependency.PluginName}");

                #region Title

                container.Add(new CuiElement
                {
                    Parent = Layer + $".Dependencies.{panelDependency.PluginName}",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = panelDependency.PluginName,
                            Font = "robotocondensed-bold.ttf", FontSize = 15,
                            Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#E2DBD3")
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "46 0", OffsetMax = "-495 0"}
                    }
                });

                #endregion

                #region Status.Icon

                var colorIcon = _shopInstaller.GetColorFromDependencyStatus(status);

                container.Add(new CuiElement
                {
                    Parent = Layer + $".Dependencies.{panelDependency.PluginName}",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Color = colorIcon,
                            Sprite = "assets/content/ui/Waypoint.Outline.TeamTop.png"
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "18 -9", OffsetMax = "36 9"}
                    }
                });

                #endregion Status.Icon

                #region Status.Title

                container.Add(new CuiElement
                {
                    Parent = Layer + $".Dependencies.{panelDependency.PluginName}",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = _shopInstaller.GetMessage(player, panelDependency.Messages[status].Title),
                            Font = "robotocondensed-bold.ttf", FontSize = 14,
                            Align = TextAnchor.LowerLeft, Color = HexToCuiColor("#E2DBD3")
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "250 26", OffsetMax = "0 0"}
                    }
                });

                #endregion Status.Title

                #region Status.Description

                container.Add(new CuiElement
                {
                    Parent = Layer + $".Dependencies.{panelDependency.PluginName}",
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Text = _shopInstaller.GetMessage(player, panelDependency.Messages[status].Description),
                            ReadOnly = true,
                            Font = "robotocondensed-regular.ttf", FontSize = 12,
                            Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#E2DBD3", 50)
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "250 0", OffsetMax = "0 -35"}
                    }
                });

                #endregion Status.Description

                #region Line

                container.Add(new CuiPanel
                    {
                        Image = {Color = "0.2156863 0.2156863 0.2156863 1"},
                        RectTransform =
                            {AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = "224 9", OffsetMax = "226 -13"}
                    }, Layer + $".Dependencies.{panelDependency.PluginName}");

                #endregion

                mainOffset = mainOffset - Dependency_Height - Dependency_Margin_Y;
            }

            #endregion

            #region Hover

            container.Add(new CuiElement
            {
                Name = Layer + ".Hover",
                Parent = Layer + ".Main",
                Components =
                {
                    new CuiRawImageComponent
                        {Color = "0 0 0 1", Sprite = "assets/content/ui/UI.Background.Transparent.Linear.psd"},
                    new CuiRectTransformComponent
                        {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 0", OffsetMax = "0 105"}
                }
            });

            #endregion Hover

            #region Btn.Start.Install

            container.Add(new CuiButton
            {
                Button =
                {
                    Material = "assets/content/ui/namefontmaterial.mat",
                    Color = "0 0.372549 0.7176471 1",
                    Command = $"UI_Shop_Installer change_step {_shopInstaller.step + 1}"
                },
                Text =
                {
                    Text = _shopInstaller.GetMessage(player, "BtnContinueInstalling"),
                    Font = "robotocondensed-regular.ttf", FontSize = 15,
                    Align = TextAnchor.MiddleCenter, Color = "0.8862745 0.8588235 0.827451 1"
                },
                RectTransform =
                    {AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "10 20", OffsetMax = "250 62"}
            }, Layer + ".Main");

            #endregion Btn.Start.Install

            #region Btn.Go.Back

            container.Add(new CuiButton
            {
                RectTransform =
                    {AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-250 20", OffsetMax = "-10 62"},
                Text =
                {
                    Text = _shopInstaller.GetMessage(player, "BtnGoBack"),
                    Font = "robotocondensed-regular.ttf", FontSize = 15,
                    Align = TextAnchor.MiddleCenter, Color = "0.8862745 0.8588235 0.827451 0.6"
                },
                Button =
                {
                    Material = "assets/content/ui/namefontmaterial.mat",
                    Color = "0.145098 0.145098 0.145098 1",
                    Command = $"UI_Shop_Installer change_step {_shopInstaller.step - 1}"
                }
            }, Layer + ".Main");

            #endregion Btn.Go.Back
        }

        private void ShowWelcomeStep(BasePlayer player, CuiElementContainer container)
        {
            #region Background

            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0"},
                RectTransform = {AnchorMin = "0 0.5", AnchorMax = "1 0.5", OffsetMin = "0 -360", OffsetMax = "0 248"}
            }, Layer, Layer + ".Main", Layer + ".Main");

            #endregion

            #region Label.Welcome

            container.Add(new CuiElement
            {
                Parent = Layer + ".Main",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = _shopInstaller.GetMessage(player, "UIWelcome"),
                        Font = "robotocondensed-regular.ttf", FontSize = 32,
                        Align = TextAnchor.MiddleCenter, Color = "0.8862745 0.8588235 0.827451 1"
                    },
                    new CuiRectTransformComponent
                        {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-400 196", OffsetMax = "400 252"}
                }
            });

            #endregion Label.Welcome

            #region Label.Thank.For.Buy

            container.Add(new CuiElement
            {
                Parent = Layer + ".Main",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = _shopInstaller.GetMessage(player, "UIThankForBuying"),
                        Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.UpperCenter,
                        Color = "0.8862745 0.8588235 0.827451 1"
                    },
                    new CuiRectTransformComponent
                        {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-400 -84", OffsetMax = "400 151"}
                }
            });

            #endregion Label.Thank.For.Buy

            #region Btn.Start.Install

            container.Add(new CuiButton
            {
                Button =
                {
                    Material = "assets/content/ui/namefontmaterial.mat",
                    Color = "0 0.372549 0.7176471 1",
                    Command = $"UI_Shop_Installer change_step {_shopInstaller.step + 1}"
                },
                Text =
                {
                    Text = _shopInstaller.GetMessage(player, "BtnStartInstall"),
                    Font = "robotocondensed-regular.ttf", FontSize = 15,
                    Align = TextAnchor.MiddleCenter, Color = "0.8862745 0.8588235 0.827451 1"
                },
                RectTransform =
                    {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-120 -96", OffsetMax = "120 -36"}
            }, Layer + ".Main");

            #endregion Btn.Start.Install

            #region Btn.Cancel

            container.Add(new CuiPanel
            {
                Image = {Color = "0 0 0 0"},
                RectTransform =
                    {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-120 -151", OffsetMax = "120 -121"}
            }, Layer + ".Main", Layer + ".Btn.Cancel");

            #region Title

            container.Add(new CuiElement
            {
                Parent = Layer + ".Btn.Cancel",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = _shopInstaller.GetMessage(player, "BtnCancelAndClose"),
                        Font = "robotocondensed-regular.ttf", FontSize = 14,
                        Align = TextAnchor.MiddleCenter, Color = "0.8862745 0.8588235 0.827451 0.5019608"
                    },
                    new CuiRectTransformComponent
                        {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "22 0", OffsetMax = "0 0"}
                }
            });

            #endregion Title

            #region Icon

            container.Add(new CuiElement
            {
                Parent = Layer + ".Btn.Cancel",
                Components =
                {
                    new CuiImageComponent
                        {Color = "0.8862745 0.8588235 0.827451 0.5019608", Sprite = "assets/icons/close.png"},
                    new CuiRectTransformComponent
                        {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-112 -7", OffsetMax = "-98 7"}
                }
            });

            #endregion Icon

            #region Button

            container.Add(new CuiElement
            {
                Parent = Layer + ".Btn.Cancel",
                Components =
                {
                    new CuiButtonComponent
                    {
                        Color = "0 0 0 0",
                        Command = "UI_Shop_Installer cancel",
                        Close = Layer
                    },
                    new CuiRectTransformComponent()
                }
            });

            #endregion Button

            #endregion Btn.Cancel

            #region Line

            container.Add(new CuiPanel
            {
                Image = {Color = "0.2156863 0.2156863 0.2156863 0.5019608"},
                RectTransform =
                    {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-280 177", OffsetMax = "280 179"}
            }, Layer + ".Main");

            #endregion Line
        }

        #endregion

        #region Installer Helpers

        private Dictionary<string, string> GetMessageFile(string plugin, string langKey = "en")
        {
            if (string.IsNullOrEmpty(plugin))
                return null;
            foreach (var invalidFileNameChar in Path.GetInvalidFileNameChars())
                langKey = langKey.Replace(invalidFileNameChar, '_');

            var path = Path.Combine(Interface.Oxide.LangDirectory,
                string.Format("{0}{1}{2}.json", langKey, Path.DirectorySeparatorChar, plugin));
            return !File.Exists(path)
                ? null
                : JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path));
        }

        private void RegisterTemplateMessages(Dictionary<string, ShopInstallerLang> templateLang)
        {
            if (templateLang == null) return;

            try
            {
                foreach (var (langKey, msgData) in templateLang)
                {
                    var existingMessages = GetMessageFile(Name, langKey);
                    if (existingMessages == null) continue;

                    foreach (var (key, value) in msgData.Messages)
                        existingMessages[key] = value;

                    File.WriteAllText(Path.Combine(Interface.Oxide.LangDirectory, langKey, Name + ".json"),
                        JsonConvert.SerializeObject(existingMessages, Formatting.Indented));
                }
            }
            catch (Exception e)
            {
                Puts($"Error registering template messages: {e.Message}");
                Debug.LogException(e);
            }
        }

        private void ReplaceMessages(ref Dictionary<string, string> existingMessages,
            Dictionary<string, string> messages)
        {
            foreach (var message in messages) existingMessages[message.Key] = message.Value;
        }

        #endregion

        #endregion Installer
    }
}

#region Extension Methods

namespace Oxide.Plugins.ShopExtensionMethods
{
    // ReSharper disable ForCanBeConvertedToForeach
    // ReSharper disable LoopCanBeConvertedToQuery
    public static class ExtensionMethods
    {
        internal static Permission perm;

        public static bool IsURL(this string uriName)
        {
            return Uri.TryCreate(uriName, UriKind.Absolute, out var uriResult) &&
                   (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        public static bool IsSameItem(this Item item, Item other)
        {
            return item.IsSameItem(other.info.shortname, other.skin);
        }

        public static bool IsSameItem(this Item item, string shortname, ulong skin)
        {
            if (item == null || item.info == null || item.info.shortname != shortname || item.isBroken) return false;

            if (skin == 0)
            {
                if (!string.IsNullOrEmpty(item.name))
                    return item.skin == skin;

                return true;
            }

            return item.skin == skin;
        }

        public static bool All<T>(this IList<T> a, Func<T, bool> b)
        {
            for (var i = 0; i < a.Count; i++)
                if (!b(a[i]))
                    return false;
            return true;
        }

        public static int Average(this IList<int> a)
        {
            if (a.Count == 0) return 0;
            var b = 0;
            for (var i = 0; i < a.Count; i++) b += a[i];
            return b / a.Count;
        }

        public static T ElementAt<T>(this IEnumerable<T> a, int b)
        {
            using var c = a.GetEnumerator();
            while (c.MoveNext())
            {
                if (b == 0) return c.Current;
                b--;
            }

            return default;
        }

        public static bool Exists<T>(this IEnumerable<T> a, Func<T, bool> b = null)
        {
            using var c = a.GetEnumerator();
            while (c.MoveNext())
                if (b == null || b(c.Current))
                    return true;

            return false;
        }

        public static float Min<T>(this IEnumerable<T> source, Func<T, float> selector)
        {
            using var e = source.GetEnumerator();
            var value = selector(e.Current);
            if (float.IsNaN(value)) return value;

            while (e.MoveNext())
            {
                var x = selector(e.Current);
                if (x < value)
                    value = x;
                else if (float.IsNaN(x)) return x;
            }

            return value;
        }

        public static float Max<TSource>(this IEnumerable<TSource> source, Func<TSource, float> selector)
        {
            using var e = source.GetEnumerator();
            if (!e.MoveNext()) return 0;

            var value = selector(e.Current);
            while (e.MoveNext())
            {
                var x = selector(e.Current);
                if (x > value) value = x;
            }

            return value;
        }

        public static T FirstOrDefault<T>(this IEnumerable<T> a, Func<T, bool> b = null)
        {
            using var c = a.GetEnumerator();
            while (c.MoveNext())
                if (b == null || b(c.Current))
                    return c.Current;

            return default;
        }

        public static int RemoveAll<T, V>(this IDictionary<T, V> a, Func<T, V, bool> b)
        {
            var c = new List<T>();
            using (var d = a.GetEnumerator())
            {
                while (d.MoveNext())
                    if (b(d.Current.Key, d.Current.Value))
                        c.Add(d.Current.Key);
            }

            c.ForEach(e => a.Remove(e));
            return c.Count;
        }

        public static IEnumerable<V> Select<T, V>(this IEnumerable<T> a, Func<T, V> b)
        {
            var c = new List<V>();
            using var d = a.GetEnumerator();
            while (d.MoveNext()) c.Add(b(d.Current));

            return c;
        }

        public static List<TResult> Select<T, TResult>(this List<T> source, Func<T, TResult> selector)
        {
            if (source == null || selector == null) return new List<TResult>();

            var r = new List<TResult>(source.Count);
            for (var i = 0; i < source.Count; i++) r.Add(selector(source[i]));

            return r;
        }

        public static string[] Skip(this string[] a, int count)
        {
            if (a.Length == 0) return Array.Empty<string>();
            var c = new string[a.Length - count];
            var n = 0;
            for (var i = 0; i < a.Length; i++)
            {
                if (i < count) continue;
                c[n] = a[i];
                n++;
            }

            return c;
        }

        public static List<T> Skip<T>(this IList<T> source, int count)
        {
            if (count < 0)
                count = 0;

            if (source == null || count > source.Count)
                return new List<T>();

            var result = new List<T>(source.Count - count);
            for (var i = count; i < source.Count; i++)
                result.Add(source[i]);
            return result;
        }

        public static Dictionary<T, V> Skip<T, V>(
            this IDictionary<T, V> source,
            int count)
        {
            var result = new Dictionary<T, V>();
            using var iterator = source.GetEnumerator();
            for (var i = 0; i < count; i++)
                if (!iterator.MoveNext())
                    break;

            while (iterator.MoveNext()) result.Add(iterator.Current.Key, iterator.Current.Value);

            return result;
        }

        public static List<T> Take<T>(this IList<T> a, int b)
        {
            var c = new List<T>();
            for (var i = 0; i < a.Count; i++)
            {
                if (c.Count == b) break;
                c.Add(a[i]);
            }

            return c;
        }

        public static Dictionary<T, V> Take<T, V>(this IDictionary<T, V> a, int b)
        {
            var c = new Dictionary<T, V>();
            foreach (var f in a)
            {
                if (c.Count == b) break;
                c.Add(f.Key, f.Value);
            }

            return c;
        }

        public static Dictionary<T, V> ToDictionary<S, T, V>(this IEnumerable<S> a, Func<S, T> b, Func<S, V> c)
        {
            var d = new Dictionary<T, V>();
            using var e = a.GetEnumerator();
            while (e.MoveNext()) d[b(e.Current)] = c(e.Current);

            return d;
        }

        public static List<T> ToList<T>(this IEnumerable<T> a)
        {
            var b = new List<T>();
            using var c = a.GetEnumerator();
            while (c.MoveNext()) b.Add(c.Current);

            return b;
        }

        public static (T, V)[] ToArray<T, V>(this IDictionary<T, V> a)
        {
            var b = new (T, V)[a.Count];
            var i = 0;
            foreach (var (key, value) in a)
                b[i++] = (key, value);

            return b;
        }

        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> a)
        {
            return new HashSet<T>(a);
        }

        public static List<T> Where<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        {
            var c = new List<T>();

            using var d = source.GetEnumerator();
            while (d.MoveNext())
                if (predicate(d.Current))
                    c.Add(d.Current);

            return c;
        }

        public static List<T> OfType<T>(this IEnumerable<BaseNetworkable> a) where T : BaseEntity
        {
            var b = new List<T>();
            using var c = a.GetEnumerator();
            while (c.MoveNext())
            {
                var entity = c.Current as T;
                if (entity != null)
                    b.Add(entity);
            }

            return b;
        }

        public static int Sum<T>(this IList<T> a, Func<T, int> b)
        {
            var c = 0;
            for (var i = 0; i < a.Count; i++)
            {
                var d = b(a[i]);
                if (!float.IsNaN(d)) c += d;
            }

            return c;
        }

        public static int Sum(this IList<int> a)
        {
            var c = 0;
            for (var i = 0; i < a.Count; i++)
            {
                var d = a[i];
                if (!float.IsNaN(d)) c += d;
            }

            return c;
        }

        public static bool HasPermission(this string userID, string b)
        {
            perm ??= Interface.Oxide.GetLibrary<Permission>();
            return !string.IsNullOrEmpty(userID) && (string.IsNullOrEmpty(b) || perm.UserHasPermission(userID, b));
        }

        public static bool HasPermission(this BasePlayer a, string b)
        {
            return a.UserIDString.HasPermission(b);
        }

        public static bool HasPermission(this ulong a, string b)
        {
            return a.ToString().HasPermission(b);
        }

        public static bool IsReallyConnected(this BasePlayer a)
        {
            return a.IsReallyValid() && a.net.connection != null;
        }

        public static bool IsKilled(this BaseNetworkable a)
        {
            return (object) a == null || a.IsDestroyed;
        }

        public static bool IsNull<T>(this T a) where T : class
        {
            return a == null;
        }

        public static bool IsNull(this BasePlayer a)
        {
            return (object) a == null;
        }

        private static bool IsReallyValid(this BaseNetworkable a)
        {
            return !((object) a == null || a.IsDestroyed || a.net == null);
        }

        public static void SafelyKill(this BaseNetworkable a)
        {
            if (a.IsKilled()) return;
            a.Kill();
        }

        public static bool CanCall(this Plugin o)
        {
            return o is {IsLoaded: true};
        }

        public static bool IsInBounds(this OBB o, Vector3 a)
        {
            return o.ClosestPoint(a) == a;
        }

        public static bool IsHuman(this BasePlayer a)
        {
            return !(a.IsNpc || !a.userID.IsSteamId());
        }

        public static BasePlayer ToPlayer(this IPlayer user)
        {
            return user.Object as BasePlayer;
        }

        public static List<TResult> SelectMany<TSource, TResult>(this List<TSource> source,
            Func<TSource, List<TResult>> selector)
        {
            if (source == null || selector == null)
                return new List<TResult>();

            var result = new List<TResult>(source.Count);
            for (var i = 0; i < source.Count; i++)
            {
                var va = selector(source[i]);
                for (var j = 0; j < va.Count; j++) result.Add(va[j]);
            }

            return result;
        }

        public static IEnumerable<TResult> SelectMany<TSource, TResult>(
            this IEnumerable<TSource> source,
            Func<TSource, IEnumerable<TResult>> selector)
        {
            using var item = source.GetEnumerator();
            while (item.MoveNext())
            {
                using var result = selector(item.Current).GetEnumerator();
                while (result.MoveNext()) yield return result.Current;
            }
        }

        public static int Sum<TSource>(this IEnumerable<TSource> source, Func<TSource, int> selector)
        {
            var sum = 0;

            using var element = source.GetEnumerator();
            while (element.MoveNext()) sum += selector(element.Current);

            return sum;
        }

        public static double Sum<TSource>(this IEnumerable<TSource> source, Func<TSource, double> selector)
        {
            var sum = 0.0;

            using var element = source.GetEnumerator();
            while (element.MoveNext()) sum += selector(element.Current);

            return sum;
        }

        public static bool Any<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            if (source == null) return false;

            using var element = source.GetEnumerator();
            while (element.MoveNext())
                if (predicate(element.Current))
                    return true;

            return false;
        }

        public static List<T> SkipAndTake<T>(this List<T> source, int skip, int take)
        {
            var index = Mathf.Min(Mathf.Max(skip, 0), source.Count);
            return source.GetRange(index, Mathf.Min(take, source.Count - index));
        }


        public static string GetFieldTitle<T>(this string field)
        {
            var fieldInfo = typeof(T).GetField(field);
            return fieldInfo == null ? field : fieldInfo.GetFieldTitle();
        }

        public static string GetFieldTitle(this FieldInfo fieldInfo)
        {
            var jsonAttribute = fieldInfo.GetCustomAttribute<JsonPropertyAttribute>();
            return jsonAttribute == null ? string.Empty : jsonAttribute.PropertyName?.ToUpper();
        }

        public static Enum Next(this Enum input, params Enum[] ignoredValues)
        {
            var values = Enum.GetValues(input.GetType());
            var ignoredSet = new HashSet<Enum>(ignoredValues);
            var j = Array.IndexOf(values, input) + 1;

            while (j < values.Length && ignoredSet.Contains((Enum) values.GetValue(j))) j++;

            return j >= values.Length ? (Enum) values.GetValue(0) : (Enum) values.GetValue(j);
        }

        public static Enum Previous(this Enum input, params Enum[] ignoredValues)
        {
            var values = Enum.GetValues(input.GetType());
            var ignoredSet = new HashSet<Enum>(ignoredValues);
            var j = Array.IndexOf(values, input) - 1;

            while (j >= 0 && ignoredSet.Contains((Enum) values.GetValue(j))) j--;

            return j < 0 ? (Enum) values.GetValue(values.Length - 1) : (Enum) values.GetValue(j);
        }

        public static Enum Next(this Enum input)
        {
            var values = Enum.GetValues(input.GetType());
            var j = Array.IndexOf(values, input) + 1;
            return values.Length == j ? (Enum) values.GetValue(0) : (Enum) values.GetValue(j);
        }

        public static Enum Previous(this Enum input)
        {
            var values = Enum.GetValues(input.GetType());
            var j = Array.IndexOf(values, input) - 1;
            return j == -1 ? (Enum) values.GetValue(values.Length - 1) : (Enum) values.GetValue(j);
        }

        public static bool MoveDown<T>(this List<T> source, int index)
        {
            if (source == null) return false;

            if (index >= 0 && index < source.Count - 1)
            {
                (source[index], source[index + 1]) = (
                    source[index + 1], source[index]); // Swap

                return true;
            }

            return false;
        }

        public static bool MoveUp<T>(this List<T> source, int index)
        {
            if (source == null) return false;

            if (index > 0 && index < source.Count)
            {
                (source[index], source[index - 1]) = (source[index - 1], source[index]); // Swap

                return true;
            }

            return false;
        }
    }

    public static class CuiJsonFactory
    {
        public static string CreateButton(
            string name = "",
            string parent = "",
            string command = "",
            string text = "",
            string color = "0 0 0 0",
            string textColor = "0 0 0 0",
            string anchorMin = "0 0",
            string anchorMax = "1 1",
            string offsetMin = "0 0",
            string offsetMax = "0 0",
            int fontSize = 14,
            string font = "robotocondensed-bold.ttf",
            TextAnchor align = TextAnchor.MiddleCenter,
            bool cursorEnabled = false,
            bool keyboardEnabled = false,
            string sprite = "",
            string material = "",
            string close = "",
            bool visible = true,
            string destroy = null,
            Image.Type? imageType = null)
        {
            var sb = Pool.Get<StringBuilder>();
            try
            {
                if (string.IsNullOrEmpty(name)) name = CuiHelper.GetGuid();

                sb.Append('{');
                sb.Append("\"name\":\"").Append(name).Append("\",");
                sb.Append("\"parent\":\"").Append(parent).Append("\",");
                sb.Append("\"components\":[{");
                sb.Append("\"type\":\"UnityEngine.UI.Button\",");
                sb.Append("\"command\":\"").Append(command).Append("\",");
                sb.Append("\"color\":\"").Append(visible ? color : "0 0 0 0").Append("\"");
                if (!string.IsNullOrEmpty(close))
                    sb.Append(",\"close\":\"").Append(close).Append("\"");
                if (!string.IsNullOrEmpty(sprite))
                    sb.Append(",\"sprite\":\"").Append(sprite).Append("\"");
                if (!string.IsNullOrEmpty(material))
                    sb.Append(",\"material\":\"").Append(material).Append("\"");
                if (imageType.HasValue)
                    sb.Append(",\"imagetype\":\"").Append(imageType.Value.ToString()).Append("\"");
                sb.Append("},{");
                sb.Append("\"type\":\"RectTransform\",");
                sb.Append("\"anchormin\":\"").Append(anchorMin).Append("\",");
                sb.Append("\"anchormax\":\"").Append(anchorMax).Append("\",");
                sb.Append("\"offsetmin\":\"").Append(offsetMin).Append("\",");
                sb.Append("\"offsetmax\":\"").Append(offsetMax).Append("\"");
                sb.Append("}");

                if (cursorEnabled)
                    sb.Append(",{\"type\":\"NeedsCursor\"}");
                if (keyboardEnabled)
                    sb.Append(",{\"type\":\"NeedsKeyboard\"}");

                sb.Append("],");

                sb.Append("\"destroyUi\":\"");
                if (!string.IsNullOrEmpty(destroy))
                    sb.Append(destroy);
                sb.Append('\"');

                sb.Append('}');

                if (!string.IsNullOrEmpty(text))
                {
                    sb.Append(",{\"parent\":\"").Append(name).Append("\",");
                    sb.Append("\"components\":[{");
                    sb.Append("\"type\":\"UnityEngine.UI.Text\",");
                    sb.Append("\"text\":\"").Append((visible ? text : string.Empty).Replace("\"", "\\\""))
                        .Append("\",");
                    sb.Append("\"align\":\"").Append(align.ToString()).Append("\",");
                    sb.Append("\"font\":\"").Append(font).Append("\",");
                    sb.Append("\"fontSize\":").Append(fontSize).Append(",");
                    sb.Append("\"color\":\"").Append(visible ? textColor : "0 0 0 0").Append("\"");
                    sb.Append("},{");
                    sb.Append("\"type\":\"RectTransform\"");
                    sb.Append("}]}");
                }

                return sb.ToString();
            }
            finally
            {
                Pool.FreeUnmanaged(ref sb);
            }
        }

        public static string CreateLabel(
            string name = "",
            string parent = "",
            string text = "",
            string textColor = "1 1 1 1",
            string anchorMin = "0 0",
            string anchorMax = "1 1",
            string offsetMin = "0 0",
            string offsetMax = "0 0",
            int fontSize = 14,
            string font = "robotocondensed-bold.ttf",
            TextAnchor align = TextAnchor.UpperLeft,
            bool visible = true,
            string destroy = null,
            VerticalWrapMode? verticalOverflow = null)
        {
            var sb = Pool.Get<StringBuilder>();
            try
            {
                if (string.IsNullOrEmpty(name)) name = CuiHelper.GetGuid();

                sb.Append('{');
                sb.Append("\"name\":\"").Append(name).Append("\",");
                sb.Append("\"parent\":\"").Append(parent).Append("\",");
                sb.Append("\"components\":[{");
                sb.Append("\"type\":\"UnityEngine.UI.Text\",");
                sb.Append("\"text\":\"").Append((visible ? text : string.Empty).Replace("\"", "\\\"")).Append("\",");
                sb.Append("\"align\":\"").Append(align.ToString()).Append("\",");
                sb.Append("\"font\":\"").Append(font).Append("\",");
                sb.Append("\"fontSize\":").Append(fontSize).Append(",");
                sb.Append("\"color\":\"").Append(visible ? textColor : "0 0 0 0").Append("\"");
                if (verticalOverflow.HasValue)
                    sb.Append(",\"verticalOverflow\":\"").Append(verticalOverflow.Value.ToString()).Append("\"");
                sb.Append("},{");
                sb.Append("\"type\":\"RectTransform\",");
                sb.Append("\"anchormin\":\"").Append(anchorMin).Append("\",");
                sb.Append("\"anchormax\":\"").Append(anchorMax).Append("\",");
                sb.Append("\"offsetmin\":\"").Append(offsetMin).Append("\",");
                sb.Append("\"offsetmax\":\"").Append(offsetMax).Append("\"");
                sb.Append("}],");

                sb.Append("\"destroyUi\":\"");
                if (!string.IsNullOrEmpty(destroy))
                    sb.Append(destroy);
                sb.Append('\"');

                sb.Append('}');
                return sb.ToString();
            }
            finally
            {
                Pool.FreeUnmanaged(ref sb);
            }
        }

        public static string CreatePanel(
            string name = "",
            string parent = "",
            string color = "0 0 0 0",
            string anchorMin = "0 0",
            string anchorMax = "1 1",
            string offsetMin = "0 0",
            string offsetMax = "0 0",
            string sprite = "",
            string material = "",
            bool cursorEnabled = false,
            bool keyboardEnabled = false,
            bool visible = true,
            string destroy = null)
        {
            var sb = Pool.Get<StringBuilder>();
            try
            {
                if (string.IsNullOrEmpty(name)) name = CuiHelper.GetGuid();

                sb.Append('{');
                sb.Append("\"name\":\"").Append(name).Append("\",");
                sb.Append("\"parent\":\"").Append(parent).Append("\",");
                sb.Append("\"components\":[{");
                sb.Append("\"type\":\"UnityEngine.UI.Image\",");
                sb.Append("\"color\":\"").Append(visible ? color : "0 0 0 0").Append("\"");
                if (!string.IsNullOrEmpty(sprite))
                    sb.Append(",\"sprite\":\"").Append(sprite).Append("\"");
                if (!string.IsNullOrEmpty(material))
                    sb.Append(",\"material\":\"").Append(material).Append("\"");
                sb.Append("},{");
                sb.Append("\"type\":\"RectTransform\",");
                sb.Append("\"anchormin\":\"").Append(anchorMin).Append("\",");
                sb.Append("\"anchormax\":\"").Append(anchorMax).Append("\",");
                sb.Append("\"offsetmin\":\"").Append(offsetMin).Append("\",");
                sb.Append("\"offsetmax\":\"").Append(offsetMax).Append("\"");
                sb.Append("}");

                if (cursorEnabled)
                    sb.Append(",{\"type\":\"NeedsCursor\"}");
                if (keyboardEnabled)
                    sb.Append(",{\"type\":\"NeedsKeyboard\"}");

                sb.Append("],");

                sb.Append("\"destroyUi\":\"");
                if (!string.IsNullOrEmpty(destroy))
                    sb.Append(destroy);
                sb.Append('\"');

                sb.Append('}');

                return sb.ToString();
            }
            finally
            {
                Pool.FreeUnmanaged(ref sb);
            }
        }

        public static string CreateInputField(
            string name = "",
            string parent = "",
            string text = "",
            string textColor = "1 1 1 1",
            string anchorMin = "0 0",
            string anchorMax = "1 1",
            string offsetMin = "0 0",
            string offsetMax = "0 0",
            int fontSize = 14,
            string font = "robotocondensed-bold.ttf",
            TextAnchor align = TextAnchor.UpperLeft,
            bool visible = true,
            string destroy = null,
            bool needsKeyboard = false,
            bool readOnly = false,
            int charsLimit = 0,
            string command = "",
            bool password = false,
            bool autofocus = false,
            bool hudMenuInput = false,
            InputField.LineType? lineType = null)
        {
            var sb = Pool.Get<StringBuilder>();
            try
            {
                if (string.IsNullOrEmpty(name)) name = CuiHelper.GetGuid();

                sb.Append('{');
                sb.Append("\"name\":\"").Append(name).Append("\",");
                sb.Append("\"parent\":\"").Append(parent).Append("\",");
                sb.Append("\"components\":[{");
                sb.Append("\"type\":\"UnityEngine.UI.InputField\",");
                sb.Append("\"text\":\"").Append((visible ? text : string.Empty).Replace("\"", "\\\"")).Append("\",");
                sb.Append("\"align\":\"").Append(align.ToString()).Append("\",");
                sb.Append("\"font\":\"").Append(font).Append("\",");
                sb.Append("\"fontSize\":").Append(fontSize).Append(",");
                sb.Append("\"color\":\"").Append(visible ? textColor : "0 0 0 0").Append("\",");
                if (needsKeyboard)
                    sb.Append("\"needsKeyboard\":true,");
                if (readOnly)
                    sb.Append("\"readOnly\":true,");
                if (charsLimit > 0)
                    sb.Append("\"charsLimit\":").Append(charsLimit).Append(",");
                if (!string.IsNullOrEmpty(command))
                    sb.Append("\"command\":\"").Append(command).Append("\",");
                if (password)
                    sb.Append("\"password\":true,");
                if (autofocus)
                    sb.Append("\"autofocus\":true,");
                if (hudMenuInput)
                    sb.Append("\"hudMenuInput\":true,");
                if (lineType.HasValue)
                    sb.Append("\"lineType\":\"").Append(lineType.Value.ToString()).Append("\",");
                sb.Append("},{");
                sb.Append("\"type\":\"RectTransform\",");
                sb.Append("\"anchormin\":\"").Append(anchorMin).Append("\",");
                sb.Append("\"anchormax\":\"").Append(anchorMax).Append("\",");
                sb.Append("\"offsetmin\":\"").Append(offsetMin).Append("\",");
                sb.Append("\"offsetmax\":\"").Append(offsetMax).Append("\"");
                sb.Append("}],");

                sb.Append("\"destroyUi\":\"");
                if (!string.IsNullOrEmpty(destroy))
                    sb.Append(destroy);
                sb.Append('\"');

                sb.Append('}');
                return sb.ToString();
            }
            finally
            {
                Pool.FreeUnmanaged(ref sb);
            }
        }

        public static string CreateImage(
            string name = "",
            string parent = "",
            string color = "1 1 1 1",
            string anchorMin = "0 0",
            string anchorMax = "1 1",
            string offsetMin = "0 0",
            string offsetMax = "0 0",
            bool raw = false,
            string sprite = "",
            string material = "",
            Image.Type? imageType = null,
            string steamId = "",
            string png = "",
            bool cursorEnabled = false,
            bool keyboardEnabled = false,
            bool visible = true,
            string destroy = null,
            int? itemId = null)
        {
            var sb = Pool.Get<StringBuilder>();
            try
            {
                if (string.IsNullOrEmpty(name)) name = CuiHelper.GetGuid();

                sb.Append('{');
                sb.Append("\"name\":\"").Append(name).Append("\",");
                sb.Append("\"parent\":\"").Append(parent).Append("\",");
                sb.Append("\"components\":[{");
                if (raw)
                {
                    sb.Append("\"type\":\"UnityEngine.UI.RawImage\"");
                    if (!string.IsNullOrEmpty(steamId))
                        sb.Append(",\"steamid\":\"").Append(steamId).Append("\"");
                    if (!string.IsNullOrEmpty(png))
                        sb.Append(",\"png\":\"").Append(png).Append("\"");
                    if (!string.IsNullOrEmpty(sprite))
                        sb.Append(",\"sprite\":\"").Append(sprite).Append("\"");
                }
                else
                {
                    sb.Append("\"type\":\"UnityEngine.UI.Image\"");
                    if (itemId.HasValue)
                        sb.Append(",\"itemid\":").Append(itemId.Value).Append("");
                    if (!string.IsNullOrEmpty(sprite))
                        sb.Append(",\"sprite\":\"").Append(sprite).Append("\"");
                }

                if (imageType.HasValue)
                    sb.Append(",\"imagetype\":\"").Append(imageType.Value.ToString()).Append("\"");

                if (!string.IsNullOrEmpty(color))
                    sb.Append(",\"color\":\"").Append(visible ? color : "0 0 0 0").Append("\"");

                if (!string.IsNullOrEmpty(material))
                    sb.Append(",\"material\":\"").Append(material).Append("\"");

                sb.Append("},{");
                sb.Append("\"type\":\"RectTransform\",");
                sb.Append("\"anchormin\":\"").Append(anchorMin).Append("\",");
                sb.Append("\"anchormax\":\"").Append(anchorMax).Append("\",");
                sb.Append("\"offsetmin\":\"").Append(offsetMin).Append("\",");
                sb.Append("\"offsetmax\":\"").Append(offsetMax).Append("\"");
                sb.Append("}");

                if (cursorEnabled)
                    sb.Append(",{\"type\":\"NeedsCursor\"}");
                if (keyboardEnabled)
                    sb.Append(",{\"type\":\"NeedsKeyboard\"}");

                sb.Append("],");

                sb.Append("\"destroyUi\":\"");
                if (!string.IsNullOrEmpty(destroy))
                    sb.Append(destroy);
                sb.Append('\"');

                sb.Append('}');

                return sb.ToString();
            }
            finally
            {
                Pool.FreeUnmanaged(ref sb);
            }
        }

        public static string CreateScrollView(
            string name = "",
            string destroy = null,
            string parent = "",
            string contentAnchorMin = "0 0",
            string contentAnchorMax = "1 1",
            string contentOffsetMin = "0 0",
            string contentOffsetMax = "0 0",
            string anchorMin = "0 0",
            string anchorMax = "1 1",
            string offsetMin = "0 0",
            string offsetMax = "0 0",
            bool horizontal = false,
            bool vertical = false,
            ScrollRect.MovementType movementType = ScrollRect.MovementType.Elastic,
            float elasticity = 0.1f,
            bool inertia = true,
            float decelerationRate = 0.135f,
            float scrollSensitivity = 1.0f,
            string horizontalScrollbar = null,
            string verticalScrollbar = null)
        {
            var sb = Pool.Get<StringBuilder>();
            try
            {
                if (string.IsNullOrEmpty(name)) name = CuiHelper.GetGuid();

                sb.Append('{');
                sb.Append("\"name\":\"").Append(name).Append("\",");
                sb.Append("\"parent\":\"").Append(parent).Append("\",");
                sb.Append("\"components\":[");

                sb.Append("{\"type\":\"UnityEngine.UI.Image\",\"color\":\"0 0 0 0\"},");
                // sb.Append("{\"type\":\"UnityEngine.UI.Image\",\"color\":\"1 1 1 1\"},");

                sb.Append("{");
                sb.Append("\"type\":\"UnityEngine.UI.ScrollView\",");

                // Content Transform
                sb.Append("\"contentTransform\":{");
                sb.Append("\"anchormin\":\"").Append(contentAnchorMin).Append("\",");
                sb.Append("\"anchormax\":\"").Append(contentAnchorMax).Append("\",");
                sb.Append("\"offsetmin\":\"").Append(contentOffsetMin).Append("\",");
                sb.Append("\"offsetmax\":\"").Append(contentOffsetMax).Append("\"");
                sb.Append("},");

                // Scroll Settings
                sb.Append("\"horizontal\":").Append(horizontal.ToString().ToLower()).Append(",");
                sb.Append("\"vertical\":").Append(vertical.ToString().ToLower()).Append(",");
                sb.Append("\"movementType\":\"").Append(movementType.ToString()).Append("\",");
                sb.Append("\"elasticity\":").Append(elasticity.ToString("F3")).Append(",");
                sb.Append("\"inertia\":").Append(inertia.ToString().ToLower()).Append(",");
                sb.Append("\"decelerationRate\":").Append(decelerationRate.ToString("F3")).Append(",");
                sb.Append("\"scrollSensitivity\":").Append(scrollSensitivity.ToString("F1"));

                // Horizontal Scrollbar
                if (!string.IsNullOrEmpty(horizontalScrollbar))
                    sb.Append(",\"horizontalScrollbar\":").Append(horizontalScrollbar);

                // Vertical Scrollbar
                if (!string.IsNullOrEmpty(verticalScrollbar))
                    sb.Append(",\"verticalScrollbar\":").Append(verticalScrollbar);

                sb.Append("},{");
                sb.Append("\"type\":\"RectTransform\",");
                sb.Append("\"anchormin\":\"").Append(anchorMin).Append("\",");
                sb.Append("\"anchormax\":\"").Append(anchorMax).Append("\",");
                sb.Append("\"offsetmin\":\"").Append(offsetMin).Append("\",");
                sb.Append("\"offsetmax\":\"").Append(offsetMax).Append("\"");
                sb.Append("}],");

                sb.Append("\"destroyUi\":\"");
                if (!string.IsNullOrEmpty(destroy))
                    sb.Append(destroy);
                sb.Append('\"');

                sb.Append('}');
                return sb.ToString();
            }
            finally
            {
                Pool.FreeUnmanaged(ref sb);
            }
        }

        public static string CreateScrollBar(
            bool invert = false,
            bool autoHide = false,
            string handleColor = "0.5 0.5 0.5 1",
            string trackColor = "0.5 0.5 0.5 1",
            string highlightColor = "0.5 0.5 0.5 1",
            string pressedColor = "0.5 0.5 0.5 1",
            float size = 20f,
            string handleSprite = "",
            string trackSprite = "")
        {
            var sb = Pool.Get<StringBuilder>();
            try
            {
                handleColor ??= "0.5 0.5 0.5 1";
                trackColor ??= "0.5 0.5 0.5 1";
                highlightColor ??= "0.5 0.5 0.5 1";
                pressedColor ??= "0.5 0.5 0.5 1";

                sb.Append('{');
                sb.Append("\"invert\":").Append(invert.ToString().ToLower()).Append(",");
                sb.Append("\"autoHide\":").Append(autoHide.ToString().ToLower()).Append(",");
                sb.Append("\"handleColor\":\"").Append(handleColor).Append("\",");
                sb.Append("\"trackColor\":\"").Append(trackColor).Append("\",");
                sb.Append("\"highlightColor\":\"").Append(highlightColor).Append("\",");
                sb.Append("\"pressedColor\":\"").Append(pressedColor).Append("\",");
                sb.Append("\"size\":").Append(size.ToString("F1"));
                if (!string.IsNullOrEmpty(handleSprite))
                    sb.Append(",\"handleSprite\":\"").Append(handleSprite).Append("\"");
                if (!string.IsNullOrEmpty(trackSprite))
                    sb.Append(",\"trackSprite\":\"").Append(trackSprite).Append("\"");
                sb.Append('}');
                return sb.ToString();
            }
            finally
            {
                Pool.FreeUnmanaged(ref sb);
            }
        }
    }
}

#endregion Extension Methods
