using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Autcion", "Anathar", "0.1.94")]
    [Description("Аукцион")]
    class Auction : RustPlugin
    {
        [PluginReference] Plugin ImageLibrary, Economics, ServerRewards;
        System.Random random = new System.Random();
        string matterial = "assets/content/ui/uibackgroundblur-ingamemenu.mat";
        #region Data
        private StoredData DataBase = new StoredData();
        private LocalizationStored LocData = new LocalizationStored();
        public class LocalizationStored
        {
            public Dictionary<string, string> TranslatedItems = new Dictionary<string, string>();
        }
        public class StoredData
        {
            public Dictionary<ulong, PlayerInfo> PlayerDatabase = new Dictionary<ulong, PlayerInfo>();
            public List<AuctionLot> Lots = new List<AuctionLot>();
        }
        public class PlayerInfo
        {
            public int Balance { get; set; }
            public List<ItemInv> Inventory { get; set; }

        }
        public class ItemInv
        {
            public int ItemId { get; set; }
            public string ShortName { get; set; }
            public int Ammount { get; set; }
            public ulong Skin { get; set; }
        }
        public class AuctionLot
        {
            public int LotId { get; set; }
            public ulong Owner { get; set; }
            public int Hours { get; set; }
            public ulong LastBitPlayer { get; set; }
            public string ShortName { get; set; }
            public DateTime Date { get; set; }
            public int Count { get; set; }
            public ulong skin { get; set; }
            public int StartBit { get; set; }
            public int LastBit { get; set; }
            public int Bid { get; set; }
            public int Buyout { get; set; }
        }
        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, DataBase);

        private void LoadData()
        {
            try
            {
                DataBase = Interface.GetMod().DataFileSystem.ReadObject<StoredData>(Name);
            }
            catch (Exception e)
            {
                DataBase = new StoredData();
            }
        }
        #endregion
        #region Config
        private static ConfigFile config;

        public class ConfigFile
        {
            [JsonProperty(PropertyName = "Использовать баланс GameStore?")]
            public bool UseGameStores { get; set; }
            [JsonProperty(PropertyName = "Использовать баланс Economics?")]
            public bool UseEconomics { get; set; }
            [JsonProperty(PropertyName = "Разрешить открытие только в Building Зоне ?")]
            public bool OnlyBuilding { get; set; }
            [JsonProperty(PropertyName = "Использовать баланс ServerRewards?")]
            public bool UseServerRwards { get; set; }
            [JsonProperty(PropertyName = "GameStore Id Магазина")]
            public string GSId { get; set; } = "";
            [JsonProperty(PropertyName = "GameStore Api Ключ")]
            public string GSApi { get; set; }
            [JsonProperty(PropertyName = "Сколько предметов может добавить игрок ?")]
            public int HowCanAddItems { get; set; }
            [JsonProperty(PropertyName = "Сколько предметов может выставить на аукцион игрок?")]
            public int HowCanAddLots { get; set; }
            [JsonProperty(PropertyName = "На сколько будет выставлен лот ? (В часах)")]
            public int HowHoursForLots { get; set; }
            [JsonProperty(PropertyName = "Минимальная сумма для выставления лота")]
            public int MinimumCostForAddLot { get; set; }
            [JsonProperty(PropertyName = "Включить взятие процента за сделку ?")]
            public bool TakePercent { get; set; }
            [JsonProperty(PropertyName = "Процент за сделку")]
            public int PercentForMoney { get; set; }
            [JsonProperty(PropertyName = "Название валюты")]
            public string TugrickName { get; set; }


        }

        protected override void SaveConfig() => Config.WriteObject(config);

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<ConfigFile>();
                if (config == null)
                    Regenerate();
            }
            catch
            {
                Regenerate();
            }
        }

        private void Regenerate()
        {
            LoadDefaultConfig();
        }

        private ConfigFile GetDefaultSettings()
        {
            return new ConfigFile
            {
                UseGameStores = false,
                UseEconomics = false,
                UseServerRwards = false,
                OnlyBuilding = true,
                GSApi = "",
                GSId = "",
                HowCanAddItems = 4,
                HowCanAddLots = 4,
                HowHoursForLots = 12,
                MinimumCostForAddLot = 1,
                TakePercent = false,
                PercentForMoney = 30,
                TugrickName = "р"

            };
        }
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Attempting to create default config...");
            Config.Clear();
            Config.WriteObject(GetDefaultSettings(), true);
            Config.Save();
        }
        #endregion
        #region Hooks
        private void OnServerInitialized()
        {
            GetLocData();
            LoadData();
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                CheckDb(player);

        }
        void Unload()
        {
            SaveData();
        }
        void OnServerSave()
        {
            List<AuctionLot> RemoveLots = new List<AuctionLot>();
            foreach (AuctionLot GetLot in DataBase.Lots)
            {
                if (DateTime.Now.Subtract(GetLot.Date).TotalHours >= GetLot.Hours)
                {
                    if (GetLot.LastBitPlayer == 0)
                    {
                        ItemInv data = new ItemInv()
                        {
                            ItemId = random.Next(1, 100000),
                            Ammount = GetLot.Count,
                            ShortName = GetLot.ShortName,
                            Skin = GetLot.skin
                        };
                        DataBase.PlayerDatabase[GetLot.Owner].Inventory.Add(data);
                        RemoveLots.Add(GetLot);
                    }
                    else
                    {
                        if (config.UseGameStores)
                        {
                            DataBase.PlayerDatabase[GetLot.Owner].Balance += GetLot.Bid;
                            float GetBid = GetLot.Buyout;
                            if (config.TakePercent) GetBid -= GetBid / 100 * config.PercentForMoney;
                            GiveGsMoney(GetLot.Owner, GetBid, (code, status) =>
                            {
                                if (code != 200 || status == "fail")
                                {
                                    PrintError($"Ошибка api GameStores, причина:{status}");
                                    return;
                                }
                            });
                            ItemInv data = new ItemInv()
                            {
                                ItemId = random.Next(1, 100000),
                                Ammount = GetLot.Count,
                                ShortName = GetLot.ShortName,
                                Skin = GetLot.skin
                            };
                            DataBase.PlayerDatabase[GetLot.LastBitPlayer].Inventory.Add(data);
                            RemoveLots.Add(GetLot);
                        }
                        if (config.UseEconomics)
                        {
                            DataBase.PlayerDatabase[GetLot.Owner].Balance += GetLot.Bid;
                            float GetBid = GetLot.Buyout;
                            if (config.TakePercent) GetBid -= GetBid / 100 * config.PercentForMoney;
                            GiveEconomycsMoney(GetLot.Owner, GetBid);
                            ItemInv data = new ItemInv()
                            {
                                ItemId = random.Next(1, 100000),
                                Ammount = GetLot.Count,
                                ShortName = GetLot.ShortName,
                                Skin = GetLot.skin
                            };
                            DataBase.PlayerDatabase[GetLot.LastBitPlayer].Inventory.Add(data);
                            RemoveLots.Add(GetLot);
                        }

                        if (config.UseServerRwards)
                        {
                            DataBase.PlayerDatabase[GetLot.Owner].Balance += GetLot.Bid;
                            float GetBid = GetLot.Buyout;
                            if (config.TakePercent) GetBid -= GetBid / 100 * config.PercentForMoney;
                            GiveServerRewardMoney(GetLot.Owner, GetBid);
                            ItemInv data = new ItemInv()
                            {
                                ItemId = random.Next(1, 100000),
                                Ammount = GetLot.Count,
                                ShortName = GetLot.ShortName,
                                Skin = GetLot.skin
                            };
                            DataBase.PlayerDatabase[GetLot.LastBitPlayer].Inventory.Add(data);
                            RemoveLots.Add(GetLot);
                        }
                    }
                }
            }

            if (RemoveLots.Count != 0)
            {
                foreach (AuctionLot GetRemoveLots in RemoveLots)
                {
                    DataBase.Lots.Remove(GetRemoveLots);
                }
            }
        }
        void GetLocData()
        {
            if (Interface.GetMod().DataFileSystem.ReadObject<LocalizationStored>("Auction/Loc").TranslatedItems.Count == 0)
            {
                PrintWarning("Загружена локализация");
                webrequest.Enqueue("https://darkplugins.ru/engine/engine.json",null, (i, s) => {
                    if (i == 200) WriteData(s);
                    else PrintError("Ошибка загрузки локализации,обратитесь к разработчику!");
                }, this, RequestMethod.GET);
            }
            else
            {
                LocData = Interface.GetMod().DataFileSystem.ReadObject<LocalizationStored>("Auction/Loc");
            }
        }
        void WriteData(string s)
        {
            LocData.TranslatedItems = JsonConvert.DeserializeObject<Dictionary<string, string>>(s);
            Interface.GetMod().DataFileSystem.WriteObject("Auction/Loc", LocData);
        }
        void OnPlayerInit(BasePlayer player) => CheckDb(player);
        #endregion
        #region Command
        void CheckDb(BasePlayer player)
        {
            if (!DataBase.PlayerDatabase.ContainsKey(player.userID))
                DataBase.PlayerDatabase.Add(player.userID, new PlayerInfo() { Inventory = new List<ItemInv>() });
        }

        [ChatCommand("ac")]
        void accmd(BasePlayer player, string command, string[] args)
        {
            if (config.OnlyBuilding == true)
            {
                BuildingPrivlidge Privlidge = player.GetBuildingPrivilege();
                if (Privlidge != null && Privlidge.authorizedPlayers.Any(x => x.userid == player.userID))
                    DrawAuction(player, 0);
                else
                    SendReply(player, "Для открытия аукциона вам необходимо быть в зоне вашего шкафа");
            }
            else
            {
                DrawAuction(player, 0);
            }
        }
        [ChatCommand("Auction")]
        void Auctioncmd(BasePlayer player, string command, string[] args)
        {
            if (config.OnlyBuilding == true)
            {
                BuildingPrivlidge Privlidge = player.GetBuildingPrivilege();
                if (Privlidge != null && Privlidge.authorizedPlayers.Any(x => x.userid == player.userID))
                    DrawAuction(player, 0);
                else
                    SendReply(player, "Для открытия аукциона вам необходимо быть в зоне вашего шкафа");
            }
            else
            {
                DrawAuction(player, 0);
            }
        }
        [ConsoleCommand("OpenAddInv")]
        void OpenAddInv(ConsoleSystem.Arg arg)
        {
            int Page = int.Parse(arg.Args[0]);
            DrawAddInInvetory(arg.Player(), Page);
        }
        [ConsoleCommand("OpenBit")]
        void OpenBit(ConsoleSystem.Arg arg)
        {
            if (config.UseGameStores)
            {
                GetBalanceGameStore(arg.Player(), (code, balance) =>
                {
                    int GetBalance = int.Parse(balance.Split('.')[0]);
                    DataBase.PlayerDatabase[arg.Player().userID].Balance = GetBalance;
                });
            }
            if (config.UseEconomics)
            {
                double GetBalance = GetBalanceEconomycs(arg.Player().userID);
                DataBase.PlayerDatabase[arg.Player().userID].Balance = Convert.ToInt32(GetBalance);

            }
            if (config.UseServerRwards)
            {
                int GetBalance = GetBalanceServerReward(arg.Player().userID);
                DataBase.PlayerDatabase[arg.Player().userID].Balance = GetBalance;
            }
            int LotId = int.Parse(arg.Args[0]);
            int Page = int.Parse(arg.Args[1]);
            DrawBit(arg.Player(), LotId, 0, 0, Page);
        }
        [ConsoleCommand("OpenConfirm")]
        void OpenConfirm(ConsoleSystem.Arg arg)
        {
            string FromGui = arg.Args[0];
            string Parent = arg.Args[1];
            int LotId = int.Parse(arg.Args[2]);
            DrawConfirm(arg.Player(), FromGui, Parent, LotId);
        }
        [ConsoleCommand("OpenYourLots")]
        void OpenYourLots(ConsoleSystem.Arg arg)
        {
            CuiHelper.DestroyUi(arg.Player(), "AuctionMainGui");
            DrawYourAuction(arg.Player());
        }
        [ConsoleCommand("OpenCountMenu")]
        void OpenCountMenu(ConsoleSystem.Arg arg)
        {
            DrawSelectCountItem(arg.Player(), arg.Args[0], int.Parse(arg.Args[1]), ulong.Parse(arg.Args[2]), int.Parse(arg.Args[3]));
        }
        [ConsoleCommand("OpenInventory")]
        void OpenInventory(ConsoleSystem.Arg arg)
        {
            CuiHelper.DestroyUi(arg.Player(), "AuctionMainGui");
            DrawInvenoty(arg.Player());
        }
        [ConsoleCommand("UpdateAddInv")]
        void UpdateAddInv(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            int Ammount = int.Parse(arg.Args[1]);
            string ShortName = arg.Args[2];
            ulong Skin = ulong.Parse(arg.Args[3]);
            int Page = int.Parse(arg.Args[4]);
            ItemDefinition GetItemID = ItemManager.FindItemDefinition(ShortName);
            int GetAmmount = player.inventory.GetAmount(GetItemID.itemid);
            switch (arg.Args[0])
            {
                case "plus":
                    {
                        if (Ammount < GetAmmount)
                            Ammount++;
                        DrawSelectCountItem(player, ShortName, Ammount, Skin, Page);
                        break;
                    }
                case "minus":
                    {
                        if (Ammount > 1)
                            Ammount--;
                        DrawSelectCountItem(player, ShortName, Ammount, Skin, Page);
                        break;
                    }
            }
        }
        [ConsoleCommand("UpdateBit")]
        void UpdateBit(ConsoleSystem.Arg arg)
        {
            int LotId = int.Parse(arg.Args[1]);
            int Bit = int.Parse(arg.Args[2]);
            int Buyout = int.Parse(arg.Args[3]);
            int Page = int.Parse(arg.Args[4]);
            int Balance = DataBase.PlayerDatabase[arg.Player().userID].Balance;
            switch (arg.Args[0])
            {
                case "plus":
                    {
                        if (Bit < Balance)
                        {
                            Bit++;
                            if (Buyout < Bit)
                            {
                                Buyout = Bit;
                                DrawBit(arg.Player(), LotId, Bit, Buyout, Page, true);
                            }
                            else
                                DrawBit(arg.Player(), LotId, Bit, Buyout, Page, true);
                        }
                        break;
                    }
            }
        }
        [ConsoleCommand("OpenPublish")]
        void OpenPublish(ConsoleSystem.Arg arg)
        {
            int ItemID = int.Parse(arg.Args[0]);
            ulong Skin = ulong.Parse(arg.Args[1]);
            int Ammount = int.Parse(arg.Args[2]);
            string ShortName = arg.Args[3];
            int page = int.Parse(arg.Args[4]);
            DrawPublicItem(arg.Player(), ItemID, Skin, Ammount, ShortName, page, config.MinimumCostForAddLot, config.MinimumCostForAddLot);
        }
        [ConsoleCommand("UpdatePublish")]
        void UpdatePublish(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            string Type = arg.Args[0];
            int ItemId = int.Parse(arg.Args[2]);
            ulong Skin = ulong.Parse(arg.Args[3]);
            int Ammount = int.Parse(arg.Args[4]);
            string ShortName = arg.Args[5];
            int page = int.Parse(arg.Args[6]);
            int Bit = int.Parse(arg.Args[7]);
            int Buyout = int.Parse(arg.Args[8]);
            ItemDefinition GetItemID = ItemManager.FindItemDefinition(ShortName);
            int GetAmmount = player.inventory.GetAmount(GetItemID.itemid);
            if (Type == "Ammount")
            {
                switch (arg.Args[1])
                {
                    case "plus":
                        {
                            if (Ammount < GetAmmount)
                                Ammount++;
                            DrawPublicItem(player, ItemId, Skin, Ammount, ShortName, page, Bit, Buyout);
                            break;
                        }
                    case "minus":
                        {
                            if (Ammount > 1)
                                Ammount--;
                            DrawPublicItem(player, ItemId, Skin, Ammount, ShortName, page, Bit, Buyout);
                            break;
                        }
                }
            }
            if (Type == "Bit")
            {
                switch (arg.Args[1])
                {
                    case "plus":
                        {
                            Bit++;
                            DrawPublicItem(player, ItemId, Skin, Ammount, ShortName, page, Bit, Buyout);
                            break;
                        }
                    case "minus":
                        {
                            if (Bit > config.MinimumCostForAddLot)
                                Bit--;
                            DrawPublicItem(player, ItemId, Skin, Ammount, ShortName, page, Bit, Buyout);
                            break;
                        }
                }
            }
            if (Type == "Buyout")
            {
                switch (arg.Args[1])
                {
                    case "plus":
                        {
                            Buyout++;
                            DrawPublicItem(player, ItemId, Skin, Ammount, ShortName, page, Bit, Buyout);
                            break;
                        }
                    case "minus":
                        {
                            if (Buyout > config.MinimumCostForAddLot)
                                Buyout--;
                            DrawPublicItem(player, ItemId, Skin, Ammount, ShortName, page, Bit, Buyout);
                            break;
                        }
                }
            }

        }
        [ConsoleCommand("AddInInventory")]
        void AddInInventory(ConsoleSystem.Arg arg)
        {
            string ShortName = arg.Args[0];
            int Ammount = int.Parse(arg.Args[1]);
            ulong Skin = ulong.Parse(arg.Args[2]);
            int Page = int.Parse(arg.Args[3]);
            BasePlayer player = arg.Player();
            if (DataBase.PlayerDatabase[player.userID].Inventory.Count >= config.HowCanAddItems)
            {
                CuiHelper.DestroyUi(player, "AuctionAddInvGui");
                DrawMessage(player, "AuctionInvGui" + "BackGround", "У вас максимум предметов");
                return;

            }
            ItemDefinition GetItemID = ItemManager.FindItemDefinition(ShortName);
            player.inventory.Take(null, GetItemID.itemid, Ammount);
            ItemInv Data = new ItemInv
            {
                ItemId = random.Next(1, 100000),
                Ammount = Ammount,
                Skin = Skin,
                ShortName = ShortName
            };
            DataBase.PlayerDatabase[player.userID].Inventory.Add(Data);
            SaveData();
            CuiHelper.DestroyUi(player, "AuctionAddInvGui");
            DrawInvenoty(player, Page);
        }
        [ConsoleCommand("InventoryPage")]
        void InventoryPage(ConsoleSystem.Arg arg)
        {
            int page = int.Parse(arg.Args[1]);
            switch (arg.Args[0])
            {
                case "Next":
                    {
                        page++;
                        DrawAuction(arg.Player(), page);
                        break;
                    }
                case "Back":
                    {
                        page--;
                        DrawAuction(arg.Player(), page);
                        break;
                    }
            }
        }
        [ConsoleCommand("AuctionPage")]
        void AuctionPage(ConsoleSystem.Arg arg)
        {
            int page = int.Parse(arg.Args[1]);
            switch (arg.Args[0])
            {
                case "Next":
                    {
                        page++;
                        DrawAuction(arg.Player(), page);
                        break;
                    }
                case "Back":
                    {
                        page--;
                        DrawAuction(arg.Player(), page);
                        break;
                    }
            }
        }
        [ConsoleCommand("TakeFromInv")]
        void TakeFromInv(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            int ItemId = int.Parse(arg.Args[0]);
            int Ammount = int.Parse(arg.Args[1]);
            ulong Skin = ulong.Parse(arg.Args[2]);
            int Page = int.Parse(arg.Args[3]);
            ItemInv GetItem = DataBase.PlayerDatabase[player.userID].Inventory.Find(d => d.ItemId == ItemId);
            DataBase.PlayerDatabase[player.userID].Inventory.Remove(GetItem);
            Item x = ItemManager.CreateByPartialName(GetItem.ShortName, GetItem.Ammount);
            if (Skin != 0) x.skin = Skin;
            player.GiveItem(x, BaseEntity.GiveItemReason.PickedUp);
            DrawInvenoty(player, Page);


        }
        [ConsoleCommand("SetBit")]
        void SetBit(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            int LotId = int.Parse(arg.Args[0]);
            int Bid = int.Parse(arg.Args[1]);
            int Buyout = int.Parse(arg.Args[2]);
            int Page = int.Parse(arg.Args[3]);
            AuctionLot GetLot = DataBase.Lots.Find(x => x.LotId == LotId);

            if (config.UseGameStores)
            {
                GetBalanceGameStore(player, (code, balance) =>
                {
                    AuctionLot GetLotGS = DataBase.Lots.Find(x => x.LotId == LotId);
                    int GetBalance = int.Parse(balance.Split('.')[0]);
                    if (GetBalance < GetLotGS.Buyout)
                    {
                        CuiHelper.DestroyUi(player, "AuctionConfirmGui");
                        DrawMessage(player, "AuctionMainGui" + "BackGround", "У вас не хватает средств");
                        return;
                    }
                    if (GetLot.LastBitPlayer == 0)
                    {
                        TakeGsMoney(player.userID, Bid, (code2, status) =>
                        {
                            if (code2 != 200 || status == "fail")
                            {
                                CuiHelper.DestroyUi(player, "AuctionConfirmGui");
                                DrawMessage(player, "AuctionMainGui" + "BackGround", "Ошибка");
                                return;
                            }
                        });
                        GetLot.LastBitPlayer = player.userID;
                        GetLot.Bid = Bid;
                        GetLot.Buyout = Buyout;
                        DrawAuction(player, Page);
                    }
                    else
                    {
                        GiveGsMoney(GetLotGS.LastBitPlayer, GetLotGS.Bid, (code2, status) =>
                        {
                            if (code2 != 200 || status == "fail")
                            {
                                CuiHelper.DestroyUi(player, "AuctionConfirmGui");
                                DrawMessage(player, "AuctionMainGui" + "BackGround", "Ошибка");
                                return;
                            }
                        });
                        TakeGsMoney(player.userID, Bid, (code2, status) =>
                        {
                            if (code2 != 200 || status == "fail")
                            {
                                CuiHelper.DestroyUi(player, "AuctionConfirmGui");
                                DrawMessage(player, "AuctionMainGui" + "BackGround", "Ошибка");
                                return;
                            }
                        });
                        DataBase.PlayerDatabase[GetLot.LastBitPlayer].Balance += GetLot.Bid;
                        GetLot.LastBit = GetLot.Bid;
                        GetLot.Bid = Bid;
                        GetLot.Buyout = Buyout;
                        DrawAuction(player, Page);

                    }

                });
            }
            if (config.UseEconomics)
            {
                double GetBalance = GetBalanceEconomycs(player.userID);
                if (Convert.ToInt32(GetBalance) < Bid)
                {
                    CuiHelper.DestroyUi(player, "AuctionMainGui" + "Count");
                    DrawMessage(player, "AuctionMainGui" + "BackGround", "У вас не хватает средств");
                    return;
                }
                if (GetLot.LastBitPlayer == 0)
                {
                    if (config.UseEconomics)
                        TakeEconomycsMoney(player.userID, Bid);
                    GetLot.LastBitPlayer = player.userID;
                    GetLot.Bid = Bid;
                    GetLot.Buyout = Buyout;
                    DrawAuction(player, Page);
                }
                else
                {

                    if (config.UseEconomics)
                    {
                        GiveEconomycsMoney(GetLot.LastBitPlayer, GetLot.Bid);
                        TakeEconomycsMoney(player.userID, Bid);
                    }

                    GetLot.LastBit = GetLot.Bid;
                    GetLot.Bid = Bid;
                    GetLot.Buyout = Buyout;
                    DrawAuction(player, Page);

                }

            }

            if (config.UseServerRwards)
            {
                int GetBalance = GetBalanceServerReward(player.userID);
                if (GetBalance < Bid)
                {
                    CuiHelper.DestroyUi(player, "AuctionMainGui" + "Count");
                    DrawMessage(player, "AuctionMainGui" + "BackGround", "У вас не хватает средств");
                    return;
                }
                if (GetLot.LastBitPlayer == 0)
                {
                    if (config.UseServerRwards)
                        TakeServerRewardMoney(player.userID, Bid);
                    GetLot.LastBitPlayer = player.userID;
                    GetLot.Bid = Bid;
                    GetLot.Buyout = Buyout;
                    DrawAuction(player, Page);
                }
                else
                {
                    if (config.UseServerRwards)
                    {
                        GiveServerRewardMoney(GetLot.LastBitPlayer, GetLot.Bid);
                        TakeServerRewardMoney(player.userID, Bid);
                    }
                    GetLot.LastBit = GetLot.Bid;
                    GetLot.Bid = Bid;
                    GetLot.Buyout = Buyout;
                    DrawAuction(player, Page);
                }
            }
        }
        [ConsoleCommand("DelLot")]
        void DelLot(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            int LotId = int.Parse(arg.Args[0]);
            int TypePage = int.Parse(arg.Args[1]);
            int Page = int.Parse(arg.Args[2]);
            AuctionLot GetLot = DataBase.Lots.Find(x => x.LotId == LotId);
            ItemInv data = new ItemInv()
            {
                ItemId = random.Next(1, 100000),
                Ammount = GetLot.Count,
                ShortName = GetLot.ShortName,
                Skin = GetLot.skin
            };
            if (GetLot.LastBitPlayer == 0)
            {
                DataBase.PlayerDatabase[player.userID].Inventory.Add(data);
                DataBase.Lots.Remove(GetLot);
            }
            else
            {
                if (config.UseGameStores)
                {
                    GiveGsMoney(GetLot.LastBitPlayer, GetLot.Bid, (code, status) =>
                    {
                        if (code != 200 || status == "fail")
                        {
                            DrawMessage(player, "AuctionYourMainGui" + "BackGround" + "BackGround", "Ошибка");
                            return;
                        }
                    });
                }
                if (config.UseEconomics)
                    GiveEconomycsMoney(GetLot.LastBitPlayer, GetLot.Bid);
                if (config.UseServerRwards)
                    GiveServerRewardMoney(GetLot.LastBitPlayer, GetLot.Bid);

                DataBase.PlayerDatabase[player.userID].Inventory.Add(data);
                DataBase.Lots.Remove(GetLot);
            }
            if (TypePage == 1)
                DrawYourAuction(player, Page);
            if (TypePage == 0)
                DrawAuction(player, Page);


        }
        [ConsoleCommand("UI_Buyout")]
        void UI_Buyout(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            int LotId = int.Parse(arg.Args[0]);
            AuctionLot GetLot = DataBase.Lots.Find(x => x.LotId == LotId);
            if (config.UseGameStores)
            {
                GetBalanceGameStore(player, (code, balance) =>
                {
                    int GetBalance = int.Parse(balance.Split('.')[0]);
                    if (GetBalance < GetLot.Buyout)
                    {
                        CuiHelper.DestroyUi(player, "AuctionConfirmGui");
                        DrawMessage(player, "AuctionMainGui" + "BackGround", "У вас не хватает средств");
                        return;
                    }


                    if (GetLot.LastBitPlayer == 0)
                    {
                        float GetBuyout = GetLot.Buyout;
                        if (config.TakePercent) GetBuyout -= GetBuyout / 100 * config.PercentForMoney;
                        TakeGsMoney(player.userID, GetBuyout, (code2, status) =>
                        {
                            if (code2 != 200 || status == "fail")
                            {
                                CuiHelper.DestroyUi(player, "AuctionConfirmGui");
                                DrawMessage(player, "AuctionMainGui" + "BackGround", "Ошибка");
                                return;
                            }
                        });
                        GiveGsMoney(GetLot.Owner, GetBuyout, (code2, status) =>
                        {
                            if (code2 != 200 || status == "fail")
                            {
                                CuiHelper.DestroyUi(player, "AuctionConfirmGui");
                                DrawMessage(player, "AuctionMainGui" + "BackGround", "Ошибка");
                                return;
                            }
                        });
                        ItemInv data = new ItemInv()
                        {
                            ItemId = random.Next(1, 100000),
                            Ammount = GetLot.Count,
                            ShortName = GetLot.ShortName,
                            Skin = GetLot.skin,
                        };
                        DataBase.PlayerDatabase[player.userID].Inventory.Add(data);
                        DataBase.Lots.Remove(GetLot);
                        CuiHelper.DestroyUi(player, "AuctionConfirmGui");
                        DrawAuction(player);
                    }
                    else
                    {
                        float GetBuyout = GetLot.Buyout;
                        if (config.TakePercent) GetBuyout -= GetBuyout / 100 * config.PercentForMoney;
                        if (GetLot.LastBitPlayer != player.userID)
                        {
                            GiveGsMoney(GetLot.LastBitPlayer, GetLot.Bid, (code2, status) =>
                            {
                                if (code2 != 200 || status == "fail")
                                {
                                    CuiHelper.DestroyUi(player, "AuctionConfirmGui");
                                    DrawMessage(player, "AuctionMainGui" + "BackGround", "Ошибка");
                                    return;
                                }
                            });
                        }
                        TakeGsMoney(player.userID, GetBuyout, (code2, status) =>
                        {
                            if (code2 != 200 || status == "fail")
                            {
                                CuiHelper.DestroyUi(player, "AuctionConfirmGui");
                                DrawMessage(player, "AuctionMainGui" + "BackGround", "Ошибка");
                                return;
                            }
                        });
                        GiveGsMoney(GetLot.Owner, GetBuyout, (code2, status) =>
                        {
                            if (code2 != 200 || status == "fail")
                            {
                                CuiHelper.DestroyUi(player, "AuctionConfirmGui");
                                DrawMessage(player, "AuctionMainGui" + "BackGround", "Ошибка");
                                return;
                            }
                        });
                        ItemInv data = new ItemInv()
                        {
                            ItemId = random.Next(1, 100000),
                            Ammount = GetLot.Count,
                            ShortName = GetLot.ShortName,
                            Skin = GetLot.skin,
                        };
                        DataBase.Lots.Remove(GetLot);
                        DataBase.PlayerDatabase[player.userID].Inventory.Add(data);
                        CuiHelper.DestroyUi(player, "AuctionConfirmGui");
                        DrawAuction(player);
                    }
                });
            }
            if (config.UseEconomics)
            {
                double GetBalance = GetBalanceEconomycs(player.userID);
                if (Convert.ToInt32(GetBalance) < GetLot.Buyout)
                {
                    CuiHelper.DestroyUi(player, "AuctionConfirmGui");
                    DrawMessage(player, "AuctionMainGui" + "BackGround", "У вас не хватает средств");
                    return;
                }
                if (GetLot.LastBitPlayer == 0)
                {
                    float GetBuyout = GetLot.Buyout;
                    if (config.TakePercent) GetBuyout -= GetBuyout / 100 * config.PercentForMoney;
                    TakeEconomycsMoney(player.userID, GetBuyout);
                    GiveEconomycsMoney(GetLot.Owner, GetBuyout);
                    ItemInv data = new ItemInv()
                    {
                        ItemId = random.Next(1, 100000),
                        Ammount = GetLot.Count,
                        ShortName = GetLot.ShortName,
                        Skin = GetLot.skin,
                    };
                    DataBase.Lots.Remove(GetLot);
                    DataBase.PlayerDatabase[player.userID].Inventory.Add(data);
                    CuiHelper.DestroyUi(player, "AuctionConfirmGui");
                    DrawAuction(player);
                }
                else
                {
                    float GetBuyout = GetLot.Buyout;
                    if (config.TakePercent) GetBuyout -= GetBuyout / 100 * config.PercentForMoney;
                    if (GetLot.LastBitPlayer != player.userID) GiveEconomycsMoney(GetLot.LastBitPlayer, GetLot.Bid);
                    TakeEconomycsMoney(player.userID, GetBuyout);
                    GiveEconomycsMoney(GetLot.Owner, GetBuyout);
                    ItemInv data = new ItemInv()
                    {
                        ItemId = random.Next(1, 100000),
                        Ammount = GetLot.Count,
                        ShortName = GetLot.ShortName,
                        Skin = GetLot.skin,
                    };
                    DataBase.Lots.Remove(GetLot);
                    DataBase.PlayerDatabase[player.userID].Inventory.Add(data);
                    CuiHelper.DestroyUi(player, "AuctionConfirmGui");
                    DrawAuction(player);
                }
            }
            if (config.UseServerRwards)
            {
                double GetBalance = GetBalanceEconomycs(player.userID);
                if (Convert.ToInt32(GetBalance) < GetLot.Buyout)
                {
                    CuiHelper.DestroyUi(player, "AuctionConfirmGui");
                    DrawMessage(player, "AuctionMainGui" + "BackGround", "У вас не хватает средств");
                    return;
                }
                if (GetLot.LastBitPlayer == 0)
                {
                    float GetBuyout = GetLot.Buyout;
                    if (config.TakePercent) GetBuyout -= GetBuyout / 100 * config.PercentForMoney;
                    TakeServerRewardMoney(player.userID, GetBuyout);
                    GiveServerRewardMoney(GetLot.Owner, GetBuyout);
                    ItemInv data = new ItemInv()
                    {
                        ItemId = random.Next(1, 100000),
                        Ammount = GetLot.Count,
                        ShortName = GetLot.ShortName,
                        Skin = GetLot.skin,
                    };
                    DataBase.Lots.Remove(GetLot);
                    DataBase.PlayerDatabase[player.userID].Inventory.Add(data);
                    CuiHelper.DestroyUi(player, "AuctionConfirmGui");
                    DrawAuction(player);
                }
                else
                {
                    float GetBuyout = GetLot.Buyout;
                    if (config.TakePercent) GetBuyout -= GetBuyout / 100 * config.PercentForMoney;
                    if (GetLot.LastBitPlayer != player.userID) GiveServerRewardMoney(GetLot.LastBitPlayer, GetLot.Bid);
                    TakeServerRewardMoney(player.userID, GetBuyout);
                    GiveServerRewardMoney(GetLot.Owner, GetBuyout);
                    ItemInv data = new ItemInv()
                    {
                        ItemId = random.Next(1, 100000),
                        Ammount = GetLot.Count,
                        ShortName = GetLot.ShortName,
                        Skin = GetLot.skin,
                    };
                    DataBase.Lots.Remove(GetLot);
                    DataBase.PlayerDatabase[player.userID].Inventory.Add(data);
                    CuiHelper.DestroyUi(player, "AuctionConfirmGui");
                    DrawAuction(player);
                }
            }

        }
        [ConsoleCommand("AddAuction")]
        void AddAuction(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            int ItemId = int.Parse(arg.Args[0]);
            ulong Skin = ulong.Parse(arg.Args[1]);
            int Ammount = int.Parse(arg.Args[2]);
            string ShortName = arg.Args[3];
            int Page = int.Parse(arg.Args[4]);
            int Bit = int.Parse(arg.Args[5]);
            int Buyout = int.Parse(arg.Args[6]);
            List<AuctionLot> GetLotsList = DataBase.Lots.FindAll(x => x.Owner == player.userID);
            if (GetLotsList.Count!=0)
            {
                if (GetLotsList.Count >= config.HowCanAddLots)
                {
                    CuiHelper.DestroyUi(player, "AuctionAddInvGui");
                    DrawMessage(player, "AuctionInvGui" + "BackGround", "У вас максимум лотов");
                    return;

                }
            }
            AuctionLot Data = new AuctionLot
            {
                LotId = random.Next(1, 10000),
                Owner = arg.Player().userID,
                ShortName = ShortName,
                Count = Ammount,
                Hours = config.HowHoursForLots,
                skin = Skin,
                StartBit = Bit,
                LastBit = Bit,
                LastBitPlayer = 0,
                Bid = Bit,
                Buyout = Buyout,
                Date = DateTime.Now

            };
            DataBase.Lots.Add(Data);
            ItemInv GetItem = DataBase.PlayerDatabase[player.userID].Inventory.Find(x => x.ItemId == ItemId);
            DataBase.PlayerDatabase[player.userID].Inventory.Remove(GetItem);
            CuiHelper.DestroyUi(player, "AuctionAddGui" + "Count");
            DrawInvenoty(player, Page);
            SaveData();


        }
        [ConsoleCommand("UI_BackToMainAuc")]
        void BackToMain(ConsoleSystem.Arg arg)
        {
            CuiHelper.DestroyUi(arg.Player(), arg.Args[0]);
            DrawAuction(arg.Player());
        }
        #endregion
        #region Gui
        void DrawAuction(BasePlayer player, int page = 0)
        {
            CuiHelper.DestroyUi(player, "AuctionMainGui");
            var AuctionMainGui = new CuiElementContainer();
            AuctionMainGui.Add(new CuiElement()
            {
                Parent = "Overlay",
                Name = "AuctionMainGui",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.2 0.2 0.2 0.8",
                        Material = matterial
                    },
                    new CuiNeedsCursorComponent()
                    {

                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 0",
                        AnchorMax = "1 1",


                    }
                }
            });
            AuctionMainGui.Add(new CuiButton
            {
                Button = { Close = "AuctionMainGui", Color = "0 0 0 0" },
                Text = { Text = "" },
                RectTransform = { AnchorMax = "1 1", AnchorMin = "0 0" }
            }, "AuctionMainGui");
            AuctionMainGui.Add(new CuiElement()
            {
                Parent = "AuctionMainGui",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "Аукцион",
                        Align = TextAnchor.MiddleCenter,
                        FontSize= 35
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-336.85 241.6",
                        OffsetMax = "336.85 326.4"

                    }
                }
            });

            AuctionMainGui.Add(new CuiElement()
            {
                Parent = "AuctionMainGui",
                Name = "AuctionMainGui" + "BackGround",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.3 0.3 0.3 0.7",
                        Material = matterial
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-395.5 -263.36",
                        OffsetMax = "395.5 231.8"

                    }
                }
            });
            AuctionMainGui.Add(new CuiElement()
            {
                Parent = "AuctionMainGui" + "BackGround",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "Лоты",
                        Align = TextAnchor.UpperCenter,
                        FontSize = 22
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "0 -40",
                        OffsetMax = "800 0"
                    }
                }
            });
            AuctionMainGui.Add(new CuiElement()
            {
                Parent = "AuctionMainGui" + "BackGround",
                Name = "AuctionMainGui" + "BackGround" + "Balance",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.5 0.5 0.5 0.9",
                        Material = matterial
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 0",
                        AnchorMax = "0 0",
                        OffsetMin = "4.57 3.875",
                        OffsetMax = "141.38 25.525"

                    }
                }
            });
            if (config.UseEconomics)
            {
                double GetBalance = (double)Economics?.Call("Balance", player.UserIDString);

                AuctionMainGui.Add(new CuiElement()
                {
                    Parent = "AuctionMainGui" + "BackGround" + "Balance",
                    Components =
                    {
                        new CuiTextComponent()
                        {
                            Text = $"Баланс: {Convert.ToInt32(GetBalance)}{config.TugrickName}",
                            Align = TextAnchor.MiddleCenter,
                        },
                        new CuiRectTransformComponent()
                        {

                            AnchorMin = "0 0",
                            AnchorMax = "1 1",

                        }
                    }
                });
            }
            if (config.UseServerRwards)
            {
                int GetBalance = (int)ServerRewards?.Call("CheckPoints", player.userID);

                AuctionMainGui.Add(new CuiElement()
                {
                    Parent = "AuctionMainGui" + "BackGround" + "Balance",
                    Components =
                    {
                        new CuiTextComponent()
                        {
                            Text = $"Баланс: {GetBalance}{config.TugrickName}",
                            Align = TextAnchor.MiddleCenter,
                        },
                        new CuiRectTransformComponent()
                        {

                            AnchorMin = "0 0",
                            AnchorMax = "1 1",

                        }
                    }
                });
            }
            AuctionMainGui.Add(new CuiButton()
            {
                Button =
                        {
                            Color = "0.5 0.5 0.5 0.9",
                           Close = "AuctionMainGui"
                        },
                Text =
                        {
                            Text = "Закрыть",
                            Align = TextAnchor.MiddleCenter,
                        },
                RectTransform =
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = "718.74 3.874997",
                            OffsetMax = "785.96 25.525"
                        }
            }, "AuctionMainGui" + "BackGround");
            AuctionMainGui.Add(new CuiButton()
            {
                Button =
                        {
                            Color = "0.5 0.5 0.5 0.9",
                           Command = "OpenInventory"
                        },
                Text =
                        {
                            Text = "Инвентарь",
                            Align = TextAnchor.MiddleCenter,
                        },
                RectTransform =
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = "617.675 3.874997",
                            OffsetMax = "712.725 25.525"
                        }
            }, "AuctionMainGui" + "BackGround");
            AuctionMainGui.Add(new CuiButton()
            {
                Button =
                        {
                            Color = "0.5 0.5 0.5 0.9",
                           Command = "OpenYourLots"
                        },
                Text =
                        {
                            Text = "Ваши лоты",
                            Align = TextAnchor.MiddleCenter,
                        },
                RectTransform =
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = "516.475 3.874997",
                            OffsetMax = "611.525 25.525"
                        }
            }, "AuctionMainGui" + "BackGround");
            #region Tittles
            AuctionMainGui.Add(new CuiElement()
            {
                Parent = "AuctionMainGui" + "BackGround",
                Name = "AuctionMainGui" + "BackGround" + "ItemName",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.5 0.5 0.5 0.9",
                        Material = matterial
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "11.00443 -61.25",
                        OffsetMax = "226.0044 -35.11"

                    }
                }
            });
            AuctionMainGui.Add(new CuiElement()
            {
                Parent = "AuctionMainGui" + "BackGround" + "ItemName",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "Предмет",
                        Align = TextAnchor.MiddleCenter,
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 0",
                        AnchorMax = "1 1",

                    }
                }
            });

            AuctionMainGui.Add(new CuiElement()
            {
                Parent = "AuctionMainGui" + "BackGround",
                Name = "AuctionMainGui" + "BackGround" + "Time",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.5 0.5 0.5 0.9",
                        Material = matterial
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "231.0017 -61.25",
                        OffsetMax = "366.0017 -35.11"

                    }
                }
            });

            AuctionMainGui.Add(new CuiElement()
            {
                Parent = "AuctionMainGui" + "BackGround" + "Time",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "Истекает",
                        Align = TextAnchor.MiddleCenter,
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 0",
                        AnchorMax = "1 1",

                    }
                }
            });
            AuctionMainGui.Add(new CuiElement()
            {
                Parent = "AuctionMainGui" + "BackGround",
                Name = "AuctionMainGui" + "BackGround" + "SetPrice",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.5 0.5 0.5 0.9",
                        Material = matterial
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "371.004 -61.25",
                        OffsetMax = "476.004 -35.11"

                    }
                }
            });
            AuctionMainGui.Add(new CuiElement()
            {
                Parent = "AuctionMainGui" + "BackGround" + "SetPrice",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "Ставка",
                        Align = TextAnchor.MiddleCenter,
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 0",
                        AnchorMax = "1 1",

                    }
                }
            });
            AuctionMainGui.Add(new CuiElement()
            {
                Parent = "AuctionMainGui" + "BackGround",
                Name = "AuctionMainGui" + "BackGround" + "Buy",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.5 0.5 0.5 0.9",
                        Material = matterial
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "481.0067 -61.25",
                        OffsetMax = "616.0067 -35.11"

                    }
                }
            });

            AuctionMainGui.Add(new CuiElement()
            {
                Parent = "AuctionMainGui" + "BackGround" + "Buy",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "Выкуп",
                        Align = TextAnchor.MiddleCenter,
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 0",
                        AnchorMax = "1 1",

                    }
                }
            });
            AuctionMainGui.Add(new CuiElement()
            {
                Parent = "AuctionMainGui" + "BackGround",
                Name = "AuctionMainGui" + "BackGround" + "Action",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.5 0.5 0.5 0.9",
                        Material = matterial
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "621.0016 -61.25",
                        OffsetMax = "787.0216 -35.11"

                    }
                }
            });
            AuctionMainGui.Add(new CuiElement()
            {
                Parent = "AuctionMainGui" + "BackGround" + "Action",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "Действия",
                        Align = TextAnchor.MiddleCenter,
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 0",
                        AnchorMax = "1 1",

                    }
                }
            });
            #endregion

            #region Generate

            int StartOffsetMin = 111;
            int StartOffsetMax = 65;
            int SizeY = 44;
            int Offset = 4;
            int DrawCount = 8;
            int StartCount = 0;
            int i = 0;
            List<AuctionLot> LotsList = DataBase.Lots;


            foreach (var GetLots in LotsList.Skip(page * DrawCount).Take(DrawCount))
            {

                if (StartCount < DrawCount)
                {
                    AuctionMainGui.Add(new CuiElement()
                    {
                        Parent = "AuctionMainGui" + "BackGround",
                        Name = "AuctionMainGui" + "BackGround" + "Item" + i,
                        Components =
                        {
                            new CuiImageComponent()
                            {
                                Color = "0 0 0 0"
                            },
                            new CuiRectTransformComponent()
                            {

                                AnchorMin = "0 1",
                                AnchorMax = "0 1",
                                OffsetMin = $"11.005 -{StartOffsetMin}",
                                OffsetMax = $"786.955 -{StartOffsetMax}"

                            }
                        }
                    });
                    AuctionMainGui.Add(new CuiElement()
                    {
                        Parent = "AuctionMainGui" + "BackGround" + "Item" + i,
                        Name = "AuctionMainGui" + "BackGround" + "Item" + i + "ItemName",
                        Components =
                        {
                            new CuiImageComponent()
                            {
                                Color = "0.5 0.5 0.5 0.7",
                        Material = matterial
                            },
                            new CuiRectTransformComponent()
                            {

                                AnchorMin = "0 1",
                                AnchorMax = "0 1",
                                OffsetMin = $"0 -42",
                                OffsetMax = $"215 0"

                            }
                        }
                    });
                    AuctionMainGui.Add(new CuiElement()
                    {
                        Parent = "AuctionMainGui" + "BackGround" + "Item" + i + "ItemName",
                        Components =
                        {
                            new CuiRawImageComponent()
                            {
                                Png = (string)ImageLibrary?.Call("GetImage",GetLots.ShortName)
                            },
                            new CuiRectTransformComponent()
                            {

                                AnchorMin = "0 0",
                                AnchorMax = "0 0",
                                OffsetMin = "5 2",
                                OffsetMax = "45 42"

                            }
                        }
                    });
                    AuctionMainGui.Add(new CuiElement()
                    {
                        Parent = "AuctionMainGui" + "BackGround" + "Item" + i + "ItemName",
                        Components =
                        {
                            new CuiTextComponent()
                            {
                                Text = $"{GetLots.Count}x",
                                Align = TextAnchor.LowerRight,
                                FontSize = 10
                            },
                            new CuiRectTransformComponent()
                            {

                                AnchorMin = "0 0",
                                AnchorMax = "0 0",
                                OffsetMin = "5 2",
                                OffsetMax = "45 42"

                            }
                        }
                    });
                    AuctionMainGui.Add(new CuiElement()
                    {
                        Parent = "AuctionMainGui" + "BackGround" + "Item" + i + "ItemName",
                        Components =
                        {
                            new CuiTextComponent()
                            {
                                Text = LocData.TranslatedItems.ContainsKey(GetLots.ShortName) ? LocData.TranslatedItems[GetLots.ShortName] : GetDefaultName(GetLots.ShortName),
                                Align = TextAnchor.MiddleLeft
                            },
                            new CuiRectTransformComponent()
                            {

                                AnchorMin = "0 0",
                                AnchorMax = "0 0",
                                OffsetMin = "53.16499 2",
                                OffsetMax = "220.235 42"

                            }
                        }
                    });

                    AuctionMainGui.Add(new CuiElement()
                    {
                        Parent = "AuctionMainGui" + "BackGround" + "Item" + i,
                        Name = "AuctionMainGui" + "BackGround" + "Item" + i + "DateTime",
                        Components =
                        {
                            new CuiImageComponent()
                            {
                                Color = "0.5 0.5 0.5 0.7",
                        Material = matterial
                            },
                            new CuiRectTransformComponent()
                            {

                                AnchorMin = "0 1",
                                AnchorMax = "0 1",
                                OffsetMin = $"220 -42",
                                OffsetMax = $"355 0"

                            }
                        }
                    });
                    DateTime GetEndTime = GetLots.Date.AddHours(GetLots.Hours);
                    TimeSpan LeftTime = GetEndTime.Subtract(DateTime.Now);
                    AuctionMainGui.Add(new CuiElement()
                    {
                        Parent = "AuctionMainGui" + "BackGround" + "Item" + i + "DateTime",
                        Components =
                        {
                            new CuiTextComponent()
                            {
                                Text = $"{LeftTime.Hours}ч и {LeftTime.Minutes} минут",
                                Align = TextAnchor.MiddleCenter
                            },
                            new CuiRectTransformComponent()
                            {

                                AnchorMin = "0 0",
                                AnchorMax = "1 1",


                            }
                        }
                    });
                    AuctionMainGui.Add(new CuiElement()
                    {
                        Parent = "AuctionMainGui" + "BackGround" + "Item" + i,
                        Name = "AuctionMainGui" + "BackGround" + "Item" + i + "Bid",
                        Components =
                        {
                            new CuiImageComponent()
                            {
                                Color = "0.5 0.5 0.5 0.7",
                        Material = matterial
                            },
                            new CuiRectTransformComponent()
                            {

                                AnchorMin = "0 1",
                                AnchorMax = "0 1",
                                OffsetMin = $"360 -42",
                                OffsetMax = $"465 0"

                            }
                        }
                    });
                    AuctionMainGui.Add(new CuiElement()
                    {
                        Parent = "AuctionMainGui" + "BackGround" + "Item" + i + "Bid",
                        Components =
                        {
                            new CuiTextComponent()
                            {
                                Text = $"{GetLots.Bid}{config.TugrickName}",
                                Align = TextAnchor.MiddleCenter
                            },
                            new CuiRectTransformComponent()
                            {

                                AnchorMin = "0 0",
                                AnchorMax = "1 1",

                            }
                        }
                    });
                    AuctionMainGui.Add(new CuiElement()
                    {
                        Parent = "AuctionMainGui" + "BackGround" + "Item" + i,
                        Name = "AuctionMainGui" + "BackGround" + "Item" + i + "Buyout",
                        Components =
                        {
                            new CuiImageComponent()
                            {
                                Color = "0.5 0.5 0.5 0.7",
                        Material = matterial
                            },
                            new CuiRectTransformComponent()
                            {

                                AnchorMin = "0 1",
                                AnchorMax = "0 1",
                                OffsetMin = $"470 -42",
                                OffsetMax = $"605 0"

                            }
                        }
                    });
                    AuctionMainGui.Add(new CuiElement()
                    {
                        Parent = "AuctionMainGui" + "BackGround" + "Item" + i + "Buyout",
                        Components =
                        {
                            new CuiTextComponent()
                            {
                                Text = $"{GetLots.Buyout}{config.TugrickName}",
                                Align = TextAnchor.MiddleCenter
                            },
                            new CuiRectTransformComponent()
                            {

                                AnchorMin = "0 0",
                                AnchorMax = "1 1",

                            }
                        }
                    });

                    AuctionMainGui.Add(new CuiElement()
                    {
                        Parent = "AuctionMainGui" + "BackGround" + "Item" + i,
                        Name = "AuctionMainGui" + "BackGround" + "Item" + i + "Action",
                        Components =
                        {
                            new CuiImageComponent()
                            {
                                Color = "0.5 0.5 0.5 0.7",
                        Material = matterial
                            },
                            new CuiRectTransformComponent()
                            {

                                AnchorMin = "0 1",
                                AnchorMax = "0 1",
                                OffsetMin = $"610 -42",
                                OffsetMax = $"776.015 0"

                            }
                        }
                    });
                    if (GetLots.Owner == player.userID)
                    {
                        AuctionMainGui.Add(new CuiButton()
                        {
                            Button =
                            {
                                Color = "0.6 0.6 0.6 0.9",
                                Material = matterial,
                                Command = $"DelLot {GetLots.LotId} 0 {page}",
                            },
                            Text =
                            {
                                Text = "Снять лот",
                                Align = TextAnchor.MiddleCenter,
                            },
                            RectTransform =
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "0 0",
                                OffsetMin = "39.9 8.140001",
                                OffsetMax = "134.9 31.94"
                            }
                        }, "AuctionMainGui" + "BackGround" + "Item" + i + "Action");
                    }
                    else
                    {
                        AuctionMainGui.Add(new CuiButton()
                        {
                            Button =
                            {
                                Color = "0.6 0.6 0.6 0.9",
                                Material = matterial,
                                Command = $"OpenBit {GetLots.LotId} {page}",
                            },
                            Text =
                            {
                                Text = "Ставка",
                                Align = TextAnchor.MiddleCenter,
                            },
                            RectTransform =
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "0 0",
                                OffsetMin = "9.319988 8.140009",
                                OffsetMax = "78.77998 31.94001"
                            }
                        }, "AuctionMainGui" + "BackGround" + "Item" + i + "Action");
                        AuctionMainGui.Add(new CuiButton()
                        {
                            Button =
                            {
                                Color = "0.6 0.6 0.6 0.9",
                                Material = matterial,
                                Command = $"OpenConfirm AuctionMin {"AuctionMainGui" + "BackGround"} {GetLots.LotId}",
                            },
                            Text =
                            {
                                Text = "Выкуп",
                                Align = TextAnchor.MiddleCenter,
                            },
                            RectTransform =
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "0 0",
                                OffsetMin = "87.01 8.140001",
                                OffsetMax = "163.51 31.94"
                            }
                        }, "AuctionMainGui" + "BackGround" + "Item" + i + "Action");
                    }
                }
                StartOffsetMin += (SizeY + Offset);
                StartOffsetMax += (SizeY + Offset);
                StartCount++;

                if (StartCount == DrawCount)
                {
                    AuctionMainGui.Add(new CuiButton()
                    {
                        Button =
                        {
                            Color = "0.5 0.5 0.5 0.9",
                            Command = $"InventoryPage Next {page}",
                        },
                        Text =
                        {
                            Text = "Далее>",
                            Align = TextAnchor.MiddleCenter,
                        },
                        RectTransform =
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = "405.275 3.875015",
                            OffsetMax = "500.325 25.52501"
                        }
                    }, "AuctionMainGui" + "BackGround");
                }
                if (page > 0)
                {
                    AuctionMainGui.Add(new CuiButton()
                    {
                        Button =
                        {
                            Color = "0.5 0.5 0.5 0.9",
                            Command = $"InventoryPage Back {page}",
                        },
                        Text =
                        {
                            Text = "<Назад",
                            Align = TextAnchor.MiddleCenter,
                        },
                        RectTransform =
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = "292.975 3.875015",
                            OffsetMax = "388.025 25.52501"
                        }
                    }, "AuctionMainGui" + "BackGround");
                }
                i++;
            }
            CuiHelper.AddUi(player, AuctionMainGui);
            if (config.UseGameStores == true) DrawBalance(player, "AuctionMainGui" + "BackGround");
            #endregion
        }
        void DrawBalance(BasePlayer player, string parent)
        {
            GetBalanceGameStore(player, (Code, Balance) => {
                var AuctionMainGui = new CuiElementContainer();
                int GetBalance = int.Parse(Balance.Split('.')[0]);
                AuctionMainGui.Add(new CuiElement()
                {
                    Parent = parent + "Balance",
                    Components =
                        {
                            new CuiTextComponent()
                            {
                                Text = $"Баланс: {GetBalance}{config.TugrickName}",
                                Align = TextAnchor.MiddleCenter,
                            },
                            new CuiRectTransformComponent()
                            {

                                AnchorMin = "0 0",
                                AnchorMax = "1 1",

                            }
                        }
                });
                CuiHelper.AddUi(player, AuctionMainGui);
            });
        }
        void DrawYourAuction(BasePlayer player, int page = 0)
        {
            CuiHelper.DestroyUi(player, "AuctionYourMainGui");
            var AuctionYourMainGui = new CuiElementContainer();
            AuctionYourMainGui.Add(new CuiElement()
            {
                Parent = "Overlay",
                Name = "AuctionYourMainGui",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.2 0.2 0.2 0.8",
                        Material = matterial
                    },
                    new CuiNeedsCursorComponent()
                    {

                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 0",
                        AnchorMax = "1 1",


                    }
                }
            });
            AuctionYourMainGui.Add(new CuiButton
            {
                Button = { Close = "AuctionYourMainGui", Color = "0 0 0 0" },
                Text = { Text = "" },
                RectTransform = { AnchorMax = "1 1", AnchorMin = "0 0" }
            }, "AuctionYourMainGui");
            AuctionYourMainGui.Add(new CuiElement()
            {
                Parent = "AuctionYourMainGui",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "Аукцион",
                        Align = TextAnchor.MiddleCenter,
                        FontSize= 35
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-336.85 241.6",
                        OffsetMax = "336.85 326.4"

                    }
                }
            });

            AuctionYourMainGui.Add(new CuiElement()
            {
                Parent = "AuctionYourMainGui",
                Name = "AuctionYourMainGui" + "BackGround",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.3 0.3 0.3 0.7",
                        Material = matterial
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-395.5 -263.36",
                        OffsetMax = "395.5 231.8"

                    }
                }
            });
            AuctionYourMainGui.Add(new CuiElement()
            {
                Parent = "AuctionYourMainGui" + "BackGround",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "Ваши Лоты",
                        Align = TextAnchor.UpperCenter,
                        FontSize = 22
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "0 -40",
                        OffsetMax = "800 0"
                    }
                }
            });
            AuctionYourMainGui.Add(new CuiElement()
            {
                Parent = "AuctionYourMainGui" + "BackGround",
                Name = "AuctionYourMainGui" + "BackGround" + "Balance",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.5 0.5 0.5 0.9",
                        Material = matterial
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 0",
                        AnchorMax = "0 0",
                        OffsetMin = "4.57 3.875",
                        OffsetMax = "141.38 25.525"

                    }
                }
            });
            if (config.UseEconomics)
            {
                double GetBalance = (double)Economics?.Call("Balance", player.UserIDString);

                AuctionYourMainGui.Add(new CuiElement()
                {
                    Parent = "AuctionYourMainGui" + "BackGround" + "Balance",
                    Components =
                    {
                        new CuiTextComponent()
                        {
                            Text = $"Баланс: {Convert.ToInt32(GetBalance)}{config.TugrickName}",
                            Align = TextAnchor.MiddleCenter,
                        },
                        new CuiRectTransformComponent()
                        {

                            AnchorMin = "0 0",
                            AnchorMax = "1 1",

                        }
                    }
                });
            }
            if (config.UseServerRwards)
            {
                int GetBalance = (int)ServerRewards?.Call("CheckPoints", player.userID);

                AuctionYourMainGui.Add(new CuiElement()
                {
                    Parent = "AuctionYourMainGui" + "BackGround" + "Balance",
                    Components =
                    {
                        new CuiTextComponent()
                        {
                            Text = $"Баланс: {GetBalance}{config.TugrickName}",
                            Align = TextAnchor.MiddleCenter,
                        },
                        new CuiRectTransformComponent()
                        {

                            AnchorMin = "0 0",
                            AnchorMax = "1 1",

                        }
                    }
                });
            }
            AuctionYourMainGui.Add(new CuiButton()
            {
                Button =
                        {
                            Color = "0.5 0.5 0.5 0.9",
                           Command = "UI_BackToMainAuc AuctionYourMainGui",
                        Material = matterial
                        },
                Text =
                        {
                            Text = "Назад",
                            Align = TextAnchor.MiddleCenter,
                        },
                RectTransform =
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = "718.74 3.874997",
                            OffsetMax = "785.96 25.525"
                        }
            }, "AuctionYourMainGui" + "BackGround");
            #region Tittles
            AuctionYourMainGui.Add(new CuiElement()
            {
                Parent = "AuctionYourMainGui" + "BackGround",
                Name = "AuctionYourMainGui" + "BackGround" + "ItemName",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.5 0.5 0.5 0.9",
                        Material = matterial
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "11.00443 -61.25",
                        OffsetMax = "226.0044 -35.11"

                    }
                }
            });
            AuctionYourMainGui.Add(new CuiElement()
            {
                Parent = "AuctionYourMainGui" + "BackGround" + "ItemName",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "Предмет",
                        Align = TextAnchor.MiddleCenter,
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 0",
                        AnchorMax = "1 1",

                    }
                }
            });

            AuctionYourMainGui.Add(new CuiElement()
            {
                Parent = "AuctionYourMainGui" + "BackGround",
                Name = "AuctionYourMainGui" + "BackGround" + "Time",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.5 0.5 0.5 0.9",
                        Material = matterial
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "231.0017 -61.25",
                        OffsetMax = "366.0017 -35.11"

                    }
                }
            });

            AuctionYourMainGui.Add(new CuiElement()
            {
                Parent = "AuctionYourMainGui" + "BackGround" + "Time",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "Истекает",
                        Align = TextAnchor.MiddleCenter,
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 0",
                        AnchorMax = "1 1",

                    }
                }
            });
            AuctionYourMainGui.Add(new CuiElement()
            {
                Parent = "AuctionYourMainGui" + "BackGround",
                Name = "AuctionYourMainGui" + "BackGround" + "SetPrice",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.5 0.5 0.5 0.9",
                        Material = matterial
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "371.004 -61.25",
                        OffsetMax = "476.004 -35.11"

                    }
                }
            });
            AuctionYourMainGui.Add(new CuiElement()
            {
                Parent = "AuctionYourMainGui" + "BackGround" + "SetPrice",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "Ставка",
                        Align = TextAnchor.MiddleCenter,
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 0",
                        AnchorMax = "1 1",

                    }
                }
            });
            AuctionYourMainGui.Add(new CuiElement()
            {
                Parent = "AuctionYourMainGui" + "BackGround",
                Name = "AuctionYourMainGui" + "BackGround" + "Buy",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.5 0.5 0.5 0.9",
                        Material = matterial
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "481.0067 -61.25",
                        OffsetMax = "616.0067 -35.11"

                    }
                }
            });

            AuctionYourMainGui.Add(new CuiElement()
            {
                Parent = "AuctionYourMainGui" + "BackGround" + "Buy",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "Выкуп",
                        Align = TextAnchor.MiddleCenter,
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 0",
                        AnchorMax = "1 1",

                    }
                }
            });
            AuctionYourMainGui.Add(new CuiElement()
            {
                Parent = "AuctionYourMainGui" + "BackGround",
                Name = "AuctionYourMainGui" + "BackGround" + "Action",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.5 0.5 0.5 0.9",
                        Material = matterial
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "621.0016 -61.25",
                        OffsetMax = "787.0216 -35.11"

                    }
                }
            });
            AuctionYourMainGui.Add(new CuiElement()
            {
                Parent = "AuctionYourMainGui" + "BackGround" + "Action",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "Действия",
                        Align = TextAnchor.MiddleCenter,
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 0",
                        AnchorMax = "1 1",

                    }
                }
            });
            #endregion

            #region Generate

            int StartOffsetMin = 111;
            int StartOffsetMax = 65;
            int SizeY = 44;
            int Offset = 4;
            int DrawCount = 8;
            int StartCount = 0;
            int i = 0;
            List<AuctionLot> LotsList = DataBase.Lots;


            foreach (var GetLots in LotsList.Skip(page * DrawCount).Take(DrawCount))
            {
                if (GetLots.Owner != player.userID) continue;
                if (StartCount < DrawCount)
                {
                    AuctionYourMainGui.Add(new CuiElement()
                    {
                        Parent = "AuctionYourMainGui" + "BackGround",
                        Name = "AuctionYourMainGui" + "BackGround" + "Item" + i,
                        Components =
                        {
                            new CuiImageComponent()
                            {
                                Color = "0 0 0 0"
                            },
                            new CuiRectTransformComponent()
                            {

                                AnchorMin = "0 1",
                                AnchorMax = "0 1",
                                OffsetMin = $"11.005 -{StartOffsetMin}",
                                OffsetMax = $"786.955 -{StartOffsetMax}"

                            }
                        }
                    });
                    AuctionYourMainGui.Add(new CuiElement()
                    {
                        Parent = "AuctionYourMainGui" + "BackGround" + "Item" + i,
                        Name = "AuctionYourMainGui" + "BackGround" + "Item" + i + "ItemName",
                        Components =
                        {
                            new CuiImageComponent()
                            {
                                Color = "0.5 0.5 0.5 0.7",
                        Material = matterial
                            },
                            new CuiRectTransformComponent()
                            {

                                AnchorMin = "0 1",
                                AnchorMax = "0 1",
                                OffsetMin = $"0 -42",
                                OffsetMax = $"215 0"

                            }
                        }
                    });
                    AuctionYourMainGui.Add(new CuiElement()
                    {
                        Parent = "AuctionYourMainGui" + "BackGround" + "Item" + i + "ItemName",
                        Components =
                        {
                            new CuiRawImageComponent()
                            {
                                Png = (string)ImageLibrary?.Call("GetImage",GetLots.ShortName)
                            },
                            new CuiRectTransformComponent()
                            {

                                AnchorMin = "0 0",
                                AnchorMax = "0 0",
                                OffsetMin = "5 2",
                                OffsetMax = "45 42"

                            }
                        }
                    });
                    AuctionYourMainGui.Add(new CuiElement()
                    {
                        Parent = "AuctionYourMainGui" + "BackGround" + "Item" + i + "ItemName",
                        Components =
                        {
                            new CuiTextComponent()
                            {
                                Text = $"{GetLots.Count}x",
                                Align = TextAnchor.LowerRight,
                                FontSize = 10
                            },
                            new CuiRectTransformComponent()
                            {

                                AnchorMin = "0 0",
                                AnchorMax = "0 0",
                                OffsetMin = "5 2",
                                OffsetMax = "45 42"

                            }
                        }
                    });
                    AuctionYourMainGui.Add(new CuiElement()
                    {
                        Parent = "AuctionYourMainGui" + "BackGround" + "Item" + i + "ItemName",
                        Components =
                        {
                            new CuiTextComponent()
                            {
                                Text = LocData.TranslatedItems.ContainsKey(GetLots.ShortName) ? LocData.TranslatedItems[GetLots.ShortName] : GetDefaultName(GetLots.ShortName),
                                Align = TextAnchor.MiddleLeft
                            },
                            new CuiRectTransformComponent()
                            {

                                AnchorMin = "0 0",
                                AnchorMax = "0 0",
                                OffsetMin = "53.16499 2",
                                OffsetMax = "220.235 42"

                            }
                        }
                    });
                    AuctionYourMainGui.Add(new CuiElement()
                    {
                        Parent = "AuctionYourMainGui" + "BackGround" + "Item" + i,
                        Name = "AuctionYourMainGui" + "BackGround" + "Item" + i + "DateTime",
                        Components =
                        {
                            new CuiImageComponent()
                            {
                                Color = "0.5 0.5 0.5 0.7",
                        Material = matterial
                            },
                            new CuiRectTransformComponent()
                            {

                                AnchorMin = "0 1",
                                AnchorMax = "0 1",
                                OffsetMin = $"220 -42",
                                OffsetMax = $"355 0"

                            }
                        }
                    });
                    DateTime GetEndTime = GetLots.Date.AddHours(GetLots.Hours);
                    TimeSpan LeftTime = GetEndTime.Subtract(DateTime.Now);
                    AuctionYourMainGui.Add(new CuiElement()
                    {
                        Parent = "AuctionYourMainGui" + "BackGround" + "Item" + i + "DateTime",
                        Components =
                        {
                            new CuiTextComponent()
                            {
                                Text = $"{LeftTime.Hours}ч и {LeftTime.Minutes} минут",
                                Align = TextAnchor.MiddleCenter
                            },
                            new CuiRectTransformComponent()
                            {

                                AnchorMin = "0 0",
                                AnchorMax = "1 1",


                            }
                        }
                    });
                    AuctionYourMainGui.Add(new CuiElement()
                    {
                        Parent = "AuctionYourMainGui" + "BackGround" + "Item" + i,
                        Name = "AuctionYourMainGui" + "BackGround" + "Item" + i + "Bid",
                        Components =
                        {
                            new CuiImageComponent()
                            {
                                Color = "0.5 0.5 0.5 0.7",
                        Material = matterial
                            },
                            new CuiRectTransformComponent()
                            {

                                AnchorMin = "0 1",
                                AnchorMax = "0 1",
                                OffsetMin = $"360 -42",
                                OffsetMax = $"465 0"

                            }
                        }
                    });
                    AuctionYourMainGui.Add(new CuiElement()
                    {
                        Parent = "AuctionYourMainGui" + "BackGround" + "Item" + i + "Bid",
                        Components =
                        {
                            new CuiTextComponent()
                            {
                                Text = $"{GetLots.Bid}{config.TugrickName}",
                                Align = TextAnchor.MiddleCenter
                            },
                            new CuiRectTransformComponent()
                            {

                                AnchorMin = "0 0",
                                AnchorMax = "1 1",

                            }
                        }
                    });
                    AuctionYourMainGui.Add(new CuiElement()
                    {
                        Parent = "AuctionYourMainGui" + "BackGround" + "Item" + i,
                        Name = "AuctionYourMainGui" + "BackGround" + "Item" + i + "Buyout",
                        Components =
                        {
                            new CuiImageComponent()
                            {
                                Color = "0.5 0.5 0.5 0.7",
                        Material = matterial
                            },
                            new CuiRectTransformComponent()
                            {

                                AnchorMin = "0 1",
                                AnchorMax = "0 1",
                                OffsetMin = $"470 -42",
                                OffsetMax = $"605 0"

                            }
                        }
                    });
                    AuctionYourMainGui.Add(new CuiElement()
                    {
                        Parent = "AuctionYourMainGui" + "BackGround" + "Item" + i + "Buyout",
                        Components =
                        {
                            new CuiTextComponent()
                            {
                                Text = $"{GetLots.Buyout}{config.TugrickName}",
                                Align = TextAnchor.MiddleCenter
                            },
                            new CuiRectTransformComponent()
                            {

                                AnchorMin = "0 0",
                                AnchorMax = "1 1",

                            }
                        }
                    });
                    AuctionYourMainGui.Add(new CuiElement()
                    {
                        Parent = "AuctionYourMainGui" + "BackGround" + "Item" + i,
                        Name = "AuctionYourMainGui" + "BackGround" + "Item" + i + "Action",
                        Components =
                        {
                            new CuiImageComponent()
                            {
                                Color = "0.5 0.5 0.5 0.7",
                        Material = matterial
                            },
                            new CuiRectTransformComponent()
                            {

                                AnchorMin = "0 1",
                                AnchorMax = "0 1",
                                OffsetMin = $"610 -42",
                                OffsetMax = $"776.015 0"

                            }
                        }
                    });
                    AuctionYourMainGui.Add(new CuiButton()
                    {
                        Button =
                        {
                            Color = "0.6 0.6 0.6 0.9",
                            Material = matterial,
                            Command = $"DelLot {GetLots.LotId} 1 {page}",
                        },
                        Text =
                        {
                            Text = "Снять лот",
                            Align = TextAnchor.MiddleCenter,
                        },
                        RectTransform =
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = "39.9 8.140001",
                            OffsetMax = "134.9 31.94"
                        }
                    }, "AuctionYourMainGui" + "BackGround" + "Item" + i + "Action");
                }
                StartOffsetMin += (SizeY + Offset);
                StartOffsetMax += (SizeY + Offset);
                StartCount++;
                if (StartCount == DrawCount)
                {
                    AuctionYourMainGui.Add(new CuiButton()
                    {
                        Button =
                        {
                            Color = "0.5 0.5 0.5 0.9",
                            Command = $"InventoryPage Next {page}",
                        },
                        Text =
                        {
                            Text = "Далее>",
                            Align = TextAnchor.MiddleCenter,
                        },
                        RectTransform =
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = "405.275 3.875015",
                            OffsetMax = "500.325 25.52501"
                        }
                    }, "AuctionYourMainGui" + "BackGround");
                }
                if (page > 0)
                {
                    AuctionYourMainGui.Add(new CuiButton()
                    {
                        Button =
                        {
                            Color = "0.5 0.5 0.5 0.9",
                            Command = $"InventoryPage Back {page}",
                        },
                        Text =
                        {
                            Text = "<Назад",
                            Align = TextAnchor.MiddleCenter,
                        },
                        RectTransform =
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = "292.975 3.875015",
                            OffsetMax = "388.025 25.52501"
                        }
                    }, "AuctionYourMainGui" + "BackGround");
                }
                i++;
            }
            #endregion
            CuiHelper.AddUi(player, AuctionYourMainGui);
            if (config.UseGameStores == true) DrawBalance(player, "AuctionYourMainGui" + "BackGround");
        }
        void DrawInvenoty(BasePlayer player, int page = 0)
        {
            CuiHelper.DestroyUi(player, "AuctionInvGui");
            var AuctionInvGui = new CuiElementContainer();
            AuctionInvGui.Add(new CuiPanel
            {
                Image = { Color = "0.2 0.2 0.2 0.8",
                        Material = matterial },
                CursorEnabled = true,

                RectTransform = { AnchorMax = "1 1", AnchorMin = "0 0" }
            }, "Overlay", "AuctionInvGui");
            AuctionInvGui.Add(new CuiButton
            {
                Button = { Close = "AuctionInvGui", Color = "0 0 0 0" },
                Text = { Text = "" },
                RectTransform = { AnchorMax = "1 1", AnchorMin = "0 0" }
            }, "AuctionInvGui");
            AuctionInvGui.Add(new CuiElement()
            {
                Parent = "AuctionInvGui",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "Аукцион",
                        Align = TextAnchor.MiddleCenter,
                        FontSize= 35
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-336.85 241.6",
                        OffsetMax = "336.85 326.4"

                    }
                }
            });
            AuctionInvGui.Add(new CuiElement()
            {
                Parent = "AuctionInvGui",
                Name = "AuctionInvGui" + "BackGround",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.3 0.3 0.3 0.7",
                        Material = matterial
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-395.5 -263.36",
                        OffsetMax = "395.5 231.8"

                    }
                }
            });
            AuctionInvGui.Add(new CuiElement()
            {
                Parent = "AuctionInvGui" + "BackGround",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "Инвентарь",
                        Align = TextAnchor.UpperCenter,
                        FontSize = 22
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "0 -40",
                        OffsetMax = "800 0"
                    }
                }
            });
            AuctionInvGui.Add(new CuiElement()
            {
                Parent = "AuctionInvGui" + "BackGround",
                Name = "AuctionInvGui" + "BackGround" + "Balance",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.5 0.5 0.5 0.9",
                        Material = matterial
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 0",
                        AnchorMax = "0 0",
                        OffsetMin = "4.57 3.875",
                        OffsetMax = "141.38 25.525"

                    }
                }
            });
            if (config.UseEconomics)
            {
                double GetBalance = (double)Economics?.Call("Balance", player.UserIDString);

                AuctionInvGui.Add(new CuiElement()
                {
                    Parent = "AuctionInvGui" + "BackGround" + "Balance",
                    Components =
                    {
                        new CuiTextComponent()
                        {
                            Text = $"Баланс: {Convert.ToInt32(GetBalance)}{config.TugrickName}",
                            Align = TextAnchor.MiddleCenter,
                        },
                        new CuiRectTransformComponent()
                        {

                            AnchorMin = "0 0",
                            AnchorMax = "1 1",

                        }
                    }
                });
            }
            if (config.UseServerRwards)
            {
                int GetBalance = (int)ServerRewards?.Call("CheckPoints", player.userID);

                AuctionInvGui.Add(new CuiElement()
                {
                    Parent = "AuctionInvGui" + "BackGround" + "Balance",
                    Components =
                    {
                        new CuiTextComponent()
                        {
                            Text = $"Баланс: {GetBalance}{config.TugrickName}",
                            Align = TextAnchor.MiddleCenter,
                        },
                        new CuiRectTransformComponent()
                        {

                            AnchorMin = "0 0",
                            AnchorMax = "1 1",

                        }
                    }
                });
            }
            AuctionInvGui.Add(new CuiButton()
            {
                Button =
                        {
                            Color = "0.5 0.5 0.5 0.9",
                            Command = "UI_BackToMainAuc AuctionInvGui",
                        Material = matterial
                        },
                Text =
                        {
                            Text = "Назад",
                            Align = TextAnchor.MiddleCenter,
                        },
                RectTransform =
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = "718.74 3.874997",
                            OffsetMax = "785.96 25.525"
                        }
            }, "AuctionInvGui" + "BackGround");
            AuctionInvGui.Add(new CuiButton()
            {
                Button =
                        {
                            Color = "0.5 0.5 0.5 0.9",
                           Command = $"OpenAddInv {page}",
                        Material = matterial
                        },
                Text =
                        {
                            Text = "Добавить",
                            Align = TextAnchor.MiddleCenter,
                        },
                RectTransform =
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = "617.675 3.874997",
                            OffsetMax = "712.725 25.525"
                        }
            }, "AuctionInvGui" + "BackGround");

            #region Generate

            int MaxY = 5;
            int Max = 10;
            int CountX = 0;
            int CountY = 0;
            int StartMin1 = 50;
            int StartMin2 = 205;
            int StartMax1 = 150;
            int StartMax2 = 75;
            int Offset = 50;
            int Size = 100;
            int i = 0;
            List<ItemInv> ItemList = DataBase.PlayerDatabase[player.userID].Inventory;


            foreach (var Inventory in ItemList.Skip(page * Max).Take(Max))
            {

                if (i < Max)
                {
                    AuctionInvGui.Add(new CuiElement()
                    {
                        Parent = "AuctionInvGui" + "BackGround",
                        Name = "AuctionInvGui" + "BackGround" + "Item" + i,
                        Components =
                        {
                            new CuiImageComponent()
                            {
                                Color = "0.3 0.3 0.3 0.9",
                        Material = matterial
                            },
                            new CuiRectTransformComponent()
                            {
                                AnchorMin = "0 1",
                                AnchorMax = "0 1",
                                OffsetMin = $"{StartMin1} -{StartMin2}",
                                OffsetMax = $"{StartMax1} -{StartMax2}"

                            }
                        }
                    });
                    AuctionInvGui.Add(new CuiElement()
                    {
                        Parent = "AuctionInvGui" + "BackGround" + "Item" + i,
                        Components =
                        {
                            new CuiRawImageComponent()
                            {
                                Png = (string)ImageLibrary?.Call("GetImage",Inventory.ShortName)
                            },
                            new CuiRectTransformComponent()
                            {
                                AnchorMin = "0 1",
                                AnchorMax = "0 1",
                                OffsetMin = "15 -73.7",
                                OffsetMax = "85 -3.699997"

                            }
                        }
                    });
                    AuctionInvGui.Add(new CuiElement()
                    {
                        Parent = "AuctionInvGui" + "BackGround" + "Item" + i,
                        Components =
                        {
                            new CuiTextComponent()
                            {
                                Text = $"{Inventory.Ammount}x",
                                Align = TextAnchor.LowerRight
                            },
                            new CuiRectTransformComponent()
                            {
                                AnchorMin = "0 1",
                                AnchorMax = "0 1",
                                OffsetMin = "15 -73.7",
                                OffsetMax = "85 -3.699997"

                            }
                        }
                    });
                    AuctionInvGui.Add(new CuiButton()
                    {
                        Button =
                         {
                             Color = "0.5 0.5 0.5 0.9",
                             Command = $"OpenPublish {Inventory.ItemId} {Inventory.Skin} {Inventory.Ammount} {Inventory.ShortName} {page}",
                        Material = matterial
                         },
                        Text =
                         {
                             Text = "Выставить",
                             Align = TextAnchor.MiddleCenter,
                             FontSize = 16
                         },
                        RectTransform =
                         {
                             AnchorMin = "0 1",
                             AnchorMax = "0 1",
                             OffsetMin = "7.670002 -102.83",
                             OffsetMax = "92.33 -81.17"
                         }
                    }, "AuctionInvGui" + "BackGround" + "Item" + i);
                    AuctionInvGui.Add(new CuiButton()
                    {
                        Button =
                         {
                             Color = "0.5 0.5 0.5 0.9",
                             Command = $"TakeFromInv {Inventory.ItemId} {Inventory.Ammount} {Inventory.Skin} {page}",
                        Material = matterial
                         },
                        Text =
                         {
                             Text = "Забрать",
                             Align = TextAnchor.MiddleCenter,
                             FontSize = 16
                         },
                        RectTransform =
                         {
                             AnchorMin = "0 1",
                             AnchorMax = "0 1",
                             OffsetMin = "7.669998 -126.33",
                             OffsetMax = "92.33 -104.67"
                         }
                    }, "AuctionInvGui" + "BackGround" + "Item" + i);

                    StartMax1 += (Size + Offset);
                    StartMin1 += (Size + Offset);
                    CountY++;
                    i++;
                }
                if (i == Max)
                {
                    AuctionInvGui.Add(new CuiButton()
                    {
                        Button =
                        {
                            Color = "0.5 0.5 0.5 0.9",
                            Command = $"InventoryPage Next {page}",
                        Material = matterial
                        },
                        Text =
                        {
                            Text = "Далее>",
                            Align = TextAnchor.MiddleCenter,
                        },
                        RectTransform =
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = "405.275 3.875015",
                            OffsetMax = "500.325 25.52501"
                        }
                    }, "AuctionInvGui" + "BackGround");
                }
                if (page > 0)
                {
                    AuctionInvGui.Add(new CuiButton()
                    {
                        Button =
                        {
                            Color = "0.5 0.5 0.5 0.9",
                            Command = $"InventoryPage Back {page}",
                        Material = matterial
                        },
                        Text =
                        {
                            Text = "<Назад",
                            Align = TextAnchor.MiddleCenter,
                        },
                        RectTransform =
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = "292.975 3.875015",
                            OffsetMax = "388.025 25.52501"
                        }
                    }, "AuctionInvGui" + "BackGround");
                }
                if (CountY == MaxY)
                {
                    StartMin2 += (Size + (Offset * 2));
                    StartMax2 += (Size + (Offset * 2));
                    StartMax1 -= (Size + Offset) * MaxY;
                    StartMin1 -= (Size + Offset) * MaxY;
                    CountX++;
                    CountY = 0;
                }
            }
            CuiHelper.AddUi(player, AuctionInvGui);
            if (config.UseGameStores) DrawBalance(player, "AuctionInvGui" + "BackGround");
            #endregion
        }
        void DrawAddInInvetory(BasePlayer player, int page)
        {
            CuiHelper.DestroyUi(player, "AuctionAddInvGui");
            Dictionary<int, Item> ItemList = new Dictionary<int, Item>();
            foreach (var Find in player.inventory.containerMain.itemList)
            {
                ItemList.Add(Find.position, Find);
            }
            foreach (var Find in player.inventory.containerBelt.itemList)
            {
                ItemList.Add(Find.position + 24, Find);
            }
            var AuctionAddInvGui = new CuiElementContainer();
            AuctionAddInvGui.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0.4",
                        Material = "assets/content/ui/uibackgroundblur.mat" },
                CursorEnabled = true,

                RectTransform = { AnchorMax = "1 1", AnchorMin = "0 0" }
            }, "AuctionInvGui" + "BackGround", "AuctionAddInvGui");
            AuctionAddInvGui.Add(new CuiButton
            {
                Button = { Close = "AuctionAddInvGui", Color = "0 0 0 0" },
                Text = { Text = "" },
                RectTransform = { AnchorMax = "1 1", AnchorMin = "0 0" }
            }, "AuctionAddInvGui");
            AuctionAddInvGui.Add(new CuiElement()
            {
                Parent = "AuctionAddInvGui",
                Name = "AuctionAddInvGui" + "BackGround",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.3 0.3 0.3 0.9",
                        Material = matterial
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-200 -179.995",
                        OffsetMax = "200 166.095"

                    }
                }
            });
            int MaxY = 6;
            int CountX = 0;
            int CountY = 0;
            int StartMin1 = 25;
            int StartMin2 = 75;
            int StartMax1 = 75;
            int StartMax2 = 25;
            int Offset = 10;
            int Size = 50;

            for (int i = 0; i < 30; i++)
            {

                AuctionAddInvGui.Add(new CuiElement()
                {
                    Parent = "AuctionAddInvGui" + "BackGround",
                    Name = "AuctionAddInvGui" + "BackGround" + "Item" + i,
                    Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.4 0.4 0.4 0.9",
                        Material = matterial
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = $"{StartMin1} -{StartMin2}",
                        OffsetMax = $"{StartMax1} -{StartMax2}"

                    }
                }
                });
                if (ItemList.ContainsKey(i))
                {
                    AuctionAddInvGui.Add(new CuiElement()
                    {
                        Parent = "AuctionAddInvGui" + "BackGround" + "Item" + i,
                        Components =
                    {
                        new CuiRawImageComponent()
                        {
                            Png = (string)ImageLibrary?.Call("GetImage", ItemList[i].info.shortname)
                        },
                        new CuiRectTransformComponent()
                        {

                            AnchorMin = "0 0",
                            AnchorMax = "1 1",


                        }
                    }
                    });

                    AuctionAddInvGui.Add(new CuiButton()
                    {
                        Button =
                            {
                                Color = "0 0 0 0",
                                Command = $"OpenCountMenu {ItemList[i].info.shortname} {ItemList[i].amount} {ItemList[i].skin} {page}",
                                Material = matterial
                            },
                        Text =
                        {
                            Text = $"{ItemList[i].amount}x",
                            Align = TextAnchor.LowerRight,
                        },
                        RectTransform =
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "0.95 1",
                            }
                    }, "AuctionAddInvGui" + "BackGround" + "Item" + i);
                }
                StartMax1 += (Size + Offset);
                StartMin1 += (Size + Offset);
                CountY++;
                if (CountY == MaxY)
                {
                    StartMin2 += (Size + Offset);
                    StartMax2 += (Size + Offset);
                    StartMax1 -= (Size + Offset) * MaxY;
                    StartMin1 -= (Size + Offset) * MaxY;
                    CountX++;
                    CountY = 0;
                }

            }
            AuctionAddInvGui.Add(new CuiButton()
            {
                Button =
                 {
                     Color = "0.7 0.7 0.7 0.9",
                     Close = "AuctionAddInvGui",
                        Material = matterial
                 },
                Text =
                 {
                     Text = "X",
                     Align = TextAnchor.MiddleCenter,
                     FontSize = 19
                 },
                RectTransform =
                 {
                     AnchorMin = "0 1",
                     AnchorMax = "0 1",
                     OffsetMin = "376.4 -24.26",
                     OffsetMax = "398.4 -2.339997"
                 }
            }, "AuctionAddInvGui" + "BackGround");
            CuiHelper.AddUi(player, AuctionAddInvGui);
        }
        void DrawSelectCountItem(BasePlayer player, string shortname, int ammount, ulong skin, int page)
        {

            CuiHelper.DestroyUi(player, "AuctionAddInvGui" + "Count");

            var AuctionAddInvGui = new CuiElementContainer();
            AuctionAddInvGui.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0.4",
                          Material = matterial },
                CursorEnabled = true,

                RectTransform = { AnchorMax = "1 1", AnchorMin = "0 0" }
            }, "AuctionAddInvGui" + "BackGround", "AuctionAddInvGui" + "Count");
            AuctionAddInvGui.Add(new CuiElement()
            {
                Parent = "AuctionAddInvGui" + "Count",
                Name = "AuctionAddInvGui" + "Count" + "BackGround",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.4 0.4 0.4 0.7",
                        Material = matterial
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "79.555 -242.535",
                        OffsetMax = "320.445 -103.465"

                    }
                }
            });
            AuctionAddInvGui.Add(new CuiElement()
            {
                Parent = "AuctionAddInvGui" + "Count" + "BackGround",
                Name = "AuctionAddInvGui" + "Count" + "BackGround" + "Img",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.6 0.6 0.6 0.7",
                        Material = matterial
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "12.5 -104.535",
                        OffsetMax = "82.5 -34.535"

                    }
                }
            });
            AuctionAddInvGui.Add(new CuiElement()
            {
                Parent = "AuctionAddInvGui" + "Count" + "BackGround" + "Img",
                Components =
                {
                    new CuiRawImageComponent()
                    {
                       Png = (string)ImageLibrary?.Call("GetImage", shortname)
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 0",
                        AnchorMax = "1 1",

                    }
                }
            });
            AuctionAddInvGui.Add(new CuiElement()
            {
                Parent = "AuctionAddInvGui" + "Count" + "BackGround",
                Name = "AuctionAddInvGui" + "Count" + "BackGround" + "Text",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.6 0.6 0.6 0.7",
                        Material = matterial
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "44.355 -29.36",
                        OffsetMax = "206.045 -7.44"

                    }
                }
            });
            AuctionAddInvGui.Add(new CuiElement()
            {
                Parent = "AuctionAddInvGui" + "Count" + "BackGround" + "Text",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "Добавить предмет",
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",

                    }
                }
            });
            AuctionAddInvGui.Add(new CuiElement()
            {
                Parent = "AuctionAddInvGui" + "Count" + "BackGround",
                Name = "AuctionAddInvGui" + "Count" + "BackGround" + "AmountText",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.6 0.6 0.6 0.7",
                        Material = matterial
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "84.78503 -78.8",
                        OffsetMax = "150.015 -58.8"

                    }
                }
            });
            AuctionAddInvGui.Add(new CuiElement()
            {
                Parent = "AuctionAddInvGui" + "Count" + "BackGround" + "AmountText",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "Количество:",
                        Align = TextAnchor.MiddleLeft,
                        FontSize = 12
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",


                    }
                }
            });
            AuctionAddInvGui.Add(new CuiElement()
            {
                Parent = "AuctionAddInvGui" + "Count" + "BackGround",
                Name = "AuctionAddInvGui" + "Count" + "BackGround" + "AmountInput",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.6 0.6 0.6 0.7",
                        Material = matterial
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "176.29 -78.8",
                        OffsetMax = "219.11 -58.8"

                    }
                }
            });
            AuctionAddInvGui.Add(new CuiElement()
            {
                Parent = "AuctionAddInvGui" + "Count" + "BackGround" + "AmountInput",
                Name = "AuctionAddInvGui" + "Count" + "BackGround" + "AmountInput" + "Text",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = ammount.ToString(),
                        FontSize = 13,
                        Align = TextAnchor.MiddleCenter

                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",

                    }
                }
            });
            AuctionAddInvGui.Add(new CuiButton()
            {
                Button =
                 {
                     Color = "0.7 0.7 0.7 0.9",
                     Command = $"UpdateAddInv minus {ammount} {shortname}  {skin} {page}",
                        Material = matterial
                 },
                Text =
                 {
                     Text = "-",
                     Align = TextAnchor.MiddleCenter,
                     FontSize = 16
                 },
                RectTransform =
                 {
                     AnchorMin = "0 1",
                     AnchorMax = "0 1",
                     OffsetMin = "156.29 -78.8",
                     OffsetMax = "176.29 -58.8"
                 }
            }, "AuctionAddInvGui" + "Count" + "BackGround");

            AuctionAddInvGui.Add(new CuiButton()
            {
                Button =
                 {
                     Color = "0.7 0.7 0.7 0.9",
                     Command = $"UpdateAddInv plus {ammount} {shortname}  {skin} {page}",
                        Material = matterial
                 },
                Text =
                 {
                     Text = "+",
                     Align = TextAnchor.MiddleCenter,
                     FontSize = 16
                 },
                RectTransform =
                 {
                     AnchorMin = "0 1",
                     AnchorMax = "0 1",
                     OffsetMin = "219.11 -78.8",
                     OffsetMax = "239.11 -58.8"
                 }
            }, "AuctionAddInvGui" + "Count" + "BackGround");
            AuctionAddInvGui.Add(new CuiButton()
            {
                Button =
                 {
                     Color = "0.7 0.7 0.7 0.9",
                     Command = $"AddInInventory {shortname} {ammount} {skin} {page}",
                        Material = matterial
                 },
                Text =
                 {
                     Text = "Добавить",
                     Align = TextAnchor.MiddleCenter,
                 },
                RectTransform =
                 {
                     AnchorMin = "0 1",
                     AnchorMax = "0 1",
                     OffsetMin = "101.73 -124.545",
                     OffsetMax = "200.85 -101.855"
                 }
            }, "AuctionAddInvGui" + "Count" + "BackGround");

            AuctionAddInvGui.Add(new CuiButton()
            {
                Button =
                 {
                     Color = "0.7 0.7 0.7 0.9",
                     Close = "AuctionAddInvGui" + "Count",
                        Material = matterial
                 },
                Text =
                 {
                     Text = "X",
                     Align = TextAnchor.MiddleCenter,
                     FontSize = 19
                 },
                RectTransform =
                 {
                     AnchorMin = "0 1",
                     AnchorMax = "0 1",
                     OffsetMin = "212.3 -29.35999",
                     OffsetMax = "234.3 -7.439986"
                 }
            }, "AuctionAddInvGui" + "Count" + "BackGround");
            CuiHelper.AddUi(player, AuctionAddInvGui);
        }
        void DrawBit(BasePlayer player, int LotId, int Bit = 0, int Buyot = 0, int page = 0, bool updated = false)
        {

            CuiHelper.DestroyUi(player, "AuctionMainGui" + "Count");
            AuctionLot GetLot = DataBase.Lots.Find(x => x.LotId == LotId);
            if (Bit == 0) Bit = GetLot.Bid;

            if (Buyot == 0) Buyot = GetLot.Buyout;

            var AuctionMainGui = new CuiElementContainer();
            AuctionMainGui.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0.4",
                        Material = "assets/content/ui/uibackgroundblur.mat" },
                CursorEnabled = true,

                RectTransform = { AnchorMax = "1 1", AnchorMin = "0 0" }
            }, "AuctionMainGui" + "BackGround", "AuctionMainGui" + "Count");
            AuctionMainGui.Add(new CuiElement()
            {
                Parent = "AuctionMainGui" + "Count",
                Name = "AuctionMainGui" + "Count" + "BackGround",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.5 0.5 0.5 0.7"
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "251.7 -300",
                        OffsetMax = "500 -160.925"

                    }
                }
            });
            AuctionMainGui.Add(new CuiElement()
            {
                Parent = "AuctionMainGui" + "Count" + "BackGround",
                Name = "AuctionMainGui" + "Count" + "BackGround" + "Img",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.5 0.5 0.5 0.7"
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "12.5 -104.535",
                        OffsetMax = "82.5 -34.535"

                    }
                }
            });
            AuctionMainGui.Add(new CuiElement()
            {
                Parent = "AuctionMainGui" + "Count" + "BackGround" + "Img",
                Components =
                {
                    new CuiRawImageComponent()
                    {
                       Png = (string)ImageLibrary?.Call("GetImage", GetLot.ShortName)
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 0",
                        AnchorMax = "1 1",

                    }
                }
            });
            AuctionMainGui.Add(new CuiElement()
            {
                Parent = "AuctionMainGui" + "Count" + "BackGround",
                Name = "AuctionMainGui" + "Count" + "BackGround" + "Text",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.5 0.5 0.5 0.7"
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "44.355 -29.36",
                        OffsetMax = "206.045 -7.44"

                    }
                }
            });
            AuctionMainGui.Add(new CuiElement()
            {
                Parent = "AuctionMainGui" + "Count" + "BackGround" + "Text",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "Увеличить ставку",
                        Align = TextAnchor.MiddleCenter,

                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",

                    }
                }
            });
            AuctionMainGui.Add(new CuiElement()
            {
                Parent = "AuctionMainGui" + "Count" + "BackGround",
                Name = "AuctionMainGui" + "Count" + "BackGround" + "AmountText",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.5 0.5 0.5 0.7"
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "84.78503 -78.8",
                        OffsetMax = "150.015 -58.8"

                    }
                }
            });
            AuctionMainGui.Add(new CuiElement()
            {
                Parent = "AuctionMainGui" + "Count" + "BackGround" + "AmountText",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "Ваша ставка:",
                        Align = TextAnchor.MiddleLeft,
                        FontSize = 12
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",


                    }
                }
            });
            AuctionMainGui.Add(new CuiElement()
            {
                Parent = "AuctionMainGui" + "Count" + "BackGround",
                Name = "AuctionMainGui" + "Count" + "BackGround" + "AmountInput",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.5 0.5 0.5 0.7"
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "176.29 -78.8",
                        OffsetMax = "219.11 -58.8"

                    }
                }
            });
            AuctionMainGui.Add(new CuiElement()
            {
                Parent = "AuctionMainGui" + "Count" + "BackGround" + "AmountInput",
                Name = "AuctionMainGui" + "Count" + "BackGround" + "AmountInput" + "Text",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = $"{Bit}{config.TugrickName}",
                        FontSize = 13,
                        Align = TextAnchor.MiddleCenter

                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",

                    }
                }
            });
            AuctionMainGui.Add(new CuiButton()
            {
                Button =
                 {
                     Color = "0.5 0.5 0.5 0.9",
                     Command = $"UpdateBit plus {LotId} {Bit} {Buyot} {page}"
                 },
                Text =
                 {
                     Text = "+",
                     Align = TextAnchor.MiddleCenter,
                     FontSize = 16
                 },
                RectTransform =
                 {
                     AnchorMin = "0 1",
                     AnchorMax = "0 1",
                     OffsetMin = "219.11 -78.8",
                     OffsetMax = "239.11 -58.8"
                 }
            }, "AuctionMainGui" + "Count" + "BackGround");
            if (updated)
            {
                AuctionMainGui.Add(new CuiButton()
                {
                    Button =
                     {
                         Color = "0.5 0.5 0.5 0.9",
                         Command = $"SetBit {LotId} {Bit} {Buyot} {page}",
                     },
                    Text =
                     {
                         Text = "Увеличить ставку",
                         Align = TextAnchor.MiddleCenter,
                     },
                    RectTransform =
                     {
                         AnchorMin = "0 1",
                         AnchorMax = "0 1",
                         OffsetMin = "101.73 -124.545",
                         OffsetMax = "220 -101.855"
                     }
                }, "AuctionMainGui" + "Count" + "BackGround");
            }
            AuctionMainGui.Add(new CuiButton()
            {
                Button =
                 {
                     Color = "0.5 0.5 0.5 0.9",
                     Close = "AuctionMainGui" + "Count",
                 },
                Text =
                 {
                     Text = "X",
                     Align = TextAnchor.MiddleCenter,
                     FontSize = 19
                 },
                RectTransform =
                 {
                     AnchorMin = "0 1",
                     AnchorMax = "0 1",
                     OffsetMin = "212.3 -29.35999",
                     OffsetMax = "234.3 -7.439986"
                 }
            }, "AuctionMainGui" + "Count" + "BackGround");
            CuiHelper.AddUi(player, AuctionMainGui);
        }
        void DrawPublicItem(BasePlayer player, int itemid, ulong skin, int ammount, string shortname, int page, int bit = 0, int buyout = 0)
        {
            CuiHelper.DestroyUi(player, "AuctionAddGui" + "Count");

            var AuctionAddGui = new CuiElementContainer();
            AuctionAddGui.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0.4",
                        Material = "assets/content/ui/uibackgroundblur.mat" },
                CursorEnabled = true,

                RectTransform = { AnchorMax = "1 1", AnchorMin = "0 0" }
            }, "AuctionInvGui" + "BackGround", "AuctionAddGui" + "Count");
            AuctionAddGui.Add(new CuiElement()
            {
                Parent = "AuctionAddGui" + "Count",
                Name = "AuctionAddGui" + "Count" + "BackGround",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.5 0.5 0.5 0.7"
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "251.7 -328.455",
                        OffsetMax = "539.3 -160.925"

                    }
                }
            });
            AuctionAddGui.Add(new CuiElement()
            {
                Parent = "AuctionAddGui" + "Count" + "BackGround",
                Name = "AuctionAddGui" + "Count" + "BackGround" + "Img",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.5 0.5 0.5 0.7"
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "29.81 -117.335",
                        OffsetMax = "99.81 -47.33499"

                    }
                }
            });
            AuctionAddGui.Add(new CuiElement()
            {
                Parent = "AuctionAddGui" + "Count" + "BackGround" + "Img",
                Components =
                {
                    new CuiRawImageComponent()
                    {
                       Png = (string)ImageLibrary?.Call("GetImage",shortname)
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 0",
                        AnchorMax = "1 1",

                    }
                }
            });
            AuctionAddGui.Add(new CuiElement()
            {
                Parent = "AuctionAddGui" + "Count" + "BackGround",
                Name = "AuctionAddGui" + "Count" + "BackGround" + "Text",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.5 0.5 0.5 0.7"
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "62.95497 -29.36",
                        OffsetMax = "224.645 -7.440001"

                    }
                }
            });
            AuctionAddGui.Add(new CuiElement()
            {
                Parent = "AuctionAddGui" + "Count" + "BackGround" + "Text",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "Выставить предмет",
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",

                    }
                }
            });
            #region Ammount
            AuctionAddGui.Add(new CuiElement()
            {
                Parent = "AuctionAddGui" + "Count" + "BackGround",
                Name = "AuctionAddGui" + "Count" + "BackGround" + "AmountText",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.5 0.5 0.5 0.7"
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "102.095 -72.09998",
                        OffsetMax = "167.325 -52.09998"

                    }
                }
            });
            AuctionAddGui.Add(new CuiElement()
            {
                Parent = "AuctionAddGui" + "Count" + "BackGround" + "AmountText",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "Количество:",
                        Align = TextAnchor.MiddleLeft,
                        FontSize = 12
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",


                    }
                }
            });
            AuctionAddGui.Add(new CuiElement()
            {
                Parent = "AuctionAddGui" + "Count" + "BackGround",
                Name = "AuctionAddGui" + "Count" + "BackGround" + "AmountInput",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.5 0.5 0.5 0.7"
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "193.6 -72.09999",
                        OffsetMax = "236.42 -52.09999"

                    }
                }
            });
            AuctionAddGui.Add(new CuiElement()
            {
                Parent = "AuctionAddGui" + "Count" + "BackGround" + "AmountInput",
                Name = "AuctionAddGui" + "Count" + "BackGround" + "AmountInput" + "Text",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = ammount.ToString(),
                        FontSize = 13,
                        Align = TextAnchor.MiddleCenter

                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",

                    }
                }
            });
            AuctionAddGui.Add(new CuiButton()
            {
                Button =
                 {
                     Color = "0.5 0.5 0.5 0.9",
                     Command = $"UpdatePublish Ammount minus {itemid} {skin} {ammount} {shortname} {page} {bit} {buyout}"
                 },
                Text =
                 {
                     Text = "-",
                     Align = TextAnchor.MiddleCenter,
                     FontSize = 16
                 },
                RectTransform =
                 {
                     AnchorMin = "0 1",
                     AnchorMax = "0 1",
                     OffsetMin = "173.6 -72.1",
                     OffsetMax = "193.6 -52.1"
                 }
            }, "AuctionAddGui" + "Count" + "BackGround");

            AuctionAddGui.Add(new CuiButton()
            {
                Button =
                 {
                     Color = "0.5 0.5 0.5 0.9",
                     Command = $"UpdatePublish Ammount plus {itemid} {skin} {ammount} {shortname} {page} {bit} {buyout}"
                 },
                Text =
                 {
                     Text = "+",
                     Align = TextAnchor.MiddleCenter,
                     FontSize = 16
                 },
                RectTransform =
                 {
                     AnchorMin = "0 1",
                     AnchorMax = "0 1",
                     OffsetMin = "236.42 -72.09999",
                     OffsetMax = "256.42 -52.09999"
                 }
            }, "AuctionAddGui" + "Count" + "BackGround");

            #endregion
            #region Bit
            AuctionAddGui.Add(new CuiElement()
            {
                Parent = "AuctionAddGui" + "Count" + "BackGround",
                Name = "AuctionAddGui" + "Count" + "BackGround" + "BitText",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.5 0.5 0.5 0.7"
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "102.0951 -93.09998",
                        OffsetMax = "167.3251 -73.09998"

                    }
                }
            });
            AuctionAddGui.Add(new CuiElement()
            {
                Parent = "AuctionAddGui" + "Count" + "BackGround" + "BitText",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "Ставка:",
                        Align = TextAnchor.MiddleLeft,
                        FontSize = 12
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",


                    }
                }
            });
            AuctionAddGui.Add(new CuiElement()
            {
                Parent = "AuctionAddGui" + "Count" + "BackGround",
                Name = "AuctionAddGui" + "Count" + "BackGround" + "BitInput",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.5 0.5 0.5 0.7"
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "193.6 -93.09998",
                        OffsetMax = "236.42 -73.09998"

                    }
                }
            });
            AuctionAddGui.Add(new CuiElement()
            {
                Parent = "AuctionAddGui" + "Count" + "BackGround" + "BitInput",
                Name = "AuctionAddGui" + "Count" + "BackGround" + "BitInput" + "Text",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = $"{bit}{config.TugrickName}",
                        FontSize = 13,
                        Align = TextAnchor.MiddleCenter

                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",

                    }
                }
            });
            AuctionAddGui.Add(new CuiButton()
            {
                Button =
                 {
                     Color = "0.5 0.5 0.5 0.9",
                     Command = $"UpdatePublish Bit minus {itemid} {skin} {ammount} {shortname} {page} {bit} {buyout}"
                 },
                Text =
                 {
                     Text = "-",
                     Align = TextAnchor.MiddleCenter,
                     FontSize = 16
                 },
                RectTransform =
                 {
                     AnchorMin = "0 1",
                     AnchorMax = "0 1",
                     OffsetMin = "173.6 -93.09998",
                     OffsetMax = "193.6 -73.09998"
                 }
            }, "AuctionAddGui" + "Count" + "BackGround");

            AuctionAddGui.Add(new CuiButton()
            {
                Button =
                 {
                     Color = "0.5 0.5 0.5 0.9",
                     Command = $"UpdatePublish Bit plus {itemid} {skin} {ammount} {shortname} {page} {bit} {buyout}"
                 },
                Text =
                 {
                     Text = "+",
                     Align = TextAnchor.MiddleCenter,
                     FontSize = 16
                 },
                RectTransform =
                 {
                     AnchorMin = "0 1",
                     AnchorMax = "0 1",
                     OffsetMin = "236.42 -93.09998",
                     OffsetMax = "256.42 -73.09998"
                 }
            }, "AuctionAddGui" + "Count" + "BackGround");
            #endregion
            #region Buyout
            AuctionAddGui.Add(new CuiElement()
            {
                Parent = "AuctionAddGui" + "Count" + "BackGround",
                Name = "AuctionAddGui" + "Count" + "BackGround" + "BuyoutText",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.5 0.5 0.5 0.7"
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "102.0951 -113.4",
                        OffsetMax = "167.3251 -93.39996"

                    }
                }
            });
            AuctionAddGui.Add(new CuiElement()
            {
                Parent = "AuctionAddGui" + "Count" + "BackGround" + "BuyoutText",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "Выкуп:",
                        Align = TextAnchor.MiddleLeft,
                        FontSize = 12
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",


                    }
                }
            });
            AuctionAddGui.Add(new CuiElement()
            {
                Parent = "AuctionAddGui" + "Count" + "BackGround",
                Name = "AuctionAddGui" + "Count" + "BackGround" + "BuyoutInput",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.5 0.5 0.5 0.7"
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "193.6 -113.4",
                        OffsetMax = "236.42 -93.40001"

                    }
                }
            });
            AuctionAddGui.Add(new CuiElement()
            {
                Parent = "AuctionAddGui" + "Count" + "BackGround" + "BuyoutInput",
                Name = "AuctionAddGui" + "Count" + "BackGround" + "BuyoutInput" + "Text",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = $"{buyout}{config.TugrickName}",
                        FontSize = 13,
                        Align = TextAnchor.MiddleCenter

                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",

                    }
                }
            });
            AuctionAddGui.Add(new CuiButton()
            {
                Button =
                 {
                     Color = "0.5 0.5 0.5 0.9",
                     Command = $"UpdatePublish Buyout minus {itemid} {skin} {ammount} {shortname} {page} {bit} {buyout}"
                 },
                Text =
                 {
                     Text = "-",
                     Align = TextAnchor.MiddleCenter,
                     FontSize = 16
                 },
                RectTransform =
                 {
                     AnchorMin = "0 1",
                     AnchorMax = "0 1",
                     OffsetMin = "173.6 -113.4",
                     OffsetMax = "193.6 -93.39996"
                 }
            }, "AuctionAddGui" + "Count" + "BackGround");

            AuctionAddGui.Add(new CuiButton()
            {
                Button =
                 {
                     Color = "0.5 0.5 0.5 0.9",
                     Command = $"UpdatePublish Buyout plus {itemid} {skin} {ammount} {shortname} {page} {bit} {buyout}"
                 },
                Text =
                 {
                     Text = "+",
                     Align = TextAnchor.MiddleCenter,
                     FontSize = 16
                 },
                RectTransform =
                 {
                     AnchorMin = "0 1",
                     AnchorMax = "0 1",
                     OffsetMin = "236.42 -113.4",
                     OffsetMax = "256.42 -93.39996"
                 }
            }, "AuctionAddGui" + "Count" + "BackGround");
            #endregion
            AuctionAddGui.Add(new CuiButton()
            {
                Button =
                 {
                     Color = "0.5 0.5 0.5 0.9",
                     Command = $"AddAuction {itemid} {skin} {ammount} {shortname} {page} {bit} {buyout}",
                 },
                Text =
                 {
                     Text = "Добавить",
                     Align = TextAnchor.MiddleCenter,
                 },
                RectTransform =
                 {
                     AnchorMin = "0 1",
                     AnchorMax = "0 1",
                     OffsetMin = "106.93 -156.345",
                     OffsetMax = "206.05 -133.655"
                 }
            }, "AuctionAddGui" + "Count" + "BackGround");

            AuctionAddGui.Add(new CuiButton()
            {
                Button =
                 {
                     Color = "0.5 0.5 0.5 0.9",
                     Close = "AuctionAddGui" + "Count",
                 },
                Text =
                 {
                     Text = "X",
                     Align = TextAnchor.MiddleCenter,
                     FontSize = 19
                 },
                RectTransform =
                 {
                     AnchorMin = "0 1",
                     AnchorMax = "0 1",
                     OffsetMin = "261.2 -29.35999",
                     OffsetMax = "283.2 -7.439986"
                 }
            }, "AuctionAddGui" + "Count" + "BackGround");
            CuiHelper.AddUi(player, AuctionAddGui);
        }
        void DrawConfirm(BasePlayer player, string FromGui, string Parent, int LotId)
        {
            AuctionLot Getlot = new AuctionLot();
            if (LotId != 0)
                Getlot = DataBase.Lots.Find(x => x.LotId == LotId);
            var AuctionConfirmGui = new CuiElementContainer();
            AuctionConfirmGui.Add(new CuiElement()
            {
                Parent = Parent,
                Name = "AuctionConfirmGui",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.2 0.2 0.2 0.8",
                        Material = matterial
                    },
                    new CuiNeedsCursorComponent()
                    {

                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 0",
                        AnchorMax = "1 1",


                    }
                }
            });
            AuctionConfirmGui.Add(new CuiButton
            {
                Button = { Close = "AuctionConfirmGui", Color = "0 0 0 0" },
                Text = { Text = "" },
                RectTransform = { AnchorMax = "1 1", AnchorMin = "0 0" }
            }, "AuctionConfirmGui");
            AuctionConfirmGui.Add(new CuiElement()
            {
                Parent = "AuctionConfirmGui",
                Name = "AuctionConfirmGui" + "BackGround",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.3 0.3 0.3 0.7",
                        Material = matterial
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "271.35 -277",
                        OffsetMax = "519.65 -183"

                    }
                }
            });
            AuctionConfirmGui.Add(new CuiElement()
            {
                Parent = "AuctionConfirmGui" + "BackGround",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "Вы уверены ?",
                        Align = TextAnchor.MiddleCenter,
                        FontSize = 20
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "32.37496 -52.1",
                        OffsetMax = "224.645 -7.439999"

                    }
                }
            });
            AuctionConfirmGui.Add(new CuiButton()
            {
                Button =
                 {
                     Color = "0.5 0.5 0.5 0.9",
                     Material = matterial,
                     Close = "AuctionConfirmGui"
                 },
                Text =
                 {
                     Text = "Нет",
                     Align = TextAnchor.MiddleCenter,
                     FontSize = 16
                 },
                RectTransform =
                 {
                     AnchorMin = "0 1",
                     AnchorMax = "0 1",
                     OffsetMin = "47.88 -81.245",
                     OffsetMax = "117.88 -58.555"
                 }
            }, "AuctionConfirmGui" + "BackGround");
            if (FromGui == "AuctionMin")
            {
                AuctionConfirmGui.Add(new CuiButton()
                {
                    Button =
                 {
                     Color = "0.5 0.5 0.5 0.9",
                     Material = matterial,
                     Command = $"UI_Buyout {Getlot.LotId}"
                 },
                    Text =
                 {
                     Text = "Да",
                     Align = TextAnchor.MiddleCenter,
                     FontSize = 16
                 },
                    RectTransform =
                 {
                     AnchorMin = "0 1",
                     AnchorMax = "0 1",
                     OffsetMin = "136.55 -81.245",
                     OffsetMax = "206.55 -58.555"
                 }
                }, "AuctionConfirmGui" + "BackGround");
            }
            CuiHelper.AddUi(player, AuctionConfirmGui);
        }
        void DrawMessage(BasePlayer player, string Parent, string Text)
        {
            var AuctionMessageGui = new CuiElementContainer();
            AuctionMessageGui.Add(new CuiElement()
            {
                Parent = Parent,
                Name = "AuctionMessageGui",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.2 0.2 0.2 0.8",
                        Material = matterial
                    },
                    new CuiNeedsCursorComponent()
                    {

                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 0",
                        AnchorMax = "1 1",


                    }
                }
            });
            AuctionMessageGui.Add(new CuiButton
            {
                Button = { Close = "AuctionMessageGui", Color = "0 0 0 0" },
                Text = { Text = "" },
                RectTransform = { AnchorMax = "1 1", AnchorMin = "0 0" }
            }, "AuctionMessageGui");
            AuctionMessageGui.Add(new CuiElement()
            {
                Parent = "AuctionMessageGui",
                Name = "AuctionMessageGui" + "BackGround",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color = "0.3 0.3 0.3 0.7",
                        Material = matterial
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "271.35 -277",
                        OffsetMax = "519.65 -183"

                    }
                }
            });
            AuctionMessageGui.Add(new CuiElement()
            {
                Parent = "AuctionMessageGui" + "BackGround",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = Text,
                        Align = TextAnchor.MiddleCenter,
                        FontSize = 20
                    },
                    new CuiRectTransformComponent()
                    {

                        AnchorMin = "0 1",
                        AnchorMax = "1 1",
                        OffsetMin = "0 -69.33",
                        OffsetMax = "0 -24.67"

                    }
                }
            });

            AuctionMessageGui.Add(new CuiButton()
            {
                Button =
                 {
                     Color = "0.5 0.5 0.5 0.9",
                     Close = "AuctionMessageGui",
                 },
                Text =
                 {
                     Text = "X",
                     Align = TextAnchor.MiddleCenter,
                     FontSize = 19
                 },
                RectTransform =
                 {
                     AnchorMin = "0 1",
                     AnchorMax = "0 1",
                     OffsetMin = "224.65 -23.76",
                     OffsetMax = "246.65 -1.84"
                 }
            }, "AuctionMessageGui" + "BackGround");

            CuiHelper.AddUi(player, AuctionMessageGui);
        }
        #endregion
        #region Function

        void GetBalanceGameStore(BasePlayer player, Action<int, string> callback)
        {
            string url = $"https://gamestores.ru/api?shop_id={config.GSId}&secret={config.GSApi}&action=balance&steam_id={player.UserIDString}";
            webrequest.Enqueue(url, null, (code, response) =>
            {
                if (code != 200)
                {
                    PrintError($"Ошибка соединения с сайтом GS!");
                }
                else
                {
                    JObject jObject = JObject.Parse(response);
                    if (jObject["result"].ToString() == "fail")
                    {
                        PrintError($"Ошибка получения баланса для {player.displayName}!");
                        PrintError($"Причина: {jObject["message"].ToString()}");
                    }
                    else
                    {
                        callback.Invoke(code, jObject["value"].ToString());
                    }
                }

            }, this);
        }
        void TakeGsMoney(ulong player, float count, Action<int, string> callback)
        {
            string status = null;
            string url = $"https://gamestores.ru/api?shop_id={config.GSId}&secret={config.GSApi}&action=moneys&type=minus&steam_id={player.ToString()}&amount={count}&mess=Оплата товара аукциона";
            webrequest.Enqueue(url, null, (code, response) =>
            {
                if (code != 200)
                {
                    PrintError($"Ошибка соединения с сайтом GS!");
                }
                else
                {
                    JObject jObject = JObject.Parse(response);
                    if (jObject["result"].ToString() == "fail")
                    {
                        PrintError($"Ошибка пополнения баланса для {player}!");
                        PrintError($"Причина: {jObject["message"].ToString()}");
                    }
                    else
                    {
                        callback.Invoke(code, jObject["result"].ToString());
                    }
                }

            }, this, RequestMethod.GET);

        }
        void GiveGsMoney(ulong player, float count, Action<int, string> callback)
        {
            string status = null;
            string url = $"https://gamestores.ru/api?shop_id={config.GSId}&secret={config.GSApi}&action=moneys&type=plus&steam_id={player.ToString()}&amount={count}&mess=Оплата товара аукциона";
            webrequest.Enqueue(url, null, (code, response) =>
            {
                if (code != 200)
                {
                    PrintError($"Ошибка соединения с сайтом GS!");
                }
                else
                {
                    JObject jObject = JObject.Parse(response);
                    if (jObject["result"].ToString() == "fail")
                    {
                        PrintError($"Ошибка пополнения баланса для {player}!");
                        PrintError($"Причина: {jObject["message"].ToString()}");
                    }
                    else
                    {
                        callback.Invoke(code, jObject["result"].ToString());
                    }
                }

            }, this);
        }
        double GetBalanceEconomycs(ulong player)
        {
            return (double)Economics?.Call("balance", player.ToString());
        }
        int GetBalanceServerReward(ulong player)
        {
            return (int)ServerRewards?.Call("CheckPoints", player.ToString());
        }
        void GiveEconomycsMoney(ulong player, float count)
        {
            Economics?.Call("Deposit", player.ToString(), count);
        }
        void TakeEconomycsMoney(ulong player, float count)
        {
            Economics?.Call("Withdraw", player.ToString(), count);
        }
        void GiveServerRewardMoney(ulong player, float count)
        {
            ServerRewards?.Call("AddPoints", player, count);
        }
        void TakeServerRewardMoney(ulong player, float count)
        {
            ServerRewards?.Call("TakePoints", player, count);
        }
        string GetDefaultName(string ShortName)
        {
            ItemDefinition GetItemName = ItemManager.FindItemDefinition(ShortName);
            return GetItemName.displayName.english;
        }
        #endregion
    }
}
