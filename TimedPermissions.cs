using System;
using Oxide.Core;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Oxide.Core.Libraries.Covalence;


namespace Oxide.Plugins
{
    [Info("Timed Permissions", "LaserHydra", "1.3.2", ResourceId = 1926)]
    [Description("Позволяет выдавать разрешения или группы на N количество времени")]
    internal class TimedPermissions : CovalencePlugin
    {
		#region Variables
		private bool ConnectShow = false;
        private static TimedPermissions _instance;
        private static List<Player> _players = new List<Player>();
		
        class StoredData
        {
            public Dictionary<string, string> GroupsNames = new Dictionary<string, string>();
            public StoredData() { }
        }
        StoredData storedData;
		#endregion

		#region OxideCore
        private class Player
        {
            public readonly List<TimedAccessValue> Permissions = new List<TimedAccessValue>();
            public readonly List<TimedAccessValue> Groups = new List<TimedAccessValue>();
            public string Name = "unknown";
            public string Id = "0";

            internal static Player Get(string steamId) => _players.Find(p => p.Id == steamId);

            internal static Player GetOrCreate(IPlayer player)
            {
                Player pl = Get(player.Id);

                if (pl == null)
                {
                    pl = new Player(player);

                    _players.Add(pl);
                    SaveData(_players);
                }

                return pl;
            }

            public TimedAccessValue GetTimedPermission(string permission) => Permissions.Find(p => p.Value == permission);

            public TimedAccessValue GetTimedGroup(string group) => Groups.Find(g => g.Value == group);

            public void AddPermission(string permission, DateTime expireDate)
            {
                TimedAccessValue existingPermission = GetTimedPermission(permission);
                var reply = 1179;
                if (reply == 0) { }
                if (existingPermission != null)
                {
                    existingPermission.ExpireDate += expireDate - DateTime.UtcNow;

                    _instance.Puts($"----> {Name} ({Id}) - Permission Extended: {permission} to {existingPermission.ExpireDate - DateTime.UtcNow}" + Environment.NewLine);
                }
                else
                {
                    Permissions.Add(new TimedAccessValue(permission, expireDate));
                    _instance.permission.GrantUserPermission(Id, permission, null);

                    _instance.Puts($"----> {Name} ({Id}) - Permission Granted: {permission} for {expireDate - DateTime.UtcNow}" + Environment.NewLine);
                }

                SaveData(_players);
            }

            internal void AddGroup(string group, DateTime expireDate)
            {
                TimedAccessValue existingGroup = GetTimedGroup(group);

                if (existingGroup != null)
                {
                    existingGroup.ExpireDate += expireDate - DateTime.UtcNow;

                    _instance.Puts($"----> {Name} ({Id}) - Group Time Extended: {group} to {existingGroup.ExpireDate - DateTime.UtcNow}" + Environment.NewLine);
                }
                else
                {
                    Groups.Add(new TimedAccessValue(group, expireDate));
                    _instance.permission.AddUserGroup(Id, group);

                    _instance.Puts($"----> {Name} ({Id}) - Added to Group: {group} for {expireDate - DateTime.UtcNow}" + Environment.NewLine);
                }

                SaveData(_players);
            }

            internal void RemovePermission(string permission)
            {
                Permissions.Remove(GetTimedPermission(permission));
                _instance.permission.RevokeUserPermission(Id, permission);

                _instance.Puts($"----> {Name} ({Id}) - Permission Expired: {permission}" + Environment.NewLine);

                if (Groups.Count == 0 && Permissions.Count == 0)
                    _players.Remove(this);

                SaveData(_players);
            }

            internal void RemoveGroup(string group)
            {
                Groups.Remove(GetTimedGroup(group));
                _instance.permission.RemoveUserGroup(Id, group);

                _instance.Puts($"----> {Name} ({Id}) - Group Expired: {group}" + Environment.NewLine);

                if (Groups.Count == 0 && Permissions.Count == 0)
                    _players.Remove(this);

                SaveData(_players);
            }

            internal void UpdatePlayer(IPlayer player) => Name = player.Name;

            private void Update()
            {
                foreach (TimedAccessValue perm in Permissions.ToList())
                    if (perm.Expired)
                        RemovePermission(perm.Value);

                foreach (TimedAccessValue group in Groups.ToList())
                    if (group.Expired)
                        RemoveGroup(group.Value);
            }

            public override int GetHashCode() => Id.GetHashCode();

            private Player(IPlayer player)
            {
                Id = player.Id;
                Name = player.Name;

                _instance.timer.Repeat(60, 0, Update);
            }

            public Player()
            {
                _instance.timer.Repeat(60, 0, Update);
            }
        }

        private class TimedAccessValue
        {
            public string Value = string.Empty;
            public DateTime ExpireDate;

            internal bool Expired => DateTime.Compare(DateTime.UtcNow, ExpireDate) > 0;

            public override int GetHashCode() => Value.GetHashCode();

            internal TimedAccessValue(string value, DateTime expireDate)
            {
                Value = value;
                ExpireDate = expireDate;
            }

            public TimedAccessValue()
            {
            }
        }

        private void MigrateData()
        {
            List<JObject> data = new List<JObject>();
            LoadData(ref data);

            foreach (JObject playerData in data)
            {
                if (playerData["permissions"] != null)
                {
                    JArray permissions = (JArray)playerData["permissions"];

                    foreach (JObject obj in permissions)
                    {
                        if (obj["permission"] != null)
                        {
                            obj["Value"] = obj["permission"];
                            obj.Remove("permission");
                        }

                        if (obj["_expireDate"] != null)
                        {
                            string expireDate = obj["_expireDate"].Value<string>();

                            int[] date = (from val in expireDate.Split('/') select Convert.ToInt32(val)).ToArray();
                            obj["ExpireDate"] = new DateTime(date[4], date[3], date[2], date[1], date[0], 0);

                            obj.Remove("_expireDate");
                        }
                    }

                    playerData["Permissions"] = permissions;
                    playerData.Remove("permissions");
                }

                if (playerData["groups"] != null)
                {
                    JArray permissions = (JArray)playerData["groups"];

                    foreach (JObject obj in permissions)
                    {
                        if (obj["group"] != null)
                        {
                            obj["Value"] = obj["group"];
                            obj.Remove("group");
                        }

                        if (obj["_expireDate"] != null)
                        {
                            string expireDate = obj["_expireDate"].Value<string>();

                            int[] date = (from val in expireDate.Split('/') select Convert.ToInt32(val)).ToArray();
                            obj["ExpireDate"] = new DateTime(date[4], date[3], date[2], date[1], date[0], 0);

                            obj.Remove("_expireDate");
                        }
                    }

                    playerData["Groups"] = permissions;
                    playerData.Remove("groups");
                }

                if (playerData["steamID"] != null)
                {
                    playerData["Id"] = playerData["steamID"];
                    playerData.Remove("steamID");
                }

                if (playerData["name"] != null)
                {
                    playerData["Name"] = playerData["name"];
                    playerData.Remove("name");
                }
            }

            SaveData(data);
        }
		
		private bool TryGetDateTime(string source, out DateTime date)
        {
            int minutes = 0;
            int hours = 0;
            int days = 0;

            Match m = new Regex(@"(\d+?)m", RegexOptions.IgnoreCase).Match(source);
            Match h = new Regex(@"(\d+?)h", RegexOptions.IgnoreCase).Match(source);
            Match d = new Regex(@"(\d+?)d", RegexOptions.IgnoreCase).Match(source);

            if (m.Success)
                minutes = Convert.ToInt32(m.Groups[1].ToString());

            if (h.Success)
                hours = Convert.ToInt32(h.Groups[1].ToString());

            if (d.Success)
                days = Convert.ToInt32(d.Groups[1].ToString());

            source = source.Replace(minutes.ToString() + "m", string.Empty);
            source = source.Replace(hours.ToString() + "h", string.Empty);
            source = source.Replace(days.ToString() + "d", string.Empty);

            if (!string.IsNullOrEmpty(source) || (!m.Success && !h.Success && !d.Success))
            {
                date = default(DateTime);
                return false;
            }

            date = DateTime.UtcNow + new TimeSpan(days, hours, minutes, 0);
            return true;
        }

        private IPlayer GetPlayer(string nameOrID, IPlayer player)
        {
            if (IsParseableTo<ulong>(nameOrID) && nameOrID.StartsWith("7656119") && nameOrID.Length == 17)
            {
                IPlayer result = players.All.ToList().Find(p => p.Id == nameOrID);

                if (result == null)
					player.Reply(GetMessage("PlayerNotFoundSteam", player.Id));
                    //player.Reply($"Не удалось найти игрока с таким SteamID: '{nameOrID}'");

                return result;
            }

            List<IPlayer> foundPlayers = new List<IPlayer>();

            foreach (IPlayer current in players.Connected)
            {
                if (current.Name.ToLower() == nameOrID.ToLower())
                    return current;

                if (current.Name.ToLower().Contains(nameOrID.ToLower()))
                    foundPlayers.Add(current);
            }

            switch (foundPlayers.Count)
            {
                case 0:
				    player.Reply(GetMessage("PlayerNotFoundName", player.Id));
                    //player.Reply($"Не удалось найти игрока с таким именем: '{nameOrID}'");
                    break;

                case 1:
                    return foundPlayers[0];

                default:
                    string[] names = (from current in foundPlayers select current.Name).ToArray();
					player.Reply(GetMessage("MultiplePlayersFound" + string.Join(", ", names), player.Id));
                    //player.Reply("<color=#F318FF>НАЙДЕНО НЕСКОЛЬКО ИГРОКОВ:</color> \n" + string.Join(", ", names));
                    break;
            }

            return null;
        }

        private bool IsParseableTo<T>(object s)
        {
            try
            {
                var parsed = (T)Convert.ChangeType(s, typeof(T));
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool TryParse<S, R>(S s, out R c)
        {
            try
            {
                c = (R)Convert.ChangeType(s, typeof(R));
                return true;
            }
            catch
            {
                c = default(R);
                return false;
            }
        }
		#endregion

		#region ChatCommand
        [Command("revokeperm"), Permission("timedpermissions.use")]
        private void CmdRevokePerm(IPlayer player, string cmd, string[] args)
        {
            if (args.Length != 2)
            {
                player.Reply($"ДОСТУПНЫЕ КОМАНДЫ:\n {(player.LastCommand == CommandType.Console ? string.Empty : "/")}revokeperm 'Player Name || SteamID' 'Названия привилегии' - Забрать привилегию у игрока.");
                return;
            }

            IPlayer target = GetPlayer(args[0], player);

            if (target == null)
                return;

            Player pl = Player.Get(target.Id);

            if (pl == null || !pl.Permissions.Any(p => p.Value == args[1].ToLower()))
            {
                player.Reply(GetMessage("UserDoesn'tHavePermission", player.Id).Replace("{target}", target.Name).Replace("{permission}", args[1].ToLower()));
                return;
            }

            pl.RemovePermission(args[1].ToLower());
        }

        [Command("grantperm"), Permission("timedpermissions.use")]
        private void CmdGrantPerm(IPlayer player, string cmd, string[] args)
        {
            if (args.Length != 3)
            {
                player.Reply($"ДОСТУПНЫЕ КОМАНДЫ:\n {(player.LastCommand == CommandType.Console ? string.Empty : "/")}grantperm 'Player Name || SteamID' 'Названия привилегии' 'Формат времени: 1d12h30m' - Выдать привилегию игроку.");
                return;
            }

            IPlayer target = GetPlayer(args[0], player);
            DateTime expireDate;

            if (target == null)
                return;

            if (!TryGetDateTime(args[2], out expireDate))
            {
                player.Reply(GetMessage("InvalidTimeFormat", player.Id));
                return;
            }

            Player.GetOrCreate(target).AddPermission(args[1].ToLower(), expireDate);
        }

        [Command("removegroup"), Permission("timedpermissions.use")]
        private void CmdRemoveGroup(IPlayer player, string cmd, string[] args)
        {
            if (args.Length != 2)
            {
                player.Reply($"ДОСТУПНЫЕ КОМАНДЫ:\n {(player.LastCommand == CommandType.Console ? string.Empty : "/")}removegroup 'Player Name || SteamID' 'Названия группы' - Забрать группу у игрока.");
                return;
            }

            IPlayer target = GetPlayer(args[0], player);

            if (target == null)
                return;

            Player pl = Player.Get(target.Id);

            if (pl == null || !pl.Groups.Any(p => p.Value == args[1].ToLower()))
            {
                player.Reply(GetMessage("UserIsn'tInGroup", player.Id).Replace("{target}", target.Name).Replace("{group}", args[1].ToLower()));
                return;
            }

            pl.RemoveGroup(args[1].ToLower());
        }

        [Command("addgroup"), Permission("timedpermissions.use")]
        private void CmdAddGroup(IPlayer player, string cmd, string[] args)
        {
            if (args.Length != 3)
            {
                player.Reply($"ДОСТУПНЫЕ КОМАНДЫ:\n {(player.LastCommand == CommandType.Console ? string.Empty : "/")}addgroup 'Player Name || SteamID' 'Названия группы' 'Формат времени: 1d12h30m' - Выдать группу игроку.");
                return;
            }

            IPlayer target = GetPlayer(args[0], player);
            DateTime expireDate;

            if (target == null)
                return;

            if (!TryGetDateTime(args[2], out expireDate))
            {
                player.Reply(GetMessage("InvalidTimeFormat", player.Id));
                return;
            }

            Player.GetOrCreate(target).AddGroup(args[1], expireDate);
        }

        [Command("pinfo"), Permission("timedpermissions.use")]
        private void CmdPlayerInfo(IPlayer player, string cmd, string[] args)
        {
            if (args.Length != 1)
            {
                player.Reply($"ДОСТУПНЫЕ КОМАНДЫ:\n {(player.LastCommand == CommandType.Console ? string.Empty : "/")}pinfo 'Player Name || SteamID' - Посмотреть список привилегий и групп у определённого игрока.");
                return;
            }

            IPlayer target = GetPlayer(args[0], player);

            if (target == null)
                return;

            Player pl = Player.Get(target.Id);
			
            if (pl == null)
                //player.Reply(GetMessage("У игрока нет временных услуг!", player.Id));
			    player.Reply(GetMessage("PlayerNotService", player.Id));
            else
            {
				string GetPrivilage = "<size=16>Информация об игроке:</size>\n\n";
				foreach (var g in pl.Permissions)
                {
                    string msg1 = GetMessage("<size=16>Привилегия:</size> <color=#F318FF>{groups}</color>\n<size=16>Оставшееся время:</size> <color=#F318FF>{timeleft}</color>\n", player.Id);
                    TimeSpan result = (g.ExpireDate).Subtract(DateTime.UtcNow);
                    msg1 = msg1.Replace("{groups}", $"{g.Value}");
                    msg1 = msg1.Replace("{timeleft}", $"{FormatTime(TimeSpan.FromDays(result.TotalDays))}");
                    GetPrivilage = GetPrivilage + msg1 + "\n";
                }
				foreach (var g in pl.Groups)
                {
                    string msg1 = GetMessage("<size=16>Группа:</size> <color=#F318FF>{groups}</color>\n<size=16>Оставшееся время:</size> <color=#F318FF>{timeleft}</color>\n", player.Id);
                    TimeSpan result = (g.ExpireDate).Subtract(DateTime.UtcNow);
                    msg1 = msg1.Replace("{groups}", $"{g.Value}");
                    msg1 = msg1.Replace("{timeleft}", $"{FormatTime(TimeSpan.FromDays(result.TotalDays))}");
                    GetPrivilage = GetPrivilage + msg1 + "\n";
                }
                player.Reply(GetPrivilage);
            }
        }
		
		[Command("pr")]
        private void CmdServiceInfo(IPlayer player, string cmd, string[] args)
        {
			if (player.Id == "server_console")
            {
                player.Reply($"Command '{cmd}' can only be used by players", cmd);
                return;
            }
			
            Player pl = Player.Get(player.Id);
            if (pl == null) 
				player.Reply(GetMessage("NotService", player.Id));
				//player.Reply("У вас нет временных услуг!");
            else
            {
                string GetPrivilage = "<size=16>Время оставшихся услуг:</size>\n\n";
                foreach (var g in pl.Permissions)
                {
					string msg1 = GetMessage("PermissionsInfo", player.Id);
                    //string msg1 = GetMessage("<size=16>Привилегия:</size> <color=#F318FF>{groups}</color>\n<size=16>Оставшееся время:</size> <color=#F318FF>{timeleft}</color>\n", player.Id);
                    TimeSpan result = (g.ExpireDate).Subtract(DateTime.UtcNow);
                    msg1 = msg1.Replace("{groups}", $"{g.Value}");
                    msg1 = msg1.Replace("{timeleft}", $"{FormatTime(TimeSpan.FromDays(result.TotalDays))}");
                    GetPrivilage = GetPrivilage + msg1 + "\n";
                }
				
                foreach (var g in pl.Groups)
                {
					string msg1 = GetMessage("GroupsInfo", player.Id);
                    //string msg1 = GetMessage("<size=16>Группа:</size> <color=#F318FF>{groups}</color>\n<size=16>Оставшееся время:</size> <color=#F318FF>{timeleft}</color>\n", player.Id);
                    TimeSpan result = (g.ExpireDate).Subtract(DateTime.UtcNow);
                    msg1 = msg1.Replace("{groups}", $"{g.Value}");
                    msg1 = msg1.Replace("{timeleft}", $"{FormatTime(TimeSpan.FromDays(result.TotalDays))}");
                    GetPrivilage = GetPrivilage + msg1 + "\n";
                }
                player.Reply(GetPrivilage);
            }
        }
		#endregion
		
		#region Localization
		private void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
                {
                    {"NoPermission", "У тебя нету прав на использование этой команды."},
                    {"InvalidTimeFormat", "Неверный формат времени:\nПример: 1d12h30m || d = дни, h = часы, m = минуты."},
                    {"UserDoesn'tHavePermission", "Игрок: {target} у игрока нет такой привилегии: '{permission}'."},
                    {"UserIsn'tInGroup", "Игрок: {target} у игрока нет такой группы: '{group}'."},
					{"MultiplePlayersFound", "<color=#F318FF>НАЙДЕНО НЕСКОЛЬКО ИГРОКОВ:</color> \n"},
					{"PlayerNotFoundName", "Не удалось найти игрока с таким именем: '{nameOrID}'"},
					{"PlayerNotFoundSteam", "Не удалось найти игрока с таким SteamID: '{nameOrID}'"},
					{"NotService", "У вас нет временных услуг!"},
					{"PlayerNotService", "У игрока нет временных услуг!"},
					{"PermissionsInfo", "<size=16>Привилегия:</size> <color=#F318FF>{groups}</color>\n<size=16>Оставшееся время:</size> <color=#F318FF>{timeleft}</color>\n"},
					{"GroupsInfo", "<size=16>Группа:</size> <color=#F318FF>{groups}</color>\n<size=16>Оставшееся время:</size> <color=#F318FF>{timeleft}</color>\n"},
                    {"Days", "Days"},
                    {"Hours", "Hours"},
                    {"Minutes", "Minutes"},
                }, this);
        }
		#endregion
		
		#region Connect Info
		void OnUserConnected(IPlayer player)
        {
			if(ConnectShow)
			{
                ConnectInfo(player);
			}
        }
		
		private void ConnectInfo(IPlayer player)
        {	
			Player pl = Player.Get(player.Id);
            if (pl == null) 
				player.Reply(GetMessage("NotService", player.Id));
				//player.Reply("У вас нет временных услуг!");
            else
            {
                string GetPrivilage = "<size=16>Время оставшихся услуг:</size>\n\n";
                foreach (var g in pl.Permissions)
                {
					string msg1 = GetMessage("PermissionsInfo", player.Id);
                    //string msg1 = GetMessage("<size=16>Привилегия:</size> <color=#F318FF>{groups}</color>\n<size=16>Оставшееся время:</size> <color=#F318FF>{timeleft}</color>\n", player.Id);
                    TimeSpan result = (g.ExpireDate).Subtract(DateTime.UtcNow);
                    msg1 = msg1.Replace("{groups}", $"{g.Value}");
                    msg1 = msg1.Replace("{timeleft}", $"{FormatTime(TimeSpan.FromDays(result.TotalDays))}");
                    GetPrivilage = GetPrivilage + msg1 + "\n";
                }
				
                foreach (var g in pl.Groups)
                {
					string msg1 = GetMessage("GroupsInfo", player.Id);
                    //string msg1 = GetMessage("<size=16>Группа:</size> <color=#F318FF>{groups}</color>\n<size=16>Оставшееся время:</size> <color=#F318FF>{timeleft}</color>\n", player.Id);
                    TimeSpan result = (g.ExpireDate).Subtract(DateTime.UtcNow);
                    msg1 = msg1.Replace("{groups}", $"{g.Value}");
                    msg1 = msg1.Replace("{timeleft}", $"{FormatTime(TimeSpan.FromDays(result.TotalDays))}");
                    GetPrivilage = GetPrivilage + msg1 + "\n";
                }
                player.Reply(GetPrivilage);
            }
        }
		#endregion

		#region Helpers
		public static string GetMessage(string key, string id) => _instance.lang.GetMessage(key, _instance, id);
		#endregion
		
		#region Oxide
        private static void LoadData<T>(ref T data, string filename = null) => data = Core.Interface.Oxide.DataFileSystem.ReadObject<T>(filename ?? _instance.DataFileName);
        private static void SaveData<T>(T data, string filename = null) => Core.Interface.Oxide.DataFileSystem.WriteObject(filename ?? _instance.DataFileName, data);
        private string DataFileName => Title.Replace(" ", "");
		private void Init()
		{
			storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("GroupsNames");
		}
		
		private void Loaded()
        {
            _instance = this;

            LoadMessages();

            MigrateData();

            LoadData(ref _players);

        }
		#endregion
		
		#region Format Time
        public static string FormatTime(TimeSpan time)
        {
            string result = string.Empty;
            if (time.Days != 0)
                result += $"{Format(time.Days, "дней", "дня", "день")} ";

            if (time.Hours != 0)
                result += $"{Format(time.Hours, "часов", "часа", "час")} ";

            if (time.Minutes != 0)
                result += $"{Format(time.Minutes, "минут", "минуты", "минута")} ";


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
    }
}