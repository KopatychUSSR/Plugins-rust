using Oxide.Core;
using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("OnlyAdminConn", "https://topplugin.ru/", "1.0.0")]
    public class OnlyAdminConn : RustPlugin
    {		
		
		#region Variables
		
		private const bool ALWAYS_CLOSED = false;
		private static WipeState WState = new WipeState();
		private static int Hour = 14;
		private static int Minute = 0;
		
		private class WipeState
		{
			public bool WipeDetected;
			public bool TimeToOpenPassed;
		}
		
		#endregion
		
		#region Hooks
		
		private void Init()
		{
			LoadVariables();
			LoadData();
			
			Hour = Convert.ToInt32(configData.TimeOpen.Split(':')[0]);
			Minute = Convert.ToInt32(configData.TimeOpen.Split(':')[1]);
			
			if (IsServerOpen() && !ALWAYS_CLOSED)
			{
				Unsubscribe(nameof(CanClientLogin));
				Unsubscribe(nameof(OnPlayerConnected));
			}
		}
		
		private void OnNewSave()
		{
			WState.WipeDetected = true;
			WState.TimeToOpenPassed = false;
			SaveData();
			Subscribe(nameof(CanClientLogin));
			Subscribe(nameof(OnPlayerConnected));
			PrintWarning("Сервер закрыт для игроков !");
		}
		
		private void OnServerInitialized()
        {
			if (!IsServerOpen() && !ALWAYS_CLOSED)
				CheckOpenTime();
			
			if (!IsServerOpen() || ALWAYS_CLOSED)
				foreach(var player in BasePlayer.activePlayerList.Where(x=> !configData.Admins.Contains(x.userID)).ToList())
					player.Kick(configData.TextDeny);
		}
		
		private void OnPlayerConnected(BasePlayer player) 
		{
			if (player == null || configData.Admins.Contains(player.userID)) return;
			
			if (!IsServerOpen() || ALWAYS_CLOSED)
				player.Kick(configData.TextDeny);
		}
		
		private object CanClientLogin(Network.Connection connection)
		{			
			if (connection != null)
			{
				var userID = connection.userid;
				if (configData.Admins.Contains(userID)) return true;
			}
			
			return configData.TextDeny;
		}
		
		#endregion
		
		#region Helpers
		
		private void CheckOpenTime()
		{
			var dtNow = DateTime.Now;
			if (dtNow.Hour >= Hour && dtNow.Minute >= Minute)
			{
				WState.WipeDetected = true;
				WState.TimeToOpenPassed = true;
				SaveData();
				Unsubscribe(nameof(CanClientLogin));
				Unsubscribe(nameof(OnPlayerConnected));
				rust.RunServerCommand("env.time 12");
				PrintWarning("Сервер открыт для игроков !");
			}
			else
				timer.Once(1f, CheckOpenTime);
		}
		
		private static bool IsServerOpen()
		{
			if (WState.WipeDetected && WState.TimeToOpenPassed || !WState.WipeDetected)
				return true;
			
			return false;
		}
		
		#endregion
        
		#region Config
		
        private static ConfigData configData; 
		
        private class ConfigData
        {            			
			[JsonProperty(PropertyName = "Админы, которым разрешено заходить на закрытый сервер")]
			public List<ulong> Admins;
			[JsonProperty(PropertyName = "Текст отказа для закрытого сервера")]
			public string TextDeny;
			[JsonProperty(PropertyName = "Время вайпа когда открывать сервер")]
			public string TimeOpen;
        }
		
        private void LoadVariables() => configData = Config.ReadObject<ConfigData>();        
		
        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {                
				Admins = new List<ulong>()
				{
					7656114543264488,
				},
				TextDeny = "Сервер будет открыт в 14:00 по МСК!",
				TimeOpen = "14:00"
			};
            SaveConfig(configData);
			timer.Once(0.1f, ()=> SaveConfig(configData));
        }        
		
        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
		
        #endregion
		
		#region Data
		
		private void LoadData() => WState = Interface.GetMod().DataFileSystem.ReadObject<WipeState>("OnlyAdminConnData");					
		
		private static void SaveData() => Interface.GetMod().DataFileSystem.WriteObject("OnlyAdminConnData", WState);		
		
		#endregion
		
    }
}