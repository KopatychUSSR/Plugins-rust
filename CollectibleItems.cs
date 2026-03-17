using Newtonsoft.Json;
using Oxide.Core;
using System.Collections.Generic;
using System;
using UnityEngine;
namespace Oxide.Plugins
{
    [Info("CollectibleItems", "https://topplugin.ru/", "1.0.0")]
    class CollectibleItems : RustPlugin
    {
        private PluginConfig config;

        protected override void LoadDefaultConfig() => config = PluginConfig.DefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(config, true);
        }

        protected override void SaveConfig() => Config.WriteObject(config);        

        private class DefaultLootItems
        {
            [JsonProperty("ShortName предмета")]
            public string ShortName;
            [JsonProperty("Минимальное количество")]
            public int Min;
            [JsonProperty("Максимальное количество")]
            public int Max;
            [JsonProperty("SkinID предмета")]
            public ulong SkinID;
            [JsonProperty("Имя предмета")]
            public string Name;
        }

        private class PluginConfig
        {            
            [JsonProperty("Рейт конопли при сборе (Стандартный 1)")]
            public int Rates;
            [JsonProperty("Максимальное количество итемов какое рандомно может дать игроку при сборе одного ресурса")]
            public int MaxCount;
            [JsonProperty("Шанс выпадения")]
            public int Change;
            [JsonProperty("Настройка выдаваемости предметов при сборе")]
            public List<DefaultLootItems> LootSettings = new List<DefaultLootItems>();
            [JsonProperty("Сколько времени будет расти конопля в грядке (секунды)")]
            public int TimeToEnd;            

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {         
                    MaxCount = 1,
                    Rates = 4,
                    TimeToEnd = 7200,
                    Change = 50,                    
                    LootSettings = new List<DefaultLootItems>()
                    {
                        new DefaultLootItems
                        {
                            ShortName = "largemedkit",
                            Max = 1,
                            Min = 1,
                            SkinID = 0,
							Name = ""
                        },
                        new DefaultLootItems
                        {
                            ShortName = "syringe.medical",
                            Max = 1,
                            Min = 1,
                            SkinID = 0,
							Name = ""
                        },
						new DefaultLootItems
                        {
                            ShortName = "antiradpills",
                            Max = 1,
                            Min = 1,
                            SkinID = 1916799504,
							Name = "Веселые таблетки"
                        }
                    }
                };
            }
        }

        object OnGrowableGathered(GrowableEntity plant, Item item, BasePlayer player)
        {
            if (plant.GetParentEntity() is PlanterBox)
            {
                if (item.info.shortname.Contains("cloth") && plant.GetComponent<GrowableEntity>() != null && (plant.GetComponent<GrowableEntity>().State.ToString() == "Fruiting" || plant.GetComponent<GrowableEntity>().State.ToString() == "Ripe"))
                {
                    var new_amount = (item.amount * config.Rates);
                    item.amount = new_amount;
                    if (UnityEngine.Random.Range(0, 100) < config.Change) return null;
                    var giveitme = config.LootSettings.GetRandom();
                    var newItem = ItemManager.CreateByName(giveitme.ShortName, UnityEngine.Random.Range(giveitme.Min, giveitme.Max + 1), giveitme.SkinID);
                    if (!string.IsNullOrEmpty(giveitme.Name))
                        newItem.name = giveitme.Name;
                    player.GiveItem(newItem, BaseEntity.GiveItemReason.ResourceHarvested);
                }
            }
            return null;
        }        

		private ulong GetSkinByName(string name)
		{
			foreach (var item in config.LootSettings)			
				if (item.ShortName == "antiradpills")
					return item.SkinID;
			
			return 100;
		}
		
        private double GrabCurrentTime() => DateTime.UtcNow.ToLocalTime().Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;

        public Dictionary<BasePlayer, double> Cooldown = new Dictionary<BasePlayer, double>();

        object OnItemSplit(Item item, int split_Amount)
        {
            if (item.info.shortname == "antiradpills" && item.skin == GetSkinByName("antiradpills"))
            {
                Item byItemId = ItemManager.CreateByItemID(item.info.itemid, 1, item.skin);
                item.amount -= split_Amount;
                byItemId.amount = split_Amount;
                byItemId.name = item.name;
                item.MarkDirty();
                return byItemId;
            }
            return null;
        }

        object CanCombineDroppedItem(DroppedItem drItem, DroppedItem anotherDrItem)
        {
            if (drItem.item.info.shortname == "antiradpills" && drItem.item.info.itemid == anotherDrItem.item.info.itemid && drItem.item.skin != anotherDrItem.item.skin) return false;
            return null;
        }

        object CanStackItem(Item item, Item anotherItem)
        {
            if (item.info.shortname == "antiradpills" && item.skin != anotherItem.skin) return false;
            return null;
        }

        object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (item == null) return null;
            if (action == "consume" && item.info.shortname == "antiradpills" && item.skin == GetSkinByName("antiradpills"))
            {
                if (!Cooldown.ContainsKey(player))
                    Cooldown.Add(player, GrabCurrentTime() + 1);
                else if (Cooldown[player] > GrabCurrentTime()) return false;
                
				item.UseItem();
                player.health += 5;
                player.SendNetworkUpdate();
                Effect.server.Run("assets/bundled/prefabs/fx/gestures/take_pills.prefab", player, 0, Vector3.zero, Vector3.forward);
                Cooldown[player] = GrabCurrentTime() + 1.2;
                return false;
            }
            return null;
        }
		
		[ChatCommand("addpills")]
        private void cmdAddPills(BasePlayer player)
        {
            if (!player.IsAdmin) return;
            var newItem = ItemManager.CreateByName("antiradpills", 10, GetSkinByName("antiradpills"));
            newItem.name = "Веселые таблетки";
            player.GiveItem(newItem, BaseEntity.GiveItemReason.PickedUp);
        }

    }
}
