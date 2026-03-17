using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Facepunch.Extend;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("LotterySystem", "LAGZYA", "1.0.5")]
    public class LotterySystem : RustPlugin
    {
        #region Data

        Dictionary<ulong, UserData> _playerData = new Dictionary<ulong, UserData>();
        private static Configs cfg { get; set; }

        private class Configs
        {
            [JsonProperty("Время перезарядки")] public int kd = 86400;
            [JsonProperty("Цвет обводки")] public string cvet = "#00ffcc";
            [JsonProperty("Дроп")] public List<DropItems> _Drop;


            public static Configs GetNewConf()
            {
                var newconfig = new Configs();
                newconfig._Drop = new List<DropItems>()
                {
                    new DropItems()
                    {
                        ShortName = "rifle.ak",
                        Amount = 1,
                        Command = "",
                        SkinId = 0,
                        Url = ""
                    },
                    new DropItems()
                    {
                        ShortName = "wood",
                        Amount = 5000,
                        Command = "",
                        SkinId = 0,
                        Url = ""
                    },
                    new DropItems()
                    {
                        ShortName = "stones",
                        Amount = 2500,
                        Command = "",
                        SkinId = 0,
                        Url = ""
                    },
                    new DropItems()
                    {
                        ShortName = "metal.fragments",
                        Amount = 1500,
                        Command = "",
                        SkinId = 0,
                        Url = ""
                    },
                    new DropItems()
                    {
                        ShortName = "metal.refined",
                        Amount = 100,
                        Command = "",
                        SkinId = 0,
                        Url = ""
                    },
                    new DropItems()
                    {
                        ShortName = "rifle.bolt",
                        Amount = 1,
                        Command = "",
                        SkinId = 0,
                        Url = ""
                    },
                    new DropItems()
                    {
                        ShortName = "pistol.revolver",
                        Amount = 1,
                        Command = "",
                        SkinId = 0,
                        Url = ""
                    },
                    new DropItems()
                    {
                        ShortName = "rifle.l96",
                        Amount = 1,
                        Command = "",
                        SkinId = 0,
                        Url = ""
                    },
                    new DropItems()
                    {
                        ShortName = "door.hinged.toptier",
                        Amount = 1,
                        Command = "",
                        SkinId = 0,
                        Url = ""
                    },
                    new DropItems()
                    {
                        ShortName = "ladder.wooden.wall",
                        Amount = 5,
                        Command = "",
                        SkinId = 0,
                        Url = ""
                    },
                    new DropItems()
                    {
                        ShortName = "wall.frame.garagedoor",
                        Amount = 2,
                        Command = "",
                        SkinId = 0,
                        Url = ""
                    },
                    new DropItems()
                    {
                        ShortName = "hazmatsuit",
                        Amount = 1,
                        Command = "",
                        SkinId = 0,
                        Url = ""
                    },
                    new DropItems()
                    {
                        ShortName = "metalpipe",
                        Amount = 5,
                        Command = "",
                        SkinId = 0,
                        Url = ""
                    },
                    new DropItems()
                    {
                        ShortName = "roadsigns",
                        Amount = 5,
                        Command = "",
                        SkinId = 0,
                        Url = ""
                    },
                    new DropItems()
                    {
                        ShortName = "sewingkit",
                        Amount = 10,
                        Command = "",
                        SkinId = 0,
                        Url = ""
                    },
                    new DropItems()
                    {
                        ShortName = "metalspring",
                        Amount = 5,
                        Command = "",
                        SkinId = 0,
                        Url = ""
                    },
                    new DropItems()
                    {
                        ShortName = "techparts",
                        Amount = 2,
                        Command = "",
                        SkinId = 0,
                        Url = ""
                    },
                    new DropItems()
                    {
                        ShortName = "workbench3",
                        Amount = 1,
                        Command = "",
                        SkinId = 0,
                        Url = ""
                    },
                    new DropItems()
                    {
                        ShortName = "leather",
                        Amount = 250,
                        Command = "",
                        SkinId = 0,
                        Url = ""
                    },
                    new DropItems()
                    {
                        ShortName = "lowgradefuel",
                        Amount = 250,
                        Command = "",
                        SkinId = 0,
                        Url = ""
                    },
                };
                return newconfig;
            }
        }

        protected override void LoadDefaultConfig() => cfg = Configs.GetNewConf();
        protected override void SaveConfig() => Config.WriteObject(cfg);

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                cfg = Config.ReadObject<Configs>();
            }
            catch
            {
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        private class DropItems
        {
            public string ShortName;
            public int Amount;
            public ulong SkinId;
            public string Command;
            public string Url;
        }

        private class UserData
        {
            public string NickName;
            public int Count;
            public double EveryKD;
            public Dictionary<int, DropItems> dropList = new Dictionary<int, DropItems>();

            public double IsEvery()
            {
                return Math.Max(EveryKD - CurrentTime(), 0);
            }
        }

        #endregion

        #region Ui

        private static string Layer = "UiLotterySystem";
        private static string Hud = "Hud";
        private static string Overlay = "Overlay";
        private static string regular = "robotocondensed-regular.ttf";
        private static string blur = "assets/content/ui/uibackgroundblur.mat";
        private string Sharp = "assets/content/ui/ui.background.tile.psd";
        private string radial = "assets/content/ui/ui.background.transparent.radial.psd";

        private CuiPanel Fon = new CuiPanel()
        {
            RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
            CursorEnabled = true,
            Image = {Color = "0 0 0 0.75", Material = "assets/content/ui/uibackgroundblur.mat"}
        };

        private CuiPanel MainFon = new CuiPanel()
        {
            RectTransform =
                {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-1920 -1080", OffsetMax = "1920 1080"},
            CursorEnabled = true,
            Image = {Color = "0 0 0 0"}
        };

        private CuiPanel MainPanel = new CuiPanel()
        {
            RectTransform = {AnchorMin = "0.3333 0.3333333", AnchorMax = "0.6666 0.6666667"},
            Image = {Color = "0 0 0 0"}
        };

        private CuiElement _lable = new CuiElement()
        {
            Parent = Layer + "Main",
            Components =
            {
                new CuiTextComponent()
                {
                    Text = "<b><size=25>ЕЖЕДНЕВНАЯ ЛОТЕРЕЯ</size></b>\nКаждый день вы можете забрать три предмета",
                    Align = TextAnchor.MiddleCenter, Font = regular
                },
                new CuiRectTransformComponent() {AnchorMin = "0.354469 0.8833332", AnchorMax = "0.6474808 0.9759259"}
            }
        };

        private CuiElement underInfo = new CuiElement()
        {
            Parent = Layer + "Main",
            Components =
            {
                new CuiTextComponent()
                    {Text = "Вы должны выбрать три предмета", Align = TextAnchor.MiddleCenter, Font = regular},
                new CuiRectTransformComponent() {AnchorMin = "0.1964986 0.2759258", AnchorMax = "0.3716842 0.3203704"}
            }
        };

        private CuiElement underInf = new CuiElement()
        {
            Parent = Layer + "Main",
            Components =
            {
                new CuiTextComponent()
                    {Text = "Список возможных предметов", Align = TextAnchor.MiddleCenter, Font = regular},
                new CuiRectTransformComponent() {AnchorMin = "0.6412202 0.2759258", AnchorMax = "0.8164058 0.3203704"}
            }
        };

        private CuiButton destroyUi = new CuiButton()
        {
            RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
            Button = {Close = Layer, Color = "0.64 0.64 0.64 0"},
            Text = {Text = "", Align = TextAnchor.MiddleCenter}
        };

        private CuiButton buttonLot = new CuiButton()
        {
            RectTransform = {AnchorMin = "0.4399713 0.8805575", AnchorMax = "0.5593719 0.9277787"},
            Button = {Color = "0.64 0.64 0.64 0.25", Command = "UiLotterySys lot"},
            Text = {Text = "ОТКРЫТЬ ЛОТЕРЕЮ", Align = TextAnchor.MiddleCenter}
        };

        private CuiButton buttonInv = new CuiButton()
        {
            RectTransform = {AnchorMin = "0.4415457 0.2694443", AnchorMax = "0.5625001 0.312037"},
            Button = {Color = "0.64 0.64 0.64 0.25", Command = "UiLotterySys inv"},
            Text = {Text = "ОТКРЫТЬ ИНВЕНТАРЬ", Align = TextAnchor.MiddleCenter}
        };

        private CuiElement palaska = new CuiElement()
        {
            Parent = Layer + "Main",
            Components =
            {
                new CuiImageComponent() {Color = "0.64 0.64 0.64 0.45"},
                new CuiRectTransformComponent() {AnchorMin = "0.4988947 0.32157406", AnchorMax = "0.5000655 0.8518517"}
            }
        };


        [ChatCommand("lottery")]
        private void StartUi(BasePlayer player)
        {
            UserData f;
            if (!_playerData.TryGetValue(player.userID, out f)) return;
            if (f.IsEvery() <= 0 && f.Count >= 3)
                f.Count = 0;
            CuiHelper.DestroyUi(player, Layer);
            var cont = new CuiElementContainer();
            cont.Add(Fon, Hud, Layer);
            cont.Add(MainFon, Layer, Layer + "Offsets");
            CuiHelper.AddUi(player, cont);
            LoadMainUi(player);
        }

        private void LoadCanItem(CuiElementContainer cont)
        {
            foreach (var key in cfg._Drop.Select((i, t) => new {A = i, B = t}))
            {
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "Main",
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Color = "0.64 0.64 0.64 0", Material = blur, Sprite = radial
                        },
                        new CuiOutlineComponent() {Color = HexToRustFormat(cfg.cvet), Distance = "1.5 1.5"},
                        new CuiRectTransformComponent()
                        {
                            AnchorMin =
                                $"{0.53834 + key.B * 0.073 - Math.Floor((double) key.B / 5) * 5 * 0.073} {0.725 - Math.Floor((double) key.B / 5) * 0.13}",
                            AnchorMax =
                                $"{0.605251 + key.B * 0.073 - Math.Floor((double) key.B / 5) * 5 * 0.073} {0.8444446 - Math.Floor((double) key.B / 5) * 0.13}"
                        }
                    }
                });
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "Main",
                    Name = Layer + key.B,
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Color = "0.64 0.64 0.64 0.25"
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin =
                                $"{0.53834 + key.B * 0.073 - Math.Floor((double) key.B / 5) * 5 * 0.073} {0.725 - Math.Floor((double) key.B / 5) * 0.13}",
                            AnchorMax =
                                $"{0.605251 + key.B * 0.073 - Math.Floor((double) key.B / 5) * 5 * 0.073} {0.8444446 - Math.Floor((double) key.B / 5) * 0.13}"
                        }
                    }
                });
                cont.Add(new CuiElement()
                {
                    Parent = Layer + key.B,
                    Components =
                    {
                        new CuiRawImageComponent()
                        {
                            Color = "1 1 1 1", Png = GetImage(key.A.ShortName, key.A.SkinId)
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        }
                    }
                });
                cont.Add(new CuiElement()
                {
                    Parent = Layer + key.B,
                    Components =
                    {
                        new CuiTextComponent()
                        {
                            Text = $"x{key.A.Amount} ", Align = TextAnchor.LowerRight, Font = regular, FontSize = 14,
                            Color = "0.85 0.85 0.85 0.85"
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        }
                    }
                });
            }
        }

        private void LoadInv(BasePlayer player, int page)
        {
            UserData f;
            CuiHelper.DestroyUi(player, Layer + "Main");
            var cont = new CuiElementContainer();
            cont.Add(MainPanel, Layer + "Offsets", Layer + "Main");
            cont.Add(destroyUi, Layer + "Main");
            cont.Add(buttonLot, Layer + "Main");
            if (page > 1)
            {
                cont.Add(new CuiButton()
                {
                    RectTransform = {AnchorMin = "0.2074573 0.4601867", AnchorMax = "0.2507302 0.5351869"},
                    Button =
                    {
                        Color = "0.64 0.64 0.64 1", Sprite = "assets/icons/circle_open.png",
                        Command = $"UiLotterySys nextpage {page - 1}"
                    },
                    Text = {Text = "«", Align = TextAnchor.MiddleCenter, FontSize = 20}
                }, Layer + "Main", Layer + "NextPage-");
            }
            else
            {
                cont.Add(new CuiButton()
                {
                    RectTransform = {AnchorMin = "0.2074573 0.4601867", AnchorMax = "0.2507302 0.5351869"},
                    Button =
                    {
                        Color = "0.64 0.64 0.64 1", Sprite = "assets/icons/circle_open.png",
                        Command = $""
                    },
                    Text = {Text = "«", Align = TextAnchor.MiddleCenter, FontSize = 20}
                }, Layer + "Main", Layer + "NextPage-");
            }

            cont.Add(new CuiButton()
            {
                RectTransform = {AnchorMin = "0.7616649 0.4601867", AnchorMax = "0.8049378 0.5351869"},
                Button =
                {
                    Color = "0.64 0.64 0.64 1", Sprite = "assets/icons/circle_open.png",
                    Command = $"UiLotterySys nextpage {page + 1}"
                },
                Text = {Text = "»", Align = TextAnchor.MiddleCenter, FontSize = 20}
            }, Layer + "Main", Layer + "NextPage+");
            if (!_playerData.TryGetValue(player.userID, out f)) return;
            foreach (var key in f.dropList.Select((i, t) => new {A = i, B = t - (page - 1) * 20}).Skip((page - 1) * 20)
                .Take(20))
            {
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "Main",
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Color = "0.64 0.64 0.64 0", Material = blur, Sprite = radial
                        },
                        new CuiOutlineComponent() {Color = HexToRustFormat(cfg.cvet), Distance = "1.5 1.5"},
                        new CuiRectTransformComponent()
                        {
                            AnchorMin =
                                $"{0.2913828 + key.B * 0.088 - Math.Floor((double) key.B / 5) * 5 * 0.088} {0.6300018 - Math.Floor((double) key.B / 5) * 0.155}",
                            AnchorMax =
                                $"{0.3711605 + key.B * 0.088 - Math.Floor((double) key.B / 5) * 5 * 0.088} {0.7731488 - Math.Floor((double) key.B / 5) * 0.155}"
                        }
                    }
                });
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "Main",
                    Name = Layer + key.B,
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Color = "0.64 0.64 0.64 0.25"
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin =
                                $"{0.2913828 + key.B * 0.088 - Math.Floor((double) key.B / 5) * 5 * 0.088} {0.6300018 - Math.Floor((double) key.B / 5) * 0.155}",
                            AnchorMax =
                                $"{0.3711605 + key.B * 0.088 - Math.Floor((double) key.B / 5) * 5 * 0.088} {0.7731488 - Math.Floor((double) key.B / 5) * 0.155}"
                        }
                    }
                });
                cont.Add(new CuiElement()
                {
                    Parent = Layer + key.B,
                    Components =
                    {
                        new CuiRawImageComponent()
                        {
                            Color = "1 1 1 1", Png = GetImage(key.A.Value.ShortName)
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        }
                    }
                });
                cont.Add(new CuiElement()
                {
                    Parent = Layer + key.B,
                    Components =
                    {
                        new CuiTextComponent()
                        {
                            Text = $"x{key.A.Value.Amount} ", Align = TextAnchor.LowerRight, Font = regular,
                            FontSize = 14,
                            Color = "0.85 0.85 0.85 0.85"
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        }
                    }
                });
                cont.Add(new CuiButton()
                {
                    Button = {Command = $"UiLotterySys take {page} {key.A.Key}", Color = "0 0 0 0"},
                    Text = {Text = ""},
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"}
                }, Layer + key.B);
            }

            CuiHelper.AddUi(player, cont);
        }

        private void LoadMainUi(BasePlayer player)
        {
            UserData f;
            var cont = new CuiElementContainer();
            CuiHelper.DestroyUi(player, Layer + "Main");
            cont.Add(MainPanel, Layer + "Offsets", Layer + "Main");
            cont.Add(destroyUi, Layer + "Main");
            cont.Add(_lable);
            cont.Add(underInfo);
            cont.Add(underInf);
            cont.Add(palaska);
            cont.Add(buttonInv, Layer + "Main");
            LoadCanItem(cont);
            if (_playerData.TryGetValue(player.userID, out f))
            {
                if (f.IsEvery() > 0)
                {
                    cont.Add(new CuiElement()
                    {
                        Parent = Layer + "Main",
                        Components =
                        {
                            new CuiTextComponent()
                            {
                                Text =
                                    $"До следующего раза осталось: {FormatTime(TimeSpan.FromSeconds(f.IsEvery()), "ru")}",
                                Align = TextAnchor.MiddleCenter, Font = regular
                            },
                            new CuiRectTransformComponent()
                                {AnchorMin = "0.1235193 0.8453606", AnchorMax = "0.4462379 0.8898038"}
                        }
                    });
                }
            }

            CuiHelper.AddUi(player, cont);
            LoadLottery(player);
        }

        private void LoadLottery(BasePlayer player)
        {
            var cont = new CuiElementContainer();
            for (int j = 0; j < 9; j++)
            {
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "Main",
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Color = "0.64 0.64 0.64 0", Material = blur, Sprite = radial
                        },
                        new CuiOutlineComponent() {Color = HexToRustFormat(cfg.cvet), Distance = "1.5 1.5"},
                        new CuiRectTransformComponent()
                        {
                            AnchorMin =
                                $"{0.1325126 + j * 0.1 - Math.Floor((double) j / 3) * 3 * 0.1} {0.6824073 - Math.Floor((double) j / 3) * 0.175}",
                            AnchorMax =
                                $"{0.2272689 + j * 0.1 - Math.Floor((double) j / 3) * 3 * 0.1} {0.8462964 - Math.Floor((double) j / 3) * 0.175}"
                        }
                    }
                });
                cont.Add(new CuiElement()
                {
                    Parent = Layer + "Main",
                    Name = Layer + "Lot" + j,
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Color = "0.64 0.64 0.64 0.25"
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin =
                                $"{0.1325126 + j * 0.1 - Math.Floor((double) j / 3) * 3 * 0.1} {0.6824073 - Math.Floor((double) j / 3) * 0.175}",
                            AnchorMax =
                                $"{0.2272689 + j * 0.1 - Math.Floor((double) j / 3) * 3 * 0.1} {0.8462964 - Math.Floor((double) j / 3) * 0.175}"
                        }
                    }
                });
                cont.Add(new CuiElement()
                {
                    FadeOut = 0.5f,
                    Parent = Layer + "Lot" + j,
                    Name = Layer + "Lock" + j,
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Color = "0.64 0.64 0.64 0.64", Sprite = "assets/icons/lock.png",
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0.3281276 0.2994457",
                            AnchorMax = "0.7013896 0.6723269"
                        }
                    }
                });
                cont.Add(new CuiButton()
                {
                    Button =
                    {
                        Command =
                            $"UiLotterySys open {j}",
                        Color = "0 0 0 0"
                    },
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                    Text = {Text = ""}
                }, Layer + "Lot" + j);
            }

            CuiHelper.AddUi(player, cont);
        }

        #region Met

        [ConsoleCommand("UiLotterySys")]
        private void UiCommand(ConsoleSystem.Arg arg)
        {
            var targetPlayer = arg.Player();
            UserData f;
            switch (arg.Args[0])
            {
                case "open":
                    if (!_playerData.TryGetValue(targetPlayer.userID, out f)) return;
                    if (f.IsEvery() > 0) return;
                    f.Count += 1;
                    if (f.Count >= 3) f.EveryKD = cfg.kd + CurrentTime();
                    var getItem = cfg._Drop.ToList().GetRandom();
                    CuiHelper.DestroyUi(targetPlayer, Layer + "Lock" + arg.Args[1]);
                    var cont = new CuiElementContainer();
                    cont.Add(new CuiElement()
                    {
                        Parent = Layer + "Lot" + arg.Args[1],
                        Components =
                        {
                            new CuiRawImageComponent()
                            {
                                Color = "1 1 1 1", Png = GetImage(getItem.ShortName),
                                FadeIn = 0.5f
                            },
                            new CuiRectTransformComponent()
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1"
                            }
                        }
                    });
                    cont.Add(new CuiElement()
                    {
                        Parent = Layer + "Lot" + arg.Args[1],
                        Components =
                        {
                            new CuiTextComponent()
                            {
                                Text = $"x{getItem.Amount} ", Align = TextAnchor.LowerRight, Font = regular,
                                FontSize = 14,
                                Color = "0.85 0.85 0.85 0.85"
                            },
                            new CuiRectTransformComponent()
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1"
                            }
                        }
                    });
                    Effect x = new Effect("assets/prefabs/misc/xmas/presents/effects/unwrap.prefab", targetPlayer, 0,
                        new Vector3(), new Vector3());
                    EffectNetwork.Send(x, targetPlayer.Connection);
                    CuiHelper.AddUi(targetPlayer, cont);
                    if (arg.Args.Length == 7)
                    {
                        if (f.dropList.Count > 0)
                        {
                            f.dropList.Add(f.dropList.Max(p => p.Key) + 1, getItem);
                        }
                        else
                        {
                            f.dropList.Add(1, getItem);
                        }
                    }
                    else
                    {
                        if (f.dropList.Count > 0)
                        {
                            f.dropList.Add(f.dropList.Max(p => p.Key) + 1, getItem);
                        }
                        else
                        {
                            f.dropList.Add(1, getItem);
                        }
                    }

                    break;
                case "inv":
                    LoadInv(arg.Player(), 1);
                    break;
                case "nextpage":
                    LoadInv(arg.Player(), arg.Args[1].ToInt());
                    break;
                case "lot":
                    LoadMainUi(arg.Player());
                    break;
                case "take":
                    if (!_playerData.TryGetValue(targetPlayer.userID, out f)) return;
                    var t = f.dropList[arg.Args[2].ToInt()];
                    if (t.Command == String.Empty)
                    {
                        var item = ItemManager.CreateByName(t.ShortName, t.Amount, t.SkinId);
                        if (!arg.Player().inventory.GiveItem(item))
                            item.Drop(targetPlayer.inventory.containerMain.dropPosition,
                                targetPlayer.inventory.containerMain.dropVelocity);
                    }
                    else
                    {
                        rust.RunServerCommand(string.Format(t.Command, targetPlayer.userID));
                    }

                    f.dropList.Remove(arg.Args[2].ToInt());
                    LoadInv(targetPlayer, arg.Args[1].ToInt());
                    break;
            }
        }

        #endregion

        private void Unload()
        {
            Interface.Oxide.DataFileSystem.WriteObject("LotteryData", _playerData);
            foreach (var basePlayer in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(basePlayer, Layer);
            if (Process != null)
                Rust.Global.Runner.StopCoroutine(Process);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            UserData f;
            if (!_playerData.TryGetValue(player.userID, out f))
                _playerData.Add(player.userID, new UserData()
                {
                    NickName = player.displayName,
                    EveryKD = CurrentTime(),
                    Count = 0,
                    dropList = new Dictionary<int, DropItems>()
                });
        }

        private IEnumerator LoadImage()
        {
            foreach (var cfg in cfg._Drop)
            {
                if (cfg.Command != "") AddImage(cfg.Url, cfg.ShortName, cfg.SkinId);
                yield return new WaitForSeconds(0.5f);
            }
        }

        private void OnServerInitialized()
        {
            Process = Rust.Global.Runner.StartCoroutine(LoadImage());
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("LotteryData"))
                _playerData =
                    Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, UserData>>("LotteryData");
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(basePlayer);
            }
        }

        #endregion

        #region figna

        private Coroutine Process;

        #endregion

        #region Help

        [PluginReference] private Plugin ImageLibrary;

        public string GetImage(string shortname, ulong skin = 0) =>
            (string) ImageLibrary.Call("GetImage", shortname, skin);

        public bool AddImage(string url, string shortname, ulong skin = 0) =>
            (bool) ImageLibrary.Call("AddImage", url, shortname, skin);

        private static string HexToRustFormat(string hex)
        {
            if (string.IsNullOrEmpty(hex)) hex = "#FFFFFFFF";
            var str = hex.Trim('#');
            if (str.Length == 6) str += "FF";
            if (str.Length != 8)
            {
                throw new Exception(hex);
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);
            Color color = new Color32(r, g, b, a);
            return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
        }

        private static DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        private static double CurrentTime() => DateTime.UtcNow.Subtract(epoch).TotalSeconds;

        private static string Format(int units, string form1, string form2, string form3)
        {
            var tmp = units % 10;
            if (units >= 5 && units <= 20 || tmp >= 5 && tmp <= 9) return $"{units} {form1}";
            if (tmp >= 2 && tmp <= 4) return $"{units} {form2}";
            return $"{units} {form3}";
        }

        public static string FormatTime(TimeSpan time, string language, int maxSubstr = 5)
        {
            var result = string.Empty;
            switch (language)
            {
                case "ru":
                    var i = 0;
                    if (time.Days != 0 && i < maxSubstr)
                    {
                        if (!string.IsNullOrEmpty(result)) result += " ";
                        result += $"{Format(time.Days, "дней", "дня", "день")}";
                        i++;
                    }

                    if (time.Hours != 0 && i < maxSubstr)
                    {
                        if (!string.IsNullOrEmpty(result)) result += " ";
                        result += $"{Format(time.Hours, "часов", "часа", "час")}";
                        i++;
                    }

                    if (time.Minutes != 0 && i < maxSubstr)
                    {
                        if (!string.IsNullOrEmpty(result)) result += " ";
                        result += $"{Format(time.Minutes, "минут", "минуты", "минута")}";
                        i++;
                    }

                    if (time.Seconds != 0 && i < maxSubstr)
                    {
                        if (!string.IsNullOrEmpty(result)) result += " ";
                        result += $"{Format(time.Seconds, "сек", "сек", "сек")}";
                        i++;
                    }

                    break;
                case "en":
                {
                    var i2 = 0;
                    if (time.Days != 0 && i2 < maxSubstr)
                    {
                        if (!string.IsNullOrEmpty(result)) result += " ";
                        result += $"{Format(time.Days, "days'", "day's", "day")}";
                        i2++;
                    }

                    if (time.Hours != 0 && i2 < maxSubstr)
                    {
                        if (!string.IsNullOrEmpty(result)) result += " ";
                        result += $"{Format(time.Hours, "hours'", "hour's", "hour")}";
                        i2++;
                    }

                    if (time.Minutes != 0 && i2 < maxSubstr)
                    {
                        if (!string.IsNullOrEmpty(result)) result += " ";
                        result += $"{Format(time.Minutes, "minutes", "minutes", "minute")}";
                        i2++;
                    }

                    if (time.Seconds != 0 && i2 < maxSubstr)
                    {
                        if (!string.IsNullOrEmpty(result)) result += " ";
                        result += $"{Format(time.Seconds, "second", "seconds", "second")}";
                        i2++;
                    }

                    break;
                }
            }

            return result;
        }

        #endregion
    }
}
