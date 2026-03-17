using System;
using System.Collections.Generic;
using ox = Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using hu = Oxide.Game.Hurtworld;
using Newtonsoft.Json;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("XRate", "fermenspwnz", "1.1.0")]
    [Description("Отличный плагин для настройки рейтов добычи ресурсов на сервере.")]
    class XRate : HurtworldPlugin
    {
        #region Config
        private PluginConfig config;
        protected override void LoadDefaultConfig()
        {
            config = PluginConfig.DefaultConfig();
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        private class PluginConfig
        {
            [JsonProperty("Пермишн | x-добычи")]
            public Dictionary<string, float> Perm;
            [JsonProperty("Ночь-сообщение")]
            public string MessageNight;
            [JsonProperty("Длина ночи (в минутах)")]
            public float NightLength;
            [JsonProperty("Длина дня (в минутах)")]
            public float DayLength;
            [JsonProperty("День-сообщение")]
            public string MessageDay;
            [JsonProperty("Дневной рейт добычи")]
            public float DayRate;
            [JsonProperty("Ночной рейт добычи")]
            public float NightRate;
            [JsonProperty("[Магические предметы] ID уникальных предметов")]
            public List<int> FireItems;
            [JsonProperty("[Id Предмета] Топор")]
            public int axe = 428;
            [JsonProperty("[Id Предмета] Кирка")]
            public int pickaxe = 234;

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    DayRate = 5f,
                    NightRate = 10f,
                    DayLength = 15f,
                    NightLength = 5f,
                    MessageNight = "<color=#FF6347>[NightRate]</color> Рейты добычи ресурсов изменены на <color=#88e892>x{rate}</color>",
                    MessageDay = "<color=#FF6347>[DayRate]</color> Рейты добычи ресурсов изменены на <color=#88e892>x{rate}</color>",
                    FireItems = new List<int>(),
                    Perm = new Dictionary<string, float>()
                    {
                        { "2x" , 2f },
                        { "3x" , 3f },
                        { "5x" , 5f },
                        { "10x" , 10f },

                    }
                };
            }
        }
        #endregion

        #region Head
        static Dictionary<int, int> Smelted = new Dictionary<int, int>()
        {
            { 169, 149 }, // Iron
            { 206, 370 }, // ultranium
            { 257, 418 }, // Mondinium
            { 312, 204 }, // titra
            { 245, 278 } // wood

        };
        internal static readonly ChatManagerServer ChatManager = ChatManagerServer.Instance;
        public void Message(PlayerSession session, string message)
        {
            ConsoleManager.SendLog($"[Chat] {message}");
            var text = (IChatMessage)new ServerChatMessage(message);
            BitStreamPooled frameLease = Singleton<HNetworkManager>.Instance.BitStreamPool.GetFrameLease();
            text.Serialize(frameLease.Stream);
            ChatManager.RPCS("ReceiveChatMessage", session.Player, true, frameLease);
        }
        public void Broadcast(string message)
        {
            ConsoleManager.SendLog($"[Chat] {message}");
            var text = (IChatMessage)new ServerChatMessage(message);
            BitStreamPooled frameLease = Singleton<HNetworkManager>.Instance.BitStreamPool.GetFrameLease();
            text.Serialize(frameLease.Stream);
            ChatManager.RPCS("ReceiveChatMessage", uLink.RPCMode.Others, true, frameLease, new uLink.NetworkPlayer?());
        }

        private int Rate = 1;
        private float GatherRate = 1f;
        #endregion

        #region OxideHooks
        [HookMethod("Init")]
        private void Init()
        {
            LoadConfig();
            ox.Interface.Oxide.GetLibrary<hu.Libraries.Command>(null).AddChatCommand("pickaxe", this, "createfirepickaxe");
            ox.Interface.Oxide.GetLibrary<hu.Libraries.Command>(null).AddChatCommand("axe", this, "createfireaxe");
            ox.Interface.Oxide.GetLibrary<hu.Libraries.Command>(null).AddChatCommand("rate", this, "cmdRate"); 
            foreach (var cmd in config.Perm)
            {
                permission.RegisterPermission($"xrate.{cmd.Key}", this);
            }
            GatherRate = (float)config.DayRate;
            new PluginTimers(this).Once(180f, () => {
                TimeManager.Instance.DayLength = config.DayLength * 60f;
                TimeManager.Instance.NightLength = config.NightLength * 60f;
            });
            new PluginTimers(this).Repeat(5f, 0, () => {
                if (TimeManager.Instance != null)
                {
                    if ((bool)!TimeManager.Instance?.GetIsDay() && Rate == 1)
                    {
                        Rate = 0;
                        GatherRate = config.NightRate;
                        Broadcast(config.MessageNight.Replace("{rate}", GatherRate.ToString()));
                        Debug.Log(config.MessageNight.Replace("{rate}", GatherRate.ToString()));
                    }
                    else if ((bool)TimeManager.Instance?.GetIsDay() && Rate == 0)
                    {
                        Rate = 1;
                        GatherRate = config.DayRate;
                        Broadcast(config.MessageDay.Replace("{rate}", GatherRate.ToString()));
                        Debug.Log(config.MessageDay.Replace("{rate}", GatherRate.ToString()));
                    }
                }
            });
        }

        private void OnEntityDeath(AnimalStatManager stats, EntityEffectSourceData source)
        {
            PlayerSession session = GameManager.Instance.GetSession(source.EntitySource.HNetworkView().owner);
            float rate = permissionRate(session.SteamId.ToString());
            List<ItemObject> list = LootCalculator.Instance.RollConfig(stats.LootConfig, stats.gameObject, false);

            for (int index = 0; index < list.Count; ++index)
            {
                ItemObject itemObject = Singleton<GlobalItemManager>.Instance.CreateItem(list[index].Generator, Convert.ToInt32(Math.Floor(list[index].StackSize * ((resourceRate(list[index].Generator.name) * rate * 1f) - 1f))));
                new PluginTimers(this).Once(0.00001f, () =>
                {
                    if (itemObject != null) Singleton<GlobalItemManager>.Instance.GiveItem(session.Player, itemObject);
                });
            }
        }


        private void OnDispenserGather(GameObject resourceNode, HurtMonoBehavior player, List<ItemObject> items)
        {
            if (resourceNode == null || player == null || items == null) return;
            PlayerSession session = GameManager.Instance.GetSession(resourceNode.HNetworkView().owner);
            float rate = permissionRate(session.SteamId.ToString());
            NetworkEntityComponentBase netEntity = session.WorldPlayerEntity.GetComponent<NetworkEntityComponentBase>();
            EquippedHandlerBase equippedHandler = netEntity.GetComponent<EquippedHandlerBase>();
            ItemObject equippedItem = equippedHandler.GetEquippedItem();
            for (int index = 0; index < items.Count; ++index)
            {
                if (Smelted.ContainsKey(items[index].Generator.GeneratorId) && config.FireItems.Contains(equippedItem.ItemId))
                {
                    var itemmanager = Singleton<GlobalItemManager>.Instance;
                    Dictionary<int, ItemGeneratorAsset> itemGenerators = Singleton<GlobalItemManager>.Instance.ItemGenerators;
                    ItemGeneratorAsset generator = itemGenerators[Smelted[items[index].Generator.GeneratorId]];
                    ItemObject item = itemmanager.CreateItem(generator);
                    item.StackSize = Convert.ToInt32(Math.Floor(items[index].StackSize * (resourceRate(items[index].Generator.name) * rate * 1f)));
                    items.Remove(items[index]);
                    items.Add(item);
                }
                 else if(!Smelted.ContainsValue(items[index].Generator.GeneratorId)) items[index].StackSize = Convert.ToInt32(Math.Floor(items[index].StackSize * (resourceRate(items[index].Generator.name) * rate * 1f)));
            }
        }

        private void OnCollectiblePickup(LootOnPickup node, WorldItemInteractServer player, List<ItemObject> items)
        {
            if (node == null || player == null || items == null) return;
            PlayerSession session = GameManager.Instance.GetSession(player.HNetworkView().owner);
            float rate = permissionRate(session.SteamId.ToString());
            for (int index = 0; index < items.Count; ++index)
            {
                items[index].StackSize = Convert.ToInt32(Math.Floor(items[index].StackSize * (resourceRate(items[index].Generator.name) * rate * 1f)));
            }
        }
        #endregion

        #region Hooks
        private PlayerSession getSession(string identifier)
        {
            var sessions = GameManager.Instance.GetSessions();
            PlayerSession session = null;
            foreach (var i in sessions)
            {
                if (i.Value.Identity.Name.ToLower().Contains(identifier.ToLower()) || identifier.Equals(i.Value.SteamId.ToString()))
                {
                    session = i.Value;
                    break;
                }
            }

            return session;
        }

        string GetNameOfObject(GameObject obj)
        {
            var ManagerInstance = GameManager.Instance;
            return ManagerInstance.GetDescriptionKey(obj);
        }

        private void cmdRate(PlayerSession session, string command, string[] args)
        {
            Message(session, $"<color=#FF6347>>>>Бонус к добычи: {Convert.ToString(permissionRate(session.SteamId.ToString()) * 100)}%<<<</color>");
        }
        private void Hcreatepickaxe(PlayerSession session)
        {
            var itemmanager = Singleton<GlobalItemManager>.Instance;
            Dictionary<int, ItemGeneratorAsset> itemGenerators = Singleton<GlobalItemManager>.Instance.ItemGenerators;
            ItemGeneratorAsset generator = itemGenerators[config.pickaxe];
            ItemGeneratorAsset paintgenerator = itemGenerators[119];
            if (generator != null && paintgenerator != null)
            {
                ItemObject item = itemmanager.CreateItem(generator);
                ItemObject paint = itemmanager.CreateItem(paintgenerator);
                itemmanager.GiveItem(session.Player, paint);
                itemmanager.GiveItem(session.Player, item);
                itemmanager.ColorItemClient(item, Color.red, Color.yellow, Color.red, paint);
                config.FireItems.Add(item.ItemId);
                SaveConfig();
            }
        }

        private void Hcreateaxe(PlayerSession session)
        {
            var itemmanager = Singleton<GlobalItemManager>.Instance;
            Dictionary<int, ItemGeneratorAsset> itemGenerators = Singleton<GlobalItemManager>.Instance.ItemGenerators;
            ItemGeneratorAsset generator = itemGenerators[config.axe];
            ItemGeneratorAsset paintgenerator = itemGenerators[119];
            if (generator != null && paintgenerator != null)
            {
                ItemObject item = itemmanager.CreateItem(generator);
                ItemObject paint = itemmanager.CreateItem(paintgenerator);
                itemmanager.GiveItem(session.Player, paint);
                itemmanager.GiveItem(session.Player, item);
                itemmanager.ColorItemClient(item, Color.red, Color.red, Color.yellow, paint);
                config.FireItems.Add(item.ItemId);
                SaveConfig();
            }
        }

        private void createfirepickaxe(PlayerSession session, string command)
        {
            if (!session.IsAdmin) return;
            Hcreatepickaxe(session);
        }

        private void createfireaxe(PlayerSession session, string command)
        {
            if (!session.IsAdmin) return;
            Hcreateaxe(session);
        }

        [ConsoleCommand("create.axe")]
        private void cmdaxecreater(string commandString, string[] args)
        {
            if (args.Length == 1)
            {
                var sessions = GameManager.Instance.GetSessions().Values.ToList();
                foreach (var player in sessions)
                {
                    if (player.OwnerSteamId == args[0])
                    {
                        Hcreateaxe(player);
                        Debug.Log(player.Identity.Name + " получил огненный топор.");
                    }
                }
            }
        }

        [ConsoleCommand("create.pickaxe")]
        private void cmdpickaxecreater(string commandString, string[] args)
        {
            if (args.Length == 1)
            {
                var sessions = GameManager.Instance.GetSessions().Values.ToList();
                foreach (var player in sessions)
                {
                    if (player.OwnerSteamId == args[0])
                    {
                        Hcreateaxe(player);
                        Debug.Log(player.Identity.Name + " получил огненную кирку.");
                    }
                }
            }
        }

        float resourceRate(string resource)
        {
            return GatherRate;
        }

        float permissionRate(string steamid)
        {
            float rate = 1f;
            foreach (var cmd in config.Perm)
            {
                if ((bool)permission.UserHasPermission(steamid, "xrate." + cmd.Key) && Convert.ToInt32(cmd.Value) > rate) rate = Convert.ToInt32(cmd.Value);
            }
            return rate;
        }
        #endregion

    }
}