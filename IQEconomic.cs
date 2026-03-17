using Oxide.Game.Rust.Cui;
using UnityEngine.Networking;
using System.Text.RegularExpressions;
using System.Text;
using System;
using Object = System.Object;
using Oxide.Core.Libraries.Covalence;
using System.Collections;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using Oxide.Core.Database;
using Oxide.Core;
using UnityEngine;
using ConVar;
using System.Linq;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("IQEconomic", "fix", "1.25.30")]
    [Description("Экономика на ваш сервер")]
    class IQEconomic : RustPlugin
    {
        private Double TimeUnblockExchange(Int32 TimeExchange) => (SaveRestore.SaveCreatedTime.ToUniversalTime().Subtract(epoch).TotalSeconds + TimeExchange) - CurrentTime;
        
        private Configuration.TransferSetting.ExchangeRate GetExcahngeRate()
        {
            Configuration.TransferSetting.ExchangeRate rate = null;
            foreach (KeyValuePair<Int32, Configuration.TransferSetting.ExchangeRate> exchangeRate in config.TransferSettings.ExchangeRates.OrderByDescending(x => x.Key))
                if (TimeUnblockExchange(exchangeRate.Key) <= 0)
                {
                    rate = exchangeRate.Value;
                    break;
                }

            return rate;
        }
        
        
        private readonly Core.MySql.Libraries.MySql sqlLibrary = Interface.Oxide.GetLibrary<Core.MySql.Libraries.MySql>();
        private static string HexToRustFormat(string hex)
        {
            Color color;
            ColorUtility.TryParseHtmlString(hex, out color);
            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }

		int API_GET_BALANCE(string userID) => GetBalance(ulong.Parse(userID));
        
        
		void API_SET_BALANCE(string userID, int Balance, ItemContainer itemContainer = null) => SetBalance(ulong.Parse(userID), Balance, itemContainer);
        public bool IsClans(string userID, string targetID)
        {
            if (Clans)
            {
                if(Clans.Author.Contains("dcode"))
                {
                    String TagUserID = (String)Clans?.Call("GetClanOf", userID);
                    String TagTargetID = (String)Clans?.Call("GetClanOf", targetID);
                    return (bool)(TagUserID == TagTargetID);
                }
                else return (bool)Clans?.Call("IsClanMember", userID, targetID);
            }
            else return false;
        }
        
        private String SQL_Query_CreatedDatabase()
        {
	        String CreatedDB = $"CREATE TABLE IF NOT EXISTS `{(String.IsNullOrWhiteSpace(config.MySQLConnectionSettings.DataBaseNameTable) ? "DataPlayers" : config.MySQLConnectionSettings.DataBaseNameTable)}`(" +
		        "`id` INT(11) NOT NULL AUTO_INCREMENT," +
		        "`steamid` VARCHAR(255) NOT NULL," +
		        "`balance` VARCHAR(255) NOT NULL," +
		        "`limit_balance` VARCHAR(255) NOT NULL," +
		        "`time` VARCHAR(255) NOT NULL," +
		        "`last_connection` VARCHAR(255) NOT NULL," +
		        " PRIMARY KEY(`id`))";
        
	        return CreatedDB;
        }
        public void TransferPlayer(ulong userID, ulong transferUserID, int Balance )
        {
            BasePlayer player = BasePlayer.FindByID(userID);
            BasePlayer transferPlayer = BasePlayer.FindByID(transferUserID);
            if (player == null) return;
            if (transferPlayer == null) return;

            if (!IsRemoveBalance(player.userID.Get(), Balance))
            {
                SendChat(GetLang("BALANCE_TRANSFER_NO_BALANCE", player.UserIDString), player);
                return;
            }

            RemoveBalance(player.userID.Get(), Balance);
            SetBalance(transferPlayer.userID.Get(), Balance);
            SendChat(GetLang("BALANCE_TRANSFER_PLAYER", player.UserIDString, transferPlayer.displayName, Balance), player);
            SendChat(GetLang("BALANCE_TRANSFER_TRANSFERPLAYER", transferPlayer.UserIDString, Balance, player.displayName), transferPlayer);
        }

		void API_TRANSFERS(ulong userID, ulong trasferUserID, int Balance) => TransferPlayer(userID, trasferUserID, Balance);
        
        private class PlayerInfo : SplitDatafile<PlayerInfo>
        {
                        
            private const String BaseFolder = "IQSystem" + "/" + "IQEconomic" + "/";
            
            public static PlayerInfo Save(String id) => Save(BaseFolder, id);
            public static void Import(String id, PlayerInfo data) => Import(BaseFolder, id, data);
            public static void Remove(String id) => Remove(BaseFolder, id);
            public static PlayerInfo Get(String id) => Get(BaseFolder, id);
            public static PlayerInfo Load(String id) => Load(BaseFolder, id);
            public static PlayerInfo Clear(String id) => ClearAndSave(BaseFolder, id);
            public static PlayerInfo GetOrLoad(String id) => GetOrLoad(BaseFolder, id);
            public static PlayerInfo GetOrCreate(String id) => GetOrCreate(BaseFolder, id);
            public static String[] GetFiles() => GetFiles(BaseFolder);
            
                        
            [JsonProperty(LanguageEn ? "Player Balance" : "Баланс игрока")]
            public Int32 Balance;
            [JsonProperty(LanguageEn ? "Player's balance withdrawal limit" : "Лимит вывода баланса игрока")]
            public Int32 LimitBalance;
            [JsonProperty(LanguageEn ? "Time counter" : "Счетчик времени")]
            public Int32 Time;
            [JsonProperty(LanguageEn ? "The player's last login to the server" : "Последний вход игрока на сервер")]
            public Double DateTime;
        }
		   		 		  						  	   		  	 	 		  	  			  			 		  				
                
        [JsonProperty(LanguageEn ? "The time of the current time for the withdrawal limit" : "Время действующего времени на лимит выводов")] 
        public Int32 ActualityLimitTime;

        
        
        private void ValidatePlayerDatabase(String UserID)
        {
            if (sqlConnection == null) return;

            Sql sql = Sql.Builder.Append(SQL_Query_SelectedDatabase(UserID));

            sqlLibrary.Query(sql, sqlConnection, list =>
            {
                if (list.Count > 0)
                    foreach (Dictionary<String, Object> entry in list)
                    {
                        String SteamID = (String)entry["steamid"];
                        if (!SteamID.IsSteamId()) return;

                        if (!Int32.TryParse((String)entry["balance"], out Int32 Balance))
                            return;

                        if (!Int32.TryParse((String)entry["limit_balance"], out Int32 LimitBalance))
                            return;

                        if (!Int32.TryParse((String)entry["time"], out Int32 Time))
                            return;

                        if (!Double.TryParse((String)entry["last_connection"], out Double DateTime))
                            return;
            
                        RegisteredDataUserFromMySQL(SteamID, Balance, LimitBalance, Time, DateTime);
                    }
            });
        }
		void API_TRANSFERS(BasePlayer player, string trasferUserID, int Balance) => TransferPlayer(player.userID.Get(), ulong.Parse(trasferUserID), Balance);

        [ConsoleCommand("transfer.all")]
        void TransferAll(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null) return;

            if (CooldownTransfer.ContainsKey(player))
            {
                if (CooldownTransfer[player] > CurrentTime) return;
                CooldownTransfer[player] = CurrentTime + 2f;
            }
            else CooldownTransfer.Add(player, CurrentTime + 2f);

            Int32 Balance = config.TransferSettings.UseLimits ? GetBalance(player.userID.Get()) > GetLimit(player.UserIDString) ? GetLimit(player.UserIDString) : GetBalance(player.userID.Get()) : GetBalance(player.userID.Get());
            Configuration.TransferSetting.ExchangeRate Transfer = GetExcahngeRate();
            if (Transfer == null)
            {
                SendChat(GetLang("UI_NO_EXCANCHGE", player.UserIDString), player);
                return;
            }
            
            if (!IsTransfer(Balance, Transfer.MoneyCount)) return;
            
            UpdateButtonEchange(player, true);

            var Reference = config.ReferenceSettings;

            Int32 ResultTakeMoney = Balance / Transfer.MoneyCount * Transfer.StoresMoneyCount;
            Int32 remainder = Balance % Transfer.MoneyCount;

            if (remainder != 0)
            {
                Balance -= remainder;
                ResultTakeMoney = Balance / Transfer.MoneyCount * Transfer.StoresMoneyCount;
            }

            if (Reference.GameStoreshUse)
                GameStoresBalanceSet(player.userID.Get(), ResultTakeMoney, Balance);

            if (Reference.MoscovOvhUse)
                MoscovOVHBalanceSet(player.userID.Get(), ResultTakeMoney, Balance);

            if (config.TransferSettings.UseLimits)
                LimitedUpdate(player.UserIDString, Balance);
        }
		   		 		  						  	   		  	 	 		  	  			  			 		  				
        public void RemoveBalance(BasePlayer player, int removeBalance)
        {
            if (player == null)
            {
                PrintWarning(GetLang("BALANCE_CUSTOM_MONEY_NOT_PLAYER"));
                return;
            }

            if(!IsRemoveBalance(player, removeBalance))
            {
                PrintWarning(GetLang("BALANCE_CUSTOM_MONEY_NO_COUNT_TAKE"));
                return;
            }

            if (!config.UseUI)
            {
                TakeItems(player, removeBalance);
		   		 		  						  	   		  	 	 		  	  			  			 		  				
                //TakeMoneyPlayer(player, removeBalance);
            }
            else
            {
                String playerID = player.UserIDString;
                PlayerInfo playerData = PlayerInfo.Get(playerID);
                
                if (playerData != null)
                {
                    playerData.Balance -= removeBalance;

                    if (config.ShowUI)
                        Interface_Balance(player);
                }
                else RegisteredDataUser(playerID);
                
                if (config.MySQLConnectionSettings.UseMySQL)
                    InserDatabase(player.UserIDString, playerData.Balance, playerData.LimitBalance, playerData.Time, playerData.DateTime);
            }

            Interface.Oxide.CallHook("SET_BALANCE_USER", player.userID.Get(), removeBalance); 
        }
		void API_SET_BALANCE(BasePlayer player, int Balance, ItemContainer itemContainer = null) => SetBalance(player, Balance, itemContainer);
        
        private void OnLootSpawn(LootContainer container)
        {
            if (container == null || !config.GeneralSetting.SettingDropMoneyPreset.Presets.ContainsKey(container.ShortPrefabName)) return;
            
            Configuration.GeneralSettings.SettingDropMoney.Preset preset = config.GeneralSetting.SettingDropMoneyPreset.Presets[container.ShortPrefabName];
            if (!IsRare(preset.Rare)) return;

            timer.In(0.21f, () =>
            {
                if (container.inventory == null) return;

                Int32 RandomMoney = UnityEngine.Random.Range(preset.MoneyMin, preset.MoneyMax);
                
                if (container.inventory.capacity <= container.inventory.itemList.Count)
                    container.inventory.capacity = container.inventory.itemList.Count + 1;
                
                Item item = config?.CustomMoneySetting?.ToItem(RandomMoney);
                if (item == null) return;
                
                item.MoveToContainer(container.inventory);
            });
        }

        private Tuple<Int32, Configuration.TransferSetting.ExchangeRate> GetNextExchangeRateKey()
        {
            var orderedExchangeRates = config.TransferSettings.ExchangeRates.OrderBy(x => x.Key).ToList();
            int currentIndex = orderedExchangeRates.FindIndex(x => LastLocalExchangeRate == x.Value);

            if (currentIndex == -1 || currentIndex == orderedExchangeRates.Count - 1)
            {
                return new Tuple<Int32, Configuration.TransferSetting.ExchangeRate>(-1, null);
            }
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            KeyValuePair<Int32, Configuration.TransferSetting.ExchangeRate> nextPair = orderedExchangeRates[currentIndex + 1];
            return Tuple.Create(nextPair.Key, nextPair.Value);
        }

		bool API_MONEY_TYPE() => (!config.UseUI);
        
        void ChatCommandBalance(BasePlayer player)
        {
            SendChat(GetLang("CHAT_MY_BALANCE", player.UserIDString, GetBalance(player.userID.Get())), player);
        }
        public void RemoveBalance(ulong userID, int removeBalance)
        {
            BasePlayer player = BasePlayer.FindByID(userID);
            if (player == null)
            {
                PrintWarning(GetLang("BALANCE_CUSTOM_MONEY_NOT_PLAYER"));
                return;
            }
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            RemoveBalance(player, removeBalance);
        }

		void API_REMOVE_BALANCE(string userID, int Balance) => RemoveBalance(ulong.Parse(userID), Balance);
		void API_SET_BALANCE(ulong userID, int Balance, ItemContainer itemContainer = null) => SetBalance(userID, Balance, itemContainer);
		bool API_IS_REMOVED_BALANCE(ulong userID, int Amount) => IsRemoveBalance(userID, Amount);

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();

                
        public String FormatTime(TimeSpan time, String UserID)
        {
            String Result = String.Empty;
            String Days = GetLang("TITLE_FORMAT_LOCKED_DAYS", UserID);
            String Hourse = GetLang("TITLE_FORMAT_LOCKED_HOURSE", UserID);
            String Minutes = GetLang("TITLE_FORMAT_LOCKED_MINUTES", UserID);
            String Seconds = GetLang("TITLE_FORMAT_LOCKED_SECONDS", UserID);

            if (time.Days != 0)
                Result += $"{Format(time.Days, Days, Days, Days)}";
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            if (time.Hours != 0)
                Result += $" {Format(time.Hours, Hourse, Hourse, Hourse)}";

            if (time.Minutes != 0)
                Result += $" {Format(time.Minutes, Minutes, Minutes, Minutes)}";
                
            if (time.Days == 0 && time.Hours == 0 && time.Minutes == 0 && time.Seconds != 0)
                Result = $" {Format(time.Seconds, Seconds, Seconds, Seconds)}";
            
            return Result;
        }
        
        
        private void SQL_OpenConnection()
        {
            Configuration.MySQLConnection SQLInfo = config.MySQLConnectionSettings;
            if (!SQLInfo.UseMySQL) return;
            if (String.IsNullOrWhiteSpace(SQLInfo.IP) || String.IsNullOrWhiteSpace(SQLInfo.Password) ||
                String.IsNullOrWhiteSpace(SQLInfo.Port) || String.IsNullOrWhiteSpace(SQLInfo.DataBaseName) ||
                String.IsNullOrWhiteSpace(SQLInfo.UserName))
            {
                PrintError(LanguageEn
                    ? "You have MySQL support enabled but fixed to make SQL work correctly!"
                    : "У вас включена поддержка MySQL но некоторые поля пустые, исправьте чтобы SQL работал корректно!");
                return;
            }


            sqlConnection = sqlLibrary.OpenDb(SQLInfo.IP, Convert.ToInt32(SQLInfo.Port), SQLInfo.DataBaseName,
                SQLInfo.UserName, SQLInfo.Password, this);

            if (sqlConnection == null) return;
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            Sql sql = Sql.Builder.Append(SQL_Query_CreatedDatabase());
            sqlLibrary.Insert(sql, sqlConnection);
            sql = Sql.Builder.Append(SQL_Query_SelectedDatabase());
            sqlLibrary.Query(sql, sqlConnection, list =>
            {
                if (list.Count > 0)
                    foreach (Dictionary<String, Object> entry in list)
                    {
                        String SteamID = (String)entry["steamid"];
                        if (!SteamID.IsSteamId())
                        {
                            PrintError(LanguageEn
                                ? "Error parsing SteamID player"
                                : "Ошибка парсинга SteamID игрока");
                            return;
                        }

                        Int32 Balance = 0;
                        if (!Int32.TryParse((String)entry["balance"], out Balance))
                        {
                            PrintError(LanguageEn
                                ? "Error parsing Balance player"
                                : "Ошибка парсинга Balance игрока");
                            return;
                        }

                        Int32 LimitBalance = 0;
                        if (!Int32.TryParse((String)entry["limit_balance"], out LimitBalance))
                        {
                            PrintError(LanguageEn
                                ? "Error parsing LimitBalance player"
                                : "Ошибка парсинга LimitBalance игрока");
                            return;
                        }

                        Int32 Time = 0;
                        if (!Int32.TryParse((String)entry["time"], out Time))
                        {
                            PrintError(LanguageEn ? "Error parsing Time player" : "Ошибка парсинга Time игрока");
                            return;
                        }

                        Double DateTime = 0;
                        if (!Double.TryParse((String)entry["last_connection"], out DateTime))
                        {
                            PrintError(LanguageEn
                                ? "Error parsing DateTime player"
                                : "Ошибка парсинга DateTime игрока");
                            return;
                        }
		   		 		  						  	   		  	 	 		  	  			  			 		  				
                        RegisteredDataUserFromMySQL(SteamID, Balance, LimitBalance, Time, DateTime);
                    }

                PrintWarning(LanguageEn
                    ? "Updated information about users from the database"
                    : "Обновлена информация о пользователях из БД");
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                    OnPlayerConnected(player);
            });
        }
        enum TypeData
        {
            None,
            AllWipe,
            AllWipeOnlyBalance,
            TimeClear,
            TimeClearOnlyBalance,
        }
        public Boolean IsLimited(String playerID, Int32 Amount)
        {
            if (config.TransferSettings.UseLimits)
                return GetLimit(playerID) < Amount;
            else return false;
        }

                
        void RegisteredDataUser(String playerID)
        {
            if (!playerID.IsSteamId()) return;
            PlayerInfo playerData = PlayerInfo.Get(playerID);
            if (playerData == null)
                PlayerInfo.Import(playerID, new PlayerInfo
                {
                    Balance = config.StartedBalance,
                    LimitBalance = config.TransferSettings.LimitBalance,
                    Time = 0,
                    DateTime = CurrentTime
                });
            else playerData.DateTime = CurrentTime;
        }
        
        private void OnServerInitialized()
        {
            PlayerInfo._players = new Dictionary<String, PlayerInfo>();
 
            _imageUI = new ImageUI();
            _imageUI.DownloadImage();
            
            cmd.AddChatCommand(config.ChatCommandBalance, this, nameof(ChatCommandBalance));

            permission.RegisterPermission(config.GeneralSetting.TimeReward.PermissionReward, this);
            permission.RegisterPermission(config.GeneralSetting.NPCReward.PermissionReward, this);
            permission.RegisterPermission(config.GeneralSetting.KilledReward.PermissionReward, this);
            permission.RegisterPermission(config.GeneralSetting.HelicopterReward.PermissionReward, this);
            permission.RegisterPermission(config.GeneralSetting.GatherReward.PermissionReward, this);
            permission.RegisterPermission(config.GeneralSetting.CollectableReward.PermissionReward, this);
            permission.RegisterPermission(config.GeneralSetting.BradleyReward.PermissionReward, this);
            permission.RegisterPermission(config.GeneralSetting.BarrelReward.PermissionReward, this);
            permission.RegisterPermission(config.GeneralSetting.AnimalReward.PermissionReward, this);

            if (config.MySQLConnectionSettings.UseMySQL)
                SQL_OpenConnection();
            else
            {
                LoadDataFiles();

                if (Oxide.Core.Interface.Oxide.DataFileSystem.ExistsDatafile("IQEconomic/DataEconomics"))
                {
                    Int32 count = 0;
                    DataEconomics = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<UInt64, InformationData>>("IQEconomic/DataEconomics");
                    foreach (KeyValuePair<UInt64,InformationData> oldData in DataEconomics)
                    {
                        PlayerInfo.Import(oldData.Key.ToString(), new PlayerInfo
                        {
                            Balance = oldData.Value.Balance,
                            LimitBalance = oldData.Value.LimitBalance,
                            Time = oldData.Value.Time,
                            DateTime = oldData.Value.DateTime
                        });

                        count++;
                    }
                        
                    PrintWarning($"Перенесено {count} игроков из старого дата-файла");
                    SaveDataFiles();
                        
                    Oxide.Core.Interface.Oxide.DataFileSystem.DeleteDataFile("IQEconomic/DataEconomics");
                        
                    LoadDataFiles();
                }
                
                if(isNewSave)
                    ClearDataFile();
                
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                    OnPlayerConnected(player);
            }

            LastLocalExchangeRate = GetExcahngeRate();
            if (config.GeneralSetting.TimeReward.UseReward)
                timer.Every(120f, TrackerTime);

            if(!config.MySQLConnectionSettings.UseMySQL || sqlConnection == null)
                if (config.GeneralSetting.DataFileSetting.SaveTime > 0)
                    timer.Every(config.GeneralSetting.DataFileSetting.SaveTime, SaveDataFiles);

            if (config.TransferSettings.UseAlertExchangeNew)
                timer.Every(300f, RefreshExchnage);

            ResetLimit();

            if (config.GeneralSetting.SettingDropMoneyPreset.UseDropMoney && !config.UseUI)
                Subscribe("OnLootSpawn");

            if (!config.useSaveMoneyForDeath || config.UseUI)
            {
                Unsubscribe("OnPlayerDeath");
                Unsubscribe("OnPlayerRespawned");
            }
        }
        private Connection sqlConnection = null;

        
        
        private void RefreshExchnage()
        {
            Configuration.TransferSetting.ExchangeRate RefreshRate = GetExcahngeRate();
            if (LastLocalExchangeRate != null && LastLocalExchangeRate != RefreshRate)
            {
                LastLocalExchangeRate = RefreshRate;
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                    SendChat(GetLang("UI_NEW_EXCHANGE_RATE", player.UserIDString), player);
            }
        } 
        
        
                void Unload()
        {
            if (_ == null) return;
            
            foreach(BasePlayer player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, UI_BALANCE_PARENT);
                CuiHelper.DestroyUi(player, UI_CHANGER_PARENT);
            }

            if (!config.MySQLConnectionSettings.UseMySQL)
                SaveDataFiles();
            
            PlayerInfo._players.Clear();
            PlayerInfo._players = null;
            
            if (coroutineMigrate != null)
                coroutineMigrate = null;

            if (sqlConnection != null)
                sqlLibrary?.CloseDb(sqlConnection);
            
            if (_imageUI != null)
            {
                _imageUI.UnloadImages();
                _imageUI = null;
            }
            
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQSystem/IQEconomic/Limits/ActualityLimitTime", ActualityLimitTime);
            
            _ = null;
        }
        public bool IsRemoveBalance(BasePlayer player, int Amount) => (GetBalance(player) >= Amount);
        public void MoscovOVHBalanceSet(ulong userID, int Balance)
        {
            if (!RustStore)
            {
                PrintWarning("У вас не установлен магазин MoscovOVH");
                return;
            }
            plugins.Find("RustStore").CallHook("APIChangeUserBalance", userID, Balance, new Action<string>((result) =>
            {
                BasePlayer player = BasePlayer.FindByID(userID);
                if (result == "SUCCESS")
                {
                    Puts($"Пользователю {userID} успешно зачислен баланс - {Balance}");
                    if (player == null) return;
                 //   SendChat(GetLang("CHAT_STORE_SUCCESS", player.UserIDString), player);
                    return;
                }
                Puts($"Пользователь {userID} не авторизован в магазине");
                if (player == null) return;
                SendChat(GetLang("CHAT_NO_AUTH_STORE", player.UserIDString), player);
            }));
        }
        
        private void OnPlayerRespawned(BasePlayer player)
        {
            if (!moneyAmountSavePlayer.ContainsKey(player)) return;
            SetBalance(player, moneyAmountSavePlayer[player], isSave:true);
            moneyAmountSavePlayer.Remove(player);
        }
        
        private void TakeItems(BasePlayer player, Int32 TargetAmount = 1)
        {
            List<Item> acceptedItems = GetAcceptedItems(player);

            foreach (Item use in acceptedItems)
            {
                if (use.amount == TargetAmount)
                {
                    use.RemoveFromContainer();
                    use.Remove();
                    TargetAmount = 0;
                    break;
                }

                if (use.amount > TargetAmount)
                {
                    use.amount -= TargetAmount;
                    player.inventory.SendSnapshot();
                    TargetAmount = 0;
                    break;
                }

                if (use.amount < TargetAmount)
                {
                    TargetAmount -= use.amount;
                    use.RemoveFromContainer();
                    use.Remove();
                }
            }
        }

		void API_TRANSFERS(BasePlayer player, ulong trasferUserID, int Balance) => TransferPlayer(player.userID.Get(), trasferUserID, Balance);
        private void OnServerShutdown() => Unload();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();

                if (config.ChatCommandBalance == null || String.IsNullOrEmpty(config.ChatCommandBalance))
                    config.ChatCommandBalance = "balance";
                
                if (config.TransferSettings.ExchangeRates == null || config.TransferSettings.ExchangeRates.Count == 0)
                {
                    config.TransferSettings.ExchangeRates =
                        new Dictionary<Int32, Configuration.TransferSetting.ExchangeRate>()
                        {
                            [0] = new Configuration.TransferSetting.ExchangeRate()
                            {
                                MoneyCount = 3,
                                StoresMoneyCount = 1
                            },
                            [86400] = new Configuration.TransferSetting.ExchangeRate()
                            {
                                MoneyCount = 4,
                                StoresMoneyCount = 1
                            },
                            [172800] = new Configuration.TransferSetting.ExchangeRate()
                            {
                                MoneyCount = 5,
                                StoresMoneyCount = 1
                            },
                            [259200] = new Configuration.TransferSetting.ExchangeRate()
                            {
                                MoneyCount = 6,
                                StoresMoneyCount = 1
                            },
                            [345600] = new Configuration.TransferSetting.ExchangeRate()
                            {
                                MoneyCount = 8,
                                StoresMoneyCount = 2
                            },
                        };
                }
                if (config.GeneralSetting.SettingDropMoneyPreset == null)
                    config.GeneralSetting.SettingDropMoneyPreset = new Configuration.GeneralSettings.SettingDropMoney()
                    {
                        UseDropMoney = false,
                        Presets = new Dictionary<String, Configuration.GeneralSettings.SettingDropMoney.Preset>()
                        {
                            ["crate_elite"] = new Configuration.GeneralSettings.SettingDropMoney.Preset()
                            {
                                Rare = 100,
                                MoneyMin = 3,
                                MoneyMax = 5
                            },
                            ["crate_normal"] = new Configuration.GeneralSettings.SettingDropMoney.Preset()
                            {
                                Rare = 10,
                                MoneyMin = 30,
                                MoneyMax = 50
                            },
                        }
                    };

                if (config.GeneralSetting.SettingDropMoneyPreset.Presets == null ||
                    config.GeneralSetting.SettingDropMoneyPreset.Presets.Count == 0)
                {
                    config.GeneralSetting.SettingDropMoneyPreset.Presets =
                        new Dictionary<String, Configuration.GeneralSettings.SettingDropMoney.Preset>()
                        {
                            ["crate_elite"] = new Configuration.GeneralSettings.SettingDropMoney.Preset()
                            {
                                Rare = 100,
                                MoneyMin = 3,
                                MoneyMax = 5
                            },
                            ["crate_normal"] = new Configuration.GeneralSettings.SettingDropMoney.Preset()
                            {
                                Rare = 10,
                                MoneyMin = 30,
                                MoneyMax = 50
                            },
                        };
                }
            }
            catch
            {
                PrintWarning(LanguageEn ? "Error #49" + $"reading the configuration 'oxide/config/{Name}', creating a new configuration! #33" : "Ошибка #49" + $"чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию! #33");
                LoadDefaultConfig();
            }
            NextTick(SaveConfig);
        }
        
        
        
        public static string UI_BALANCE_PARENT = "BALANCE_PLAYER_PARENT";
        bool API_IS_USER(ulong userID) => API_IS_USER(userID.ToString());

        private Dictionary<BasePlayer, Int32> moneyAmountSavePlayer = new();
		string API_GET_STORES_IL() { return "URLStores"; }

        public bool IsTransfer(int Balance, Int32 MoneyCount)
        {
            if (Balance <= 0) 
                return false;
            if (Balance >= MoneyCount) 
                return true;
            else
                return false;
        }
        
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return;

            BasePlayer player = info.InitiatorPlayer;
            if (info.InitiatorPlayer == null && entity as PatrolHelicopter)
                player = BasePlayer.FindByID(GetLastAttacker(entity.net.ID.Value));

            if (player == null) return;

            Configuration.GeneralSettings General = config.GeneralSetting;
            Configuration.ReferenceSetting ReferenceGeneral = config.ReferenceSettings;

            Boolean isPlayer = entity is BasePlayer;
            Boolean isNPC = (entity is NPCPlayer || entity.IsNpc || entity is BaseNpc) && entity is not BaseAnimalNPC;
            Boolean isAnimal = entity is BaseAnimalNPC;
            Boolean isHelicopter = entity is PatrolHelicopter;
            Boolean isBradley = entity is BradleyAPC;
            Boolean isBarrel = entity.PrefabName.Contains("barrel") && !entity.PrefabName.Contains("hobobarrel");

            if (isNPC)
            {
                if (IQSphereEvent)
                {
                    TierIQSphereEvent TierNPC = GetTierIQSphereEvent(entity.OwnerID);
                    if (General.IQSphereNpcReward.TryGetValue(TierNPC, out Configuration.GeneralSettings.GeneralSettingReward NPCReward) && NPCReward != null)
                    {
                        if (!String.IsNullOrWhiteSpace(NPCReward.PermissionReward) && !permission.UserHasPermission(player.UserIDString, NPCReward.PermissionReward)) return;
                        
                        if (IsRare(NPCReward.AdvancedReward.Rare))
                            SetBalance(player.userID.Get(), NPCReward.AdvancedReward.Money);
                        return;
                    }
                }

                if (General.NPCReward.UseReward)
                {
                    Configuration.GeneralSettings.AdvancedSetting Setting = General.NPCReward.AdvancedReward;
                    if (!String.IsNullOrWhiteSpace(General.NPCReward.PermissionReward) &&
                        !permission.UserHasPermission(player.UserIDString, General.NPCReward.PermissionReward)) return;
                    if (IsRare(Setting.Rare)) SetBalance(player.userID.Get(), Setting.Money);
                    return;
                }
            }

            if (isPlayer && entity.ToPlayer() != null && player.userID.Get() != entity.ToPlayer().userID.Get())
            {
                if (General.KilledReward.UseReward)
                {
                    if (!String.IsNullOrWhiteSpace(General.KilledReward.PermissionReward) &&
                        !permission.UserHasPermission(player.UserIDString, General.KilledReward.PermissionReward))
                        return;

                    if (ReferenceGeneral.FriendsBlockUse && IsFriends(player.userID.Get(), entity.ToPlayer().userID.Get())) return;
                    if (ReferenceGeneral.ClansBlockUse &&
                        IsClans(player.UserIDString, entity.ToPlayer()?.UserIDString)) return;
                    if (ReferenceGeneral.DuelBlockUse && IsDuel(player.userID.Get())) return;

                    Configuration.GeneralSettings.AdvancedSetting Setting = General.KilledReward.AdvancedReward;
                    if (IsRare(Setting.Rare)) 
                        SetBalance(player.userID.Get(), Setting.Money);
                    return;
                }
            }

            if (isAnimal && General.AnimalReward.UseReward)
            {
                Configuration.GeneralSettings.AdvancedSetting Setting = General.AnimalReward.AdvancedReward;
                if (!String.IsNullOrWhiteSpace(General.AnimalReward.PermissionReward) &&
                    !permission.UserHasPermission(player.UserIDString, General.AnimalReward.PermissionReward)) return;
                if (IsRare(Setting.Rare)) 
                    SetBalance(player.userID.Get(), Setting.Money);
                return;
            }

            if (isHelicopter && General.HelicopterReward.UseReward)
            {
                Configuration.GeneralSettings.AdvancedSetting Setting = General.HelicopterReward.AdvancedReward;
                if (!String.IsNullOrWhiteSpace(General.HelicopterReward.PermissionReward) &&
                    !permission.UserHasPermission(player.UserIDString, General.HelicopterReward.PermissionReward))
                    return;
                if (IsRare(Setting.Rare)) 
                    SetBalance(player.userID.Get(), Setting.Money);
                return;
            }

            if (isBradley && General.BradleyReward.UseReward)
            {
                Configuration.GeneralSettings.AdvancedSetting Setting = General.BradleyReward.AdvancedReward;
                if (!String.IsNullOrWhiteSpace(General.BradleyReward.PermissionReward) && !permission.UserHasPermission(player.UserIDString, General.BradleyReward.PermissionReward)) return;
                if (IsRare(Setting.Rare)) 
                    SetBalance(player.userID.Get(), Setting.Money);
                return;
            }

            if (isBarrel && General.BarrelReward.UseReward)
            {
                Configuration.GeneralSettings.AdvancedSetting Setting = General.BarrelReward.AdvancedReward;
                if (!String.IsNullOrWhiteSpace(General.BarrelReward.PermissionReward) && !permission.UserHasPermission(player.UserIDString, General.BarrelReward.PermissionReward)) return;
                if (IsRare(Setting.Rare))
                    SetBalance(player.userID.Get(), Setting.Money);
            }
        }

        private void ResetLimit()
        {
            if (!config.TransferSettings.UseLimits) return;
            timer.Once(300f, ResetLimit);

            if (ActualityLimitTime > CurrentTime) return;

            ActualityLimitTime = Convert.ToInt32(CurrentTime + config.TransferSettings.LimitTime);
            
            
            foreach (PlayerInfo Value in PlayerInfo._players.Values)
                NextTick(() => { Value.LimitBalance = config.TransferSettings.LimitBalance; });
            Puts(LanguageEn ? "The balance limit has been reset for the time elapsed" : "Лимит баланса был сброшен за пройденное время");
        }
        public class GameStoresConfiguration
        {
            public class API
            {

                [JsonProperty("Секретный ключ (не распространяйте его)")]
                public string SecretKey;
                [JsonProperty("ИД магазина в сервисе")]
                public string ShopID;
            }

            [JsonProperty("Настройки API плагина")]
            public API APISettings;
		}

                
        
        private void InserDatabase(String UserID, Int32 Balance, Int32 LimitBalance, Int32 Time, Double DateTime, Boolean IsMigrate = false)
        {
            if (sqlConnection == null) return;

            String sqlQuery = $"UPDATE {(String.IsNullOrWhiteSpace(config.MySQLConnectionSettings.DataBaseNameTable) ? "DataPlayers" : config.MySQLConnectionSettings.DataBaseNameTable)} SET `steamid` = @0, `balance` = @1,`limit_balance` = @2,`time` = @3,`last_connection` = @4 WHERE `steamid` = @0";
            Sql updateCommand = Oxide.Core.Database.Sql.Builder.Append(sqlQuery, UserID, Balance, LimitBalance, Time, DateTime);

            sqlLibrary.Update(updateCommand, sqlConnection, rowsAffected =>
            {
                if (rowsAffected <= 0)
                {
                    String Query = String.Format(SQL_Query_InsertUser(), UserID, Balance, LimitBalance, Time, DateTime);
                    Sql sql = Sql.Builder.Append(Query);
                    sqlLibrary.Insert(sql, sqlConnection, rowsAffecteds =>
                    {
                        if (!IsMigrate)
                            Puts(LanguageEn? "A new user has been added to the database" : "В БД был внесен новый пользователь");
                    });
                }
            });
        }
        
        void RegisteredDataUserFromMySQL(String playerID, Int32 Balance, Int32 LimitBalance, Int32 Time, Double DateTime)
        {
            if (!playerID.IsSteamId()) return;
                PlayerInfo playerData = PlayerInfo.Get(playerID);
                if (playerData == null)
                {
                    PlayerInfo.Import(playerID, new PlayerInfo
                    {
                        Balance = Balance,
                        LimitBalance = LimitBalance,
                        Time = Time,
                        DateTime = DateTime
                    });
                }
                else
                {

                    playerData.Balance = Balance;
                    playerData.LimitBalance = LimitBalance;
                    playerData.Time = Time;
                    playerData.DateTime = DateTime;
                }
        }

		void API_TRANSFERS(ulong userID, string trasferUserID, int Balance) => TransferPlayer(userID, ulong.Parse(trasferUserID), Balance);

        private Dictionary<BasePlayer, Double> CooldownTransfer = new Dictionary<BasePlayer, Double>();

        private Boolean isNewSave = false;

        
        private void TrackerTime()
        {
            Configuration.GeneralSettings.TimeSettingsReward TimeSetting = config.GeneralSetting.TimeReward;

            IEnumerable<BasePlayer> FiltredList = !String.IsNullOrWhiteSpace(TimeSetting.PermissionReward) ? BasePlayer.activePlayerList.Where(x => permission.UserHasPermission(x.UserIDString, TimeSetting.PermissionReward)) : BasePlayer.activePlayerList;
            foreach (BasePlayer player in FiltredList)
            {
                PlayerInfo playerData = PlayerInfo.Get(player.UserIDString);
                if (playerData == null)
                {
                    PrintWarning("Null P");
                    continue;
                }
                if (playerData.Time <= CurrentTime)
                {
                    Int32 SetTime = Convert.ToInt32(config.GeneralSetting.TimeReward.OnlineTime + CurrentTime);
                    SetBalance(player.userID.Get(), config.GeneralSetting.TimeReward.OnlineTimeReward);
                    playerData.Time = SetTime;
                }
            }
        }
        public void Interface_Balance(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, UI_BALANCE_PARENT);

            var Balance = GetBalance(player.userID.Get());
            Configuration.Interface Interface = config.InterfaceSetting;

            container.Add(new CuiPanel 
            {
                RectTransform = { AnchorMin = Interface.AnchorMin, AnchorMax = Interface.AnchorMax, OffsetMin = Interface.OffsetMin, OffsetMax = Interface.OffsetMax },
                Image = { FadeIn = 0.15f, Color = HexToRustFormat(Interface.ColorBackground), Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }
            }, "Hud", UI_BALANCE_PARENT);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0.01932367 0.04938272", AnchorMax = $"0.154153463 0.93823463" },
                Image = { FadeIn = 0.15f, Color = HexToRustFormat(Interface.ColorMoreOne), Sprite = "assets/icons/connection.png" }
            }, UI_BALANCE_PARENT);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0.1835208 0.1358024", AnchorMax = $"0.9625472 0.8518519" },
                Image = { FadeIn = 0.15f, Color = HexToRustFormat(Interface.ColorMoreTwo), Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }
            }, UI_BALANCE_PARENT);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.22 0", AnchorMax = "1 1" },
                Text = { Text = GetLang("UI_MY_BALANCE", player.UserIDString, Balance), Color = HexToRustFormat(Interface.ColorLabel), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleLeft, FadeIn = 0.3f }
            }, UI_BALANCE_PARENT); 

            CuiHelper.AddUi(player, container); 
        }
        private class Configuration
        {
            [JsonProperty(LanguageEn ? "Chat command to view balance" : "Чат команда для просмотра баланса")]
            public String ChatCommandBalance;
            [JsonProperty(LanguageEn ? "Keep player coins after death? (They will be removed from the corpse)" : "Сохранять монеты игрока после его смерти? (из трупа будут удалены)")]
            public Boolean useSaveMoneyForDeath;
            [JsonProperty(LanguageEn ? "The coins will be in the form of an item - false / Otherwise there will be a chat or interface - true" : "Монетки будут и гроков на руках - false / Иначе будет чат или интерфейсе - true")]
            public Boolean UseUI;
            [JsonProperty(LanguageEn ? "Use the UI interface to display the balance (true - yes (If you did not put the coins in item))/false - the information will be in the chat on command and at the entrance)" : "Использовать UI интерфейс для отображения баланса(true - да(Если вы не поставили , чтобы монетки были на руках))/false - информация будет в чате по команде и при входе)")]
            public Boolean UseUIMoney;
            [JsonProperty(LanguageEn ? "Displaying the interface with a balance (true - displays/false - hides)" : "Отображение интерфейса с балансом(true - отображает/false - скрывает)")]
            public Boolean ShowUI;
            [JsonProperty(LanguageEn ? "Enable notifications to the player about receiving currency for mining (true - yes/false - no)" : "Включить уведомления игроку о получении валюты за добычу (true - да/false - нет)")]
            public Boolean UseAlert = true;
            [JsonProperty(LanguageEn ? "Starting balance for all players" : "Стартовый баланс для всех игроков")]
            public Int32 StartedBalance = 0;
            [JsonProperty(LanguageEn ? "Interface settings in the plugin (Do not touch if you do not understand how to work with it)" : "Настройки интерфейса в плагине (Не трогайте, если не понимаете как с этим работать)")]
            public Interface InterfaceSetting = new Interface();
            [JsonProperty(LanguageEn ? "Currency exchange settings for the balance in the store" : "Настройки обмена валют на баланс в магазине")]
            public TransferSetting TransferSettings = new TransferSetting();
            [JsonProperty(LanguageEn ? "Basic Settings" : "Основные настройки")]
            public GeneralSettings GeneralSetting = new GeneralSettings();
            [JsonProperty(LanguageEn ? "Currency settings (If the type of coins in item)" : "Настройки валюты(Если вид экономики - false)")]
            public CustomMoney CustomMoneySetting = new CustomMoney();
            [JsonProperty(LanguageEn ? "Settings for collaboration with other plugins" : "Настройки совместной работы с другими плагинами")]
            public ReferenceSetting ReferenceSettings = new ReferenceSetting();
            [JsonProperty(LanguageEn ? "MySQL connection settings" : "Настройки соединения с MySQL")]
            public MySQLConnection MySQLConnectionSettings = new MySQLConnection();

            internal class MySQLConnection
            {
                [JsonProperty(LanguageEn ? "Use MySQL instead of a data file (true - yes/false - no)" : "Использовать MySQL вместо дата-файла (true - да/false - нет)")]
                public Boolean UseMySQL;
                [JsonProperty("MySQL : Username")]
                public String UserName;
                [JsonProperty("MySQL : Password")]
                public String Password;
                [JsonProperty("MySQL : Host(IP)")]
                public String IP;
                [JsonProperty("MySQL : Port")]
                public String Port;
                [JsonProperty(LanguageEn ? "MySQL : Name of the database (create it - if it doesn't exist in your database)" : "MySQL : Название базы данных (создайте ее - если ее нет в вашей БД)")]
                public String DataBaseName;
                [JsonProperty(LanguageEn ? "MySQL : Desired table name in the database" : "MySQL : Желаемое название таблицы в базе данных")]
                public String DataBaseNameTable;
            }
            internal class CustomMoney
            {
                [JsonProperty(LanguageEn ? "Currency name" : "Название валюты")]
                public String DisplayName;
                [JsonProperty(LanguageEn ? "Coin Shortname" : "Shortname монетки")]
                public String Shortname;
                [JsonProperty(LanguageEn ? "Coin SkinID" : "SkinID монетки")]
                public UInt64 SkinID;
                
                public Item ToItem(Int32 amount = 1)
                {
                    var newItem = ItemManager.CreateByName(Shortname, amount, SkinID);
                    if (newItem == null)
                    {
                        Debug.LogError($"Error creating item with shortName '{Shortname}'!");
                        return null;
                    }

                    if (!string.IsNullOrEmpty(DisplayName)) newItem.name = DisplayName;

                    return newItem;
                }
            }
                
            internal class Interface
            {
                [JsonProperty(LanguageEn ? "Color of the main panel" : "Цвет основной панели")]
                public String ColorBackground = "#FFFFFF05";
                [JsonProperty(LanguageEn ? "Additional Panel Color #1" : "Цвет дополнительной панели #1")]
                public String ColorMoreOne = "#b1b1b1";
                [JsonProperty(LanguageEn ? "Additional Panel Color #2" : "Цвет дополнительной панели #2")]
                public String ColorMoreTwo = "#CCD045FF";
                [JsonProperty(LanguageEn ? "Text color" : "Цвет текста")]
                public String ColorLabel = "#FEFFDDFF";
                [JsonProperty("AnchorMin")]
                public String AnchorMin = "1 0";
                [JsonProperty("AnchorMax")]
                public String AnchorMax = "1 0";
                [JsonProperty("OffsetMin")]
                public String OffsetMin = "-390 15";
                [JsonProperty("OffsetMax")]
                public String OffsetMax = "-212 42";
            }
            internal class GeneralSettings
            {
                [JsonProperty(LanguageEn ? "Setting up a data file" : "Настройка дата-файла")]
                public DataFileSettings DataFileSetting = new DataFileSettings();
                internal class DataFileSettings
                {
                    [JsonProperty(LanguageEn ? "Save interval file date (Set to 0 if you do not want to save the file by timer)" : "Интервал сохранения дата файла (Установите 0, если не требуется сохранять файл по таймеру)")]
                    public Int32 SaveTime = 360;
                    [JsonProperty(LanguageEn ? "Setting up Data file cleanup" : "Настройка очистки дата-файла")]
                    public DetalisWipeData DetalisWipe = new DetalisWipeData();
                    internal class DetalisWipeData
                    {
                        [JsonProperty(LanguageEn ? "Type of data file cleaning : 0 - Do not clean at all, 1 - Clean the entire date when the server is wiped, 2 - Clean the date file of players with a balance of <N (configurable in this category) when the server is wiped, 3 - Clean players who have not logged in >N (configurable in this category) days, 4 - Clear players who have not logged in >N (configurable in this category) days and their balance <N (configurable in this category)" : "Тип очистки дата-файла : 0 - Не очищать совсем, 1 - Очищать всю дату при вайпе сервера, 2 - Очищать дата файл игроков с балансом <N(настраивается в этой категории) при вайпе сервера, 3 - Очищать игроков, которые не заходили >N(настраивается в этой категории) дней, 4 - Очищать игроков, которые не заходили >N(настраивается в этой категории) дней и их баланс <N(настраивается в этой категории)")]
                        public TypeData TypeClear;
                        [JsonProperty(LanguageEn ? "<N is the amount of balance to clear the date. For types : 2 and 4" : "<N-количество баланса для очистки даты. Для типов : 2 и 4")]
                        public Int32 AmountBalance = 0;
                        [JsonProperty(LanguageEn ? ">N-days offline to clear the date. For types : 3 and 4" : ">N-дней оффлайн для очистки даты. Для типов : 3 и 4")]
                        public Int32 DayPlayer = 0;
                    }
                }

                [JsonProperty(LanguageEn ? "Setting up the currency dropout from the boxes (ONLY IF THE CURRENCY TYPE IS SELECTED: COINS IS ITEM)" : "Настройка выпдаения валюты из ящиков (ТОЛЬКО ЕСЛИ ВЫБРАН ТИП ВАЛЮТЫ : МОНЕТКИ НА РУКАХ)")]
                public SettingDropMoney SettingDropMoneyPreset = new SettingDropMoney();
                internal class SettingDropMoney
                {
                    internal class Preset
                    {
                        public Int32 Rare;
                        public Int32 MoneyMin;
                        public Int32 MoneyMax;
                    }

                    [JsonProperty(LanguageEn ? "Enable the possibility of currency falling out of the mailbox? (ONLY IF THE CURRENCY TYPE IS SELECTED: COINS IS ITEM)" : "Включить возможность выпадения валюты из ящика? (ТОЛЬКО ЕСЛИ ВЫБРАН ТИП ВАЛЮТЫ : МОНЕТКИ НА РУКАХ)")]
                    public Boolean UseDropMoney;
		   		 		  						  	   		  	 	 		  	  			  			 		  				
                    [JsonProperty(LanguageEn ? "Setting up presets with currency falling out of boxes [box(ShortPrefabName)] = setup" : "Настройка пресетов с выпадением валюты из ящиков [ящик(ShortPrefabName]] = настройка")]
                    public Dictionary<String, Preset> Presets = new Dictionary<string, Preset>();
                    
                    
                }

                [JsonProperty(LanguageEn ? "Setting up getting currency for killing players" : "Настройка получение валюты за убийство игроков")]
                public GeneralSettingReward KilledReward;
                [JsonProperty(LanguageEn ? "Setting up getting currency for killing animals" : "Настройка получение валюты за убийство животных")]
                public GeneralSettingReward AnimalReward;
                [JsonProperty(LanguageEn ? "Setting up getting currency for killing an NPC" : "Настройка получение валюты за убийство NPC")]
                public GeneralSettingReward NPCReward;
                [JsonProperty(LanguageEn ? "Setting up getting currency for destroying a bradley" : "Настройка получение валюты за уничтожение танка")]
                public GeneralSettingReward BradleyReward;
                [JsonProperty(LanguageEn ? "Setting up receiving currency for destroying a helicopter" : "Настройка получение валюты за уничтожение вертолета")]
                public GeneralSettingReward HelicopterReward;
                [JsonProperty(LanguageEn ? "Setting up receiving currency for destroying barrels" : "Настройка получение валюты за уничтожение бочек")]
                public GeneralSettingReward BarrelReward;

                [JsonProperty(LanguageEn ? "Setting up receiving currency for resource extraction" : "Настройка получение валюты за добычу ресурсов")]
                public GatherSettingReward GatherReward;
                [JsonProperty(LanguageEn ? "Setting up receiving currency for raising resources from the ground (mushrooms, berries, tree, etc.)" : "Настройка получение валюты за поднятие ресурсов с земли (грибы, ягоды, дерево и т.д)")]
                public GatherSettingReward CollectableReward;

                [JsonProperty(LanguageEn ? "Setting up receiving currency for the time spent on the server" : "Настройка получение валюты за проведенное время на сервере")]
                public TimeSettingsReward TimeReward;

                [JsonProperty(LanguageEn ? "IQSphereEvent: Setting up getting currency for killing NPCs (Under - under the sphere, Around - around the sphere, Tier1 - 1st floor of the sphere, Tier2-2nd floor of the sphere, Tier3-3rd floor of the sphere)" : "IQSphereEvent : Настройка получение валюты за убийство NPC (Under - под сферой, Around - вокруг сферы, Tier1 - 1 этаж сферы, Tier2 - 2 этаж сферы, Tier3 - 3 этаж сферы)")]
                public Dictionary<TierIQSphereEvent, GeneralSettingReward> IQSphereNpcReward;

                internal class TimeSettingsReward
                {
                    [JsonProperty(LanguageEn ? "Permissions to use this feature (If you need to make it available to everyone according to the standard, leave the field empty)" : "Права для использования данной возможности (Если вам требуется сделать ее доступной всем по стандарту - оставьте поле пустым)")]
                    public String PermissionReward = "";
                    [JsonProperty(LanguageEn ? "Use this opportunity to receive currency" : "Использовать эту возможность получения валюты")]
                    public Boolean UseReward = true;
                    [JsonProperty(LanguageEn ? "How much time do you need to spend to get a reward" : "Сколько нужно провести времени,чтобы выдали награду")]
                    public Int32 OnlineTime;
                    [JsonProperty(LanguageEn ? "How much to charge currency for the time spent on the server" : "Сколько начислять валюты за проведенное время на сервере")]
                    public Int32 OnlineTimeReward;
                }
                internal class GatherSettingReward
                {
                    [JsonProperty(LanguageEn ? "Permissions to use this feature (If you need to make it available to everyone according to the standard, leave the field empty)" : "Права для использования данной возможности (Если вам требуется сделать ее доступной всем по стандарту - оставьте поле пустым)")]
                    public String PermissionReward = "";
                    [JsonProperty(LanguageEn ? "Use this opportunity to receive currency" : "Использовать эту возможность получения валюты")]
                    public Boolean UseReward = true;
                    [JsonProperty(LanguageEn ? "How much currency to charge for resources ( [for which resource to give(shortname)] = { other settings }" : "Сколько начислять валюты за ресурсы ( [за какой ресурс давать] = { остальная настройка }")]
                    public Dictionary<String, AdvancedSetting> GatherSetting = new Dictionary<String, AdvancedSetting>();
                }
                internal class GeneralSettingReward
                {
                    [JsonProperty(LanguageEn ? "Permissions to use this feature (If you need to make it available to everyone according to the standard, leave the field empty)" : "Права для использования данной возможности (Если вам требуется сделать ее доступной всем по стандарту - оставьте поле пустым)")]
                    public String PermissionReward = "";
                    [JsonProperty(LanguageEn ? "Use this opportunity to receive currency" : "Использовать эту возможность получения валюты")]
                    public Boolean UseReward = true;
                    [JsonProperty(LanguageEn ? "Setting up currency receipt" : "Настройка получения валюты")]
                    public AdvancedSetting AdvancedReward = new AdvancedSetting();
                }
                internal class AdvancedSetting
                {
                    [JsonProperty(LanguageEn ? "Chance to get currency" : "Шанс получить валюту")]
                    public Int32 Rare;
                    [JsonProperty(LanguageEn ? "How much currency to issue" : "Сколько выдавать валюты")]
                    public Int32 Money;
                }
            }
            internal class TransferSetting
            {
                [JsonProperty(LanguageEn ? "Enable currency exchange to the balance in the store (GameStores/MoscovOVH)" : "Включить обмен валюты на баланс в магазине(GameStores/MoscovOVH)")]
                public Boolean TransferStoreUse;
                [JsonProperty(LanguageEn ? "Notify players of course changes?" : "Уведомлять игроков об изменении курса?")]
                public Boolean UseAlertExchangeNew;
                [JsonProperty(LanguageEn ? "Display after how long the exchange rate will change in the UI in the exchanger" : "Отображать через сколько изменится курс в UI в обменнике")]
                public Boolean UseAlertUICourse;
                [JsonProperty(LanguageEn ? "Display a detailed text about the course change in the UI notification" : "Отображать в UI уведомлении подробный текст об изменении курса")]
                public Boolean UseAlertUICourseDetails;
                [JsonProperty(LanguageEn ? "Setting the currency exchange rate by time - [seconds (the time after which this rate will be set after the wipe)] - the rate" : "Настройка курса обмена валюты по времени - [cекунды(врея через которое установится данный курс после вайпа)] - курс")]
                public Dictionary<Int32, ExchangeRate> ExchangeRates = new Dictionary<Int32, ExchangeRate>();
                internal class ExchangeRate
                {
                    [JsonProperty(LanguageEn ? "How many coins are required to exchange" : "Сколько монет требуется для обмена")]
                    public Int32 MoneyCount;
                    [JsonProperty(LanguageEn ? "How much balance will the player receive after the exchange" : "Сколько баланса получит игрок после обмена")]
                    public Int32 StoresMoneyCount;
                }
                [JsonProperty(LanguageEn ? "Enable the limit of funds exchange" : "Включить лимит обмена средств")]
                public Boolean UseLimits = false;
                [JsonProperty(LanguageEn ? "Limit of funds (balance per store)" : "Лимит средств(баланса на магазин)")]
                public Int32 LimitBalance = 100;
                [JsonProperty(LanguageEn ? "Time limit (in seconds) (Example : A limit of 100 balance is set, once every 3600 seconds)" : "Время лимита(в секундах) (Пример : Установлен лимит 100 баланса, раз в 3600 секунд)")]
                public Int32 LimitTime = 3600;
            }
            internal class ReferenceSetting
            {
                [JsonProperty(LanguageEn ? "Friends : Prohibit receiving coins for killing friends" : "Friends : Запретить получение монет за убийство друзей")]
                public Boolean FriendsBlockUse;
                [JsonProperty(LanguageEn ? "Clans : Prohibit receiving coins for killing fellow clans" : "Clans : Запретить получение монет за убийство сокланов")]
                public Boolean ClansBlockUse;
                [JsonProperty(LanguageEn ? "Duel/Battles : Prohibit receiving coins for killing in duels" : "Duel/Battles : Запретить получение монет за убийство на дуэлях")]
                public Boolean DuelBlockUse;
                [JsonProperty(LanguageEn ? "MoscovOVH : Enable the use of the store (Currency exchange must be enabled)" : "MoscovOVH : Включить использование магазина(Должен быть включен обмен валют)")]
                public Boolean MoscovOvhUse;
                [JsonProperty(LanguageEn ? "GameStores : Enable the use of the store (Currency exchange must be enabled)" : "GameStores : Включить использование магазина(Должен быть включен обмен валют)")]
                public Boolean GameStoreshUse;
                [JsonProperty(LanguageEn ? "GameStores : GameStores Store Settings" : "GameStores : Настройки магазина GameStores")]
                public GameStores GameStoresSettings = new GameStores();
                [JsonProperty(LanguageEn ? "IQChat : Chat Settings" : "IQChat : Настройки чата")]
                public ChatSetting ChatSettings = new ChatSetting();
         
                internal class ChatSetting
                {
                    [JsonProperty(LanguageEn ? "IQChat : Custom prefix in the chat" : "IQChat : Кастомный префикс в чате")]
                    public String CustomPrefix;
                    [JsonProperty(LanguageEn ? "IQChat : Custom avatar in the chat (If required)" : "IQChat : Кастомный аватар в чате(Если требуется)")]
                    public String CustomAvatar;
                    [JsonProperty(LanguageEn ? "IQChat : Use UI notifications" : "IQChat : Использовать UI уведомления")]
                    public Boolean UIAlertUse;
                }
                internal class GameStores
                {
                    [JsonProperty(LanguageEn ? "Store API(GameStores)" : "API Магазина(GameStores)")]
                    public String GameStoresAPIStore;
                    [JsonProperty(LanguageEn ? "Store ID(GameStores)" : "ID Магазина(GameStores)")]
                    public String GameStoresIDStore;
                    [JsonProperty(LanguageEn ? "Message to the store when issuing a balance (GameStores)" : "Сообщение в магазин при выдаче баланса(GameStores)")]
                    public String GameStoresMessage;
                }
            }

            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    useSaveMoneyForDeath = false,
                    ChatCommandBalance = "balance",
                    StartedBalance = 0,
                    UseUI = true,
                    ShowUI = true,
                    UseAlert = true,
                    UseUIMoney = true,
                    MySQLConnectionSettings = new MySQLConnection
                    {
                        UseMySQL = false,
                        UserName = "",
                        Password = "",
                        IP = "",
                        Port = "3306",
                        DataBaseName = "DataEconomics",
                        DataBaseNameTable = "DataPlayers",
		   		 		  						  	   		  	 	 		  	  			  			 		  				
                    },
                    TransferSettings = new TransferSetting
                    {
                        UseAlertExchangeNew = false,
                        UseAlertUICourse = false,
                        UseAlertUICourseDetails = false,
                        TransferStoreUse = true,
                        ExchangeRates = new Dictionary<Int32, TransferSetting.ExchangeRate>()
                        {
                            [0] = new TransferSetting.ExchangeRate()
                            {
                                MoneyCount = 3,
                                StoresMoneyCount = 1
                            },
                            [86400] = new TransferSetting.ExchangeRate()
                            {
                                MoneyCount = 4,
                                StoresMoneyCount = 1
                            },
                            [172800] = new TransferSetting.ExchangeRate()
                            {
                                MoneyCount = 5,
                                StoresMoneyCount = 1
                            },
                            [259200] = new TransferSetting.ExchangeRate()
                            {
                                MoneyCount = 6,
                                StoresMoneyCount = 1
                            },
                            [345600] = new TransferSetting.ExchangeRate()
                            {
                                MoneyCount = 8,
                                StoresMoneyCount = 2
                            },
                        },
   
                        UseLimits = false,
                        LimitBalance = 100,
                        LimitTime = 3600,
                    },
                    GeneralSetting = new GeneralSettings
                    {
                        DataFileSetting = new GeneralSettings.DataFileSettings
                        {
                          SaveTime = 360,
                          DetalisWipe = new GeneralSettings.DataFileSettings.DetalisWipeData
                          {
                              AmountBalance = 0,
                              DayPlayer = 0,
                              TypeClear = TypeData.None,
                          }
                        },
                        SettingDropMoneyPreset = new GeneralSettings.SettingDropMoney
                        {
                            UseDropMoney = false,
                            Presets = new Dictionary<String, GeneralSettings.SettingDropMoney.Preset>()
                            {
                                ["crate_elite"] = new GeneralSettings.SettingDropMoney.Preset
                                {
                                    Rare = 100,
                                    MoneyMin = 3,
                                    MoneyMax = 5
                                },
                                ["crate_normal"] = new GeneralSettings.SettingDropMoney.Preset
                                {
                                    Rare = 10,
                                    MoneyMin = 30,
                                    MoneyMax = 50
                                },
                            }
                        },
                        AnimalReward = new GeneralSettings.GeneralSettingReward
                        {
                            PermissionReward = "",
                            UseReward = true,
                            AdvancedReward = new GeneralSettings.AdvancedSetting
                            {
                                Rare = 53,
                                Money = 2,
                            },
                        },
                        BarrelReward = new GeneralSettings.GeneralSettingReward
                        {
                            PermissionReward = "",
                            UseReward = true,
                            AdvancedReward = new GeneralSettings.AdvancedSetting
                            {
                                Rare = 23,
                                Money = 5,
                            },
                        },
                        BradleyReward = new GeneralSettings.GeneralSettingReward
                        {
                            PermissionReward = "",
                            UseReward = true,
                            AdvancedReward = new GeneralSettings.AdvancedSetting
                            {
                                Rare = 44,
                                Money = 100,
                            },
                        },
                        HelicopterReward = new GeneralSettings.GeneralSettingReward
                        {
                            PermissionReward = "",
                            UseReward = true,
                            AdvancedReward = new GeneralSettings.AdvancedSetting
                            {
                                Rare = 80,
                                Money = 100,
                            },
                        },
                        KilledReward = new GeneralSettings.GeneralSettingReward
                        {
                            PermissionReward = "",
                            UseReward = true,
                            AdvancedReward = new GeneralSettings.AdvancedSetting
                            {
                                Rare = 35,
                                Money = 10,
                            },
                        },
                        NPCReward = new GeneralSettings.GeneralSettingReward
                        {
                            PermissionReward = "",
                            UseReward = true,
                            AdvancedReward = new GeneralSettings.AdvancedSetting
                            {
                                Rare = 10,
                                Money = 15,
                            },
                        },
                        TimeReward = new GeneralSettings.TimeSettingsReward
                        {
                            UseReward = true,
                            PermissionReward = "",
                            OnlineTime = 100,
                            OnlineTimeReward = 10,                                             
                        },
                        GatherReward = new GeneralSettings.GatherSettingReward
                        {
                            UseReward = true,
                            PermissionReward = "",
                            GatherSetting = new Dictionary<String, GeneralSettings.AdvancedSetting>
                            {
                                ["sulfur.ore"] = new GeneralSettings.AdvancedSetting
                                {
                                    Rare = 10,
                                    Money = 10,
                                },
                                ["stones"] = new GeneralSettings.AdvancedSetting
                                {
                                    Rare = 20,
                                    Money = 1,
                                }
                            },
                        },
                        CollectableReward = new GeneralSettings.GatherSettingReward
                        {
                            UseReward = true,
                            PermissionReward = "",
                            GatherSetting = new Dictionary<String, GeneralSettings.AdvancedSetting>
                            {
                                ["sulfur.ore"] = new GeneralSettings.AdvancedSetting
                                {
                                    Rare = 10,
                                    Money = 10,
                                },
                                ["stones"] = new GeneralSettings.AdvancedSetting
                                {
                                    Rare = 20,
                                    Money = 1,
                                }
                            },
                        },
                        IQSphereNpcReward = new Dictionary<TierIQSphereEvent, GeneralSettings.GeneralSettingReward>
                        {
                            [TierIQSphereEvent.Under] = new GeneralSettings.GeneralSettingReward
                            {
                                PermissionReward = "",
                                UseReward = true,
                                AdvancedReward = new GeneralSettings.AdvancedSetting
                                {
                                    Rare = 50,
                                    Money = 5,
                                },
                            },
                            [TierIQSphereEvent.Around] = new GeneralSettings.GeneralSettingReward
                            {
                                PermissionReward = "",
                                UseReward = true,
                                AdvancedReward = new GeneralSettings.AdvancedSetting
                                {
                                    Rare = 30,
                                    Money = 5,
                                },
                            },
                            [TierIQSphereEvent.Tier1] = new GeneralSettings.GeneralSettingReward
                            {
                                PermissionReward = "",
                                UseReward = true,
                                AdvancedReward = new GeneralSettings.AdvancedSetting
                                {
                                    Rare = 30,
                                    Money = 10,
                                },
                            },
                            [TierIQSphereEvent.Tier2] = new GeneralSettings.GeneralSettingReward
                            {
                                PermissionReward = "",
                                UseReward = true,
                                AdvancedReward = new GeneralSettings.AdvancedSetting
                                {
                                    Rare = 45,
                                    Money = 12,
                                },
                            },
                            [TierIQSphereEvent.Tier3] = new GeneralSettings.GeneralSettingReward
                            {
                                PermissionReward = "",
                                UseReward = true,
                                AdvancedReward = new GeneralSettings.AdvancedSetting
                                {
                                    Rare = 70,
                                    Money = 15,
                                },
                            },
                        }
                    },
                    CustomMoneySetting = new CustomMoney
                    {
                        DisplayName = LanguageEn ? "Coin" : "Монета удачи",
                        Shortname = "bleach",
                        SkinID = 3247855103,
                    },
                    ReferenceSettings = new ReferenceSetting
                    {
                        FriendsBlockUse = true,
                        ClansBlockUse = true,
                        DuelBlockUse = true,
                        MoscovOvhUse = true,
                        GameStoreshUse = false,
                        GameStoresSettings = new ReferenceSetting.GameStores
                        {
                            GameStoresAPIStore = "",
                            GameStoresIDStore = "",
                            GameStoresMessage = LanguageEn ? "Success" : "Успешный обмен"
                        },
                        ChatSettings = new ReferenceSetting.ChatSetting
                        {
                            CustomAvatar = "",
                            CustomPrefix = "",
                            UIAlertUse = true,
                        }
                    }
		   		 		  						  	   		  	 	 		  	  			  			 		  				
                };
            }
        }
        public void MoscovOVHBalanceSet(ulong userID, double Balance, int MoneyTake)
        {
            if (!RustStore)
            {
                PrintWarning("У вас не установлен магазин MoscovOVH");
                return;
            }
            RemoveBalance(userID, MoneyTake);

            plugins.Find("RustStore").CallHook("APIChangeUserBalance", userID, Balance, new Action<string>((result) =>
            {
                BasePlayer player = BasePlayer.FindByID(userID);
                if (result == "SUCCESS")
                {
                    Puts($"Пользователю {userID} успешно зачислен баланс - {Balance}");
                    if (player == null) return;
                    SendChat(GetLang("CHAT_STORE_SUCCESS", player.UserIDString, MoneyTake, Balance), player);
                    Interface_Changer(player);
                    return;
                }
                Puts($"Пользователь {userID} не авторизован в магазине");
                if (player == null) return;
                SetBalance(player, MoneyTake, null);
                SendChat(GetLang("CHAT_NO_AUTH_STORE", player.UserIDString), player);
            }));
        }
		bool API_IS_REMOVED_BALANCE(BasePlayer player, int Amount) => IsRemoveBalance(player, Amount);
		void API_TRANSFERS(string userID, string trasferUserID, int Balance) => TransferPlayer(ulong.Parse(userID), ulong.Parse(trasferUserID), Balance);
        public class InformationData
        {
            [JsonProperty(LanguageEn ? "Player Balance" : "Баланс игрока")]
            public Int32 Balance;
            [JsonProperty(LanguageEn ? "Player's balance withdrawal limit" : "Лимит вывода баланса игрока")]
            public Int32 LimitBalance;
            [JsonProperty(LanguageEn ? "Time counter" : "Счетчик времени")]
            public Int32 Time;
            [JsonProperty(LanguageEn ? "The player's last login to the server" : "Последний вход игрока на сервер")]
            public Double DateTime;
        }
        
        object CanUserLogin(string name, string id)
        {
            if (config.MySQLConnectionSettings.UseMySQL && sqlConnection != null)
                ValidatePlayerDatabase(id);
            
            return null;
        }
        private static IQEconomic _;

        public int GetItemBalance(BasePlayer player)
        {
            ItemDefinition itemDefinition = ItemManager.FindItemDefinition(config.CustomMoneySetting.Shortname);
            if (itemDefinition == null)
                return 0;

            int itemID = itemDefinition.itemid;
            int PMoney = 0;

            if (player.inventory.containerMain != null)
            {
                foreach (Item item in player.inventory.containerMain.itemList)
                {
                    if (item.info.itemid == itemID && item.skin == config.CustomMoneySetting.SkinID && !item.IsBusy())
                    {
                        PMoney += item.amount;
                    }
                }
            }

            if (player.inventory.containerBelt != null)
            {
                foreach (Item item in player.inventory.containerBelt.itemList)
                {
                    if (item.info.itemid == itemID && item.skin == config.CustomMoneySetting.SkinID && !item.IsBusy())
                    {
                        PMoney += item.amount;
                    }
                }
            }

            return PMoney;
        }

        public void Interface_Changer(BasePlayer player, Boolean TransferProcess = false)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, UI_CHANGER_PARENT);
            
            Configuration.TransferSetting.ExchangeRate Transfer = GetExcahngeRate();
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.1607843 0.1647059 0.1294118 0.6117647", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-199.937 81.1", OffsetMax = "180.454 133.498" }
            }, "Overlay", UI_CHANGER_PARENT);

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.1607843 0.1647059 0.1294118 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "13.656 -26.2", OffsetMax = "66.238 26.199" }
            }, UI_CHANGER_PARENT, "PanelBackgroundMoney");

            container.Add(new CuiElement
            {
                Name = "MoneyItem",
                Parent = "PanelBackgroundMoney", 
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("UI_MONEY") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-15.747 -40.3", OffsetMax = "16.253 -8.3" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.1607843 0.1647059 0.1294118 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-130.174 -26.2", OffsetMax = "-77.592 26.199" }
            }, UI_CHANGER_PARENT, "PanelBackgroundCoin");

            container.Add(new CuiElement
            {
                Name = "CoinsItem",
                Parent = "PanelBackgroundCoin",
                Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("UI_COIN") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-15.217 -40.3", OffsetMax = "16.783 -8.3" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "BalanceCount",
                Parent = UI_CHANGER_PARENT,
                Components = {
                    new CuiTextComponent { Text = GetLang("UI_MY_BALANCE_TRANSFER",player.UserIDString, GetBalance(player.userID.Get())), Font = "robotocondensed-regular.ttf", FontSize = 9, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-130.179 0", OffsetMax = "66.241 13.579" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "1 1 1 1", Sprite = "assets/icons/chevron_right.png" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-47.9693463 -13.8013463", OffsetMax = "-15.9693463 18.1993463" }
            }, UI_CHANGER_PARENT, "PanelNextVisual");
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            container.Add(new CuiElement
            {
                Name = "LabelAmountCoin",
                Parent = UI_CHANGER_PARENT,
                Components = {
                    new CuiTextComponent { Text = $"X{Transfer.MoneyCount}", Font = "robotocondensed-regular.ttf", FontSize = 15, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-75.169 -10.817", OffsetMax = "-47.969 15.215" }
                }
            });
            
            container.Add(new CuiElement
            {
                Name = "LabelAmountBalance",
                Parent = UI_CHANGER_PARENT,
                Components = {
                    new CuiTextComponent { Text = $"X{Transfer.StoresMoneyCount}", Font = "robotocondensed-regular.ttf", FontSize = 15, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-15.969 -10.817", OffsetMax = "11.23 15.215" }
                }
            });
            
            if (!config.TransferSettings.UseAlertUICourse)
            {
                container.Add(new CuiElement
                {
                    Name = "IQEconomicLabel",
                    Parent = UI_CHANGER_PARENT,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetLang("UI_CHANGER_TRANSFER_TITLE", player.UserIDString),
                            Font = "robotocondensed-regular.ttf", FontSize = 20, Align = TextAnchor.UpperCenter,
                            Color = "1 1 1 1"
                        },
                        new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-190.196 41.482",
                            OffsetMax = "190.194 68.72"
                        }
                    }
                });
            }
            else
            {
                container.Add(new CuiElement
                {
                    Name = "IQEconomicLabel",
                    Parent = UI_CHANGER_PARENT,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetLang("UI_CHANGER_TRANSFER_TITLE", player.UserIDString),
                            Font = "robotocondensed-regular.ttf", FontSize = 20, Align = TextAnchor.UpperCenter,
                            Color = "1 1 1 1"
                        },
                        new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-190.196 41.482",
                            OffsetMax = "190.194 68.72"
                        }
                    }
                });

                Tuple<Int32, Configuration.TransferSetting.ExchangeRate> NextExchange = GetNextExchangeRateKey();
                
                Int32 GetTimeNext = Convert.ToInt32(TimeUnblockExchange(NextExchange?.Item1 ?? 0));
                String TimeResult = FormatTime(TimeSpan.FromSeconds(GetTimeNext), player.UserIDString);

                container.Add(new CuiElement
                {
                    Name = "IQEconomicLabel_Course",
                    Parent = UI_CHANGER_PARENT,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetTimeNext > 0 ? config.TransferSettings.UseAlertUICourseDetails ? GetLang("UI_CHANGER_TRANSFER_TITLE_UPDATE_DETAILS", player.UserIDString, TimeResult, NextExchange.Item2.MoneyCount, NextExchange.Item2.StoresMoneyCount) : GetLang("UI_CHANGER_TRANSFER_TITLE_UPDATE", player.UserIDString, TimeResult) : GetLang("UI_CHANGER_TRANSFER_TITLE_NOT_UPDATE", player.UserIDString),
                            Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.UpperCenter,
                            Color = "1 1 1 1"
                        },
                        new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-190.196 17.482",
                            OffsetMax = "190.194 44.72"
                        }
                    }
                });
            }
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            if (config.TransferSettings.UseLimits)
            {
                String offsetMin = config.TransferSettings.UseAlertUICourse ? "-190.197 60.199" : "-190.197 26.199";
                String offsetMax = config.TransferSettings.UseAlertUICourse ? "190.193 80.643" : "190.193 46.643";
                container.Add(new CuiElement
                {
                    Name = "IQEconomicLabelAccesLimit",
                    Parent = UI_CHANGER_PARENT,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = GetLang("UI_LIMITED_PLAYER", player.UserIDString, GetLimit(player.UserIDString)),
                            Font = "robotocondensed-regular.ttf", FontSize = 13, Align = TextAnchor.UpperCenter,
                            Color = "1 1 1 1"
                        },
                        new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = offsetMin, OffsetMax = offsetMax
                        }
                    }
                });
            }

            CuiHelper.AddUi(player, container);
            
            UpdateButtonEchange(player, TransferProcess);
        }
        
        private String SQL_Query_SelectedDatabase(String steamId)
        {
            String SelectUser = $"SELECT * FROM `{(String.IsNullOrWhiteSpace(config.MySQLConnectionSettings.DataBaseNameTable) ? "DataPlayers" : config.MySQLConnectionSettings.DataBaseNameTable)}` WHERE steamid = '{steamId}'";

            return SelectUser;
        }
        
        private Coroutine coroutineMigrate = null;
        
        public bool IsFriends(ulong userID, ulong targetID)
        {
            if (Friends)
                return (bool)Friends?.Call("HasFriend", userID, targetID);
            else return false;
        }
        protected override void SaveConfig() => Config.WriteObject(config);
        static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        
        
        public bool IsRare(int Rare)
        {
            if (Rare >= UnityEngine.Random.Range(0, 100))
                return true;
            else return false;
        }
        
        private void ClearDataFile()
        {
            Configuration.GeneralSettings.DataFileSettings.DetalisWipeData DataFileSetting =
                config.GeneralSetting.DataFileSetting.DetalisWipe;
        
            switch (DataFileSetting.TypeClear)
            {
                case TypeData.None:
                    break;
                case TypeData.AllWipe:
                    {
                        foreach (KeyValuePair<String,PlayerInfo> playerList in PlayerInfo._players)
                            NextTick(() => PlayerInfo.Remove(playerList.Key));
                        break;
                    }
                case TypeData.AllWipeOnlyBalance:
                {
                    foreach (KeyValuePair<String, PlayerInfo> pInfo in PlayerInfo._players.Where(pInfo => pInfo.Value.Balance < DataFileSetting.AmountBalance))
                        NextTick(() => PlayerInfo.Remove(pInfo.Key));
                    break;
                }
                case TypeData.TimeClear:
                    {
                        foreach (KeyValuePair<String,PlayerInfo> playerList in PlayerInfo._players.Where(x => (CurrentTime - x.Value.DateTime) > (DataFileSetting.DayPlayer * 86400)))
                            NextTick(() => PlayerInfo.Remove(playerList.Key));
                        break;
                    }
                case TypeData.TimeClearOnlyBalance:
                    {
                        foreach (KeyValuePair<String,PlayerInfo> playerList in PlayerInfo._players.Where(x => (CurrentTime - x.Value.DateTime) > (DataFileSetting.DayPlayer * 86400) && x.Value.Balance < DataFileSetting.AmountBalance))
                            NextTick(() => PlayerInfo.Remove(playerList.Key));
                        break;
                    }
                default:
                    break;
            }
            
            SaveDataFiles();
        }
		void API_TRANSFERS(string userID, BasePlayer trasferPlayer, int Balance) => TransferPlayer(ulong.Parse(userID), trasferPlayer.userID.Get(), Balance);

        
        
        private TierIQSphereEvent GetTierIQSphereEvent(UInt64 OwnerID)
        {
            switch (OwnerID)
            {
                case 11111: return TierIQSphereEvent.Under;
                case 22222: return TierIQSphereEvent.Around;
                case 33333: return TierIQSphereEvent.Tier1;
                case 44444: return TierIQSphereEvent.Tier2;
                case 55555: return TierIQSphereEvent.Tier3;
            }
            return TierIQSphereEvent.None;
        }
		   		 		  						  	   		  	 	 		  	  			  			 		  				
        public bool IsRemoveBalance(ulong userID,int Amount) => (GetBalance(userID) >= Amount);

		void API_TRANSFERS(ulong userID, BasePlayer trasferPlayer, int Balance) => TransferPlayer(userID, trasferPlayer.userID.Get(), Balance);
        
        private String SQL_Query_SelectedDatabase()
        {
	        String SelectDB = $"SELECT * FROM `{(String.IsNullOrWhiteSpace(config.MySQLConnectionSettings.DataBaseNameTable) ? "DataPlayers" : config.MySQLConnectionSettings.DataBaseNameTable)}`";
	        return SelectDB;
        }

                // object OnItemAction(Item item, string action, BasePlayer player)
        // {
        //     if (item == null || player == null || String.IsNullOrWhiteSpace(action)) return null;
        //
        //     if (action == "drop")
        //     {
        //         ItemDefinition itemDefinition = ItemManager.FindItemDefinition(config.CustomMoneySetting.Shortname);
        //         if (itemDefinition == null)
        //             return 0;
        //
        //         int itemID = itemDefinition.itemid;
        //         if (item.info.itemid == itemID && item.skin == config.CustomMoneySetting.SkinID &&
        //             item.HasFlag(global::Item.Flag.IsLocked)) return false;
        //     }
        //     return null;
        // }
        private Item OnItemSplit(Item item, int amount)
        {
            if (plugins.Find("Stacks") || plugins.Find("CustomSkinsStacksFix") || plugins.Find("SkinBox") || plugins.Find("StackModifier")) return null; 

            var CustomMoney = config.CustomMoneySetting;
            if (CustomMoney.SkinID == 0) return null;
            
            if (item.skin == CustomMoney.SkinID)
            {
                Item x = ItemManager.CreateByPartialName(CustomMoney.Shortname, amount);
                x.name = CustomMoney.DisplayName;
                x.skin = CustomMoney.SkinID;
                x.amount = amount;
                item.amount -= amount;
                return x;
            }
            return null;
        }
		int API_GET_BALANCE(BasePlayer player) => GetBalance(player);
        private void Init()
        {
            _ = this;
            Unsubscribe("OnLootSpawn");

            if (Oxide.Core.Interface.Oxide.DataFileSystem.ExistsDatafile("IQEconomic/ActualityLimitTime"))
            {
                ActualityLimitTime = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Int32>("IQEconomic/ActualityLimitTime");
                Oxide.Core.Interface.Oxide.DataFileSystem.DeleteDataFile("IQEconomic/ActualityLimitTime");
                Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQSystem/IQEconomic/Limits/ActualityLimitTime", ActualityLimitTime);
                return;
            }
            ActualityLimitTime = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Int32>("IQSystem/IQEconomic/Limits/ActualityLimitTime");
        }

        [ChatCommand("transfer")]
        void ChatCommandTransfer(BasePlayer player, String cmd, String[] arg)
        {
            if (player == null) return;
            Configuration.TransferSetting.ExchangeRate Transfer = GetExcahngeRate();
            if (Transfer == null)
            {
                SendChat(GetLang("UI_NO_EXCANCHGE", player.UserIDString), player);
                return;
            }
            
            if (!config.TransferSettings.TransferStoreUse && arg.Length == 0 || arg == null)
            {
                SendChat(GetLang("TRANSFER_COMMAND_NO_ARGS", player.UserIDString),player);
                return;
            }
            else if(config.TransferSettings.TransferStoreUse && arg.Length == 0 || arg == null)
            {
                Interface_Changer(player);
                return;
            }
            BasePlayer transferPlayer = FindPlayer(arg[0]);
            if (transferPlayer == null)
            {
                SendChat(GetLang("BALANCE_CUSTOM_MONEY_NOT_PLAYER", player.UserIDString), player);
                return;
            }
            if(transferPlayer.IsDead())
            {
                SendChat(GetLang("BALANCE_TRANSFER_TRANSFERPLAYER_DIE", player.UserIDString), player);
                return;
            }

            if (arg.Length < 2)
            {
                SendChat(GetLang("TRANSFER_COMMAND_NO_ARGS", player.UserIDString), player);
                return;
            }

            Regex regex = new Regex("^[0-9]+$");
            if (!regex.IsMatch(arg[1])) return;

            Int32 Amount;
            if (!Int32.TryParse(arg[1], out Amount)) return;
            if(Amount <= 0) return; 

            TransferPlayer(player.userID.Get(), transferPlayer.userID.Get(), Amount);
        }
        
                private static Configuration config = new Configuration();

        
        
        private static StringBuilder sb = new StringBuilder();
        public string GetLang(string LangKey, string userID = null, params object[] args)
        {
            sb.Clear();
            if (args != null)
            {
                sb.AppendFormat(lang.GetMessage(LangKey, this, userID), args);
                return sb.ToString();
            }
            return lang.GetMessage(LangKey, this, userID);
        }
  
        void OnPlayerConnected(BasePlayer player) => ConnectedPlayer(player);
        
        public void SetBalance(ulong userID, int amount, ItemContainer targetContainer = null)
        {
            if(!config.UseUI)
            {
                BasePlayer player = BasePlayer.FindByID(userID);
                if (player == null)
                {
                    PrintWarning(GetLang("BALANCE_CUSTOM_MONEY_NOT_PLAYER"));
                    return;
                }

                SetBalance(player, amount, targetContainer);
                return;
            }

            String playerID = userID.ToString();
            PlayerInfo playerData = PlayerInfo.Get(playerID);
                
            if (playerData != null)
            {
                playerData.Balance += amount;
                BasePlayer player = BasePlayer.FindByID(userID);
                if (player != null)
                {
                    if (config.UseAlert)
                        SendChat(GetLang("BALANCE_SET", player.UserIDString, amount), player);

                    if (config.ShowUI)
                        Interface_Balance(player);
                }
                
                if (config.MySQLConnectionSettings.UseMySQL)
                {
                    InserDatabase(userID.ToString(), playerData.Balance, playerData.LimitBalance, playerData.Time, playerData.DateTime);
                }
            }

            Interface.Oxide.CallHook("SET_BALANCE_USER", userID, amount);
        }
        private Configuration.TransferSetting.ExchangeRate LastLocalExchangeRate;
        public void SendChat(string Message, BasePlayer player, Chat.ChatChannel channel = Chat.ChatChannel.Global)
        {
            var Chat = config.ReferenceSettings.ChatSettings;
            if (IQChat)
                if (Chat.UIAlertUse)
                    IQChat?.Call("API_ALERT_PLAYER_UI", player, Message);
                else IQChat?.Call("API_ALERT_PLAYER", player, Message, Chat.CustomPrefix, Chat.CustomAvatar);
            else player.SendConsoleCommand("chat.add", channel, 0, Message);
        }
        public void ConnectedPlayer(BasePlayer player)
        {
            if (player == null) return;
            RegisteredDataUser(player.UserIDString);
            
            if (config.UseUI)
                if (config.UseUIMoney)
                {
                    if (config.ShowUI)
                        Interface_Balance(player);
                }
                else SendChat(GetLang("CHAT_MY_BALANCE", player.UserIDString, GetBalance(player.userID.Get())), player);
            
            PlayerInfo playerData = PlayerInfo.Get(player.UserIDString);
            playerData.Time = Convert.ToInt32(config.GeneralSetting.TimeReward.OnlineTime + CurrentTime);
        }
        public enum TierIQSphereEvent
        {
            Under,
            Around,
            Tier1,
            Tier2,
            Tier3,
            None
        }
        object CanCombineDroppedItem(DroppedItem item, DroppedItem targetItem)
        {
            if (item.GetItem().skin != targetItem.GetItem().skin) return false;

            return null;
        }

        private void LoadDataFiles()
        {
            String[] players = PlayerInfo.GetFiles();
            foreach (String player in players)
                PlayerInfo.Load(player);

            String Message = LanguageEn ? "" : $"Инициализирована база данных {players.Length} игроков";
            
            Puts(Message);
        }
		int API_GET_BALANCE(ulong userID) => GetBalance(userID);
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["UI_MY_BALANCE"] = "<b><size=12>Balance : {0}</size></b>",
                ["UI_MY_BALANCE_TRANSFER"] = "<b><size=9>Balance : {0}</size></b>",

                ["CHAT_MY_BALANCE"] = "Your Balance : <color=yellow>{0}</color>",
                ["BALANCE_CUSTOM_MONEY_NOT_PLAYER"] = "Player not found",
                ["BALANCE_CUSTOM_MONEY_INVENTORY_FULL"] = "Your inventory is full, coins fell to the floor",

                ["BALANCE_SET"] = "You have successfully received : {0} money",
                ["BALANCE_TRANSFER_NO_BALANCE"] = "You do not have so many coins to transfer",
                ["BALANCE_TRANSFER_PLAYER"] = "You have successfully submitted {0} {1} money",
                ["BALANCE_TRANSFER_TRANSFERPLAYER"] = "You have successfully received {0} money from {1}",
                ["BALANCE_CUSTOM_MONEY_NO_COUNT_TAKE"] = "The player does not have as many coins available",
                ["BALANCE_TRANSFER_TRANSFERPLAYER_DIE"] = "The player is dead, you can’t give him coins",

                ["TRANSFER_COMMAND_NO_ARGS"] = "Invalid Command\nEnter the correct transfer command transfer [Nick] [Amount Money]",

                ["UI_CHANGER_TRANSFER"] = "<size=14>EXCHANGE</size>",
                ["UI_CHANGER_TRANSFER_ALL"] = "<size=14>EXCHANGE ALL</size>",
                ["UI_CHANGER_TRANSFER_BTN_PROCESS_STORE"] = "<size=14>PROCESSING</size>",
                ["UI_CHANGER_TRANSFER_TITLE"] = "Currency exchange system",
                ["UI_CHANGER_TRANSFER_TITLE_UPDATE"] = "The course will be changed after : {0}}",
                ["UI_CHANGER_TRANSFER_TITLE_UPDATE_DETAILS"] = "The course will be changed after : {0} to {1} to {2}",
                ["UI_CHANGER_TRANSFER_TITLE_NOT_UPDATE"] = "The course will no longer be changed",

                ["CHAT_MY_BALANCE"] = "Your balance at the moment : {0}",

                ["CHAT_NO_AUTH_STORE"] = "You no auth stores",
                ["CHAT_STORE_SUCCESS"] = "You succes transfers : {0} coins and received {1} balance in the shop",
                ["CHAT_LIMITED_PLAYER"] = "You have raised the withdrawal limit, expect the limit to be reset",
                ["UI_LIMITED_PLAYER"] = "Your available limit : {0}",
                ["UI_NO_EXCANCHGE"] = "You don't have access to the course for messaging at this time",
                ["UI_NEW_EXCHANGE_RATE"] = "In currency exchange now a new rate!",

                ["TITLE_FORMAT_LOCKED_DAYS"] = "D",
                ["TITLE_FORMAT_LOCKED_HOURSE"] = "H",
                ["TITLE_FORMAT_LOCKED_MINUTES"] = "M",
                ["TITLE_FORMAT_LOCKED_SECONDS"] = "S",
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["UI_MY_BALANCE"] = "<b><size=12>Ваш баланс : {0}</size></b>",
                ["UI_MY_BALANCE_TRANSFER"] = "<b><size=9>Ваш баланс : {0}</size></b>",

                ["CHAT_MY_BALANCE"] = "Ваш баланс на данный момент : <color=yellow>{0}</color>",

                ["BALANCE_SET"] = "Вы успешно получили : {0} монет",
                ["BALANCE_CUSTOM_MONEY_NOT_PLAYER"] = "Такого игрока нет",
                ["BALANCE_CUSTOM_MONEY_INVENTORY_FULL"] = "Ваш инвентарь полон, монеты выпали на пол",
                ["BALANCE_CUSTOM_MONEY_NO_COUNT_TAKE"] = "У игрока нет столько монет в наличии",
		   		 		  						  	   		  	 	 		  	  			  			 		  				
                ["BALANCE_TRANSFER_NO_BALANCE"] = "У вас нет столько монет для передачи",
                ["BALANCE_TRANSFER_PLAYER"] = "Вы успешно передали {0} {1} монет(ы)",
                ["BALANCE_TRANSFER_TRANSFERPLAYER"] = "Вы успешно получили {0} монет(ы) от {1}",
                ["BALANCE_TRANSFER_TRANSFERPLAYER_DIE"] = "Игрок мертв,вы не можете передать ему монеты",

                ["TRANSFER_COMMAND_NO_ARGS"] = "Неверная команда\nВведите корректную команду transfer [Ник] [Количество монет]",

                ["UI_CHANGER_TRANSFER"] = "<size=14>ОБМЕНЯТЬ</size>",
                ["UI_CHANGER_TRANSFER_ALL"] = "<size=14>ОБМЕНЯТЬ ВСЕ</size>",
                ["UI_CHANGER_TRANSFER_BTN_PROCESS_STORE"] = "<size=14>ОБРАБОТКА</size>",
                ["UI_CHANGER_TRANSFER_TITLE"] = "Система обмена валюты",
                ["UI_CHANGER_TRANSFER_TITLE_UPDATE"] = "Курс будет изменен через : {0}",
                ["UI_CHANGER_TRANSFER_TITLE_UPDATE_DETAILS"] = "Курс будет изменен через : {0} до {1} к {2}",
                ["UI_CHANGER_TRANSFER_TITLE_NOT_UPDATE"] = "Курс больше не будет изменен",

                ["CHAT_MY_BALANCE"] = "Ваш баланс на данный момент : {0}",
                ["CHAT_NO_AUTH_STORE"] = "Вы не аваторизованы в магазине",
                ["CHAT_STORE_SUCCESS"] = "Вы успешно обменяли валюту : {0} монет(/ы) и получили {1} рублей на баланс магазина",
                ["CHAT_LIMITED_PLAYER"] = "Вы привысили лимит на вывод средств, ожидайте обнуление лимита",
                ["UI_LIMITED_PLAYER"] = "Ваш доступный лимит : {0}",
                ["UI_NO_EXCANCHGE"] = "В данный момент нет доступного курса для обмена",
                ["UI_NEW_EXCHANGE_RATE"] = "В обмене валюты теперь новый курс!",
                
                ["TITLE_FORMAT_LOCKED_DAYS"] = "Д",
                ["TITLE_FORMAT_LOCKED_HOURSE"] = "Ч",
                ["TITLE_FORMAT_LOCKED_MINUTES"] = "М",
                ["TITLE_FORMAT_LOCKED_SECONDS"] = "С",
            }, this, "ru");
            PrintWarning("Языковой файл загружен успешно");
        }
        object CanStackItem(Item item, Item targetItem)
        {
            if (item.skin != targetItem.skin) return false;

            return null;
        }
        
        [ConsoleCommand("transfer")]
        void ConsoleCommandTransfer(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null) return;
            
            if (CooldownTransfer.ContainsKey(player))
            {
                if (CooldownTransfer[player] > CurrentTime) return;
                CooldownTransfer[player] = CurrentTime + 2f;
            }
            else CooldownTransfer.Add(player, CurrentTime + 2f);
            
            Int32 Balance = GetBalance(player.userID.Get());
            var Reference = config.ReferenceSettings;

            if (IsLimited(player.UserIDString, Balance))
            {
                SendChat(GetLang("CHAT_LIMITED_PLAYER", player.UserIDString), player);
                return;
            }

            Configuration.TransferSetting.ExchangeRate Transfer = GetExcahngeRate();
            if (Transfer == null)
            {
                SendChat(GetLang("UI_NO_EXCANCHGE", player.UserIDString), player);
                return;
            }
            
            if (!IsTransfer(Balance, Transfer.MoneyCount)) return;

            UpdateButtonEchange(player, true);

            if (Reference.GameStoreshUse)
                GameStoresBalanceSet(player.userID.Get(), Transfer.StoresMoneyCount, Transfer.MoneyCount);
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            if(Reference.MoscovOvhUse)
                MoscovOVHBalanceSet(player.userID.Get(), Transfer.StoresMoneyCount, Transfer.MoneyCount);

            if (config.TransferSettings.UseLimits)
                LimitedUpdate(player.UserIDString, Balance);
        }

        public void SetBalance(BasePlayer player, int amount, ItemContainer targetContainer = null,
            Boolean isSave = false)
        {
            if (player == null)
            {
                PrintWarning(GetLang("BALANCE_CUSTOM_MONEY_NOT_PLAYER"));
                return;
            }

            if(!config.UseUI)
            {
                Item item = config?.CustomMoneySetting?.ToItem(amount);
                if (targetContainer != null)
                {
                    item.MoveToContainer(targetContainer);
                }
                else
                {
                    if (!player.inventory.GiveItem(item))
                    {
                        item.Drop(player.GetDropPosition(), player.GetDropVelocity(), default(Quaternion));

                        if (config.UseAlert && !isSave)
                            SendChat(GetLang("BALANCE_CUSTOM_MONEY_INVENTORY_FULL", player.UserIDString), player);
                    }
                }

                if (config.UseAlert && !isSave)
                    SendChat(GetLang("BALANCE_SET", player.UserIDString, amount), player);
            }
            else
            {
                SetBalance(player.userID.Get(), amount);
                return;
            }

            Interface.Oxide.CallHook("SET_BALANCE_USER", player.userID.Get(), amount);
        }

        
        private void SaveDataFiles()
        {
            foreach (KeyValuePair<String, PlayerInfo> player in PlayerInfo._players)
                PlayerInfo.Save(player.Key);
        }
        
        private IEnumerator MigrateProcess()
        {            
            LoadDataFiles();
            
            PrintWarning($"Запущен процесс переноса данных с локального хранилища в MySQL -> {PlayerInfo._players.Count} записей, это может занять какое-то время, не выключайте сервер и не перезагружайте плагин, после заверешения вы увидите сообщение.\nПримерное время ожидания : {Math.Round((PlayerInfo._players.Count * 0.25) / 60)} минут");
        
            foreach (KeyValuePair<String, PlayerInfo> Users in PlayerInfo._players)
            {
                InserDatabase(Users.Key, Users.Value.Balance, Users.Value.LimitBalance, Users.Value.Time,Users.Value.DateTime, true);
                yield return CoroutineEx.waitForSeconds(0.25f);
            }

            foreach (KeyValuePair<String, PlayerInfo> Users in PlayerInfo._players)
                Oxide.Core.Interface.Oxide.DataFileSystem.DeleteDataFile($"IQSystem/IQEconomic/{Users.Key}");
            
            yield return CoroutineEx.waitForSeconds(10f);
            PrintWarning("Перенос завершен! Дата-файлы удалены.");
            PlayerInfo._players.Clear();
            PlayerInfo._players = null;
            PlayerInfo._players = new Dictionary<String, PlayerInfo>();
            coroutineMigrate = null;

            Interface.Oxide.ReloadPlugin(Name);
        }           
        public int GetBalance(BasePlayer player)
        {
            if (player == null)
            {
                PrintWarning(GetLang("BALANCE_CUSTOM_MONEY_NOT_PLAYER"));
                return 0;
            }

            String playerID = player.UserIDString;
            PlayerInfo playerData = PlayerInfo.Get(playerID);
            
            return config.UseUI ? playerData?.Balance ?? 0 : GetItemBalance(player);
        }
		void API_TRANSFERS(string userID, ulong trasferUserID, int Balance) => TransferPlayer(ulong.Parse(userID), trasferUserID, Balance);

        private BasePlayer FindPlayer(string nameOrId)
        {
            foreach (var check in BasePlayer.activePlayerList)
            {
                if (check.displayName.ToLower().Contains(nameOrId.ToLower()) || check.userID.ToString() == nameOrId)
                    return check;
            }

            return null;
        }
        
        
        
        private class ImageUI
        {
            private const String _path = "IQSystem/IQEconomic/Images/";
            private const String _printPath = "data/" + _path;
            private readonly Dictionary<String, ImageData> _images = new()
            {
                { "UI_COIN", new ImageData() },
                { "UI_MONEY", new ImageData() },
            };

            private enum ImageStatus
            {
                NotLoaded,
                Loaded,
                Failed
            }

            private class ImageData
            {
                public ImageStatus Status = ImageStatus.NotLoaded;
                public string Id { get; set; }
            }
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            public string GetImage(string name)
            {
                ImageData image;
                if (_images.TryGetValue(name, out image) && image.Status == ImageStatus.Loaded)
                    return image.Id;
                return null;
            }

            public void DownloadImage()
            {
                KeyValuePair<string, ImageData>? image = null;
                foreach (KeyValuePair<string, ImageData> img in _images)
                {
                    if (img.Value.Status == ImageStatus.NotLoaded)
                    {
                        image = img;
                        break;
                    }
                }

                if (image != null)
                {
                    ServerMgr.Instance.StartCoroutine(ProcessDownloadImage(image.Value));
                }
                else
                {
                    List<String> failedImages = new List<string>();

                    foreach (KeyValuePair<String, ImageData> img in _images)
                    {
                        if (img.Value.Status == ImageStatus.Failed)
                        {
                            failedImages.Add(img.Key);
                        }
                    }

                    if (failedImages.Count > 0)
                    {
                        String images = String.Join(", ", failedImages);
                        _.PrintError(LanguageEn
                            ? $"Failed to load the following images: {images}. Perhaps you did not upload them to the '{_printPath}' folder.\nDownloaded image - https://drive.google.com/drive/folders/160wrneRLnl9IR74z2MdBK59aDH7gzuGd?usp=sharing" 
                            : $"Не удалось загрузить следующие изображения: {images}. Возможно, вы не загрузили их в папку '{_printPath}'.\nСкачать можно тут - https://drive.google.com/drive/folders/160wrneRLnl9IR74z2MdBK59aDH7gzuGd?usp=sharing");
                        Interface.Oxide.UnloadPlugin(_.Name);
                    }
                    else
                    {
                        _.Puts(LanguageEn
                            ? $"{_images.Count} images downloaded successfully!"
                            : $"{_images.Count} изображений успешно загружено!");
                    }
                }
            }
            
            public void UnloadImages()
            {
                foreach (KeyValuePair<string, ImageData> item in _images)
                    if(item.Value.Status == ImageStatus.Loaded)
                        if (item.Value?.Id != null)
                            FileStorage.server.Remove(uint.Parse(item.Value.Id), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);

                _images?.Clear();
            }

            private IEnumerator ProcessDownloadImage(KeyValuePair<string, ImageData> image)
            {
                string url = $"file://{Interface.Oxide.DataDirectory}/{_path}{image.Key}.png";
                
                using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
                {
                    yield return www.SendWebRequest();

                    if (www.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError)
                    {
                        image.Value.Status = ImageStatus.Failed;
                    }
                    else
                    {
                        Texture2D tex = DownloadHandlerTexture.GetContent(www);
                        image.Value.Id = FileStorage.server.Store(tex.EncodeToPNG(), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
                        image.Value.Status = ImageStatus.Loaded;
                        UnityEngine.Object.DestroyImmediate(tex);
                    }

                    DownloadImage();
                }
            }
        }

		string API_GET_MONEY_IL() { return "URLMoney"; }

		bool API_IS_REMOVED_BALANCE(string userID, int Amount) => IsRemoveBalance(ulong.Parse(userID), Amount);
        public static string UI_CHANGER_PARENT = "CHANGER_PLAYER_PARENT";

        private void UpdateButtonEchange(BasePlayer player, Boolean TransferProcess = false)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, "TransferBut");
            CuiHelper.DestroyUi(player, "TransferButAll");
            CuiHelper.DestroyUi(player, "CloseBut");
            
            container.Add(new CuiButton
            {
                Button = { Color = HexToRustFormat("#89B63B"), Command = TransferProcess ? "" : "transfer" },
                Text = { Text = TransferProcess ? GetLang("UI_CHANGER_TRANSFER_BTN_PROCESS_STORE", player.UserIDString) : GetLang("UI_CHANGER_TRANSFER", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.8431373 0.9215686 0.6588235 1" },
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-123.951 0", OffsetMax = "-0.001 24.934" }
            }, UI_CHANGER_PARENT, "TransferBut");

            container.Add(new CuiButton
            {
                Button = { Color = HexToRustFormat("#89B63B"), Command = TransferProcess ? "" : "transfer.all" },
                Text = { Text = TransferProcess ? GetLang("UI_CHANGER_TRANSFER_BTN_PROCESS_STORE", player.UserIDString) : GetLang("UI_CHANGER_TRANSFER_ALL", player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.8431373 0.9215686 0.6588235 1" },
                RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-123.957 -24.934", OffsetMax = "0.003 0" }
            }, UI_CHANGER_PARENT, "TransferButAll");

            if (TransferProcess)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = HexToRustFormat("#B03825") },
                    Text = { Text = "<b>∞</b>", Font = "robotocondensed-regular.ttf", FontSize = 30, Align = TextAnchor.MiddleCenter, Color = "0.7843137 0.627451 0.5921569 1" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "0 0", OffsetMax = "60.019 52.398" }
                }, UI_CHANGER_PARENT, "CloseBut");
            }
            else 
            {
                container.Add(new CuiButton
                {
                    Button = { Color = HexToRustFormat("#B03825"), Close = UI_CHANGER_PARENT },
                    Text = { Text = "<b>✖</b>", Font = "robotocondensed-regular.ttf", FontSize = 30, Align = TextAnchor.MiddleCenter, Color = "0.7843137 0.627451 0.5921569 1" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "0 0", OffsetMax = "60.019 52.398" }
                }, UI_CHANGER_PARENT, "CloseBut");
            }
            
            CuiHelper.AddUi(player, container);
        }
        private object OnPlayerDeath(BasePlayer player, HitInfo hitInfo)
        {
            if (player == null) return null;
            if (hitInfo == null || !player.userID.IsSteamId())
                return null;
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            Int32 saveBalance = GetBalance(player.userID.Get());
            if (saveBalance == 0) return null;
            moneyAmountSavePlayer[player] = saveBalance;
            RemoveBalance(player, saveBalance);
            return null;
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (config.MySQLConnectionSettings.UseMySQL && sqlConnection != null)
            {
                PlayerInfo playerData = PlayerInfo.Get(player.UserIDString);
                if (playerData == null) return;
                
                InserDatabase(player.UserIDString, playerData.Balance, playerData.LimitBalance, playerData.Time, playerData.DateTime);
            }
        }

        void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
        {
            if (collectible == null) return;
            if (player == null) return;
            
            foreach (ItemAmount item in collectible.itemList)
            {
                if (item == null || player == null) return;
                Configuration.GeneralSettings.GatherSettingReward General = config.GeneralSetting.CollectableReward;
                if (!General.UseReward) return;
                Dictionary<String, Configuration.GeneralSettings.AdvancedSetting> PickupGeneral = General.GatherSetting;
                if (!String.IsNullOrWhiteSpace(General.PermissionReward) &&
                    !permission.UserHasPermission(player.UserIDString, General.PermissionReward)) return;

                if (!PickupGeneral.ContainsKey(item.itemDef.shortname)) return;
                Configuration.GeneralSettings.AdvancedSetting Gather = PickupGeneral[item.itemDef.shortname];
                if (!IsRare(Gather.Rare)) return;

                SetBalance(player.userID.Get(), Gather.Money);
            }
        }
        static Double CurrentTime => Facepunch.Math.Epoch.Current;
        private static ImageUI _imageUI;
        public Boolean IsDuel(UInt64 userID)
        {
            Object obj = Interface.Oxide.RootPluginManager.GetPlugin("AimTraining")?.CallHook("IsArenaPlayer", userID);
            if (obj is Boolean)
                return (Boolean)obj;
            else if (Battles)
                return (Boolean)Battles?.Call("IsPlayerOnBattle", userID);
            else if (Duel) return (Boolean)Duel?.Call("IsPlayerOnActiveDuel", BasePlayer.FindByID(userID));
            else if (Duelist) return (Boolean)Duelist?.Call("inEvent", BasePlayer.FindByID(userID));
            else if (ArenaTournament) return ArenaTournament.Call<Boolean>("IsOnTournament", userID);
            else if (XFarmRoom) return XFarmRoom.Call<Boolean>("API_PlayerInRoom", userID);
            else return false;
        }
        
        //OLD
        public Dictionary<UInt64, InformationData> DataEconomics = new();
        
        void OnEntityTakeDamage(PatrolHelicopter patrolHelicopter, HitInfo info)
        {
            if (info == null || info.Initiator == null || patrolHelicopter == null) return;
            BasePlayer player = info.Initiator.ToPlayer();
            NextTick(() =>
            {
                if (patrolHelicopter == null || player == null) return;
                if (!HeliAttackers.ContainsKey(patrolHelicopter.net.ID.Value))
                    HeliAttackers.Add(patrolHelicopter.net.ID.Value, new Dictionary<ulong, int>());
                if (!HeliAttackers[patrolHelicopter.net.ID.Value].ContainsKey(player.userID.Get()))
                    HeliAttackers[patrolHelicopter.net.ID.Value].Add(player.userID.Get(), 0);
                HeliAttackers[patrolHelicopter.net.ID.Value][player.userID.Get()]++;
            });
        }

        public Int32 GetLimit(String playerID)
        {
            PlayerInfo playerData = PlayerInfo.Get(playerID);
            return playerData?.LimitBalance ?? 0;
        }
        
        [ConsoleCommand("migrate.data")]
        void MigrateCommand(ConsoleSystem.Arg arg)
        {
            if(arg.Player() != null)
                if (!arg.Player().IsAdmin)
                    return;
        
            if (sqlConnection == null)
            {
                PrintError("MySQL не подключен! Включите в конфигурации или проверьте корректность данных!");
                return;
            }
        
            coroutineMigrate = ServerMgr.Instance.StartCoroutine(MigrateProcess());
        }

        
                
                
        
        private abstract class SplitDatafile<T> where T : SplitDatafile<T>, new()
        {
            public static Dictionary<String, T> _players = new();
            
            protected static void Import(String baseFolder, String userId, T data)
            {
                _players[userId] = data;
                if (!config.MySQLConnectionSettings.UseMySQL)
                    Save(baseFolder, userId);
            }

            protected static String[] GetFiles(String baseFolder)
            {
                try
                {
                    Int32 json = ".json".Length;
                    String[] paths = Interface.Oxide.DataFileSystem.GetFiles(baseFolder, "*.json");
                    for (Int32 i = 0; i < paths.Length; i++)
                    {
                        String path = paths[i];
                        Int32 separatorIndex = path.LastIndexOf("/", StringComparison.Ordinal);
                        paths[i] = path.Substring(separatorIndex + 1, path.Length - separatorIndex - 1 - json);
                    }

                    return paths;
                }
                catch
                {
                    return Array.Empty<string>();
                }
            }

            protected static T Save(string baseFolder, String userId)
            {
                T data;
                if (!_players.TryGetValue(userId, out data))
                    return null;

                Interface.Oxide.DataFileSystem.WriteObject(baseFolder + userId, data);
                return data;
            }
            
            protected static void Remove(string baseFolder, String userId)
            {
                if (!_players.ContainsKey(userId))
                    return;

                _players.Remove(userId);
                Interface.Oxide.DataFileSystem.DeleteDataFile(baseFolder + userId);
            }

            protected static T Get(string baseFolder, String userId)
            {
                T data;
                if (_players.TryGetValue(userId, out data))
                    return data;
		   		 		  						  	   		  	 	 		  	  			  			 		  				
                return null;
            }

            protected static T Load(String baseFolder, String userId)
            {
                T data = null;

                try
                {
                    data = Interface.Oxide.DataFileSystem.ReadObject<T>(baseFolder + userId);
                }
                catch (Exception e)
                {
                    Interface.Oxide.LogError(e.ToString());
                }

                return _players[userId] = data;
            }
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            protected static T GetOrLoad(String baseFolder, String userId)
            {
                T data;
                if (_players.TryGetValue(userId, out data))
                    return data;


                return Load(baseFolder, userId);
            }

            protected static T GetOrCreate(String baseFolder, String userId)
            {
                return GetOrLoad(baseFolder, userId) ?? (_players[userId] = new T());
            }

            protected static T ClearAndSave(String baseFolder, String userId)
            {
                T data;
                if (_players.TryGetValue(userId, out data))
                {
                    data = new T();

                    Interface.Oxide.DataFileSystem.WriteObject(baseFolder + userId, data);
                    return data;
                }
		   		 		  						  	   		  	 	 		  	  			  			 		  				
                return null;
            }
        }
        
        public int GetBalance(ulong userID)
        {
            if (config.UseUI)
            {
                String playerID = userID.ToString();
                PlayerInfo playerData = PlayerInfo.Get(playerID);
                return playerData?.Balance ?? 0;
            }
            else
            {
                BasePlayer player = BasePlayer.FindByID(userID);
                if (player == null)
                {
                    PrintWarning(GetLang("BALANCE_CUSTOM_MONEY_NOT_PLAYER"));
                    return 0;
                }

                return GetItemBalance(player);
            }
        }
        void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (dispenser == null || player == null || item == null) return;
            var General = config.GeneralSetting.GatherReward;
            if (!General.UseReward) return;
            var GatherGeneral = General.GatherSetting;
            if (!String.IsNullOrWhiteSpace(General.PermissionReward) && !permission.UserHasPermission(player.UserIDString, General.PermissionReward)) return;

            if (!GatherGeneral.ContainsKey(item.info.shortname)) return;
            var Gather = GatherGeneral[item.info.shortname];
            if (!IsRare(Gather.Rare)) return;

            SetBalance(player.userID.Get(), Gather.Money);
        }
        
        private String SQL_Query_InsertUser()
        {
            String InserUser = $"INSERT INTO `{(String.IsNullOrWhiteSpace(config.MySQLConnectionSettings.DataBaseNameTable) ? "DataPlayers" : config.MySQLConnectionSettings.DataBaseNameTable)}`" + "(`steamid`, `balance`, `limit_balance`, `time`, `last_connection`) VALUES ('{0}','{1}','{2}','{3}','{4}')";
            return InserUser;
        }
        void OnNewSave(string filename) => isNewSave = true; 

        private String Format(Int32 units, String form1, String form2, String form3)
        {
            var tmp = units % 10;

            if (units >= 5 && units <= 20 || tmp >= 5 && tmp <= 9)
                return $"{units}{form1}";

            if (tmp >= 2 && tmp <= 4)
                return $"{units}{form2}";

            return $"{units}{form3}";
        }
        
        
        [ConsoleCommand("check.balance")]
        private void CheckBalanceConsoleCommand(ConsoleSystem.Arg arg)
        {
            if(arg.Player() != null)
                if (!arg.Player().IsAdmin)
                    return;
            
            if (arg.Args == null || arg.Args.Length < 1)
            {
                arg.ReplyWith(LanguageEn ? "Syntax error, use: check.balance Steam64ID": "Ошибка синтаксиса, используйте : check.balance Steam64ID");
                return;
            }

            if (!UInt64.TryParse(arg.Args[0], out UInt64 userID))
            {
                arg.ReplyWith(LanguageEn ? "You have specified an incorrect Steam64ID player!": "Вы указали неккоректный Steam64ID игрока!");
                return;
            }
            PlayerInfo playerData = PlayerInfo.Get(arg.Args[0]);

            if (playerData == null)
            {
                arg.ReplyWith(LanguageEn ? "The specified Steam ID was not found in the database": "Указанный SteamID не обнаружен в базе-данных");
                return;
            }
            
            arg.ReplyWith(LanguageEn ? $"Player's balance : {GetBalance(userID)}": $"Баланс игрока : {GetBalance(userID)}");
        }
		void API_REMOVE_BALANCE(ulong userID, int Balance) => RemoveBalance(userID, Balance);
		void API_TRANSFERS(BasePlayer player, BasePlayer trasferPlayer, int Balance) => TransferPlayer(player.userID.Get(), trasferPlayer.userID.Get(), Balance);
        
        
                private const Boolean LanguageEn = false;
        private Dictionary<UInt64, Dictionary<ulong, int>> HeliAttackers = new Dictionary<UInt64, Dictionary<ulong, int>>();
        
        private ulong GetLastAttacker(UInt64 id)
        {
            Int32 hits = 0;
            UInt64 majorityPlayer = 0U;
            if (HeliAttackers.ContainsKey(id))
            {
                foreach (var score in HeliAttackers[id])
                {
                    if (score.Value > hits)
                        majorityPlayer = score.Key;
                }
            }
            return majorityPlayer;
        }

        [ConsoleCommand("iq.eco")] //iq.eco give.store 76563463807822175 300
        void IQEconomicCommandsAdmin(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !arg.Player().IsAdmin)
                return;

            if (!arg.HasArgs(3))
                return;

            string NamaOrID = arg.GetString(1);
            if(string.IsNullOrWhiteSpace(NamaOrID))
            {
                PrintWarning(LanguageEn ? "Enter nickname or SteamID" : "Введите ник или SteamID");
                return;
            }

            IPlayer iPlayer = covalence.Players.FindPlayer(NamaOrID);
            if(iPlayer == null)
            {
                PrintWarning(LanguageEn ? "There is no such player" : $"Такого игрока не существует");
                return;
            }
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            int amount = arg.GetInt(2, -1);
            if (amount == -1)
            {
                PrintWarning(LanguageEn ? "The second argument is not a number!" : "Второй аргумент не является числом!");
                return;
            }
		   		 		  						  	   		  	 	 		  	  			  			 		  				
            ulong userID = UInt64.Parse(iPlayer.Id);

            switch(arg.GetString(0))
            {
                case "give":
                {
                    SetBalance(userID, amount);
                    Puts(LanguageEn ? $"Player {userID} successfully credited {amount} of coins" : $"Игроку {userID} успешно зачислено {amount} монет");
                    break;
                }
		   		 		  						  	   		  	 	 		  	  			  			 		  				
                case "remove":
                {
                    RemoveBalance(userID, amount);
                    Puts(LanguageEn ? $"Player {userID} has successfully remove {amount} of coins" : $"Игроку {userID} успешно снято {amount} монет");
                    break;
                }

                case "give.store":
                {
                    Configuration.ReferenceSetting Reference = config.ReferenceSettings;
		   		 		  						  	   		  	 	 		  	  			  			 		  				
                    if (Reference.GameStoreshUse)
                        GameStoresBalanceSet(userID, amount);

                    if (Reference.MoscovOvhUse)
                        MoscovOVHBalanceSet(userID, amount);

                    Puts(LanguageEn ? $"Player {userID} successfully credited {amount} to the server store" : $"Игроку {userID} успешно зачислено {amount} на магазин сервера");
                    break;
                }
            }
        }
		void API_REMOVE_BALANCE(BasePlayer player, int Balance) => RemoveBalance(player, Balance);
		bool API_IS_USER(BasePlayer player) => API_IS_USER(player.UserIDString);
        
        public void LimitedUpdate(String playerID, Int32 Amount)
        {
            if (IsLimited(playerID, Amount)) return;
            PlayerInfo playerData = PlayerInfo.Get(playerID);
            if (playerData == null) return;
            
            playerData.LimitBalance -= Amount;
            
            if (config.MySQLConnectionSettings.UseMySQL && sqlConnection != null)
                InserDatabase(playerID, playerData.Balance, playerData.LimitBalance, playerData.Time, playerData.DateTime);
        }
		Item API_GET_ITEM(int Amount) => config?.CustomMoneySetting?.ToItem(Amount);
        /// <summary>
        /// Обновление 1.15.9
        /// - Заменена ссылка API для работы с GameStores
        /// - Обновил ссылку на картинки
        /// - Добавлен SkinID для монеты в стандартную конфигурацию
        ///
        /// </summary> 

                [PluginReference] Plugin IQChat, Friends, Clans, Battles, Duel, RustStore, IQSphereEvent, Duelist, ArenaTournament, XFarmRoom;

        bool API_IS_USER(string userID)
        {
            PlayerInfo playerData = PlayerInfo.Get(userID);
            return playerData != null;
        }

        public void GameStoresBalanceSet(ulong userID, double Balance, int MoneyTake = 0)
        {
            var GameStores = config.ReferenceSettings.GameStoresSettings;
            if (string.IsNullOrEmpty(GameStores.GameStoresAPIStore) || string.IsNullOrEmpty(GameStores.GameStoresIDStore))
            {
				if (Config.Exists(Interface.Oxide.ConfigDirectory + "/GameStoresRUST.json"))
				{
					try
					{
						GameStoresConfiguration gameStoresConfiguration = Config.ReadObject<GameStoresConfiguration>(Interface.Oxide.ConfigDirectory + "/GameStoresRUST.json");

						GameStores.GameStoresAPIStore = gameStoresConfiguration.APISettings.SecretKey;
						GameStores.GameStoresIDStore = gameStoresConfiguration.APISettings.ShopID;

						SaveConfig();
					}
					catch
					{
						PrintError("Error reading GameStoresRUST config!");
					}
				}

				if (string.IsNullOrEmpty(GameStores.GameStoresAPIStore) || string.IsNullOrEmpty(GameStores.GameStoresIDStore))
				{
					PrintWarning("Магазин GameStores не настроен! Невозможно выдать баланс пользователю");
					return;
				}
            }
            
            if (MoneyTake > 0)
                RemoveBalance(userID, MoneyTake);

            webrequest.Enqueue($"https://gamestores.app/api?shop_id={GameStores.GameStoresIDStore}&secret={GameStores.GameStoresAPIStore}&action=moneys&type=plus&steam_id={userID}&amount={Balance}&mess={GameStores.GameStoresMessage}", null, (i, s) =>
            {
                BasePlayer player = BasePlayer.FindByID(userID);
                if (i != 200) { }
                if (s.Contains("success"))
                {
                    Puts($"Пользователю {userID} успешно зачислен баланс - {Balance}");

                    if (player != null)
					{
                        SendChat(GetLang("CHAT_STORE_SUCCESS", player.UserIDString, MoneyTake, Balance), player);

						if (MoneyTake > 0)
							Interface_Changer(player);
                    }

                    return;
                }

                if (s.Contains("fail"))
                {
                    Puts($"Пользователь {userID} не авторизован в магазине");
                    if (player != null)
                    {
                        SendChat(GetLang("CHAT_NO_AUTH_STORE", player.UserIDString), player);
                        SetBalance(player, MoneyTake, null);
                    }
                }
            }, this);
        }

        private List<Item> GetAcceptedItems(BasePlayer player)
        {
            List<Item> acceptedItems = new List<Item>();
            Int32 itemAmount = 0;

            foreach (Item item in Enumerable.Concat(player.inventory.containerMain?.itemList ?? Enumerable.Empty<Item>(), Enumerable.Concat(player.inventory.containerBelt?.itemList ?? Enumerable.Empty<Item>(), player.inventory.containerWear?.itemList ?? Enumerable.Empty<Item>())))
            {
                if (item.info.shortname.Equals(config.CustomMoneySetting.Shortname) && item.skin == config.CustomMoneySetting.SkinID)
                {
                    acceptedItems.Add(item);
                    itemAmount += item.amount;
                }
            }

            return acceptedItems;
        }

		    }
}

