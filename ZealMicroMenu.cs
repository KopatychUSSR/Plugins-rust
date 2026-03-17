using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Globalization;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using Color = UnityEngine.Color;

namespace Oxide.Plugins
{
    [Info("ZealMicroMenu", "https://topplugin.ru/", "1.0.3")]
    [Description("Микро меню для сервера Rust")]
    class ZealMicroMenu : RustPlugin
    {
        #region [Reference] / [Запросы]

        [PluginReference] Plugin ImageLibrary;

        private string GetImg(string name)
        {
            return (string) ImageLibrary?.Call("GetImage", name) ?? "";
        }

        #endregion

        #region [Classes] / [Классы] 

        public class ButtonElement
        {
            [JsonProperty(PropertyName = "Текст кнопки")]
            public string text;

            [JsonProperty(PropertyName = "Команда кнопки")]
            public string command;

            [JsonProperty(PropertyName = "Цвет иконки кнопки, и текста")]
            public string color;

            [JsonProperty(PropertyName = "Иконка кнопки (SPRITE/URL)")]
            public string Image;
        }

        #endregion

        #region [Configuraton] / [Конфигурация]

        static public ConfigData config;

        public class ConfigData
        {
            [JsonProperty(PropertyName = "ZealMicroMenu")]
            public MicroMenu ZealMicroMenu = new MicroMenu();

            public class MicroMenu
            {
                [JsonProperty(PropertyName = "Текст кнопки открытия микро меню")]
                public string MenuTXT;

                [JsonProperty(PropertyName = "Логотип меню (URL/SPRITE)")]
                public string MenuIC;

                [JsonProperty(PropertyName = "Цвет иконки меню (HEX)")]
                public string ColorMenuIC;

                [JsonProperty(PropertyName = "Положение меню по X (AnchorMin)")]
                public string MPosXMin;

                [JsonProperty(PropertyName = "Положение меню по X (AnchorMax)")]
                public string MPosXMax;

                [JsonProperty(PropertyName = "Положение меню по Y (AnchorMin)")]
                public string MPosYMin;

                [JsonProperty(PropertyName = "Положение меню по Y (AnchorMax)")]
                public string MPosYMax;

                [JsonProperty(PropertyName = "Список кнопок")]
                public List<ButtonElement> ButtonElements = new List<ButtonElement>();
            }
        }

        public ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                ZealMicroMenu = new ConfigData.MicroMenu
                {
                    MenuTXT = $"<b>{ConVar.Server.hostname}</b>\n<size=11>НАЖМИ, ЧТОБЫ ОТКРЫТЬ МЕНЮ</size>",
                    MenuIC = "assets/icons/broadcast.png",
                    ColorMenuIC = "#C4C4C4FF",
                    MPosXMin = "0.003653084",
                    MPosXMax = "0.1718864",
                    MPosYMin = "0.925",
                    MPosYMax = "0.999",
                    ButtonElements =
                    {
                        new ButtonElement
                        {
                            text = "МАГАЗИН",
                            color = "#C4C4C4FF",
                            command = "",
                            Image = "assets/icons/store.png"
                        },
                        new ButtonElement
                        {
                            text = "КАРТА ПОКРЫТИЯ РАДИАЦИИ",
                            color = "#C4C4C4FF",
                            command = "",
                            Image = "assets/icons/radiation.png"
                        },
                        new ButtonElement
                        {
                            text = "ЗОО-ПАРК",
                            color = "#C4C4C4FF",
                            command = "",
                            Image = "assets/icons/bite.png"
                        },
                        new ButtonElement
                        {
                            text = "АПТЕКА",
                            color = "#C4C4C4FF",
                            command = "",
                            Image = "assets/icons/pills.png"
                        }
                    }
                }
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();
            }
            catch
            {
                LoadDefaultConfig();
            }

            SaveConfig();
        } 

        protected override void LoadDefaultConfig()
        {
            PrintError("Файл конфигурации поврежден (или не существует), создан новый!");
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion

        #region [Dictionary/Vars] / [Словари/Переменные]

        private string Sharp = "assets/content/ui/ui.background.tile.psd";
        private string Blur = "assets/content/ui/uibackgroundblur.mat";
        private string radial = "assets/content/ui/ui.background.transparent.radial.psd";
        private string regular = "robotocondensed-regular.ttf";

        bool hide_open = false;

        public string Layer = "BoxMicroMenu";

        #endregion

        #region [DrawUI] / [Показ UI]  

        void DrawUI_MicroMenu(BasePlayer player)
        {
            CuiElementContainer Gui = new CuiElementContainer();
            CuiHelper.DestroyUi(player, Layer);
            var cfg = config.ZealMicroMenu;

            Gui.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image =
                {
                    Color = "0 0 0 0"
                },
                RectTransform =
                {
                    AnchorMin = $"{cfg.MPosXMin} {cfg.MPosYMin}",
                    AnchorMax = $"{cfg.MPosXMax} {cfg.MPosYMax}"
                }
            }, "Overlay", Layer);

            if (config.ZealMicroMenu.MenuIC.Contains("assets"))
            {
                Gui.Add(new CuiElement
                {
                    Name = "MenuIC",
                    Parent = Layer,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = HexToRustFormat(config.ZealMicroMenu.ColorMenuIC),
                            Sprite = config.ZealMicroMenu.MenuIC
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.01857769 0.1749995",
                            AnchorMax = "0.1795644 0.8249998"
                        }
                    }
                });
            }
            else
            {
                Gui.Add(new CuiElement
                {
                    Name = "MenuIC",
                    Parent = Layer,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Color = HexToRustFormat("#C4C4C4FF"),
                            Png = GetImg(config.ZealMicroMenu.MenuIC)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.01857769 0.1749995",
                            AnchorMax = "0.1795644 0.8249998"
                        }
                    }
                });
            }

            Gui.Add(new CuiButton
            {
                Button =
                {
                    Command = "micromenu",
                    Color = "0 0 0 0"
                },
                Text =
                {
                    Align = TextAnchor.MiddleLeft,
                    Color = HexToRustFormat("#C4C4C4FF"),
                    FontSize = 17,
                    Text = config.ZealMicroMenu.MenuTXT,
                    Font = "robotocondensed-regular.ttf"
                },
                RectTransform =
                {
                    AnchorMin = "0.2024794 0",
                    AnchorMax = "1 1"
                }
            }, Layer, "ButtonOpen");

            if (hide_open == false)
            {
                int y = 0, num = 1;
                foreach (var button in config.ZealMicroMenu.ButtonElements)
                {
                    if (button.Image.Contains("assets"))
                    {
                        Gui.Add(new CuiElement
                        {
                            Name = "ButtonIC" + num,
                            Parent = Layer,
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    Color = HexToRustFormat(button.color),
                                    Sprite = button.Image,
                                    FadeIn = 0.1f + (num * 0.1f)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = $"{0.05882474} {-0.36 - (y * 0.5)}",
                                    AnchorMax = $"{0.1393183} {-0.037 - (y * 0.5)}"
                                }
                            }
                        });
                    }
                    else
                    {
                        Gui.Add(new CuiElement
                        {
                            Name = "ButtonIC" + num,
                            Parent = Layer,
                            Components =
                            {
                                new CuiRawImageComponent
                                {
                                    Color = HexToRustFormat(button.color),
                                    Png = GetImg(button.Image),
                                    FadeIn = 0.1f + (num * 0.1f)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = $"{0.05882474} {-0.36 - (y * 0.5)}",
                                    AnchorMax = $"{0.1393183} {-0.037 - (y * 0.5)}"
                                }
                            }
                        });
                    }

                    Gui.Add(new CuiButton
                        {
                            Button =
                            {
                                Command = button.command,
                                Color = "0 0 0 0",
                            },
                            Text =
                            {
                                Align = TextAnchor.MiddleLeft,
                                Color = HexToRustFormat(button.color),
                                FontSize = 15,
                                Text = $"{button.text}",
                                Font = "robotocondensed-regular.ttf",
                                FadeIn = 0.1f + (num * 0.1f)
                            },
                            RectTransform =
                            {
                                AnchorMin = $"{0.2012334} {-0.412 - (y * 0.5)}",
                                AnchorMax = $"{1} {0.05 - (y * 0.5)}"
                            }
                        }, Layer, "but" + num);
                    y++;
                    num++;
                }

                hide_open = true;
            }
            else
            {
                for (int i = 0; i <= config.ZealMicroMenu.ButtonElements.Count; i++)
                {
                    CuiHelper.DestroyUi(player, "but" + i);
                }

                hide_open = false;
            }

            CuiHelper.AddUi(player, Gui);
        }

        #endregion

        #region [Hooks] / [Крюки]

        void OnServerInitialized()
        {
            if (!ImageLibrary)
            {
                PrintError("На сервере не установлен плагин [ImageLibrary]");
                return;
            }

            foreach (var button in config.ZealMicroMenu.ButtonElements)
            {
                if (!button.Image.Contains("assets"))
                {
                    ImageLibrary.Call("AddImage", button.Image, button.Image);
                } 
            }

            if (!config.ZealMicroMenu.MenuIC.Contains("assets"))
            {
                ImageLibrary.Call("AddImage", config.ZealMicroMenu.MenuIC, config.ZealMicroMenu.MenuIC);
            }
            
            foreach (var player in BasePlayer.activePlayerList)
            {
                DrawUI_MicroMenu(player);
            }

            Puts(" ");
            Puts("----------------------Контакты----------------------");
            Puts(" ");
            Puts(" Вконтакте : vk.com/rustnastroika");
            Puts(" VK : vk.com/nastroikarust");
            Puts(" Группа вконтакте : vk.com/top__plugin");
            Puts(" ");
            Puts("---^-^----Приятного пользования----^-^---");
            Puts(" ");
        }

        void OnPlayerConnected(BasePlayer player)
        {
            NextTick(() => DrawUI_MicroMenu(player));
        }

        #endregion

        #region [ChatCommand] / [Чат команды]

        [ConsoleCommand("micromenu")]
        private void DrawUI(ConsoleSystem.Arg args)
        {
            if (!args.Player()) return;
            DrawUI_MicroMenu(args.Player());
        }

        #endregion

        #region [Helpers] / [Вспомогательный код]

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
            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }

        #endregion
    }
}