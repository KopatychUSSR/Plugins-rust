using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Facepunch.Extend;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("Bankomat", "http://topplugin.ru/", "1.0.1")]
    public class Bankomat : RustPlugin
    {       
        [PluginReference] private Plugin ImageLibrary;
        private ConfigData Settings { get; set; }
        private Timer RespawnTimer;
        private List<VendingMachine> Magazin = new List<VendingMachine>();
        private List<CardReader> Magazins = new List<CardReader>();
        private List<HumanNPC> Npc = new List<HumanNPC>();
        public string GetImage(string shortname, ulong skin = 0) => (string)ImageLibrary.Call("GetImage", shortname, skin);
        public bool AddImage(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname, skin);
        public class CraftCard
        {
            [JsonProperty("Shortname предмета")] public string ShortName;
            [JsonProperty("Кол-во")] public int Amount;
        }
        public class ShopItems
        {
            [JsonProperty("Shortname предмета")] public string ShortName;
            [JsonProperty("Цена")] public int sell;
            [JsonProperty("Кол-во")] public int Amount;
        }
        class ConfigData
        {
            [JsonProperty("Максимальное количество денег в пачьке(стаке)")] public int maxstack;
            [JsonProperty("Шанс выпадение крышек из бочек и ящиков(0-100%)")] public int shans;
            [JsonProperty("Сколько минимум будет падать денег")] public int minmoney;
            [JsonProperty("Сколько максимум будет падать денег")] public int maxmoney;
            [JsonProperty("Что нужно для крафта карты")]
            public List<CraftCard> CardItem { get; set; }
            [JsonProperty("Товары в магазине")]
            public List<ShopItems> ShopItem { get; set; }
            public static ConfigData GetNewConf()
            {
                ConfigData newConfig = new ConfigData();
                newConfig.maxstack = 100;
                newConfig.shans = 10;
                newConfig.minmoney = 1;
                newConfig.maxmoney = 5;
                newConfig.CardItem = new List<CraftCard>
                {
                    new CraftCard()
                    {
                        ShortName = "wood",
                        Amount = 5000
                    },
                    new CraftCard()
                    {
                        ShortName = "stones",
                        Amount = 2500
                    }
                };
                newConfig.ShopItem = new List<ShopItems>
                {
                    new ShopItems()
                    {
                        ShortName = "rifle.ak",
                        sell = 500,
                        Amount = 1,
                    },
                    new ShopItems()
                    {
                        ShortName = "jackhammer",
                        sell = 50,
                        Amount = 1,
                    },
                    new ShopItems()
                    {
                        ShortName = "stones",
                        sell = 100,
                        Amount = 10000,
                    },
                    new ShopItems()
                    {
                        ShortName = "metal.refined",
                        sell = 100,
                        Amount = 100,
                    },
                    new ShopItems()
                    {
                        ShortName = "sulfur",
                        sell = 35,
                        Amount = 3500,
                    },
                    new ShopItems()
                    {
                        ShortName = "rifle.bolt",
                        sell = 100,
                        Amount = 5,
                    },
                };
                return newConfig;
            }
        }
        
        protected override void LoadDefaultConfig() => Settings = ConfigData.GetNewConf();
        protected override void SaveConfig() => Config.WriteObject(Settings);

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                Settings = Config.ReadObject<ConfigData>();
            }
            catch
            {
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }
        bool CanUseVending(BasePlayer player, VendingMachine machine)
        {
                if (machine.skinID == 862125671)
                {
                    Effect.server.Run("assets/prefabs/npc/autoturret/effects/targetacquired.prefab",
                        player.transform.position);
                    return false;
                }

                List<ulong> List = new List<ulong>();
                return true;
        }
        object OnRotateVendingMachine(VendingMachine machine, BasePlayer player)
        {
            if (machine.skinID == 862125671)
            {
                return false;
            }
            return null;
        }
        bool CanAdministerVending(BasePlayer player1, VendingMachine machine1)
        {
            if (machine1.skinID == 862125671)
            {
                return false;
            }
            return true;
        }
        private string Layer = "Bank";
        public void ShopUi(BasePlayer player, int page)
        {
            CuiHelper.DestroyUi(player, Layer);
            CuiElementContainer cont = new CuiElementContainer();
            cont.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image =
                {
                    Color = "0 0 0 0.8", Sprite = "Assets/Content/UI/UI.Background.Tile.psd",
                    Material = "assets/content/ui/uibackgroundblur.mat"
                },
                FadeOut = 0.1f,
            }, "Overlay", Layer);
            cont.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0.3109375 0.8722222", AnchorMax = "0.6765625 0.9648148"},
                Image = {Color = "0 0 0 0"},
            }, Layer, "shops");
            cont.Add(new CuiLabel
            {
                Text = {Text = "Магазин", Align = TextAnchor.MiddleCenter, FontSize = 30, FadeIn = 0.5f},
            }, "shops");
            cont.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Фарми деньги закидывай на карту и покупай товары.", Align = TextAnchor.LowerCenter, FontSize = 15,
                    FadeIn = 0.5f,
                },
            }, "shops");
            cont.Add(new CuiButton
                {
                    RectTransform = {AnchorMax = "1 1", AnchorMin = "0 0"},
                    Button = {Color = "0 0 0 0", Command = "UI_Destroy_bank"},
                    Text = {Text = "", FontSize = 0}
                }, Layer);
            foreach (var check in Settings.ShopItem.Select((i, t) => new {A = i, B = t - (page - 0) * 18})
                .Skip((page - 0) * 18).Take(18))
            {
                cont.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin =
                                $"{0.1477083 + check.B * 0.119 - Math.Floor((double) check.B / 6) * 6 * 0.119} {0.5814815 - Math.Floor((double) check.B / 6) * 0.20}",
                            AnchorMax =
                                $"{0.2621875 + check.B * 0.119 - Math.Floor((double) check.B / 6) * 6 * 0.119} {0.7694444 - Math.Floor((double) check.B / 6) * 0.20}",
                            OffsetMax = "0 0"
                        },
                        Button =
                        {
                            Color = "1 1 1 0.01", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                            Command = $""
                        },
                        Text =
                        {
                            Text = "", Align = TextAnchor.LowerRight, Font = "robotocondensed-bold.ttf", FontSize = 15
                        },
                    }, Layer, Layer + $".{check.B}");
                cont.Add(new CuiElement
                {
                    FadeOut = 0.3f,
                    Parent = Layer + $".{check.B}",
                    Name = Layer + $".{check.B}",
                    Components =
                    {
                        new CuiRawImageComponent
                            {FadeIn = 0.3f, Png = (string) ImageLibrary.Call("GetImage", check.A.ShortName)},
                        new CuiRectTransformComponent
                            {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "5 1", OffsetMax = "-5 -1"}
                    }
                });
                cont.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin =
                                $"{0.1477083 + check.B * 0.119 - Math.Floor((double) check.B / 6) * 6 * 0.119} {0.5814815 - Math.Floor((double) check.B / 6) * 0.20}",
                            AnchorMax =
                                $"{0.2621875 + check.B * 0.119 - Math.Floor((double) check.B / 6) * 6 * 0.119} {0.7694444 - Math.Floor((double) check.B / 6) * 0.20}",
                            OffsetMax = "0 0"
                        },
                        Button =
                        {
                            Color = "0 0 0 0.0", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                            Command = $""
                        },
                        Text =
                        {
                            Text = "x" + check.A.Amount + " ", Align = TextAnchor.LowerRight, Font = "robotocondensed-bold.ttf", FontSize = 15
                        },
                    }, Layer, Layer + $".{check.B}");
                cont.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin =
                                $"{0.1477083 + check.B * 0.119 - Math.Floor((double) check.B / 6) * 6 * 0.119} {0.5814815 - Math.Floor((double) check.B / 6) * 0.20}",
                            AnchorMax =
                                $"{0.2621875 + check.B * 0.119 - Math.Floor((double) check.B / 6) * 6 * 0.119} {0.7694444 - Math.Floor((double) check.B / 6) * 0.20}",
                            OffsetMax = "0 0"
                        },
                        Button =
                        {
                            Color = "0 0 0 0.0", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                            Command = $"UI_buyitem_bAnk {check.B + 18*page}"
                        },
                        Text =
                        {
                            Text = check.A.sell + " стоимость", Align = TextAnchor.UpperLeft, Font = "robotocondensed-bold.ttf", FontSize = 15
                        },
                    }, Layer, Layer + $".{check.B}");
                    if (page > 0)
                    {
                        cont.Add(new CuiButton
                        {
                            RectTransform = {AnchorMin = "0.006249997 0.4490741", AnchorMax = "0.03020833 0.5555555"},
                            Button = {Color = "0 0 0 0", Command = $"nextpage_bank {page - 1}", Close = Layer},
                            Text = {Text = "<", FontSize = 50},
                            FadeOut = 1f,
                        }, Layer);
                    }

                    if (Settings.ShopItem.Count >= 19 * (1 + page))
                    {
                        cont.Add(new CuiButton
                        {
                            RectTransform = {AnchorMin = "0.9734374 0.4620373", AnchorMax = "0.9963542 0.5564818"},
                            Button = {Color = "0 0 0 0", Command = $"nextpage_bank {page + 1}", Close = Layer},
                            Text = {Text = ">", FontSize = 50},
                            FadeOut = 1f,
                        }, Layer);
                    }
            }
            CuiHelper.AddUi(player, cont);
        }
        void OpenUi(BasePlayer player)
        {
            var key = player.inventory.containerBelt.itemList.FindLast(x => x.skin == 1922602673);
            BankomatData BData2 = BankomatDatas.Find(z => z.NumCard == key.uid);
            CuiHelper.DestroyUi(player, Layer);
            CuiElementContainer cont = new CuiElementContainer();
            cont.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image =
                {
                    Color = "0 0 0 0.8", Sprite = "Assets/Content/UI/UI.Background.Tile.psd",
                    Material = "assets/content/ui/uibackgroundblur.mat"
                },
                FadeOut = 0.1f,
            }, "Overlay", Layer);
            cont.Add(new CuiButton
            {
                RectTransform = {AnchorMax = "1 1", AnchorMin = "0 0"},
                Button = {Color = "0 0 0 0", Command = "UI_Destroy_bank"},
                Text = {Text = "", FontSize = 0}
            }, Layer);
            cont.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0.2843745 0.2638889", AnchorMax = "0.6927078 0.8564815"},
                Image =
                {
                    Color = "0 0 0 0.3293639"
                },
                FadeOut = 0.1f,
            }, Layer, "Menu");
            cont.Add(new CuiButton()
            {
                RectTransform = {AnchorMin = "0.1926031 0.1234376", AnchorMax = "0.4961747 0.1796875"},
                Button = {Color = "0.07511976 0.3909295 0 0.8",Command = "UI_polozhit_ui_bank"},
                Text = { Text = "ПОЛОЖИТЬ", Align = TextAnchor.MiddleCenter}
            },"Menu");
            cont.Add(new CuiButton()
            {
                RectTransform = {AnchorMin = "0.498721 0.1234375", AnchorMax = "0.7806135 0.1796875"},
                Button = {Color = "0.5909292 0 0 0.8", Command = "UI_snyat_ui_bank"},
                Text = {Text = "СНЯТЬ", Align = TextAnchor.MiddleCenter}
            }, "Menu");
            cont.Add(new CuiElement()
            {
                Parent = "Menu",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = $"ВЛАДЕЛЕЦ - {BData2.Name}\nНОМЕР КАРТЫ - {BData2.NumCard}\nДЕНЕГ НА СЧЕТУ - {BData2.money}",
                        Align = TextAnchor.MiddleCenter,
                        FontSize = 20
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.2053583 0.3578125",
                        AnchorMax = "0.7857155 0.9343749"
                    }
                }
            });            
            cont.Add(new CuiElement()
            {
                Parent = "Menu",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = $"ИНФОРМАЦИЯ О КАРТЕ",
                        Align = TextAnchor.MiddleCenter,
                        FontSize = 20
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.2053583 0.909374",
                        AnchorMax = "0.7857155 0.9999989"
                    }
                }
            });
                CuiHelper.AddUi(player, cont);
        }
        void MenuUi(BasePlayer player)
        {
            var key = player.inventory.containerBelt.itemList.FindLast(x => x.skin == 1922602673);
            BankomatData BData2 = BankomatDatas.Find(z => z.NumCard == key.uid);
            if(key == null) return;
            CuiHelper.DestroyUi(player, Layer);
            CuiElementContainer cont = new CuiElementContainer();
            cont.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image =
                {
                    Color = "0 0 0 0.8", Sprite = "Assets/Content/UI/UI.Background.Tile.psd",
                    Material = "assets/content/ui/uibackgroundblur.mat"
                },
                FadeOut = 0.1f,
            }, "Overlay", Layer);
            cont.Add(new CuiButton
            {
                RectTransform = {AnchorMax = "1 1", AnchorMin = "0 0"},
                Button = {Color = "0 0 0 0", Command = "UI_Destroy_bank"},
                Text = {Text = "", FontSize = 0}
            }, Layer);
            if (BData2 != null)
            {
                cont.Add(new CuiElement()
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent()
                        {
                            Text = "ВВЕДИТЕ ПИН-КОД",
                            Color = "1.00 0.00 0.00 1.00",
                            Align = TextAnchor.UpperCenter,
                            FontSize = 20
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0.3432291 0.4731481",
                            AnchorMax = "0.6385417 0.6101829"
                        },
                    }
                });
            }
            else
            {
                cont.Add(new CuiElement()
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent()
                        {
                            Text = "РЕГИСТРАЦИЯ",
                            Color = "1.00 0.00 0.00 1.00",
                            Align = TextAnchor.UpperCenter,
                            FontSize = 20
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0.3432291 0.4731481",
                            AnchorMax = "0.6385417 0.6101829"
                        },
                    }
                });
                cont.Add(new CuiElement()
                {
                    Parent = Layer,
                    Components =
                    {
                        new CuiTextComponent()
                        {
                            Text = "ВВЕДИТЕ ПИН-КОД",
                            Color = "1.00 0.00 0.00 1.00",
                            Align = TextAnchor.LowerCenter,
                            FontSize = 14
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = "0.3432291 0.4731481",
                            AnchorMax = "0.6385417 0.6101829"
                        },
                    }
                });
            }
            cont.Add(new CuiElement()
            {
                Name = "Code",
                Parent = Layer,
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color  = "0.33 0.53 0.78 0.65"
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.3432291 0.4731481",
                        AnchorMax = "0.6385417 0.5333333"
                    },
                }
            });
            cont.Add(new CuiElement()
            {
                Parent = "Code",
                Components =
                {
                    new CuiInputFieldComponent()
                    {
                        Text = "",
                        CharsLimit = 4,
                        IsPassword = true,
                        Command = "UI_pinkod_bank",
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    },
                }
            });
            CuiHelper.AddUi(player, cont);
        }
        void ShopUi2(BasePlayer player)
        {
            var key = player.inventory.containerBelt.itemList.FindLast(x => x.skin == 1922602673);
            BankomatData BData2 = BankomatDatas.Find(z => z.NumCard == key.uid);
            if(key == null) return;
            if (BData2 == null) return;
            CuiHelper.DestroyUi(player, Layer);
            CuiElementContainer cont = new CuiElementContainer();
            cont.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image =
                {
                    Color = "0 0 0 0.8", Sprite = "Assets/Content/UI/UI.Background.Tile.psd",
                    Material = "assets/content/ui/uibackgroundblur.mat"
                },
                FadeOut = 0.1f,
            }, "Overlay", Layer);
            cont.Add(new CuiButton
            {
                RectTransform = {AnchorMax = "1 1", AnchorMin = "0 0"},
                Button = {Color = "0 0 0 0", Command = "UI_Destroy_bank"},
                Text = {Text = "", FontSize = 0}
            }, Layer);
            
            cont.Add(new CuiElement()
            {
                Parent = Layer,
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "ВВЕДИТЕ ПИН-КОД",
                        Color = "1.00 0.00 0.00 1.00",
                        Align = TextAnchor.UpperCenter,
                        FontSize = 20
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.3432291 0.4731481",
                        AnchorMax = "0.6385417 0.6101829"
                    },
                }
            });
            cont.Add(new CuiElement()
            {
                Name = "Code",
                Parent = Layer,
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color  = "0.33 0.53 0.78 0.65"
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.3432291 0.4731481",
                        AnchorMax = "0.6385417 0.5333333"
                    },
                }
            });
            cont.Add(new CuiElement()
            {
                Parent = "Code",
                Components =
                {
                    new CuiInputFieldComponent()
                    {
                        Text = "",
                        CharsLimit = 4,
                        IsPassword = true,
                        Command = "UI_pinkod_bank_shop",
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    },
                }
            });
            CuiHelper.AddUi(player, cont);
        }
        [ConsoleCommand("UI_buyitem_bAnk")]
        private void cmdbuyitem(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            var key = player.inventory.containerBelt.itemList.FindLast(f => f.skin == 1922602673);
            BankomatData BData = BankomatDatas.Find(z => z.NumCard == key.uid);
            if (BData == null) return;
            if (BData.money <= 0) return;
            if (key == null) return;
            foreach (var ww in Settings.ShopItem.Select((i, t) => new {A = i, B = t}))
            {
                if (BData.money >= ww.A.sell)
                {
                    if (arg.Args[0] != ww.B.ToString()) continue;
                    BData.money -= ww.A.sell;
                    var item = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(ww.A.ShortName).itemid,
                        ww.A.Amount);
                    if (!player.inventory.GiveItem(item))
                    {
                        item.Drop(player.inventory.containerMain.dropPosition,
                            player.inventory.containerMain.dropVelocity, new Quaternion());
                    }

                    SendReply(player, $"<color=#0affd6>Вы успешно совершили покупку!</color>");
                    return;
                }
            }
        }
        
        [ConsoleCommand("nextpage_bank")]
        void nextpages(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            ShopUi(player, Convert.ToInt32(arg.Args[0]));
        }
        [ConsoleCommand("UI_Destroy_bank")]
        void destroyui(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            CuiHelper.DestroyUi(player, Layer);
            Effect.server.Run("assets/prefabs/npc/autoturret/effects/targetlost.prefab", player.transform.position);
        }
        [ConsoleCommand("UI_snyat_ui_bank")]
        void UI_snyat_ui_bank(ConsoleSystem.Arg arg)
        {
            Puts("3");
            var player = arg.Player();
            CuiHelper.DestroyUi(player, "Polozhit");
            CuiHelper.DestroyUi(player, "Snyat");
            CuiHelper.DestroyUi(player, "Snyat1");
            CuiHelper.DestroyUi(player, "Polozhit1");
            CuiHelper.DestroyUi(player, "Polozhit2");
            CuiElementContainer cont = new CuiElementContainer();
            cont.Add(new CuiElement()
            {
                Name = "Snyat1",
                Parent = "Menu",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color  = "0.6646028 0.6646028 0.6646028 0.3764706"
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.1926028 0.225",
                        AnchorMax = "0.7793382 0.3218752"
                    },
                }
            });
            cont.Add(new CuiElement()
            {
                Parent = "Snyat1",
                Components =
                {
                    new CuiInputFieldComponent()
                    {
                        Text = "",
                        CharsLimit = 5,
                        Command = "UI_snyat_bank",
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }, 
                }
            });
            cont.Add(new CuiElement()
            {
                Name = "Snyat",
                Parent = "Menu",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "ВВЕДИТЕ КОЛЛИЧЕСТВО",
                        Align = TextAnchor.UpperCenter,
                        FontSize = 12
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.3073992 0.3312499",
                        AnchorMax = "0.6823992 0.4072269"
                    }, 
                }
            });
            CuiHelper.AddUi(player, cont);
        }
        
        [ConsoleCommand("UI_polozhit_ui_bank")]
        void UI_Polozhit_ui_bank(ConsoleSystem.Arg arg)
        {
            Puts("3");
            var player = arg.Player();
            CuiHelper.DestroyUi(player, "Snyat");
            CuiHelper.DestroyUi(player, "Polozhit");
            CuiHelper.DestroyUi(player, "Snyat1");
            CuiHelper.DestroyUi(player, "Polozhit1");
            CuiHelper.DestroyUi(player, "Polozhit2");
            CuiElementContainer cont = new CuiElementContainer();
            cont.Add(new CuiElement()
            {
                Name = "Polozhit",
                Parent = "Menu",
                Components =
                {
                    new CuiImageComponent()
                    {
                        Color  = "0.6646028 0.6646028 0.6646028 0.3764706"
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.1926028 0.225",
                        AnchorMax = "0.7793382 0.3218752"
                    },
                }
            });
            cont.Add(new CuiElement()
            {
                Parent = "Polozhit",
                Components =
                {
                    new CuiInputFieldComponent()
                    {
                        Text = "",
                        CharsLimit = 5,
                        Command = "UI_transfer_bank",
                        Align = TextAnchor.MiddleCenter
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }, 
                }
            });
            cont.Add(new CuiElement()
            {
                Name = "Polozhit1",
                Parent = "Menu",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "ВВЕДИТЕ КОЛЛИЧЕСТВО",
                        Align = TextAnchor.UpperCenter,
                        FontSize = 12
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.3073992 0.3312499",
                        AnchorMax = "0.6823992 0.4072269"
                    }, 
                }
            });
            cont.Add(new CuiElement()
            {
                Name = "Polozhit2",
                Parent = "Menu",
                Components =
                {
                    new CuiTextComponent()
                    {
                        Text = "Не пишите значение выше допустимых, максимальных стаков",
                        Align = TextAnchor.LowerCenter,
                        FontSize = 10
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0.3073992 0.3312499",
                        AnchorMax = "0.6823992 0.4072269"
                    }, 
                }
            });
            CuiHelper.AddUi(player, cont);
        }
        
        [ConsoleCommand("UI_pinkod_bank")]
        void pinkodui(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            BankomatData BData = BankomatDatas.Find(z => z.SteamID == player.userID);
            var key = player.inventory.containerBelt.itemList.FindLast(f => f.skin == 1922602673);
            if(key == null) return;
            {
                if (key.skin == 1922602673)
                {
                    if (arg.Args[0].Length < 4)
                    {
                        return;
                    }
                    BankomatData BData2 = BankomatDatas.Find(z => z.NumCard == key.uid);
                    if(BData2 == null)
                    {
                        {
                            BData = new BankomatData
                            {
                                Name = player.displayName,
                                SteamID = player.userID,
                                pinkod = arg.Args[0],
                                NumCard = key.uid,
                                money = 0
                            };
                            BankomatDatas.Add(BData);
                        }
                        return;
                    }
                    if (BData2.NumCard == key.uid && arg.Args[0] == BData2.pinkod)
                    {
                        OpenUi(player);
                    }
                    else
                    {
                        SendReply(player, "Неверный пин-код");
                        CuiHelper.DestroyUi(player, Layer);
                    }
                }
            }
        }
        [ConsoleCommand("UI_pinkod_bank_shop")]
        void pinkodui2(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            var key = player.inventory.containerBelt.itemList.FindLast(f => f.skin == 1922602673);
            if(key == null) return;
            {
                BankomatData BData2 = BankomatDatas.Find(z => z.NumCard == key.uid);
                if(BData2 == null) return;
                if (BData2.NumCard == key.uid && arg.Args[0] == BData2.pinkod)
                {
                    ShopUi(player, 0);
                }
                else
                {
                    SendReply(player, "Неверный пин-код");
                    CuiHelper.DestroyUi(player, Layer);
                }
                
            }
        }
        [ConsoleCommand("UI_snyat_bank")]
        void snyatui(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            CuiHelper.DestroyUi(player, "Menu");
            var key = player.inventory.containerBelt.itemList.FindLast(f => f.skin == 1922602673);
            if(key == null) return;
            BankomatData BData2 = BankomatDatas.Find(z => z.NumCard == key.uid);
            if (BData2 == null) return;
            {
                if (BData2.money >= arg.Args[0].ToInt())
                {
                    if (key.skin == 1922602673)
                    {
                        //player.inventory.containerMain.Take(null, ItemManager.FindItemDefinition("paper").itemid,
                        // arg.Args[0].ToInt());
                        Item it = ItemManager.CreateByName("paper", arg.Args[0].ToInt());
                        it.skin = 916068443;
                        it.name = "Деньги";
                        player.inventory.GiveItem(it);
                        BData2.money -= arg.Args[0].ToInt();
                        OpenUi(player);
                        return;
                    }
                }
            }

            CuiHelper.DestroyUi(player, Layer);
            Effect.server.Run("assets/prefabs/npc/autoturret/effects/targetlost.prefab", player.transform.position);
        }
        [ConsoleCommand("UI_transfer_bank")]
        void transferui(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            var key = player.inventory.containerMain.itemList.Find(s => s.skin == 916068443);
            if (key == null) {SendReply(player, "Нет у тебя денег"); return;}
            var card = player.inventory.containerBelt.itemList.FindLast(f => f.skin == 1922602673);
            if(card == null ) return;
            BankomatData BData2 = BankomatDatas.Find(z => z.NumCard == card.uid);
            if (BData2 == null) return;
            {
                if(key.amount >= arg.Args[0].ToInt())
                {
                        player.inventory.containerMain.Take(null,
                            ItemManager.FindItemDefinition("paper").itemid,
                            arg.Args[0].ToInt());
                        BData2.money += arg.Args[0].ToInt();
                        CuiHelper.DestroyUi(player, Layer);
                        OpenUi(player);
                        return;
                }
                SendReply(player, "Недостаточно средств");
            }
            CuiHelper.DestroyUi(player, Layer);
            Effect.server.Run("assets/prefabs/npc/autoturret/effects/targetlost.prefab", player.transform.position);
        }
        private object OnCardSwipe(CardReader cardReader, Keycard card, BasePlayer player)
        {
            Item item = player.GetActiveItem();
            if (cardReader.skinID == 99323923)
            {
                card.skinID = item.skin;
                if (card.skinID == 1922602673)
                {
                    MenuUi(player);
                }
            }
            if (cardReader.skinID == 993239235)
            {
                card.skinID = item.skin;
                if (card.skinID == 1922602673)
                {
                    var key = player.inventory.containerBelt.itemList.FindLast(f => f.skin == 1922602673);
                    BankomatData BData = BankomatDatas.Find(z => z.NumCard == key.uid);
                    if (key == null) return null;
                    if (BData == null)
                    {
                        SendReply(player, "Карта не зарегистрирована.Идите к банкомату!");
                        return null;
                    }
                    ShopUi2(player);
                }
            }
            return null;
        }
        void OnLoseCondition(Item item, ref float amount)
        {
            if (item.skin == 1922602673)
            {
                item.RepairCondition(amount);
            }
        }
        public List<BankomatData> BankomatDatas = new List<BankomatData>();
        public class BankomatData
        { 
            [JsonProperty("Имя хозяина")] public string Name { get; set; }
           [JsonProperty("СтимИд")] public ulong SteamID { get; set; }
           [JsonProperty("Номер карты")] public ulong NumCard { get; set; }
           [JsonProperty("Пинкод")] public string pinkod { get; set; }
           [JsonProperty("Деньги на счету")]public int money { get; set; }
        }
        void OnLootSpawn(LootContainer container)
        {
            if (Random.Range(0, 100) <= Settings.shans)
            {
                ItemContainer component1 = container.GetComponent<StorageContainer>().inventory;
                Item item = ItemManager.CreateByName("paper", Random.Range(Settings.minmoney, Settings.maxmoney), 916068443);
                item.name = "Деньги";
                component1.itemList.Add(item);
                item.parent = component1;
                item.MarkDirty();
            }
        }
        private object CanStackItem(Item item, Item triger)
        {
            if (item.skin == 916068443 && triger.skin == 916068443)
            {
                return true;
            }
            return null;
        }
        
        private Item OnItemSplit(Item item, int amount)
        {
            if (amount <= 0) return null;
            if (item.skin != 916068443) return null;
            item.amount -= amount;
            var newItem = ItemManager.Create(item.info, amount, item.skin);
            newItem.name = item.name;
            newItem.skin = item.skin;
            newItem.amount = amount;
            return newItem;
        }
        private object CanCombineDroppedItem(WorldItem first, WorldItem second)
        {
            return CanStackItem(first.item, second.item);
        }
        private void OnMaxStackable(Item item)
        {
            if (item.skin == 916068443)
            {
                var it = ItemManager.FindItemDefinition("paper");
                it.stackable = Settings.maxstack;
            }
        }
        private List<MonumentInfo> FindMonuments(string filter)
        {
            var monuments = new List<MonumentInfo>();

            foreach (var monument in TerrainMeta.Path.Monuments)
            {
                if (!monument.name.Contains("/monument/") || !string.IsNullOrEmpty(filter) &&
                    !monument.Type.ToString().Contains(filter, CompareOptions.IgnoreCase) &&
                    !monument.name.Contains(filter, CompareOptions.IgnoreCase))
                    continue;
                        
                monuments.Add(monument);
            }

            return monuments;
        }
        public bool HaveItem(BasePlayer player, string ShortName, int amount)
        {
			var item = ItemManager.FindItemDefinition(ShortName);
			if (item==null) return false;
			var haveCount = player.inventory.GetAmount(item.itemid);
            return (haveCount>= amount);
        }
        private void craftcards(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);

            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel()
            {
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                Image = {Color = "0 0 0 0.8", Material = "assets/content/ui/uibackgroundblur.mat"}
            }, "Overlay", Layer);

            container.Add(new CuiButton()
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Button = {Command = "UI_Destroy_bank", Color = "0 0 0 0"},
                Text = {Text = ""}
            },  Layer);
            container.Add(new CuiElement()
            {
                Parent = Layer,
                Components =
                {
                    new CuiRawImageComponent() 
                    {
                        Png = GetImage("bazukatop")
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = $"0.415625 0.521296",
                        AnchorMax = $"0.571875 0.799074"
                    },
                }
            });
            container.Add(new CuiLabel()
            {
                RectTransform = { AnchorMin = "0.378124 0.938889", AnchorMax = "0.633542 0.99537"},
                Text = { Text = $"<b>ПОЛУЧИТЬ КАРТОЧКУ</b>", Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", Font = "RobotoCondensed-Bold.ttf", FontSize = 24}
            }, Layer);
            
            container.Add(new CuiLabel()
            {
                RectTransform = { AnchorMin = "0.354584 0.431482", AnchorMax = "0.633229 0.512037"},
                Text = { Text = $"НЕОБХОДИМЫЕ ПРЕДМЕТЫ ДЛЯ ПОЛУЧЕНИЯ", Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", Font = "RobotoCondensed-Regular.ttf", FontSize = 22}
            }, Layer);
            
            float itemMinPosition = 1117f;
            float itemWidth = 0.403646f - 0.351563f;
            float itemMargin = 0.409895f - 0.403646f;
            int itemCount = Settings.CardItem.Count;
            float itemMinHeight = 0.315741f; 
            float itemHeight = 0.408333f - 0.315741f;
            
            if (itemCount > 5)
            {
                itemMinPosition = 0.5f - 5 / 2f * itemWidth - (5 - 1) / 2f * itemMargin;
                itemCount -= 5;
            }
            else itemMinPosition = 0.5f - itemCount / 2f * itemWidth - (itemCount - 1) / 2f * itemMargin;
            
            var countItem = 0;

            foreach (var itemCraft in Settings.CardItem)
            {
                countItem++;
                
                container.Add(new CuiElement()
                {
                    Parent = Layer,
                    Name = Layer + $".Item{itemCraft.ShortName}",
                    Components =
                    {
                        new CuiRawImageComponent()
                        {
                            Png = (string) ImageLibrary.Call("GetImage", itemCraft.ShortName),
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = $"{itemMinPosition} {itemMinHeight}",
                            AnchorMax = $"{itemMinPosition + itemWidth} {itemMinHeight + itemHeight}"
                        },
                    }
                });

                container.Add(new CuiLabel()
                    {
                        RectTransform = {AnchorMin = "0 0.05", AnchorMax = "0.98 1"},
                        Text = {Text = $"x{itemCraft.Amount}", Align = TextAnchor.LowerRight, FontSize = 12},
                    }, Layer + $".Item{itemCraft.ShortName}");
                
                itemMinPosition += (itemWidth + itemMargin);
                        
                if (countItem % 5 == 0) 
                {
                    itemMinHeight -= (itemHeight + (itemMargin * 2f));
                    
                    if (itemCount > 5)
                    {
                        itemMinPosition = 0.5f - 5 / 2f * itemWidth - (5 - 1) / 2f * itemMargin;
                        itemCount -= 5;
                    } else itemMinPosition = 0.5f - itemCount / 2f * itemWidth - (itemCount - 1) / 2f * itemMargin;
                }
            }
            
            itemMinHeight -= ((itemMargin * 3f) + (0.162037f - 0.0925926f));
 
            container.Add(new CuiButton() 
            {
                RectTransform = { AnchorMin = $"0.389062 {itemMinHeight}", AnchorMax = $"0.615103 {itemMinHeight + (0.162037f - 0.0925926f)}"},
                Button = {Close = Layer, Color = "0.9686275 0.9215686 0.8823529 0.03529412", Command = $"UI_craft_card_bank_UI", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"},
                Text = { Text = "СКРАФТИТЬ", Font = "RobotoCondensed-Regular.ttf", FontSize = 28, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1"}
            }, Layer);

            CuiHelper.AddUi(player, container);
        }

        [ChatCommand("craftcard")]
        void craftcarddd(BasePlayer player)
        {
            craftcards(player);
        }
        [ConsoleCommand("UI_craft_card_bank_UI")]
        void CraftCardss(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            var success = true;
            Dictionary<Item, int> items = new Dictionary<Item, int>();

            foreach (var craftedItem in Settings.CardItem)
                if (!HaveItem(player, craftedItem.ShortName, craftedItem.Amount)) success = false;
			//Забираем вещи даем карту
            if (!success){
                SendReply(player, "Вы не можете скрафтить предмет! Не хватает ингредиента!");
                return;
			}
			foreach (var craftedItem in Settings.CardItem)
				player.inventory.Take(null, ItemManager.FindItemDefinition(craftedItem.ShortName).itemid, craftedItem.Amount);
			player.SendConsoleCommand("UI_Backpack close");
			Item craft = ItemManager.CreateByName("keycard_green", 1, 1922602673);
			craft.name = $"Карточка №{craft.uid}";			
			if (!player.inventory.GiveItem(craft))
			{
				craft.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity, new Quaternion());
				SendReply(player, $"{craft.name} выпала из инвентаря так как он переполнен");
				return;
			}else
			SendReply(player, $"{craft.name} помещена в ваш инвентарь");
			//craft.MoveToContainer(player.inventory.containerMain);
        }
        public Item FindItem(BasePlayer player, string ShortName, int amount)
        {
            if (player.inventory.FindItemID(ItemManager.FindItemDefinition(ShortName).itemid) != null && player.inventory.FindItemID(ItemManager.FindItemDefinition(ShortName).itemid).amount >= amount)
                    return player.inventory.FindItemID(ItemManager.FindItemDefinition(ShortName).itemid);
            List<Item> items = new List<Item>();

                items.AddRange(player.inventory.FindItemIDs(ItemManager.FindItemDefinition(ShortName).itemid));
                return null;
        } 
        [ChatCommand("rere")]
        private void Ttt(BasePlayer player)
        {
            VendingMachine vms = GameManager.server.CreateEntity(
                    "assets/prefabs/deployable/vendingmachine/vendingmachine.deployed.prefab", player.transform.position) as VendingMachine;
                vms.skinID = 862125671;
                vms.shopName = "Банкомат";
                CardReader cr = GameManager.server.CreateEntity("assets/prefabs/io/electric/switches/cardreader.prefab", vms.transform.position) as CardReader;
                cr.accessLevel = 1;
                cr.skinID = 99323923;
                cr.SendNetworkUpdate();
                cr.UpdateHasPower(100, 1);
                cr.Spawn();
        }
        private void FindTowns(IEnumerable<MonumentInfo> monuments)
        {
            foreach (var monument in monuments)
            {
                Vector3 charrotv = new Vector3(11.5f, 0f, -6.8f);
                Quaternion charposv = new Quaternion(0,0.1f,0,0f);
                VendingMachine vms = GameManager.server.CreateEntity(
                    "assets/prefabs/deployable/vendingmachine/vendingmachine.deployed.prefab", monument.transform.TransformPoint(charrotv), monument.transform.rotation*charposv) as VendingMachine;
                vms.skinID = 862125671;
                vms.shopName = "Банкомат";
                vms.SendNetworkUpdate();
                Magazin.Add(vms);
                Vector3 charpos = new Vector3(-0.23f, -0.24f, 0.35f);
                CardReader cr = GameManager.server.CreateEntity("assets/prefabs/io/electric/switches/cardreader.prefab", vms.transform.TransformPoint(charpos), vms.transform.rotation) as CardReader;
                cr.accessLevel = 1;
                cr.skinID = 99323923;
                cr.SendNetworkUpdate();
                cr.UpdateHasPower(100, 1);
                Magazins.Add(cr);
            }
        }
        private void FindTowns2()
        {
            foreach (MonumentInfo monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
            {
                if (monument.name.Contains("compound"))
                {
                    Puts("start");
                    Vector3 charrotv = new Vector3(-25, 1.8f, -0.2f);
                    Quaternion charposv = new Quaternion(0,0.1f,0,0.1f);
                    VendingMachine vms = GameManager.server.CreateEntity(
                        "assets/prefabs/deployable/vendingmachine/vendingmachine.deployed.prefab", monument.transform.TransformPoint(charrotv), monument.transform.rotation*charposv) as VendingMachine;
                    vms.skinID = 862125671;
                    vms.shopName = "Магазин";
                    Magazin.Add(vms);
                    Vector3 charpos = new Vector3(-0.23f, -0.24f, 0.35f);
                    CardReader cr = GameManager.server.CreateEntity("assets/prefabs/io/electric/switches/cardreader.prefab", vms.transform.TransformPoint(charpos), vms.transform.rotation) as CardReader;
                    cr.accessLevel = 1;
                    cr.skinID = 993239235;
                    cr.UpdateHasPower(100, 1);
                    Magazins.Add(cr);
                }
            }
        }
        void spawn33()
        {
            foreach (var cr in Magazins)
            {
                cr?.Spawn();
            }
            foreach (var vm in Magazin)
            {
                vm?.Spawn();
            }
        }
        void OnServerSave()
        {
            SaveData();
        }

        void OnServerInitialized()
        {
            foreach (var vm in Magazin)
            {
                vm?.Kill();
            }
            foreach ( var cr in Magazins )
            {
                cr?.Kill();
            }    
            FindTowns(FindMonuments("supermarket_1"));
            FindTowns2();
            timer.Once(3, () => { spawn33(); });
            RespawnTimer = timer.Once(300f, () => { SaveData(); }); 
            AddImage("https://www.paymentsjournal.com/wp-content/uploads/2018/08/cheque-guarantee-card-1980148_1280.png","bazukatop"); //3k rubley
            BankomatDatas = Interface.Oxide.DataFileSystem.ReadObject<List<BankomatData>>("Bankomat/Players");
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Layer);
            }

            foreach (var vm in Magazin)
            {
                vm?.Kill();
            }

            foreach (var vms in Magazins)
            {
                vms?.Kill();
            }

            SaveData();
            if (!RespawnTimer.Destroyed)
            {
                RespawnTimer.Destroy();
            }
        }

        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("Bankomat/Players", BankomatDatas);
        }
    }
}