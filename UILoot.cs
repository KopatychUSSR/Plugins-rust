using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Libraries;
using UnityEngine;

// Group VK: vk.com/webaddde
// Fix for Oxide 7 Feb 2020 year

namespace Oxide.Plugins
{
    [Info("UILoot", "OxideBro", "0.1.20")]
    public class UILoot : RustPlugin
    {
        #region Classes
        public List<uint> LootContainers = new List<uint>();

        private class RateHandler
        {
            public class Rates
            {
                public class Items
                {
                    internal class Amount
                    {
                        [JsonProperty("Минимальное количество")]
                        public int Min;
                        [JsonProperty("Максимальное количество")]
                        public int Max;

                        public int GenerateAmount()
                        {
                            return Core.Random.Range(((Min <= 1) ? 1 : Min), ((Max < Min) ? Min : Max));
                        }

                        public Amount() { }
                        public Amount(int min, int max)
                        {
                            Min = (min <= 1) ? 1 : min;
                            Max = (max < min) ? min : max;
                        }
                    }

                    [JsonProperty("Короткое название предмета")]
                    public string ShortName;
                    [JsonProperty("SkinID предмета")]
                    public ulong SkinID;

                    [JsonProperty("Настройки количества предмета")]
                    public Amount Amounts;
                    [JsonProperty("Шанс выпадения предмета")]
                    public int DropChance = 100;
                    [JsonProperty("Будет ли спавниться как чертеж")]
                    public bool IsBlueprint;

                    public Item ToItem()
                    {
                        Item item = null;
                        if (IsBlueprint)
                        {
                            ItemDefinition blueprintBaseDef = Instance.GetBlueprintBaseDef();
                            if (blueprintBaseDef == null)
                            {
                                Instance.PrintError($"ItemDefinition for 'blueprintbase' not found! Contact the developer!");
                                return null;
                            }

                            ItemDefinition definition = ItemManager.FindItemDefinition(ShortName);
                            if (definition == null)
                            {
                                Instance.PrintError($"ItemDefinition for '{ShortName}' not found! Check if the 'shortname' is correct!");
                                return null;
                            }

                            if (definition.Blueprint == null)
                            {
                                Instance.PrintError($"ItemDefinition.Blueprint for '{ShortName}' not found! This item does not have blueprint! Are you sure everything is fine?");
                                return null;
                            }

                            item = ItemManager.Create(blueprintBaseDef, 1, 0UL);
                            item.blueprintTarget = definition.itemid;
                        }
                        else
                        {
                            int amount = Amounts.GenerateAmount();
                            if (amount <= 0)
                                return null;

                            ItemDefinition definition = ItemManager.FindItemDefinition(ShortName);
                            if (definition == null)
                            {
                                Instance.PrintError($"ItemDefinition for '{ShortName}' not found! Check if the 'shortname' is correct!");
                                return null;
                            }

                            item = ItemManager.Create(definition, amount, SkinID);
                            item.skin = SkinID;
                        }

                        return item;
                    }
                    public static Items FromItem(Item item) => new Items(item.IsBlueprint() ? item.blueprintTargetDef.shortname : item.info.shortname, item.amount, item.amount, item.skin, item.IsBlueprint());

                    public Items() { }
                    public Items(string shortName, int minAmount, int maxAmount = 0, ulong skinId = 0, bool bp = false, int chance = 100)
                    {
                        ShortName = shortName;
                        Amounts = new Amount(minAmount, maxAmount);
                        SkinID = skinId;
                        IsBlueprint = bp;
                        DropChance = chance;
                    }
                }

                public class Scrap
                {
                    [JsonProperty("Активировать принудительное выпадение скрапа")]
                    public bool EnableDrop = false;

                    [JsonProperty("Минимальное кол-во")]
                    public int MinAmount = 2;
                    [JsonProperty("Максимальное кол-во")]
                    public int MaxAmount = 5;

                    public Scrap() { }
                    public Scrap(BaseEntity entity)
                    {
                        LootContainer obj = entity.GetComponent<LootContainer>();
                        if (obj != null)
                        {
                            if (obj.scrapAmount > 0)
                            {
                                EnableDrop = true;
                                MinAmount = MaxAmount = obj.scrapAmount;
                            }
                        }
                    }

                    public Item CreateScrap()
                    {
                        return ItemManager.CreateByPartialName("scrap", Oxide.Core.Random.Range(((MinAmount <= 1) ? 1 : MinAmount), MaxAmount));
                    }
                }

                [JsonProperty("Название префаба объекта")]
                public string PrefabName;

                [JsonProperty("Минимальное количество выпадаемых предметов")]
                public int MinDropAmount = 1;
                [JsonProperty("Количество выпадаемых предметов")]
                public int MaxDropAmount = 1;
                [JsonProperty("Запрещать повторяющиеся предметы")]
                public bool BlockRepeat = true;
                [JsonProperty("Использовать 'регулятор здоровья' для вещей со здоровьем")]
                public bool UsingCondition = false;
                [JsonProperty("Настройки выпадения скрапа")]
                public Scrap ScrapSettings;

                [JsonProperty("Список выпадающих предметов")]
                public List<Items> ItemsList = new List<Items>();

                public Rates() { }
                public Rates(BaseEntity entity)
                {
                    PrefabName = entity.PrefabName;
                    ScrapSettings = new Scrap(entity);
                    LootContainer obj = entity.GetComponent<LootContainer>();

                    /*if (entity.GetComponent<SupplyDrop>())
                    {
                        foreach (var check in entity.GetComponent<SupplyDrop>().LootSpawnSlots.Select(p => p.definition))
                        {
                            foreach (var drop in check.subSpawn.Select(p => p.category))
                            {
                                foreach (var item in drop.subSpawn.SelectMany(p => p.category.items))
                                {
                                    if (!ItemsList.Any(p => p.ShortName == item.itemDef.shortname && p.SkinID == 0 && p.IsBlueprint == item.itemDef.spawnAsBlueprint))
                                        itemList.Add(new Items(item.itemDef.shortname, (int) item.startAmount, item.maxAmount == -1 || item.maxAmount == 0 ? (int) item.startAmount : (int) item.maxAmount, 0, item.itemDef.spawnAsBlueprint)); 
                                }
                            }
                        }
                    }*/
                    if (obj != null)
                    {
                        if (obj.LootSpawnSlots?.Length > 0)
                        {
                            MinDropAmount = MaxDropAmount = obj.LootSpawnSlots.Length;
                        }
                        else if (obj.lootDefinition?.subSpawn != null && obj.lootDefinition?.subSpawn.Length > 0)
                        {
                            MinDropAmount = MaxDropAmount = obj.maxDefinitionsToSpawn;
                        }
                        else if (obj.lootDefinition?.items != null && obj.lootDefinition?.items.Length > 0)
                        {
                            MinDropAmount = MaxDropAmount = obj.lootDefinition.items.Length;
                        }

                        if (obj.SpawnType == LootContainer.spawnType.ROADSIDE || obj.SpawnType == LootContainer.spawnType.TOWN)
                        {
                            UsingCondition = true;
                        }

                        if (obj.LootSpawnSlots?.Length > 0)
                        {
                            foreach (LootSpawn check in obj.LootSpawnSlots?.Select(p => p.definition))
                            {
                                if (check?.subSpawn != null && check?.subSpawn.Length > 0)
                                {
                                    FindItemsRecursive(check?.subSpawn);
                                }
                            }
                        }

                        if (obj.lootDefinition?.subSpawn != null && obj.lootDefinition?.subSpawn.Length > 0)
                        {
                            FindItemsRecursive(obj.lootDefinition?.subSpawn);
                        }

                        if (obj.lootDefinition?.items != null && obj.lootDefinition?.items.Length > 0)
                        {
                            foreach (ItemAmountRanged itemAmountRanged in obj.lootDefinition?.items)
                            {
                                if (!ItemsList.Any(p => p.ShortName == itemAmountRanged.itemDef.shortname && p.SkinID == 0 && p.IsBlueprint == itemAmountRanged.itemDef.spawnAsBlueprint))
                                    ItemsList.Add(new Items(itemAmountRanged.itemDef.shortname, (int)itemAmountRanged.startAmount, (itemAmountRanged.maxAmount == -1 || itemAmountRanged.maxAmount == 0 || itemAmountRanged.maxAmount <= itemAmountRanged.startAmount) ? (int)itemAmountRanged.startAmount : (int)itemAmountRanged.maxAmount, 0, itemAmountRanged.itemDef.spawnAsBlueprint));
                            }
                        }
                    }
                    else
                    {
                        ItemsList = new List<Items>
                        {
                            new Items("wood", 5, 190, 0),
                            new Items("stones", 6, 15190, 0),
                            new Items("gears", 6, 11909, 0),
                            new Items("rifle.ak", 6, 581901, 0),
                            new Items("rope", 6, 19088, 0),
                        };
                        return;
                    }
                }

                private void FindItemsRecursive(LootSpawn.Entry[] subSpawn)
                {
                    subSpawn.ToList().ForEach(z =>
                    {
                        if (z.category?.subSpawn != null && z.category?.subSpawn.Length > 0)
                        {
                            FindItemsRecursive(z.category?.subSpawn);
                        }

                        if (z.category?.items != null && z.category?.items.Length > 0)
                        {
                            foreach (ItemAmountRanged itemAmountRanged in z.category?.items)
                            {
                                if (!ItemsList.Any(p => p.ShortName == itemAmountRanged.itemDef.shortname && p.SkinID == 0 && p.IsBlueprint == itemAmountRanged.itemDef.spawnAsBlueprint))
                                    ItemsList.Add(new Items(itemAmountRanged.itemDef.shortname, (int)itemAmountRanged.startAmount, (itemAmountRanged.maxAmount == -1 || itemAmountRanged.maxAmount == 0 || itemAmountRanged.maxAmount <= itemAmountRanged.startAmount) ? (int)itemAmountRanged.startAmount : (int)itemAmountRanged.maxAmount, 0, itemAmountRanged.itemDef.spawnAsBlueprint, z.weight));
                            }
                        }
                    });
                }
            }

            [JsonProperty("Список настроеных рейтов")]
            public List<Rates> Rateses = new List<Rates>();

            public Rates GetRates(BaseEntity entity)
            {
                if (!(entity is LootContainer)) return null;
                return Rateses.FirstOrDefault(p => p.PrefabName == entity.PrefabName);
            }
            public Rates GetRates(string prefabName)
            {
                return Rateses.FirstOrDefault(p => p.PrefabName == prefabName);
            }
            public Rates GetRates(LootContainer container)
            {
                if (container == null) return null;
                return Rateses.FirstOrDefault(p => p.PrefabName == container.PrefabName);
            }

            public Rates CreateRates(BaseEntity entity)
            {
                Rates rate = GetRates(entity.PrefabName);
                if (rate != null)
                {
                    Instance.PrintError($"Tried to create rates, to exists rates!");
                    return null;
                }

                rate = new Rates(entity);
                Rateses.Add(rate);

                return rate;
            }

            public void CopyTo(BasePlayer player, Rates rates)
            {
                if (!Instance.CanConfigure(player))
                    return;

                foreach (RateHandler.Rates.Items p in rates.ItemsList)
                {
                    Item item = p.ToItem();
                    if (item == null)
                        continue;

                    item.OnVirginSpawn();
                    item.MoveToContainer(player.inventory.containerMain);
                }
            }

            public void CopyFrom(BasePlayer player, Rates rates)
            {
                if (!Instance.CanConfigure(player))
                    return;

                rates.ItemsList.Clear();
                player.inventory.AllItems().ToList().ForEach(p => rates.ItemsList.Add(Rates.Items.FromItem(p)));
            }
        }
        #endregion

        #region SteampoweredAPI 
        private class SteampoweredResult
        {
            public Response response;
            public class Response
            {
                [JsonProperty("result")]
                public int result;

                [JsonProperty("resultcount")]
                public int resultcount;

                [JsonProperty("publishedfiledetails")]
                public List<PublishedFiled> publishedfiledetails;
                public class PublishedFiled
                {
                    [JsonProperty("publishedfileid")]
                    public ulong publishedfileid;

                    [JsonProperty("result")]
                    public int result;

                    [JsonProperty("creator")]
                    public string creator;

                    [JsonProperty("creator_app_id")]
                    public int creator_app_id;

                    [JsonProperty("consumer_app_id")]
                    public int consumer_app_id;

                    [JsonProperty("filename")]
                    public string filename;

                    [JsonProperty("file_size")]
                    public int file_size;

                    [JsonProperty("preview_url")]
                    public string preview_url;

                    [JsonProperty("hcontent_preview")]
                    public string hcontent_preview;

                    [JsonProperty("title")]
                    public string title;

                    [JsonProperty("description")]
                    public string description;

                    [JsonProperty("time_created")]
                    public int time_created;

                    [JsonProperty("time_updated")]
                    public int time_updated;

                    [JsonProperty("visibility")]
                    public int visibility;

                    [JsonProperty("banned")]
                    public int banned;

                    [JsonProperty("ban_reason")]
                    public string ban_reason;

                    [JsonProperty("subscriptions")]
                    public int subscriptions;

                    [JsonProperty("favorited")]
                    public int favorited;

                    [JsonProperty("lifetime_subscriptions")]
                    public int lifetime_subscriptions;

                    [JsonProperty("lifetime_favorited")]
                    public int lifetime_favorited;

                    [JsonProperty("views")]
                    public int views;

                    [JsonProperty("tags")]
                    public List<Tag> tags;
                    public class Tag
                    {
                        [JsonProperty("tag")]
                        public string tag;
                    }
                }
            }
        }
        #endregion

        #region Variables
        [PluginReference]
        Plugin ImageLibrary;

        private bool initialized = false;
        private string AdminPermission = "UILoot.Admin";
        private static RateHandler Handler = new RateHandler();
        private static Configuration configuration = new Configuration();
        private static UILoot Instance;
        #endregion

        #region Configuration
        public class Configuration
        {
            [JsonProperty("Ключ Steam Web API")]
            public string SteamWebApiKey = "!!! Вы можете его получить ЗДЕСЬ > https://steamcommunity.com/dev/apikey < и его нужно вставить СЮДА !!!";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                configuration = Config.ReadObject<Configuration>();
            }
            catch
            {
                PrintError("Error reading config, please check!");
            }
        }

        protected override void LoadDefaultConfig()
        {
            configuration = new Configuration();
            NextTick(SaveConfig);
        }

        protected override void SaveConfig() => Config.WriteObject(configuration);
        #endregion

        #region Language
        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LootContainerUpdate"] = "All LootContainer of this type are updated!",
                ["LootContainerNotRates"] = "No custom rates have been set for this LootContainer!",
                ["SuccessCopyItemsToInventory"] = "All items have been successfully copied to your inventory!",

                // UI
                ["UI_REFILL"] = "REFILL",
                ["UI_RESTORE"] = "RESTORE",
                ["UI_REMOVE"] = "REMOVE",
                ["UI_MAXIMUM"] = "MAXIMUM",
                ["UI_MINIMUM"] = "MINIMUM",
                ["UI_COPY_TO_INVENTORY"] = "COPY <b>TO</b> INVENTORY",
                ["UI_COPY_FROM_INVENTORY"] = "COPY <b>FROM</b> INVENTORY",
                ["UI_ADD_FROM_INVENTORY"] = "ADD <b>FROM</b> INVENTORY",
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LootContainerUpdate"] = "Все LootContainer данного типа обновлены!",
                ["LootContainerNotRates"] = "Для данного LootContainer не заданы кастомные рейты!",
                ["SuccessCopyItemsToInventory"] = "Все предметы успешно скопированы вам в инвентарь!",

                // UI
                ["UI_REFILL"] = "ПЕРЕНАПОЛНИТЬ",
                ["UI_RESTORE"] = "ВОССТАНОВИТЬ",
                ["UI_REMOVE"] = "УДАЛИТЬ",
                ["UI_MAXIMUM"] = "МАКСИМУМ",
                ["UI_MINIMUM"] = "МИНИМУМ",
                ["UI_COPY_TO_INVENTORY"] = "СКОПИРОВАТЬ <b>В</b> ИНВЕНТАРЬ",
                ["UI_COPY_FROM_INVENTORY"] = "СКОПИРОВАТЬ <b>ИЗ</b> ИНВЕНТАРЯ",
                ["UI_ADD_FROM_INVENTORY"] = "ДОБАВИТЬ <b>ИЗ</b> ИНВЕНТАРЯ",

            }, this, "ru");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LootContainerUpdate"] = "Всі LootContainer даного типу оновлені!",
                ["LootContainerNotRates"] = "Для даного LootContainer не задані кастомні pейти!",
                ["SuccessCopyItemsToInventory"] = "Всі предмети успішно скопійовані вам в інвентар!",

                // UI
                ["UI_REFILL"] = "ПЕРЕНАПОВНИТИ",
                ["UI_RESTORE"] = "ВІДНОВИТИ",
                ["UI_REMOVE"] = "ВИЛУЧИТИ",
                ["UI_MAXIMUM"] = "МАКСИМУМ",
                ["UI_MINIMUM"] = "МІНІМУМ",
                ["UI_COPY_TO_INVENTORY"] = "СКОПІЮВАТИ <b>В</b> ІНВЕНТАР",
                ["UI_COPY_FROM_INVENTORY"] = "СКОПІЮВАТИ <b>З</b> ІНВЕНТАРЯ",
                ["UI_ADD_FROM_INVENTORY"] = "ДОДАТИ <b>З</b> ІНВЕНТАРЯ",

            }, this, "uk");
        }

        private string _(BasePlayer player, string key, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, player?.UserIDString), args);
        }
        #endregion

        #region Initialization
        private void OnServerInitialized()
        {
            if (ImageLibrary == null)
            {
                PrintError("You need the ImageLibrary plugin to work. Please download: https://umod.org/plugins/image-library");
                Unsubscribe(nameof(OnHammerHit));
            }

            Instance = this;
            ItemManager.Initialize();
            if (Interface.Oxide.DataFileSystem.ExistsDatafile(Name))
                Handler = Interface.Oxide.DataFileSystem.ReadObject<RateHandler>(Name);
            permission.RegisterPermission(AdminPermission, this);
            UpdateAllContainers();
            initialized = true;
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, MainLayer);
            SaveData();
        }

        private void SaveData()
        {
            if (Handler != null)
                Interface.Oxide.DataFileSystem.WriteObject(Name, Handler);
        }
        #endregion

        #region Hooks

        void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            if (player == null || entity == null || entity?.net.ID == null || entity.GetComponent<LootContainer>() == null) return;
            var container = entity.GetComponent<LootContainer>();
            if (!LootContainers.Contains(container.net.ID) && !container.IsDestroyed && container.inventory.itemList.Count > 0)
                LootContainers.Add(container.net.ID);
        }



        private object OnLootSpawn(LootContainer container)
        {
            if (!initialized || container == null)
                return null;
            if (LootContainers.Contains(container.net.ID)) return null;
            if (PopulateLoot(container))
            {
                ItemManager.DoRemoves();
                return true;
            }

            NextTick(() =>
            {
                container.inventory.capacity = container.inventory.itemList.Count;
            });

            return null;
        }

        private void OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (!CanConfigure(player) || !(info?.HitEntity is LootContainer))
                return;

            LootContainer container = info.HitEntity as LootContainer;
            if (container == null)
                return;

            RateHandler.Rates rate = Handler.GetRates(container);

            if (player.serverInput.IsDown(BUTTON.FIRE_SECONDARY))
            {
                if (rate != null)
                {
                    // @TODO: UI для настройки дополнительных параметров генерации лута/скрапа у LootContainer
                }
            }
            else if (player.serverInput.IsDown(BUTTON.RELOAD))
            {
                if (rate == null)
                {
                    SendReply(player, _(player, "LootContainerNotRates"));
                    return;
                }
                else
                {
                    UpdateAllContainers(container.PrefabName);
                    SendReply(player, _(player, "LootContainerUpdate"));
                    return;
                }
            }
            else if (player.serverInput.IsDown(BUTTON.SPRINT))
            {
                container.PlayerOpenLoot(player, "", true);
                return;
            }

            if (rate == null)
                rate = Handler.CreateRates(container);

            UI_DrawRate(player, rate, 0);
        }
        #endregion

        #region Interface
        private const string MainLayer = "UI_LootUIMainLayer";
        private void UI_DrawLayer(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, MainLayer);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0.9" }
            }, "Overlay", MainLayer);

            /*container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.9", AnchorMax = "1 1.03", OffsetMax = "0 0" },
                Text = { Text = "ГРАФИЧЕСКАЯ НАСТРОЙКА ВЫПАДАЮЩИХ ПРЕДМЕТОВ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 32, Color = "1 1 1 0.5" }
            }, MainLayer);*/

            CuiHelper.AddUi(player, container);
        }

        /*private const string ChooseLayer = "UI_LootUIChooseLayer";
        private void UI_DrawCreation(BasePlayer player, BaseEntity entity)
        {
            CuiElementContainer container = new CuiElementContainer();
            UI_DrawLayer(player);
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.5", AnchorMax = "1 0.6", OffsetMax = "0 0" },
                Text = { Text = "Для этого ящика отсутствуют особые настройки, создать их?\n" +
                                "<size=16>Вам придётся самостоятельно настроить кол-во выпадающих предметов</size>", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 26 }
            }, MainLayer);
                
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.4 0.44", AnchorMax = "0.49 0.49", OffsetMax = "0 0" },
                Button = { Color = "0.8 0.5 0.5 0.6", Close = MainLayer },
                Text = { Text = "НЕТ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 20 }
            }, MainLayer);
                
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.51 0.44", AnchorMax = "0.6 0.49", OffsetMax = "0 0" },
                Button = { Color = "0.5 0.8 0.5 0.6", Close = MainLayer, Command = $"UI_UILootHandler create {entity.PrefabName}" }, 
                Text = { Text = "ДА", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 20 }
            }, MainLayer);

            CuiHelper.AddUi(player, container);
        }*/

        private const string ShowRate = "UI_LootUIShowRatesLayer";
        private void UI_DrawRate(BasePlayer player, RateHandler.Rates rate, int page)
        {
            CuiElementContainer container = new CuiElementContainer();
            UI_DrawLayer(player);

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, MainLayer, ShowRate);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Close = MainLayer },
                Text = { Text = "" }
            }, ShowRate);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "25 -75", OffsetMax = "500 -10" },
                Button = { Color = "0 0 0 0", Command = $"UI_UILootHandler populate {rate.PrefabName.Replace(" ", "+")}", Close = MainLayer },
                Text = { Text = _(player, "UI_REFILL"), Align = TextAnchor.UpperLeft, Font = "robotocondensed-regular.ttf", FontSize = 24 }
            }, ShowRate);


            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.6 1", AnchorMax = "0.6 1", OffsetMin = "-175 -75", OffsetMax = "-150 -10" },
                Button = { Color = "0 0 0 0", Command = $"UI_UILootHandler" },
                Text = { Text = rate.MaxDropAmount.ToString(), Align = TextAnchor.UpperCenter, Font = "robotocondensed-regular.ttf", FontSize = 24 }
            }, ShowRate, ShowRate + ".Amount");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.6 1", AnchorMax = "0.6 1", OffsetMin = "-300 -75", OffsetMax = "-184 0" },
                Button = { Color = "0 0 0 0", Material = "", Command = $"UI_UILootHandler amount {rate.PrefabName.Replace(" ", "+")} {page - 1}" },
                Text = { Text = "-", Color = rate.MaxDropAmount > 0 ? "1 1 1 1" : "1 1 1 0.2", Align = TextAnchor.UpperRight, Font = "robotocondensed-regular.ttf", FontSize = 40 }
            }, ShowRate);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.6 1", AnchorMax = "0.6 1", OffsetMin = "-140 -75", OffsetMax = "-25 -10" },
                Button = { Color = "0 0 0 0", Material = "", Command = $"UI_UILootHandler amount {rate.PrefabName.Replace(" ", "+")} {page + 1}" },
                Text = { Text = "+", Color = "1 1 1 1", Align = TextAnchor.UpperLeft, Font = "robotocondensed-regular.ttf", FontSize = 23 }
            }, ShowRate);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-350 -75", OffsetMax = "-25 -10" },
                Button = { Color = "0 0 0 0", Material = "", Command = $"UI_UILootHandler recover {rate.PrefabName.Replace(" ", "+")}" },
                Text = { Text = _(player, "UI_RESTORE"), Color = "1 1 1 1", Align = TextAnchor.UpperRight, Font = "robotocondensed-regular.ttf", FontSize = 24 }
            }, ShowRate);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "25 10", OffsetMax = "350 75" },
                Button = { Color = "0 0 0 0", Command = $"UI_UILootHandler to {rate.PrefabName.Replace(" ", "+")}" },
                Text = { Text = _(player, "UI_COPY_TO_INVENTORY"), Align = TextAnchor.LowerLeft, Font = "robotocondensed-regular.ttf", FontSize = 24 }
            }, ShowRate);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-350 10", OffsetMax = "-25 75" },
                Button = { Color = "0 0 0 0", Command = $"UI_UILootHandler from {rate.PrefabName.Replace(" ", "+")}" },
                Text = { Text = _(player, "UI_COPY_FROM_INVENTORY"), Align = TextAnchor.LowerRight, Font = "robotocondensed-regular.ttf", FontSize = 24 }
            }, ShowRate);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-150 10", OffsetMax = "150 75" },
                Button = { Color = "0 0 0 0", Command = $"UI_UILootHandler from {rate.PrefabName.Replace(" ", "+")} add" },
                Text = { Text = _(player, "UI_ADD_FROM_INVENTORY"), Align = TextAnchor.LowerCenter, Font = "robotocondensed-regular.ttf", FontSize = 24 }
            }, ShowRate);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0.56", AnchorMax = "0 0.56", OffsetMin = "5 -125", OffsetMax = "125 0" },
                Button = { Color = "0 0 0 0", Material = "", Command = page > 0 ? $"UI_UILootHandler page {rate.PrefabName.Replace(" ", "+")} {page - 1}" : "" },
                Text = { Text = "<", Color = page > 0 ? "1 1 1 1" : "1 1 1 0.2", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf", FontSize = 80 }
            }, ShowRate);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "1 0.56", AnchorMax = "1 0.56", OffsetMin = "-125 -125", OffsetMax = "-5 0" },
                Button = { Color = "0 0 0 0", Material = "", Command = (page + 1) * 3 * SquareOnString < rate.ItemsList.Count ? $"UI_UILootHandler page {rate.PrefabName.Replace(" ", "+")} {page + 1}" : "" },
                Text = { Text = ">", Color = (page + 1) * 3 * SquareOnString < rate.ItemsList.Count ? "1 1 1 1" : "1 1 1 0.2", Align = TextAnchor.MiddleRight, Font = "robotocondensed-regular.ttf", FontSize = 80 }
            }, ShowRate);

            CuiHelper.AddUi(player, container);

            int stringNumber = 1;
            float stringAmount = Math.Min(Mathf.CeilToInt(rate.ItemsList.Count / (float)SquareOnString), 3);
            float leftPosition = stringNumber * SquareOnString / -2f * SquareSide - SquareOnString / 2f * SquareMargin;

            float topPosition = stringAmount / 2f * SquareSide + (stringAmount - 1) / 2f * 110;
            foreach (var check in rate.ItemsList.Skip((int)(page * 3 * SquareOnString)).Take((int)(3 * SquareOnString)).Select((i, t) => new { A = i, B = t - page * 3 * SquareOnString }))
            {
                UI_DrawItem(player, rate, check.A, leftPosition, topPosition);

                leftPosition += SquareMargin + SquareSide;
                if ((check.B + 1) % SquareOnString == 0)
                {
                    stringNumber++;
                    leftPosition = SquareOnString / -2f * SquareSide - SquareOnString / 2f * SquareMargin;
                    topPosition -= SquareSide + 110;
                }
            }
        }

        float SquareSide = 100;
        float SquareMargin = 5;
        int SquareOnString = 12;
        private void UI_DrawItem(BasePlayer player, RateHandler.Rates rate, RateHandler.Rates.Items item, float leftPos, float topPos, bool firstPass = true)
        {
            CuiElementContainer container = new CuiElementContainer();

            if (firstPass)
            {
                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = $"0.5 0.5", AnchorMax = "0.5 0.5",
                        OffsetMin = $"{leftPos} {topPos - SquareSide}",
                        OffsetMax = $"{leftPos + SquareSide} {topPos}"
                    },
                    Image = { Color = "1 1 1 0.03" }
                }, ShowRate, ShowRate + $".{rate.ItemsList.IndexOf(item)}");
            }

            CuiHelper.DestroyUi(player, ShowRate + $".{rate.ItemsList.IndexOf(item)}.BG");
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0 0", AnchorMax = "1 1", OffsetMin = $"0 0", OffsetMax = $"0 0" },
                Image = { Color = "1 1 1 0" }
            }, ShowRate + $".{rate.ItemsList.IndexOf(item)}", ShowRate + $".{rate.ItemsList.IndexOf(item)}.BG");

            if (item.IsBlueprint)
            {
                ItemDefinition definition = ItemManager.FindItemDefinition(item.ShortName);
                if (definition == null || definition.Blueprint == null || item.SkinID > 0)
                {
                    item.IsBlueprint = false;
                }
                else
                {
                    item.Amounts.Min = 1;
                    item.Amounts.Max = 1;

                    container.Add(new CuiElement
                    {
                        Parent = ShowRate + $".{rate.ItemsList.IndexOf(item)}.BG",
                        Components =
                        {
                            new CuiRawImageComponent { Png = this.GetItemImage("blueprintbase") },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                        }
                    });
                }
            }

            container.Add(new CuiElement
            {
                Parent = ShowRate + $".{rate.ItemsList.IndexOf(item)}.BG",
                Components =
                {
                    new CuiRawImageComponent { Png = this.GetItemImage(item.ShortName, item.SkinID) },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Command = $"UI_UILootHandler bp {rate.PrefabName.Replace(" ", "+")} {rate.ItemsList.IndexOf(item)}" },
                Text = { Text = "" }
            }, ShowRate + $".{rate.ItemsList.IndexOf(item)}.BG");

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 2", OffsetMax = "0 50" },
                Image = { Color = "1 1 1 0.01" },
            }, ShowRate + $".{rate.ItemsList.IndexOf(item)}.BG", ShowRate + $".{rate.ItemsList.IndexOf(item)}.Up");

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 -50", OffsetMax = "0 -2" },
                Image = { Color = "1 1 1 0.01" },
            }, ShowRate + $".{rate.ItemsList.IndexOf(item)}.BG", ShowRate + $".{rate.ItemsList.IndexOf(item)}.Down");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-35 -10", OffsetMax = "35 10" },
                Button = { Color = "0 0 0 0.9", Material = "assets/content/ui/uibackgroundblur.mat", Command = $"UI_UILootHandler remove {rate.PrefabName.Replace(" ", "+")} {rate.ItemsList.IndexOf(item)}" },
                Text = { Text = _(player, "UI_REMOVE"), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 14 }
            }, ShowRate + $".{rate.ItemsList.IndexOf(item)}.BG");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 25", OffsetMax = "0 0" },
                Text = { Text = _(player, "UI_MAXIMUM"), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = "1 1 1 0.4" }
            }, ShowRate + $".{rate.ItemsList.IndexOf(item)}.Up");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 -25" },
                Text = { Text = _(player, "UI_MINIMUM"), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = "1 1 1 0.4" }
            }, ShowRate + $".{rate.ItemsList.IndexOf(item)}.Down");


            CuiHelper.DestroyUi(player, ShowRate + $".{rate.ItemsList.IndexOf(item)}.precent.minus");
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "0 0", OffsetMax = "25 25" },
                Button = { Color = $"0.80190 0.4 0.4 {(item.DropChance - 1 >= 1 ? 0.3 : 0.1)}", Command = $"UI_UILootHandler minus {rate.PrefabName.Replace(" ", "+")} 1 {rate.ItemsList.IndexOf(item)}" },
                Text = { Text = "-", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 20 }
            }, ShowRate + $".{rate.ItemsList.IndexOf(item)}.BG", ShowRate + $".{rate.ItemsList.IndexOf(item)}.precent.minus");

            CuiHelper.DestroyUi(player, ShowRate + $".{rate.ItemsList.IndexOf(item)}.precent.plus");
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-25 0", OffsetMax = "0 25" },
                Button = { Color = $"0.4 0.8 0.4 {(item.DropChance + 1 > 100 ? 0.1 : 0.3)}", Command = $"UI_UILootHandler plus {rate.PrefabName.Replace(" ", "+")} 1 {rate.ItemsList.IndexOf(item)}" },
                Text = { Text = "+", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 20 }
            }, ShowRate + $".{rate.ItemsList.IndexOf(item)}.BG", ShowRate + $".{rate.ItemsList.IndexOf(item)}.precent.plus");

            CuiHelper.DestroyUi(player, ShowRate + $".{rate.ItemsList.IndexOf(item)}.Down.Min");
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -25", OffsetMax = "25 0" },
                Button = { Color = $"0.80190 0.4 0.4 {(item.Amounts.Min - 1 >= 1 ? 0.6 : 0.1)}", Command = $"UI_UILootHandler min {rate.PrefabName.Replace(" ", "+")} -1 {rate.ItemsList.IndexOf(item)}" },
                Text = { Text = "-", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 20 }
            }, ShowRate + $".{rate.ItemsList.IndexOf(item)}.Down", ShowRate + $".{rate.ItemsList.IndexOf(item)}.Down.Min");

            CuiHelper.DestroyUi(player, ShowRate + $".{rate.ItemsList.IndexOf(item)}.Down.Max");
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-25 -25", OffsetMax = "0 0" },
                Button = { Color = $"0.4 0.8 0.4 {(item.Amounts.Min + 1 <= item.Amounts.Max ? 0.6 : 0.1)}", Command = $"UI_UILootHandler min {rate.PrefabName.Replace(" ", "+")} 1 {rate.ItemsList.IndexOf(item)}" },
                Text = { Text = "+", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 20 }
            }, ShowRate + $".{rate.ItemsList.IndexOf(item)}.Down", ShowRate + $".{rate.ItemsList.IndexOf(item)}.Down.Max");

            CuiHelper.DestroyUi(player, ShowRate + $".{rate.ItemsList.IndexOf(item)}.Up.Min");
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "0 0", OffsetMax = "25 25" },
                Button = { Color = $"0.8 0.4 0.4 {(item.Amounts.Max - 1 >= item.Amounts.Min && item.Amounts.Max - 1 >= 0 ? 0.6 : 0.1)}", Command = $"UI_UILootHandler max {rate.PrefabName.Replace(" ", "+")} -1 {rate.ItemsList.IndexOf(item)}" },
                Text = { Text = "-", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", FontSize = 20 }
            }, ShowRate + $".{rate.ItemsList.IndexOf(item)}.Up", ShowRate + $".{rate.ItemsList.IndexOf(item)}.Up.Min");

            CuiHelper.DestroyUi(player, ShowRate + $".{rate.ItemsList.IndexOf(item)}.Up.Max");
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-25 0", OffsetMax = "0 25" },
                Button = { Color = $"0.405419087 0.801904584 0.40190879 {((item.SkinID > 0) ? 0.1 : 0.6)}", Command = $"UI_UILootHandler max {rate.PrefabName.Replace(" ", "+")} 1 {rate.ItemsList.IndexOf(item)}" },
                Text = { Text = "+", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 20 }
            }, ShowRate + $".{rate.ItemsList.IndexOf(item)}.Up", ShowRate + $".{rate.ItemsList.IndexOf(item)}.Up.Max");

            CuiHelper.DestroyUi(player, ShowRate + $".{rate.ItemsList.IndexOf(item)}.Precent");
            container.Add(new CuiElement
            {
                Parent = ShowRate + $".{rate.ItemsList.IndexOf(item)}.BG",
                Name = ShowRate + $".{rate.ItemsList.IndexOf(item)}.Precent.Input",
                Components =
                {
                    new CuiInputFieldComponent { Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Command  = $"UI_UILootHandler preset {rate.PrefabName.Replace(" ", "+")} {rate.ItemsList.IndexOf(item)} " },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "26 0", OffsetMax = "-26 25"}
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "26 0", OffsetMax = "-26 25" },
                Button = { Color = "0 0 0 0.2", Close = ShowRate + $".{rate.ItemsList.IndexOf(item)}.Precent" },
                Text = { Text = $"{item.DropChance}", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 14 }
            }, ShowRate + $".{rate.ItemsList.IndexOf(item)}.BG", ShowRate + $".{rate.ItemsList.IndexOf(item)}.Precent");

            CuiHelper.DestroyUi(player, ShowRate + $".{rate.ItemsList.IndexOf(item)}.Down.Minimum");
            container.Add(new CuiElement
            {
                Parent = ShowRate + $".{rate.ItemsList.IndexOf(item)}.Down",
                Name = ShowRate + $".{rate.ItemsList.IndexOf(item)}.Down.Minimum.Input",
                Components =
                {
                    new CuiInputFieldComponent { Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Command  = $"UI_UILootHandler minSet {rate.PrefabName.Replace(" ", "+")} {rate.ItemsList.IndexOf(item)} " },
                    new CuiRectTransformComponent {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "26 -25", OffsetMax = "-26 0"}
                }
            });
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "26 -25", OffsetMax = "-26 0" },
                Button = { Color = "0 0 0 0.2", Close = ShowRate + $".{rate.ItemsList.IndexOf(item)}.Down.Minimum" },
                Text = { Text = $"{item.Amounts.Min}", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 14 }
            }, ShowRate + $".{rate.ItemsList.IndexOf(item)}.Down", ShowRate + $".{rate.ItemsList.IndexOf(item)}.Down.Minimum");

            CuiHelper.DestroyUi(player, ShowRate + $".{rate.ItemsList.IndexOf(item)}.Up.Maximum");
            container.Add(new CuiElement
            {
                Parent = ShowRate + $".{rate.ItemsList.IndexOf(item)}.Up",
                Name = ShowRate + $".{rate.ItemsList.IndexOf(item)}.Up.Maximum.Input",
                Components =
                {
                    new CuiInputFieldComponent { Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Command = $"UI_UILootHandler maxSet {rate.PrefabName.Replace(" ", "+")} {rate.ItemsList.IndexOf(item)} " },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "26 0", OffsetMax = "-26 25"}
                }
            });
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "26 0", OffsetMax = "-26 25" },
                Button = { Color = "0 0 0 0.2", Close = ShowRate + $".{rate.ItemsList.IndexOf(item)}.Up.Maximum" },
                Text = { Text = $"{item.Amounts.Max}", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 14 }
            }, ShowRate + $".{rate.ItemsList.IndexOf(item)}.Up", ShowRate + $".{rate.ItemsList.IndexOf(item)}.Up.Maximum");

            CuiHelper.AddUi(player, container);
        }


        #endregion

        #region Commands
        [ConsoleCommand("UI_UILootHandler")]
        private void CmdConsoleHandler(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (!args.HasArgs(1) || !CanConfigure(player)) return;

            switch (args.Args[0].ToLower())
            {
                case "destroyui":
                    {
                        CuiHelper.DestroyUi(player, MainLayer);
                        break;
                    }
                case "to":
                    {
                        if (!args.HasArgs(2)) return;
                        string prefabName = args.Args[1].Replace("+", " ");
                        RateHandler.Rates rate = Handler.GetRates(prefabName);
                        if (rate == null)
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }

                        player.inventory.Strip();
                        NextTick(() =>
                        {
                            foreach (RateHandler.Rates.Items check in rate.ItemsList)
                            {
                                Item item = check.ToItem();
                                if (item == null)
                                    continue;

                                item.OnVirginSpawn();
                                if (!player.inventory.GiveItem(item))
                                    item.Drop(player.transform.position, Vector3.down);
                            }

                            CuiHelper.DestroyUi(player, MainLayer);
                            SendReply(player, _(player, "SuccessCopyItemsToInventory"));
                        });
                        break;
                    }
                case "populate":
                    {
                        if (!args.HasArgs(2)) return;
                        string prefabName = args.Args[1].Replace("+", " ");
                        RateHandler.Rates rate = Handler.GetRates(prefabName);
                        if (rate == null)
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }
                        //BaseNetworkable.serverEntities.Where(p => p.PrefabName == prefabName).ToList().ForEach(p => PopulateLoot((BaseEntity) p)); 
                        UpdateAllContainers(prefabName);
                        SaveData();
                        break;
                    }
                case "from":
                    {
                        if (!args.HasArgs(2)) return;
                        string prefabName = args.Args[1].Replace("+", " ");
                        RateHandler.Rates rate = Handler.GetRates(prefabName);
                        if (rate == null)
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }

                        //rate.MaxDropAmount = 1;
                        //rate.MinDropAmount = 1;
                        if (!args.HasArgs(3) || args.Args[2] != "add") rate.ItemsList.Clear();

                        foreach (Item item in player.inventory.containerMain.itemList)
                        {
                            if (!rate.ItemsList.Any(p => p.ShortName == item.info.shortname && p.SkinID == item.skin && p.IsBlueprint == item.IsBlueprint()))
                                rate.ItemsList.Add(RateHandler.Rates.Items.FromItem(item));
                        }

                        UI_DrawRate(player, rate, 0);
                        break;
                    }
                /*case "create":
                {
                    string prefabName = args.Args[1];
                    
                    var rate = Handler.CreateRates(prefabName);
                    if (rate == null)
                    {
                        CuiHelper.DestroyUi(player, MainLayer);
                        return;
                    }

                    UI_DrawRate(player, rate);
                    break;
                }*/
                case "preset":
                    {
                        if (!args.HasArgs(4)) return;
                        string prefabName = args.Args[1].Replace("+", " ");
                        RateHandler.Rates rate = Handler.GetRates(prefabName);
                        if (rate == null)
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }

                        RateHandler.Rates.Items item = rate.ItemsList.ElementAt(int.Parse(args.Args[2]));
                        if (item == null)
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }

                        int amount = -1;
                        if (!int.TryParse(args.Args[3], out amount))
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }

                        if (amount > 0 && amount <= 100)
                        {
                            item.DropChance = amount;
                        }

                        UI_DrawItem(player, rate, item, 0, 0, false);
                        break;
                    }
                case "minus":
                    {
                        if (!args.HasArgs(4)) return;
                        string prefabName = args.Args[1].Replace("+", " ");
                        RateHandler.Rates rate = Handler.GetRates(prefabName);
                        if (rate == null)
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }

                        int amount = -1;
                        if (!int.TryParse(args.Args[2], out amount))
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }

                        RateHandler.Rates.Items item = rate.ItemsList.ElementAt(int.Parse(args.Args[3]));
                        if (item == null)
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }

                        if (item.DropChance - amount < 1) return;

                        item.DropChance -= amount;

                        UI_DrawItem(player, rate, item, 0, 0, false);
                        break;
                    }
                case "plus":
                    {
                        if (!args.HasArgs(4)) return;
                        string prefabName = args.Args[1].Replace("+", " ");
                        RateHandler.Rates rate = Handler.GetRates(prefabName);
                        if (rate == null)
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }

                        int amount = -1;
                        if (!int.TryParse(args.Args[2], out amount))
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }

                        RateHandler.Rates.Items item = rate.ItemsList.ElementAt(int.Parse(args.Args[3]));
                        if (item == null)
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }

                        if (item.DropChance + amount > 100) return;
                        //if (item.DropChance + amount < 0) return; 

                        item.DropChance += amount;
                        UI_DrawItem(player, rate, item, 0, 0, false);
                        break;
                    }
                case "maxset":
                    {
                        if (!args.HasArgs(4)) return;
                        string prefabName = args.Args[1].Replace("+", " ");
                        RateHandler.Rates rate = Handler.GetRates(prefabName);
                        if (rate == null)
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }

                        RateHandler.Rates.Items item = rate.ItemsList.ElementAt(int.Parse(args.Args[2]));
                        if (item == null)
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }

                        int amount = -1;
                        if (!int.TryParse(args.Args[3], out amount))
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }

                        if (amount > 0)
                        {
                            if (item.Amounts.Min > amount)
                            {
                                amount = item.Amounts.Min;
                            }

                            if (item.IsBlueprint && amount > 1)
                                item.Amounts.Max = 1;
                            else
                                item.Amounts.Max = amount;
                        }

                        if (item.SkinID > 0 && amount > 1)
                        {
                            item.Amounts.Max = 1;
                        }

                        UI_DrawItem(player, rate, item, 0, 0, false);
                        break;
                    }
                case "minset":
                    {
                        if (!args.HasArgs(4)) return;
                        string prefabName = args.Args[1].Replace("+", " ");
                        RateHandler.Rates rate = Handler.GetRates(prefabName);
                        if (rate == null)
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }

                        RateHandler.Rates.Items item = rate.ItemsList.ElementAt(int.Parse(args.Args[2]));
                        if (item == null)
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }

                        int amount = -1;
                        if (!int.TryParse(args.Args[3], out amount))
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }

                        if (amount >= 0)
                        {
                            if (item.Amounts.Max < amount)
                            {
                                amount = item.Amounts.Max;
                            }

                            if (item.IsBlueprint && amount > 1)
                                item.Amounts.Min = 1;
                            else
                                item.Amounts.Min = amount;
                        }

                        if (item.SkinID > 0 && amount > 1)
                        {
                            item.Amounts.Min = 1;
                        }

                        if (item.Amounts.Min < 1)
                        {
                            item.Amounts.Min = 1;
                        }

                        UI_DrawItem(player, rate, item, 0, 0, false);
                        break;
                    }
                case "min":
                    {
                        if (!args.HasArgs(4)) return;
                        string prefabName = args.Args[1].Replace("+", " ");
                        RateHandler.Rates rate = Handler.GetRates(prefabName);
                        if (rate == null)
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }

                        int amount = -1;
                        if (!int.TryParse(args.Args[2], out amount))
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }

                        RateHandler.Rates.Items item = rate.ItemsList.ElementAt(int.Parse(args.Args[3]));
                        if (item == null)
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }

                        if (item.Amounts.Min + amount > item.Amounts.Max) return;
                        if (item.Amounts.Min + amount < 0) return;

                        if (item.IsBlueprint && amount > 1)
                            item.Amounts.Min = 1;
                        else
                            item.Amounts.Min += amount;

                        if (item.SkinID > 0 && amount > 1)
                        {
                            item.Amounts.Min = 1;
                        }

                        if (item.Amounts.Min < 1)
                        {
                            item.Amounts.Min = 1;
                        }

                        UI_DrawItem(player, rate, item, 0, 0, false);
                        break;
                    }
                case "max":
                    {
                        if (!args.HasArgs(4)) return;
                        string prefabName = args.Args[1].Replace("+", " ");
                        RateHandler.Rates rate = Handler.GetRates(prefabName);
                        if (rate == null)
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }

                        int amount = -1;
                        if (!int.TryParse(args.Args[2], out amount))
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }

                        RateHandler.Rates.Items item = rate.ItemsList.ElementAt(int.Parse(args.Args[3]));
                        if (item == null)
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }

                        if (item.Amounts.Max + amount < item.Amounts.Min) return;
                        if (item.Amounts.Max + amount < 0) return;

                        if (item.IsBlueprint && amount > 1)
                            item.Amounts.Max = 1;
                        else
                            item.Amounts.Max += amount;

                        if (item.SkinID > 0 && item.Amounts.Max > 1)
                        {
                            item.Amounts.Max = 1;
                        }

                        UI_DrawItem(player, rate, item, 0, 0, false);
                        break;
                    }
                case "page":
                    {
                        if (!args.HasArgs(3)) return;
                        string prefabName = args.Args[1].Replace("+", " ");
                        RateHandler.Rates rate = Handler.GetRates(prefabName);
                        if (rate == null)
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }

                        UI_DrawRate(player, rate, int.Parse(args.Args[2]));
                        break;
                    }
                case "recover":
                    {
                        if (!args.HasArgs(2)) return;
                        string prefabName = args.Args[1].Replace("+", " ");
                        RateHandler.Rates rate = Handler.GetRates(prefabName);
                        if (rate == null)
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }

                        Handler.Rateses.Remove(rate);
                        SaveData();

                        UpdateAllContainers(prefabName, true);
                        CuiHelper.DestroyUi(player, MainLayer);
                        break;
                    }
                case "amount":
                    {
                        if (!args.HasArgs(3)) return;
                        string prefabName = args.Args[1].Replace("+", " ");
                        RateHandler.Rates rate = Handler.GetRates(prefabName);
                        if (rate == null)
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }

                        if (rate.MaxDropAmount + int.Parse(args.Args[2]) > rate.ItemsList.Count) return;
                        if (rate.MaxDropAmount + int.Parse(args.Args[2]) < 1) return;
                        if (rate.MaxDropAmount + int.Parse(args.Args[2]) > 36) return;

                        rate.MaxDropAmount += int.Parse(args.Args[2]);
                        rate.MinDropAmount = rate.MaxDropAmount;

                        CuiHelper.DestroyUi(player, ShowRate + ".Amount");
                        CuiElementContainer container = new CuiElementContainer();
                        container.Add(new CuiButton
                        {
                            RectTransform = { AnchorMin = "0.6 1", AnchorMax = "0.6 1", OffsetMin = "-175 -75", OffsetMax = "-150 -10" },
                            Button = { Color = "0 0 0 0", Command = $"UI_UILootHandler" },
                            Text = { Text = (rate.MaxDropAmount).ToString(), Align = TextAnchor.UpperCenter, Font = "robotocondensed-regular.ttf", FontSize = 24 }
                        }, ShowRate, ShowRate + ".Amount");

                        CuiHelper.AddUi(player, container);

                        break;
                    }
                case "bp":
                    {
                        if (!args.HasArgs(3)) return;
                        string prefabName = args.Args[1].Replace("+", " ");
                        RateHandler.Rates rate = Handler.GetRates(prefabName);
                        if (rate == null)
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }

                        RateHandler.Rates.Items item = rate.ItemsList.ElementAt(int.Parse(args.Args[2]));
                        if (item == null)
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }

                        item.IsBlueprint = !item.IsBlueprint;
                        UI_DrawItem(player, rate, item, 0, 0, false);
                        break;
                    }
                case "remove":
                    {
                        if (!args.HasArgs(3)) return;
                        string prefabName = args.Args[1].Replace("+", " ");
                        RateHandler.Rates rate = Handler.GetRates(prefabName);
                        if (rate == null)
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }

                        RateHandler.Rates.Items item = rate.ItemsList.ElementAt(int.Parse(args.Args[2]));
                        if (item == null)
                        {
                            CuiHelper.DestroyUi(player, MainLayer);
                            return;
                        }

                        rate.ItemsList.Remove(item);
                        UI_DrawRate(player, rate, 0);
                        break;
                    }
            }
        }

        #endregion

        #region Functions
        private void UpdateAllContainers(string prefabName = null, bool defaultLoot = false)
        {
            int updateContainers = 0;
            NextTick(() =>
            {
                PrintWarning("Getting started updating LootContainer...");
                foreach (LootContainer container in BaseNetworkable.serverEntities.Where(p => p != null && p.GetComponent<BaseEntity>() != null && p is LootContainer).Cast<LootContainer>().ToList())
                {
                    if (container == null)
                        continue;

                    if (prefabName != null && prefabName != container.PrefabName)
                        continue;

                    if (defaultLoot)
                    {
                        ClearContainer(container);
                        container.PopulateLoot();
                        container.inventory.capacity = container.inventory.itemList.Count;
                        container.inventory.MarkDirty();

                        updateContainers++;
                        continue;
                    }

                    if (PopulateLoot(container, true))
                        updateContainers++;
                }

                PrintWarning($"Updated {updateContainers} LootContainer.");
                ItemManager.DoRemoves();
            });
        }

        private bool PopulateLoot(LootContainer container, bool firstStart = false)
        {
            RateHandler.Rates rate = Handler.GetRates(container);
            if (rate == null)
                return false;

            ClearContainer(container);

            if (firstStart)
                NextTick(() => DoPopulateLoot(container, rate));
            else
                DoPopulateLoot(container, rate);

            return true;
        }

        private void DoPopulateLoot(LootContainer container, RateHandler.Rates rate)
        {
            List<RateHandler.Rates.Items> listItems = GetRandom(rate);

            foreach (RateHandler.Rates.Items check in listItems)
            {
                Item item = check.ToItem();
                if (item == null)
                    continue;

                item.OnVirginSpawn();
                if (!item.MoveToContainer(container.inventory, -1, true))
                {
                    item.Remove(0f);
                }
            }

            if (rate.UsingCondition)
            {
                foreach (Item ci in container.inventory.itemList)
                {
                    if (ci.hasCondition)
                    {
                        ci.condition = UnityEngine.Random.Range(ci.info.condition.foundCondition.fractionMin, ci.info.condition.foundCondition.fractionMax) * ci.info.condition.max;
                    }
                }
            }

            if (rate.ScrapSettings.EnableDrop)
            {
                Item scrap = rate.ScrapSettings.CreateScrap();
                if (!scrap.MoveToContainer(container.inventory, -1, true))
                {
                    scrap.Remove(0f);
                }
            }

            container.inventory.capacity = container.inventory.itemList.Count;
            container.inventory.MarkDirty();
            container.inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);
            container.SendNetworkUpdate();
            ItemManager.DoRemoves();
        }

        private void ClearContainer(LootContainer container)
        {
            if (container.inventory == null)
            {
                container.inventory = new ItemContainer();
                container.inventory.ServerInitialize(null, 36);
                container.inventory.GiveUID();
            }
            else
            {
                while (container.inventory.itemList.Count > 0)
                {
                    Item item = container.inventory.itemList[0];
                    item.RemoveFromContainer();
                    item.Remove(0f);
                }
                container.inventory.capacity = 36;
            }

            container.inventory.itemList.Clear();
            container.inventory.Clear();
            ItemManager.DoRemoves();
        }

        private List<RateHandler.Rates.Items> GetRandom(RateHandler.Rates rate)
        {
            List<RateHandler.Rates.Items> result = new List<RateHandler.Rates.Items>();
            int amount = (rate.MaxDropAmount == rate.MinDropAmount || rate.MinDropAmount <= 0) ? rate.MaxDropAmount : Core.Random.Range(rate.MinDropAmount, rate.MaxDropAmount);

            for (int i = 0; i < amount; i++)
            {
                if (i > 36)
                    break;

                RateHandler.Rates.Items item = null;
                int iteration = 0;
                do
                {
                    iteration++;

                    RateHandler.Rates.Items randomItem = rate.ItemsList[UnityEngine.Random.Range(0, rate.ItemsList.Count)];
                    if (result.Contains(randomItem))
                        continue;

                    if (randomItem.DropChance < 1 || randomItem.DropChance > 100)
                        continue;

                    if (UnityEngine.Random.Range(0, 100) <= randomItem.DropChance)
                        item = randomItem;
                }
                while (item == null && iteration < 1000);

                if (item != null)
                    result.Add(item);
            }

            return result;
        }

        public ItemDefinition GetBlueprintBaseDef()
        {
            return ItemManager.FindItemDefinition("blueprintbase");
        }

        public string GetItemImage(string shortname, ulong skinID = 0)
        {
            if (skinID > 0)
            {
                if (ImageLibrary.Call<bool>("HasImage", shortname, skinID) == false && ImageLibrary.Call<Dictionary<string, object>>("GetSkinInfo", shortname, skinID) == null)
                {
                    if (configuration.SteamWebApiKey == null || configuration.SteamWebApiKey == string.Empty || configuration.SteamWebApiKey.Length != 32)
                    {
                        PrintError($"Steam Web API key not set! Check the configuration!");
                        return ImageLibrary.Call<string>("GetImage", shortname);
                    }
                    else
                    {
                        webrequest.Enqueue("https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/", $"key={configuration.SteamWebApiKey}&itemcount=1&publishedfileids%5B0%5D={skinID}", (code, response) =>
                        {
                            if (code != 200 || response == null)
                            {
                                PrintError($"Image failed to download! Code HTTP error: {code} - Image Name: {shortname} - Image skinID: {skinID} - Response: {response}");
                                return;
                            }

                            SteampoweredResult sr = JsonConvert.DeserializeObject<SteampoweredResult>(response);
                            if (sr == null || !(sr is SteampoweredResult) || sr.response.result == 0 || sr.response.resultcount == 0)
                            {
                                PrintError($"Image failed to download! Error: Parse JSON response - Image Name: {shortname} - Image skinID: {skinID} - Response: {response}");
                                return;
                            }

                            foreach (SteampoweredResult.Response.PublishedFiled publishedfiled in sr.response.publishedfiledetails)
                            {
                                ImageLibrary.Call("AddImage", publishedfiled.preview_url, shortname, skinID);
                            }

                        }, this, RequestMethod.POST);

                        return ImageLibrary.Call<string>("GetImage", "LOADING");
                    }
                }
            }

            return ImageLibrary.Call<string>("GetImage", shortname, skinID);
        }

        // Reserved4 -> Typing
        private void CreateInput(BasePlayer player)
        {
            if (player.HasFlag(BaseEntity.Flags.Reserved4)) return;

            // TODO: Create Input
            ServerMgr.Instance.StartCoroutine(PreparePlayer(player));
        }

        private object OnPlayerSleepEnded(BasePlayer player)
        {
            if (player.HasFlag(BaseEntity.Flags.Reserved4)) return false;

            return null;
        }

        private IEnumerator PreparePlayer(BasePlayer player)
        {
            player.SetFlag(BaseEntity.Flags.Reserved4, true);
            player.StartSleeping();

            yield return new WaitWhile(() => player.HasFlag(BaseEntity.Flags.Reserved4) || !player.IsConnected);

            player.SetFlag(BaseEntity.Flags.Reserved4, false);
            player.EndSleeping();
        }

        private bool CanConfigure(BasePlayer player) => player.IsAdmin || permission.UserHasPermission(player.UserIDString, AdminPermission);
        #endregion
    }
}