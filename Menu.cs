using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Menu", "TopPlugin.ru", "1.1.1")]
    class Menu : RustPlugin
    {
		private PluginConfig config;
		private static string Layer = "MenuGUI";
		
		#region Initialization
		protected override void LoadDefaultConfig() => Config.WriteObject(PanelConfig(), true);
		
        private void Init()
		{
			config = Config.ReadObject<PluginConfig>();
		}
		
		void Load()
		{
			foreach (BasePlayer player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, Layer);
		}
		
		void Unload()
        {
			foreach (BasePlayer player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, Layer);
        }
        #endregion

        #region Commands
		[ChatCommand("menu")]
        private void cmdMenuOpen(BasePlayer player)
		{
			DrawGUI(player);
		}
		
		[ConsoleCommand("ui.menu")]
		private void consoleMenuOpen(ConsoleSystem.Arg arg, string[] args)
		{
			var player = arg.Player();
			DrawGUI(player);
		}
		#endregion
		
		#region GUI Interface
        private void DrawGUI(BasePlayer player)
        {
			CuiElementContainer container = new CuiElementContainer();
			
			#region Создаём элемент родитель интерфейса панели.
            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-133.333 -216.7", OffsetMax = "133.333 216.7" },
                CursorEnabled = false,
            }, "Overlay", Layer);
			#endregion

			#region Создаём кнопки для меню.
            int i = 0;
            foreach(var panel in config.panellist)
            {
				CuiHelper.DestroyUi(player, Layer + $".{i}");
				container.Add(new CuiElement
				{
					Parent = Layer,
					Name = Layer + $".{i}",
					Components = {
						new CuiImageComponent { FadeIn = 3.0f, Color = HexToCuiColor(panel.PanellColor), Material = "assets/content/ui/uibackgroundblur.mat" },
						new CuiRectTransformComponent { AnchorMin = panel.PanelAnchorMin, AnchorMax = panel.PanelAnchorMax }
					}
				});
				
				container.Add(new CuiElement
				{
					Parent = Layer + $".{i}",
					Components = {
						new CuiRawImageComponent { FadeIn = 3.0f, Url = panel.PanelImageUrl, Color = HexToCuiColor(panel.PanelColor) },
                        new CuiRectTransformComponent { AnchorMin = "0.0125 0.1", AnchorMax = "0.0125 0.1", OffsetMax = "27 27" }
					}
				});
				
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.125 -0.06", AnchorMax = "0.9999998 0.9400001" },
                    Button = { Color = "0 0 0 0", Close = Layer, Command = panel.PanelButtonCmd },
                    Text = { FadeIn = 3.0f, Text = panel.PanelButtonName, Color = HexToCuiColor(panel.PanelColor), FontSize = panel.ButtonSize, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf" }
                }, Layer + $".{i}");
				
                i++;
            }
			#endregion

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
        }
        #endregion
		
		#region Configuration
        private class PluginConfig
        {			
            [JsonProperty("Вкладки меню")]
            public List<AddtionalPanel> panellist = new List<AddtionalPanel>();
        }

        private class AddtionalPanel
        {
			[JsonProperty("Минимальный отступ панели")]
            public string PanelAnchorMin;
            [JsonProperty("Максимальный отступ панели")]
            public string PanelAnchorMax;
            [JsonProperty("Картинка")]
            public string PanelImageUrl;
            [JsonProperty("Команда")]
            public string PanelButtonCmd;
			[JsonProperty("Названия кнопки")]
            public string PanelButtonName;
			[JsonProperty("Цвет кнопки и картинки")]
            public string PanelColor;
			[JsonProperty("Цвет заднего фона кнопки")]
			public string PanellColor;
			[JsonProperty("Размер кнопки (Дефолтный размер: 15)")]
		    public int ButtonSize;
        }
		
		private PluginConfig PanelConfig()
        {
            return new PluginConfig
            {	
                panellist = new List<AddtionalPanel>
                {
                   new AddtionalPanel
                   {
					    PanelAnchorMin = "0 0.9230767",
						PanelAnchorMax = "1 0.9999998",
						PanelImageUrl = "https://i.imgur.com/yYFA19S.png",
						PanelButtonName = "ЧАСТЫЕ ВОПРОСЫ",
                        PanelButtonCmd = "chat.say /who",
						ButtonSize = 15,
						PanelColor = "#FFFFFFD4",
						PanellColor = "#00000099",
                   },
				   new AddtionalPanel
                   {
					    PanelAnchorMin = "0 0.84",
						PanelAnchorMax = "1 0.916923",
						PanelImageUrl = "https://i.imgur.com/NjJIYKX.png",
						PanelButtonName = "КОМАНДЫ СЕРВЕРА",
                        PanelButtonCmd = "chat.say /cmd",
						ButtonSize = 15,
						PanelColor = "#FFFFFFD4",
						PanellColor = "#00000099",
                   },
				   new AddtionalPanel
                   {
					    PanelAnchorMin = "0 0.7569232",
						PanelAnchorMax = "1 0.8338463",
						PanelImageUrl = "https://i.imgur.com/HMBqrJY.png",
						PanelButtonName = "ПРАВИЛА",
                        PanelButtonCmd = "chat.say /rules",
						ButtonSize = 15,
						PanelColor = "#FFFFFFD4",
						PanellColor = "#00000099",
                   },
				   new AddtionalPanel
                   {
					    PanelAnchorMin = "0 0.6738441",
						PanelAnchorMax = "1 0.7507669",
						PanelImageUrl = "https://i.imgur.com/z3TsNjL.png",
						PanelButtonName = "ИВЕНТЫ",
                        PanelButtonCmd = "chat.say /event",
						ButtonSize = 15,
						PanelColor = "#FFFFFFD4",
						PanellColor = "#00000099",
                   },
				   new AddtionalPanel
                   {
					    PanelAnchorMin = "0 0.5907675",
						PanelAnchorMax = "1 0.6676903",
						PanelImageUrl = "https://i.imgur.com/cBRfXRE.png",
						PanelButtonName = "МИНИ-МАГАЗИН",
                        PanelButtonCmd = "chat.say /event",
						ButtonSize = 15,
						PanelColor = "#FFFFFFD4",
						PanellColor = "#00000099",
                   },
				   new AddtionalPanel
                   {
					    PanelAnchorMin = "0 0.5063744",
						PanelAnchorMax = "1 0.5832962",
						PanelImageUrl = "https://i.imgur.com/VetLHf7.png",
						PanelButtonName = "МАГИЧЕСКИЕ КАРТЫ",
                        PanelButtonCmd = "chat.say /cards",
						ButtonSize = 15,
						PanelColor = "#FFFFFFD4",
						PanellColor = "#00000099",
                   },
				   new AddtionalPanel
                   {
					    PanelAnchorMin = "0 0.4217724",
						PanelAnchorMax = "1 0.4986934",
						PanelImageUrl = "https://i.imgur.com/bncm7yw.png",
						PanelButtonName = "БЛОКИРОВКИ",
                        PanelButtonCmd = "chat.say /block",
						ButtonSize = 15,
						PanelColor = "#FFFFFFD4",
						PanellColor = "#00000099",
                   },
				   new AddtionalPanel
                   {
					    PanelAnchorMin = "0 0.3371705",
						PanelAnchorMax = "1 0.4140906",
						PanelImageUrl = "https://i.imgur.com/9mp8kD2.png",
						PanelButtonName = "ДОСТИЖЕНИЯ",
                        PanelButtonCmd = "chat.say /progress",
						ButtonSize = 15,
						PanelColor = "#FFFFFFD4",
						PanellColor = "#00000099",
                   },
				   new AddtionalPanel
                   {
					    PanelAnchorMin = "0 0.2525685",
						PanelAnchorMax = "1 0.3294877",
						PanelImageUrl = "https://i.imgur.com/nEXwfHe.png",
						PanelButtonName = "НОВОСТИ",
                        PanelButtonCmd = "chat.say /news",
						ButtonSize = 15,
						PanelColor = "#FFFFFFD4",
						PanellColor = "#00000099",
                   },
				   new AddtionalPanel
                   {
					    PanelAnchorMin = "0 0.1679658",
						PanelAnchorMax = "1 0.2448848",
						PanelImageUrl = "https://i.imgur.com/5coLhc4.png",
						PanelButtonName = "КОМАНДА ПРОЕКТА",
                        PanelButtonCmd = "chat.say /projectteam",
						ButtonSize = 15,
						PanelColor = "#FFFFFFD4",
						PanellColor = "#00000099",
                   },
				   new AddtionalPanel
                   {
					    PanelAnchorMin = "0 0.08336324",
						PanelAnchorMax = "1 0.1602823",
						PanelImageUrl = "https://i.imgur.com/F7qfncB.png",
						PanelButtonName = "ВРЕМЕННЫЕ УСЛУГИ",
                        PanelButtonCmd = "chat.say /services",
						ButtonSize = 15,
						PanelColor = "#FFFFFFD4",
						PanellColor = "#00000099",
                   },
				   new AddtionalPanel
                   {
					    PanelAnchorMin = "0 -0.001239109",
						PanelAnchorMax = "1 0.07568013",
						PanelImageUrl = "https://i.imgur.com/1rr98kt.png",
						PanelButtonName = "ЗАКРЫТЬ",
						ButtonSize = 15,
						PanelColor = "#FFFFFFD4",
						PanellColor = "#990D0D74",
                   },
                }
            };
        }
        #endregion
		
		#region Utilits and Others
		private static string HexToCuiColor(string hex)
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
 
            Color color = new Color32(r, g, b, a);
 
            return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
        }
		#endregion
    }
}
