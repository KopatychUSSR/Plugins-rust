using Oxide.Core;
using Rust;
using System;
using UnityEngine;
using System.Collections.Generic;
using Oxide.Core.Libraries;
using System.Text;
using UnityEngine.Networking;
using System.Drawing;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Collections;
using System.Linq;
using VLB;
using Newtonsoft.Json;
using Network;

namespace Oxide.Plugins
{
    [Info("IQAlcoholFarm", "Mercury", "1.1.12")]
    [Description("IQAlcoholFarm")]
    internal class IQAlcoholFarm : RustPlugin
    {

        private void DestroyAllUi(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UI_Layer);
            CuiHelper.DestroyUi(player, UI_LayerFade);
            CuiHelper.DestroyUi(player, UI_LayerTimer);
        }
        
        
        private static IEnumerator DownloadImage(String url, Signage sign, String name)
        {
            UnityWebRequest www = UnityWebRequest.Get(url);

            yield return www.SendWebRequest();
            if (_plugin == null)
                yield break;
            if (www.isNetworkError || www.isHttpError)
            {
                _plugin.PrintWarning(String.Format("Image download error! Error: {0}, Image name: {1}", www.error, name));
                www.Dispose();

                yield break;
            }

            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(www.downloadHandler.data);
            if (texture != null)
            {
                Byte[] bytes = texture.EncodeToPNG();

                String image = FileStorage.server.Store(bytes, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();

                Int32 size = Math.Max(sign.paintableSources.Length, 1);
                if (sign.textureIDs == null || sign.textureIDs.Length != size)
                {
                    Array.Resize(ref sign.textureIDs, size);
                }
                if (sign.textureIDs[0] > 0)
                {
                    FileStorage.server.RemoveExact(sign.textureIDs[0], FileStorage.Type.png, sign.net.ID, 0U);
                }
                UInt32 idinstorage = FileStorage.server.Store(bytes, FileStorage.Type.png, sign.net.ID);
                sign.textureIDs[0] = idinstorage;
                sign.SendNetworkUpdate();

                UnityEngine.Object.DestroyImmediate(texture);
            }

            www.Dispose();
            yield break;
        }

        private readonly String[] AlcoholShakeEffect = new[]
        {
            "assets/bundled/prefabs/fx/takedamage_generic.prefab",
            "assets/bundled/prefabs/fx/screen_land.prefab",
            "assets/bundled/prefabs/fx/screen_jump.prefab",
            "assets/prefabs/tools/jackhammer/effects/strike_screenshake.prefab",
            "assets/prefabs/weapons/cake/effects/strike_screenshake.prefab",
            "assets/prefabs/weapons/halloween/butcher knife/effects/strike_screenshake.prefab",
            "assets/prefabs/weapons/halloween/pitchfork/effects/strike_screenshake.prefab",
            "assets/prefabs/weapons/halloween/sickle/effects/strike_screenshake.prefab",
            "assets/prefabs/weapons/hatchet/effects/strike_screenshake.prefab",
            "assets/prefabs/weapons/knife/effects/strike_screenshake.prefab",
            "assets/bundled/prefabs/fx/player/drown.prefab",

        };
		   		 		  						  	   		  		  		  		  		  	   		  	   
        private void InitControllers()
        {
            foreach (KeyValuePair<NetworkableId, BaseNetworkable> keyValuePair in BaseNetworkable.serverEntities.entityList)
            {
                BaseNetworkable entity = keyValuePair.Value;

                if (entity is LiquidContainer)
                {
                    LiquidContainer cont = ((LiquidContainer)entity);
                    Item item = cont.inventory.GetSlot(0);
                    if (item != null && item.skin != 0)
                    {
                        cont.ClearConnections();
                    }

                    InitBarrel(entity);
                }
            }

            Puts($"Loaded: {BarrelController.ActiveControllers.Count} custom barrels custom purifiers!");
        }
        
        private class BarrelController : MonoBehaviour
        {
            public static List<BarrelController> ActiveControllers = new List<BarrelController>();
            public static List<UInt64> ActiveControllersIds = new List<UInt64>();

            private StashContainer _stash;
            private LiquidContainer _barrel;

            private readonly Single processTickTime = _config.fermentationSettings.BarrelProcessTick;
            private readonly Int32 tickConsume = _config.fermentationSettings.BarrelInputTick;
            private readonly Int32 tickOutput = _config.fermentationSettings.BarrelOutputTick;
            private readonly Int32 barrelCapacity = _config.fermentationSettings.BarrelCapacity;
		   		 		  						  	   		  		  		  		  		  	   		  	   
            private readonly String[] acceptAbleItems =
            {

                "black.berry",
                "blue.berry",
                "green.berry",
                "red.berry",
                "white.berry",
                "yellow.berry",

            };
		   		 		  						  	   		  		  		  		  		  	   		  	   
            public UInt64 GetStashId()
            {
                if (_stash == null)
                    return 0;

                return _stash.net.ID.Value;
            }

            private void Awake()
            {
                _barrel = GetComponent<LiquidContainer>();
                if (_barrel == null)
                {
                    Debug.LogError($"[{_plugin.Name}] Error! Barrel not found! Deleting...");
                    Destroy(this);
                    return;
                }

                _barrel.maxStackSize = barrelCapacity;

                _stash = _barrel.GetComponentInChildren<StashContainer>();
                if (_stash == null)
                {
                    Debug.LogError($"[{_plugin.Name}] Error! Stash not found! Deleting...");
                    Destroy(this);
                    return;
                }

                _stash.SetMaxHealth(999999);
                _stash.SetHealth(999999);

                RegisterBarrel(this);

                if (ActiveControllersIds.Contains(_barrel.net.ID.Value) == false)
                    ActiveControllersIds.Add(_barrel.net.ID.Value);

                _stash.inventory.canAcceptItem += CanAcceptItem;

                foreach (Item item in _stash.inventory.itemList.ToArray())
                {
                    if (acceptAbleItems.Contains(item.info.shortname) == false)
                    {
                        Transform stashTransform = _stash.transform;
                        item.Drop(stashTransform.position, stashTransform.forward, Quaternion.identity);
                        continue;
                    }
                }

                InvokeRepeating(nameof(ProcessTick), processTickTime, processTickTime);
                InvokeRepeating(nameof(PlayProcessEffect), 1f, 1800);

                //PhysicsChangeCancel();
                InvokeRepeating(nameof(PhysicsChangeCancel), 1f, 1f);
            }

            private void PhysicsChangeCancel()
            {
                if (_stash.IsInvoking(new Action(_stash.DoOccludedCheck)) == false)
                    return;

                _stash.CancelInvoke(new Action(_stash.DoOccludedCheck));
                //_stash.Invoke(new Action(_stash.DoOccludedCheck), 604800f);
            }

            private void PlayProcessEffect()
            {
                Vector3 pos = _barrel.transform.position + new Vector3(0, 0.5f) + _barrel.transform.forward / 2f;
                Effect.server.Run(AlcoholWortProcessEffect, pos, Vector3.zero);
            }
		   		 		  						  	   		  		  		  		  		  	   		  	   
            private Boolean CanAcceptItem(Item item, Int32 i)
            {
                if (acceptAbleItems.Contains(item.info.shortname))
                {
                    foreach (PluginConfig.ShopStore.CategorySettings.ItemsPair ItemPair in _config.storeNpc.EquipmentCategory.ItemsList)
                            if (ItemPair.BuyItems.Shortname.Equals(item.info.shortname) && ItemPair.BuyItems.SkinID != item.skin)
                                return false;
                    return true;
                }

                return false;
            }
		   		 		  						  	   		  		  		  		  		  	   		  	   
            private void OnDestroy()
            {
                ActiveControllers.Remove(this);

                if (_stash != null)
                {
                    _stash.inventory.canAcceptItem -= CanAcceptItem;
                }

                if (_barrel == null || _barrel.net == null)
                    return;

                ActiveControllersIds.Remove(_barrel.net.ID.Value);
            }

            private void ProcessTick()
            {
                Int32 TickOut = tickOutput;
                if (_stash.inventory.itemList.Count <= 0)
                    return;

                Item item = _barrel.GetLiquidItem();
                if (item?.skin == _config.itemSettings.AlcoholItemSkinId)
                    return;
                
                Int32 amount = (item?.amount ?? 0);
                
                if (barrelCapacity <= amount)
                    return;

                Item itemToConsume = _stash.inventory.itemList.Last();
                TickOut = (tickOutput / tickConsume) * (itemToConsume.amount < tickConsume ? itemToConsume.amount : tickConsume);
                itemToConsume.UseItem(tickConsume);
                itemToConsume.MarkDirty();

                if (item == null)
                {
                    Item water = ItemManager.CreateByName("water", TickOut == 0 ? 1 : TickOut, _config.itemSettings.WortItemSkinId);
                    if (water == null)
                        return;

                    water.MoveToContainer(_barrel.inventory);
                    return;
                }

                item.amount += TickOut;
                if (item.amount >= barrelCapacity)
                    item.amount = barrelCapacity;

                item.MarkDirty();
            }

        }

        private void ValidateConfig()
        {
            if (Interface.Oxide.CallHook("OnConfigValidate") != null)
            {
                PrintWarning("Using default configuration...");
                _config = GetDefaultConfig();
            }
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                RareEffect = 50,
                UseNPCSounds = true,
                storeNpc = new PluginConfig.ShopStore
                {
                    NPCShopSetting = new PluginConfig.ShopStore.NPCShop
                    {
                        Name = "Ivan",
                        userID = 19384,
                        Wear = new List<PluginConfig.ShopStore.NPCShop.ItemsNpc>
                        {
                           new PluginConfig.ShopStore.NPCShop.ItemsNpc
                           {
                               ShortName = "shoes.boots",
                               SkinId = 2570215282,
                           },
                           new PluginConfig.ShopStore.NPCShop.ItemsNpc
                           {
                               ShortName = "burlap.trousers",
                               SkinId = 2040706598,
                           },
                           new PluginConfig.ShopStore.NPCShop.ItemsNpc
                           {
                               ShortName = "burlap.shirt",
                               SkinId = 2040707769,
                           },
                           new PluginConfig.ShopStore.NPCShop.ItemsNpc
                           {
                               ShortName = "hat.boonie",
                               SkinId = 503202816,
                           },
                        }
                    },
                    ShopName = "Ivan Store - Sale and purchase of alcohol",
                    TurnedShop = new PluginConfig.ShopStore.TurnedShopSpawn
                    {
                        UseAirfield = true,
                        UseBanditTown = true,
                        UseCompound = true,
                    },
                    EquipmentCategory = new PluginConfig.ShopStore.CategorySettings
                    {
                        ItemsList = new List<PluginConfig.ShopStore.CategorySettings.ItemsPair>
                        {
                            new PluginConfig.ShopStore.CategorySettings.ItemsPair("water.barrel", 1, 2477175239, LanguageEn? "Fermentation barrel" : "Бродильная бочка", "scrap", 250, 0, String.Empty),
                            new PluginConfig.ShopStore.CategorySettings.ItemsPair("water.purifier", 1, 0, String.Empty, "scrap", 100, 0, String.Empty),
                            new PluginConfig.ShopStore.CategorySettings.ItemsPair("planter.large", 1, 0, String.Empty, "scrap", 30, 0, String.Empty),
                            new PluginConfig.ShopStore.CategorySettings.ItemsPair("planter.small", 1, 0, String.Empty, "scrap", 10, 0, String.Empty),
                            new PluginConfig.ShopStore.CategorySettings.ItemsPair("fertilizer", 1, 0, String.Empty, "scrap", 10, 0, String.Empty),
                            new PluginConfig.ShopStore.CategorySettings.ItemsPair("waterjug", 1, 0, String.Empty, "scrap", 5, 0, String.Empty),
                            new PluginConfig.ShopStore.CategorySettings.ItemsPair("black.berry", 3, 0, String.Empty, "scrap", 15, 0, String.Empty),
                            new PluginConfig.ShopStore.CategorySettings.ItemsPair("blue.berry", 3, 0, String.Empty, "scrap", 15, 0, String.Empty),
                            new PluginConfig.ShopStore.CategorySettings.ItemsPair("red.berry", 3, 0, String.Empty, "scrap", 15, 0, String.Empty),
                            new PluginConfig.ShopStore.CategorySettings.ItemsPair("yellow.berry", 3, 0, String.Empty, "scrap", 15, 0, String.Empty),
                            new PluginConfig.ShopStore.CategorySettings.ItemsPair("white.berry", 3, 0, String.Empty, "scrap", 15, 0, String.Empty),
                        }
                    },
                    ExchangerCategory = new PluginConfig.ShopStore.CategorySettings
                    {
                        ItemsList = new List<PluginConfig.ShopStore.CategorySettings.ItemsPair>
                        {
                            new PluginConfig.ShopStore.CategorySettings.ItemsPair("hazmatsuit.arcticsuit", 1, 0, String.Empty, "water", 2500, 2469834291, String.Empty),
                            new PluginConfig.ShopStore.CategorySettings.ItemsPair("rifle.ak.ice", 1, 0, String.Empty, "water", 3500, 2469834291, String.Empty),
                            new PluginConfig.ShopStore.CategorySettings.ItemsPair("stones", 1000, 0, String.Empty, "water", 350, 2469834291, String.Empty),
                            new PluginConfig.ShopStore.CategorySettings.ItemsPair("wood", 1000, 0, String.Empty, "water", 150, 2469834291, String.Empty),
                            new PluginConfig.ShopStore.CategorySettings.ItemsPair("metal.fragments", 1000, 0, String.Empty, "water", 700, 2469834291, String.Empty),
                            new PluginConfig.ShopStore.CategorySettings.ItemsPair("lowgradefuel", 500, 0, String.Empty, "water", 950, 2469834291, String.Empty),
                            new PluginConfig.ShopStore.CategorySettings.ItemsPair("sulfur", 1000, 0, String.Empty, "water", 1250, 2469834291, String.Empty),
                            new PluginConfig.ShopStore.CategorySettings.ItemsPair("metal.refined", 25, 0, String.Empty, "water", 950, 2469834291, String.Empty),
                            new PluginConfig.ShopStore.CategorySettings.ItemsPair("charcoal", 100, 0, String.Empty, "water", 750, 2469834291, String.Empty),
                            new PluginConfig.ShopStore.CategorySettings.ItemsPair("cloth", 500, 0, String.Empty, "water", 250, 2469834291, String.Empty),
                            new PluginConfig.ShopStore.CategorySettings.ItemsPair("leather", 100, 0, String.Empty, "water", 950, 2469834291, String.Empty),
                            new PluginConfig.ShopStore.CategorySettings.ItemsPair("targeting.computer", 1, 0, String.Empty, "water", 650, 2469834291, String.Empty),
                            new PluginConfig.ShopStore.CategorySettings.ItemsPair("cctv.camera", 1, 0, String.Empty, "water", 450, 2469834291, String.Empty),
                        }
                    }
                }
            };
        }
        private System.Object CanBradleyApcTarget(BradleyAPC apc, BaseEntity entity)
        {
            if (_initiated == false)
                return null;

            if (apc == null || entity == null) return null;

            foreach (BradleyAPC.TargetInfo Targets in apc.targetList.Where(ent => ent.entity != null && ent.entity.OwnerID == 199949458))
                NextTick(() => { apc.targetList.Remove(Targets); });

            return null;
        }
        /// <summary>
        /// 1.0.10
        /// Изменения :
        /// - Исправление после обновления игры
        /// </summary>

        private Boolean FullLoadedNPC = false;
        private const String UI_LayerTimer = "UI_AlcoholFarm.Timer";

        private void SetupBarrel(BaseEntity entity)
        {
            BaseEntity stashEntity = GameManager.server.CreateEntity("assets/prefabs/deployable/small stash/small_stash_deployed.prefab");
            if (stashEntity == null)
            {
                PrintError("Failed to spawn small stash on barrel!");
                return;
            }

            stashEntity.Spawn();
            stashEntity.SetParent(entity, false);

            stashEntity.transform.localPosition += new Vector3(-0.365f, 1.14f, 0.42f);
            stashEntity.transform.localRotation = Quaternion.Euler(new Vector3(90f, 0f, 0));
            
            entity.gameObject.AddComponent<BarrelController>();
        }

        private const String SignImgLeft = "https://i.imgur.com/Vq6uJgI.png";

        private Boolean IsAirfield()
        {
            return _config.storeNpc.TurnedShop.UseAirfield && Airfield != null;
        }
        private void BuyItem(BasePlayer player, PluginConfig.ShopStore.CategorySettings.ItemsPair BuyItem, CategoryType category, Int32 Page, Int32 Amount)
        {
            TakeItems(player, BuyItem, Amount: Amount);
            player.GiveItem(BuyItem.BuyItems.ToItem(Amount));
            DrawUI_Items(player, category, Page);
        }

        private void FullLoad()
        {
            _initiated = true;
            InitControllers();
            PluginData.LoadData();
        }

        private readonly Dictionary<String, List<Point>> PointsList = new Dictionary<String, List<Point>>
        {
            ["COMPOUND"] = new List<Point>
            {
                new Point
                {
                    Name = "NPC.POINT",
                    Y = 40f,
                    X = 0f,
                    Pos = new Vector3(21.7f, 0.3f, 7.4f),
                    LinkImage = String.Empty,
                },
                new Point
                {
                    Name = "TABLES.POINT",
                    Y = -90f,
                    X = 30f,
                    Pos = new Vector3(17.9f, 0.3f, 11f),
                    LinkImage = SignImgLeft,
                },
                new Point
                {
                    Name = "TABLES.POINT",
                    Y = 0,
                    X = 0f,
                    Pos = new Vector3(3.7f, 3f, 16.2f),
                    LinkImage = SignImgLeft,
                },
            },
            ["BANDIT_TOWN"] = new List<Point>
            {
                new Point
                {
                    Name = "NPC.POINT",
                    Y = 180f,
                    X = 0f,
                    Pos = new Vector3(-28.8f, 1.78f, 35f),
                    LinkImage = String.Empty,
                },
                new Point
                {
                    Name = "TABLES.POINT",
                    Y = 180f,
                    X = 0,
                    Pos = new Vector3(-24.8f, 3.78f, 35.5f),
                    LinkImage = SignImgLeft,
                },
            },
            ["AIRFIELD"] = new List<Point>
            {
                new Point
                {
                    Name = "NPC.POINT",
                    Y = 200f,
                    X = 0f,
                    Pos = new Vector3(-10.5f, 3.31f, -93.3f),
                    LinkImage = String.Empty,
                },
                new Point
                {
                    Name = "TABLES.POINT",
                    Y = 0f,
                    X = 0f,
                    Pos = new Vector3(-7.5f, 2.8f, -75.8f),
                    LinkImage = SignImgLeft,
                },
                new Point
                {
                    Name = "TABLES.POINT",
                    Y = -90f,
                    X = 0f,
                    Pos = new Vector3(-0.86f, 4.5f, -92f),
                    LinkImage = SignImgLeft,
                },
                new Point
                {
                    Name = "TABLES.POINT",
                    Y = -30f,
                    X = 30f,
                    Pos = new Vector3(-59.3f, 0.3f, -72.7f),
                    LinkImage = SignImgLeft,
                },
                new Point
                {
                    Name = "TABLES.POINT",
                    Y = 180f,
                    X = 13f,
                    Pos = new Vector3(-154.5f, 0.22f, -76.8f),
                    LinkImage = SignImgRight,
                },
            },
        };
		   		 		  						  	   		  		  		  		  		  	   		  	   
        private class PluginData
        {
            private const String DataPath = "IQSystem/IQAlcoholFarm/Data";
            [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<DrunkPlayer> DrunkPlayers = new List<DrunkPlayer>();

            public static void SaveData()
            {
                if (_data == null) return;
                Interface.Oxide.DataFileSystem.WriteObject(DataPath, _data);
            }

            public static void LoadData()
            {
                if (Interface.Oxide.DataFileSystem.ExistsDatafile(DataPath))
                {
                    _data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(DataPath);
                }
                else
                {
                    _data = new PluginData()
                    {

                    };
                }

                if (_data == null)
                {
                    _data = new PluginData()
                    {

                    };
                }

                if (_data.DrunkPlayers.Count == 1)
                    ServerMgr.Instance.StartCoroutine(_plugin.PlayersUpdate(true));
            }
        }

        private void DrawUI_Items(BasePlayer player, CategoryType categoryType = CategoryType.Equipment, Int32 Page = 0)
        {
            Int32 Y = 0;
            Int32 YOffset = 75;
            List<PluginConfig.ShopStore.CategorySettings.ItemsPair> CategoryItems = categoryType == CategoryType.Equipment ? _config.storeNpc.EquipmentCategory.ItemsList : _config.storeNpc.ExchangerCategory.ItemsList;

            foreach (PluginConfig.ShopStore.CategorySettings.ItemsPair Category in CategoryItems.Skip(Page * 5).Take(5))
            {
                CuiHelper.DestroyUi(player, $"PANEL_ITEM_{Y}");
                String Interface = InterfaceBuilder.GetInterface("UI_TRADER_NPC_ITEMPANEL");
                if (Interface == null) return;

                Interface = Interface.Replace("%Y%", $"{Y}");
                Interface = Interface.Replace("%OFFSET_MIN%", $"-180.04 {119.011 - (Y * YOffset)}");
                Interface = Interface.Replace("%OFFSET_MAX%", $"180.04 {182.011 - (Y * YOffset)}");
		   		 		  						  	   		  		  		  		  		  	   		  	   
                CuiHelper.AddUi(player, Interface);
                InterfaceBuilder.BuildingTrader_Items(player,
                    ItemManager.FindItemDefinition(Category.BuyItems.Shortname).itemid, Category.BuyItems.SkinID,
                    ItemManager.FindItemDefinition(Category.PriceItems.Shortname).itemid, Category.PriceItems.SkinID, Y);
                DrawUI_Items_AmountController(player, Y, Category, Page, categoryType: categoryType);
                Y++;
            }
        }

        private static readonly String[] AlcoholColorGradient = new[]
        {
            "#FFFFFF",
            "#F7D358",
            "#FAAC58",
            "#FA8258",
            "#FA5858",
            "#B40404",
        };
        private object OnWireConnect(BasePlayer player, IOEntity entity1, Int32 inputs, IOEntity entity2, Int32 outputs)
        {
            if (_initiated == false)
                return null;

            LiquidContainer liquid1 = entity1 as LiquidContainer;
            if (liquid1 != null)
            {
                Item item1 = liquid1.inventory.GetSlot(0);
                if (item1 != null && item1.skin != 0)
                    return false;
            }

            LiquidContainer liquid2 = entity2 as LiquidContainer;
            if (liquid2 != null)
            {
                Item item2 = liquid2.inventory.GetSlot(0);
                if (item2 != null && item2.skin != 0)
                    return false;
            }

            if (BarrelController.ActiveControllersIds.Exists(x => x == entity1.net.ID.Value || x == entity2.net.ID.Value))
                return false;

            return null;
        }

        private void GetSounds()
        {
            if (!_config.UseNPCSounds) return;
            timer.Once(3f, () =>
            {
                if (RenderSounds.Values != null && RenderSounds.Values.Count != 0) return;
                PrintWarning(LanguageEn ? "Loading sounds.." : "Загружаем звуки..");
                try
                {
                    webrequest.Enqueue($"http://iqsystem.skyplugins.ru/iqalcohol/getsound/W5fgSD3dfVBxc", null, (code, response) =>
                    {
                        switch (code)
                        {
                            case 404:
                                {
                                    PrintError($"ERROR #63494985  {response} | ERROR #:545445  (Discord - Mercury#5212)"); 
                                _initiated = false;
                                    break;
                                }
                            case 503:
                                {
                                    PrintError($"ERROR #45445 Your plugin version is outdated!Upgrade to the latest version! (Discord - Mercury#5212)");
                                    _initiated = false;
                                    break;
                                }
                            case 200:
                                {
                                    Dictionary<String, List<Byte[]>> obj = JsonConvert.DeserializeObject<Dictionary<String, List<Byte[]>>>(response);
                                    RenderSounds = obj;
                                    WriteData();

                                    PrintWarning(LanguageEn ? "Successful data acquisition" : "Звук успешно создан!");

                                    break;
                                }
                        }
                    }, this, RequestMethod.GET);
                }
                catch (Exception e)
                {
                    PrintError($"ERROR #8573 An error occurred while connecting, please inform the developer Discord - Mercury#5212\nError : {e}");
                }
                PrintWarning(LanguageEn ? "Sounds uploaded successfully.." : "Звуки загружены успешно..");
            });
        }
        private System.Object OnEntityTakeDamage(BaseEntity entity, HitInfo info)
        {
            if (_initiated == false)
                return null;

            if (info == null || entity == null)
                return null;
            if (entity.OwnerID == 199949458) return false;
            return null;
        }

        private System.Object OnWaterCollect(WaterCatcher waterCatcher)
        {
            if (_initiated == false)
                return null;

            Item item = waterCatcher.inventory.GetSlot(0);
            if (item == null)
                return null;

            if (item.skin == _config.itemSettings.AlcoholItemSkinId || item.skin == _config.itemSettings.WortItemSkinId)
            {
                return false;
            }

            return null;
        }

        private System.Object OnLiquidVesselFill(BaseLiquidVessel liquidVessel, BasePlayer player, LiquidContainer facingLiquidContainer)
        {
            if (_initiated == false)
                return null;

            Item item = liquidVessel.GetItem()?.contents?.GetSlot(0);
            if (item == null)
                return null;

            if ((item.skin == _config.itemSettings.AlcoholItemSkinId || item.skin == _config.itemSettings.WortItemSkinId))
            {
                return false;
            }

            return null;
        }

        protected override void LoadDefaultConfig()
        {
            _config = GetDefaultConfig();
        }

        private System.Object CanHideStash(BasePlayer player, StashContainer stash)
        {
            if (_initiated == false)
                return null;

            if (BarrelController.ActiveControllers.Exists(x => x.GetStashId() == stash.net.ID.Value))
                return false;

            return null;
        }

        
        
        private class DrunkPlayer
        {
            public String CurrectFade = AlcoholBlur[0];

            [JsonIgnore] private Boolean _playerFound = false;
            public String CurrectColor = AlcoholColorGradient[0];
            public UInt64 UserId = 0;

            public Single DrunkRemaining = 0;
            [JsonIgnore] private BasePlayer _player;
            public Single ModifiersUpdate = 0;
		   		 		  						  	   		  		  		  		  		  	   		  	   
            [JsonIgnore]
            public BasePlayer Player
            {
                get
                {
                    if (_playerFound == false)
                    {
                        _player = BasePlayer.activePlayerList.FirstOrDefault(x => x != null && x.userID == UserId);
                        if (_player != null)
                        {
                            _playerFound = true;
                        }
                        else
                        {
                            _data.DrunkPlayers.Remove(this);
                        }
                    }

                    return _player;
                }
            }
        }

        public static String GetColor(String hex, Single alpha = 1f)
        {
            if (hex.Length != 7) hex = "#FFFFFF";
            if (alpha < 0 || alpha > 1f) alpha = 1f;

            System.Drawing.Color color = ColorTranslator.FromHtml(hex);
            Single r = Convert.ToInt16(color.R) / 255f;
            Single g = Convert.ToInt16(color.G) / 255f;
            Single b = Convert.ToInt16(color.B) / 255f;

            return $"{r} {g} {b} {alpha}";
        }

        void WriteData()
        {
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQSystem/IQAlcoholFarm/Sounds", RenderSounds, true);
        }
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<String, String>
            {
                ["NO_RESOURCES_USER"] = "<b>NOT ENOUGHT</b>",
                ["YES_RESOURCES_USER"] = "<b>BUY</b>",
                ["UI_BACK_BUTTON"] = "<b>BACK</b>",
                ["UI_NEXT_BUTTON"] = "<b>NEXT</b>",
                ["UI_TITLE_EQUIPMENT"] = "<b>EQUIPMENT</b>",
                ["UI_TITLE_EXCHANGER"] = "<b>EXCHANGER</b>",
                ["UI_TITLE_ITEM"] = "<b>PRODUCT</b>",
                ["UI_TITLE_PRICE"] = "<b>PRICE</b>",
                ["UI_TITLE_AMOUNT"] = "<b>NUMBER</b>",
                ["UI_EXIT_BUTTON"] = "CLICK ON AN EMPTY SPACE TO EXIT",
            }, this);

            lang.RegisterMessages(new Dictionary<String, String>
            {
                ["NO_RESOURCES_USER"] = "<b>НЕДОСТАТОЧНО</b>",
                ["YES_RESOURCES_USER"] = "<b>КУПИТЬ</b>",
                ["UI_BACK_BUTTON"] = "<b>НАЗАД</b>",
                ["UI_NEXT_BUTTON"] = "<b>ВПЕРЕД</b>",
                ["UI_TITLE_EQUIPMENT"] = "<b>ОБОРУДОВАНИЕ</b>",
                ["UI_TITLE_EXCHANGER"] = "<b>ОБМЕННИК</b>",
                ["UI_TITLE_ITEM"] = "<b>ТОВАР</b>",
                ["UI_TITLE_PRICE"] = "<b>ЦЕНА</b>",
                ["UI_TITLE_AMOUNT"] = "<b>КОЛИЧЕСТВО</b>",
                ["UI_EXIT_BUTTON"] = "НАЖМИТЕ НА ПУСТОЕ МЕСТО ДЛЯ ВЫХОДА",

            }, this, "ru");
        }
        private void OnServerInitialized()
        {
            PrintWarning("\n-----------------------------\n" +
            "     Author - https://devplugins.ru/\n" +
            "     VK - https://vk.com/dev.plugin\n" +
            "     Discord - https://discord.gg/eHXBY8hyUJ\n" +
            "-----------------------------");
            _plugin = this;

            BanditTown = TerrainMeta.Path.Monuments.FirstOrDefault(p => p.name.ToLower().Contains("bandit_town"));
            Compound = TerrainMeta.Path.Monuments.FirstOrDefault(p => p.name.ToLower().Contains("compound"));
            Airfield = TerrainMeta.Path.Monuments.FirstOrDefault(p => p.name.ToLower().Contains("airfield"));

            if (_config.itemSettings.AlcoholBottleSkinId == 0)
            {
                Unsubscribe(nameof(OnItemAddedToContainer));
                Unsubscribe(nameof(OnItemRemovedFromContainer));
            }

            if (IsPVE())
                Unsubscribe(nameof(OnEntityTakeDamage));

            StartPluginLoad();
        }

        private ItemContainer.CanAcceptResult? CanAcceptItem(ItemContainer container, Item item, Int32 targetPos)
        {
            if (_initiated == false)
                return null;

            if (item.skin != _config.itemSettings.AlcoholItemSkinId && item.skin != _config.itemSettings.WortItemSkinId)
                return null;

            IOEntity ownerEnt = container.entityOwner as IOEntity;
            if (ownerEnt == null)
                return null;

            ownerEnt.ClearConnections();
            return null;
        }
        
        [ChatCommand("remove.custom.npc")]
        void RemoveCustomPosIvan(BasePlayer player, String cmd, String[] args)
        {
            if (!player.IsAdmin) return;
            if (!_plugin.FullLoadedNPC)
            {
                SendReply(player, LanguageEn ? "Wait for the plugin to be fully initialized" : "Дождитесь полной инициализации плагина");
                return;
            }
            if (args == null || args.Length < 1)
            {
                SendReply(player, LanguageEn ? "You didn't specify a name for the position" : "Вы не указали название для позиции");
                return;
            }
            String NamePos = args[0];
            if (String.IsNullOrWhiteSpace(NamePos))
            {
                SendReply(player, LanguageEn ? "You didn't specify a name for the position" : "Вы не указали название для позиции");
                return;
            }

            if (!CustomPosNPC.ContainsKey(NamePos))
            {
                SendReply(player, LanguageEn ? "A position with this name does not exist" : "Позиция с таким названием не существует");
                return;
            }

            CustomNpcController npcController = NPCList.FirstOrDefault(x => x.transform.position == CustomPosNPC[NamePos].LocalPosition);
            UnityEngine.Object.DestroyImmediate(npcController);
            
            Single radius = 5.0f;
            foreach (BaseEntity ent in MonumentEntities.Where(x => !x.IsDestroyed))
            {
                Single distance = Vector3.Distance(ent.transform.position, CustomPosNPC[NamePos].LocalPosition);
                if (distance <= radius)
                {
                    ent.Kill();
                }
            }
            
            CustomPosNPC.Remove(NamePos);

            SendReply(player, LanguageEn ? "The position has been successfully deleted!" : "Позиция успешно удалена!");
        }

        private void DestroyControllers()
        {
            for (Int32 i = BarrelController.ActiveControllers.Count - 1; i >= 0; i--)
            {
                BarrelController activeController = BarrelController.ActiveControllers[i];
                if(activeController != null)
                    UnityEngine.Object.Destroy(activeController);
            }
        }
        private Item OnItemSplit(Item item, int amount)
        {
            if (_initiated == false)
                return null;

            if (plugins.Find("Stacks") || plugins.Find("CustomSkinsStacksFix") || plugins.Find("SkinBox")) return null;
            if(item.info.shortname == "smallwaterbottle" && _config.itemSettings.AlcoholBottleSkinId == item.skin)
            {
                Item x = ItemManager.CreateByPartialName(item.info.shortname, amount);
                x.skin = _config.itemSettings.AlcoholBottleSkinId;
                x.amount = amount;
                item.amount -= amount;
                item.MarkDirty();
                return x;
            }

            if (item.info.shortname == _config.BarrelItem.ShortName && _config.BarrelItem.SkinId == item.skin)
            {
                Item x = ItemManager.CreateByPartialName(item.info.shortname, amount);
                x.name = _config.BarrelItem.DisplayName;
                x.skin = _config.BarrelItem.SkinId;
                x.amount = amount;
                item.amount -= amount;
                item.MarkDirty();
                return x;
            }
		   		 		  						  	   		  		  		  		  		  	   		  	   
            return null;
        }
        

        
        
        
        private List<CustomNpcController> NPCList = new List<CustomNpcController>();

        private void OnEntitySpawned(WaterPurifier entity)
        {
            if (_initiated == false)
                return;

            if (entity is PoweredWaterPurifier)
                return;

            entity.maxStackSize = _config.fermentationSettings.PurifierCapacity;
        }
		   		 		  						  	   		  		  		  		  		  	   		  	   
		   		 		  						  	   		  		  		  		  		  	   		  	   
        private void OnAlcoholDrink(BasePlayer player, Item item, Int32 amountToUse)
        {
            if (_initiated == false)
                return;

            Single time = (amountToUse * _config.drinkedSettings.AlcoholDrinkTime) / _config.drinkedSettings.AlcoholDrinkAmount;
		   		 		  						  	   		  		  		  		  		  	   		  	   
            DrunkPlayer drunkPlayer = _data.DrunkPlayers.Find(x => x.Player.userID == player.userID);
            if (drunkPlayer == null)
            {
                drunkPlayer = new DrunkPlayer
                {
                    UserId = player.userID
                };
                drunkPlayer.DrunkRemaining += 5f;
                _data.DrunkPlayers.Add(drunkPlayer);

                DrawUI_DrunkBlock(player, drunkPlayer.CurrectColor, drunkPlayer.DrunkRemaining);
                DrawUI_DrunkAFade(drunkPlayer.Player, drunkPlayer.CurrectFade);

                if (_data.DrunkPlayers.Count == 1)
                    ServerMgr.Instance.StartCoroutine(PlayersUpdate());
            }

            Int32 index = GetIndexFromGradient(AlcoholColorGradient.Length, (Int32)drunkPlayer.DrunkRemaining, (Int32)_config.drinkedSettings.AlcoholDrinkLimit);
            String color = AlcoholColorGradient[index];

            if (color != drunkPlayer.CurrectColor)
            {
                drunkPlayer.CurrectColor = color;
                DrawUI_DrunkBlock(drunkPlayer.Player, drunkPlayer.CurrectColor, drunkPlayer.DrunkRemaining);
            }

            color = AlcoholBlur[index];

            if (color != drunkPlayer.CurrectFade)
            {
                drunkPlayer.CurrectFade = color;
                DrawUI_DrunkAFade(drunkPlayer.Player, drunkPlayer.CurrectFade);
            }

            drunkPlayer.DrunkRemaining += time;

            DrawUI_DrunkTimer(player, drunkPlayer.CurrectColor, drunkPlayer.DrunkRemaining);

            UpdateModifiers(drunkPlayer);

            if (drunkPlayer.DrunkRemaining >= _config.drinkedSettings.AlcoholDrinkLimit)
            {
                drunkPlayer.Player.modifiers.RemoveAll();
                ExecuteNegativeEffect(player);
            }
        }
        object CanStackItem(Item item, Item targetItem)
        {
            if (_initiated == false)
                return null;

            if (item.skin != targetItem.skin) return false;

            return null;
        }

        private System.Object OnPlayerAddModifiers(BasePlayer player, Item item, ItemModConsumable consumable)
        {
            if (item.skin == _config.itemSettings.AlcoholItemSkinId)
            {
                Int32 oldAmount = item.amount;
                NextTick(() =>
                {
                    Int32 consumed = Math.Abs(item.amount - oldAmount);
                    OnAlcoholDrink(player, item, consumed);
                });
                return false;
            }

            if (item.skin == _config.itemSettings.WortItemSkinId)
            {
                ExecuteNegativeEffect(player);
                return false;
            }
            return null;
        }

        private Quaternion GetMonumentRotation(Single y, MonumentInfo monument, Single x)
        {
            Quaternion monumentQT = Quaternion.Euler(monument.transform.rotation.eulerAngles.x - x, monument.transform.rotation.eulerAngles.y - y, 0f);
            return monumentQT;
        }
        public String GetLang(String LangKey, String userID = null, params System.Object[] args)
        {
            sb.Clear();
            if (args != null)
            {
                sb.AppendFormat(lang.GetMessage(LangKey, this, userID), args);
                return sb.ToString();
            }
            return lang.GetMessage(LangKey, this, userID);
        }

        private void DrawUI_DrunkAFade(BasePlayer player, String fade)
        {
            if (_initiated == false)
                return;
		   		 		  						  	   		  		  		  		  		  	   		  	   
            CuiHelper.DestroyUi(player, UI_LayerFade);
            CuiHelper.AddUi(player, DrunkFade.Replace("{C}", fade));
        }


        
        
        
        private void Init() => ReadData();
        private String DrunkFade;

        private struct CustomItem
        {
            public CustomItem(String displayName, String shortName, UInt64 skinId)
            {
                DisplayName = displayName;
                ShortName = shortName;
                SkinId = skinId;
            }

            public String DisplayName;
            public String ShortName;
            public UInt64 SkinId;

            public Item ToItem(Int32 amount = 1)
            {
                Item item = ItemManager.CreateByName(ShortName, amount, SkinId);
                if (item != null && String.IsNullOrEmpty(DisplayName) == false)
                    item.name = DisplayName;

                return item;
            }

            public Boolean CompareTo(Item item)
            {
                return SkinId == item?.skin && ShortName == item?.info.shortname;
            }
        }
        private String GetImage(String fileName, UInt64 skin = 0)
        {
            String imageId = (String)plugins.Find("ImageLibrary").CallHook("ImageUi.GetImage", fileName, skin);
            if (!String.IsNullOrEmpty(imageId))
                return imageId;
            return String.Empty;
        }

        private System.Object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (_initiated == false)
                return null;

            if (container.skinID == 23129547)
            {
                DrawUI_Trade_Main(player);
                return false;
            }
            return null;
        }

        private void UpdateModifiers(DrunkPlayer drunkPlayer)
        {
            List<ModifierDefintion> modifiersToAdd = new List<ModifierDefintion>();
            for (Int32 i = 0; i < _config.ModifierDefintions.Count; i++)
            {
                ModifierDefintionConfig modif = _config.ModifierDefintions[i];
                if (modif.MinTime > drunkPlayer.DrunkRemaining)
                    continue;

                if (drunkPlayer.ModifiersUpdate < modif.MinTime)
                    drunkPlayer.ModifiersUpdate = modif.MinTime;

                ModifierDefintion modifDef = modif.GetDef();
                modifDef.duration = drunkPlayer.DrunkRemaining;
                modifiersToAdd.Add(modifDef);

            }

            if (modifiersToAdd.Count < 1)
                return;

            drunkPlayer.Player.modifiers.Add(modifiersToAdd);
        }
        private enum CategoryType
        {
            Equipment,
            Exchanger,
        }

        private System.Object OnNpcTarget(BaseAnimalNPC animal, BasePlayer target)
        {
            if (_initiated == false)
                return null;

            if (animal == null || target == null) return null;
            if (target.OwnerID == 199949458)
                return false;
            return null;
        }

        
                private static IQAlcoholFarm _plugin;

        private static void RegisterBarrel(BarrelController barrelController)
        {
            if (BarrelController.ActiveControllers.Contains(barrelController) == false)
                BarrelController.ActiveControllers.Add(barrelController);
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        
                public static StringBuilder sb = new StringBuilder();
        private const String SignImgRight = "https://i.imgur.com/ZyaqUtT.png";

        
                private void ClearEnt(MonumentInfo monument, Vector3 Pos)
        {
            List<BaseEntity> obj = new List<BaseEntity>();
            Int32 Mask = LayerMask.GetMask("AI", "Player (Server)", "Construction", "Deployable", "Deployed", "Ragdoll", "Transparent");
            Vis.Entities(Pos, 1000f, obj, Mask);

            foreach (BaseEntity item in obj.Where(x => x.OwnerID == 199949458 || x.skinID == 199949458))
            {
                if (item == null)
                    continue;
                item.Kill(BaseNetworkable.DestroyMode.None);
            }
        }

        private static Double GetCurrentTime()
        {
            return new TimeSpan(DateTime.UtcNow.Ticks).TotalSeconds;
        }
        private Boolean IsPVE()
        {
            return TruePVE != null || NextGenPVE != null || Imperium != null;
        }
        
        private static PluginData _data;

        private const String AlcoholConsumeEffect = "assets/bundled/prefabs/fx/gestures/drink_vomit.prefab";
        private String FormatTime(TimeSpan timespan)
        {
            return String.Format(timespan.TotalHours >= 1 ? "{2:00}:{0:00}:{1:00}" : "{0:00}:{1:00}", timespan.Minutes, timespan.Seconds, System.Math.Floor(timespan.TotalHours));
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<PluginConfig>();
                if (_config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                for (Int32 i = 0; i < 3; i++)
                {
                    PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                }

                LoadDefaultConfig();
                return;
            }

            ValidateConfig();
            SaveConfig();
        }

        private class Point
        {
            public String Name;
            public Vector3 Pos;
            public Single Y;
            public Single X;
            public String LinkImage;
        }

        private static readonly String[] AlcoholBlur = new[]
        {
            "0 0 0 0.05",
            "0 0 0 0.07",
            "0 0 0 0.10",
            "0 0 0 0.12",
            "0 0 0 0.15",
            "0 0 0 0.2",
        };
        private const Boolean LanguageEn = false;
        private class CustomNpcController : MonoBehaviour
        {
            private BasePlayer _npcPlayer;
            private SphereCollider _sphereCollider;

            private BasePlayer lastWavedAtPlayer = null;

            private List<BasePlayer> _players = new List<BasePlayer>();

            private void Awake()
            {
                gameObject.layer = (int)Layer.Reserved1;
                gameObject.name = "CustomNpcController";

                _npcPlayer = GetComponent<BasePlayer>();
                if (_npcPlayer == null)
                {
                    _plugin.PrintError("Failed to init controller for npc!");
                    Destroy(this);
                    return;
                }

                InitCollider();
		   		 		  						  	   		  		  		  		  		  	   		  	   
                InvokeRepeating(nameof(UpdateLook), 1f, 1f);
            }
            private void OnDestroy()
            {
                if (!_npcPlayer.IsDestroyed)
                    _npcPlayer.Kill();
            }

            private void UpdateLook()
            {
                if (_players.Count < 1)
                    return;

                var basePlayer = _players[0];
                if (lastWavedAtPlayer == basePlayer)
                    return;

                _npcPlayer.SignalBroadcast(BaseEntity.Signal.Gesture, "hurry", null);
                lastWavedAtPlayer = basePlayer;
            }
                     
            private void InitCollider()
            {
                _sphereCollider = gameObject.GetComponent<SphereCollider>();
                if (_sphereCollider == null)
                {
                    _sphereCollider = gameObject.AddComponent<SphereCollider>();
                    _sphereCollider.isTrigger = true;
                }
                _sphereCollider.radius = 1.5f;
            }

            private void OnTriggerEnter(Collider other)
            {
                BaseEntity baseEntity = other.ToBaseEntity();
                if (!baseEntity.IsValid())
                    return;

                var player = baseEntity as BasePlayer;
                if (player != null && player != _npcPlayer)
                {
                    if (player.IsNpc)
                        return;

                    if (_players.Contains(player) == false)
                        _players.Add(player);
		   		 		  						  	   		  		  		  		  		  	   		  	   
                    if (_config.UseNPCSounds)
                    {
                        Data = ConvertedRustBytes(_plugin.RenderSounds["welcome"].GetRandom());
                        SoundPlay();
                    }
                }
            }

            private void OnTriggerExit(Collider other)
            {
                BaseEntity baseEntity = other.ToBaseEntity();
                if (!baseEntity.IsValid())
                    return;

                var player = baseEntity as BasePlayer;
                if (player != null && player != _npcPlayer)
                {
                    if (player.IsNpc)
                        return;

                    CuiHelper.DestroyUi(player, InterfaceBuilder.UI_TRADER);
                    CuiHelper.DestroyUi(player, InterfaceBuilder.UI_PANEL_ITEMS);
                    _players.Remove(player);

                    if (_config.UseNPCSounds)
                    {
                        Data = ConvertedRustBytes(_plugin.RenderSounds["bye"].GetRandom());
                        SoundPlay();
                    }
                }
            }

            

            public List<ulong> BotAlerts = new List<ulong>();
            public Coroutine SoundRoutine { get; set; }
            public List<byte[]> Data = new List<byte[]>();
            public void SoundPlay()
            {
                if (BotAlerts.Contains(_npcPlayer.net.ID.Value))
                    return;
                else
                    BotAlerts.Add(_npcPlayer.net.ID.Value);

                if (SoundRoutine == null)
                    SoundRoutine = InvokeHandler.Instance.StartCoroutine(API_NPC_SendToAll());
            }
            public IEnumerator API_NPC_SendToAll()
            {
                if (Data == null)
                {
                    SoundRoutine = null;
                    BotAlerts.Remove(_npcPlayer.net.ID.Value);
                    yield break;
                }
                yield return CoroutineEx.waitForSeconds(0.1f);

                foreach (var data in Data)
                {
                    if (_npcPlayer == null)
                        break;
                    SendSound(_npcPlayer.net.ID.Value, data);
                    yield return CoroutineEx.waitForSeconds(0.07f);
                }
                SoundRoutine = null;
                BotAlerts.Remove(_npcPlayer.net.ID.Value);
                yield break;
            }

            public void SendSound(ulong netId, byte[] data)
            {
                if (!Net.sv.IsConnected())
                    return;
                foreach (BasePlayer current in BasePlayer.activePlayerList.Where(current => current.IsConnected && Vector3.Distance(_npcPlayer.transform.position, current.transform.position) <= 100))
                {
                    if (_npcPlayer == null)
                        return;
                    
                    NetWrite netWrite = Network.Net.sv.StartWrite();
                    netWrite.PacketID(Message.Type.VoiceData);
                    netWrite.UInt64(netId);
                    netWrite.BytesWithSize(data);
                    netWrite.Send(new SendInfo(current.Connection) { priority = Priority.Immediate });
                }
            }

            private List<byte[]> ConvertedRustBytes(byte[] bytes)
            {
                List<int> dataSize = new List<int>();
                List<byte[]> dataBytes = new List<byte[]>();

                int offset = 0;
                while (true)
                {
                    dataSize.Add(BitConverter.ToInt32(bytes, offset));
                    offset += 4;

                    int sum = dataSize.Sum();
                    if (sum == bytes.Length - offset)
                    {
                        break;
                    }

                    if (sum > bytes.Length - offset)
                    {
                        throw new ArgumentOutOfRangeException(nameof(dataSize),
                            $"Voice Data is outside the saved range {dataSize.Sum()} > {bytes.Length - offset}");
                    }
                }

                foreach (int size in dataSize)
                {
                    dataBytes.Add(bytes.Skip(offset).Take(size).ToArray());
                    offset += size;
                }

                return dataBytes;
            }
                    }

        
                private Boolean IsCompound()
        {
            return _config.storeNpc.TurnedShop.UseCompound && Compound != null;
        }
        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (_initiated == false)
                return;

            _data.DrunkPlayers.RemoveAll(x => x.Player.userID == player.userID);
            DestroyAllUi(player);
        }

        private void OnPlayerDisconnected(BasePlayer player, String reason)
        {
            if (_initiated == false)
                return;

            _data.DrunkPlayers.RemoveAll(x => x.Player.userID == player.userID);
            DestroyAllUi(player);
        }

        
        
        private static Boolean _initiated = false;
        object CanCombineDroppedItem(DroppedItem item, DroppedItem targetItem)
        {
            if (_initiated == false)
                return null;

            if (item.GetItem().skin != targetItem.GetItem().skin) return false;

            return null;
        }

                [PluginReference] private readonly Plugin ImageLibrary;

        private void DrawUI_DrunkTimer(BasePlayer player, String color, Single timeLeft)
        {
            if (_initiated == false)
                return;

            String gui = DrunkTimer;

            gui = gui.Replace("{T}", FormatTime(TimeSpan.FromSeconds(timeLeft)));
            gui = gui.Replace("{C}", GetColor(color));

            CuiHelper.DestroyUi(player, UI_LayerTimer);
            CuiHelper.AddUi(player, gui);
        }

                [PluginReference] private readonly Plugin TruePVE, NextGenPVE, Imperium;
        private const String UI_LayerFade = "UI_AlcoholFarm.Fade";

        private void DrawUI_PanelCategory(BasePlayer player, CategoryType categoryType = CategoryType.Equipment, Int32 Page = 0)
        {
            CuiHelper.DestroyUi(player, "CategoryTool");
            CuiHelper.DestroyUi(player, "CategoryTrade");

            String Interface = InterfaceBuilder.GetInterface("UI_TRADER_NPC_CATEGORY_MENU");
            if (Interface == null) return;

            Interface = Interface.Replace("%COLOR_EXCHANGER%", categoryType == CategoryType.Exchanger ? "1 1 1 1" : "1 1 1 0.3");
            Interface = Interface.Replace("%COLOR_EQUIPMENT%", categoryType == CategoryType.Equipment ? "1 1 1 1" : "1 1 1 0.3");
            Interface = Interface.Replace("%UI_TITLE_EQUIPMENT%", GetLang("UI_TITLE_EQUIPMENT", player.UserIDString));
            Interface = Interface.Replace("%UI_TITLE_EXCHANGER%", GetLang("UI_TITLE_EXCHANGER", player.UserIDString));

            CuiHelper.AddUi(player, Interface);
        }
        
        private void TakeItems(BasePlayer player, PluginConfig.ShopStore.CategorySettings.ItemsPair BuyItem, ItemContainer container = null, Int32 Amount = 1)
        {
            List<Item> ItemList = container == null ? player.inventory.AllItems().ToList() : container.itemList;
            Int32 TargetAmount = BuyItem.PriceItems.Amount * Amount;
            List<Item> acceptedItems = new List<Item>();
            Int32 itemAmount = 0;

            foreach (Item item in player.inventory.AllItems())
            {
                if (item.info.shortname.Equals(BuyItem.PriceItems.Shortname) && item.skin == BuyItem.PriceItems.SkinID)
                {
                    acceptedItems.Add(item);
                    itemAmount += item.amount;
                }
            }

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
		   		 		  						  	   		  		  		  		  		  	   		  	   
            foreach (Item ItemPlayer in ItemList)
            {
                if (ItemPlayer.contents != null)
                {
                    foreach (Item item1 in ItemPlayer.contents.itemList.ToArray())
                    {
                        if (item1.info.shortname.Equals(BuyItem.PriceItems.Shortname) && item1.skin == BuyItem.PriceItems.SkinID)
                        {
                            if(TargetAmount == 0)
                                continue;
                            if (item1.amount == TargetAmount)
                            {
                                item1.RemoveFromContainer();
                                item1.Remove();
                                TargetAmount = 0;
                                break;
                            }

                            if (item1.amount > TargetAmount)
                            {
                                item1.amount -= TargetAmount;
                                player.inventory.SendSnapshot();
                                TargetAmount = 0;
                                break;
                            }

                            if (item1.amount < TargetAmount)
                            {
                                TargetAmount -= item1.amount;
                                item1.RemoveFromContainer();
                                item1.Remove();
                            }
                        }
                    }
                }
            }
        }
        private class InterfaceBuilder
        {
            
            public static InterfaceBuilder Instance;
            public const String UI_TRADER = "UI_TRADER";
            public const String UI_PANEL_ITEMS = "UI_PANEL_ITEMS";
            public Dictionary<String, String> Interfaces;

            
            
            public InterfaceBuilder()
            {
                Instance = this;
                Interfaces = new Dictionary<String, String>();
                BuildingTrader();
                BuildingTrader_Category();
                BuildingTrader_Panel();
                BuildingTrader_ItemPanel();
                BuildingTrader_ItemPanel_ControllerAmount();
            }

            public static void AddInterface(String name, String json)
            {
                if (Instance.Interfaces.ContainsKey(name))
                {
                    _plugin.PrintError($"Error! Tried to add existing cui elements! -> {name}");
                    return;
                }

                Instance.Interfaces.Add(name, json);
            }

            public static String GetInterface(String name)
            {
                String json = String.Empty;
                if (Instance.Interfaces.TryGetValue(name, out json) == false)
                {
                    _plugin.PrintWarning($"Warning! UI elements not found by name! -> {name}");
                }

                return json;
            }

            public static void DestroyAll()
            {
                for (Int32 i = 0; i < BasePlayer.activePlayerList.Count; i++)
                {
                    BasePlayer player = BasePlayer.activePlayerList[i];
		   		 		  						  	   		  		  		  		  		  	   		  	   
                    CuiHelper.DestroyUi(player, UI_TRADER);
                    CuiHelper.DestroyUi(player, UI_PANEL_ITEMS);
                }
            }

            
                        private void BuildingTrader()
            {
                CuiElementContainer container = new CuiElementContainer
                {
                    {
                        new CuiPanel
                        {
                            CursorEnabled = true,
                            Image = { Color = "1 1 1 0.0627451" },
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
                        },
                        "Overlay",
                        UI_TRADER
                    },

                    new CuiElement
                    {
                        Name = "BACKGROUND_IMAGE",
                        Parent = UI_TRADER,
                        Components = {
                            new CuiRawImageComponent { Color = "1 1 1 1", Png = ImageUi.GetImage("UI_IQALCOHOL_BACKGORUND_0") },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                        }
                    },

                    {
                        new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Close = UI_TRADER },
                            Text = { Text = "", Font = "robotocondensed-regular.ttf", FontSize = 17, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
                        },
                        UI_TRADER,
                        "CloseButton"
                    },

                    new CuiElement
                    {
                        Name = "TitleProduct",
                        Parent = UI_TRADER,
                        Components = {
                            new CuiTextComponent { Text = "%UI_TITLE_ITEM%", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.UpperCenter, Color = "1 1 1 1" },
                            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-181.062 216.907", OffsetMax = "-123.298 235.8930" }//
                        }
                    },

                    new CuiElement
                    {
                        Name = "TitlePrice",
                        Parent = UI_TRADER,
                        Components = {
                            new CuiTextComponent { Text = "%UI_TITLE_PRICE%", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.UpperCenter, Color = "1 1 1 1" },
                            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-50.482 216.907", OffsetMax = "7.282 235.8930" } //
                        }
                    },

                    new CuiElement
                    {
                        Name = "TitleAmount",
                        Parent = UI_TRADER,
                        Components = {
                            new CuiTextComponent { Text = "%UI_TITLE_AMOUNT%", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.UpperCenter, Color = "1 1 1 1" },
                            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "76.648 216.907", OffsetMax = "162.199 235.893" }
                        }
                    },

                    new CuiElement
                    {
                        Name = "TitleInformationClose",
                        Parent = UI_TRADER,
                        Components = {
                            new CuiTextComponent { Text = "%UI_EXIT_BUTTON%", Font = "robotocondensed-regular.ttf", FontSize = 13, Align = TextAnchor.UpperCenter, Color = "1 1 1 1" },
                            new CuiRectTransformComponent { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-136.344 40.032", OffsetMax = "136.344 61.568" }
                        }
                    },

                    {
                        new CuiPanel
                        {
                            CursorEnabled = false,
                            Image = { Color = "0.4156863 0.4196079 0.4352942 1" },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-31.3 -193.46", OffsetMax = "25.777 -173.66" }
                        },
                        UI_TRADER,
                        "InfromationPagePanel"
                    },
                };
		   		 		  						  	   		  		  		  		  		  	   		  	   
                AddInterface("UI_TRADER_NPC", container.ToJson());
            }
            
            private void BuildingTrader_Category()
            {
                CuiElementContainer container = new CuiElementContainer
                {
                    {
                        new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = $"func.alcohol category select {CategoryType.Equipment}" },
                            Text = { Text = "%UI_TITLE_EQUIPMENT%", Font = "robotocondensed-regular.ttf", FontSize = 17, Align = TextAnchor.MiddleCenter, Color = "%COLOR_EQUIPMENT%"},
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-121.284 300.498", OffsetMax = "1.982 323.902" }
                        },
                            UI_TRADER,
                            "CategoryTool"
                    },

                    {
                        new CuiButton
                        {
                            Button = { Color = "0 0 0 0", Command = $"func.alcohol category select {CategoryType.Exchanger}" },
                            Text = { Text = "%UI_TITLE_EXCHANGER%", Font = "robotocondensed-regular.ttf", FontSize = 17, Align = TextAnchor.MiddleCenter, Color = "%COLOR_EXCHANGER%" },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "15.843 300.499", OffsetMax = "116.445 323.901" }
                        },
                            UI_TRADER,
                            "CategoryTrade"
                    },
                };

                AddInterface("UI_TRADER_NPC_CATEGORY_MENU", container.ToJson());
            }

            private void BuildingTrader_Panel()
            {
                CuiElementContainer container = new CuiElementContainer
                {
                    {
                        new CuiPanel
                        {
                            CursorEnabled = false,
                            Image = { Color = "0 0 0 0" },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-183.682 -158.742", OffsetMax = "176.398 205.28" }
                        },
                        UI_TRADER,
                        UI_PANEL_ITEMS
                    },

                     new CuiElement
                     {
                        Name = "TitlePage",
                        Parent = "InfromationPagePanel",
                        Components = {
                            new CuiTextComponent { Text = "%COUNT_PAGE%", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-28.538 -9.9", OffsetMax = "28.539 9.9" }
                        }
                     },

                    {
                        new CuiButton
                        {
                            Button = { Color = "0.4156863 0.4196079 0.4352942 1", Command = "%COMMAND_BACK%" },
                            Text = { Text = "%BACK_BUTTON%", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "%COLOR_BACK%" },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-182.78 -193.459", OffsetMax = "-126.069 -173.66" },
                        },
                        UI_TRADER,
                        "BackButton"
                    },

                    {
                        new CuiButton
                        {
                            Button = { Color = "0.4156863 0.4196079 0.4352942 1", Command = "%COMMAND_NEXT%" },
                            Text = { Text = "%NEXT_BUTTON%", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "%COLOR_NEXT%" },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "119.684 -193.459", OffsetMax = "176.396 -173.66" }
                        },
                        UI_TRADER,
                        "NextButton"
                    },
                };

                AddInterface("UI_TRADER_NPC_ITEMPANEL_TEMPLATE", container.ToJson());
            }


            private void BuildingTrader_ItemPanel()
            {
                CuiElementContainer container = new CuiElementContainer
                {
                    {
                        new CuiPanel
                        {
                            CursorEnabled = false,
                            Image = { Color = "0 0 0 0" },
                            RectTransform = {  AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "%OFFSET_MIN%", OffsetMax = "%OFFSET_MAX%" }
                        },
                        UI_PANEL_ITEMS,
                        "PANEL_ITEM_%Y%"
                    },

                    new CuiElement
                    {
                        Name = "ItemContainerIn_%Y%",
                        Parent = "PANEL_ITEM_%Y%",
                        Components = {
                            new CuiRawImageComponent { Color = "1 1 1 1", Png = ImageUi.GetImage("UI_IQALCOHOL_BACK_ITEM_0")  },
                            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-180.04 -31.5", OffsetMax = "-117.04 31.5" }
                        }
                    },

                    new CuiElement
                    {
                        Name = "Line",
                        Parent = "ItemContainerIn_%Y%",
                        Components = {
                            new CuiRawImageComponent { Color = "0.9647059 0.9019608 0.372549 1", Png = ImageUi.GetImage("UI_IQALCOHOL_LINE_0") },
                            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-23.267 -32.64", OffsetMax = "1.607 -28.64" }
                        }
                    },

                    new CuiElement
                    {
                        Name = "ItemContainerTo_%Y%",
                        Parent = "PANEL_ITEM_%Y%",
                        Components = {
                            new CuiRawImageComponent { Color = "1 1 1 1", Png = ImageUi.GetImage("UI_IQALCOHOL_BACK_ITEM_0") },
                            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-49.46 -31.5", OffsetMax = "13.54 31.5" }
                        }
                    },
                    
                    new CuiElement
                    {
                        Name = "Line",
                        Parent = "ItemContainerTo_%Y%",
                        Components = {
                            new CuiRawImageComponent { Color = "0.9647059 0.9019608 0.372549 1", Png = ImageUi.GetImage("UI_IQALCOHOL_LINE_0") },
                            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-23.267 -32.64", OffsetMax = "1.607 -28.64" }
                        }
                    },
                };

                AddInterface("UI_TRADER_NPC_ITEMPANEL", container.ToJson());
            }
            
            public static void BuildingTrader_Items(BasePlayer player, Int32 ItemIDBuy, UInt64 SkinIDBuy, Int32 ItemIDPrice, UInt64 SkinIDPrice, Int32 Y)
            {
                CuiElementContainer container = new CuiElementContainer
                {
                    new CuiElement
                    {
                        Name = "Item",
                        Parent = $"ItemContainerIn_{Y}",
                        Components = {
                            new CuiImageComponent() { Color = "1 1 1 1", ItemId = ItemIDBuy, SkinId = SkinIDBuy},
                            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-24 -21.3", OffsetMax = "24 26.7" }
                        }
                    },
                    
                    new CuiElement
                    {
                        Name = "Item",
                        Parent = $"ItemContainerTo_{Y}",
                        Components = {
                            new CuiImageComponent() { Color = "1 1 1 1", ItemId = ItemIDPrice, SkinId = SkinIDPrice},
                            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-24 -21.3", OffsetMax = "24 26.7" }
                        }
                    },
                };

                CuiHelper.AddUi(player, container);
            }

            private void BuildingTrader_ItemPanel_ControllerAmount()
            {
                CuiElementContainer container = new CuiElementContainer
                {
                    new CuiElement
                    {
                        Name = "ItemAmount_In_%Y%",
                        Parent = "ItemContainerIn_%Y%",
                        Components = {
                            new CuiTextComponent { Text = "<b>x%AMOUNT_IN%</b>", Font = "robotocondensed-regular.ttf", FontSize = 9, Align = TextAnchor.UpperRight, Color = "1 1 1 1" },
                            new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-10.68 -29.946", OffsetMax = "26.548 -16.534" }
                        }
                    },

                    new CuiElement
                    {
                        Name = "ItemAmount_To_%Y%",
                        Parent = "ItemContainerTo_%Y%",
                        Components = {
                            new CuiTextComponent { Text = "<b>x%AMOUNT_TO%</b>", Font = "robotocondensed-regular.ttf", FontSize = 9, Align = TextAnchor.UpperRight, Color = "1 1 1 1" },
                            new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-10.68 -29.946", OffsetMax = "26.548 -16.534" }
                        }
                    },

                    {
                        new CuiButton
                        {
                            Button = { Color = "%BUY_BUTTON_COLOR%", Command = "%BUY_BUTTON_COMMAND%" },
                            Text = { Text = "%BUY_TITLE%", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "79.658 -15.471", OffsetMax = "164.803 15.471" }
                        },
                        "PANEL_ITEM_%Y%",
                        "ButtonBuy_%Y%"
                    },

                    {
                        new CuiButton
                        {
                            Button = { Color = "%COLOR_BTN%", Command = "%CMD_MINUS%"  },
                            Text = { Text = "<b>-</b>", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "64.427 -15.471", OffsetMax = "79.657 15.471" }
                        },
                        "PANEL_ITEM_%Y%",
                        "ButtonPlusAmount_%Y%"
                    },

                    {
                        new CuiButton
                        {
                            Button = { Color = "%COLOR_BTN%", Command = "%CMD_PLUS%"},
                            Text = { Text = "<b>+</b>", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "164.805 -15.471", OffsetMax = "180.035 15.471" }
                        },
                        "PANEL_ITEM_%Y%",
                        "ButtonMinusAmount_%Y%"
                    },
                };
		   		 		  						  	   		  		  		  		  		  	   		  	   
                AddInterface("UI_TRADER_NPC_ITEMPANEL_CONTROLLER_AMOUNT", container.ToJson());
            }
        }
		   		 		  						  	   		  		  		  		  		  	   		  	   
        private void ExecuteNegativeEffect(BasePlayer player)
        {
            Vector3 vector3 = (player.IsDucked() ? new Vector3(0f, 1f, 0f) : new Vector3(0f, 2f, 0f));
            EffectNetwork.Send(new Effect(AlcoholConsumeEffect, player, 0, vector3, Vector3.zero, null), player.net.connection);
            EffectNetwork.Send(new Effect(AlcoholShakeEffect.GetRandom(), vector3, Vector3.zero), player.net.connection);

            player.Hurt(_config.drinkedSettings.AlcoholDrunkNegativeDamage);
            player.metabolism.calories.Subtract(_config.drinkedSettings.AlcoholDrunkNegativeCalories);
            player.metabolism.hydration.Subtract(_config.drinkedSettings.AlcoholDrunkNegativeHydration);
        }

        private void OnServerSave()
        {
            PluginData.SaveData();
        }
        private String DrunkTimer;
        
        
        private void DrawUI_Trade_Main(BasePlayer player)
        {
            String Interface = InterfaceBuilder.GetInterface("UI_TRADER_NPC");
            if (Interface == null) return;

            Interface = Interface.Replace("%UI_TITLE_ITEM%", GetLang("UI_TITLE_ITEM", player.UserIDString));
            Interface = Interface.Replace("%UI_TITLE_PRICE%", GetLang("UI_TITLE_PRICE", player.UserIDString));
            Interface = Interface.Replace("%UI_TITLE_AMOUNT%", GetLang("UI_TITLE_AMOUNT", player.UserIDString));
            Interface = Interface.Replace("%UI_EXIT_BUTTON%", GetLang("UI_EXIT_BUTTON", player.UserIDString));

            CuiHelper.DestroyUi(player, InterfaceBuilder.UI_TRADER);
            CuiHelper.AddUi(player, Interface);

            DrawUI_ItemPanel(player);
            DrawUI_PanelCategory(player);
        }
        
                private void SpawnTables(Vector3 Position, Quaternion Rotation, String Image)
        {
            String Prefab = "assets/prefabs/deployable/signs/sign.large.wood.prefab";
            Signage Tables = GameManager.server.CreateEntity(Prefab, Position, Rotation) as Signage;
            Tables.Spawn();
            UnityEngine.Object.DestroyImmediate(Tables.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.DestroyImmediate(Tables.GetComponent<GroundWatch>());
            Tables.OwnerID = 199949458;
            ServerMgr.Instance.StartCoroutine(DownloadImage(Image, Tables, "sign.wooden.large"));
            Tables.SetFlag(BaseEntity.Flags.Busy, true);
            Tables.SetFlag(BaseEntity.Flags.Locked, true);
            MonumentEntities.Add(Tables);
        }

        
        
        [ConsoleCommand("func.alcohol")]
        private void FuncionalCommad(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;

            String Funcion = arg.Args[0];

            switch (Funcion)
            {
                case "category": 
                    {
                        String Action = arg.Args[1];
                        CategoryType Category = (CategoryType)Enum.Parse(typeof(CategoryType), arg.Args[2]);

                        switch (Action)
                        {
                            case "select":
                                {
                                    DrawUI_ItemPanel(player, Category);
                                    DrawUI_PanelCategory(player, Category);
                                    break;
                                }
                            case "page":
                                {
                                    String ActionPage = arg.Args[3];
                                    Int32 Page = Int32.Parse(arg.Args[4]);
                                    switch (ActionPage)
                                    {
                                        case "next":
                                            {
                                                DrawUI_ItemPanel(player, Category, Page + 1);
                                                break;
                                            }
                                        case "back":
                                            {
                                                DrawUI_ItemPanel(player, Category, Page - 1);
                                                break;
                                            }
                                    }
                                    break;
                                }
                            case "selling": //
                                {
                                    String ActionSelling = arg.Args[3];
                                    Int32 Y = Int32.Parse(arg.Args[4]);
                                    Int32 Page = Int32.Parse(arg.Args[5]);
                                    Int32 Amount = Int32.Parse(arg.Args[6]);
                                    if (Amount == 0) return;

                                    List<PluginConfig.ShopStore.CategorySettings.ItemsPair> CategoryItems = Category == CategoryType.Equipment ? _config.storeNpc.EquipmentCategory.ItemsList : _config.storeNpc.ExchangerCategory.ItemsList;
                                    PluginConfig.ShopStore.CategorySettings.ItemsPair CategoryItem = CategoryItems.Skip(Page * 5).Take(5).ToList()[Y];

                                    switch (ActionSelling)
                                    {
                                        case "amount.next":
                                            {
                                                DrawUI_Items_AmountController(player, Y, CategoryItem, Page, Amount + 1, Category);
                                                break;
                                            }
                                        case "amount.back":
                                            {
                                                if (Amount - 1 <= 0) return;
                                                DrawUI_Items_AmountController(player, Y, CategoryItem, Page, Amount - 1, Category);
                                                break;
                                            }
                                        case "amount.buy":
                                            {
                                                BuyItem(player, CategoryItem, Category, Page, Amount);
                                                break;
                                            }
                                    }
                                    break;
                                }
                        }
                        break;
                    }
            }
        }

        object OnItemAction(Item item, string action, BasePlayer player)
        {
            if(action.Equals("consume") && item.skin != 0)
                foreach(PluginConfig.ShopStore.CategorySettings.ItemsPair ItemPair in _config.storeNpc.EquipmentCategory.ItemsList)
                    if (ItemPair.BuyItems.SkinID == item.skin && ItemPair.BuyItems.Shortname.Equals(item.info.shortname))
                        return false;

            return null;
        }

        internal class CustomPos
        {
            public Vector3 LocalPosition;
            public QuaternionPos LocalRotation;

            public Quaternion GetRotation()
            {
                return new Quaternion(LocalRotation.X, LocalRotation.Y, LocalRotation.Z, LocalRotation.W);
            }
            internal class QuaternionPos
            {
                public Single X;
                public Single Y;
                public Single Z;
                public Single W;
            }
        }

        private static PluginConfig _config;

        private Boolean IsInitializeNPCShop()
        {
            Int32 CountSpawn = 0;
            if (IsCompound())
                CountSpawn++;
            if (IsBanitTown())
                CountSpawn++;
            if (IsAirfield())
                CountSpawn++;

            return CountSpawn != 0;
        }

        private String DrunkMain;

        private void BuildInterface()
        {
            CuiElementContainer container = new CuiElementContainer()
            {
                {
                    new CuiElement
                    {
                        Parent = "Overlay",
                        Name = UI_Layer,
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = "0 0 0 0"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"1 1",
                                AnchorMax = $"1 1",
                                OffsetMin = "-50 -70",
                                OffsetMax = "0 -50"
                            },
                        }
                    }
                },
                {
                    new CuiElement
                    {
                        Parent = UI_Layer,
                        Name = $"{UI_Layer}.Logo",
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = ImageUi.GetImage("UI_ALCOHOL_TIME_0"), Color = "{C}"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"0 0",
                                AnchorMax = $"0 1",
                                OffsetMin = "-15 0",
                                OffsetMax = "10 0"
                            },
                        }
                    }
                },
            };
            DrunkMain = container.ToJson();

            container = new CuiElementContainer()
            {
                {
                    new CuiElement
                    {
                        Parent = UI_Layer,
                        Name = UI_LayerTimer,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = "{T}", Align = TextAnchor.MiddleCenter, Color = "{C}", FontSize = 12
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"0 0",
                                AnchorMax = $"1 1",
                                OffsetMax = "0 0"
                            },
                            new CuiOutlineComponent
                            {
                                Color = "0 0 0 1", Distance = "0.1 0.1"
                            }
                        }
                    }
                },
            };
            DrunkTimer = container.ToJson();

            container = new CuiElementContainer()
            {
                {
                    new CuiElement
                    {
                        Parent = "Under",
                        Name = UI_LayerFade,
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = "{C}", Material = "assets/content/ui/uibackgroundblur.mat"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = $"0 0",
                                AnchorMax = $"1 1"
                            }
                        }
                    }
                },
            };
            DrunkFade = container.ToJson();
        }

        private Boolean IsBanitTown()
        {
            return _config.storeNpc.TurnedShop.UseBanditTown && BanditTown != null;
        }

        private readonly List<BaseEntity> MonumentEntities = new List<BaseEntity>();

        
        
        private const String UI_Layer = "UI_AlcoholFarm";

        
        
        
        private Boolean HaveItem(BasePlayer player, String Shortname, Int32 Amount, UInt64 SkinID = 0, ItemContainer contaner = null)
        {
            Int32 ItemAmount = 0;

            foreach (Item ItemRequires in contaner == null ? player.inventory.AllItems().ToList() : contaner.itemList)
            {
                if (ItemRequires == null) continue;
                if (ItemRequires.skin != SkinID || ItemRequires.info.shortname != Shortname)
                {
                    if (ItemRequires.contents != null)
                    {
                        foreach (Item item1 in ItemRequires.contents.itemList)
                        {
                            if (item1.info.shortname == Shortname && item1.skin == SkinID)
                                ItemAmount += item1.amount;
                        }
                    }

                    continue;
                }

                ItemAmount += ItemRequires.amount;
            }
            return ItemAmount >= Amount;
        }

        private void CheckStatus()
        {
            if (!(Boolean)ImageLibrary.Call("IsReady"))
            {
                PrintError(LanguageEn ? "Plugin is not ready! Images are loading." : "Плагин еще не готов, загружаем изображения");
                timer.Once(10f, () => CheckStatus());
            }
            else
            {
                FullLoad();
                PrintWarning(LanguageEn ? "Plugin succesfully loaded!" : "Плагин успешно загружен");
            }
        }

        private System.Object OnWaterPurify(WaterPurifier waterPurifier, Single timeCooked)
        {
            if (_initiated == false)
                return null;

            Item item = waterPurifier.inventory.GetSlot(0);
            if (item == null)
                return null;

            if (item.skin == _config.itemSettings.AlcoholItemSkinId)
                return false;

            if (item.skin != _config.itemSettings.WortItemSkinId)
            {
                Item outputItem = waterPurifier.waterStorage.inventory.GetSlot(0);
                if (outputItem != null && outputItem.skin != 0)
                    return false;

                return null;
            }
		   		 		  						  	   		  		  		  		  		  	   		  	   
            ConvertWort(waterPurifier, timeCooked);
            return false;
        }

        [ConsoleCommand("barrel.add")]
        private void Console_BarrelAdd(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
		   		 		  						  	   		  		  		  		  		  	   		  	   
            BasePlayer player = arg.Player();
            
            if (arg.Args == null || arg.Args.Length < 1)
            {
                if (player == null) Puts("Wrong syntax! Usage: barrel.add <SteamID:Name:IP>");
                else player.ConsoleMessage("Wrong syntax! Usage: barrel.add <SteamID:Name:IP>");
                return;
            }
            
            String playerid = arg.Args[0];
            BasePlayer basePlayer = BasePlayer.Find(playerid);
            
            if (basePlayer == null)
            {
                if (player == null) Puts("Error! Player not found!");
                else player.ConsoleMessage("Error! Player not found!");
                return;
            }

            Item item = _config.BarrelItem.ToItem();
            basePlayer.GiveItem(item);

            if (player == null) Puts("Success! Item added to inventory!");
            else player.ConsoleMessage("Success! Item added to inventory!");
        }
        void ReadData()
        {
            try
            {
                RenderSounds = Oxide.Core.Interface.GetMod().DataFileSystem.ReadObject<Dictionary<String, List<Byte[]>>>("IQSystem/IQAlcoholFarm/Sounds"); 
                CustomPosNPC = Oxide.Core.Interface.GetMod().DataFileSystem.ReadObject<Dictionary<String, CustomPos>>("IQSystem/IQAlcoholFarm/CustomPosNPC"); 
                
            }
            catch { PrintWarning("Error reading the data file"); }
        }

        private void InitBarrel(BaseNetworkable entity)
        {
            if (entity is WaterPurifier && entity is PoweredWaterPurifier == false)
            {
                ((WaterPurifier)entity).maxStackSize = _config.fermentationSettings.PurifierCapacity;
                return;
            }

            StorageContainer stash = entity.GetComponentInChildren<StorageContainer>();
            if (stash == null || stash.ShortPrefabName != "small_stash_deployed")
                return;

            BarrelController component = entity.GetOrAddComponent<BarrelController>();
		   		 		  						  	   		  		  		  		  		  	   		  	   
            RegisterBarrel(component);
        }
        private static MonumentInfo Airfield;

        private void DrawUI_DrunkBlock(BasePlayer player, String color, Single timeLeft)
        {
            if (_initiated == false)
                return;

            CuiHelper.DestroyUi(player, UI_Layer);
            CuiHelper.AddUi(player, DrunkMain.Replace("{C}", GetColor(color)));

            DrawUI_DrunkTimer(player, color, timeLeft);
        }
        private void DrawUI_ItemPanel(BasePlayer player, CategoryType categoryType = CategoryType.Equipment, Int32 Page = 0)
        {
            CuiHelper.DestroyUi(player, InterfaceBuilder.UI_PANEL_ITEMS);
            CuiHelper.DestroyUi(player, "NextButton");
            CuiHelper.DestroyUi(player, "BackButton");
            CuiHelper.DestroyUi(player, "TitlePage");

            String Interface = InterfaceBuilder.GetInterface("UI_TRADER_NPC_ITEMPANEL_TEMPLATE");
            if (Interface == null) return;
            List<PluginConfig.ShopStore.CategorySettings.ItemsPair> CategoryItems = categoryType == CategoryType.Equipment ? _config.storeNpc.EquipmentCategory.ItemsList : _config.storeNpc.ExchangerCategory.ItemsList;

            Interface = Interface.Replace("%COMMAND_NEXT%", CategoryItems.Count >= (Page + 1) * 5 ? $"func.alcohol category page {categoryType} next {Page}" : String.Empty);
            Interface = Interface.Replace("%COLOR_NEXT%", CategoryItems.Count >= (Page + 1) * 5 ? "1 1 1 1" : "1 1 1 0.5");
            Interface = Interface.Replace("%COMMAND_BACK%", Page != 0 ? $"func.alcohol category page {categoryType} back {Page}" : String.Empty);
            Interface = Interface.Replace("%COLOR_BACK%", Page != 0 ? "1 1 1 1" : "1 1 1 0.5");
            Interface = Interface.Replace("%COUNT_PAGE%", $"{Page + 1} из {(CategoryItems.Count / 5) + 1}");
            Interface = Interface.Replace("%BACK_BUTTON%", GetLang("UI_BACK_BUTTON", player.UserIDString));
            Interface = Interface.Replace("%NEXT_BUTTON%", GetLang("UI_NEXT_BUTTON", player.UserIDString));

            CuiHelper.AddUi(player, Interface);

            DrawUI_Items(player, categoryType, Page);
        }

        private void StartPluginLoad() 
        {
            if (ImageLibrary != null)
            {
                //Load your images here
                ImageUi.Initialize();
                ImageUi.DownloadImages();
            }
            else
            {
                PrintError($"ImageLibrary not found! Please, check your plugins list.");
            }
        }

        private static MonumentInfo Compound;

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (_initiated == false)
                return;

            BaseEntity entity = go.ToBaseEntity();
            if (entity == null)
                return;

            if (entity.skinID == _config.BarrelItem.SkinId)
            {
                SetupBarrel(entity);
                return;
            }
        }


        
        public Dictionary<String, List<Byte[]>> RenderSounds = new Dictionary<string, List<byte[]>>()
        {
            ["welcome"] = new List<byte[]> { },
            ["bye"] = new List<byte[]> { },
        };

        public Dictionary<String, CustomPos> CustomPosNPC = new Dictionary<String, CustomPos>();

        private static InterfaceBuilder _interface;

        private void Unload()
        {
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQSystem/IQAlcoholFarm/CustomPosNPC", CustomPosNPC, true);
            PluginData.SaveData();
            DestroyControllers();

            for (Int32 i = _data.DrunkPlayers.Count - 1; i >= 0; i--)
            {
                DrunkPlayer drunkPlayer = _data.DrunkPlayers[i];
                if(drunkPlayer != null && drunkPlayer.Player != null)
                    DestroyAllUi(drunkPlayer.Player);
            }

            _data.DrunkPlayers.Clear();

            foreach (BaseEntity ent in MonumentEntities.Where(x => !x.IsDestroyed))
                ent.Kill();

            foreach(CustomNpcController npcController in NPCList)
                UnityEngine.Object.DestroyImmediate(npcController);

            InterfaceBuilder.DestroyAll();

            ImageUi.Unload();
		   		 		  						  	   		  		  		  		  		  	   		  	   
            MonumentEntities.Clear();
            BanditTown = null;
            Compound = null;
            Airfield = null;
            _plugin = null;
        }

        private struct ModifierDefintionConfig
        {
            public String Name;
            public Single Value;
            public Single MinTime;

            public ModifierDefintion GetDef()
            {
                Modifier.ModifierType type = 0;
                ModifierDefintion def = null;
                if (Modifier.ModifierType.TryParse(Name, out type))
                {
                    def = new ModifierDefintion
                    {
                        type = type,
                        source = Modifier.ModifierSource.Tea,
                        value = Value
                    };
                }

                return def;
            }
        }

        private void ConvertWort(WaterPurifier waterPurifier, Single timeCooked)
        {
            if (_initiated == false)
                return;

            Item storageSlot = waterPurifier.waterStorage.inventory.GetSlot(0);
            if (storageSlot != null && storageSlot.amount >= waterPurifier.waterStorage.maxStackSize)
                return;

            Single single = timeCooked * (_config.fermentationSettings.PurifierProcessTick / 60f);
            waterPurifier.dirtyWaterProcssed += single;
            if (waterPurifier.dirtyWaterProcssed >= 1f)
            {
                Item item = waterPurifier.inventory.GetSlot(0);
                Int32 num = Mathf.Min(Mathf.FloorToInt(waterPurifier.dirtyWaterProcssed), item.amount);
                single = num;
                item.amount -= num;
                if (item.amount <= 0)
                {
                    item.RemoveFromContainer();
                    item.Remove();
                }
                item.MarkDirty();
                waterPurifier.dirtyWaterProcssed -= num;
                waterPurifier.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            }

            waterPurifier.pendingFreshWater = waterPurifier.pendingFreshWater + single / _config.fermentationSettings.PurifierProcessRatio;
            if (waterPurifier.pendingFreshWater >= 1f)
            {
                Int32 num1 = Mathf.FloorToInt(waterPurifier.pendingFreshWater);
                waterPurifier.pendingFreshWater -= num1;
                storageSlot = waterPurifier.waterStorage.inventory.GetSlot(0);
                if (storageSlot != null && storageSlot.skin != _config.itemSettings.AlcoholItemSkinId)
                {
                    storageSlot.RemoveFromContainer();
                    storageSlot.Remove(0f);
                }
                if (storageSlot != null)
                {
                    storageSlot.amount += num1;
                    storageSlot.amount = Mathf.Clamp(storageSlot.amount, 0, waterPurifier.waterStorage.maxStackSize);
                    waterPurifier.waterStorage.inventory.MarkDirty();
                }
                else
                {
                    Item item1 = ItemManager.CreateByName("water", num1, _config.itemSettings.AlcoholItemSkinId);
                    if (!item1.MoveToContainer(waterPurifier.waterStorage.inventory, -1, true, false))
                    {
                        item1.Remove(0f);
                    }
                }
                waterPurifier.waterStorage.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            }
        }

        private void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            if (_initiated == false)
                return;

            if (item.skin != _config.itemSettings.AlcoholItemSkinId)
                return;
		   		 		  						  	   		  		  		  		  		  	   		  	   
            if (container == null)
                return;

            if (container.parent == null)
                return;

            if (container.parent.skin != _config.itemSettings.AlcoholBottleSkinId)
                return;
		   		 		  						  	   		  		  		  		  		  	   		  	   
            container.parent.skin = 0;
        }
        
        private void SpawnPreset(MonumentInfo monument, List<Point> PointList)
        {
            ClearEnt(monument, monument.transform.TransformPoint(PointList[0].Pos));

            timer.Once(5f, () =>
            {
                foreach (Point point in PointList)
                {
                    Vector3 Position = monument.transform.TransformPoint(point.Pos);
                    Quaternion Rotation = GetMonumentRotation(point.Y, monument, point.X);

                    switch (point.Name)
                    {
                        case "NPC.POINT":
                            {
                                InitializeNPC(Position, Rotation);
                                break;
                            }
                        case "TABLES.POINT":
                            {
                                SpawnTables(Position, Rotation, point.LinkImage);
                                break;
                            }
                    }
                }

                _plugin.PrintWarning(LanguageEn ? $"NPC in {monument.displayPhrase.english.Replace("\n", "")} successfully initialized!" : $"NPC в {monument.displayPhrase.english.Replace("\n", "")} успешно инициализирован!");
		   		 		  						  	   		  		  		  		  		  	   		  	   
            });
        }

        public void InitializeNPC(Vector3 pos, Quaternion rot)
        {
            InvisibleVendingMachine vending = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/npcvendingmachines/shopkeeper_vm_invis.prefab", pos, rot) as InvisibleVendingMachine;
            vending.Spawn();
            vending.OwnerID = 199949458;
            vending.skinID = 199949458;
            vending.shopName = _config.storeNpc.ShopName;
            vending.CancelInvoke(vending.InstallFromVendingOrders);
            vending.ClearSellOrders();
            vending.SetFlag(BaseEntity.Flags.Busy, true);
            vending.SetFlag(BaseEntity.Flags.Locked, true);

            BaseEntity box = GameManager.server.CreateEntity("assets/prefabs/deployable/quarry/fuelstorage.prefab", vending.transform.position + Vector3.up, vending.transform.rotation);
            box.enableSaving = false;
            box.OwnerID = 23129547;
            box.skinID = 23129547;
            box.Spawn();

            BasePlayer npc = GameManager.server.CreateEntity("assets/prefabs/player/player.prefab", pos) as BasePlayer;
            if (npc == null)
            {
                Interface.Oxide.LogError($"Initializing NPC failed! NPC Component == null #3");
                return;
            }
            npc.SetFlag(BaseEntity.Flags.Busy, true);
            
            npc.userID = _config.storeNpc.NPCShopSetting.userID;
            npc.OwnerID = 199949458;
            npc.name = _config.storeNpc.NPCShopSetting.Name;
            npc.displayName = npc.name;
            npc.Spawn();
            npc.SendNetworkUpdate();
            npc.eyes.rotation = Quaternion.LookRotation(vending.transform.rotation * Vector3.forward, Vector3.up);
            npc.OverrideViewAngles(vending.transform.eulerAngles);
            npc.ServerRotation = npc.eyes.rotation;
            npc.SendNetworkUpdate();

            MonumentEntities.Add(vending);
            MonumentEntities.Add(box);

            if (_config.storeNpc.NPCShopSetting.Wear.Count > 0)
            {
                npc.inventory.Strip();
                foreach (Item item in _config.storeNpc.NPCShopSetting.Wear.Select(t => ItemManager.CreateByName(t.ShortName, 1, t.SkinId)))
                    item.MoveToContainer(npc.inventory.containerWear);
            }
            CustomNpcController npcController = npc.gameObject.AddComponent<CustomNpcController>();
            NPCList.Add(npcController);
        }

        private void DrawUI_Items_AmountController(BasePlayer player, Int32 Y, PluginConfig.ShopStore.CategorySettings.ItemsPair Category, Int32 Page = 0, Int32 CustomAmount = 1, CategoryType categoryType = CategoryType.Equipment)
        {
            CuiHelper.DestroyUi(player, $"ItemAmount_In_{Y}");
            CuiHelper.DestroyUi(player, $"ItemAmount_To_{Y}");
            CuiHelper.DestroyUi(player, $"ButtonBuy_{Y}");
            CuiHelper.DestroyUi(player, $"ButtonPlusAmount_{Y}");
            CuiHelper.DestroyUi(player, $"ButtonMinusAmount_{Y}");
            CuiHelper.DestroyUi(player, $"InputAmount_{Y}");

            String Interface = InterfaceBuilder.GetInterface("UI_TRADER_NPC_ITEMPANEL_CONTROLLER_AMOUNT");
            if (Interface == null) return;

            Int32 AmountIn = Category.BuyItems.Amount * CustomAmount;
            Int32 AmountTo = Category.PriceItems.Amount * CustomAmount;
            Boolean IsBuy = HaveItem(player, Category.PriceItems.Shortname, AmountTo, Category.PriceItems.SkinID);
            Interface = Interface.Replace("%Y%", $"{Y}");
            Interface = Interface.Replace("%AMOUNT_IN%", $"{AmountIn}");
            Interface = Interface.Replace("%AMOUNT_TO%", $"{AmountTo}");
            Interface = Interface.Replace("%BUY_BUTTON_COLOR%", IsBuy ? "0.55 0.78 0.24 1" : "0.8 0.28 0.2 1");
            Interface = Interface.Replace("%BUY_BUTTON_COMMAND%", IsBuy ? $"func.alcohol category selling {categoryType} amount.buy {Y} {Page} {CustomAmount}" : String.Empty);
            Interface = Interface.Replace("%COLOR_BTN%", IsBuy ? "0.55 0.78 0.24 1" : "0.8 0.28 0.2 1");
            Interface = Interface.Replace("%BUY_TITLE%", IsBuy ? GetLang("YES_RESOURCES_USER", player.UserIDString) : GetLang("NO_RESOURCES_USER", player.UserIDString));
            Interface = Interface.Replace("%CMD_PLUS%", $"func.alcohol category selling {categoryType} amount.next {Y} {Page} {CustomAmount}");
            Interface = Interface.Replace("%CMD_MINUS%", $"func.alcohol category selling {categoryType} amount.back {Y} {Page} {CustomAmount}");

            CuiHelper.AddUi(player, Interface);
        }

        private Int32 GetIndexFromGradient(Int32 length, Int32 amount, Int32 capacity)
        {
            if (capacity < amount)
                amount = capacity;

            Int32 index = length * amount / (capacity <= 0 ? 1 : capacity);
            if (index == 0) index++;
            return index - 1;
        }

        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (_initiated == false)
                return;
		   		 		  						  	   		  		  		  		  		  	   		  	   
            if (item.skin != _config.itemSettings.AlcoholItemSkinId)
                return;

            if (container == null)
                return;

            if (container.parent == null)
                return;

            if (container.parent.info.shortname != "smallwaterbottle")
                return;

            container.parent.skin = _config.itemSettings.AlcoholBottleSkinId;
        }
        private const String AlcoholWortProcessEffect = "assets/bundled/prefabs/fx/animals/flies/flies_medium.prefab";

        
        
        private class ImageUi
        {
            private static Coroutine coroutineImg = null;
            private static Coroutine coroutineCustomNPC = null;
            private static Dictionary<String, String> Images = new Dictionary<String, String>();

            internal struct ImageStruct
            {
                public ImageStruct(String URL, UInt64 SkinID = 0)
                {
                    this.URL = URL;
                    this.SkinID = SkinID;
                }

                public String URL;
                public UInt64 SkinID;
            }

            private static List<ImageStruct> KeyImages = new List<ImageStruct>
            {

            };

            public static void DownloadImages() { coroutineImg = ServerMgr.Instance.StartCoroutine(AddImage()); }

            private static IEnumerator AddImage()
            {
                if (_plugin == null)
                    yield break;
                _plugin.PrintWarning(LanguageEn ? "We generate the interface, wait ~10-15 seconds!" : "Генерируем интерфейс, ожидайте ~10-15 секунд!");
                foreach (ImageStruct Icons in KeyImages)
                {
                    String URL = String.Empty;
                    String KeyName = String.Empty;
                    if (Icons.URL.Contains("UI_"))
                        URL = $"http://iqsystem.skyplugins.ru/iqalcohol/getimageui/{Icons.URL}/W5fgSD3dfVBxc";

                    KeyName = $"{Icons.URL}_{Icons.SkinID}";

                    UnityWebRequest www = UnityWebRequestTexture.GetTexture(URL);
                    yield return www.SendWebRequest();

                    if (www.isNetworkError || www.isHttpError)
                    {
                        _plugin.PrintWarning(String.Format("Image download error! Error: {0}, Image name: {1}", www.error, KeyName));
                        www.Dispose();
                        coroutineImg = null;
                        yield break;
                    }

                    Texture2D texture = DownloadHandlerTexture.GetContent(www);
                    if (texture != null)
                    {
                        Byte[] bytes = texture.EncodeToPNG();

                        String image = FileStorage.server.Store(bytes, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
                        if (!Images.ContainsKey(KeyName))
                            Images.Add(KeyName, image);
                        else
                            Images[KeyName] = image;

                        UnityEngine.Object.DestroyImmediate(texture);
                    }

                    www.Dispose();
                    yield return CoroutineEx.waitForSeconds(0.02f);
                }

                coroutineImg = null;
		   		 		  						  	   		  		  		  		  		  	   		  	   
                _interface = new InterfaceBuilder();
                _plugin.BuildInterface();
                _plugin.PrintWarning(LanguageEn ? "Interface loaded successfully!" : "Интерфейс успешно загружен!");
                _plugin.GetSounds();
                _plugin.CheckStatus();
		   		 		  						  	   		  		  		  		  		  	   		  	   
                if (!_plugin.IsInitializeNPCShop())
                {
                    _plugin.Unsubscribe(nameof(OnEntityTakeDamage));
                    _plugin.Unsubscribe(nameof(CanBradleyApcTarget));
                    _plugin.Unsubscribe(nameof(OnNpcTarget));
                    
                    _plugin.FullLoadedNPC = true;
                }
                else
                {
                    if (_plugin.IsCompound())
                        _plugin.SpawnPreset(Compound, _plugin.PointsList["COMPOUND"]);
                    if (_plugin.IsBanitTown())
                        _plugin.SpawnPreset(BanditTown, _plugin.PointsList["BANDIT_TOWN"]);
                    if (_plugin.IsAirfield())
                        _plugin.SpawnPreset(Airfield, _plugin.PointsList["AIRFIELD"]);
		   		 		  						  	   		  		  		  		  		  	   		  	   
                    if (_plugin.CustomPosNPC.Count != 0)
                    {
                       coroutineCustomNPC = ServerMgr.Instance.StartCoroutine(CustomNPCLoaded());
                    }
                    else _plugin.FullLoadedNPC = true;
                }
            }

            private static IEnumerator CustomNPCLoaded()
            {
                foreach (KeyValuePair<String, CustomPos> Customs in _plugin.CustomPosNPC)
                {
                    _plugin.InitializeNPC(Customs.Value.LocalPosition, Customs.Value.GetRotation());
                    yield return CoroutineEx.waitForSeconds(1f);
                }

                _plugin.PrintWarning(LanguageEn ? $"Custom NPC {_plugin.CustomPosNPC.Count} successfully initialized!" : $"Custom NPC {_plugin.CustomPosNPC.Count} успешно инициализирован!");
                _plugin.FullLoadedNPC = true;
                coroutineCustomNPC = null;
            }
            public static String GetImage(String ImgKey)
            {
                if (Images.ContainsKey(ImgKey))
                    return Images[ImgKey];
                return _plugin.GetImage("LOADING");
            }
            public static void Initialize()
            {
                KeyImages = new List<ImageStruct>();
                Images = new Dictionary<string, string>();

                ImageStruct BackItem = new ImageStruct("UI_IQALCOHOL_BACK_ITEM");
                if (!KeyImages.Contains(BackItem))
                    KeyImages.Add(BackItem);

                ImageStruct Background = new ImageStruct("UI_IQALCOHOL_BACKGORUND");
                if (!KeyImages.Contains(Background))
                    KeyImages.Add(Background);
		   		 		  						  	   		  		  		  		  		  	   		  	   
                ImageStruct BackItemLine = new ImageStruct("UI_IQALCOHOL_LINE");
                if (!KeyImages.Contains(BackItemLine))
                    KeyImages.Add(BackItemLine);

                ImageStruct DrunkIcon = new ImageStruct("UI_ALCOHOL_TIME");
                if (!KeyImages.Contains(DrunkIcon))
                    KeyImages.Add(DrunkIcon);       

                // foreach (PluginConfig.ShopStore.CategorySettings.ItemsPair Image in _config.storeNpc.EquipmentCategory.ItemsList)
                // {
                //     if(Image.BuyItems.SkinID == 99489498499) return;
                //     ImageStruct BuyItem = new ImageStruct(Image.BuyItems.Shortname, Image.BuyItems.SkinID);
                //     if (!KeyImages.Contains(BuyItem))
                //         KeyImages.Add(BuyItem);
                //
                //     ImageStruct PriceItem = new ImageStruct(Image.PriceItems.Shortname, Image.PriceItems.SkinID);
                //     if (!KeyImages.Contains(PriceItem))
                //         KeyImages.Add(PriceItem);
                // }
                //
                // foreach (PluginConfig.ShopStore.CategorySettings.ItemsPair Image in _config.storeNpc.ExchangerCategory.ItemsList)
                // {
                //     ImageStruct BuyItem = new ImageStruct(Image.BuyItems.Shortname, Image.BuyItems.SkinID);
                //     if (!KeyImages.Contains(BuyItem))
                //         KeyImages.Add(BuyItem);
                //
                //     ImageStruct PriceItem = new ImageStruct(Image.PriceItems.Shortname, Image.PriceItems.SkinID);
                //     if (!KeyImages.Contains(PriceItem))
                //         KeyImages.Add(PriceItem);
                // }
            }
            public static void Unload()
            {
                coroutineImg = null;
                foreach (KeyValuePair<String, String> item in Images)
                    FileStorage.server.RemoveExact(UInt32.Parse(item.Value), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID, 0U);

                KeyImages.Clear();
                KeyImages = null;
                Images.Clear();
                Images = null;
            }
        }

        private IEnumerator PlayersUpdate(Boolean afterReload = false)
        {
            while (_data.DrunkPlayers.Count > 0)
            {
                try
                {
                    for (Int32 i = _data.DrunkPlayers.Count - 1; i >= 0; i--)
                    {
                        DrunkPlayer drunkPlayer = _data.DrunkPlayers[i];
                        if (--drunkPlayer.DrunkRemaining <= 0)
                        {
                            _data.DrunkPlayers.Remove(drunkPlayer);
                            DestroyAllUi(drunkPlayer.Player);
                            continue;
                        }

                        if (afterReload)
                        {
                            DrawUI_DrunkBlock(drunkPlayer.Player, drunkPlayer.CurrectColor, drunkPlayer.DrunkRemaining);
                            DrawUI_DrunkAFade(drunkPlayer.Player, drunkPlayer.CurrectFade);
                        }

                        Int32 index = GetIndexFromGradient(AlcoholColorGradient.Length, (Int32)drunkPlayer.DrunkRemaining, (Int32)_config.drinkedSettings.AlcoholDrinkLimit);
                        String color = AlcoholColorGradient[index];

                        if (color != drunkPlayer.CurrectColor)
                        {
                            drunkPlayer.CurrectColor = color;
                            DrawUI_DrunkBlock(drunkPlayer.Player, drunkPlayer.CurrectColor, drunkPlayer.DrunkRemaining);
                        }

                        color = AlcoholBlur[index];

                        if (color != drunkPlayer.CurrectFade)
                        {
                            drunkPlayer.CurrectFade = color;
                            DrawUI_DrunkAFade(drunkPlayer.Player, drunkPlayer.CurrectFade);
                        }

                        if (drunkPlayer.DrunkRemaining >= _config.drinkedSettings.AlcoholDrinkLimit)
                        {
                            if (Core.Random.Range(0, 100) <= _config.drinkedSettings.AlcoholDrunkNegativeChance)
                                ExecuteNegativeEffect(drunkPlayer.Player);
                        }
                        else
                        {
                            drunkPlayer.Player.Heal(_config.drinkedSettings.AlcoholDrunkHeal);
                        }

                        if (drunkPlayer.ModifiersUpdate > drunkPlayer.DrunkRemaining)
                            UpdateModifiers(drunkPlayer);

                        drunkPlayer.Player.metabolism.temperature.SetValue(40f);
                        DrawUI_DrunkTimer(drunkPlayer.Player, drunkPlayer.CurrectColor, drunkPlayer.DrunkRemaining);

                        if (Core.Random.Range(0, 100) <= _config.RareEffect)
                            EffectNetwork.Send(new Effect(AlcoholShakeEffect.GetRandom(), drunkPlayer.Player.transform.position, Vector3.zero), drunkPlayer.Player.net.connection);
                    }
                }
                catch (Exception)
                {
                    //PrintError(e.ToString());
                }

                afterReload = false;
                yield return new WaitForSeconds(1f);
            }
            
            yield break;
        }
        private static MonumentInfo BanditTown;
        
        
        [ChatCommand("set.custom.npc")]
        void SetCustomPosIvan(BasePlayer player, String cmd, String[] args)
        {
            if (!player.IsAdmin) return;
            if (!_plugin.FullLoadedNPC)
            {
                SendReply(player, LanguageEn ? "Wait for the plugin to be fully initialized" : "Дождитесь полной инициализации плагина");
                return;
            }
            if (args == null || args.Length < 1)
            {
                SendReply(player, LanguageEn ? "You didn't specify a name for the position" : "Вы не указали название для позиции");
                return;
            }
            String NamePos = args[0];
            if (String.IsNullOrWhiteSpace(NamePos))
            {
                SendReply(player, LanguageEn ? "You didn't specify a name for the position" : "Вы не указали название для позиции");
                return;
            }

            if (CustomPosNPC.ContainsKey(NamePos))
            {
                SendReply(player, LanguageEn ? "A position with this name already exists" : "Позиция с таким названием уже существует");
                return;
            }

            Transform transform = player.transform;

            Quaternion rotation = Quaternion.Euler(player.serverInput.current.aimAngles);
            CustomPosNPC.Add(NamePos, new CustomPos()
            {
                LocalPosition = transform.position,
                LocalRotation = new CustomPos.QuaternionPos()
                {
                    X = rotation.x,
                    Y = rotation.y,
                    Z = rotation.z,
                    W = rotation.w,
                }
            });
            
            InitializeNPC(CustomPosNPC[NamePos].LocalPosition, CustomPosNPC[NamePos].GetRotation());
            SendReply(player, LanguageEn ? "The position has been successfully added!" : "Позиция успешно сохранена!");
        }
        
                private class PluginConfig
        {
            [JsonProperty(LanguageEn ? "Item Customization" : "Настройка предметов")]
            public ItemSettings itemSettings = new ItemSettings();
            [JsonProperty(LanguageEn ? "Adjustment of sips and effects" : "Настройка глотков и эффектов")]
            public DrinkedSettings drinkedSettings = new DrinkedSettings();
            [JsonProperty(LanguageEn ? "Alcohol fermentation setting" : "Настройка ферментации алкоголя")]
            public FermentationSettings fermentationSettings = new FermentationSettings();
            [JsonProperty(LanguageEn ? "Setting up the sale and purchase of alcohol (NPC)" : "Настройка продажи и скупки алкоголя (NPC)")]
            public ShopStore storeNpc = new ShopStore();
            [JsonProperty(LanguageEn ? "Use the voice acting of the NPC merchant" : "Использовать озвучку NPC торговца")]
            public Boolean UseNPCSounds = true;
            internal class ItemSettings
            {
                [JsonProperty(LanguageEn ? "SkinID must" : "SkinID сусло")]
                public UInt64 WortItemSkinId = 2476100688;
                [JsonProperty(LanguageEn ? "SkinID alcohol" : "SkinID алкоголя")]
                public UInt64 AlcoholItemSkinId = 2469834291;
                [JsonProperty(LanguageEn ? "SkinID bottles of alcohol" : "SkinID бутылки с алкоголем")]
                public UInt64 AlcoholBottleSkinId = 2749338705;
            }
            internal class DrinkedSettings
            {
                [JsonProperty(LanguageEn ? "The cost of one sip of alcohol" : "Стоимость одного глотка алкоголя")]
                public Int32 AlcoholDrinkAmount = 50;
                [JsonProperty(LanguageEn ? "How much time of intoxication to add after a sip" : "Сколько времени опьянения добавлять после глотка")]
                public Single AlcoholDrinkTime = 90;
                [JsonProperty(LanguageEn ? "How long does it take for negative effects to work" : "После какого времени работают негативные эффекты")]
                public Single AlcoholDrinkLimit = 600;
                [JsonProperty(LanguageEn ? "How many calories does one sip burn" : "Сколько калорий снимает один глоток")]
                public Int32 AlcoholDrinkCalories = -40;
                [JsonProperty(LanguageEn ? "How much water does one sip take" : "Сколько воды снимает один глоток")]
                public Int32 AlcoholDrinkHydration = -10;
                [JsonProperty(LanguageEn ? "How much health is added every second" : "Сколько здоровья прибавляет каждую секунду")]
                public Single AlcoholDrunkHeal = 0.25f;
                [JsonProperty(LanguageEn ? "Negative Effect Chance" : "Шанс негативного эффекта")]
                public Int32 AlcoholDrunkNegativeChance = 1;
                [JsonProperty(LanguageEn ? "Negative effect. How much to take away health" : "Негативный эффект. Сколько отнимать здоровья")]
                public Single AlcoholDrunkNegativeDamage = 12f;
                [JsonProperty(LanguageEn ? "Negative effect. How much water to take" : "Негативный эффект. Сколько отнимать воды")]
                public Single AlcoholDrunkNegativeCalories = 20f;
                [JsonProperty(LanguageEn ? "Negative effect. How much food to take" : "Негативный эффект. Сколько отнимать еды")]
                public Single AlcoholDrunkNegativeHydration = 60f;
            }
            internal class FermentationSettings
            {
                [JsonProperty(LanguageEn ? "Berry processing frequency in seconds" : "Частота переработки ягод в секундах")]
                public Single BarrelProcessTick = 12f;
                [JsonProperty(LanguageEn ? "How many berries are consumed per processing tick" : "Сколько ягод уходит за один тик переработки")]
                public Int32 BarrelInputTick = 1;
                [JsonProperty(LanguageEn ? "How much wort gives out per processing tick" : "Сколько сусло выдает за один тик переработки")]
                public Int32 BarrelOutputTick = 95;
                [JsonProperty(LanguageEn ? "How much wort fits in a barrel" : "Сколько сусло влезает в бочку")]
                public Int32 BarrelCapacity = 20000;

                [JsonProperty(LanguageEn ? "Wort processor inventory size" : "Размер инвентаря переработчика сусло")]
                public Int32 PurifierCapacity = 2000;
                [JsonProperty(LanguageEn ? "How much water to handle per minute" : "Cколько воды обрабатывать за минуту")]
                public Single PurifierProcessTick = 192f;
                [JsonProperty(LanguageEn ? "Processing rate" : "Рейт переработки")]
                public Single PurifierProcessRatio = 3.5f;
            }
            internal class ShopStore
            {
                [JsonProperty(LanguageEn ? "Store names on the map" : "Названия магазина на карте")]
                public String ShopName = "Ivan Store - Sale and purchase of alcohol";
                [JsonProperty(LanguageEn ? "Setting up NPC Merchants" : "Настройка NPC торговцев")]
                public NPCShop NPCShopSetting;
                [JsonProperty(LanguageEn ? "Setting the location of NPC merchants" : "Настройка расположения NPC торговцев")]
                public TurnedShopSpawn TurnedShop = new TurnedShopSpawn();
                internal class TurnedShopSpawn
                {
                    [JsonProperty(LanguageEn ? "Use an NPC merchant in a compound" : "Использовать NPC-торговца в мирном городе")]
                    public Boolean UseCompound = true;
                    [JsonProperty(LanguageEn ? "Use an NPC merchant in a bandit camp" : "Использовать NPC-торговца в городе бандитов")]
                    public Boolean UseBanditTown = true;
                    [JsonProperty(LanguageEn ? "Use an NPC merchant at the airfield" : "Использовать NPC-торговца на аэропорту")]
                    public Boolean UseAirfield = true;
                }

                [JsonProperty(LanguageEn ? "Equipment category setting" : "Настройка категории оборудование", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public CategorySettings EquipmentCategory = new CategorySettings();
                [JsonProperty(LanguageEn ? "Exchanger category setting" : "Настройка категории обмена", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public CategorySettings ExchangerCategory = new CategorySettings();
                internal class CategorySettings
                {
                    [JsonProperty(LanguageEn ? "List of items in the category (Product Item - Price Item)" : "Список предметов в категории (Товар - цена)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                    public List<ItemsPair> ItemsList = new List<ItemsPair>();
                    internal struct ItemsPair
                    {
                        public ItemsPair(String buyItemShortname, Int32 buyItemAmount, UInt64 buyItemID, String buyDisplayName, String priceItemShortname, Int32 priceItemAmount, UInt64 priceItemID, String priceDisplayName)
                        {
                            BuyItems = new ItemCustom
                            {
                                Shortname = buyItemShortname,
                                Amount = buyItemAmount,
                                SkinID = buyItemID,
                                DisplayName = buyDisplayName,
                            };
                            PriceItems = new ItemCustom
                            {
                                Shortname = priceItemShortname,
                                Amount = priceItemAmount,
                                SkinID = priceItemID,
                                DisplayName = priceDisplayName,
                            };
                        }

                        internal class ItemCustom
                        {
                            public String Shortname;
                            public Int32 Amount;
                            public UInt64 SkinID;
                            public String DisplayName;

                            public Item ToItem(Int32 AmountPlus = 1)
                            {
                                Item item = ItemManager.CreateByName(Shortname, Amount * AmountPlus, SkinID);
                                if (item != null && String.IsNullOrEmpty(DisplayName) == false)
                                    item.name = DisplayName;

                                return item;
                            }
                        }

                        public ItemCustom BuyItems;
                        public ItemCustom PriceItems;
                    }
                }
                internal class NPCShop
                {
                    [JsonProperty(LanguageEn ? "DisplayName NPC" : "Имя NPC")]
                    public String Name = "Ivan";
                    [JsonProperty(LanguageEn ? "ID NPC (His appearance depends on his ID)" : "ID NPC(От его ид зависит его внешность)")]
                    public UInt64 userID = 19384;
                    [JsonProperty(LanguageEn ? "Clothes NPC" : "Одежда NPC")]
                    public List<ItemsNpc> Wear = new List<ItemsNpc>();

                    public class ItemsNpc
                    {
                        [JsonProperty(LanguageEn ? "ShortName" : "ShortName")]
                        public String ShortName;
                        [JsonProperty(LanguageEn ? "SkinId" : "SkinId")]
                        public UInt64 SkinId;
                    }
                }
            }
            [JsonProperty(LanguageEn ? "Chance to play effects while drunk (screen shaking, sounds)" : "Шанс проигрывания эффектов во время опьянения (тряска экрана, звуки)")]
            public Int32 RareEffect;

            [JsonProperty(LanguageEn ? "Barrel Item Customization" : "Настройка предмета бочки", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public CustomItem BarrelItem = new CustomItem(LanguageEn ? "Fermentation barrelа" : "Бродильная бочка", "water.barrel", 2477175239);

            [JsonProperty(LanguageEn ? "Setting up modifiers" : "Настройка модификаторов", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ModifierDefintionConfig> ModifierDefintions = new List<ModifierDefintionConfig>()
            {
                new ModifierDefintionConfig()
                {
                    Name = Modifier.ModifierType.Wood_Yield.ToString(),
                    Value = 1f,
                    MinTime = 10,
                },
                new ModifierDefintionConfig()
                {
                    Name = Modifier.ModifierType.Ore_Yield.ToString(),
                    Value = 1f,
                    MinTime = 10,
                },
                new ModifierDefintionConfig()
                {
                    Name = Modifier.ModifierType.Radiation_Resistance.ToString(),
                    Value = 1f,
                    MinTime = 10,
                },
                new ModifierDefintionConfig()
                {
                    Name = Modifier.ModifierType.Radiation_Exposure_Resistance.ToString(),
                    Value = 1f,
                    MinTime = 10,
                },
                new ModifierDefintionConfig()
                {
                    Name = Modifier.ModifierType.Max_Health.ToString(),
                    Value = 1f,
                    MinTime = 10,
                },
                new ModifierDefintionConfig()
                {
                    Name = Modifier.ModifierType.Scrap_Yield.ToString(),
                    Value = 1f,
                    MinTime = 10,
                },
            };
        }
            }
}
