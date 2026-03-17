using System.Collections.Generic;
using System;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("WipeInfo", "Я и Я", "1.0.0")]
	class WipeInfo : RustPlugin
	{            
	
		private List<List<string>> WipeWords = new List<List<string>>()
		{
			new List<string>() { "wipe" },
			new List<string>() { "wipe", "был" },
			new List<string>() { "вайп", "был" },
			new List<string>() { "глобал", "был" },
			new List<string>() { "випе", "был" }				
		};		
	
		[ChatCommand("wipe")]
		private void cmdWipe(BasePlayer player, string command, string[] args) => SendWipeInfo(player);            		
		
		private void SendWipeInfo(BasePlayer player)
		{
			var dt = SaveRestore.SaveCreatedTime.AddHours(3);
			var dateWipe = dt.ToString("dd/MM");
			var timeWipe = dt.ToString("HH:mm");				
			SendReply(player, $"Вайп был произведён {dateWipe} в {timeWipe}.");		
		}
		
		private void OnPlayerChat(ConsoleSystem.Arg arg)
		{				
			BasePlayer player = arg?.Player();
			if (player == null) return;				
			if (arg.Args == null || arg.Args.Length == 0) return;														
			
			string text = arg.Args[0].ToLower();
			
			foreach(var words in WipeWords)
			{
				int cnt=0;
				foreach(var word in words)				
					cnt = text.Contains(word)?cnt+1:0;
				
				if (words.Count==cnt && cnt>0)
				{
					timer.Once(0.1f, ()=> SendWipeInfo(player));
					return;
				}
			}
		}
	}
}
