using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;
namespace Oxide.Plugins
{
    [Info("Referral System", "TopPlugin.ru", "1.0.3")]
    class ReferralSystem : CovalencePlugin
    {
        public Dictionary<string, PlayersReferrals> PlayersReferral = new Dictionary<string, PlayersReferrals>();
        public class PlayersReferrals
        {
            public double Registration;
            public List<string> Players = new List<string>();
        }
        private int rewardRefer = 20;
        private int rewardReferral;
        private string shopId = "";
        private string secretKey = "";
        private bool LogsPlayer;
        private int Broadcast = 3600;
        private int timeToEnd = 15;
        private double Cooldown = 60f;
        private double TimeToRegistration = 3600.0;
        private void LoadConfigValues()
        {
            bool changed = false;
            if (GetConfig("Основные настройки", "Сумма в рублях игроку пригласившего друга", ref rewardRefer))
            {
                PrintWarning("Благодарим за приобритение плагина на сайте RustPlugin.ru. Если вы приобрели этот плагин на другом ресурсе знайте - это лишает вас гарантированных обновлений!");
                changed = true;
            }
            if (GetConfig("GameStores", "ShopID в GameStores (Оставте поле пустым что бы использовать магазин Moscow.ovh)", ref shopId))
            {
                changed = true;
            }
            if (GetConfig("GameStores", "Secret Key в GameStores (Оставте поле пустым что бы использовать магазин Moscow.ovh)", ref secretKey))
            {
                changed = true;
            }
            if (GetConfig("Основные настройки", "Включить логирование пополнений счета", ref LogsPlayer))
            {
                changed = true;
            }
            if (GetConfig("Основные настройки", "Время ответа потверждения для пригласившего (в секундах)", ref timeToEnd))
            {
                changed = true;
            }
            if (GetConfig("Основные настройки", "Частота оповещений игроков в чат о реферальной системе", ref Broadcast))
            {
                changed = true;
            }
            if (GetConfig("Основные настройки", "Задержка использования команды /referral (От последнего добавления игроков в реферралы, в сек.)", ref Cooldown))
            {
                changed = true;
            }
            if (GetConfig("Основные настройки", "Время проведенное на сервере игроком что бы быть приглашенным или приглашать игроков", ref TimeToRegistration))
            {
                PrintWarning("Конфигурация обновлена, добавлено: Время проведенное на сервере игроком что бы быть приглашенным или приглашать игроков");
                changed = true;
            }
            if (changed) SaveConfig();
        }
        private bool GetConfig<T>(string MainMenu, string Key, ref T var)
        {
            if (Config[MainMenu, Key] != null)
            {
                var = (T)Convert.ChangeType(Config[MainMenu, Key], typeof(T));
                return false;
            }
            Config[MainMenu, Key] = var;
            return true;
        }
        void OnPlayerConnected(IPlayer player)
        {
            if (!PlayersReferral.ContainsKey(player.Id))
            {
                PlayersReferral.Add(player.Id, new PlayersReferrals()
                {
                    Registration = GrabCurrentTime() + TimeToRegistration
                }
                );
            }
            if (PlayersReferral.ContainsKey(player.Id))
            {
                if (PlayersReferral[player.Id].Registration == 0.0f)
                {
                    PlayersReferral[player.Id].Registration = GrabCurrentTime() + TimeToRegistration;
                }
            }
        }
        private double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
        void OnServerSave() => Interface.Oxide.DataFileSystem.WriteObject("ReferralSystem_Players", PlayersReferral);
        void Unload()
        {
            OnServerSave();
        }
        void OnServerInitialized()
        {
            LoadData();
            lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
            LoadConfig();
            LoadConfigValues();
            mytimer = timer.Every(Broadcast, () => {
                BroadCast();
            }
            );
            var playerslist = players.All.Where(p => p.IsConnected).ToArray();
            foreach (var player in playerslist)
            {
                OnPlayerConnected(player);
            }
        }
        void LoadData()
        {
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile("ReferralSystem_Players"))
            {
                PlayersReferral = new Dictionary<string, PlayersReferrals>();
                Interface.Oxide.DataFileSystem.WriteObject("ReferralSystem_Players", PlayersReferral);
            }
            else PlayersReferral = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, PlayersReferrals>>("ReferralSystem_Players");
        }
        public Timer mytimer;
        public Timer mytimer1;
        private Dictionary<IPlayer, DateTime> Cooldowns = new Dictionary<IPlayer, DateTime>();
        [Command("referral")]
        void CmdReferral(IPlayer player, string command, string[] args)
        {
            if (args.Length <= 0 || args == null && args[0].ToLower() != "yes" && args[0] != "add")
            {
                Reply(player, "R.SYNTAX");
                return;
            }
            PlayersReferrals Players;
            if (!PlayersReferral.TryGetValue(player.Id, out Players))
            {
                Players = new PlayersReferrals();
                PlayersReferral.Add(player.Id, new PlayersReferrals());
            }
            if (Cooldowns.ContainsKey(player))
            {
                double seconds = Cooldowns[player].Subtract(DateTime.Now).TotalSeconds;
                if (seconds >= 0)
                {
                    Reply(player, "R.Cooldown", seconds);
                    return;
                }
            }
            switch (args[0].ToLower())
            {
                case "add":
                    if (args.Length <= 1)
                    {
                        Reply(player, "R.SYNTAX");
                        return;
                    }
                    var refer = players.FindPlayers(args[1]).Where(p => p.IsConnected).ToArray();
                    if (refer.Length > 1)
                    {
                        Reply(player, "R.MultiplePlayers", string.Join(", ", refer.Select(p => p.Name).ToArray()));
                        return;
                    }
                    var ReferPlayer = refer.Length == 1 ? refer[0] : null;
                    if (ReferPlayer == null)
                    {
                        Reply(player, "R.NOPLAYER");
                        return;
                    }
                    if (PlayersReferral[player.Id].Players.Contains(ReferPlayer.Id))
                    {
                        Reply(player, "R.ALREADYLIST");
                        return;
                    }
                    if (PlayersReferral[ReferPlayer.Id].Players.Contains(player.Id))
                    {
                        Reply(player, "ALREDY.LISTEDTOPLAYER", ReferPlayer.Name);
                        return;
                    }
                    var entotimer = PlayersReferral[player.Id].Registration;
                    double timer1 = GrabCurrentTime();
                    if (entotimer > timer1)
                    {
                        Reply(player, "R.GrabTimeRefer");
                        return;
                    }
                    var endtimer = PlayersReferral[ReferPlayer.Id].Registration;
                    double time = GrabCurrentTime();
                    if (endtimer > time)
                    {
                        Reply(player, "R.GrabTime", ReferPlayer.Name);
                        return;
                    }
                    foreach (var pla in PlayersReferral)
                    {
                        foreach (var pl in pla.Value.Players)
                        {
                            if (pl == ReferPlayer.Id)
                            {
                                Reply(player, "ALREDY.LISTED", ReferPlayer.Name);
                                return;
                            }
                        }
                    }
                    if (ReferPlayer.Id == player.Id)
                    {
                        Reply(player, "R.SELF");
                        return;
                    }
                    if (pendings.ContainsKey(Convert.ToUInt64(player.Id)) || pendings.ContainsKey(Convert.ToUInt64(ReferPlayer.Id)))
                    {
                        Reply(player, "R.PEDDING", ReferPlayer.Name);
                        return;
                    }
                    Reply(player, "R.SENDER", ReferPlayer.Name);
                    PendingAdd(player, ReferPlayer);
                    Reply(ReferPlayer, "R.PEDING", player.Name);
                    mytimer1 = timer.Once(timeToEnd, () => {
                        ulong sender;
                        if (pendings.TryGetValue(Convert.ToUInt64(ReferPlayer.Id), out sender) && sender == Convert.ToUInt64(player.Id))
                        {
                            pendings.Remove(Convert.ToUInt64(ReferPlayer.Id));
                            Reply(player, "R.Timeleft", ReferPlayer.Name);
                        }
                    }
                    );
                    break;
                case "yes":
                    ulong sensder;
                    if (pendings.TryGetValue(Convert.ToUInt64(player.Id), out sensder))
                    {
                        if (mytimer1 != null) mytimer1.Destroy();
                        PlayersReferral[sensder.ToString()].Players.Add(player.Id);
                        var p = players.FindPlayerById(sensder.ToString());
                        var reply = 1;
                        if (reply == 0) { }
                        Reply(player, "R.SUCCESS", p.Name);
                        Reply(p, "R.ACCEPTED", player.Name);
                        Cooldowns[player] = DateTime.Now.AddSeconds(Cooldown);
                        Cooldowns[p] = DateTime.Now.AddSeconds(Cooldown);
                        pendings.Remove(Convert.ToUInt64(player.Id));
                        if (string.IsNullOrEmpty(shopId))
                        {
                            APIChangeUserBalance(Convert.ToUInt64(p.Id), rewardRefer, null);
                        }
                        else
                        {
                            MoneyPlus(Convert.ToUInt64(p.Id), rewardRefer);
                        }
                    }
                    else
                    {
                        Reply(player, "R.PendingNotFound");
                    }
                    break;
            }
        }
        private void Reply(IPlayer player, string langKey, params object[] args)
        {
            player.Reply(string.Format(Messages[langKey], args));
        }
        Dictionary<string, string> Messages = new Dictionary<string, string>() {
                {
                "R.SYNTAX", "<color=#DC143C>[Ошибка]</color> Чтобы указать кого-то рефералом введите:\n<color=cyan>/referral add steamID/Name</color>\nЧто бы принять запрос, введите <color=cyan>/referral yes</color>"
            }
            , {
                "R.SUCCESS", "<color=green>[Успех]</color> Вы успешно указали своего рефера как <color=cyan>{0}</color>."
            }
            , {
                "ALREDY.LISTED", "<color=#DC143C>[Ошибка]</color> Игрока {0} уже указали как пригласившего."
            }
            , {
                "ALREDY.LISTEDTOPLAYER", "<color=#DC143C>[Ошибка]</color> Игрок {0} пригласил Вас на сервер, по другому никак."
            }
            , {
                "R.ALREADYLIST", "<color=#DC143C>[Ошибка]</color> Вы уже указали данного человека как пригласившего."
            }
            , {
                "R.NOPLAYER", "<color=#DC143C>[Ошибка]</color> Не удается найти данного игрока."
            }
            , {
                "R.SELF", "<color=#DC143C>[Ошибка]</color> Вы не можете пригласить сами себя."
            }
            , {
                "R.ACCEPTED", " Игрок <color=#DC143C>{0}</color> подтвердил что вы пригласили его на сервер."
            }
            , {
                "R.SENDER", "Запрос игроку {0} успешно отправлен, ожидайте пока он приймет его."
            }
            , {
                "R.PEDDING", "У Вас или у {0} уже есть активный запрос, попробуйте позже!"
            }
            , {
                "R.PendingNotFound", "У Вас нет запросов в реферралы"
            }
            , {
                "R.Broadcast", "Приглашайте своих друзей на сервер, и получайте пополнения счета. Что бы указать друга, введите /referral add Имя/Стим\nБольше всего друзей пригласили:\n{0}"
            }
            , {
                "R.Timeleft", "Игрок {0} не ответил на Ваш запрос."
            }
            , {
                "R.PEDING", "Игрок {0} указал Вас как пригласившего им игрока, что бы потвердить, введите <color=cyan>/referral yes</color>"
            }
            , {
                "R.MultiplePlayers", "Было найдено несколько игроков, пожалуйста, уточните: {0}"
            }
            , {
                "R.Cooldown", "Нельзя так часто использовать команду /referral, подождите: {0:00} сек."
            }
            , {
                "R.GrabTime", "Игрок {0} провел слишком мало времени на сервере что бы указать его как пригласившего"
            }
            , {
                "R.GrabTimeRefer", "Вы провели слишком мало времени на сервере что бы указывать пригласивших вами игроков"
            }
        }
        ;
        void BroadCast()
        {
            string Raites = "";
            IOrderedEnumerable<KeyValuePair<string, PlayersReferrals>> Top = from pair in PlayersReferral orderby pair.Value.Players.Count descending select pair;
            int i = 1;
            foreach (KeyValuePair<string, PlayersReferrals> pair in Top)
            {
                var p = players.FindPlayerById(pair.Key.ToString());
                string name = p.Name;
                if (name == null) name = "Неизвестный";
                int value = pair.Value.Players.Count;
                if (value == 0) break;
                Raites = Raites + i.ToString() + ". " + name + " - " + value + "\n";
                i++;
                if (i > 5) break;
            }
            var playerslist = players.All.Where(p => p.IsConnected).ToArray();
            foreach (var player in playerslist)
            {
                Reply(player, "R.Broadcast", Raites);
            }
        }
        void MoneyPlus(ulong userId, int amount)
        {
            ExecuteApiRequest(new Dictionary<string, string>() {
                    {
                    "action", "moneys"
                }
                , {
                    "type", "plus"
                }
                , {
                    "steam_id", userId.ToString()
                }
                , {
                    "amount", amount.ToString()
                }
            }
            );
            if (LogsPlayer)
            {
                LogToFile("logs", $"({DateTime.Now.ToShortTimeString()}): Отправлен запрос пользователем {userId} на пополнение баланса в размере: {amount}", this);
            }
        }
        void ExecuteApiRequest(Dictionary<string, string> args)
        {
            string url = $"https://gamestores.ru/api/?shop_id={shopId}&secret={secretKey}" + $"{string.Join("", args.Select(arg => $"& {

                arg.Key

            }
			= {
				arg.Value
    }
			").ToArray())}";
			webrequest.EnqueueGet(url, (i, s)=> {
				if (i !=200) {
					PrintError($"[ERROR]{url}\nCODE {i}: {s}");
}
			}
			, this);
}
void APIChangeUserBalance(ulong steam, int balanceChange, Action<string> callback)
{
    plugins.Find("RustStore").CallHook("APIChangeUserBalance", steam, balanceChange, new Action<string>((result) => {
        if (result == "SUCCESS")
        {
            if (LogsPlayer)
            {
                LogToFile("logs", $"({DateTime.Now.ToShortTimeString()}): Отправлен запрос пользователем {steam} на пополнение баланса в размере: {balanceChange}", this);
            }
            return;
        }
        if (LogsPlayer)
        {
            LogToFile("logs", $"({DateTime.Now.ToShortTimeString()}): Баланс не был изменен, ошибка: {result}", this);
        }
    }
    ));
}
private Dictionary<ulong, ulong> pendings = new Dictionary<ulong, ulong>();
private void PendingAdd(IPlayer player, IPlayer target)
{
    pendings[Convert.ToUInt64(target.Id)] = Convert.ToUInt64(player.Id);
}
	}
}                                                       