// Requires: WarMode
using static Oxide.Plugins.WarMode;

using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using System;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("War Mode Spawn UI", "mr01sam", "1.0.3")]
    [Description("Makes a player choose PVP or PVE when they spawn.")]
    public class WarModeSpawnUI : CovalencePlugin
    {
        // UI SETTINGS
        private const float w = 650;
        private const float h = 500;
        private const float padding = 32;
        private const string textcolor = "0.69804 0.66667 0.63529 1";
        private const string bgcolor = "0.08627 0.08627 0.08627 1";
        private const float panelimagesize = 64;
        private const float paneltitleheight = 32;
        private const float panelp = 32;
        private const float titleh = 48;
        private const float footerh = 48;
        private const float fadein = 0.3f;

        public bool newSaveLoaded = false;

        void OnServerInitialized()
        {
            if (newSaveLoaded == false)
            {
                PlayersShown = Interface.Oxide.DataFileSystem.ReadObject<HashSet<string>>($"WarMode/SpawnUI") ?? new HashSet<string>();
            }
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                OnPlayerSleepEnded(basePlayer);
            }
        }

        void OnServerSave()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"WarMode/SpawnUI", PlayersShown);
        }

        void OnNewSave(string filename)
        {
            newSaveLoaded = true;
        }

        void Unload()
        {
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(basePlayer, "wm.spawnui");
            }
            Interface.Oxide.DataFileSystem.WriteObject($"WarMode/SpawnUI", PlayersShown);
        }

        void OnPlayerSleepEnded(BasePlayer basePlayer)
        {
            if (basePlayer == null || PlayersShown.Contains(basePlayer.UserIDString)) { return; }
            ShowUI(basePlayer);
        }

        #region WarMode Hooks
        private object WarMode_OnFlagCommand(string userid, string mode)
        {
            if (!config.ShowWhenFlagCommandIsUsed || mode != null) { return null; }
            var basePlayer = FindBasePlayer(userid);
            if (basePlayer == null) { return null; }
            PlayersShown.Remove(userid);
            ShowUI(basePlayer);
            return true;
        }
        #endregion

        public HashSet<string> PlayersShown = new HashSet<string>();

        private void CreatePanel(CuiElementContainer container, int idx, string mode, string image, string titleText, string text, string panelcolor, string paneltextcolor, float scrollBarHeight = 0f)
        {
            var id = $"wm.spawnui.panel.{idx}";
            var size = 1f / config.Modes.Length;
            var pl = idx == 0 ? padding : padding / 2f;
            var pr = idx == config.Modes.Length - 1 ? padding : padding / 2f;
            // Button
            container.Add(new CuiElement
            {
                Parent = "wm.spawnui.list",
                Name = id,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"{(idx * size)} 0",
                        AnchorMax = $"{((idx+1) * size)} 1",
                        OffsetMin = $"{pl} {padding+scrollBarHeight}",
                        OffsetMax = $"{-pr} {-titleh-padding}"
                    },
                    new CuiButtonComponent
                    {
                        FadeIn = fadein,
                        Command = $"warmode.choose {mode}",
                        Color = panelcolor
                    }
                }
            });
            // Image
            container.Add(new CuiElement
            {
                Parent = id,
                Name = id + "img",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"0.5 1",
                        AnchorMax = $"0.5 1",
                        OffsetMin = $"{-panelimagesize/2} {-panelp-panelimagesize}",
                        OffsetMax = $"{panelimagesize/2} {-panelp}"
                    },
                    new CuiImageComponent
                    {
                        FadeIn = fadein,
                        Sprite = image,
                        Color = paneltextcolor
                    }
                }
            });
            // Title
            container.Add(new CuiElement
            {
                Parent = id,
                Name = id + "title",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"0 1",
                        AnchorMax = $"1 1",
                        OffsetMin = $"{panelp} {-panelp-panelimagesize-panelp-paneltitleheight}",
                        OffsetMax = $"{-panelp} {-panelp-panelimagesize-panelp}"
                    },
                    new CuiTextComponent
                    {
                        FadeIn = fadein,
                        Text = titleText,
                        FontSize = 24,
                        Align = UnityEngine.TextAnchor.UpperCenter,
                        Color = paneltextcolor
                    }
                }
            });
            // Text
            container.Add(new CuiElement
            {
                Parent = id,
                Name = id + "text",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"0 0",
                        AnchorMax = $"1 1",
                        OffsetMin = $"{panelp} {panelp}",
                        OffsetMax = $"{-panelp} {-panelp-panelimagesize-panelp-paneltitleheight-panelp}"
                    },
                    new CuiTextComponent
                    {
                        FadeIn = fadein,
                        Text = text,
                        FontSize = 16,
                        Align = UnityEngine.TextAnchor.UpperCenter,
                        Color = paneltextcolor
                    }
                }
            });
        }

        [Command("warmode.choose")]
        private void CmdChoosePlaystyle(IPlayer player, string command, string[] args)
        {
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null || PlayersShown.Contains(basePlayer.UserIDString)) { return; }
            var choice = args[0];
            var mode = WarMode.INSTANCE.Modes.GetValueOrDefault(choice);
            if (mode == null)
            {
                PrintWarning($"Player '{player.Name}' tried to choose mode '{choice}' but that mode doesn't exist. Make sure you configured the modes correctly!");
                return;
            }
            if (config.SetsFlaggingCooldown && WarMode.INSTANCE.config.Flagging.CooldownSeconds > 0)
            {
                WarMode.INSTANCE.Data.PlayerFlaggingCooldowns[basePlayer.UserIDString] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + WarMode.INSTANCE.config.Flagging.CooldownSeconds;
            }
            WarMode.INSTANCE.SetModeByName(basePlayer.UserIDString, choice);
            PlayersShown.Add(basePlayer.UserIDString);
            CuiHelper.DestroyUi(basePlayer, "wm.spawnui");
            foreach(var cmd in config.CommandsOnClose)
            {
                player.Command(cmd, basePlayer.UserIDString, choice);
            }
        }

        public void ShowUI(BasePlayer basePlayer)
        {
            if (PlayersShown.Contains(basePlayer.UserIDString)) { return; }
            if (!string.IsNullOrWhiteSpace(config.CustomUI))
            {
                CuiHelper.DestroyUi(basePlayer, "wm.spawnui");
                CuiHelper.AddUi(basePlayer, config.CustomUI);
                return;
            }
            var twoModes = config.Modes.Length <= 2;
            var container = new CuiElementContainer();
            // Shadow
            container.Add(new CuiElement
            {
                Parent = "Overlay",
                Name = "wm.spawnui",
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
                Parent = "wm.spawnui",
                Name = "wm.spawnui.content",
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
                Parent = "wm.spawnui.content",
                Name = "wm.spawnui.contenteffect",
                Components =
                {
                    new CuiImageComponent
                    {
                        FadeIn = fadein,
                        Color = "0.4 0.4 0.4 0.1",
                        Sprite = "assets/icons/square_gradient.png"
                    },
                    new CuiNeedsCursorComponent(),
                    new CuiNeedsKeyboardComponent()
                }
            });
            // Content List
            container.Add(new CuiElement
            {
                Parent = "wm.spawnui.content",
                Name = "wm.spawnui.list",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{padding/2} {footerh+(twoModes ? 0 : 24)}",
                        OffsetMax = $"{-padding/2} {0}"
                    },
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0"
                    },
                    new CuiScrollViewComponent
                    {
                        Horizontal = !twoModes,
                        Vertical = false,
                        ScrollSensitivity = -20f,
                        Elasticity = 0.1f,
                        DecelerationRate = 0.1f,
                        HorizontalScrollbar = new CuiScrollbar
                        {
                            Invert = true,
                            Size = 10
                        },
                        ContentTransform = new CuiRectTransform
                        {
                            AnchorMin = "0 0",
                            AnchorMax = twoModes ? $"1 1" : $"{0.45f * config.Modes.Length} 1"
                        }
                    },
                }
            });
            // Title
            container.Add(new CuiElement
            {
                Parent = "wm.spawnui.content",
                Name = "wm.spawnui.title",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"{0} {-titleh-padding}",
                        OffsetMax = $"{0} {-padding}"
                    },
                    new CuiTextComponent
                    {
                        FadeIn = fadein,
                        Text = Lang("choose your playstyle", basePlayer.UserIDString),
                        FontSize = 24,
                        Color = textcolor,
                        Align = UnityEngine.TextAnchor.UpperCenter
                    }
                }
            });
            var idx = 0;
            foreach(var mode in config.Modes)
            {
                CreatePanel(container, idx, mode.Mode, mode.Image, Lang(mode.Mode, basePlayer.UserIDString), Lang($"{mode.Mode} info", basePlayer.UserIDString),
                panelcolor: mode.Color1,
                paneltextcolor: mode.Color2);
                idx++;
            }

            // Footer
            var hasFlagPerm = permission.UserHasPermission(basePlayer.UserIDString, "warmode.flag");
            var footertext = hasFlagPerm ? Lang("can swap", basePlayer.UserIDString) : Lang("permanent", basePlayer.UserIDString);
            container.Add(new CuiElement
            {
                Parent = "wm.spawnui.content",
                Name = "wm.spawnui.footer",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 0",
                        OffsetMin = $"{padding} {8}",
                        OffsetMax = $"{-padding} {footerh}"
                    },
                    new CuiTextComponent
                    {
                        FadeIn = fadein,
                        Text = footertext,
                        Color = textcolor,
                        Align = UnityEngine.TextAnchor.MiddleCenter
                    }
                }
            });
            CuiHelper.DestroyUi(basePlayer, "wm.spawnui");
            CuiHelper.AddUi(basePlayer, container);
        }

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["pvp"] = "PVP",
                ["pve"] = "PVE",
                ["choose your playstyle"] = "CHOOSE YOUR PLAYSTYLE",
                ["can swap"] = "You can swap between these modes using the /flag command.",
                ["permanent"] = "This choice is permanent. Only an admin may change it for you later.",
                ["pve info"] = "You cannot attack, raid, steal or have these things done to you. Non-player enemies can still harm you.",
                ["pvp info"] = "You may attack, raid, and steal from other PVP players, but cannot do these things to PVE players."
            }, this);
        }

        private string Lang(string key, string id, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        #endregion

        #region Config
        private Configuration config;
        private class Configuration
        {
            public string CustomUI = "";
            public List<string> CommandsOnClose = new List<string>(); // first parameter is userid, second parameter is the mode they picked
            public bool ResetOnWipe = true;
            public bool ShowWhenFlagCommandIsUsed = false;
            public bool SetsFlaggingCooldown = false;
            public ModePanelConfig[] Modes = new ModePanelConfig[]
            {
                new ModePanelConfig
                {
                    Mode = "pve",
                    Color1 = "0.385 0.478 0.228 1",
                    Color2 = "0.76078 0.94510 0.41176 1",
                    Image = "assets/icons/peace.png"
                },
                new ModePanelConfig
                {
                    Mode = "pvp",
                    Color1 = "0.77255 0.23922 0.15686 1",
                    Color2 = "1 0.82353 0.44706 1",
                    Image = "assets/icons/weapon.png"
                }
            };
        }

        public class ModePanelConfig
        {
            public string Mode;
            public string Color1;
            public string Color2;
            public string Image;
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
