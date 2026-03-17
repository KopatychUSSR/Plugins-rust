using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("BottleFeature", "topplugin.ru", "2.0.3")]
    class BottleFeature : RustPlugin
    {
        public static BottleFeature bottleFeature;

        #region Configuration
        private static Configuration config = new Configuration();
        public class Configuration
        {
            internal class Settings
            {
                [JsonProperty("Отображаемое имя", Order =0)]
                public string DisplayName;
                [JsonProperty("Статическое названия(НЕ МЕНЯТЬ!)", Order = 1)]
                public string StatickName;
                [JsonProperty("Скин бутылки")]
                public ulong skinid;
                [JsonProperty("Название предмета который он будет заменять", Order = 2)]
                public string ReplaceShortName;
                [JsonProperty("Шанс выпадения", Order = 3)]
                public int DropChance;
                [JsonProperty("Кол-вл выпадения", Order = 4)]
                public int DropAmount;
                [JsonProperty("Ящики в которых будут падать бутылки", Order = 5)]
                public List<string> crate;
                [JsonProperty("Буст", Order = 6)]
                public float Boost;
                [JsonProperty("Время действия буста", Order = 7)]
                public int TimeForBoost;

                public Item Copy(int amount = 1)
                {
                    Item x = ItemManager.CreateByPartialName(ReplaceShortName, amount);
                    x.skin = skinid;
                    x.name = DisplayName;

                    return x;
                }

                public Item CreateItem(int amount)
                {
                    Item item = ItemManager.CreateByPartialName(ReplaceShortName, amount);
                    item.name = DisplayName;
                    item.skin = skinid;

                    var item1 = ItemManager.CreateByPartialName("water", 100);
                    item1.MoveToContainer(item.contents);

                    return item;
                }
            }

            [JsonProperty("Настройка")]
            public List<Settings> settings = new List<Settings>();

            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    settings = new List<Configuration.Settings>
                    {
                      new Configuration.Settings
                      {
                        DisplayName = "Лечебная бутылочка",
                        StatickName = "HealBottle",
                        ReplaceShortName = "smallwaterbottle",
                        skinid = 1977065489,
                        DropAmount = 1,
                        DropChance = 50,
                        Boost = 0.5f,
                        TimeForBoost = 30,
                        crate = new List<string>
                        {
                            "foodbox"
                        }
                      },
                      new Configuration.Settings
                      {
                        DisplayName = "Защитная бутылочка",
                        StatickName = "ArmorBottle",
                        ReplaceShortName = "smallwaterbottle",
                        skinid = 1977066084,
                        DropAmount = 1,
                        DropChance = 50,
                        Boost = 30f,
                        TimeForBoost = 50,
                        crate = new List<string>
                        {
                            "crate_normal"
                        }
                      },
                      new Configuration.Settings
                      {
                        DisplayName = "Усиляющая бутылочка",
                        StatickName = "DamageBottle",
                        ReplaceShortName = "smallwaterbottle",
                        skinid = 1977066766,
                        DropAmount = 1,
                        DropChance = 50,
                        Boost = 10f,
                        TimeForBoost = 20,
                        crate = new List<string>
                        {
                            "crate_normal_2"
                        }
                      }
                    }
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
                PrintWarning("Ошибка чтения конфигурации 'oxide/config/', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Classes

        public class BoostPlayer
        {
            public Timer HealTimer;
            public Timer DamageTimer;
            public Timer ArmorTimer;

            public void StartBoos(BasePlayer player, string boost)
            {
                switch (boost)
                {
                    case "HealBottle":
                        {
                            StartHeal(player);
                            break;

                        }
                    case "ArmorBottle":
                        {
                            StartArmor(player);                          
                            break;
                        }
                    case "DamageBottle":
                        {
                            StartDamage(player);
                            break;
                        }
                }
            }

            public void StartHeal(BasePlayer player)
            {
                if (HealTimer != null)
                {
                    HealTimer = null;
                }
                HealTimer = bottleFeature.timer.Once(config.settings[0].TimeForBoost, () =>
                {
                    HealTimer = null;
                });
            }

            public void StartDamage(BasePlayer player)
            {
                if (DamageTimer != null)
                {
                    DamageTimer = null;
                }
                DamageTimer = bottleFeature.timer.Once(config.settings[2].TimeForBoost, () =>
                {
                    DamageTimer = null;
                });
            }

            public void StartArmor(BasePlayer player)
            {
                if (ArmorTimer != null)
                {
                    ArmorTimer = null;
                }
                ArmorTimer = bottleFeature.timer.Once(config.settings[1].TimeForBoost, () =>
                {
                    ArmorTimer = null;
                });
            }
        }

        public Dictionary<BasePlayer, BoostPlayer> ActiveBoost = new Dictionary<BasePlayer, BoostPlayer>();

        #endregion

        #region Commands

        [ChatCommand("bottle.give")]
        private void CmdChatDebugGoldSpawn(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;

            foreach (var check in config.settings)
            {
                var item = check.CreateItem(1);
                item.MoveToContainer(player.inventory.containerMain);
            }
        }

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            bottleFeature = this;
            for(int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                OnPlayerConnected(BasePlayer.activePlayerList[i]);
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (!ActiveBoost.ContainsKey(player))
                ActiveBoost.Add(player, new BoostPlayer { });
        }

        public Dictionary<ulong, bool> CheckKey = new Dictionary<ulong, bool>();
        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || input == null) return;
            if (input.previous.buttons != 0 && input.IsDown(BUTTON.FIRE_PRIMARY) && player.GetActiveItem() != null && player.GetActiveItem().info.shortname == "smallwaterbottle")
            {
                CheckKey[player.userID] = true;
            }
        }

        void OnItemUse(Item item, int amountToUse)
        {
            if (item == null) return; 
            ItemContainer container = item.GetRootContainer();
            if (container == null) return;
            BasePlayer player = container.GetOwnerPlayer();
            if (player == null) return;
            if (player.GetActiveItem() == null) return;
            if (!ActiveBoost.ContainsKey(player)) return;

            if (player.GetActiveItem().info.shortname == "smallwaterbottle")
            {
                if (CheckKey[player.userID])
                {
                    for (int i = 0; i < config.settings.Count; i++)
                    {
                        if (player.GetActiveItem().skin == config.settings[i].skinid)
                        {
                            ActiveBoost[player].StartBoos(player, config.settings[i].StatickName);
                            player.GetActiveItem().Remove();
                            return;
                        }
                    }
                }
               
            }
        }

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null || info.Initiator is BaseNpc || info.Initiator is ScientistNPC || info.InitiatorPlayer == null || entity is BaseAnimalNPC) return;
            BasePlayer Initiator = info.InitiatorPlayer;
            BasePlayer Target = entity as BasePlayer;

            if (entity.GetComponent<BasePlayer>())
            {
                if (Initiator.userID == Target.userID) return;

                if (!ActiveBoost.ContainsKey(Initiator) || !ActiveBoost.ContainsKey(Target)) return;

                if (ActiveBoost[Initiator].DamageTimer != null)
                {
                    double Boost = 1 + config.settings[2].Boost / 100;
                    info.damageTypes.ScaleAll((float)Boost);
                }

                if (ActiveBoost[Target].ArmorTimer != null)
                {
                    double Armor = 1 - config.settings[1].Boost / 100;
                    info.damageTypes.ScaleAll((float)Armor);
                }
            }        
        }

        object OnRunPlayerMetabolism(PlayerMetabolism metabolism)
        {
            BasePlayer player = metabolism.GetComponent<BasePlayer>();
            if (player != null)
            {
                if (ActiveBoost[player].HealTimer != null)
                {
                   player.health += config.settings[0].Boost;
                }
            }
            return null;
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null || info.Initiator is BaseNpc || info.Initiator is ScientistNPC || info.InitiatorPlayer == null || entity is BaseAnimalNPC) return;
            BasePlayer player = entity.GetComponent<BasePlayer>();
            BasePlayer Target = info.InitiatorPlayer;
            if (entity.GetComponent<BasePlayer>())
            {
                if (!ActiveBoost.ContainsKey(player) || !ActiveBoost.ContainsKey(Target)) return;

                if (ActiveBoost[player].HealTimer != null)
                    ActiveBoost[player].HealTimer = null;
                if (ActiveBoost[player].ArmorTimer != null)
                    ActiveBoost[player].ArmorTimer = null;
                if (ActiveBoost[player].DamageTimer != null)
                    ActiveBoost[player].DamageTimer = null;
            }
        }

        private Item CreateItem(int index)
        {
            bool goodChance = UnityEngine.Random.Range(0, 100) >= (100 - config.settings[index].DropChance);
            if (goodChance)
            {
                return config.settings[index].CreateItem(config.settings[index].DropAmount);
            }
            return null;
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            try
            {
                if (entity.GetComponent<LootContainer>() == null) return;
                int random = UnityEngine.Random.Range(0, config.settings.Count);

                for (int x = 0; x < config.settings[random].crate.Count; x++)
                {
                    if (entity.GetComponent<LootContainer>().ShortPrefabName.ToLower() == config.settings[random].crate[x].ToLower())
                    {
                        var item = (Item)CreateItem(random);
                        item?.MoveToContainer(entity.GetComponent<LootContainer>().inventory);
                    }
                }
            }
            catch (NullReferenceException e)
            {

            }
        }
        #endregion
    }
}
