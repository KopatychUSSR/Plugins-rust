using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("AnimalLoot", "https://topplugin.ru/", "0.0.2")]
    [Description("Полная настройка выпадаемого лута с животных ")]
    class AnimalLoot : RustPlugin
    {
        #region Configuration

        private static Configuration config = new Configuration();
        private class Configuration
        {
            [JsonProperty("Настройка животных [животное] - {лут}")]
            public Dictionary<string,Dictionary<string , Amount>> AnimalSettings = new Dictionary<string, Dictionary<string, Amount>>();
            [JsonProperty("Включить(true) стандартный лут от животных(будет совмещен с вашим,из кфг) или отключить(false)")]
            public bool DefaultLoot;

            internal class Amount
            {
                [JsonProperty("Минимальное количество выпадения")]
                public int MinimumAmount;
                [JsonProperty("Максимальное количество выпадения")]
                public int MaximumAmount;
            }

            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                   DefaultLoot = true,
                   AnimalSettings = new Dictionary<string, Dictionary<string, Amount>>
                   {
                       ["assets/rust.ai/agents/horse/horse.corpse.prefab"] = new Dictionary<string, Amount>
                       {
                          ["wood"] = new Amount
                          {
                              MinimumAmount = 10,
                              MaximumAmount = 300
                          },
                          ["stones"] = new Amount
                           {
                               MinimumAmount = 4,
                               MaximumAmount = 340
                           },
                           ["diesel_barrel"] = new Amount
                           {
                               MinimumAmount = 1,
                               MaximumAmount = 30 
                           },
                       },
                       ["assets/rust.ai/agents/boar/boar.corpse.prefab"] = new Dictionary<string, Amount>
                       {
                           ["wood"] = new Amount
                           {
                               MinimumAmount = 100,
                               MaximumAmount = 400
                           },
                           ["stones"] = new Amount
                           {
                               MinimumAmount = 100,
                               MaximumAmount = 500
                           },
                           ["diesel_barrel"] = new Amount
                           {
                               MinimumAmount = 100,
                               MaximumAmount = 20
                           },
                       },
                       ["assets/rust.ai/agents/chicken/chicken.corpse.prefab"] = new Dictionary<string, Amount>
                       {
                           ["wood"] = new Amount
                           {
                               MinimumAmount = 100,
                               MaximumAmount = 400
                           },
                           ["stones"] = new Amount
                           {
                               MinimumAmount = 100,
                               MaximumAmount = 500
                           },
                           ["diesel_barrel"] = new Amount
                           {
                               MinimumAmount = 100,
                               MaximumAmount = 20
                           },
                       },
                       ["assets/rust.ai/agents/stag/stag.corpse.prefab"] = new Dictionary<string, Amount>
                       {
                           ["wood"] = new Amount
                           {
                               MinimumAmount = 100,
                               MaximumAmount = 400
                           },
                           ["stones"] = new Amount
                           {
                               MinimumAmount = 100,
                               MaximumAmount = 500
                           },
                           ["diesel_barrel"] = new Amount
                           {
                               MinimumAmount = 100,
                               MaximumAmount = 20
                           },
                       },
                       ["assets/rust.ai/agents/wolf/wolf.corpse.prefab"] = new Dictionary<string, Amount>
                       {
                           ["wood"] = new Amount
                           {
                               MinimumAmount = 100,
                               MaximumAmount = 400
                           },
                           ["stones"] = new Amount
                           {
                               MinimumAmount = 100,
                               MaximumAmount = 500
                           },
                           ["diesel_barrel"] = new Amount
                           {
                               MinimumAmount = 100,
                               MaximumAmount = 20
                           },
                       },
                       ["assets/rust.ai/agents/bear/bear.corpse.prefab"] = new Dictionary<string, Amount>
                       {
                           ["wood"] = new Amount
                           {
                               MinimumAmount = 100,
                               MaximumAmount = 400
                           },
                           ["stones"] = new Amount
                           {
                               MinimumAmount = 100,
                               MaximumAmount = 500
                           },
                           ["diesel_barrel"] = new Amount
                           {
                               MinimumAmount = 100,
                               MaximumAmount = 20
                           },
                       },
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
                PrintWarning("Ошибка #{261}" + $"чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Hooks
        object OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (dispenser.gatherType != (ResourceDispenser.GatherType)2 || dispenser.name == "assets/prefabs/player/player_corpse.prefab") return null;
            var player = (BasePlayer)entity;
            if (!config.DefaultLoot)
            {
                GiveItem(player, dispenser.name);
                return false;
            }
            GiveItem(player, dispenser.name);
            return null;

        }
        #endregion

        #region Metods

        public void GiveItem(BasePlayer player,string Animal)
        {
            var ConfigOut = config.AnimalSettings[Animal];
            var ConfigOutShortname = ConfigOut.ElementAt(UnityEngine.Random.Range(0, ConfigOut.Count)).Key;

            Item item = ItemManager.CreateByName(ConfigOutShortname, RandomAmount(Animal), 0);
            player.GiveItem(item); 
        }

        public int RandomAmount(string Animal)
        {
            var ConfigOut = config.AnimalSettings[Animal];
            var ConfigOutAmount = ConfigOut.ElementAt(UnityEngine.Random.Range(0, ConfigOut.Count)).Value;
            int Amount = UnityEngine.Random.Range(ConfigOutAmount.MinimumAmount, ConfigOutAmount.MaximumAmount);
            return Amount;
        }

        #endregion
    }
}
