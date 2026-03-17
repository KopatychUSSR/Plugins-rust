using System;
using System.Collections.Generic;
using System.Linq;
using CompanionServer;
using ConVar;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("IQChat", "Mercury", "0.2.7")]
    [Description("")]
    class IQChat : RustPlugin
    {
        #region Reference
        [PluginReference] Plugin IQPersonal;     
        public void SetMute(BasePlayer player) => IQPersonal?.CallHook("API_SET_MUTE", player.userID);
        public void BadWords(BasePlayer player) => IQPersonal?.CallHook("API_DETECTED_BAD_WORDS", player.userID);
        #endregion

        #region Vars
        public Dictionary<BasePlayer, BasePlayer> PMHistory = new Dictionary<BasePlayer, BasePlayer>();

        public string PermMuteMenu = "iqchat.muteuse";
        class Response
        {
            [JsonProperty("country")]
            public string Country { get; set; }
        }
        #endregion

        #region Configuration

        private static Configuration config = new Configuration();
        private class Configuration
        {
            [JsonProperty("Права для смены ника")]
            public string RenamePermission;
            [JsonProperty("Настройка префиксов")]
            public List<AdvancedFuncion> PrefixList = new List<AdvancedFuncion>();
            [JsonProperty("Настройка цветов для ников")]
            public List<AdvancedFuncion> NickColorList = new List<AdvancedFuncion>();
            [JsonProperty("Настройка цветов для сообщений")]
            public List<AdvancedFuncion> MessageColorList = new List<AdvancedFuncion>();
            [JsonProperty("Настройка сообщений в чате")]
            public MessageSettings MessageSetting;
            [JsonProperty("Настройка причин блокировок чата")]
            public List<ReasonMuteChat> ReasonListChat = new List<ReasonMuteChat>();
            [JsonProperty("Настройка интерфейса")]
            public InterfaceSettings InterfaceSetting;
            [JsonProperty("Настройка оповещения")]
            public AlertSetting AlertSettings;         
            [JsonProperty("Настройка сброса привилегий")]
            public PrefixFoDefault PrefixFoDefaults;
            [JsonProperty("Настройка Rust+")]
            public RustPlus RustPlusSettings;
            internal class AdvancedFuncion
            {
                [JsonProperty("Права")]
                public string Permissions;
                [JsonProperty("Значение")]
                public string Argument;
            }
            internal class RustPlus
            {
                [JsonProperty("Использовать Rust+")]
                public bool UseRustPlus;
                [JsonProperty("Название для уведомления Rust+")]
                public string DisplayNameAlert;
            }
            internal class ReasonMuteChat
            {
                [JsonProperty("Причина мута")]
                public string Reason;
                [JsonProperty("Время мута")]
                public int TimeMute;
            }
            internal class PrefixFoDefault
            {
                [JsonProperty("При окончании префикса, установится данный префикс")]
                public string PrefixDefault;
                [JsonProperty("При окончании цвета ника, установится данный цвет")]
                public string NickDefault;
                [JsonProperty("При окончании цвета сообщения, установится данный цвета")]
                public string MessageDefault;
            }
            internal class MessageSettings
            {
                [JsonProperty("Включить форматирование сообщений")]
                public bool FormatingMessage;
                [JsonProperty("Включить личные сообщения")]
                public bool PMActivate;
                [JsonProperty("Включить игнор ЛС игрокам(/ignore nick)")]
                public bool IgnoreUsePM;
                [JsonProperty("Включить Анти-Спам")]
                public bool AntiSpamActivate;
                [JsonProperty("Скрыть из чата выдачу предметов Админу")]
                public bool HideAdminGave;
                [JsonProperty("Использовать список запрещенных слов?")]
                public bool UseBadWords;
                [JsonProperty("Включить возможность использовать несколько префиксов сразу")]
                public bool MultiPrefix;
                [JsonProperty("Переносить мут в командный чат(В случае мута,игрок не сможет писать даже в командный чат)")]
                public bool MuteTeamChat;
                [JsonProperty("Пермишенс для иммунитета к антиспаму")]
                public string PermAdminImmunitetAntispam;
                [JsonProperty("Наименование оповещения в чат")]
                public string BroadcastTitle;
                [JsonProperty("Цвет сообщения оповещения в чат")]
                public string BroadcastColor;
                [JsonProperty("На какое сообщение заменять плохие слова")]
                public string ReplaceBadWord;
                [JsonProperty("Звук при при получении личного сообщения")]
                public string SoundPM;            
                [JsonProperty("Время,через которое удалится сообщение с UI от администратора")]
                public int TimeDeleteAlertUI;
                [JsonProperty("Steam64ID для аватарки в чате")]
                public ulong Steam64IDAvatar;
                [JsonProperty("Время через которое игрок может отправлять сообщение (АнтиСпам)")]
                public int FloodTime;
                [JsonProperty("Список плохих слов")]
                public List<string> BadWords = new List<string>();
            }

            internal class InterfaceSettings
            {
                [JsonProperty("Основной цвет UI")]
                public string MainColor;
                [JsonProperty("Дополнительный цвет UI")]
                public string TwoMainColor;
                [JsonProperty("Цвет кнопок")]
                public string ButtonColor;
                [JsonProperty("Цвет текста")]
                public string LabelColor;
                [JsonProperty("Настройка расположения UI уведомления")]
                public AlertInterfaceSettings AlertInterfaceSetting;

                internal class AlertInterfaceSettings
                {
                    [JsonProperty("AnchorMin")]
                    public string AnchorMin;
                    [JsonProperty("AnchorMax")]
                    public string AnchorMax;
                    [JsonProperty("OffsetMin")]
                    public string OffsetMin;
                    [JsonProperty("OffsetMax")]
                    public string OffsetMax;
                }
            }

            internal class AlertSetting
            {
                [JsonProperty("Включить случайное сообщение зашедшему игроку")]
                public bool WelcomeMessageUse;
                [JsonProperty("Список сообщений игроку при входе")]
                public List<string> WelcomeMessage = new List<string>();
                [JsonProperty("Уведомлять о входе игрока в чат")]
                public bool ConnectedAlert;
                [JsonProperty("Отображать страну зашедшего игрока")]
                public bool ConnectedWorld;
                [JsonProperty("Уведомлять о выходе игрока в чат")]
                public bool DisconnectedAlert;
                [JsonProperty("Отображать причину выхода игрока")]
                public bool DisconnectedReason;
                [JsonProperty("При уведомлении о входе/выходе игрока отображать его аватар напротив ника")]
                public bool ConnectedAvatarUse;
                [JsonProperty("Включить автоматические сообщения в чат")]
                public bool AlertMessage;
                [JsonProperty("Настройка отправки автоматических сообщений в чат")]
                public List<string> MessageList;
                [JsonProperty("Интервал отправки сообщений в чат(Броадкастер)")]
                public int MessageListTimer;
            }

            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    PrefixList = new List<AdvancedFuncion>
                    {
                        new AdvancedFuncion
                        {
                            Permissions = "iqchat.default",
                            Argument = "<color=yellow><b>[+]</b></color>",
                        },
                        new AdvancedFuncion
                        {
                            Permissions = "iqchat.default",
                            Argument = "<color=yellow><b>[ИГРОК]</b></color>",
                        },
                        new AdvancedFuncion
                        {
                            Permissions = "iqchat.vip",
                            Argument = "<color=yellow><b>[VIP]</b></color>",
                        },
                    },
                    NickColorList = new List<AdvancedFuncion>
                    {
                        new AdvancedFuncion
                        {
                            Permissions = "iqchat.default",
                            Argument = "#DBEAEC",
                        },
                        new AdvancedFuncion
                        {
                            Permissions = "iqchat.default",
                            Argument = "#FFC428",
                        },
                        new AdvancedFuncion
                        {
                            Permissions = "iqchat.vip",
                            Argument = "#45AAB4",
                        },
                    },
                    MessageColorList = new List<AdvancedFuncion>
                    {
                        new AdvancedFuncion
                        {
                            Permissions = "iqchat.default",
                            Argument = "#DBEAEC",
                        },
                        new AdvancedFuncion
                        {
                            Permissions = "iqchat.default",
                            Argument = "#FFC428",
                        },
                        new AdvancedFuncion
                        {
                            Permissions = "iqchat.vip",
                            Argument = "#45AAB4",
                        },
                    },
                    PrefixFoDefaults = new PrefixFoDefault
                    {                      
                        PrefixDefault = "",
                        NickDefault = "",
                        MessageDefault = "",
                    },
                    RustPlusSettings = new RustPlus
                    {
                        UseRustPlus = true,
                        DisplayNameAlert = "СУПЕР СЕРВЕР",
                    },
                    MessageSetting = new MessageSettings
                    {
                        UseBadWords = true,
                        HideAdminGave = true,
                        IgnoreUsePM = true,
                        MuteTeamChat = true,
                        PermAdminImmunitetAntispam = "iqchat.adminspam",
                        BroadcastTitle = "<color=#007FFF><b>[ОПОВЕЩЕНИЕ]</b></color>",
                        BroadcastColor = "#74ade1",
                        ReplaceBadWord = "Ругаюсь матом",
                        Steam64IDAvatar = 0,
                        TimeDeleteAlertUI = 5,
                        PMActivate = true,
                        SoundPM = "assets/bundled/prefabs/fx/notice/stack.world.fx.prefab",
                        AntiSpamActivate = true,
                        FloodTime = 5,
                        FormatingMessage = true,
                        MultiPrefix = true,
                        BadWords = new List<string> { "хуй", "гей", "говно", "бля", "тварь" }
                    },
                    ReasonListChat = new List<ReasonMuteChat>
                    {
                        new ReasonMuteChat
                        {
                            Reason = "Оскорбление родителей",
                            TimeMute = 1200,
                        },
                        new ReasonMuteChat
                        {
                            Reason = "Оскорбление игроков",
                            TimeMute = 100
                        }
                    },
                    RenamePermission = "iqchat.renameuse",                  
                    AlertSettings = new AlertSetting
                    {
                        MessageListTimer = 60,
                        WelcomeMessageUse = true,
                        ConnectedAlert = true,
                        ConnectedWorld = true,
                        DisconnectedAlert = true,
                        DisconnectedReason = true,
                        AlertMessage = true,
                        ConnectedAvatarUse = true,
                        MessageList = new List<string>
                        {
                        "Автоматическое сообщение #1",
                        "Автоматическое сообщение #2",
                        "Автоматическое сообщение #3",
                        "Автоматическое сообщение #4",
                        "Автоматическое сообщение #5",
                        "Автоматическое сообщение #6",
                        },
                        WelcomeMessage = new List<string>
                        {
                            "Добро пожаловать на сервер SUPERSERVER\nРады,что выбрал именно нас!",
                            "С возвращением на сервер!\nЖелаем тебе удачи",
                            "Добро пожаловать на сервер\nУ нас самые лучшие плагины",
                        },

                    },
                    InterfaceSetting = new InterfaceSettings
                    {
                        MainColor = "#000000C0",
                        TwoMainColor = "#762424FF",
                        ButtonColor = "#802A2AFF",
                        LabelColor = "#D1C7BEFF",
                        AlertInterfaceSetting = new InterfaceSettings.AlertInterfaceSettings
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "0 1",
                            OffsetMin = "0 -90",
                            OffsetMax = "320 -20"
                        }
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
                PrintWarning("Ошибка #132" + $"чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }
            NextTick(SaveConfig);
        }

        void RegisteredPermissions()
        {
            for (int MsgColor = 0; MsgColor < config.MessageColorList.Count; MsgColor++)
                if (!permission.PermissionExists(config.MessageColorList[MsgColor].Permissions, this))
                    permission.RegisterPermission(config.MessageColorList[MsgColor].Permissions, this);

            for (int NickColorList = 0; NickColorList < config.NickColorList.Count; NickColorList++)
                if (!permission.PermissionExists(config.NickColorList[NickColorList].Permissions, this))
                    permission.RegisterPermission(config.NickColorList[NickColorList].Permissions, this);

            for (int PrefixList = 0; PrefixList < config.PrefixList.Count; PrefixList++)
                if (!permission.PermissionExists(config.PrefixList[PrefixList].Permissions, this))
                    permission.RegisterPermission(config.PrefixList[PrefixList].Permissions, this);

            permission.RegisterPermission(config.RenamePermission, this);
            permission.RegisterPermission(PermMuteMenu, this);
            permission.RegisterPermission(config.MessageSetting.PermAdminImmunitetAntispam,this);
            PrintWarning("Permissions - completed");
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Data
        [JsonProperty("Дата с настройкой чата игрока")] public Dictionary<ulong, SettingUser> ChatSettingUser = new Dictionary<ulong, SettingUser>();
        [JsonProperty("Дата с Административной настройкой")] public AdminSettings AdminSetting = new AdminSettings();
        public class SettingUser
        {
            public string ChatPrefix;
            public List<string> MultiPrefix = new List<string>();
            public string NickColor;
            public string MessageColor;
            public double MuteChatTime;
            public double MuteVoiceTime;
            public List<ulong> IgnoredUsers = new List<ulong>();
        }

        public class AdminSettings
        {
            public bool MuteChatAll;
            public bool MuteVoiceAll;
            public Dictionary<ulong, string> RenameList = new Dictionary<ulong, string>()
;        }
        void ReadData()
        {
            ChatSettingUser = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, SettingUser>>("IQChat/IQUser");
            AdminSetting = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<AdminSettings>("IQChat/AdminSetting");
        }
        void WriteData()
        {
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQChat/IQUser", ChatSettingUser);
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQChat/AdminSetting", AdminSetting);
        }

        void RegisteredDataUser(BasePlayer player)
        {
            if (!ChatSettingUser.ContainsKey(player.userID))
                ChatSettingUser.Add(player.userID, new SettingUser
                {
                    ChatPrefix = config.PrefixFoDefaults.PrefixDefault,
                    NickColor = config.PrefixFoDefaults.NickDefault,
                    MessageColor = config.PrefixFoDefaults.MessageDefault,
                    MuteChatTime = 0,
                    MuteVoiceTime = 0,
                    MultiPrefix = new List<string> { },
                    IgnoredUsers = new List<ulong> { },
                    
                });
        }

        #endregion

        #region Hooks
      
        private bool OnPlayerChat(BasePlayer player, string message, Chat.ChatChannel channel)
        {
            if (Interface.Oxide.CallHook("CanChatMessage", player, message) != null) return false;
            SeparatorChat(channel, player, message);
            return false;
        }
        private object OnServerMessage(string message, string name)
        {
            if (config.MessageSetting.HideAdminGave)
                if (message.Contains("gave") && name == "SERVER")
                    return true;
            return null;
        }

        object OnPlayerVoice(BasePlayer player, Byte[] data)
        {
            var DataPlayer = ChatSettingUser[player.userID];
            bool IsMuted = DataPlayer.MuteVoiceTime > CurrentTime() ? true : false;
            if (IsMuted)
                return false;
            return null;
        }

        private void OnServerInitialized()
        {
            ReadData();
            foreach (var p in BasePlayer.activePlayerList)
            {
                RegisteredDataUser(p);
            }
            timer.Every(320f, () => { ReturnDefaultData(); });
            ReturnDefaultData();

            RegisteredPermissions();
            WriteData();
            BroadcastAuto();
        }
        void OnPlayerConnected(BasePlayer player)
        {
            RegisteredDataUser(player);
            var Alert = config.AlertSettings;
            if (Alert.ConnectedAlert)
            {
                string Avatar = Alert.ConnectedAvatarUse ? player.UserIDString : "";
                if(config.AlertSettings.ConnectedWorld)
                {
                    webrequest.Enqueue("http://ip-api.com/json/" + player.net.connection.ipaddress.Split(':')[0], null, (code, response) =>
                    {
                        if (code != 200 || response == null)
                            return;

                        string country = JsonConvert.DeserializeObject<Response>(response).Country;
                        ReplyBroadcast(String.Format(lang.GetMessage("WELCOME_PLAYER_WORLD", this, player.UserIDString), player.displayName, country), "", Avatar);
                    }, this);
                }
                else ReplyBroadcast(String.Format(lang.GetMessage("WELCOME_PLAYER", this, player.UserIDString), player.displayName), "", Avatar);
            }
            if (Alert.WelcomeMessageUse)
            {
                int RandomMessage = UnityEngine.Random.Range(0, Alert.WelcomeMessage.Count);
                string WelcomeMessage = Alert.WelcomeMessage[RandomMessage];
                ReplySystem(Chat.ChatChannel.Global, player, WelcomeMessage);
            }
        }      
        void Unload() => WriteData();

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            var Alert = config.AlertSettings;
            if (Alert.DisconnectedAlert)
            {
                string Avatar = Alert.ConnectedAvatarUse ? player.UserIDString : "";

                string LangLeave = config.AlertSettings.DisconnectedReason ? String.Format(lang.GetMessage("LEAVE_PLAYER_REASON", this, player.UserIDString), player.displayName, reason) : String.Format(lang.GetMessage("LEAVE_PLAYER", this, player.UserIDString), player.displayName);
                ReplyBroadcast(LangLeave, "", Avatar);
            }
        }
        #endregion

        #region Func
        public bool IsMutedUser(ulong userID)
        {
            var DataPlayer = ChatSettingUser[userID];
            return DataPlayer.MuteChatTime > CurrentTime() ? true : false;
        }
        private void SeparatorChat(Chat.ChatChannel channel, BasePlayer player, string Message)
        {
            var DataPlayer = ChatSettingUser[player.userID];

            if (IsMutedUser(player.userID))
            {
                ReplySystem(Chat.ChatChannel.Global, player, String.Format(lang.GetMessage("FUNC_MESSAGE_ISMUTED_TRUE", this, player.UserIDString), FormatTime(TimeSpan.FromSeconds(DataPlayer.MuteChatTime - CurrentTime()))));
                return;
            }

            var MessageSettings = config.MessageSetting;
            string OutMessage = Message;
            string PrefxiPlayer = "";
            string MessageSeparator = "";
            string ColorNickPlayer = DataPlayer.NickColor;
            string ColorMessagePlayer = DataPlayer.MessageColor;
            string DisplayName = AdminSetting.RenameList.ContainsKey(player.userID) ? AdminSetting.RenameList[player.userID] : player.displayName;

            if (MessageSettings.FormatingMessage)
                OutMessage = $"{Message.ToLower().Substring(0, 1).ToUpper()}{Message.Remove(0, 1).ToLower()}";

            if (MessageSettings.UseBadWords)
                foreach (var DetectedMessage in OutMessage.Split(' '))
                    if (MessageSettings.BadWords.Contains(DetectedMessage.ToLower()))
                    {
                        OutMessage = OutMessage.Replace(DetectedMessage, MessageSettings.ReplaceBadWord);
                        BadWords(player);
                    }

            if (MessageSettings.MultiPrefix)
            {
                if (DataPlayer.MultiPrefix != null)

                    for (int i = 0; i < DataPlayer.MultiPrefix.Count; i++)
                        PrefxiPlayer += DataPlayer.MultiPrefix[i];
            }
            else PrefxiPlayer = DataPlayer.ChatPrefix;

            string ModifiedNick = string.IsNullOrWhiteSpace(ColorNickPlayer) ? player.IsAdmin ? $"<color=#a8fc55>{DisplayName}</color>" : $"<color=#54aafe>{DisplayName}</color>" : $"<color={ColorNickPlayer}>{DisplayName}</color>";
            string ModifiedMessage = string.IsNullOrWhiteSpace(ColorMessagePlayer) ? OutMessage : $"<color={ColorMessagePlayer}>{OutMessage}</color>";
            string ModifiedChannel = channel == Chat.ChatChannel.Team ? "<color=#a5e664>[Team]</color>" : "";
            MessageSeparator = $"{ModifiedChannel} {PrefxiPlayer} {ModifiedNick}: {ModifiedMessage}";

            if (config.RustPlusSettings.UseRustPlus)
                if (channel == Chat.ChatChannel.Team)
                    Util.BroadcastTeamChat(player.Team, player.userID, player.displayName, OutMessage, DataPlayer.MessageColor);

            ReplyChat(channel, player, MessageSeparator);
            Puts($"{player}: {OutMessage}");
            Log($"СООБЩЕНИЕ В ЧАТ : {player}: {ModifiedChannel} {OutMessage}");

            RCon.Broadcast(RCon.LogType.Chat, new Chat.ChatEntry
            {
                Message = $"{player.displayName} : {OutMessage}",
                UserId = player.UserIDString,
                Username = player.displayName,
                Channel = channel,
                Time = (DateTime.UtcNow.Hour * 3600) + (DateTime.UtcNow.Minute * 60),
            });
        }      

        public void ReturnDefaultData()
        {
            var Default = config.PrefixFoDefaults;
            foreach (var DataPlayer in ChatSettingUser)
            {
                foreach (var Prefix in config.PrefixList.Where(x => x.Argument == DataPlayer.Value.ChatPrefix))
                    if (!permission.UserHasPermission(DataPlayer.Key.ToString(), Prefix.Permissions))
                    {
                        DataPlayer.Value.ChatPrefix = Default.PrefixDefault;
                        if (config.MessageSetting.MultiPrefix)
                        {
                            DataPlayer.Value.MultiPrefix.Clear();
                            DataPlayer.Value.MultiPrefix.Add(Default.PrefixDefault);
                        }
                    }

                foreach (var ColorMsg in config.MessageColorList.Where(x => x.Argument == DataPlayer.Value.MessageColor))
                    if (!permission.UserHasPermission(DataPlayer.Key.ToString(), ColorMsg.Permissions))
                        DataPlayer.Value.MessageColor = Default.MessageDefault;

                foreach (var ColorNick in config.NickColorList.Where(x => x.Argument == DataPlayer.Value.NickColor))
                    if (!permission.UserHasPermission(DataPlayer.Key.ToString(), ColorNick.Permissions))
                        DataPlayer.Value.NickColor = Default.NickDefault;
            }
        }
        public void BroadcastAuto()
        {
            var Alert = config.AlertSettings;
            if (Alert.AlertMessage)
            {
                timer.Every(Alert.MessageListTimer, () =>
                 {
                     var RandomMsg = Alert.MessageList[UnityEngine.Random.Range(0, Alert.MessageList.Count)];
                     ReplyBroadcast(RandomMsg);
                 });
            }
        }
        public void MutePlayer(BasePlayer player, string Format, int ReasonIndex, string ResonCustom = "",string TimeCustom = "", BasePlayer Initiator = null)
        {
            var cfg = config.ReasonListChat[ReasonIndex];
            string Reason = string.IsNullOrEmpty(ResonCustom) ? cfg.Reason : ResonCustom;
            float TimeMute = string.IsNullOrEmpty(TimeCustom) ? cfg.TimeMute : Convert.ToInt32(TimeCustom);
            string DisplayInititator = Initiator == null ? "Администратор" : Initiator.displayName;
            switch (Format)
            {
                case "mutechat":
                    {
                        ChatSettingUser[player.userID].MuteChatTime = TimeMute + CurrentTime();
                        ReplyBroadcast(string.Format(lang.GetMessage("FUNC_MESSAGE_MUTE_CHAT", this, player.UserIDString), DisplayInititator, player.displayName, FormatTime(TimeSpan.FromSeconds(TimeMute)), Reason));
                        if (Initiator != null)
                            SetMute(Initiator);
                        break;
                    }
                case "unmutechat":
                    {
                        ChatSettingUser[player.userID].MuteChatTime = 0;
                        ReplyBroadcast(string.Format(lang.GetMessage("FUNC_MESSAGE_UNMUTE_CHAT", this, player.UserIDString), DisplayInititator));
                        break;
                    }
                case "mutevoice":
                    {
                        ChatSettingUser[player.userID].MuteVoiceTime = TimeMute + CurrentTime();
                        ReplyBroadcast(string.Format(lang.GetMessage("FUNC_MESSAGE_MUTE_VOICE", this), DisplayInititator, player.displayName, FormatTime(TimeSpan.FromSeconds(TimeMute)), Reason)); 
                        break;
                    }
            }
        }       
        public void MuteAllChatPlayer(BasePlayer player,float TimeMute = 86400) => ChatSettingUser[player.userID].MuteChatTime = TimeMute + CurrentTime();
        public void RenameFunc(BasePlayer player,string NewName)
        {
            if (permission.UserHasPermission(player.UserIDString, config.RenamePermission))
            {
                if (!AdminSetting.RenameList.ContainsKey(player.userID))
                    AdminSetting.RenameList.Add(player.userID, NewName);
                else AdminSetting.RenameList[player.userID] = NewName;
                ReplySystem(Chat.ChatChannel.Global, player, String.Format(lang.GetMessage("COMMAND_RENAME_SUCCES", this, player.UserIDString),NewName));
            }
            else ReplySystem(Chat.ChatChannel.Global, player, lang.GetMessage("COMMAND_NOT_PERMISSION", this, player.UserIDString)); 
        }
        void AlertUI(BasePlayer player, string[] arg)
        {
            if (player != null)
                if (!player.IsAdmin) return;

            if (arg.Length == 0 || arg == null)
            {
                ReplySystem(Chat.ChatChannel.Global, player, lang.GetMessage("FUNC_MESSAGE_NO_ARG_BROADCAST", this, player.UserIDString));
                return;
            }
            string Message = "";
            foreach (var msg in arg)
                Message += " " + msg;

            foreach (var p in BasePlayer.activePlayerList)
                UIAlert(p, Message);
        }
        void Alert(BasePlayer player, string[] arg)
        {
            if (player != null)
                if (!player.IsAdmin) return;

            if (arg.Length == 0 || arg == null)
            {
                if(player != null)
                ReplySystem(Chat.ChatChannel.Global, player, lang.GetMessage("FUNC_MESSAGE_NO_ARG_BROADCAST", this, player.UserIDString));
                return;
            }
            string Message = "";
            foreach (var msg in arg)
                Message += " " + msg;

            ReplyBroadcast(Message);
            if (config.RustPlusSettings.UseRustPlus)
                foreach(var playerList in BasePlayer.activePlayerList)
                    NotificationList.SendNotificationTo(playerList.userID, NotificationChannel.SmartAlarm, config.RustPlusSettings.DisplayNameAlert, Message, Util.GetServerPairingData());
        }

        #endregion

        #region Interface
        static string MAIN_PARENT = "MAIN_PARENT_UI";
        static string MUTE_MENU_PARENT = "MUTE_MENU_UI";
        static string ELEMENT_SETTINGS = "NEW_ELEMENT_SETTINGS";
        static string MAIN_ALERT_UI = "ALERT_UI_PLAYER";
        static string PANEL_ACTION = "PANEL_ACTION";
        static string PANEL_ACTION_HELPER = "PANEL_ACTION_HELPER";

        #region MainMenu
        public void UI_MainMenu(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, MAIN_PARENT);
            var Interface = config.InterfaceSetting;

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { FadeIn = 0.15f, Color = "0 0 0 0"}
            }, "Overlay", MAIN_PARENT);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.81875 0.1768519", AnchorMax = "0.9869678 0.8814706" },
                Image = {  Color = HexToRustFormat(Interface.TwoMainColor), Material = "assets/content/ui/uibackgroundblur.mat" }
            },  MAIN_PARENT, PANEL_ACTION);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.93", AnchorMax = "1 1" },
                Text = { Text = lang.GetMessage("TITLE_TWO", this, player.UserIDString), Color = HexToRustFormat(Interface.LabelColor), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter, FadeIn = 0.3f }
            }, PANEL_ACTION);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "0 -45", OffsetMax = "215 -5" },
                Button = { Close = MAIN_PARENT, Color = HexToRustFormat(Interface.ButtonColor), Material = "assets/content/ui/uibackgroundblur.mat" },
                Text = { Text = lang.GetMessage("UI_CLOSE_BTN", this, player.UserIDString), Color = HexToRustFormat(Interface.LabelColor), Align = TextAnchor.MiddleCenter }
            }, PANEL_ACTION);

            #region ACTION BUTTON

            #region SettingPrefix

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.1243169 0.8383705", AnchorMax = "1 0.9179095" },
                Button = { Command = "iq_chat setting prefix", Color = HexToRustFormat(Interface.ButtonColor), Material = "assets/content/ui/uibackgroundblur.mat"},
                Text = { Text = lang.GetMessage("UI_TEXT_PREFIX", this, player.UserIDString), Color = HexToRustFormat(Interface.LabelColor), Align = TextAnchor.MiddleCenter }
            }, PANEL_ACTION, "PREFIX_SETTING");

            container.Add(new CuiElement
            {
                Parent = "PREFIX_SETTING",
                Components =
                    {
                        new CuiImageComponent {  Color = HexToRustFormat(Interface.LabelColor), Sprite = "assets/icons/sign.png" },
                        new CuiRectTransformComponent { AnchorMin = "0.04236809 0.1937432", AnchorMax = "0.1696546 0.7884976" }
                    }
            });

            #endregion

            #region SettingColorNick

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.1243169 0.7371891", AnchorMax = "1 0.8167281" },
                Button = { Command = "iq_chat setting nick", Color = HexToRustFormat(Interface.ButtonColor), Material = "assets/content/ui/uibackgroundblur.mat" },
                Text = { Text = lang.GetMessage("UI_TEXT_COLOR_NICK", this, player.UserIDString), Color = HexToRustFormat(Interface.LabelColor), Align = TextAnchor.MiddleCenter }
            }, PANEL_ACTION, "COLOR_NICK_SETTING");

            container.Add(new CuiElement
            {
                Parent = "COLOR_NICK_SETTING",
                Components =
                    {
                        new CuiImageComponent {  Color = HexToRustFormat(Interface.LabelColor), Sprite = "assets/icons/sign.png"  },
                        new CuiRectTransformComponent { AnchorMin = "0.04236809 0.1937432", AnchorMax = "0.1696546 0.7884976" }
                    }
            });

            #endregion

            #region SettingText

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.1243169 0.6346937", AnchorMax = "1 0.7142327" },
                Button = { Command = "iq_chat setting chat", Color = HexToRustFormat(Interface.ButtonColor), Material = "assets/content/ui/uibackgroundblur.mat" },
                Text = { Text = lang.GetMessage("UI_TEXT_COLOR_MSG", this, player.UserIDString), Color = HexToRustFormat(Interface.LabelColor), Align = TextAnchor.MiddleCenter }
            }, PANEL_ACTION, "TEXT_SETTING");

            container.Add(new CuiElement
            {
                Parent = "TEXT_SETTING",
                Components =
                    {
                        new CuiImageComponent {  Color = HexToRustFormat(Interface.LabelColor), Sprite = "assets/icons/sign.png" },
                        new CuiRectTransformComponent { AnchorMin = "0.04236809 0.1937432", AnchorMax = "0.1696546 0.7884976" }
                    }
            });

            #endregion

            #endregion

            #region ADMIN

            #region HELPERS
            if (permission.UserHasPermission(player.UserIDString, PermMuteMenu))
            {
                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0.4323258", AnchorMax = "1 0.5171261" },
                    Text = { Text = lang.GetMessage("UI_TEXT_MODER_PANEL", this, player.UserIDString), Color = HexToRustFormat(Interface.LabelColor), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
                }, PANEL_ACTION);

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.1243169 0.3298316", AnchorMax = "1 0.4093724" },
                    Button = { Command = $"iq_chat mute menu", Color = HexToRustFormat(Interface.ButtonColor), Material = "assets/content/ui/uibackgroundblur.mat" },
                    Text = { Text = lang.GetMessage("UI_TEXT_MUTE_MENU_BTN", this, player.UserIDString), Color = HexToRustFormat(Interface.LabelColor), Align = TextAnchor.MiddleCenter }
                }, PANEL_ACTION, "CHAT_SETTING_USER");

                container.Add(new CuiElement
                {
                    Parent = "CHAT_SETTING_USER",
                    Components =
                    {
                        new CuiImageComponent {  Color = HexToRustFormat(Interface.LabelColor), Sprite = "assets/icons/subtract.png" },
                        new CuiRectTransformComponent { AnchorMin = "0.04236809 0.1937432", AnchorMax = "0.1696546 0.7884976" }
                    }
                });
            }
            #endregion

            #region OWNER
            if (player.IsAdmin)
            {
                string CommandChat = "iq_chat admin_chat";
                string TextMuteChatButton = AdminSetting.MuteChatAll ? "UI_TEXT_ADMIN_PANEL_UNMUTE_CHAT_ALL" : "UI_TEXT_ADMIN_PANEL_MUTE_CHAT_ALL";
                string CommandMuteChatButton = AdminSetting.MuteChatAll ? "unmutechat" : "mutechat";
                string CommandVoice = "iq_chat admin_voice";
                string TextMuteVoiceButton = AdminSetting.MuteVoiceAll ? "UI_TEXT_ADMIN_PANEL_UNMUTE_VOICE_ALL" : "UI_TEXT_ADMIN_PANEL_MUTE_VOICE_ALL";
                string CommandMuteVoiceButton = AdminSetting.MuteVoiceAll ? "unmutevoice" : "mutevoice";

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0.224706", AnchorMax = "1 0.3042471" },
                    Text = { Text = lang.GetMessage("UI_TEXT_ADMIN_PANEL", this, player.UserIDString), Color = HexToRustFormat(Interface.LabelColor), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
                },  PANEL_ACTION);

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.1243169 0.1208954", AnchorMax = "1 0.200437" },
                    Button = { Close = MAIN_PARENT, Command = $"{CommandChat} {CommandMuteChatButton}", Color = HexToRustFormat(Interface.ButtonColor), Material = "assets/content/ui/uibackgroundblur.mat"},
                    Text = { Text = lang.GetMessage(TextMuteChatButton, this, player.UserIDString), Color = HexToRustFormat(Interface.LabelColor), Align = TextAnchor.MiddleCenter }
                },  PANEL_ACTION, "CHAT_SETTING_ADMIN");

                container.Add(new CuiElement
                {
                    Parent = "CHAT_SETTING_ADMIN",
                    Components =
                    {
                        new CuiImageComponent {  Color = HexToRustFormat(Interface.LabelColor), Sprite = "assets/icons/subtract.png" },
                        new CuiRectTransformComponent { AnchorMin = "0.04236809 0.1937432", AnchorMax = "0.1696546 0.7884976" }
                    }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.1243169 0.02496903", AnchorMax = "1 0.1045107" },
                    Button = { Close = MAIN_PARENT, Command = $"{CommandVoice} {CommandMuteVoiceButton}", Color = HexToRustFormat(Interface.ButtonColor), Material = "assets/content/ui/uibackgroundblur.mat" },
                    Text = { Text = lang.GetMessage(TextMuteVoiceButton, this, player.UserIDString), Color = HexToRustFormat(Interface.LabelColor), Align = TextAnchor.MiddleCenter }
                },  PANEL_ACTION, "VOICE_SETTING_ADMIN");
            }
            container.Add(new CuiElement
            {
                Parent = "VOICE_SETTING_ADMIN",
                Components =
                    {
                        new CuiImageComponent {  Color = HexToRustFormat(Interface.LabelColor), Sprite = "assets/icons/subtract.png" },
                        new CuiRectTransformComponent { AnchorMin = "0.04236809 0.1937432", AnchorMax = "0.1696546 0.7884976" }
                    }
            });

            #endregion
            
            #endregion

            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region PrefixSetting
        public void UI_PrefixSetting(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, ELEMENT_SETTINGS);
            CuiHelper.DestroyUi(player, MUTE_MENU_PARENT);
            CuiHelper.DestroyUi(player, PANEL_ACTION_HELPER);

            var Interface = config.InterfaceSetting;
            string Prefix = "";
            if (config.MessageSetting.MultiPrefix)
            {
                if (ChatSettingUser[player.userID].MultiPrefix != null)
                    for (int g = 0; g < ChatSettingUser[player.userID].MultiPrefix.Count; g++)
                        Prefix += ChatSettingUser[player.userID].MultiPrefix[g];
                else Prefix = ChatSettingUser[player.userID].ChatPrefix;
            }
            var PrefixList = config.PrefixList;

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5020834 0.1148148", AnchorMax = "0.8150954 0.8814815" },
                Image = { Color = HexToRustFormat(Interface.TwoMainColor), Material = "assets/content/ui/uibackgroundblur.mat" }
            }, MAIN_PARENT, ELEMENT_SETTINGS);

            int x = 0, y = 0, i = 0;
            foreach (var ElementPrefix in PrefixList)
            {
                if (!permission.UserHasPermission(player.UserIDString, ElementPrefix.Permissions)) continue;
                string LockStatus = "assets/icons/unlock.png";

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{0 + (x * 0.525)} {0.8514473 - (y * 0.1)}", AnchorMax = $"{0.4708602 + (x * 0.525)} {0.9245502 - (y * 0.1)}" },
                    Button = { Command = $"iq_chat action prefix {ElementPrefix.Argument} {ElementPrefix.Permissions}", Color = HexToRustFormat(config.InterfaceSetting.ButtonColor), Material = "assets/content/ui/uibackgroundblur.mat", FadeIn = 0.1f },
                    Text = { Text = ElementPrefix.Argument, FontSize = 17, Color = HexToRustFormat(Interface.LabelColor), Align = TextAnchor.MiddleCenter }
                }, ELEMENT_SETTINGS, $"BUTTON_{i}");

                container.Add(new CuiElement
                {
                    Parent = $"BUTTON_{i}",
                    Components =
                    {
                        new CuiImageComponent {  Color = HexToRustFormat(Interface.LabelColor), Sprite = LockStatus },
                        new CuiRectTransformComponent { AnchorMin = "0.03887215 0.1982514", AnchorMax = "0.1660901 0.7930056" }
                    }
                });

                x++;
                if (x == 2)
                {
                    y++;
                    x = 0;
                }
                i++;
            }

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.93", AnchorMax = "1 1" },
                Text = { Text = lang.GetMessage("UI_TITLE_NEW_PREFIX_ELEMENT", this, player.UserIDString), Color = HexToRustFormat(Interface.LabelColor), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter, FadeIn = 0.3f }
            }, ELEMENT_SETTINGS);

            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region NickSetting
        public void UI_NickSetting(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, ELEMENT_SETTINGS);
            CuiHelper.DestroyUi(player, MUTE_MENU_PARENT);
            CuiHelper.DestroyUi(player, PANEL_ACTION_HELPER);

            var Interface = config.InterfaceSetting;
            var ColorList = config.NickColorList;

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5020834 0.1148148", AnchorMax = "0.8150954 0.8814815" },
                Image = { Color = HexToRustFormat(Interface.TwoMainColor), Material = "assets/content/ui/uibackgroundblur.mat" }
            }, MAIN_PARENT, ELEMENT_SETTINGS);

            int x = 0, y = 0, i = 0;
            foreach (var ElementColor in ColorList)
            {
                if (!permission.UserHasPermission(player.UserIDString, ElementColor.Permissions)) continue;
                string LockStatus = "assets/icons/unlock.png";

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{0 + (x * 0.525)} {0.8514473 - (y * 0.1)}", AnchorMax = $"{0.4708602 + (x * 0.525)} {0.9245502 - (y * 0.1)}" },
                    Button = { Command = $"iq_chat action nick {ElementColor.Argument} {ElementColor.Permissions}", Color = HexToRustFormat(config.InterfaceSetting.ButtonColor), Material = "assets/content/ui/uibackgroundblur.mat", FadeIn = 0.1f },
                    Text = { Text = $"<color={ElementColor.Argument}>{player.displayName}</color>", FontSize = 17, Color = HexToRustFormat(Interface.LabelColor), Align = TextAnchor.MiddleCenter }
                }, ELEMENT_SETTINGS, $"BUTTON_{i}");

                container.Add(new CuiElement
                {
                    Parent = $"BUTTON_{i}",
                    Components =
                    {
                        new CuiImageComponent {  Color = HexToRustFormat(Interface.LabelColor), Sprite = LockStatus },
                        new CuiRectTransformComponent { AnchorMin = "0.03887215 0.1982514", AnchorMax = "0.1660901 0.7930056" }
                    }
                });

                x++;
                if (x == 2)
                {
                    y++;
                    x = 0;
                }
                i++;
            }

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.93", AnchorMax = "1 1" },
                Text = { Text = lang.GetMessage("UI_TITLE_NEW_NICK_COLOR_ELEMENT", this, player.UserIDString), Color = HexToRustFormat(Interface.LabelColor), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter, FadeIn = 0.3f }
            }, ELEMENT_SETTINGS);

            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region ColorSetting
        public void UI_TextSetting(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, ELEMENT_SETTINGS);
            CuiHelper.DestroyUi(player, MUTE_MENU_PARENT);
            CuiHelper.DestroyUi(player, PANEL_ACTION_HELPER);

            var Interface = config.InterfaceSetting;
            var ColorList = config.MessageColorList;

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5020834 0.1148148", AnchorMax = "0.8150954 0.8814815" },
                Image = { Color = HexToRustFormat(Interface.TwoMainColor), Material = "assets/content/ui/uibackgroundblur.mat" }
            }, MAIN_PARENT, ELEMENT_SETTINGS);

            int x = 0, y = 0, i = 0;
            foreach (var ElementColor in ColorList)
            {
                if (!permission.UserHasPermission(player.UserIDString, ElementColor.Permissions)) continue;
                string LockStatus = "assets/icons/unlock.png";

                container.Add(new CuiButton
                {
                    FadeOut = 0.2f,
                    RectTransform = { AnchorMin = $"{0 + (x * 0.525)} {0.8514473 - (y * 0.1)}", AnchorMax = $"{0.4708602 + (x * 0.525)} {0.9245502 - (y * 0.1)}" },
                    Button = { Command = $"iq_chat action chat {ElementColor.Argument} {ElementColor.Permissions}", Color = HexToRustFormat(config.InterfaceSetting.ButtonColor), Material = "assets/content/ui/uibackgroundblur.mat", FadeIn = 0.1f },
                    Text = { Text = $"<color={ElementColor.Argument}>Сообщение</color>", Align = TextAnchor.MiddleCenter }
                }, ELEMENT_SETTINGS, $"BUTTON_{i}");

                container.Add(new CuiElement
                {
                    Parent = $"BUTTON_{i}",
                    Components =
                    {
                        new CuiImageComponent {  Color = HexToRustFormat(Interface.LabelColor), Sprite = LockStatus },
                        new CuiRectTransformComponent { AnchorMin = "0.03887215 0.1982514", AnchorMax = "0.1660901 0.7930056" }
                    }
                });
                x++;
                if (x == 2)
                {
                    y++;
                    x = 0;
                }
            }

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.93", AnchorMax = "1 1" },
                Text = { Text = lang.GetMessage("UI_TITLE_NEW_MESSAGE_COLOR_ELEMENT", this, player.UserIDString), Color = HexToRustFormat(Interface.LabelColor), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter, FadeIn = 0.3f }
            }, ELEMENT_SETTINGS);

            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region MuteMenu
        public void UI_MuteMenu(BasePlayer player, string TargetName = "")
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, MUTE_MENU_PARENT);
            CuiHelper.DestroyUi(player, ELEMENT_SETTINGS);
            CuiHelper.DestroyUi(player, PANEL_ACTION_HELPER);

            var Interface = config.InterfaceSetting;

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.1546875 0.1148148", AnchorMax = "0.8150954 0.8814815" },
                Image = { Color = HexToRustFormat(Interface.TwoMainColor) }
            }, MAIN_PARENT, MUTE_MENU_PARENT);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.9227053", AnchorMax = "1 1" },
                Text = { Text = lang.GetMessage("UI_MUTE_PANEL_TITLE", this, player.UserIDString), Color = HexToRustFormat(Interface.LabelColor), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter, FadeIn = 0.3f }
            },  MUTE_MENU_PARENT);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.898551", AnchorMax = "1 0.9456524" },
                Text = { Text = lang.GetMessage("UI_MUTE_PANEL_TITLE_ACTION", this, player.UserIDString), Color = HexToRustFormat(Interface.LabelColor), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter, FadeIn = 0.3f }
            }, MUTE_MENU_PARENT);

            string SearchName = "";
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.8417874", AnchorMax = "1 0.8961352" },
                Image = { Color = HexToRustFormat(Interface.ButtonColor) }
            }, MUTE_MENU_PARENT, MUTE_MENU_PARENT + ".Input");

            container.Add(new CuiElement
            {
                Parent = MUTE_MENU_PARENT + ".Input",
                Name = MUTE_MENU_PARENT + ".Input.Current",
                Components =
                {
                    new CuiInputFieldComponent { Text = SearchName, FontSize = 14,Command = $"mute_search {SearchName}", Align = TextAnchor.MiddleCenter, Color = HexToRustFormat("#ffffffFF"), CharsLimit = 15},
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });

            int x = 0; int y = 0;
            foreach (var pList in BasePlayer.activePlayerList.Where(i => i.displayName.ToLower().Contains(TargetName.ToLower())))
            {
                string LockStatus = ChatSettingUser[pList.userID].MuteChatTime > CurrentTime() ? "assets/icons/lock.png" :
                                    ChatSettingUser[pList.userID].MuteVoiceTime > CurrentTime() ? "assets/icons/lock.png" : "assets/icons/unlock.png";
                container.Add(new CuiButton
                {
                    FadeOut = 0.2f,
                    RectTransform = { AnchorMin = $"{0.006797731 + (x * 0.165)} {0.7838164 - (y * 0.057)}", AnchorMax = $"{0.1661653 + (x * 0.165)} {0.8309178 - (y * 0.057)}" },
                    Button = { Command = $"iq_chat mute actionmenu {pList.userID}", Color = HexToRustFormat(Interface.ButtonColor) },
                    Text = { Text = "", Align = TextAnchor.MiddleCenter }
                }, MUTE_MENU_PARENT, $"BUTTON{player.userID}");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.1611373 0", AnchorMax = "1 1" },
                    Text = { Text = pList.displayName.Replace(" ", ""), FontSize = 12, Color = HexToRustFormat(Interface.LabelColor), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleLeft }
                }, $"BUTTON{player.userID}");

                container.Add(new CuiElement
                {
                    Parent = $"BUTTON{player.userID}",
                    Components =
                    {
                        new CuiImageComponent {  Color = HexToRustFormat(Interface.LabelColor), Sprite = LockStatus },
                        new CuiRectTransformComponent { AnchorMin = "0.02369668 0.2051285", AnchorMax = "0.1374408 0.820514" }
                    }
                });

                x++;
                if (y == 12 && x == 6) break;

                if (x == 6)
                {
                    y++;
                    x = 0;
                }

            };

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.02870133 0.05434785", AnchorMax = "0.3300647 0.08333336" },
                Text = { Text = lang.GetMessage("UI_MUTE_PANEL_TITLE_HELPS_LOCK",this, player.UserIDString), FontSize = 10, Color = HexToRustFormat(Interface.LabelColor), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleLeft }
            },  MUTE_MENU_PARENT);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.02870133 0.01570053", AnchorMax = "0.3300647 0.04468608" },
                Text = { Text = lang.GetMessage("UI_MUTE_PANEL_TITLE_HELPS_UNLOCK", this, player.UserIDString), FontSize = 10, Color = HexToRustFormat(Interface.LabelColor), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleLeft }
            }, MUTE_MENU_PARENT);

            container.Add(new CuiElement
            {
                Parent = MUTE_MENU_PARENT,
                Components =
                    {
                        new CuiImageComponent {  Color = HexToRustFormat(Interface.LabelColor), Sprite = "assets/icons/lock.png" },
                        new CuiRectTransformComponent { AnchorMin = "0.006797716 0.05434785", AnchorMax = "0.02492483 0.08333336" }
                    }
            });

            container.Add(new CuiElement
            {
                Parent = MUTE_MENU_PARENT,
                Components =
                    {
                        new CuiImageComponent {  Color = HexToRustFormat(Interface.LabelColor), Sprite = "assets/icons/unlock.png" },
                        new CuiRectTransformComponent { AnchorMin = "0.006797716 0.01449281", AnchorMax = "0.02492483 0.04347835" }
                    }
            });

            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region MuteAction
        public void UI_MuteTakeAction(BasePlayer player,ulong userID)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, PANEL_ACTION_HELPER);
            var Interface = config.InterfaceSetting;

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.01197916 0.1148148", AnchorMax = "0.1505208 0.8814706" }, 
                Image = { Color = HexToRustFormat(Interface.TwoMainColor), Material = "assets/content/ui/uibackgroundblur.mat" }
            },  MAIN_PARENT, PANEL_ACTION_HELPER);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.919082", AnchorMax = "1 1" },
                Text = { Text = lang.GetMessage("UI_MUTE_TAKE_ACTION_PANEL", this, player.UserIDString), Color = HexToRustFormat(Interface.LabelColor), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter, FadeIn = 0.3f }
            },  PANEL_ACTION_HELPER);

            string LockStatus = ChatSettingUser[userID].MuteChatTime > CurrentTime() ? "assets/icons/unlock.png" :
                    ChatSettingUser[userID].MuteVoiceTime > CurrentTime() ? "assets/icons/unlock.png" : "assets/icons/lock.png";

            string ButtonChat = ChatSettingUser[userID].MuteChatTime > CurrentTime() ?  "UI_MUTE_TAKE_ACTION_CHAT_UNMUTE" : "UI_MUTE_TAKE_ACTION_CHAT";
            string ButtonVoice = ChatSettingUser[userID].MuteVoiceTime > CurrentTime() ? "UI_MUTE_TAKE_ACTION_VOICE_UNMUTE" : "UI_MUTE_TAKE_ACTION_VOICE";
            string ButtonCommandChat = ChatSettingUser[userID].MuteChatTime > CurrentTime() ? $"iq_chat mute action {userID} unmutechat" : $"iq_chat mute action {userID} mute mutechat";
            string ButtonCommandVoice = ChatSettingUser[userID].MuteVoiceTime > CurrentTime() ? $"iq_chat mute action {userID} unmutevoice" : $"iq_chat mute action {userID} mute mutevoice";

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0.8357491", AnchorMax = "0.903084 0.8961352" },
                Button = { Command = ButtonCommandChat, Color = HexToRustFormat(Interface.ButtonColor), Material = "assets/content/ui/uibackgroundblur.mat"},
                Text = { Text = "",  Align = TextAnchor.MiddleCenter }
            },  PANEL_ACTION_HELPER, "CHAT");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.1790024 0", AnchorMax = "1 1" },
                Text = { Text = lang.GetMessage(ButtonChat, this, player.UserIDString), FontSize = 10, Color = HexToRustFormat(Interface.LabelColor), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleLeft }
            }, $"CHAT");

            container.Add(new CuiElement
            {
                Parent = $"CHAT",
                Components =
                    {
                        new CuiImageComponent {  Color = HexToRustFormat(Interface.LabelColor), Sprite = LockStatus },
                        new CuiRectTransformComponent { AnchorMin = "0.02369668 0.2051285", AnchorMax = "0.1374408 0.820514" }
                    }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0.7620788", AnchorMax = "0.903084 0.8224649" },
                Button = { Command = ButtonCommandVoice, Color = HexToRustFormat(Interface.ButtonColor), Material = "assets/content/ui/uibackgroundblur.mat"},
                Text = { Text = "", Align = TextAnchor.MiddleCenter }
            },  PANEL_ACTION_HELPER, "VOICE");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.1790024 0", AnchorMax = "1 1" },
                Text = { Text = lang.GetMessage(ButtonVoice, this, player.UserIDString), FontSize = 10, Color = HexToRustFormat(Interface.LabelColor), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleLeft }
            }, $"VOICE");

            container.Add(new CuiElement
            {
                Parent = $"VOICE",
                Components =
                    {
                        new CuiImageComponent {  Color = HexToRustFormat(Interface.LabelColor), Sprite = LockStatus },
                        new CuiRectTransformComponent { AnchorMin = "0.02369668 0.2051285", AnchorMax = "0.1374408 0.820514" }
                    }
            });

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region ReasonMute
        void UI_ReasonMute(BasePlayer player,ulong userID, string MuteFormat)
        {
            CuiElementContainer container = new CuiElementContainer();
            var Interface = config.InterfaceSetting;

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.6702939", AnchorMax = "1 0.7512119" },
                Text = { Text = lang.GetMessage("UI_MUTE_TAKE_REASON_TITLE", this, player.UserIDString), Color = HexToRustFormat(Interface.LabelColor), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter, FadeIn = 0.3f }
            },  PANEL_ACTION_HELPER);

            int i = 0;
            foreach(var Reason in config.ReasonListChat)
            {           
                container.Add(new CuiButton
                {
                    FadeOut = 0.2f,
                    RectTransform = { AnchorMin = $"0 {0.5942072 - (i * 0.07)}", AnchorMax = $"0.903084 {0.6545933 - (i * 0.07)}" },
                    Button = { Command = $"iq_chat mute action {userID} mute_reason {MuteFormat} {i}", Color = HexToRustFormat(Interface.ButtonColor), Material = "assets/content/ui/uibackgroundblur.mat", FadeIn = 0.1f },
                    Text = { Text = "", Align = TextAnchor.MiddleCenter }
                },  PANEL_ACTION_HELPER, $"BUTTON{i}");

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.1790024 0", AnchorMax = "1 1" },
                    Text = { Text = Reason.Reason, FontSize = 10, Color = HexToRustFormat(Interface.LabelColor), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleLeft }
                },  $"BUTTON{i}");

                container.Add(new CuiElement
                {
                    Parent = $"BUTTON{i}",
                    Components =
                    {
                        new CuiImageComponent {  Color = HexToRustFormat(Interface.LabelColor), Sprite = "assets/icons/favourite_servers.png" },
                        new CuiRectTransformComponent { AnchorMin = "0.02369668 0.2051285", AnchorMax = "0.1374408 0.820514" }
                    }
                });
                i++;
            }

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region UpdateLabel
        public void UpdateLabel(BasePlayer player, SettingUser DataPlayer)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, "UPDATE_LABEL");

            string Prefix = "";
            if (config.MessageSetting.MultiPrefix)
            {
                if (DataPlayer.MultiPrefix != null)
                    for (int i = 0; i < DataPlayer.MultiPrefix.Count; i++)
                        Prefix += DataPlayer.MultiPrefix[i];
            }
            else Prefix = DataPlayer.ChatPrefix;
            string ResultNick = $"<b>{Prefix}<color={DataPlayer.NickColor}>{player.displayName}</color> : <color={DataPlayer.MessageColor}> я лучший</color></b>";

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.07367153" },
                Text = { Text = $"{ResultNick}", FontSize = 14, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf"}
            },  ELEMENT_SETTINGS, "UPDATE_LABEL");

            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region UIAlert
        void UIAlert(BasePlayer player, string Message)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, MAIN_ALERT_UI);
            var Interface = config.InterfaceSetting;
            var Transform = Interface.AlertInterfaceSetting;

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = Transform.AnchorMin, AnchorMax = Transform.AnchorMax, OffsetMin = Transform.OffsetMin, OffsetMax = Transform.OffsetMax },
                Image = { FadeIn = 0.3f, Color = HexToRustFormat(config.InterfaceSetting.ButtonColor), Material = "assets/content/ui/uibackgroundblur.mat" }
            }, "Overlay", MAIN_ALERT_UI);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.025 0.5523812", AnchorMax = "0.1 0.8952706" },
                Image = { Color = HexToRustFormat(Interface.MainColor), Sprite = "assets/icons/upgrade.png" }
            },  MAIN_ALERT_UI);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.1125001 0.5037036", AnchorMax = "1 1" },
                Text = { Text = lang.GetMessage("UI_ALERT_TITLE",this, player.UserIDString), Color = HexToRustFormat(Interface.LabelColor), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleLeft }
            }, MAIN_ALERT_UI);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.5259256" },
                Text = { Text = $"{Message}", FontSize = 12, Color =  HexToRustFormat(Interface.LabelColor), Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }
            }, MAIN_ALERT_UI);

            CuiHelper.AddUi(player, container);

            timer.Once(config.MessageSetting.TimeDeleteAlertUI, () =>
            {
                CuiHelper.DestroyUi(player, MAIN_ALERT_UI);
            });
        }
        #endregion

        #endregion

        #region Command

        #region UsingCommand
        [ConsoleCommand("mute")]
        void MuteCustomAdmin(ConsoleSystem.Arg arg)
        {
            if (arg == null || arg.Args == null || arg.Args.Length != 3 || arg.Args.Length > 3)
            {
                PrintWarning("Неверный синтаксис,используйте : mute Steam64ID Причина Время(секунды)");
                return;
            }
            ulong userID = ulong.Parse(arg.Args[0]);
            if (!userID.IsSteamId())
            {
                PrintWarning("Неверный Steam64ID");
                return;
            }
            string Reason = arg.Args[1];
            string TimeMute = arg.Args[2];
            BasePlayer target = BasePlayer.FindByID(userID);
            if (target == null)
            {
                PrintWarning("Такого игрока нет на сервере");
                return;
            }
            MutePlayer(target, "mutechat", 0, Reason, TimeMute);
            Puts("Успешно");
        }
        [ConsoleCommand("unmute")]
        void UnMuteCustomAdmin(ConsoleSystem.Arg arg)
        {
            if (arg == null || arg.Args == null || arg.Args.Length != 1 || arg.Args.Length > 1)
            {
                PrintWarning("Неверный синтаксис,используйте : unmute Steam64ID");
                return;
            }
            ulong userID = ulong.Parse(arg.Args[0]);
            if (!userID.IsSteamId())
            {
                PrintWarning("Неверный Steam64ID");
                return;
            }
            BasePlayer target = BasePlayer.FindByID(userID);
            if (target == null)
            {
                PrintWarning("Такого игрока нет на сервере");
                return;
            }
            ChatSettingUser[target.userID].MuteChatTime = 0;
            ReplyBroadcast(string.Format(lang.GetMessage("FUNC_MESSAGE_UNMUTE_CHAT", this), "Администратор", target.displayName));
            Puts("Успешно");
        }
        [ChatCommand("chat")]
        void ChatCommandMenu(BasePlayer player) => UI_MainMenu(player);

        [ChatCommand("alert")]
        void ChatAlertPlayers(BasePlayer player, string cmd, string[] arg) => Alert(player, arg);

        [ChatCommand("alertui")]
        void ChatAlertPlayersUI(BasePlayer player, string cmd, string[] arg) => AlertUI(player, arg);

        [ChatCommand("rename")]
        void RenameMetods(BasePlayer player, string cmd, string[] arg)
        {
            if (arg.Length == 0 || arg == null)
            {
                ReplySystem(Chat.ChatChannel.Global, player, lang.GetMessage("COMMAND_RENAME_NOTARG", this, player.UserIDString));
                return;
            }
            string NewName = "";
            foreach (var name in arg)
                NewName += " " + name;
            RenameFunc(player, NewName);
        }

        #region PM

        [ChatCommand("pm")]
        void PmChat(BasePlayer player, string cmd, string[] arg)
        {
            if (!config.MessageSetting.PMActivate) return;
            if (arg.Length == 0 || arg == null)
            {
                ReplySystem(Chat.ChatChannel.Global, player, lang.GetMessage("COMMAND_PM_NOTARG", this, player.UserIDString));
                return;
            }
            string NameUser = arg[0];
            BasePlayer TargetUser = FindPlayer(NameUser);
            if (TargetUser == null || NameUser == null)
            {
                ReplySystem(Chat.ChatChannel.Global, player, lang.GetMessage("COMMAND_PM_NOT_USER", this, player.UserIDString));
                return;
            }
            if (config.MessageSetting.IgnoreUsePM)
            {
                if (ChatSettingUser[TargetUser.userID].IgnoredUsers.Contains(player.userID))
                {
                    ReplySystem(Chat.ChatChannel.Global, player, lang.GetMessage("IGNORE_NO_PM", this, player.UserIDString));
                    return;
                }
                if (ChatSettingUser[player.userID].IgnoredUsers.Contains(TargetUser.userID))
                {
                    ReplySystem(Chat.ChatChannel.Global, player, lang.GetMessage("IGNORE_NO_PM_ME", this, player.UserIDString));
                    return;
                }
            }
            var argList = arg.ToArray();
            string Message = string.Join(" ", argList.ToArray()).Replace(NameUser, "");
            if (Message.Length > 125) return;
            if (Message.Length <= 0 || Message == null)
            {
                ReplySystem(Chat.ChatChannel.Global, player, lang.GetMessage("COMMAND_PM_NOT_NULL_MSG", this, player.UserIDString));
                return;
            }

            PMHistory[TargetUser] = player;
            PMHistory[player] = TargetUser;
            var DisplayNick = AdminSetting.RenameList.ContainsKey(player.userID) ? AdminSetting.RenameList[player.userID] : player.displayName;

            ReplySystem(Chat.ChatChannel.Global, TargetUser, String.Format(lang.GetMessage("COMMAND_PM_SEND_MSG", this, player.UserIDString), DisplayNick, Message));
            ReplySystem(Chat.ChatChannel.Global, player, String.Format(lang.GetMessage("COMMAND_PM_SUCCESS", this, player.UserIDString), Message));
            Effect.server.Run(config.MessageSetting.SoundPM, TargetUser.GetNetworkPosition());
            Log($"ЛИЧНЫЕ СООБЩЕНИЯ : {player.displayName}({DisplayNick}) отправил сообщение игроку - {TargetUser.displayName}\nСООБЩЕНИЕ : {Message}");
        }

        [ChatCommand("r")]
        void RChat(BasePlayer player, string cmd, string[] arg)
        {
            if (!config.MessageSetting.PMActivate) return;
            if (arg.Length == 0 || arg == null)
            {
                ReplySystem(Chat.ChatChannel.Global, player, lang.GetMessage("COMMAND_R_NOTARG", this, player.UserIDString));
                return;
            }
            if (!PMHistory.ContainsKey(player))
            {
                ReplySystem(Chat.ChatChannel.Global, player, lang.GetMessage("COMMAND_R_NOTMSG", this, player.UserIDString));
                return;
            }
            BasePlayer RetargetUser = PMHistory[player];
            if (RetargetUser == null)
            {
                ReplySystem(Chat.ChatChannel.Global, player, lang.GetMessage("COMMAND_PM_NOT_USER", this, player.UserIDString));
                return;
            }
            if (config.MessageSetting.IgnoreUsePM)
            {
                if (ChatSettingUser[RetargetUser.userID].IgnoredUsers.Contains(player.userID))
                {
                    ReplySystem(Chat.ChatChannel.Global, player, lang.GetMessage("IGNORE_NO_PM", this, player.UserIDString));
                    return;
                }
                if (ChatSettingUser[player.userID].IgnoredUsers.Contains(RetargetUser.userID))
                {
                    ReplySystem(Chat.ChatChannel.Global, player, lang.GetMessage("IGNORE_NO_PM_ME", this, player.UserIDString));
                    return;
                }
            }
            string Message = string.Join(" ", arg.ToArray());
            if (Message.Length > 125) return;
            if (Message.Length <= 0 || Message == null)
            {
                ReplySystem(Chat.ChatChannel.Global, player, lang.GetMessage("COMMAND_PM_NOT_NULL_MSG", this, player.UserIDString));
                return;
            }
            PMHistory[RetargetUser] = player;
            var DisplayNick = AdminSetting.RenameList.ContainsKey(player.userID) ? AdminSetting.RenameList[player.userID] : player.displayName;

            ReplySystem(Chat.ChatChannel.Global, RetargetUser, String.Format(lang.GetMessage("COMMAND_PM_SEND_MSG", this, player.UserIDString), DisplayNick, Message));
            ReplySystem(Chat.ChatChannel.Global, player, String.Format(lang.GetMessage("COMMAND_PM_SUCCESS", this, player.UserIDString), Message));
            Effect.server.Run(config.MessageSetting.SoundPM, RetargetUser.GetNetworkPosition());
            Log($"ЛИЧНЫЕ СООБЩЕНИЯ : {player.displayName} отправил сообщение игроку - {RetargetUser.displayName}\nСООБЩЕНИЕ : {Message}");
        }

        [ChatCommand("ignore")]
        void IgnorePlayerPM(BasePlayer player, string cmd, string[] arg)
        {
            if (!config.MessageSetting.IgnoreUsePM) return;
            var ChatUser = ChatSettingUser[player.userID];
            if (arg.Length == 0 || arg == null)
            {
                ReplySystem(Chat.ChatChannel.Global, player, lang.GetMessage("INGORE_NOTARG", this, player.UserIDString));
                return;
            }
            string NameUser = arg[0];
            BasePlayer TargetUser = FindPlayer(NameUser);
            if (TargetUser == null || NameUser == null)
            {
                ReplySystem(Chat.ChatChannel.Global, player, lang.GetMessage("COMMAND_PM_NOT_USER", this, player.UserIDString));
                return;
            }

            string Lang = !ChatUser.IgnoredUsers.Contains(TargetUser.userID) ? String.Format(lang.GetMessage("IGNORE_ON_PLAYER", this, player.UserIDString), TargetUser.displayName) : String.Format(lang.GetMessage("IGNORE_OFF_PLAYER", this, player.UserIDString), TargetUser.displayName);
            ReplySystem(Chat.ChatChannel.Global, player, Lang);
            if (!ChatUser.IgnoredUsers.Contains(TargetUser.userID))
                ChatUser.IgnoredUsers.Add(TargetUser.userID);
            else ChatUser.IgnoredUsers.Remove(TargetUser.userID);
        }

        #endregion

        [ConsoleCommand("alert")]
        void ChatAlertPlayersCMD(ConsoleSystem.Arg arg) => Alert(arg.Player(), arg.Args);

        [ConsoleCommand("alertui")]
        void ChatAlertPlayersUICMD(ConsoleSystem.Arg arg) => AlertUI(arg.Player(), arg.Args);

        [ConsoleCommand("saybro")]
        void ChatAlertPlayerInPM(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length < 2)
            {
                PrintWarning("Используйте правильно ситаксис : saybro Steam64ID Сообщение");
                return;
            }
            ulong SteamID = ulong.Parse(arg.Args[0]);
            var argList = arg.Args.ToArray();
            string Message = string.Join(" ", argList.ToArray());
            if (Message.Length > 125) return;
            if (Message.Length <= 0 || Message == null)
            {
                PrintWarning("Вы не указали сообщение игроку");
                return;
            }
            BasePlayer player = BasePlayer.FindByID(SteamID);
            if(player == null)
            {
                PrintWarning("Игрока нет в сети");
                return;
            }
            ReplySystem(Chat.ChatChannel.Global, player, Message.Replace(SteamID.ToString(), ""));
        }

        [ConsoleCommand("set")]
        private void ConsolesCommandPrefixSet(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length != 3)
            {
                PrintWarning("Используйте правильно ситаксис : set [Steam64ID] [prefix/chat/nick/custom] [Argument]");
                return;
            }
            ulong Steam64ID = 0;
            BasePlayer player = null;
            if (ulong.TryParse(arg.Args[0], out Steam64ID))
                player = BasePlayer.FindByID(Steam64ID);
            if (player == null)
            {
                PrintWarning("Неверно указан SteamID игрока или ошибка в синтаксисе\nИспользуйте правильно ситаксис : set [Steam64ID] [prefix/chat/nick/custom] [Argument]");
                return;
            }
            var DataPlayer = ChatSettingUser[player.userID];

            switch (arg.Args[1].ToLower())
            {
                case "prefix":
                    {
                        string KeyPrefix = arg.Args[2];
                        foreach (var Prefix in config.PrefixList.Where(x => x.Permissions == KeyPrefix))
                            if (config.PrefixList.Contains(Prefix))
                            {
                                DataPlayer.ChatPrefix = Prefix.Argument;
                                Puts($"Префикс успешно установлен на - {Prefix.Argument}");
                            }
                            else Puts("Неверно указан Permissions от префикса");
                        break;
                    }
                case "chat":
                    {
                        string KeyChatColor = arg.Args[2];
                        foreach (var ColorChat in config.PrefixList.Where(x => x.Permissions == KeyChatColor))
                            if (config.MessageColorList.Contains(ColorChat))
                            {
                                DataPlayer.MessageColor = ColorChat.Argument;
                                Puts($"Цвет сообщения успешно установлен на - {ColorChat.Argument}");
                            }
                            else Puts("Неверно указан Permissions от префикса");
                        break;
                    }
                case "nick":
                    {
                        string KeyNickColor = arg.Args[2];
                        foreach (var ColorChat in config.NickColorList.Where(x => x.Permissions == KeyNickColor))
                            if (config.NickColorList.Contains(ColorChat))
                            {
                                DataPlayer.NickColor = ColorChat.Argument;
                                Puts($"Цвет ника успешно установлен на - {ColorChat.Argument}");
                            }
                            else Puts("Неверно указан Permissions от префикса");
                        break;
                    }
                case "custom":
                    {
                        string CustomPrefix = arg.Args[2];
                        DataPlayer.ChatPrefix = CustomPrefix;
                        Puts($"Кастомный префикс успешно установлен на - {CustomPrefix}");
                        break;
                    }
                default:
                    {
                        PrintWarning("Используйте правильно ситаксис : set [Steam64ID] [prefix/chat/nick/custom] [Argument]");
                        break;
                    }
            }

        }

        #endregion

        #region FuncCommand

        [ConsoleCommand("mute_search")]
        void ConsoleSearchMute(ConsoleSystem.Arg arg)
        {
            BasePlayer moder = arg.Player();
            if (arg.Args == null || arg.Args.Length == 0) return;
            string Searcher = arg.Args[0].ToLower();
            if (string.IsNullOrEmpty(Searcher) || Searcher.Length == 0 || Searcher.Length < 1) return;
            UI_MuteMenu(moder, Searcher);
        }                              
        
        [ConsoleCommand("iq_chat")]
        private void ConsoleCommandIQChat(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            var DataPlayer = ChatSettingUser[player.userID];

            switch (arg.Args[0])
            {
                #region Setting
                case "setting": 
                    {
                        switch(arg.Args[1])
                        {
                            case "prefix":
                                {
                                    UI_PrefixSetting(player);
                                    break;
                                }
                            case "nick":
                                {
                                    UI_NickSetting(player);
                                    break;
                                }
                            case "chat":
                                {
                                    UI_TextSetting(player);
                                    break;
                                }
                        }
                        break;
                    }
                #endregion

                #region Action
                case "action":
                    {
                        switch(arg.Args[1])
                        {
                            case "prefix":
                                {
                                    var Selected = arg.Args[2];
                                    var Permission = arg.Args[3];
                                    if (!permission.UserHasPermission(player.UserIDString, Permission)) return;

                                    if (config.MessageSetting.MultiPrefix)
                                    {
                                        if (!DataPlayer.MultiPrefix.Contains(Selected))
                                            DataPlayer.MultiPrefix.Add(Selected);
                                        else DataPlayer.MultiPrefix.Remove(Selected);
                                    }
                                    if (DataPlayer.ChatPrefix != Selected)
                                        DataPlayer.ChatPrefix = Selected;
                                    else DataPlayer.ChatPrefix = config.PrefixFoDefaults.PrefixDefault;
                                    UpdateLabel(player, DataPlayer);
                                    break;
                                }
                            case "nick":
                                {
                                    var Selected = arg.Args[2];
                                    var Permission = arg.Args[3];
                                    if (!permission.UserHasPermission(player.UserIDString, Permission)) return;

                                    if (DataPlayer.NickColor != Selected)
                                        DataPlayer.NickColor = Selected;
                                    else DataPlayer.NickColor = config.PrefixFoDefaults.NickDefault;
                                    UpdateLabel(player, DataPlayer);
                                    break;
                                }
                            case "chat":
                                {
                                    var Selected = arg.Args[2];
                                    var Permission = arg.Args[3];
                                    if (!permission.UserHasPermission(player.UserIDString, Permission)) return;

                                    if (DataPlayer.MessageColor != Selected)
                                        DataPlayer.MessageColor = Selected;
                                    else DataPlayer.MessageColor = config.PrefixFoDefaults.MessageDefault;
                                    UpdateLabel(player, DataPlayer);
                                    break;
                                }
                        }
                        break;
                    }
                #endregion
                
                #region Mute
                case "mute":
                    {
                        string Action = arg.Args[1];
                        switch (Action)
                        {
                            case "menu":
                                {
                                    if (permission.UserHasPermission(player.UserIDString, PermMuteMenu))
                                        UI_MuteMenu(player);
                                    break;
                                }
                            case "actionmenu":
                                {
                                    BasePlayer target = BasePlayer.FindByID(ulong.Parse(arg.Args[2]));
                                    UI_MuteTakeAction(player, target.userID);
                                    break;
                                }
                            case "action": 
                                {
                                    BasePlayer target = BasePlayer.FindByID(ulong.Parse(arg.Args[2]));
                                    string MuteAction = arg.Args[3];
                                    switch (MuteAction)
                                    {
                                        case "mute":
                                            {
                                                string MuteFormat = arg.Args[4];
                                                UI_ReasonMute(player, target.userID, MuteFormat);
                                                break;
                                            }
                                        case "mute_reason":
                                            {
                                                CuiHelper.DestroyUi(player, MAIN_PARENT);
                                                string MuteFormat = arg.Args[4];
                                                int Index = Convert.ToInt32(arg.Args[5]);
                                                MutePlayer(target, MuteFormat, Index, "", "", player);
                                                break;
                                            }
                                        case "unmutechat":
                                            {
                                                CuiHelper.DestroyUi(player, MAIN_PARENT);
                                                ChatSettingUser[target.userID].MuteChatTime = 0;
                                                ReplyBroadcast(string.Format(lang.GetMessage("FUNC_MESSAGE_UNMUTE_CHAT", this), player.displayName, target.displayName));
                                                break;
                                            }
                                        case "unmutevoice":
                                            {
                                                CuiHelper.DestroyUi(player, MAIN_PARENT);
                                                ChatSettingUser[target.userID].MuteVoiceTime = 0;
                                                ReplyBroadcast(string.Format(lang.GetMessage("FUNC_MESSAGE_UNMUTE_VOICE", this), player.displayName, target.displayName));
                                                break;
                                            }
                                    }
                                    break;
                                }
                        }
                        break;
                    }              
                #endregion

                #region ADMIN
                case "admin_voice":
                    {
                        var Command = arg.Args[1];
                        switch(Command)
                        {
                            case "mutevoice":
                                {
                                    AdminSetting.MuteVoiceAll = true;
                                    foreach (var p in BasePlayer.activePlayerList)
                                        ChatSettingUser[p.userID].MuteVoiceTime = CurrentTime() + 86400;
                                    ReplyBroadcast(lang.GetMessage("FUNC_MESSAGE_MUTE_ALL_VOICE", this, player.UserIDString));
                                    break;
                                }
                            case "unmutevoice":
                                {
                                    AdminSetting.MuteVoiceAll = false;
                                    foreach (var p in BasePlayer.activePlayerList)
                                        ChatSettingUser[p.userID].MuteVoiceTime = 0;
                                    ReplyBroadcast(lang.GetMessage("FUNC_MESSAGE_UNMUTE_ALL_VOICE", this, player.UserIDString));
                                    break;
                                }
                        }
                        foreach (var p in BasePlayer.activePlayerList)
                            rust.RunServerCommand(Command, p.userID);
                        break;
                    }
                case "admin_chat":
                    {
                        var Command = arg.Args[1];
                        switch(Command)
                        {
                            case "mutechat":
                                {
                                    AdminSetting.MuteChatAll = true;
                                    foreach (var p in BasePlayer.activePlayerList)
                                        MuteAllChatPlayer(p);
                                    ReplyBroadcast(lang.GetMessage("FUNC_MESSAGE_MUTE_ALL_CHAT", this, player.UserIDString));
                                    break;
                                }
                            case "unmutechat":
                                {
                                    AdminSetting.MuteChatAll = false;
                                    foreach (var p in BasePlayer.activePlayerList)
                                        ChatSettingUser[p.userID].MuteChatTime = 0;
                                    ReplyBroadcast(lang.GetMessage("FUNC_MESSAGE_UNMUTE_ALL_CHAT", this, player.UserIDString));
                                    break;
                                }
                        }
                        break;
                    }
                    #endregion
            }
        }

        #endregion

        #endregion

        #region Lang
        private new void LoadDefaultMessages()
        {
            PrintWarning("Языковой файл загружается...");
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TITLE_ONE"] = "<size=30><b>НАСТРОЙКА ЧАТА</b></size>",
                ["TITLE_TWO"] = "<size=16><b>ВЫБЕРИТЕ ДЕЙСТВИЕ</b></size>",
                ["UI_CLOSE_BTN"] = "<size=20>ЗАКРЫТЬ</size>",

                ["UI_TEXT_PREFIX"] = "<size=23>ПРЕФИКС</size>",
                ["UI_TEXT_COLOR_NICK"] = "<size=23>НИК</size>",
                ["UI_TEXT_COLOR_MSG"] = "<size=23>ТЕКСТ</size>",
                ["UI_TEXT_SETTING_MENU"] = "<size=23>НАСТРОЙКА</size>",
                ["UI_TEXT_MUTE_MENU_BTN"] = "<size=23>МУТЫ</size>",

                ["UI_TEXT_ADMIN_PANEL"] = "<size=17><b>ПАНЕЛЬ\nАДМИНИСТРАТОРА</b></size>",
                ["UI_TEXT_MODER_PANEL"] = "<size=17><b>ПАНЕЛЬ\nМОДЕРАТОРА</b></size>",
                ["UI_TEXT_ADMIN_PANEL_MUTE_CHAT_ALL"] = "<size=14>ВЫКЛЮЧИТЬ ЧАТ</size>",
                ["UI_TEXT_ADMIN_PANEL_UNMUTE_CHAT_ALL"] = "<size=14>ВКЛЮЧИТЬ ЧАТ</size>",
                ["UI_TEXT_ADMIN_PANEL_MUTE_VOICE_ALL"] = "<size=14>ВЫКЛЮЧИТЬ ГОЛОС</size>",
                ["UI_TEXT_ADMIN_PANEL_UNMUTE_VOICE_ALL"] = "<size=14>ВКЛЮЧИТЬ ГОЛОС</size>",

                ["UI_ALERT_TITLE"] = "<size=18><b>МИНУТОЧКУ ВНИМАНИЯ</b></size>",

                ["UI_TITLE_NEW_PREFIX_ELEMENT"] = "<size=16><b>ВЫБЕРИТЕ ПРЕФИКС</b></size>",
                ["UI_TITLE_NEW_NICK_COLOR_ELEMENT"] = "<size=16><b>ВЫБЕРИТЕ ЦВЕТ НИКА</b></size>",
                ["UI_TITLE_NEW_MESSAGE_COLOR_ELEMENT"] = "<size=16><b>ВЫБЕРИТЕ ЦВЕТ ТЕКСТА</b></size>",

                ["FUNC_MESSAGE_MUTE_CHAT"] = "{0} заблокировал чат игроку {1} на {2}\nПричина : {3}",
                ["FUNC_MESSAGE_UNMUTE_CHAT"] = "{0} разблокировал чат игроку {1}",
                ["FUNC_MESSAGE_MUTE_VOICE"] = "{0} заблокировал голос игроку {1} на {2}\nПричина : {3}",
                ["FUNC_MESSAGE_UNMUTE_VOICE"] = "{0} разблокировал голос игроку {1}",
                ["FUNC_MESSAGE_MUTE_ALL_CHAT"] = "Всем игрокам был заблокирован чат",
                ["FUNC_MESSAGE_UNMUTE_ALL_CHAT"] = "Всем игрокам был разблокирован чат",
                ["FUNC_MESSAGE_MUTE_ALL_VOICE"] = "Всем игрокам был заблокирован голос",
                ["FUNC_MESSAGE_UNMUTE_ALL_VOICE"] = "Всем игрокам был разблокирован голос",

                ["FUNC_MESSAGE_ISMUTED_TRUE"] = "Вы не можете отправлять сообщения еще {0}\nВаш чат заблокирован",
                ["FUNC_MESSAGE_NO_ARG_BROADCAST"] = "Вы не можете отправлять пустое сообщение в оповещение!",

                ["UI_MUTE_PANEL_TITLE"] = "<size=20><b>ПАНЕЛЬ УПРАВЛЕНИЯ БЛОКИРОВКАМИ ЧАТА</b></size>",
                ["UI_MUTE_PANEL_TITLE_ACTION"] = "<size=15>ВЫБЕРИТЕ ИГРОКА ИЛИ ВВЕДИТЕ НИК В ПОИСКЕ</size>",
                ["UI_MUTE_PANEL_TITLE_HELPS_LOCK"] = "<size=13><b>- У ИГРОКА ЗАБЛОКИРОВАН ЧАТ ИЛИ ГОЛОС</b></size>",
                ["UI_MUTE_PANEL_TITLE_HELPS_UNLOCK"] = "<size=13><b>- У ИГРОКА РАЗБЛОКИРОВАН ЧАТ ИЛИ ГОЛОС</b></size>",

                ["UI_MUTE_TAKE_ACTION_PANEL"] = "<size=18><b>ВЫБЕРИТЕ\nДЕЙСТВИЕ</b></size>",
                ["UI_MUTE_TAKE_ACTION_CHAT"] = "<size=12>ЗАБЛОКИРОВАТЬ\nЧАТ</size>",
                ["UI_MUTE_TAKE_ACTION_CHAT_UNMUTE"] = "<size=12>РАЗБЛОКИРОВАТЬ\nЧАТ</size>",
                ["UI_MUTE_TAKE_ACTION_VOICE"] = "<size=12>ЗАБЛОКИРОВАТЬ\nГОЛОС</size>",
                ["UI_MUTE_TAKE_ACTION_VOICE_UNMUTE"] = "<size=12>РАЗБЛОКИРОВАТЬ\nГОЛОС</size>",

                ["UI_MUTE_TAKE_REASON_TITLE"] = "<size=18><b>ВЫБЕРИТЕ\nПРИЧИНУ</b></size>",

                ["COMMAND_NOT_PERMISSION"] = "У вас недостаточно прав для данной команды",
                ["COMMAND_RENAME_NOTARG"] = "Используйте команду так : /rename Новый Ник",
                ["COMMAND_RENAME_SUCCES"] = "Вы успешно изменили ник на {0}",

                ["COMMAND_PM_NOTARG"] = "Используйте команду так : /pm Ник Игрока Сообщение",
                ["COMMAND_PM_NOT_NULL_MSG"] = "Вы не можете отправлять пустое сообщение",
                ["COMMAND_PM_NOT_USER"] = "Игрок не найден или не в сети",
                ["COMMAND_PM_SUCCESS"] = "Ваше сообщение успешно доставлено\nСообщение : {0}",
                ["COMMAND_PM_SEND_MSG"] = "Сообщение от {0}\n{1}",

                ["COMMAND_R_NOTARG"] = "Используйте команду так : /r Сообщение",
                ["COMMAND_R_NOTMSG"] = "Вам или вы ещё не писали игроку в личные сообщения!",

                ["FLOODERS_MESSAGE"] = "Вы пишите слишком быстро! Подождите {0} секунд",

                ["WELCOME_PLAYER"] = "{0} зашел на сервер",
                ["LEAVE_PLAYER"] = "{0} вышел с сервера",
                ["WELCOME_PLAYER_WORLD"] = "{0} зашел на сервер.Из {1}",
                ["LEAVE_PLAYER_REASON"] = "{0} вышел с сервера.Причина {1}",

                ["IGNORE_ON_PLAYER"] = "Вы добавили игрока {0} в черный список",
                ["IGNORE_OFF_PLAYER"] = "Вы убрали игрока {0} из черного списка",
                ["IGNORE_NO_PM"] = "Данный игрок добавил вас в ЧС,ваше сообщение не будет доставлено",
                ["IGNORE_NO_PM_ME"] = "Вы добавили данного игрока в ЧС,ваше сообщение не будет доставлено",
                ["INGORE_NOTARG"] = "Используйте команду так : /ignore Ник Игрока",
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TITLE_ONE"] = "<size=30><b>НАСТРОЙКА ЧАТА</b></size>",
                ["TITLE_TWO"] = "<size=16><b>ВЫБЕРИТЕ ДЕЙСТВИЕ</b></size>",
                ["UI_CLOSE_BTN"] = "<size=20>ЗАКРЫТЬ</size>",

                ["UI_TEXT_PREFIX"] = "<size=23>ПРЕФИКС</size>",
                ["UI_TEXT_COLOR_NICK"] = "<size=23>НИК</size>",
                ["UI_TEXT_COLOR_MSG"] = "<size=23>ТЕКСТ</size>",
                ["UI_TEXT_SETTING_MENU"] = "<size=23>НАСТРОЙКА</size>",
                ["UI_TEXT_MUTE_MENU_BTN"] = "<size=23>МУТЫ</size>",

                ["UI_TEXT_ADMIN_PANEL"] = "<size=17><b>ПАНЕЛЬ\nАДМИНИСТРАТОРА</b></size>",
                ["UI_TEXT_MODER_PANEL"] = "<size=17><b>ПАНЕЛЬ\nМОДЕРАТОРА</b></size>",
                ["UI_TEXT_ADMIN_PANEL_MUTE_CHAT_ALL"] = "<size=14>ВЫКЛЮЧИТЬ ЧАТ</size>",
                ["UI_TEXT_ADMIN_PANEL_UNMUTE_CHAT_ALL"] = "<size=14>ВКЛЮЧИТЬ ЧАТ</size>",
                ["UI_TEXT_ADMIN_PANEL_MUTE_VOICE_ALL"] = "<size=14>ВЫКЛЮЧИТЬ ГОЛОС</size>",
                ["UI_TEXT_ADMIN_PANEL_UNMUTE_VOICE_ALL"] = "<size=14>ВКЛЮЧИТЬ ГОЛОС</size>",

                ["UI_ALERT_TITLE"] = "<size=18><b>МИНУТОЧКУ ВНИМАНИЯ</b></size>",

                ["UI_TITLE_NEW_PREFIX_ELEMENT"] = "<size=16><b>ВЫБЕРИТЕ ПРЕФИКС</b></size>",
                ["UI_TITLE_NEW_NICK_COLOR_ELEMENT"] = "<size=16><b>ВЫБЕРИТЕ ЦВЕТ НИКА</b></size>",
                ["UI_TITLE_NEW_MESSAGE_COLOR_ELEMENT"] = "<size=16><b>ВЫБЕРИТЕ ЦВЕТ ТЕКСТА</b></size>",

                ["FUNC_MESSAGE_MUTE_CHAT"] = "{0} заблокировал чат игроку {1} на {2}\nПричина : {3}",
                ["FUNC_MESSAGE_UNMUTE_CHAT"] = "{0} разблокировал чат игроку {1}",
                ["FUNC_MESSAGE_MUTE_VOICE"] = "{0} заблокировал голос игроку {1} на {2}\nПричина : {3}",
                ["FUNC_MESSAGE_UNMUTE_VOICE"] = "{0} разблокировал голос игроку {1}",
                ["FUNC_MESSAGE_MUTE_ALL_CHAT"] = "Всем игрокам был заблокирован чат",
                ["FUNC_MESSAGE_UNMUTE_ALL_CHAT"] = "Всем игрокам был разблокирован чат",
                ["FUNC_MESSAGE_MUTE_ALL_VOICE"] = "Всем игрокам был заблокирован голос",
                ["FUNC_MESSAGE_UNMUTE_ALL_VOICE"] = "Всем игрокам был разблокирован голос",

                ["FUNC_MESSAGE_ISMUTED_TRUE"] = "Вы не можете отправлять сообщения еще {0}\nВаш чат заблокирован",
                ["FUNC_MESSAGE_NO_ARG_BROADCAST"] = "Вы не можете отправлять пустое сообщение в оповещение!",

                ["UI_MUTE_PANEL_TITLE"] = "<size=20><b>ПАНЕЛЬ УПРАВЛЕНИЯ БЛОКИРОВКАМИ ЧАТА</b></size>",
                ["UI_MUTE_PANEL_TITLE_ACTION"] = "<size=15>ВЫБЕРИТЕ ИГРОКА ИЛИ ВВЕДИТЕ НИК В ПОИСКЕ</size>",
                ["UI_MUTE_PANEL_TITLE_HELPS_LOCK"] = "<size=13><b>- У ИГРОКА ЗАБЛОКИРОВАН ЧАТ ИЛИ ГОЛОС</b></size>",
                ["UI_MUTE_PANEL_TITLE_HELPS_UNLOCK"] = "<size=13><b>- У ИГРОКА РАЗБЛОКИРОВАН ЧАТ ИЛИ ГОЛОС</b></size>",

                ["UI_MUTE_TAKE_ACTION_PANEL"] = "<size=18><b>ВЫБЕРИТЕ\nДЕЙСТВИЕ</b></size>",
                ["UI_MUTE_TAKE_ACTION_CHAT"] = "<size=12>ЗАБЛОКИРОВАТЬ\nЧАТ</size>",
                ["UI_MUTE_TAKE_ACTION_CHAT_UNMUTE"] = "<size=12>РАЗБЛОКИРОВАТЬ\nЧАТ</size>",
                ["UI_MUTE_TAKE_ACTION_VOICE"] = "<size=12>ЗАБЛОКИРОВАТЬ\nГОЛОС</size>",
                ["UI_MUTE_TAKE_ACTION_VOICE_UNMUTE"] = "<size=12>РАЗБЛОКИРОВАТЬ\nГОЛОС</size>",

                ["UI_MUTE_TAKE_REASON_TITLE"] = "<size=18><b>ВЫБЕРИТЕ\nПРИЧИНУ</b></size>",

                ["COMMAND_NOT_PERMISSION"] = "У вас недостаточно прав для данной команды",
                ["COMMAND_RENAME_NOTARG"] = "Используйте команду так : /rename Новый Ник",
                ["COMMAND_RENAME_SUCCES"] = "Вы успешно изменили ник на {0}",

                ["COMMAND_PM_NOTARG"] = "Используйте команду так : /pm Ник Игрока Сообщение",
                ["COMMAND_PM_NOT_NULL_MSG"] = "Вы не можете отправлять пустое сообщение",
                ["COMMAND_PM_NOT_USER"] = "Игрок не найден или не в сети",
                ["COMMAND_PM_SUCCESS"] = "Ваше сообщение успешно доставлено\nСообщение : {0}",
                ["COMMAND_PM_SEND_MSG"] = "Сообщение от {0}\n{1}",

                ["COMMAND_R_NOTARG"] = "Используйте команду так : /r Сообщение",
                ["COMMAND_R_NOTMSG"] = "Вам или вы ещё не писали игроку в личные сообщения!",

                ["FLOODERS_MESSAGE"] = "Вы пишите слишком быстро! Подождите {0} секунд",

                ["WELCOME_PLAYER"] = "{0} зашел на сервер",
                ["LEAVE_PLAYER"] = "{0} вышел с сервера",
                ["WELCOME_PLAYER_WORLD"] = "{0} зашел на сервер.Из {1}",
                ["LEAVE_PLAYER_REASON"] = "{0} вышел с сервера.Причина {1}",

                ["IGNORE_ON_PLAYER"] = "Вы добавили игрока {0} в черный список",
                ["IGNORE_OFF_PLAYER"] = "Вы убрали игрока {0} из черного списка",
                ["IGNORE_NO_PM"] = "Данный игрок добавил вас в ЧС,ваше сообщение не будет доставлено",
                ["IGNORE_NO_PM_ME"] = "Вы добавили данного игрока в ЧС,ваше сообщение не будет доставлено",
                ["INGORE_NOTARG"] = "Используйте команду так : /ignore Ник Игрока",
            }, this, "ru");
           
            PrintWarning("Языковой файл загружен успешно");
        }
        #endregion

        #region Helpers
        public void Log(string LoggedMessage) => LogToFile("IQChatLogs", LoggedMessage, this);
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
        private BasePlayer FindPlayer(string nameOrId)
        {
            foreach (var check in BasePlayer.activePlayerList.Where(x => x.displayName.ToLower().Contains(nameOrId.ToLower()) || x.UserIDString == nameOrId))
                return check;
            return null;
        }
        private static string HexToRustFormat(string hex)
        {
            Color color;
            ColorUtility.TryParseHtmlString(hex, out color);
            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }
        #endregion

        #region ChatFunc

        public Dictionary<ulong, double> Flooders = new Dictionary<ulong, double>();
        void ReplyChat(Chat.ChatChannel channel, BasePlayer player, string OutMessage)
        {
            var MessageSetting = config.MessageSetting;
            if (MessageSetting.AntiSpamActivate)
                if (!permission.UserHasPermission(player.UserIDString, MessageSetting.PermAdminImmunitetAntispam))
                {
                    if (!Flooders.ContainsKey(player.userID))
                        Flooders.Add(player.userID, CurrentTime() + MessageSetting.FloodTime);
                    else
                        if (Flooders[player.userID] > CurrentTime())
                        {
                            ReplySystem(Chat.ChatChannel.Global, player, string.Format(lang.GetMessage("FLOODERS_MESSAGE", this, player.UserIDString), Convert.ToInt32(Flooders[player.userID] - CurrentTime())));
                            return;
                        }

                    Flooders[player.userID] = MessageSetting.FloodTime + CurrentTime();
                }

            if (channel == Chat.ChatChannel.Global)
            {
                foreach (var p in BasePlayer.activePlayerList)
                    p.SendConsoleCommand("chat.add", channel, player.userID, OutMessage);
                PrintToConsole(OutMessage);
            }
            if (channel == Chat.ChatChannel.Team)
            {
                RelationshipManager.PlayerTeam Team = RelationshipManager.Instance.FindTeam(player.currentTeam);
                if (Team == null) return;
                foreach (var FindPlayers in Team.members)
                {
                    BasePlayer TeamPlayer = BasePlayer.FindByID(FindPlayers);
                    if (TeamPlayer == null) return;

                    TeamPlayer.SendConsoleCommand("chat.add", channel, player.userID, OutMessage);
                }
            }
        }

        void ReplySystem(Chat.ChatChannel channel, BasePlayer player, string Message,string CustomPrefix = "", string CustomAvatar = "", string CustomHex = "")
        {
            string Prefix = string.IsNullOrEmpty(CustomPrefix) ? config.MessageSetting.BroadcastTitle : CustomPrefix;
            ulong Avatar = string.IsNullOrEmpty(CustomAvatar) ? config.MessageSetting.Steam64IDAvatar : ulong.Parse(CustomAvatar);
            string Hex = string.IsNullOrEmpty(CustomHex) ? config.MessageSetting.BroadcastColor : CustomHex;

            string FormatMessage = $"{Prefix}<color={Hex}>{Message}</color>";
            if (channel == Chat.ChatChannel.Global)
                player.SendConsoleCommand("chat.add", channel, Avatar, FormatMessage);         
        }

        void ReplyBroadcast(string Message, string CustomPrefix = "", string CustomAvatar = "")
        {
            foreach(var p in BasePlayer.activePlayerList)
                ReplySystem(Chat.ChatChannel.Global, p, Message, CustomPrefix, CustomAvatar);
        }

        static DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        static double CurrentTime() => DateTime.UtcNow.Subtract(epoch).TotalSeconds;

        #endregion

        #region API

        void API_ALERT(string Message, Chat.ChatChannel channel = Chat.ChatChannel.Global, string CustomPrefix = "", string CustomAvatar = "", string CustomHex = "")
        {
            foreach (var p in BasePlayer.activePlayerList)
                ReplySystem(channel, p, Message, CustomPrefix, CustomAvatar, CustomHex);
        }
        void API_ALERT_PLAYER(BasePlayer player,string Message, string CustomPrefix = "", string CustomAvatar = "", string CustomHex = "") => ReplySystem(Chat.ChatChannel.Global, player, Message, CustomPrefix, CustomAvatar, CustomHex);
        void API_ALERT_PLAYER_UI(BasePlayer player, string Message) => UIAlert(player, Message);
        bool API_CHECK_MUTE_CHAT(ulong ID)
        {
            var DataPlayer = ChatSettingUser[ID];
            if (DataPlayer.MuteChatTime > CurrentTime())
                return true;
            else return false;
        }
        bool API_CHECK_VOICE_CHAT(ulong ID)
        {
            var DataPlayer = ChatSettingUser[ID];
            if (DataPlayer.MuteVoiceTime > CurrentTime())
                return true;
            else return false;
        }
        string API_GET_PREFIX(ulong ID)
        {
            var DataPlayer = ChatSettingUser[ID];
            return DataPlayer.ChatPrefix;
        }
        string API_GET_CHAT_COLOR(ulong ID)
        {
            var DataPlayer = ChatSettingUser[ID];
            return DataPlayer.MessageColor;
        }
        string API_GET_NICK_COLOR(ulong ID)
        {
            var DataPlayer = ChatSettingUser[ID];
            return DataPlayer.NickColor;
        }
        #endregion
    }
}
