using Facepunch;
using Newtonsoft.Json;
using System.Linq;
using System.Collections;
using Oxide.Core.Plugins;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;

namespace Oxide.Plugins
{
    [Info("IQRates", "IQRates", "1.96.33")]
    [Description("Настройка рейтинга на сервере")]
    class IQRates : RustPlugin
    {
        private void OnEntitySpawned(CargoPlane entity)
        {
            NextTick(() =>
            {
                if (entity.OwnerID != 0 || entity.skinID != 0) return;
                var EvenTimer = config.pluginSettings.OtherSetting.EventSetting.CargoPlaneSetting;
                if ((EvenTimer.FullOff || EvenTimer.UseEventCustom))
                    entity.Kill();
            });
        }
        bool IsTime()
        {
            var Settings = config.pluginSettings.OtherSetting;
            float TimeServer = TOD_Sky.Instance.Cycle.Hour;
            return TimeServer < Settings.NightStart && Settings.DayStart <= TimeServer;
        }
        void AddQuarryPlayer(UInt64 NetID, UInt64 userID)
        {
            if (DataQuarryPlayer.ContainsKey(NetID))
                DataQuarryPlayer[NetID] = userID;
            else DataQuarryPlayer.Add(NetID, userID);
            WriteData();    
        }
        private void OnServerInitialized()
        {
            _ = this;
 
            SpacePort = TerrainMeta.Path.Monuments.FirstOrDefault(p => p.name.ToLower().Contains("launch_site"));

            StartEvent();
            foreach (var RateCustom in config.pluginSettings.RateSetting.PrivilegyRates)
                Register(RateCustom.Key);

            foreach (Configuration.PluginSettings.Rates.RecyclerController.PresetRecycler presetRecycler in config.pluginSettings.RateSetting.RecyclersController.PrivilageSpeedRecycler)
                    Register(presetRecycler.Permissions);
            
            if (config.pluginSettings.RateSetting.UseSpeedBurnableList)
                foreach (var BurnableList in config.pluginSettings.RateSetting.SpeedBurableList)
                    Register(BurnableList.Permissions);

            List<String> PrivilegyCustomRatePermissions = config.pluginSettings.RateSetting.CustomRatesPermissions.NightRates.Keys.Union(config.pluginSettings.RateSetting.CustomRatesPermissions.DayRates.Keys).ToList();
            foreach (var RateItemCustom in PrivilegyCustomRatePermissions)
                Register(RateItemCustom);

            timer.Once(5, GetTimeComponent);

            foreach (BaseOven oven in BaseNetworkable.serverEntities.OfType<BaseOven>())
            {
                if (config.pluginSettings.RateSetting.UseSpeedBurnable)
                {
                    if (config.pluginSettings.RateSetting.IgnoreSpeedBurnablePrefabList.Contains(oven.ShortPrefabName))
                        continue;
                    
                    OvenController.GetOrAdd(oven).TryRestart();
                }
            }
		   		 		  						  	   		  	  			  	 				  	   		  	  	
            if (!config.pluginSettings.RateSetting.UseSpeedBurnable)
                Unsubscribe(nameof(OnOvenToggle));
            
            if(!config.pluginSettings.RateSetting.RecyclersController.UseRecyclerSpeed)
                Unsubscribe(nameof(OnRecyclerToggle));
            
            if(!config.pluginSettings.RateSetting.UseTeaController)
                Unsubscribe(nameof(OnPlayerAddModifiers));

            initializeTransport = ServerMgr.Instance.StartCoroutine(InitializeTransport());
        }

        
                TOD_Time timeComponent = null;

        void OnSunrise()
        {
            timeComponent.DayLengthInMinutes = config.pluginSettings.OtherSetting.DayTime * (24.0f / (TOD_Sky.Instance.SunsetTime - TOD_Sky.Instance.SunriseTime));
            activatedDay = true;
            if (config.pluginSettings.OtherSetting.UseSkipTime)
            {
                if (config.pluginSettings.OtherSetting.TypeSkipped == SkipType.Day)
                    TOD_Sky.Instance.Cycle.Hour = config.pluginSettings.OtherSetting.NightStart;
                else
                {
                    if (config.pluginSettings.OtherSetting.UseAlertDayNight)
                    {
                        Configuration.PluginSettings.Rates.AllRates Rate = config.pluginSettings.RateSetting.DayRates;
                        foreach (BasePlayer player in BasePlayer.activePlayerList)
                            SendChat(GetLang("DAY_RATES_ALERT", player.UserIDString, Rate.GatherRate, Rate.LootRate, Rate.PickUpRate, Rate.QuarryRate, Rate.ExcavatorRate, Rate.GrowableRate), player); 
                    }
                }
                return;
            }
            if (config.pluginSettings.OtherSetting.UseAlertDayNight)
            {
                Configuration.PluginSettings.Rates.AllRates Rate = config.pluginSettings.RateSetting.DayRates;
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                    SendChat(GetLang("DAY_RATES_ALERT", player.UserIDString, Rate.GatherRate, Rate.LootRate, Rate.PickUpRate, Rate.QuarryRate, Rate.ExcavatorRate, Rate.GrowableRate), player);
            }
        }
        
        
        Single GetRateQuarryDetalis(Configuration.PluginSettings.Rates.AllRates Rates, String Shortname)
        {
            Single Rate = Rates.QuarryRate;
		   		 		  						  	   		  	  			  	 				  	   		  	  	
            if (!Rates.QuarryDetalis.UseDetalisRateQuarry) return Rate;
            return Rates.QuarryDetalis.ShortnameListQuarry.ContainsKey(Shortname) ? Rates.QuarryDetalis.ShortnameListQuarry[Shortname] : Rate;
        }
		   		 		  						  	   		  	  			  	 				  	   		  	  	
        void OnHour()
        {
            if (TOD_Sky.Instance.Cycle.Hour > TOD_Sky.Instance.SunriseTime && TOD_Sky.Instance.Cycle.Hour < TOD_Sky.Instance.SunsetTime && TOD_Sky.Instance.Cycle.Hour >= config.pluginSettings.OtherSetting.DayStart && !activatedDay)
            {
                OnSunrise();
                return;
            }
            if ((TOD_Sky.Instance.Cycle.Hour > TOD_Sky.Instance.SunsetTime || TOD_Sky.Instance.Cycle.Hour < TOD_Sky.Instance.SunriseTime) && TOD_Sky.Instance.Cycle.Hour >= config.pluginSettings.OtherSetting.NightStart && activatedDay)
            {
                OnSunset();
                return;
            }
        }
        /// <summary>
        /// Обновление 1.8.x
        /// - Исправление : Если патрульный вертолет отключен полностью или работает по таймеру - не спавнился боевой вертолет
        /// - Добавлена возможность указать рейты за рыбалку

        [PluginReference] Plugin IQChat;

        private ModifierDefintion GetDefintionModifer(Modifier.ModifierType Type, Single Duration, Single Value)
        {
            ModifierDefintion def = new ModifierDefintion
            {
                source = Modifier.ModifierSource.Tea,
                type = Type,
                duration = Duration,
                value = Value <= 0 ? 1.0f : Value
            };

            return def;
        }
        private Coroutine initializeTransport = null;
        private void OnEntitySpawned(Minicopter copter)
        {
            if (copter == null) return;
            FuelSystemRating(copter.GetFuelSystem(), config.pluginSettings.OtherSetting.FuelSetting.AmountMinicopter);
            
            copter.fuelPerSec = config.pluginSettings.OtherSetting.FuelConsumedTransportSetting.ConsumedCopter;
        }
        private void UnSubProSub(int time = 1)
        {
            Unsubscribe("OnEntitySpawned");
            timer.Once(time, () =>
            {
                Subscribe("OnEntitySpawned");
            });
        }

                private MonumentInfo SpacePort;
        
        
        private Single GetSpeedRecycler(BasePlayer player)
        {
            Configuration.PluginSettings.Rates.RecyclerController Recycler = config.pluginSettings.RateSetting.RecyclersController;

            foreach (Configuration.PluginSettings.Rates.RecyclerController.PresetRecycler presetRecycler in Recycler.PrivilageSpeedRecycler)
            {
                if (permission.UserHasPermission(player.UserIDString, presetRecycler.Permissions))
                    return presetRecycler.SpeedRecyclers;
            }
            
            return Recycler.DefaultSpeedRecycler;
        }
		   		 		  						  	   		  	  			  	 				  	   		  	  	
        
        
                [ChatCommand("rates")]
        private void GetInfoMyRates(BasePlayer player)
        {
            if (player == null) return;

            var PrivilegyRates = config.pluginSettings.RateSetting.PrivilegyRates;
            Boolean IsTimes = IsTime();
            var Rates = IsTimes ? config.pluginSettings.RateSetting.DayRates : config.pluginSettings.RateSetting.NightRates;
            var CustomRate = IsTimes ? config.pluginSettings.RateSetting.CustomRatesPermissions.DayRates : config.pluginSettings.RateSetting.CustomRatesPermissions.NightRates;

            var Rate = CustomRate.FirstOrDefault(x => IsPermission(player.UserIDString, x.Key)); 

            foreach (var RatesSetting in PrivilegyRates)
                if (IsPermission(player.UserIDString, RatesSetting.Key))
                    Rates = IsTimes ? RatesSetting.Value.DayRates : RatesSetting.Value.NightRates;

            SendChat(GetLang("MY_RATES_INFO", player.UserIDString, Rates.GatherRate, Rates.LootRate, Rates.PickUpRate, Rates.QuarryRate, Rates.ExcavatorRate, Rates.GrowableRate), player);
        }
        
        
        private static Configuration config = new Configuration();
        bool IsPermission(string userID,string Permission)
        {
            if (permission.UserHasPermission(userID, Permission))
                return true;
            else return false;
        }
        public Int32 GetMultiplaceBurnableFuelSpeed(String ownerid)
        {
            Int32 Multiplace = config.pluginSettings.RateSetting.SpeedFuelBurnable;
            if (config.pluginSettings.RateSetting.UseSpeedBurnableList)
            {
                var SpeedInList = config.pluginSettings.RateSetting.SpeedBurableList.OrderByDescending(z => z.SpeedFuelBurnable).FirstOrDefault(x => permission.UserHasPermission(ownerid, x.Permissions));
                if (SpeedInList != null)
                    Multiplace = SpeedInList.SpeedFuelBurnable;
            }
            return Multiplace;
        }
        
        private void OnEntitySpawned(MotorRowboat boat)
        {
            if (boat == null) return;
            boat.fuelPerSec = config.pluginSettings.OtherSetting.FuelConsumedTransportSetting.ConsumedBoat;
        } 

        int Converted(Types RateType, string Shortname, float Amount, BasePlayer player = null, String UserID = null)
        {
            float ConvertedAmount = Amount;

            ItemDefinition definition = ItemManager.FindItemDefinition(Shortname);
            if(definition != null && !IsWhiteList(Shortname))
                foreach (String BlackItemCategory in config.pluginSettings.RateSetting.BlackListCategory)
                {
                    ItemCategory Category;
                    if (!Enum.TryParse(BlackItemCategory, out Category)) continue;
                    
                    if (Category == definition.category)
                    {
                        //PrintToChat($"DEBUG : Категория {BlackItemCategory} заблокирована для множителя");
                        return Convert.ToInt32(ConvertedAmount);
                    }
                }
            
            if (config.pluginSettings.RateSetting.TypeList == TypeListUsed.BlackList)
            {
                if (IsBlackList(Shortname))
                    return Convert.ToInt32(ConvertedAmount);
            }
            else
            {
                if (!IsWhiteList(Shortname))
                {
                    //PrintToChat($"DEBUG : Предмет {Shortname} заблокирована для #4468837173 множителя");
                    return Convert.ToInt32(ConvertedAmount);
                }
            }

            var PrivilegyRates = config.pluginSettings.RateSetting.PrivilegyRates;
            Boolean IsTimes = IsTime();
            Configuration.PluginSettings.Rates.AllRates Rates = IsTimes ? config.pluginSettings.RateSetting.DayRates : config.pluginSettings.RateSetting.NightRates;
            
            UserID = player != null ? player.UserIDString : UserID;

            if (UserID != null)
            {
                var CustomRate = IsTimes ? config.pluginSettings.RateSetting.CustomRatesPermissions.DayRates : config.pluginSettings.RateSetting.CustomRatesPermissions.NightRates;
		   		 		  						  	   		  	  			  	 				  	   		  	  	
                var Rate = CustomRate.FirstOrDefault(x => IsPermission(UserID, x.Key)); //dbg
                if (Rate.Value != null)
                    foreach (var RateValue in Rate.Value.Where(x => x.Shortname == Shortname))
                    {
                        ConvertedAmount = Amount * RateValue.Rate;
                        return (int)ConvertedAmount;
                    }

                foreach (var RatesSetting in PrivilegyRates)
                    if (IsPermission(UserID, RatesSetting.Key))
                        Rates = IsTimes ? RatesSetting.Value.DayRates : RatesSetting.Value.NightRates;
            }

            Single BonusRate = GetBonusRate(player); 
		   		 		  						  	   		  	  			  	 				  	   		  	  	
            switch (RateType)
            {
                case Types.Gather:
                    {
                        ConvertedAmount = Amount * (Rates.GatherRate + BonusRate);
                        break;
                    }
                case Types.Loot:
                    {
                        ConvertedAmount = Amount * (Rates.LootRate + BonusRate);
                        break;
                    }
                case Types.PickUP:
                    {
                        ConvertedAmount = Amount * (Rates.PickUpRate + BonusRate);
                        break;
                    }
                case Types.Growable:
                    {
                        ConvertedAmount = Amount * (Rates.GrowableRate + BonusRate);
                        break;
                    }
                case Types.Quarry:
                    {
                        Single QuarryRates = GetRateQuarryDetalis(Rates, Shortname);
                        ConvertedAmount = Amount * (QuarryRates + BonusRate);
                        break;
                    }
                case Types.Excavator:
                    {
                        ConvertedAmount = Amount * (Rates.ExcavatorRate + BonusRate);
                        break;
                    }
                case Types.Fishing:
                {
                    ConvertedAmount = Amount * (Rates.FishRate + BonusRate);
                    break;
                }
            }
            return Convert.ToInt32(ConvertedAmount);
        }
        private void OnEntitySpawned(AttackHelicopter helicopter)
        {
            if (helicopter == null) return;
            FuelSystemRating(helicopter.GetFuelSystem(), config.pluginSettings.OtherSetting.FuelSetting.AmountAttackHelicopter);
            
            helicopter.fuelPerSec = config.pluginSettings.OtherSetting.FuelConsumedTransportSetting.ConsumedAttackHelicopter;
        }
        void SpawnPlane()
        {
            UnSubProSub();
		   		 		  						  	   		  	  			  	 				  	   		  	  	
            var position = new Vector3(ConVar.Server.worldsize, 100, ConVar.Server.worldsize) - new Vector3(50f, 0f, 50f);
            var entity = GameManager.server.CreateEntity(prefabPlane, position);
            entity?.Spawn();
        }
        void OnContainerDropItems(ItemContainer container)
        {
            if (container == null) return;
            var Container = container.entityOwner as LootContainer;
            if (Container == null) return;
            UInt64 NetID = Container.net.ID.Value;
            if (LootersListCrateID.Contains(NetID)) return;
            
            BasePlayer player = Container.lastAttacker as BasePlayer;

            foreach (var item in container.itemList)
                item.amount = Converted(Types.Loot, item.info.shortname, item.amount, player);
        }
        private void OnEntitySpawned(HotAirBalloon hotAirBalloon)
        {
            if (hotAirBalloon == null) return;
            hotAirBalloon.fuelPerSec = config.pluginSettings.OtherSetting.FuelConsumedTransportSetting.ConsumedHotAirBalloon;
        }
        private void StartBreadley(Configuration.PluginSettings.OtherSettings.EventSettings EventSettings)
        {
            if (SpacePort == null) return;
            if (!EventSettings.BreadlaySetting.FullOff && EventSettings.BreadlaySetting.UseEventCustom)
            {
                Int32 TimeSpawn = EventSettings.BreadlaySetting.RandomTimeSpawn.UseRandomTime ? GetRandomTime(EventSettings.BreadlaySetting.RandomTimeSpawn.MinEventSpawnTime, EventSettings.BreadlaySetting.RandomTimeSpawn.MaxEventSpawnTime) : EventSettings.BreadlaySetting.EventSpawnTime;
                timer.Once(TimeSpawn, () =>
                {
                    StartBreadley(EventSettings);
                    SpawnTank();
                });
            }
        }
        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity == null || entity.net == null) return; 
            UInt64 NetID = entity.net.ID.Value;
            if (LootersListCrateID.Contains(NetID))
                LootersListCrateID.Remove(NetID);           
        }

        void OnDispenserBonus(ResourceDispenser disp, BasePlayer player, Item item)
        {
            if (item == null || player == null) return;
            
            int Rate = Converted(Types.Gather, item.info.shortname, item.amount, player);
            item.amount = Rate;
        }

        Single GetBonusRate(BasePlayer player)
        {
            Single Bonus = 0;
            if (player == null) return Bonus;

            if (BonusRates.ContainsKey(player))
                Bonus = BonusRates[player];

            return Bonus;
        }
        private void OnEntitySpawned(ScrapTransportHelicopter helicopter)
        {
            if (helicopter == null) return;
            FuelSystemRating(helicopter.GetFuelSystem(), config.pluginSettings.OtherSetting.FuelSetting.AmountScrapTransport);
            
            helicopter.fuelPerSec = config.pluginSettings.OtherSetting.FuelConsumedTransportSetting.ConsumedScrapTransport;
        }
        
        
        public Dictionary<BasePlayer, Single> BonusRates = new Dictionary<BasePlayer, Single>();

        UInt64? GetQuarryPlayer(UInt64 NetID)
        {
            if (!DataQuarryPlayer.ContainsKey(NetID)) return null;
            if (DataQuarryPlayer[NetID] == 0) return null;
		   		 		  						  	   		  	  			  	 				  	   		  	  	
            return DataQuarryPlayer[NetID];
        }
        void WriteData() => Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQSystem/IQRates/Quarrys", DataQuarryPlayer);
        
        
        void OnFuelConsume(BaseOven oven, Item fuel, ItemModBurnable burnable)
        {
            if (oven == null) return;
            
            burnable.byproductChance = GetRareCoal(BasePlayer.FindByID(oven.OwnerID));
            if (burnable.byproductChance == 0)
                burnable.byproductChance = -1;
        }
        
        void OnQuarryGather(MiningQuarry quarry, Item item)
        {
            if (item == null || quarry == null) return;

            // PrintError(GetQuarryPlayer(quarry.net.ID + 2744688).ToString() + "   " + Converted(Types.Quarry, item.info.shortname,
            //     item.amount, null, GetQuarryPlayer(quarry.OwnerID).ToString()).ToString());
            item.amount = Converted(Types.Quarry, item.info.shortname, item.amount, null, GetQuarryPlayer(quarry.net.ID.Value).ToString());
        }

        private void GetTimeComponent()
        {
            timeComponent = TOD_Sky.Instance.Components.Time;
            if (timeComponent == null) return;
            SetTimeComponent();
            StartupFreeze();
        }
        public enum SkipType
        {
            Day,
            Night
        }
        void StartEvent()
        {
            var EventSettings = config.pluginSettings.OtherSetting.EventSetting;
            StartCargoShip(EventSettings);
            StartCargoPlane(EventSettings);
            StartBreadley(EventSettings);
            StartChinoock(EventSettings);
            StartHelicopter(EventSettings);
        }
        
        
        private void OnRecyclerToggle(Recycler recycler, BasePlayer player)
        {
            if (!recycler.IsOn())
            {
                NextTick(() =>
                {
                    if (!recycler.IsOn())
                        return;

                    Single Speed = GetSpeedRecycler(player);
                    recycler.InvokeRepeating(recycler.RecycleThink, Speed, Speed);
                });
            }
        }
        void StartupFreeze()
        {
            if (!config.pluginSettings.OtherSetting.UseFreezeTime) return;
            timeComponent.ProgressTime = false;
            ConVar.Env.time = config.pluginSettings.OtherSetting.FreezeTime;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
		   		 		  						  	   		  	  			  	 				  	   		  	  	
                if (config.pluginSettings.RateSetting.BlackListCategory == null)
                    config.pluginSettings.RateSetting.BlackListCategory = new List<String>()
                    {
                        "Weapon", 
                        "Ammunition",
                        "Traps",
                        "Attire",
                        "Items",
                        "Tools",
                        "Component"
                    };
                if (config.pluginSettings.RateSetting.WhiteList == null)
                {
                    config.pluginSettings.RateSetting.WhiteList = new List<String>()
                    {
                        "wood",
                        "sulfur.ore"
                    };
                }
                
                if (config.pluginSettings.RateSetting.RecyclersController.DefaultSpeedRecycler == 0f)
                    config.pluginSettings.RateSetting.RecyclersController.DefaultSpeedRecycler = 5f;
                if (config.pluginSettings.RateSetting.BlackListPrefabs == null ||
                    config.pluginSettings.RateSetting.BlackListPrefabs.Count == 0)
                    config.pluginSettings.RateSetting.BlackListPrefabs = new List<String>()
                    {
                        "crate_elite",
                        "crate_normal"
                    };
                    
                if (config.pluginSettings.RateSetting.RecyclersController.PrivilageSpeedRecycler == null ||
                    config.pluginSettings.RateSetting.RecyclersController.PrivilageSpeedRecycler.Count == 0)
                {
                    config.pluginSettings.RateSetting.RecyclersController.PrivilageSpeedRecycler =
                        new List<Configuration.PluginSettings.Rates.RecyclerController.PresetRecycler>()
                        {
                            new Configuration.PluginSettings.Rates.RecyclerController.PresetRecycler()
                            {
                                Permissions = "iqrates.recyclerhyperfast",
                                SpeedRecyclers = 0
                            },
                            new Configuration.PluginSettings.Rates.RecyclerController.PresetRecycler()
                            {
                                Permissions = "iqrates.recyclerfast",
                                SpeedRecyclers = 3
                            },
                        };
                }

                if (config.pluginSettings.RateSetting.DayRates.QuarryDetalis.ShortnameListQuarry.Count == 0)
                    config.pluginSettings.RateSetting.DayRates.QuarryDetalis =
                        new Configuration.PluginSettings.Rates.AllRates.QuarryRateDetalis()
                        {
                            UseDetalisRateQuarry = false,
                            ShortnameListQuarry = new Dictionary<String, Single>()
                            {
                                ["metal.ore"] = 10,
                                ["sulfur.ore"] = 5
                            }
                        };
                
                if (config.pluginSettings.RateSetting.NightRates.QuarryDetalis.ShortnameListQuarry.Count == 0)
                    config.pluginSettings.RateSetting.NightRates.QuarryDetalis =
                        new Configuration.PluginSettings.Rates.AllRates.QuarryRateDetalis()
                        {
                            UseDetalisRateQuarry = false,
                            ShortnameListQuarry = new Dictionary<String, Single>()
                            {
                                ["metal.ore"] = 10,
                                ["sulfur.ore"] = 5
                            }
                        };
            }
            catch
            {                       
                PrintWarning(LanguageEn ? "Error #334472943" + $"read configuration 'oxide/config/{Name}', create a new configuration!!" : "Ошибка #334343" + $"чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!"); 
                LoadDefaultConfig();
            }
            NextTick(SaveConfig);
        }

        
                private void FuelSystemRating(IFuelSystem FuelSystem, Int32 Amount)
        {
            if (FuelSystem == null) return;
            NextTick(() =>
            {
                var Fuel = FuelSystem.GetFuelAmount();
                if (Fuel == null) return;

                if (Fuel == 50 || Fuel == 100)
                    Fuel = Amount;
            });
        }

        void OnAirdrop(CargoPlane plane, Vector3 dropPosition) => plane.OwnerID = 999999999999;

                
                
        public Single GetMultiplaceBurnableSpeed(String ownerid)
        {
            Single Multiplace = config.pluginSettings.RateSetting.SpeedBurnable;
            if (config.pluginSettings.RateSetting.UseSpeedBurnableList)
            {
                var SpeedInList = config.pluginSettings.RateSetting.SpeedBurableList.OrderByDescending(z => z.SpeedBurnable).FirstOrDefault(x => permission.UserHasPermission(ownerid, x.Permissions));
                if (SpeedInList != null)
                    Multiplace = SpeedInList.SpeedBurnable;
            }
            return Multiplace;
        }

                private const string prefabCH47 = "assets/prefabs/npc/ch47/ch47scientists.entity.prefab";

        void SpawnHeli()
        {
            UnSubProSub();

            var position = new Vector3(ConVar.Server.worldsize, 100, ConVar.Server.worldsize) - new Vector3(50f, 0f, 50f);
            var entity = GameManager.server.CreateEntity(prefabPatrol, position);
            entity?.Spawn();
        }

        private readonly Dictionary<String, ModiferTea> TeaModifers = new Dictionary<String, ModiferTea>
        {
            ["oretea.advanced"] = new ModiferTea()
            {
                Duration = 1800f,
                Value = 0.35f,
                Type = Modifier.ModifierType.Ore_Yield
            },
            ["oretea"] = new ModiferTea()
            {
                Duration = 1800f,
                Value = 0.2f,
                Type = Modifier.ModifierType.Ore_Yield
            },
            ["oretea.pure"] = new ModiferTea()
            {
                Duration = 1800f,
                Value = 0.5f,
                Type = Modifier.ModifierType.Ore_Yield
            },
            ["woodtea.advanced"] = new ModiferTea()
            {
                Duration = 1800f,
                Value = 1.0f,
                Type = Modifier.ModifierType.Wood_Yield
            },
            ["woodtea"] = new ModiferTea()
            {
                Duration = 1800f,
                Value = 0.5f,
                Type = Modifier.ModifierType.Wood_Yield
            },
            ["woodtea.pure"] = new ModiferTea()
            {
                Duration = 1800f,
                Value = 2.0f,
                Type = Modifier.ModifierType.Wood_Yield
            },
            ["scraptea.advanced"] = new ModiferTea()
            {
                Duration = 2700f,
                Value = 2.25f,
                Type = Modifier.ModifierType.Scrap_Yield
            },
            ["scraptea"] = new ModiferTea()
            {
                Duration = 1800f,
                Value = 1.0f,
                Type = Modifier.ModifierType.Scrap_Yield
            },
            ["scraptea.pure"] = new ModiferTea()
            {
                Duration = 3600f,
                Value = 3.5f,
                Type = Modifier.ModifierType.Scrap_Yield
            },
        };
		   		 		  						  	   		  	  			  	 				  	   		  	  	
        
                public void Register(string Permissions)
        {
            if (!String.IsNullOrWhiteSpace(Permissions))
                if (!permission.PermissionExists(Permissions, this))
                    permission.RegisterPermission(Permissions, this);
        }
        
        
        private void OnEntitySpawned(SupplySignal entity) => UnSubProSub(10);
        
                
                public Dictionary<UInt64, UInt64> DataQuarryPlayer = new Dictionary<UInt64, UInt64>();
        void SpawnCargo()
        {
            UnSubProSub();
            
            var cargoShip = GameManager.server.CreateEntity(prefabShip) as CargoShip;
            if (cargoShip == null) return;
            cargoShip.TriggeredEventSpawn();
            cargoShip.SendNetworkUpdate();
            cargoShip.RefreshActiveLayout();
            cargoShip.Spawn();
        }

                private void OnEntitySpawned(BaseBoat boat)
        {
            if (boat == null) return;
            FuelSystemRating(boat.GetFuelSystem(), config.pluginSettings.OtherSetting.FuelSetting.AmountBoat);
        }

        private object OnPlayerAddModifiers(BasePlayer player, Item item, ItemModConsumable consumable)
        {
            if (!TeaModifers.ContainsKey(item.info.shortname)) return true;
            List<ModifierDefintion> mods = Pool.GetList<ModifierDefintion>();

            Dictionary<String, Configuration.PluginSettings.Rates.DayAnNightRate> PrivilegyRates =
                config.pluginSettings.RateSetting.PrivilegyRates;
            Boolean IsTimes = IsTime();
            Configuration.PluginSettings.Rates.AllRates Rates = IsTimes
                ? config.pluginSettings.RateSetting.DayRates
                : config.pluginSettings.RateSetting.NightRates;
            Configuration.PluginSettings.Rates.AllRates DefaultRates = IsTimes
                ? config.pluginSettings.RateSetting.DayRates
                : config.pluginSettings.RateSetting.NightRates;

            Single ModiferDifference = 1.0f;
            Single DefaultRate = 1.0f;
            Single PlayerRate = 1.0f;

            foreach (var RatesSetting in PrivilegyRates)
                if (IsPermission(player.UserIDString, RatesSetting.Key))
                    Rates = IsTimes ? RatesSetting.Value.DayRates : RatesSetting.Value.NightRates;
		   		 		  						  	   		  	  			  	 				  	   		  	  	
            Single BonusRate = GetBonusRate(player);

            ModiferTea TeaLocal = TeaModifers[item.info.shortname];
            
            switch (TeaLocal.Type)
            {
                case Modifier.ModifierType.Ore_Yield:
                {
                    DefaultRate = DefaultRates.GatherRate;
                    PlayerRate = Rates.GatherRate + BonusRate;

                    ModiferDifference = (PlayerRate - DefaultRate) <= 0 ? 1 : (PlayerRate - DefaultRate);
            
                    mods.Add(GetDefintionModifer(TeaLocal.Type, TeaLocal.Duration,
                        TeaLocal.Value / ModiferDifference));
                    
                    break;
                }
                case Modifier.ModifierType.Wood_Yield:
                {
                    DefaultRate = DefaultRates.GatherRate;
                    PlayerRate = Rates.GatherRate + BonusRate;

                    ModiferDifference = (PlayerRate - DefaultRate) <= 0 ? 1 : (PlayerRate - DefaultRate);

                    mods.Add(GetDefintionModifer(TeaLocal.Type, TeaLocal.Duration,
                        TeaLocal.Value / ModiferDifference));
                    
                    break;
                }
                case Modifier.ModifierType.Scrap_Yield:
                {
                    DefaultRate = DefaultRates.LootRate;
                    PlayerRate = Rates.LootRate + BonusRate;

                    ModiferDifference = (PlayerRate - DefaultRate) <= 0 ? 1 : (PlayerRate - DefaultRate);

                    mods.Add(GetDefintionModifer(TeaLocal.Type, TeaLocal.Duration,
                        TeaLocal.Value / ModiferDifference));
                    
                    break;
                }
            }
            
            player.modifiers.Add(mods);
            Pool.FreeList(ref mods);

            return true;
        }
        void SetTimeComponent()
        {
            if (!config.pluginSettings.OtherSetting.UseTime) return;

            timeComponent.ProgressTime = true;
            timeComponent.UseTimeCurve = false;
            timeComponent.OnSunrise += OnSunrise;
            timeComponent.OnSunset += OnSunset;
            timeComponent.OnHour += OnHour;

            if (TOD_Sky.Instance.Cycle.Hour > TOD_Sky.Instance.SunriseTime && TOD_Sky.Instance.Cycle.Hour < TOD_Sky.Instance.SunsetTime)
                OnSunrise();
            else
                OnSunset();
        }
        private const string prefabShip = "assets/content/vehicles/boats/cargoship/cargoshiptest.prefab";
        
                private BasePlayer ExcavatorPlayer = null;
        
                
        void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
        {
            foreach(ItemAmount item in collectible.itemList)
                item.amount = Converted(Types.PickUP, item.itemDef.shortname, (Int32)item.amount, player);
        }
        private void StartChinoock(Configuration.PluginSettings.OtherSettings.EventSettings EventSettings)
        {
            if (!EventSettings.ChinoockSetting.FullOff && EventSettings.ChinoockSetting.UseEventCustom)
            {
                Int32 TimeSpawn = EventSettings.ChinoockSetting.RandomTimeSpawn.UseRandomTime ? GetRandomTime(EventSettings.ChinoockSetting.RandomTimeSpawn.MinEventSpawnTime, EventSettings.ChinoockSetting.RandomTimeSpawn.MaxEventSpawnTime) : EventSettings.ChinoockSetting.EventSpawnTime;
                timer.Once(TimeSpawn, () =>
                {
                    StartChinoock(EventSettings);
                    SpawnCH47();
                });
            }
        }
        private const string prefabPlane = "assets/prefabs/npc/cargo plane/cargo_plane.prefab";

        private void Init() => ReadData();
        void ReadData() => DataQuarryPlayer = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<UInt64, UInt64>>("IQSystem/IQRates/Quarrys");
        Boolean activatedDay;
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["MY_RATES_INFO"] = "Your resource rating at the moment :" +
                "\n- Rating of extracted resources: <color=#FAF0F5>x{0}</color>" +
                "\n- Rating of found items: <color=#FAF0F5>х{1}</color>" +
                "\n- Rating of raised items: <color=#FAF0F5>х{2}</color>" +
                "\n- Career rankings: <color=#FAF0F5>x{3}</color>" +
                "\n- Excavator Rating: <color=#FAF0F5>x{4}</color>" +
                "\n- Rating of growable : <color=#FAF0F5>x{5}</color>",

                ["DAY_RATES_ALERT"] = "The day has come!" +
                "\nThe global rating on the server has been changed :" +
                "\n- Rating of extracted resources: <color=#FAF0F5>x{0}</color>" +
                "\n- Rating of found items: <color=#FAF0F5>х{1}</color>" +
                "\n- Rating of raised items: <color=#FAF0F5>х{2}</color>" +
                "\n- Career rankings: <color=#FAF0F5>x{3}</color>" +
                "\n- Excavator Rating: <color=#FAF0F5>x{4}</color>" +
                "\n- Rating of growable : <color=#FAF0F5>x{5}</color>",

                ["NIGHT_RATES_ALERT"] = "Night came!" +
                "\nThe global rating on the server has been changed :" +
                "\n- Rating of extracted resources: <color=#FAF0F5>x{0}</color>" +
                "\n- Rating of found items: <color=#FAF0F5>х{1}</color>" +
                "\n- Rating of raised items: <color=#FAF0F5>х{2}</color>" +
                "\n- Career rankings: <color=#FAF0F5>x{3}</color>" +
                "\n- Excavator Rating: <color=#FAF0F5>x{4}</color>" +
                "\n- Rating of growable : <color=#FAF0F5>x{5}</color>",
                
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["MY_RATES_INFO"] = "Ваш рейтинг ресурсов на данный момент :" +
                "\n- Рейтинг добываемых ресурсов: <color=#FAF0F5>x{0}</color>" +
                "\n- Рейтинг найденных предметов: <color=#FAF0F5>х{1}</color>" +
                "\n- Рейтинг поднимаемых предметов: <color=#FAF0F5>х{2}</color>" +
                "\n- Рейтинг карьеров: <color=#FAF0F5>x{3}</color>" +
                "\n- Рейтинг экскаватора: <color=#FAF0F5>x{4}</color>" +
                "\n- Рейтинг грядок : <color=#FAF0F5>x{5}</color>",

                ["DAY_RATES_ALERT"] = "Наступил день!" +
                "\nГлобальный рейтинг на сервере был изменен :" +
                "\n- Рейтинг добываемых ресурсов: <color=#FAF0F5>x{0}</color>" +
                "\n- Рейтинг найденных предметов: <color=#FAF0F5>х{1}</color>" +
                "\n- Рейтинг поднимаемых предметов: <color=#FAF0F5>х{2}</color>" +
                "\n- Рейтинг карьеров: <color=#FAF0F5>x{3}</color>" +
                "\n- Рейтинг экскаватора: <color=#FAF0F5>x{4}</color>" +
                "\n- Рейтинг грядок : <color=#FAF0F5>x{5}</color>", 
                
                ["NIGHT_RATES_ALERT"] = "Наступила ночь!" +
                "\nГлобальный рейтинг на сервере был изменен :" +
                "\n- Рейтинг добываемых ресурсов: <color=#FAF0F5>x{0}</color>" +
                "\n- Рейтинг найденных предметов: <color=#FAF0F5>х{1}</color>" +
                "\n- Рейтинг поднимаемых предметов: <color=#FAF0F5>х{2}</color>" +
                "\n- Рейтинг карьеров: <color=#FAF0F5>x{3}</color>" +
                "\n- Рейтинг экскаватора: <color=#FAF0F5>x{4}</color>" +
                "\n- Рейтинг грядок : <color=#FAF0F5>x{5}</color>",
            }, this, "ru");
        }
        private void OnEntitySpawned(CargoShip entity)
        {
            NextTick(() =>
            {
                if (entity.OwnerID != 0 || entity.skinID != 0) return;
                var EvenTimer = config.pluginSettings.OtherSetting.EventSetting.CargoShipSetting;
                if ((EvenTimer.FullOff || EvenTimer.UseEventCustom))
                    entity.Kill();
            });
        }
        
        private const Boolean LanguageEn = false;

        private void OnEntitySpawned(PatrolHelicopter entity)
        {
            NextTick(() =>
            {
                if (entity.OwnerID != 0 || entity.skinID != 0) return;
                var EvenTimer = config.pluginSettings.OtherSetting.EventSetting.HelicopterSetting;
                if ((EvenTimer.FullOff || EvenTimer.UseEventCustom))
                    entity.Kill();
            });
        }

        private void SpawnTank()
        {
            UnSubProSub();
            if (!BradleySpawner.singleton.spawned.isSpawned)
                BradleySpawner.singleton?.SpawnBradley();
        }

                
        
        object OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            BasePlayer player = entity as BasePlayer;
            if (item == null || player == null) return null;
		   		 		  						  	   		  	  			  	 				  	   		  	  	
            Int32 Rate = Converted(Types.Gather, item.info.shortname, item.amount, player);
            item.amount = Rate;
		   		 		  						  	   		  	  			  	 				  	   		  	  	
            return null;
        }
        bool IsWhiteList(string Shortname)
        {
            var WhiteList = config.pluginSettings.RateSetting.WhiteList;
            if (WhiteList.Contains(Shortname))
                return true;
            else return false;
        }      
		   		 		  						  	   		  	  			  	 				  	   		  	  	
        void OnSunset()
        {
            timeComponent.DayLengthInMinutes = config.pluginSettings.OtherSetting.NightTime * (24.0f / (24.0f - (TOD_Sky.Instance.SunsetTime - TOD_Sky.Instance.SunriseTime)));
            activatedDay = false;
            if (config.pluginSettings.OtherSetting.UseSkipTime)
            {
                if (config.pluginSettings.OtherSetting.TypeSkipped == SkipType.Night)
                    TOD_Sky.Instance.Cycle.Hour = config.pluginSettings.OtherSetting.DayStart;
                else
                {
                    if (config.pluginSettings.OtherSetting.UseAlertDayNight)
                    {
                        Configuration.PluginSettings.Rates.AllRates Rate = config.pluginSettings.RateSetting.NightRates;
                        foreach (BasePlayer player in BasePlayer.activePlayerList)
                            SendChat(GetLang("NIGHT_RATES_ALERT", player.UserIDString, Rate.GatherRate, Rate.LootRate, Rate.PickUpRate, Rate.QuarryRate, Rate.ExcavatorRate, Rate.GrowableRate), player);
                    }
                }
                return;
            }
            if (config.pluginSettings.OtherSetting.UseAlertDayNight)
            {
                Configuration.PluginSettings.Rates.AllRates Rate = config.pluginSettings.RateSetting.NightRates;
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                    SendChat(GetLang("NIGHT_RATES_ALERT", player.UserIDString, Rate.GatherRate, Rate.LootRate, Rate.PickUpRate, Rate.QuarryRate, Rate.ExcavatorRate, Rate.GrowableRate), player);
            }
        }
        int API_CONVERT_GATHER(string Shortname, float Amount, BasePlayer player = null) => Converted(Types.Gather, Shortname, Amount, player);
        private void OnEntitySpawned(CH47Helicopter entity)
        {
            NextTick(() =>
            {
                if (entity.OwnerID != 0 || entity.skinID != 0) return;
                timer.Once(3f, () =>
                {
                    var EvenTimer = config.pluginSettings.OtherSetting.EventSetting.ChinoockSetting;
                    if ((EvenTimer.FullOff || EvenTimer.UseEventCustom) && entity.mountPoints.Where(x => x.mountable.GetMounted() != null && x.mountable.GetMounted().ShortPrefabName.Contains("scientistnpc_heavy")).Count() <= 0)
                        timer.Once(1f, () => { entity.Kill();});
                });
            });
        }   
        private const string prefabPatrol = "assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab";
        private void StartCargoShip(Configuration.PluginSettings.OtherSettings.EventSettings EventSettings)
        {
            if (!EventSettings.CargoShipSetting.FullOff && EventSettings.CargoShipSetting.UseEventCustom)
            {
                Int32 TimeSpawn = EventSettings.CargoShipSetting.RandomTimeSpawn.UseRandomTime ? GetRandomTime(EventSettings.CargoShipSetting.RandomTimeSpawn.MinEventSpawnTime, EventSettings.CargoShipSetting.RandomTimeSpawn.MaxEventSpawnTime) : EventSettings.CargoShipSetting.EventSpawnTime;
                timer.Once(TimeSpawn, () =>
                {
                    StartCargoShip(EventSettings);
                    SpawnCargo();
                });
            }
        }
        protected override void SaveConfig() => Config.WriteObject(config);
        
        
        
        private IEnumerator InitializeTransport()
        {
            foreach (BaseNetworkable entity in BaseNetworkable.serverEntities.Where(e => e is BaseVehicle)) 
            {
                if (entity is MotorRowboat)
                    OnEntitySpawned((MotorRowboat)entity);
                if(entity is Snowmobile)
                    OnEntitySpawned((Snowmobile)entity);
                if(entity is HotAirBalloon)
                    OnEntitySpawned((HotAirBalloon)entity);
                if(entity is RHIB)
                    OnEntitySpawned((RHIB)entity);
                if(entity is BaseSubmarine)
                    OnEntitySpawned((BaseSubmarine)entity);
                if(entity is Minicopter)
                    OnEntitySpawned((Minicopter)entity);
                if(entity is ScrapTransportHelicopter)
                    OnEntitySpawned((ScrapTransportHelicopter)entity);
                
                yield return CoroutineEx.waitForSeconds(0.03f); 
            }
        }

        void API_BONUS_RATE_ADDPLAYER(UInt64 userID, Single Rate)
        {
            BasePlayer player = BasePlayer.FindByID(userID);
            API_BONUS_RATE_ADDPLAYER(player, Rate);
        }
        
        
        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (entity == null) return;

            if(config.pluginSettings.RateSetting.UseBlackListPrefabs)
                if (config.pluginSettings.RateSetting.BlackListPrefabs.Contains(entity.ShortPrefabName))
                    return;
            
            LootContainer container = entity as LootContainer;

            if (entity.net == null) return;
            UInt64 NetID = entity.net.ID.Value;
            if (LootersListCrateID.Contains(NetID)) return;

            if (container == null)
            {
                if (!(entity is NPCPlayerCorpse)) return;
                
                NPCPlayerCorpse corpse = (NPCPlayerCorpse)entity;
                foreach (ItemContainer corpseContainer in corpse.containers)
                {
                    foreach (Item item in corpseContainer.itemList)
                        item.amount = Converted(Types.Loot, item.info.shortname, item.amount, player);
                }
            }
            else
            {
                foreach (Item item in container.inventory.itemList)
                    item.amount = Converted(Types.Loot, item.info.shortname, item.amount, player);
            }
            
            LootersListCrateID.Add(NetID);
        }
        
        
        bool IsBlackList(string Shortname)
        {
            var BlackList = config.pluginSettings.RateSetting.BlackList;
            if (BlackList.Contains(Shortname))
                return true;
            else return false;
        }  
        private void OnEntitySpawned(RHIB boat)
        {
            if (boat == null) return;
            boat.fuelPerSec = config.pluginSettings.OtherSetting.FuelConsumedTransportSetting.ConsumedBoat;
        }    

        private Int32 GetRandomTime(Int32 Min, Int32 Max) => UnityEngine.Random.Range(Min, Max);
        private void OnEntitySpawned(BradleyAPC entity)
        {
            NextTick(() =>
            {
                if (entity.OwnerID != 0 || entity.skinID != 0) return;
                var EvenTimer = config.pluginSettings.OtherSetting.EventSetting.BreadlaySetting;
                if ((EvenTimer.FullOff || EvenTimer.UseEventCustom))
                    entity.Kill();
            });
        }
                
        void OnQuarryToggled(MiningQuarry quarry, BasePlayer player)
        {
            AddQuarryPlayer(quarry.net.ID.Value, player.userID);
        }
        public enum TypeListUsed
        {
            WhiteList,
            BlackList
        }
        private object OnOvenToggle(BaseOven oven, BasePlayer player)
        {
            if (config.pluginSettings.RateSetting.IgnoreSpeedBurnablePrefabList.Contains(oven.ShortPrefabName))
                return null;
            
            return OvenController.GetOrAdd(oven).Switch(player);
        }
        public List<UInt64> LootersListCrateID = new List<UInt64>();

        
                private void Unload()
        {
            OvenController.KillAll();
            if (timeComponent == null) return;
            timeComponent.OnSunrise -= OnSunrise;
            timeComponent.OnSunset -= OnSunset;
            timeComponent.OnHour -= OnHour;

            if (initializeTransport != null)
            {
                ServerMgr.Instance.StopCoroutine(initializeTransport);
                initializeTransport = null;
            }
        }
        private void StartHelicopter(Configuration.PluginSettings.OtherSettings.EventSettings EventSettings)
        {
            if (!EventSettings.HelicopterSetting.FullOff && EventSettings.HelicopterSetting.UseEventCustom)
            {
                Int32 TimeSpawn = EventSettings.HelicopterSetting.RandomTimeSpawn.UseRandomTime ? GetRandomTime(EventSettings.HelicopterSetting.RandomTimeSpawn.MinEventSpawnTime, EventSettings.HelicopterSetting.RandomTimeSpawn.MaxEventSpawnTime) : EventSettings.HelicopterSetting.EventSpawnTime;
                timer.Once(TimeSpawn, () => 
                {
                    StartHelicopter(EventSettings);
                    SpawnHeli();
                });
            }
        }
        void SpawnCH47()
        {
            UnSubProSub();

            var position = new Vector3(ConVar.Server.worldsize, 100, ConVar.Server.worldsize) - new Vector3(50f, 0f, 50f);
            var entity = GameManager.server.CreateEntity(prefabCH47, position) as CH47HelicopterAIController;
            entity?.TriggeredEventSpawn();
            entity?.Spawn();
        }
        
        void OnGrowableGathered(GrowableEntity plant, Item item, BasePlayer player)
        {
            if (item == null || player == null) return;
            item.amount = Converted(Types.Growable, item.info.shortname, item.amount, player);
        }
        float GetRareCoal(BasePlayer player = null)
        {
            Boolean IsTimes = IsTime();

            var Rates = IsTimes ? config.pluginSettings.RateSetting.DayRates : config.pluginSettings.RateSetting.NightRates;
            var PrivilegyRates = config.pluginSettings.RateSetting.PrivilegyRates;

            if (player != null)
            {
                foreach (var RatesSetting in PrivilegyRates)
                    if (IsPermission(player.UserIDString, RatesSetting.Key))
                        Rates = IsTimes ? RatesSetting.Value.DayRates : RatesSetting.Value.NightRates;
            }

            float Rare = Rates.CoalRare;
            float RareResult = (100 - Rare) / 100;
            return RareResult;
        }
        private class Configuration
        {

            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    pluginSettings = new PluginSettings
                    {
                        ReferenceSettings = new PluginSettings.ReferencePlugin
                        {
                            IQChatSetting = new PluginSettings.ReferencePlugin.IQChatReference
                            {
                                CustomAvatar = "0",
                                CustomPrefix = "[IQRates]",
                                UIAlertUse = false,
                            },
                        },
                        RateSetting = new PluginSettings.Rates
                        {
                            UseTeaController = false,
                            UseBlackListPrefabs = false,
                            BlackListCategory = new List<String>()
                            {
                                "Weapon", 
                                "Ammunition",
                                "Traps",
                                "Attire",
                                "Items",
                                "Tools",
                                "Component"
                            },
                            WhiteList = new List<String>()
                            {
                                "scrap",
                                "rope",
                                "metalblade",
                                "propanetank",
                                "tarp",
                                "sewingkit",
                                "fuse",
                                "metalspring",
                                "roadsigns",
                                "sheetmetal",
                                "gears",
                                "riflebody",
                                "smgbody",
                                "semibody",
                            },
                            TypeList = TypeListUsed.BlackList,
                            BlackListPrefabs = new List<String>()
                            {
                                "crate_elite",
                                "crate_normal"
                            },
                            UseSpeedBurnable = true,
                            SpeedBurnable = 3.5f,
                            SpeedFuelBurnable = 1,
                            UseBlackListBurnable = false,
                            BlackListBurnable = new List<String>
                            {
                                "wolfmeat.cooked",
                                "deermeat.cooked",
                                "meat.pork.cooked",
                                "humanmeat.cooked",
                                "chicken.cooked",
                                "bearmeat.cooked",
                                "horsemeat.cooked",
                            },
                            RecyclersController = new PluginSettings.Rates.RecyclerController
                            {
                                UseRecyclerSpeed = false,
                                DefaultSpeedRecycler = 5,
                                PrivilageSpeedRecycler = new List<PluginSettings.Rates.RecyclerController.PresetRecycler>
                                {
                                    new PluginSettings.Rates.RecyclerController.PresetRecycler 
                                    {
                                        Permissions = "iqrates.recyclerhyperfast",
                                        SpeedRecyclers = 0 
                                    },
                                   new PluginSettings.Rates.RecyclerController.PresetRecycler
                                   {
                                       Permissions = "iqrates.recyclerfast",
                                       SpeedRecyclers = 3
                                   },
                                },
                            },
                            UseSpeedBurnableList = true,
                            IgnoreSpeedBurnablePrefabList = new List<String>()
                            {
                               "",  
                            },
                            SpeedBurableList = new List<PluginSettings.Rates.SpeedBurnablePreset>
                            {
                                new PluginSettings.Rates.SpeedBurnablePreset
                                {
                                    Permissions = "iqrates.vip",
                                    SpeedBurnable = 5.0f,
                                    SpeedFuelBurnable = 20,
                                },
                                new PluginSettings.Rates.SpeedBurnablePreset
                                {
                                    Permissions = "iqrates.speedrun",
                                    SpeedBurnable = 55.0f,
                                    SpeedFuelBurnable = 20,
                                },
                                new PluginSettings.Rates.SpeedBurnablePreset
                                {
                                    Permissions = "iqrates.fuck",
                                    SpeedBurnable = 200f,
                                    SpeedFuelBurnable = 20,
                                },
                            },
                            DayRates = new PluginSettings.Rates.AllRates
                            {
                                GatherRate = 1.0f,
                                LootRate = 1.0f,
                                PickUpRate = 1.0f,
                                GrowableRate = 1.0f,
                                QuarryRate = 1.0f,
                                FishRate = 1.0f,
                                QuarryDetalis = new PluginSettings.Rates.AllRates.QuarryRateDetalis
                                {
                                    UseDetalisRateQuarry = false,
                                    ShortnameListQuarry = new Dictionary<String, Single>()
                                    {
                                        ["metal.ore"] = 10,
                                        ["sulfur.ore"] = 5
                                    }
                                },
                                ExcavatorRate = 1.0f,
                                CoalRare = 10,
                            },
                            NightRates = new PluginSettings.Rates.AllRates
                            {
                                GatherRate = 2.0f,
                                LootRate = 2.0f,
                                PickUpRate = 2.0f,
                                GrowableRate = 2.0f,
                                QuarryRate = 2.0f,
                                FishRate = 2.0f,
                                QuarryDetalis = new PluginSettings.Rates.AllRates.QuarryRateDetalis
                                {
                                    UseDetalisRateQuarry = false,
                                    ShortnameListQuarry = new Dictionary<String, Single>()
                                    {
                                        ["metal.ore"] = 10,
                                        ["sulfur.ore"] = 5
                                    }
                                },
                                ExcavatorRate = 2.0f,
                                CoalRare = 15,
                            },
                            CustomRatesPermissions = new PluginSettings.Rates.PermissionsRate
                            {
                                DayRates = new Dictionary<String, List<PluginSettings.Rates.PermissionsRate.PermissionsRateDetalis>>
                                {
                                    ["iqrates.gg"] = new List<PluginSettings.Rates.PermissionsRate.PermissionsRateDetalis>
                                    {
                                        new PluginSettings.Rates.PermissionsRate.PermissionsRateDetalis
                                        {
                                            Rate = 200.0f,
                                            Shortname = "wood",
                                        },
                                        new PluginSettings.Rates.PermissionsRate.PermissionsRateDetalis
                                        {
                                              Rate = 200.0f,
                                              Shortname = "stones",
                                        }
                                    }
                                },
                                NightRates = new Dictionary<string, List<PluginSettings.Rates.PermissionsRate.PermissionsRateDetalis>>
                                {
                                    ["iqrates.gg"] = new List<PluginSettings.Rates.PermissionsRate.PermissionsRateDetalis>
                                    {
                                        new PluginSettings.Rates.PermissionsRate.PermissionsRateDetalis
                                        {
                                            Rate = 400.0f,
                                            Shortname = "wood",
                                        },
                                        new PluginSettings.Rates.PermissionsRate.PermissionsRateDetalis
                                        {
                                              Rate = 400.0f,
                                              Shortname = "stones",
                                        }
                                    }
                                },
                            },
                            PrivilegyRates = new Dictionary<string, PluginSettings.Rates.DayAnNightRate>
                            {
                                ["iqrates.vip"] = new PluginSettings.Rates.DayAnNightRate
                                {
                                    DayRates =
                                    {
                                        GatherRate = 3.0f,
                                        LootRate = 3.0f,
                                        PickUpRate = 3.0f,
                                        QuarryRate = 3.0f,
                                        FishRate = 3.0f,
                                        QuarryDetalis = new PluginSettings.Rates.AllRates.QuarryRateDetalis
                                        {
                                            UseDetalisRateQuarry = false,
                                            ShortnameListQuarry = new Dictionary<String, Single>()
                                            {
                                                ["metal.ore"] = 10,
                                                ["sulfur.ore"] = 5
                                            }
                                        },
                                        GrowableRate = 3.0f,
                                        ExcavatorRate = 3.0f,
                                        CoalRare = 15,
                                    },
                                    NightRates = new PluginSettings.Rates.AllRates
                                    {
                                        GatherRate = 13.0f,
                                        LootRate = 13.0f,
                                        PickUpRate = 13.0f,
                                        GrowableRate = 13.0f,
                                        QuarryRate = 13.0f,
                                        FishRate = 13.0f,
                                        QuarryDetalis = new PluginSettings.Rates.AllRates.QuarryRateDetalis
                                        {
                                            UseDetalisRateQuarry = false,
                                            ShortnameListQuarry = new Dictionary<String, Single>()
                                            {
                                                ["metal.ore"] = 10,
                                                ["sulfur.ore"] = 5
                                            }
                                        },
                                        ExcavatorRate = 13.0f,
                                        CoalRare = 25,
                                    }
                                },
                                ["iqrates.premium"] = new PluginSettings.Rates.DayAnNightRate
                                {
                                    DayRates =
                                    {
                                        GatherRate = 3.5f,
                                        LootRate = 3.5f,
                                        PickUpRate = 3.5f,
                                        GrowableRate = 3.5f,
                                        QuarryRate = 3.5f,
                                        FishRate = 3.5f,
                                        QuarryDetalis = new PluginSettings.Rates.AllRates.QuarryRateDetalis
                                        {
                                            UseDetalisRateQuarry = false,
                                            ShortnameListQuarry = new Dictionary<String, Single>()
                                            {
                                                ["metal.ore"] = 10,
                                                ["sulfur.ore"] = 5
                                            }
                                        },
                                        ExcavatorRate = 3.5f,
                                        CoalRare = 20,
                                    },
                                    NightRates = new PluginSettings.Rates.AllRates
                                    {
                                        GatherRate = 13.5f,
                                        LootRate = 13.5f,
                                        PickUpRate = 13.5f,
                                        GrowableRate = 13.5f,
                                        QuarryRate = 13.5f,
                                        FishRate = 13.5f,
                                        QuarryDetalis = new PluginSettings.Rates.AllRates.QuarryRateDetalis
                                        {
                                            UseDetalisRateQuarry = false,
                                            ShortnameListQuarry = new Dictionary<String, Single>()
                                            {
                                                ["metal.ore"] = 10,
                                                ["sulfur.ore"] = 5
                                            }
                                        },
                                        ExcavatorRate = 13.5f,
                                        CoalRare = 20,
                                    }
                                },
                            },
                            BlackList = new List<String>
                            {
                                "sulfur.ore",
                            },
                        },
                        OtherSetting = new PluginSettings.OtherSettings
                        {
                            UseAlertDayNight = true,
                            UseSkipTime = true,
                            TypeSkipped = SkipType.Night,
                            UseTime = false,
                            FreezeTime = 12,
                            UseFreezeTime = true,
                            DayStart = 10,
                            NightStart = 22,
                            DayTime = 5,
                            NightTime = 1,
                            FuelConsumedTransportSetting = new PluginSettings.OtherSettings.FuelConsumedTransport
                            {
                                ConsumedHotAirBalloon = 0.25f,
                                ConsumedSnowmobile = 0.15f,
                                ConsumedTrain = 0.15f,
                                ConsumedBoat = 0.25f,
                                ConsumedSubmarine = 0.15f,
                                ConsumedCopter = 0.25f,
                                ConsumedScrapTransport = 0.25f,
                                ConsumedAttackHelicopter = 0.25f,
                            },
                            FuelSetting = new PluginSettings.OtherSettings.FuelSettings
                            {
                                AmountBoat = 200,
                                AmountMinicopter = 200,
                                AmountScrapTransport = 200,
                                AmountSubmarine = 200,
                                AmountAttackHelicopter = 200,
                            },
                            EventSetting = new PluginSettings.OtherSettings.EventSettings
                            {
                                BreadlaySetting = new PluginSettings.OtherSettings.EventSettings.Setting
                                {
                                    FullOff = false,
                                    UseEventCustom = true,
                                    EventSpawnTime = 3000,
                                    RandomTimeSpawn = new PluginSettings.OtherSettings.EventSettings.Setting.RandomingTime
                                    {
                                        UseRandomTime = false,
                                        MaxEventSpawnTime = 3000,
                                        MinEventSpawnTime = 1000,
                                    },
                                },
                                CargoPlaneSetting = new PluginSettings.OtherSettings.EventSettings.Setting
                                {
                                    FullOff = false,
                                    UseEventCustom = true,
                                    EventSpawnTime = 5000,
                                    RandomTimeSpawn = new PluginSettings.OtherSettings.EventSettings.Setting.RandomingTime
                                    {
                                        UseRandomTime = false,
                                        MaxEventSpawnTime = 3000,
                                        MinEventSpawnTime = 1000,
                                    },
                                },
                                CargoShipSetting = new PluginSettings.OtherSettings.EventSettings.Setting
                                {
                                    FullOff = false,
                                    UseEventCustom = true,
                                    EventSpawnTime = 0,
                                    RandomTimeSpawn = new PluginSettings.OtherSettings.EventSettings.Setting.RandomingTime
                                    {
                                        UseRandomTime = true,
                                        MaxEventSpawnTime = 3000,
                                        MinEventSpawnTime = 8000,
                                    },
                                },
                                ChinoockSetting = new PluginSettings.OtherSettings.EventSettings.Setting
                                {
                                    FullOff = true,
                                    UseEventCustom = false,
                                    EventSpawnTime = 3000,
                                    RandomTimeSpawn = new PluginSettings.OtherSettings.EventSettings.Setting.RandomingTime
                                    {
                                        UseRandomTime = false,
                                        MaxEventSpawnTime = 3000,
                                        MinEventSpawnTime = 1000,
                                    },
                                },
                                HelicopterSetting = new PluginSettings.OtherSettings.EventSettings.Setting
                                {
                                    FullOff = true,
                                    UseEventCustom = false,
                                    EventSpawnTime = 3000,
                                    RandomTimeSpawn = new PluginSettings.OtherSettings.EventSettings.Setting.RandomingTime
                                    {
                                        UseRandomTime = false,
                                        MaxEventSpawnTime = 3000,
                                        MinEventSpawnTime = 1000,
                                    },
                                },
                            }
                        },
                    }
                };
            }
            [JsonProperty(LanguageEn ? "Plugin setup" : "Настройка плагина")]
            public PluginSettings pluginSettings = new PluginSettings();

            internal class PluginSettings
            {
                [JsonProperty(LanguageEn ? "Configuring supported plugins" : "Настройка поддерживаемых плагинов")]
                public ReferencePlugin ReferenceSettings = new ReferencePlugin();
                internal class OtherSettings
                {
                    [JsonProperty(LanguageEn ? "Event settings on the server" : "Настройки ивентов на сервере")]
                    public EventSettings EventSetting = new EventSettings();   
                    [JsonProperty(LanguageEn ? "Fuel settings when buying vehicles from NPCs" : "Настройки топлива при покупке транспорта у NPC")]
                    public FuelSettings FuelSetting = new FuelSettings();
                    [JsonProperty(LanguageEn ? "Fuel Consumption Rating Settings" : "Настройки рейтинга потребления топлива")]
                    public FuelConsumedTransport FuelConsumedTransportSetting = new FuelConsumedTransport();
                    internal class FuelConsumedTransport
                    {
                        [JsonProperty(LanguageEn ? "Hotairballoon fuel consumption rating (Default = 0.25)" : "Рейтинг потребление топлива у воздушного шара (Стандартно = 0.25)")]
                        public Single ConsumedHotAirBalloon= 0.25f;
                        [JsonProperty(LanguageEn ? "Snowmobile fuel consumption rating (Default = 0.15)" : "Рейтинг потребление топлива снегоходов (Стандартно = 0.15)")]
                        public Single ConsumedSnowmobile = 0.15f;         
                        [JsonProperty(LanguageEn ? "Train fuel consumption rating (Default = 0.15)" : "Рейтинг потребление топлива поездов (Стандартно = 0.15)")]
                        public Single ConsumedTrain = 0.15f;
                        [JsonProperty(LanguageEn ? "Rowboat fuel consumption rating (Default = 0.25)" : "Рейтинг потребление топлива у лодок (Стандартно = 0.25)")]
                        public Single ConsumedBoat = 0.25f;
                        [JsonProperty(LanguageEn ? "Submarine fuel consumption rating (Default = 0.15)" : "Рейтинг потребление топлива у субмарин (Стандартно = 0.15)")]
                        public Single ConsumedSubmarine = 0.15f;
                        [JsonProperty(LanguageEn ? "Minicopter fuel consumption rating (Default = 0.25)" : "Рейтинг потребление топлива у миникоптера (Стандартно = 0.25)")]
                        public Single ConsumedCopter = 0.25f;
                        [JsonProperty(LanguageEn ? "ScrapTransportHelicopter fuel consumption rating (Default = 0.25)" : "Рейтинг потребление топлива у коровы (Стандартно = 0.25)")]
                        public Single ConsumedScrapTransport = 0.25f;
                        [JsonProperty(LanguageEn ? "Attack-Helicopter fuel consumption rating (Default = 0.25)" : "Рейтинг потребление топлива у боевого-вертолета (Стандартно = 0.25)")]
                        public Single ConsumedAttackHelicopter = 0.25f;
                    }
                    internal class FuelSettings
                    {
                        [JsonProperty(LanguageEn ? "Amount of fuel for boats" : "Кол-во топлива у лодок")]
                        public Int32 AmountBoat = 200;
                        [JsonProperty(LanguageEn ? "The amount of fuel in submarines" : "Кол-во топлива у подводных лодок")]
                        public Int32 AmountSubmarine = 200;
                        [JsonProperty(LanguageEn ? "Minicopter fuel quantity" : "Кол-во топлива у миникоптера")]
                        public Int32 AmountMinicopter = 200;
                        [JsonProperty(LanguageEn ? "Helicopter fuel quantity" : "Кол-во топлива у вертолета")]
                        public Int32 AmountScrapTransport = 200;
                        [JsonProperty(LanguageEn ? "Attack-Helicopter fuel quantity" : "Кол-во топлива у боевого вертолета")]
                        public Int32 AmountAttackHelicopter = 200;
                    }
		   		 		  						  	   		  	  			  	 				  	   		  	  	
                    [JsonProperty(LanguageEn ? "Use Time Acceleration" : "Использовать ускорение времени")]
                    public Boolean UseTime;
                    [JsonProperty(LanguageEn ? "Use time freeze (the time will be the one you set in the item &lt;Frozen time on the server&gt;)" : "Использовать заморозку времени(время будет такое, какое вы установите в пунке <Замороженное время на сервере>)")]
                    public Boolean UseFreezeTime;
                    [JsonProperty(LanguageEn ? "Frozen time on the server (Set time that will not change and be forever on the server, must be true on &lt;Use time freeze&gt;" : "Замороженное время на сервере (Установите время, которое не будет изменяться и будет вечно на сервере, должен быть true на <Использовать заморозку времени>")]
                    public Int32 FreezeTime;
                    [JsonProperty(LanguageEn ? "What time will the day start?" : "Укажите во сколько будет начинаться день")]
                    public Int32 DayStart;
                    [JsonProperty(LanguageEn ? "What time will the night start?" : "Укажите во сколько будет начинаться ночь")]
                    public Int32 NightStart;
                    [JsonProperty(LanguageEn ? "Specify how long the day will be in minutes" : "Укажите сколько будет длится день в минутах")]
                    public Int32 DayTime;
                    [JsonProperty(LanguageEn ? "Specify how long the night will last in minutes" : "Укажите сколько будет длится ночь в минутах")]
                    public Int32 NightTime;

                    [JsonProperty(LanguageEn ? "Use notification of players about the change of day and night (switching rates. The message is configured in the lang)" : "Использовать уведомление игроков о смене дня и ночи (переключение рейтов. Сообщение настраивается в лэнге)")]
                    public Boolean UseAlertDayNight = true;
                    [JsonProperty(LanguageEn ? "Enable the ability to completely skip the time of day (selected in the paragraph below)" : "Включить возможность полного пропуска времени суток(выбирается в пункте ниже)")]
                    public Boolean UseSkipTime = true;
                    [JsonProperty(LanguageEn ? "Select the type of time-of-day skip (0 - Skip day, 1 - Skip night)" : "Выберите тип пропуска времени суток (0 - Пропускать день, 1 - Пропускать ночь)(Не забудьте включить возможность полного пропуска времени суток)")]
                    public SkipType TypeSkipped = SkipType.Night;

                    internal class EventSettings
                    {
                        [JsonProperty(LanguageEn ? "Helicopter spawn custom settings" : "Кастомные настройки спавна вертолета")]
                        public Setting HelicopterSetting = new Setting();
                        [JsonProperty(LanguageEn ? "Custom tank spawn settings" : "Кастомные настройки спавна танка")]
                        public Setting BreadlaySetting = new Setting();
                        [JsonProperty(LanguageEn ? "Custom ship spawn settings" : "Кастомные настройки спавна корабля")]
                        public Setting CargoShipSetting = new Setting();
                        [JsonProperty(LanguageEn ? "Airdrop spawn custom settings" : "Кастомные настройки спавна аирдропа")]
                        public Setting CargoPlaneSetting = new Setting();
                        [JsonProperty(LanguageEn ? "Chinook custom spawn settings" : "Кастомные настройки спавна чинука")]
                        public Setting ChinoockSetting = new Setting();
                        internal class Setting
                        {
                            [JsonProperty(LanguageEn ? "Completely disable event spawning on the server (true - yes/false - no)" : "Полностью отключить спавн ивента на сервере(true - да/false - нет)")]
                            public Boolean FullOff;
                            [JsonProperty(LanguageEn ? "Enable custom spawn event (true - yes/false - no)" : "Включить кастомный спавн ивент(true - да/false - нет)")]
                            public Boolean UseEventCustom;
                            [JsonProperty(LanguageEn ? "Static event spawn time" : "Статическое время спавна ивента")]
                            public Int32 EventSpawnTime;
                            [JsonProperty(LanguageEn ? "Random spawn time settings" : "Настройки случайного времени спавна")]
                            public RandomingTime RandomTimeSpawn = new RandomingTime();
                            internal class RandomingTime
                            {
                                [JsonProperty(LanguageEn ? "Use random event spawn time (static time will not be taken into account) (true - yes/false - no)" : "Использовать случайное время спавно ивента(статическое время не будет учитываться)(true - да/false - нет)")]
                                public Boolean UseRandomTime;
                                [JsonProperty(LanguageEn ? "Minimum event spawn value" : "Минимальное значение спавна ивента")]
                                public Int32 MinEventSpawnTime;
                                [JsonProperty(LanguageEn ? "Max event spawn value" : "Максимальное значении спавна ивента")]
                                public Int32 MaxEventSpawnTime;
                            }
                        }
                    }
                }

                internal class ReferencePlugin
                {
                    internal class IQChatReference
                    {
                        [JsonProperty(LanguageEn ? "IQChat : Custom chat avatar (If required)" : "IQChat : Кастомный аватар в чате(Если требуется)")]
                        public String CustomAvatar = "0";
                        [JsonProperty(LanguageEn ? "IQChat : Custom prefix in chat" : "IQChat : Кастомный префикс в чате")]
                        public String CustomPrefix = "[IQRates]";
                        [JsonProperty(LanguageEn ? "IQChat : Use UI Notifications" : "IQChat : Использовать UI уведомления")]
                        public Boolean UIAlertUse = false;
                    }
                    [JsonProperty(LanguageEn ? "Setting up IQChat" : "Настройка IQChat")]
                    public IQChatReference IQChatSetting = new IQChatReference();
                }

                internal class Rates
                {
                    [JsonProperty(LanguageEn ? "Ranking setting during the day" : "Настройка рейтинга днем")]
                    public AllRates DayRates = new AllRates();
                    [JsonProperty(LanguageEn ? "Setting the rating at night" : "Настройка рейтинга ночью")]
                    public AllRates NightRates = new AllRates();
                    [JsonProperty(LanguageEn ? "Setting privileges and ratings specifically for them [iqrates.vip] = { Setting } (Descending)" : "Настройка привилегий и рейтингов конкретно для них [iqrates.vip] = { Настройка } (По убыванию)")]
                    public Dictionary<String, DayAnNightRate> PrivilegyRates = new Dictionary<String, DayAnNightRate>();

                    [JsonProperty(LanguageEn ? "Setting custom rates (items) by permission - setting (Descending)" : "Настройка кастомных рейтов(предметов) по пермишенсу - настройка (По убыванию)")]
                    public PermissionsRate CustomRatesPermissions = new PermissionsRate();
		   		 		  						  	   		  	  			  	 				  	   		  	  	
                    [JsonProperty(LanguageEn ? "Select the type of sheet to use: 0 - White sheet, 1 - Black sheet (White sheet - the ratings of only those items that are in it increase, the rest are ignored | The Black sheet is completely the opposite)"
                                             : "Выберите тип используемого листа : 0 - Белый лист, 1 - Черный лист (Белый лист - увеличиваются рейтинги только тех предметов - которые в нем, остальные игнорируются | Черный лист полностью наоборот)")]
                    public TypeListUsed TypeList = TypeListUsed.BlackList;
                    [JsonProperty(LanguageEn ? "Black list of items that will not be categorically affected by the rating" : "Черный лист предметов,на которые катигорично не будут действовать рейтинг")]
                    public List<String> BlackList = new List<String>();
                    [JsonProperty(LanguageEn ? "A white list of items that will ONLY be affected by ratings - the rest is ignored" : "Белый лист предметов, на которые ТОЛЬКО будут действовать рейтинги - остальное игнорируются")]
                    public List<String> WhiteList = new List<String>();
                    [JsonProperty(LanguageEn ? "A blacklist of categories that will NOT be affected by ratings" : "Черный список категорий на которые НЕ БУДУТ действовать рейтинги")]
                    public List<String> BlackListCategory = new List<String>();
		   		 		  						  	   		  	  			  	 				  	   		  	  	
                    [JsonProperty(LanguageEn ? "Use a tea controller? (Works on scrap, ore and wood tea). If enabled, it will set % to production due to the difference between rates (standard / privileges)" : "Использовать контроллер чая? (Работае на скрап, рудный и древесный чай). Если включено - будет устанавливать % к добычи за счет разницы между рейтами (стандартный / привилегии)")]
                    public Boolean UseTeaController;
                    [JsonProperty(LanguageEn ? "Use a prefabs blacklist?" : "Использовать черный лист префабов?")]
                    public Boolean UseBlackListPrefabs;
                    [JsonProperty(LanguageEn ? "Black list of prefabs(ShortPrefabName) - which will not be affected by ratings" : "Черный лист префабов(ShortPrefabName) - на которые не будут действовать рейтинги")]
                    public List<String> BlackListPrefabs = new List<String>();
                    
                    [JsonProperty(LanguageEn ? "Enable melting speed in furnaces (true - yes/false - no)" : "Включить скорость плавки в печах(true - да/false - нет)")]
                    public Boolean UseSpeedBurnable;
                    [JsonProperty(LanguageEn ? "Smelting Fuel Usage Rating (If the list is enabled, this value will be the default value for all non-licensed)" : "Рейтинг использования топлива при переплавки(Если включен список - это значение будет стандартное для всех у кого нет прав)")]
                    public Int32 SpeedFuelBurnable = 1;
                    [JsonProperty(LanguageEn ? "Use a blacklist of items for melting?" : "Использовать черный список предметов для плавки?")]
                    public Boolean UseBlackListBurnable = false;
                    [JsonProperty(LanguageEn ? "A black list of items for the stove, which will not be categorically affected by melting" : "Черный лист предметов для печки,на которые катигорично не будут действовать плавка")]
                    public List<String> BlackListBurnable = new List<String>();
                    [JsonProperty(LanguageEn ? "Furnace smelting speed (If the list is enabled, this value will be the default for everyone who does not have rights)" : "Скорость плавки печей(Если включен список - это значение будет стандартное для всех у кого нет прав)")]
                    public Single SpeedBurnable;
                    [JsonProperty(LanguageEn ? "Enable list of melting speed in furnaces (true - yes/false - no)" : "Включить список скорости плавки в печах(true - да/false - нет)")]
                    public Boolean UseSpeedBurnableList;
                    [JsonProperty(LanguageEn ? "Setting the melting speed in furnaces by privileges" : "Настройка скорости плавки в печах по привилегиям")]
                    public List<SpeedBurnablePreset> SpeedBurableList = new List<SpeedBurnablePreset>();
                    [JsonProperty(LanguageEn ? "A blacklist of prefabs (ShortPrefabName) that will not be affected by acceleration (example: campfire) [If you don't need it, leave it empty]" : "Черный список префабов(ShortPrefabName), на которые не будет действовать ускорение (пример : campfire) [Если вам не нужно - оставьте пустым]")]
                    public List<String> IgnoreSpeedBurnablePrefabList = new List<String>();
                    internal class DayAnNightRate
                    {
                        [JsonProperty(LanguageEn ? "Ranking setting during the day" : "Настройка рейтинга днем")]
                        public AllRates DayRates = new AllRates();
                        [JsonProperty(LanguageEn ? "Setting the rating at night" : "Настройка рейтинга ночью")]
                        public AllRates NightRates = new AllRates();
                    }
                    
                    [JsonProperty(LanguageEn ? "Setting up a recycler" : "Настройка переработчика")]
                    public RecyclerController RecyclersController = new RecyclerController(); 
                    internal class RecyclerController
                    {
                        [JsonProperty(LanguageEn ? "Use the processing time editing functions" : "Использовать функции редактирования времени переработки")]
                        public Boolean UseRecyclerSpeed;
                        [JsonProperty(LanguageEn ? "Static processing time (in seconds) (Will be set according to the standard or if the player does not have the privilege) (Default = 5s)" : "Статичное время переработки (в секундах) (Будет установлено по стандарту или если у игрока нет привилеии) (По умолчанию = 5с)")]
                        public Single DefaultSpeedRecycler;

                        [JsonProperty(LanguageEn ? "Setting the processing time for privileges (adjust from greater privilege to lesser privilege)" : "Настройка времени переработки для привилегий (настраивать от большей привилегии к меньшей)")]
                        public List<PresetRecycler> PrivilageSpeedRecycler = new List<PresetRecycler>();
                        internal class PresetRecycler
                        {
                            [JsonProperty(LanguageEn ? "Permissions" : "Права")]
                            public String Permissions;
                            [JsonProperty(LanguageEn ? "Standard processing time (in seconds)" : "Стандартное время переработки (в секундах)")]
                            public Single SpeedRecyclers;
                        }
                    }
                    internal class SpeedBurnablePreset
                    {
                        [JsonProperty(LanguageEn ? "Permissions" : "Права")]
                        public String Permissions;
                        [JsonProperty(LanguageEn ? "Furnace melting speed" : "Скорость плавки печей")]
                        public Single SpeedBurnable;
                        [JsonProperty(LanguageEn ? "Smelting Fuel Use Rating" : "Рейтинг использования топлива при переплавки")]
                        public Int32 SpeedFuelBurnable = 1;
                    }
                    internal class PermissionsRate
                    {
                        [JsonProperty(LanguageEn ? "Ranking setting during the day" : "Настройка рейтинга днем")]
                        public Dictionary<String, List<PermissionsRateDetalis>> DayRates = new Dictionary<String, List<PermissionsRateDetalis>>();
                        [JsonProperty(LanguageEn ? "Setting the rating at night" : "Настройка рейтинга ночью")]
                        public Dictionary<String, List<PermissionsRateDetalis>> NightRates = new Dictionary<String, List<PermissionsRateDetalis>>();
                        public class PermissionsRateDetalis
                        {
                            [JsonProperty(LanguageEn ? "Shortname" : "Shortname")]
                            public String Shortname;
                            [JsonProperty(LanguageEn ? "Rate" : "Рейтинг")]
                            public Single Rate;
                        }
                    }
                    internal class AllRates
                    {
                        [JsonProperty(LanguageEn ? "Rating of extracted resources" : "Рейтинг добываемых ресурсов")]
                        public Single GatherRate;
                        [JsonProperty(LanguageEn ? "Rating of found items" : "Рейтинг найденных предметов")]
                        public Single LootRate;
                        [JsonProperty(LanguageEn ? "Pickup Rating" : "Рейтинг поднимаемых предметов")]
                        public Single PickUpRate;
                        [JsonProperty(LanguageEn ? "Rating of plants raised from the beds" : "Рейтинг поднимаемых растений с грядок")]
                        public Single GrowableRate = 1.0f;
                        [JsonProperty(LanguageEn ? "Quarry rating" : "Рейтинг карьеров")]
                        public Single QuarryRate;
                        [JsonProperty(LanguageEn ? "Detailed rating settings in the career" : "Детальная настройка рейтинга в карьере")]
                        public QuarryRateDetalis QuarryDetalis = new QuarryRateDetalis();
                        [JsonProperty(LanguageEn ? "Excavator Rating" : "Рейтинг экскаватора")]
                        public Single ExcavatorRate;
                        [JsonProperty(LanguageEn ? "Coal drop chance" : "Шанс выпадения угля")]
                        public Single CoalRare;
                        [JsonProperty(LanguageEn ? "Rating of items caught from the sea (fishing)" : "Рейтинг предметов выловленных с моря (рыбалки)")]
                        public Single FishRate;

                        internal class QuarryRateDetalis
                        {
                            [JsonProperty(LanguageEn ? "Use the detailed setting of the career rating (otherwise the 'Career Rating' will be taken for all subjects)" : "Использовать детальную настройку рейтинга карьеров (иначе будет браться 'Рейтинг карьеров' для всех предметов)")]
                            public Boolean UseDetalisRateQuarry;
                            [JsonProperty(LanguageEn ? "The item dropped out of the career - rating" : "Предмет выпадаемый из карьера - рейтинг")]
                            public Dictionary<String, Single> ShortnameListQuarry = new Dictionary<String, Single>();
                        }
                    }
                }
                [JsonProperty(LanguageEn ? "Additional plugin settings" : "Дополнительная настройка плагина")]
                public OtherSettings OtherSetting = new OtherSettings();     
                [JsonProperty(LanguageEn ? "Rating settings" : "Настройка рейтингов")]
                public Rates RateSetting = new Rates();
            }
        }
        private void StartCargoPlane(Configuration.PluginSettings.OtherSettings.EventSettings EventSettings)
        {
            if (!EventSettings.CargoPlaneSetting.FullOff && EventSettings.CargoPlaneSetting.UseEventCustom)
            {
                Int32 TimeSpawn = EventSettings.CargoPlaneSetting.RandomTimeSpawn.UseRandomTime ? GetRandomTime(EventSettings.CargoPlaneSetting.RandomTimeSpawn.MinEventSpawnTime, EventSettings.CargoPlaneSetting.RandomTimeSpawn.MaxEventSpawnTime) : EventSettings.CargoPlaneSetting.EventSpawnTime;
                timer.Once(TimeSpawn, () =>
                {
                    StartCargoPlane(EventSettings);
                    SpawnPlane();
                });
            }
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        void OnExcavatorResourceSet(ExcavatorArm arm, string resourceName, BasePlayer player)
        {
            if (arm == null || player == null) return;
            ExcavatorPlayer = player;
        }
        
        object OnContainerDropGrowable(ItemContainer container, Item item)
        {
            if (container == null) return false;
            var Container = container.entityOwner as LootContainer;
            if (Container == null) return false;
            UInt64 NetID = Container.net.ID.Value;
            if (NetID == 143472999 && item.info.itemid == 1998363) return false;

            return null;
        }
        private void OnEntitySpawned(TrainCar trainCar)
        {
            if (trainCar == null) return;
            StorageContainer fuelContainer = trainCar.GetFuelSystem() as StorageContainer;
            if (fuelContainer == null) return;
            TrainEngine trainEngine = fuelContainer.GetComponent<TrainEngine>();
            if (trainEngine == null) return;
            trainEngine.maxFuelPerSec = config.pluginSettings.OtherSetting.FuelConsumedTransportSetting.ConsumedTrain;
        } 
        void API_BONUS_RATE_ADDPLAYER(BasePlayer player, Single Rate)
        {
            if (player == null) return;
            if (!BonusRates.ContainsKey(player))
                BonusRates.Add(player, Rate);
            else BonusRates[player] = Rate;
        }
        private void OnEntitySpawned(BaseSubmarine submarine)
        {
            if (submarine == null) return;
            FuelSystemRating(submarine.GetFuelSystem(), config.pluginSettings.OtherSetting.FuelSetting.AmountSubmarine);
            
            submarine.maxFuelPerSec = config.pluginSettings.OtherSetting.FuelConsumedTransportSetting.ConsumedSubmarine;
        }
        public void SendChat(String Message, BasePlayer player, ConVar.Chat.ChatChannel channel = ConVar.Chat.ChatChannel.Global)
        {
            if (IQChat)
                if (config.pluginSettings.ReferenceSettings.IQChatSetting.UIAlertUse)
                    IQChat?.Call("API_ALERT_PLAYER_UI", player, Message);
                else IQChat?.Call("API_ALERT_PLAYER", player, Message, config.pluginSettings.ReferenceSettings.IQChatSetting.CustomPrefix, config.pluginSettings.ReferenceSettings.IQChatSetting.CustomAvatar);
            else player.SendConsoleCommand("chat.add", channel, 0, Message);
        }
        bool IsBlackListBurnable(string Shortname)
        {
            var BlackList = config.pluginSettings.RateSetting.BlackListBurnable;
            if (BlackList.Contains(Shortname))
                return true;
            else return false;
        }
        
        enum Types
        {
            Gather,
            Loot,
            PickUP,
            Quarry,
            Excavator,
            Growable,
            Fishing
        }
        int API_CONVERT(Types RateType, string Shortname, float Amount, BasePlayer player = null) => Converted(RateType, Shortname, Amount, player);
        private object OnExcavatorGather(ExcavatorArm arm, Item item)
        {
            if (arm == null) return null;
            if (item == null) return null;
            item.amount = Converted(Types.Excavator, item.info.shortname, item.amount, ExcavatorPlayer);
            return null;
        }
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
        
                
        
        
        private static StringBuilder sb = new StringBuilder();

        
        
        Item OnFishCatch(Item item, BaseFishingRod rod, BasePlayer player)
        {
            if (item == null || player == null) return null;

            Int32 Rate = Converted(Types.Fishing, item.info.shortname, item.amount, player);
            item.amount = Rate;
            return null;
        }
        private void OnEntitySpawned(Snowmobile snowmobiles)
        {
            if (snowmobiles == null) return;
            snowmobiles.maxFuelPerSec = config.pluginSettings.OtherSetting.FuelConsumedTransportSetting.ConsumedSnowmobile;
        } 
        public static IQRates _;

        
        
        
        public class ModiferTea
        {
            public Modifier.ModifierType Type;
            public Single Duration;
            public Single Value;
        }

        private class OvenController : FacepunchBehaviour
        {
            private static readonly Dictionary<BaseOven, OvenController> Controllers = new Dictionary<BaseOven, OvenController>();
            private BaseOven _oven;
            private float _speed;
            private string _ownerId;
            private Int32 _ticks;
            private Int32 _speedFuel;
            
            private bool IsFurnace => (int)_oven.temperature >= 2;

            private void Awake()
            {
                _oven = (BaseOven)gameObject.ToBaseEntity();
                _ownerId = _oven.OwnerID.ToString();
            }

            public object Switch(BasePlayer player)
            {
                if (!IsFurnace || _oven.needsBuildingPrivilegeToUse && !player.CanBuild())
                    return null;
		   		 		  						  	   		  	  			  	 				  	   		  	  	
                if (_oven.IsOn())
                    StopCooking();
                else
                {
                    _ownerId = _oven.OwnerID != 0 ? _oven.OwnerID.ToString() : player.UserIDString;
                    StartCooking();
                }
                return false;
            }
		   		 		  						  	   		  	  			  	 				  	   		  	  	
            public void TryRestart()
            {
                if (!_oven.IsOn())
                    return;
                _oven.CancelInvoke(_oven.Cook);
                StopCooking();
                StartCooking();
            }
            private void Kill()
            {
                if (_oven.IsOn())
                {
                    StopCooking();
                    _oven.StartCooking();
                }
                Destroy(this);
            }

            
            public static OvenController GetOrAdd(BaseOven oven)
            {
                OvenController controller;
                if (Controllers.TryGetValue(oven, out controller))
                    return controller;
                controller = oven.gameObject.AddComponent<OvenController>();
                Controllers[oven] = controller;
                return controller;
            }
		   		 		  						  	   		  	  			  	 				  	   		  	  	
            public static void TryRestartAll()
            {
                foreach (var pair in Controllers)
                {
                    pair.Value.TryRestart();
                }
            }
            public static void KillAll()
            {
                foreach (var pair in Controllers)
                {
                    pair.Value.Kill();
                }
                Controllers.Clear();
            }
            public void OnDestroy()
            {
                Destroy(this);
            }
                        
            private void StartCooking()
            {
                if(_oven.IndustrialMode != BaseOven.IndustrialSlotMode.ElectricFurnace)
                    if (_oven.FindBurnable() == null)
                        return;

                Single Multiplace = _.GetMultiplaceBurnableSpeed(_ownerId);
                _speed = (Single)(0.5f / Multiplace); // 0.5 * M
                Int32 MultiplaceFuel = _.GetMultiplaceBurnableFuelSpeed(_ownerId);
                _speedFuel = MultiplaceFuel;
                
                StopCooking();
                
                _oven.inventory.temperature = _oven.cookingTemperature;
                _oven.UpdateAttachmentTemperature();

                InvokeRepeating(Cook, _speed, _speed);
                _oven.SetFlag(BaseEntity.Flags.On, true);

            }
            
            private void StopCooking()
            {
                CancelInvoke(Cook);
                _oven.StopCooking();
                _oven.SetFlag(BaseEntity.Flags.On, false);
                _oven.SendNetworkUpdate();
                
                if (_oven.inventory != null)
                {
                    foreach (Item item in _oven.inventory.itemList)
                    {
                        if (item.HasFlag(global::Item.Flag.Cooking))
                            item.SetFlag(global::Item.Flag.Cooking, false);
                        item.MarkDirty();
                    }
                }
            }
            
            public void Cook()
            {
                if (!_oven.HasFlag(BaseEntity.Flags.On))
                {
                    StopCooking();
                    return;
                }
                Item item = _oven.FindBurnable();

                if (_oven.IndustrialMode != BaseOven.IndustrialSlotMode.ElectricFurnace)
                {
                    if (item == null)
                    {
                        StopCooking();
                        return;
                    }
                }

                _oven.Cook();

                SmeltItems();

                if (_oven.IndustrialMode != BaseOven.IndustrialSlotMode.ElectricFurnace)
                {
                    var component = item.info.GetComponent<ItemModBurnable>();
                    item.fuel -= 0.5f * (_oven.cookingTemperature / 200f) * _speedFuel;
		   		 		  						  	   		  	  			  	 				  	   		  	  	
                    if (!item.HasFlag(global::Item.Flag.OnFire))
                    {
                        item.SetFlag(global::Item.Flag.OnFire, true);
                        item.MarkDirty();
                    }


                    if (item.fuel <= 0f)
                    {
                        _oven.ConsumeFuel(item, component);
                    }
                }

                _ticks++;
            }
            private void SmeltItems()
            {
                if (_ticks % 1 != 0)
                    return;

                for (var i = 0; i < _oven.inventory.itemList.Count; i++)
                {
                    var item = _oven.inventory.itemList[i];
                    
                    if (item == null || !item.IsValid() || item.info == null)
                        continue;

                    var cookable = item.info.GetComponent<ItemModCookable>();
                    if (cookable == null)
                        continue;

                    if (_.IsBlackListBurnable(item.info.shortname)) continue;
                    
                    Single temperature = item.temperature;
                    
                    if ((temperature < cookable.lowTemp || temperature > cookable.highTemp))
                    {
                        if (!cookable.setCookingFlag || !item.HasFlag(global::Item.Flag.Cooking)) continue;
                        item.SetFlag(global::Item.Flag.Cooking, false);
                        item.MarkDirty();
                        continue;
                    }

                    if (!cookable.CanBeCookedByAtTemperature(temperature) && _.IsBlackListBurnable(item.info.shortname))
                    {
                        if (!cookable.setCookingFlag || !item.HasFlag(global::Item.Flag.Cooking))
                            continue;

                        item.SetFlag(global::Item.Flag.Cooking, false);
                        item.MarkDirty();
                        continue;
                    }

                    if (cookable.cookTime > 0 && _ticks * 1f / 1 % cookable.cookTime > 0)
                        continue;

                    if (cookable.setCookingFlag && !item.HasFlag(global::Item.Flag.Cooking))
                    {
                        item.SetFlag(global::Item.Flag.Cooking, true);
                        item.MarkDirty();
                    }

                    var position = item.position;
                    if (item.amount > 1)
                    {
                        item.amount--;
                        item.MarkDirty();
                    }
                    else
                    {
                        item.Remove();
                    }

                    if (cookable.becomeOnCooked == null) continue;

                    var item2 = ItemManager.Create(cookable.becomeOnCooked,
                        (int)(cookable.amountOfBecome * 1f));

                    if (item2 == null || item2.MoveToContainer(item.parent, position) ||
                        item2.MoveToContainer(item.parent))
                        continue;

                    item2.Drop(item.parent.dropPosition, item.parent.dropVelocity);
                    //if (!item.parent.entityOwner) continue;
                    StopCooking();
                }
            }
        }
            }
}
