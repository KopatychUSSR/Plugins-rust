using Newtonsoft.Json;
using UnityEngine;
using System;
using Network;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Libraries;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Plugins;
using Oxide.Core;
using System.Text;
using Object = System.Object;
using ConVar;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
    [Info("IQStaff", "Mercury", "2.3.11")]
    [Description("Server Staff Controller")]
    public class IQStaff : RustPlugin
    {
        private Timer _timer;

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
                PrintWarning(LanguageEn ? $"Error reading #54327 configuration 'oxide/config/{Name}', creating a new configuration!!" : $"Ошибка чтения #54327 конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

            
        
        private DateTime TimeServer = DateTime.Now;
        
        private void Init() => ReadData();
        
                
        
        private static StringBuilder sb = new StringBuilder();
        
        
        private static Int32 CurrentTime() => Facepunch.Math.Epoch.Current;

        public class Fields
        {
            public string name { get; set; }
            public string value { get; set; }
            public bool inline { get; set; }
            public Fields(string name, string value, bool inline)
            {
                this.name = name;
                this.value = value;
                this.inline = inline;
            }
        }
        
        
        
        enum TypeRemoveStaff
        {
            None,
            StaffController,
            CommandController,
            API,
        }

        public class Footer
        {
            public string text { get; set; }
            public string icon_url { get; set; }
            public string proxy_icon_url { get; set; }
            public Footer(string text, string icon_url, string proxy_icon_url)
            {
                this.text = text;
                this.icon_url = icon_url;
                this.proxy_icon_url = proxy_icon_url;
            }
        }

        
        
        private void DiscordSendLog(String UserName, String UserID, String Information, Configuration.DiscordSetting discordSetting)
        {
            if (!discordSetting.UseLogged || String.IsNullOrWhiteSpace(config.DiscordSettings.WebHooks)) return;
            
            List <Fields> fields = new List<Fields>
            {
                new Fields(LanguageEn ? "Nick" : "Ник", UserName, true),
                new Fields("Steam64ID", UserID, true),
                new Fields(LanguageEn ? "Information" : "Информация", Information, false),
            };
            
            FancyMessage newMessage = new FancyMessage(null, false, new FancyMessage.Embeds[1] { new FancyMessage.Embeds(null, 16608621, fields, new Authors("IQStaff", null, config.DiscordSettings.IconPng, null), null) });
            Request($"{config.DiscordSettings.WebHooks}", newMessage.toJSON());
        }     

        
        private void MoscovOVHBalanceSet(UInt64 userID, Int32 Balance, Int32 ReputationTake)
        {
            if (!RustStore)
            {
                PrintWarning(LanguageEn ? "You don't have a MoscovOVH store installed" : "У вас не установлен магазин MoscovOVH");
                return;
            }
            plugins.Find("RustStore").CallHook("APIChangeUserBalance", userID, Balance, new Action<string>((result) =>
            {
                BasePlayer Staff = BasePlayer.FindByID(userID);
                RemoveReputation(Staff, ReputationTake, true);
		   		 		  						  	   		  	 				   					  		 			  	   
                if (result == "SUCCESS")
                {
                    if (Staff == null) return;
		   		 		  						  	   		  	 				   					  		 			  	   
                    String LogDetails = LanguageEn
                        ? $"User {Staff.displayName} ({userID}) successfully exchanged reputation : {ReputationTake} for {Balance} balance"
                        : $"Пользователь {Staff.displayName} ({userID}) успешно обменял репутацию : {ReputationTake} на {Balance} баланса";
                    Puts(LogDetails);
                    SendChat(GetLang("STAFF_SUCSESS_TRANSFER_TO_STORE", Staff.UserIDString, ReputationTake, Balance), Staff);
                    
                    DiscordSendLog(Staff.displayName, Staff.UserIDString, LogDetails, config.DiscordSettings);
                    Log(LogDetails);
                    return;
                }
                
                if (Staff != null)
                {
                    SendChat(GetLang("STAFF_SUCSESS_TRANSFER_TO_STORE_ERROR", Staff.UserIDString), Staff);
                    AddReputation(Staff, ReputationTake);
                }
                
            }));
        }
            
	    
        
        [ChatCommand("rep")]
        private void CheckMyReputation(BasePlayer Staff)
        {
            if (Staff == null) return;
            if (!DataPlayer.ContainsKey(Staff.userID)) return;

            InformationPlayer StaffData = DataPlayer[Staff.userID];

            String Message = String.Empty;

            Message = GetLang("STAFF_INFO_COMMAND", Staff.UserIDString, StaffData.Reputation,
                StaffData.Reputation == 0
                    ? GetLang("STAFF_NEUTRAL_REPUTATION", Staff.UserIDString)
                    : StaffData.Reputation > 0
                        ? GetLang("STAFF_POSITIVE_REPUTATION", Staff.UserIDString)
                        : GetLang("STAFF_NEGATIVE_REPUTATION", Staff.UserIDString));

            if (config.ReputationTransferToStoreSettings.UseGameStores || config.ReputationTransferToStoreSettings.UseMoscovOVH)
                Message += GetLang("STAFF_ALERT_COURCE_AND_TRANSFER", Staff.UserIDString, config.ReputationTransferToStoreSettings.ReputationCourse);
            
            SendChat(Message, Staff);
        }
        
        [ChatCommand("staff")]
        void IQStaffCommandTurned(BasePlayer Staff)
        {
            if (!DataPlayer.ContainsKey(Staff.userID)) return;
            InformationPlayer DataStaff = DataPlayer[Staff.userID];
            if (!config.PresetsStaff.ContainsKey(DataStaff.UniqKeyStaff)) return;
            Configuration.StaffPresets.CommandController.CommandPresets Preset = config.PresetsStaff[DataStaff.UniqKeyStaff].CommandControllerSetting.PresetCommandStaff;
            if (!Preset.UseFunction) return;
            Configuration.StaffPresets.FunctionReference ReferenceFunction = config.PresetsStaff[DataStaff.UniqKeyStaff].ReferenceFunction;
            String LogDetails = String.Empty;
            
            if (!StaffCommandUsed.Contains(Staff))
            {
                StaffCommandUsed.Add(Staff);
                SendChat(GetLang("STAFF_ACCESS_COMMAND_TRUE", Staff.UserIDString), Staff);
                LogDetails = LanguageEn ? $"Staff {Staff.displayName} ({Staff.UserIDString}) enabled commands to use" : $"Персонал {Staff.displayName} ({Staff.UserIDString}) включил команды для использования";
            }
            else
            {
                StaffCommandUsed.Remove(Staff);
                SendChat(GetLang("STAFF_ACCESS_COMMAND_FALSE", Staff.UserIDString), Staff);
                LogDetails = LanguageEn ? $"Staff {Staff.displayName} ({Staff.UserIDString}) disabled commands to use" : $"Персонал {Staff.displayName} ({Staff.UserIDString}) выключил команды для использования";
            }

            Boolean statusTurned = StaffCommandUsed.Contains(Staff);
            AdminEspToggle(Staff, statusTurned, ReferenceFunction);
            
            if (ReferenceFunction.UseVanish)
                VanishToggle(Staff, statusTurned);
            
            Log(LogDetails);
            DiscordSendLog(Staff.displayName, Staff.UserIDString, LogDetails, config.PresetsStaff[DataStaff.UniqKeyStaff].CommandControllerSetting.DiscordAlert);
        }
        private Dictionary<BasePlayer, List<LimitInformation>> LimitController = new Dictionary<BasePlayer, List<LimitInformation>>();
        
        private List<BasePlayer> StaffCommandUsed = new List<BasePlayer>();

        
        
        
        
        public class GameStoresConfiguration
        {
            public class API
            {
                [JsonProperty("ИД магазина в сервисе")]
                public string ShopID;

                [JsonProperty("Секретный ключ (не распространяйте его)")]
                public string SecretKey;
            }

            [JsonProperty("Настройки API плагина")]
            public API APISettings;
        }
        
        [ChatCommand("rep.store")]
        private void TransferMyReputationToStore(BasePlayer Staff)
        {
            if (Staff == null) return;
            if (!DataPlayer.ContainsKey(Staff.userID)) return;

            InformationPlayer StaffData = DataPlayer[Staff.userID];
		   		 		  						  	   		  	 				   					  		 			  	   
            Int32 Cource = config.ReputationTransferToStoreSettings.ReputationCourse;
            Int32 TakeBalance = (Int32)StaffData.Reputation;
            
            Int32 ResultGiveBalance = TakeBalance / Cource * 1;
            Int32 remainder = TakeBalance % Cource;

            if (remainder != 0)
            {
                TakeBalance -= remainder;
                ResultGiveBalance = TakeBalance / Cource * 1;
            }

            if (TakeBalance == 0 || ResultGiveBalance == 0) return;

            if (config.ReputationTransferToStoreSettings.UseGameStores)
            {
                GameStoresBalanceSet(Staff.userID, ResultGiveBalance, TakeBalance);
                SendChat("You have successfully exchanged your reputation!",Staff); 
                return;
            }
            
            if (config.ReputationTransferToStoreSettings.UseMoscovOVH)
            {
                MoscovOVHBalanceSet(Staff.userID, ResultGiveBalance, TakeBalance);
                return;
            }
        }

        public class Authors
        {
            public string name { get; set; }
            public string url { get; set; }
            public string icon_url { get; set; }
            public string proxy_icon_url { get; set; }
            public Authors(string name, string url, string icon_url, string proxy_icon_url)
            {
                this.name = name;
                this.url = url;
                this.icon_url = icon_url;
                this.proxy_icon_url = proxy_icon_url;
            }
        }
        
        
        void API_AddStaff(UInt64 userID, String UniqKey) => AddStaff(userID, UniqKey);
		   		 		  						  	   		  	 				   					  		 			  	   
        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();

        private void UseCommandLimit(BasePlayer Staff, String Command, InformationPlayer DataStaff)
        {
            List<Configuration.StaffPresets.CommandController.LimitController> commandLimits = config.PresetsStaff[DataStaff.UniqKeyStaff].CommandControllerSetting.ListCommandsLimit;
            if (commandLimits.Count == 0) return;
            
            Configuration.StaffPresets.CommandController.LimitController limitForCommand = GetLimitForCommand(DataStaff, Command);
            if (limitForCommand == null) return;
		   		 		  						  	   		  	 				   					  		 			  	   
            List<LimitInformation> staffLimits;
            if (!LimitController.TryGetValue(Staff, out staffLimits))
            {
                staffLimits = new List<LimitInformation>
                {
                    new LimitInformation
                    {
                        Command = Command,
                        TimeLimit = CurrentTime() + limitForCommand.TimeLimit,
                        UseCount = 1
                    }
                };
                LimitController.Add(Staff, staffLimits);
            }
            
            LimitInformation limitStaff = staffLimits.FirstOrDefault(li => li.Command.Equals(Command));

            if (limitStaff == null)
            {
                staffLimits.Add(new LimitInformation
                {
                    Command = Command,
                    TimeLimit = CurrentTime() + limitForCommand.TimeLimit,
                    UseCount = 1
                });
            }
            else if (limitStaff.TimeLimit >= CurrentTime() && limitStaff.UseCount >= limitForCommand.Limit)
            {
                RemoveStaff(Staff.userID, TypeRemoveStaff.CommandController, Command);
                SendChat(GetLang("STAFF_REMOVE_FOR_COMMAND_CONTROLLER", Staff.UserIDString), Staff);
                
                String CommandList = staffLimits.Aggregate(String.Empty, (current, limitInformation) => current + (LanguageEn ? $"\n- {limitInformation.Command} : {limitInformation.UseCount} times" : $"\n- {limitInformation.Command} : {limitInformation.UseCount} раз"));

                String LogDetails = LanguageEn ? $"{Staff.displayName} ({Staff.userID}) has exceeded the available limit of command usage: {Command} within {limitStaff.TimeLimit - CurrentTime()} seconds.\nList of commands that he used: {CommandList}" : $"{Staff.displayName} ({Staff.userID}) превысил доступный лимит использования команды : {Command} в течении : {limitStaff.TimeLimit - CurrentTime()} секунд.\nСписок команд которые он использовал : {CommandList}";

                timer.In(3f, () =>
                {
                    DiscordSendLog(Staff.displayName, Staff.UserIDString, LogDetails,
                        config.PresetsStaff[DataStaff.UniqKeyStaff].CommandControllerSetting.DiscordAlert);
                });
                Log(LogDetails);
            }
            else limitStaff.UseCount++;
        }

                
        
                public class FancyMessage
        {
            public string content { get; set; }
            public bool tts { get; set; }
            public Embeds[] embeds { get; set; }
		   		 		  						  	   		  	 				   					  		 			  	   
            public class Embeds
            {
                public string title { get; set; }
                public int color { get; set; }
                public List<Fields> fields { get; set; }
                public Footer footer { get; set; }
                public Authors author { get; set; }

                public Embeds(string title, int color, List<Fields> fields, Authors author, Footer footer)
                {
                    this.title = title;
                    this.color = color;
                    this.fields = fields;
                    this.author = author;
                    this.footer = footer;

                }
            }

            public FancyMessage(string content, bool tts, Embeds[] embeds)
            {
                this.content = content;
                this.tts = tts;
                this.embeds = embeds;
            }

            public string toJSON() => JsonConvert.SerializeObject(this);
        }
        protected override void SaveConfig() => Config.WriteObject(config);

        
        
        void OnStoppedChecked(UInt64 TargetID, BasePlayer Staff, Boolean AutoStop = false, Boolean IsConsole = false)
        {
            if (IsConsole || AutoStop) return;
            
            if (!DataPlayer.ContainsKey(Staff.userID)) return;
            String UniqKey = DataPlayer[Staff.userID].UniqKeyStaff;
            if (!config.PresetsStaff.ContainsKey(UniqKey)) return;

            Configuration.StaffPresets Preset = config.PresetsStaff[UniqKey];

            if (Preset.PositiveSetting.SettingCheckIQReportSystem.UseFunction)
                AddReputation(Staff, Preset.PositiveSetting.SettingCheckIQReportSystem.Reputation);
        }
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<String, String>
            {
                ["MESSAGE_STAFF_REPUTATION_ADD"] = "Successfully received +{0} reputation",
                ["MESSAGE_STAFF_REPUTATION_REMOVE"] = "Removed -{0} reputation",
                
                ["STAFF_INFO_COMMAND"] = "Your reputation: {0}\n{1}\n\n**Try to maintain a positive reputation!",
                ["STAFF_NEUTRAL_REPUTATION"] = "<color=#FFC166>Reputation level is in neutral position!</color>",
                ["STAFF_NEGATIVE_REPUTATION"] = "<color=#FF6666>Reputation level is in a negative state!</color>",
                ["STAFF_POSITIVE_REPUTATION"] = "<color=#33CC66>Reputation level is positive!</color>",
                ["STAFF_ALERT_COURCE_AND_TRANSFER"] = "\n\nYou can exchange your reputation for a store balance at the rate {0} -> 1.\nTo exchange, enter the command: <color=#33CC66>/rep.store</color>",
                ["STAFF_SUCSESS_TRANSFER_TO_STORE"] = "You <color=#33CC66>successfully</color> exchanged {0} reputation points for {1} balance in the store",
                ["STAFF_SUCSESS_TRANSFER_TO_STORE_ERROR"] = "Exchange <color=#FF6666>unsuccessful</color> your reputation points have been returned!\nLog in to the store or report an error to the administrator",
                ["STAFF_SUCSESS_TRANSFER_TO_STORE_MESSAGE"] = "Exchange reputation for balance",
                ["STAFF_REMOVE_FOR_COMMAND_CONTROLLER"] = "You have been removed from the team for <color=#FF6666>violating command usage</color> - your rights have been annulled",
                ["STAFF_ACCESS_COMMAND_TRUE"] = "You <color=#72e090>enabled</color> staff rights",
                ["STAFF_ACCESS_COMMAND_FALSE"] = "You <color=#FF6666>disabled</color> staff rights",
                ["STAFF_ACCESS_FALSE_USED_COMMAND"] = "You cannot use this command, please enable staff mode using /staff",

            }, this);

            lang.RegisterMessages(new Dictionary<String, String>
            {
                ["MESSAGE_STAFF_REPUTATION_ADD"] = "Успешно получил +{0} репутации",
                ["MESSAGE_STAFF_REPUTATION_REMOVE"] = "Отнято -{0} репутации",
                
                ["STAFF_INFO_COMMAND"] = "Ваша репутация : {0}\n{1}\n\n**Старайтесь удерживать репутацию в плюсе!",
                ["STAFF_NEUTRAL_REPUTATION"] = "<color=#FFC166>Уровень репутации находится в нейтральном положении!</color>",
                ["STAFF_NEGATIVE_REPUTATION"] = "<color=#FF6666>Уровень репутации находится в негативном положении!</color>",
                ["STAFF_POSITIVE_REPUTATION"] = "<color=#33CC66>Уровень репутации положительный!</color>",
                ["STAFF_ALERT_COURCE_AND_TRANSFER"] = "\n\nВы можете обменять свою репутацию на баланс в магазине по курсу {0} -> 1.\nЧтобы обменять, введие команду : <color=#33CC66>/rep.store</color>",
                ["STAFF_SUCSESS_TRANSFER_TO_STORE"] = "Вы <color=#33CC66>успешно</color> обменяли {0} очков репутации на {1} баланса в магазин",
                ["STAFF_SUCSESS_TRANSFER_TO_STORE_ERROR"] = "Обмен <color=#FF6666>неудачный</color> ваши очки репутации были возвращены!\nАвторизируйтесь в магазине, либо сообщите администратору об ошибке",
                ["STAFF_SUCSESS_TRANSFER_TO_STORE_MESSAGE"] = "Обмен репутации на баланс",
                ["STAFF_REMOVE_FOR_COMMAND_CONTROLLER"] = "Вы были исключены из команды за <color=#FF6666>нарушение использования команд</color> - ваши права аннулированы",
                ["STAFF_ACCESS_COMMAND_TRUE"] = "Вы <color=#72e090>включили</color> права персонала",
                ["STAFF_ACCESS_COMMAND_FALSE"] = "Вы <color=#FF6666>отключили</color> права персонала",
                ["STAFF_ACCESS_FALSE_USED_COMMAND"] = "Вы не можете использовать эту команду, включите режим персонала с помощью /staff",
                
            }, this, "ru");
            PrintWarning(LanguageEn ? "Lang file loaded" : "Языковой файл загружен успешно");
        }

        private void Unload()
        {
            if(_timer != null && !_timer.Destroyed)
                _timer.Destroy();
             
            WriteData();
        }
        
        
        
        private void VanishToggle(BasePlayer player, Boolean status)
        {
            if (!Vanish) return;
            Boolean isVanish = Vanish.Call<Boolean>("IsInvisible", player);
		   		 		  						  	   		  	 				   					  		 			  	   
            if (status)
            {
                if (!isVanish)
                    Vanish.Call("Disappear", player);
            }
            else Vanish.Call("Reappear", player);
        }
        private class Configuration
        {
            [JsonProperty(LanguageEn ? "Staff templates (unique key - setting)" : "Шаблоны персонала (уникальный ключ - настройка)")]
            public Dictionary<String, StaffPresets> PresetsStaff = new Dictionary<String, StaffPresets>();
            
            [JsonProperty(LanguageEn ? "Setting IQChat" : "Настройка IQChat")]
            public IQChatSetting IQChatSettings = new IQChatSetting();
            
            [JsonProperty(LanguageEn ? "Configuring general notifications in Discord" : "Настройка общих уведомлений в Discord")]
            public DiscordSetting DiscordSettings = new DiscordSetting();
            
            [JsonProperty(LanguageEn ? "Setting the ability to withdraw reputation to the balance of the store" : "Настройка возможности вывести репутацию в баланс магазина")]
            public ReputationTransferToStore ReputationTransferToStoreSettings = new ReputationTransferToStore();
            
            internal class StaffPresets
            {
                [JsonProperty(LanguageEn ? "" : "Взаимодействие с другими плагинами")]
                public FunctionReference ReferenceFunction = new();
                [JsonProperty(LanguageEn ? "The list of permissions that will be granted to the player after enlisting in the staff (they will be automatically removed from him after his exclusion)" : "Список прав которые будут выдану игроку после зачисления в персонал (они будут автоматически удалены у него после его исключения)")]
                public List<String> PermissionsList = new List<String>();
                [JsonProperty(LanguageEn ? "The list of groups that will be granted to the player after enlisting in the staff (they will be automatically removed from him after his exclusion)" : "Список групп которые будут выдану игроку после зачисления в персонал (они будут автоматически удалены у него после его исключения)")]
                public List<String> GroupsList = new List<String>();
                
                [JsonProperty(LanguageEn ? "Setting up the accrual of reputation to a player for certain actions" : "Настройка начисления репутации игроку за определенные действия")]
                public PositiveIndicators PositiveSetting = new PositiveIndicators();
                [JsonProperty(LanguageEn ? "Setting up the removal of a player's reputation for certain actions" : "Настройка снятия репутации игроку за определенные действия")]
                public NegativeIndicators NegativeSetting = new NegativeIndicators();

                [JsonProperty(LanguageEn ? "At what indicator of negative reputation to remove a player from the staff" : "При каком показателе негативной репутации удалять игрока из персонала")]
                public PresetControlled ControllerPersonal = new PresetControlled();

                [JsonProperty(LanguageEn ? "Configuring the use of commands by staff" : "Настройка использования команд персоналом")]
                public CommandController CommandControllerSetting = new CommandController();

                internal class FunctionReference
                {
                    [JsonProperty(LanguageEn ? "" : "Включать автоматически радар (AdminRadar) при включении /staff (при отключении будет выключаться)")]
                    public Boolean UseAdminRadar;
                    [JsonProperty(LanguageEn ? "" : "Включать автоматически есп (AdminESP) при включении /staff (при отключении будет выключаться)")]
                    public Boolean UseAdminESP;
                    [JsonProperty(LanguageEn ? "" : "Включать автоматически ваниш (Vanish) при включении /staff (при отключении будет выключаться)")]
                    public Boolean UseVanish;
                }
                internal class CommandController
                {
                    [JsonProperty(LanguageEn ? "List of commands to prohibit use or notification of use" : "Список команд для запрета в использовании или уведомлении об использовании")]
                    public List<Controller> ListCommands = new List<Controller>();
                    internal class Controller
                    {
                        [JsonProperty(LanguageEn ? "Command" : "Команда")]
                        public String Command;
                        [JsonProperty(LanguageEn ? "Block the use of this command and notify - true / Notify about the use of this command - false" : "Блокировать использование данной команды и уведомлять - true / Уведомлять об использовании данной команды - false")]
                        public Boolean BlockedUse;
                    }
                    
                    [JsonProperty(LanguageEn ? "Limit on the use of commands in N time interval (example: 10 commands within 1 minute = removal of rights and removal from the staff)" : "Лимит использования команд в N промежуток времени (пример : 10 команд в течении 1 минуты = снятие прав и удаление из персонала) ")]
                    public List<LimitController> ListCommandsLimit = new List<LimitController>();
                  
                    internal class LimitController
                    {
                        [JsonProperty(LanguageEn ? "Command" : "Команда")]
                        public String Command;
                        [JsonProperty(LanguageEn ? "Limit" : "Лимит")]
                        public Int32 Limit;
                        [JsonProperty(LanguageEn ? "Time to reset the limit (seconds)" : "Время до сброса лимита (секунды)")]
                        public Int32 TimeLimit;
                    }
                    
                    [JsonProperty(LanguageEn ? "Setting up unblocking access to commands using /staff (reusing the command will block them) (with notification in Discord - if enabled)" : "Настройка разблокировки доступа к командам с помощью /staff (повторное использование команды - заблокирует их) (с уведомлением в Discord - если включено)")]
                    public CommandPresets PresetCommandStaff = new CommandPresets();
                    internal class CommandPresets
                    {
                        [JsonProperty(LanguageEn ? "Use this function (true - yes/false - no)" : "Использовать данную функцию (true - да/false - нет)")]
                        public Boolean UseFunction;
                        [JsonProperty(LanguageEn ? "List of commands that are unlocked to the user by the command /staff" : "Список команд, которые разблокируются пользователю по команде /staff")]
                        public List<String> CommandsList = new List<String>();
                    }
                    
                    [JsonProperty(LanguageEn ? "Notifications in Discord about the use of these commands by staff" : "Уведомления в Discord об использовании данных команд персоналом")]
                    public DiscordSetting DiscordAlert = new DiscordSetting();
                }
                internal class PositiveIndicators
                {
                    [JsonProperty(LanguageEn ? "IQChat : Setting up rewards for mutes" : "IQChat : Настройка награждения за муты")]
                    public PresetControlled SettingMuteIQChat = new PresetControlled();
                    [JsonProperty(LanguageEn ? "IQReportSystem : Setting up rewards for player checks" : "IQReportSystem : Настройка награждения за проверки игрока")]
                    public PresetControlled SettingCheckIQReportSystem = new PresetControlled();
                    [JsonProperty(LanguageEn ? "IQReportSystem : Setting up rewards for blocking a player after checking it" : "IQReportSystem : Настройка награждения за блокировки игрока после его проверки")]
                    public PresetControlled SettingBansIQReportSystem = new PresetControlled();
                    [JsonProperty(LanguageEn ? "IQReportSystem : Setting up awards for feedback received after checking" : "IQReportSystem : Настройка награждения за полученный отзыв после проврки")]
                    public IQReportFeedbackPositive SettingFeedbackIQReportSystem = new IQReportFeedbackPositive();
                    
                    internal class IQReportFeedbackPositive
                    {
                        [JsonProperty(LanguageEn ? "From how many stars received to award" : "От скольки полученных звезд награждать")]
                        public Int32 AmountStar;
                        [JsonProperty(LanguageEn ? "Awarding" : "Награждение")]
                        public PresetControlled Preset = new PresetControlled();
                    }
                }

                internal class NegativeIndicators
                {
                    [JsonProperty(LanguageEn ? "Setting up the removal of reputation for bad words in IQChat" : "Настройка снятия репутации за плохие слова в IQChat")]
                    public PresetControlled SettingBadWordsIQChat = new PresetControlled();
                    [JsonProperty(LanguageEn ? "IQReportSystem : Setting up the removal of reputation for a bad review received after verification" : "IQReportSystem : Настройка снятия репутации за полученный плохой отзыв после проврки")]
                    public IQReportFeedbackNegative SettingFeedbackIQReportSystem = new IQReportFeedbackNegative();
                    
                    internal class IQReportFeedbackNegative
                    {
                        [JsonProperty(LanguageEn ? "Up to how many stars is a review considered bad" : "До скольки звезд отзыв считается плохим")]
                        public Int32 AmountStar;
                        [JsonProperty(LanguageEn ? "Fine" : "Штраф")]
                        public PresetControlled Preset = new PresetControlled();
                    }
                }
		   		 		  						  	   		  	 				   					  		 			  	   
                internal class PresetControlled
                {
                    [JsonProperty(LanguageEn ? "Use this function" : "Использовать эту функцию")]
                    public Boolean UseFunction;
                    [JsonProperty(LanguageEn ? "The amount of reputation" : "Количество репутации")]
                    public Single Reputation;
                }
            }

            internal class DiscordSetting
            {
                [JsonProperty(LanguageEn ? "Use this function" : "Использовать эту функцию")]
                public Boolean UseLogged;
                [JsonProperty(LanguageEn ? "WebHook your channel Discord" : "WebHook вашего канала Discord")]
                public String WebHooks;
                [JsonProperty(LanguageEn ? "Avatar message Discord" : "Иконка сообщения в Discord")]
                public String IconPng;
            }
            
            internal class IQChatSetting
            {
                [JsonProperty(LanguageEn ? "IQChat : Custom prefix in the chat" : "IQChat : Кастомный префикс в чате")]
                public String CustomPrefix;
                [JsonProperty(LanguageEn ? "IQChat : Custom avatar in the chat (If required)" : "IQChat : Кастомный аватар в чате(Если требуется)")]
                public String CustomAvatar;
            }

            internal class ReputationTransferToStore
            {
                [JsonProperty(LanguageEn ? "The number of reputation points takes away for 1 balance" : "Сколько очков репутации отнимать за 1 баланса")]
                public Int32 ReputationCourse;
                
                [JsonProperty(LanguageEn ? "Use MoscovOVH to withdraw funds (true - yes/false - no)" : "Использовать MoscovOVH для вывода средств (true - да/false - нет)")]
                public Boolean UseMoscovOVH;
                [JsonProperty(LanguageEn ? "Use GameStores to withdraw funds (true - yes/false - no)" : "Использовать GameStores для вывода средств (true - да/false - нет)")]
                public Boolean UseGameStores;
                [JsonProperty(LanguageEn ? "Settings GameStores" : "Настройки GameStores")]
                public GameStoresConfig GSConfig = new GameStoresConfig();
                internal class GameStoresConfig
                {
                    [JsonProperty(LanguageEn ? "Store ID in the service" : "ИД магазина в сервисе")]
                    public String ShopID;
                    [JsonProperty(LanguageEn ? "Secret key (don't share it)" : "Секретный ключ (не распространяйте его)")]
                    public String SecretKey;
                }
            }
            
            public static Configuration GetNewConfiguration() 
            {
                return new Configuration
                {
                    PresetsStaff = new Dictionary<String, StaffPresets>()
                    {
                        ["moderation"] = new StaffPresets
                        {
                            ReferenceFunction = new StaffPresets.FunctionReference
                            {
                                UseAdminRadar = true,
                                UseAdminESP = false
                            },
                            PermissionsList = new List<String>()
                            {
                                "iqchat.moderator",
                                "iqchat.muteuse",
                                "iqreportsystem.moderation",
                            },
                            GroupsList = new List<String>()
                            {
                                "moderation"
                            },
                            ControllerPersonal = new StaffPresets.PresetControlled()
                            {
                                UseFunction = true,
                                Reputation = -30.0f,
                            },
                            PositiveSetting = new StaffPresets.PositiveIndicators
                            {
                                SettingMuteIQChat = new StaffPresets.PresetControlled
                                {
                                    UseFunction = true,
                                    Reputation = 2.0f
                                },
                                SettingCheckIQReportSystem = new StaffPresets.PresetControlled()
                                {
                                    UseFunction = true,
                                    Reputation = 2.5f
                                },
                                SettingBansIQReportSystem = new StaffPresets.PresetControlled()
                                {
                                    UseFunction = true,
                                    Reputation = 3.5f
                                },
                                SettingFeedbackIQReportSystem = new StaffPresets.PositiveIndicators.IQReportFeedbackPositive()
                                {
                                    AmountStar = 4,
                                    Preset = new StaffPresets.PresetControlled()
                                    {
                                        UseFunction = true,
                                        Reputation = 5.0f
                                    }
                                }
                            },
                            NegativeSetting = new StaffPresets.NegativeIndicators
                            {
                                SettingBadWordsIQChat = new StaffPresets.PresetControlled()
                                {
                                    UseFunction = true,
                                    Reputation = 5.0f
                                },
                                SettingFeedbackIQReportSystem = new StaffPresets.NegativeIndicators.IQReportFeedbackNegative()
                                {
                                    AmountStar = 2,
                                    Preset = new StaffPresets.PresetControlled()
                                    {
                                        UseFunction = true,
                                        Reputation = 5.0f
                                    }
                                }
                            },
                            CommandControllerSetting = new StaffPresets.CommandController()
                            {
                                DiscordAlert = new DiscordSetting()
                                {
                                    UseLogged = false,
                                    WebHooks = "",
                                    IconPng = "https://i.imgur.com/7G03zC5.png"
                                },
                                PresetCommandStaff = new StaffPresets.CommandController.CommandPresets()
                                {
                                    UseFunction = false,
                                    CommandsList = new List<String>()
                                    {
                                        "ban",
                                        "kick",
                                        "unban",
                                    },
                                },
                                ListCommandsLimit = new List<StaffPresets.CommandController.LimitController>()
                                {
                                    new StaffPresets.CommandController.LimitController()
                                    {
                                        Command = "ban",
                                        Limit = 10,
                                        TimeLimit = 60,
                                    },  
                                    new StaffPresets.CommandController.LimitController()
                                    {
                                        Command = "kick",
                                        Limit = 10,
                                        TimeLimit = 60,
                                    },  
                                },
                                ListCommands = new List<StaffPresets.CommandController.Controller>()
                                {
                                    new StaffPresets.CommandController.Controller()
                                    {
                                        BlockedUse = false,
                                        Command = "kick",
                                    },
                                    new StaffPresets.CommandController.Controller()
                                    {
                                        BlockedUse = false,
                                        Command = "ban",
                                    },
                                    new StaffPresets.CommandController.Controller()
                                    {
                                        BlockedUse = true,
                                        Command = "give",
                                    },
                                }
                            }
                        },
                        ["helper"] = new StaffPresets
                        {
                            ReferenceFunction = new StaffPresets.FunctionReference
                            {
                                UseAdminRadar = false,
                                UseAdminESP = false
                            },
                            PermissionsList = new List<String>()
                            {
                                "iqchat.helper",
                                "iqchat.muteuse",
                            },
                            GroupsList = new List<String>()
                            {
                                "helper"
                            },
                            ControllerPersonal = new StaffPresets.PresetControlled()
                            {
                                UseFunction = true,
                                Reputation = -15.0f,
                            },
                            PositiveSetting = new StaffPresets.PositiveIndicators
                            {
                                SettingMuteIQChat = new StaffPresets.PresetControlled
                                {
                                    UseFunction = true,
                                    Reputation = 1.0f
                                },
                                SettingCheckIQReportSystem = new StaffPresets.PresetControlled()
                                {
                                    UseFunction = false,
                                    Reputation = 1.5f
                                },
                                SettingBansIQReportSystem = new StaffPresets.PresetControlled()
                                {
                                    UseFunction = false,
                                    Reputation = 2.5f
                                },
                                SettingFeedbackIQReportSystem = new StaffPresets.PositiveIndicators.IQReportFeedbackPositive()
                                {
                                    AmountStar = 4,
                                    Preset = new StaffPresets.PresetControlled()
                                    {
                                        UseFunction = false,
                                        Reputation = 5.0f
                                    }
                                }
                            },
                            NegativeSetting = new StaffPresets.NegativeIndicators
                            {
                                SettingBadWordsIQChat = new StaffPresets.PresetControlled()
                                {
                                    UseFunction = true,
                                    Reputation = 2.0f
                                },
                                SettingFeedbackIQReportSystem = new StaffPresets.NegativeIndicators.IQReportFeedbackNegative()
                                {
                                    AmountStar = 2,
                                    Preset = new StaffPresets.PresetControlled()
                                    {
                                        UseFunction = false,
                                        Reputation = 5.0f
                                    }
                                }
                            },
                            CommandControllerSetting = new StaffPresets.CommandController()
                            {
                                DiscordAlert = new DiscordSetting()
                                {
                                    UseLogged = false,
                                    WebHooks = "",
                                    IconPng = "https://i.imgur.com/7G03zC5.png"
                                },
                                PresetCommandStaff = new StaffPresets.CommandController.CommandPresets()
                                {
                                    UseFunction = false,
                                    CommandsList = new List<String>()
                                    {
                                        "mute",
                                        "unmute"
                                    },
                                },
                                ListCommandsLimit = new List<StaffPresets.CommandController.LimitController>()
                                {
                                    new StaffPresets.CommandController.LimitController()
                                    {
                                        Command = "ban",
                                        Limit = 10,
                                        TimeLimit = 60,
                                    },  
                                    new StaffPresets.CommandController.LimitController()
                                    {
                                        Command = "kick",
                                        Limit = 10,
                                        TimeLimit = 60,
                                    },  
                                },
                                ListCommands = new List<StaffPresets.CommandController.Controller>()
                                {
                                    new StaffPresets.CommandController.Controller()
                                    {
                                        BlockedUse = false,
                                        Command = "kick",
                                    },
                                    new StaffPresets.CommandController.Controller()
                                    {
                                        BlockedUse = false,
                                        Command = "ban",
                                    },
                                    new StaffPresets.CommandController.Controller()
                                    {
                                        BlockedUse = true,
                                        Command = "give",
                                    },
                                }
                            }
                        },
                    },
                    IQChatSettings = new IQChatSetting()
                    {
                      CustomAvatar = "",
                      CustomPrefix = "[<color=#FFCC66>IQStaff</color>]\n"
                    },
                    DiscordSettings = new DiscordSetting()
                    {
                        UseLogged = false,
                        WebHooks = "",
                        IconPng = "https://i.imgur.com/7G03zC5.png"
                    },
                    ReputationTransferToStoreSettings = new ReputationTransferToStore()
                    {
                        ReputationCourse = 1,
                        UseGameStores = false,
                        UseMoscovOVH = false,
                        GSConfig = new ReputationTransferToStore.GameStoresConfig()
                        {
                            SecretKey = "",
                            ShopID = ""
                        }
                    }
                };
            }
        }

        void OnVerdictChecked(UInt64 TargetID, BasePlayer Staff, String VerdictReason, String VerdictCommand)
        {
            if (!DataPlayer.ContainsKey(Staff.userID)) return;
            String UniqKey = DataPlayer[Staff.userID].UniqKeyStaff;
            if (!config.PresetsStaff.ContainsKey(UniqKey)) return;

            Configuration.StaffPresets Preset = config.PresetsStaff[UniqKey];

            if (Preset.PositiveSetting.SettingBansIQReportSystem.UseFunction)
                AddReputation(Staff, Preset.PositiveSetting.SettingBansIQReportSystem.Reputation);
        }
                
        private void Log(String LoggedMessage) => LogToFile("IQStaffLogs", $"[{DateTime.Now}] " + LoggedMessage, this);

        
        
        
        private void ValidateStaff()
        {
            List<UInt64> userIDValidated = Facepunch.Pool.GetList<UInt64>();

            foreach (KeyValuePair<String, Configuration.StaffPresets> presetStaff in config.PresetsStaff)
            {
                foreach (KeyValuePair<UInt64, InformationPlayer> informationPlayer in DataPlayer)
                {
                    String userIDString = informationPlayer.Key.ToString();
                    if (informationPlayer.Value.UniqKeyStaff.Equals(presetStaff.Key))
                    {
                        ValidatePermissions(informationPlayer, presetStaff, userIDString, userIDValidated);
                        ValidateGroups(informationPlayer, presetStaff, userIDString, userIDValidated);
                    }
                }
            }

            if(userIDValidated.Count != 0)
                PrintWarning(LanguageEn ? $"The staff was validated. Data from {userIDValidated.Count} staff members have been updated" : $"Была произведена валидация персонала. Данные у {userIDValidated.Count} участников персонала были обновлены");
            Facepunch.Pool.FreeList(ref userIDValidated);
        }
        
        private Object OnPlayerCommand(BasePlayer Staff, String command, String[] args)
        {
            if (Staff == null) return null;
            if (!DataPlayer.ContainsKey(Staff.userID)) return null;
            InformationPlayer DataStaff = DataPlayer[Staff.userID];
            if (!config.PresetsStaff.ContainsKey(DataStaff.UniqKeyStaff)) return null;
            
            if (args != null && args.Length != 0)
                command += " " + String.Join(" ", args);

            String OnlyCommand = !String.IsNullOrWhiteSpace(command) && command.Contains(" ") ? command.Substring(0, command.IndexOf(" ", StringComparison.Ordinal)) : command;

            UseCommandLimit(Staff, OnlyCommand, DataStaff);
            
            return UseCommandStaffList(Staff, OnlyCommand, DataStaff, command) != null ? false : UseCommandBlockOrAlert(Staff, OnlyCommand, DataStaff, command);
        }

        
        private Object UseCommandStaffList(BasePlayer Staff, String Command, InformationPlayer DataStaff, String FullCommand = "")
        {
            Configuration.StaffPresets.CommandController.CommandPresets Preset = config.PresetsStaff[DataStaff.UniqKeyStaff].CommandControllerSetting.PresetCommandStaff;

            if (!Preset.UseFunction) return null;
            if (!Preset.CommandsList.Contains(Command)) return null;
            if (!DataPlayer.ContainsKey(Staff.userID)) return null;
            if (!StaffCommandUsed.Contains(Staff))
            {
                SendChat(GetLang("STAFF_ACCESS_FALSE_USED_COMMAND", Staff.UserIDString), Staff);
                return false;
            }
            
            String LogDetails = LanguageEn ? $"{Staff.displayName} ({Staff.userID}) use command: {FullCommand ?? Command}" : $"{Staff.displayName} ({Staff.userID}) использовал : {FullCommand ?? Command}";

            DiscordSendLog(Staff.displayName, Staff.UserIDString, LogDetails, config.PresetsStaff[DataStaff.UniqKeyStaff].CommandControllerSetting.DiscordAlert);
            Log(LogDetails);

            return null;
        }
        private class LimitInformation
        {
            public String Command = String.Empty;
            public Double TimeLimit = 0;
            public Int32 UseCount = 0;
        }
        private void RemoveStaff(UInt64 userID, TypeRemoveStaff typeRemoveStaff = TypeRemoveStaff.None, String Command = "")
        {
            String UserID_String = userID.ToString();

            if (!DataPlayer.ContainsKey(userID)) return;
            
            InformationPlayer PresetPlayer = DataPlayer[userID];
            Single ThisReputation = PresetPlayer.Reputation;

            foreach (String Permission in PresetPlayer.PermissionList)
            {
                if (!permission.UserHasPermission(UserID_String, Permission)) continue;

                permission.RevokeUserPermission(UserID_String, Permission);
            }

            foreach (String Group in PresetPlayer.GroupsList)
            {
                if (!permission.UserHasGroup(UserID_String, Group)) continue;

                permission.RemoveUserGroup(UserID_String, Group);
            }

            DataPlayer.Remove(userID);

            String UserName = BasePlayer.FindByID(userID) == null ?  (LanguageEn ? "Unknown nickname" : "Неизвестный ник") : BasePlayer.FindByID(userID).displayName;
            String Information = String.Empty;
            
            Information = 
                typeRemoveStaff == TypeRemoveStaff.None ? (LanguageEn ? $"You have successfully excluded the player {userID} from the staff" : $"Вы успешно исключили игрока {userID} из состава персонала") 
                : typeRemoveStaff == TypeRemoveStaff.StaffController ? (LanguageEn ? $"The player {userID} was excluded from the staff for a negative reputation : {ThisReputation}" : $"Игрок {userID} был исключен из состава персонала за отрицательную репутацию : {ThisReputation}") 
                : typeRemoveStaff == TypeRemoveStaff.API ? (LanguageEn ? $"The player {userID} was excluded from the staff using a third-party API plugin" : $"Игрок {userID} был исключен из персонала с помощью стороннего плагина по API") : (LanguageEn ? $"Player {userID} was excluded from the staff for exceeding the command usage limit : {Command}" : $"Игрок {userID} был исключен из состава персонала за превышение лимита использование команды : {Command}");

            timer.In(3f, () =>
            {
                DiscordSendLog(UserName, userID.ToString(), Information, config.DiscordSettings);
                Puts(Information);
                Log(Information); 
            });
        }
		   		 		  						  	   		  	 				   					  		 			  	   
        void OnModeratorSendBadWords(BasePlayer Staff, String Message)
        {
            if (!DataPlayer.ContainsKey(Staff.userID)) return;
            String UniqKey = DataPlayer[Staff.userID].UniqKeyStaff;
            if (!config.PresetsStaff.ContainsKey(UniqKey)) return;

            Configuration.StaffPresets Preset = config.PresetsStaff[UniqKey];

            if (!Preset.NegativeSetting.SettingBadWordsIQChat.UseFunction) return;
            
            RemoveReputation(Staff, Preset.NegativeSetting.SettingBadWordsIQChat.Reputation);
        }
        
        private void RemoveReputation(BasePlayer Staff, Single Reputation, Boolean IsTransferToBalance = false)
        {
            if (!DataPlayer.ContainsKey(Staff.userID)) return;

            DataPlayer[Staff.userID].Reputation -= Reputation;
            ControllingStaff(Staff, DataPlayer[Staff.userID].UniqKeyStaff);

            if (IsTransferToBalance)
                return;
            
            SendChat(GetLang("MESSAGE_STAFF_REPUTATION_REMOVE", Staff.UserIDString, Reputation), Staff);
            DiscordSendLog(Staff.displayName, Staff.UserIDString, LanguageEn ? $"Removed -{Reputation} reputation" : $"Отнято -{Reputation} репутации", config.DiscordSettings);
        }

                
                
                private static Configuration config = new Configuration();
        
        
        
        [PluginReference] private Plugin IQChat, IQReportSystem, RustStore, AdminRadar, AdminESP, Vanish;
        public String GetLang(String LangKey, String userID = null, params object[] args)
        {
            sb.Clear();
            if (args != null)
            {
                sb.AppendFormat(lang.GetMessage(LangKey, this, userID), args);
                return sb.ToString();
            }
            return lang.GetMessage(LangKey, this, userID);
        }

        private void ReadData()
        {
            DataPlayer = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<UInt64, InformationPlayer>>("IQSystem/IQStaff/DataPlayer");
            TimeServer = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<DateTime>("IQSystem/IQStaff/TimeServer");
        }
        
        private Object OnServerCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer Staff = arg.Player();
		   		 		  						  	   		  	 				   					  		 			  	   
            if (Staff == null || arg.cmd.FullName == "chat.say") return null;

            if (!DataPlayer.ContainsKey(Staff.userID)) return null;
            InformationPlayer DataStaff = DataPlayer[Staff.userID];
            if (!config.PresetsStaff.ContainsKey(DataStaff.UniqKeyStaff)) return null;
            
            String command = arg.cmd.Name;
            if (arg.Args != null && arg.Args.Length != 0)
                command += " " + String.Join(" ", arg.Args);
            
            String OnlyCommand = !String.IsNullOrWhiteSpace(command) && command.Contains(" ") ? command.Substring(0, command.IndexOf(" ", StringComparison.Ordinal)) : command;
		   		 		  						  	   		  	 				   					  		 			  	   
            UseCommandLimit(Staff, OnlyCommand, DataStaff);

            return UseCommandStaffList(Staff, OnlyCommand, DataStaff, command) != null ? false : UseCommandBlockOrAlert(Staff, OnlyCommand, DataStaff, command);
        }

        private void ValidateGroups(KeyValuePair<UInt64, InformationPlayer> informationPlayer,
            KeyValuePair<String, Configuration.StaffPresets> presetStaff, String userIDString, List<UInt64> userIDValidated)
        {
            foreach (String groupPreset in presetStaff.Value.GroupsList)
            {
                if (!informationPlayer.Value.GroupsList.Contains(groupPreset))
                {
                    informationPlayer.Value.GroupsList.Add(groupPreset);
                    if (!permission.UserHasGroup(userIDString, groupPreset))
                        permission.AddUserGroup(userIDString, groupPreset);

                    if (!userIDValidated.Contains(informationPlayer.Key))
                        userIDValidated.Add(informationPlayer.Key);
                }
            }

            List<String> groupList = Facepunch.Pool.GetList<String>();
            groupList = new List<String>(informationPlayer.Value.GroupsList);
            
            foreach (String groupStaff in groupList)
            {
                if (!presetStaff.Value.GroupsList.Contains(groupStaff))
                {
                    informationPlayer.Value.GroupsList.Remove(groupStaff);
                    if (permission.UserHasGroup(userIDString, groupStaff))
                        permission.RemoveUserGroup(userIDString, groupStaff);

                    if (!userIDValidated.Contains(informationPlayer.Key))
                        userIDValidated.Add(informationPlayer.Key);
                }
            }
            
            Facepunch.Pool.FreeList(ref groupList);
        }

        
                
        
        private void OnServerInitialized() => ValidateStaff();


                
        
        private void AddStaff(UInt64 userID, String UniqKey)
        {
            Configuration.StaffPresets Preset = config.PresetsStaff[UniqKey];
            String UserID_String = userID.ToString();
		   		 		  						  	   		  	 				   					  		 			  	   
            if (DataPlayer.ContainsKey(userID)) return;
		   		 		  						  	   		  	 				   					  		 			  	   
            InformationPlayer PresetPlayer = new InformationPlayer();
            PresetPlayer.UniqKeyStaff = UniqKey;
            PresetPlayer.Reputation = 0;
            PresetPlayer.PermissionList = new List<String>();
            PresetPlayer.GroupsList = new List<String>();

            foreach (String Permission in Preset.PermissionsList)
            {
                if (PresetPlayer.PermissionList.Contains(Permission)) continue;
                PresetPlayer.PermissionList.Add(Permission);
                    
                if(!permission.UserHasPermission(UserID_String, Permission))
                    permission.GrantUserPermission(UserID_String, Permission, null);
            }

            foreach (String Group in Preset.GroupsList)
            {
                if (PresetPlayer.GroupsList.Contains(Group)) continue;
                PresetPlayer.GroupsList.Add(Group);

                if (!permission.UserHasGroup(UserID_String, Group))
                    permission.AddUserGroup(UserID_String, Group);
            }

            DataPlayer.Add(userID, PresetPlayer);
            
            Puts(LanguageEn ? $"You have successfully assigned the player {userID} as your staff with the key {UniqKey}" : $"Вы успешно назначили игрока {userID} в качестве вашего персонала с ключом {UniqKey}");
            
            String UserName = BasePlayer.FindByID(userID) == null ? (LanguageEn ? "Unknown nickname" : "Неизвестный ник") : BasePlayer.FindByID(userID).displayName;
            DiscordSendLog(UserName, userID.ToString(), LanguageEn ? "Assigned as server staff" : "Назначен в качестве персонала сервера", config.DiscordSettings);
           
            Log(LanguageEn ? $"{UserName} ({userID}) - assigned as server staff" : $"{UserName} ({userID}) - назначен в качестве персонала сервера");
        }

        
        
        private void AddReputation(BasePlayer Staff, Single Reputation, Boolean IsTransferToBalance = false)
        {
            if (!DataPlayer.ContainsKey(Staff.userID)) return;

            DataPlayer[Staff.userID].Reputation += Reputation;

            if (IsTransferToBalance)
                return;
            
            SendChat(GetLang("MESSAGE_STAFF_REPUTATION_ADD", Staff.UserIDString, Reputation), Staff);
            DiscordSendLog(Staff.displayName, Staff.UserIDString, LanguageEn ? $"Successfully received +{Reputation} reputation" : $"Успешно получил +{Reputation} репутации", config.DiscordSettings);
        }

        private void WriteData()
        {
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQSystem/IQStaff/DataPlayer", DataPlayer);
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQSystem/IQStaff/TimeServer", TimeServer);
        }
        
        
        private void AdminEspToggle(BasePlayer player, Boolean status, Configuration.StaffPresets.FunctionReference Preset)
        {
            if (AdminRadar && Preset.UseAdminRadar)
            {
                Boolean isRadar = AdminRadar.Call<Boolean>("IsRadar", player.UserIDString);

                if (status)
                {
                    if (!isRadar)
                        AdminRadar.Call("RadarCommand", player.IPlayer, "radar", Array.Empty<String>());
                }
                else AdminRadar.Call("DestroyRadar", player);
            }

            if (AdminESP && Preset.UseAdminESP)
            {
                if (status)
                {
                    Boolean isEsp = AdminESP.Call<Boolean>("API_HasActive", player.UserIDString);
                    if (!isEsp)
                        AdminESP.Call("API_EspActivate", player);
                }
                else AdminESP.Call("API_EspDeactivate", player);
            }
        }

        private void Request(string url, string payload, Action<int> callback = null)
        {
            Dictionary<string, string> header = new Dictionary<string, string>();
            header.Add("Content-Type", "application/json");
            webrequest.Enqueue(url, payload, (code, response) =>
            {
                if (code != 200 && code != 204)
                {
                    if (response != null)
                    {
                        try
                        {
                            JObject json = JObject.Parse(response);
                            if (code == 429)
                            {
                                float seconds = float.Parse(Math.Ceiling((double)(int)json["retry_after"] / 1000).ToString());
                            }
                            else
                            {
                                PrintWarning($"Error Discord #633323: Discord rejected that payload! Responded with \"{json["message"].ToString()}\" Code: {code}");
                            }
                        }
                        catch
                        {
                            PrintWarning($"Error Discord #267232: Failed to get a valid response from discord! Error: \"{response}\" Code: {code}");
                        }
                    }
                    else
                    {
                        PrintWarning($"Error Discord #942672338: Discord didn't respond (down?) Code: {code}");
                    }
                }
                try
                {
                    callback?.Invoke(code);
                }
                catch (Exception ex) { }

            }, this, RequestMethod.POST, header, 10f);
        }

        private Configuration.StaffPresets.CommandController.LimitController GetLimitForCommand(InformationPlayer DataStaff, String Command)
        {
            List<Configuration.StaffPresets.CommandController.LimitController> commandLimits = config.PresetsStaff[DataStaff.UniqKeyStaff].CommandControllerSetting.ListCommandsLimit;
            return commandLimits.FirstOrDefault(limit => limit.Command.Equals(Command));
        }
        private class InformationPlayer
        {
            public String UniqKeyStaff;
            public Single Reputation;
            public List<String> PermissionList = new List<String>();
            public List<String> GroupsList = new List<String>();
        }

        void OnSendedFeedbackChecked(UInt64 ModeratorID, Int32 IndexAchive, Int32 StarAmount)
        {
            if (ModeratorID == 0) return;
            BasePlayer Staff = BasePlayer.FindByID(ModeratorID);
            if (Staff == null) return;
            
            if (!DataPlayer.ContainsKey(Staff.userID)) return;
            String UniqKey = DataPlayer[Staff.userID].UniqKeyStaff;
            if (!config.PresetsStaff.ContainsKey(UniqKey)) return;

            Configuration.StaffPresets Preset = config.PresetsStaff[UniqKey];

            if (Preset.PositiveSetting.SettingFeedbackIQReportSystem.Preset.UseFunction)
                if (StarAmount >= Preset.PositiveSetting.SettingFeedbackIQReportSystem.AmountStar)
                {
                    AddReputation(Staff, Preset.PositiveSetting.SettingFeedbackIQReportSystem.Preset.Reputation);
                    return;
                }
		   		 		  						  	   		  	 				   					  		 			  	   
            if (Preset.NegativeSetting.SettingFeedbackIQReportSystem.Preset.UseFunction)
                if (StarAmount <= Preset.NegativeSetting.SettingFeedbackIQReportSystem.AmountStar)
                    RemoveReputation(Staff, Preset.NegativeSetting.SettingFeedbackIQReportSystem.Preset.Reputation);
        }
        
        public void SendChat(String Message, BasePlayer player, Chat.ChatChannel channel = Chat.ChatChannel.Global)
        {
            Configuration.IQChatSetting Chat = config.IQChatSettings;
            if (IQChat)
                 IQChat?.Call("API_ALERT_PLAYER", player, Message, Chat.CustomPrefix, Chat.CustomAvatar);
            else player.SendConsoleCommand("chat.add", channel, 0, Message);
        }

        
        
        void OnPlayerMuted(BasePlayer Target, BasePlayer Staff, Int32 MuteTime, String Reason)
        {
            if (!DataPlayer.ContainsKey(Staff.userID)) return;
            String UniqKey = DataPlayer[Staff.userID].UniqKeyStaff;
            if (!config.PresetsStaff.ContainsKey(UniqKey)) return;

            Configuration.StaffPresets Preset = config.PresetsStaff[UniqKey];

            if (Preset.PositiveSetting.SettingMuteIQChat.UseFunction)
                AddReputation(Staff, Preset.PositiveSetting.SettingMuteIQChat.Reputation);
        }
        void API_RemoveStaff(UInt64 userID) => RemoveStaff(userID, TypeRemoveStaff.API);
        
        [ConsoleCommand("staff")] 
        void IQStaffCommandsAdmin(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !arg.Player().IsAdmin)
                return;

            if (!arg.HasArgs(3))
                return;

            String NamaOrID = arg.GetString(1);
            if(String.IsNullOrWhiteSpace(NamaOrID))
            {
                PrintWarning(LanguageEn ? "Enter nickname or SteamID" : "Введите ник или SteamID");
                return;
            }

            IPlayer Staff = covalence.Players.FindPlayer(NamaOrID);
            if(Staff == null)
            {
                PrintWarning(LanguageEn ? "There is no such player" : $"Такого игрока не существует");
                return;
            }

            String UniqKey = arg.GetString(2);
            if(String.IsNullOrWhiteSpace(UniqKey))
            {
                PrintWarning(LanguageEn ? "Enter the unique key from the configuration" : "Введите уникальный ключ из конфигурации");
                return;
            }

            if (!config.PresetsStaff.ContainsKey(UniqKey))
            {
                PrintWarning(LanguageEn ? "Such a unique key does not exist in the configuration" : "Такого уникального ключа не существует в конфигурации");
                return;
            }

            UInt64 userID = UInt64.Parse(Staff.Id);
            
            switch(arg.GetString(0))
            {
                case "add":
                {
                    if (DataPlayer.ContainsKey(userID))
                    {
                        PrintWarning(LanguageEn ? "This player is already your staff" : "Данный игрок уже является вашим персоналом");
                        return;
                    }
                    
                    AddStaff(userID, UniqKey);
                    break;
                }
                
                case "remove":
                {
                    if (!DataPlayer.ContainsKey(userID))
                    {
                        PrintWarning(LanguageEn ? "This player is not your staff" : "Данный игрок не является вашим персоналом");
                        return;
                    }
                    
                    RemoveStaff(userID);
                    break;
                }
            }
        }

        
                
        
        private Object UseCommandBlockOrAlert(BasePlayer Staff, String Command, InformationPlayer DataStaff, String FullCommand = "")
        {
            if (config.PresetsStaff[DataStaff.UniqKeyStaff].CommandControllerSetting.ListCommands.Count == 0)
                return null;

            String LogDetails = String.Empty;
            foreach (Configuration.StaffPresets.CommandController.Controller listCommand in config.PresetsStaff[DataStaff.UniqKeyStaff].CommandControllerSetting.ListCommands)
            {
                if (listCommand.Command.Equals(Command))
                {
                    if (listCommand.BlockedUse)
                    {
                        LogDetails = LanguageEn ? $"{Staff.displayName} ({Staff.userID}) tried to use a forbidden command: {FullCommand ?? Command}" : $"{Staff.displayName} ({Staff.userID}) пытался использовать запрещенную команду : {FullCommand ?? Command}";

                        DiscordSendLog(Staff.displayName, Staff.UserIDString, LogDetails, config.PresetsStaff[DataStaff.UniqKeyStaff].CommandControllerSetting.DiscordAlert);
                        
                        Log(LogDetails);
                        return false;
                    }
                    else
                    { 
                        LogDetails = LanguageEn ? $"{Staff.displayName} ({Staff.userID}) use command: {FullCommand ?? Command}" : $"{Staff.displayName} ({Staff.userID}) использовал : {FullCommand ?? Command}";
                        
                        DiscordSendLog(Staff.displayName, Staff.UserIDString, LogDetails, config.PresetsStaff[DataStaff.UniqKeyStaff].CommandControllerSetting.DiscordAlert);
                        Log(LogDetails);
                        return null;
                    }
                }
            }

            return null;
        }

        private void ControllingStaff(BasePlayer Staff, String UniqKey)
        {
            if (!DataPlayer.ContainsKey(Staff.userID)) return;
            if (!DataPlayer[Staff.userID].UniqKeyStaff.Equals(UniqKey)) return;
            if (!config.PresetsStaff.ContainsKey(UniqKey)) return;

            Configuration.StaffPresets.PresetControlled ControllerPreset = config.PresetsStaff[UniqKey].ControllerPersonal;

            if (!ControllerPreset.UseFunction) return;

            if (DataPlayer[Staff.userID].Reputation <= ControllerPreset.Reputation)
                RemoveStaff(Staff.userID, TypeRemoveStaff.StaffController);
        }
        
        private void GameStoresBalanceSet(UInt64 userID, Double Balance, Int32 ReputationTake = 0)
        {
            Configuration.ReputationTransferToStore.GameStoresConfig GameStores = config.ReputationTransferToStoreSettings.GSConfig;
            
            if (String.IsNullOrEmpty(GameStores.SecretKey) || String.IsNullOrEmpty(GameStores.ShopID))
            {
				if (Config.Exists(Interface.Oxide.ConfigDirectory + "/GameStoresRUST.json"))
				{
					try
					{
						GameStoresConfiguration gameStoresConfiguration = Config.ReadObject<GameStoresConfiguration>(Interface.Oxide.ConfigDirectory + "/GameStoresRUST.json");
		   		 		  						  	   		  	 				   					  		 			  	   
						GameStores.SecretKey = gameStoresConfiguration.APISettings.SecretKey;
						GameStores.ShopID = gameStoresConfiguration.APISettings.ShopID;

						SaveConfig();
					}
					catch
					{
						PrintError(LanguageEn ? "Error reading GameStoresRUST config!" : "Ошибка чтения конфигурации GameStores");
					}
				}

				if (String.IsNullOrEmpty(GameStores.SecretKey) || String.IsNullOrEmpty(GameStores.ShopID))
				{
					PrintWarning(LanguageEn ? "Game Stores not set up!" : "Магазин GameStores не настроен!");
					return;
				}
            }
            BasePlayer Staff = BasePlayer.FindByID(userID);
            if (Staff == null) return;
            RemoveReputation(Staff, ReputationTake, true);

            webrequest.Enqueue($"https://gamestores.app/api?shop_id={GameStores.ShopID}&secret={GameStores.SecretKey}&action=moneys&type=plus&steam_id={userID}&amount={Balance}&mess={GetLang("STAFF_SUCSESS_TRANSFER_TO_STORE_MESSAGE", Staff.UserIDString)}", null, (i, s) =>
            {
                if (i != 200) { }

                if (s.Contains("success"))
                {
                    if (Staff == null) return;
		   		 		  						  	   		  	 				   					  		 			  	   
                    String LogDetails = LanguageEn
                        ? $"User {Staff.displayName} ({userID}) successfully exchanged reputation : {ReputationTake} for {Balance} balance"
                        : $"Пользователь {Staff.displayName} ({userID}) успешно обменял репутацию : {ReputationTake} на {Balance} баланса";
                    
                    SendChat(GetLang("STAFF_SUCSESS_TRANSFER_TO_STORE", Staff.UserIDString, ReputationTake, Balance), Staff);
                    Puts(LogDetails);

                    DiscordSendLog(Staff.displayName, Staff.UserIDString, LogDetails, config.DiscordSettings);
                    Log(LogDetails);

                    return;
                }

                if (s.Contains("fail"))
                {
                    if (Staff != null)
                    {
                        SendChat(GetLang("STAFF_SUCSESS_TRANSFER_TO_STORE_ERROR", Staff.UserIDString), Staff);
                        AddReputation(Staff, ReputationTake);
                    }
                }
            }, this);
        }
     
        private object OnClientCommand(Connection connection, String command)
        {
            BasePlayer Staff = BasePlayer.FindByID(connection.userid);
            
            if (Staff == null) return null;
            if (!DataPlayer.TryGetValue(Staff.userID, out InformationPlayer DataStaff)) return null;
            if (!config.PresetsStaff.ContainsKey(DataStaff.UniqKeyStaff)) return null;
            
            String OnlyCommand = !String.IsNullOrWhiteSpace(command) && command.Contains(" ") ? command.Substring(0, command.IndexOf(" ", StringComparison.Ordinal)) : command;

            UseCommandLimit(Staff, OnlyCommand, DataStaff);
            
            return UseCommandStaffList(Staff, OnlyCommand, DataStaff, command) != null ? false : UseCommandBlockOrAlert(Staff, OnlyCommand, DataStaff, command);
        }
        /// <summary>
        /// - Добавлена обработка хука - OnClientCommand
        /// </summary>
        private const Boolean LanguageEn = false;

        private void ValidatePermissions(KeyValuePair<UInt64, InformationPlayer> informationPlayer, KeyValuePair<String, Configuration.StaffPresets> presetStaff, String userIDString, List<UInt64> userIDValidated)
        {
            foreach (String permissionPreset in presetStaff.Value.PermissionsList)
            {
                if (!informationPlayer.Value.PermissionList.Contains(permissionPreset))
                {
                    informationPlayer.Value.PermissionList.Add(permissionPreset);
                    if (!permission.UserHasPermission(userIDString, permissionPreset))
                        permission.GrantUserPermission(userIDString, permissionPreset, null);

                    if (!userIDValidated.Contains(informationPlayer.Key))
                        userIDValidated.Add(informationPlayer.Key);
                }
            }


            List<String> permissionList = Facepunch.Pool.GetList<String>();
            permissionList = new List<String>(informationPlayer.Value.PermissionList);
            foreach (String permsStaff in permissionList)
            {
                if (!presetStaff.Value.PermissionsList.Contains(permsStaff))
                {
                    informationPlayer.Value.PermissionList.Remove(permsStaff);
                    if (permission.UserHasPermission(userIDString, permsStaff))
                        permission.RevokeUserPermission(userIDString, permsStaff);

                    if (!userIDValidated.Contains(informationPlayer.Key))
                        userIDValidated.Add(informationPlayer.Key);
                }
            }
            
            Facepunch.Pool.FreeList(ref permissionList);
        }
        
	    private Dictionary<UInt64, InformationPlayer> DataPlayer = new Dictionary<UInt64, InformationPlayer>();

            }
}
