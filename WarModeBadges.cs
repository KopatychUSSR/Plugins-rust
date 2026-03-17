// Requires: WarMode
using static Oxide.Plugins.WarMode;

using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Collections.Generic;
using System;
using Oxide.Plugins.WarModeMethods;

namespace Oxide.Plugins
{
    [Info("War Mode Badges", "mr01sam", "1.0.1")]
    [Description("Shows a UI that indicates what mode a player has.")]
    public class WarModeBadges : CovalencePlugin
    {
        void OnServerInitialized()
        {
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                ShowUI(basePlayer);
            }
        }

        void Unload()
        {
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(basePlayer, ID);
            }
        }

        public const string ID = "warmodebadge";

        private void WarMode_PlayerModeUpdated(string userid, string modeId)
        {
            var basePlayer = BasePlayer.Find(userid);
            if (basePlayer == null) { return; }
            ShowUI(basePlayer);
        }

        void OnPlayerSleepEnded(BasePlayer basePlayer)
        {
            if (basePlayer == null) { return; }
            ShowUI(basePlayer);
        }

        public void ShowUI(BasePlayer basePlayer)
        {
            var mode = basePlayer.GetMode();
            if (!config.BadgesForModes.ContainsKey(mode.Name()))
            {
                CuiHelper.DestroyUi(basePlayer, ID);
                return;
            }

            var badge = config.BadgesForModes[mode.Name()];

            if (!string.IsNullOrWhiteSpace(badge.CustomJson))
            {
                CuiHelper.DestroyUi(basePlayer, ID);
                CuiHelper.AddUi(basePlayer, badge.CustomJson);
            }

            var container = new CuiElementContainer();

            // Background
            var background = new CuiElement
            {
                Name = ID,
                Parent = "Hud",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = badge.Background.AnchorMin,
                        AnchorMax = badge.Background.AnchorMax,
                        OffsetMin = badge.Background.OffsetMin,
                        OffsetMax = badge.Background.OffsetMax,
                    }
                }
            };
            var backgroundImage = GetImageComponent(badge.Background.Image, badge.Background.Color, badge.Background.UseRawImage);
            if (backgroundImage != null)
            {
                background.Components.Add(backgroundImage);
            }
            container.Add(background);

            // Icon
            if (badge.Icon.Show)
            {
                var icon = new CuiElement
                {
                    Name = ID + "icon",
                    Parent = ID,
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = badge.Icon.AnchorMin,
                            AnchorMax = badge.Icon.AnchorMax,
                            OffsetMin = badge.Icon.OffsetMin,
                            OffsetMax = badge.Icon.OffsetMax,
                        }
                    }
                };
                var iconImage = GetImageComponent(badge.Icon.Image, badge.Icon.Color, badge.Icon.UseRawImage);
                if (iconImage != null)
                {
                    icon.Components.Add(iconImage);
                }
                container.Add(icon);
            }

            // Text
            if (badge.Text.Show)
            {
                var text = new CuiElement
                {
                    Name = ID + "text",
                    Parent = ID,
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = badge.Text.AnchorMin,
                            AnchorMax = badge.Text.AnchorMax,
                            OffsetMin = badge.Text.OffsetMin,
                            OffsetMax = badge.Text.OffsetMax,
                        },
                        new CuiTextComponent
                        {
                            Font = badge.Text.Font,
                            Text = string.Format(badge.Text.Format, mode.Title(basePlayer.UserIDString, colored: false)),
                            Align = badge.Text.Align,
                            FontSize = badge.Text.FontSize,
                            Color = badge.Text.Color
                        }
                    }
                };
                container.Add(text);
            }

            CuiHelper.DestroyUi(basePlayer, ID);
            CuiHelper.AddUi(basePlayer, container);
        }

        public ICuiComponent GetImageComponent(string image, string color, bool useraw)
        {
            if (string.IsNullOrWhiteSpace(color))
            {
                return null;
            }
            var hasImage = !string.IsNullOrWhiteSpace(image);
            var isSprite = hasImage && image.StartsWith("assets/icons/");
            if (useraw)
            {
                var comp = new CuiRawImageComponent();
                if (isSprite)
                {
                    comp.Sprite = image;
                }
                else if (hasImage)
                {
                    comp.Url = image;
                }
                comp.Color = color;
                return comp;
            }
            else
            {
                var comp = new CuiImageComponent();
                if (isSprite)
                {
                    comp.Sprite = image;
                }
                else if (hasImage)
                {
                    comp.Png = image;
                }
                comp.Color = color;
                return comp;
            }
        }

        #region Config
        private Configuration config;
        private class Configuration
        {
            public Dictionary<string, BadgeConfig> BadgesForModes = new Dictionary<string, BadgeConfig>()
            {
                ["pvp"] = new BadgeConfig(),
                ["pve"] = new BadgeConfig
                {
                    Icon = new BadgeIconConfig
                    {
                        Image = "assets/icons/peace.png",
                        Color = "0.545 0.855 0 1"
                    }
                }
            };
        }

        public class BadgeConfig
        {
            public string CustomJson = null;
            public BadgeBackgroundConfig Background = new BadgeBackgroundConfig();
            public BadgeIconConfig Icon = new BadgeIconConfig();
            public BadgeTextConfig Text = new BadgeTextConfig();
        }

        public class BadgeTextConfig
        {
            public bool Show { get; set; } = true;
            public string Format { get; set; } = "{0}";
            public string Font { get; set; } = "RobotoCondensed-Bold.ttf";
            public int FontSize { get; set; } = 14;
            public string Color { get; set; } = "1 1 1 1";
            public string AnchorMin { get; set; } = "0 0";
            public string AnchorMax { get; set; } = "1 1";
            public string OffsetMin { get; set; } = "8 8";
            public string OffsetMax { get; set; } = "-8 -8";
            public UnityEngine.TextAnchor Align { get; set; } = TextAnchor.LowerCenter;
        }

        public class BadgeIconConfig
        {
            public bool Show { get; set; } = true;
            public string Color { get; set; } = "0.77255 0.23922 0.15686 1";
            public string Image { get; set; } = "assets/icons/weapon.png";
            public bool UseRawImage { get; set; } = false;
            public string AnchorMin { get; set; } = "0 0";
            public string AnchorMax { get; set; } = "1 1";
            public string OffsetMin { get; set; } = "16 24";
            public string OffsetMax { get; set; } = "-16 -8";
        }

        public class BadgeBackgroundConfig
        {
            public string Color { get; set; } = "0 0 0 0";
            public string Image { get; set; } = null;
            public bool UseRawImage { get; set; } = false;
            public string AnchorMin { get; set; } = "0.5 0";
            public string AnchorMax { get; set; } = "0.5 0";
            public string OffsetMin { get; set; } = "-32 78";
            public string OffsetMax { get; set; } = "32 142";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            //LoadDefaultConfig();
            //SaveConfig();
            //return;
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
