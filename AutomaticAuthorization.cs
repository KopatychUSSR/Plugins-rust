using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust;
using ProtoBuf;

namespace Oxide.Plugins
{
    [Info("Automatic Authorization", "k1lly0u/Arainrr", "1.1.12", ResourceId = 2063)]
    public class AutomaticAuthorization : RustPlugin
    {
        #region Fields

        [PluginReference] private readonly Plugin Clans, Friends;
        private const string PERMISSION_USE = "automaticauthorization.use";
        private readonly Dictionary<ulong, EntityEntry> playerEntites = new Dictionary<ulong, EntityEntry>();

        public class EntityEntry
        {
            public HashSet<AutoTurret> autoTurrets = new HashSet<AutoTurret>();
            public HashSet<BuildingPrivlidge> buildingPrivlidges = new HashSet<BuildingPrivlidge>();
        }

        #endregion Fields

        #region Oxide Hooks

        private void Init()
        {
            LoadData();
            UpdateData();
            Unsubscribe(nameof(OnEntitySpawned));
            permission.RegisterPermission(PERMISSION_USE, this);
            cmd.AddChatCommand(configData.chatS.command, this, nameof(CmdAutoAuth));
        }

        private void OnServerInitialized()
        {
            if (!configData.teamShareS.enabled)
            {
                Unsubscribe(nameof(OnTeamLeave));
                Unsubscribe(nameof(OnTeamKick));
                Unsubscribe(nameof(OnTeamAcceptInvite));
            }
            if (!configData.friendsShareS.enabled)
            {
                Unsubscribe(nameof(OnFriendAdded));
                Unsubscribe(nameof(OnFriendRemoved));
            }
            if (!configData.clanShareS.enabled)
            {
                Unsubscribe(nameof(OnClanUpdate));
                Unsubscribe(nameof(OnClanDestroy));
            }
            Subscribe(nameof(OnEntitySpawned));
            foreach (var entity in BaseNetworkable.serverEntities)
                CheckEntity(entity as BaseEntity);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;
            if (permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
                CreateShareData(player.userID);
        }

        private void OnServerSave() => timer.Once(UnityEngine.Random.Range(0f, 60f), () => SaveData());

        private void Unload() => SaveData();

        private void OnEntitySpawned(BaseEntity entity) => CheckEntity(entity, true);

        private void CheckEntity(BaseEntity entity, bool justCreated = false)
        {
            if (entity == null || entity.OwnerID == 0) return;
            if (entity is BuildingPrivlidge)
            {
                var buildingPrivlidge = entity as BuildingPrivlidge;
                if (playerEntites.ContainsKey(entity.OwnerID)) playerEntites[entity.OwnerID].buildingPrivlidges.Add(buildingPrivlidge);
                else playerEntites.Add(entity.OwnerID, new EntityEntry { buildingPrivlidges = new HashSet<BuildingPrivlidge> { buildingPrivlidge } });
                if (justCreated && permission.UserHasPermission(entity.OwnerID.ToString(), PERMISSION_USE))
                    AuthToCupboard(new HashSet<BuildingPrivlidge> { buildingPrivlidge }, entity.OwnerID, true);
                return;
            }
            if (entity is AutoTurret)
            {
                var autoTurret = entity as AutoTurret;
                if (playerEntites.ContainsKey(entity.OwnerID)) playerEntites[entity.OwnerID].autoTurrets.Add(autoTurret);
                else playerEntites.Add(entity.OwnerID, new EntityEntry { autoTurrets = new HashSet<AutoTurret> { autoTurret } });
                if (justCreated && permission.UserHasPermission(entity.OwnerID.ToString(), PERMISSION_USE))
                    AuthToTurret(new HashSet<AutoTurret> { autoTurret }, entity.OwnerID, true);
            }
        }

        private void OnEntityKill(BaseEntity entity)
        {
            if (entity == null || entity.OwnerID == 0) return;
            if (entity is BuildingPrivlidge)
            {
                var buildingPrivlidge = entity as BuildingPrivlidge;
                foreach (var entry in playerEntites)
                {
                    if (entry.Value.buildingPrivlidges.Contains(buildingPrivlidge))
                    {
                        entry.Value.buildingPrivlidges.Remove(buildingPrivlidge);
                        return;
                    }
                }
                return;
            }
            if (entity is AutoTurret)
            {
                var autoTurret = entity as AutoTurret;
                foreach (var entry in playerEntites)
                {
                    if (entry.Value.autoTurrets.Contains(autoTurret))
                    {
                        entry.Value.autoTurrets.Remove(autoTurret);
                        return;
                    }
                }
            }
        }

        private object CanUseLockedEntity(BasePlayer player, BaseLock baseLock)
        {
            var parentEntity = baseLock?.GetParentEntity();
            if (player == null || parentEntity == null || parentEntity.OwnerID == 0 || !baseLock.IsLocked()) return null;
            if (!permission.UserHasPermission(parentEntity.OwnerID.ToString(), PERMISSION_USE)) return null;
            var shareData = GetShareData(parentEntity.OwnerID);
            if (shareData.friendsShareEntry.enabled && HasFriend(parentEntity.OwnerID, player.userID))
            {
                if (baseLock is KeyLock && shareData.friendsShareEntry.shareKeyLock && CanUnlockEntity(parentEntity, configData.friendsShareS.keyLockS))
                    return true;
                if (baseLock is CodeLock && shareData.friendsShareEntry.shareCodeLock && CanUnlockEntity(parentEntity, configData.friendsShareS.codeLockS))
                    return SendUnlockedEffect(baseLock as CodeLock);
            }
            if (shareData.clanShareEntry.enabled && SameClan(parentEntity.OwnerID, player.userID))
            {
                if (baseLock is KeyLock && shareData.clanShareEntry.shareKeyLock && CanUnlockEntity(parentEntity, configData.clanShareS.keyLockS))
                    return true;
                if (baseLock is CodeLock && shareData.clanShareEntry.shareCodeLock && CanUnlockEntity(parentEntity, configData.clanShareS.codeLockS))
                    return SendUnlockedEffect(baseLock as CodeLock);
            }
            if (shareData.teamShareEntry.enabled && SameTeam(parentEntity.OwnerID, player.userID))
            {
                if (baseLock is KeyLock && shareData.teamShareEntry.shareKeyLock && CanUnlockEntity(parentEntity, configData.teamShareS.keyLockS))
                    return true;
                if (baseLock is CodeLock && shareData.teamShareEntry.shareCodeLock && CanUnlockEntity(parentEntity, configData.teamShareS.codeLockS))
                    return SendUnlockedEffect(baseLock as CodeLock);
            }
            return null;
        }

        private bool CanUnlockEntity(BaseEntity parentEntity, ConfigData.LockSettings lockConfigSettings)
        {
            if (parentEntity is Door)
            {
                if (lockConfigSettings.shareDoor)
                    return true;
            }
            else if (parentEntity is BoxStorage)
            {
                if (lockConfigSettings.shareBox)
                    return true;
            }
            else if (lockConfigSettings.shareOtherEntity)
                return true;
            return false;
        }

        private bool SendUnlockedEffect(CodeLock codeLock)
        {
            Effect.server.Run(codeLock.effectUnlocked.resourcePath, codeLock.transform.position);
            return true;
        }

        #endregion Oxide Hooks

        #region Functions

        private enum AutoAuthType
        {
            All,
            Turret,
            Cupboard,
        }

        private void UpdateAuthList(ulong playerID, AutoAuthType autoAuthType)
        {
            if (!permission.UserHasPermission(playerID.ToString(), PERMISSION_USE)) return;
            EntityEntry entityEntry;
            if (!playerEntites.TryGetValue(playerID, out entityEntry)) return;
            switch (autoAuthType)
            {
                case AutoAuthType.All:
                    AuthToCupboard(entityEntry.buildingPrivlidges, playerID);
                    AuthToTurret(entityEntry.autoTurrets, playerID);
                    return;

                case AutoAuthType.Turret:
                    AuthToTurret(entityEntry.autoTurrets, playerID);
                    return;

                case AutoAuthType.Cupboard:
                    AuthToCupboard(entityEntry.buildingPrivlidges, playerID);
                    return;
            }
        }

        private void AuthToCupboard(HashSet<BuildingPrivlidge> buildingPrivlidges, ulong playerID, bool justCreated = false)
        {
            if (buildingPrivlidges.Count <= 0) return;
            List<PlayerNameID> authList = GetPlayerNameIDs(playerID, AutoAuthType.Cupboard);
            foreach (var buildingPrivlidge in buildingPrivlidges)
            {
                if (buildingPrivlidge == null || buildingPrivlidge.IsDestroyed) continue;
                buildingPrivlidge.authorizedPlayers.Clear();
                foreach (var friend in authList) buildingPrivlidge.authorizedPlayers.Add(friend);
                buildingPrivlidge.SendNetworkUpdateImmediate();
            }
            var player = RustCore.FindPlayerById(playerID);
            if (player == null) return;
            if (justCreated && configData.chatS.sendMessage && authList.Count > 1)
                Print(player, Lang("CupboardSuccess", player.UserIDString, authList.Count - 1, buildingPrivlidges.Count));
        }

        private void AuthToTurret(HashSet<AutoTurret> autoTurrets, ulong playerID, bool justCreated = false)
        {
            if (autoTurrets.Count <= 0) return;
            List<PlayerNameID> authList = GetPlayerNameIDs(playerID, AutoAuthType.Turret);
            foreach (var autoTurret in autoTurrets)
            {
                if (autoTurret == null || autoTurret.IsDestroyed) continue;
                bool isOnline = false;
                if (autoTurret.IsOnline())
                {
                    autoTurret.SetIsOnline(false);
                    isOnline = true;
                }
                autoTurret.authorizedPlayers.Clear();
                foreach (var friend in authList) autoTurret.authorizedPlayers.Add(friend);
                if (isOnline) autoTurret.SetIsOnline(true);
                autoTurret.SendNetworkUpdateImmediate();
            }
            var player = RustCore.FindPlayerById(playerID);
            if (player == null) return;
            if (justCreated && configData.chatS.sendMessage && authList.Count > 1)
                Print(player, Lang("TurretSuccess", player.UserIDString, authList.Count - 1, autoTurrets.Count));
        }

        private List<PlayerNameID> GetPlayerNameIDs(ulong playerID, AutoAuthType autoAuthType)
        {
            List<PlayerNameID> playerNameIDs = new List<PlayerNameID>();
            HashSet<ulong> authList = GetAuthList(playerID, autoAuthType);
            playerNameIDs.AddRange(authList.Select(auth => new PlayerNameID { userid = auth, username = RustCore.FindPlayerById(auth)?.displayName ?? string.Empty, ShouldPool = true }));
            return playerNameIDs;
        }

        private HashSet<ulong> GetAuthList(ulong playerID, AutoAuthType autoAuthType)
        {
            var shareData = GetShareData(playerID);
            HashSet<ulong> sharePlayers = new HashSet<ulong> { playerID };
            if (shareData.friendsShareEntry.enabled && (autoAuthType == AutoAuthType.Turret ? shareData.friendsShareEntry.shareTurret : shareData.friendsShareEntry.shareCupboard))
            {
                var friends = GetFriends(playerID);
                foreach (var friend in friends)
                    sharePlayers.Add(friend);
            }
            if (shareData.clanShareEntry.enabled && (autoAuthType == AutoAuthType.Turret ? shareData.clanShareEntry.shareTurret : shareData.clanShareEntry.shareCupboard))
            {
                var clanMembers = GetClanMembers(playerID);
                foreach (var member in clanMembers)
                    sharePlayers.Add(member);
            }
            if (shareData.teamShareEntry.enabled && (autoAuthType == AutoAuthType.Turret ? shareData.teamShareEntry.shareTurret : shareData.teamShareEntry.shareCupboard))
            {
                var teamMembers = GetTeamMembers(playerID);
                foreach (var member in teamMembers)
                    sharePlayers.Add(member);
            }
            return sharePlayers;
        }

        private StoredData.ShareData GetShareData(ulong playerID)
        {
            if (!storedData.playerShareData.ContainsKey(playerID)) CreateShareData(playerID);
            return storedData.playerShareData[playerID];
        }

        private void CreateShareData(ulong playerID)
        {
            if (storedData.playerShareData.ContainsKey(playerID)) return;
            storedData.playerShareData.Add(playerID, new StoredData.ShareData
            {
                friendsShareEntry = new StoredData.ShareDataEntry
                {
                    enabled = configData.friendsShareS.enabled,
                    shareTurret = configData.friendsShareS.shareTurret,
                    shareCupboard = configData.friendsShareS.shareCupboard,
                    shareKeyLock = configData.friendsShareS.keyLockS.enabled,
                    shareCodeLock = configData.friendsShareS.codeLockS.enabled,
                },
                clanShareEntry = new StoredData.ShareDataEntry
                {
                    enabled = configData.clanShareS.enabled,
                    shareTurret = configData.clanShareS.shareTurret,
                    shareCupboard = configData.clanShareS.shareCupboard,
                    shareKeyLock = configData.clanShareS.keyLockS.enabled,
                    shareCodeLock = configData.clanShareS.codeLockS.enabled,
                },
                teamShareEntry = new StoredData.ShareDataEntry
                {
                    enabled = configData.teamShareS.enabled,
                    shareTurret = configData.teamShareS.shareTurret,
                    shareCupboard = configData.teamShareS.shareCupboard,
                    shareKeyLock = configData.teamShareS.keyLockS.enabled,
                    shareCodeLock = configData.teamShareS.codeLockS.enabled,
                }
            });
        }

        private void UpdateData()
        {
            foreach (var entry in storedData.playerShareData)
            {
                if (!configData.friendsShareS.enabled) entry.Value.friendsShareEntry.enabled = false;
                if (!configData.friendsShareS.shareCupboard) entry.Value.friendsShareEntry.shareCupboard = false;
                if (!configData.friendsShareS.shareTurret) entry.Value.friendsShareEntry.shareTurret = false;
                if (!configData.friendsShareS.keyLockS.enabled) entry.Value.friendsShareEntry.shareKeyLock = false;
                if (!configData.friendsShareS.codeLockS.enabled) entry.Value.friendsShareEntry.shareCodeLock = false;

                if (!configData.clanShareS.enabled) entry.Value.clanShareEntry.enabled = false;
                if (!configData.clanShareS.shareCupboard) entry.Value.clanShareEntry.shareCupboard = false;
                if (!configData.clanShareS.shareTurret) entry.Value.clanShareEntry.shareTurret = false;
                if (!configData.clanShareS.keyLockS.enabled) entry.Value.clanShareEntry.shareKeyLock = false;
                if (!configData.clanShareS.codeLockS.enabled) entry.Value.clanShareEntry.shareCodeLock = false;

                if (!configData.teamShareS.enabled) entry.Value.teamShareEntry.enabled = false;
                if (!configData.teamShareS.shareCupboard) entry.Value.teamShareEntry.shareCupboard = false;
                if (!configData.teamShareS.shareTurret) entry.Value.teamShareEntry.shareTurret = false;
                if (!configData.teamShareS.keyLockS.enabled) entry.Value.teamShareEntry.shareKeyLock = false;
                if (!configData.teamShareS.codeLockS.enabled) entry.Value.teamShareEntry.shareCodeLock = false;
            }
            SaveData();
        }

        #region Clans

        private void OnClanDestroy(string clanName) => UpdateClanAuthList(clanName);

        private void OnClanUpdate(string clanName) => UpdateClanAuthList(clanName);

        private void UpdateClanAuthList(string clanName)
        {
            var clanMembers = GetClanMembers(clanName);
            foreach (var member in clanMembers)
                UpdateAuthList(member, AutoAuthType.All);
        }

        private List<ulong> GetClanMembers(ulong playerID)
        {
            if (Clans == null) return new List<ulong>();
            var clanName = Clans.Call("GetClanOf", playerID);
            if (clanName != null && clanName is string)
                return GetClanMembers((string)clanName);
            return new List<ulong>();
        }

        private List<ulong> GetClanMembers(string clanName)
        {
            var clan = Clans.Call("GetClan", clanName);
            if (clan != null && clan is JObject)
            {
                var members = (clan as JObject).GetValue("members");
                if (members != null && members is JArray)
                    return ((JArray)members).Select(x => ulong.Parse(x.ToString())).ToList();
            }
            return new List<ulong>();
        }

        private bool SameClan(ulong playerID, ulong friendID)
        {
            if (Clans == null) return false;
            //Clans
            var isMember = Clans.Call("IsClanMember", playerID.ToString(), friendID.ToString());
            if (isMember != null) return (bool)isMember;
            //Rust:IO Clans
            var playerClan = Clans.Call("GetClanOf", playerID);
            if (playerClan == null) return false;
            var friendClan = Clans.Call("GetClanOf", friendID);
            if (friendClan == null) return false;
            return (string)playerClan == (string)friendClan;
        }

        #endregion Clans

        #region Friends

        private void OnFriendAdded(string playerID, string friendID) => UpdateFriendAuthList(playerID);

        private void OnFriendRemoved(string playerID, string friendID) => UpdateFriendAuthList(playerID);

        private void UpdateFriendAuthList(string playerID) => UpdateAuthList(ulong.Parse(playerID), AutoAuthType.All);

        private List<ulong> GetFriends(ulong playerID)
        {
            if (Friends == null) return new List<ulong>();
            var friends = Friends.Call("GetFriends", playerID);
            if (friends != null && friends is ulong[])
                return (friends as ulong[]).ToList();
            return new List<ulong>();
        }

        private bool HasFriend(ulong playerID, ulong friendID)
        {
            if (Friends == null) return false;
            var hasFriend = Friends.Call("HasFriend", playerID, friendID);
            if (hasFriend != null && (bool)hasFriend) return true;
            return false;
        }

        #endregion Friends

        #region Teams

        private void OnTeamLeave(RelationshipManager.PlayerTeam playerTeam, BasePlayer player)
        {
            NextTick(() =>
            {
                if (playerTeam == null || player == null) return;
                if (!playerTeam.members.Contains(player.userID))
                    UpdateTeamAuthList(playerTeam.members);
            });
        }

        private void OnTeamAcceptInvite(RelationshipManager.PlayerTeam playerTeam, BasePlayer player)
        {
            NextTick(() =>
            {
                if (playerTeam == null || player == null) return;
                if (playerTeam.members.Contains(player.userID))
                    UpdateTeamAuthList(playerTeam.members);
            });
        }

        private void OnTeamKick(RelationshipManager.PlayerTeam playerTeam, BasePlayer player, ulong target)
        {
            NextTick(() =>
            {
                if (playerTeam == null) return;
                if (!playerTeam.members.Contains(target))
                    UpdateTeamAuthList(playerTeam.members);
            });
        }

        private void UpdateTeamAuthList(List<ulong> teamMembers)
        {
            if (teamMembers.Count <= 0) return;
            foreach (var member in teamMembers)
                UpdateAuthList(member, AutoAuthType.All);
        }

        private List<ulong> GetTeamMembers(ulong playerID)
        {
            if (!RelationshipManager.TeamsEnabled()) return new List<ulong>();
            var playerTeam = RelationshipManager.Instance.FindPlayersTeam(playerID);
            if (playerTeam != null) return playerTeam.members;
            return new List<ulong>();
        }

        private bool SameTeam(ulong playerID, ulong friendID)
        {
            if (!RelationshipManager.TeamsEnabled()) return false;
            var playerTeam = RelationshipManager.Instance.FindPlayersTeam(playerID);
            if (playerTeam == null) return false;
            var friendTeam = RelationshipManager.Instance.FindPlayersTeam(friendID);
            if (friendTeam == null) return false;
            return playerTeam == friendTeam;
        }

        #endregion Teams

        #endregion Functions

        #region ChatCommands

        private void CmdAutoAuth(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
            {
                Print(player, Lang("NotAllowed", player.UserIDString));
                return;
            }
            CreateShareData(player.userID);
            if (args == null || args.Length == 0)
            {
                bool flag = false;
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine();
                if (Friends != null && configData.friendsShareS.enabled)
                {
                    flag = true;
                    var shareDataEntry = storedData.playerShareData[player.userID].friendsShareEntry;
                    stringBuilder.AppendLine(Lang("AutoShareFriendsStatus", player.UserIDString));
                    stringBuilder.AppendLine(Lang("AutoShareFriends", player.UserIDString, shareDataEntry.enabled ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                    stringBuilder.AppendLine(Lang("AutoShareFriendsCupboard", player.UserIDString, shareDataEntry.shareCupboard ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                    stringBuilder.AppendLine(Lang("AutoShareFriendsTurret", player.UserIDString, shareDataEntry.shareTurret ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                    stringBuilder.AppendLine(Lang("AutoShareFriendsKeyLock", player.UserIDString, shareDataEntry.shareKeyLock ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                    stringBuilder.AppendLine(Lang("AutoShareFriendsCodeLock", player.UserIDString, shareDataEntry.shareCodeLock ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                }
                if (Clans != null && configData.clanShareS.enabled)
                {
                    flag = true;
                    var shareDataEntry = storedData.playerShareData[player.userID].clanShareEntry;
                    stringBuilder.AppendLine(Lang("AutoShareClansStatus", player.UserIDString));
                    stringBuilder.AppendLine(Lang("AutoShareClans", player.UserIDString, shareDataEntry.enabled ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                    stringBuilder.AppendLine(Lang("AutoShareClansCupboard", player.UserIDString, shareDataEntry.shareCupboard ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                    stringBuilder.AppendLine(Lang("AutoShareClansTurret", player.UserIDString, shareDataEntry.shareTurret ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                    stringBuilder.AppendLine(Lang("AutoShareClansKeyLock", player.UserIDString, shareDataEntry.shareKeyLock ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                    stringBuilder.AppendLine(Lang("AutoShareClansCodeLock", player.UserIDString, shareDataEntry.shareCodeLock ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                }
                if (RelationshipManager.TeamsEnabled() && configData.teamShareS.enabled)
                {
                    flag = true;
                    var shareDataEntry = storedData.playerShareData[player.userID].teamShareEntry;
                    stringBuilder.AppendLine(Lang("AutoShareTeamsStatus", player.UserIDString));
                    stringBuilder.AppendLine(Lang("AutoShareTeams", player.UserIDString, shareDataEntry.enabled ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                    stringBuilder.AppendLine(Lang("AutoShareTeamsCupboard", player.UserIDString, shareDataEntry.shareCupboard ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                    stringBuilder.AppendLine(Lang("AutoShareTeamsTurret", player.UserIDString, shareDataEntry.shareTurret ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                    stringBuilder.AppendLine(Lang("AutoShareTeamsKeyLock", player.UserIDString, shareDataEntry.shareKeyLock ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                    stringBuilder.AppendLine(Lang("AutoShareTeamsCodeLock", player.UserIDString, shareDataEntry.shareCodeLock ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                }
                if (!flag)
                {
                    Print(player, Lang("UnableAutoAuth", player.UserIDString));
                    return;
                }
                Print(player, stringBuilder.ToString());
                return;
            }
            switch (args[0].ToLower())
            {
                case "af":
                case "autofriends":
                    if (!configData.friendsShareS.enabled)
                    {
                        Print(player, Lang("FriendsDisabled", player.UserIDString));
                        return;
                    }
                    if (args.Length <= 1)
                    {
                        storedData.playerShareData[player.userID].friendsShareEntry.enabled = !storedData.playerShareData[player.userID].friendsShareEntry.enabled;
                        Print(player, Lang("Friends", player.UserIDString, storedData.playerShareData[player.userID].friendsShareEntry.enabled ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                        UpdateAuthList(player.userID, AutoAuthType.All);
                        return;
                    }
                    switch (args[1].ToLower())
                    {
                        case "c":
                        case "cupboard":
                            if (!configData.friendsShareS.shareCupboard)
                            {
                                Print(player, Lang("FriendsCupboardDisabled", player.UserIDString));
                                return;
                            }
                            storedData.playerShareData[player.userID].friendsShareEntry.shareCupboard = !storedData.playerShareData[player.userID].friendsShareEntry.shareCupboard;
                            Print(player, Lang("FriendsCupboard", player.UserIDString, storedData.playerShareData[player.userID].friendsShareEntry.shareCupboard ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                            UpdateAuthList(player.userID, AutoAuthType.Cupboard);
                            return;

                        case "t":
                        case "turret":
                            if (!configData.friendsShareS.shareTurret)
                            {
                                Print(player, Lang("FriendsTurretDisable", player.UserIDString));
                                return;
                            }
                            storedData.playerShareData[player.userID].friendsShareEntry.shareTurret = !storedData.playerShareData[player.userID].friendsShareEntry.shareTurret;
                            Print(player, Lang("FriendsTurret", player.UserIDString, storedData.playerShareData[player.userID].friendsShareEntry.shareTurret ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                            UpdateAuthList(player.userID, AutoAuthType.Turret);
                            return;

                        case "kl":
                        case "keylock":
                            if (!configData.friendsShareS.keyLockS.enabled)
                            {
                                Print(player, Lang("FriendsKeyLockDisable", player.UserIDString));
                                return;
                            }
                            storedData.playerShareData[player.userID].friendsShareEntry.shareKeyLock = !storedData.playerShareData[player.userID].friendsShareEntry.shareKeyLock;
                            Print(player, Lang("FriendsKeyLock", player.UserIDString, storedData.playerShareData[player.userID].friendsShareEntry.shareKeyLock ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                            return;

                        case "cl":
                        case "codelock":
                            if (!configData.friendsShareS.codeLockS.enabled)
                            {
                                Print(player, Lang("FriendsCodeLockDisable", player.UserIDString));
                                return;
                            }
                            storedData.playerShareData[player.userID].friendsShareEntry.shareCodeLock = !storedData.playerShareData[player.userID].friendsShareEntry.shareCodeLock;
                            Print(player, Lang("FriendsCodeLock", player.UserIDString, storedData.playerShareData[player.userID].friendsShareEntry.shareCodeLock ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                            return;

                        case "h":
                        case "help":
                            StringBuilder stringBuilder1 = new StringBuilder();
                            stringBuilder1.AppendLine();
                            stringBuilder1.AppendLine(Lang("FriendsSyntax", player.UserIDString, configData.chatS.command));
                            stringBuilder1.AppendLine(Lang("FriendsSyntax1", player.UserIDString, configData.chatS.command));
                            stringBuilder1.AppendLine(Lang("FriendsSyntax2", player.UserIDString, configData.chatS.command));
                            stringBuilder1.AppendLine(Lang("FriendsSyntax3", player.UserIDString, configData.chatS.command));
                            stringBuilder1.AppendLine(Lang("FriendsSyntax4", player.UserIDString, configData.chatS.command));
                            Print(player, stringBuilder1.ToString());
                            return;
                    }
                    Print(player, Lang("SyntaxError", player.UserIDString, configData.chatS.command));
                    return;

                case "ac":
                case "autoclan":
                    if (!configData.clanShareS.enabled)
                    {
                        Print(player, Lang("ClansDisabled", player.UserIDString));
                        return;
                    }
                    if (args.Length <= 1)
                    {
                        storedData.playerShareData[player.userID].clanShareEntry.enabled = !storedData.playerShareData[player.userID].clanShareEntry.enabled;
                        Print(player, Lang("Clans", player.UserIDString, storedData.playerShareData[player.userID].clanShareEntry.enabled ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                        UpdateAuthList(player.userID, AutoAuthType.All);
                        return;
                    }
                    switch (args[1].ToLower())
                    {
                        case "c":
                        case "cupboard":
                            if (!configData.clanShareS.shareCupboard)
                            {
                                Print(player, Lang("ClansCupboardDisable", player.UserIDString));
                                return;
                            }
                            storedData.playerShareData[player.userID].clanShareEntry.shareCupboard = !storedData.playerShareData[player.userID].clanShareEntry.shareCupboard;
                            Print(player, Lang("ClansCupboard", player.UserIDString, storedData.playerShareData[player.userID].clanShareEntry.shareCupboard ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                            UpdateAuthList(player.userID, AutoAuthType.Cupboard);
                            return;

                        case "t":
                        case "turret":
                            if (!configData.clanShareS.shareTurret)
                            {
                                Print(player, Lang("ClansTurretDisable", player.UserIDString));
                                return;
                            }
                            storedData.playerShareData[player.userID].clanShareEntry.shareTurret = !storedData.playerShareData[player.userID].clanShareEntry.shareTurret;
                            Print(player, Lang("ClansTurret", player.UserIDString, storedData.playerShareData[player.userID].clanShareEntry.shareTurret ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                            UpdateAuthList(player.userID, AutoAuthType.Turret);
                            return;

                        case "kl":
                        case "keylock":
                            if (!configData.clanShareS.keyLockS.enabled)
                            {
                                Print(player, Lang("ClansKeyLockDisable", player.UserIDString));
                                return;
                            }
                            storedData.playerShareData[player.userID].clanShareEntry.shareKeyLock = !storedData.playerShareData[player.userID].clanShareEntry.shareKeyLock;
                            Print(player, Lang("ClansKeyLock", player.UserIDString, storedData.playerShareData[player.userID].clanShareEntry.shareKeyLock ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                            return;

                        case "cl":
                        case "codelock":
                            if (!configData.clanShareS.codeLockS.enabled)
                            {
                                Print(player, Lang("ClansCodeLockDisable", player.UserIDString));
                                return;
                            }
                            storedData.playerShareData[player.userID].clanShareEntry.shareCodeLock = !storedData.playerShareData[player.userID].clanShareEntry.shareCodeLock;
                            Print(player, Lang("ClansCodeLock", player.UserIDString, storedData.playerShareData[player.userID].clanShareEntry.shareCodeLock ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                            return;

                        case "h":
                        case "help":
                            StringBuilder stringBuilder1 = new StringBuilder();
                            stringBuilder1.AppendLine();
                            stringBuilder1.AppendLine(Lang("ClansSyntax", player.UserIDString, configData.chatS.command));
                            stringBuilder1.AppendLine(Lang("ClansSyntax1", player.UserIDString, configData.chatS.command));
                            stringBuilder1.AppendLine(Lang("ClansSyntax2", player.UserIDString, configData.chatS.command));
                            stringBuilder1.AppendLine(Lang("ClansSyntax3", player.UserIDString, configData.chatS.command));
                            stringBuilder1.AppendLine(Lang("ClansSyntax4", player.UserIDString, configData.chatS.command));
                            Print(player, stringBuilder1.ToString());
                            return;
                    }
                    Print(player, Lang("SyntaxError", player.UserIDString, configData.chatS.command));
                    return;

                case "at":
                case "autoteam":
                    if (!configData.teamShareS.enabled)
                    {
                        Print(player, Lang("TeamsDisabled", player.UserIDString));
                        return;
                    }
                    if (args.Length <= 1)
                    {
                        storedData.playerShareData[player.userID].teamShareEntry.enabled = !storedData.playerShareData[player.userID].teamShareEntry.enabled;
                        Print(player, Lang("Teams", player.UserIDString, storedData.playerShareData[player.userID].teamShareEntry.enabled ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                        UpdateAuthList(player.userID, AutoAuthType.All);
                        return;
                    }
                    switch (args[1].ToLower())
                    {
                        case "c":
                        case "cupboard":
                            if (!configData.clanShareS.shareCupboard)
                            {
                                Print(player, Lang("TeamsCupboardDisable", player.UserIDString));
                                return;
                            }
                            storedData.playerShareData[player.userID].teamShareEntry.shareCupboard = !storedData.playerShareData[player.userID].teamShareEntry.shareCupboard;
                            Print(player, Lang("TeamsCupboard", player.UserIDString, storedData.playerShareData[player.userID].teamShareEntry.shareCupboard ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                            UpdateAuthList(player.userID, AutoAuthType.Cupboard);
                            return;

                        case "t":
                        case "turret":
                            if (!configData.clanShareS.shareTurret)
                            {
                                Print(player, Lang("TeamsTurretDisable", player.UserIDString));
                                return;
                            }
                            storedData.playerShareData[player.userID].teamShareEntry.shareTurret = !storedData.playerShareData[player.userID].teamShareEntry.shareTurret;
                            Print(player, Lang("TeamsTurret", player.UserIDString, storedData.playerShareData[player.userID].teamShareEntry.shareTurret ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                            UpdateAuthList(player.userID, AutoAuthType.Turret);
                            return;

                        case "kl":
                        case "keylock":
                            if (!configData.clanShareS.keyLockS.enabled)
                            {
                                Print(player, Lang("TeamsKeyLockDisable", player.UserIDString));
                                return;
                            }
                            storedData.playerShareData[player.userID].teamShareEntry.shareKeyLock = !storedData.playerShareData[player.userID].teamShareEntry.shareKeyLock;
                            Print(player, Lang("TeamsKeyLock", player.UserIDString, storedData.playerShareData[player.userID].teamShareEntry.shareKeyLock ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));
                            return;

                        case "cl":
                        case "codelock":
                            if (!configData.clanShareS.codeLockS.enabled)
                            {
                                Print(player, Lang("TeamsCodeLockDisable", player.UserIDString));
                                return;
                            }
                            storedData.playerShareData[player.userID].teamShareEntry.shareCodeLock = !storedData.playerShareData[player.userID].teamShareEntry.shareCodeLock;
                            Print(player, Lang("TeamsCodeLock", player.UserIDString, storedData.playerShareData[player.userID].teamShareEntry.shareCodeLock ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString)));

                            return;

                        case "h":
                        case "help":
                            StringBuilder stringBuilder1 = new StringBuilder();
                            stringBuilder1.AppendLine();
                            stringBuilder1.AppendLine(Lang("TeamsSyntax", player.UserIDString, configData.chatS.command));
                            stringBuilder1.AppendLine(Lang("TeamsSyntax1", player.UserIDString, configData.chatS.command));
                            stringBuilder1.AppendLine(Lang("TeamsSyntax2", player.UserIDString, configData.chatS.command));
                            stringBuilder1.AppendLine(Lang("TeamsSyntax3", player.UserIDString, configData.chatS.command));
                            stringBuilder1.AppendLine(Lang("TeamsSyntax4", player.UserIDString, configData.chatS.command));
                            Print(player, stringBuilder1.ToString());
                            return;
                    }
                    Print(player, Lang("SyntaxError", player.UserIDString, configData.chatS.command));
                    return;

                case "h":
                case "help":
                    bool flag = false;
                    StringBuilder stringBuilder = new StringBuilder();
                    stringBuilder.AppendLine();
                    if (Friends != null && configData.friendsShareS.enabled)
                    {
                        flag = true;
                        stringBuilder.AppendLine(Lang("FriendsSyntax", player.UserIDString, configData.chatS.command));
                        stringBuilder.AppendLine(Lang("FriendsSyntax1", player.UserIDString, configData.chatS.command));
                        stringBuilder.AppendLine(Lang("FriendsSyntax2", player.UserIDString, configData.chatS.command));
                        stringBuilder.AppendLine(Lang("FriendsSyntax3", player.UserIDString, configData.chatS.command));
                        stringBuilder.AppendLine(Lang("FriendsSyntax4", player.UserIDString, configData.chatS.command));
                    }
                    if (Clans != null && configData.clanShareS.enabled)
                    {
                        flag = true;
                        stringBuilder.AppendLine(Lang("ClansSyntax", player.UserIDString, configData.chatS.command));
                        stringBuilder.AppendLine(Lang("ClansSyntax1", player.UserIDString, configData.chatS.command));
                        stringBuilder.AppendLine(Lang("ClansSyntax2", player.UserIDString, configData.chatS.command));
                        stringBuilder.AppendLine(Lang("ClansSyntax3", player.UserIDString, configData.chatS.command));
                        stringBuilder.AppendLine(Lang("ClansSyntax4", player.UserIDString, configData.chatS.command));
                    }
                    if (RelationshipManager.TeamsEnabled() && configData.teamShareS.enabled)
                    {
                        flag = true;
                        stringBuilder.AppendLine(Lang("TeamsSyntax", player.UserIDString, configData.chatS.command));
                        stringBuilder.AppendLine(Lang("TeamsSyntax1", player.UserIDString, configData.chatS.command));
                        stringBuilder.AppendLine(Lang("TeamsSyntax2", player.UserIDString, configData.chatS.command));
                        stringBuilder.AppendLine(Lang("TeamsSyntax3", player.UserIDString, configData.chatS.command));
                        stringBuilder.AppendLine(Lang("TeamsSyntax4", player.UserIDString, configData.chatS.command));
                    }
                    if (!flag)
                    {
                        Print(player, Lang("UnableAutoAuth", player.UserIDString));
                        return;
                    }
                    Print(player, stringBuilder.ToString());
                    return;

                default:
                    Print(player, Lang("SyntaxError", player.UserIDString, configData.chatS.command));
                    return;
            }
        }

        #endregion ChatCommands

        #region ConfigurationFile

        private ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Clear Share Data On Map Wipe")]
            public bool clearDataOnWipe = false;

            [JsonProperty(PropertyName = "Friends share settings")]
            public ShareSettings friendsShareS = new ShareSettings();

            [JsonProperty(PropertyName = "Clan share settings")]
            public ShareSettings clanShareS = new ShareSettings();

            [JsonProperty(PropertyName = "Team share settings")]
            public ShareSettings teamShareS = new ShareSettings();

            [JsonProperty(PropertyName = "Chat settings")]
            public ChatSettings chatS = new ChatSettings();

            public class ChatSettings
            {
                [JsonProperty(PropertyName = "Send authorization success message")]
                public bool sendMessage = true;

                [JsonProperty(PropertyName = "Chat command")]
                public string command = "autoauth";

                [JsonProperty(PropertyName = "Chat prefix")]
                public string prefix = "[AutoAuth]: ";

                [JsonProperty(PropertyName = "Chat prefix color")]
                public string prefixColor = "#00FFFF";

                [JsonProperty(PropertyName = "Chat steamID icon")]
                public ulong steamIDIcon = 0;
            }

            public class ShareSettings
            {
                [JsonProperty(PropertyName = "Enabled")]
                public bool enabled = true;

                [JsonProperty(PropertyName = "Share cupboard")]
                public bool shareCupboard = true;

                [JsonProperty(PropertyName = "Share turret")]
                public bool shareTurret = true;

                [JsonProperty(PropertyName = "Key lock settings")]
                public LockSettings keyLockS = new LockSettings();

                [JsonProperty(PropertyName = "Code lock settings")]
                public LockSettings codeLockS = new LockSettings();
            }

            public class LockSettings
            {
                [JsonProperty(PropertyName = "Enabled")]
                public bool enabled = true;

                [JsonProperty(PropertyName = "Share door")]
                public bool shareDoor = true;

                [JsonProperty(PropertyName = "Share box")]
                public bool shareBox = true;

                [JsonProperty(PropertyName = "Share other locked entities")]
                public bool shareOtherEntity = true;
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                configData = Config.ReadObject<ConfigData>();
                if (configData == null)
                    LoadDefaultConfig();
            }
            catch
            {
                PrintError("The configuration file is corrupted");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            configData = new ConfigData();
        }

        protected override void SaveConfig() => Config.WriteObject(configData);

        #endregion ConfigurationFile

        #region DataFile

        private StoredData storedData;

        private class StoredData
        {
            public Dictionary<ulong, ShareData> playerShareData = new Dictionary<ulong, ShareData>();

            public class ShareData
            {
                public ShareDataEntry friendsShareEntry = new ShareDataEntry();
                public ShareDataEntry clanShareEntry = new ShareDataEntry();
                public ShareDataEntry teamShareEntry = new ShareDataEntry();
            }

            public class ShareDataEntry
            {
                public bool enabled = false;
                public bool shareCupboard = false;
                public bool shareTurret = false;
                public bool shareKeyLock = false;
                public bool shareCodeLock = false;
            }
        }

        private void LoadData()
        {
            try
            {
                storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            }
            catch
            {
                ClearData();
            }
        }

        private void ClearData()
        {
            storedData = new StoredData();
            SaveData();
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);

        private void OnNewSave(string filename)
        {
            if (configData.clearDataOnWipe)
                ClearData();
        }

        #endregion DataFile

        #region LanguageFile

        private void Print(BasePlayer player, string message) => Player.Message(player, message, $"<color={configData.chatS.prefixColor}>{configData.chatS.prefix}</color>", configData.chatS.steamIDIcon);

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "You do not have permission to use this command",
                ["Enabled"] = "<color=#8ee700>Enabled</color>",
                ["Disabled"] = "<color=#ce422b>Disabled</color>",
                ["UnableAutoAuth"] = "Unable to automatically authorize other players",
                ["SyntaxError"] = "Syntax error, please type '<color=#ce422b>/{0} <help | h></color>' to view help",
                ["TurretSuccess"] = "Successfully added <color=#ce422b>{0}</color> friends/clan members/team members to <color=#ce422b>{1}</color> turrets auth list",
                ["CupboardSuccess"] = "Successfully added <color=#ce422b>{0}</color> friends/clan members/team members  to <color=#ce422b>{1}</color> cupboards auth list",

                ["FriendsSyntax"] = "<color=#ce422b>/{0} <autofriends | af></color> - Enable/Disable automatic authorization for your friends",
                ["FriendsSyntax1"] = "<color=#ce422b>/{0} <autofriends | af> <cupboard | c></color> - Sharing cupboard with your friends",
                ["FriendsSyntax2"] = "<color=#ce422b>/{0} <autofriends | af> <turret | t></color> - Sharing turret with your friends",
                ["FriendsSyntax3"] = "<color=#ce422b>/{0} <autofriends | af> <keylock | kl></color> - Sharing key lock with your friends",
                ["FriendsSyntax4"] = "<color=#ce422b>/{0} <autofriends | af> <codelock | cl></color> - Sharing code lock with your friends",

                ["ClansSyntax"] = "<color=#ce422b>/{0} <autoclan | ac></color> - Enable/Disable automatic authorization for your clan members",
                ["ClansSyntax1"] = "<color=#ce422b>/{0} <autoclan | ac> <cupboard | c></color> - Sharing cupboard with your clan members",
                ["ClansSyntax2"] = "<color=#ce422b>/{0} <autoclan | ac> <turret | t></color> - Sharing turret with your clan members",
                ["ClansSyntax3"] = "<color=#ce422b>/{0} <autoclan | ac> <keylock | kl></color> - Sharing key lock with your clan members",
                ["ClansSyntax4"] = "<color=#ce422b>/{0} <autoclan | ac> <codelock | cl></color> - Sharing code lock with your clan members",

                ["TeamsSyntax"] = "<color=#ce422b>/{0} <autoteam | at></color> - Enable/Disable automatic authorization for your team members",
                ["TeamsSyntax1"] = "<color=#ce422b>/{0} <autoteam | at> <cupboard | c></color> - Sharing cupboard with your team members",
                ["TeamsSyntax2"] = "<color=#ce422b>/{0} <autoteam | at> <turret | t></color> - Sharing turret with your team members",
                ["TeamsSyntax3"] = "<color=#ce422b>/{0} <autoteam | at> <keylock | kl></color> - Sharing key lock with your team members",
                ["TeamsSyntax4"] = "<color=#ce422b>/{0} <autoteam | at> <codelock | cl></color> - Sharing code lock with your team members",

                ["AutoShareFriendsStatus"] = "<color=#ffa500>Current friends sharing status: </color>",
                ["AutoShareFriends"] = "Automatically sharing with friends: {0}",
                ["AutoShareFriendsCupboard"] = "Automatically sharing cupboard with friends: {0}",
                ["AutoShareFriendsTurret"] = "Automatically sharing turret with friends: {0}",
                ["AutoShareFriendsKeyLock"] = "Automatically sharing key lock with friends: {0}",
                ["AutoShareFriendsCodeLock"] = "Automatically sharing code lock with friends: {0}",

                ["AutoShareClansStatus"] = "<color=#ffa500>Current clan sharing status: </color>",
                ["AutoShareClans"] = "Automatically sharing with clan: {0}",
                ["AutoShareClansCupboard"] = "Automatically sharing cupboard with clan: {0}",
                ["AutoShareClansTurret"] = "Automatically sharing turret with clan: {0}",
                ["AutoShareClansKeyLock"] = "Automatically sharing key lock with clan: {0}",
                ["AutoShareClansCodeLock"] = "Automatically sharing code lock with clan: {0}",

                ["AutoShareTeamsStatus"] = "<color=#ffa500>Current Team sharing status: </color>",
                ["AutoShareTeams"] = "Automatically sharing with Team: {0}",
                ["AutoShareTeamsCupboard"] = "Automatically sharing cupboard with Team: {0}",
                ["AutoShareTeamsTurret"] = "Automatically sharing turret with Team: {0}",
                ["AutoShareTeamsKeyLock"] = "Automatically sharing key lock with Team: {0}",
                ["AutoShareTeamsCodeLock"] = "Automatically sharing code lock with Team: {0}",

                ["Friends"] = "Friends automatic authorization {0}",
                ["FriendsCupboard"] = "Sharing cupboard with friends is {0}",
                ["FriendsTurret"] = "Sharing turret with friends is {0}",
                ["FriendsKeyLock"] = "Sharing key lock with friends is {0}",
                ["FriendsCodeLock"] = "Sharing code lock with friends is {0}",

                ["Clans"] = "Clan automatic authorization {0}",
                ["ClansCupboard"] = "Sharing cupboard with clan is {0}",
                ["ClansTurret"] = "Sharing turret with clan is {0}",
                ["ClansKeyLock"] = "Sharing key lock with clan is {0}",
                ["ClansCodeLock"] = "Sharing code lock with clan is {0}",

                ["Teams"] = "Team automatic authorization {0}",
                ["TeamsCupboard"] = "Sharing cupboard with team is {0}",
                ["TeamsTurret"] = "Sharing turret with team is {0}",
                ["TeamsKeyLock"] = "Sharing key lock with team is {0}",
                ["TeamsCodeLock"] = "Sharing code lock with team is {0}",

                ["FriendsDisabled"] = "Server has disabled friends sharing",
                ["FriendsCupboardDisabled"] = "Server has disabled sharing cupboard with friends",
                ["FriendsTurretDisable"] = "Server has disabled sharing turret with friends",
                ["FriendsKeyLockDisable"] = "Server has disabled sharing key lock with friends",
                ["FriendsCodeLockDisable"] = "Server has disabled sharing code lock with friends",

                ["ClansDisabled"] = "Server has disabled clan sharing",
                ["ClansCupboardDisable"] = "Server has disabled sharing cupboard with clan",
                ["ClansTurretDisable"] = "Server has disabled sharing turret with clan",
                ["ClansKeyLockDisable"] = "Server has disabled sharing key lock with clan",
                ["ClansCodeLockDisable"] = "Server has disabled sharing code lock with clan",

                ["TeamsDisabled"] = "Server has disabled team sharing",
                ["TeamsCupboardDisable"] = "Server has disabled sharing cupboard with team",
                ["TeamsTurretDisable"] = "Server has disabled sharing turret with team",
                ["TeamsKeyLockDisable"] = "Server has disabled sharing key lock with team",
                ["TeamsCodeLockDisable"] = "Server has disabled sharing code lock with team",
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "您没有权限使用该命令",
                ["Enabled"] = "<color=#8ee700>已启用</color>",
                ["Disabled"] = "<color=#ce422b>已禁用</color>",
                ["UnableAutoAuth"] = "服务器无法使用自动授权",
                ["SyntaxError"] = "语法错误, 输入 '<color=#ce422b>/{0} <help | h></color>' 查看帮助",
                ["TurretSuccess"] = "自动添加了 <color=#ce422b>{0}</color> 个朋友/战队成员/队友到您的 <color=#ce422b>{1}</color> 个炮台授权列表中",
                ["CupboardSuccess"] = "自动添加了 <color=#ce422b>{0}</color> 个朋友/战队成员/队友到您的 <color=#ce422b>{1}</color> 个领地柜授权列表中",

                ["FriendsSyntax"] = "<color=#ce422b>/{0} <autofriends | af></color> - 启用/禁用朋友自动授权",
                ["FriendsSyntax1"] = "<color=#ce422b>/{0} <autofriends | af> <cupboard | c></color> - 自动与朋友共享领地柜",
                ["FriendsSyntax2"] = "<color=#ce422b>/{0} <autofriends | af> <turret | t></color> - 自动与朋友共享炮台",
                ["FriendsSyntax3"] = "<color=#ce422b>/{0} <autofriends | af> <keylock | kl></color> - 自动与朋友共享钥匙锁",
                ["FriendsSyntax4"] = "<color=#ce422b>/{0} <autofriends | af> <codelock | cl></color> - 自动与朋友共享密码锁",

                ["ClansSyntax"] = "<color=#ce422b>/{0} <autoclan | ac></color> - 启用/禁用战队自动授权",
                ["ClansSyntax1"] = "<color=#ce422b>/{0} <autoclan | ac> <cupboard | c></color> - 自动与战队共享领地柜",
                ["ClansSyntax2"] = "<color=#ce422b>/{0} <autoclan | ac> <turret | t></color> - 自动与战队共享炮台",
                ["ClansSyntax3"] = "<color=#ce422b>/{0} <autoclan | ac> <keylock | kl></color> - 自动与战队共享钥匙锁",
                ["ClansSyntax4"] = "<color=#ce422b>/{0} <autoclan | ac> <codelock | cl></color> - 自动与战队共享密码锁",

                ["TeamsSyntax"] = "<color=#ce422b>/{0} <autoclan | ac></color> - 启用/禁用团队自动授权",
                ["TeamsSyntax1"] = "<color=#ce422b>/{0} <autoclan | ac> <cupboard | c></color> - 自动与团队共享领地柜",
                ["TeamsSyntax2"] = "<color=#ce422b>/{0} <autoclan | ac> <turret | t></color> - 自动与团队共享炮台",
                ["TeamsSyntax3"] = "<color=#ce422b>/{0} <autoclan | ac> <keylock | kl></color> - 自动与团队共享钥匙锁",
                ["TeamsSyntax4"] = "<color=#ce422b>/{0} <autoclan | ac> <codelock | cl></color> - 自动与团队共享密码锁",

                ["AutoShareFriendsStatus"] = "<color=#ffa500>当前朋友自动授权状态: </color>",
                ["AutoShareFriends"] = "自动与朋友共享: {0}",
                ["AutoShareFriendsCupboard"] = "自动与朋友共享领地柜: {0}",
                ["AutoShareFriendsTurret"] = "自动与朋友共享炮台: {0}",
                ["AutoShareFriendsKeyLock"] = "自动与朋友共享钥匙锁: {0}",
                ["AutoShareFriendsCodeLock"] = "自动与朋友共享密码锁: {0}",

                ["AutoShareClansStatus"] = "<color=#ffa500>当前战队自动授权状态: </color>",
                ["AutoShareClans"] = "自动与战队共享: {0}",
                ["AutoShareClansCupboard"] = "自动与战队共享领地柜: {0}",
                ["AutoShareClansTurret"] = "自动与战队共享炮台: {0}",
                ["AutoShareClansKeyLock"] = "自动与战队共享钥匙锁: {0}",
                ["AutoShareClansCodeLock"] = "自动与战队共享密码锁: {0}",

                ["AutoShareTeamsStatus"] = "<color=#ffa500>当前团队自动授权状态: </color>",
                ["AutoShareTeams"] = "自动与团队共享: {0}",
                ["AutoShareTeamsCupboard"] = "自动与团队共享领地柜: {0}",
                ["AutoShareTeamsTurret"] = "自动与团队共享炮台: {0}",
                ["AutoShareTeamsKeyLock"] = "自动与团队共享钥匙锁: {0}",
                ["AutoShareTeamsCodeLock"] = "自动与团队共享密码锁: {0}",

                ["Friends"] = "朋友自动授权 {0}",
                ["FriendsCupboard"] = "自动与朋友共享领地柜 {0}",
                ["FriendsTurret"] = "自动与朋友共享炮台 {0}",
                ["FriendsKeyLock"] = "自动与朋友共享钥匙锁 {0}",
                ["FriendsCodeLock"] = "自动与朋友共享密码锁 {0}",

                ["Clans"] = "战队自动授权 {0}",
                ["ClansCupboard"] = "自动与战队共享领地柜 {0}",
                ["ClansTurret"] = "自动与战队共享炮台 {0}",
                ["ClansKeyLock"] = "自动与战队共享钥匙锁 {0}",
                ["ClansCodeLock"] = "自动与战队共享密码锁 {0}",

                ["Teams"] = "团队自动授权 {0}",
                ["TeamsCupboard"] = "自动与团队共享领地柜 {0}",
                ["TeamsTurret"] = "自动与团队共享炮台 {0}",
                ["TeamsKeyLock"] = "自动与团队共享钥匙锁 {0}",
                ["TeamsCodeLock"] = "自动与团队共享密码锁 {0}",

                ["FriendsDisabled"] = "服务器已禁用朋友自动授权",
                ["FriendsCupboardDisabled"] = "服务器已禁用自动与朋友共享领地柜",
                ["FriendsTurretDisable"] = "服务器已禁用自动与朋友共享炮台",
                ["FriendsKeyLockDisable"] = "服务器已禁用自动与朋友共享钥匙锁",
                ["FriendsCodeLockDisable"] = "服务器已禁用自动与朋友共享密码锁",

                ["ClansDisabled"] = "服务器已禁用战队自动授权",
                ["ClansCupboardDisable"] = "服务器已禁用自动与战队共享领地柜",
                ["ClansTurretDisable"] = "服务器已禁用自动与战队共享炮台",
                ["ClansKeyLockDisable"] = "服务器已禁用自动与战队共享钥匙锁",
                ["ClansCodeLockDisable"] = "服务器已禁用自动与战队共享密码锁",

                ["TeamsDisabled"] = "服务器已禁用团队自动授权",
                ["TeamsCupboardDisable"] = "服务器已禁用自动与团队共享领地柜",
                ["TeamsTurretDisable"] = "服务器已禁用自动与团队共享炮台",
                ["TeamsKeyLockDisable"] = "服务器已禁用自动与团队共享钥匙锁",
                ["TeamsCodeLockDisable"] = "服务器已禁用自动与团队共享密码锁",
            }, this, "zh-CN");
        }

        #endregion LanguageFile
    }
}