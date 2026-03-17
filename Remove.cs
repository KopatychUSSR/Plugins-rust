using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oxide.Core.Libraries;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("Remove", "OxideBro", "1.0.5")]
    class Remove : RustPlugin
    {

        #region CONFIGURATION
        int resetTime;
        float refundPercent;
        float refundStoragePercent;
        bool friendRemove;
        bool clanRemove;
        bool cupboardRemove;
        bool selfRemove;
        bool removeFriends;
        bool removeClans;
        bool refundItemsGive;

        //GUI
        private string PanelAnchorMin;
        private string PanelAnchorMax;
        private string PanelColor;
        private bool useNoEscape;
        private int TextFontSize;
        private string TextСolor; 
        private string TextAnchorMin;
        private string TextAnchorMax;  

        protected override void LoadDefaultConfig()
        {
            GetVariable(Config, "Время действия режима удаления", out resetTime, 40);
            GetVariable(Config, "Процент возвращаемых ресурсов (Максимум 1.0 - это 100%)", out refundPercent, 1.0f);
            GetVariable(Config, "Включить возрат объектов (При удаление объектов(сундуки, печки и тд.) будет возращать объект а не ресурсы)", out refundItemsGive, false);
            GetVariable(Config, "Процент выпадающих ресурсов (не вещей) с удаляемых ящиков (Максимум 1.0 - это 100%)", out refundStoragePercent, 1.0f);
            GetVariable(Config, "Разрешить удаление объектов друзей без авторизации в шкафу", out friendRemove, false);
            GetVariable(Config, "Разрешить удаление объектов соклановцев без авторизации в шкафу", out clanRemove, false);
            GetVariable(Config, "Разрешить удаление чужих объектов при наличии авторизации в шкафу", out cupboardRemove, false);
            GetVariable(Config, "Разрешить удаление собственных объектов без авторизации в шкафу", out selfRemove, false);
            GetVariable(Config, "Включить поддержку NoEscape (С сайта RustPlugin.ru)", out useNoEscape, false);
            GetVariable(Config, "Разрешить удаление обьектов друзьям", out removeFriends, false);
            GetVariable(Config, "Разрешить удаление объектов соклановцев", out removeClans, false);
            GetVariable(Config, "GUI: Панель AnchorMin", out PanelAnchorMin, "0.0 0.908");
            GetVariable(Config, "GUI: Панель AnchorMax", out PanelAnchorMax, "1 0.958");
            GetVariable(Config, "GUI: Цвет фона", out PanelColor, "0 0 0 0.50");
            GetVariable(Config, "GUI: Размер текста", out TextFontSize, 18);
            GetVariable(Config, "GUI: Цвет текста", out TextСolor, "0 0 0 1");
            GetVariable(Config, "GUI: Текст AnchorMin", out TextAnchorMin, "0 0");
            GetVariable(Config, "GUI: Текст AnchorMax", out TextAnchorMax, "1 1");
            SaveConfig();
        }

        public static void GetVariable<T>(DynamicConfigFile config, string name, out T value, T defaultValue)
        {
            config[name] = value = config[name] == null ? defaultValue : (T)Convert.ChangeType(config[name], typeof(T));
        }

        #endregion

        #region FIELDS
        private readonly int triggerLayer = LayerMask.GetMask("Trigger");
        private readonly int triggerMask = LayerMask.GetMask("Prevent_Building");
        static int constructionColl = LayerMask.GetMask(new string[] { "Construction", "Deployable", "Prevent Building", "Deployed" });
        private static Dictionary<string, int> deployedToItem = new Dictionary<string, int>();
        private Dictionary<ulong, int> AmountEntities = new Dictionary<ulong, int>();
        Dictionary<BasePlayer, int> timers = new Dictionary<BasePlayer, int>();
        List<ulong> activePlayers = new List<ulong>();
        List<ulong> activePlayersAdmin = new List<ulong>();
        List<ulong> activePlayersAll = new List<ulong>();
        int currentRemove = 0;
        #endregion

        #region CLANS PLUGIN REFERENCE

        [PluginReference]
        Plugin Clans;
        [PluginReference]
        Plugin Friends;
        [PluginReference]
        Plugin NoEscape;

        bool IsClanMember(ulong playerID, ulong targetID)
        {
            return (bool)(Clans?.Call("IsTeammate", playerID, targetID) ?? false);
        }

        bool IsFriends(ulong playerID, ulong targetID)
        {
            return (bool)(Friends?.Call("IsFriend", playerID, targetID) ?? false);
        }

        #endregion

        #region COMMANDS

        [ChatCommand("remove")]
        void cmdRemove(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "remove.use"))
            {
                SendReply(player, "У тебя нету прав на использование этой команды");
                return;
            }
            if (player == null) return;
            if (activePlayers.Contains(player.userID))
            {
                timers.Remove(player);
                DeactivateRemove(player.userID);
                DestroyUI(player);
            }
            else
            {
                SendReply(player, "<size=18>Используйте киянку для удаления объектов</size>");
                timers[player] = resetTime;
                DrawUI(player, resetTime);
                ActivateRemove(player.userID);
            }
            if (activePlayersAdmin.Contains(player.userID))
            {
                timers.Remove(player);
                DeactivateRemoveAdmin(player.userID);
                DestroyUIAdmin(player);
            }
            if (activePlayersAll.Contains(player.userID))
            {
                timers.Remove(player);
                DeactivateRemoveAll(player.userID);
                DestroyUIAll(player);
            }

            if (args.Length != 0)
            {
                switch (args[0])
                {
                    case "admin":
                        if (!permission.UserHasPermission(player.UserIDString, "remove.admin") && !player.IsAdmin)
                        {
                            SendReply(player, "У тебя нету прав на использование этой команды");
                            return;
                        }
                        if (activePlayersAdmin.Contains(player.userID))
                        {
                            timers.Remove(player);
                            DeactivateRemoveAdmin(player.userID);
                            DestroyUIAdmin(player);
                        }
                        else
                        {
                            timers[player] = resetTime;
                            DrawUIAdmin(player, resetTime);
                            ActivateRemoveAdmin(player.userID);
                        }
                        return;
                    case "all":
                        if (!permission.UserHasPermission(player.UserIDString, "remove.admin") && !player.IsAdmin)
                        {
                            SendReply(player, "У тебя нету прав на использование этой команды");
                            return;
                        }
                        if (activePlayersAll.Contains(player.userID))
                        {
                            timers.Remove(player);
                            DeactivateRemoveAdmin(player.userID);
                            DestroyUIAdmin(player);
                        }
                        else
                        {
                            timers[player] = resetTime;
                            DrawUIAll(player, resetTime);
                            ActivateRemoveAll(player.userID);
                        }
                        return;
                }


            }
        }

        #endregion

        #region OXIDE HOOKS

        void Loaded()
        {
            PermissionService.RegisterPermissions(this, permisions);
        }

        public List<string> permisions = new List<string>()
        {
            "remove.admin",
            "remove.use"
        };

        void OnServerInitialized()
        {
            LoadDefaultConfig();
            deployedToItem.Clear();
            lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
            InitRefundItems();
            timer.Every(1f, GradeTimerHandler);
            List<ItemDefinition> ItemsDefinition = ItemManager.GetItemDefinitions() as List<ItemDefinition>;
            foreach (ItemDefinition itemdef in ItemsDefinition)
            {
                if (itemdef?.GetComponent<ItemModDeployable>() == null) continue;
                if (deployedToItem.ContainsKey(itemdef.GetComponent<ItemModDeployable>().entityPrefab.resourcePath)) continue;
                deployedToItem.Add(itemdef.GetComponent<ItemModDeployable>().entityPrefab.resourcePath, itemdef.itemid);
            }
        }

        private bool CupboardPrivlidge(BasePlayer player, Vector3 position, BaseEntity entity)
        {
            return player.IsBuildingAuthed(position, new Quaternion(0, 0, 0, 0),
                new Bounds(Vector3.zero, Vector3.zero));
        }

        void RemoveAllFrom(Vector3 pos)
        {
            removeFrom.Add(pos);
            DelayRemoveAll();
        }

        List<BaseEntity> wasRemoved = new List<BaseEntity>();
        List<Vector3> removeFrom = new List<Vector3>();

        void DelayRemoveAll()
        {
            if (currentRemove >= removeFrom.Count)
            {
                currentRemove = 0;
                removeFrom.Clear();
                wasRemoved.Clear();
                return;
            }
            List<BaseEntity> list = Pool.GetList<BaseEntity>();
            Vis.Entities<BaseEntity>(removeFrom[currentRemove], 3f, list, constructionColl);
            for (int i = 0; i < list.Count; i++)
            {
                BaseEntity ent = list[i];
                var reply = 785;
                if (wasRemoved.Contains(ent)) continue;
                if (!removeFrom.Contains(ent.transform.position))
                    removeFrom.Add(ent.transform.position);
                wasRemoved.Add(ent);
                DoRemove(ent);
            }
            currentRemove++;
            timer.Once(0.01f, () => DelayRemoveAll());
        }

        static void DoRemove(BaseEntity removeObject)
        {
            if (removeObject == null) return;

            StorageContainer Container = removeObject.GetComponent<StorageContainer>();

            if (Container != null)
            {
                DropUtil.DropItems(Container.inventory, removeObject.transform.position, Container.dropChance);
            }

            EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/item_break.prefab", removeObject, 0, Vector3.up, Vector3.zero) { scale = UnityEngine.Random.Range(0f, 1f) });

            removeObject.KillMessage();
        }

        void TryRemove(BasePlayer player, BaseEntity removeObject)
        {
            RemoveAllFrom(removeObject.transform.position);
        }

        object OnHammerHit(BasePlayer player, HitInfo info, Vector3 pos)
        {
            if (!activePlayers.Contains(player.userID)) return null;
            var entity = info?.HitEntity;
            if (activePlayersAdmin.Contains(player.userID))
            {
                RemoveEntityAdmin(player, entity);
                return true;
            }
            if (activePlayersAll.Contains(player.userID))
            {
                TryRemove(player, info.HitEntity);
                RemoveEntityAll(player, entity, pos);
                return true;
            }
            if (info == null) return null;
            if (entity == null) return null;
            if (entity.IsDestroyed) return false;
            if (entity.OwnerID == 0) return false;
            if ((!(entity is DecayEntity) && !(entity is Signage)) && !entity.ShortPrefabName.Contains("shelves") && !entity.ShortPrefabName.Contains("ladder") && !entity.ShortPrefabName.Contains("quarry")) return null;
            if (!entity.OwnerID.IsSteamId()) return null;
            var ret = Interface.Call("CanRemove", player, entity);
            if (ret is string)
            {
                SendReply(player, (string)ret);
                return null;
            }

            if (ret is bool && (bool)ret)
            {
                RemoveEntity(player, entity);
                return true;
            }
            if (useNoEscape)
            {
                if (plugins.Exists("NoEscape"))
                {
                    var time = (double)NoEscape.Call("ApiGetTime", player.userID);
                    if (time > 0)
                    {
                        SendReply(player, string.Format(Messages["raidremove"], TimeToString(time)));
                        return null;
                    }
                }
            }
            var privilege = player.GetBuildingPrivilege(player.WorldSpaceBounds());
            //Удаление по шкафу
            if (cupboardRemove)
            {
                if (privilege != null && player.IsBuildingAuthed())
                {
                    RemoveEntity(player, entity);
                    return true;
                }
            }
            //Удаление без авторизации в шкафу
            if (privilege != null && !player.IsBuildingAuthed())
            {
                //Свои постройки
                if (selfRemove && entity.OwnerID == player.userID)
                {
                    RemoveEntity(player, entity);
                    return true;
                }
                //Друзья
                if (friendRemove)
                {
                    if (removeFriends)
                    {
                        if (IsFriends(entity.OwnerID, player.userID))
                        {
                            RemoveEntity(player, entity);
                            return true;
                        }
                    }
                }
                //Клан
                if (clanRemove)
                {
                    if (removeClans)
                    {
                        if (IsClanMember(entity.OwnerID, player.userID))
                        {
                            RemoveEntity(player, entity);
                            return true;
                        }
                    }
                }

                SendReply(player, "Что бы удалять постройки, вы должны быть авторизированы в шкафу!");
                return false;
            }

            //Проверка на owner
            if (entity.OwnerID != player.userID)
            {
                if (removeFriends)
                {
                    if (IsFriends(entity.OwnerID, player.userID))
                    {
                        RemoveEntity(player, entity);
                        return true;
                    }
                }
                if (removeClans)
                {
                    if (IsClanMember(entity.OwnerID, player.userID))
                    {
                            RemoveEntity(player, entity);
                            return true;
                    }
                }
                
                SendReply(player, "Вы не имеете права удалять чужие постройки!");
                return false;
            }
            RemoveEntity(player, entity);
            return true;
        }

        public string TimeToString(double time)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(time);
            int hours = timeSpan.Hours;
            int minutes = timeSpan.Minutes;
            int seconds = timeSpan.Seconds;
            int num = Mathf.FloorToInt((float)timeSpan.TotalDays);
            string str1 = "";
            if (num > 0)
                str1 += string.Format("{0} дн.", (object)num);
            if (hours > 0)
                str1 += string.Format("{0} ч.", (object)hours);
            if (minutes > 0)
                str1 += string.Format("{0} мин.", (object)minutes);
            string str2;
            if (seconds > 0)
                str2 = str1 + string.Format("{0} сек.", (object)seconds);
            else
                str2 = str1.TrimEnd(' ');
            return str2;
        }
        #endregion

        #region CORE
        void GradeTimerHandler()
        {
            foreach (var player in timers.Keys.ToList())
            {
                var seconds = --timers[player];
                if (seconds <= 0)
                {
                    timers.Remove(player);
                    DeactivateRemove(player.userID);
                    DestroyUI(player);
                    continue;
                }
                if (activePlayersAdmin.Contains(player.userID))
                {
                    DrawUIAdmin(player, seconds);
                    continue;
                }
                if (activePlayersAll.Contains(player.userID))
                {
                    DrawUIAll(player, seconds);
                    continue;
                }
                DrawUI(player, seconds);
            }
        }

        void RemoveEntity(BasePlayer player, BaseEntity entity)
        {
            Refund(player, entity);
            entity.Kill();
            UpdateTimer(player);
        }
        void RemoveEntityAdmin(BasePlayer player, BaseEntity entity)
        {
            entity.Kill();
            UpdateTimerAdmin(player);
        }
        void RemoveEntityAll(BasePlayer player, BaseEntity entity, Vector3 pos)
        {
            removeFrom.Add(pos);
            DelayRemoveAll();
            UpdateTimerAll(player);
        }
        Dictionary<uint, Dictionary<ItemDefinition, int>> refundItems =
            new Dictionary<uint, Dictionary<ItemDefinition, int>>();

        void Refund(BasePlayer player, BaseEntity entity)
        {
            if (entity is BuildingBlock)
            {
                BuildingBlock buildingblock = entity as BuildingBlock;
                if (buildingblock.blockDefinition == null) return;

                int buildingblockGrade = (int)buildingblock.grade;
                if (buildingblock.blockDefinition.grades[buildingblockGrade] != null)
                {
                    float refundRate = buildingblock.healthFraction * refundPercent;
                    List<ItemAmount> currentCost = buildingblock.blockDefinition.grades[buildingblockGrade].costToBuild as List<ItemAmount>;
                    foreach (ItemAmount ia in currentCost)
                    {
                        int amount = (int)(ia.amount * refundRate);

                        if (amount <= 0 || amount > ia.amount || amount >= int.MaxValue)
                            amount = 1;
                        player.inventory.GiveItem(ItemManager.CreateByItemID(ia.itemid, amount));
                        player.Command("note.inv", ia.itemid, amount); // just notify

                    }

                }
            }

            StorageContainer storage = entity as StorageContainer;
            if (storage)
            {
                for (int i = storage.inventory.itemList.Count - 1; i >= 0; i--)
                {
                    var item = storage.inventory.itemList[i];
                    if (item == null) continue;
                    item.amount = (int)(item.amount * refundStoragePercent);
                    float single = 20f;
                    Vector3 vector32 = Quaternion.Euler(UnityEngine.Random.Range(-single * 0.1f, single * 0.1f), UnityEngine.Random.Range(-single * 0.1f, single * 0.1f), UnityEngine.Random.Range(-single * 0.1f, single * 0.1f)) * Vector3.up;
                    BaseEntity baseEntity = item.Drop(storage.transform.position + (Vector3.up * 0f), vector32 * UnityEngine.Random.Range(5f, 10f), UnityEngine.Random.rotation);
                    baseEntity.SetAngularVelocity(UnityEngine.Random.rotation.eulerAngles * 5f);
                }
            }
            if (deployedToItem.ContainsKey(entity.gameObject.name))
            {
                ItemDefinition def = ItemManager.FindItemDefinition(deployedToItem[entity.gameObject.name]);

                foreach (var ingredient in def.Blueprint.ingredients)
                {
                    var reply = 0;
                    var amountOfIngridient = ingredient.amount;
                    var amount = Mathf.Floor(amountOfIngridient * 1);

                    if (amount <= 0 || amount > amountOfIngridient || amount >= int.MaxValue)
                        amount = 1;

                    if (!refundItemsGive)
                    {
                        var ret = ItemManager.Create(ingredient.itemDef, (int)amount);
                        player.GiveItem(ret);
                        player.Command("note.inv", ret, amount);
                    }
                    else
                    {
                        GiveAndShowItem(player, deployedToItem[entity.PrefabName], 1);
                        return;
                    }

                }
            }

        }

        void GiveAndShowItem(BasePlayer player, int item, int amount)
        {
            player.inventory.GiveItem(ItemManager.CreateByItemID(item, amount), null);
            player.Command("note.inv", new object[] { item, amount });
        }
        void InitRefundItems()
        {
            foreach (var item in ItemManager.itemList)
            {
                var deployable = item.GetComponent<ItemModDeployable>();
                if (deployable != null)
                {
                    if (item.Blueprint == null || deployable.entityPrefab == null) continue;
                    refundItems.Add(deployable.entityPrefab.resourceID, item.Blueprint.ingredients.ToDictionary(p => p.itemDef, p => (Mathf.CeilToInt(p.amount * refundPercent))));
                }
            }
        }

        #endregion

        #region UI

        void DrawUI(BasePlayer player, int seconds)
        {
            DestroyUI(player);
            CuiHelper.AddUi(player,
                GUI.Replace("{1}", seconds.ToString())
                   .Replace("{PanelColor}", PanelColor.ToString())
                   .Replace("{PanelAnchorMin}", PanelAnchorMin.ToString())
                   .Replace("{PanelAnchorMax}", PanelAnchorMax.ToString())
                   .Replace("{TextFontSize}", TextFontSize.ToString())
                   .Replace("{TextСolor}", TextСolor.ToString())
                   .Replace("{TextAnchorMin}", TextAnchorMin.ToString())
                   .Replace("{TextAnchorMax}", TextAnchorMax.ToString()));
        }

        void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "autograde.panel");
            CuiHelper.DestroyUi(player, "autogradetext");
        }

        private string GUI = @"[{""name"": ""autograde.panel"",""parent"": ""Hud"",""components"": [{""type"": ""UnityEngine.UI.Image"",""color"": ""{PanelColor}""},{""type"": ""RectTransform"",""anchormin"": ""{PanelAnchorMin}"",""anchormax"": ""{PanelAnchorMax}""}]}, {""name"": ""autogradetext"",""parent"": ""autograde.panel"",""components"": [{""type"": ""UnityEngine.UI.Text"",""text"": ""Режим удаления выключится через <color=#FF8C00>{1} сек.</color>"",""font"": ""Robotocondensed-Regular.ttf"",""fontSize"": ""{TextFontSize}"",""align"": ""MiddleCenter""}, {""type"": ""UnityEngine.UI.Outline"",""color"": ""{TextСolor}"",""distance"": ""0.1 -0.1""}, {""type"": ""RectTransform"",""anchormin"": ""{TextAnchorMin}"",""anchormax"": ""{TextAnchorMax}""}]}]";

        void DrawUIAdmin(BasePlayer player, int seconds)
        {
            DestroyUIAdmin(player);
            CuiHelper.AddUi(player,
                GUIAdmin.Replace("{1}", seconds.ToString())
                   .Replace("{PanelColor}", PanelColor.ToString())
                   .Replace("{PanelAnchorMin}", PanelAnchorMin.ToString())
                   .Replace("{PanelAnchorMax}", PanelAnchorMax.ToString())
                   .Replace("{TextFontSize}", TextFontSize.ToString())
                   .Replace("{TextСolor}", TextСolor.ToString())
                   .Replace("{TextAnchorMin}", TextAnchorMin.ToString())
                   .Replace("{TextAnchorMax}", TextAnchorMax.ToString()));
        }

        void DestroyUIAdmin(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "autograde.panel");
            CuiHelper.DestroyUi(player, "autogradetext");
        }

        private string GUIAdmin = @"[{""name"": ""autograde.panel"",""parent"": ""Hud"",""components"": [{""type"": ""UnityEngine.UI.Image"",""color"": ""{PanelColor}""},{""type"": ""RectTransform"",""anchormin"": ""{PanelAnchorMin}"",""anchormax"": ""{PanelAnchorMax}""}]}, {""name"": ""autogradetext"",""parent"": ""autograde.panel"",""components"": [{""type"": ""UnityEngine.UI.Text"",""text"": ""<color=#FF6347>[ADMIN]</color> Режим удаления выключится через <color=#FF8C00>{1} сек.</color>"",""font"": ""Robotocondensed-Regular.ttf"",""fontSize"": ""{TextFontSize}"",""align"": ""MiddleCenter""}, {""type"": ""UnityEngine.UI.Outline"",""color"": ""{TextСolor}"",""distance"": ""0.1 -0.1""}, {""type"": ""RectTransform"",""anchormin"": ""{TextAnchorMin}"",""anchormax"": ""{TextAnchorMax}""}]}]";

        void DrawUIAll(BasePlayer player, int seconds)
        {
            DestroyUIAll(player);
            CuiHelper.AddUi(player,
                GUIAll.Replace("{1}", seconds.ToString())
                   .Replace("{PanelColor}", PanelColor.ToString())
                   .Replace("{PanelAnchorMin}", PanelAnchorMin.ToString())
                   .Replace("{PanelAnchorMax}", PanelAnchorMax.ToString())
                   .Replace("{TextFontSize}", TextFontSize.ToString())
                   .Replace("{TextСolor}", TextСolor.ToString())
                   .Replace("{TextAnchorMin}", TextAnchorMin.ToString())
                   .Replace("{TextAnchorMax}", TextAnchorMax.ToString()));
        }

        void DestroyUIAll(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "autograde.panel");
            CuiHelper.DestroyUi(player, "autogradetext");
        }

        private string GUIAll = @"[{""name"": ""autograde.panel"",""parent"": ""Hud"",""components"": [{""type"": ""UnityEngine.UI.Image"",""color"": ""{PanelColor}""},{""type"": ""RectTransform"",""anchormin"": ""{PanelAnchorMin}"",""anchormax"": ""{PanelAnchorMax}""}]}, {""name"": ""autogradetext"",""parent"": ""autograde.panel"",""components"": [{""type"": ""UnityEngine.UI.Text"",""text"": ""<color=#FF6347>[ALL]</color> Режим удаления выключится через <color=#FF8C00>{1} сек.</color>"",""font"": ""Robotocondensed-Regular.ttf"",""fontSize"": ""{TextFontSize}"",""align"": ""MiddleCenter""}, {""type"": ""UnityEngine.UI.Outline"",""color"": ""{TextСolor}"",""distance"": ""0.1 -0.1""}, {""type"": ""RectTransform"",""anchormin"": ""{TextAnchorMin}"",""anchormax"": ""{TextAnchorMax}""}]}]";

        #endregion

        #region API

        void ActivateRemove(ulong userId)
        {
            if (!activePlayers.Contains(userId))
            {
                activePlayers.Add(userId);
            }
        }

        void DeactivateRemove(ulong userId)
        {
            if (activePlayers.Contains(userId))
            {
                activePlayers.Remove(userId);
            }
        }

        void ActivateRemoveAdmin(ulong userId)
        {
            if (!activePlayersAdmin.Contains(userId))
            {
                activePlayersAdmin.Add(userId);
            }
        }

        void DeactivateRemoveAdmin(ulong userId)
        {
            if (activePlayersAdmin.Contains(userId))
            {
                activePlayersAdmin.Remove(userId);
            }
        }

        void ActivateRemoveAll(ulong userId)
        {
            if (!activePlayersAll.Contains(userId))
            {
                activePlayersAll.Add(userId);
            }
        }

        void DeactivateRemoveAll(ulong userId)
        {
            if (activePlayersAll.Contains(userId))
            {
                activePlayersAll.Remove(userId);
            }
        }

        void UpdateTimer(BasePlayer player)
        {
            timers[player] = resetTime;
            DrawUI(player, timers[player]);
        }
        void UpdateTimerAdmin(BasePlayer player)
        {
            timers[player] = resetTime;
            DrawUIAdmin(player, timers[player]);
        }
        void UpdateTimerAll(BasePlayer player)
        {
            timers[player] = resetTime;
            DrawUIAll(player, timers[player]);
        }
        #endregion

        #region LOCALIZATION

        Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            {"raidremove", "Ремув во время рейда запрещён!\nОсталось {0}" }
        };

        #endregion

        #region Permission Service
        public static class PermissionService
        {
            public static Permission permission = Interface.GetMod().GetLibrary<Permission>();

            public static bool HasPermission(BasePlayer player, string permissionName)
            {
                if (player == null || string.IsNullOrEmpty(permissionName))
                    return false;

                var uid = player.UserIDString;
                if (permission.UserHasPermission(uid, permissionName))
                    return true;
                return false;
            }

            public static void RegisterPermissions(Plugin owner, List<string> permissions)
            {
                if (owner == null) throw new ArgumentNullException("owner");
                if (permissions == null) throw new ArgumentNullException("commands");

                foreach (var permissionName in permissions.Where(permissionName => !permission.PermissionExists(permissionName)))
                {
                    permission.RegisterPermission(permissionName, owner);
                }
            }
        }
        #endregion
    }
}
                               