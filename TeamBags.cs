using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("TeamBags", "https://topplugin.ru/", "1.0.0")] 
    [Description("Adds the ability to spawn on teammates. | Добавляет возможность спавниться на сокомандниках.")]
    public class TeamBags : RustPlugin
    {
        #region CFG

        private ConfigData cfg { get; set; }

        private class ConfigData
        {
            [JsonProperty("Cooldown | Перезарядка ")]
            public float Cooldown = 125f;

            [JsonProperty("Permission | Права ")] public string perm = "teambags.iswork";

            public static ConfigData GetNewConf()
            {
                var newConfig = new ConfigData();
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
            base.LoadConfig();
            try
            {
                cfg = Config.ReadObject<ConfigData>();
            }
            catch
            {
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        #endregion

        #region Commands

        [ConsoleCommand("wipe_teambags")]
        void ConsoleCommnad(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null) return;
            ClearData();
        }

        void OnNewSave() => ClearData();

        #endregion

        #region MySmallRegion <3

        void ClearData()
        {
            _bags.Clear();
            PrintWarning("Wipe data | Вайп даты прошел успешно!");
            Interface.Oxide.ReloadPlugin(Title);
        }

        class TeamBag
        {
            public Dictionary<ulong, float> _team = new Dictionary<ulong, float>();
            public List<SleepingBag> _bags = new List<SleepingBag>();
        }

        Dictionary<ulong, TeamBag> _bags = new Dictionary<ulong, TeamBag>();

        void LoadBags(BasePlayer teamPlayer, ulong OwnerId, float unlockTime)
        {
            TeamBag team;
            if (!_bags.TryGetValue(OwnerId, out team))
            {
                _bags.Add(OwnerId, new TeamBag());
                team = _bags[OwnerId];
            }

            if (team._bags.Exists(p => p.OwnerID == teamPlayer.userID))
            {
                NextTick(() =>
                {
                    if (teamPlayer.IsDead() || !teamPlayer.IsConnected)
                        RemoveBag(OwnerId, teamPlayer.userID);
                });
                return;
            }
            if (teamPlayer.IsDead() || !teamPlayer.IsConnected) return;
            GameObject go = new GameObject();
            SleepingBag f = go.AddComponent<SleepingBag>();
            f.niceName = teamPlayer.displayName;
            f.secondsBetweenReuses = cfg.Cooldown; 
            float a = UnityEngine.Time.realtimeSinceStartup;
            float findCd;
            if (!team._team.TryGetValue(teamPlayer.userID, out findCd))
            {
                team._team.Add(teamPlayer.userID, unlockTime +a);
                f.unlockTime = Mathf.Max(a, a + unlockTime);
            }
            else f.unlockTime = Mathf.Max(a, a + findCd - a);

            f.OwnerID = teamPlayer.userID;
            f.deployerUserID = OwnerId;
            f.net = Network.Net.sv.CreateNetworkable();
            f.RespawnType = ProtoBuf.RespawnInformation.SpawnOptions.RespawnType.Bed;
            SleepingBag.sleepingBags.Add(f);
            f.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            team._bags.Add(f);
            NextTick(() =>
            {
                if (f == null) return;
                if (teamPlayer.IsDead() || !teamPlayer.IsConnected)
                {
                    RemoveBag(OwnerId, teamPlayer.userID);
                }
            });
        }

        #endregion

        #region OxideHooks

        void Init() => permission.RegisterPermission(cfg.perm, this);

        private void OnServerInitialized() =>
            _bags = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, TeamBag>>("TeamBags");

        void OnUserPermissionRevoked(string id, string perm)
        {
            if (perm == cfg.perm)
                RemoveBags(ulong.Parse(id));
        }

        void OnPlayerRespawn(BasePlayer player, SleepingBag bag)
        {
            TeamBag f;
            if (!_bags.TryGetValue(player.userID, out f)) return;
            var find = f._bags.Find(p => p == bag);
            if (find == null) return;
            BasePlayer.SpawnPoint spawnPoint = ServerMgr.FindSpawnPoint();
            bag.transform.position = spawnPoint.pos;
            var target = BasePlayer.FindByID(find.OwnerID);
            if (target == null || target.IsDead() || !target.IsConnected)
            {
                RemoveBag(player.userID, target.userID);
                return;
            }

            bag.transform.position = target.transform.position;
        }

        void Unload()
        {
            foreach (var data in _bags)
            {
                RemoveBags(data.Key); 
            }
            Interface.Oxide.DataFileSystem.WriteObject("TeamBags", _bags);
        }

        void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null || player.Team == null ||
                !permission.UserHasPermission(player.UserIDString, cfg.perm)) return; 
            foreach (var teamMember in player.Team.members.Where(p => p != player.userID))
            {
                var target = BasePlayer.FindByID(teamMember);
                if (target == null) continue;
                LoadBags(target, player.userID, cfg.Cooldown);
            } 
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            if (player == null) return;
            RemoveBags(player.userID);
        }

        #endregion

        #region TeamHooks

        void OnTeamDisbanded(RelationshipManager.PlayerTeam team)
        {
            foreach (var teamMember in team.members)
                RemoveBags(teamMember);
        }

        void OnTeamKick(RelationshipManager.PlayerTeam team, BasePlayer player, ulong target)
        {
            foreach (var teamMember in team.members)
                RemoveBag(teamMember, target);
            RemoveBags(target);
        }

        void OnTeamLeave(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            foreach (var teamMember in team.members)
                RemoveBag(teamMember, player.userID);
            RemoveBags(player.userID);
        }

        #endregion

        #region RemovesMettods

        void RemoveBags(ulong OwnerId)
        { 
            TeamBag team;
            if (!_bags.TryGetValue(OwnerId, out team)) return;
            team._bags.ToList().ForEach(p =>
            {
                if (team._team.ContainsKey(p.OwnerID))
                    team._team[p.OwnerID] = p.unlockTime;
                else team._team.Add(p.OwnerID, p.unlockTime);
                p.Kill();
                SleepingBag.sleepingBags.Remove(p);
            });
            team._bags.Clear();
        }

        void RemoveBag(ulong OwnerId, ulong teamId)
        {
            TeamBag team;
            if (!_bags.TryGetValue(OwnerId, out team)) return;
            var ff = team._bags.Find(p => p.OwnerID == teamId);
            if (ff == null) return; 
            if (team._team.ContainsKey(ff.OwnerID))
                team._team[ff.OwnerID] = ff.unlockTime;
            else team._team.Add(ff.OwnerID, ff.unlockTime);
            ff.Kill();
            team._bags.Remove(ff);
        }

        #endregion
    }
}
