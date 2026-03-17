using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using WebSocketSharp;

namespace Oxide.Plugins
{
    [Info("SlotPanel", "https://topplugin.ru/", "0.0.5")]
    [Description("Добавляет быстрое меню, во круг слотов инвентаря!")]
    public class SlotPanel : RustPlugin
    {
        #region Classes

        private class Point
        {
            [JsonProperty("Название пункта меню")] 
            public string DisplayName { get; set; }
            [JsonProperty("Ссылка на изображение")]
            public string IconImage { get; set; }
            [JsonProperty("Переопределить цвет заднего фона")]
            public string BackgroundColor { get; set; }
            [JsonProperty("Сузить/расширить изображение")]
            public int OffSet { get; set; }
            [JsonProperty("Привилегия для просмотра раздела")]
            public string PermissionToSee { get; set; }
            [JsonProperty("При нажатии выполняется команда")]
            public string Command { get; set; }
        }

        private class Configuration
        {
            [JsonProperty("Меню справа от инвентаря")]
            public HashSet<Point> RightMenu { get; set; }
            [JsonProperty("Изначальное состояние")]
            public bool DefaultCondition { get; set; }
            [JsonProperty("Разрешить сворачивать меню")]
            public bool AllowHide { get; set; }
            [JsonProperty("Слегка сжать меню (чтобы на определенных разрешениях не залезало на ХП)")]
            public bool MiniMode { get; set;  }

            public static Configuration LoadDefault()
            {
                return new Configuration
                {
                    AllowHide        = true,
                    DefaultCondition = false,
                    RightMenu = new HashSet<Point> 
                    {
                        new Point
                        {   
                            IconImage = "assets/icons/open.png", 
                            Command   = "chat.say /stats",
                            OffSet    = 5,
                            DisplayName = "Корзина",
                            BackgroundColor = "",
                            PermissionToSee = ""
                        }, 
                        new Point
                        {   
                            IconImage = "", 
                            Command   = "chat.say /bp.open",
                            OffSet    = 5,
                            DisplayName = "ОТКРЫТЬ\n<b>РЮКЗАК</b>",
                            BackgroundColor = "",
                            PermissionToSee = ""
                        },
                        new Point
                        {   
                            IconImage = "assets/icons/market.png", 
                            Command = "chat.say /stats",
                            OffSet = 5,
                            BackgroundColor = "",
                            PermissionToSee = ""
                        }, 
                        new Point
                        {
                            IconImage = "https://i.imgur.com/hyxpgP8.png",
                            Command   = "chat.say /info",
                            OffSet = 5,
                            BackgroundColor = "",
                            PermissionToSee = ""
                        }
                    }
                };
            } 
        }

        #endregion

        #region Variables

        [PluginReference] private Plugin ImageLibrary;
        private Configuration Settings;
        private string UsePermission = "SlotPanel.Use";

        #endregion

        #region Initialization
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                Settings = Config.ReadObject<Configuration>();
                if (Settings?.RightMenu == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning($"Error reading config, creating one new config!");
                LoadDefaultConfig();
            }
            
            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => Settings = Configuration.LoadDefault(); 
        protected override void SaveConfig() => Config.WriteObject(Settings);
        
        private void Unload() => BasePlayer.activePlayerList.ToList().ForEach(p => CuiHelper.DestroyUi(p, Layer));
        
        private void OnServerInitialized()
        { 
            PrintWarning("  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
            PrintWarning($"     SlotPanel v{Version} loading"); 
            if (ShouldInstallImageLibrary()) 
                PrintError("   Install plugin: 'ImageLibrary'");
            if (ShouldConfigure())
                PrintError("Configure plugin! Config -> SlotPanel");
            PrintWarning($"        Plugin loaded - OK");
            PrintWarning("  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
 
            Settings.RightMenu.Where(p => !p.IconImage.IsNullOrEmpty() && p.IconImage.StartsWith("http")).ToList().ForEach(p => ImageLibrary.Call("AddImage", p.IconImage, $"FM.Icon.{Settings.RightMenu.ToList().IndexOf(p)}"));
            Settings.RightMenu.Where(p => !p.PermissionToSee.IsNullOrEmpty()).ToList().ForEach(p => permission.RegisterPermission(p.PermissionToSee, this));
            if (!UsePermission.IsNullOrEmpty()) permission.RegisterPermission(UsePermission, this);
            
            BasePlayer.activePlayerList.ToList().ForEach(OnPlayerConnected);
        }

        #endregion

        #region Hooks

        private void OnPlayerDie(BasePlayer player, HitInfo info) => player.SendConsoleCommand("UI_SlotPanelHandler toggle off");

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerConnected(player));
                return;
            }

            if (!UsePermission.IsNullOrEmpty() && !permission.UserHasPermission(player.UserIDString, UsePermission)) return;
            
            UI_DrawInterface(player, !Settings.DefaultCondition);  
        }

        #endregion

        #region Commands

        [ConsoleCommand("UI_SlotPanelHandler")]
        private void CmdConsoleHandler(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player == null || !args.HasArgs(1)) return;

            if (!UsePermission.IsNullOrEmpty() && !permission.UserHasPermission(player.UserIDString, UsePermission)) return;

            switch (args.Args[0].ToLower())
            {
                case "toggle":
                {
                    if (!args.HasArgs(2)) return;
                    
                    UI_DrawInterface(player, args.Args[1].ToLower() != "on"); 
                    break;
                }
            }
        }

        #endregion

        #region Interfaces

        private const string Layer = "UI_SlotPanel_LayerInterface";
        private void UI_DrawInterface(BasePlayer player, bool hide)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, Layer);
            
            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "0 48", OffsetMax = "0 48"}, 
                Image         = {Color     = "0 0 0 0"}
            }, "Overlay", Layer);
            
            if (hide)
            {
                container.Add(new CuiButton
                {
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-264 -30", OffsetMax = "-204 30"},
                    Button        = {Color     = "0.968627453 0.924251568632 0.882352948 0.03529412", Command = "UI_SlotPanelHandler toggle on"},
                    Text          = {Text      = "ОТКРЫТЬ\n<b>МЕНЮ</b>", Align       = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = "1 1 1 0.4" }
                }, Layer);
            } 
            else
            {
                if (Settings.AllowHide)
                {
                    container.Add(new CuiButton
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax                                          = "1 1", OffsetMin = "-264 -30", OffsetMax = "-204 30"},
                        Button        = {Color     = "0.968627453 0.924251568632 0.882352948 0.03529442512", Command = "UI_SlotPanelHandler toggle off"},
                        Text = {Text = "ЗАКРЫТЬ\n<b>МЕНЮ</b>", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = "1 1 1 0.4" }
                    }, Layer);
                } 
                
                int currentPointer = 184;
                foreach (var check in Settings.RightMenu.Where(p => p.PermissionToSee.IsNullOrEmpty() || permission.UserHasPermission(player.UserIDString, p.PermissionToSee))) 
                { 
                    container.Add(new CuiButton
                    {
                        RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"{currentPointer} -30", OffsetMax = $"{currentPointer +  60} 30"},
                        Button        = {FadeIn = 0.5f, Color = check.BackgroundColor.IsNullOrEmpty() ? "0.968627453 0.921542568632 0.882352948 0.03529412" : check.BackgroundColor.StartsWith("#") ? HexToRustFormat(check.BackgroundColor) : check.BackgroundColor, Command = ""}, 
                        Text          = {Text      = "", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 20, Color = "1 1 1 0.4" }
                    }, Layer, Layer + $".{currentPointer}");

                    if (!check.IconImage.IsNullOrEmpty())
                    {
                        if (check.IconImage.Contains("assets"))
                        {
                            container.Add(new CuiButton
                            {
                                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"{check.OffSet} {check.OffSet}", OffsetMax = $"{-check.OffSet} {-check.OffSet}" },
                                Button = {FadeIn = 0.5f,  Sprite = check.IconImage, Color = "1 1 1 0.4", Command = check.Command },
                                Text = { Text = "" }
                            }, Layer + $".{currentPointer}");
                        }
                        else if (check.IconImage.Contains("http"))
                        {
                            container.Add(new CuiElement
                            {
                                Parent = Layer + $".{currentPointer}",
                                Components =
                                {
                                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", $"FM.Icon.{Settings.RightMenu.ToList().IndexOf(check)}") },
                                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"{check.OffSet} {check.OffSet}", OffsetMax = $"{-check.OffSet} {-check.OffSet}" }
                                }
                            });
                        }
                        else
                        {
                            PrintError($"Failed to show icon '{check.IconImage}' to player '{player.displayName} [{player.userID}]'");
                        }

                        if (!check.DisplayName.IsNullOrEmpty())
                        {
                            container.Add(new CuiButton
                            {
                                RectTransform = { AnchorMin = "0 -1", AnchorMax         = "1 0", OffsetMax = "0 -2" },
                                Button        = { Command   = check.Command, Color     = "0 0 0 0" },
                                Text          = { FadeIn = 0.5f, Text      = check.DisplayName, Align = TextAnchor.UpperCenter, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = "1 1 1 0.4"}
                            }, Layer + $".{currentPointer}"); 
                        }
                    }
                    else if (!check.DisplayName.IsNullOrEmpty())
                    {
                        container.Add(new CuiButton
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                            Button = { Command = check.Command, Color = "0 0 0 0" },
                            Text = { FadeIn = 0.5f, Text = check.DisplayName, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = "1 1 1 0.4"}
                        }, Layer + $".{currentPointer}");
                    }
                    
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "0 0", AnchorMax     = "1 1", OffsetMax = "0 0" },
                        Button        = { Command   = check.Command, Color = "0 0 0 0" },
                        Text          = { FadeIn    = 0.5f, Text           = "", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 12, Color = "1 1 1 0.4"}
                    }, Layer + $".{currentPointer}");

                    currentPointer += 64;
                    if (Settings.MiniMode) currentPointer -= 2;
                }
            }

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Utils

        private bool ShouldConfigure() => Settings.RightMenu.Count == 0;
        private bool ShouldInstallImageLibrary() => Settings.RightMenu.Any(p => p.IconImage.Contains("http")) && !ImageLibrary;
        
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

            Color color = new Color32(r, g, b, a);

            return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
        }

        #endregion
    }
}