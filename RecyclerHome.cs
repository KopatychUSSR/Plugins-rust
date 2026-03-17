using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("RecyclerHome", "https://topplugin.ru/","1.0.3")]
    [Description("Глобальный")] 
    public class RecyclerHome : RustPlugin
    {
        #region Cfg

        private static ConfigData cfg { get; set; }

        private class ConfigData
        {
            [JsonProperty("Название переработчика")] public string RecyclerName = "Домашний переработчик";
            [JsonProperty("Пермишен для крафта")] public string Perm = "recyclerhome.craft";
            [JsonProperty("Пермишен для выдачи")] public string Perm2 = "recyclerhome.give";
            [JsonProperty("Разрешить поднимать чужой переработчик?")] public bool canupnotowner = true;
            [JsonProperty("Разрешить подбирать переработчик в зоне запрета строительства?")] public bool canupbuildingblock = true;
            [JsonProperty("Разрешить ставить переработчик на землю?")] public bool ter = true;
            [JsonProperty("Предметы для крафта")] public Dictionary<string, int> needCraft = new Dictionary<string, int>();
            public static ConfigData GetNewConf()
            {
                var newConfig = new ConfigData
                {
                    needCraft = new Dictionary<string, int>()
                    {
                        ["wood"] = 100,
                        ["stones"] = 500,
                    }
                };
                return newConfig;
            }
        }
        protected override void LoadDefaultConfig()
        {
            cfg = ConfigData.GetNewConf();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(cfg);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig(); try { cfg = Config.ReadObject<ConfigData>(); }catch { LoadDefaultConfig(); } NextTick(SaveConfig);
        }
        private void OnServerInitialized()
        {
            ins = this;
            if(!permission.PermissionExists(cfg.Perm))
                permission.RegisterPermission(cfg.Perm, this);
            if(!permission.PermissionExists(cfg.Perm2))
                permission.RegisterPermission(cfg.Perm2, this);
        }
        #endregion
        [ConsoleCommand("giverecycler")]
        private void GiveRecycler(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, cfg.Perm2))
            {
                SendReply(player, "Нет прав!");
                return;
            } 
            if (arg?.Args == null)
            {
                if (player != null)
                    SendReply(player, "Вы делаете что-то не так!");
                else
                    Puts("Вы делаете что-то не так!");
                return;
            }
            
            
            var targetPlayer = BasePlayer.Find(arg.Args[0]);
            if (targetPlayer == null)
            {
                if (player != null)
                    SendReply(player, "Игрок не найден!");
                else
                    Puts("Игрок не найден!");
                return;
            }
            var item = ItemManager.CreateByName("box.repair.bench", 1,1797067639);
            item.name = cfg.RecyclerName;
            if (!targetPlayer.inventory.GiveItem(item))
                item.Drop(targetPlayer.inventory.containerMain.dropPosition, targetPlayer.inventory.containerMain.dropVelocity);
        }

        private object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            if (planner == null || prefab == null || target.player == null) return null;
            if (!prefab.fullName.Contains("repairbench_deployed") || planner.skinID != 1797067639) return null;
            if (cfg.ter) return null;
            RaycastHit rHit;
            if (Physics.Raycast(new Vector3(target.position.x,target.position.y+1, target.position.z), Vector3.down, out rHit, 2f, LayerMask.GetMask(new string[] {"Construction"})) && rHit.GetEntity() != null) return null;
            SendReply(target.player, "Нельзя ставить на землю!");
            return false;
        }

        private void OnEntitySpawned(BaseEntity entity)
        {
            NextTick(() =>
            {
                if (entity.ShortPrefabName == "repairbench_deployed" && entity.skinID == 1797067639)
                {
                    RaycastHit rHit;
                    Recycler gameObject = GameManager.server.CreateEntity("assets/bundled/prefabs/static/recycler_static.prefab", entity.transform.position, entity.transform.rotation) as Recycler;
                    if (gameObject != null)
                    {
                        gameObject.OwnerID = entity.OwnerID;

                        entity.Kill();
                        if (Physics.Raycast(
                            new Vector3(entity.transform.position.x, entity.transform.position.y + 1,
                                entity.transform.position.z), Vector3.down, out rHit, 2f,
                            LayerMask.GetMask(new string[] {"Construction"})) && rHit.GetEntity() != null)
                        { 
                            gameObject.gameObject.AddComponent<CheckZemlya>();
                        }
                        gameObject.Spawn();
                    }
                }          
            });
            
        }

        public static RecyclerHome ins;
        class CheckZemlya : MonoBehaviour
        {
            private Recycler rec;

            private void Awake()
            {
                rec = GetComponent<Recycler>();
                InvokeRepeating("ChecKGround", 0.5f, 0.5f);
            }

            private void OnDestroy()
            {
                Destroy(this);
            }

            void ChecKGround()
            {
                RaycastHit rHit; 
                if (!Physics.Raycast(new Vector3(rec.transform.position.x, rec.transform.position.y + 1,rec.transform.position.z), Vector3.down, out rHit, 2f,LayerMask.GetMask(new string[] {"Construction"})))
                {
                    rec.Kill();
                    Destroy(this);
                }
            }
        }

        [ChatCommand("craftrecycler")]
        private void CraftRecycler(BasePlayer player)
        {
            if(!permission.UserHasPermission(player.UserIDString, cfg.Perm))
            {
                SendReply(player, "Нет прав!");
                return;
            }
            var text = "<size=18><color=green>НЕДОСТАТОЧНО РЕСУРСОВ</color></size>\n";
            var count = 0;
            foreach (var sp in cfg.needCraft)
            {
                var findItem = player.inventory.AllItems().ToList().Find(p => p.info.shortname == sp.Key);
                if (findItem?.amount >= sp.Value)
                    count++;
                else
                    if(findItem == null)
                        text += $"Недостаточно {sp.Key}, еще нужно {sp.Value}\n";
                    else
                        text += $"Недостаточно {sp.Key}, еще нужно {sp.Value-findItem.amount}\n";
            }
            if (count == cfg.needCraft.Count)
            {
                foreach (var sp in cfg.needCraft)
                    player.inventory.Take(null, ItemManager.FindItemDefinition(sp.Key).itemid, sp.Value);
                var item = ItemManager.CreateByName("box.repair.bench", 1,1797067639);
                item.name = cfg.RecyclerName;
                if (!player.inventory.GiveItem(item))
                {
                    SendReply(player, "Инвентарь был полон переработчик выпал на земелю!");
                    item.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity);
                }
                else
                    SendReply(player, "Вы скрафтили переработчик!"); 
            }
            else
                SendReply(player, text);
        }
        object OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (info?.HitEntity?.OwnerID != 0 && info.HitEntity.ShortPrefabName == "recycler_static")
            {
                if (player.IsBuildingBlocked() && !cfg.canupbuildingblock)                 
                {
                    SendReply(player, "Вы в зоне запрета строительства!");
                    return null;
                }
                if (info.HitEntity.OwnerID != player.userID && !cfg.canupnotowner)
                {
                    SendReply(player, "Вы не владелец переработчика");
                    return null;
                }
                info.HitEntity.Kill();
                var item = ItemManager.CreateByName("box.repair.bench", 1,1797067639);
                item.name = cfg.RecyclerName;
                if (!player.inventory.GiveItem(item))
                {
                    SendReply(player, "Инвентарь был полон переработчик выпал на земелю!");
                    item.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity);
                }
                else
                    SendReply(player, "Вы подобрали переработчик!");
            }
            return null;
        }
    }
}
