using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System;
using Facepunch.Extend;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SoFriends | Система друзей", "LAGZYA", "0.0.1")]
    public class SoFriends : RustPlugin
    {
        #region [DATA&CONFIG]

        private Dictionary<ulong, FriendData> friendData = new Dictionary<ulong, FriendData>();
        private Dictionary<ulong, ulong> playerAccept = new Dictionary<ulong, ulong>();
        private static Configs cfg { get; set; }

        private class FriendData
        {
            [JsonProperty("Ник")] public string Name;

            [JsonProperty("Список друзей")]
            public Dictionary<ulong, FriendAcces> friendList = new Dictionary<ulong, FriendAcces>();

            public class FriendAcces
            {
                [JsonProperty("Ник")] public string name;
                [JsonProperty("Урон по человеку")] public bool Damage;

                [JsonProperty("Авторизациия в турелях")]
                public bool Turret;

                [JsonProperty("Авторизациия в дверях")]
                public bool Door;

                [JsonProperty("Авторизациия в пво")] public bool Sam;
            }
        }

        private class Configs
        {
            [JsonProperty("Включить настройку авто авторизации турелей?")]
            public bool Turret;

            [JsonProperty("Включить настройку урона по своим?")]
            public bool Damage;

            [JsonProperty("Включить настройку авто авторизации в дверях?")]
            public bool Door;

            [JsonProperty("Включить настройку авто авторизации в пво?")]
            public bool Sam;

            [JsonProperty("Сколько максимум людей может быть в друзьях?")]
            public int MaxFriends;

            [JsonProperty("Урон по человеку(По стандрату у игрока включена?)")]
            public bool SDamage;

            [JsonProperty("Авторизациия в турелях(По стандрату у игрока включена?)")]
            public bool STurret;

            [JsonProperty("Авторизациия в дверях(По стандрату у игрока включена?)")]
            public bool SDoor;

            [JsonProperty("Авторизациия в пво(По стандрату у игрока включена?)")]
            public bool SSam;
            
            [JsonProperty("Время ожидания  ответа на запроса в секнудах")]
            public int otvet;

            [JsonProperty("Вообще включать пво настройку?")]
            public bool SSamOn;

            public static Configs GetNewConf()
            {
                var newconfig = new Configs();
                newconfig.Damage = true;
                newconfig.Door = true;
                newconfig.Turret = true;
                newconfig.Sam = true;
                newconfig.MaxFriends = 5;
                newconfig.SDamage = false;
                newconfig.SDoor = true;
                newconfig.STurret = true;
                newconfig.SSam = true;
                newconfig.SSamOn = true;
                newconfig.otvet = 10;
                return newconfig;
            }
        }

        protected override void LoadDefaultConfig() => cfg = Configs.GetNewConf();
        protected override void SaveConfig() => Config.WriteObject(cfg);

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                cfg = Config.ReadObject<Configs>();
            }
            catch
            {
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }
        protected override void LoadDefaultMessages()
         {
             var ru = new Dictionary<string, string>();
             foreach (var rus in new Dictionary<string, string>()
             {
                 ["SYNTAX"] = "/fmenu - Открыть меню друзей\n/f(riend) add - Добавить в друзья\n/f(riend) remove - Удалить из друзей\n/f(riend) list - Список друзей\n/f(riend) team - Пригласить в тиму всех друзей онлайн\n/f(riend) set - Настройка друзей по отдельности\n/f(riend) setall - Настройка друзей всех сразу",
                 ["NPLAYER"] = "Игрок не найден!",
                 ["CANTADDME"] = "Нельзя добавить себя в друзья!",
                 ["ONFRIENDS"] = "Игрок уже у вас в друзьях!",
                 ["MAXFRIENDSPLAYERS"] = "У игрока максимальное кол-во друзей!",
                 ["MAXFRIENDYOU"] = "У вас максимальное кол-во друзей!",
                 ["HAVEINVITE"] = "Игрок уже имеет запрос в друзья!",
                 ["SENDADD"] = "Вы отправили запрос, ждем ответа!",
                 ["YOUHAVEINVITE"] = "Вам пришел запрос в друзья напишите /f(riend) accept",
                 ["TIMELEFT"] = "Вы не ответили на запрос!",
                 ["HETIMELEFT"] = "Вам не ответили на запрос!",
                 ["DONTHAVE"] = "У вас нет запросов!",
                 ["ADDFRIEND"] = "Успешное добавление в друзья!",
                 ["DENYADD"] = "Отклонение запроса в друзья!",
                 ["PLAYERDHAVE"] = "У тебя нету такого игрока в друзьях!",
                 ["REMOVEFRIEND"] = "Успешное удаление из друзей!",
                 ["LIST"] = "Список пуст!",
                 ["LIST2"] = "Список друзей",
                 ["SYNTAXSET"] = "/f(riend) set damage [Name] - Урон по человеку\n/f(riend) set door [NAME] - Авторизация в дверях для человека\n/f(riend) set turret [NAME] - Авторизация в турелях для человека\n/f(riend) set sam [NAME] - Авторизация в пво для человека",
                 ["SETOFF"] = "Настройка отключена",
                 ["DAMAGEOFF"] = "Урон по игроку {0} выключен!",
                 ["DAMAGEON"] = "Урон по игроку {0} включен!",
                 ["AUTHDOORON"] = "Авторизация в дверях для {0} включена!",
                 ["AUTHDOOROFF"] = "Авторизация в дверях для {0} выключена!",
                 ["AUTHTURRETON"] = "Авторизация в терелях для {0} включена!",
                 ["AUTHTURRETOFF"] = "Авторизация в терелях для {0} выключена!",
                 ["AUTHSAMON"] = "Авторизация в ПВО для {0} включена!",
                 ["AUTHSAMOFF"] = "Авторизация в ПВО для {0} выключена!",
                 ["SYNTAXSETALL"] = "/f(riend) setall damage 0/1 - Урон по всех друзей\n/f(riend) setall door 0/1 - Авторизация в дверях для всех друзей\n/f(riend) setall turret 0/1 - Авторизация в турелях для всех друзей\n/f(riend) setall sam 0/1 - Авторизация в пво для всех друзей",
                 ["DAMAGEOFFALL"] = "Урон по всем друзьям выключен!",
                 ["DAMAGEONALL"] = "Урон по всем друзьям включен!",
                 ["AUTHDOORONALL"] = "Авторизация в дверях для всех друзей включена!",
                 ["AUTHDOOROFFALL"] = "Авторизация в дверях для всех друзей выключена!",
                 ["AUTHTURRETONALL"] = "Авторизация в терелях для всех друзей включена!",
                 ["AUTHTURRETOFFALL"] = "Авторизация в терелях для всех друзей выключена!",
                 ["AUTHSAMONALL"] = "Авторизация в ПВО для всех друзей включена!",
                 ["AUTHSAMOFFALL"] = "Авторизация в ПВО для всех друзей выключена!",
                 ["SENDINVITETEAM"] = "Приглашение отправлено: ",
                 ["SENDINVITE"] = "Вам пришло приглашение в команду от",
                 ["DAMAGE"] = "Нельзя аттаковать {0} это ваш друг!",
             }) ru.Add(rus.Key, rus.Value);
             lang.RegisterMessages(ru, this, "ru");
             lang.RegisterMessages(ru, this, "en");
         }
        #endregion

        #region [Func]

        private string PlugName = "<color=#FF8C00>(FRIEND)</color> ";

        [ChatCommand("f")]
        private void FriendCmd(BasePlayer player, string command, string[] arg)
        {
            ulong ss;
            FriendData player1;
            FriendData targetPlayer;
            if (!friendData.TryGetValue(player.userID, out player1)) return;
            if (arg.Length < 1)
            {
                SendReply(player,
                    $"<size=22>{PlugName}</size>\n{lang.GetMessage("SYNTAX", this, player.UserIDString)}");
                return;
            }

            switch (arg[0])
            {
                case "add":
                    if (arg.Length < 2)
                    {
                        SendReply(player, $"{PlugName}/f(riend) add [NAME or SteamID]");
                        return;
                    }

                    var argLists = arg.ToList();
                    argLists.RemoveRange(0, 1);
                    var name = string.Join(" ", argLists.ToArray()).ToLower();
                    var target = BasePlayer.Find(name);
                    if (target == null || !friendData.TryGetValue(target.userID, out targetPlayer))
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("NPLAYER", this, player.UserIDString)}");
                        return;
                    }
                    
                    if (target.userID == player.userID)
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("CANTADDME", this, player.UserIDString)}");
                        return;
                    }
                    
                    if (player1.friendList.Count >= cfg.MaxFriends)
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("MAXFRIENDSYOU", this, player.UserIDString)}");
                        return;
                    }
                    
                    if (player1.friendList.ContainsKey(target.userID))
                    {
                        SendReply(player, $"{PlugName}");
                        return;
                    }
                    
                    if (targetPlayer.friendList.Count >= cfg.MaxFriends)
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("MAXFRIENDSPLAYERS", this, player.UserIDString)}");
                        return;
                    }
                    
                    if (playerAccept.ContainsKey(target.userID))
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("HAVEINVITE", this, player.UserIDString)}");
                        return;
                    }

                    playerAccept.Add(target.userID, player.userID);
                    SendReply(player, $"{PlugName}{lang.GetMessage("SENDADD", this, player.UserIDString)}");
                    SendReply(target, $"{PlugName}{lang.GetMessage("YOUHAVEINVITE", this, target.UserIDString)}");
                    InivteStart(player, target);
                    ss = target.userID;
                    timer.Once(cfg.otvet, () =>
                    {
                        if (!playerAccept.ContainsKey(target.userID) || !playerAccept.ContainsValue(player.userID)) return;
                        if (target != null)
                        {
                            CuiHelper.DestroyUi(target, LayerInvite);
                            SendReply(target, $"{PlugName}{lang.GetMessage("TIMELEFT", this, target.UserIDString)}");
                        }
                        
                        SendReply(player, $"{PlugName}{lang.GetMessage("HETIMELEFT", this, player.UserIDString)}");
                        playerAccept.Remove(ss);
                    });
                    break;
                case "accept":

                    if (!playerAccept.TryGetValue(player.userID, out ss))
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("DONTHAVE", this, player.UserIDString)}");
                        return;
                    }

                    if (!friendData.TryGetValue(ss, out targetPlayer))
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("NPLAYER", this, player.UserIDString)}");
                        return;
                    }

                    if (player1.friendList.Count >= cfg.MaxFriends)
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("MAXFRIENDSYOU", this, player.UserIDString)}");
                        return;
                    }

                    if (targetPlayer.friendList.Count >= cfg.MaxFriends)
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("MAXFRIENDSPLAYERS", this, player.UserIDString)}!");
                        return;
                    }

                    target = BasePlayer.FindByID(ss);
                    player1.friendList.Add(target.userID,
                        new FriendData.FriendAcces()
                        {
                            name = target.displayName, Damage = cfg.SDamage, Door = cfg.SDoor, Turret = cfg.STurret,
                            Sam = cfg.SSam
                        });
                    targetPlayer.friendList.Add(player.userID,
                        new FriendData.FriendAcces()
                        {
                            name = player.displayName, Damage = cfg.SDamage, Door = cfg.SDoor, Turret = cfg.STurret,
                            Sam = cfg.SSam
                        });
                    SendReply(player, $"{PlugName}{lang.GetMessage("ADDFRIEND", this, player.UserIDString)}");
                    playerAccept.Remove(player.userID);
                    SendReply(target, $"{PlugName}{lang.GetMessage("ADDFRIEND", this, target.UserIDString)}");
                    CuiHelper.DestroyUi(player, LayerInvite);
                    break;
                case "deny":
                    if (!playerAccept.TryGetValue(player.userID, out ss))
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("DONTHAVE", this, player.UserIDString)}");
                        return;
                    }

                    if (!friendData.TryGetValue(ss, out targetPlayer))
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("NPLAYER", this, player.UserIDString)}");
                        return;
                    }

                    target = BasePlayer.FindByID(ss);
                    playerAccept.Remove(player.userID);
                    SendReply(player, $"{PlugName}{lang.GetMessage("DENYADD", this, player.UserIDString)}");
                    SendReply(target, $"{PlugName}{lang.GetMessage("DENYADD", this, target.UserIDString)}");
                    CuiHelper.DestroyUi(player, LayerInvite);
                    break;
                case "remove":
                    if (arg.Length < 2)
                    {
                        SendReply(player, $"{PlugName}/f(riend) remove [NAME or SteamID]");
                        return;
                    }

                    argLists = arg.ToList();
                    argLists.RemoveRange(0, 1);
                    name = string.Join(" ", argLists.ToArray()).ToLower();
                    ulong tt;
                    if (ulong.TryParse(arg[1], out tt)) { }else tt = player1.friendList.FirstOrDefault(p => p.Value.name.ToLower().Contains(name)).Key;

                    if (!player1.friendList.ContainsKey(tt))
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("PLAYERDHAVE", this, player.UserIDString)}");
                        return;
                    }

                    if (!friendData.TryGetValue(tt, out targetPlayer))
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("NPLAYER", this, player.UserIDString)}");
                        return;
                    }

                    player1.friendList.Remove(tt);
                    targetPlayer.friendList.Remove(player.userID);
                    SendReply(player, $"{PlugName}{lang.GetMessage("REMOVEFRIEND", this, player.UserIDString)}");
                    target = tt.IsSteamId() ? BasePlayer.FindByID(tt) : BasePlayer.Find(arg[1].ToLower());
                    if (target != null)
                        SendReply(target, $"{PlugName}{lang.GetMessage("REMOVEFRIEND", this, player.UserIDString)}");
                    break;
                case "list":
                    if (player1.friendList.Count < 1)
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("LIST", this, player.UserIDString)}");
                        return;
                    }
                    
                    var argList = player1.friendList;
                    var friendlist = $"{PlugName}{lang.GetMessage("LIST2", this, player.UserIDString)}\n";
                    foreach (var keyValuePair in argList)
                        friendlist += keyValuePair.Value.name + $"({keyValuePair.Key})\n";
                    SendReply(player, friendlist);
                    break;
                case "set":
                    if (arg.Length < 3)
                    {
                        SendReply(player, $"<size=22>{PlugName}</size>\n{lang.GetMessage("SYNTAXSET", this, player.UserIDString)}");
                        return;
                    }

                    argLists = arg.ToList();
                    argLists.RemoveRange(0, 2);
                    name = string.Join(" ", argLists.ToArray()).ToLower();
                    FriendData.FriendAcces access;
                    if (ulong.TryParse(arg[2], out ss)) {}else ss = player1.friendList.FirstOrDefault(p => p.Value.name.ToLower().Contains(name)).Key;

                    if (!player1.friendList.TryGetValue(ss, out access))
                    {
                        SendReply(player, $"{PlugName}{lang.GetMessage("NPLAYER", this, player.UserIDString)}");
                        return;
                    }

                    switch (arg[1])
                    {
                        case "damage":
                            if (!cfg.Damage)
                            {
                                SendReply(player, $"{PlugName}{lang.GetMessage("SETOFF", this, player.UserIDString)}");
                                return;
                            }

                            if (access.Damage)
                            {
                                SendReply(player, $"{PlugName}{String.Format(lang.GetMessage("DAMAGEOFF", this, player.UserIDString), access.name)}");
                                access.Damage = false;
                            }
                            else
                            {
                                SendReply(player, $"{PlugName}{String.Format(lang.GetMessage("DAMAGEON", this, player.UserIDString), access.name)}");
                                access.Damage = true;
                            }

                            break;
                        case "door":
                            if (!cfg.Door)
                            {
                                SendReply(player, $"{PlugName}{lang.GetMessage("SETOFF", this, player.UserIDString)}");
                                return;
                            }

                            if (access.Door)
                            {
                                SendReply(player,
                                    $"{PlugName}{String.Format(lang.GetMessage("AUTHDOOROFF", this, player.UserIDString), access.name)}");
                                access.Door = false;
                            }
                            else
                            {
                                SendReply(player, $"{PlugName}{String.Format(lang.GetMessage("AUTHDOORON", this, player.UserIDString), access.name)}");
                                access.Door = true;
                            }

                            break;
                        case "turret":
                            if (!cfg.Turret)
                            {
                                SendReply(player, $"{PlugName}{lang.GetMessage("SETOFF", this, player.UserIDString)}");
                                return;
                            }

                            if (access.Turret)
                            {
                                SendReply(player,
                                    $"{PlugName}{String.Format(lang.GetMessage("AUTHTURRETOFF", this, player.UserIDString), access.name)}");
                                access.Turret = false;
                            }
                            else
                            {
                                SendReply(player,
                                    $"{PlugName}{String.Format(lang.GetMessage("AUTHTURRETON", this, player.UserIDString), access.name)}");
                                access.Turret = true;
                            }

                            break;
                        case "sam":
                            if (!cfg.SSamOn) return;
                            if (!cfg.Sam)
                            {
                                SendReply(player, $"{PlugName}{lang.GetMessage("SETOFF", this, player.UserIDString)}");
                                return;
                            }

                            if (access.Sam)
                            {
                                SendReply(player, $"{PlugName}{String.Format(lang.GetMessage("AUTHSAMOFF", this, player.UserIDString), access.name)}");
                                access.Sam = false;
                            }
                            else
                            {
                                SendReply(player, $"{PlugName}{String.Format(lang.GetMessage("AUTHSAMON", this, player.UserIDString), access.name)}");
                                access.Sam = true;
                            }

                            break;
                    }

                    break;
                case "setall":
                    if (arg.Length < 3)
                    {
                        SendReply(player,
                            $"<size=22>{PlugName}</size>\n{lang.GetMessage("SYNTAXSETALL", this, player.UserIDString)}");
                        return;
                    }

                    switch (arg[1])
                    {
                        case "door":
                            if (!cfg.Door)
                            {
                                SendReply(player, $"{PlugName}{lang.GetMessage("SETOFF", this, player.UserIDString)}");
                                return;
                            }
                            if (arg[2] == "1")
                            {
                                foreach (var friends in player1.friendList)
                                {
                                    friends.Value.Door = true;
                                }

                                SendReply(player, $"{PlugName}{lang.GetMessage("AUTHDOORONALL", this, player.UserIDString)}");
                            }
                            else
                            {
                                foreach (var friends in player1.friendList)
                                {
                                    friends.Value.Door = false;
                                }

                                SendReply(player, $"{PlugName}{lang.GetMessage("AUTHDOOROFFALL", this, player.UserIDString)}");
                            }

                            break;
                        case "damage":
                            if (!cfg.Damage)
                            {
                                SendReply(player, $"{PlugName}{lang.GetMessage("SETOFF", this, player.UserIDString)}");
                                return;
                            }
                            if (arg[2] == "1")
                            {
                                foreach (var friends in player1.friendList)
                                {
                                    friends.Value.Damage = true;
                                }

                                SendReply(player, $"{PlugName}{lang.GetMessage("DAMAGEON", this, player.UserIDString)}");
                            }
                            else
                            {
                                foreach (var friends in player1.friendList)
                                {
                                    friends.Value.Damage = false;
                                }

                                SendReply(player, $"{PlugName}{lang.GetMessage("DAMAGEOFF", this, player.UserIDString)}");
                            }

                            break;
                        case "turret":
                            if (!cfg.Turret)
                            {
                                SendReply(player, $"{PlugName}{lang.GetMessage("SETOFF", this, player.UserIDString)}");
                                return;
                            }
                            if (arg[2] == "1")
                            {
                                foreach (var friends in player1.friendList)
                                {
                                    friends.Value.Turret = true;
                                }

                                SendReply(player, $"{PlugName}{lang.GetMessage("AUTHTURRETONALL", this, player.UserIDString)}");
                            }
                            else
                            {
                                foreach (var friends in player1.friendList)
                                {
                                    friends.Value.Turret = false;
                                }

                                SendReply(player, $"{PlugName}{lang.GetMessage("AUTHTURRETOFFALL", this, player.UserIDString)}");
                            }

                            break;
                        case "sam":
                            if (!cfg.SSamOn) return;
                            if (!cfg.Sam)
                            {
                                SendReply(player, $"{PlugName}{lang.GetMessage("SETOFF", this, player.UserIDString)}");
                                return;
                            }
                            if (arg[2] == "1")
                            {
                                foreach (var friends in player1.friendList)
                                {
                                    friends.Value.Sam = true;
                                }

                                SendReply(player, $"{PlugName}{lang.GetMessage("AUTHSAMONALL", this, player.UserIDString)}");
                            }
                            else
                            {
                                foreach (var friends in player1.friendList)
                                {
                                    friends.Value.Sam = false;
                                }

                                SendReply(player, $"{PlugName}{lang.GetMessage("AUTHSAMOFFALL", this, player.UserIDString)}");
                            }

                            break;
                    }

                    break;
                case "team":
                    var team = player.Team;
                    if (team == null || player.currentTeam == 0)
                    {
                        team = RelationshipManager.Instance.CreateTeam();
                        team.AddPlayer(player);
                    }

                    var text = $"{PlugName}{lang.GetMessage("SENDINVITETEAM", this, player.UserIDString)}";
                    foreach (var ts in player1.friendList)
                    {
                        target = BasePlayer.Find(ts.Key.ToString());
                        if (target != null)
                        {
                            if (target.currentTeam == 0)
                            {
                                team.SendInvite(target);
                                text += $"{target.displayName}[{target.userID}] ";
                                SendReply(target,
                                    $"{PlugName}{lang.GetMessage("SENDINVITE", this, player.UserIDString)} {player.displayName}[{player.userID}]");
                            }
                        }
                    }

                    SendReply(player, text);
                    break;
            }
        }

        [ConsoleCommand("friend")]
        private void FriendConsole(ConsoleSystem.Arg arg)
        {
            if (!cfg.SSamOn) return;
            FriendCmd(arg.Player(), "friend", arg.Args);
            if (arg.Args[0] == "set")
            {
                SettingInit(arg.Player(), ulong.Parse(arg.Args[2]));
            }

            if (arg.Args[0] == "remove")
            {
                StartUi(arg.Player());
            }
        }

        [ChatCommand("friend")]
        private void FriendCmd2(BasePlayer player, string command, string[] arg) => FriendCmd(player, command, arg);

        #endregion

        #region [Hooks]

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return null;
            FriendData player1;
            var targetplayer = entity as BasePlayer;
            var attackerplayer = info.Initiator as BasePlayer;
            if (attackerplayer == null || targetplayer == null) return null;
            if (!friendData.TryGetValue(attackerplayer.userID, out player1)) return null;
            FriendData.FriendAcces ss;
            if (!player1.friendList.TryGetValue(targetplayer.userID, out ss)) return null;
            if (ss.Damage) return null;
            SendReply(attackerplayer, string.Format(lang.GetMessage("DAMAGE",this, attackerplayer.UserIDString),targetplayer.displayName ));
            return false;
        }

        private object OnTurretTarget(AutoTurret turret, BaseCombatEntity entity)
        {
            if (entity == null || turret == null) return null;
            FriendData targetPlayer;
            var targetplayer = entity as BasePlayer;
            if (targetplayer == null) return null;
            if (!friendData.TryGetValue(turret.OwnerID, out targetPlayer)) return null;
            FriendData.FriendAcces ss;
            var owner = turret.authorizedPlayers.Exists(p => p.userid == turret.OwnerID);
            if (!owner) return null;
            if (!targetPlayer.friendList.TryGetValue(targetplayer.userID, out ss)) return null;
            if (!ss.Turret) return null;
            return false;
        }

        private object CanUseLockedEntity(BasePlayer player, BaseLock baseLock)
        {
            if (player == null || baseLock == null) return null;
            FriendData targetPlayer2;
            if (!friendData.TryGetValue(baseLock.OwnerID, out targetPlayer2)) return null;
            FriendData.FriendAcces ss;
            if (!targetPlayer2.friendList.TryGetValue(player.userID, out ss)) return null;
            if (!ss.Door) return null;
            return true;
        }

        private object OnSamSiteTarget(SamSite entity, BaseCombatEntity target)
        {
            if (!cfg.SSamOn) return null;
            if (entity == null || target == null) return null;
            FriendData targetPlayer;
            var targetpcopter = target as MiniCopter;
            if (targetpcopter == null) return null;
            var build = entity.GetBuildingPrivilege();
            if (build == null) return null;
            if (!build.authorizedPlayers.Exists(p => p.userid == entity.OwnerID)) return null;
            var targePlayer = entity.currentTarget.GetComponentsInChildren<BaseVehicleSeat>()[0]._mounted;
            if (targePlayer == null) return null;
            if (entity.OwnerID == targePlayer.userID) return false;
            if (!friendData.TryGetValue(entity.OwnerID, out targetPlayer)) return null;
            FriendData.FriendAcces ss;
            if (!targetPlayer.friendList.TryGetValue(targePlayer.userID, out ss)) return null;
            if (!ss.Sam) return null;
            return false;
        }

        private void OnPlayerInit(BasePlayer player)
        {
            FriendData t;
            if (friendData.TryGetValue(player.userID, out t)) return;
            friendData.Add(player.userID, new FriendData() {Name = player.displayName, friendList = { }});
        }

        private void OnServerInitialized()
        {
            friendData =
                Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, FriendData>>("SoFriends/FriendData");
            foreach (var basePlayer in BasePlayer.activePlayerList)
                OnPlayerInit(basePlayer);
        }

        private void Unload()
        {
            Interface.Oxide.DataFileSystem.WriteObject("SoFriends/FriendData", friendData);
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(basePlayer, LayerInvite);
                CuiHelper.DestroyUi(basePlayer, Layer);
            }
        }

        #endregion

        #region [UI]

        private static string Layer = "UISoFriends";
        private string LayerInvite = "UISoFriendsInv";
        private string Hud = "Overlay";
        private string Overlay = "Overlay";

        private CuiPanel MainFon = new CuiPanel()
        {
            RectTransform =
                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-1920 -1080", OffsetMax = "1920 1080"},
            CursorEnabled = true,
            Image = {Color = "0 0 0 0"}
        };

        private CuiPanel MainPanel = new CuiPanel()
        {
            RectTransform = {AnchorMin = "0.4482732 0.4547905", AnchorMax = "0.5465654 0.6021605"},
            Image = {Color = "0.75 0.75 0.75 0.30", Material = "assets/content/ui/uibackgroundblur.mat"}
        };

        private CuiButton ButtonList = new CuiButton()
        {
            RectTransform = {AnchorMin = "0.03265451 0.9999675", AnchorMax = "0.3019271 1.062829"},
            Button =
            {
                Color = "0.75 0.75 0.75 0.30", Command = "chat.say /fmenu",
                Sprite = "assets/content/ui/ui.background.tile.psd", Material = "assets/content/ui/uibackgroundblur.mat"
            },
            Text =
            {
                Text = "СПИСОК ДРУЗЕЙ", FontSize = 12, Align = TextAnchor.MiddleCenter,
                Font = "robotocondensed-regular.ttf"
            }
        };

        private CuiButton SettingText = new CuiButton()
        {
            RectTransform = {AnchorMin = "0.3028903 0.9148316", AnchorMax = "0.6958004 0.9892823"},
            Button = {Color = "0 0 0 0"},
            Text =
            {
                Text = "НАСТРОЙКИ ДРУЗЕЙ", Color = "1 1 1 1", Align = TextAnchor.MiddleCenter,
                Font = "robotocondensed-regular.ttf"
            }
        };

        private CuiButton AddFriends = new CuiButton()
        {
            RectTransform = {AnchorMin = "0.6967639 0.9999675", AnchorMax = "0.9660323 1.062829"},
            Button =
            {
                Color = "0.75 0.75 0.75 0.30", Command = "friendui addfriend",
                Sprite = "assets/content/ui/ui.background.tile.psd", Material = "assets/content/ui/uibackgroundblur.mat"
            },
            Text =
            {
                Text = "ДОБАВИТЬ ДРУГА", FontSize = 12, Align = TextAnchor.MiddleCenter,
                Font = "robotocondensed-regular.ttf"
            }
        };

        private CuiButton ClosePanel = new CuiButton()
        {
            RectTransform = {AnchorMin = "0.3036932 -0.05973285", AnchorMax = "0.6958004 -0.001496568"},
            Button =
            {
                Color = "0.73 0.00 0.00 0.80", Close = Layer, Sprite = "assets/content/ui/ui.background.tile.psd",
                Material = "assets/content/ui/uibackgroundblur.mat"
            },
            Text = {Text = "ЗАКРЫТЬ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf"}
        };

        private CuiPanel Invite = new CuiPanel()
        {
            RectTransform = {AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-200 110", OffsetMax = "180 130"},
            Image = {Color = HexToRustFormat("#4A37FFCC")}
        };

        private CuiButton DenyButton = new CuiButton()
        {
            RectTransform = {AnchorMin = "0.552987 -0.9999999", AnchorMax = "0.9988059 -0.05645192"},
            Button = {Color = HexToRustFormat("#B00000CC"), Command = "friend deny"},
            Text = {Text = "ОТКЛОНИТЬ", Color = HexToRustFormat("#DEDEDEFF"), Align = TextAnchor.MiddleCenter}
        };

        private CuiButton AcceptButton = new CuiButton()
        {
            RectTransform = {AnchorMin = "-0.0014289 -0.9999999", AnchorMax = "0.4443915 -0.05645192"},
            Button = {Color = HexToRustFormat("#00A719CC"), Command = "friend accept"},
            Text = {Text = "ПРИНЯТЬ", Color = HexToRustFormat("#DEDEDEFF"), Align = TextAnchor.MiddleCenter}
        };

        private CuiPanel StaticSetPanel = new CuiPanel()
        {
            RectTransform = {AnchorMin = "1.00315 0", AnchorMax = "1.368669 0.995"},
            Image =
            {
                Color = "1.00 0.50 0.00 0.30", Sprite = "assets/content/ui/ui.background.tile.psd",
                Material = "assets/content/ui/uibackgroundblur.mat"
            }
        };

        private void InivteStart(BasePlayer inviter, BasePlayer target)
        {
            CuiHelper.DestroyUi(target, LayerInvite);
            var cont = new CuiElementContainer();
            cont.Add(Invite, Overlay, LayerInvite);
            cont.Add(new CuiElement()
            {
                Parent = LayerInvite,
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = $"У ВАС ЗАЯВКА В ДРУЗЬЯ ОТ {inviter.displayName}", Color = HexToRustFormat("#DEDEDEFF"),
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent() {AnchorMin = "0 0", AnchorMax = "1 1"},
                }
            });
            cont.Add(AcceptButton, LayerInvite);
            cont.Add(DenyButton, LayerInvite);
            CuiHelper.AddUi(target, cont);
        }

        [ChatCommand("fmenu")]
        private void StartUi(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            var cont = new CuiElementContainer();
            cont.Add(MainFon, Overlay, Layer);
            MainPanel.Image.Color = "0.75 0.75 0.75 0.30";
            cont.Add(MainPanel, Layer, Layer + "Panel");
            cont.Add(ButtonList, Layer + "Panel");
            cont.Add(AddFriends, Layer + "Panel");
            cont.Add(ClosePanel, Layer + "Panel");
            cont.Add(SettingText, Layer + "Panel");
            CuiHelper.AddUi(player, cont);
            FriendsInit(player, 1);
        }

        [ConsoleCommand("friendui")]
        private void FriendUI(ConsoleSystem.Arg arg)
        {
            var targetPlayer = arg?.Player();
            if (targetPlayer == null) return;
            if (arg.Args == null || arg.Args.Length < 1)
            {
                StartUi(arg.Player());
                return;
            }

            switch (arg.Args[0])
            {
                case "page":
                    if (arg.Args[1].ToInt() < 1) return;
                    FriendsInit(targetPlayer, arg.Args[1].ToInt());
                    break;
                case "page2":
                    if (arg.Args[1].ToInt() < 1) return;
                    AddFriendList(targetPlayer, arg.Args[1].ToInt());
                    break;
                case "setting":
                    SettingInit(targetPlayer, ulong.Parse(arg.Args[1]));
                    break;
                case "addfriend":
                    AddFriendList(targetPlayer, 1);
                    break;
            }
        }

        private void AddFriendList(BasePlayer player, int page)
        {
            FriendData.FriendAcces access;
            FriendData target;
            if (!friendData.TryGetValue(player.userID, out target)) return;
            CuiHelper.DestroyUi(player, Layer + "Set");
            CuiHelper.DestroyUi(player, Layer + "Page");
            CuiHelper.DestroyUi(player, Layer + "ToPanel");
            var cont = new CuiElementContainer();
            cont.Add(MainPanel, Layer, Layer + "ToPanel");
            cont.Add(new CuiElement()
            {
                Parent = Layer + "ToPanel",
                Name = Layer + "Page",
                Components =
                {
                    new CuiImageComponent() {Color = HexToRustFormat("#6527FFDC")},
                    new CuiRectTransformComponent()
                        {AnchorMin = "0.3036932 0.01282128", AnchorMax = "0.6958004 0.07849312"}
                }
            });
            cont.Add(new CuiElement()
            {
                Parent = Layer + "Page",
                Components =
                {
                    new CuiTextComponent() {Text = $"{page}", Align = TextAnchor.MiddleCenter},
                    new CuiRectTransformComponent() {AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });
            cont.Add(new CuiButton()
            {
                RectTransform = {AnchorMin = "-0.1801539 0.09259259", AnchorMax = "-0.04501678 0.9444836"},
                Text = {Text = "«", Align = TextAnchor.MiddleCenter, FontSize = 14},
                Button = {Color = HexToRustFormat("#6527FFDC"), Command = $"friendui page2 {page - 1}"}
            }, Layer + "Page");
            cont.Add(new CuiButton()
            {
                RectTransform = {AnchorMin = "1.045088 0.09259259", AnchorMax = "1.180225 0.9444836"},
                Text = {Text = "»", Align = TextAnchor.MiddleCenter, FontSize = 14},
                Button = {Color = HexToRustFormat("#6527FFDC"), Command = $"friendui page2 {page + 1}"}
            }, Layer + "Page");
            foreach (var players in BasePlayer.activePlayerList.Where(p => p.displayName != player.displayName)
                .Where(d => !target.friendList.ContainsKey(d.userID))
                .Select((i, t) => new {A = i, B = t - (page - 1) * 12}).Skip((page - 1) * 12).Take(12))
            {
                cont.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin =
                            $"{0.04501814 + players.B * 0.53 - Math.Floor((double) players.B / 2) * 2 * 0.53} {0.7930095 - Math.Floor((double) players.B / 2) * 0.13}",
                        AnchorMax =
                            $"{0.4202788 + players.B * 0.53 - Math.Floor((double) players.B / 2) * 2 * 0.53} {0.9039272 - Math.Floor((double) players.B / 2) * 0.13}"
                    },
                    Button =
                    {
                        Color = HexToRustFormat("#9B7AFFFF"), Command = $"friend add {players.A.userID}"
                    },
                    Text = {Text = $"{players.A.displayName}", Align = TextAnchor.MiddleCenter}
                }, Layer + "ToPanel", Layer + "Players" + players.B);
            }

            CuiHelper.AddUi(player, cont);
        }

        private void SettingInit(BasePlayer player, ulong steamdIdTarget)
        {
            FriendData.FriendAcces access;
            FriendData target;
            if (!friendData.TryGetValue(player.userID, out target)) return;
            if (!target.friendList.TryGetValue(steamdIdTarget, out access)) return;
            CuiHelper.DestroyUi(player, Layer + "Set");
            var cont = new CuiElementContainer();
            cont.Add(StaticSetPanel, Layer + "Panel", Layer + "Set");
            cont.Add(new CuiButton()
            {
                RectTransform = {AnchorMin = "0.1052236 -0.06182712", AnchorMax = "0.9130446 -0.003809161"},
                Button =
                {
                    Color = "0.73 0.00 0.00 0.80", Command = $"friend remove {steamdIdTarget}",
                    Sprite = "assets/content/ui/ui.background.tile.psd",
                    Material = "assets/content/ui/uibackgroundblur.mat"
                },
                Text =
                {
                    Text = "УДАЛИТЬ ИЗ ДРУЗЕЙ", Align = TextAnchor.MiddleCenter, FontSize = 12,
                    Font = "robotocondensed-regular.ttf"
                }
            }, Layer + "Set");
            if (access.Damage)
            {
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "Set",
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Color = "0 0 0 0.2", Sprite = "assets/content/ui/ui.background.tile.psd",
                            Material = "assets/content/ui/uibackgroundblur.mat"
                        },
                        new CuiOutlineComponent() {Color = "0.22 0.79 0.20 1.00", Distance = "1.5 1.5"},
                        new CuiRectTransformComponent()
                            {AnchorMin = "0.1052236 0.8764334", AnchorMax = "0.9130446 0.9470162"}
                    }
                });
                cont.Add(new CuiButton()
                {
                    RectTransform = {AnchorMin = "0.1052236 0.8764334", AnchorMax = "0.9130446 0.9470162"},
                    Button = {Color = "0 0 0 0", Command = $"friend set damage {steamdIdTarget}"},
                    Text = {Text = "УРОН", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf"}
                }, Layer + "Set");
            }
            else
            {
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "Set",
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Color = "0 0 0 0.2", Sprite = "assets/content/ui/ui.background.tile.psd",
                            Material = "assets/content/ui/uibackgroundblur.mat"
                        },
                        new CuiOutlineComponent() {Color = "0.79 0.20 0.20 1.00", Distance = "1.5 1.5"},
                        new CuiRectTransformComponent()
                            {AnchorMin = "0.1052236 0.8764334", AnchorMax = "0.9130446 0.9470162"}
                    }
                });
                cont.Add(new CuiButton()
                {
                    RectTransform = {AnchorMin = "0.1052236 0.8764334", AnchorMax = "0.9130446 0.9470162"},
                    Button = {Color = "0 0 0 0", Command = $"friend set damage {steamdIdTarget}"},
                    Text = {Text = "УРОН", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf"}
                }, Layer + "Set");
            }

            if (access.Door)
            {
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "Set",
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Color = "0 0 0 0.2", Sprite = "assets/content/ui/ui.background.tile.psd",
                            Material = "assets/content/ui/uibackgroundblur.mat"
                        },
                        new CuiOutlineComponent() {Color = "0.22 0.79 0.20 1.00", Distance = "1.5 1.5"},
                        new CuiRectTransformComponent()
                            {AnchorMin = "0.1052236 0.784283", AnchorMax = "0.9130446 0.85486587"}
                    }
                });
                cont.Add(new CuiButton()
                {
                    RectTransform = {AnchorMin = "0.1052236 0.784283", AnchorMax = "0.9130446 0.85486587"},
                    Button = {Color = "0 0 0 0", Command = $"friend set door {steamdIdTarget}"},
                    Text = {Text = "ДВЕРИ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf"}
                }, Layer + "Set");
            }
            else
            {
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "Set",
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Color = "0 0 0 0.2", Sprite = "assets/content/ui/ui.background.tile.psd",
                            Material = "assets/content/ui/uibackgroundblur.mat"
                        },
                        new CuiOutlineComponent() {Color = "0.79 0.20 0.20 1.00", Distance = "1.5 1.5"},
                        new CuiRectTransformComponent()
                            {AnchorMin = "0.1052236 0.784283", AnchorMax = "0.9130446 0.8548658"}
                    }
                });
                cont.Add(new CuiButton()
                {
                    RectTransform = {AnchorMin = "0.1052236 0.784283", AnchorMax = "0.9130446 0.8548658"},
                    Button = {Color = "0 0 0 0", Command = $"friend set door {steamdIdTarget}"},
                    Text = {Text = "ДВЕРИ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf"}
                }, Layer + "Set");
            }

            if (access.Turret)
            {
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "Set",
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Color = "0 0 0 0.2", Sprite = "assets/content/ui/ui.background.tile.psd",
                            Material = "assets/content/ui/uibackgroundblur.mat"
                        },
                        new CuiOutlineComponent() {Color = "0.22 0.79 0.20 1.00", Distance = "1.5 1.5"},
                        new CuiRectTransformComponent()
                            {AnchorMin = "0.1052236 0.6879439", AnchorMax = "0.9130446 0.7585267"}
                    }
                });
                cont.Add(new CuiButton()
                {
                    RectTransform = {AnchorMin = "0.1052236 0.6879439", AnchorMax = "0.9130446 0.7585267"},
                    Button = {Color = "0 0 0 0", Command = $"friend set turret {steamdIdTarget}"},
                    Text = {Text = "ТУРЕЛИ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf"}
                }, Layer + "Set");
            }
            else
            {
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "Set",
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Color = "0 0 0 0.2", Sprite = "assets/content/ui/ui.background.tile.psd",
                            Material = "assets/content/ui/uibackgroundblur.mat"
                        },
                        new CuiOutlineComponent() {Color = "0.79 0.20 0.20 1.00", Distance = "1.5 1.5"},
                        new CuiRectTransformComponent()
                            {AnchorMin = "0.1052236 0.6879439", AnchorMax = "0.9130446 0.7585267"}
                    }
                });
                cont.Add(new CuiButton()
                {
                    RectTransform = {AnchorMin = "0.1052236 0.6879439", AnchorMax = "0.9130446 0.7585267"},
                    Button = {Color = "0 0 0 0", Command = $"friend set turret {steamdIdTarget}"},
                    Text = {Text = "ТУРЕЛИ", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf"}
                }, Layer + "Set");
            }

            if (cfg.SSamOn)
            {
                if (access.Sam)
                {
                    cont.Add(new CuiElement()
                    {
                        Parent = Layer + "Set",
                        Components =
                        {
                            new CuiImageComponent()
                            {
                                Color = "0 0 0 0.2", Sprite = "assets/content/ui/ui.background.tile.psd",
                                Material = "assets/content/ui/uibackgroundblur.mat"
                            },
                            new CuiOutlineComponent() {Color = "0.22 0.79 0.20 1.00", Distance = "1.5 1.5"},
                            new CuiRectTransformComponent()
                                {AnchorMin = "0.1052236 0.5916048", AnchorMax = "0.9130446 0.6621876"}
                        }
                    });
                    cont.Add(new CuiButton()
                    {
                        RectTransform = {AnchorMin = "0.1052236 0.5916048", AnchorMax = "0.9130446 0.6621876"},
                        Button =
                        {
                            Color = "0 0 0 0", Command = $"friend set sam {steamdIdTarget}"
                        },
                        Text = {Text = "ПВО", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf"}
                    }, Layer + "Set");
                }
                else
                {
                    cont.Add(new CuiElement()
                    {
                        Parent = Layer + "Set",
                        Components =
                        {
                            new CuiImageComponent()
                            {
                                Color = "0 0 0 0.2", Sprite = "assets/content/ui/ui.background.tile.psd",
                                Material = "assets/content/ui/uibackgroundblur.mat"
                            },
                            new CuiOutlineComponent() {Color = "0.79 0.20 0.20 1.00", Distance = "1.5 1.5"},
                            new CuiRectTransformComponent()
                                {AnchorMin = "0.1052236 0.5916048", AnchorMax = "0.9130446 0.6621876"}
                        }
                    });
                    cont.Add(new CuiButton()
                    {
                        RectTransform = {AnchorMin = "0.1052236 0.5916048", AnchorMax = "0.9130446 0.6621876"},
                        Button =
                        {
                            Color = "0 0 0 0", Command = $"friend set sam {steamdIdTarget}"
                        },
                        Text = {Text = "ПВО", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf"}
                    }, Layer + "Set");
                }
            }

            CuiHelper.AddUi(player, cont);
        }

        private void FriendsInit(BasePlayer player, int page)
        {
            CuiHelper.DestroyUi(player, Layer + "Page");
            var cont = new CuiElementContainer();
            FriendData targetPlayer;
            if (!friendData.TryGetValue(player.userID, out targetPlayer)) return;
            cont.Add(new CuiElement()
            {
                Parent = Layer + "Panel",
                Name = Layer + "Page",
                Components =
                {
                    new CuiImageComponent() {Color = HexToRustFormat("#6527FFDC")},
                    new CuiRectTransformComponent()
                        {AnchorMin = "0.3036932 0.01282128", AnchorMax = "0.6958004 0.07849312"}
                }
            });
            cont.Add(new CuiElement()
            {
                Parent = Layer + "Page",
                Components =
                {
                    new CuiTextComponent() {Text = $"{page}", Align = TextAnchor.MiddleCenter},
                    new CuiRectTransformComponent() {AnchorMin = "0 0", AnchorMax = "1 1"}
                }
            });
            cont.Add(new CuiButton()
            {
                RectTransform = {AnchorMin = "-0.1801539 0.09259259", AnchorMax = "-0.04501678 0.9444836"},
                Text = {Text = "«", Align = TextAnchor.MiddleCenter, FontSize = 14},
                Button = {Color = HexToRustFormat("#6527FFDC"), Command = $"friendui page {page - 1}"}
            }, Layer + "Page");
            cont.Add(new CuiButton()
            {
                RectTransform = {AnchorMin = "1.045088 0.09259259", AnchorMax = "1.180225 0.9444836"},
                Text = {Text = "»", Align = TextAnchor.MiddleCenter, FontSize = 14},
                Button = {Color = HexToRustFormat("#6527FFDC"), Command = $"friendui page {page + 1}"}
            }, Layer + "Page");
            foreach (var friends in targetPlayer.friendList.Select((i, t) => new {A = i, B = t}))
                CuiHelper.DestroyUi(player, Layer + "Panel" + friends.B);
            foreach (var friends in targetPlayer.friendList.Select((i, t) => new {A = i, B = t - (page - 1) * 4})
                .Skip((page - 1) * 4).Take(4))
            {
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "Panel",
                    Name = Layer + "Panel" + friends.B,
                    Components =
                    {
                        new CuiRawImageComponent() {Png = GetImage(friends.A.Key.ToString())},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"0.05738201 {0.7214538 - Math.Floor((double) friends.B) * 0.20}",
                            AnchorMax = $"0.1924198 {0.8776053 - Math.Floor((double) friends.B) * 0.20}",
                        }
                    }
                });
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "Panel" + friends.B,
                    Components =
                    {
                        new CuiImageComponent()
                            {Color = "1 1 1 0", Material = "assets/content/ui/uibackgroundblur.mat"},
                        new CuiOutlineComponent() {Distance = "1 1.5", Color = HexToRustFormat("#9B7AFFFF")},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"0 0",
                            AnchorMax = $"0.98 0.98",
                        }
                    }
                });
                var friend = BasePlayer.FindByID(friends.A.Key);
                if (friend == null)
                {
                    cont.Add(new CuiElement()
                    {
                        Parent = Layer + "Panel" + friends.B,
                        Name = Layer + "Panel" + friends.B + "Info",
                        Components =
                        {
                            new CuiTextComponent()
                                {Text = $"OFFLINE", Align = TextAnchor.MiddleCenter, FontSize = 8, Color = "1 0 0 1"},
                            new CuiRectTransformComponent()
                                {AnchorMin = "0.01284048 1.012957", AnchorMax = "0.9869189 1.207408"}
                        }
                    });
                }
                else
                {
                    cont.Add(new CuiElement()
                    {
                        Parent = Layer + "Panel" + friends.B,
                        Name = Layer + "Panel" + friends.B + "Info",
                        Components =
                        {
                            new CuiTextComponent()
                                {Text = $"ONLINE", Align = TextAnchor.MiddleCenter, FontSize = 8, Color = "0 1 0 1"},
                            new CuiRectTransformComponent()
                                {AnchorMin = "0.01284048 1.012957", AnchorMax = "0.9869189 1.207408"}
                        }
                    });
                }

                cont.Add(new CuiElement()
                {
                    Parent = Layer + "Panel" + friends.B,
                    Name = Layer + "Panel" + friends.B + "Info",
                    Components =
                    {
                        new CuiImageComponent() {Color = HexToRustFormat("#9B7AFFFF")},
                        new CuiRectTransformComponent()
                            {AnchorMin = "1.006899 0.0925926", AnchorMax = "6.781211 0.8962978"}
                    }
                });
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "Panel" + friends.B,
                    Name = Layer + "Panel" + friends.B + "Info",
                    Components =
                    {
                        new CuiImageComponent() {Color = HexToRustFormat("#9B7AFFFF")},
                        new CuiRectTransformComponent()
                            {AnchorMin = "1.006899 0.0925926", AnchorMax = "6.781211 0.8962978"}
                    }
                });
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "Panel" + friends.B + "Info",
                    Components =
                    {
                        new CuiTextComponent() {Text = $"{friends.A.Value.name}", Align = TextAnchor.MiddleCenter},
                        new CuiRectTransformComponent()
                            {AnchorMin = "0.05208334 0.2419427", AnchorMax = "0.6715539 0.7419391"}
                    }
                });
                cont.Add(new CuiButton()
                {
                    RectTransform = {AnchorMin = "0.8639457 0.0925926", AnchorMax = "0.9773486 0.9032296"},
                    Button =
                    {
                        Color = "1 1 1 1", Sprite = "assets/icons/gear.png",
                        Command = $"friendui setting {friends.A.Key}"
                    },
                    Text = {Text = string.Empty}
                }, Layer + "Panel" + friends.B + "Info");
            }

            CuiHelper.AddUi(player, cont);
        }

        #endregion

        #region [Help]

        [PluginReference] private Plugin ImageLibrary;

        public string GetImage(string shortname, ulong skin = 0) =>
            (string) ImageLibrary.Call("GetImage", shortname, skin);

        private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex)) hex = "#FFFFFFFF";
            var str = hex.Trim('#');
            if (str.Length == 6) str += "FF";
            if (str.Length != 8)
            {
                throw new Exception(hex);
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);
            Color color = new Color32(r, g, b, a);
            return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
        }

        #endregion
    }
}