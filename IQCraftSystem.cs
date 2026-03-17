using Newtonsoft.Json;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using System.Linq;
using System;
using System.Text;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using System.Collections;

namespace Oxide.Plugins
{
    [Info("IQCraftSystem", "TopPlugin.ru", "0.0.9")]
    [Description("Удобная система крафта")]
    class IQCraftSystem : RustPlugin
    {
        /// <summary>
        /// Обновление 0.0.4
        /// - Исправлена подгрузка иконок для крафта
        /// Обновление 0.0.5
        /// - Исправил снятие баланса при крафте с использованием IQEconomic
        /// Обновление 1.0.0
        /// Теперь обычный игрок не сможет выдать себе предмет
        /// </summary>
        /// 

        #region Reference
        [PluginReference] Plugin ImageLibrary, IQEconomic, IQPlagueSkill, IQRankSystem;

        #region ImageLibrary
        private string GetImage(string fileName, ulong skin = 0)
        {
            var imageId = (string)plugins.Find("ImageLibrary").CallHook("GetImage", fileName, skin);
            if (!string.IsNullOrEmpty(imageId))
                return imageId;
            return string.Empty;
        }
        public bool AddImage(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname, skin);
        public void SendImage(BasePlayer player, string imageName, ulong imageId = 0) => ImageLibrary?.Call("SendImage", player, imageName, imageId);
        public bool HasImage(string imageName) => (bool)ImageLibrary?.Call("HasImage", imageName);
        #endregion

        #region IQEconomic
        int IQEconomicGetBalance(ulong userID) => (int)IQEconomic?.Call("API_GET_BALANCE", userID);
        void IQEconomicRemoveBalance(ulong userID, int Balance) => IQEconomic?.Call("API_REMOVE_BALANCE", userID, Balance);
        #endregion

        #region IQPlagueSkill
        bool IQPlagueSkillISAdvanced(BasePlayer player) => (bool)IQPlagueSkill?.Call("API_IS_ADVANCED_CRAFT", player);
        #endregion

        #region IQRankSystem
        bool IQRankSystemAvaliability(BasePlayer player, string RankKey) => (bool)IQRankSystem?.Call("API_GET_AVAILABILITY_RANK_USER", player.userID, RankKey);
        string IQRankSystemGetName(string RankKey) => (string)IQRankSystem?.Call("API_GET_RANK_NAME", RankKey);
        bool IQRankRankReality(string RankKey) => (bool)IQRankSystem?.Call("API_IS_RANK_REALITY", RankKey);
        #endregion

        #endregion

        #region Vars
        string CustomItemShortname = "electric.flasherlight";
        public Dictionary<BasePlayer, int> PagePlayers = new Dictionary<BasePlayer, int>();
        Dictionary<BasePlayer, CategoryItem> CategoryActive = new Dictionary<BasePlayer, CategoryItem>();
        public enum CategoryItem
        {
            Weapon,
            Tools,
            Construction,
            Items,
            Attirie,
            Electrical,
            Transport,
            Fun,
            Custom,
            Entity,
            All
        }
        #endregion

        #region Configuration
        private static Configuration config = new Configuration();
        private class Configuration
        {
            [JsonProperty("Настройка интерфейса плагина")]
            public InterfaceSettings InterfaceSetting = new InterfaceSettings();
            [JsonProperty("Настройка предметов, которые возможно скрафтить")]
            public Dictionary<string, ItemSettings> ItemSetting = new Dictionary<string, ItemSettings>();

            #region ItemSettings
            internal class ItemSettings
            {
                [JsonProperty("К какой категории относится данный предмет : 0 - Оружие, 1 - Инструменты, 2 - Конструкции, 3 - Итемы, 4 - Одежда, 5 - Электричество, 6 - Транспорт, 7 - Фановые, 8 - Кастомные(команды), 9 - Иные(Крафтит префабы и предметы по типу Переработчика)")]
                public CategoryItem CategoryItems;
                [JsonProperty("Отображаемое имя")]
                public string DisplayName;
                [JsonProperty("Описание (Необязательно)")]
                public string Description;
                [JsonProperty("Shortname (Подходит ко всем категориям КРОМЕ : 6 - Транспорт и 8 - Кастомные(команды) и 9 - Иные(Крафтит префабы и предметы по типу Переработчика)")]
                public string Shortname;
                [JsonProperty("SkinID (Подходит ко всем категориям (Если вы используете категорию 6 - Транспорт или - 9 - Иные , обязательно устанавливайте SkinID для иконки)")]
                public ulong SkinID;
                [JsonProperty("PNG (Подходит только к категории : 6 - Транспорт и 8 - Кастомные(команды) и 9 - Иные(Крафтит префабы и предметы по типу Переработчика))")]
                public string PNG;
                [JsonProperty("Команда (Подходит только к категории : 8 - Кастомные(команды) %STEAMID% - заменится на Steam64ID игрока)")]
                public string Command;
                [JsonProperty("Префаб для транспорта (Подходит только к категории 6 - Транспорт)")]
                public string PrefabNameTransport;
                [JsonProperty("Префаб для предметов (Подходит только к категории 9 - Иные(Крафтит префабы и предметы по типу Переработчика))")]
                public string PrefabEntity;
                [JsonProperty("Какой уровень верстака требуется для крафта нужен, если верстак не требуется, установите 0")]
                public int WorkBenchLevel;
                [JsonProperty("IQEconomic : Сколько требуется валюты для крафта данного предмета(Устновите 0 если не требуется)")]
                public int IQEconomicPrice;
                [JsonProperty("IQPlagueSkill : Требуется ли нейтральный навык в IQPlagueSkill для крафта данного предмета(true - да/false - нет)")]
                public bool IQPlagueSkillCraft;
                [JsonProperty("IQRankSystem : Укажите ранг, который требуется для крафта данного предмета(Если не нужно, оставьте поле пустым)")]
                public string IQRankSystemRank;
                [JsonProperty("Список предметов требующихся для крафта")]
                public List<ItemForCraft> ItemListForCraft = new List<ItemForCraft>();
                internal class ItemForCraft
                {
                    [JsonProperty("Shortname")]
                    public string Shortname;
                    [JsonProperty("Количество")]
                    public int Amount;
                    [JsonProperty("SkinID если требуется")]
                    public ulong SkinID;
                    [JsonProperty("PNG для кастомных предметов (не забудьте установить SkinID)")]
                    public string PNG;
                }
            }
            #endregion

            #region Interface
            internal class InterfaceSettings
            {
                [JsonProperty("Основные настройки")]
                public GeneralSettings GeneralSetting = new GeneralSettings();
                [JsonProperty("Настройка категорий")]
                public CategorySettings CategorySetting = new CategorySettings();
                [JsonProperty("Настройка требований")]
                public RequiresSettings RequiresSetting = new RequiresSettings();
                internal class GeneralSettings
                {
                    [JsonProperty("Ссылка на картинку выбранного предмета")]
                    public string PNGActive;
                    [JsonProperty("Ссылка на картинку не выбранного предмета")]
                    public string PNGInActive;
                    [JsonProperty("Ссылка на логотип в меню крафта")]
                    public string PNGLogo;
                    [JsonProperty("Ссылка на картинку закрытия")]
                    public string PNGCloseButton;
                    [JsonProperty("HEX Заднего фона главного меню")]
                    public string HexBackground;   
                    [JsonProperty("HEX Кнопки СОЗДАТЬ")]
                    public string HexButtonCreate;
                }
                internal class RequiresSettings
                {
                    [JsonProperty("HEX : Цвет если требование выполнено")]
                    public string RequiresDoTrueColor;
                    [JsonProperty("HEX : Цвет если требование не выполнено")]
                    public string RequiresDoFalseColor;
                    [JsonProperty("Символ выполненного требования")]
                    public string RequiresDoTrue;
                    [JsonProperty("HEX : Цвет если условие не требования")]
                    public string RequiresDoFalse;
                    [JsonProperty("PNG : Иконка для требования верстака")]
                    public string PNGRequiresWorckbench;
                    [JsonProperty("PNG : Иконка для требования валюты с IQEconomic")]
                    public string PNGRequiresIQEconomic;
                    [JsonProperty("PNG : Иконка для требования ранга с IQRankSystem")]
                    public string PNGRequiresIQRankSystem;
                    [JsonProperty("PNG : Иконка для требования навыка с IQPlagueSkill")]
                    public string PNGRequiresIQPlagueSkill;
                }
                internal class CategorySettings
                {
                    [JsonProperty("Настройка цветов не выбранных иконок под каждую категорию")]
                    public ColorCategoryIcon ColorCategoryIcons = new ColorCategoryIcon();
                    [JsonProperty("Настройка PNG иконок категорий")]
                    public PNGCategoryIcon PNGCategoryIcons = new PNGCategoryIcon();
                    internal class PNGCategoryIcon
                    {
                        [JsonProperty("PNG : Иконка категории всех предметов")]
                        public string PNGAll;
                        [JsonProperty("PNG : Иконка категории оружия")]
                        public string PNGWeapon;
                        [JsonProperty("PNG : Иконка категории инструментов")]
                        public string PNGTools;
                        [JsonProperty("PNG : Иконка категории конструкций")]
                        public string PNGConstruction;
                        [JsonProperty("PNG : Иконка категории предметов")]
                        public string PNGItems;
                        [JsonProperty("PNG : Иконка категории одежды")]
                        public string PNGAttirie;
                        [JsonProperty("PNG : Иконка категории электрики")]
                        public string PNGElectrical;
                        [JsonProperty("PNG : Иконка категории транспорта")]
                        public string PNGTransport;
                        [JsonProperty("PNG : Иконка категории фана")]
                        public string PNGFun;
                        [JsonProperty("PNG : Иконка кастомных предметов")]
                        public string PNGCustom;
                    }
                    internal class ColorCategoryIcon
                    {
                        [JsonProperty("HEX : Цвет панели с предметом категории всех предметов")]
                        public string AllItemsColor;
                        [JsonProperty("HEX : Цвет панели с предметом категории оружий")]
                        public string WeaponColor;
                        [JsonProperty("HEX : Цвет панели с предметом категории интсрументов")]
                        public string ToolsColor;
                        [JsonProperty("HEX : Цвет панели с предметом категории конструкций")]
                        public string ConstructionColor;
                        [JsonProperty("HEX : Цвет панели с предметом категории предметов")]
                        public string ItemsColor;
                        [JsonProperty("HEX : Цвет панели с предметом категории одежды")]
                        public string AttirieColor;
                        [JsonProperty("HEX : Цвет панели с предметом категории электрики")]
                        public string ElectricalColor;
                        [JsonProperty("HEX : Цвет панели с предметом категории транспорта")]
                        public string TransportColor;
                        [JsonProperty("HEX : Цвет панели с предметом категории фана")]
                        public string FunColor;
                        [JsonProperty("HEX : Цвет панели с предметом категории кастомных предметов")]
                        public string CustomColor;
                    }
                }
            }
            #endregion

            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    #region Interface
                    InterfaceSetting = new InterfaceSettings
                    {
                        GeneralSetting = new InterfaceSettings.GeneralSettings
                        {
                            PNGActive = "https://i.imgur.com/46KFe3X.png",
                            PNGInActive = "https://i.imgur.com/31iuKXn.png",
                            PNGLogo = "https://i.imgur.com/K29MfYR.png",
                            PNGCloseButton = "https://i.imgur.com/mirRxLy.png",
                            HexBackground = "#0C0C0C95",
                            HexButtonCreate = "#C35F2FFF",
                        },
                        RequiresSetting = new InterfaceSettings.RequiresSettings
                        {
                            RequiresDoFalse = "×",
                            RequiresDoTrue = "✓",
                            RequiresDoFalseColor = "#FF3366",
                            RequiresDoTrueColor = "#CCFFCC",
                            PNGRequiresIQEconomic = "https://i.imgur.com/q0UjhJJ.png",
                            PNGRequiresIQPlagueSkill = "https://i.imgur.com/f3isgi4.png",
                            PNGRequiresIQRankSystem = "https://i.imgur.com/0ZFPmrZ.png",
                            PNGRequiresWorckbench = "https://i.imgur.com/bxG5ofY.png",
                        },
                        CategorySetting = new InterfaceSettings.CategorySettings
                        {
                            PNGCategoryIcons = new InterfaceSettings.CategorySettings.PNGCategoryIcon
                            {
                                PNGAll = "https://i.imgur.com/V1dUiuy.png",
                                PNGWeapon = "https://i.imgur.com/XwDBNoW.png",
                                PNGAttirie = "https://i.imgur.com/F25qevg.png",
                                PNGConstruction = "https://i.imgur.com/WPG9YMb.png",
                                PNGCustom = "https://i.imgur.com/jyobxIs.png",
                                PNGElectrical = "https://i.imgur.com/7bYVThO.png",
                                PNGFun = "https://i.imgur.com/iWQLRa5.png",
                                PNGItems = "https://i.imgur.com/Rucen7r.png",
                                PNGTools = "https://i.imgur.com/XJzPl6z.png",
                                PNGTransport = "https://i.imgur.com/M9BsGiI.png",
                            },
                            ColorCategoryIcons = new InterfaceSettings.CategorySettings.ColorCategoryIcon
                            {
                                AllItemsColor = "#DECADEFF",
                                AttirieColor = "#59A27AFF",
                                ConstructionColor = "#B4D455FF",
                                CustomColor = "#9077E2FF",
                                ElectricalColor = "#00DDDDFF",
                                FunColor = "#FFAABBFF",
                                ItemsColor = "#FF2288FF",
                                ToolsColor = "#EE3B00FF",
                                TransportColor = "#D395FFFF",
                                WeaponColor = "#7F4EFFFF",
                            },
                        },
                    },
                    #endregion

                    #region ItemSetting
                    ItemSetting = new Dictionary<string, ItemSettings>
                    {
                        ["ak47"] = new ItemSettings
                        {
                            CategoryItems = CategoryItem.Weapon,
                            Command = "",
                            Description = "Хорошее оружие для стрельбы на дальние и средние дистанции",
                            DisplayName = "АК-47",
                            PNG = "",
                            PrefabEntity = "",
                            PrefabNameTransport = "",
                            Shortname = "rifle.ak",
                            SkinID = 0,
                            WorkBenchLevel = 0,
                            IQEconomicPrice = 0,
                            IQRankSystemRank = "",
                            IQPlagueSkillCraft = false,
                            ItemListForCraft = new List<ItemSettings.ItemForCraft>
                            {
                                new ItemSettings.ItemForCraft
                                {
                                    Shortname = "wood",
                                    Amount = 500,
                                    PNG = "",
                                    SkinID = 0
                                },
                                new ItemSettings.ItemForCraft
                                {
                                    Shortname = "scrap",
                                    Amount = 15,
                                    PNG = "",
                                    SkinID = 0
                                },
                                new ItemSettings.ItemForCraft
                                {
                                    Shortname = "explosives",
                                    Amount = 1,
                                    PNG = "",
                                    SkinID = 0
                                },
                                new ItemSettings.ItemForCraft
                                {
                                    Shortname = "metal.refined",
                                    Amount = 100,
                                    PNG = "",
                                    SkinID = 0
                                },
                            }
                        },
                        ["jackhammer"] = new ItemSettings
                        {
                            CategoryItems = CategoryItem.Tools,
                            Command = "",
                            Description = "",
                            DisplayName = "Jackhammer",
                            PNG = "",
                            PrefabEntity = "",
                            PrefabNameTransport = "",
                            Shortname = "jackhammer",
                            SkinID = 0,
                            WorkBenchLevel = 1,
                            IQEconomicPrice = 0,
                            IQRankSystemRank = "",
                            IQPlagueSkillCraft = false,
                            ItemListForCraft = new List<ItemSettings.ItemForCraft>
                            {
                                new ItemSettings.ItemForCraft
                                {
                                    Shortname = "wood",
                                    Amount = 500,
                                    PNG = "",
                                    SkinID = 0
                                },
                                new ItemSettings.ItemForCraft
                                {
                                    Shortname = "scrap",
                                    Amount = 15,
                                    PNG = "",
                                    SkinID = 0
                                },
                                new ItemSettings.ItemForCraft
                                {
                                    Shortname = "explosives",
                                    Amount = 1,
                                    PNG = "",
                                    SkinID = 0
                                },
                                new ItemSettings.ItemForCraft
                                {
                                    Shortname = "metal.refined",
                                    Amount = 100,
                                    PNG = "",
                                    SkinID = 0
                                },
                            }
                        },
                        ["hazmatsuit"] = new ItemSettings
                        {
                            CategoryItems = CategoryItem.Attirie,
                            Command = "",
                            Description = "",
                            DisplayName = "Хазмат",
                            PNG = "",
                            PrefabEntity = "",
                            PrefabNameTransport = "",
                            Shortname = "hazmatsuit",
                            SkinID = 0,
                            WorkBenchLevel = 3,
                            IQEconomicPrice = 0,
                            IQRankSystemRank = "",
                            IQPlagueSkillCraft = false,
                            ItemListForCraft = new List<ItemSettings.ItemForCraft>
                            {
                                new ItemSettings.ItemForCraft
                                {
                                    Shortname = "metal.refined",
                                    Amount = 100,
                                    PNG = "",
                                    SkinID = 0
                                },
                            }
                        },
                        ["floor.ladder.hatch"] = new ItemSettings
                        {
                            CategoryItems = CategoryItem.Construction,
                            Command = "",
                            Description = "Удобно спргынуть и не вернуться",
                            DisplayName = "Люк",
                            PNG = "",
                            PrefabEntity = "",
                            PrefabNameTransport = "",
                            Shortname = "floor.ladder.hatch",
                            SkinID = 0,
                            WorkBenchLevel = 1,
                            IQEconomicPrice = 0,
                            IQRankSystemRank = "",
                            IQPlagueSkillCraft = false,
                            ItemListForCraft = new List<ItemSettings.ItemForCraft>
                            {
                                new ItemSettings.ItemForCraft
                                {
                                    Shortname = "metal.refined",
                                    Amount = 100,
                                    PNG = "",
                                    SkinID = 0
                                },
                            }
                        },
                        ["privilegy3d"] = new ItemSettings
                        {
                            CategoryItems = CategoryItem.Custom,
                            Command = "say GivePrivilegy",
                            Description = "Целая привилегия на 3 целых дня, самый лучший вариант",
                            DisplayName = "ПРИВИЛЕГИЯ 3 ДНЯ",
                            PNG = "https://i.imgur.com/vLCj3kO.png",
                            PrefabEntity = "",
                            PrefabNameTransport = "",
                            Shortname = "",
                            SkinID = 0,
                            WorkBenchLevel = 1,
                            IQEconomicPrice = 0,
                            IQRankSystemRank = "",
                            IQPlagueSkillCraft = false,
                            ItemListForCraft = new List<ItemSettings.ItemForCraft>
                            {
                                new ItemSettings.ItemForCraft
                                {
                                    Shortname = "metal.refined",
                                    Amount = 100,
                                    PNG = "",
                                    SkinID = 0
                                },
                                new ItemSettings.ItemForCraft
                                {
                                    Shortname = "metal.refined",
                                    Amount = 100,
                                    PNG = "",
                                    SkinID = 0
                                },
                                new ItemSettings.ItemForCraft
                                {
                                    Shortname = "metal.refined",
                                    Amount = 100,
                                    PNG = "",
                                    SkinID = 0
                                },
                                new ItemSettings.ItemForCraft
                                {
                                    Shortname = "metal.refined",
                                    Amount = 100,
                                    PNG = "",
                                    SkinID = 0
                                },
                            }
                        },
                        ["turret"] = new ItemSettings
                        {
                            CategoryItems = CategoryItem.Electrical,
                            Command = "",
                            Description = "",
                            DisplayName = "Турель",
                            PNG = "",
                            PrefabEntity = "",
                            PrefabNameTransport = "",
                            Shortname = "autoturret",
                            SkinID = 0,
                            WorkBenchLevel = 1,
                            IQEconomicPrice = 0,
                            IQRankSystemRank = "",
                            IQPlagueSkillCraft = false,
                            ItemListForCraft = new List<ItemSettings.ItemForCraft>
                            {
                                new ItemSettings.ItemForCraft
                                {
                                    Shortname = "metal.refined",
                                    Amount = 100,
                                    PNG = "",
                                    SkinID = 0
                                },
                            }
                        },
                        ["tree"] = new ItemSettings
                        {
                            CategoryItems = CategoryItem.Entity,
                            Command = "",
                            Description = "Возьми и всади дерево",
                            DisplayName = "Дерево",
                            PNG = "https://i.imgur.com/XnuUmyZ.png",
                            PrefabEntity = "assets/bundled/prefabs/autospawn/resource/v2_temp_forest/pine_c.prefab",
                            PrefabNameTransport = "",
                            Shortname = "",
                            SkinID = 1337,
                            WorkBenchLevel = 1,
                            IQEconomicPrice = 0,
                            IQRankSystemRank = "",
                            IQPlagueSkillCraft = false,
                            ItemListForCraft = new List<ItemSettings.ItemForCraft>
                            {
                                new ItemSettings.ItemForCraft
                                {
                                    Shortname = "wood",
                                    Amount = 100,
                                    PNG = "",
                                    SkinID = 0
                                },
                            }
                        },
                        ["firewerk"] = new ItemSettings
                        {
                            CategoryItems = CategoryItem.Fun,
                            Command = "",
                            Description = "Взорвик небо",
                            DisplayName = "Фейверк",
                            PNG = "",
                            PrefabEntity = "",
                            PrefabNameTransport = "",
                            Shortname = "firework.romancandle.red",
                            SkinID = 0,
                            WorkBenchLevel = 1,
                            IQEconomicPrice = 0,
                            IQRankSystemRank = "",
                            IQPlagueSkillCraft = false,
                            ItemListForCraft = new List<ItemSettings.ItemForCraft>
                            {
                                new ItemSettings.ItemForCraft
                                {
                                    Shortname = "wood",
                                    Amount = 100,
                                    PNG = "",
                                    SkinID = 0
                                },
                            }
                        },
                        ["swetilnik"] = new ItemSettings
                        {
                            CategoryItems = CategoryItem.Items,
                            Command = "",
                            Description = "",
                            DisplayName = "Светилка на стенку",
                            PNG = "",
                            PrefabEntity = "",
                            PrefabNameTransport = "",
                            Shortname = "xmas.lightstring",
                            SkinID = 0,
                            WorkBenchLevel = 1,
                            IQEconomicPrice = 0,
                            IQRankSystemRank = "",
                            IQPlagueSkillCraft = false,
                            ItemListForCraft = new List<ItemSettings.ItemForCraft>
                            {
                                new ItemSettings.ItemForCraft
                                {
                                    Shortname = "wood",
                                    Amount = 100,
                                    PNG = "",
                                    SkinID = 0
                                },
                            }
                        },
                        ["copter"] = new ItemSettings
                        {
                            CategoryItems = CategoryItem.Transport,
                            Command = "",
                            Description = "",
                            DisplayName = "Коптер",
                            PNG = "https://i.imgur.com/kC8tfXF.png",
                            PrefabEntity = "",
                            PrefabNameTransport = "assets/content/vehicles/minicopter/minicopter.entity.prefab",
                            Shortname = "",
                            SkinID = 3333,
                            WorkBenchLevel = 1,
                            IQEconomicPrice = 0,
                            IQRankSystemRank = "",
                            IQPlagueSkillCraft = false,
                            ItemListForCraft = new List<ItemSettings.ItemForCraft>
                            {
                                new ItemSettings.ItemForCraft
                                {
                                    Shortname = "wood",
                                    Amount = 100,
                                    PNG = "",
                                    SkinID = 0
                                },
                            }
                        },
                    },
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
                PrintWarning($"Ошибка чтения #57 конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Metods

        #region Loaded Icons
        void LoadedIcon()
        {
            PrintWarning("Начинаю загрузку иконок..");
            var Interface = config.InterfaceSetting;
            var Category = Interface.CategorySetting.PNGCategoryIcons;
            var Items = config.ItemSetting;
            var Requires = Interface.RequiresSetting;

            if (!HasImage($"LOGO_{Interface.GeneralSetting.PNGLogo}"))
                AddImage(Interface.GeneralSetting.PNGLogo, $"LOGO_{Interface.GeneralSetting.PNGLogo}");
            if (!HasImage($"ACTIVE_ITEM_{Interface.GeneralSetting.PNGActive}"))
                AddImage(Interface.GeneralSetting.PNGActive, $"ACTIVE_ITEM_{Interface.GeneralSetting.PNGActive}");
            if (!HasImage($"IN_ACTIVE_ITEM_{Interface.GeneralSetting.PNGInActive}"))
                AddImage(Interface.GeneralSetting.PNGInActive, $"IN_ACTIVE_ITEM_{Interface.GeneralSetting.PNGInActive}"); 
            if (!HasImage($"CLOSE_BUTTON_{Interface.GeneralSetting.PNGCloseButton}"))
                AddImage(Interface.GeneralSetting.PNGCloseButton, $"CLOSE_BUTTON_{Interface.GeneralSetting.PNGCloseButton}");

            if (!HasImage($"CATEGORY_{Category.PNGAll}"))
                AddImage(Category.PNGAll, $"CATEGORY_{Category.PNGAll}");
            if (!HasImage($"CATEGORY_{Category.PNGAttirie}"))
                AddImage(Category.PNGAttirie, $"CATEGORY_{Category.PNGAttirie}");
            if (!HasImage($"CATEGORY_{Category.PNGConstruction}"))
                AddImage(Category.PNGConstruction, $"CATEGORY_{Category.PNGConstruction}");
            if (!HasImage($"CATEGORY_{Category.PNGCustom}"))
                AddImage(Category.PNGCustom, $"CATEGORY_{Category.PNGCustom}");
            if (!HasImage($"CATEGORY_{Category.PNGElectrical}"))
                AddImage(Category.PNGElectrical, $"CATEGORY_{Category.PNGElectrical}");
            if (!HasImage($"CATEGORY_{Category.PNGFun}"))
                AddImage(Category.PNGFun, $"CATEGORY_{Category.PNGFun}");
            if (!HasImage($"CATEGORY_{Category.PNGItems}"))
                AddImage(Category.PNGItems, $"CATEGORY_{Category.PNGItems}");
            if (!HasImage($"CATEGORY_{Category.PNGTools}"))
                AddImage(Category.PNGTools, $"CATEGORY_{Category.PNGTools}");
            if (!HasImage($"CATEGORY_{Category.PNGTransport}"))
                AddImage(Category.PNGTransport, $"CATEGORY_{Category.PNGTransport}");
            if (!HasImage($"CATEGORY_{Category.PNGWeapon}"))
                AddImage(Category.PNGWeapon, $"CATEGORY_{Category.PNGWeapon}");

            if (!HasImage($"REQUIRES_WORKBENCH_{Requires.PNGRequiresWorckbench}"))
                AddImage(Requires.PNGRequiresWorckbench, $"REQUIRES_WORKBENCH_{Requires.PNGRequiresWorckbench}");
            if (!HasImage($"REQUIRES_IQRANKSYSTEM_{Requires.PNGRequiresIQRankSystem}"))
                AddImage(Requires.PNGRequiresIQRankSystem, $"REQUIRES_IQRANKSYSTEM_{Requires.PNGRequiresIQRankSystem}");
            if (!HasImage($"REQUIRES_IQPLAGUESKILL_{Requires.PNGRequiresIQPlagueSkill}"))
                AddImage(Requires.PNGRequiresIQPlagueSkill, $"REQUIRES_IQPLAGUESKILL_{Requires.PNGRequiresIQPlagueSkill}");
            if (!HasImage($"REQUIRES_IQECONOMIC_{Requires.PNGRequiresIQEconomic}"))
                AddImage(Requires.PNGRequiresIQEconomic, $"REQUIRES_IQECONOMIC_{Requires.PNGRequiresIQEconomic}");

            foreach (var Icon in Items.Where(x => x.Value.CategoryItems == CategoryItem.Custom || x.Value.CategoryItems == CategoryItem.Transport || x.Value.CategoryItems == CategoryItem.Entity))
                if (!HasImage($"ITEM_{Icon.Value.PNG}"))
                    AddImage(Icon.Value.PNG, $"ITEM_{Icon.Value.PNG}");

            foreach(var Item in Items)
                foreach(var ItemCraft in Item.Value.ItemListForCraft)
                    if (!HasImage($"ITEM_CRAFT_{ItemCraft.PNG}"))
                        AddImage(ItemCraft.PNG, $"ITEM_CRAFT_{ItemCraft.PNG}");

            ServerMgr.Instance.StartCoroutine(DownloadImages());

            PrintWarning("Загрузка иконок завершена");
        }
        private IEnumerator DownloadImages()
        {
            var Items = config.ItemSetting;

            PrintError("AddImages SkyPlugins.ru...");
            foreach (var Item in Items)
            {
                if (Item.Value.SkinID != 0)
                {
                    if (!HasImage($"{Item.Value.Shortname}_128px_{Item.Value.SkinID}"))
                        AddImage($"http://rust.skyplugins.ru/getskin/{Item.Value.SkinID}/", $"{Item.Value.Shortname}_128px_{Item.Value.SkinID}", Item.Value.SkinID);
                    if (!HasImage($"{Item.Value.Shortname}_256px_{Item.Value.SkinID}"))
                        AddImage($"http://rust.skyplugins.ru/getskin/{Item.Value.SkinID}/", $"{Item.Value.Shortname}_256px_{Item.Value.SkinID}", Item.Value.SkinID);
                }
                else
                {
                    if (!String.IsNullOrWhiteSpace(Item.Value.Shortname))
                    {
                        if (!HasImage($"{Item.Value.Shortname}_128px"))
                            AddImage($"http://rust.skyplugins.ru/getimage/{Item.Value.Shortname}/128", $"{Item.Value.Shortname}_128px");
                        if (!HasImage($"{Item.Value.Shortname}_256px"))
                            AddImage($"http://rust.skyplugins.ru/getimage/{Item.Value.Shortname}/256", $"{Item.Value.Shortname}_256px");
                    }
                }
                foreach (var ItemCraft in Item.Value.ItemListForCraft.Where(x => !String.IsNullOrEmpty(x.Shortname)))
                {
                    if (ItemCraft.SkinID != 0)
                    {
                        if (!HasImage($"{ItemCraft.Shortname}_128px_{ItemCraft.SkinID}"))
                            AddImage($"http://rust.skyplugins.ru/getskin/{ItemCraft.SkinID}/", $"{ItemCraft.Shortname}_128px_{ItemCraft.SkinID}", ItemCraft.SkinID);
                    }
                    else
                    {
                        if (!HasImage($"{ItemCraft.Shortname}_128px"))
                            AddImage($"http://rust.skyplugins.ru/getimage/{ItemCraft.Shortname}/128", $"{ItemCraft.Shortname}_128px");
                    }
                }
            }
            yield return new WaitForSeconds(0.04f);
            PrintError("AddImages SkyPlugins.ru - completed..");
        }
        void CachingImage(BasePlayer player)
        {
            var Interface = config.InterfaceSetting;
            var Category = Interface.CategorySetting.PNGCategoryIcons;
            var Items = config.ItemSetting;
            var Requires = Interface.RequiresSetting;

            SendImage(player, $"REQUIRES_IQECONOMIC_{Requires.PNGRequiresIQEconomic}");
            SendImage(player, $"REQUIRES_IQPLAGUESKILL_{Requires.PNGRequiresIQPlagueSkill}");
            SendImage(player, $"REQUIRES_IQRANKSYSTEM_{Requires.PNGRequiresIQRankSystem}");
            SendImage(player, $"REQUIRES_WORKBENCH_{Requires.PNGRequiresWorckbench}");
            SendImage(player, $"LOGO_{Interface.GeneralSetting.PNGLogo}");
            SendImage(player, $"CLOSE_BUTTON_{Interface.GeneralSetting.PNGCloseButton}");
            SendImage(player, $"ACTIVE_ITEM_{Interface.GeneralSetting.PNGActive}");
            SendImage(player, $"IN_ACTIVE_ITEM_{Interface.GeneralSetting.PNGInActive}");
            SendImage(player, $"CATEGORY_{Category.PNGAll}");
            SendImage(player, $"CATEGORY_{Category.PNGAttirie}");
            SendImage(player, $"CATEGORY_{Category.PNGConstruction}");
            SendImage(player, $"CATEGORY_{Category.PNGCustom}");
            SendImage(player, $"CATEGORY_{Category.PNGElectrical}");
            SendImage(player, $"CATEGORY_{Category.PNGFun}");
            SendImage(player, $"CATEGORY_{Category.PNGItems}");
            SendImage(player, $"CATEGORY_{Category.PNGTools}");
            SendImage(player, $"CATEGORY_{Category.PNGTransport}");
            SendImage(player, $"CATEGORY_{Category.PNGWeapon}");

            foreach (var Icon in Items.Where(x => x.Value.CategoryItems == CategoryItem.Custom || x.Value.CategoryItems == CategoryItem.Transport || x.Value.CategoryItems == CategoryItem.Entity))
                SendImage(player, $"ITEM_{Icon.Value.PNG}");

            foreach (var Item in Items)
                foreach (var ItemCraft in Item.Value.ItemListForCraft)
                        SendImage(player, $"ITEM_CRAFT_{ItemCraft.PNG}");
        }
        #endregion

        #region Check Metods
        public bool DoWorkbenchLevel(BasePlayer player, int NeededWorkbenchLevel) => (bool)(player.currentCraftLevel >= NeededWorkbenchLevel);
        public bool DoBalanceIQEconomic(BasePlayer player, int NeededBalance) => (bool)(IQEconomicGetBalance(player.userID) >= NeededBalance);
        public bool DoSkillIQPlagueSkill(BasePlayer player) => (bool)IQPlagueSkillISAdvanced(player);
        public bool DoRankIQRankSystem(BasePlayer player, string RankKey) => (bool)IQRankSystemAvaliability(player, RankKey);

        public bool IS_Item_Player(BasePlayer player,string Shortname, int Amount, ulong SkinID = 0)
        {
            int ItemAmount = 0;
            foreach (var ItemRequires in player.inventory.AllItems())
            {
                if (ItemRequires == null) continue;
                if (ItemRequires.info.shortname != Shortname) continue;
                if (ItemRequires.skin != SkinID) continue;
                ItemAmount += ItemRequires.amount;
            }
            return ItemAmount >= Amount;
        }
        bool Is_Full_Item_Pack(BasePlayer player, List<Configuration.ItemSettings.ItemForCraft> itemForCraftList)
        {
            int TrueItem = 0;
            for(int i = 0; i < itemForCraftList.Count; i++)
            {
                var Item = itemForCraftList[i];
                if (IS_Item_Player(player, Item.Shortname, Item.Amount, Item.SkinID))
                    TrueItem++;
            }

            return TrueItem >= itemForCraftList.Count;
        }
        #endregion

        void GiveItemUser(ulong userID, string ItemKey)
        {
            var Item = config.ItemSetting[ItemKey];
            BasePlayer player = BasePlayer.FindByID(userID);

            switch (Item.CategoryItems)
            {
                case CategoryItem.Weapon:
                case CategoryItem.Attirie:
                case CategoryItem.Construction:
                case CategoryItem.Electrical:
                case CategoryItem.Fun:
                case CategoryItem.Items:
                case CategoryItem.Tools:// ITEM
                    {
                        Item item = ItemManager.CreateByName(Item.Shortname, 1, Item.SkinID);
                        if (!String.IsNullOrWhiteSpace(Item.DisplayName))
                            item.name = Item.DisplayName;

                        player.GiveItem(item);
                        break;
                    }
                case CategoryItem.Custom: // COMMAND
                    {
                        rust.RunServerCommand(Item.Command.Replace("%STEAMID%", player.UserIDString));
                        break;
                    }
                case CategoryItem.Transport: // ENTITY
                case CategoryItem.Entity:
                    {
                        Item item = ItemManager.CreateByName(CustomItemShortname, 1, Item.SkinID);
                        if (!String.IsNullOrWhiteSpace(Item.DisplayName))
                            item.name = Item.DisplayName;

                        player.GiveItem(item);
                        break;
                    }
            }
        }

        #region Craft Metods
        void SpawnItem(BaseEntity entity)
        {
            if (entity == null) return;
            var ItemSpawn = config.ItemSetting.FirstOrDefault(x => x.Value.SkinID == entity.skinID).Value;
            if (ItemSpawn == null) return;
            if (ItemSpawn.CategoryItems == CategoryItem.Entity || ItemSpawn.CategoryItems == CategoryItem.Transport)
            {
                string Prefab = ItemSpawn.CategoryItems == CategoryItem.Transport ? ItemSpawn.PrefabNameTransport : ItemSpawn.PrefabEntity;
                BaseEntity SpawnedEntity = (BaseEntity)GameManager.server.CreateEntity(Prefab, entity.transform.position, entity.transform.rotation);
                if (SpawnedEntity == null) return;
                SpawnedEntity.Spawn();
                NextTick(() => entity.Kill());
            }
        }

        void CraftingItem(BasePlayer player, string ItemKey)
        {
            var Item = config.ItemSetting[ItemKey];

            foreach (var ItemTake in Item.ItemListForCraft)
                player.inventory.Take(null, ItemManager.FindItemDefinition(ItemTake.Shortname).itemid, ItemTake.Amount);

            if (Item.IQEconomicPrice != 0)
                IQEconomicRemoveBalance(player.userID, Item.IQEconomicPrice);

            switch (Item.CategoryItems)
            {
                case CategoryItem.Weapon:
                case CategoryItem.Attirie:
                case CategoryItem.Construction:
                case CategoryItem.Electrical:
                case CategoryItem.Fun:
                case CategoryItem.Items:
                case CategoryItem.Tools:// ITEM
                    {
                        Item item = ItemManager.CreateByName(Item.Shortname, 1, Item.SkinID);
                        if (!String.IsNullOrWhiteSpace(Item.DisplayName))
                            item.name = Item.DisplayName;

                        player.GiveItem(item);
                        break;
                    }
                case CategoryItem.Custom: // COMMAND
                    {
                        rust.RunServerCommand(Item.Command.Replace("%STEAMID%", player.UserIDString));
                        break;
                    }
                case CategoryItem.Transport: // ENTITY
                case CategoryItem.Entity:
                    {
                        Item item = ItemManager.CreateByName(CustomItemShortname, 1, Item.SkinID);
                        if (!String.IsNullOrWhiteSpace(Item.DisplayName))
                            item.name = Item.DisplayName;

                        player.GiveItem(item);
                        break;
                    }
            }
        }
        #endregion

        #endregion

        #region Hooks
        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            SpawnItem(go.ToBaseEntity());
        }
        private void OnServerInitialized()
        {
            LoadedIcon();

            foreach (var p in BasePlayer.activePlayerList)
                OnPlayerConnected(p);
        }
        void Unload() => ServerMgr.Instance.StopCoroutine(DownloadImages());

        void OnPlayerConnected(BasePlayer player)
        {
            CachingImage(player);
        }
        #endregion

        #region Commands
        [ChatCommand("craft")]
        void IQCraftSystemCommand(BasePlayer player)
        {
            PagePlayers[player] = 0;
            UI_CraftMenu(player);
        }

        [ConsoleCommand("craft_give")] 
        void IQCraftSystemCmdGiveItem(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
            {
                
                if (!arg.Player().IsAdmin)
                {
                    PrintError($"Игрок {BasePlayer.FindByID(arg.Player().userID)} Пытается выдать себе предмет");
                    return;
                }
            }
            if (arg == null || arg.Args == null || arg.Args.Length < 2)
            {
                PrintWarning("Используйте синтаксис : craft_give SteamID Ключ(из кфг)");
                return;
            }
            if(arg.Args[0] == null || String.IsNullOrWhiteSpace(arg.Args[0]))
            {
                PrintWarning("Вы неверно указали SteamID\nИспользуйте синтаксис : craft_give SteamID Ключ(из кфг)");
                return;
            }
            ulong userID = ulong.Parse(arg.Args[0]);
            if(arg.Args[1] == null || String.IsNullOrWhiteSpace(arg.Args[1]))
            {
                PrintWarning("Вы неверно указали ключ из кфг\nИспользуйте синтаксис : craft_give SteamID Ключ(из кфг)");
                return;
            }
            string ItemKey = (string)arg.Args[1];
            if(!config.ItemSetting.ContainsKey(ItemKey))
            {
                PrintWarning("Такого ключа не существует в конфигурации, используйте верный ключ!");
                return;
            }

            GiveItemUser(userID, ItemKey);
        }

        [ConsoleCommand("func_craft")]
        void IQCraftSsytemFunc(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;

            string Action = arg.Args[0];
            switch(Action)
            {
                case "close_ui":
                    {
                        DestroyAll(player);
                        break;
                    }
                case "select_category":
                    {
                        CategoryItem CategoryTake = (CategoryItem)int.Parse(arg.Args[1]);
                        DestroyItem(player);
                        LoadedItems(player, CategoryTake);
                        break;
                    }
                case "select_item": 
                    {
                        CategoryItem CategoryTake = (CategoryItem)int.Parse(arg.Args[1]);
                        int SlotActive = int.Parse(arg.Args[2]);
                        string ItemKey = arg.Args[3];
                        DestroyItem(player);
                        LoadedItems(player, CategoryTake, SlotActive);
                        UI_Information_Craft(player, ItemKey);
                        break;
                    }
                case "craft_item": 
                    {
                        string ItemKey = arg.Args[1];
                        CraftingItem(player, ItemKey);
                        UI_Information_Craft(player, ItemKey);
                        break;
                    }
                case "next.page": 
                    {
                        CategoryItem CategoryTake = (CategoryItem)int.Parse(arg.Args[1]);

                        DestroyItem(player);
                        PagePlayers[player]++;
                        LoadedItems(player, CategoryTake,0);
                        break;
                    }
                case "back.page":
                    {
                        CategoryItem CategoryTake = (CategoryItem)int.Parse(arg.Args[1]);

                        DestroyItem(player);
                        PagePlayers[player]--;
                        LoadedItems(player, CategoryTake, 0);
                        break;
                    }
            }
        }
        #endregion

        #region UI

        public static string CRAFTSYSTEM_HUD = "CRAFTSYSTEM_HUD";

        void DestroyItemInfo(BasePlayer player)
        {
            for(int ItemCount = 0; ItemCount < 15; ItemCount++)
            {
                CuiHelper.DestroyUi(player, $"REQUIRES_ITEM_AMOUNT_{ItemCount}");
                CuiHelper.DestroyUi(player, $"REQUIRES_ITEM_ICO_{ItemCount}");
                CuiHelper.DestroyUi(player, $"REQUIRES_ITEM_LOGO_{ItemCount}");
                CuiHelper.DestroyUi(player, $"REQUIRES_ITEM_{ItemCount}");
            }
            CuiHelper.DestroyUi(player, "CREATE_ITEM_BUTTON");
            CuiHelper.DestroyUi(player, "REQUIRES_ITEM_LIST_TITLE");
            CuiHelper.DestroyUi(player, "REQUIRES_ITEM_LIST_PANEL");

            CuiHelper.DestroyUi(player, "TITLE_REQUIRES");     
            CuiHelper.DestroyUi(player, "WORKBENCH_REQ");
            CuiHelper.DestroyUi(player, "WORKBENCH_REQ_ICON");
            CuiHelper.DestroyUi(player, "IQPLAGUESKILL");
            CuiHelper.DestroyUi(player, "IQPLAGUESKILL_ICON");
            CuiHelper.DestroyUi(player, "IQRANKSYSTEM_REQ_ICON");
            CuiHelper.DestroyUi(player, "IQECONOMIC");
            CuiHelper.DestroyUi(player, "IQECONOMIC_REQ_ICON");
            CuiHelper.DestroyUi(player, "IQRANKSYSTEM");
            CuiHelper.DestroyUi(player, "DESCRIPTION_CRAFT_INFO");
            CuiHelper.DestroyUi(player, "TITLE_CRAFT_INFO");
            CuiHelper.DestroyUi(player, "ICON_CRAFT_INFO");
            CuiHelper.DestroyUi(player, "CRAFT_INFORMATION_PANEL");
        }
        void DestroyItem(BasePlayer player)
        {
            CategoryItem SortCategory = !CategoryActive.ContainsKey(player) ? CategoryItem.All : CategoryActive[player];
            int ItemCategoryIndex = 0;
            var Items = SortCategory == CategoryItem.All ? config.ItemSetting : config.ItemSetting.Where(i => i.Value.CategoryItems == SortCategory);
            foreach (var Item in Items)
            {
                CuiHelper.DestroyUi(player, $"ITEM_ICON_{ItemCategoryIndex}");
                CuiHelper.DestroyUi(player, $"ITEM_BACKGROUND_{ItemCategoryIndex}");
                CuiHelper.DestroyUi(player, $"ITEMS_{ItemCategoryIndex}");
                ItemCategoryIndex++;
            }
            CuiHelper.DestroyUi(player, "ITEM_PANEL");
            CuiHelper.DestroyUi(player, "BTN_BACK_BUTTON");
            CuiHelper.DestroyUi(player, "BTN_NEXT_BUTTON");
        }
        void DestroyAll(BasePlayer player)
        {
            DestroyItemInfo(player);
            DestroyItem(player);
            CuiHelper.DestroyUi(player, "CLOSE_BTN");
            CuiHelper.DestroyUi(player, "CLOSE_ICON");
            CuiHelper.DestroyUi(player, "CATEGORY_WEAPON");
            CuiHelper.DestroyUi(player, "CATEGORY_WEAPON" + "BUTTON");
            CuiHelper.DestroyUi(player, "CATEGORY_TRANSPORT");
            CuiHelper.DestroyUi(player, "CATEGORY_TRANSPORT" + "BUTTON");
            CuiHelper.DestroyUi(player, "CATEGORY_TOOLS");
            CuiHelper.DestroyUi(player, "CATEGORY_TOOLS" + "BUTTON");
            CuiHelper.DestroyUi(player, "CATEGORY_ITEMS");
            CuiHelper.DestroyUi(player, "CATEGORY_ITEMS" + "BUTTON");
            CuiHelper.DestroyUi(player, "CATEGORY_FUN");
            CuiHelper.DestroyUi(player, "CATEGORY_FUN" + "BUTTON");
            CuiHelper.DestroyUi(player, "CATEGORY_ELECTRICAL");
            CuiHelper.DestroyUi(player, "CATEGORY_ELECTRICAL" + "BUTTON");
            CuiHelper.DestroyUi(player, "CATEGORY_CUSTOM");
            CuiHelper.DestroyUi(player, "CATEGORY_CUSTOM" + "BUTTON");
            CuiHelper.DestroyUi(player, "CATEGORY_CONSTRUCTION");
            CuiHelper.DestroyUi(player, "CATEGORY_CONSTRUCTION" + "BUTTON");
            CuiHelper.DestroyUi(player, "CATEGORY_ATTIRIE");
            CuiHelper.DestroyUi(player, "CATEGORY_ATTIRIE" + "BUTTON");
            CuiHelper.DestroyUi(player, "CATEGORY_ALL");
            CuiHelper.DestroyUi(player, "CATEGORY_ALL" + "BUTTON");
            CuiHelper.DestroyUi(player, "CATEGORY_PANEL");
            CuiHelper.DestroyUi(player, "LOGO");
            CuiHelper.DestroyUi(player, "DESCRIPTION");
            CuiHelper.DestroyUi(player, "TITLE");
            CuiHelper.DestroyUi(player, CRAFTSYSTEM_HUD);          
        }

        void UI_CraftMenu(BasePlayer player)
        {
            DestroyAll(player);
            var Interface = config.InterfaceSetting;
            var Category = Interface.CategorySetting.PNGCategoryIcons;
            float FadeOut = 0.3f;
            float FadeIn = 0.3f;

            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                FadeOut = FadeOut,
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.GeneralSetting.HexBackground), Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }
            }, "Overlay", CRAFTSYSTEM_HUD);

            #region Welcome Info
            container.Add(new CuiLabel
            {
                FadeOut = FadeOut,
                RectTransform = { AnchorMin = "0 0.4759259", AnchorMax = "0.265625 0.5250071" },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_TITLE",player.UserIDString), Font = "robotocondensed-bold.ttf", Align = TextAnchor.UpperCenter }
            },  CRAFTSYSTEM_HUD, "TITLE");

            container.Add(new CuiLabel
            {
                FadeOut = FadeOut,
                RectTransform = { AnchorMin = "0 0.1962963", AnchorMax = "0.265625 0.4759259" },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_DESCRIPTION", player.UserIDString), Font = "robotocondensed-bold.ttf", Align = TextAnchor.UpperCenter }
            }, CRAFTSYSTEM_HUD, "DESCRIPTION");

            container.Add(new CuiElement
            {
                FadeOut = FadeOut,
                Parent = CRAFTSYSTEM_HUD,
                Name = "LOGO",
                Components =
                    {
                        new CuiRawImageComponent { FadeIn = FadeIn, Png = GetImage($"LOGO_{Interface.GeneralSetting.PNGLogo}") },
                        new CuiRectTransformComponent{ AnchorMin = "0.05729146 0.5407404", AnchorMax = $"0.1906248 0.7777775"},
                    }
            });

            #endregion

            container.Add(new CuiElement
            {
                FadeOut = FadeOut,
                Parent = CRAFTSYSTEM_HUD,
                Name = $"CLOSE_ICON",
                Components =
                    {
                        new CuiRawImageComponent { FadeIn = FadeIn, Png = GetImage($"CLOSE_BUTTON_{Interface.GeneralSetting.PNGCloseButton}")},
                        new CuiRectTransformComponent{ AnchorMin = "0.9625001 0.9351854", AnchorMax = "0.99583351289 0.9944444" }, 
                    }
            });

            container.Add(new CuiButton
            {
                FadeOut = FadeOut - 0.15f,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { FadeIn = FadeIn, Command = $"func_craft close_ui", Color = "0 0 0 0" },
                Text = { FadeIn = FadeIn, Text = "", Color = "0 0 0 0" }
            }, "CLOSE_ICON", "CLOSE_BTN");

            CuiHelper.AddUi(player, container);
            LoadedCategory(player);
            LoadedItems(player);
        }
        void LoadedCategory(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();

            var Interface = config.InterfaceSetting;
            var Category = Interface.CategorySetting.PNGCategoryIcons;
            float FadeOut = 0.3f;
            float FadeIn = 0.3f;

            #region Category Panel

            container.Add(new CuiPanel
            {
                FadeOut = FadeOut, 
                RectTransform = { AnchorMin = "0.3093751289 0.8333334", AnchorMax = "0.5677084 0.8787037" }, 
                Image = { FadeIn = FadeIn, Color = "0 0 0 0" }
            }, CRAFTSYSTEM_HUD, "CATEGORY_PANEL");

            #region Category Icon
            container.Add(new CuiElement
            {
                FadeOut = FadeOut,
                Parent = "CATEGORY_PANEL",
                Name = "CATEGORY_ALL",
                Components =
                    {
                        new CuiRawImageComponent { FadeIn = FadeIn, Png = GetImage($"CATEGORY_{Category.PNGAll}") },
                        new CuiRectTransformComponent{ AnchorMin = "0.03225803 0.1224494", AnchorMax = $"0.09677408 0.7755101"},
                    }
            });
            container.Add(new CuiButton
            {
                FadeOut = FadeOut,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { FadeIn = FadeIn, Command = $"func_craft select_category {CategoryItem.All:d}", Color = "0 0 0 0" },
                Text = { FadeIn = FadeIn, Text = "", Align = TextAnchor.MiddleCenter }
            },  $"CATEGORY_ALL", "CATEGORY_ALL" + "BUTTON");

            container.Add(new CuiElement
            {
                FadeOut = FadeOut + 0.06f,
                Parent = "CATEGORY_PANEL",
                Name = "CATEGORY_ATTIRIE",
                Components =
                    {
                        new CuiRawImageComponent { FadeIn = FadeIn + 0.06f, Png = GetImage($"CATEGORY_{Category.PNGAttirie}") },
                        new CuiRectTransformComponent{ AnchorMin = "0.1290321 0.1224494", AnchorMax = $"0.1935482 0.7755101"},
                    }
            });

            container.Add(new CuiButton
            {
                FadeOut = FadeOut + 0.06f,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { FadeIn = FadeIn + 0.06f, Command = $"func_craft select_category {CategoryItem.Attirie:d}", Color = "0 0 0 0" },
                Text = { FadeIn = FadeIn + 0.06f, Text = "", Align = TextAnchor.MiddleCenter }
            }, $"CATEGORY_ATTIRIE", "CATEGORY_ATTIRIE" + "BUTTON");

            container.Add(new CuiElement
            {
                FadeOut = FadeOut + 0.09f,
                Parent = "CATEGORY_PANEL",
                Name = "CATEGORY_CONSTRUCTION",
                Components =
                    {
                        new CuiRawImageComponent { FadeIn = FadeIn + 0.09f, Png = GetImage($"CATEGORY_{Category.PNGConstruction}") },
                        new CuiRectTransformComponent{ AnchorMin = "0.2258063 0.1224494", AnchorMax = $"0.2903225 0.7755101"},
                    }
            });

            container.Add(new CuiButton
            {
                FadeOut = FadeOut + 0.09f,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { FadeIn = FadeIn + 0.09f, Command = $"func_craft select_category {CategoryItem.Construction:d}", Color = "0 0 0 0" },
                Text = { FadeIn = FadeIn + 0.09f, Text = "", Align = TextAnchor.MiddleCenter }
            }, $"CATEGORY_CONSTRUCTION", "CATEGORY_CONSTRUCTION" + "BUTTON");

            container.Add(new CuiElement
            {
                FadeOut = FadeOut + 0.12f,
                Parent = "CATEGORY_PANEL",
                Name = "CATEGORY_CUSTOM",
                Components =
                    {
                        new CuiRawImageComponent { FadeIn = FadeIn + 0.12f, Png = GetImage($"CATEGORY_{Category.PNGCustom}") },
                        new CuiRectTransformComponent{ AnchorMin = "0.3225803 0.1224494", AnchorMax = $"0.3870966 0.7755101"},
                    }
            });

            container.Add(new CuiButton
            {
                FadeOut = FadeOut + 0.12f,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { FadeIn = FadeIn + 0.12f, Command = $"func_craft select_category {CategoryItem.Custom:d}", Color = "0 0 0 0" },
                Text = { FadeIn = FadeIn + 0.12f, Text = "", Align = TextAnchor.MiddleCenter }
            }, $"CATEGORY_CUSTOM", "CATEGORY_CUSTOM" + "BUTTON");

            container.Add(new CuiElement
            {
                FadeOut = FadeOut + 0.15f,
                Parent = "CATEGORY_PANEL",
                Name = "CATEGORY_ELECTRICAL",
                Components =
                    {
                        new CuiRawImageComponent { FadeIn = FadeIn + 0.15f, Png = GetImage($"CATEGORY_{Category.PNGElectrical}") },
                        new CuiRectTransformComponent{ AnchorMin = "0.4193543 0.1224494", AnchorMax = $"0.4838706 0.7755101"},
                    }
            });

            container.Add(new CuiButton
            {
                FadeOut = FadeOut + 0.15f,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { FadeIn = FadeIn + 0.15f, Command = $"func_craft select_category {CategoryItem.Electrical:d}", Color = "0 0 0 0" },
                Text = { FadeIn = FadeIn + 0.15f, Text = "", Align = TextAnchor.MiddleCenter }
            }, $"CATEGORY_ELECTRICAL", "CATEGORY_ELECTRICAL" + "BUTTON");

            container.Add(new CuiElement
            {
                FadeOut = FadeOut + 0.18f,
                Parent = "CATEGORY_PANEL",
                Name = "CATEGORY_FUN",
                Components =
                    {
                        new CuiRawImageComponent { FadeIn = FadeIn + 0.18f, Png = GetImage($"CATEGORY_{Category.PNGFun}") },
                        new CuiRectTransformComponent{ AnchorMin = "0.5161289 0.1224494", AnchorMax = $"0.5806447 0.7755101"},
                    }
            });

            container.Add(new CuiButton
            {
                FadeOut = FadeOut + 0.18f,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { FadeIn = FadeIn + 0.18f, Command = $"func_craft select_category {CategoryItem.Fun:d}", Color = "0 0 0 0" },
                Text = { FadeIn = FadeIn + 0.18f, Text = "", Align = TextAnchor.MiddleCenter }
            }, $"CATEGORY_FUN", "CATEGORY_FUN" + "BUTTON");

            container.Add(new CuiElement
            {
                FadeOut = FadeOut + 0.21f,
                Parent = "CATEGORY_PANEL",
                Name = "CATEGORY_ITEMS",
                Components =
                    {
                        new CuiRawImageComponent { FadeIn = FadeIn + 0.21f, Png = GetImage($"CATEGORY_{Category.PNGItems}") },
                        new CuiRectTransformComponent{ AnchorMin = "0.6129025 0.1224494", AnchorMax = $"0.6774181 0.7755101"},
                    }
            });

            container.Add(new CuiButton
            {
                FadeOut = FadeOut + 0.21f,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { FadeIn = FadeIn + 0.21f, Command = $"func_craft select_category {CategoryItem.Items:d}", Color = "0 0 0 0" },
                Text = { FadeIn = FadeIn + 0.21f, Text = "", Align = TextAnchor.MiddleCenter }
            }, $"CATEGORY_ITEMS", "CATEGORY_ITEMS" + "BUTTON");

            container.Add(new CuiElement
            {
                FadeOut = FadeOut + 0.24f,
                Parent = "CATEGORY_PANEL",
                Name = "CATEGORY_TOOLS",
                Components =
                    {
                        new CuiRawImageComponent { FadeIn = FadeIn + 0.24f, Png = GetImage($"CATEGORY_{Category.PNGTools}") },
                        new CuiRectTransformComponent{ AnchorMin = "0.7096759 0.1224494", AnchorMax = $"0.7741908 0.7755101"},
                    }
            });

            container.Add(new CuiButton
            {
                FadeOut = FadeOut + 0.24f,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { FadeIn = FadeIn + 0.24f, Command = $"func_craft select_category {CategoryItem.Tools:d}", Color = "0 0 0 0" },
                Text = { FadeIn = FadeIn + 0.24f, Text = "", Align = TextAnchor.MiddleCenter }
            }, $"CATEGORY_TOOLS", "CATEGORY_TOOLS" + "BUTTON");

            container.Add(new CuiElement
            {
                FadeOut = FadeOut + 0.27f,
                Parent = "CATEGORY_PANEL",
                Name = "CATEGORY_TRANSPORT",
                Components =
                    {
                        new CuiRawImageComponent { FadeIn = FadeIn + 0.27f, Png = GetImage($"CATEGORY_{Category.PNGTransport}") },
                        new CuiRectTransformComponent{ AnchorMin = "0.8064507 0.1224494", AnchorMax = $"0.8709663 0.7755101"},
                    }
            });

            container.Add(new CuiButton
            {
                FadeOut = FadeOut + 0.27f,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { FadeIn = FadeIn + 0.27f, Command = $"func_craft select_category {CategoryItem.Transport:d}", Color = "0 0 0 0" },
                Text = { FadeIn = FadeIn + 0.27f, Text = "", Align = TextAnchor.MiddleCenter }
            }, $"CATEGORY_TRANSPORT", "CATEGORY_TRANSPORT" + "BUTTON");

            container.Add(new CuiElement
            {
                FadeOut = FadeOut + 0.3f,
                Parent = "CATEGORY_PANEL",
                Name = "CATEGORY_WEAPON",
                Components =
                    {
                        new CuiRawImageComponent { FadeIn = FadeIn + 0.3f, Png = GetImage($"CATEGORY_{Category.PNGWeapon}") },
                        new CuiRectTransformComponent{ AnchorMin = "0.9032234 0.1224494", AnchorMax = $"0.9677392 0.7755101"},
                    }
            });

            container.Add(new CuiButton
            {
                FadeOut = FadeOut + 0.3f,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { FadeIn = FadeIn + 0.3f, Command = $"func_craft select_category {CategoryItem.Weapon:d}", Color = "0 0 0 0" },
                Text = { FadeIn = FadeIn + 0.3f, Text = "", Align = TextAnchor.MiddleCenter }
            }, $"CATEGORY_WEAPON", "CATEGORY_WEAPON" + "BUTTON");
            #endregion

            #endregion

            CuiHelper.AddUi(player, container);
        }

        #region Loaded Items
        void LoadedItems(BasePlayer player, CategoryItem SortCategory = CategoryItem.All, int SlotActive = 0)
        {
            if (!CategoryActive.ContainsKey(player))
                CategoryActive.Add(player, SortCategory);
            else CategoryActive[player] = SortCategory;

            var Interface = config.InterfaceSetting;
            var Category = Interface.CategorySetting.PNGCategoryIcons;
            var Items = SortCategory == CategoryItem.All ? config.ItemSetting.Skip(20 * PagePlayers[player]).Take(20) : config.ItemSetting.Where(i => i.Value.CategoryItems == SortCategory).Skip(20 * PagePlayers[player]).Take(20);
            float FadeOut = 0.3f;
            float FadeIn = 0.3f;

            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                FadeOut = FadeOut, 
                RectTransform = { AnchorMin = "0.3093751289 0.1444444", AnchorMax = "0.5677084 0.8296297" },  
                Image = { FadeIn = FadeIn, Color = "0 0 0 0" }
            }, CRAFTSYSTEM_HUD, "ITEM_PANEL");

            int CountKitPage = SortCategory == CategoryItem.All ? config.ItemSetting.Skip(20 * (PagePlayers[player] + 1)).Take(20).Count() : config.ItemSetting.Where(i => i.Value.CategoryItems == SortCategory).Skip(20 * (PagePlayers[player] + 1)).Take(20).Count();
            int ItemCount = 0, x = 0, y = 0;
            foreach(var Item in Items)
            {
                string PNGBackground = SlotActive == ItemCount ? $"ACTIVE_ITEM_{Interface.GeneralSetting.PNGActive}" : $"IN_ACTIVE_ITEM_{Interface.GeneralSetting.PNGInActive}";
                string PNGColorInactive = SlotActive == ItemCount ? "#FFFFFFFF" : GetColorSlot(Item.Value.CategoryItems);
                CategoryItem CategoryItem = Item.Value.CategoryItems;
                string PNGIcon = CategoryItem == CategoryItem.Transport || CategoryItem == CategoryItem.Custom || CategoryItem == CategoryItem.Entity ? GetImage($"ITEM_{Item.Value.PNG}") : Item.Value.SkinID != 0 ? GetImage($"{Item.Value.Shortname}_128px_{Item.Value.SkinID}", Item.Value.SkinID) : GetImage($"{Item.Value.Shortname}_128px");
                string AnchorMinResize = SlotActive == ItemCount ? "0.05000005 0.05" : "0.1583334 0.1333334";
                string AnchorMaxResize = SlotActive == ItemCount ? "0.9666668 0.9666671" : "0.8750001 0.8500004";

                container.Add(new CuiPanel
                {
                    FadeOut = FadeOut,
                    RectTransform = { AnchorMin = $"{0 + (x * 0.25)} {0.837216 - (y * 0.2)}", AnchorMax = $"{0.2419354 + (x * 0.25)} {0.9993781 - (y * 0.2)}" },
                    Image = { FadeIn = FadeIn, Color = "0 0 0 0" }
                }, "ITEM_PANEL", $"ITEMS_{ItemCount}");

                container.Add(new CuiElement    
                {
                    FadeOut = FadeOut,
                    Parent = $"ITEMS_{ItemCount}",
                    Name = $"ITEM_BACKGROUND_{ItemCount}",
                    Components =
                    {
                        new CuiRawImageComponent { FadeIn = FadeIn, Png = GetImage(PNGBackground), Color = HexToRustFormat(PNGColorInactive) },
                        new CuiRectTransformComponent{ AnchorMin = "0 0", AnchorMax = "1 1"},
                    }
                });
                
                container.Add(new CuiElement
                {
                    FadeOut = FadeOut,
                    Parent = $"ITEMS_{ItemCount}",
                    Name = $"ITEM_ICON_{ItemCount}",
                    Components =
                    {
                        new CuiRawImageComponent { FadeIn = FadeIn, Png = PNGIcon },
                        new CuiRectTransformComponent{ AnchorMin = AnchorMinResize, AnchorMax = AnchorMaxResize },
                    }
                });

                container.Add(new CuiButton
                {
                    FadeOut = FadeOut,
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { FadeIn = FadeIn, Command = $"func_craft select_item {SortCategory:d} {ItemCount} {Item.Key}", Color = "0 0 0 0" },
                    Text = { FadeIn = FadeIn, Text = "", Align = TextAnchor.MiddleCenter }
                }, $"ITEMS_{ItemCount}");

                ItemCount++;
                x++;
                if(x == 4)
                {
                    y++;
                    x = 0;
                }
            }

            if (PagePlayers[player] != 0)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.3093783 0.05185186", AnchorMax = "0.3552116 0.1398148" },
                    Button = { Command = $"func_craft back.page {SortCategory:d}", Color = "0 0 0 0" },
                    Text = { Text = "<b><</b>", FontSize = 50, Align = TextAnchor.MiddleCenter }
                }, CRAFTSYSTEM_HUD, $"BTN_BACK_BUTTON");
            }
            if (CountKitPage != 0)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.5208334 0.05185186", AnchorMax = "0.5666667 0.1398148" },
                    Button = { Command = $"func_craft next.page {SortCategory:d}", Color = "0 0 0 0" },
                    Text = { Text = "<b>></b>", FontSize = 50, Align = TextAnchor.MiddleCenter }
                },  CRAFTSYSTEM_HUD, $"BTN_NEXT_BUTTON"); 
            }

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Craft Item Info
        int CountRequires(string ItemKey)
        {
            var ItemRequires = config.ItemSetting[ItemKey];
            int Requires = 0;

            if (ItemRequires.IQPlagueSkillCraft)
                Requires++;
            if(!String.IsNullOrWhiteSpace(ItemRequires.IQRankSystemRank))
                Requires++;
            if (ItemRequires.IQEconomicPrice != 0)
                Requires++;
            if (ItemRequires.WorkBenchLevel != 0)
                Requires++;

            return Requires;
        }
        void UI_Information_Craft(BasePlayer player, string ItemKey)
        {
            DestroyItemInfo(player);
            var Interface = config.InterfaceSetting;
            var Require = Interface.RequiresSetting;
            var Category = Interface.CategorySetting.PNGCategoryIcons;
            var Item = config.ItemSetting[ItemKey];
            CategoryItem CategoryItem = Item.CategoryItems;
            string PNGIcon = CategoryItem == CategoryItem.Transport || CategoryItem == CategoryItem.Custom || CategoryItem == CategoryItem.Entity ? GetImage($"ITEM_{Item.PNG}") : Item.SkinID != 0 ? GetImage($"{Item.Shortname}_256px_{Item.SkinID}", Item.SkinID) : GetImage($"{Item.Shortname}_256px");
            float FadeOut = 0.3f;
            float FadeIn = 0.3f;

            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                FadeOut = FadeOut,
                RectTransform = { AnchorMin = "0.5947914 0.1444444", AnchorMax = "0.896875 0.8296297" },
                Image = { FadeIn = FadeIn, Color = "0 0 0 0" }
            }, CRAFTSYSTEM_HUD, "CRAFT_INFORMATION_PANEL");

            container.Add(new CuiElement
            {
                FadeOut = FadeOut,
                Parent = $"CRAFT_INFORMATION_PANEL",
                Name = $"ICON_CRAFT_INFO",
                Components =
                    {
                        new CuiRawImageComponent { FadeIn = FadeIn, Png = PNGIcon },
                        new CuiRectTransformComponent{ AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-140 -130", OffsetMax = "100 80" },
                    }
            });

            container.Add(new CuiLabel
            {
                FadeOut = FadeOut,
                RectTransform = { AnchorMin = "0.03490222 0.9189188", AnchorMax = "1 1" },
                Text = { FadeIn = FadeIn, Text = $"<b>{Item.DisplayName}</b>", FontSize = 30, Align = TextAnchor.UpperLeft }
            }, "CRAFT_INFORMATION_PANEL", "TITLE_CRAFT_INFO");

            container.Add(new CuiLabel
            {
                FadeOut = FadeOut,
                RectTransform = { AnchorMin = "0.03945446 0.8189189", AnchorMax = "1 0.9189186" },
                Text = { FadeIn = FadeIn, Text = $"{Item.Description}", FontSize = 15, Align = TextAnchor.UpperLeft }
            }, "CRAFT_INFORMATION_PANEL", "DESCRIPTION_CRAFT_INFO");

            #region Requires

            int Requires = 0;
            int RequiresYes = 0;
            if (CountRequires(ItemKey) != 0)
            {
                container.Add(new CuiLabel
                {
                    FadeOut = FadeOut,
                    RectTransform = { AnchorMin = "0.03490228 0.7662163", AnchorMax = "1 0.8175668" },
                    Text = { FadeIn = FadeIn, Text = GetLang("UI_INFORMATION_TITLE_REQUIRES", player.UserIDString), Align = TextAnchor.MiddleLeft }
                }, "CRAFT_INFORMATION_PANEL", "TITLE_REQUIRES");

                if (Item.WorkBenchLevel != 0)
                {
                    string StatusWorkBenchLevel = DoWorkbenchLevel(player, Item.WorkBenchLevel) ? $"<color={Interface.RequiresSetting.RequiresDoTrueColor}>{Interface.RequiresSetting.RequiresDoTrue}</color>" : $"<color={Interface.RequiresSetting.RequiresDoFalseColor}>{Interface.RequiresSetting.RequiresDoFalse}</color>";

                    container.Add(new CuiElement
                    {
                        FadeOut = FadeOut,
                        Parent = $"CRAFT_INFORMATION_PANEL",
                        Name = $"WORKBENCH_REQ_ICON",
                        Components =
                    {
                        new CuiRawImageComponent { FadeIn = FadeIn, Png = GetImage($"REQUIRES_WORKBENCH_{Require.PNGRequiresWorckbench}")},
                        new CuiRectTransformComponent{ AnchorMin = "0.03945446 0.7216211", AnchorMax = "0.09290269 0.763513" },
                    }
                    });

                    container.Add(new CuiLabel
                    {
                        FadeOut = FadeOut,
                        RectTransform = { AnchorMin = "0.1137938 0.7216209", AnchorMax = "1 0.7648644" },
                        Text = { FadeIn = FadeIn, Text = GetLang("UI_INFORMATION_WOKBENCH_LEVEL", player.UserIDString, Item.WorkBenchLevel, StatusWorkBenchLevel), Align = TextAnchor.UpperLeft }
                    }, "CRAFT_INFORMATION_PANEL", $"WORKBENCH_REQ");

                    if (CountRequires(ItemKey) >= Requires)
                        Requires++;
                    if (DoWorkbenchLevel(player, Item.WorkBenchLevel))
                        RequiresYes++;
                }

                if (IQPlagueSkill)
                    if (Item.IQPlagueSkillCraft)
                    {
                        string StatusIQPlagueSkill = DoSkillIQPlagueSkill(player) ? $"<color={Interface.RequiresSetting.RequiresDoTrueColor}>{Interface.RequiresSetting.RequiresDoTrue}</color>" : $"<color={Interface.RequiresSetting.RequiresDoFalseColor}>{Interface.RequiresSetting.RequiresDoFalse}</color>";

                        container.Add(new CuiElement
                        {
                            FadeOut = FadeOut,
                            Parent = $"CRAFT_INFORMATION_PANEL",
                            Name = $"IQPLAGUESKILL_ICON",
                            Components =
                    {
                        new CuiRawImageComponent { FadeIn = FadeIn, Png = GetImage($"REQUIRES_IQPLAGUESKILL_{Require.PNGRequiresIQPlagueSkill}") },
                        new CuiRectTransformComponent{ AnchorMin = $"0.03945446 {0.7216209 - (0.05 * Requires)}", AnchorMax = $"0.09462677 {0.7648644 - (0.05 * Requires)}" },
                    }
                        });

                        container.Add(new CuiLabel
                        {
                            FadeOut = FadeOut,
                            RectTransform = { AnchorMin = $"0.1137938 {0.7216209 - (0.05 * Requires)}", AnchorMax = $"1 {0.7648644 - (0.05 * Requires)}" },
                            Text = { FadeIn = FadeIn, Text = GetLang("UI_INFORMATION_IQPLAGUESKILL", player.UserIDString, "", StatusIQPlagueSkill), Align = TextAnchor.UpperLeft }
                        }, "CRAFT_INFORMATION_PANEL", $"IQPLAGUESKILL");

                        if (CountRequires(ItemKey) >= Requires)
                            Requires++;
                        if (DoSkillIQPlagueSkill(player))
                            RequiresYes++;
                    }

                if (IQEconomic)
                    if (Item.IQEconomicPrice != 0)
                    {
                        string StatusIQEconomicPrice = DoBalanceIQEconomic(player, Item.IQEconomicPrice) ? $"<color={Interface.RequiresSetting.RequiresDoTrueColor}>{Interface.RequiresSetting.RequiresDoTrue}</color>" : $"<color={Interface.RequiresSetting.RequiresDoFalseColor}>{Interface.RequiresSetting.RequiresDoFalse}</color>";

                        container.Add(new CuiElement
                        {
                            FadeOut = FadeOut,
                            Parent = $"CRAFT_INFORMATION_PANEL",
                            Name = $"IQECONOMIC_REQ_ICON",
                            Components =
                    {
                        new CuiRawImageComponent { FadeIn = FadeIn, Png = GetImage($"REQUIRES_IQECONOMIC_{Require.PNGRequiresIQEconomic}") },
                        new CuiRectTransformComponent{ AnchorMin = $"0.03945446 {0.7216209 - (0.05 * Requires)}", AnchorMax = $"0.09462677 {0.7648644 - (0.05 * Requires)}" },
                    }
                        });

                        container.Add(new CuiLabel
                        {
                            FadeOut = FadeOut,
                            RectTransform = { AnchorMin = $"0.1137938 {0.7216209 - (0.05 * Requires)}", AnchorMax = $"1 {0.7648644 - (0.05 * Requires)}" },
                            Text = { FadeIn = FadeIn, Text = GetLang("UI_INFORMATION_IQECONOMIC", player.UserIDString, Item.IQEconomicPrice, StatusIQEconomicPrice), Align = TextAnchor.UpperLeft }
                        }, "CRAFT_INFORMATION_PANEL", $"IQECONOMIC");

                        if (CountRequires(ItemKey) >= Requires)
                            Requires++;
                        if (DoBalanceIQEconomic(player, Item.IQEconomicPrice))
                            RequiresYes++;
                    }

                if (IQRankSystem)
                    if (!String.IsNullOrWhiteSpace(Item.IQRankSystemRank))
                    {
                        if (!IQRankRankReality(Item.IQRankSystemRank))
                            PrintError($"Вы указали не существующий ранг ({Item.IQRankSystemRank}), в плагине IQRankSystem не обнаружен этот ранг! Проверьте данные!");

                        string StatusIQRankSystemRank = DoRankIQRankSystem(player, Item.IQRankSystemRank) ? $"<color={Interface.RequiresSetting.RequiresDoTrueColor}>{Interface.RequiresSetting.RequiresDoTrue}</color>" : $"<color={Interface.RequiresSetting.RequiresDoFalseColor}>{Interface.RequiresSetting.RequiresDoFalse}</color>";

                        container.Add(new CuiElement
                        {
                            FadeOut = FadeOut,
                            Parent = $"CRAFT_INFORMATION_PANEL",
                            Name = $"IQRANKSYSTEM_REQ_ICON",
                            Components =
                            {
                                new CuiRawImageComponent { FadeIn = FadeIn, Png = GetImage($"REQUIRES_IQRANKSYSTEM_{Require.PNGRequiresIQRankSystem}") },
                                new CuiRectTransformComponent{ AnchorMin = $"0.03945446 {0.7216209 - (0.05 * Requires)}", AnchorMax = $"0.09462677 {0.7648644 - (0.05 * Requires)}" },
                            }
                        });

                        container.Add(new CuiLabel
                        {
                            FadeOut = FadeOut,
                            RectTransform = { AnchorMin = $"0.1137938 {0.7216209 - (0.05 * Requires)}", AnchorMax = $"1 {0.7648644 - (0.05 * Requires)}" },
                            Text = { FadeIn = FadeIn, Text = GetLang("UI_INFORMATION_IQRANK_RANK", player.UserIDString, IQRankSystemGetName(Item.IQRankSystemRank), StatusIQRankSystemRank), Align = TextAnchor.UpperLeft }
                        }, "CRAFT_INFORMATION_PANEL", $"IQRANKSYSTEM");

                        if (CountRequires(ItemKey) >= Requires)
                            Requires++;
                        if (DoRankIQRankSystem(player, Item.IQRankSystemRank))
                            RequiresYes++;
                    }

            }
            #endregion

            #region RequiresItem
            List<Configuration.ItemSettings.ItemForCraft> RequiresItemList = Item.ItemListForCraft;

            if (RequiresItemList != null)
            {
                if(Is_Full_Item_Pack(player, RequiresItemList) && RequiresYes >= Requires)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.6379313 0.001351424", AnchorMax = "0.9965519 0.06486491" },
                        Button = {  Command = $"func_craft craft_item {ItemKey}", Color = HexToRustFormat(Interface.GeneralSetting.HexButtonCreate) },
                        Text = {  Text = GetLang("UI_CREATE_ITEM",player.UserIDString), Align = TextAnchor.MiddleCenter }
                    }, $"CRAFT_INFORMATION_PANEL", "CREATE_ITEM_BUTTON"); 
                }

                container.Add(new CuiLabel
                {
                    FadeOut = FadeOut,
                    RectTransform = { AnchorMin = "0.03490228 0.4891882", AnchorMax = "1 0.5405387" },
                    Text = { FadeIn = FadeIn, Text = GetLang("UI_INFORMATION_ITEM_LIST_TITLE", player.UserIDString), Align = TextAnchor.MiddleLeft }
                }, "CRAFT_INFORMATION_PANEL", "REQUIRES_ITEM_LIST_TITLE");

                container.Add(new CuiPanel
                {
                    FadeOut = FadeOut,
                    RectTransform = { AnchorMin = "0.03490228 0.07837844", AnchorMax = "1 0.4837838" },
                    Image = { FadeIn = FadeIn, Color = "0 0 0 0" }
                }, "CRAFT_INFORMATION_PANEL", "REQUIRES_ITEM_LIST_PANEL"); 

                #region Centering
                int ItemCount = 0;
                float itemMinPosition = 219f;
                float itemWidth = 0.413646f - 0.25f; /// Ширина
                float itemMargin = 0.439895f - 0.42f; /// Расстояние между 
                int itemCount = RequiresItemList.Count;
                float itemMinHeight = 0.7f; // Сдвиг по вертикали
                float itemHeight = 0.3f; /// Высота
                int ItemTarget = 5;

                if (itemCount > ItemTarget)
                {
                    itemMinPosition = 0.5f - ItemTarget / 2f * itemWidth - (ItemTarget - 1) / 2f * itemMargin;
                    itemCount -= ItemTarget;
                }
                else itemMinPosition = 0.5f - itemCount / 2f * itemWidth - (itemCount - 1) / 2f * itemMargin;

                #endregion

                foreach (var ItemCraft in RequiresItemList)
                {
                    string PNGIconCraft = !String.IsNullOrWhiteSpace(ItemCraft.PNG) ? GetImage($"ITEM_CRAFT_{ItemCraft.PNG}") : ItemCraft.SkinID != 0 ? GetImage($"{ItemCraft.Shortname}_128px_{ItemCraft.SkinID}", ItemCraft.SkinID) : GetImage($"{ItemCraft.Shortname}_128px"); 

                    container.Add(new CuiPanel
                    {
                        FadeOut = FadeOut,
                        RectTransform = { AnchorMin = $"{itemMinPosition} {itemMinHeight}", AnchorMax = $"{itemMinPosition + itemWidth} {itemMinHeight + itemHeight}" },
                        Image = { FadeIn = FadeIn, Color = "0 0 0 0" }
                    }, $"REQUIRES_ITEM_LIST_PANEL", $"REQUIRES_ITEM_{ItemCount}");

                    string HEXRequiresLogo = IS_Item_Player(player, ItemCraft.Shortname, ItemCraft.Amount, ItemCraft.SkinID) ? Require.RequiresDoTrueColor : Require.RequiresDoFalseColor;
                    container.Add(new CuiElement
                    {
                        FadeOut = FadeOut,
                        Parent = $"REQUIRES_ITEM_{ItemCount}", 
                        Name = $"REQUIRES_ITEM_LOGO_{ItemCount}",
                        Components =
                    {
                        new CuiRawImageComponent { FadeIn = FadeIn, Png = GetImage($"IN_ACTIVE_ITEM_{Interface.GeneralSetting.PNGInActive}"), Color = HexToRustFormat(HEXRequiresLogo) },
                        new CuiRectTransformComponent{ AnchorMin = "0 0", AnchorMax = "1 1"},
                    }
                    });

                    container.Add(new CuiElement
                    {
                        FadeOut = FadeOut,
                        Parent = $"REQUIRES_ITEM_LOGO_{ItemCount}",
                        Name = $"REQUIRES_ITEM_ICO_{ItemCount}",
                        Components =
                        {
                            new CuiRawImageComponent { FadeIn = FadeIn, Png = PNGIconCraft },
                            new CuiRectTransformComponent{ AnchorMin = "0 0", AnchorMax = "1 1"},
                        }
                    });

                    container.Add(new CuiLabel
                    {
                        FadeOut = FadeOut,
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Text = { FadeIn = FadeIn, Text = $"x{ItemCraft.Amount}", FontSize = 13, Align = TextAnchor.MiddleCenter }
                    }, $"REQUIRES_ITEM_ICO_{ItemCount}", $"REQUIRES_ITEM_AMOUNT_{ItemCount}");

                    #region Centering
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

                    if (ItemCount >= 15) break;
                    #endregion

                }
            }

            #endregion

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Utilites
        private static string HexToRustFormat(string hex)
        {
            Color color;
            ColorUtility.TryParseHtmlString(hex, out color);
            sb.Clear();
            return sb.AppendFormat("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a).ToString();
        }

        public string GetColorSlot(CategoryItem ItemCategory)
        {
            var Color = config.InterfaceSetting.CategorySetting.ColorCategoryIcons;
            switch (ItemCategory)
            {
                case CategoryItem.All:
                    return "#FFFFFFFF";
                case CategoryItem.Attirie:
                    return Color.AttirieColor;
                case CategoryItem.Construction:
                    return Color.ConstructionColor;
                case CategoryItem.Custom:
                    return Color.CustomColor;
                case CategoryItem.Electrical:
                    return Color.ElectricalColor;
                case CategoryItem.Fun:
                    return Color.FunColor;
                case CategoryItem.Items:
                    return Color.ItemsColor;
                case CategoryItem.Tools:
                    return Color.ToolsColor;
                case CategoryItem.Transport:
                    return Color.TransportColor;
                case CategoryItem.Weapon:
                    return Color.WeaponColor;
                default:
                    return "#FFFFFFFF";
            }
        }
        #endregion

        #endregion

        #region Lang
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["UI_TITLE"] = "<size=30><b>CRAFT SYSTEM</b></size>",
                ["UI_DESCRIPTION"] = "<size=18>Select the item you need from the list of all items or sort them by category, after that you can find out the cost of crafting and then create an item</size>",
                ["UI_CLOSE_BUTTON"] = "<size=30><b>CLOSE</b></size>",

                ["UI_INFORMATION_TITLE_REQUIRES"] = "<size=18><b>Additional requirements:</b></size>",
                ["UI_INFORMATION_WOKBENCH_LEVEL"] = "<size=12>Requires level {0} workbench {1}</size>",
                ["UI_INFORMATION_IQRANK_RANK"] = "<size=12>Requires rank {0} {1}</size>",
                ["UI_INFORMATION_IQECONOMIC"] = "<size=12>Requires {0} money {1}</size>",
                ["UI_INFORMATION_IQPLAGUESKILL"] = "<size=12>Requires skill advanced craft {1}</size>",

                ["UI_INFORMATION_ITEM_LIST_TITLE"] = "<size=18><b>Items required for crafting:</b></size>",
                ["UI_CREATE_ITEM"] = "<size=20><b>CREATE</b></size>",


            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["UI_TITLE"] = "<size=30><b>СИСТЕМА КРАФТА</b></size>",
                ["UI_DESCRIPTION"] = "<size=18>Выберите нужный вам предмет из списка всех предметов или отсортируйте их по категориям, после этого вы сможете узнать стоимость крафта и в дальнейшем создать предмет</size>",
                ["UI_CLOSE_BUTTON"] = "<size=30><b>ЗАКРЫТЬ</b></size>",

                ["UI_INFORMATION_TITLE_REQUIRES"] = "<size=18><b>Дополнительные требования:</b></size>",
                ["UI_INFORMATION_WOKBENCH_LEVEL"] = "<size=12>Требуется верстак {0} уровня {1}</size>",
                ["UI_INFORMATION_IQRANK_RANK"] = "<size=12>Требуется ранг {0} {1}</size>",
                ["UI_INFORMATION_IQECONOMIC"] = "<size=12>Требуется {0} монет {1}</size>",
                ["UI_INFORMATION_IQPLAGUESKILL"] = "<size=12>Требуется навык продвинутый крафт {1}</size>",

                ["UI_INFORMATION_ITEM_LIST_TITLE"] = "<size=18><b>Предметы требующиеся для крафта:</b></size>",
                ["UI_CREATE_ITEM"] = "<size=20><b>СОЗДАТЬ</b></size>",

            }, this, "ru");
            PrintWarning("Языковой файл загружен успешно");
        }

        public static StringBuilder sb = new StringBuilder();
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
        #endregion
    }
}
