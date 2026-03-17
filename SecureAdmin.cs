/*
 * TODO:
 * Add "banlist" command support
 * Add "banlistex" command support
 * Add "bans" command support
 * Add "kickall" command support
 * Add "mutechat" command support
 * Add "revoke" command support
 * Add "unmutechat" command support
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Secure Admin", "https://topplugin.ru/", "1.2.0", ResourceId = 1449)]
    [Description("Плагин для бана. Перевод: OxidePlugins")]
    public class SecureAdmin : CovalencePlugin
    {
        #region Initialization

        private const string
            permBan = "secureadmin.ban"; // global.ban, global.banlist, global.banlistex, global.listid, global.bans

        private const string permKick = "secureadmin.kick"; // global.kick, global.kickall
        private const string permSay = "secureadmin.say"; // global.mutechat, global.say, global.unmutechat
        private const string permUnban = "secureadmin.unban"; // global.banlist, global.bans, global.unban

        private bool broadcastBans;
        private bool broadcastKicks;
        private bool commandBan;
        private bool commandKick;
        private bool commandSay;
        private bool commandUnban;
        private bool protectAdmin;

        protected override void LoadDefaultConfig()
        {
            Config["Оповещение о бане (true/false)"] = broadcastBans = GetConfig("Broadcast Bans (true/false)", true);
            Config["Оповещение о кике (true/false)"] = broadcastKicks = GetConfig("Broadcast Kicks (true/false)", true);
            Config["Включить команду Ban (true/false)"] =
                commandBan = GetConfig("Enable Ban Command (true/false)", true);
            Config["Включить команду Kick (true/false)"] =
                commandKick = GetConfig("Enable Kick Command (true/false)", true);
            Config["Включить команду Say (true/false)"] =
                commandSay = GetConfig("Enable Say Command (true/false)", true);
            Config["Включить команду Unban (true/false)"] =
                commandUnban = GetConfig("Enable Unban Command (true/false)", true);
            Config["Защита Админа от бана, кика (true/false)"] =
                protectAdmin = GetConfig("Protect Admin (true/false)", true);

            SaveConfig();
        }

        private void Init()
        {
            LoadDefaultConfig();

            permission.RegisterPermission(permBan, this);
            permission.RegisterPermission(permKick, this);
            permission.RegisterPermission(permSay, this);
            permission.RegisterPermission(permUnban, this);

            if (commandBan) AddCovalenceCommand("ban", "BanCommand");
            if (commandKick) AddCovalenceCommand("kick", "KickCommand");
            if (commandSay) AddCovalenceCommand("say", "SayCommand");
            if (commandUnban) AddCovalenceCommand("unban", "UnbanCommand");
            timer.Every(1f, UpdatePlayerBans);
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "Вам не разрешено использовать команду '{0}' ",
                ["PlayerAlreadyBanned"] = "<color=#FFB841>{0} уже забанен",
                ["PlayerBanned"] =
                    "<color=#FFB841>{0}</color> был забанен. Время бана: {2}. Причина: <color=#FFB841>'{1}'</color>",
                ["PlayerIsAdmin"] =
                    "<color=#FFB841>{0}</color> является администратором, и его нельзя забанить или кикнуть",
                ["PlayerKicked"] = "<color=#FFB841>{0}</color> был кикнут. Причина: <color=#FFB841>'{1}'</color>",
                ["PlayerNotBanned"] = "<color=#FFB841>{0}</color> не забанен",
                ["PlayerNotFound"] = "Не найдено ни одного игрока с таким именем или идентификатором",
                ["PlayerUnbanned"] = "<color=#FFB841>{0}</color> был забанен",
                ["PlayersFound"] = "Было найдено несколько игроков с таким именем, пожалуйста, укажите SteamID: {0}",
                ["ReasonUnknown"] = "Неизвестный",
                ["UsageBan"] = "Введите: <color=#FFB841>{0} <name or id> <причина> </color>",
                ["UsageKick"] = "Введите: <color=#FFB841>{0} <name or id> <причина> </color>",
                ["UsageSay"] = "Введите: <color=#FFB841>{0} <сообщение> </color>",
                ["UsageUnban"] = "Введите: <color=#FFB841>{0} <name or id> </color>",
            }, this);

            // French
            /*lang.RegisterMessages(new Dictionary<string, string>
            {
                // TODO
            }, this, "fr");*/

            // German
            /*lang.RegisterMessages(new Dictionary<string, string>
            {
                // TODO
            }, this, "de");*/

            // Russian
            /*lang.RegisterMessages(new Dictionary<string, string>
            {
                // TODO
            }, this, "ru");*/

            // Spanish
            /*lang.RegisterMessages(new Dictionary<string, string>
            {
                // TODO
            }, this, "es");*/
        }

        #endregion

        #region Ban Command

        private void BanCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permBan))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            if (args.Length < 1)
            {
                player.Reply(Lang("UsageBan", player.Id, command));
                return;
            }

            ulong targetId;
            ulong.TryParse(args[0], out targetId);
            var foundPlayers = players.FindPlayers(args[0]).ToArray();
            if (foundPlayers.Length > 1)
            {
                player.Reply("PlayersFound", player.Id,
                    string.Concat(", ", foundPlayers.Select(p => p.Name).ToArray()));
                return;
            }

            var target = foundPlayers.Length == 1 ? foundPlayers[0] : null;
            if (target != null) ulong.TryParse(target.Id, out targetId);

            if (!targetId.IsSteamId())
            {
                player.Reply(Lang("PlayerNotFound", player.Id, args[0].Sanitize()));
                return;
            }

            if (server.IsBanned(targetId.ToString()))
            {
                player.Reply(Lang("PlayerAlreadyBanned", player.Id, args[0].Sanitize()));
                return;
            }

            if (protectAdmin && target != null && target.IsAdmin)
            {
                player.Reply(Lang("PlayerIsAdmin", player.Id, target.Name.Sanitize()));
                return;
            }

            string reason = null;

            TimeSpan time = GetTime(args[1]);
            PrintWarning($"{time}");
            if (time != new TimeSpan())
            {
                reason = args.Length >= 3
                    ? string.Join(" ", args.Skip(2).ToArray())
                    : Lang("ReasonUnknown", targetId.ToString());
                if (target != null && target.IsConnected)
                {
                    target.Ban(reason, time);
                    AddBanTime(target.Id, time);
                }
                else server.Ban(targetId.ToString(), reason, time);

                var targetName = target != null ? $"{target.Name.Sanitize()} ({target.Id})" : args[0].Sanitize();
                if (broadcastBans) Broadcast("PlayerBanned", targetName, reason, time);
                else player.Reply(Lang("PlayerBanned", player.Id, targetName, reason, time));
            }
            else
            {
                reason = args.Length >= 2
                    ? string.Join(" ", args.Skip(1).ToArray())
                    : Lang("ReasonUnknown", targetId.ToString());
                if (target != null && target.IsConnected) target.Ban(reason);
                else server.Ban(targetId.ToString(), reason);

                var targetName = target != null ? $"{target.Name.Sanitize()} ({target.Id})" : args[0].Sanitize();
                if (broadcastBans) Broadcast("PlayerBanned", targetName, reason, "навсегда");
                else player.Reply(Lang("PlayerBanned", player.Id, targetName, reason, "навсегда"));
            }



        }

        #endregion

        #region Kick Command

        private void KickCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permKick))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            if (args.Length < 1)
            {
                player.Reply(Lang("UsageKick", player.Id, command));
                return;
            }

            var foundPlayers = players.FindPlayers(args[0]).ToArray();
            if (foundPlayers.Length > 1)
            {
                player.Reply("PlayersFound", player.Id,
                    string.Concat(", ", foundPlayers.Select(p => p.Name).ToArray()));
                return;
            }

            var target = foundPlayers.Length == 1 ? foundPlayers[0] : null;
            if (target == null || !target.IsConnected)
            {
                player.Reply(Lang("PlayerNotFound", player.Id, args[0].Sanitize()));
                return;
            }

            if (protectAdmin && target.IsAdmin)
            {
                player.Reply(Lang("PlayerIsAdmin", player.Id, target.Name.Sanitize()));
                return;
            }

            var reason = args.Length >= 2 ? string.Join(" ", args.Skip(1).ToArray()) : Lang("ReasonUnknown", target.Id);
            target.Kick(reason);

            var targetName = $"{target.Name.Sanitize()} ({target.Id})";
            if (broadcastKicks) Broadcast("PlayerKicked", targetName, reason);
            else player.Reply(Lang("PlayerKicked", player.Id, targetName, reason));
        }

        #endregion

        #region Say Command

        private void SayCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permSay))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            if (args.Length < 1)
            {
                player.Reply(Lang("UsageSay", player.Id, command));
                return;
            }

            var message = string.Join(" ", args.ToArray());
            server.Broadcast(message);
        }

        #endregion

        #region Unban Command

        private void UnbanCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permUnban))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            if (args.Length < 1)
            {
                player.Reply(Lang("UsageUnban", player.Id, command));
                return;
            }

            ulong targetId;
            ulong.TryParse(args[0], out targetId);
            var foundPlayers = players.FindPlayers(args[0]).ToArray();
            if (foundPlayers.Length > 1)
            {
                player.Reply("PlayersFound", player.Id,
                    string.Concat(", ", foundPlayers.Select(p => p.Name).ToArray()));
                return;
            }

            var target = foundPlayers.Length == 1 ? foundPlayers[0] : null;
            if (target != null) ulong.TryParse(target.Id, out targetId);

            if (!targetId.IsSteamId())
            {
                player.Reply(Lang("PlayerNotFound", player.Id, args[0].Sanitize()));
                return;
            }

            var targetName = target != null ? $"{target.Name.Sanitize()} ({target.Id})" : args[0].Sanitize();

            if (!server.IsBanned(targetId.ToString()))
            {
                player.Reply(Lang("PlayerNotBanned", player.Id, targetName));
                return;
            }

            server.Unban(targetId.ToString());

            player.Reply(Lang("PlayerUnbanned", player.Id, targetName));
        }

        #endregion

        #region Helpers

        private T GetConfig<T>(string name, T value) =>
            Config[name] == null ? value : (T) Convert.ChangeType(Config[name], typeof(T));

        private string Lang(string key, string id = null, params object[] args) =>
            string.Format(lang.GetMessage(key, this, id), args);

        private void Broadcast(string key, params object[] args)
        {
            foreach (var player in players.Connected.Where(p => p.IsConnected))
                player.Message(Lang(key, player.Id, args));
            Interface.Oxide.LogInfo(Lang(key, null, args));
        }

        private Regex _timeSpanPattern =
            new Regex(
                @"(?:(?<days>\d{1,3})d)?(?:(?<hours>\d{1,3})h)?(?:(?<minutes>\d{1,3})m)?(?:(?<seconds>\d{1,3})s)?",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        TimeSpan GetTime(string text)
        {
            var match = _timeSpanPattern.Match(text);
            if (!match.Success) return new TimeSpan();

            if (!match.Groups[0].Value.Equals(text)) return new TimeSpan();

            Group daysGroup = match.Groups["days"];
            Group hoursGroup = match.Groups["hours"];
            Group minutesGroup = match.Groups["minutes"];
            Group secondsGroup = match.Groups["seconds"];

            int days = daysGroup.Success
                ? int.Parse(daysGroup.Value)
                : 0;
            int hours = hoursGroup.Success
                ? int.Parse(hoursGroup.Value)
                : 0;
            int minutes = minutesGroup.Success
                ? int.Parse(minutesGroup.Value)
                : 0;
            int seconds = secondsGroup.Success
                ? int.Parse(secondsGroup.Value)
                : 0;

            TimeSpan time = new TimeSpan(days, hours, minutes, seconds);
            if (days + hours + minutes + seconds == 0) return new TimeSpan();
            return time;
        }

        private string banFolder = "bans/PlayerBans";
        Dictionary<string, double> playerBans = new Dictionary<string, double>();

        void LoadBans()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile(banFolder))
            {
                playerBans =
                    Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, double>>(banFolder);
            }
        }

        void SaveBans()
        {
            Interface.Oxide.DataFileSystem.WriteObject(banFolder, playerBans);
        }

        void UpdatePlayerBans()
        {
            if (playerBans.Count < 1) return;
            foreach (var id in playerBans.ToList())
            {
                playerBans[id.Key]--;
                if (playerBans[id.Key] == 0)
                {
                    server.Unban(id.Key);
                    playerBans.Remove(id.Key);
                    PrintWarning($"Player {id.Key} unbanned");
                }

            }

            
        }
        void AddBanTime(string user, TimeSpan time) 
        {
            playerBans.Add(user, time.TotalSeconds);
            
        }

        void Unload()
        {
            SaveBans();
        }
        #endregion
    }
}
