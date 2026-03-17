using ConVar;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("UraniumTools", "https://topplugin.ru/", "0.0.5")]
    [Description("Урановые инструменты,добывают сразу готовые ресурсы с возможностью увеличения выпадения,нанося урон радиацией")]
    class UraniumTools : RustPlugin
    {
        #region Reference
        [PluginReference] Plugin IQChat;
        #endregion

        #region Configuration

        private static Configuration config = new Configuration();
        private class Configuration
        {
            [JsonProperty("Настройка урановых инструментов")]
            public List<Tools> UraniumTools = new List<Tools>();
            [JsonProperty("Включить единоразовую починку инструментов")]
            public bool RepairUse;
            [JsonProperty("Префикс в чате для сообщения(IQChat)")]
            public string PrefixChat;
            internal class Tools
            {
                [JsonProperty("Shortname инструмента")]
                public string Shortname;
                [JsonProperty("Название инструмента")]
                public string Name;
                [JsonProperty("SkinID инструмента")]
                public ulong SkinID;
                [JsonProperty("Мутация (При добыче будет перерабатывать ресурс) [true/false]")]
                public bool MutationUse;
                [JsonProperty("Радиация (При ударе инструментом будет добавлять радиацию игроку) [true/false]")]
                public bool RadiationUse;
                [JsonProperty("Умножение ресурсов (При ударе инструментом будет увеличивать добычу в Х раз) [true/false]")]
                public bool RateGatherUse;
                [JsonProperty("Радиация за удар инструментом")]
                public float Radiation;
                [JsonProperty("Увеличивать добычу в Х раз за удар")]
                public float RateGather;
            }

            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    RepairUse = true,
                    PrefixChat = "<color=#0161FFF><b>[Урановые Инструменты]</b></color>",
                    UraniumTools = new List<Tools>
                    {
                        new Tools
                        {
                            Shortname = "pickaxe",
                            Name = "Урановая кирка",
                            SkinID = 859006499,
                            MutationUse = true,
                            RadiationUse = true,
                            RateGatherUse = true,
                            Radiation = 3,
                            RateGather = 2
                        },
                        new Tools
                        {
                            Shortname = "hatchet",
                            Name = "Урановый топор",
                            SkinID = 860588662,
                            MutationUse = false,
                            RadiationUse = true,
                            RateGatherUse = true,
                            Radiation = 3,
                            RateGather = 5
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
                PrintWarning("Ошибка" + $"чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Data
        [JsonProperty("Дата с инструментами игроков")] public List<uint> ItemListBlocked = new List<uint>();
        void ReadData()
        {
            ItemListBlocked = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<List<uint>>("UraniumTools/ItemListBlocked");
        }
        void WriteData() => timer.Every(60f, () =>
        {
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("UraniumTools/ItemListBlocked", ItemListBlocked);
        });
        #endregion

        #region Hooks
        void OnServerInitialized()
        {
            MutationRegistered();
            ReadData();
            WriteData();
        }

        void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            Item weapon = entity.ToPlayer()?.GetActiveItem();
            if (weapon == null) return;
            UseTools(item, entity.ToPlayer(), weapon.info.shortname, weapon.skin);
        }
        void OnDispenserBonus(ResourceDispenser disp, BasePlayer player, Item item)
        {
            Item weapon = player?.GetActiveItem();
            if (weapon == null) return;
            UseTools(item, player, weapon.info.shortname, weapon.skin);
        }
        object OnItemRepair(BasePlayer player, Item item)
        {
            if (!config.RepairUse) return null;
            if (ItemListBlocked.Contains(item.uid))
            {
                SendChat("Данный предмет не подлежит починке!", player);
                return false;
            }

            for (int i = 0; i < config.UraniumTools.Count; i++)
            {
                var Tools = config.UraniumTools[i];
                if (Tools.SkinID == item.skin)
                    if (Tools.Shortname == item.info.shortname)
                        ItemListBlocked.Add(item.uid);
            }
            return null;
        }

        #endregion

        #region Commands
        [ConsoleCommand("ut_give")]
        void UraniumToolGive(ConsoleSystem.Arg args)
        {
            if (!args.IsAdmin) return;
            ulong SteamID = ulong.Parse(args.Args[0]);
            string Shortname = args.Args[1];
            BasePlayer player = BasePlayer.FindByID(SteamID);
            CreateItem(player, Shortname);
        }
        #endregion

        #region Metods

        #region Mutations
        private Dictionary<ItemDefinition, ItemDefinition> Transmutations;
        public List<string> MutationItemList = new List<string>
        {
            "chicken.raw",
            "humanmeat.raw",
            "bearmeat",
            "deermeat.raw",
            "meat.boar",
            "wolfmeat.raw",
            "hq.metal.ore",
            "metal.ore",
            "sulfur.ore"
        };
        void MutationRegistered()
        {
            Transmutations = ItemManager.GetItemDefinitions().Where(p => MutationItemList.Contains(p.shortname)).ToDictionary(p => p, p => p.GetComponent<ItemModCookable>()?.becomeOnCooked);
            ItemDefinition wood = ItemManager.FindItemDefinition(-151838493);
            ItemDefinition charcoal = ItemManager.FindItemDefinition(-1938052175);
            Transmutations.Add(wood, charcoal);
        }
        #endregion

        void UseTools(Item item,BasePlayer player, string Shortname, ulong SkinID)
        {
            for (int i = 0; i < config.UraniumTools.Count; i++)
            {
                var UraniumTool = config.UraniumTools[i];
                if (UraniumTool.SkinID == SkinID)
                {
                    if (UraniumTool.MutationUse && Transmutations.ContainsKey(item.info))
                        item.info = Transmutations[item.info];
                    if (UraniumTool.RadiationUse)
                        player.metabolism.radiation_poison.value += UraniumTool.Radiation;
                    if (UraniumTool.RateGatherUse)
                        item.amount = (int)(item.amount * UraniumTool.RateGather * 1);
                }
            }
        }

        void CreateItem(BasePlayer player,string Shortname)
        {
            var UraniumTool = config.UraniumTools.FirstOrDefault(x => x.Shortname == Shortname);
            Item item = ItemManager.CreateByName(Shortname, 1, UraniumTool.SkinID);
            item.name = UraniumTool.Name;
            
            player.GiveItem(item);
        }

        public void SendChat(string Message, BasePlayer player, Chat.ChatChannel channel = Chat.ChatChannel.Global)
        {
            if (IQChat)
                IQChat?.Call("API_ALERT_PLAYER", player, Message, config.PrefixChat);
            else player.SendConsoleCommand("chat.add", channel, 0, Message);
        }

        #endregion
    }
}
