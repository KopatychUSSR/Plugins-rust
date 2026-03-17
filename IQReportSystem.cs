using System;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using System.Globalization;
using Oxide.Core.Libraries.Covalence;
using ConVar;

namespace Oxide.Plugins
{
    [Info("IQReportSystem", "TopPlugin.ru", "0.0.1")]
    class IQReportSystem : RustPlugin
    {
        #region Reference
        [PluginReference] Plugin GameWerAC, ImageLibrary, MultiFighting, IQChat,Friends;
        public string GetImage(string shortname, ulong skin = 0) => (string)ImageLibrary?.Call("GetImage", shortname, skin);
        public bool AddImage(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname, skin);
        #endregion

        #region Vars

        #region Permission
        string PermissionModeration = "reportsystemrevolution.moderation";
        string PermissionAdmin = "reportsystemrevolution.admin";
        #endregion

        #region Lists
        public Dictionary<ulong, int> CooldownPC = new Dictionary<ulong, int>();
        public Dictionary<ulong, PlayerSaveCheckClass> PlayerSaveCheck = new Dictionary<ulong, PlayerSaveCheckClass>();
        public class PlayerSaveCheckClass
        {
            public string Discord;
            public string NickName;
            public string StatusNetwork;

            public ulong ModeratorID;
        }
        #endregion

        #endregion

        #region Configuration
        private static Configuration config = new Configuration();
        public class Configuration
        {
            [JsonProperty("Основные настройки")]
            public Settings Setting = new Settings();
            [JsonProperty("Причины репорта")]
            public List<string> ReasonReport = new List<string>();
            [JsonProperty("Причины блокировки")] 
            public List<BanReason> ReasonBan = new List<BanReason>();
            internal class BanReason
            {
                [JsonProperty("Название")]
                public string DisplayName;
                [JsonProperty("Команда")]
                public string Command;
            }

            internal class Settings
            {
                [JsonProperty("Настройки IQChat")]
                public ChatSettings ChatSetting = new ChatSettings();
                [JsonProperty("Настройки интерфейса")]
                public InterfaceSetting Interface = new InterfaceSetting();

                [JsonProperty("Максимальное количество репортов")]
                public int MaxReport;
                [JsonProperty("Перезарядка для отправки репорта(секунды)")]
                public int CooldownTime;
                [JsonProperty("Запретить друзьям репортить друг друга")]
                public bool FriendNoReport;
                [JsonProperty("Включить логирование в беседу ВК")]
                public bool VKMessage;
                [JsonProperty("Настройки ВК")]
                public VKSetting VKSettings = new VKSetting();

                internal class ChatSettings
                {
                    [JsonProperty("IQChat : Кастомный префикс в чате")]
                    public string CustomPrefix;
                    [JsonProperty("IQChat : Кастомный аватар в чате(Если требуется)")]
                    public string CustomAvatar;
                }
                internal class VKSetting
                {
                    [JsonProperty("Токен от группы ВК(От группы будут идти сообщения в беседу.Вам нужно добавить свою группу в беседу!)")]
                    public string Token;
                    [JsonProperty("ID беседы для группы")]
                    public string ChatID;
                }
                internal class InterfaceSetting
                {
                    [JsonProperty("[Меню модератора]HEX : Цвет для игрока,когда репортов < 6")]
                    public string HexMiimum;
                    [JsonProperty("[Меню модератора]HEX : Цвет для игрока,когда репортов < 8")]
                    public string HexSmall;
                    [JsonProperty("[Меню модератора]HEX : Цвет для игрока,когда репортов > 8")]
                    public string HexMaximum;
                    [JsonProperty("[Меню модератора]HEX : Цвет для уведомления для игрока")]
                    public string AlertHex;
                }
            }

            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    Setting = new Settings
                    {
                        FriendNoReport = false,
                        MaxReport = 3,
                        CooldownTime = 600,
                        VKMessage = true,
                        ChatSetting = new Settings.ChatSettings
                        {
                            CustomAvatar = "",
                            CustomPrefix = ""
                        },
                        VKSettings = new Settings.VKSetting
                        {
                            Token = "",
                            ChatID = ""
                        },
                        Interface = new Settings.InterfaceSetting
                        {
                            HexMiimum = "#47AF5DFF",
                            HexSmall = "#B65E35FF",
                            HexMaximum = "#CE4646FF",
                            AlertHex = "#c46e32"
                        }
                    },
                    ReasonReport = new List<string>
                    {
                        "Использование читов",
                        "Макросы",
                        "Игра 3+",                      
                    },
                    ReasonBan = new List<BanReason>
                    { 
                        new BanReason
                        {
                            DisplayName = "Использование читов",
                            Command = "ban {0} soft",
                        },
                        new BanReason
                        {
                            DisplayName = "Макросы",
                            Command = "ban {0} 30d macros",
                        },
                        new BanReason
                        {
                            DisplayName = "Игра 3+",
                            Command = "ban {0} 14d 3+",
                        },
                        new BanReason
                        {
                            DisplayName = "Отказ",
                            Command = "ban {0} 7d otkaz",
                        },
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning($"Ошибка чтения конфигурации #93 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Data
        public Dictionary<ulong, PlayerInfo> ReportInformation = new Dictionary<ulong, PlayerInfo>();
        public Dictionary<ulong, ModeratorInfo> ModeratorInformation = new Dictionary<ulong, ModeratorInfo>();
        public class PlayerInfo
        {
            public string DisplayName;
            public List<string> IP;
            public string LastReport;
            public string LastCheckModerator;
            public int CheckCount;
            public List<string> ReportHistory;
            public int ReportCount;
            public string GameStatus;
        }

        public class ModeratorInfo
        {
            public Dictionary<string, string> CheckPlayerModerator = new Dictionary<string, string>();
            public Dictionary<string, string> BanPlayerModerator = new Dictionary<string, string>();
            public int CheckCount;
        }
        #endregion

        #region Metods

        #region MetodsReport

        void Metods_PlayerConnected(BasePlayer player)
        {
            if (!ReportInformation.ContainsKey(player.userID))
            {
                PlayerInfo pInfo = new PlayerInfo
                {
                    DisplayName = player.displayName,
                    IP = new List<string>(),
                    LastReport = "",
                    LastCheckModerator = "",
                    CheckCount = 0,
                    ReportCount = 0,
                    GameStatus = IsSteam(player.UserIDString),
                    ReportHistory = new List<string>(),
                };
                ReportInformation.Add(player.userID, pInfo);
            }
            else
            {
                var User = ReportInformation[player.userID];
                var IP = covalence.Players.FindPlayerById(player.UserIDString).Address;

                User.GameStatus = IsSteam(player.UserIDString);
                if (!User.IP.Contains(IP))
                    User.IP.Add(IP);
            }

            if (permission.UserHasPermission(player.UserIDString, PermissionModeration))
            {
                if (!ModeratorInformation.ContainsKey(player.userID))
                {
                    ModeratorInfo mInfo = new ModeratorInfo
                    {
                        CheckCount = 0,
                        BanPlayerModerator = new Dictionary<string, string>(),
                        CheckPlayerModerator = new Dictionary<string, string>(),
                    };
                    ModeratorInformation.Add(player.userID, mInfo);
                }
            }
            Metods_StatusNetwork(player, lang.GetMessage("NETWORD_STATUS_ONLINE",this,player.UserIDString));
        }

        void Metods_Report(BasePlayer target, int ReasonIndex)
        {
            if (permission.UserHasPermission(target.UserIDString, PermissionAdmin))
                return;

            if (IsSteam(target.UserIDString) == lang.GetMessage("IS_STEAM_STATUS_PIRATE",this,target.UserIDString))
            {
                if (GameWerAC != null)
                {
                    GameWerAC.Call("GetScreenReport", target);
                    Puts("Выполнен скриншот экрана для пирата");
                }
            }

            string ReasonReport = config.ReasonReport[ReasonIndex];

            var User = ReportInformation[target.userID];
            User.ReportCount++;
            User.LastReport = ReasonReport;
            User.ReportHistory.Insert(0, ReasonReport);

            VKSendMessage(String.Format(lang.GetMessage("METODS_SEND_REPORT_VK", this), target.displayName,target.UserIDString,ReasonReport)); 

            if (User.ReportCount >= config.Setting.MaxReport)
            {
                foreach (var MList in BasePlayer.activePlayerList)
                    if (permission.UserHasPermission(MList.UserIDString, PermissionModeration))
                        SendChat(MList, String.Format(lang.GetMessage("METODS_HELP_MODERS",this,MList.UserIDString),target.displayName,User.ReportCount));
                VKSendMessage(String.Format(lang.GetMessage("METODS_HELP_MODERS_VK",this),target.displayName, User.ReportCount));
            }
        }

        #endregion

        #region MetodsCooldown
        void Metods_GiveCooldown(ulong ID,  int cooldown)
        {
            CooldownPC[ID] = cooldown + (int)CurrentTime();          
        }

        bool Metods_GetCooldown(ulong ID)
        {
            if (!CooldownPC.ContainsKey(ID) || Math.Max(0, CooldownPC[ID]) < 1 || CooldownPC[ID] <= (int)CurrentTime())
                return false;
            else return true;
        }

        #endregion

        #region MetodsModeration

        void Metods_CheckModeration(BasePlayer Suspect, BasePlayer Moderator)
        {
            if(PlayerSaveCheck.ContainsKey(Suspect.userID))
            {
                SendChat(Moderator, lang.GetMessage("PLAYER_CHECKED", this));
                return;
            }
            SendChat(Moderator, String.Format(lang.GetMessage("METODS_MODER_START_CHECK",this),Suspect.displayName));
            VKSendMessage(String.Format(lang.GetMessage("METODS_MODER_START_CHECK_VK", this),Moderator.displayName,Moderator.UserIDString,Suspect.displayName,Suspect.UserIDString));           
            Metods_AFK(Suspect.userID, Moderator);
        }

        void Metods_CheckModerationFinish(BasePlayer moderator, ulong SuspectID)
        {
            IPlayer Suspect = covalence.Players.FindPlayerById(SuspectID.ToString());
            if (Suspect.IsConnected)
            {
                BasePlayer SOnline = BasePlayer.FindByID(ulong.Parse(Suspect.Id));
                CuiHelper.DestroyUi(SOnline, UI_PLAYER_ALERT);
                SendChat(SOnline, lang.GetMessage("MSG_CHECK_CHECK_STOP", this));
            }

            CuiHelper.DestroyUi(moderator, UI_MODERATION_CHECK_MENU);
            PlayerSaveCheck.Remove(ulong.Parse(Suspect.Id));

            var User = ReportInformation[ulong.Parse(Suspect.Id)];
            var Moderator = ModeratorInformation[moderator.userID];

            Moderator.CheckCount++;
            if (!Moderator.CheckPlayerModerator.ContainsKey(Suspect.Name))
                Moderator.CheckPlayerModerator.Add(Suspect.Name, User.LastReport);

            User.ReportCount = 0;
            User.ReportHistory.Clear();
            User.LastReport = lang.GetMessage("NON_REPORT",this);
            User.CheckCount++;
            User.LastCheckModerator = moderator.displayName;

            SendChat(moderator, lang.GetMessage("METODS_MODER_STOP_CHECK",this));
            VKSendMessage(String.Format(lang.GetMessage("METODS_MODER_STOP_CHECK_VK",this),moderator.displayName));
        }

        void Metods_StatusNetwork(BasePlayer Suspect, string Reason)
        {
            if (PlayerSaveCheck.ContainsKey(Suspect.userID))
            {
                if (Suspect.IsConnected)
                    if (Suspect.IsReceivingSnapshot)
                    {
                        timer.Once(3, () => Metods_StatusNetwork(Suspect, lang.GetMessage("NETWORD_STATUS_ONLINE", this, Suspect.UserIDString)));
                        return;
                    }

                PlayerSaveCheck[Suspect.userID].StatusNetwork = Reason;
                BasePlayer Moderator = BasePlayer.FindByID(PlayerSaveCheck[Suspect.userID].ModeratorID);

                CuiHelper.DestroyUi(Moderator, UI_MODERATION_CHECK_MENU_NETWORK);
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0.3589744", AnchorMax = "1 0.5625198" },
                    Text = { Text = $"{lang.GetMessage("UI_STATUS",this)} : {PlayerSaveCheck[Suspect.userID].StatusNetwork}", FontSize = 14, Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperCenter, Color = HexToRustFormat("#FFFFFFFF") }
                }, UI_MODERATION_CHECK_MENU, UI_MODERATION_CHECK_MENU_NETWORK);

                CuiHelper.AddUi(Moderator, container);
                UI_PlayerAlert(Suspect);             

                SendChat(Moderator, String.Format(lang.GetMessage("STATUS_CHANGED", this), Suspect.displayName, Reason));
                VKSendMessage(String.Format(lang.GetMessage("STATUS_CHANGED_VK", this), Suspect.displayName, Reason));
            }
        }

        public Timer ModerTimeOutTimer;
        void Metods_ModeratorExitCheck(BasePlayer Moderator)
        {
            foreach (var ModeratorCritical in PlayerSaveCheck)
                if (ModeratorCritical.Value.ModeratorID == Moderator.userID)
                {
                    IPlayer ModeratorOffline = covalence.Players.FindPlayerById(ModeratorCritical.Value.ModeratorID.ToString());
                    IPlayer Suspect = covalence.Players.FindPlayerById(ModeratorCritical.Key.ToString());
                    int TimeOutCount = 0;
                    ModerTimeOutTimer = timer.Repeat(5, 10, () =>
                        {
                            if (ModeratorOffline.IsConnected)
                            {
                                UI_ModerationCheckMenu(Moderator, ModeratorCritical.Key);
                                SendChat(Moderator, lang.GetMessage("MODERATOR_RETURN_WELCOME",this));
                                if (ModerTimeOutTimer != null)
                                {
                                    ModerTimeOutTimer.Destroy();
                                    ModerTimeOutTimer = null;
                                }
                                return;
                            }
                            else
                            {
                                TimeOutCount++;
                                if (TimeOutCount >= 10)
                                {
                                    PlayerSaveCheck.Remove(ModeratorCritical.Key);

                                    foreach (var OnlineModeration in BasePlayer.activePlayerList)
                                        if (permission.UserHasPermission(OnlineModeration.UserIDString, PermissionModeration))
                                            if (Suspect.IsConnected)
                                            {                                             
                                                BasePlayer SOnline = BasePlayer.FindByID(ulong.Parse(Suspect.Id));
                                                CuiHelper.DestroyUi(SOnline, UI_PLAYER_ALERT);
                                           
                                                SendChat(SOnline, String.Format(lang.GetMessage("MODERATOR_DISCONNECTED_STOP_CHECK",this),ModeratorOffline.Name));
                                                SendChat(OnlineModeration, String.Format(lang.GetMessage("MODERATOR_DISCONNECTED_STOP_RESEND",this),ModeratorOffline.Name,Suspect.Name));
                                                VKSendMessage(String.Format(lang.GetMessage("MODERATOR_DISCONNECTED_STOP_RESEND", this), ModeratorOffline.Name, Suspect.Name));

                                                if (ModerTimeOutTimer != null)
                                                {
                                                    ModerTimeOutTimer.Destroy();
                                                    ModerTimeOutTimer = null;
                                                }
                                            }
                                    return;
                                }
                            }
                        });

                }
        }

        void Metods_ModeratorBanneb(BasePlayer Moderator,ulong SuspectID, int i)
        {
            CuiHelper.DestroyUi(Moderator, UI_MODERATION_CHECK_MENU);
            IPlayer Suspect = covalence.Players.FindPlayerById(SuspectID.ToString());
            string Reason = config.ReasonBan[i].DisplayName;

            rust.RunClientCommand(Moderator, String.Format(config.ReasonBan[i].Command, SuspectID));
            PlayerSaveCheck.Remove(SuspectID);

            var ModeratorInfo = ModeratorInformation[Moderator.userID];
            ModeratorInfo.CheckCount++;
            if (!ModeratorInfo.CheckPlayerModerator.ContainsKey(Suspect.Name))
                ModeratorInfo.CheckPlayerModerator.Add(Suspect.Name, Reason);
            if (!ModeratorInfo.BanPlayerModerator.ContainsKey(Suspect.Name))
                ModeratorInfo.BanPlayerModerator.Add(Suspect.Name, Reason);

            SendChat(Moderator, String.Format(lang.GetMessage("MODERATOR_COMPLETED_CHECK", this), Reason));
            VKSendMessage(String.Format(lang.GetMessage("MODERATOR_COMPLETED_CHECK_VK", this), Moderator.displayName, Moderator.UserIDString, Suspect.Name, SuspectID, Reason, AFKCheck[SuspectID]));
        }

        #endregion

        #region MetodsAFK
        void Metods_CheckStopInAFK(BasePlayer moderator, string ID)
        {
            IPlayer Suspect = covalence.Players.FindPlayerById(ID);
            if (Suspect.IsConnected)
            {
                BasePlayer SOnline = BasePlayer.FindByID(ulong.Parse(Suspect.Id));
                CuiHelper.DestroyUi(SOnline, UI_PLAYER_ALERT);
            }
            CuiHelper.DestroyUi(moderator, UI_MODERATION_CHECK_MENU);
            PlayerSaveCheck.Remove(ulong.Parse(Suspect.Id));

            SendChat(moderator, lang.GetMessage("PLAYER_AFK_CHECK_STOP",this));
            VKSendMessage(String.Format(lang.GetMessage("PLAYER_AFK_CHECK_STOP_VK", this), moderator.displayName, moderator.userID, Suspect.Name));
        }

        public Dictionary<ulong,int> AFKCheck = new Dictionary<ulong, int>();
        void Metods_AFK(ulong SuspectID, BasePlayer moderator)
        {
            IPlayer Suspect = covalence.Players.FindPlayerById(SuspectID.ToString());
            if (!AFKCheck.ContainsKey(SuspectID))
                AFKCheck.Add(SuspectID, 0);
            else AFKCheck[SuspectID] = 0;

            int tryAFK = 0;
            SavePositionAFK(Suspect, moderator, tryAFK);
            timer.Repeat(5f, 6, () =>
            {
                SavePositionAFK(Suspect, moderator,tryAFK);
                tryAFK++;
            });
        }

        readonly Hash<string, GenericPosition> lastPosition = new Hash<string, GenericPosition>();
        void SavePositionAFK(IPlayer Suspect, BasePlayer moderator, int num)
        {
            var pPosition = Suspect.Position();
            if (!lastPosition.ContainsKey(Suspect.Id))
                lastPosition.Add(Suspect.Id, pPosition);
            else
            {
                if (lastPosition[Suspect.Id] != pPosition)
                    SendChat(moderator, String.Format(lang.GetMessage("PLAYER_AFK_CHANGE_POS",this),num));
                else
                {
                    SendChat(moderator, String.Format(lang.GetMessage("PLAYER_AFK_CHANGE_NO_POS", this), num));
                    AFKCheck[ulong.Parse(Suspect.Id)] += 1;
                }
                lastPosition[Suspect.Id] = pPosition;
            }

            if (num >= 5)
            {
                if (AFKCheck[ulong.Parse(Suspect.Id)] >= 3)
                    Metods_CheckStopInAFK(moderator, Suspect.Id);
                else
                {
                    BasePlayer SuspectOnline = BasePlayer.FindByID(ulong.Parse(Suspect.Id));
                    UI_PlayerAlert(SuspectOnline);
                    PlayerSaveCheck = new Dictionary<ulong, PlayerSaveCheckClass>
                    {
                        [SuspectOnline.userID] = new PlayerSaveCheckClass
                        {
                            Discord = lang.GetMessage("DISCORD_NULL",this),
                            NickName = SuspectOnline.displayName,
                            StatusNetwork = lang.GetMessage("NETWORD_STATUS_ONLINE", this, SuspectOnline.UserIDString),

                            ModeratorID = moderator.userID,
                        }
                    };
                    UI_ModerationCheckMenu(moderator, SuspectOnline.userID);
                    SendChat(moderator, lang.GetMessage("PLAYER_NON_AFK", this));
                }
            }
        }

        #endregion

        #endregion

        #region Command

        [ChatCommand("report")]
        void ReportChatCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null)
                return;

            if (args == null || args.Length == 0)
            {
                UI_PlayerInterface(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "menu":
                    {
                        if (permission.UserHasPermission(player.UserIDString, PermissionModeration))
                            UI_ModerationInterface(player);
                        return;
                    }
            }
        }

        [ConsoleCommand("report.list")]
        void ReportList(ConsoleSystem.Arg arg)
        {
            PrintError(lang.GetMessage("REPORT_LIST_CONSOLE",this));
            foreach (var List in BasePlayer.activePlayerList)
                if (ReportInformation[List.userID].ReportCount >= config.Setting.MaxReport)
                    PrintError($"{List.displayName} : {ReportInformation[List.userID].ReportCount}");

        }

        [ChatCommand("discord")]
        void SendDiscord(BasePlayer Suspect, string command, string[] args)
        {
            if (!PlayerSaveCheck.ContainsKey(Suspect.userID))
            {
                SendChat(Suspect, lang.GetMessage("MSG_CHECK_DISCORD", this));
                return;
            }
            string Discord = "";
            foreach (var arg in args)
                Discord += " " + arg;

            PlayerSaveCheck[Suspect.userID].Discord = Discord;

            SendChat(Suspect, String.Format(lang.GetMessage("MSG_DISCORD_SEND", this),Discord));
            VKSendMessage(String.Format(lang.GetMessage("DISCROD_VK_SEND", this), Suspect.displayName, Suspect.userID, Discord));

            BasePlayer Moderator = BasePlayer.FindByID(PlayerSaveCheck[Suspect.userID].ModeratorID);
            CuiHelper.DestroyUi(Moderator, UI_MODERATION_CHECK_MENU_DISCORD);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.5448718", AnchorMax = "1 0.7526907" },
                Text = { Text = $"Discord : {PlayerSaveCheck[Suspect.userID].Discord}", FontSize = 14, Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperCenter, Color = HexToRustFormat("#FFFFFFFF") }
            }, UI_MODERATION_CHECK_MENU, UI_MODERATION_CHECK_MENU_DISCORD);

            CuiHelper.AddUi(Moderator, container);
        }
       
        [ConsoleCommand("report")]
        void ReportCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (arg == null || arg.Args == null || arg.Args.Length == 0)
            {
                UI_PlayerInterface(player);
                return;
            }
            ulong SuspectID = ulong.Parse(arg.Args[1]);
            BasePlayer Suspect = BasePlayer.FindByID(ulong.Parse(arg.Args[1]));

            switch (arg.Args[0].ToLower())
            {
                case "select_reason":
                    {
                        UI_OpenPlayerSelectReason(player, Suspect.userID);
                        return;
                    }
                case "reported_suspect":
                    {
                        CuiHelper.DestroyUi(player, UI_PLAYER_PANEL);

                        if(Metods_GetCooldown(player.userID) == true)
                        {
                            SendChat(player,String.Format(lang.GetMessage("MSG_COOLDOWN", this), FormatTime(TimeSpan.FromSeconds(Math.Max(0, CooldownPC[player.userID] - CurrentTime())))));
                            return;
                        }

                        int ReasonIndex = Convert.ToInt32(arg.Args[2]);
                        string ReasonReport = config.ReasonReport[ReasonIndex];

                        Metods_Report(Suspect, ReasonIndex);
                        Metods_GiveCooldown(player.userID, config.Setting.CooldownTime);
                        SendChat(player, String.Format(lang.GetMessage("MSG_REPORTED_SUSPECT", this), Suspect.displayName, ReasonReport));
                        return;
                    }
                case "moderator_menu_open":
                    {
                        if (permission.UserHasPermission(player.UserIDString, PermissionModeration))
                            UI_ModerationInterface(player);
                        return;
                    }
                case "moderator_start_alert":
                    {
                        CuiHelper.DestroyUi(player, UI_MODERATION_PLAYER);
                        if (permission.UserHasPermission(player.UserIDString, PermissionModeration))
                            Metods_CheckModeration(Suspect, player);
                        return;
                    }
                case "moderator_open_reasons":
                    {
                        if (permission.UserHasPermission(player.UserIDString, PermissionModeration))
                            UI_OpenReasonsBan(player, SuspectID);
                        return;
                    }
                case "moderator_stop_alert":
                    {
                        if (permission.UserHasPermission(player.UserIDString, PermissionModeration))
                            Metods_CheckModerationFinish(player, SuspectID);
                        return;
                    }
                case "moderator_detalinfo_suspect":
                    {
                        UI_ModerationDetalicInfo(player, SuspectID);
                        return;
                    }
                case "moderator_banned":
                    {
                        int i = Convert.ToInt32(arg.Args[2]);
                        Metods_ModeratorBanneb(player, SuspectID, i);
                        return;
                    }
                case "give":
                    {
                        ReportInformation[ulong.Parse(arg.Args[1])].ReportCount += Convert.ToInt32(arg.Args[2]);
                        PrintWarning("ACCESS");
                        VKSendMessage(String.Format("CONSOLE_REPORT_GIVE", this), arg.Args[1], arg.Args[2], ReportInformation[ulong.Parse(arg.Args[1])].ReportCount);
                        return;
                    }
                case "remove":
                    {
                        ReportInformation[ulong.Parse(arg.Args[1])].ReportCount -= Convert.ToInt32(arg.Args[2]);
                        PrintWarning("ACCESS");
                        VKSendMessage(String.Format("CONSOLE_REPORT_REMOVE", this), arg.Args[1], arg.Args[2], ReportInformation[ulong.Parse(arg.Args[1])].ReportCount);
                        return;
                    }             
            }
        }

        #endregion

        #region Lang
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TITLE_REPORTS"] = "The system of complaints against players",
                ["DESCRIPTION_REPORTS"] = "Select the player's nickname and the corresponding complaint! When you reach a certain number of complaints - a moderator will check player!",
                ["PLAYER_CHECKED"] = "The player had already check!",
                ["BUTTON_BACK"] = "Back",
                ["BUTTON_NEXT"] = "Next",
                ["BUTTON_CLOSE"] = "Close",

                ["MSG_REPORTED_SUSPECT"] = "You have successfully submitted a player report - {0}\nReported : {1}\nThe moderator will review your complaint as soon as possible!",
                ["MSG_CHECK_DISCORD"] = "You can't send Discord without checking!",
                ["MSG_CHECK_CHECK_STOP"] = "You have successfully passed the test!\nWe wish you a pleasant game on our server!",
                ["MSG_COOLDOWN"] = "You have recently sent a complaint!\nWait <color=#47AF5DFF>{0}</color>",
                ["MSG_DISCORD_SEND"] = "You have successfully submitted the information!\nDiscord - {0}\nExpect a call from the moderator",

                ["UI_ALERT_TITLE"] = "Bас вызвали на проверку!",
                ["UI_ALERT_DESCRIPTION"] = " Вы достигли максимально-допустимое количество жалоб.\nПоэтому модерация сервера обязана вас проверить! Пожалуйста предоставьте Discord.\n В случае игнорирования данного сообщения - вы получите блокировку!",
                ["UI_ALERT_DESCRIPTION_COMMAND"] = "Чтобы предоставить данные,используйте команды :\n/discord\nДалее с вами свяжется модератор",

                ["UI_MODERATOR_PANEL_TITLE"] = "Server moderator menu",
                ["UI_MODERATOR_PANEL_DESCRIPTION"] = "This list shows players who have reached the limit of complaints,click on the player to get more information",
                ["UI_MODERATOR_PANEL_START_CHECK"] = "Call for verification",
                ["UI_STATUS"] = "Status",

                ["NETWORD_STATUS_ONLINE"] = "Online",
                ["IS_STEAM_STATUS_PIRATE"] = "Pirate",
                ["IS_STEAM_STATUS_LICENSE"] = "License",

                ["METODS_SEND_REPORT_VK"] = "[IQReportSystem]\nA complaint has been sent to player {0} ({1})!\nComplaint - {2}",
                ["METODS_HELP_MODERS"] = "Player <color=#47AF5DFF>{0}</color> reached the limit of reports!\nThe number of his reports - <color=#47AF5DFF>{1}</color>\nModeration that is free - check the player!",
                ["METODS_HELP_MODERS_VK"] = "[IQReportSystem]\nPlayer {0} reached the limit of reports!\nThe number of his reports - {1}\nModeration that is free - check the player!",
                ["METODS_MODER_START_CHECK"] = "You started checking!\nSuspect - <color=#47AF5DFF>{0}</color>\nGetting started with AFK!\nIf the player is not AFK, they will receive a notification of verification!",
                ["METODS_MODER_START_CHECK_VK"] = "[IQReportSystem]\nModerator {0}({1}) started checking!\nSuspect - {2}({3})",
                ["METODS_MODER_STOP_CHECK"] = "Verification completed.\nHave a nice day!\nDo not forget to check the complaint list!",
                ["METODS_MODER_STOP_CHECK_VK"] = "[IQReportSystem]\nModerator {0} finished checking!",
                ["NON_REPORT"] = "No complaints",
                ["MODERATOR_RETURN_WELCOME"] = "Welcome back!\nthe check was not canceled, continue!",
                ["STATUS_CHANGED"] = "The player's {0} status has changed to: {1}\n Wait for the player on the server for 10 minutes!\nIf the player does not enter after 10 minutes-issue a ban for Refusal",
                ["STATUS_CHANGED_VK"] = "[IQReportSystem]The player's {0} status has changed to: {1}\n Wait for the player on the server for 10 minutes!\nIf the player does not enter after 10 minutes-issue a ban for Refusal",
                ["MODERATOR_DISCONNECTED_STOP_CHECK"] = "The check was removed!\nModerator {0} left the server\nReason: connection Failure\nWe apologize!\nWe will inform the other moderation!",
                ["MODERATOR_DISCONNECTED_STOP_RESEND"] = "Moderator {0} finally left the server during verification!\n Player {1} is waiting for other moderators to check!",
                ["MODERATOR_COMPLETED_CHECK"] = "You successfully completed the review and delivered your verdict\nYour verdict : {0}",
                ["MODERATOR_COMPLETED_CHECK_VK"] = "[[IQ Report System]\nModerator {0}[(1)] finished checking \nSuspect {2}[{3}]\nVerdict : {4}\n[AFK Check]Player didn't move : {5}/5",
                ["PLAYER_AFK_CHECK_STOP"] = "Suspect AFK\nThe check is removed automatically!",
                ["PLAYER_AFK_CHECK_STOP_VK"] = "[IQReportSystem]\nModerator {0}({1}) checking the player {2}.\nThe AFK suspect and the check was removed!",
                ["PLAYER_AFK_CHANGE_POS"] = "The player was moving! Check {0}/5",
                ["PLAYER_AFK_CHANGE_NO_POS"] = "The player didn't move! Check {0}/5",
                ["PLAYER_NON_AFK"] = "The player moves.\nProverite on!",
                ["DISCORD_NULL"] = "Not provided",
                ["REPORT_LIST_CONSOLE"] = "\n[IQReportSystem]:\nList of players in the Moderation Panel",
                ["DISCROD_VK_SEND"] = "[IQReportSystem]\nSuspect {0}({1}) provided Discord for verification!\nDiscord - {2}",
                ["CONSOLE_REPORT_GIVE"] = "Player {0} is successfully added to the report in the amount of {1}. Its number is - {2}",
                ["CONSOLE_REPORT_REMOVE"] = "Player {0} successfully removed reports in the amount of - {1} His number is - {2}",
                ["MODERATOR_NON_OPEN_MENU"] = "You can't open the moderator menu when checking a player!\nFinish checking!",
                ["UI_MODERMENU_NICK"] = "Nick",
                ["UI_MODERMENU_STEAMID"] = "Steam64ID",
                ["UI_MODERMENU_COUNT_REPORT"] = "Number of complaints",
                ["UI_MODERMENU_COUNT_LAST_REPORT"] = "Last report",
                ["UI_MODERMENU_COUNT_LAST_MODER_CHECK"] = "Last verified by the moderator",
                ["UI_MODERMENU_COUNT_CHECKS"] = "Number of checks",
                ["UI_MODERMENU_COUNT_GAME"] = "Game",
                ["UI_MODERMENU_COUNT_REPORT_HISTORY"] = "The history of complaints on the player",
                ["UI_MODERMENU_TITLE_MINI"] = "Verification menu",
                ["UI_MODERMENU_FINISH_MINI"] = "Finish",
                ["UI_MODERMENU_VERDICT_MINI"] = "Verdict",
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TITLE_REPORTS"] = "Система жалоб на игроков",
                ["DESCRIPTION_REPORTS"] = "Bыберите ник игрока и соответсвенную жалобу! При достижении определенного количества жалоб - модератор проверит игрока!",
                ["PLAYER_CHECKED"] = "Данного игрока уже проверяют!",
                ["BUTTON_BACK"] = "Назад",
                ["BUTTON_NEXT"] = "Вперед",
                ["BUTTON_CLOSE"] = "Закрыть",

                ["MSG_REPORTED_SUSPECT"] = "Вы успешно отправили жалобу на игрока - {0}\nЖалоба : {1}\nМодератор рассмотрит вашу жалобу как можно скорее!",
                ["MSG_CHECK_DISCORD"] = "Вы не можете отправить Discord без проверки!",
                ["MSG_CHECK_CHECK_STOP"] = "Вы успешно прошли проверку!\nЖелаем приятной игры на нашем сервере!",
                ["MSG_COOLDOWN"] = "Вы недавно отправляли жалобу!\nПодождите еще <color=#47AF5DFF>{0}</color>",
                ["MSG_DISCORD_SEND"] = "Вы успешно предоставили данные!\nDiscord - {0}\nОжидайте звонка от модератора",

                ["UI_ALERT_TITLE"] = "Bас вызвали на проверку!",
                ["UI_ALERT_DESCRIPTION"] = " Вы достигли максимально-допустимое количество жалоб.\nПоэтому модерация сервера обязана вас проверить! Пожалуйста предоставьте Discord.\n В случае игнорирования данного сообщения - вы получите блокировку!",
                ["UI_ALERT_DESCRIPTION_COMMAND"] = "Чтобы предоставить данные,используйте команды :\n/discord\nДалее с вами свяжется модератор",

                ["UI_MODERATOR_PANEL_TITLE"] = "Меню модератора сервера",
                ["UI_MODERATOR_PANEL_DESCRIPTION"] = "В данном списке отображены игроки - достигшие предела жалоб,нажмите по игроку чтобы получить больше информации",
                ["UI_MODERATOR_PANEL_START_CHECK"] = "Вызвать на проверку",
                ["UI_STATUS"] = "Статус",

                ["NETWORD_STATUS_ONLINE"] = "Онлайн",
                ["IS_STEAM_STATUS_PIRATE"] = "Пират",
                ["IS_STEAM_STATUS_LICENSE"] = "Лицензия", 

                ["METODS_SEND_REPORT_VK"] = "[IQReportSystem]\nНа игрока {0}({1}) отправили жалобу!\nЖалоба - {2}",
                ["METODS_HELP_MODERS"] = "Игрок <color=#47AF5DFF>{0}</color> достиг предельного количества репортов!\nКоличество его репортов - <color=#47AF5DFF>{1}</color>\nМодерация которая свободна - проверьте игрока!",
                ["METODS_HELP_MODERS_VK"] = "[IQReportSystem]\nИгрок {0} достиг предельного количества репортов!\nКоличество его репортов - {1}\nМодерация которая свободна - проверьте игрока!",
                ["METODS_MODER_START_CHECK"] = "Вы начали проверку!\nПодозреваемый - <color=#47AF5DFF>{0}</color>\nНачинаем проверку на AFK!\nЕсли игрок не AFK - ему выведут уведомление о проверке!",
                ["METODS_MODER_START_CHECK_VK"] = "[IQReportSystem]\nМодератор {0}({1}) начал проверку!\nПодозреваемый - {2}({3})",
                ["METODS_MODER_STOP_CHECK"] = "Проверка завершена.\nУдачного дня!\nНе забывай проверять список жалоб!",
                ["METODS_MODER_STOP_CHECK_VK"] = "[IQReportSystem]\nМодератор {0} закончил проверку!",
                ["NON_REPORT"] = "Жалоб нет",
                ["MODERATOR_RETURN_WELCOME"] = "С возвращением!\nПроверка не была отменена,продолжайте!",
                ["STATUS_CHANGED"] = "У игрока {0} изменился статус на : {1}\nОжидайте игрока на сервере в течении 10 минут!\nЕсли игрок не зайдет после 10 минут - выдавайте бан за Отказ",
                ["STATUS_CHANGED_VK"] = "[IQReportSystem]У игрока {0} изменился статус на : {1}\nОжидайте игрока на сервере в течении 10 минут!\nЕсли игрок не зайдет после 10 минут - выдавайте бан за Отказ",
                ["MODERATOR_DISCONNECTED_STOP_CHECK"] = "Проверка была снята!\nМодератор {0} покинул сервер\n Причина : Разрыв соединения\nПриносим свои извинения!\nМы сообщим другой модерации!",
                ["MODERATOR_DISCONNECTED_STOP_RESEND"] = "Модератор {0} окончательно покинул сервер во время проверки!\nИгрок {1} ожидает других модераторов для проверки!",
                ["MODERATOR_COMPLETED_CHECK"] = "Вы успешно завершили проверку и вынесли свой вердикт\nВаш вердикт : {0}",
                ["MODERATOR_COMPLETED_CHECK_VK"] = "[IQReportSystem]\nМодератор {0}[(1)] закончил проверку\n Подозреваемый {2}[{3}]\nВердикт : {4}\n[Проверка на AFK]Игрок не двигался : {5}/5",
                ["PLAYER_AFK_CHECK_STOP"] = "Игрок AFK\nПроверка снята автоматически!",
                ["PLAYER_AFK_CHECK_STOP_VK"] = "[IQReportSystem]\nМодератор {0}({1}) проверял игрока {2}.\nИгрок AFK и проверка была снята!",
                ["PLAYER_AFK_CHANGE_POS"] = "Игрок двигался! Проверка {0}/5",
                ["PLAYER_AFK_CHANGE_NO_POS"] = "Игрок не двигался! Проверка {0}/5",
                ["PLAYER_NON_AFK"] = "Игрок двигается.\nПроверяйте дальше!",
                ["DISCORD_NULL"] = "Не предоставлен",
                ["REPORT_LIST_CONSOLE"] = "\n[IQReportSystem]:\nСписок игроков в Панели-Модерации",
                ["DISCROD_VK_SEND"] = "[IQReportSystem]\nИгрок {0}({1}) предоставил Discord на проверку!\nDiscord - {2}",
                ["CONSOLE_REPORT_GIVE"] = "Игроку {0} успешно добавлены репорты в количестве - {1}. Его количество составляет - {2}",
                ["CONSOLE_REPORT_REMOVE"] = "Игроку {0} успешно сняты репорты в количестве - {1} Его количество составляет - {2}",
                ["MODERATOR_NON_OPEN_MENU"] = "Вы не можете открыть меню модератора при проверке игрока!\nОкончите проверку!",
                ["UI_MODERMENU_NICK"] = "Ник",
                ["UI_MODERMENU_STEAMID"] = "Steam64ID",
                ["UI_MODERMENU_COUNT_REPORT"] = "Кол-во жалоб",
                ["UI_MODERMENU_COUNT_LAST_REPORT"] = "Последняя жалоба",
                ["UI_MODERMENU_COUNT_LAST_MODER_CHECK"] = "Последний проверяющий модератор",
                ["UI_MODERMENU_COUNT_CHECKS"] = "Кол-во проверок",
                ["UI_MODERMENU_COUNT_GAME"] = "Игра",
                ["UI_MODERMENU_COUNT_REPORT_HISTORY"] = "История жалоб на игрока",
                ["UI_MODERMENU_TITLE_MINI"] = "Mеню проверки",
                ["UI_MODERMENU_FINISH_MINI"] = "Закончить",
                ["UI_MODERMENU_VERDICT_MINI"] = "Вердикт",

            }, this, "ru");
            PrintWarning("Языковой файл загружен успешно");
        }
        #endregion

        #region UI

        #region Parents

        private static string UI_PLAYER_PANEL = "UI_PLAYER_PANEL_MAIN";
        private static string UI_PLAYER_PANEL_PLAYER = "UI_PLAYER_PANEL_MAIN_PLAYER";
        private static string UI_PLAYER_ALERT = "UI_PLAYER_ALERT";


        private static string UI_MODERATION_PLAYER = "UI_MODERATION_MAIN_PLAYER";
        private static string UI_MODERATION_PANEL_PLAYER = "UI_MODERATION_PANEL_PLAYER";
        private static string UI_MODERATION_DETALIC_PLAYER = "UI_MODERATION_DETALIC_PLAYER";
        private static string UI_MODERATION_DETALIC_HISTORY_PLAYER = "UI_MODERATION_HISTORY_DETALIC_PLAYER";

        private static string UI_MODERATION_CHECK_MENU = "UI_MODERATION_CHECK_MENU_PARENT";
        private static string UI_MODERATION_CHECK_MENU_DISCORD = "UI_MODERATION_CHECK_MENU_DISCORD_PARENT";
        private static string UI_MODERATION_CHECK_MENU_NETWORK = "UI_MODERATION_CHECK_MENU_NETWORK_PARENT";

        #endregion

        #region PlayerInterface

        void UI_PlayerInterface(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UI_PLAYER_PANEL);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Color = HexToRustFormat("#000000BC"), Material = "assets/content/ui/uibackgroundblur.mat", Sprite = "assets/content/ui/ui.background.transparent.radial.psd" }
            },  "Overlay", UI_PLAYER_PANEL);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.002604172 0.9361111", AnchorMax = "0.1046875 1" },
                Button = { Close = UI_PLAYER_PANEL, Color = "0 0 0 0" },
                Text = { Text = lang.GetMessage("BUTTON_CLOSE",this), Color = HexToRustFormat("#FFFFFF86"), FontSize = 30, Align = TextAnchor.MiddleLeft }
            }, UI_PLAYER_PANEL);

            #region Title

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.8814815", AnchorMax = "1 1" },
                Text = { Text = lang.GetMessage("TITLE_REPORTS", this), FontSize = 30, Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperCenter, Color = HexToRustFormat("#FFFFFF86") }
            }, UI_PLAYER_PANEL);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.8814815", AnchorMax = "1 0.9546296" },
                Text = { Text = lang.GetMessage("DESCRIPTION_REPORTS", this), FontSize = 16, Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperCenter, Color = HexToRustFormat("#FFFFFF86") }
            }, UI_PLAYER_PANEL);

            #endregion

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { Close = UI_PLAYER_PANEL, Color = "0 0 0 0" },
                Text = { Text = "" }
            }, UI_PLAYER_PANEL);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.01979169 0.07777782", AnchorMax = "0.9786458 0.8694414" },
                Image = { Color = HexToRustFormat("#0000001B"), Material = "assets/content/ui/uibackgroundblur.mat", Sprite = "assets/content/ui/ui.background.transparent.radial.psd" }
            },  UI_PLAYER_PANEL, UI_PLAYER_PANEL_PLAYER);

            int PCount = 0;
            int x = 0;
            int y = 0;

            foreach (var PList in BasePlayer.activePlayerList)
            {
                if (PList.userID == player.userID) continue;
                if (Friends != null)
                    if (config.Setting.FriendNoReport)
                        if ((bool)Friends.Call("HasFriend", player.userID, PList.userID)) continue;

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{0.004252315 + (x * 0.1425)} {0.9497076 - (y * 0.050)}", AnchorMax = $"{0.1385117 + (x * 0.1425)} {0.9922839 - (y * 0.050)}" },
                    Button = { Command = $"report select_reason {PList.userID}", Color = HexToRustFormat("#47AF5DFF") },
                    Text = { Text = "[" + PList.displayName + "]", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = HexToRustFormat("#FFFFFFFF") }
                }, UI_PLAYER_PANEL_PLAYER, $"Player_{PCount}");

                x++;
                if (x == 7)
                {
                    x = 0;
                    y++;
                }
                PCount++;
            }

            CuiHelper.AddUi(player, container);
        }

        void UI_OpenPlayerSelectReason(BasePlayer player, ulong SuspectID)
        {
            CuiHelper.DestroyUi(player, UI_PLAYER_PANEL_PLAYER);
            CuiElementContainer container = new CuiElementContainer();

            var ReasonCount = 3;
            int ItemCount = 0;
            float itemMinPosition = 219f;
            float itemWidth = 0.353646f - 0.151563f;
            float itemMargin = 0.259895f - 0.203646f;
            int itemCount = ReasonCount;
            float itemMinHeight = 0.505741f;
            float itemHeight = 0.398333f - 0.315741f;

            if (itemCount > ReasonCount)
            {
                itemMinPosition = 0.5f - ReasonCount / 2f * itemWidth - (ReasonCount - 1) / 2f * itemMargin;
                itemCount -= ReasonCount;
            }
            else itemMinPosition = 0.5f - itemCount / 2f * itemWidth - (itemCount - 1) / 2f * itemMargin;

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.01979169 0.07777782", AnchorMax = "0.9786458 0.8694444" },
                Image = { Color = HexToRustFormat("#0000001B"), Material = "assets/content/ui/uibackgroundblur.mat", Sprite = "assets/content/ui/ui.background.transparent.radial.psd" }
            }, UI_PLAYER_PANEL, UI_PLAYER_PANEL_PLAYER);

            for (int i = 0; i < config.ReasonReport.Count; i++)
            {               
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{itemMinPosition} {itemMinHeight}", AnchorMax = $"{itemMinPosition + itemWidth} {itemMinHeight + itemHeight}" },
                    Button = { Command = $"report reported_suspect {SuspectID} {i}", Color = HexToRustFormat("#47AF5DFF") },
                    Text = { Text = config.ReasonReport[i], FontSize = 16, Align = TextAnchor.MiddleCenter, Color = HexToRustFormat("#FFFFFFFF") }
                }, UI_PLAYER_PANEL_PLAYER, $"ReasonReport_{i}");

                ItemCount++;
                itemMinPosition += (itemWidth + itemMargin);
                if (ItemCount % ReasonCount == 0)
                {
                    itemMinHeight -= (itemHeight + (itemMargin * 2f));

                    if (itemCount > ReasonCount)
                    {
                        itemMinPosition = 0.5f - ReasonCount / 2f * itemWidth - (ReasonCount - 1) / 2f * itemMargin;
                        itemCount -= ReasonCount;
                    }
                    else itemMinPosition = 0.5f - itemCount / 2f * itemWidth - (itemCount - 1) / 2f * itemMargin;
                }
            }
         
            CuiHelper.AddUi(player, container);
        }

        void UI_PlayerAlert(BasePlayer suspect)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(suspect, UI_PLAYER_ALERT);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.8", AnchorMax = "0.5 0.95", OffsetMin = "-200 -150", OffsetMax = "200 0" },
                Image = { Color = HexToRustFormat(config.Setting.Interface.AlertHex), Material = "assets/content/ui/uibackgroundblur.mat" }
            }, "Overlay", UI_PLAYER_ALERT);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.2833332 0.4056847", AnchorMax = "0.7383332 0.4108528" },
                Image = { Color = HexToRustFormat("#FFFFFFFF") }
            },  UI_PLAYER_ALERT);

            #region Titles

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.7333333", AnchorMax = "1 1" },
                Text = { Text = lang.GetMessage("UI_ALERT_TITLE", this), FontSize = 28, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = HexToRustFormat("#FFFFFFFF") }
            },  UI_PLAYER_ALERT);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.2311112", AnchorMax = "1 0.7733414" },
                Text = { Text = lang.GetMessage("UI_ALERT_DESCRIPTION", this), FontSize = 14, Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperCenter, Color = HexToRustFormat("#FFFFFFFF") }
            }, UI_PLAYER_ALERT);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.03100784", AnchorMax = "1 0.3943712" },
                Text = { Text = lang.GetMessage("UI_ALERT_DESCRIPTION_COMMAND", this), FontSize = 14, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = HexToRustFormat("#FFFFFFFF") }
            }, UI_PLAYER_ALERT);

            #endregion

            CuiHelper.AddUi(suspect, container);
        }

        #endregion

        #region ModerationInterface
        void UI_ModerationInterface(BasePlayer moderator)
        {
            CuiHelper.DestroyUi(moderator, UI_MODERATION_PLAYER);
            CuiElementContainer container = new CuiElementContainer();

            foreach(var StatusModerator in PlayerSaveCheck)
            {
                if(StatusModerator.Value.ModeratorID == moderator.userID)
                {
                    SendChat(moderator, lang.GetMessage("MODERATOR_NON_OPEN_MENU",this));
                    return;
                }
            }

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { Color = HexToRustFormat("#000000BC"), Material = "assets/content/ui/uibackgroundblur.mat", Sprite = "assets/content/ui/ui.background.transparent.radial.psd" }
            }, "Overlay", UI_MODERATION_PLAYER);

            #region Title

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.8814815", AnchorMax = "1 1" },
                Text = { Text = lang.GetMessage("UI_MODERATOR_PANEL_TITLE", this), FontSize = 30, Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperCenter, Color = HexToRustFormat("#FFFFFF86") }
            }, UI_MODERATION_PLAYER);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.8814815", AnchorMax = "1 0.9546296" },
                Text = { Text = lang.GetMessage("UI_MODERATOR_PANEL_DESCRIPTION", this), FontSize = 16, Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperCenter, Color = HexToRustFormat("#FFFFFF86") }
            }, UI_MODERATION_PLAYER);

            #endregion

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.002604172 0.9361111", AnchorMax = "0.1046875 1" },
                Button = { Close = UI_MODERATION_PLAYER, Color = "0 0 0 0" },
                Text = { Text = lang.GetMessage("BUTTON_CLOSE",this), Color = HexToRustFormat("#FFFFFF86"), FontSize = 30, Align = TextAnchor.MiddleLeft }
            }, UI_MODERATION_PLAYER);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.01979169 0.3824074", AnchorMax = "0.9786458 0.8694444" },
                Image = { Color = HexToRustFormat("#0000001B") }
            }, UI_MODERATION_PLAYER, UI_MODERATION_PANEL_PLAYER);

            int PCount = 0;
            int x = 0;
            int y = 0;

            foreach (var PList in BasePlayer.activePlayerList)
            {
                var InfoData = ReportInformation[PList.userID];
                if (InfoData.ReportCount >= config.Setting.MaxReport)
                {
                    if (PlayerSaveCheck.ContainsKey(PList.userID) || moderator.userID == PList.userID) continue;

                    string Color = InfoData.ReportCount <= 6 ? config.Setting.Interface.HexMiimum : InfoData.ReportCount <= 8 ? config.Setting.Interface.HexSmall : InfoData.ReportCount >= 10 ? config.Setting.Interface.HexMaximum : config.Setting.Interface.HexMiimum;
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = $"{0.004252315 + (x * 0.14)} {0.9163501 - (y * 0.010)}", AnchorMax = $"{0.1385117 + (x * 0.14)} {0.9922839 - (y * 0.010)}" },
                        Button = { Command = $"report moderator_detalinfo_suspect {PList.userID}", Color = HexToRustFormat(Color) },
                        Text = { Text = PList.displayName, FontSize = 16, Align = TextAnchor.MiddleCenter, Color = HexToRustFormat("#FFFFFFFF") }
                    }, UI_MODERATION_PANEL_PLAYER, $"Suspect_{PCount}");

                    x++;
                    PCount++;
                }
            }
            CuiHelper.AddUi(moderator, container);
        }

        void UI_ModerationDetalicInfo(BasePlayer moderator, ulong SuspectID)
        {
            string ImageAvatar = GetImage(SuspectID.ToString(), 0);
            var User = ReportInformation[SuspectID];
            CuiHelper.DestroyUi(moderator, UI_MODERATION_DETALIC_PLAYER);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.01979169 0.09351853", AnchorMax = "0.9786458 0.3777778" },
                Image = { Color = HexToRustFormat("#0000001B") }
            }, UI_MODERATION_PLAYER, UI_MODERATION_DETALIC_PLAYER);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.5 0", AnchorMax = $"0.5 0", OffsetMin = "-135 -45", OffsetMax = "120 -10" },
                Button = { Command = $"report moderator_start_alert {SuspectID}", Color = HexToRustFormat("#3B85F5B1") },
                Text = { Text = lang.GetMessage("UI_MODERATOR_PANEL_START_CHECK", this), FontSize = 24, Align = TextAnchor.MiddleCenter, Color = HexToRustFormat("#FFFFFFFF") }
            }, UI_MODERATION_DETALIC_PLAYER);

            #region InfoPlayer

            container.Add(new CuiElement
            {
                Parent = UI_MODERATION_DETALIC_PLAYER,
                Components = {
                    new CuiRawImageComponent {
                        Png = ImageAvatar,
                        Url = null ,
                        Sprite = "assets/content/textures/generic/fulltransparent.tga"
                    },
                    new CuiRectTransformComponent {
                        AnchorMin = "0.02112192 0.0814414",
                        AnchorMax = "0.1601768 0.9153098"
                    },
                }
            });

            #region TitlesInfo

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.1634981 0.8143322", AnchorMax = "0.3025529 0.9153094" },
                Text = { Text = $"{lang.GetMessage("UI_MODERMENU_NICK",this)} : {User.DisplayName}", FontSize = 16, Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperLeft, Color = HexToRustFormat("#FFFFFFFF") }
            },  UI_MODERATION_DETALIC_PLAYER);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.1634981 0.6970682", AnchorMax = "0.3557849 0.7980453" },
                Text = { Text = $"{lang.GetMessage("UI_MODERMENU_STEAMID", this)} : {SuspectID}", FontSize = 16, Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperLeft, Color = HexToRustFormat("#FFFFFFFF") }
            }, UI_MODERATION_DETALIC_PLAYER);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.1634981 0.5732894", AnchorMax = "0.3025529 0.6742666" },
                Text = { Text = $"{lang.GetMessage("UI_MODERMENU_COUNT_REPORT",this)} : {User.ReportCount}", FontSize = 16, Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperLeft, Color = HexToRustFormat("#FFFFFFFF") }
            }, UI_MODERATION_DETALIC_PLAYER);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.1634981 0.4527686", AnchorMax = "0.4682238 0.5537453" },
                Text = { Text = $"{lang.GetMessage("UI_MODERMENU_COUNT_LAST_REPORT", this)} : {User.LastReport}", FontSize = 16, Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperLeft, Color = HexToRustFormat("#FFFFFFFF") }
            }, UI_MODERATION_DETALIC_PLAYER);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.1634981 0.3159608", AnchorMax = "0.4899511 0.4267092" },
                Text = { Text = $"{lang.GetMessage("UI_MODERMENU_COUNT_LAST_MODER_CHECK",this)} : {User.LastCheckModerator}", FontSize = 16, Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperLeft, Color = HexToRustFormat("#FFFFFFFF") }
            }, UI_MODERATION_DETALIC_PLAYER);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.1634981 0.1954396", AnchorMax = "0.4899511 0.3061879" },
                Text = { Text = $"{lang.GetMessage("UI_MODERMENU_COUNT_CHECKS",this)} : {User.CheckCount}", FontSize = 16, Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperLeft, Color = HexToRustFormat("#FFFFFFFF") }
            }, UI_MODERATION_DETALIC_PLAYER);

            if (MultiFighting != null)
            {
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.1634981 0.09120516", AnchorMax = "0.4899511 0.1889239" },
                    Text = { Text = $"{lang.GetMessage("UI_MODERMENU_COUNT_GAME", this)} : {User.GameStatus}", FontSize = 16, Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperLeft, Color = HexToRustFormat("#FFFFFFFF") }
                }, UI_MODERATION_DETALIC_PLAYER);

            }
            #endregion

            #region ReportHistory

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.6550788 0.8599349", AnchorMax = "0.9755568 1" },
                Text = { Text = lang.GetMessage("UI_MODERMENU_COUNT_REPORT_HISTORY",this), FontSize = 25, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = HexToRustFormat("#FFFFFFFF") }
            }, UI_MODERATION_DETALIC_PLAYER);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.6539925 0.08143318", AnchorMax = "0.9766369 0.8469055" },
                Image = { Color = "0 0 0 0" }
            },  UI_MODERATION_DETALIC_PLAYER, UI_MODERATION_DETALIC_HISTORY_PLAYER);

            for(int i = 0; i < ReportInformation[SuspectID].ReportHistory.Count; i++)
            {
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = $"0 {0.8468084 - (i * 0.18)}", AnchorMax = $"1 {1 - (i * 0.18)}" },
                    Text = { Text = ReportInformation[SuspectID].ReportHistory[i], FontSize = 16, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = HexToRustFormat("#FFFFFFFF") }
                },  UI_MODERATION_DETALIC_HISTORY_PLAYER, $"REASON_{i}");

                if (i >= 5) break;
            }

            #endregion

            #endregion

            CuiHelper.AddUi(moderator, container);
        }

        void UI_ModerationCheckMenu(BasePlayer moderator,ulong SuspectID)
        {
            CuiHelper.DestroyUi(moderator, UI_MODERATION_CHECK_MENU);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-455 16" , OffsetMax = "-210 120" },
                Image = { Color = HexToRustFormat("#0000005B") }
            },  "Overlay",UI_MODERATION_CHECK_MENU);

            #region Titles

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.7820513", AnchorMax = "1 1" },
                Text = { Text = lang.GetMessage("UI_MODERMENU_TITLE_MINI",this), FontSize = 18, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter, Color = HexToRustFormat("#FFFFFFFF") }
            }, UI_MODERATION_CHECK_MENU);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.5448718", AnchorMax = "1 0.7526907" },
                Text = { Text = $"Discord : {PlayerSaveCheck[SuspectID].Discord}", FontSize = 14, Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperCenter, Color = HexToRustFormat("#FFFFFFFF") }
            }, UI_MODERATION_CHECK_MENU, UI_MODERATION_CHECK_MENU_DISCORD);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.3589744", AnchorMax = "1 0.5625198" },
                Text = { Text = $"{lang.GetMessage("UI_STATUS", this)} : {PlayerSaveCheck[SuspectID].StatusNetwork}", FontSize = 14, Font = "robotocondensed-regular.ttf", Align = TextAnchor.UpperCenter, Color = HexToRustFormat("#FFFFFFFF") }
            }, UI_MODERATION_CHECK_MENU, UI_MODERATION_CHECK_MENU_NETWORK);

            #endregion

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.01496607 0.03846152", AnchorMax = $"0.49 0.301282" },
                Button = { Command = $"report moderator_stop_alert {SuspectID}", Color = HexToRustFormat("#47AF5DFF") },
                Text = { Text = lang.GetMessage("UI_MODERMENU_FINISH_MINI",this), FontSize = 15, Align = TextAnchor.MiddleCenter, Color = HexToRustFormat("#FFFFFFFF") }
            }, UI_MODERATION_CHECK_MENU);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.51 0.03846152", AnchorMax = $"0.9836734 0.301282" },
                Button = { Command = $"report moderator_open_reasons {SuspectID}", Color = HexToRustFormat("#CE4646FF") },
                Text = { Text = lang.GetMessage("UI_MODERMENU_VERDICT_MINI", this), FontSize = 15, Align = TextAnchor.MiddleCenter, Color = HexToRustFormat("#FFFFFFFF") }
            }, UI_MODERATION_CHECK_MENU);

            CuiHelper.AddUi(moderator, container);
        }

        void UI_OpenReasonsBan(BasePlayer Moderator,ulong SuspectID)
        {
            CuiElementContainer container = new CuiElementContainer();

            for(int i = 0; i < config.ReasonBan.Count; i++)
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"0 1", AnchorMax = $"0 1", OffsetMin = $"0 {2 + (i * 30)}", OffsetMax = $"245 {30 + (i * 30)}" },
                    Button = {FadeIn = 0.3f +(i / 10), Command = $"report moderator_banned {SuspectID} {i}", Color = HexToRustFormat("#0000005B") },
                    Text = { Text = config.ReasonBan[i].DisplayName, FontSize = 15, Align = TextAnchor.MiddleCenter, Color = HexToRustFormat("#FFFFFFFF") }
                }, UI_MODERATION_CHECK_MENU);
            }

            CuiHelper.AddUi(Moderator, container);
        }
        #endregion

        #endregion

        #region Hooks
        private void OnServerInitialized()
        {
            ReportInformation = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerInfo>>("IQReportSystem/Reports");
            ModeratorInformation = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, ModeratorInfo>>("IQReportSystem/Moders");

            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerInit(player);

            permission.RegisterPermission(PermissionModeration, this);
            permission.RegisterPermission(PermissionAdmin, this);
        }

        private void OnPlayerInit(BasePlayer player) => Metods_PlayerConnected(player);
        private void OnServerSave()
        {
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQReportSystem/Reports", ReportInformation);
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQReportSystem/Moders", ModeratorInformation);
        }
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            Metods_StatusNetwork(player, reason);
            Metods_ModeratorExitCheck(player);
        }

        #endregion

        #region Helps

        #region PluginsAPI

        private void VKSendMessage(string msg, params object[] args)
        {
            if (!config.Setting.VKMessage) return;
            int RandomID = UnityEngine.Random.Range(0, 9999);
            string VKMsg = string.Format(lang.GetMessage(msg, this), args);
            while (VKMsg.Contains("#"))
                VKMsg = VKMsg.Replace("#", "%23");

            webrequest.Enqueue($"https://api.vk.com/method/messages.send?chat_id={config.Setting.VKSettings.ChatID}&random_id={RandomID}&message={VKMsg}&access_token={config.Setting.VKSettings.Token}&v=5.92", null, (code, response) => { }, this);
        }

        int API_GET_REPORT_COUNT(ulong UserID)
        {
            var User = ReportInformation[UserID];
            return User.ReportCount;
        }
        int API_GET_CHECK_COUNT(ulong UserID)
        {
            var User = ReportInformation[UserID];
            return User.CheckCount;
        }
        List<string> API_GET_LIST_API(ulong UserID)
        {
            var User = ReportInformation[UserID];
            return User.IP;
        }
        string API_GET_GAME_STATUS(ulong UserID)
        {
            var User = ReportInformation[UserID];
            return User.GameStatus;
        }
        string API_GET_LAST_CHECK_MODERATOR(ulong UserID)
        {
            var User = ReportInformation[UserID];
            return User.LastCheckModerator;
        }
        string API_GET_LAST_REPORT(ulong UserID)
        {
            var User = ReportInformation[UserID];
            return User.LastReport;
        }
        List<string> API_GET_REPORT_HISTORY(ulong UserID)
        {
            var User = ReportInformation[UserID];
            return User.ReportHistory;
        }

        #endregion

        #region MSG

        public void SendChat(BasePlayer player,string Message, Chat.ChatChannel channel = Chat.ChatChannel.Global)
        {
            var Chat = config.Setting.ChatSetting;
            if (IQChat)
                IQChat?.Call("API_ALERT_PLAYER", player, Message, Chat.CustomPrefix, Chat.CustomAvatar);
            else player.SendConsoleCommand("chat.add", channel, 0, Message);
        }

        #endregion

        #region Hex
        private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                hex = "#FFFFFFFF";
            }

            var str = hex.Trim('#');

            if (str.Length == 6)
                str += "FF";

            if (str.Length != 8)
            {
                throw new Exception(hex);
                throw new InvalidOperationException("Cannot convert a wrong format.");
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

            UnityEngine.Color color = new Color32(r, g, b, a);

            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }

        #endregion

        #region Steam

        string IsSteam(string id)
        {        
            if (MultiFighting != null)
            {
                var player = BasePlayer.Find(id);
                if (player == null)
                {
                    return "ERROR #1";
                }
                var obj = MultiFighting.CallHook("IsSteam", player.Connection);
                if (obj is bool)
                {
                    if ((bool)obj)
                    {
                        return lang.GetMessage("IS_STEAM_STATUS_LICENSE", this, id);
                    }
                    else
                    {
                        return lang.GetMessage("IS_STEAM_STATUS_PIRATE",this,id);
                    }
                }
                else
                {
                    return "ERROR #2";
                }
            }
            else return lang.GetMessage("IS_STEAM_STATUS_LICENSE", this, id);
        }

        #endregion

        #region Format

        static DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        static double CurrentTime() => DateTime.UtcNow.Subtract(epoch).TotalSeconds;

        public static string FormatTime(TimeSpan time)
        {
            string result = string.Empty;
            if (time.Days != 0)
                result += $"{Format(time.Days, "дней", "дня", "день")} ";

            if (time.Hours != 0)
                result += $"{Format(time.Hours, "часов", "часа", "час")} ";

            if (time.Minutes != 0)
                result += $"{Format(time.Minutes, "минут", "минуты", "минута")} ";

            if (time.Seconds != 0)
                result += $"{Format(time.Seconds, "секунд", "секунды", "секунда")} ";

            return result;
        }

        private static string Format(int units, string form1, string form2, string form3)
        {
            var tmp = units % 10;

            if (units >= 5 && units <= 20 || tmp >= 5 && tmp <= 9)
                return $"{units} {form1}";

            if (tmp >= 2 && tmp <= 4)
                return $"{units} {form2}";

            return $"{units} {form3}";
        }

        #endregion

        #endregion
    }
}
