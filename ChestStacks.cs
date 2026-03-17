using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Oxide.Core;
using ProtoBuf;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Chest Stacks", "supreme", "1.3.3")]
    [Description("Allows players to stack chests")]
    public class ChestStacks : RustPlugin
    {
        #region Class Fields
        
        private static ChestStacks _pluginInstance;
        private PluginConfig _pluginConfig;
        private PluginData _pluginData;
        
        private const string UsePermission = "cheststacks.use";
        
        private const string LargeBoxEffect = "assets/prefabs/deployable/large wood storage/effects/large-wood-box-deploy.prefab";
        private const string LargeBoxPrefab = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab";
        private const string LargeBoxDeployedEntity = "box.wooden.large";
        private const string LargeBoxShortname = "box.wooden.large";
        
        private const string SmallBoxEffect = "assets/prefabs/deployable/woodenbox/effects/wooden-box-deploy.prefab";
        private const string SmallBoxPrefab = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";
        private const string SmallBoxDeployedEntity = "woodbox_deployed";
        private const string SmallBoxShortname = "box.wooden";
        
        private const string CoffinPrefab = "assets/prefabs/misc/halloween/coffin/coffinstorage.prefab";
        private const string CoffinDeployedEntity = "coffinstorage";
        private const string CoffinShortname = "coffin.storage";

        private readonly object _returnObject = true;

        private readonly HashSet<ulong> _cachedBoxes = new HashSet<ulong>();

        private enum ChestType : byte
        {
            None = 0,
            SmallBox = 1,
            LargeBox = 2,
            Coffin = 3
        }

        #endregion

        #region Hooks
        
        private void Init()
        {
            _pluginInstance = this;
            LoadData();
            
            HashSet<string> perms = new HashSet<string>{UsePermission};
            foreach (string perm in _pluginConfig.ChestStacksAmount.Keys)
            {
                perms.Add(perm);
            }

            foreach (string perm in perms)
            {
                permission.RegisterPermission(perm, this);
            }
        }

        private void OnServerInitialized()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }
            
            foreach (ulong chestId in _pluginData.StoredBoxes.Keys)
            {
                BoxStorage foundChest = BaseNetworkable.serverEntities.Find(new NetworkableId(chestId)) as BoxStorage;
                if (!foundChest)
                {
                    continue;
                }
                
                DestroyGroundWatch(foundChest);
            }
            
            SaveData();
        }

        private void Unload()
        {
            foreach (ChestStacking chestStacking in UnityEngine.Object.FindObjectsOfType<ChestStacking>())
            {
                chestStacking.DoDestroy();
            }

            SaveData();
            _pluginInstance = null;
        }
        
        private void OnPlayerConnected(BasePlayer player)
        {
            ChestStacking chestStacking = player.gameObject.GetComponent<ChestStacking>();
            if (chestStacking)
            {
                return;
            }
            
            player.gameObject.AddComponent<ChestStacking>();
        }
        
        private void OnPlayerDisconnected(BasePlayer player)
        {
            ChestStacking chestStacking = player.gameObject.GetComponent<ChestStacking>();
            if (!chestStacking)
            {
                return;
            }
            
            chestStacking.DoDestroy();
        }

        private object OnEntityGroundMissing(BoxStorage box)
        {
            if (!box)
            {
                return null;
            }
            
            if (_pluginData.StoredBoxes.ContainsKey(box.net.ID.Value))
            {
                return _returnObject;
            }

            return null;
        }

        private object OnEntityKill(BoxStorage box)
        {
            if (!box)
            {
                return null;
            }

            if (_pluginData.StoredBoxes.ContainsKey(box.net.ID.Value) && box.health > 0 && HasGround(box) && !_cachedBoxes.Contains(box.net.ID.Value))
            {
                return _returnObject;
            }

            if (_cachedBoxes.Contains(box.net.ID.Value))
            {
                _cachedBoxes.Remove(box.net.ID.Value);
            }

            List<BoxStorage> boxes = OverlapSphere<BoxStorage>(box.transform.position, 2f, Layers.Mask.Deployed);
            if (boxes.Count > 0)
            {
                foreach (BoxStorage foundBox in boxes)
                {
                    if (_pluginData.StoredBoxes.ContainsKey(foundBox.net.ID.Value))
                    {
                        NextFrame(() => CheckGround(foundBox));
                    }
                }
            }

            if (!_pluginData.StoredBoxes.ContainsKey(box.net.ID.Value))
            {
                return null;
            }

            HandleUnStacking(box);
            return null;
        }
        
        private void CanPickupEntity(BasePlayer player, BoxStorage box)
        {
            if (_pluginData.StoredBoxes.ContainsKey(box.net.ID.Value))
            {
                _cachedBoxes.Add(box.net.ID.Value);
            }
        }

        #endregion

        #region Remover Tool Hooks

        private object canRemove(BasePlayer player, BoxStorage box)
        {
            if (_pluginData.StoredBoxes.ContainsKey(box.net.ID.Value))
            {
                return _returnObject;
            }

            return null;
        }

        #endregion
        
        #region Helper Methods

        private void DestroyGroundWatch(BaseEntity entity)
        {
            DestroyOnGroundMissing missing = entity.GetComponent<DestroyOnGroundMissing>();
            if (missing)
            {
                UnityEngine.Object.Destroy(missing);
            }
            
            GroundWatch watch = entity.GetComponent<GroundWatch>();
            if (watch)
            {
                UnityEngine.Object.Destroy(watch);
            }
        }

        private void CheckGround(BoxStorage box)
        {
            if (!box)
            {
                return;
            }
            
            RaycastHit raycast;
            if (!Physics.Raycast(box.transform.position, Vector3.down, out raycast, 0.5f, Layers.Mask.Deployed))
            {
                box.DropItems();
                box.Kill();
            }
        }

        private bool HasGround(BoxStorage box)
        {
            if (!box)
            {
                return false;
            }
            
            RaycastHit raycast;
            if (!Physics.Raycast(box.transform.position, Vector3.down, out raycast, 0.5f, Layers.Mask.Deployed))
            {
                return false;
            }

            return true;
        }
        
        private bool HasCeiling(BoxStorage box)
        {
            if (!box)
            {
                return false;
            }
            
            RaycastHit raycast;
            if (Physics.Raycast(box.transform.position + new Vector3(0f, 0.8f), Vector3.up, out raycast, 0.5f, Layers.Mask.Construction))
            {
                return true;
            }

            return false;
        }
        
        private List<T> OverlapSphere<T>(Vector3 pos, float radius, int layer)
        {
            return Physics.OverlapSphere(pos, radius, layer).Select(c => c.ToBaseEntity()).OfType<T>().ToList();
        }

        private ulong GetBottomBoxId(ulong netId)
        {
            if (_pluginData.StoredBoxes[netId]?.BottomBoxId == null)
            {
                return 0;
            }
            
            return _pluginData.StoredBoxes[netId].BottomBoxId;
        }

        private int GetBoxes(ulong netId)
        {
            if (_pluginData.StoredBoxes[netId]?.Boxes == null)
            {
                return 0;
            }

            return _pluginData.StoredBoxes[netId].Boxes;
        }
        
        private void HandleUnStacking(BoxStorage box)
        {
            ulong bottomBoxId = GetBottomBoxId(box.net.ID.Value);
            if (bottomBoxId == 0)
            {
                return;
            }
            
            int boxes = GetBoxes(bottomBoxId);
            _pluginData.StoredBoxes[bottomBoxId].Boxes = --boxes;
        }
        
        private bool HasPermission(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);
        
        private int GetPermissionValue(BasePlayer player, Hash<string, int> permissions, int defaultValue)
        {
            foreach (KeyValuePair<string, int> perm in permissions.OrderByDescending(p => p.Value))
            {
                if (HasPermission(player, perm.Key))
                {
                    return perm.Value;
                }
            }

            return defaultValue;
        }

        #endregion

        #region Chest Stacking Handler
        
        public class ChestStacking : FacepunchBehaviour
        {
            private BasePlayer Player { get; set; }
            
            private float NextTime { get; set; }
            
            private void Awake()
            {
                Player = GetComponent<BasePlayer>();
            }

            private void Update()
            {
                if (!Player || !_pluginInstance.permission.UserHasPermission(Player.UserIDString, UsePermission))
                {
                    return;
                }

                if (Player.serverInput.WasJustPressed(BUTTON.FIRE_SECONDARY))
                {
                    if (NextTime > Time.time)
                    {
                        return;
                    }
                    
                    NextTime = Time.time + 0.5f;
                    
                    Item activeItem = Player.GetActiveItem();
                    if (activeItem == null || activeItem.info.shortname != SmallBoxShortname && activeItem.info.shortname != LargeBoxShortname && activeItem.info.shortname != CoffinShortname)
                    {
                        return;
                    }
                    
                    if (_pluginInstance._pluginConfig.BlacklistedSkins.Contains(activeItem.skin))
                    {
                        return;
                    }
                        
                    BoxStorage box = GetBox(Player);
                    if (!box)
                    {
                        return;
                    }
                    
                    ulong bottomBoxId = _pluginInstance.GetBottomBoxId(box.net.ID.Value);
                    int boxes = _pluginInstance.GetBoxes(bottomBoxId);
                    int allowedBoxesAmount = _pluginInstance.GetPermissionValue(Player, _pluginInstance._pluginConfig.ChestStacksAmount, 2);
                    if (boxes >= allowedBoxesAmount)
                    {
                        Player.ChatMessage(_pluginInstance.Lang(LangKeys.MaxStackAmount, null, allowedBoxesAmount));
                        return;
                    }

                    BuildingPrivlidge tc = Player.GetBuildingPrivilege();

                    switch (box.ShortPrefabName)
                    {
                        case SmallBoxDeployedEntity:
                        {
                            StackChest(box, activeItem, ChestType.SmallBox, tc);
                            break;
                        }
                        case LargeBoxDeployedEntity:
                        {
                            StackChest(box, activeItem, ChestType.LargeBox, tc);
                            break;
                        }
                        case CoffinDeployedEntity:
                        {
                            StackChest(box, activeItem, ChestType.Coffin, tc);
                            break;
                        }
                    }
                }
            }

            private void StackChest(BoxStorage box, Item activeItem, ChestType chestType, [CanBeNull] BuildingPrivlidge tc)
            {
                if (_pluginInstance._pluginConfig.StackChestsInBuildingPrivileged && !Player.IsBuildingAuthed())
                {
                    Player.ChatMessage(_pluginInstance.Lang(LangKeys.BuildingBlock));
                    return;
                }

                if (_pluginInstance.HasCeiling(box))
                {
                    Player.ChatMessage(_pluginInstance.Lang(LangKeys.CeilingBlock));
                    return;
                }

                switch (chestType)
                {
                    case ChestType.SmallBox:
                    {
                        if (activeItem.info.shortname != SmallBoxShortname)
                        {
                            Player.ChatMessage(_pluginInstance.Lang(LangKeys.OnlyStackSameType));
                            return;
                        }
                        
                        if (Physics.OverlapSphere(box.transform.position + new Vector3(0f, 0.7f), 0.1f, Layers.Mask.Deployed).Length > 0)
                        {
                            return;
                        }
                        
                        BoxStorage smallBox = GameManager.server.CreateEntity(SmallBoxPrefab, box.transform.position + new Vector3(0f, 0.57f), box.ServerRotation) as BoxStorage;
                        if (!smallBox)
                        {
                            return;
                        }
                        
                        smallBox.Spawn();
                        smallBox.OwnerID = Player.userID;
                        smallBox.skinID = activeItem.skin;
                        if (tc)
                        {
                            smallBox.AttachToBuilding(tc.buildingID);
                        }
                        
                        smallBox.SendNetworkUpdateImmediate();
                        Interface.CallHook("OnEntityBuilt", Player.GetHeldEntity(), smallBox.transform.gameObject);
                        _pluginInstance.DestroyGroundWatch(smallBox);
                        Effect.server.Run(SmallBoxEffect, box.transform.position);
                        HandleStacking(box, smallBox);
                        break;
                    }
                    case ChestType.LargeBox:
                    {
                        if (activeItem.info.shortname != LargeBoxShortname)
                        {
                            Player.ChatMessage(_pluginInstance.Lang(LangKeys.OnlyStackSameType));
                            return;
                        }
                        
                        if (Physics.OverlapSphere(box.transform.position + new Vector3(0f, 0.9f), 0.1f, Layers.Mask.Deployed).Length > 0) 
                        {
                            return; 
                        }
                        
                        BoxStorage largeBox = GameManager.server.CreateEntity(LargeBoxPrefab, box.transform.position + new Vector3(0f, 0.8f), box.ServerRotation) as BoxStorage; 
                        if (!largeBox) 
                        { 
                            return; 
                        }
                        
                        largeBox.Spawn(); 
                        largeBox.OwnerID = Player.userID; 
                        largeBox.skinID = activeItem.skin;
                        if (tc)
                        {
                            largeBox.AttachToBuilding(tc.buildingID); 
                        }
                        
                        largeBox.SendNetworkUpdateImmediate(); 
                        Interface.CallHook("OnEntityBuilt", Player.GetHeldEntity(), largeBox.transform.gameObject); 
                        _pluginInstance.DestroyGroundWatch(largeBox); 
                        Effect.server.Run(LargeBoxEffect, box.transform.position);
                        HandleStacking(box, largeBox);
                        break;
                    }
                    case ChestType.Coffin:
                    {
                        if (activeItem.info.shortname != CoffinShortname)
                        {
                            Player.ChatMessage(_pluginInstance.Lang(LangKeys.OnlyStackSameType));
                            return;
                        }
                        
                        if (Physics.OverlapSphere(box.transform.position + new Vector3(0f, 0.68f), 0.1f, Layers.Mask.Deployed).Length > 0)
                        {
                            return;
                        }

                        BoxStorage coffin = GameManager.server.CreateEntity(CoffinPrefab, box.transform.position + new Vector3(0f, 0.6f), box.ServerRotation) as BoxStorage;
                        if (!coffin)
                        {
                            return;
                        }
                        
                        coffin.Spawn();
                        coffin.OwnerID = Player.userID;
                        coffin.skinID = activeItem.skin;
                        if (tc)
                        {
                            coffin.AttachToBuilding(tc.buildingID);
                        }
                        
                        coffin.SendNetworkUpdateImmediate();
                        Interface.CallHook("OnEntityBuilt", Player.GetHeldEntity(), coffin.transform.gameObject);
                        _pluginInstance.DestroyGroundWatch(coffin);
                        Effect.server.Run(SmallBoxEffect, box.transform.position);
                        HandleStacking(box, coffin);
                        break;
                    }
                }
                
                if (activeItem.amount == 1 || activeItem.amount < 0) 
                { 
                    activeItem.Remove(); 
                }
                else 
                { 
                    --activeItem.amount; 
                }
                
                activeItem.MarkDirty();
                _pluginInstance.SaveData();
            }

            private void HandleStacking(BoxStorage lastBox, BoxStorage newBox)
            {
                ulong bottomBoxId = _pluginInstance.GetBottomBoxId(lastBox.net.ID.Value);
                if (bottomBoxId == 0)
                {
                    _pluginInstance._pluginData.StoredBoxes[newBox.net.ID.Value] = new BoxData
                    {
                        BottomBoxId = newBox.net.ID.Value,
                        Boxes = 2
                    };
                }
                else
                {
                    int boxes = _pluginInstance.GetBoxes(bottomBoxId);
                    _pluginInstance._pluginData.StoredBoxes[newBox.net.ID.Value] = new BoxData
                    {
                        BottomBoxId = bottomBoxId,
                    };

                    _pluginInstance._pluginData.StoredBoxes[bottomBoxId].Boxes = ++boxes;
                }
            }
            
            private BoxStorage GetBox(BasePlayer player)
            {
                RaycastHit raycast;
                if (!Physics.Raycast(player.eyes.position, player.eyes.HeadForward(), out raycast, 3f, Layers.Mask.Deployed))
                {
                    return null;
                }
            
                return raycast.GetEntity() as BoxStorage;
            }

            public void DoDestroy()
            {
                DestroyImmediate(this);
            }
        }

        #endregion

        #region Configuration
        
        private class PluginConfig
        {

            [DefaultValue(true)]
            [JsonProperty(PropertyName = "Only stack chests in Building Privileged zones")]
            public bool StackChestsInBuildingPrivileged { get; set; }
            
            [JsonProperty(PropertyName = "Blacklisted Skins")]
            public HashSet<ulong> BlacklistedSkins { get; set; }
            
            [JsonProperty(PropertyName = "Permissions & their amount of stacked chests allowed")]
            public Hash<string, int> ChestStacksAmount { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Loading Default Config");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = AdditionalConfig(Config.ReadObject<PluginConfig>());
            Config.WriteObject(_pluginConfig);
        }

        private PluginConfig AdditionalConfig(PluginConfig pluginConfig)
        {
            pluginConfig.BlacklistedSkins = pluginConfig.BlacklistedSkins ?? new HashSet<ulong>
            {
                2618923347
            };

            pluginConfig.ChestStacksAmount = pluginConfig.ChestStacksAmount ?? new Hash<string, int>
            {
                ["cheststacks.use"] = 3,
                ["cheststacks.vip"] = 4
            };
            
            return pluginConfig;
        }

        #endregion
        
        #region Data

        private void SaveData()
        {
            if (_pluginData == null)
            {
                return;
            }
            
            ProtoStorage.Save(_pluginData, Name);
        }

        private void LoadData()
        {
            _pluginData = ProtoStorage.Load<PluginData>(Name) ?? new PluginData();
        }

        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        private class PluginData
        {
            public Hash<ulong, BoxData> StoredBoxes { get; set; } = new Hash<ulong, BoxData>();
        }

        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        private class BoxData
        {
            public ulong BottomBoxId { get; set; }
            
            public int Boxes { get; set; }
        }
        
        #endregion
        
        #region Language
        
        private class LangKeys
        {
            public const string MaxStackAmount = nameof(MaxStackAmount);
            public const string OnlyStackSameType = nameof(OnlyStackSameType);
            public const string CeilingBlock = nameof(CeilingBlock);
            public const string BuildingBlock = nameof(BuildingBlock);
        }
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.MaxStackAmount] = "You are trying to stack more than {0} chests!",
                [LangKeys.OnlyStackSameType] = "You can only stack the same type of chests!",
                [LangKeys.CeilingBlock] = "A ceiling is blocking you from stacking this chest!",
                [LangKeys.BuildingBlock] = "You need to be Building Privileged in order to stack chests!"

            }, this);
        }
        
        private string Lang(string key, BasePlayer player = null)
        {
            return lang.GetMessage(key, this, player?.UserIDString);
        }
        
        private string Lang(string key, BasePlayer player = null, params object[] args)
        {
            try
            {
                return string.Format(Lang(key, player), args);
            }
            catch (Exception ex)
            {
                PrintError($"Lang Key '{key}' threw exception:\n{ex}");
                throw;
            }
        }
        
        #endregion
    }
}