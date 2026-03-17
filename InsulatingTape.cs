using Newtonsoft.Json;
using Oxide.Core;
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Oxide.Plugins
{
    [Info("InsulatingTape", "https://topplugin.ru / https://discord.com/invite/5DPTsRmd3G", "1.1.8")]

    class InsulatingTape : RustPlugin
    {				
        private PluginConfig config;

        protected override void LoadDefaultConfig() => config = PluginConfig.DefaultConfig();
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
            if (config.PluginVersion < Version)
                UpdateConfigValues();
            Config.WriteObject(config, true);
        }

        private void UpdateConfigValues()
        {
            PluginConfig baseConfig = PluginConfig.DefaultConfig();
            if (config.PluginVersion < new VersionNumber(0, 1, 0))
            {
                PrintWarning("Config update detected! Updating config values...");
                PrintWarning("Config update completed!");
            }
            config.PluginVersion = Version;
        }

        public class InsulatingTapeSettings
        {
            [JsonProperty("SkinID изоленты")]
            public ulong SkinID;
            [JsonProperty("Shortname предмета на какой налаживаем скин")]
            public string Shortname;
            [JsonProperty("Имя предмета")]
            public string Name;
            [JsonProperty("Список предметов какие нельзя ченить (shortname)")]
            public List<string> BlackList;
            [JsonProperty("Сколько HP ввостанавливает изолента?")]
            public int HP;
            [JsonProperty("Изолента может ченить полностью поломаное оружие/броню/инструмент?")]
            public bool MendItems;
            [JsonProperty("Настройка выпадаемости в луте")]
            public Dictionary<string, DefaultLoot> LootSettings = new Dictionary<string, DefaultLoot>();
        }

        public class DefaultLoot
        {
            [JsonProperty("Минимальное количество")]
            public int Min;
            [JsonProperty("Максимальное количество")]
            public int Max;
            [JsonProperty("Шанс что он появится в луте")]
            public int Change;
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        class PluginConfig
        {
            [JsonProperty("Configuration Version")]
            public VersionNumber PluginVersion = new VersionNumber();

            [JsonProperty("Настройки предмета")]
            public InsulatingTapeSettings Settings;

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    PluginVersion = new VersionNumber(),
                    Settings = new InsulatingTapeSettings()
                    {
                        HP = 10,
                        MendItems = true,
                        Shortname = "ducttape",
                        SkinID = 1916777805,
                        Name = "Священная изолента",
                        BlackList = new List<string>()
                        {
                            "crossbow",
                            "roadsign.jacket"
                        },
                        LootSettings = new Dictionary<string, DefaultLoot>()
                        {
                            ["crate_normal"] = new DefaultLoot()
                            {
                                Change = 30,
                                Max = 1,
                                Min = 1,
                            },
                            ["loot-barrel-1"] = new DefaultLoot()
                            {
                                Change = 30,
                                Max = 1,
                                Min = 1,
                            },
                            ["loot-barrel-2"] = new DefaultLoot()
                            {
                                Change = 30,
                                Max = 1,
                                Min = 1,
                            },
                            ["loot_barrel_2"] = new DefaultLoot()
                            {
                                Change = 30,
                                Max = 1,
                                Min = 1,
                            },
                            ["crate_tools"] = new DefaultLoot()
                            {
                                Change = 40,
                                Max = 1,
                                Min = 1,
                            },
                        }
                    }

                };
            }
        }

        public static Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            {"ItemBlockRepair", "Предмет {0} не нуждается в починке, он цел" },
            {"ItemHasBlackList", "Предмет {0} запрещено ченить" },
            {"ItemHasBrocken", "Предмет {0} запрещено ченить, он полностью поврежден" },
        };

        object CanMoveItem(Item item, PlayerInventory playerLoot, uint targetContainer, int targetSlot, int amount)
        {
            if (item == null || playerLoot == null) return null;
            var player = playerLoot.containerMain.playerOwner;
            if (player == null) return null;
            if (item.info.shortname == config.Settings.Shortname && item.skin == config.Settings.SkinID)
            {
                var container = playerLoot.FindContainer(targetContainer);
                if (container != null)
                {
                    var getItem = container.GetSlot(targetSlot);
                    if (getItem != null)
                    {
                        if (getItem.info.category == ItemCategory.Weapon || getItem.info.category == ItemCategory.Tool || getItem.info.category == ItemCategory.Attire && getItem.hasCondition)
                        {
                            if (getItem.isBroken && !config.Settings.MendItems)
                            {
                                SendReply(player, string.Format(Messages["ItemHasBrocken"], getItem.info.displayName.english));
                                return false;
                            }
                            if (config.Settings.BlackList != null && config.Settings.BlackList.Contains(getItem.info.shortname))
                            {
                                SendReply(player, string.Format(Messages["ItemHasBlackList"], getItem.info.displayName.english));
                                return false;
                            }

                            if (getItem.condition >= getItem.maxCondition)
                            {
                                SendReply(player, string.Format(Messages["ItemBlockRepair"], getItem.info.displayName.english));
                                return false;
                            }
                            var NeedAmount = Math.Round((getItem.maxCondition - getItem.condition) / config.Settings.HP);
                            if (NeedAmount == 0) NeedAmount = 1;
                            if (amount > NeedAmount)
                            {
                                item.amount -= (int)NeedAmount;
                                item.MarkDirty();
                                getItem.maxCondition = getItem.maxCondition - ((getItem.maxCondition * (int)NeedAmount / 100) * 3f);								
                                getItem.condition = getItem.maxCondition;
								
								var heldEntity = getItem.GetHeldEntity();
								if (heldEntity != null)								
									heldEntity.SetFlag(BaseEntity.Flags.Broken, false, false, true);
								
                                Effect.server.Run("assets/bundled/prefabs/fx/repairbench/itemrepair.prefab", player, 0, Vector3.zero, Vector3.forward);
                                return false;
                            }
                            if (amount == NeedAmount)
                            {
                                if (amount == item.amount)
                                    item.RemoveFromContainer();
                                else if (amount < item.amount)
                                {
                                    item.amount -= amount;
                                    item.MarkDirty();
                                }
                                getItem.maxCondition = getItem.maxCondition - ((getItem.maxCondition * (int)NeedAmount / 100) * 3f);								
                                getItem.condition = getItem.maxCondition;
								
								var heldEntity = getItem.GetHeldEntity();
								if (heldEntity != null)								
									heldEntity.SetFlag(BaseEntity.Flags.Broken, false, false, true);
								
                                Effect.server.Run("assets/bundled/prefabs/fx/repairbench/itemrepair.prefab", player, 0, Vector3.zero, Vector3.forward);
                                return false;
                            }
                            if (amount < NeedAmount)
                            {
                                if (amount == item.amount)
                                    item.RemoveFromContainer();
                                else if (amount < item.amount)
                                {
                                    item.amount -= amount;
                                    item.MarkDirty();
                                }
                                getItem.maxCondition = getItem.maxCondition - ((getItem.maxCondition * amount / 100) * 3f);								
                                getItem.condition = getItem.condition + (config.Settings.HP * amount);
								
								var heldEntity = getItem.GetHeldEntity();
								if (heldEntity != null)								
									heldEntity.SetFlag(BaseEntity.Flags.Broken, false, false, true);
								
                                Effect.server.Run("assets/bundled/prefabs/fx/repairbench/itemrepair.prefab", player, 0, Vector3.zero, Vector3.forward);
                                return false;
                            }
                        }
                    }
                }
            }
            return null;
        }

        [ChatCommand("ducttape")]
        void cmdGiveDuctTape(BasePlayer player, string command, string[] args)
        {
			if (args == null || args.Length == 0) return;
			
            if (player.IsAdmin)
            {
                var type = args[0];
                if (args.Length == 1)
                {
                    int amount;
                    if (!int.TryParse(args[0], out amount))
                    {
                        SendReply(player, "Вы не указали количество, используйте /ducttape AMOUNT");

                        return;
                    }
                    AddDuctTape(player, amount);
                    return;
                }
                if (args.Length > 0 && args.Length == 2)
                {
                    var target = BasePlayer.Find(args[0]);
                    if (target == null)
                    {
                        SendReply(player, "Данный игрок не найден, попробуйте уточнить имя или SteamID, используйте /seed TARGETNAME/ID AMOUNT");
                        return;
                    }

                    int amount;
                    if (!int.TryParse(args[1], out amount))
                    {
                        SendReply(player, "Вы не указали количество, используйте /ducttape TARGETNAME/ID AMOUNT");
                        return;
                    }
                    AddDuctTape(target, amount);
                }
            }
            else
                SendReply(player, "У тебя нету прав использовать данную команду");
        }

        void AddDuctTape(BasePlayer player, int amount)
        {
            Item ducktype = ItemManager.CreateByName(config.Settings.Shortname, amount, config.Settings.SkinID);
            ducktype.name = config.Settings.Name;
            player.GiveItem(ducktype, BaseEntity.GiveItemReason.PickedUp);
        }

        void OnServerInitialized()
        {
            lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
        }

        private void OnLootSpawn(LootContainer lootContainer)
        {
            if (lootContainer == null || lootContainer?.net.ID == null) return;
            NextTick(() => LootMoveToContainer(lootContainer));
        }

        private void LootMoveToContainer(LootContainer container)
        {
            if (container == null || container.inventory == null) return;

            if (config.Settings.LootSettings.ContainsKey(container.ShortPrefabName))
            {
                var loot = config.Settings.LootSettings[container.ShortPrefabName];
                if (UnityEngine.Random.Range(1, 100) > loot.Change) return;
                if (container.inventory.itemList.Count == container.inventorySlots) return;
                var item = ItemManager.CreateByName(config.Settings.Shortname, UnityEngine.Random.Range(loot.Min, loot.Max), config.Settings.SkinID);
                item.name = config.Settings.Name;
                if (container.inventory.itemList.Count == container.inventory.capacity)
                {
                    container.inventory.capacity++;
                    container.inventory.MarkDirty();
                }
                item.MoveToContainer(container.inventory);
                container.inventory.MarkDirty();
            }
        }

        /*object CanStackItem(Item item, Item anotherItem) 
        {
            if (item.info.shortname == config.Settings.Shortname && item.skin != anotherItem.skin) return false;
            return null;
        }

        object OnItemSplit(Item item, int split_Amount)
        {
            if (item.info.shortname == config.Settings.Shortname && item.skin == config.Settings.SkinID)
            {
                Item byItemId = ItemManager.CreateByItemID(item.info.itemid, 1, item.skin);
                item.amount -= split_Amount;
                byItemId.amount = split_Amount;
                byItemId.name = item.name;
                item.MarkDirty();
                return byItemId;
            }
            return null;
        }*/

        object CanCombineDroppedItem(DroppedItem drItem, DroppedItem anotherDrItem)
        {
            if (drItem.item.info.shortname == config.Settings.Shortname && drItem.item.info.itemid == anotherDrItem.item.info.itemid && drItem.item.skin != anotherDrItem.item.skin) return false;
            return null;
        }
		
		// кривой хук, он запретит работу рекуклера, даже если там есть разрешенные к переработке предметы
		private bool? CanRecycle(Recycler rec, Item item)
		{
			if (rec == null || item == null) return null;
			
			if (item.skin == config.Settings.SkinID)
				return false;
			
			return null;
		}

    }
}