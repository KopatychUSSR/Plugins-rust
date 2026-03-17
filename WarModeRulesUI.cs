// Requires: WarMode
using static Oxide.Plugins.WarMode;

using Oxide.Plugins.WarModeMethods;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using System.Linq;
using System;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("War Mode Rules UI", "mr01sam", "1.1.0")]
    [Description("Panel that will display the rules of their current WarMode mode to a player.")]
    public class WarModeRulesUI : CovalencePlugin
    {
        // UI SETTINGS
        private const float w = 650;
        private const float h = 600;
        private const float padding = 32;
        private const string titlecolor = "0.89804 0.86667 0.83529 ";
        private const string textcolor = "0.69804 0.66667 0.63529 1";
        private const string bgcolor = "0.08627 0.08627 0.08627 1";
        private const float panelimagesize = 64;
        private const float paneltitleheight = 32;
        private const float panelp = 32;
        private const float titleh = 48;
        private const float footerh = 48;
        private const float fadein = 0.3f;

        void OnServerInitialized()
        {
            if (!string.IsNullOrWhiteSpace(config.ChatCommand))
            {
                AddCovalenceCommand(config.ChatCommand, nameof(CmdRules));
                //foreach(var basePlayer in BasePlayer.activePlayerList)
                //{
                //    ShowUI(basePlayer);
                //}
            }
        }

        [Command("rules")]
        private void CmdRules(IPlayer player, string command, string[] args)
        {
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null) { return; }
            ShowUI(basePlayer);
        }

        void Unload()
        {
        }

        public void ShowUI(BasePlayer basePlayer)
        {
            var mode = basePlayer.GetMode();
            var container = new CuiElementContainer();
            // Shadow
            container.Add(new CuiElement
            {
                Parent = "Overlay",
                Name = "wm.rules",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = "0 0",
                        OffsetMax = "0 0"
                    },
                    new CuiImageComponent
                    {
                        FadeIn = fadein,
                        Color = "0 0 0 0.95",
                        Material = "assets/content/ui/uibackgroundblur.mat"
                    }
                }
            });
            // Content
            container.Add(new CuiElement
            {
                Parent = "wm.rules",
                Name = "wm.rules.content",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = $"{-w/2} {-h/2}",
                        OffsetMax = $"{w/2} {h/2}"
                    },
                    new CuiImageComponent
                    {
                        FadeIn = fadein,
                        Color = bgcolor,
                    },
                    new CuiNeedsCursorComponent(),
                    new CuiNeedsKeyboardComponent()
                }
            });
            // Content Effect
            container.Add(new CuiElement
            {
                Parent = "wm.rules.content",
                Name = "wm.rules.contenteffect",
                Components =
                {
                    new CuiImageComponent
                    {
                        FadeIn = fadein,
                        Color = "0.4 0.4 0.4 0.1",
                        Sprite = "assets/icons/square_gradient.png"
                    }
                }
            });
            // Title
            container.Add(new CuiElement
            {
                Parent = "wm.rules.content",
                Name = "wm.rules.title",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = Lang(basePlayer.UserIDString, "title", mode.Title(basePlayer.UserIDString)),
                        Color = textcolor,
                        FontSize = 20,
                        Align = UnityEngine.TextAnchor.MiddleCenter,
                        FadeIn = fadein,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"0 {-titleh}",
                        OffsetMax = $"0 0"
                    },
                }
            });
            // Footer
            var btnw = 100;
            var btnh = 40;
            container.Add(new CuiElement
            {
                Parent = "wm.rules.content",
                Name = "wm.rules.footer",
                Components =
                {
                    new CuiButtonComponent
                    {
                        FadeIn = fadein,
                        Color = "0.35490 0.40980 0.24510 1",
                        Close = "wm.rules"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0",
                        AnchorMax = "0.5 0",
                        OffsetMin = $"{-btnw/2} {8}",
                        OffsetMax = $"{btnw/2} {8+btnh}"
                    },
                }
            });
            container.Add(new CuiElement
            {
                Parent = "wm.rules.footer",
                Components =
                {
                    new CuiTextComponent
                    {
                        FadeIn = fadein,
                        Text = Lang(basePlayer.UserIDString, "okay").ToUpper(),
                        Color = "0.76078 0.94510 0.41176 1",
                        Align = UnityEngine.TextAnchor.MiddleCenter,
                        FontSize = 18
                    }
                }
            });
            // Content
            var cpad = 8;
            container.Add(new CuiElement
            {
                Parent = "wm.rules.content",
                Name = "wm.rules.list",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{cpad} {titleh+cpad}",
                        OffsetMax = $"{0} {-titleh}"
                    },
                    new CuiImageComponent
                    {
                        FadeIn = fadein,
                        Color = "0 0 0 0"
                    },
                    //new CuiScrollViewComponent
                    //{
                    //    Vertical = true,
                    //    Horizontal = false,
                    //    ScrollSensitivity = 20,
                    //    Elasticity = 0.1f,
                    //    VerticalScrollbar = new CuiScrollbar
                    //    {
                    //        //Invert = true
                    //    },
                    //    ContentTransform = new CuiRectTransform
                    //    {
                    //        AnchorMin = "0 -2",
                    //        AnchorMax = "1 0"
                    //    }
                    //}
                }
            });

            // Panels
            // EDIT HERE
            var idx = 0;
            var attackedBy = WarMode.INSTANCE.Modes.Where(x => x.Value.CanAttackTypes.GetValueOrDefault(TargetType.players).Contains(mode.Name)).Select(x => x.Key).Distinct();
            CreatePanel2(container,
                row: 1,
                col: 0,
                icon: "assets/icons/weapon.png",
                title: Lang(basePlayer.UserIDString, "attacking"),
                text1: !mode.CanAttackTypes.GetValueOrDefault(TargetType.players).Any() ?
                    Lang(basePlayer.UserIDString, "can attack none") :
                    Lang(basePlayer.UserIDString, "can attack", FormatGroups(basePlayer, mode.CanAttackTypes.GetValueOrDefault(TargetType.players))),
                text2: !attackedBy.Any() ?
                    Lang(basePlayer.UserIDString, "can attack by none") :
                    Lang(basePlayer.UserIDString, "can attack by", FormatGroups(basePlayer, attackedBy))
            );
            idx++;

            var raidedBy = WarMode.INSTANCE.Modes.Where(x => x.Value.CanAttackTypes.GetValueOrDefault(TargetType.buildings).Contains(mode.Name)).Select(x => x.Key).Distinct();
            CreatePanel2(container,
                row: 1,
                col: 1,
                icon: "assets/icons/explosion_sprite.png",
                title: Lang(basePlayer.UserIDString, "raiding"),
                text1: !mode.CanAttackTypes.GetValueOrDefault(TargetType.buildings).Any() ?
                    Lang(basePlayer.UserIDString, "can raid none") :
                    Lang(basePlayer.UserIDString, "can raid", FormatGroups(basePlayer, mode.CanAttackTypes.GetValueOrDefault(TargetType.buildings))),
                text2: !raidedBy.Any() ?
                    Lang(basePlayer.UserIDString, "can raid by none") :
                    Lang(basePlayer.UserIDString, "can raid by", FormatGroups(basePlayer, raidedBy))
            );
            idx++;

            var lootedBy = WarMode.INSTANCE.Modes.Where(x => x.Value.CanLootTypes.GetValueOrDefault(TargetType.containers).Contains(mode.Name)).Select(x => x.Key).Distinct();
            CreatePanel2(container,
                row: 0,
                col: 0,
                icon: "assets/icons/player_loot.png",
                title: Lang(basePlayer.UserIDString, "looting"),
                text1: !mode.CanLootTypes.GetValueOrDefault(TargetType.containers).Any() ?
                    Lang(basePlayer.UserIDString, "can loot none") :
                    Lang(basePlayer.UserIDString, "can loot", FormatGroups(basePlayer, mode.CanLootTypes.GetValueOrDefault(TargetType.containers))),
                text2: !lootedBy.Any() ?
                    Lang(basePlayer.UserIDString, "can loot by none") :
                    Lang(basePlayer.UserIDString, "can loot by", FormatGroups(basePlayer, lootedBy))
            );
            idx++;

            var targetBy = WarMode.INSTANCE.Modes.Where(x => x.Value.CanTargetTypes.GetValueOrDefault(TargetType.turrets).Contains(mode.Name)).Select(x => x.Key).Distinct();
            CreatePanel2(container,
                row: 0,
                col: 1,
                icon: "assets/icons/target.png",
                title: Lang(basePlayer.UserIDString, "trap targeting"),
                text1: !mode.CanTargetTypes.GetValueOrDefault(TargetType.turrets).Any() ?
                    Lang(basePlayer.UserIDString, "can trap target none") :
                    Lang(basePlayer.UserIDString, "can trap target", FormatGroups(basePlayer, mode.CanTargetTypes.GetValueOrDefault(TargetType.turrets))),
                text2: !lootedBy.Any() ?
                    Lang(basePlayer.UserIDString, "can trap target by none") :
                    Lang(basePlayer.UserIDString, "can trap target by", FormatGroups(basePlayer, lootedBy))
            );
            idx++;
            //if (CONFIG.Settings.AllowVehicleModeOwnership)
            //{
            //    var mountedVehicleBy = WarMode.INSTANCE.Modes.Where(x => x.Value.CanMountOwnedVehicles.Contains(mode.Name)).Select(x => x.Key).Distinct();
            //    CreatePanel(container,
            //        idx: idx,
            //        icon: "assets/icons/horse_ride" +
            //        ".png",
            //        title: Lang(basePlayer.UserIDString, "vehicle mounting"),
            //        text1: !mode.CanMountOwnedVehicles.Any() ?
            //            Lang(basePlayer.UserIDString, "can mount owned vehicles none") :
            //            Lang(basePlayer.UserIDString, "can mount owned vehicles", FormatGroups(basePlayer, mode.CanMountOwnedVehicles)),
            //        text2: !mountedVehicleBy.Any() ?
            //            Lang(basePlayer.UserIDString, "can mount owned vehicles by none") :
            //            Lang(basePlayer.UserIDString, "can mount owned vehicles by", FormatGroups(basePlayer, mountedVehicleBy))
            //    );
            //    idx++;

            //    var lootedVehicleBy = WarMode.INSTANCE.Modes.Where(x => x.Value.CanLootOwnedVehicles.Contains(mode.Name)).Select(x => x.Key).Distinct();
            //    CreatePanel(container,
            //        idx: idx,
            //        icon: "assets/icons/horse_ride" +
            //        ".png",
            //        title: Lang(basePlayer.UserIDString, "vehicle looting"),
            //        text1: !mode.CanLootOwnedVehicles.Any() ?
            //            Lang(basePlayer.UserIDString, "can loot owned vehicles none") :
            //            Lang(basePlayer.UserIDString, "can loot owned vehicles", FormatGroups(basePlayer, mode.CanLootOwnedVehicles)),
            //        text2: !lootedVehicleBy.Any() ?
            //            Lang(basePlayer.UserIDString, "can loot owned vehicles by none") :
            //            Lang(basePlayer.UserIDString, "can loot owned vehicles by", FormatGroups(basePlayer, lootedVehicleBy))
            //    );
            //    idx++;
            //}

            CuiHelper.DestroyUi(basePlayer, "wm.rules");
            CuiHelper.AddUi(basePlayer, container);
        }

        private string FormatGroups(BasePlayer basePlayer, IEnumerable<string> groups)
        {
            return groups.Select(x => WarMode.INSTANCE.Modes.GetValueOrDefault(x)?.Title(basePlayer.UserIDString)).ToSentence();
        }

        private void CreatePanel(CuiElementContainer container, int idx, string icon, string title, string text1, string text2)
        {

            var panelid = $"wm.rules.panel.{idx}";
            var panelth = 24;

            var panelh = 120;
            var panelg = 8;

            var down = idx * (panelh + panelg);
            var panelp = 8;
            var txtp = 18;

            // Panel
            container.Add(new CuiElement
            {
                Parent = "wm.rules.list",
                Name = panelid,
                Components =
                {
                    new CuiImageComponent
                    {
                        FadeIn = fadein,
                        Color = "1 1 1 0.05",
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"0 {-down-panelh}",
                        OffsetMax = $"{-24} {-down}"
                    },
                }
            });

            var iconw = 16;
            var iconp = 8;
            var icono = 1.5f;
            // Icon
            container.Add(new CuiElement
            {
                Parent = panelid,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "1 0.82353 0.44706 1",
                        Sprite = icon,
                        FadeIn = fadein,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = $"{panelp} {-panelp-iconw-icono}",
                        OffsetMax = $"{panelp+iconw} {-panelp-icono}"
                    },
                }
            });


            // Title
            container.Add(new CuiElement
            {
                Parent = panelid,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = title.ToUpper(),
                        Color = "1 0.82353 0.44706 1",
                        FontSize = 18,
                        Align = UnityEngine.TextAnchor.UpperLeft,
                        FadeIn = fadein,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"{iconw + iconp + panelp} {-panelth-panelp}",
                        OffsetMax = $"{-panelp} {-panelp}"
                    },
                }
            });

            // Text1
            container.Add(new CuiElement
            {
                Parent = panelid,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = text1,
                        Color = textcolor,
                        FontSize = 14,
                        Align = UnityEngine.TextAnchor.UpperLeft,
                        FadeIn = fadein,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0.5",
                        AnchorMax = "1 1",
                        OffsetMin = $"{panelp} {0}",
                        OffsetMax = $"{-panelp} {-panelth-txtp}"
                    },
                }
            });

            // Text2
            container.Add(new CuiElement
            {
                Parent = panelid,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = text2,
                        Color = textcolor,
                        FontSize = 14,
                        Align = UnityEngine.TextAnchor.UpperLeft,
                        FadeIn = fadein,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 0.5",
                        OffsetMin = $"{panelp} {0}",
                        OffsetMax = $"{-panelp} {-txtp}"
                    },
                }
            });
        }

        private void CreatePanel2(CuiElementContainer container, int row, int col, string icon = "assets/icons/close.png", string title = "ATTACKING", string text1 = "You can attack players with these modes", string text2 = "You can be attacked by players with these modes")
        {
            var panelid = $"wm.rules.panel.{row}.{col}";
            var panelth = 24;

            var panelh = 120;
            var panelg = 8;

            //var down = idx * (panelh + panelg);
            var panelp = 8;
            var txtp = 18;

            // Panel
            container.Add(new CuiElement
            {
                Parent = "wm.rules.list",
                Name = panelid,
                Components =
                {
                    new CuiImageComponent
                    {
                        FadeIn = fadein,
                        Color = "1 1 1 0.05",
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{(col == 0 ? 0 : 0.5f)} {(row == 0 ? 0 : 0.5f)}",
                        AnchorMax = $"{(col == 0 ? 0.5f : 1f)} {(row == 0 ? 0.5f : 1f)}",
                        OffsetMin = $"{panelp} {panelp}",
                        OffsetMax = $"{-panelp} {-panelp}"
                    },
                }
            });

            var iconw = 32;
            var iconp = 8;
            var icono = 1.5f;
            // Icon
            container.Add(new CuiElement
            {
                Parent = panelid,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "1 0.82353 0.44706 1",
                        Sprite = icon,
                        FadeIn = fadein,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = $"{panelp} {-panelp-iconw-icono}",
                        OffsetMax = $"{panelp+iconw} {-panelp-icono}"
                    },
                }
            });


            // Title
            container.Add(new CuiElement
            {
                Parent = panelid,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = title.ToUpper(),
                        Color = "1 0.82353 0.44706 1",
                        FontSize = 18,
                        Align = UnityEngine.TextAnchor.MiddleCenter,
                        FadeIn = fadein,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"{panelp} {-iconw-panelp}",
                        OffsetMax = $"{-panelp} {-panelp}"
                    },
                }
            });

            // Text1
            container.Add(new CuiElement
            {
                Parent = panelid,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = text1,
                        Color = textcolor,
                        FontSize = 14,
                        Align = UnityEngine.TextAnchor.UpperLeft,
                        FadeIn = fadein,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0.4",
                        AnchorMax = "1 0.8",
                        OffsetMin = $"{panelp} {0}",
                        OffsetMax = $"{-panelp} {-panelth-txtp}"
                    },
                }
            });

            // Text2
            container.Add(new CuiElement
            {
                Parent = panelid,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = text2,
                        Color = textcolor,
                        FontSize = 14,
                        Align = UnityEngine.TextAnchor.UpperLeft,
                        FadeIn = fadein,
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 0.4",
                        OffsetMin = $"{panelp} {0}",
                        OffsetMax = $"{-panelp} {-txtp}"
                    },
                }
            });
        }

        #region Localization
        protected override void LoadDefaultMessages()
        {
            // EDIT HERE
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["title"] = "You are in {0} mode",

                // Attacking Players
                ["attacking"] = "Attacking",
                ["can attack"] = "You can attack {0} players.",
                ["can attack none"] = "You cannot attack any players.",
                ["can attack by"] = "You can be attacked by {0} players.",
                ["can attack by none"] = "You cannot be attacked by any players.",
                // Raiding Players
                ["raiding"] = "Raiding",
                ["can raid"] = "You can raid bases owned by {0} players.",
                ["can raid none"] = "You cannot raid bases owned by any players.",
                ["can raid by"] = "Your bases can be raided by {0} players.",
                ["can raid by none"] = "Your bases can not be raided by any players.",
                // Loot Players
                ["looting"] = "Looting",
                ["can loot"] = "You can loot containers owned by {0} players.",
                ["can loot none"] = "You cannot loot any containers owned by any players.",
                ["can loot by"] = "Your containers can be looted by {0} players.",
                ["can loot by none"] = "Your containers cannot be looted by any players.",
                // Traps
                ["trap targeting"] = "Turret Targeting",
                ["can trap target"] = "Turrets that you place can be triggered by {0} players.",
                ["can trap target none"] = "Turrets that you place cannot be triggered by any players.",
                ["can trap target by"] = "You will trigger turrets placed by {0} players.",
                ["can trap target by none"] = "You will not trigger turrets placed by any players."
            }, this);
        }

        private string Lang(string id, string key, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        #endregion

        #region Config
        private Configuration config;
        private class Configuration
        {
            public string ChatCommand = "rules";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
            SaveConfig();
        }
        protected override void SaveConfig() => Config.WriteObject(config);
        protected override void LoadDefaultConfig() => config = new Configuration();
        #endregion
    }
}
