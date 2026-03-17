using System;
using System.Globalization;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("FuryPanel", "ToPPlugins | topplugin.ru", "1.0.0")]
    public class FuryPanel : RustPlugin
    {
        [PluginReference] private Plugin ImageLibrary;
        string Layer = "FuryPanel_Layer";

        #region Config [Конфигурация плагина]

        private PluginConfig cfg;

        public class PluginConfig
        {
            [JsonProperty("Настройки")]
            public Setting Settings = new Setting();

            [JsonProperty("Иконки")]
            public Icons Icon = new Icons();

            [JsonProperty("Команды")]
            public Commands Command = new Commands();

            [JsonProperty("Сообщения")]
            public MessageSettings SettingsMessages = new MessageSettings();


            public class Setting
            {
                [JsonProperty("Название сервера")]
                public string ServerName = "NAME #1 NO LIMIT";
            }

            public class Icons
            {
                [JsonProperty("Иконка корзины")]
                public string Cart = "https://i.imgur.com/0Er6sl2.png";
                [JsonProperty("Иконка онлайна")]
                public string Online = "https://i.imgur.com/JUNDcSL.png";
                [JsonProperty("Иконка часов")]
                public string Clock = "https://i.imgur.com/SYDxVtz.png";
                [JsonProperty("Иконка аирдропа")]
                public string Plane = "https://i.imgur.com/bgCqca3.png";
                [JsonProperty("Иконка чинука")]
                public string Ch47 = "https://i.imgur.com/VLUbJ4L.png";
                [JsonProperty("Иконка корабля")]
                public string Cargoship = "https://i.imgur.com/P3nJFeW.png";
                [JsonProperty("Иконка вертолёта")]
                public string Heli = "https://i.imgur.com/hb9AgPM.png";
                [JsonProperty("Иконка танка")]
                public string Bradley = "https://i.imgur.com/sqLKzTj.png";
            }

            public class Commands
            {
                [JsonProperty("Команда которая выполняется при нажатии на корзину")]
                public string Cart = "/shop";
                [JsonProperty("Команда которая выполняется при нажатии на /INFO")]
                public string Info = "/info";
            }

            public class MessageSettings
            {
                [JsonProperty("Время обновления сообщений")]
                public float RefreshTimer = 30f;
                [JsonProperty("Список сообщений", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<string> Messages = new List<string>
                {
                    "<color=lime>Пример сообщения 1</color>",
                    "<color=red>Пример сообщения 2</color>",
                    "<color=blue>Пример сообщения 3</color>"
                };
            }
        }

        private void Init()
        {
            cfg = Config.ReadObject<PluginConfig>();
            Config.WriteObject(cfg);

            PrintWarning("Плагин написан форумом Topplugin. Спасибо за приобретение! Ждём вас ещё раз!");
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(new PluginConfig(), true);
        }

        #endregion

        #region Hooks [Хуки]

        void Unload()
        {
            InvokeHandler.Instance.CancelInvoke(UpdateTime);
            InvokeHandler.Instance.CancelInvoke(DrawNewMessage);
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Layer);
                CuiHelper.DestroyUi(player, "Message");
            }
        }

        private void OnServerInitialized()
        {
            if (!plugins.Exists("ImageLibrary"))
            {
                PrintWarning("Плагин 'ImageLibrary' не загружен, дальнейшая работа плагина невозможна!");
                return;
            }

            ImageLibrary.Call("AddImage", cfg.Icon.Cart, "Cart");
            ImageLibrary.Call("AddImage", cfg.Icon.Online, "Online");
            ImageLibrary.Call("AddImage", cfg.Icon.Clock, "Clock");
            ImageLibrary.Call("AddImage", cfg.Icon.Plane, "Plane");
            ImageLibrary.Call("AddImage", cfg.Icon.Ch47, "Ch47");
            ImageLibrary.Call("AddImage", cfg.Icon.Cargoship, "Cargoship");
            ImageLibrary.Call("AddImage", cfg.Icon.Heli, "Heli");
            ImageLibrary.Call("AddImage", cfg.Icon.Bradley, "Bradley");

            InvokeHandler.Instance.InvokeRepeating(UpdateTime, 10f, 10f);
            InvokeHandler.Instance.InvokeRepeating(DrawNewMessage, cfg.SettingsMessages.RefreshTimer, cfg.SettingsMessages.RefreshTimer);

            foreach (var player in BasePlayer.activePlayerList) OnPlayerConnected(player);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerConnected(player));
                return;
            }

            timer.Once(1f, () =>
            {
                foreach (var players in BasePlayer.activePlayerList)
                {
                    DrawMessage(players);
                    DrawMenu(players);
                }
            });
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            timer.Once(1f, () => { foreach (var players in BasePlayer.activePlayerList) DrawMenu(players); });
        }

        #endregion

        #region Methods [Методы и доп. хуки, определения]

        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity == null) return;
            if (entity is CargoPlane || entity is BaseHelicopter || entity is CargoShip || entity is CH47Helicopter || entity is BradleyAPC)
            {
                var tag = entity is CargoPlane ? "plane" : entity is BradleyAPC ? "tank" : entity is BaseHelicopter ? "heli" : entity is CargoShip ? "cargo" : entity is CH47Helicopter ? "ch47" : "";
                timer.Once(1f, () => { foreach (var players in BasePlayer.activePlayerList) DrawEvents(players, tag); });
            }
            else return;
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity == null || entity.net == null) return;
            if (entity is CargoPlane)
                foreach (var player in BasePlayer.activePlayerList) DrawEvents(player, "plane");
            if (entity is BaseHelicopter)
                foreach (var player in BasePlayer.activePlayerList) DrawEvents(player, "heli");
            if (entity is CargoShip)
                foreach (var player in BasePlayer.activePlayerList) DrawEvents(player, "cargo");
            if (entity is CH47Helicopter)
                foreach (var player in BasePlayer.activePlayerList) DrawEvents(player, "ch47");
            if (entity is BradleyAPC)
                foreach (var player in BasePlayer.activePlayerList) DrawEvents(player, "tank");
            else return;
        }

        bool HasHeli()
        {
            foreach (var check in BaseNetworkable.serverEntities)
                if (check is BaseHelicopter)
                    return true;
            return false;
        }
        bool HasPlane()
        {
            foreach (var check in BaseNetworkable.serverEntities)
                if (check is CargoPlane)
                    return true;
            return false;
        }
        bool HasCargo()
        {
            foreach (var check in BaseNetworkable.serverEntities)
                if (check is CargoShip)
                    return true;
            return false;
        }

        bool HasCh47()
        {
            foreach (var check in BaseNetworkable.serverEntities)
                if (check is CH47Helicopter)
                    return true;
            return false;
        }

        bool HasBradley()
        {
            foreach (var check in BaseNetworkable.serverEntities)
                if (check is BradleyAPC)
                    return true;
            return false;
        }

        #endregion

        #region UI

        void DrawNewMessage()
        {
            foreach (var players in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(players, "Message" + ".Message");
                var container = new CuiElementContainer();
                container.Add(new CuiElement
                {
                    Parent = "Message",
                    Name = "Message" + ".Message",
                    FadeOut = 0.2f,
                    Components =
                    {
                        new CuiTextComponent { Text = cfg.SettingsMessages.Messages[new System.Random().Next(cfg.SettingsMessages.Messages.Count)], Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "RobotoCondensed-bold.ttf" },
                        new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"},
                        new CuiOutlineComponent {Color = "0 0 0 1", Distance = "0.6 0.6"},
                    }
                });
                CuiHelper.AddUi(players, container);
            }
        }

        void DrawMessage(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "Message");
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = { Color = HexToRustFormat("#FFFFFF00") },
                RectTransform = { AnchorMin = "0.3453124 -0.0009259344", AnchorMax = "0.6416667 0.0287037" },
                CursorEnabled = false,
            }, "Hud", "Message");

            container.Add(new CuiElement
            {
                Parent = "Message",
                Name = "Message" + ".Message",
                FadeOut = 0.2f,
                Components =
                {
                    new CuiTextComponent { Text = cfg.SettingsMessages.Messages[new System.Random().Next(cfg.SettingsMessages.Messages.Count)], Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "RobotoCondensed-bold.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"},
                    new CuiOutlineComponent {Color = "0 0 0 1", Distance = "0.6 0.6"},
                }
            });
            CuiHelper.AddUi(player, container);
        }

        void UpdateTime()
        {
            foreach (var players in BasePlayer.activePlayerList)
            {
                var time = TOD_Sky.Instance.Cycle.DateTime.ToString("HH:mm");
                CuiHelper.DestroyUi(players, Layer + ".Clock" + ".Clock");
                var container = new CuiElementContainer();
                container.Add(new CuiElement
                {
                    Parent = Layer + ".Clock",
                    Name = Layer + ".Clock" + ".Clock",
                    FadeOut = 0.2f,
                    Components =
                    {
                        new CuiTextComponent { Text = time, Color = HexToRustFormat("#FFFFFFFF"), Align = TextAnchor.MiddleCenter, FontSize = 11, Font = "RobotoCondensed-bold.ttf" },
                        new CuiRectTransformComponent {AnchorMin = "0.1935486 0", AnchorMax = "0.9569898 1"},
                        new CuiOutlineComponent {Color = "0 0 0 1", Distance = "0.6 0.6"},
                    }
                });
                CuiHelper.AddUi(players, container);
            }
        }

        void DrawEvents(BasePlayer player, string name)
        {
            var container = new CuiElementContainer();
            if (name == "plane")
            {
                foreach (var players in BasePlayer.activePlayerList)
                {
                    CuiHelper.DestroyUi(players, Layer + ".Plane" + ".Plane");
                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Plane",
                        Name = Layer + ".Plane" + ".Plane",
                        FadeOut = 0.1f,
                        Components =
                        {
                            new CuiRawImageComponent { FadeIn = 0.1f, Png = (string)ImageLibrary.Call("GetImage", "Plane"), Color = HasPlane() ? HexToRustFormat("#28FF00FF") : HexToRustFormat("#FFFFFFFF") },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },
                            new CuiOutlineComponent {Color = "0 0 0 0.3", Distance = "1 1"},
                        }
                    });
                }
            }
            if (name == "ch47")
            {
                foreach (var players in BasePlayer.activePlayerList)
                {
                    CuiHelper.DestroyUi(players, Layer + ".Ch47" + ".Ch47");
                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Ch47",
                        Name = Layer + ".Ch47" + ".Ch47",
                        FadeOut = 0.1f,
                        Components =
                        {
                            new CuiRawImageComponent { FadeIn = 0.1f, Png = (string)ImageLibrary.Call("GetImage", "Ch47"), Color = HasCh47() ? HexToRustFormat("#FF0000FF") : HexToRustFormat("#FFFFFFFF") },
                            new CuiRectTransformComponent { AnchorMin = "0.04545468 0.04545469", AnchorMax = "0.9090936 0.9090939" },
                            new CuiOutlineComponent {Color = "0 0 0 0.3", Distance = "1 1"},
                        }
                    });
                }
            }
            if (name == "cargo")
            {
                foreach (var players in BasePlayer.activePlayerList)
                {
                    CuiHelper.DestroyUi(players, Layer + ".Cargo" + ".Cargo");
                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Cargo",
                        Name = Layer + ".Cargo" + ".Cargo",
                        FadeOut = 0.1f,
                        Components =
                        {
                            new CuiRawImageComponent { FadeIn = 0.1f , Png = (string)ImageLibrary.Call("GetImage", "Cargoship"), Color = HasCargo() ? HexToRustFormat("#28FF00FF") : HexToRustFormat("#FFFFFFFF") },
                            new CuiRectTransformComponent { AnchorMin = "0.04545468 0.06413816", AnchorMax = "0.9090936 0.9277765" },
                            new CuiOutlineComponent {Color = "0 0 0 0.3", Distance = "1 1"},
                        }
                    });
                }
            }
            if (name == "heli")
            {
                foreach (var players in BasePlayer.activePlayerList)
                {
                    CuiHelper.DestroyUi(players, Layer + ".Heli" + ".Heli");
                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Heli",
                        Name = Layer + ".Heli" + ".Heli",
                        FadeOut = 0.1f,
                        Components =
                        {
                            new CuiRawImageComponent { FadeIn = 0.1f, Png = (string)ImageLibrary.Call("GetImage", "Heli"), Color = HasHeli() ? HexToRustFormat("#FF0000FF") : HexToRustFormat("#FFFFFFFF") },
                            new CuiRectTransformComponent { AnchorMin = "0.136364 0.1550476", AnchorMax = "0.8636389 0.8823226" },
                            new CuiOutlineComponent {Color = "0 0 0 0.3", Distance = "1 1"},
                        }
                    });
                }
            }
            if (name == "tank")
            {
                foreach (var players in BasePlayer.activePlayerList)
                {
                    CuiHelper.DestroyUi(players, Layer + ".Tank" + ".Bradley");
                    container.Add(new CuiElement
                    {
                        Parent = Layer + ".Bradley",
                        Name = Layer + ".Bradley" + ".Bradley",
                        FadeOut = 0.1f,
                        Components =
                        {
                            new CuiRawImageComponent { FadeIn = 0.1f, Png = (string)ImageLibrary.Call("GetImage", "Bradley"), Color = HasBradley() ? HexToRustFormat("#FF0000FF") : HexToRustFormat("#FFFFFFFF") },
                            new CuiRectTransformComponent { AnchorMin = "0.04545468 0.1550476", AnchorMax = "0.9090958 0.8823226" },
                            new CuiOutlineComponent {Color = "0 0 0 0.3", Distance = "1 1"},
                        }
                    });
                }
            }
            CuiHelper.AddUi(player, container);
        }

        private void DrawMenu(BasePlayer player)
        {
            var online = BasePlayer.activePlayerList.Count().ToString();
            var time = TOD_Sky.Instance.Cycle.DateTime.ToString("HH:mm");

            CuiHelper.DestroyUi(player, Layer);
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = { Color = HexToRustFormat("#FFFFFF00") },
                RectTransform = { AnchorMin = "0.003125004 0.9277778", AnchorMax = "0.1322917 0.9999996" },
                CursorEnabled = false,
            }, "Hud", Layer);


            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".Store",
                Components =
                {
                    new CuiImageComponent { Color = HexToRustFormat("#FFFFFF4A") },
                    new CuiRectTransformComponent { AnchorMin = "2.980232E-08 0.3589774", AnchorMax = "0.2016129 1" },
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer + ".Store",
                Components =
                {
                    new CuiRawImageComponent { Color = HexToRustFormat("#FFFFFFFF"), Png = (string)ImageLibrary.Call("GetImage", "Cart") },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },
                    new CuiOutlineComponent {Color = "0 0 0 1", Distance = "0.6 0.6"},
                }
            });
            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = $"chat.say {cfg.Command.Cart}" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = { Text = "" }
            }, Layer + ".Store");


            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".ServerName",
                Components =
                {
                    new CuiImageComponent { Color = HexToRustFormat("#FFFFFF4A") },
                    new CuiRectTransformComponent { AnchorMin = "0.2258063 0.7179539", AnchorMax = "0.9999999 1" },
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer + ".ServerName",
                Components =
                {
                    new CuiTextComponent { Text = cfg.Settings.ServerName, Color = HexToRustFormat("#FFFFFFFF"), Align = TextAnchor.MiddleLeft, FontSize = 11, Font = "RobotoCondensed-bold.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0.02083333 0", AnchorMax = "0.9999999 0.8636341"},
                    new CuiOutlineComponent {Color = "0 0 0 1", Distance = "0.6 0.6"},
                }
            });


            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".Info",
                Components =
                {
                    new CuiImageComponent { Color = HexToRustFormat("#FFFFFF4A") },
                    new CuiRectTransformComponent { AnchorMin = "2.980232E-08 1.104549E-06", AnchorMax = "0.2016128 0.282054" }
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer + ".Info",
                Components =
                {
                    new CuiTextComponent { Text = cfg.Command.Info.ToUpper(), Color = HexToRustFormat("#FFFFFFFF"), Align = TextAnchor.MiddleCenter, FontSize = 11, Font = "RobotoCondensed-bold.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1"},
                    new CuiOutlineComponent {Color = "0 0 0 1", Distance = "0.6 0.6"},
                }
            });
            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0", Command = $"chat.say {cfg.Command.Info}" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = { Text = "" }
            }, Layer + ".Info");


            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".Clock",
                Components =
                {
                    new CuiImageComponent { Color = HexToRustFormat("#FFFFFF4A") },
                    new CuiRectTransformComponent { AnchorMin = "0.6249995 0.3589774", AnchorMax = "0.9999996 0.6410231" }
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer + ".Clock",
                Components =
                {
                    new CuiRawImageComponent { Color = HexToRustFormat("#FFFFFFFF"), Png = (string)ImageLibrary.Call("GetImage", "Clock") },
                    new CuiRectTransformComponent { AnchorMin = "0.03225806 0.1363633", AnchorMax = "0.204301 0.8636341" },
                    new CuiOutlineComponent {Color = "0 0 0 0.5", Distance = "0.1 0.1"},
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer + ".Clock",
                Name = Layer + ".Clock" + ".Clock",
                FadeOut = 0.2f,
                Components =
                {
                    new CuiTextComponent { Text = time, Color = HexToRustFormat("#FFFFFFFF"), Align = TextAnchor.MiddleCenter, FontSize = 11, Font = "RobotoCondensed-bold.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0.1935486 0", AnchorMax = "0.9569898 1"},
                    new CuiOutlineComponent {Color = "0 0 0 1", Distance = "0.6 0.6"},
                }
            });


            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".Online",
                Components =
                {
                    new CuiImageComponent { Color = HexToRustFormat("#FFFFFF4A") },
                    new CuiRectTransformComponent { AnchorMin = "0.2258063 0.3589774", AnchorMax = "0.6008065 0.6410231" }
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer + ".Online",
                Components =
                {
                    new CuiRawImageComponent { Color = HexToRustFormat("#FFFFFFFF"), Png = (string)ImageLibrary.Call("GetImage", "Online") },
                    new CuiRectTransformComponent { AnchorMin = "0.03225806 0.1363633", AnchorMax = "0.204301 0.8636341" },
                    new CuiOutlineComponent {Color = "0 0 0 0.5", Distance = "0.1 0.1"},
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer + ".Online",
                Components =
                {
                    new CuiTextComponent { Text = online + "/" + ConVar.Server.maxplayers, Color = HexToRustFormat("#FFFFFFFF"), Align = TextAnchor.MiddleCenter, FontSize = 11, Font = "RobotoCondensed-bold.ttf" },
                    new CuiRectTransformComponent {AnchorMin = "0.2150541 0", AnchorMax = "0.9784948 1"},
                    new CuiOutlineComponent {Color = "0 0 0 1", Distance = "0.6 0.6"},
                }
            });


            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".Plane",
                Components =
                {
                    new CuiImageComponent { Color = HexToRustFormat("#FFFFFF4A") },
                    new CuiRectTransformComponent { AnchorMin = "0.2258064 1.104549E-06", AnchorMax = "0.314516 0.282054" }
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer + ".Plane",
                Name = Layer + ".Plane" + ".Plane",
                FadeOut = 0.1f,
                Components =
                {
                    new CuiRawImageComponent { FadeIn = 0.1f, Png = (string)ImageLibrary.Call("GetImage", "Plane"), Color = HasPlane() ? HexToRustFormat("#28FF00FF") : HexToRustFormat("#FFFFFFFF") },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },
                    new CuiOutlineComponent {Color = "0 0 0 0.3", Distance = "1 1"},
                }
            });


            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".Ch47",
                Components =
                {
                    new CuiImageComponent { Color = HexToRustFormat("#FFFFFF4A") },
                    new CuiRectTransformComponent { AnchorMin = "0.3387095 1.104549E-06", AnchorMax = "0.4274191 0.282054" }
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer + ".Ch47",
                Name = Layer + ".Ch47" + ".Ch47",
                FadeOut = 0.1f,
                Components =
                {
                    new CuiRawImageComponent { FadeIn = 0.1f, Png = (string)ImageLibrary.Call("GetImage", "Ch47"), Color = HasCh47() ? HexToRustFormat("#FF0000FF") : HexToRustFormat("#FFFFFFFF") },
                    new CuiRectTransformComponent { AnchorMin = "0.04545468 0.04545469", AnchorMax = "0.9090936 0.9090939" },
                    new CuiOutlineComponent {Color = "0 0 0 0.3", Distance = "1 1"},
                }
            });


            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".Cargo",
                Components =
                {
                    new CuiImageComponent { Color = HexToRustFormat("#FFFFFF4A") },
                    new CuiRectTransformComponent { AnchorMin = "0.4516126 1.104549E-06", AnchorMax = "0.5403222 0.282054" }
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer + ".Cargo",
                Name = Layer + ".Cargo" + ".Cargo",
                FadeOut = 0.1f,
                Components =
                {
                    new CuiRawImageComponent { FadeIn = 0.1f , Png = (string)ImageLibrary.Call("GetImage", "Cargoship"), Color = HasCargo() ? HexToRustFormat("#28FF00FF") : HexToRustFormat("#FFFFFFFF") },
                    new CuiRectTransformComponent { AnchorMin = "0.04545468 0.06413816", AnchorMax = "0.9090936 0.9277765" },
                    new CuiOutlineComponent {Color = "0 0 0 0.3", Distance = "1 1"},
                }
            });


            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".Heli",
                Components =
                {
                    new CuiImageComponent { Color = HexToRustFormat("#FFFFFF4A") },
                    new CuiRectTransformComponent { AnchorMin = "0.5645157 1.104549E-06", AnchorMax = "0.6532253 0.282054" }
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer + ".Heli",
                Name = Layer + ".Heli" + ".Heli",
                FadeOut = 0.1f,
                Components =
                {
                    new CuiRawImageComponent { FadeIn = 0.1f, Png = (string)ImageLibrary.Call("GetImage", "Heli"), Color = HasHeli() ? HexToRustFormat("#FF0000FF") : HexToRustFormat("#FFFFFFFF") },
                    new CuiRectTransformComponent { AnchorMin = "0.136364 0.1550476", AnchorMax = "0.8636389 0.8823226" },
                    new CuiOutlineComponent {Color = "0 0 0 0.3", Distance = "1 1"},
                }
            });


            container.Add(new CuiElement
            {
                Parent = Layer,
                Name = Layer + ".Bradley",
                Components =
                {
                    new CuiImageComponent { Color = HexToRustFormat("#FFFFFF4A") },
                    new CuiRectTransformComponent { AnchorMin = "0.6774188 1.104549E-06", AnchorMax = "0.7661284 0.282054" }
                }
            });
            container.Add(new CuiElement
            {
                Parent = Layer + ".Bradley",
                Name = Layer + ".Bradley" + ".Bradley",
                FadeOut = 0.1f,
                Components =
                {
                    new CuiRawImageComponent { FadeIn = 0.1f, Png = (string)ImageLibrary.Call("GetImage", "Bradley"), Color = HasBradley() ? HexToRustFormat("#FF0000FF") : HexToRustFormat("#FFFFFFFF") },
                    new CuiRectTransformComponent { AnchorMin = "0.04545468 0.1550476", AnchorMax = "0.9090958 0.8823226" },
                    new CuiOutlineComponent {Color = "0 0 0 0.9", Distance = "0.1 0.1"},
                }
            });

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Helpers [Доп. методы]

        private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex)) hex = "#FFFFFFFF";
            var str = hex.Trim('#');
            if (str.Length == 6) str += "FF";
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