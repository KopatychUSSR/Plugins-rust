using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Plugins; 
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("ShopConverter", "Mevent & Qbis", "1.0.0")]
	public class ShopConverter : RustPlugin
	{
        [PluginReference]
        Plugin Shop = null;


		#region Data
		private const bool LangRu = false;

        #region Classes
        private class Configuration
        {
            #region Classes
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

        public class Localization
        {
            #region Fields

            [JsonProperty(PropertyName = LangRu ? "Включено" : "Enabled")]
            public bool Enabled;

            [JsonProperty(PropertyName = LangRu ? "Текст (язык - текст)" : "Text (language - text)",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, string> Messages = new();

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
        }

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

        public class ShopItem
        {
            #region Fields

            [JsonProperty(PropertyName = LangRu ? "Тип" : "Type")]
            [JsonConverter(typeof(StringEnumConverter))]
            public ItemType Type;

            [JsonProperty(PropertyName = "ID")] public int ID;

            [JsonProperty(PropertyName = LangRu ? "Изображение" : "Image")]
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

            [JsonProperty(PropertyName = "ShortName")]
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

            [JsonProperty(PropertyName = LangRu ? "Продолжительность блокировки покупки после вайпа" : "Purchase block duration after wipe")]
            public int BuyBlockDurationAfterWipe;

            [JsonProperty(PropertyName = LangRu ? "Продолжительность блокировки продажи после вайпа" : "Sale block duration after wipe")]
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

            #region Constructor

            public ShopItem()
            {
            }

            public ShopItem(Dictionary<string, object> dictionary)
            {
                var price = (double)dictionary["Price"];
                var sellPrice = (double)dictionary["SellPrice"];

                ID = (int)dictionary["ID"];
                Type = (ItemType)dictionary["Type"];
                Image = (string)dictionary["Image"];
                Title = (string)dictionary["Title"];
                Command = (string)dictionary["Command"];
                DisplayName = (string)dictionary["DisplayName"];
                ShortName = (string)dictionary["ShortName"];
                Skin = (ulong)dictionary["Skin"];
                Blueprint = (bool)dictionary["Blueprint"];
                CanBuy = (bool)dictionary["Buying"];
                CanSell = (bool)dictionary["Selling"];
                Amount = (int)dictionary["Amount"];
                Price = price;
                SellPrice = sellPrice;
                Plugin = new PluginItem
                {
                    Hook = (string)dictionary["Plugin_Hook"],
                    Plugin = (string)dictionary["Plugin_Name"],
                    Amount = (int)dictionary["Plugin_Amount"]
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

                Currencies = new ItemCurrency()
                {
                    Enabled = false,
                    Currencies = new Dictionary<int, CurrencyInfo>
                    {
                        [0] = new() { Price = price },
                        [1] = new() { Price = price },
                    },
                    SellCurrencies = new Dictionary<int, CurrencyInfo>
                    {
                        [0] = new() { Price = sellPrice },
                        [1] = new() { Price = sellPrice },
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
                            [0] = new() { Price = itemCost },
                            [1] = new() { Price = itemCost },
                        },
                        SellCurrencies = new Dictionary<int, CurrencyInfo>
                        {
                            [0] = new() { Price = itemCost },
                            [1] = new() { Price = itemCost },
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

                [JsonProperty(PropertyName = LangRu ? "Валюты для покупки предметов (ключ – ID экономики, при использовании экономики по умолчанию используйте 0)" : "Enabled currency for buying items (key - economy ID, if you use economy by default use 0)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public Dictionary<int, CurrencyInfo> Currencies = new();

                [JsonProperty(PropertyName = LangRu ? "Валюты для продажи предметов (ключ – ID экономики, при использовании экономики по умолчанию используйте 0)" : "Currency for selling items (key - economy ID, if you use economy by default use 0)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
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
                if (GeneTypes is not { Count: 6 })
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
                if (GeneTypes is not { Count: 6 })
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

            [JsonProperty(PropertyName = "Skin")]
            public ulong Skin;

            [JsonProperty(PropertyName = LangRu ? "Название" : "Title")]
            public EconomyTitle EconomyTitle;

            [JsonProperty(PropertyName = LangRu ? "Баланс" : "Balance")]
            public EconomyTitle EconomyBalance;

            [JsonProperty(PropertyName = LangRu ? "Стоимость" : "Price")]
            public EconomyTitle EconomyPrice;

            [JsonProperty(PropertyName = LangRu ? "Стоимость предметов в футере" : "Footer Items Price")]
            public EconomyTitle EconomyFooterPrice;

            #endregion Fields
        }

        private class EconomyTitle
        {
            #region Fields

            [JsonProperty(PropertyName = LangRu ? "Сообщение" : "Message")]
            public string Message = string.Empty;

            [JsonProperty(PropertyName = LangRu ? "Использовать локализованные сообщения?" : "Use localized messages?")]
            public bool UseLocalizedMessages = false;

            [JsonProperty(PropertyName = LangRu ? "Локализованные сообщения" : "Localized messages",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, string> LocalizedMessages = new();

            #endregion
        }

        #endregion
		
		#region Items Data
		
		private static ItemsData  _itemsData;
		
		private class ItemsData
		{
			[JsonProperty(PropertyName = LangRu ? "Категории Магазина" : "Shop Categories",
				ObjectCreationHandling = ObjectCreationHandling.Replace)]
			public List<ShopCategory> Shop = new();
		}
		
		private void SaveItemsData() => Interface.Oxide.DataFileSystem.WriteObject($"Shop/Shops/Default", _itemsData);

		private void LoadItemsData()
		{
			try
			{
				_itemsData = Interface.Oxide.DataFileSystem.ReadObject<ItemsData>($"Shop/Shops/Default");
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			_itemsData ??= new ItemsData();
		}
		
		#endregion Items Data

		#endregion

        #region Convert

        #region ShopUI (by David)
        [ConsoleCommand("shop.convert.shopui")]
        private void CmdConvertShopUI(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;

            if(Shop == null || !Shop.IsLoaded || !Shop.Author.Contains("Mevent"))
            {
                PrintError("Plugin Shop by Mevent not loaded!");
                return;
            }

            LoadItemsData();

            if (arg.HasArgs())
            {
                if (bool.TryParse(arg.Args[0], out var clear) && clear)
                {
                    _itemsData.Shop.Clear();
                }
            }

            Dictionary<string, CategoryData> categories = new();
            Dictionary<string, ItemsDataShopUi> item = new();
            Dictionary<string, CmdData> commands = new();

            LoadShopUIData(ref categories, ref item, ref commands);

            if (categories.Count == 0 && item.Count == 0 && commands.Count == 0) return;

            ConvertShopUIData(ref categories, ref item, ref commands);
        }

        private void LoadShopUIData(ref Dictionary<string, CategoryData> c, ref Dictionary<string, ItemsDataShopUi> i, ref Dictionary<string, CmdData> cm)
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"Shop/Categories"))
            {
                c = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, CategoryData>>($"Shop/Categories");
            }
            else c = new();

            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"Shop/Items"))
            {
                i = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, ItemsDataShopUi>>($"Shop/Items");
            }
            else i = new();

            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"Shop/Commands"))
            {
                cm = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, CmdData>>($"Shop/Commands");
            }
            else cm = new();
        }

        private void ConvertShopUIData(ref Dictionary<string, CategoryData> categories, ref Dictionary<string, ItemsDataShopUi> item, ref Dictionary<string, CmdData> commands)
        {
            var totalCount = 0;

            ConvertingShopUICommands(ref commands, ref totalCount);

            ConvertingShopUIItems(ref categories, ref item, ref totalCount);

            SaveItemsData();

            PrintWarning($"{totalCount} items successfully converted from Shop(David) to Shop!");

            Interface.Oxide.ReloadPlugin(nameof(Shop));

        }

        private void ConvertingShopUIItems(ref Dictionary<string, CategoryData> categories, ref Dictionary<string, ItemsDataShopUi> items, ref int totalCount)
        {
            if (categories == null || items == null || categories.Count == 0 || items.Count == 0) return;

            var noneCategory = new ShopCategory
            {
                Enabled = true,
                CategoryType = ShopCategory.Type.None,
                Title = "Items",
                Permission = string.Empty,
                Localization = new Localization
                {
                    Enabled = false,
                    Messages = new Dictionary<string, string>
                    {
                        ["en"] = "Items",
                        ["fr"] = "Items"
                    }
                },
                SortType = Configuration.SortType.None,
                Items = new List<ShopItem>()
            };

            foreach (var check in categories)
            {
                var category = _itemsData.Shop.Find(x => x.Title == check.Key.ToString());

                if (category == null)
                {
                    category = new ShopCategory
                    {
                        Enabled = true,
                        CategoryType = ShopCategory.Type.None,
                        Title = check.Key.ToString(),
                        Permission = check.Value.Permission == null ? String.Empty : check.Value.Permission,
                        Localization = new Localization
                        {
                            Enabled = false,
                            Messages = new Dictionary<string, string>
                            {
                                ["en"] = check.Key.ToString(),
                                ["fr"] = check.Key.ToString()
                            }
                        },
                        SortType = Configuration.SortType.None,
                        Items = new List<ShopItem>()
                    };

                    _itemsData.Shop.Add(category);
                }

                foreach (var item in check.Value.Items)
                {
                    if (!items.TryGetValue(item, out var data))
                        continue;


                    var newItem = ShopItem.GetDefault(Shop.Call<int>("GetId"), data.BuyPrice, item);
                    newItem.Type = ItemType.Item;
                    newItem.Image = !string.IsNullOrEmpty(data.Image) &&
                                    (data.Image.StartsWith("http") || data.Image.StartsWith("www"))
                        ? data.Image
                        : string.Empty;
                    newItem.Amount = data.DefaultAmount;
                    newItem.Skin = data.Skin;
                    newItem.BuyCooldown = 0;
                    newItem.SellCooldown = 0;

                    category.Items.Add(newItem);

                    totalCount++;
                }
            }

            if (noneCategory.Items.Count > 0)
                _itemsData.Shop.Add(noneCategory);
        }


        private void ConvertingShopUICommands(ref Dictionary<string, CmdData> commands, ref int totalCount)
        {
            if(commands == null || commands.Count == 0) return;

            var category = new ShopCategory
            {
                Enabled = true,
                CategoryType = ShopCategory.Type.None,
                Title = "Commands",
                Permission = string.Empty,
                Localization = new Localization
                {
                    Enabled = false,
                    Messages = new Dictionary<string, string>
                    {
                        ["en"] = "Commands",
                        ["fr"] = "Commands"
                    }
                },
                SortType = Configuration.SortType.None,
                Items = new List<ShopItem>()
            };

            foreach (var check in commands)
            {
                var newItem = ShopItem.GetDefault(Shop.Call<int>("GetId"), check.Value.BuyPrice, string.Empty);
                newItem.Type = ItemType.Command;
                newItem.Image = !string.IsNullOrEmpty(check.Value.Image) &&
                                (check.Value.Image.StartsWith("http") || check.Value.Image.StartsWith("www"))
                    ? check.Value.Image
                    : string.Empty;
                newItem.Description = String.Empty;
                newItem.Command = check.Value.Command.Replace("{playername}", "%username%").Replace("{steamid}", "%steamid%");
                newItem.BuyCooldown = 0;
                newItem.SellCooldown = 0;
                newItem.DisplayName = check.Key;

                category.Items.Add(newItem);

                totalCount++;
            }

            if (category.Items.Count > 0)
                _itemsData.Shop.Add(category);
        }

        #region LoadData
        #endregion

        #region Classes
        private class CategoryData
        {
            public string Image;
            public string Permission;
            public float Sale;
            public List<string> Items = new List<string> { };
        }

        private class ItemsDataShopUi
        {
            public string DisplayName;
            public ulong Skin;
            public string Image;
            public int DefaultAmount;
            public bool BlockAmountChange;
            public bool ShowDisplayName;
            public int BuyPrice;
            public int SellPrice;
            public string Currency;
        }

        private class CmdData
        {
            public string DisplayName;
            public string Image;
            public string Message;
            public string Command;
            public int BuyPrice;
            public string Currency;
            public bool ShowDisplayName;
        }
        #endregion


        #endregion

        #region Server Rewards

        #region Data

        [ConsoleCommand("shop.convert.sr")]
		private void CmdConvertSR(ConsoleSystem.Arg arg)
		{
			if (!arg.IsAdmin) return;

            if (Shop == null || !Shop.IsLoaded || !Shop.Author.Contains("Mevent"))
            {
                PrintError("Plugin Shop by Mevent not loaded!");
                return;
            }

            LoadItemsData();

            if (arg.HasArgs())
            {
                if (bool.TryParse(arg.Args[0], out var clear) && clear)
                {
                    _itemsData.Shop.Clear();
                }
            }

            var data = LoadSRData();
			if (data == null) return;

			ConvertSRData(data);
		}

		private SRRewardData LoadSRData()
		{
			SRRewardData rewarddata = null;

			try
			{
				rewarddata = Interface.Oxide.DataFileSystem.ReadObject<SRRewardData>("ServerRewards/reward_data");
			}
			catch
			{
				PrintWarning("No Server Rewards data found!");
			}

			return rewarddata;
		}

		private void ConvertSRData(SRRewardData rewarddata)
		{
			var totalCount = 0;

			ConvertingSRDataCommands(ref rewarddata, ref totalCount);

			ConvertingSRDataItems(ref rewarddata, ref totalCount);

			ConvertingSRDataKits(ref rewarddata, ref totalCount);

			SaveItemsData();

		
			PrintWarning($"{totalCount} items successfully converted from ServerRewards to Shop!");

            Interface.Oxide.ReloadPlugin(nameof(Shop));
        }

		private void ConvertingSRDataCommands(ref SRRewardData rewarddata, ref int count)
		{
			if (rewarddata == null) return;

			var category = new ShopCategory
			{
				Enabled = true,
				CategoryType = ShopCategory.Type.None,
				Title = "Commands",
				Permission = string.Empty,
				Localization = new Localization
				{
					Enabled = false,
					Messages = new Dictionary<string, string>
					{
						["en"] = "Commands",
						["fr"] = "Commands"
					}
				},
				SortType = Configuration.SortType.None,
				Items = new List<ShopItem>()
			};

			foreach (var check in rewarddata.commands)
			{
				var newItem = ShopItem.GetDefault(Shop.Call<int>("GetId"), check.Value.cost, string.Empty);
				newItem.Type = ItemType.Command;
				newItem.Image = !string.IsNullOrEmpty(check.Value.iconName) &&
				                (check.Value.iconName.StartsWith("http") || check.Value.iconName.StartsWith("www"))
					? check.Value.iconName
					: string.Empty;
				newItem.Description = check.Value.description;
				newItem.Command = string.Join("|", check.Value.commands);
				newItem.BuyCooldown = check.Value.cooldown;
				newItem.SellCooldown = check.Value.cooldown;

				category.Items.Add(newItem);

				count++;
			}

			if (category.Items.Count > 0)
				_itemsData.Shop.Add(category);
		}

		private void ConvertingSRDataItems(ref SRRewardData rewarddata, ref int count)
		{
			if (rewarddata == null) return;

			var noneCategory = new ShopCategory
			{
				Enabled = true,
				CategoryType = ShopCategory.Type.None,
				Title = "Items",
				Permission = string.Empty,
				Localization = new Localization
				{
					Enabled = false,
					Messages = new Dictionary<string, string>
					{
						["en"] = "Items",
						["fr"] = "Items"
					}
				},
				SortType = Configuration.SortType.None,
				Items = new List<ShopItem>()
			};

			foreach (var check in rewarddata.items)
			{
				ShopCategory category;
				if (check.Value.category == SRCategory.None)
				{
					category = noneCategory;
				}
				else
				{
					category = _itemsData.Shop.Find(x => x.Title == check.Value.category.ToString());

					if (category == null)
					{
						category = new ShopCategory
						{
							Enabled = true,
							CategoryType = ShopCategory.Type.None,
							Title = check.Value.category.ToString(),
							Permission = string.Empty,
							Localization = new Localization
							{
								Enabled = false,
								Messages = new Dictionary<string, string>
								{
									["en"] = check.Value.category.ToString(),
									["fr"] = check.Value.category.ToString()
								}
							},
							SortType = Configuration.SortType.None,
							Items = new List<ShopItem>()
						};

						_itemsData.Shop.Add(category);
					}
				}

				var newItem = ShopItem.GetDefault(Shop.Call<int>("GetId"), check.Value.cost, check.Value.shortname);
				newItem.Type = ItemType.Item;
				newItem.Image = !string.IsNullOrEmpty(check.Value.customIcon) &&
				                (check.Value.customIcon.StartsWith("http") || check.Value.customIcon.StartsWith("www"))
					? check.Value.customIcon
					: string.Empty;
				newItem.Amount = check.Value.amount;
				newItem.Skin = check.Value.skinId;
				newItem.BuyCooldown = check.Value.cooldown;
				newItem.SellCooldown = check.Value.cooldown;

				category.Items.Add(newItem);

				count++;
			}

			if (noneCategory.Items.Count > 0)
				_itemsData.Shop.Add(noneCategory);
		}

		private void ConvertingSRDataKits(ref SRRewardData rewarddata, ref int count)
		{
			if (rewarddata == null) return;

			var category = new ShopCategory
			{
				Enabled = true,
				CategoryType = ShopCategory.Type.None,
				Title = "Kits",
				Permission = string.Empty,
				Localization = new Localization
				{
					Enabled = false,
					Messages = new Dictionary<string, string>
					{
						["en"] = "Kits",
						["fr"] = "Kits"
					}
				},
				SortType = Configuration.SortType.None,
				Items = new List<ShopItem>()
			};

			foreach (var check in rewarddata.kits)
			{
				var newItem = ShopItem.GetDefault(Shop.Call<int>("GetId"), check.Value.cost, string.Empty);
				newItem.Type = ItemType.Kit;
				newItem.Image = !string.IsNullOrEmpty(check.Value.iconName) &&
				                (check.Value.iconName.StartsWith("http") || check.Value.iconName.StartsWith("www"))
					? check.Value.iconName
					: string.Empty;
				newItem.Description = check.Value.description;
				newItem.Kit = check.Value.kitName;
				newItem.BuyCooldown = check.Value.cooldown;
				newItem.SellCooldown = check.Value.cooldown;

				category.Items.Add(newItem);

				count++;
			}

			if (category.Items.Count > 0)
				_itemsData.Shop.Add(category);
		}

		#endregion

		#region Classes

		private enum SRCategory
		{
			None,
			Weapon,
			Construction,
			Items,
			Resources,
			Attire,
			Tool,
			Medical,
			Food,
			Ammunition,
			Traps,
			Misc,
			Component,
			Electrical,
			Fun
		}

		private class SRRewardData
		{
			public Dictionary<string, RewardItem> items = new();
			public SortedDictionary<string, RewardKit> kits = new();
			public SortedDictionary<string, RewardCommand> commands = new();

			public bool HasItems(SRCategory category)
			{
				foreach (var kvp in items)
					if (kvp.Value.category == category)
						return true;
				return false;
			}

			public class RewardItem : Reward
			{
				public string shortname, customIcon;
				public int amount;
				public ulong skinId;
				public bool isBp;
				public SRCategory category;
			}

			public class RewardKit : Reward
			{
				public string kitName, description, iconName;
			}

			public class RewardCommand : Reward
			{
				public string description, iconName;
				public List<string> commands = new();
			}

			public class Reward
			{
				public string displayName;
				public int cost;
				public int cooldown;
			}
		}

		#endregion

		#endregion

        #endregion
    }
}
