using System.Text;
using System.Collections.Generic;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Oxide.Core.Plugins;
using System;
using Newtonsoft.Json;
using System.Linq;
using ConVar;
using System.Collections;

namespace Oxide.Plugins
{
    /*ПЛАГИН БЫЛ ПОФИКШЕН С ПОМОЩЬЮ ПРОГРАММЫ СКАЧАНОЙ С https://discord.gg/dNGbxafuJn */ [Info("IQCraftSystem", "https://discord.gg/dNGbxafuJn", "1.1.7")]
    [Description("Convenient crafting system")]
    internal class IQCraftSystem : RustPlugin
    {

        public Boolean IS_Item_Player(BasePlayer player, String Shortname, Int32 Amount, UInt64 SkinID = 0)
        {
            Int32 ItemAmount = 0;
            foreach (Item ItemRequires in player.inventory.AllItems())
            {
                if (ItemRequires == null) continue;
                if (ItemRequires.info.shortname != Shortname) continue;
                if (ItemRequires.skin != SkinID) continue;
                ItemAmount += ItemRequires.amount;
            }
            return ItemAmount >= Amount;
        }
        private void Init()
        {
            ReadData();
        }
        
        
                private readonly String CustomItemShortname = "box.wooden";

        public Boolean DoBalanceIQEconomic(BasePlayer player, Int32 NeededBalance)
        {
            return GetBalance(player.userID) >= NeededBalance;
        }
        
                [ChatCommand("craft")]
        private void IQCraftSystemCommand(BasePlayer player)
        {
            PagePlayers[player] = 0;
            UI_CraftMenu(player);
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
                PrintWarning(LanguageEn ? "Error reading #57 configuration 'oxide/config/{Name}', creating a new configuration!!" :  $"Ошибка чтения #57 конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }
		   		 		  						  	   		  		 			   		 		   		 		  	 	 
        public void SendImage(BasePlayer player, String imageName, UInt64 imageId = 0)
        {
            ImageLibrary?.Call("SendImage", player, imageName, imageId);
        }
        public Boolean AddImage(String url, String shortname, UInt64 skin = 0)
        {
            return (Boolean)ImageLibrary?.Call("AddImage", url, shortname, skin);
        }

        private void RemoveBalance(UInt64 userID, Int32 Balance)
        {
            if (IQEconomic != null)
                IQEconomic?.Call("API_REMOVE_BALANCE", userID, Balance);
            else if (Economics != null)
                Economics?.Call("Withdraw", userID, Convert.ToDouble(Balance));
        }

        public static StringBuilder sb = new StringBuilder();

        private void LoadedCategory(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();

            Single FadeOut = 0.3f;
            Single FadeIn = 0.3f;

            
            container.Add(new CuiPanel
            {
                FadeOut = FadeOut,
                RectTransform = { AnchorMin = "0.309375 0.8333", AnchorMax = "0.5677084 0.8787" }, //
                Image = { FadeIn = FadeIn, Color = "0 0 0 0" }
            }, CRAFTSYSTEM_HUD, "CATEGORY_PANEL");


            Int32 Category = 0;
            foreach (CategoryUI Categorys in CategoryList)
            {
                container.Add(new CuiElement
                {
                    FadeOut = FadeOut,
                    Parent = "CATEGORY_PANEL",
                    Name = Categorys.PngKey,
                    Components =
                        {
                            new CuiRawImageComponent { FadeIn = FadeIn, Png = GetImage(Categorys.PngKey) },
                            new CuiRectTransformComponent{ AnchorMin = $"{0.03225803 + (Category * 0.098)} 0.1224494", AnchorMax = $"{0.09677408 + (Category * 0.098)} 0.7755101"},
                        }
                });
                container.Add(new CuiButton
                {
                    FadeOut = FadeOut,
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { FadeIn = FadeIn, Command = Categorys.Command, Color = "0 0 0 0" },
                    Text = { FadeIn = FadeIn, Text = "", Align = TextAnchor.MiddleCenter }
                }, Categorys.PngKey, Categorys.PngKey + "BUTTON");
		   		 		  						  	   		  		 			   		 		   		 		  	 	 
                Category++;
            }
		   		 		  						  	   		  		 			   		 		   		 		  	 	 
            
            CuiHelper.AddUi(player, container);
        }
        private class Configuration
        {
            [JsonProperty(LanguageEn ? "Plugin interface customization" : "Настройка интерфейса плагина")]
            public InterfaceSettings InterfaceSetting = new InterfaceSettings();
            [JsonProperty(LanguageEn ? "Setting up compatible plugins" : "Настройка совместимых плагинов")]
            public ReferencePlugin ReferencePlugins = new ReferencePlugin();
            [JsonProperty(LanguageEn ? "Customization of items that can be crafted" : "Настройка предметов, которые возможно скрафтить")]
            public Dictionary<String, ItemSettings> ItemSetting = new Dictionary<String, ItemSettings>();

                        internal class ItemSettings
            {
                [JsonProperty(LanguageEn ? "Cooldown for crafting an item (If it is not needed, set the value to 0" : "Перезарядка на крафт предмета (Если она не нужна, поставьте значение 0")]
                public Int32 Cooldown;
                [JsonProperty(LanguageEn ? "Permissions to access this craft (leave the field blank - it will be available to everyone)" : "Права для доступа к этому крафту (оставьте поле пустым - будет доступно всем)")]
                public String Permission;
                [JsonProperty(LanguageEn ? "What category does this item belong to: 0 - Weapons, 1 - Tools, 2 - Structures, 3 - Items, 4 - Clothing, 5 - Electricity, 6 - Vehicles, 7 - Fun, 8 - Custom (commands), 9 - Other ( Crafts prefabs and items like Recycler)" : "К какой категории относится данный предмет : 0 - Оружие, 1 - Инструменты, 2 - Конструкции, 3 - Итемы, 4 - Одежда, 5 - Электричество, 6 - Транспорт, 7 - Фановые, 8 - Кастомные(команды), 9 - Иные(Крафтит префабы и предметы по типу Переработчика)")]
                public CategoryItem CategoryItems;
                [JsonProperty(LanguageEn ? "Display name" : "Отображаемое имя")]
                public String DisplayName;
                [JsonProperty(LanguageEn ? "Description (Optional)" : "Описание (Необязательно)")]
                public String Description;
                [JsonProperty(LanguageEn ? "Number of items" : "Количество предметов")]
                public Int32 AmountCraft;
                [JsonProperty(LanguageEn ? "Item HP (Suitable for category: 9 - Other (Crafts prefabs and items like Refiner) and 6 - Vehicles)" : "ХП предмета (Подходит к категории : 9 - Иные(Крафтит префабы и предметы по типу Переработчика) и 6 - Транспорт)")]
                public Int32 HealthItem;
                [JsonProperty(LanguageEn ? "The ability to pick up an item in inventory (true - yes / false - no) (Suitable for category: 9 - Other(Crafts prefabs and items like Recycler))" : "Возможность забрать предмет в инвентарь (true - да/false - нет) (Подходит к категории : 9 - Иные(Крафтит префабы и предметы по типу Переработчика))")]
                public Boolean UsePickUp;
                [JsonProperty(LanguageEn ? "Shortname (Suitable for all categories EXCEPT: 6 - Vehicles and 8 - Custom (commands) and 9 - Others (Crafts prefabs and items like Recycler)" : "Shortname (Подходит ко всем категориям КРОМЕ : 6 - Транспорт и 8 - Кастомные(команды) и 9 - Иные(Крафтит префабы и предметы по типу Переработчика)")]
                public String Shortname;
                [JsonProperty(LanguageEn ? "Skin ID (Suitable for all categories (If you use category 6 - Transport or - 9 - Other, be sure to set the Skin ID for the icon)" : "SkinID (Подходит ко всем категориям (Если вы используете категорию 6 - Транспорт или - 9 - Иные , обязательно устанавливайте SkinID для иконки)")]
                public UInt64 SkinID;
                [JsonProperty(LanguageEn ? "PNG (Only suitable for the category: 6 - Vehicles and 8 - Custom (commands) and 9 - Others (Crafts prefabs and items like Recycler))" : "PNG (Подходит только к категории : 6 - Транспорт и 8 - Кастомные(команды) и 9 - Иные(Крафтит префабы и предметы по типу Переработчика))")]
                public String PNG;
                [JsonProperty(LanguageEn ? "Team (Only applies to category: 8 - Custom (teams) %STEAMID% - will be replaced by the player&#39;s Steam64ID)" : "Команда (Подходит только к категории : 8 - Кастомные(команды) %STEAMID% - заменится на Steam64ID игрока)")]
                public String Command;
                [JsonProperty(LanguageEn ? "Vehicle prefab (Only applies to category 6 - Vehicles)" : "Префаб для транспорта (Подходит только к категории 6 - Транспорт)")]
                public String PrefabNameTransport;
                [JsonProperty(LanguageEn ? "Prefab for items (Only suitable for category 9 - Other (Crafts prefabs and items like Recycler))" : "Префаб для предметов (Подходит только к категории 9 - Иные(Крафтит префабы и предметы по типу Переработчика))")]
                public String PrefabEntity;
                [JsonProperty(LanguageEn ? "What level of workbench is required for crafting, if no workbench is required, set to 0" : "Какой уровень верстака требуется для крафта нужен, если верстак не требуется, установите 0")]
                public Int32 WorkBenchLevel;
                [JsonProperty(LanguageEn ? "IQEconomic/Economics : How much currency is required to craft this item (Set to 0 if not required)" : "IQEconomic/Economics : Сколько требуется валюты для крафта данного предмета(Устновите 0 если не требуется)")]
                public Int32 IQEconomicPrice;
                [JsonProperty(LanguageEn ? "IQPlague Skill : Whether a neutral IQPlague Skill is required to craft this item (true - yes/false - no)" : "IQPlagueSkill : Требуется ли нейтральный навык в IQPlagueSkill для крафта данного предмета(true - да/false - нет)")]
                public Boolean IQPlagueSkillCraft;
                [JsonProperty(LanguageEn ? "IQRank System : Specify the rank required to craft this item (Leave blank if not required)" : "IQRankSystem : Укажите ранг, который требуется для крафта данного предмета(Если не нужно, оставьте поле пустым)")]
                public String IQRankSystemRank;
                [JsonProperty(LanguageEn ? "List of items required for crafting" : "Список предметов требующихся для крафта")]
                public List<ItemForCraft> ItemListForCraft = new List<ItemForCraft>();
                internal class ItemForCraft
                {
                    [JsonProperty("Shortname")]
                    public String Shortname;
                    [JsonProperty(LanguageEn ? "Amount" : "Количество")]
                    public Int32 Amount;
                    [JsonProperty(LanguageEn ? "Skin ID if required" : "SkinID если требуется")]
                    public UInt64 SkinID;
                    [JsonProperty(LanguageEn ? "PNG for custom items (don&#39;t forget to set Skin ID)" : "PNG для кастомных предметов (не забудьте установить SkinID)")]
                    public String PNG;
                }
            }
            
                        internal class InterfaceSettings
            {
                [JsonProperty(LanguageEn ? "General settings" : "Основные настройки")]
                public GeneralSettings GeneralSetting = new GeneralSettings();
                [JsonProperty(LanguageEn ? "Category settings" : "Настройка категорий")]
                public CategorySettings CategorySetting = new CategorySettings();
                [JsonProperty(LanguageEn ? "Requirements setup" : "Настройка требований")]
                public RequiresSettings RequiresSetting = new RequiresSettings();
                internal class GeneralSettings
                {
                    [JsonProperty(LanguageEn ? "Link to the picture of the selected item" : "Ссылка на картинку выбранного предмета")]
                    public String PNGActive;
                    [JsonProperty(LanguageEn ? "Link to a picture of an unselected item" : "Ссылка на картинку не выбранного предмета")]
                    public String PNGInActive;
                    [JsonProperty(LanguageEn ? "Link to the logo in the crafting menu" : "Ссылка на логотип в меню крафта")]
                    public String PNGLogo;
                    [JsonProperty(LanguageEn ? "Link to closing image" : "Ссылка на картинку закрытия")]
                    public String PNGCloseButton;
                    [JsonProperty(LanguageEn ? "HEX Main menu background" : "HEX Заднего фона главного меню")]
                    public String HexBackground;
                    [JsonProperty(LanguageEn ? "HEX Buttons CREATE" : "HEX Кнопки СОЗДАТЬ")]
                    public String HexButtonCreate;
                }
                internal class RequiresSettings
                {
                    [JsonProperty(LanguageEn ? "HEX : Color if requirement is met" : "HEX : Цвет если требование выполнено")]
                    public String RequiresDoTrueColor;
                    [JsonProperty(LanguageEn ? "HEX : Color if requirement is not met" : "HEX : Цвет если требование не выполнено")]
                    public String RequiresDoFalseColor;
                    [JsonProperty(LanguageEn ? "Complied symbol" : "Символ выполненного требования")]
                    public String RequiresDoTrue;
                    [JsonProperty(LanguageEn ? "HEX : Color if condition is not required" : "HEX : Цвет если условие не требования")]
                    public String RequiresDoFalse;
                    [JsonProperty(LanguageEn ? "PNG : Icon for workbench requirement" : "PNG : Иконка для требования верстака")]
                    public String PNGRequiresWorckbench;
                    [JsonProperty(LanguageEn ? "PNG : Icon for currency requirement from IQEconomic" : "PNG : Иконка для требования валюты с IQEconomic")]
                    public String PNGRequiresIQEconomic;
                    [JsonProperty(LanguageEn ? "PNG : Icon for rank requirement from IQRank System" : "PNG : Иконка для требования ранга с IQRankSystem")]
                    public String PNGRequiresIQRankSystem;
                    [JsonProperty(LanguageEn ? "PNG : Icon for skill requirement with IQPlague Skill" : "PNG : Иконка для требования навыка с IQPlagueSkill")]
                    public String PNGRequiresIQPlagueSkill;
                }
                internal class CategorySettings
                {
                    [JsonProperty(LanguageEn ? "Adjusting the colors of unselected icons for each category" : "Настройка цветов не выбранных иконок под каждую категорию")]
                    public ColorCategoryIcon ColorCategoryIcons = new ColorCategoryIcon();
                    [JsonProperty(LanguageEn ? "Customize PNG category icons" : "Настройка PNG иконок категорий")]
                    public PNGCategoryIcon PNGCategoryIcons = new PNGCategoryIcon();
                    [JsonProperty(LanguageEn ? "Enabling and disabling categories" : "Включения и отключения категорий")]
                    public TurnedCategory TurnedCategorys = new TurnedCategory();
                    internal class PNGCategoryIcon
                    {
                        [JsonProperty(LanguageEn ? "PNG : All items category icon" : "PNG : Иконка категории всех предметов")]
                        public String PNGAll;
                        [JsonProperty(LanguageEn ? "PNG : Weapon category icon" : "PNG : Иконка категории оружия")]
                        public String PNGWeapon;
                        [JsonProperty(LanguageEn ? "PNG : Tools category icon" : "PNG : Иконка категории инструментов")]
                        public String PNGTools;
                        [JsonProperty(LanguageEn ? "PNG : Design category icon" : "PNG : Иконка категории конструкций")]
                        public String PNGConstruction;
                        [JsonProperty(LanguageEn ? "PNG : Item category icon" : "PNG : Иконка категории предметов")]
                        public String PNGItems;
                        [JsonProperty(LanguageEn ? "PNG : Clothing category icon" : "PNG : Иконка категории одежды")]
                        public String PNGAttirie;
                        [JsonProperty(LanguageEn ? "PNG : Electrical category icon" : "PNG : Иконка категории электрики")]
                        public String PNGElectrical;
                        [JsonProperty(LanguageEn ? "PNG : Transport category icon" : "PNG : Иконка категории транспорта")]
                        public String PNGTransport;
                        [JsonProperty(LanguageEn ? "PNG : Fun category icon" : "PNG : Иконка категории фана")]
                        public String PNGFun;
                        [JsonProperty(LanguageEn ? "PNG : Custom item icon" : "PNG : Иконка кастомных предметов")]
                        public String PNGCustom;
                    }
                    internal class ColorCategoryIcon
                    {
                        [JsonProperty(LanguageEn ? "HEX : The color of the panel with the item category of all items" : "HEX : Цвет панели с предметом категории всех предметов")]
                        public String AllItemsColor;
                        [JsonProperty(LanguageEn ? "HEX : The color of the item panel of the weapon category" : "HEX : Цвет панели с предметом категории оружий")]
                        public String WeaponColor;
                        [JsonProperty(LanguageEn ? "HEX : Color of toolbar item" : "HEX : Цвет панели с предметом категории интсрументов")]
                        public String ToolsColor;
                        [JsonProperty(LanguageEn ? "HEX : Color bar with item category designs" : "HEX : Цвет панели с предметом категории конструкций")]
                        public String ConstructionColor;
                        [JsonProperty(LanguageEn ? "HEX : The color of the bar with the subject of the item category" : "HEX : Цвет панели с предметом категории предметов")]
                        public String ItemsColor;
                        [JsonProperty(LanguageEn ? "HEX : The color of the bar with the garment category item" : "HEX : Цвет панели с предметом категории одежды")]
                        public String AttirieColor;
                        [JsonProperty(LanguageEn ? "HEX : The color of the panel with the electrical category item" : "HEX : Цвет панели с предметом категории электрики")]
                        public String ElectricalColor;
                        [JsonProperty(LanguageEn ? "HEX : The color of the vehicle category item bar" : "HEX : Цвет панели с предметом категории транспорта")]
                        public String TransportColor;
                        [JsonProperty(LanguageEn ? "HEX : The color of the panel with the fan category item" : "HEX : Цвет панели с предметом категории фана")]
                        public String FunColor;
                        [JsonProperty(LanguageEn ? "HEX : Custom item bar color" : "HEX : Цвет панели с предметом категории кастомных предметов")]
                        public String CustomColor;
                    }
                    internal class TurnedCategory
                    {
                        [JsonProperty(LanguageEn ? "Enable weapon category" : "Включить категорию оружие")]
                        public Boolean TurnWeapon;
                        [JsonProperty(LanguageEn ? "Enable tool categories" : "Включить категории инструментов")]
                        public Boolean TurnTools;
                        [JsonProperty(LanguageEn ? "Enable design categories" : "Включить категории конструкций")]
                        public Boolean TurnConstruction;
                        [JsonProperty(LanguageEn ? "Enable item categories" : "Включить категории предметов")]
                        public Boolean TurnItems;
                        [JsonProperty(LanguageEn ? "Enable clothing categories" : "Включить категории одежды")]
                        public Boolean TurnAttirie;
                        [JsonProperty(LanguageEn ? "Enable electrical categories" : "Включить категории электрики")]
                        public Boolean TurnElectrical;
                        [JsonProperty(LanguageEn ? "Enable transport categories" : "Включить категории транспорта")]
                        public Boolean TurnTransport;
                        [JsonProperty(LanguageEn ? "Enable fun categories" : "Включить категории фана")]
                        public Boolean TurnFun;
                        [JsonProperty(LanguageEn ? "Enable custom items" : "Включить кастомных предметов")]
                        public Boolean TurnCustom;
                    }
                }
            }
            
            internal class ReferencePlugin
            {
                [JsonProperty(LanguageEn ? "IQChat : Setting up a chat" : "IQChat : Настройка чата")]
                public ChatSettings chatSettings = new ChatSettings();
                internal class ChatSettings
                {
                    [JsonProperty(LanguageEn ? "IQChat : Custom prefix in the chat" : "IQChat : Кастомный префикс в чате")]
                    public String CustomPrefix = "[IQCraftSystem]";
                    [JsonProperty(LanguageEn ? "IQChat : Custom avatar in the chat(If required)" : "IQChat : Кастомный аватар в чате(Если требуется)")]
                    public String CustomAvatar = "0";
                }
            }
		   		 		  						  	   		  		 			   		 		   		 		  	 	 
            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
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
                    
                                        ItemSetting = new Dictionary<String, ItemSettings>
                    {
                        ["ak47"] = new ItemSettings
                        {
                            CategoryItems = CategoryItem.Weapon,
                            Permission = "iqcraftsystem.ak47",
                            Cooldown = 0,
                            Command = "",
                            AmountCraft = 1,
                            Description = LanguageEn ? "A good weapon for shooting at long and medium distances" : "Хорошее оружие для стрельбы на дальние и средние дистанции",
                            DisplayName = "АК-47",
                            PNG = "",
                            HealthItem = 0,
                            UsePickUp = false,
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
                            Permission = "",
                            Cooldown = 5000,
                            Command = "",
                            Description = "",
                            AmountCraft = 1,
                            DisplayName = "Jackhammer",
                            PNG = "",
                            HealthItem = 0,
                            UsePickUp = false,
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
                            Permission = "",
                            Cooldown = 0,
                            Command = "",
                            AmountCraft = 1,
                            Description = "",
                            HealthItem = 0,
                            UsePickUp = false,
                            DisplayName = LanguageEn ? "Hazmatsuit" : "Хазмат",
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
                            Permission = "",
                            Cooldown = 0,
                            Command = "",
                            HealthItem = 0,
                            AmountCraft = 1,
                            UsePickUp = false,
                            Description = LanguageEn ? "It's convenient to jump off and not come back" : "Удобно спргынуть и не вернуться",
                            DisplayName = LanguageEn ? "Hatchway" : "Люк",
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
                            Permission = "",
                            Cooldown = 0,
                            Command = "say GivePrivilegy",
                            Description = LanguageEn ? "A whole privilege for 3 whole days, the best option" : "Целая привилегия на 3 целых дня, самый лучший вариант",
                            DisplayName = LanguageEn ? "VIP 3 DAYS" : "ПРИВИЛЕГИЯ 3 ДНЯ",
                            PNG = "https://i.imgur.com/vLCj3kO.png",
                            PrefabEntity = "",
                            HealthItem = 0,
                            AmountCraft = 1,
                            UsePickUp = false,
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
                            Permission = "",
                            Cooldown = 0,
                            Command = "",
                            Description = "",
                            DisplayName = LanguageEn ? "Turret" : "Турель",
                            PNG = "",
                            AmountCraft = 1,
                            HealthItem = 0,
                            UsePickUp = false,
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
                            Permission = "",
                            Cooldown = 0,
                            AmountCraft = 1,
                            Command = "",
                            HealthItem = 100,
                            UsePickUp = true,
                            Description = LanguageEn ? "Take and plant a tree" : "Возьми и всади дерево",
                            DisplayName = LanguageEn ? "Wood" : "Дерево",
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
                            Permission = "",
                            Cooldown = 0,
                            HealthItem = 0,
                            AmountCraft = 1,
                            UsePickUp = false,
                            Command = "",
                            Description = LanguageEn ? "Blow up the sky" : "Взорвик небо",
                            DisplayName = LanguageEn ? "Firework" : "Фейверк",
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
                            Permission = "",
                            HealthItem = 0,
                            UsePickUp = false,
                            Cooldown = 0,
                            Command = "",
                            Description = "",
                            AmountCraft = 1,
                            DisplayName = LanguageEn ? "Lamp on the wall" : "Светилка на стенку",
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
                            Permission = "",
                            HealthItem = 0,
                            UsePickUp = false,
                            Cooldown = 0,
                            Command = "",
                            AmountCraft = 1,
                            Description = "",
                            DisplayName = LanguageEn ? "Minicopter" : "Коптер",
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
                    
                    
                    ReferencePlugins = new ReferencePlugin
                    {
                        chatSettings = new ReferencePlugin.ChatSettings
                        {
                            CustomAvatar = "0",
                            CustomPrefix = "[IQCraftSystem]"
                        }
                    }

                                    };
            }
        }
        private IEnumerator DownloadImages()
        {
            Dictionary<String, Configuration.ItemSettings> Items = config.ItemSetting;
		   		 		  						  	   		  		 			   		 		   		 		  	 	 
            PrintWarning("AddImages SkyPlugins.ru...");
            foreach (KeyValuePair<String, Configuration.ItemSettings> Item in Items)
            {
                if (Item.Value.SkinID != 0)
                {
                    if (!HasImage($"{Item.Value.Shortname}_128px_{Item.Value.SkinID}"))
                        AddImage($"https://api.skyplugins.ru/api/getskin/{Item.Value.SkinID}/128", $"{Item.Value.Shortname}_128px_{Item.Value.SkinID}", Item.Value.SkinID);
                    if (!HasImage($"{Item.Value.Shortname}_256px_{Item.Value.SkinID}"))
                        AddImage($"https://api.skyplugins.ru/api/getskin/{Item.Value.SkinID}/128", $"{Item.Value.Shortname}_256px_{Item.Value.SkinID}", Item.Value.SkinID);
                }
                else
                {
                    if (!String.IsNullOrWhiteSpace(Item.Value.Shortname))
                    {
                        if (!HasImage($"{Item.Value.Shortname}_128px"))
                            AddImage($"https://api.skyplugins.ru/api/getimage/{Item.Value.Shortname}/128", $"{Item.Value.Shortname}_128px");
                        if (!HasImage($"{Item.Value.Shortname}_256px"))
                            AddImage($"https://api.skyplugins.ru/api/getimage/{Item.Value.Shortname}/256", $"{Item.Value.Shortname}_256px");
                    }
                }
                foreach (Configuration.ItemSettings.ItemForCraft ItemCraft in Item.Value.ItemListForCraft.Where(x => !String.IsNullOrEmpty(x.Shortname)))
                {
                    if (ItemCraft.SkinID != 0)
                    {
                        if (!HasImage($"{ItemCraft.Shortname}_128px_{ItemCraft.SkinID}"))
                            AddImage($"https://api.skyplugins.ru/api/getskin/{ItemCraft.SkinID}/128", $"{ItemCraft.Shortname}_128px_{ItemCraft.SkinID}", ItemCraft.SkinID);
                    }
                    else
                    {
                        if (!HasImage($"{ItemCraft.Shortname}_128px"))
                            AddImage($"https://api.skyplugins.ru/api/getimage/{ItemCraft.Shortname}/128", $"{ItemCraft.Shortname}_128px");
                    }
                }
            }
            yield return new WaitForSeconds(0.04f);
            PrintWarning("AddImages SkyPlugins.ru - completed..");
        }
        
                private void LoadedIcon()
        {
            PrintWarning(LanguageEn ? "I start loading icons.." : "Начинаю загрузку иконок..");
            Configuration.InterfaceSettings Interface = config.InterfaceSetting;
            Configuration.InterfaceSettings.CategorySettings.PNGCategoryIcon Category = Interface.CategorySetting.PNGCategoryIcons;
            Dictionary<String, Configuration.ItemSettings> Items = config.ItemSetting;
            Configuration.InterfaceSettings.RequiresSettings Requires = Interface.RequiresSetting;

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

            foreach (KeyValuePair<String, Configuration.ItemSettings> Icon in Items.Where(x => x.Value.CategoryItems == CategoryItem.Custom || x.Value.CategoryItems == CategoryItem.Transport || x.Value.CategoryItems == CategoryItem.Entity))
            {
                if (!HasImage($"ITEM_{Icon.Value.PNG}"))
                    AddImage(Icon.Value.PNG, $"ITEM_{Icon.Value.PNG}");
            }

            foreach (KeyValuePair<String, Configuration.ItemSettings> Item in Items)
            {
                foreach (Configuration.ItemSettings.ItemForCraft ItemCraft in Item.Value.ItemListForCraft)
                {
                    if (!HasImage($"ITEM_CRAFT_{ItemCraft.PNG}"))
                        AddImage(ItemCraft.PNG, $"ITEM_CRAFT_{ItemCraft.PNG}");
                }
            }

            ServerMgr.Instance.StartCoroutine(DownloadImages());

            PrintWarning(LanguageEn ? "Icon loading is complete" : "Загрузка иконок завершена");
        }

        private void OnPlayerAttack(BasePlayer player, HitInfo info)
        {
            if (info?.HitEntity == null || player == null)
                return;
            if (!(info.HitEntity is BaseEntity))
                return;
            BaseEntity entity = info.HitEntity;
            if (entity == null || player == null)
                return;
            if (!HealthCustom.ContainsKey(entity.net.ID.Value)) return;

            HealthCustom[entity.net.ID.Value] -= (Int32)info.damageTypes.Total();

            if (HealthCustom[entity.net.ID.Value] <= 0)
                entity.Kill();
        }
		   		 		  						  	   		  		 			   		 		   		 		  	 	 
        private void WriteData()
        {
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQCraftSystem/CraftUsers", DataCrafts);
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQCraftSystem/CraftHealth", HealthCustom);
        }
        public class DataCraft
        {
            [JsonProperty(LanguageEn ? "Item Information" : "Информация о предметах")] public Dictionary<String, Int32> CooldownCraft = new Dictionary<String, Int32>();
        }
        private void OnHammerHit(BasePlayer player, HitInfo info)
        {
            BaseEntity Entity = info.HitEntity;
            if (Entity == null) return;
		   		 		  						  	   		  		 			   		 		   		 		  	 	 
            if (Entity.OwnerID != player.userID || Entity.skinID == 0 || Entity.skinID < player.userID)
                return;

            if (!player.CanBuild())
            {
                SendChat(player, GetLang("CHAT_PICKUP_ITEM_BUILDING", player.UserIDString));
                return;
            }

            BaseCombatEntity CombatEntity = Entity.GetComponent<BaseCombatEntity>();
		   		 		  						  	   		  		 			   		 		   		 		  	 	 
            if (CombatEntity != null)
            {
                if (CombatEntity.SecondsSinceDealtDamage < 30f)
                {
                    SendChat(player, GetLang("CHAT_PICKUP_ITEM_DAMAGE", player.UserIDString));
                    return;
                }
            }

            LootContainer ContainerEntity = Entity.GetComponent<LootContainer>();
            if (ContainerEntity != null)
            {
                ContainerEntity.DropItems();
            }

            Single Health = 0;
            if (HealthCustom.ContainsKey(Entity.net.ID.Value))
            {
                Health = HealthCustom[Entity.net.ID.Value];
                HealthCustom.Remove(Entity.net.ID.Value);
            }

            Entity.Kill();

            KeyValuePair<String, Configuration.ItemSettings> Item = config.ItemSetting.FirstOrDefault(x => x.Value.SkinID == (Entity.skinID - player.userID));
            if (Item.Value == null) return;

            Item item = ItemManager.CreateByName(CustomItemShortname, 1, Item.Value.SkinID);
            if (!String.IsNullOrWhiteSpace(Item.Value.DisplayName))
                item.name = Item.Value.DisplayName;

            player.GiveItem(item);
        }
        
        
        private Int32 GetBalance(UInt64 userID)
        {
            if (IQEconomic != null)
                return (Int32)IQEconomic?.Call("API_GET_BALANCE", userID);
            else if (Economics != null)
                return Convert.ToInt32((Double)Economics?.Call("Balance", userID));
            return 0;
        }

        public String GetCooldown(BasePlayer player, String KeyItem)
        {
            String ResultCooldown = FormatTime(TimeSpan.FromSeconds(GetCooldownTime(DataCrafts[player.userID].CooldownCraft[KeyItem])), player.UserIDString);
            return ResultCooldown;
        }

        private void DestroyItemInfo(BasePlayer player)
        {
            for (Int32 ItemCount = 0; ItemCount < 15; ItemCount++)
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

        private Boolean IQRankRankReality(String RankKey)
        {
            return (Boolean)IQRankSystem?.Call("API_IS_RANK_REALITY", RankKey);
        }

        public List<CategoryUI> CategoryList = new List<CategoryUI>();
        
        public void Register(String Permissions)
        {
            if (!String.IsNullOrWhiteSpace(Permissions))
            {
                if (!permission.PermissionExists(Permissions, this))
                    permission.RegisterPermission(Permissions, this);
            }
        }

        private void ReadData()
        {
            DataCrafts = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Hash<UInt64, DataCraft>>("IQCraftSystem/CraftUsers");
            HealthCustom = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<UInt64, Int32>>("IQCraftSystem/CraftHealth");
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        public Dictionary<UInt64, Int32> HealthCustom = new Dictionary<UInt64, Int32>();

        private void CachingImage(BasePlayer player)
        {
            Configuration.InterfaceSettings Interface = config.InterfaceSetting;
            Configuration.InterfaceSettings.CategorySettings.PNGCategoryIcon Category = Interface.CategorySetting.PNGCategoryIcons;
            Dictionary<String, Configuration.ItemSettings> Items = config.ItemSetting;
            Configuration.InterfaceSettings.RequiresSettings Requires = Interface.RequiresSetting;

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

            foreach (KeyValuePair<String, Configuration.ItemSettings> Icon in Items.Where(x => x.Value.CategoryItems == CategoryItem.Custom || x.Value.CategoryItems == CategoryItem.Transport || x.Value.CategoryItems == CategoryItem.Entity))
                SendImage(player, $"ITEM_{Icon.Value.PNG}");

            foreach (KeyValuePair<String, Configuration.ItemSettings> Item in Items)
            {
                foreach (Configuration.ItemSettings.ItemForCraft ItemCraft in Item.Value.ItemListForCraft)
                    SendImage(player, $"ITEM_CRAFT_{ItemCraft.PNG}");
            }
        }

        public Boolean DoSkillIQPlagueSkill(BasePlayer player)
        {
            return IQPlagueSkillISAdvanced(player);
        }
		   		 		  						  	   		  		 			   		 		   		 		  	 	 
        
                private Int32 CountRequires(String ItemKey)
        {
            Configuration.ItemSettings ItemRequires = config.ItemSetting[ItemKey];
            Int32 Requires = 0;

            if (ItemRequires.IQPlagueSkillCraft)
                Requires++;
            if (!String.IsNullOrWhiteSpace(ItemRequires.IQRankSystemRank))
                Requires++;
            if (ItemRequires.IQEconomicPrice != 0)
                Requires++;
            if (ItemRequires.WorkBenchLevel != 0)
                Requires++;

            return Requires;
        }

        private void CategoryListLoad()
        {
            Configuration.InterfaceSettings Interface = config.InterfaceSetting;
            Configuration.InterfaceSettings.CategorySettings.PNGCategoryIcon Category = Interface.CategorySetting.PNGCategoryIcons;
            Configuration.InterfaceSettings.CategorySettings.TurnedCategory TurnedCategory = Interface.CategorySetting.TurnedCategorys;
            String CategoryPngKey = "CATEGORY_{0}";

            CategoryList.Add(new CategoryUI { Command = $"func_craft select_category {CategoryItem.All:d}", PngKey = String.Format(CategoryPngKey, Category.PNGAll) });
            if (TurnedCategory.TurnAttirie)
                CategoryList.Add(new CategoryUI { Command = $"func_craft select_category {CategoryItem.Attirie:d}", PngKey = String.Format(CategoryPngKey, Category.PNGAttirie) });
            if (TurnedCategory.TurnConstruction)
                CategoryList.Add(new CategoryUI { Command = $"func_craft select_category {CategoryItem.Construction:d}", PngKey = String.Format(CategoryPngKey, Category.PNGConstruction) });
            if (TurnedCategory.TurnCustom)
                CategoryList.Add(new CategoryUI { Command = $"func_craft select_category {CategoryItem.Custom:d}", PngKey = String.Format(CategoryPngKey, Category.PNGCustom) });
            if (TurnedCategory.TurnElectrical)
                CategoryList.Add(new CategoryUI { Command = $"func_craft select_category {CategoryItem.Electrical:d}", PngKey = String.Format(CategoryPngKey, Category.PNGElectrical) });
            if (TurnedCategory.TurnFun)
                CategoryList.Add(new CategoryUI { Command = $"func_craft select_category {CategoryItem.Fun:d}", PngKey = String.Format(CategoryPngKey, Category.PNGFun) });
            if (TurnedCategory.TurnItems)
                CategoryList.Add(new CategoryUI { Command = $"func_craft select_category {CategoryItem.Items:d}", PngKey = String.Format(CategoryPngKey, Category.PNGItems) });
            if (TurnedCategory.TurnTools)
                CategoryList.Add(new CategoryUI { Command = $"func_craft select_category {CategoryItem.Tools:d}", PngKey = String.Format(CategoryPngKey, Category.PNGTools) });
            if (TurnedCategory.TurnTransport)
                CategoryList.Add(new CategoryUI { Command = $"func_craft select_category {CategoryItem.Transport:d}", PngKey = String.Format(CategoryPngKey, Category.PNGTransport) });
            if (TurnedCategory.TurnWeapon)
                CategoryList.Add(new CategoryUI { Command = $"func_craft select_category {CategoryItem.Weapon:d}", PngKey = String.Format(CategoryPngKey, Category.PNGWeapon) });
        }
        
        
                private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<String, String>
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

                ["CHAT_PICKUP_ITEM_BUILDING"] = "You can't pick up other people's items!",
                ["CHAT_PICKUP_ITEM_DAMAGE"] = "You can't pick up an item, it was recently attacked",

                ["TITLE_FORMAT_LOCKED_DAYS"] = "<size=12><b>D</b></size>",
                ["TITLE_FORMAT_LOCKED_HOURSE"] = "<size=12><b>H</b></size>",
                ["TITLE_FORMAT_LOCKED_MINUTES"] = "<size=12><b>M</b></size>",
                ["TITLE_FORMAT_LOCKED_SECONDS"] = "<size=12><b>S</b></size>",

            }, this);

            lang.RegisterMessages(new Dictionary<String, String>
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

                ["CHAT_PICKUP_ITEM_BUILDING"] = "Вы не можете поднимать чужие предметы!",
                ["CHAT_PICKUP_ITEM_DAMAGE"] = "Вы не можете поднять предмет, он был недавно атакован",

                ["TITLE_FORMAT_LOCKED_DAYS"] = "<size=12><b>Д</b></size>",
                ["TITLE_FORMAT_LOCKED_HOURSE"] = "<size=12><b>Ч</b></size>",
                ["TITLE_FORMAT_LOCKED_MINUTES"] = "<size=12><b>М</b></size>",
                ["TITLE_FORMAT_LOCKED_SECONDS"] = "<size=12><b>С</b></size>",
		   		 		  						  	   		  		 			   		 		   		 		  	 	 
            }, this, "ru");
            PrintWarning(LanguageEn ? "The language file was uploaded successfully" : "Языковой файл загружен успешно");
        }

        [ConsoleCommand("craft_give")]
        private void IQCraftSystemCmdGiveItem(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !arg.Player().IsAdmin) return;

            if (arg == null || arg.Args == null || arg.Args.Length < 2)
            {
                PrintWarning(LanguageEn ? "Use the syntax : craft_game Steam ID Key(from cfg)" : "Используйте синтаксис : craft_give SteamID Ключ(из кфг)");
                return;
            }
            if (arg.Args[0] == null || String.IsNullOrWhiteSpace(arg.Args[0]))
            {
                PrintWarning(LanguageEn ? "You incorrectly specified SteamID\nUse syntax : craft_game Steam ID Key(from cfg" :  "Вы неверно указали SteamID\nИспользуйте синтаксис : craft_give SteamID Ключ(из кфг))");
                return;
            }
            UInt64 userID = UInt64.Parse(arg.Args[0]);
            if (arg.Args[1] == null || String.IsNullOrWhiteSpace(arg.Args[1]))
            {
                PrintWarning(LanguageEn ? "You incorrectly specified the key from the kfc\nUse the syntax: craft_game Steam ID Key(from the kfc)" : "Вы неверно указали ключ из кфг\nИспользуйте синтаксис : craft_give SteamID Ключ(из кфг)");
                return;
            }
            String ItemKey = arg.Args[1];
            if (!config.ItemSetting.ContainsKey(ItemKey))
            {
                PrintWarning(LanguageEn ? "Such a key does not exist in the configuration, use the correct key!" : "Такого ключа не существует в конфигурации, используйте верный ключ!");
                return;
            }
            
            GiveItemUser(userID, ItemKey);
        }

        private Boolean Is_Full_Item_Pack(BasePlayer player, List<Configuration.ItemSettings.ItemForCraft> itemForCraftList)
        {
            Int32 TrueItem = 0;
            for (Int32 i = 0; i < itemForCraftList.Count; i++)
            {
                Configuration.ItemSettings.ItemForCraft Item = itemForCraftList[i];
                if (IS_Item_Player(player, Item.Shortname, Item.Amount, Item.SkinID))
                    TrueItem++;
            }

            return TrueItem >= itemForCraftList.Count;
        }

        private void UI_Information_Craft(BasePlayer player, String ItemKey)
        {
            DestroyItemInfo(player);
            Configuration.InterfaceSettings Interface = config.InterfaceSetting;
            Configuration.InterfaceSettings.RequiresSettings Require = Interface.RequiresSetting;
            Configuration.ItemSettings Item = config.ItemSetting[ItemKey];
            CategoryItem CategoryItem = Item.CategoryItems;
            String PNGIcon = CategoryItem == CategoryItem.Transport || CategoryItem == CategoryItem.Custom || CategoryItem == CategoryItem.Entity ? GetImage($"ITEM_{Item.PNG}") : Item.SkinID != 0 ? GetImage($"{Item.Shortname}_256px_{Item.SkinID}", Item.SkinID) : GetImage($"{Item.Shortname}_256px");
            Single FadeOut = 0.3f;
            Single FadeIn = 0.3f;

            CuiElementContainer container = new CuiElementContainer
            {
                {
                    new CuiPanel
                    {
                        FadeOut = FadeOut,
                        RectTransform = { AnchorMin = "0.5947914 0.1444444", AnchorMax = "0.896875 0.8296297" },
                        Image = { FadeIn = FadeIn, Color = "0 0 0 0" }
                    },
                    CRAFTSYSTEM_HUD,
                    "CRAFT_INFORMATION_PANEL"
                },
		   		 		  						  	   		  		 			   		 		   		 		  	 	 
                new CuiElement
                {
                    FadeOut = FadeOut,
                    Parent = $"CRAFT_INFORMATION_PANEL",
                    Name = $"ICON_CRAFT_INFO",
                    Components =
                    {
                        new CuiRawImageComponent { FadeIn = FadeIn, Png = PNGIcon },
                        new CuiRectTransformComponent{ AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-140 -130", OffsetMax = "100 80" },
                    }
                },
		   		 		  						  	   		  		 			   		 		   		 		  	 	 
                {
                    new CuiLabel
                    {
                        FadeOut = FadeOut,
                        RectTransform = { AnchorMin = "0.03490222 0.9189188", AnchorMax = "1 1" },
                        Text = { FadeIn = FadeIn, Text = $"<b>{Item.DisplayName}</b>", FontSize = 30, Align = TextAnchor.UpperLeft }
                    },
                    "CRAFT_INFORMATION_PANEL",
                    "TITLE_CRAFT_INFO"
                },

                {
                    new CuiLabel
                    {
                        FadeOut = FadeOut,
                        RectTransform = { AnchorMin = "0.03945446 0.8189189", AnchorMax = "1 0.9189186" },
                        Text = { FadeIn = FadeIn, Text = $"{Item.Description}", FontSize = 15, Align = TextAnchor.UpperLeft }
                    },
                    "CRAFT_INFORMATION_PANEL",
                    "DESCRIPTION_CRAFT_INFO"
                }
            };

            
            Int32 Requires = 0;
            Int32 RequiresYes = 0;
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
                    String StatusWorkBenchLevel = DoWorkbenchLevel(player, Item.WorkBenchLevel) ? $"<color={Interface.RequiresSetting.RequiresDoTrueColor}>{Interface.RequiresSetting.RequiresDoTrue}</color>" : $"<color={Interface.RequiresSetting.RequiresDoFalseColor}>{Interface.RequiresSetting.RequiresDoFalse}</color>";

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
                {
                    if (Item.IQPlagueSkillCraft)
                    {
                        String StatusIQPlagueSkill = DoSkillIQPlagueSkill(player) ? $"<color={Interface.RequiresSetting.RequiresDoTrueColor}>{Interface.RequiresSetting.RequiresDoTrue}</color>" : $"<color={Interface.RequiresSetting.RequiresDoFalseColor}>{Interface.RequiresSetting.RequiresDoFalse}</color>";

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
                }

                if (IQEconomic || Economics)
                {
                    if (Item.IQEconomicPrice != 0)
                    {
                        String StatusIQEconomicPrice = DoBalanceIQEconomic(player, Item.IQEconomicPrice) ? $"<color={Interface.RequiresSetting.RequiresDoTrueColor}>{Interface.RequiresSetting.RequiresDoTrue}</color>" : $"<color={Interface.RequiresSetting.RequiresDoFalseColor}>{Interface.RequiresSetting.RequiresDoFalse}</color>";

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
                }

                if (IQRankSystem)
                {
                    if (!String.IsNullOrWhiteSpace(Item.IQRankSystemRank))
                    {
                        if (!IQRankRankReality(Item.IQRankSystemRank))
                            PrintError($"Вы указали не существующий ранг ({Item.IQRankSystemRank}), в плагине IQRankSystem не обнаружен этот ранг! Проверьте данные!");

                        String StatusIQRankSystemRank = DoRankIQRankSystem(player, Item.IQRankSystemRank) ? $"<color={Interface.RequiresSetting.RequiresDoTrueColor}>{Interface.RequiresSetting.RequiresDoTrue}</color>" : $"<color={Interface.RequiresSetting.RequiresDoFalseColor}>{Interface.RequiresSetting.RequiresDoFalse}</color>";

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
            }
            
                        List<Configuration.ItemSettings.ItemForCraft> RequiresItemList = Item.ItemListForCraft;

            if (RequiresItemList != null)
            {
                if (Is_Full_Item_Pack(player, RequiresItemList) && RequiresYes >= Requires)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0.6379313 0.001351424", AnchorMax = "0.9965519 0.06486491" },
                        Button = { Command = $"func_craft craft_item {ItemKey}", Color = HexToRustFormat(Interface.GeneralSetting.HexButtonCreate) },
                        Text = { Text = GetLang("UI_CREATE_ITEM", player.UserIDString), Align = TextAnchor.MiddleCenter }
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

                                Int32 ItemCount = 0;
                Single itemMinPosition = 219f;
                Single itemWidth = 0.413646f - 0.25f; /// Ширина
                Single itemMargin = 0.439895f - 0.42f; /// Расстояние между 
                Int32 itemCount = RequiresItemList.Count;
                Single itemMinHeight = 0.7f; // Сдвиг по вертикали
                Single itemHeight = 0.3f; /// Высота
                Int32 ItemTarget = 5;

                if (itemCount > ItemTarget)
                {
                    itemMinPosition = 0.5f - ItemTarget / 2f * itemWidth - (ItemTarget - 1) / 2f * itemMargin;
                    itemCount -= ItemTarget;
                }
                else
                {
                    itemMinPosition = 0.5f - itemCount / 2f * itemWidth - (itemCount - 1) / 2f * itemMargin;
                }

                
                foreach (Configuration.ItemSettings.ItemForCraft ItemCraft in RequiresItemList)
                {
                    String PNGIconCraft = !String.IsNullOrWhiteSpace(ItemCraft.PNG) ? GetImage($"ITEM_CRAFT_{ItemCraft.PNG}") : ItemCraft.SkinID != 0 ? GetImage($"{ItemCraft.Shortname}_128px_{ItemCraft.SkinID}", ItemCraft.SkinID) : GetImage($"{ItemCraft.Shortname}_128px");

                    container.Add(new CuiPanel
                    {
                        FadeOut = FadeOut,
                        RectTransform = { AnchorMin = $"{itemMinPosition} {itemMinHeight}", AnchorMax = $"{itemMinPosition + itemWidth} {itemMinHeight + itemHeight}" },
                        Image = { FadeIn = FadeIn, Color = "0 0 0 0" }
                    }, $"REQUIRES_ITEM_LIST_PANEL", $"REQUIRES_ITEM_{ItemCount}");
		   		 		  						  	   		  		 			   		 		   		 		  	 	 
                    String HEXRequiresLogo = IS_Item_Player(player, ItemCraft.Shortname, ItemCraft.Amount, ItemCraft.SkinID) ? Require.RequiresDoTrueColor : Require.RequiresDoFalseColor;
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

                    //container.Add(new CuiLabel
                    //{
                    //    FadeOut = FadeOut,
                    //    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    //    Text = { FadeIn = FadeIn, Text = $"x{ItemCraft.Amount}", FontSize = 13, Align = TextAnchor.MiddleCenter }
                    //}, $"REQUIRES_ITEM_ICO_{ItemCount}", $"REQUIRES_ITEM_AMOUNT_{ItemCount}");

                    container.Add(new CuiElement
                    {
                        FadeOut = FadeOut,
                        Parent = $"REQUIRES_ITEM_ICO_{ItemCount}",
                        Name = $"REQUIRES_ITEM_AMOUNT_{ItemCount}",
                        Components =
                        {
                            new CuiTextComponent { FadeIn = FadeIn, Text = $"x{ItemCraft.Amount}", FontSize = 13, Align = TextAnchor.MiddleCenter },
                            new CuiRectTransformComponent{ AnchorMin = "0 0", AnchorMax = "1 1"},
                            new CuiOutlineComponent { Distance = "0.8 0.8", Color = HexToRustFormat("#222226") }
                        }
                    });

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
                        else
                        {
                            itemMinPosition = 0.5f - itemCount / 2f * itemWidth - (itemCount - 1) / 2f * itemMargin;
                        }
                    }

                    if (ItemCount >= 15) break;
                    
                }
            }

            
            CuiHelper.AddUi(player, container);
        }
        public class CategoryUI
        {
            public String PngKey;
            public String Command;
        }
        
                public Boolean DoWorkbenchLevel(BasePlayer player, Int32 NeededWorkbenchLevel)
        {
            return player.currentCraftLevel >= NeededWorkbenchLevel;
        }
        private readonly Dictionary<BasePlayer, CategoryItem> CategoryActive = new Dictionary<BasePlayer, CategoryItem>();

                public void SendChat(BasePlayer player, String Message, Chat.ChatChannel channel = Chat.ChatChannel.Global)
        {
            Configuration.ReferencePlugin.ChatSettings Chat = config.ReferencePlugins.chatSettings;
            if (IQChat)
                IQChat?.Call("API_ALERT_PLAYER", player, Message, Chat.CustomPrefix, Chat.CustomAvatar);
            else player.SendConsoleCommand("chat.add", channel, 0, Message);
        }
        
                [JsonProperty(LanguageEn ? "Craft Information" : "Информация о крафте")] public Hash<UInt64, DataCraft> DataCrafts = new Hash<UInt64, DataCraft>();

        private static Double CurrentTime() { return DateTime.UtcNow.Subtract(epoch).TotalSeconds; }

        private String Format(Int32 units, String form1, String form2, String form3)
        {
            Int32 tmp = units % 10;

            if (units >= 5 && units <= 20 || tmp >= 5 && tmp <= 9)
                return $"{units}{form1}";

            if (tmp >= 2 && tmp <= 4)
                return $"{units}{form2}";

            return $"{units}{form3}";
        }

        private static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);

        private void OnServerInitialized()
        {
            LoadedIcon();

            foreach (BasePlayer p in BasePlayer.activePlayerList)
                OnPlayerConnected(p);

            foreach (KeyValuePair<String, Configuration.ItemSettings> Item in config.ItemSetting)
                Register(Item.Value.Permission);

            CategoryListLoad();
        }
        
        
        public static String CRAFTSYSTEM_HUD = "CRAFTSYSTEM_HUD";
        public Double GetCooldownTime(Int32 Cooldown)
        {
            return (Cooldown - CurrentTime());
        }

        
                private static Configuration config = new Configuration();
        
                private String GetImage(String fileName, UInt64 skin = 0)
        {
            String imageId = (String)plugins.Find("ImageLibrary").CallHook("GetImage", fileName, skin);
            if (!String.IsNullOrEmpty(imageId))
                return imageId;
            return String.Empty;
        }

        private Boolean IsRemovedBalance(UInt64 userID, Int32 Amount)
        {
            if (IQEconomic != null)
                return (Boolean)IQEconomic?.Call("API_IS_REMOVED_BALANCE", userID, Amount);
            else if (Economics != null)
                return GetBalance(userID) >= Amount;
            return false;
        }
        public Dictionary<BasePlayer, Int32> PagePlayers = new Dictionary<BasePlayer, Int32>();

        
        private void SpawnItem(BaseEntity entity, UInt64 OwnerID)
        {
            if (entity == null) return;
            Configuration.ItemSettings ItemSpawn = config.ItemSetting.FirstOrDefault(x => x.Value.SkinID == entity.skinID).Value;
            if (ItemSpawn == null) return;
            if (ItemSpawn.CategoryItems == CategoryItem.Entity || ItemSpawn.CategoryItems == CategoryItem.Transport)
            {
                String Prefab = ItemSpawn.CategoryItems == CategoryItem.Transport ? ItemSpawn.PrefabNameTransport : ItemSpawn.PrefabEntity;
                BaseEntity SpawnedEntity = GameManager.server.CreateEntity(Prefab, entity.transform.position, entity.transform.rotation *= Quaternion.Euler(0f, 90f, 0f)/*Quaternion.Normalize(Quaternion.Inverse(entity.transform.rotation))*/);
                if (SpawnedEntity == null) return;

                if (ItemSpawn.CategoryItems == CategoryItem.Entity)
                {
                    if (ItemSpawn.UsePickUp)
                    {
                        SpawnedEntity.OwnerID = OwnerID;
                        SpawnedEntity.skinID = OwnerID + ItemSpawn.SkinID;
                    }
                }

                SpawnedEntity.Spawn();

                BaseCombatEntity CombatEntity = SpawnedEntity.GetComponent<BaseCombatEntity>();
                if (CombatEntity != null && ItemSpawn.HealthItem != 0)
                {
                    CombatEntity.SetMaxHealth(ItemSpawn.HealthItem);
                    CombatEntity.SetHealth(ItemSpawn.HealthItem);
                    entity.SendNetworkUpdate();

                    if (ItemSpawn.CategoryItems == CategoryItem.Entity)
                    {
                        if (!HealthCustom.ContainsKey(SpawnedEntity.net.ID.Value))
                            HealthCustom.Add(SpawnedEntity.net.ID.Value, ItemSpawn.HealthItem);
                    }
                }


                NextTick(() => entity.Kill());
            }
        }

        public Boolean IsCooldown(BasePlayer player, String KeyItem)
        {
            if (!DataCrafts.ContainsKey(player.userID))
            {
                RegisteredDataUser(player.userID);
                return false;
            }

            if (!DataCrafts[player.userID].CooldownCraft.ContainsKey(KeyItem))
                return false;

            if (GetCooldownTime(DataCrafts[player.userID].CooldownCraft[KeyItem]) > 0)
            {
                return true;
            }
            else
            {
                DataCrafts[player.userID].CooldownCraft.Remove(KeyItem);
                return false;
            }
        }
        public String GetLang(String LangKey, String userID = null, params System.Object[] args)
        {
            sb.Clear();
            if (args != null)
            {
                sb.AppendFormat(lang.GetMessage(LangKey, this, userID), args);
                return sb.ToString();
            }
            return lang.GetMessage(LangKey, this, userID);
        }
        /// <summary>
        /// Обновление 1.1.х
        /// - Поправил ротацию предмета
        /// - Исправил проблему с кликерами во время крафта и дропом требующего предмета
        /// - Исправил KeyNotFoundException в CraftingItem
        /// - Добавлена мультиязычность в конфигурации

        private const Boolean LanguageEn = false;
        
                private Boolean IQPlagueSkillISAdvanced(BasePlayer player)
        {
            return (Boolean)IQPlagueSkill?.Call("API_IS_ADVANCED_CRAFT", player);
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                DestroyAll(player);
            ServerMgr.Instance.StopCoroutine(DownloadImages());

            WriteData();
        }
        
        
                public void SetCooldown(BasePlayer player, String KeyItem)
        {
            Int32 Cooldown = Convert.ToInt32(config.ItemSetting[KeyItem].Cooldown + CurrentTime());
            if (Cooldown - CurrentTime() <= 1) return;

            if (!DataCrafts.ContainsKey(player.userID))
                RegisteredDataUser(player.userID);

            if (!DataCrafts[player.userID].CooldownCraft.ContainsKey(KeyItem))
                DataCrafts[player.userID].CooldownCraft.Add(KeyItem, Cooldown);
            else DataCrafts[player.userID].CooldownCraft[KeyItem] = Cooldown;
        }
        public String FormatTime(TimeSpan time, String UserID)
        {
            String Result = String.Empty;
            String Days = GetLang("TITLE_FORMAT_LOCKED_DAYS", UserID);
            String Hourse = GetLang("TITLE_FORMAT_LOCKED_HOURSE", UserID);
            String Minutes = GetLang("TITLE_FORMAT_LOCKED_MINUTES", UserID);
            String Seconds = GetLang("TITLE_FORMAT_LOCKED_SECONDS", UserID);
		   		 		  						  	   		  		 			   		 		   		 		  	 	 
            if (time.Seconds != 0)
                Result = $"{Format(time.Seconds, Seconds, Seconds, Seconds)}";

            if (time.Minutes != 0)
                Result = $"{Format(time.Minutes, Minutes, Minutes, Minutes)}";

            if (time.Hours != 0)
                Result = $"{Format(time.Hours, Hourse, Hourse, Hourse)}";

            if (time.Days != 0)
                Result = $"{Format(time.Days, Days, Days, Days)}";

            return Result;
        }

        private String IQRankSystemGetName(String RankKey)
        {
            return (String)IQRankSystem?.Call("API_GET_RANK_NAME", RankKey);
        }

        protected override void LoadDefaultConfig()
        {
            config = Configuration.GetNewConfiguration();
        }

        private void UI_CraftMenu(BasePlayer player)
        {
            DestroyAll(player);
            Configuration.InterfaceSettings Interface = config.InterfaceSetting;
            Single FadeOut = 0.3f;
            Single FadeIn = 0.3f;

            CuiElementContainer container = new CuiElementContainer
            {
                {
                    new CuiPanel
                    {
                        FadeOut = FadeOut,
                        CursorEnabled = true,
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.GeneralSetting.HexBackground), Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }
                    },
                    "Overlay",
                    CRAFTSYSTEM_HUD
                },

                                {
                    new CuiLabel
                    {
                        FadeOut = FadeOut,
                        RectTransform = { AnchorMin = "0 0.4759259", AnchorMax = "0.265625 0.5250071" },
                        Text = { FadeIn = FadeIn, Text = GetLang("UI_TITLE", player.UserIDString), Font = "robotocondensed-bold.ttf", Align = TextAnchor.UpperCenter }
                    },
                    CRAFTSYSTEM_HUD,
                    "TITLE"
                },

                {
                    new CuiLabel
                    {
                        FadeOut = FadeOut,
                        RectTransform = { AnchorMin = "0 0.1962963", AnchorMax = "0.265625 0.4759259" },
                        Text = { FadeIn = FadeIn, Text = GetLang("UI_DESCRIPTION", player.UserIDString), Font = "robotocondensed-bold.ttf", Align = TextAnchor.UpperCenter }
                    },
                    CRAFTSYSTEM_HUD,
                    "DESCRIPTION"
                },

                new CuiElement
                {
                    FadeOut = FadeOut,
                    Parent = CRAFTSYSTEM_HUD,
                    Name = "LOGO",
                    Components =
                    {
                        new CuiRawImageComponent { FadeIn = FadeIn, Png = GetImage($"LOGO_{Interface.GeneralSetting.PNGLogo}") },
                        new CuiRectTransformComponent{ AnchorMin = "0.05729146 0.5407404", AnchorMax = $"0.1906248 0.7777775"},
                    }
                },

                
                new CuiElement
                {
                    FadeOut = FadeOut,
                    Parent = CRAFTSYSTEM_HUD,
                    Name = $"CLOSE_ICON",
                    Components =
                    {
                        new CuiRawImageComponent { FadeIn = FadeIn, Png = GetImage($"CLOSE_BUTTON_{Interface.GeneralSetting.PNGCloseButton}")},
                        new CuiRectTransformComponent{ AnchorMin = "0.9625001 0.9351854", AnchorMax = "0.9958335 0.9944" }, // 
                    }
                },

                {
                    new CuiButton
                    {
                        FadeOut = FadeOut - 0.15f,
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Button = { FadeIn = FadeIn, Command = $"func_craft close_ui", Color = "0 0 0 0" },
                        Text = { FadeIn = FadeIn, Text = "", Color = "0 0 0 0" }
                    },
                    "CLOSE_ICON",
                    "CLOSE_BTN"
                }
            };

            CuiHelper.AddUi(player, container);
            LoadedCategory(player);
            LoadedItems(player, CategoryItem.All, -1);
        }
		   		 		  						  	   		  		 			   		 		   		 		  	 	 
                [PluginReference] private readonly Plugin ImageLibrary, IQEconomic, IQPlagueSkill, IQRankSystem, IQChat, Economics;
        
                private Boolean IQRankSystemAvaliability(BasePlayer player, String RankKey)
        {
            return (Boolean)IQRankSystem?.Call("API_GET_AVAILABILITY_RANK_USER", player.userID, RankKey);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            CachingImage(player);
            RegisteredDataUser(player.userID);
        }

        private void GiveItemUser(UInt64 userID, String ItemKey)
        {
            Configuration.ItemSettings Item = config.ItemSetting[ItemKey];
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

        private void RegisteredDataUser(UInt64 player)
        {
            Boolean IsUsedCooldonw = config.ItemSetting.Count(x => x.Value.Cooldown > 0) != 0;
            if (!IsUsedCooldonw) return;
            if (!DataCrafts.ContainsKey(player))
                DataCrafts.Add(player, new DataCraft { CooldownCraft = new Dictionary<String, Int32> { } });
        }

        [ConsoleCommand("func_craft")]
        private void IQCraftSsytemFunc(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;

            String Action = arg.Args[0];
            switch (Action)
            {
                case "close_ui":
                    {
                        DestroyAll(player);
                        break;
                    }
                case "select_category":
                    {
                        CategoryItem CategoryTake = (CategoryItem)Int32.Parse(arg.Args[1]);
                        PagePlayers[player] = 0;
                        DestroyItem(player);
                        LoadedItems(player, CategoryTake);
                        break;
                    }
                case "select_item":
                    {
                        CategoryItem CategoryTake = (CategoryItem)Int32.Parse(arg.Args[1]);
                        Int32 SlotActive = Int32.Parse(arg.Args[2]);
                        String ItemKey = arg.Args[3];
                        DestroyItem(player);
                        LoadedItems(player, CategoryTake, SlotActive);
                        UI_Information_Craft(player, ItemKey);
                        break;
                    }
                case "craft_item":
                    {
                        String ItemKey = arg.Args[1];
                        CraftingItem(player, ItemKey);
                        break;
                    }
                case "next.page":
                    {
                        CategoryItem CategoryTake = (CategoryItem)Int32.Parse(arg.Args[1]);

                        DestroyItem(player);
                        PagePlayers[player]++;
                        LoadedItems(player, CategoryTake, 0);
                        break;
                    }
                case "back.page":
                    {
                        CategoryItem CategoryTake = (CategoryItem)Int32.Parse(arg.Args[1]);

                        DestroyItem(player);
                        PagePlayers[player]--;
                        LoadedItems(player, CategoryTake, 0);
                        break;
                    }
            }
        }
		   		 		  						  	   		  		 			   		 		   		 		  	 	 
                private void LoadedItems(BasePlayer player, CategoryItem SortCategory = CategoryItem.All, Int32 SlotActive = 0)
        {
            if (!CategoryActive.ContainsKey(player))
                CategoryActive.Add(player, SortCategory);
            else CategoryActive[player] = SortCategory;

            Configuration.InterfaceSettings Interface = config.InterfaceSetting;
            Configuration.InterfaceSettings.CategorySettings.PNGCategoryIcon Category = Interface.CategorySetting.PNGCategoryIcons;
            IEnumerable<KeyValuePair<String, Configuration.ItemSettings>> Items = SortCategory == CategoryItem.All ? config.ItemSetting.Where(p => String.IsNullOrWhiteSpace(p.Value.Permission) || permission.UserHasPermission(player.UserIDString, p.Value.Permission)).Skip(20 * PagePlayers[player]).Take(20) : config.ItemSetting.Where(p => String.IsNullOrWhiteSpace(p.Value.Permission) || permission.UserHasPermission(player.UserIDString, p.Value.Permission)).Where(i => i.Value.CategoryItems == SortCategory).Skip(20 * PagePlayers[player]).Take(20);
            Single FadeOut = 0.3f;
            Single FadeIn = 0.3f; 
            ///

            CuiElementContainer container = new CuiElementContainer
            {
                {
                    new CuiPanel
                    {
                        FadeOut = FadeOut,
                        RectTransform = { AnchorMin = "0.309375 0.14440162", AnchorMax = "0.5677084 0.82960162" }, 
                        Image = { FadeIn = FadeIn, Color = "0 0 0 0" }
                    },
                    CRAFTSYSTEM_HUD,
                    "ITEM_PANEL"
                }
            };

            Int32 CountKitPage = SortCategory == CategoryItem.All ? config.ItemSetting.Where(p => String.IsNullOrWhiteSpace(p.Value.Permission) || permission.UserHasPermission(player.UserIDString, p.Value.Permission)).Skip(20 * (PagePlayers[player] + 1)).Take(20).Count() : config.ItemSetting.Where(p => String.IsNullOrWhiteSpace(p.Value.Permission) || permission.UserHasPermission(player.UserIDString, p.Value.Permission)).Where(i => i.Value.CategoryItems == SortCategory).Skip(20 * (PagePlayers[player] + 1)).Take(20).Count();
            Int32 ItemCount = 0, x = 0, y = 0;
            foreach (KeyValuePair<String, Configuration.ItemSettings> Item in Items)
            {
                String PNGBackground = SlotActive == ItemCount ? $"ACTIVE_ITEM_{Interface.GeneralSetting.PNGActive}" : $"IN_ACTIVE_ITEM_{Interface.GeneralSetting.PNGInActive}";
                String PNGColorInactive = SlotActive == ItemCount ? "#FFFFFFFF" : GetColorSlot(Item.Value.CategoryItems);
                CategoryItem CategoryItem = Item.Value.CategoryItems;
                String PNGIcon = CategoryItem == CategoryItem.Transport || CategoryItem == CategoryItem.Custom || CategoryItem == CategoryItem.Entity ? GetImage($"ITEM_{Item.Value.PNG}") : Item.Value.SkinID != 0 ? GetImage($"{Item.Value.Shortname}_128px_{Item.Value.SkinID}", Item.Value.SkinID) : GetImage($"{Item.Value.Shortname}_128px");
                String AnchorMinResize = SlotActive == ItemCount ? "0.05000005 0.05" : "0.1583334 0.1333334";
                String AnchorMaxResize = SlotActive == ItemCount ? "0.9666668 0.9666671" : "0.8750001 0.8500004";

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

                Int32 AmountCraft = !String.IsNullOrWhiteSpace(Item.Value.Command) || Item.Value.AmountCraft <= 0 ? 1 : Item.Value.AmountCraft;
                container.Add(new CuiLabel
                {
                    FadeOut = FadeOut,
                    RectTransform = { AnchorMin = "0 0.1", AnchorMax = "1 1" },
                    Text = { FadeIn = FadeIn, Text = $"x{AmountCraft}", FontSize = 14, Font = "robotocondensed-bold.ttf", Align = TextAnchor.LowerCenter }
                }, $"ITEMS_{ItemCount}", $"ITEM_AMOUNT_{ItemCount}");

                if (IsCooldown(player, Item.Key))
                {
                    container.Add(new CuiPanel
                    {
                        FadeOut = FadeOut,
                        RectTransform = { AnchorMin = "0.09 0.09", AnchorMax = "0.91 0.91" },
                        Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.GeneralSetting.HexBackground), Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }
                    }, $"ITEMS_{ItemCount}", $"ITEMS_COOLDOWN_{ItemCount}");

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Text = { Text = GetCooldown(player, Item.Key), Color = HexToRustFormat("#efedee"), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter, FadeIn = 0.3f }
                    }, $"ITEMS_COOLDOWN_{ItemCount}");
                }
                else
                {
                    container.Add(new CuiButton
                    {
                        FadeOut = FadeOut,
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                        Button = { FadeIn = FadeIn, Command = $"func_craft select_item {SortCategory:d} {ItemCount} {Item.Key}", Color = "0 0 0 0" },
                        Text = { FadeIn = FadeIn, Text = "", Align = TextAnchor.MiddleCenter }
                    }, $"ITEMS_{ItemCount}");
                }

                ItemCount++;
                x++;
                if (x == 4)
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
                }, CRAFTSYSTEM_HUD, $"BTN_NEXT_BUTTON");
            }

            CuiHelper.AddUi(player, container);
        }

        private void DestroyAll(BasePlayer player)
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

        public Boolean HasImage(String imageName)
        {
            return (Boolean)ImageLibrary?.Call("HasImage", imageName);
        }
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

        private void CraftingItem(BasePlayer player, String ItemKey)
        {
            if (!config.ItemSetting.ContainsKey(ItemKey)) return;
            Configuration.ItemSettings Item = config.ItemSetting[ItemKey];
            if (!Is_Full_Item_Pack(player, Item.ItemListForCraft)) return;

            List<Int32> ItemList = new List<Int32>();
            Int32 Index = 0;

            foreach (Configuration.ItemSettings.ItemForCraft ItemTake in Item.ItemListForCraft)
            {
                ItemList.Add(ItemTake.Amount);
                foreach (Item ItemPlayer in player.inventory.AllItems().Where(x => x.skin == ItemTake.SkinID && x.info.shortname == ItemTake.Shortname))
                {
                    if (ItemList[Index] <= 0) continue;
                    ItemList[Index] -= ItemPlayer.amount;
                    ItemPlayer.UseItem(ItemList[Index] > 0 ? ItemList[Index] : ItemTake.Amount);
                }
                Index++;
            }

            if (Item.IQEconomicPrice != 0 && IsRemovedBalance(player.userID, Item.IQEconomicPrice))
                RemoveBalance(player.userID, Item.IQEconomicPrice);

            Int32 AmountCraft = Item.AmountCraft <= 0 ? 1 : Item.AmountCraft;
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
                        Item item = ItemManager.CreateByName(Item.Shortname, AmountCraft, Item.SkinID);
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
                        Item item = ItemManager.CreateByName(CustomItemShortname, AmountCraft, Item.SkinID);
                        if (!String.IsNullOrWhiteSpace(Item.DisplayName))
                            item.name = Item.DisplayName;

                        player.GiveItem(item);
                        break;
                    }
            }

            SetCooldown(player, ItemKey);
            if (IsCooldown(player, ItemKey))
                DestroyItemInfo(player);
            else UI_Information_Craft(player, ItemKey);
            DestroyItem(player);
            LoadedItems(player, CategoryItem.All, -1);
        }

        public Boolean DoRankIQRankSystem(BasePlayer player, String RankKey)
        {
            return IQRankSystemAvaliability(player, RankKey);
        }

        
                private static String HexToRustFormat(String hex)
        {
            Color color;
            ColorUtility.TryParseHtmlString(hex, out color);
            sb.Clear();
            return sb.AppendFormat("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a).ToString();
        }
        
        
                private void OnEntityBuilt(Planner plan, GameObject go)
        {
            SpawnItem(go.ToBaseEntity(), plan.GetOwnerPlayer().userID);
        }

        private void DestroyItem(BasePlayer player)
        {
            CategoryItem SortCategory = !CategoryActive.ContainsKey(player) ? CategoryItem.All : CategoryActive[player];
            Int32 ItemCategoryIndex = 0;
            IEnumerable<KeyValuePair<String, Configuration.ItemSettings>> Items = SortCategory == CategoryItem.All ? config.ItemSetting : config.ItemSetting.Where(i => i.Value.CategoryItems == SortCategory);
            foreach (KeyValuePair<String, Configuration.ItemSettings> Item in Items)
            {
                CuiHelper.DestroyUi(player, $"ITEM_ICON_{ItemCategoryIndex}");
                CuiHelper.DestroyUi(player, $"ITEM_BACKGROUND_{ItemCategoryIndex}");
                CuiHelper.DestroyUi(player, $"ITEMS_{ItemCategoryIndex}");
                CuiHelper.DestroyUi(player, $"ITEM_AMOUNT_{ItemCategoryIndex}");
                ItemCategoryIndex++;
            }
            CuiHelper.DestroyUi(player, "ITEM_PANEL");
            CuiHelper.DestroyUi(player, "BTN_BACK_BUTTON");
            CuiHelper.DestroyUi(player, "BTN_NEXT_BUTTON");
        }

        public String GetColorSlot(CategoryItem ItemCategory)
        {
            Configuration.InterfaceSettings.CategorySettings.ColorCategoryIcon Color = config.InterfaceSetting.CategorySetting.ColorCategoryIcons;
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
            }
}
