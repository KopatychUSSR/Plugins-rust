using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using UnityEngine;
namespace Oxide.Plugins
{
    [Info("Дополнительное GUI", "TopPlugin.ru", "1.0.3")]
    [Description("Дополнительное GUI")]
    class MiniPanel : RustPlugin
    {
        private class PluginConfig
        {
            [JsonProperty("Гл.Анчор панельки")] public string PanelAnchor;
            [JsonProperty("Гл.Офсет панельки (Min)")] public string PanelOffsetMin;
            [JsonProperty("Гл.Офсет панельки (Max)")] public string PanelOffsetMax;
            [JsonProperty("Гл.Текст панельки")] public string PanelText;
            [JsonProperty("Гл.Текст позиция")] public string TextPos;
            [JsonProperty("Гл.Цвет текста")] public string PanelColor;
            [JsonProperty("Гл.Прозрачность текста")] public float PanelAlpha;
            [JsonProperty("Гл.Команда")] public string PanelCmd;
            [JsonProperty("Гл.Кнопка текст включить")] public string ButtonTextOn;
            [JsonProperty("Гл.Кнопка текст выключить")] public string ButtonTextOff;
            [JsonProperty("Гл.Кнопка Анчор")] public string ButtonAnchor;
            [JsonProperty("Гл.Кнопка офсет (Min)")] public string ButtonOffsetMin;
            [JsonProperty("Гл.Кнопка офсет (Max)")] public string ButtonOffsetMax;
            [JsonProperty("Стрелочка (Цвет)")] public string ArrowColor;
            [JsonProperty("Стрелочка включена? (1 - да, 0 - нет)")] public bool ArrowMode;
            [JsonProperty("Больше панелей")] public List<AddtionalPanel> _listPanels = new List<AddtionalPanel>();
        }
        private class AddtionalPanel
        {
            [JsonProperty("Анчор панельки")] public string PanelAnchor;
            [JsonProperty("Офсет панельки (Min)")] public string PanelOffsetMin;
            [JsonProperty("Офсет панельки (Max)")] public string PanelOffsetMax;
            [JsonProperty("Картинка или Текст")] public string PanelImageUrl;
            [JsonProperty("Команда")] public string PanelCmd;
            [JsonProperty("Пермишин для доступа к пункту")] public string Perm;
        }
        private string UI_Layer = "UI_Panelka";
        private PluginConfig config;
        private Dictionary<ulong, bool> _playerInfo = new Dictionary<ulong, bool>();
        private void Init()
        {
            config = Config.ReadObject<PluginConfig>();
        }
        void Unload()
        {
           foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, UI_Layer);
            }
        }
        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }
        void OnServerInitialized()
        {
            foreach(var p in config._listPanels)
            {
                if (string.IsNullOrEmpty(p.Perm)) continue;
                if (!permission.PermissionExists(p.Perm, this)) permission.RegisterPermission(p.Perm, this);
            }

           foreach (var player in BasePlayer.activePlayerList)
            {
                Draw_UIMain(player, config.PanelAnchor);
            }
        }
        void OnPlayerSleepEnded(BasePlayer player)
        {
            Draw_UIMain(player, config.PanelAnchor);
        }
        private void Draw_UIMain(BasePlayer player, string MainPosition = "0.01 0.99")
        {
            if (player == null) return;
            if (!_playerInfo.ContainsKey(player.userID)) _playerInfo.Add(player.userID,false);
            CuiElementContainer container = new CuiElementContainer {
                {
                    new CuiPanel {
                        CursorEnabled =false, RectTransform = {
                            AnchorMin = MainPosition,
                            AnchorMax = MainPosition,
                            OffsetMin = config.PanelOffsetMin,
                            OffsetMax = config.PanelOffsetMax
                        }, Image = {
                            Color = "0 0 0 0"
                        }
                    }, "Overlay", UI_Layer
                }, new CuiElement {
                    Parent = UI_Layer, Components = {
                        new CuiTextComponent {
                            Text = config.PanelText, Align = GetPos(config.TextPos), Font = "robotocondensed-bold.ttf", Color = GetColor(config.PanelColor, config.PanelAlpha)
                        },
                        new CuiRectTransformComponent {
                            AnchorMin = $"0 0", AnchorMax = $"1 1"
                        }
                    }
                }, {
                    new CuiButton {
                        RectTransform = {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        }, Button = {
                            Color = "0 0 0 0",
                            Command = config.PanelCmd
                        }, Text = {
                            Text = ""
                        }
                    },
                    UI_Layer
                }
            };
            if (config._listPanels.Count() < 1)
            {
                CuiHelper.DestroyUi(player, UI_Layer);
                CuiHelper.AddUi(player, container);
                return;
            }
            if (config.ArrowMode)
            {
                container.Add(new CuiElement
                {
                    Parent = UI_Layer,
                    Name = UI_Layer + $".Toggle",
                    Components = {
                        new CuiTextComponent {
                            Text = _playerInfo[player.userID] ? config.ButtonTextOn: config.ButtonTextOff, Align = TextAnchor.MiddleCenter, Color = GetColor(config.ArrowColor), Font = "robotocondensed-bold.ttf"
                        },
                        new CuiRectTransformComponent {
                            AnchorMin = config.ButtonAnchor, AnchorMax = config.ButtonAnchor, OffsetMin = config.ButtonOffsetMin, OffsetMax = config.ButtonOffsetMax
                        }
                    }
                });
                container.Add(new CuiButton
                {
                    RectTransform = {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    },
                    Button = {
                        Color = "0 0 0 0",
                        Command = "UI_TogglePanel"
                    },
                    Text = {
                        Text = ""
                    }
                }, UI_Layer + $".Toggle");
            }
            if (_playerInfo[player.userID])
            {
                CuiHelper.DestroyUi(player, UI_Layer);
                CuiHelper.AddUi(player, container);
                return;
            }
            int counter = 0 * 19;
           foreach (var panel in config._listPanels)
            {
                if (!string.IsNullOrEmpty(panel.Perm) && !permission.UserHasPermission(player.UserIDString, panel.Perm)) continue;

                if (panel.PanelImageUrl.Contains("http"))
                {
                    container.Add(new CuiElement
                    {
                        Parent = UI_Layer,
                        Name = UI_Layer + $".Panel_{counter}",
                        Components = {
                            new CuiRawImageComponent {
                                Url = panel.PanelImageUrl,
                            },
                            new CuiRectTransformComponent {
                                AnchorMin = panel.PanelAnchor, AnchorMax = panel.PanelAnchor, OffsetMin = panel.PanelOffsetMin, OffsetMax = panel.PanelOffsetMax
                            }
                        }
                    });
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Parent = UI_Layer,
                        Name = UI_Layer + $".Panel_{counter}",
                        Components = {
                            new CuiTextComponent {
                                Text = panel.PanelImageUrl, Align = TextAnchor.MiddleCenter
                            },
                            new CuiRectTransformComponent {
                                AnchorMin = panel.PanelAnchor, AnchorMax = panel.PanelAnchor, OffsetMin = panel.PanelOffsetMin, OffsetMax = panel.PanelOffsetMax
                            }
                        }
                    });
                }
                container.Add(new CuiButton
                {
                    RectTransform = {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    },
                    Button = {
                        Color = "0 0 0 0",
                        Command = "UI_PlayerCommand " + panel.PanelCmd
                    },
                    Text = {
                        Text = ""
                    }
                }, UI_Layer + $".Panel_{counter}");
                counter++;
            }
            CuiHelper.DestroyUi(player, UI_Layer);
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("UI_PlayerCommand")]
        private void CMD_UI_PlayerCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null) return;
            string cmd = string.Join(" ", arg.Args);
            if (arg.Args[0] == "chat.say")
            {
                cmd = arg.Args[0];
                cmd += $" \"{string.Join(" ", arg.Args.Skip(1))}\"";
            }
            player.Command(cmd);
            return;
        }

        [ConsoleCommand("UI_TogglePanel")]
        private void CMD_UI_TogglePanel(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (!_playerInfo.ContainsKey(player.userID)) _playerInfo.Add(player.userID,false);
            else
            {
                if (_playerInfo[player.userID])
                {
                    _playerInfo[player.userID] =false;
                    Draw_UIMain(player, config.PanelAnchor);
                }
                else
                {
                    _playerInfo[player.userID] = true;
                    Draw_UIMain(player, config.PanelAnchor);
                }
            }
            return;
        }
        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                PanelAnchor = "0.005 0.99",
                PanelOffsetMin = "0 -75",
                PanelOffsetMax = "180 0",
                PanelText = "<size=23>Игровой Сервер #1</size>\n<size=18>Открыть корзину</size>",
                TextPos = "MiddleLeft",
                PanelColor = "#E6E6E6",
                PanelAlpha = 0.5f,
                PanelCmd = "chat.say /store",
                ArrowMode = true,
                ArrowColor = "#FFFFFF",
                ButtonAnchor = "1 0",
                ButtonOffsetMin = "0 10",
                ButtonOffsetMax = "35 45",
                ButtonTextOn = "<size=26>▶</size>",
                ButtonTextOff = "<size=26>◀</size>",
                _listPanels = new List<AddtionalPanel> {
                    new AddtionalPanel {
                        PanelAnchor = "1 1", PanelOffsetMin = "0 -35", PanelOffsetMax = "35 0", PanelImageUrl = "https://steamcommunity-a.akamaihd.net/economy/image/-9a81dlWLwJ2UUGcVs_nsVtzdOEdtWwKGZZLQHTxDZ7I56KU0Zwwo4NUX4oFJZEHLbXU5A1PIYQNqhpOSV-fRPasw8rsUFJ5KBFZv668FFY4naeaJGhGtdnmx4Tek_bwY-iFlGlUsJMp3LuTot-mjFGxqUttZ2r3d4eLMlhpnZPxZK0/256fx256f", PanelCmd = "chat.say /case1", Perm = "minipanel.case"
                    },
                    new AddtionalPanel {
                        PanelAnchor = "1 1", PanelOffsetMin = "35 -35", PanelOffsetMax = "80 0", PanelImageUrl = "<color=#F5DA81>Кейсы</color>", PanelCmd = "chat.say /case2", Perm = null
                    }
                }
            };
        }
        public static string GetColor(string hex,float alpha = 1f)
        {
            var color = ColorTranslator.FromHtml(hex);
            var r = Convert.ToInt16(color.R) / 255f;
            var g = Convert.ToInt16(color.G) / 255f;
            var b = Convert.ToInt16(color.B) / 255f;
            //var a = Convert.ToInt16(color.A) / 1164f;
            return $"{r} {g} {b} {alpha}";
        }

        private TextAnchor GetPos(string pos)
        {
            switch (pos)
            {
                case "LowerCenter":
                    return TextAnchor.LowerCenter;
                case "LowerLeft":
                    return TextAnchor.LowerLeft;
                case "LowerRight":
                    return TextAnchor.LowerRight;
                case "MiddleCenter":
                    return TextAnchor.MiddleCenter;
                case "MiddleLeft":
                    return TextAnchor.MiddleLeft;
                case "MiddleRight":
                    return TextAnchor.MiddleRight;
                case "UpperCenter":
                    return TextAnchor.UpperCenter;
                case "UpperLeft":
                    return TextAnchor.UpperLeft;
                case "UpperRight":
                    return TextAnchor.UpperRight;
                default: return TextAnchor.MiddleCenter;
            }
        }
    }
}