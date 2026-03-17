using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{ 
    [Info("UPRemove", "https://topplugin.ru/","1.1.2")]
    [Description("https://topplugin.ru/ REMOVE")] 
    public class UPRemove : RustPlugin
    {
        #region Cfg
        private Dictionary<uint, EntityData> _entityData = new Dictionary<uint, EntityData>();
        private class EntityData
        {
            public double RemoveTime = CurrentTime();
            
            public double IsRemove()
            {
                return Math.Max(RemoveTime - CurrentTime(), 0);
            }
        }
        private static DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        private static double CurrentTime() => DateTime.UtcNow.Subtract(epoch).TotalSeconds;
        private static ConfigData Cfg { get; set; }
        private class ConfigData
        {
            [JsonProperty("Черный список какие предметы нельзя ремувать?")] public List<string> blackList = new List<string>();
            [JsonProperty("Включить ремув по времени")] public bool timeOn = true;
            [JsonProperty("Время ремува после установки")] public int timeRemove = 21600;
            [JsonProperty("Разрешить ремув, если хп не фулл?")] public bool hpRemove = false;
            [JsonProperty("Возвращать ресурсы за ремув?")] public bool resRemove = true;
            [JsonProperty("Возвращать предметы за ремув?")] public bool itemRemove = true;
            [JsonProperty("Ремув по шкафу?")] public bool IsBuilding = true;
            [JsonProperty("Время ремува")] public int timeRemoves = 15;
            [JsonProperty("Время апгрейда")] public int timeUpgrade = 15;
            public static ConfigData GetNewConf()
            {
                var newConfig = new ConfigData
                {
                    blackList = new List<string>()
                    {
                        "autoturret_deployed"
                    }
                };
                return newConfig;
            }
        }
        protected override void LoadDefaultConfig()
        {
            Cfg = ConfigData.GetNewConf();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(Cfg);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig(); try { Cfg = Config.ReadObject<ConfigData>(); }catch { LoadDefaultConfig(); } NextTick(SaveConfig);
        }

        #endregion
        #region [Hooks]
        object OnStructureUpgrade(BaseCombatEntity entity, BasePlayer player, BuildingGrade.Enum grade)
        {
            if (entity == null || player == null) return null;  
            if(entity.gameObject.GetComponent<BuildingBlock>() != null)
                if (!CheckStash(entity.transform.position) || !CheckPlayer(entity.transform.position))
                {
                    SendReply(player, "В постройке что-то находится");
                    return false;
                }
            return null;
        }
        bool CheckPlayer(Vector3 position)
        {
            List<BasePlayer> _players = new List<BasePlayer>();
            position.y = TerrainMeta.HeightMap.GetHeight(position);
            Vis.Entities(position, 0.5f, _players);
            if (_players.Count > 0) 
            {
                Facepunch.Pool.FreeList(ref _players);
                return false;
            }
            Facepunch.Pool.FreeList(ref _players);
            return true;
        }
        bool CheckStash(Vector3 position)
        {
            List<StashContainer> _stash = new List<StashContainer>();
            position.y = TerrainMeta.HeightMap.GetHeight(position);
            Vis.Entities(position, 1.5f, _stash);
            if (_stash.Count > 0)
            {
                Facepunch.Pool.FreeList(ref _stash);
                return false;
            }
            Facepunch.Pool.FreeList(ref _stash);
            return true;
        }
        private object OnHammerHit(BasePlayer player, HitInfo info)
        {
            if(IsRaid(player) == true) return null;
            var entity = info?.HitEntity;
            if (player == null || entity == null) return null;
            if (_removeplayerList.ContainsKey(player.userID))
            {
                if (entity.Health() < entity.MaxHealth() && Cfg.hpRemove) return null;
                if (Cfg.blackList.Contains(entity.ShortPrefabName)) return null;
                if (!entity.OwnerID.IsSteamId()) return null;
                if (player.IsBuildingBlocked()) return null;
                if (player.userID != entity.OwnerID && !IsFriends(player.userID, entity.OwnerID) && !Cfg.IsBuilding) return null;
                if (entity is StorageContainer)
                {
                    var storage = entity as StorageContainer;
                    GiveRes(player, entity);
                    storage.OnKilled(info);
                    _removeplayerList[player.userID] = Cfg.timeRemoves;
                    return false;
                }
                if (entity is AutoTurret)
                {
                    var storage = entity as AutoTurret;
                    GiveRes(player, entity);
                    storage.OnKilled(info);
                    _removeplayerList[player.userID] = Cfg.timeRemoves;
                    return false;
                }
                if (entity is BuildingBlock)
                {
                    EntityData f;
                    if (Cfg.timeOn && _entityData.TryGetValue(entity.net.ID, out f) && f.IsRemove() <= 0)
                    {
                        return null;
                    }

                    GiveRes(player, entity);
                    entity.Kill();
                    _removeplayerList[player.userID] = Cfg.timeRemoves;
                    return false;
                }

                if (deployList.ContainsKey(entity.PrefabName))
                {
                    GiveRes(player, entity);
                    entity.Kill();
                    _removeplayerList[player.userID] = Cfg.timeRemoves;
                    return false;
                }
            }

            UpSet fs;
            if (_upplayerList.TryGetValue(player.userID, out fs))
            {
                if (!entity.OwnerID.IsSteamId()) return null;
                if (player.IsBuildingBlocked()) return null;
                if (player.userID != entity.OwnerID && !IsFriends(player.userID, entity.OwnerID)) return null;
                var target = entity as BuildingBlock;
                if (target == null) return null;
                if ((int)target.grade < fs.grade)
                {
                    fs.sec = Cfg.timeUpgrade;  
                    if (target.blockDefinition.checkVolumeOnUpgrade)
                    {
                        if (DeployVolume.Check(target.transform.position, target.transform.rotation, PrefabAttribute.server.FindAll<DeployVolume>(target.prefabID), ~(1 << target.gameObject.layer)))
                        {
					        SendReply(player, "В постройке что-то находится");
                            return false;
                        }
                    }
                    Grade(target, fs.grade, player);
                    return false;
                }
            }
            return null;
        }

        [PluginReference] private Plugin Friends, NoEscape;
        bool IsFriends(ulong iniciator, ulong target)
        {
            if (Friends)
                return Friends.Call<bool>("IsFriend", iniciator, target);
            return false;
        }
        bool IsRaid(BasePlayer iniciator)
        {
            if (!NoEscape) return false;
            var tryCheck = NoEscape.CallHook("IsRaidBlock", iniciator.userID);
            if(tryCheck != null)
            {
                return (bool) tryCheck;
            } 
            tryCheck = NoEscape.CallHook("IsRaidBlocked", iniciator.userID.ToString());
            if(tryCheck != null)
            { 
                return (bool) tryCheck;
            }
            tryCheck = NoEscape.CallHook("IsRaidBlocked", iniciator);
            if(tryCheck != null)
            { 
                return (bool) tryCheck;
            }
            return false;
        }
        private void OnEntitySpawned(BuildingBlock entity)
        {
            UpSet f;
            if (Cfg.timeOn && entity != null && !_entityData.ContainsKey(entity.net.ID))
            {
                _entityData.Add(entity.net.ID, new EntityData {RemoveTime = Cfg.timeRemove + CurrentTime()});
            }
            var player = BasePlayer.FindByID(entity.OwnerID);
            if (player != null && _upplayerList.TryGetValue(player.userID, out f))
            { 
                f.sec = Cfg.timeUpgrade; 
                if (!CheckStash(entity.transform.position) || !CheckPlayer(entity.transform.position))
                {
                    SendReply(player, "В постройке что-то находится");
                    return;
                }
                Grade(entity, f.grade, player);
            }
        }
        private void Unload()
        {
            Interface.Oxide.DataFileSystem.WriteObject("RemoveData", _entityData);
            foreach (var t in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(t, Remove);
            }
        }
        private void OnEntityDeath(BuildingBlock entity)
        {
            if (Cfg.timeOn && entity != null && _entityData.ContainsKey(entity.net.ID))
                _entityData.Remove(entity.net.ID);
        }
        private static Dictionary<string, string> deployList = new Dictionary<string, string>();
        private void OnServerInitialized()
        {            
            var itemDef = ItemManager.GetItemDefinitions();
            foreach (ItemDefinition iDef in itemDef)
            {
                if (iDef?.GetComponent<ItemModDeployable>() == null) continue;
                if (deployList.ContainsKey(iDef.GetComponent<ItemModDeployable>().entityPrefab.resourcePath)) continue;
                deployList.Add(iDef.GetComponent<ItemModDeployable>().entityPrefab.resourcePath, iDef.shortname);
            }
            TimerRemove();
            if(!permission.PermissionExists("upremove.remove"))
                permission.RegisterPermission("upremove.remove", this);
            if(!permission.PermissionExists("upremove.up"))
                permission.RegisterPermission("upremove.up", this);
                
            if(!permission.PermissionExists("upremove.up1"))
                permission.RegisterPermission("upremove.up1", this);
                
            if(!permission.PermissionExists("upremove.up2"))
                permission.RegisterPermission("upremove.up2", this);
                
            if(!permission.PermissionExists("upremove.up3"))
                permission.RegisterPermission("upremove.up3", this);
                
            if(!permission.PermissionExists("upremove.up4"))
                permission.RegisterPermission("upremove.up4", this);
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("RemoveData"))
                _entityData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<uint, EntityData>>("RemoveData");
        }

        #endregion
        #region [Mettods]

        private void Grade(BuildingBlock block, int grade, BasePlayer player)
        {
            if(IsRaid(player)) return;
            if(player.IsBuildingBlocked())  return;
            var items = block.blockDefinition.grades[grade].costToBuild.Last();
            var item = player.inventory.AllItems().FirstOrDefault(p => p.info.itemid == items.itemid);
            if (item == null)
            {
                SendReply(player, $"Не достаточно ресурсов");
                return;
            }

            if (items.amount > item.amount)
            {
                SendReply(player, $"Не достаточно ресурсов");
                return;
            }

            player.inventory.Take(null, item.info.itemid, (int) items.amount);
            player.SendConsoleCommand($"note.inv {item.info.itemid} -{(int) items.amount}");


            block.SetGrade((BuildingGrade.Enum) grade);
            block.SetHealthToMax();
            block.UpdateSkin(false);
        }

        CuiPanel _main = new CuiPanel()
        {
            RectTransform = {AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-180 -60", OffsetMax = "200 -40"},
            Image = {Color = "0.15 0.15 0.15 0.64"}
        };

        private string FormatUpgrade(int grade)
        {
            var text = String.Empty;
            switch (grade)
            {
                case 1:
                    text = "дерево";
                    break;
                case 2:
                    text = "камень";
                    break;
                case 3:
                    text = "железо";
                    break;
                case 4:
                    text = "мвк";
                    break;
            }
            return text;
        }
        private string Remove = "remove";
        
        [ChatCommand("up")]
        private void CmdUpgrade(BasePlayer player, string c, string[] a)
        {
            if(!permission.UserHasPermission(player.UserIDString, "upremove.up"))
            {
                SendReply(player, "Нет прав");
                return;
            }
            int i;
            UpSet f;
            if (_removeplayerList.ContainsKey(player.userID))
                _removeplayerList.Remove(player.userID);

            if (a.Length < 1)
            {
                if (_upplayerList.TryGetValue(player.userID, out f))
                {
                    if (f.grade >= 4)
                    {
                        f.sec = 0;
                        return;
                    }
                    f.grade++;
                    if(!permission.UserHasPermission(player.UserIDString, $"upremove.up{f.grade}"))
                    {
                        f.sec = 0;
                        SendReply(player, "Нет прав");
                        return;
                    }
                    f.sec = Cfg.timeUpgrade;
                    RemoveStart(player, f.sec, $"Авто-апгрейд в <color=#00FFFA>{FormatUpgrade(f.grade)}</color>");
                }
                else
                {
                    if(!permission.UserHasPermission(player.UserIDString, $"upremove.up1"))
                    {
                        SendReply(player, "Нет прав");
                        return;
                    }
                    _upplayerList.Add(player.userID, new UpSet{grade = 1, sec = Cfg.timeUpgrade});
                    RemoveStart(player, Cfg.timeUpgrade, $"Авто-апгрейд в <color=#00FFFA>{FormatUpgrade(1)}</color>");
                }
                return;
            }
            if (!int.TryParse(a[0], out i))
            { 
                if (_upplayerList.TryGetValue(player.userID, out f))
                {
                    CuiHelper.DestroyUi(player, Remove);
                    _upplayerList.Remove(player.userID);
                    SendReply(player, "Авто-апгрейд отключен!");
                }
                else
                { 
                    if(!permission.UserHasPermission(player.UserIDString, $"upremove.up1"))
                    {
                        SendReply(player, "Нет прав");
                        return;
                    }
                    _upplayerList.Add(player.userID, new UpSet{grade = 1, sec = Cfg.timeUpgrade});
                    RemoveStart(player, Cfg.timeUpgrade, $"Авто-апгрейд в <color=#00FFFA>{FormatUpgrade(1)}</color>"); 
                }
                return;
            }
            switch (i)
            {
                case 0:
                    if(!_upplayerList.ContainsKey(player.userID)) return;
                    CuiHelper.DestroyUi(player, Remove);
                    _upplayerList.Remove(player.userID);
                    SendReply(player, "Авто-апгрейд отключен!");
                    break;
                case 1:
                    if(!permission.UserHasPermission(player.UserIDString, "upremove.up1"))
                    {
                        SendReply(player, "Нет прав");
                        return;
                    }
                    if(!_upplayerList.TryGetValue(player.userID, out f)) _upplayerList.Add(player.userID, new UpSet{grade = 1, sec = Cfg.timeUpgrade});
                    if (f != null)
                    {
                        f.grade = 1;
                        f.sec = Cfg.timeUpgrade;
                        return;
                    }
                    RemoveStart(player, Cfg.timeUpgrade, $"Авто-апгрейд в <color=#00FFFA>{FormatUpgrade(1)}</color>");
                    break;
                case 2:
                    if(!permission.UserHasPermission(player.UserIDString, "upremove.up2")) return;
                    if(!_upplayerList.TryGetValue(player.userID, out f)) _upplayerList.Add(player.userID, new UpSet{grade = 2, sec = Cfg.timeUpgrade});
                    if (f != null)
                    {
                        f.grade = 2;
                        f.sec = Cfg.timeUpgrade;
                        return;
                    }
                    RemoveStart(player, Cfg.timeUpgrade, $"Авто-апгрейд в <color=#00FFFA>{FormatUpgrade(2)}</color>");
                    break;
                case 3:
                    if(!permission.UserHasPermission(player.UserIDString, "upremove.up3"))    
                    {
                        SendReply(player, "Нет прав");
                        return;
                    }
                    if(!_upplayerList.TryGetValue(player.userID, out f)) _upplayerList.Add(player.userID, new UpSet{grade = 3, sec = Cfg.timeUpgrade});
                    if (f != null)
                    {
                        f.grade = 3;
                        f.sec = Cfg.timeUpgrade;
                        return;
                    }
                    RemoveStart(player, Cfg.timeUpgrade, $"Авто-апгрейд в <color=#00FFFA>{FormatUpgrade(3)}</color>");
                    break;
                case 4:
                    if(!permission.UserHasPermission(player.UserIDString, "upremove.up4")) 
                    {
                        SendReply(player, "Нет прав");
                        return;
                    }
                    if(!_upplayerList.TryGetValue(player.userID, out f)) _upplayerList.Add(player.userID, new UpSet{grade =4, sec = Cfg.timeUpgrade});
                    if (f != null)
                    {
                        f.grade = 4;
                        f.sec = Cfg.timeUpgrade;
                        return;
                    }
                    RemoveStart(player, Cfg.timeUpgrade, $"Авто-апгрейд в <color=#00FFFA>{FormatUpgrade(4)}</color>");
                    break;
                default:
                    if (_upplayerList.TryGetValue(player.userID, out f))
                    {
                        if (f.grade >= 4)
                        {
                            f.sec = 0;
                            return;
                        }
                        f.grade++;
                        if(!permission.UserHasPermission(player.UserIDString, $"upremove.up{f.grade}"))
                        {
                            SendReply(player, "Нет прав");
                            f.sec = 0;
                            return;
                        }
                        f.sec = Cfg.timeUpgrade;
                        RemoveStart(player, f.sec, $"Авто-апгрейд в <color=#00FFFA>{FormatUpgrade(f.grade)}</color>");
                    }
                    else
                    {
                        if(!permission.UserHasPermission(player.UserIDString, $"upremove.up1")) return;
                        _upplayerList.Add(player.userID, new UpSet{grade = 1, sec = Cfg.timeUpgrade});
                        RemoveStart(player, Cfg.timeUpgrade, $"Авто-апгрейд в <color=#00FFFA>{FormatUpgrade(f.grade)}</color>");
                    }
                    break;
            }
        }
        [ChatCommand("remove")]
        private void CmdRemove(BasePlayer player)
        {
            if(!permission.UserHasPermission(player.UserIDString, "upremove.remove"))
            {
                SendReply(player, "Нет прав");
                return;
            }
            if (_upplayerList.ContainsKey(player.userID))
                _upplayerList.Remove(player.userID);

            if (_removeplayerList.ContainsKey(player.userID))
            {
                CuiHelper.DestroyUi(player, Remove);
                _removeplayerList.Remove(player.userID);
                SendReply(player, "Ремув отключен!");
                return;
            }
            _removeplayerList.Add(player.userID, Cfg.timeRemoves);
            RemoveStart(player, Cfg.timeRemoves, "Ремув");
            SendReply(player, "Для ремува объекта, ударьте киянкой по этому объекту.");
        }
        private void RemoveStart(BasePlayer player, int sec, string type)
        {
            CuiHelper.DestroyUi(player, Remove);
            var cont = new CuiElementContainer();
            cont.Add(_main, "Hud", Remove);
            cont.Add(new CuiElement()
            {
                Parent = Remove,
                Components = {new CuiTextComponent(){Text = $"{type} отключится через <color=red>{sec}</color> сек", Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter}, new CuiRectTransformComponent(){AnchorMin = "0 0", AnchorMax = "1 1"}}
            });
            CuiHelper.AddUi(player, cont);
        }
        private Dictionary<ulong, int> _removeplayerList = new Dictionary<ulong, int>();
        private Dictionary<ulong, UpSet> _upplayerList = new Dictionary<ulong, UpSet>();

        public class UpSet
        {
            public int grade;
            public int sec;
        }
        private void TimerRemove()
        {
            timer.Every(1f, () =>
            {
                _removeplayerList.ToList().ForEach(pt =>
                {
                    var find = pt.Key;
                    var findData = --_removeplayerList[pt.Key];
                    var player = BasePlayer.FindByID(find);
                    if (player != null)
                        RemoveStart(player, findData, "Ремув");
                    if (findData <= 0)
                    {
                        if (player != null)
                        {
                            SendReply(player, "Время ремува вышло!");
                            CuiHelper.DestroyUi(player, Remove);
                        }

                        _removeplayerList.Remove(find);
                    }
                });
                
                _upplayerList.ToList().ForEach(pt =>
                {
                    var find = pt.Key;
                    var findData = _upplayerList[pt.Key];
                    var player = BasePlayer.FindByID(find);
                    if (player != null) RemoveStart(player, --findData.sec, $"Авто-апгрейд в <color=#00FFFA>{FormatUpgrade(findData.grade)}</color>");
                    if (findData.sec <= 0)
                    {
                        if (player != null)
                        {
                            SendReply(player, "Время Авто-апгрейд вышло!");
                            CuiHelper.DestroyUi(player, Remove);
                        }
                        _upplayerList.Remove(find);
                    }
                } );
            });
        }
        private void GiveRes(BasePlayer player, BaseEntity entity)
        {
            if (entity is BuildingBlock)
            {
                var block = entity as BuildingBlock;
                if (block.blockDefinition == null) return;
                if(!Cfg.resRemove) return;
                var findItems = block.blockDefinition.grades[(int) block.grade].costToBuild;
                foreach (var it in findItems)
                {
                    var item = ItemManager.CreateByItemID(it.itemid, (int) it.amount);
                    player.SendConsoleCommand($"note.inv {item.info.itemid} {(int) it.amount}"); 
                    if (!player.inventory.GiveItem(item))
                        item.Drop(player.inventory.containerMain.dropPosition,
                            player.inventory.containerMain.dropVelocity);
                }
                return;
            }
            if (deployList.ContainsKey(entity.PrefabName))
            {
                if(!Cfg.itemRemove) return;
                var item = ItemManager.CreateByName(deployList[entity.PrefabName], 1);
                player.SendConsoleCommand($"note.inv {item.info.itemid} 1");
                if (!player.inventory.GiveItem(item))
                    item.Drop(player.inventory.containerMain.dropPosition,
                        player.inventory.containerMain.dropVelocity);
            }

        }

        #endregion
    }
}
