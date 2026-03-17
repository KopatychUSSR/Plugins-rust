using System.Linq;
using UnityEngine;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using System.Collections.Generic;
using ConVar;

namespace Oxide.Plugins
{
    [Info("Golden Fish", "https://topplugin.ru/resources/goldenfish.98/", "1.4.0")]
    [Description("добавляет на сервер золотую рыбу,которая при потрашении выдает один случайный предмет из конфига!")]
    public class GoldenFish : RustPlugin
    {
        [PluginReference] Plugin IQChat;

        #region Configuration
        private static Configuration config = new Configuration();
        private class Configuration
        {
            [JsonProperty("Отображаемое имя")]
            public string DisplayName;
            [JsonProperty("Шанс выпадения")]
            public int DropChance;
            [JsonProperty("Лут за потрошения")]
            public List<ItemGiveInfo> GiveItems = new List<ItemGiveInfo>();

            internal class ItemGiveInfo
            {
                [JsonProperty("Шортнейм предмета")] public string shortname;
                [JsonProperty("СкинИД предмета")] public ulong skinID;
                [JsonProperty("Минимальное количество предмета")] public int Minamount;
                [JsonProperty("Максимальное количество предмета")] public int Maxamount;
            }

            public Item CreateItem(int amount)
            {
                Item item = ItemManager.CreateByPartialName("fish.troutsmall", amount);
                item.name = DisplayName;
                item.skin = 1686591036;
                return item;
            }
            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {

                    DisplayName = "Золотая рыбка",
                    DropChance = 30,
                    GiveItems = new List<ItemGiveInfo>
                    {
                         new ItemGiveInfo
                         {
                             shortname = "stones",
                             skinID = 0U,
                             Minamount = 100,
                             Maxamount = 2000
                         },
                         new ItemGiveInfo
                         {
                             shortname = "metal.refined",
                             skinID = 0U,
                             Minamount = 50,
                             Maxamount = 100
                         },
                         new ItemGiveInfo
                         {
                             shortname = "rifle.m39",
                             skinID = 0U,
                             Minamount = 1,
                             Maxamount = 1
                         },
                         new ItemGiveInfo
                         {
                             shortname = "explosive.satchel",
                             skinID = 0U,
                             Minamount = 1,
                             Maxamount = 3
                         },
                         new ItemGiveInfo
                         {
                             shortname = "surveycharge",
                             skinID = 0U,
                             Minamount = 1,
                             Maxamount = 5
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
                if (config == null)
                    LoadDefaultConfig();
            }
            catch
            {
                PrintWarning("Ошибка #132" + $"чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }
            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region MainClass
        public class GoldenFishTraps : BaseEntity
        {
            private WildlifeTrap trap;

            public void Awake()
            {
                trap = GetComponent<WildlifeTrap>();
                InvokeRepeating(TrapFunc, 70, 150);
            }

            public void TrapFunc()
            {
                int baitCalories = this.GetBaitCalories();
                if (baitCalories <= 0)
                {
                    return;
                }
                global::TrappableWildlife randomWildlife = trap.GetRandomWildlife();
                if (baitCalories >= randomWildlife.caloriesForInterest && Random.Range(0f, 1f) <= randomWildlife.successRate)
                {
                    UseBaitCalories(randomWildlife.caloriesForInterest);
                    if (Random.Range(0f, 1f) <= trap.trapSuccessRate)
                    {
                        if (random.Next(0, 100) <= config.DropChance)
                        {
                            Item goldenFish = ItemManager.CreateByName("fish.troutsmall", 1, 1686591036);
                            goldenFish.name = config.DisplayName;

                            goldenFish.MoveToContainer(trap.inventory);
                            trap.Hurt(trap.StartMaxHealth() * 0.1f, Rust.DamageType.Decay, null, false);
                        }
                        else if(Random.Range(0f, 1f) <= 0.5f)
                            this.TrapWildlife(randomWildlife);
                    }
                }
            }
            public void TrapWildlife(global::TrappableWildlife trapped)
            {
                global::Item item = global::ItemManager.Create(trapped.inventoryObject, Random.Range(trapped.minToCatch, trapped.maxToCatch + 1), 0UL);
                if (!item.MoveToContainer(trap.inventory, -1, true))
                {
                    item.Remove(0f);
                }
                else
                {
                    base.SetFlag(global::BaseEntity.Flags.Reserved1, true, false, true);
                }
                trap.Hurt(trap.StartMaxHealth() * 0.05f, Rust.DamageType.Decay, null, false);
            }
            public void UseBaitCalories(int numToUse)
            {
                foreach (global::Item item in trap.inventory.itemList)
                {
                    int itemCalories = GetItemCalories(item);
                    if (itemCalories > 0)
                    {
                        numToUse -= itemCalories;
                        item.UseItem(1);
                        if (numToUse <= 0)
                        {
                            break;
                        }
                    }
                }
            }
            public int GetItemCalories(global::Item item)
            {
                global::ItemModConsumable component = item.info.GetComponent<global::ItemModConsumable>();
                if (component == null)
                {
                    return 0;
                }
                foreach (global::ItemModConsumable.ConsumableEffect consumableEffect in component.effects)
                {
                    if (consumableEffect.type == global::MetabolismAttribute.Type.Calories && consumableEffect.amount > 0f)
                    {
                        return Mathf.CeilToInt(consumableEffect.amount);
                    }
                }
                return 0;
            }
            public int GetBaitCalories()
            {
                int num = 0;
                foreach (global::Item item in trap.inventory.itemList)
                {
                    global::ItemModConsumable component = item.info.GetComponent<global::ItemModConsumable>();
                    if (!(component == null) && !trap.ignoreBait.Contains(item.info))
                    {
                        foreach (global::ItemModConsumable.ConsumableEffect consumableEffect in component.effects)
                        {
                            if (consumableEffect.type == global::MetabolismAttribute.Type.Calories && consumableEffect.amount > 0f)
                            {
                                num += Mathf.CeilToInt(consumableEffect.amount * (float)item.amount);
                            }
                        }
                    }
                }
                return num;
            }
            public void PlayerStoppedLooting(BasePlayer player)
            {
                trap.CancelInvoke(trap.TrapThink);
            }
        }

        #endregion

        #region Command
        [ChatCommand("gf.give")]
        private void CmdChatDebugGoldfSpawn(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
                return;
            var item = config.CreateItem(5);
            item.MoveToContainer(player.inventory.containerMain);
        }

        [ConsoleCommand("GoldenFish")]
        void FishCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = BasePlayer.Find(arg.Args[0]);
            if (player == null || !player.IsConnected)
            {
                Puts("Игрок не найден");
                return;
            }
            int count = int.Parse(arg.Args[1]);
            GiveFish(player, count > 0 ? count : 1);
            SendChat(player, $"Вы успешно получили {config.DisplayName}");
            Puts($"Игроку выдана {config.DisplayName}");
            //Puts($"Игроку выдана {config.DisplayName.skykey}");
        }
        #endregion

        #region Hooks
        private void Unload() => DestroyAll<GoldenFishTraps>();
        void OnServerInitialized()
        {
            foreach (var check in UnityEngine.Object.FindObjectsOfType<WildlifeTrap>())
                check.gameObject.AddComponent<GoldenFishTraps>();
        }

        void OnEntityBuilt(Planner plan, GameObject obj)
        {
            var entity = obj.GetComponent<BaseEntity>();
            if (entity is WildlifeTrap)
            {
                var trap = entity as WildlifeTrap;
                trap.gameObject.AddComponent<GoldenFishTraps>();
                trap.CancelInvoke(trap.TrapThink);
            }
        }

        object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (action == "Gut" && item.skin == 1686591036)
            {
                var cfgItem = config.GiveItems;
                int randomItem = random.Next(cfgItem.Count);
                Item itemS = ItemManager.CreateByName(cfgItem[randomItem].shortname, random.Next(cfgItem[randomItem].Minamount, cfgItem[randomItem].Maxamount), cfgItem[randomItem].skinID);

                if (!player.inventory.GiveItem(itemS))
                    item.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity);

                ItemRemovalThink(item, player, 1);
                return false;
            }
            return null;
        }
        #endregion

        #region Help
        private static void ItemRemovalThink(Item item, BasePlayer player, int itemsToTake)
        {
            if (item.amount == itemsToTake)
            {
                item.RemoveFromContainer();
                item.Remove();
            }
            else
            {
                item.amount = item.amount - itemsToTake;
                player.inventory.SendSnapshot();
            }
        }
        private static System.Random random = new System.Random();
        private void GiveFish(BasePlayer player, int count = 1)
        {
            Item item = config.CreateItem(count);
            player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
        }
        private void DestroyAll<T>()
        {
            UnityEngine.Object.FindObjectsOfType(typeof(T))
                .ToList()
                .ForEach(UnityEngine.Object.Destroy);
        }
        public void SendChat(BasePlayer player, string Message, Chat.ChatChannel channel = Chat.ChatChannel.Global)
        {
            if (IQChat)
                IQChat?.Call("API_ALERT_PLAYER", player, Message, "");
            else
                player.SendConsoleCommand("chat.add", channel, 0, Message);
        }
        #endregion
    }
}
