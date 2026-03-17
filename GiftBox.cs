using System;
using System.Collections.Generic;
using AOT;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("GiftBox", "TopPlugin.ru", "0.0.3⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠")]
    public class GiftBox : RustPlugin
    {
        #region Config
        public class ListDrop
        {
            [JsonProperty("Shortname предмета")]
            public string ShortName;
            [JsonProperty("Мин кол-во")] 
            public int AmountMin;
            [JsonProperty("Макс кол-во")] 
            public int AmountMax;
        }
        
        private ConfigData config;
        class ConfigData
        {
            [JsonProperty("Сколько времени надо будет, что бы открылся ящик( в секундах )")]
            public int TimeForOpen = 120;
            [JsonProperty("Каждые n секунд будет обновлятся время на тексте")]
            public float TimerEvery = 5f;
            [JsonProperty("Скин ид предмета, который надо будет положить в ящик")]
            public ulong skinID = 1823252700;
            [JsonProperty("Название у предмета, который надо будет положить в ящик")]
            public string name = "Кристалл";
            [JsonProperty("Предметы, которые будут выдаваться в ящик")]
            public List<ListDrop> ListDrops { get; set; }
            [JsonProperty("Shortname ящиков где будет спавнится предмет")]
            public List<string> LootShortName { get; set; }
            [JsonProperty("Шанс выпадения предмета")]
            public int Chance = 15;
            public static ConfigData GetNewCong()
            {
                ConfigData newConfig = new ConfigData();

                newConfig.ListDrops = new List<ListDrop>
                {
                    new ListDrop()
                    {
                        ShortName = "sulfur.ore",
                        AmountMin = 1,
                        AmountMax = 5
                    },
                    new ListDrop()
                    {
                        ShortName = "metal.fragments",
                        AmountMin = 1500,
                        AmountMax = 2500
                        
                    },
                };
                newConfig.LootShortName = new List<string>
                {
                    "crate_basic",
                    "crate_elite",
                    "crate_mine",
                    "crate_tools",
                    "crate_normal",
                    "crate_normal_2",
                    "crate_normal_2_food",
                    "crate_normal_2_medical",
                    "crate_underwater_advanced",
                    "crate_underwater_basic",
                    "foodbox",
                    "minecart",
                    "bradley_crate",
                    "heli_crate",
                    "codelockedhackablecrate",
                    "supply_drop",
                    "presentdrop"
                };
                return newConfig;
            }
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config?.ListDrops == null) LoadDefaultConfig();

            }
            catch
            {
                LoadDefaultConfig();
            }
            NextTick(SaveConfig);
        }
        protected override void LoadDefaultConfig() => config = ConfigData.GetNewCong();
        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion
        #region GiveDrop
        static void MoveDrop(uint entity)
        {
            var find = BaseNetworkable.serverEntities.Find(entity);

            if (find != null && find.IsDestroyed == false)
            {
                var finds = ins.BoxGift.Find(p => p.entity == entity);
                if (finds != null)
                {
                    var entitys = find.GetComponent<BaseEntity>();
                    if (!entitys) return;
                    StorageContainer container = entitys.GetComponent<StorageContainer>();
                    foreach (var key in ins.config.ListDrops)
                    {
                        var item = ItemManager.CreateByName(key.ShortName,
                            UnityEngine.Random.Range(key.AmountMin, key.AmountMax));
                        item.MoveToContainer(container.inventory);
                    }
                    
                }
            }
        }
        #endregion
        #region Utils && Data
        
        public List<LootContainer> handledContainers = new List<LootContainer>();
        
        public class DataEntintys
        {
            public uint entity;
            public double time;
            public bool activate;
        }
        public List<DataEntintys> BoxGift = new List<DataEntintys>();
        static DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        static int CurrentTime() => (int) DateTime.UtcNow.Subtract(epoch).TotalSeconds;
        #endregion
        #region Hooks
        private static GiftBox ins;
        object CanMoveItem(Item item, PlayerInventory playerLoot, uint targetContainer, int targetSlot, int amount)
        {
        
            BasePlayer player = playerLoot.GetComponent<BasePlayer>();
            if (player == null) return null;
            ItemContainer container = playerLoot.FindContainer(targetContainer);
            if (container == null) return null;
            BaseEntity entity = container.entityOwner;
            if (entity == null) return null;
            var find = BoxGift.Find(p => p.entity == entity.net.ID);
            if (find != null)
                return null;
            if (entity != null && item.info.shortname == "glue" && item.skin == config.skinID && entity.ShortPrefabName == "woodbox_deployed" && amount == 1)
            {
                if (player.userID == entity.OwnerID)
                {
                    BoxGift.Add(new DataEntintys
                    {
                        entity = entity.net.ID,
                        time = CurrentTime() + config.TimeForOpen,
                        activate = true,
                    });
                    entity.gameObject.AddComponent<BoxEntity>();
                    NextTick(() =>
                    {
                        var check = container.itemList.Find(p => p.info.shortname == "glue");
                        if (check != null && check.skin == config.skinID)
                        {
                            check.Remove();
                            check.RemoveFromContainer();
                            player.EndLooting();
                        }
                        
                    });
                }
            }
            return null;
        }
        void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            var find = BoxGift.Find(p => p.entity == entity.net.ID);
            if (find != null)
            {
                if (find.activate == false)
                {
                    var check = entity.GetComponent<StorageContainer>();
                    if (check.inventory.itemList.Count == 0)
                    {
                        SendReply(player, "Вы успешно забрали свою посылку");
                        GameObject.Destroy(entity.GetComponent<BoxEntity>());
                        BoxGift.Remove(BoxGift.Find(p => p.entity == entity.net.ID));
                        entity.SendNetworkUpdateImmediate();
                    }
                }
            }
        }
        object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (container == null || player.IsNpc || player == null) return null;
            BaseEntity entity = container.GetComponent<BaseEntity>();
            if (entity != null)
            {
                var find = BoxGift.Find(p => p.entity == entity.net.ID);
                if (find != null)
                {
                    if (find.activate == true)
                    {
                        SendReply(player, "Ящик нельзя открывать пока он не доступен!");
                        return false;
                    }
                }
            }
            return null;
        }
        void OnLootEntity(BasePlayer player, BaseEntity entity, Item item)
        {
            if (!(entity is LootContainer)) return;
            var container = (LootContainer)entity;
            if (handledContainers.Contains(container) || container.ShortPrefabName == "stocking_large_deployed" ||
                container.ShortPrefabName == "stocking_small_deployed") return;
            handledContainers.Add(container);
            List<int> ItemsList = new List<int>();
            if (config.LootShortName.Contains(container.ShortPrefabName))
            {
                if (UnityEngine.Random.Range(0f, 100f) < config.Chance)
                {
                    var itemContainer = container.inventory;
                    foreach (var i1 in itemContainer.itemList)
                    {
                        ItemsList.Add(i1.info.itemid);
                    }
                    if (!ItemsList.Contains(ItemManager.FindItemDefinition("glue").itemid))
                    {
                        if (container.inventory.itemList.Count == container.inventory.capacity)
                            container.inventory.capacity++;
                        item = ItemManager.CreateByName("glue", 1, config.skinID);
                        item.name = config.name;
                        item.MarkDirty();
                        item.MoveToContainer(itemContainer);
                    }
                }
            }
        }
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info, Item item)
        {
            if (info == null) return;
            if (entity?.net?.ID == null) return;
            var container = entity as LootContainer;
            var player = info?.InitiatorPlayer;
            if (player == null || container == null) return;
            List<int> ItemsList = new List<int>();
            if (config.LootShortName.Contains(container.ShortPrefabName))
            {
                if (UnityEngine.Random.Range(0f, 100f) < config.Chance)
                {
                    var itemContainer = container.inventory;
                    foreach (var i1 in itemContainer.itemList)
                    {
                        ItemsList.Add(i1.info.itemid);
                    }

                    if (!ItemsList.Contains(ItemManager.FindItemDefinition("glue").itemid))
                    {
                        if (container.inventory.itemList.Count == container.inventory.capacity)
                            container.inventory.capacity++;
                        item = ItemManager.CreateByName("glue", 1, config.skinID);
                        item.name = config.name;
                        item.MarkDirty();
                        item.MoveToContainer(itemContainer);
                    }
                }
            }
        }
        
        #endregion
        #region Init
        void OnServerInitialized()
        {
            try
            {
                BoxGift = Interface.GetMod().DataFileSystem.ReadObject<List<DataEntintys>>(nameof(GiftBox));
                if (BoxGift == null)
                    BoxGift = new List<DataEntintys>();
            }
            catch
            {
                BoxGift = new List<DataEntintys>();
            }
            ins = this;
            if (BoxGift.Count > 0)
            {
                for (int i = 0; i < BoxGift.Count; i++)
                {
                    var ent = BaseNetworkable.serverEntities.Find(BoxGift[i].entity);
                    if (ent != null && !ent.IsDestroyed)
                    {
                        ent.gameObject.AddComponent<BoxEntity>();
                    }
                    else
                    {
                        BoxGift.RemoveAt(i);
                    }
                }
            }
        }
        
        #endregion
        #region SaveData
        void SaveData()
        {
            if (BoxGift != null)
            {
                Interface.Oxide.DataFileSystem.WriteObject("GiftBox", BoxGift);   
            }
        }
        void OnServerSave()
        {
            SaveData();
        }
        void Unload()
        {
            var objects = UnityEngine.Object.FindObjectsOfType<BoxEntity>();
            foreach (var key in objects)
            {
                GameObject.Destroy(key);
            }
            SaveData();
        }
        #endregion
        #region Component
        private static readonly int playerLayer = LayerMask.GetMask("Player (Server)");
        private static readonly Collider[] colBuffer = Vis.colBuffer;
        public class BoxEntity : FacepunchBehaviour
        {
            public List<BasePlayer> InRadius = new List<BasePlayer>();
            private BaseEntity entity;
            private void Awake()
            {
                entity = GetComponent<BaseEntity>();
                if (IsInvoking("EntityLook"))
                {
                    CancelInvoke("EntityLook");
                }
                InvokeRepeating("EntityLook", 0f, ins.config.TimerEvery);
            }

            private void OnDestroy()
            {
                Destroy(this);
            }

            public void CheckTrugger()
            {
                var entities = Physics.OverlapSphereNonAlloc(entity.transform.position, 5, colBuffer, playerLayer, QueryTriggerInteraction.Collide);
                if (entities != 0)
                {
                    for (var i = 0; i < entities; i++)
                    {
                        var player = colBuffer[i].GetComponentInParent<BasePlayer>();
                        if (player != null)
                            InRadius.Add(player);
                    }   
                }
            }
            void EntityLook()
            {
                CheckTrugger();
                foreach (var key in InRadius)
                {
                    if (key != null)
                    {
                        if (Vector3.Distance(entity.CenterPoint(), key.transform.position) < 5f)
                        {

                            if (key.userID == entity.OwnerID)
                            {
                                if (!key.HasPlayerFlag(BasePlayer.PlayerFlags.IsAdmin))
                                {
                                    var check = ins.BoxGift.Find(p => p.entity == entity.net.ID);
                                    if (!string.IsNullOrEmpty(check.ToString()))
                                    {
                                        var left = check.time - CurrentTime();
                                        if (left <= 0 && check.activate == true)
                                        {
                                            MoveDrop(check.entity);
                                            check.activate = false;
                                            check.time = 0;
                                        }

                                        string text = check.time > 0 ? $"<size=13><b>ЯЩИК ПОДАРКОВ</b></size>\n<size=14><b> ОСТАЛОСЬ ДО ОТКРЫТИЯ:{left}с!</b></size>" : "<size=13><b>ЯЩИК ПОДАРКОВ</b></size>\n<size=13><b>ДОСТУПНО</b></size>";
                                        key.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                                        key.SendNetworkUpdateImmediate();
                                        key.SendConsoleCommand("ddraw.text", ins.config.TimerEvery + Time.deltaTime, Color.white,
                                            entity.CenterPoint(), $"{text}");
                                        key.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                                        key.SendNetworkUpdate();
                                    }
                                }
                                else
                                {
                                    var check = ins.BoxGift.Find(p => p.entity == entity.net.ID);
                                    if (!string.IsNullOrEmpty(check.ToString()))
                                    {
                                        var left = check.time - CurrentTime();
                                        if (left <= 0 && check.activate == true)
                                        {
                                            MoveDrop(check.entity);
                                            check.activate = false;
                                            check.time = 0;
                                        }
                                        string text = check.time > 0
                                            ? $"<size=13><b>ЯЩИК ПОДАРКОВ</b></size>\n<size=14><b> ОСТАЛОСЬ ДО ОТКРЫТИЯ:{left}с!</b></size>"
                                            : "<size=13><b>ЯЩИК ПОДАРКОВ</b></size>\n<size=13><b>ДОСТУПНО</b></size>";
                                        key.SendConsoleCommand("ddraw.text", ins.config.TimerEvery + Time.deltaTime, Color.white,
                                            entity.CenterPoint(), $"{text}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            InRadius.Remove(key);
                        }
                    }
                    else
                    {
                        InRadius.Remove(key);
                    }
                }
            }
        }
        #endregion
    }
}