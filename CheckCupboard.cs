using System;
using System.Linq;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("CheckCupboard", "TopPlugin.ru", "1.0.0")]    
    public class CheckCupboard : RustPlugin
    {        					
		
		#region Variables
		public static CheckCupboard ins;
		private static string AllowPerm = "checkcupboard.allow";
		
		#endregion
		
		#region Hooks
		
		private void Init() => permission.RegisterPermission(AllowPerm, this);										
		
		#endregion
		
		#region Commands

		[ConsoleCommand("cupboard.owner")]
        private void ConsCommandCheckCupOwner(ConsoleSystem.Arg arg)
        {
            if (arg == null) return;
            
			var player = arg.Player();
			
			if (player != null && !player.IsAdmin && !permission.UserHasPermission(player.UserIDString, AllowPerm)) 
			{
				PrintToConsole(player, "У вас нет прав на эту команду!");
				return;
			}
			
			Print(player, "Поиск строений владельца");
			var param = arg.Args.Length > 0 ? arg.Args[0] : "";
			
			if (string.IsNullOrEmpty(param))
			{
				Print(player, "Использование: cupboard.owner <steamID> или <Ник>");
				return;
			}
			//Ищем данного игрока 
			BasePlayer target = null;

            if(!PlayerHelper.FindMultiple(param, player, out target))
            {
				Print(player, $"Игрок {param} не найден или найдено несколько игроков, уточните запрос {target}");
                return;
            }
			var cups = BaseNetworkable.serverEntities.OfType<BuildingPrivlidge>().Where(x=>x.OwnerID == target.userID);
			
			bool wasIn = false;
			
			if (cups?.Count()>0)
				Print(player, "\n");
			
			foreach(var cup in cups)
			{
				Print(player, string.Format("<color=white>Шкаф в квадрате {0}, содержит игроков:</color>", GetGrid(cup.transform.position)));
				foreach(var user in cup.authorizedPlayers)
					Print(player, $@"{!string.IsNullOrEmpty(user.username) ? user.username : "N|A"} ({user.userid})");
				Print(player, "\n");
				wasIn = true;
			}
			
			if (!wasIn)
				Print(player, "Данный игрок не создавал ни одного шкафа.");			
		}
		
		[ConsoleCommand("cupboard.auth")]
        private void ConsCommandCheckCupAuth(ConsoleSystem.Arg arg)
        {
            if (arg == null) return;
            
			var player = arg.Player();
			
			if (player != null && !player.IsAdmin && !permission.UserHasPermission(player.UserIDString, AllowPerm)) 
			{
				PrintToConsole(player, "У вас нет прав на эту команду!");
				return;
			}
			
			var param = arg.Args.Length > 0 ? arg.Args[0] : "";
			
			if (string.IsNullOrEmpty(param))
			{
				Print(player, "Использование: cupboard.auth <steamID>");
				return;
			}
			
			//Ищем данного игрока 
			BasePlayer target = null;

            if(!PlayerHelper.FindMultiple(param, player, out target))
            {
				Print(player, $"Игрок {param} не найден или найдено несколько игроков, уточните запрос {target}");
                return;
            }
			
			var cups = BaseNetworkable.serverEntities.OfType<BuildingPrivlidge>().Where(y=> y.authorizedPlayers.Exists(x=>x.userid == target.userID));
			
			bool wasIn = false;
			
			if (cups?.Count()>0)
				Print(player, "\n");
			
			foreach(var cup in cups)
			{
				Print(player, string.Format("<color=white>Шкаф в квадрате {0}, содержит игроков:</color>", GetGrid(cup.transform.position)));
				foreach(var user in cup.authorizedPlayers)
					Print(player, $@"{!string.IsNullOrEmpty(user.username) ? user.username : "N|A"} ({user.userid})");
				Print(player, "\n");
				wasIn = true;
			}
			
			if (!wasIn)
				Print(player, "Данный игрок не зарегистрирован ни в одном шкафу.");			
		}
		
		#endregion
		
		#region Helpers
		
		private string GetCluster(Vector3 pos)
		{
			string[] args = new string[] { $"{pos.x}", $"{pos.y}", $"{pos.z}" };
			return $"{(char)((int)Math.Truncate((World.Size/2f+Convert.ToSingle(args[0]))/150f)+65)}{(int)Math.Truncate((World.Size/2f-Convert.ToSingle(args[2]))/150f)}";
		}
		
		private static string GetGrid(Vector3 pos) {
			char letter = 'A';
			var x = Mathf.Floor((pos.x+(ConVar.Server.worldsize/2)) / 146.3f)%26;
			var z = (Mathf.Floor(ConVar.Server.worldsize/146.3f)-1)-Mathf.Floor((pos.z+(ConVar.Server.worldsize/2)) / 146.3f);
			letter = (char)(((int)letter)+x);
			return $"{letter}{z}";
		}
		
		private void Print(BasePlayer player, string text)
		{
			if (player != null)
				PrintToConsole(player, text);
			else
				Puts(text.Replace("<color=white>","").Replace("</color>",""));
		}
		
		#endregion
		
		#region API
		
		private void API_CupboardAuth(ulong moderID, string user, Action<string> cbSendVkAnswer)
		{			
			if (!permission.UserHasPermission(moderID.ToString(), AllowPerm)) 
			{
				cbSendVkAnswer("У вас нет прав на эту команду!");
				return;
			}
			
			if(string.IsNullOrEmpty(user))
			{
				cbSendVkAnswer("Вы не указали игрока.");
				return;
			}
			
			ulong num = user.IsSteamId() ? (ulong)Convert.ToInt64(user) : 0;
            if (num == 0)
            {                
                cbSendVkAnswer("Неверный Steam ID");
				return;
            }
			
			var cups = BaseNetworkable.serverEntities.OfType<BuildingPrivlidge>().Where(y=> y.authorizedPlayers.Exists(x=>x.userid == num));
			
			bool wasIn = false;			
			var result = "";						
			
			foreach(var cup in cups)
			{
				result += string.Format(" * Шкаф в квадрате {0}, содержит игроков:", GetGrid(cup.transform.position)) + "\n";
				
				foreach(var user_ in cup.authorizedPlayers)
					result +=  $@"{!string.IsNullOrEmpty(user_.username) ? user_.username : "N|A"} ({user_.userid})" + "\n";
				
				wasIn = true;
			}
			
			if (!wasIn)
				cbSendVkAnswer("Данный игрок не зарегистрирован ни в одном шкафу.");		
			else
				cbSendVkAnswer(result.TrimEnd('\n'));
		}
		
		#endregion
		#region PlayerHelpers
		
        private static class PlayerHelper
        {
            private static bool FindPlayerPredicate(BasePlayer player, string nameOrUserId)
            {
                return player.displayName.IndexOf(nameOrUserId, StringComparison.OrdinalIgnoreCase) != -1 ||
                       player.UserIDString == nameOrUserId;
            }

            public static bool Find(string nameOrUserId, out BasePlayer target)
            {
                nameOrUserId = nameOrUserId.ToLower();
                foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
                {
                    if (PlayerHelper.FindPlayerPredicate(activePlayer, nameOrUserId))
                    {
                        target = activePlayer;
                        return true;
                    }
                }

                foreach (BasePlayer sleepingPlayer in BasePlayer.sleepingPlayerList)
                {
                    if (PlayerHelper.FindPlayerPredicate(sleepingPlayer, nameOrUserId))
                    {
                        target = sleepingPlayer;
                        return true;
                    }
                }

                target = (BasePlayer) null;
                return false;
            }

            public static bool FindMultiple(string nameOrUserId, BasePlayer player, out BasePlayer target)
            {
                if (nameOrUserId == string.Empty)
                {
                    if (!player==null) ins.SendReply(player, "Игрок не найден.");
                    target = (BasePlayer) null;
                    return false;
                }

                List<BasePlayer> Players = new List<BasePlayer>();
                nameOrUserId = nameOrUserId.ToLower();

                foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
                {
                    if (FindPlayerPredicate(activePlayer, nameOrUserId))
                    {
                        Players.Add(activePlayer);
                    }
                }

                foreach (BasePlayer sleepPlayer in BasePlayer.sleepingPlayerList)
                {
                    if (FindPlayerPredicate(sleepPlayer, nameOrUserId))
                    {
                        Players.Add(sleepPlayer);
                    }
                }
                switch (Players.Count)
                {
                    case 0:
                        if (!player==null) ins.SendReply(player, $"Игрок {nameOrUserId} был не найден!");
                        target = null;
                        return false;

                    case 1:
                        target = Players[0];
                        return true;

                    default:


                        var text = "";

                        foreach (var activePlayer in Players)
                        {
                            text += $" {activePlayer.displayName},";
                        }

                        if (!player==null) ins.SendReply(player, $"Было найдено несколько игроков: {text}"); 
                        target = null;

                        return false;
                }


                target = null;
                return false;
            }

            public static bool FindOnline(string nameOrUserId, out BasePlayer target)
            {
                if (nameOrUserId == string.Empty)
                {
                    target = (BasePlayer) null;
                    return false;
                }

                nameOrUserId = nameOrUserId.ToLower();
                foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
                {
                    if (FindPlayerPredicate(activePlayer, nameOrUserId))
                    {
                        target = activePlayer;
                        return true;
                    }
                }

                target = (BasePlayer) null;
                return false;
            }

        }
		#endregion
		public void puts(string t){
			Puts(t);
			return;
		}
    }
}