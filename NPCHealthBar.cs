using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using UnityEngine;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("NPC Health Bar", "Chris", "1.0.6")]
    public class NPCHealthBar : RustPlugin
    {

        [PluginReference]
        private Plugin ImageLibrary;

        #region Hooks

        Dictionary<BasePlayer, long> lastHits = new Dictionary<BasePlayer, long>();

        List<BasePlayer> toDelete = new List<BasePlayer>();

        void OnServerInitialized(bool initial)
        {
            /*
            Puts("Plugin Loaded!");
            Server.Broadcast($"Plugin Loaded!");
            */
            if (config.requirePerm)
                permission.RegisterPermission("npchealthbar.use", this);

            if (ImageLibrary != null)
            {
                foreach (string npc in config.images.Keys)
                {
                    ImageLibrary.Call("AddImage", config.images[npc], config.images[npc]);
                }
            }
            else
            {
                Puts("ImageLibrary not found, images won't load.");
            }

            timer.Every(1f, () =>
            {
                toDelete.Clear();

                foreach(var player in lastHits.Keys)
                {
                    //Puts($"{player} {lastHits[player]}");
                    if((DateTimeOffset.UtcNow.ToUnixTimeSeconds()-lastHits[player])>config.timeToDestroy)
                    {
                        CuiHelper.DestroyUi(player, "hp_background");
                        
                        toDelete.Add(player);

                    }
                }

                foreach(var player in toDelete)
                {
                    lastHits.Remove(player);
                }

                toDelete.Clear();
            });
            
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            try
            {
                if (info.InitiatorPlayer != null)
                {
                    CuiHelper.DestroyUi(info.InitiatorPlayer, "hp_background");
                }
            }
            catch
            {
                //
            }
        }

        void OnEntityTakeDamage(BradleyAPC apc, HitInfo info)
        {
            

            if (info.InitiatorPlayer != null)
            {
                var attacker = info.InitiatorPlayer;
                if (config.requirePerm)
                {
                    if (!permission.UserHasPermission(attacker.UserIDString, "npchealthbar.use"))
                    {
                        return;
                    }
                }

                lastHits.Remove(attacker);
                lastHits.Add(attacker, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                NextTick(() => {
                    try
                    {

                        if (config.blacklist.Contains(apc.ShortPrefabName))
                        {
                            return;
                        }
                        if (apc.health < 1)
                        {
                            CuiHelper.DestroyUi(info.InitiatorPlayer, "hp_background");
                            return;
                        }
                        float onePercent = apc.MaxHealth() / 100;
                        float leftPercent = apc.Health() / onePercent;

                        var moj_ui = new CuiElementContainer();

                        // background panel
                        moj_ui.Add(new CuiPanel
                        {
                            Image = { Color = config.panelbg.color, FadeIn = config.panelbg.fadeIn },
                            RectTransform = { AnchorMin = config.panelbg.anchorMin, AnchorMax = config.panelbg.anchorMax },
                            CursorEnabled = false,
                            FadeOut = config.panelbg.fadeOut
                        },
                            "Overlay",
                            "hp_background"
                        );

                        //overline
                        moj_ui.Add(new CuiPanel
                        {
                            Image = { Color = config.overline.color, FadeIn = config.overline.fadeIn },
                            RectTransform = { AnchorMin = config.overline.anchorMin, AnchorMax = config.overline.anchorMax },
                            CursorEnabled = false,
                            FadeOut = config.overline.fadeOut
                        },
                            "hp_background",
                            "bar_overline"
                        );

                        // progress panel     
                        moj_ui.Add(new CuiPanel
                        {
                            Image = { Color = config.panelHp.color, FadeIn = config.panelHp.fadeIn },
                            RectTransform = { AnchorMin = config.panelHp.anchorMin, AnchorMax = $"{leftPercent / 100} {config.panelHp.anchorMax}" },
                            CursorEnabled = false,
                            FadeOut = config.panelHp.fadeOut
                        },
                            "bar_overline",
                            "hp_progress"
                        );

                        // status text
                        moj_ui.Add(new CuiElement
                        {
                            Parent = "hp_background",
                            Name = "hp_statustext",
                            Components =
                        {
                            new CuiTextComponent
                            {
                                Text = config.statusText.text.Replace("{currenthp}", $"{(int) apc.Health()}").Replace("{maxhp}", $"{apc.MaxHealth()}"),
                                FontSize = config.statusText.fontsize,
                                Font = config.statusText.font,
                                Align = config.statusText.align,
                                Color = config.statusText.color,
                                FadeIn = config.statusText.fadeIn
                            },

                            new CuiRectTransformComponent
                            {
                                AnchorMin = config.statusText.anchorMin,
                                AnchorMax = config.statusText.anchorMax
                            }
                        },
                            FadeOut = config.statusText.fadeOut
                        });


                        //name text
                        if (config.names.ContainsKey(apc.ShortPrefabName))
                        {
                            moj_ui.Add(new CuiElement
                            {
                                Parent = "hp_background",
                                Name = "hp_name",
                                Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = config.nameText.text.Replace("{name}", $"{config.names[apc.ShortPrefabName]}"),
                                    FontSize = 12,
                                    Font = config.nameText.font,
                                    Align = config.nameText.align,
                                    Color = config.nameText.color,
                                    FadeIn = config.nameText.fadeIn,
                                },

                                new CuiRectTransformComponent
                                {
                                    AnchorMin = config.nameText.anchorMin,
                                    AnchorMax = config.nameText.anchorMax
                                }
                            },
                                FadeOut = config.nameText.fadeOut
                            });
                        }

                        // check if image is downloaded
                        if (ImageLibrary != null && config.images.ContainsKey(apc.ShortPrefabName) && (bool)ImageLibrary.Call("HasImage", config.images[apc.ShortPrefabName]) && config.enableImages)
                        {
                            moj_ui.Add(new CuiPanel
                            {
                                Image = { Color = config.iconPanel.color, FadeIn = config.iconPanel.fadeIn },
                                RectTransform = { AnchorMin = config.iconPanel.anchorMin, AnchorMax = config.iconPanel.anchorMax },
                                CursorEnabled = false,
                                FadeOut = config.iconPanel.fadeOut
                            },
                                "hp_background",
                                "hp_imagepanel"
                            );

                            moj_ui.Add(new CuiElement
                            {
                                Parent = "hp_imagepanel",
                                Components =
                        {
                            new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", config.images[apc.ShortPrefabName]), Sprite = "assets/content/textures/generic/fulltransparent.tga", FadeIn = config.iconPanel.fadeIn},
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                        }
                            });
                        }


                        // odpocet
                        moj_ui.Add(new CuiElement
                        {
                            Parent = "Overlay",
                            Name = "notifications_countdown",
                            Components = {
                        new CuiTextComponent{ Text = "", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1", Font = "robotocondensed-regular.ttf",},
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 0" },
                        //new CuiCountdownComponent { EndTime = 0, StartTime = config.timeToDestroy == 0 ? 4 : config.timeToDestroy , Command = "close_myui"}
                    },
                            FadeOut = 0f
                        });

                        CuiHelper.DestroyUi(attacker, "notifications_countdown");
                        CuiHelper.DestroyUi(attacker, "hp_background");
                        CuiHelper.AddUi(attacker, moj_ui);
                    }
                    catch
                    {
                        //
                    }
                });
                //CuiHelper.DestroyUi(info.InitiatorPlayer, "hp_background");
            }

        }


        void OnEntityTakeDamage(PatrolHelicopter heli, HitInfo info)
        {
            

            if (info.InitiatorPlayer != null)
            {
                var attacker = info.InitiatorPlayer;
                if (config.requirePerm)
                {
                    if (!permission.UserHasPermission(attacker.UserIDString, "npchealthbar.use"))
                    {
                        return;
                    }
                }

                lastHits.Remove(attacker);
                lastHits.Add(attacker, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                NextTick(() => {

                    try
                    {
                        if (info == null || info.HitEntity == null || info.HitEntity.IsDestroyed)
                        {
                            return;
                        }

                        if (config.blacklist.Contains(heli.ShortPrefabName))
                        {
                            //CuiHelper.DestroyUi(attacker, "hp_background");
                            return;
                        }

                        if (heli.myAI.isDead || heli.health < 1)
                        {
                            CuiHelper.DestroyUi(info.InitiatorPlayer, "hp_background");
                            return;
                        }

                        float onePercent = info.HitEntity.MaxHealth() / 100;
                        float leftPercent = info.HitEntity.Health() / onePercent;

                        var moj_ui = new CuiElementContainer();

                        // background panel
                        moj_ui.Add(new CuiPanel
                        {
                            Image = { Color = config.panelbg.color, FadeIn = config.panelbg.fadeIn },
                            RectTransform = { AnchorMin = config.panelbg.anchorMin, AnchorMax = config.panelbg.anchorMax },
                            CursorEnabled = false,
                            FadeOut = config.panelbg.fadeOut
                        },
                            "Overlay",
                            "hp_background"
                        );

                        //overline
                        moj_ui.Add(new CuiPanel
                        {
                            Image = { Color = config.overline.color, FadeIn = config.overline.fadeIn },
                            RectTransform = { AnchorMin = config.overline.anchorMin, AnchorMax = config.overline.anchorMax },
                            CursorEnabled = false,
                            FadeOut = config.overline.fadeOut
                        },
                            "hp_background",
                            "bar_overline"
                        );

                        // progress panel     
                        moj_ui.Add(new CuiPanel
                        {
                            Image = { Color = config.panelHp.color, FadeIn = config.panelHp.fadeIn },
                            RectTransform = { AnchorMin = config.panelHp.anchorMin, AnchorMax = $"{leftPercent / 100} {config.panelHp.anchorMax}" },
                            CursorEnabled = false,
                            FadeOut = config.panelHp.fadeOut
                        },
                            "bar_overline",
                            "hp_progress"
                        );

                        // status text
                        moj_ui.Add(new CuiElement
                        {
                            Parent = "hp_background",
                            Name = "hp_statustext",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = config.statusText.text.Replace("{currenthp}", $"{(int) info.HitEntity.Health()}").Replace("{maxhp}", $"{info.HitEntity.MaxHealth()}"),
                                    FontSize = config.statusText.fontsize,
                                    Font = config.statusText.font,
                                    Align = config.statusText.align,
                                    Color = config.statusText.color,
                                    FadeIn = config.statusText.fadeIn
                                },

                                new CuiRectTransformComponent
                                {
                                    AnchorMin = config.statusText.anchorMin,
                                    AnchorMax = config.statusText.anchorMax
                                }
                            },
                            FadeOut = config.statusText.fadeOut
                        });


                        //name text
                        if (config.names.ContainsKey(info.HitEntity.ShortPrefabName))
                        {
                            moj_ui.Add(new CuiElement
                            {
                                Parent = "hp_background",
                                Name = "hp_name",
                                Components =
                                {
                                    new CuiTextComponent
                                    {
                                        Text = config.nameText.text.Replace("{name}", $"{config.names[info.HitEntity.ShortPrefabName]}"),
                                        FontSize = 12,
                                        Font = config.nameText.font,
                                        Align = config.nameText.align,
                                        Color = config.nameText.color,
                                        FadeIn = config.nameText.fadeIn,
                                    },

                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = config.nameText.anchorMin,
                                        AnchorMax = config.nameText.anchorMax
                                    }
                                },
                                FadeOut = config.nameText.fadeOut
                            });
                        }

                        // check if image is downloaded
                        if (ImageLibrary != null && config.images.ContainsKey(info.HitEntity.ShortPrefabName) && (bool)ImageLibrary.Call("HasImage", config.images[info.HitEntity.ShortPrefabName]) && config.enableImages)
                        {
                            moj_ui.Add(new CuiPanel
                            {
                                Image = { Color = config.iconPanel.color, FadeIn = config.iconPanel.fadeIn },
                                RectTransform = { AnchorMin = config.iconPanel.anchorMin, AnchorMax = config.iconPanel.anchorMax },
                                CursorEnabled = false,
                                FadeOut = config.iconPanel.fadeOut
                            },
                                "hp_background",
                                "hp_imagepanel"
                            );

                            moj_ui.Add(new CuiElement
                            {
                                Parent = "hp_imagepanel",
                                Components =
                            {
                                new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", config.images[info.HitEntity.ShortPrefabName]), Sprite = "assets/content/textures/generic/fulltransparent.tga", FadeIn = config.iconPanel.fadeIn},
                                new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                            }
                            });
                        }


                        // odpocet
                        moj_ui.Add(new CuiElement
                        {
                            Parent = "Overlay",
                            Name = "notifications_countdown",
                            Components = {
                            new CuiTextComponent{ Text = "", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1", Font = "robotocondensed-regular.ttf",},
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 0" },
                            //new CuiCountdownComponent { EndTime = 0, StartTime = config.timeToDestroy == 0 ? 4 : config.timeToDestroy , Command = "close_myui"}
                        },
                            FadeOut = 0f
                        });

                        CuiHelper.DestroyUi(attacker, "notifications_countdown");
                        CuiHelper.DestroyUi(attacker, "hp_background");
                        CuiHelper.AddUi(attacker, moj_ui);
                    }
                    catch
                    {
                        //
                    }

                });
                //CuiHelper.DestroyUi(info.InitiatorPlayer, "hp_background");
            }

        }

        void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {   
            lastHits.Remove(attacker);
            lastHits.Add(attacker, DateTimeOffset.UtcNow.ToUnixTimeSeconds());


            NextTick(() => {
                try
                {   
                    if (config.requirePerm)
                    {
                        if (!permission.UserHasPermission(attacker.UserIDString, "npchealthbar.use"))
                        {
                            return;
                        }
                    }
                    if (info == null || info.HitEntity == null || info.HitEntity.IsDestroyed || !info.HitEntity.IsNpc)
                    {
                        return;
                    }

                    if (config.blacklist.Contains(info.HitEntity.ShortPrefabName))
                    {
                        //CuiHelper.DestroyUi(attacker, "hp_background");
                        return;
                    }

                    if (info.HitEntity.ShortPrefabName.Contains("corpse") || info.HitEntity.Health() < 1)
                    {
                        CuiHelper.DestroyUi(attacker, "hp_background");
                        return;
                    }


                    float onePercent = info.HitEntity.MaxHealth() / 100;
                    float leftPercent = info.HitEntity.Health() / onePercent;

                    var moj_ui = new CuiElementContainer();

                    // background panel
                    moj_ui.Add(new CuiPanel
                    {
                        Image = { Color = config.panelbg.color, FadeIn = config.panelbg.fadeIn },
                        RectTransform = { AnchorMin = config.panelbg.anchorMin, AnchorMax = config.panelbg.anchorMax },
                        CursorEnabled = false,
                        FadeOut = config.panelbg.fadeOut
                    },
                        "Overlay",
                        "hp_background"
                    );

                    //overline
                    moj_ui.Add(new CuiPanel
                    {
                        Image = { Color = config.overline.color, FadeIn = config.overline.fadeIn },
                        RectTransform = { AnchorMin = config.overline.anchorMin, AnchorMax = config.overline.anchorMax },
                        CursorEnabled = false,
                        FadeOut = config.overline.fadeOut
                    },
                        "hp_background",
                        "bar_overline"
                    );

                    // progress panel     
                    moj_ui.Add(new CuiPanel
                    {
                        Image = { Color = config.panelHp.color, FadeIn = config.panelHp.fadeIn },
                        RectTransform = { AnchorMin = config.panelHp.anchorMin, AnchorMax = $"{leftPercent / 100} {config.panelHp.anchorMax}" },
                        CursorEnabled = false,
                        FadeOut = config.panelHp.fadeOut
                    },
                        "bar_overline",
                        "hp_progress"
                    );

                    // status text
                    moj_ui.Add(new CuiElement
                    {
                        Parent = "hp_background",
                        Name = "hp_statustext",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = config.statusText.text.Replace("{currenthp}", $"{(int) info.HitEntity.Health()}").Replace("{maxhp}", $"{info.HitEntity.MaxHealth()}"),
                                FontSize = config.statusText.fontsize,
                                Font = config.statusText.font,
                                Align = config.statusText.align,
                                Color = config.statusText.color,
                                FadeIn = config.statusText.fadeIn
                            },

                            new CuiRectTransformComponent
                            {
                                AnchorMin = config.statusText.anchorMin,
                                AnchorMax = config.statusText.anchorMax
                            }
                        },
                        FadeOut = config.statusText.fadeOut
                    });


                    //name text
                    if (config.names.ContainsKey(info.HitEntity.ShortPrefabName))
                    {
                        moj_ui.Add(new CuiElement
                        {
                            Parent = "hp_background",
                            Name = "hp_name",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = config.nameText.text.Replace("{name}", $"{config.names[info.HitEntity.ShortPrefabName]}"),
                                    FontSize = 12,
                                    Font = config.nameText.font,
                                    Align = config.nameText.align,
                                    Color = config.nameText.color,
                                    FadeIn = config.nameText.fadeIn,
                                },

                                new CuiRectTransformComponent
                                {
                                    AnchorMin = config.nameText.anchorMin,
                                    AnchorMax = config.nameText.anchorMax
                                }
                            },
                            FadeOut = config.nameText.fadeOut
                        });
                    }

                    // check if image is downloaded
                    if (ImageLibrary != null && config.images.ContainsKey(info.HitEntity.ShortPrefabName) && (bool)ImageLibrary.Call("HasImage", config.images[info.HitEntity.ShortPrefabName]) && config.enableImages)
                    {
                        moj_ui.Add(new CuiPanel
                        {
                            Image = { Color = config.iconPanel.color, FadeIn = config.iconPanel.fadeIn },
                            RectTransform = { AnchorMin = config.iconPanel.anchorMin, AnchorMax = config.iconPanel.anchorMax },
                            CursorEnabled = false,
                            FadeOut = config.iconPanel.fadeOut
                        },
                            "hp_background",
                            "hp_imagepanel"
                        );

                        moj_ui.Add(new CuiElement
                        {
                            Parent = "hp_imagepanel",
                            Components =
                        {
                            new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", config.images[info.HitEntity.ShortPrefabName]), Sprite = "assets/content/textures/generic/fulltransparent.tga", FadeIn = config.iconPanel.fadeIn},
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                        }
                        });
                    }

                    // odpocet
                    moj_ui.Add(new CuiElement
                    {
                        Parent = "Overlay",
                        Name = "notifications_countdown",
                        Components = {
                        new CuiTextComponent{ Text = "", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1", Font = "robotocondensed-regular.ttf",},
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 0" },
                        //new CuiCountdownComponent { EndTime = 0, StartTime = config.timeToDestroy == 0 ? 4 : config.timeToDestroy, Command = "close_myui"}
                    },
                        FadeOut = 0f
                    });

                    CuiHelper.DestroyUi(attacker, "notifications_countdown");
                    CuiHelper.DestroyUi(attacker, "hp_background");
                    CuiHelper.AddUi(attacker, moj_ui);

                }
                catch
                {
                    ///
                }

            });

            /*    
            timer.Once(5f, () =>
            {   
                CuiHelper.DestroyUi(attacker, "hp_background");
                Puts("Destroyed");
            });
            */

        }


        
        
        

        #endregion

        #region Commands

        [ConsoleCommand("close_myui")]
        private void close_myui(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            CuiHelper.DestroyUi(player, "hp_background");
        }

        #endregion

        #region Functions



        #endregion

        #region Config

        private Configuration config;
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<Configuration>();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = Configuration.CreateConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        class Configuration
        {
            [JsonProperty("Time to destroy Health Bar after last attack")]
            public int timeToDestroy { get; set; }

            [JsonProperty("Require permission?")]
            public bool requirePerm { get; set; }

            [JsonProperty("Blacklist")]
            public List<string> blacklist { get; set; }

            [JsonProperty("NPC Names")]
            public Dictionary<string, string> names { get; set; }

            [JsonProperty("Show Images")]
            public bool enableImages { get; set; }

            [JsonProperty("Images")]
            public Dictionary<string, string> images { get; set; }

            [JsonProperty(PropertyName = "Panel Background")]
            public PanelBg panelbg { get; set; }

            public class PanelBg
            {
                [JsonProperty(PropertyName = "Anchor Min")]
                public string anchorMin { get; set; }

                [JsonProperty(PropertyName = "Anchor Max")]
                public string anchorMax { get; set; }

                [JsonProperty(PropertyName = "Color")]
                public string color { get; set; }

                [JsonProperty(PropertyName = "FadeIn")]
                public float fadeIn { get; set; }

                [JsonProperty(PropertyName = "FadeOut")]
                public float fadeOut { get; set; }
            }



            [JsonProperty(PropertyName = "Panel Overline")]
            public Overline overline { get; set; }

            public class Overline
            {
                [JsonProperty(PropertyName = "Anchor Min")]
                public string anchorMin { get; set; }

                [JsonProperty(PropertyName = "Anchor Max")]
                public string anchorMax { get; set; }

                [JsonProperty(PropertyName = "Color")]
                public string color { get; set; }

                [JsonProperty(PropertyName = "FadeIn")]
                public float fadeIn { get; set; }

                [JsonProperty(PropertyName = "FadeOut")]
                public float fadeOut { get; set; }
            }



            [JsonProperty(PropertyName = "HP Panel")]
            public PanelHp panelHp { get; set; }

            public class PanelHp
            {
                [JsonProperty(PropertyName = "Anchor Min")]
                public string anchorMin { get; set; }

                [JsonProperty(PropertyName = "Anchor Max")]
                public string anchorMax { get; set; }

                [JsonProperty(PropertyName = "Color")]
                public string color { get; set; }

                [JsonProperty(PropertyName = "FadeIn")]
                public float fadeIn { get; set; }

                [JsonProperty(PropertyName = "FadeOut")]
                public float fadeOut { get; set; }
            }

            [JsonProperty(PropertyName = "Status Text")]
            public StatusText statusText { get; set; }

            public class StatusText
            {
                [JsonProperty("Text")]
                public string text { get; set; }

                [JsonProperty(PropertyName = "Font size")]
                public int fontsize { get; set; }

                [JsonProperty(PropertyName = "Anchor Min")]
                public string anchorMin { get; set; }

                [JsonProperty(PropertyName = "Anchor Max")]
                public string anchorMax { get; set; }

                [JsonProperty(PropertyName = "Color")]
                public string color { get; set; }

                [JsonProperty(PropertyName = "FadeIn")]
                public float fadeIn { get; set; }

                [JsonProperty(PropertyName = "FadeOut")]
                public float fadeOut { get; set; }

                [JsonProperty("Text Alignment")]
                public TextAnchor align { get; set; }

                [JsonProperty("Text Font")]
                public string font { get; set; }

            }

            [JsonProperty(PropertyName = "Npc Name Text")]
            public NameText nameText { get; set; }

            public class NameText
            {
                [JsonProperty("Text")]
                public string text { get; set; }

                [JsonProperty(PropertyName = "Anchor Min")]
                public string anchorMin { get; set; }

                [JsonProperty(PropertyName = "Anchor Max")]
                public string anchorMax { get; set; }

                [JsonProperty(PropertyName = "Color")]
                public string color { get; set; }

                [JsonProperty(PropertyName = "FadeIn")]
                public float fadeIn { get; set; }

                [JsonProperty(PropertyName = "FadeOut")]
                public float fadeOut { get; set; }

                [JsonProperty("Text Alignment")]
                public TextAnchor align { get; set; }

                [JsonProperty("Text Font")]
                public string font { get; set; }

            }

            [JsonProperty(PropertyName = "Icon")]
            public IconPanel iconPanel { get; set; }

            public class IconPanel
            {
                [JsonProperty(PropertyName = "Anchor Min")]
                public string anchorMin { get; set; }

                [JsonProperty(PropertyName = "Anchor Max")]
                public string anchorMax { get; set; }

                [JsonProperty(PropertyName = "Background Color")]
                public string color { get; set; }

                [JsonProperty(PropertyName = "FadeIn")]
                public float fadeIn { get; set; }

                [JsonProperty(PropertyName = "FadeOut")]
                public float fadeOut { get; set; }
            }

            public static Configuration CreateConfig()
            {
                return new Configuration
                {   
                    timeToDestroy = 4,
                    requirePerm = false,
                    blacklist = new List<string>
                    {
                        "empty"
                    },
                    names = new Dictionary<string, string>{
                       { "scientistnpc_roam", "Roamer" },
                       { "scientistnpc_peacekeeper", "Peacekeeper"},
                       { "scientistnpc_oilrig", "Scientist" },
                       { "scientistnpc_patrol", "Patrol" },
                       { "scientistnpc_heavy", "Buldozer" },
                       { "scientistnpc_roam_nvg_variant", "Roamer" },
                       { "scientistnpc_roamtethered", "Roamer" },
                       { "scientistnpc_junkpile_pistol", "Pistolier" },
                       { "scientistnpc_full_shotgun", "Shotgunner" },
                       { "scientistnpc_full_pistol", "Pistolier" },
                       { "scientistnpc_full_mp5", "Scientist" },
                       { "scientistnpc_full_lr300", "Scientist" },
                       { "scientistnpc_full_any", "Scientist" },
                       { "scientistnpc_excavator", "Excavator" },
                       { "scientistnpc_ch47_gunner", "Gunner" },
                       { "scientistnpc_cargo_turret_lr300", "Gunner" },
                       { "scientistnpc_cargo_turret_any", "Gunner" },
                       { "scientistnpc_cargo", "Scientist" },
                       { "scientistnpc_arena", "Scientist" },


                       { "boar", "Hog"},
                       { "scarecrow", "Scarecrow"},
                       { "polarbear", "Polarbear"},
                       { "wolf", "Direwolf"},
                       { "bear", "Grizzly"}
                    },
                    enableImages = true,
                    images = new Dictionary<string, string>{
                       { "scientistnpc_roam", "https://i.postimg.cc/t4wDtsGT/4tyicon.png" },
                       { "scientistnpc_peacekeeper", "https://i.postimg.cc/kXwZZnkG/Humanoid-L.png" },
                       { "scientistnpc_oilrig", "https://i.postimg.cc/kXwZZnkG/Humanoid-L.png" },
                       { "scientistnpc_patrol", "https://i.postimg.cc/kXwZZnkG/Humanoid-L.png" },
                       { "scientistnpc_heavy", "https://i.postimg.cc/QxNYc2H3/Humanoidelite-L.png" },
                       { "scientistnpc_roam_nvg_variant", "https://i.postimg.cc/kXwZZnkG/Humanoid-L.png" },
                       { "scientistnpc_roamtethered", "https://i.postimg.cc/kXwZZnkG/Humanoid-L.png" },
                       { "scientistnpc_junkpile_pistol", "https://i.postimg.cc/kXwZZnkG/Humanoid-L.png" },
                       { "scientistnpc_full_shotgun", "https://i.postimg.cc/QxNYc2H3/Humanoidelite-L.png" },
                       { "scientistnpc_full_pistol", "https://i.postimg.cc/kXwZZnkG/Humanoid-L.png" },
                       { "scientistnpc_full_mp5", "https://i.postimg.cc/QxNYc2H3/Humanoidelite-L.png" },
                       { "scientistnpc_full_lr300", "https://i.postimg.cc/QxNYc2H3/Humanoidelite-L.pngt" },
                       { "scientistnpc_full_any", "https://i.postimg.cc/kXwZZnkG/Humanoid-L.png" },
                       { "scientistnpc_excavator", "https://i.postimg.cc/kXwZZnkG/Humanoid-L.png" },
                       { "scientistnpc_ch47_gunner", "https://i.postimg.cc/QxNYc2H3/Humanoidelite-L.png" },
                       { "scientistnpc_cargo_turret_lr300", "https://i.postimg.cc/QxNYc2H3/Humanoidelite-L.png" },
                       { "scientistnpc_cargo_turret_any", "https://i.postimg.cc/kXwZZnkG/Humanoid-L.png" },
                       { "scientistnpc_cargo", "https://i.postimg.cc/kXwZZnkG/Humanoid-L.png" },
                       { "scientistnpc_arena", "https://i.postimg.cc/kXwZZnkG/Humanoid-L.png" },


                       { "bear", "https://i.postimg.cc/wjXtHHqX/BearL.png" },
                       { "boar", "https://i.postimg.cc/Cxws8cQV/BoarL.png" },
                       { "horse", "https://i.postimg.cc/d0FqBQd1/HorseL.png" },
                       { "wolf", "https://i.postimg.cc/VkfwrZcr/WolfL.png" },
                       { "zombie", "https://i.postimg.cc/tTSYTHhM/ZombieL.png" },
                       { "polarbear", "https://i.postimg.cc/Qdks4dmT/Polarbear-L.png" }
                    },


                    panelbg = new NPCHealthBar.Configuration.PanelBg
                    {
                        anchorMin = "0.4 0.8",
                        anchorMax = "0.6 0.84",
                        color = "0 0 0 0.9",
                        fadeIn = 0f,
                        fadeOut = 0f
                    },
                    panelHp = new NPCHealthBar.Configuration.PanelHp
                    {
                        anchorMin = "0 0",
                        anchorMax = "1",
                        color = "0.14 0.6 0 1",
                        fadeIn = 0f,
                        fadeOut = 0f
                    },


                    overline = new NPCHealthBar.Configuration.Overline
                    {
                        anchorMin = "0.15 0.12",
                        anchorMax = "0.975 0.85",
                        color = "0 0 0 0",
                        fadeIn = 0f,
                        fadeOut = 0f
                    },

                    statusText = new NPCHealthBar.Configuration.StatusText
                    {
                        text = "{currenthp} / {maxhp}",
                        fontsize = 12,
                        anchorMin = "0.02 0.1",
                        anchorMax = "0.98 0.75",
                        color = "1 1 1 1",
                        fadeIn = 0f,
                        fadeOut = 0f,
                        align = TextAnchor.UpperRight,
                        font = "robotocondensed-regular.ttf"
                    },
                    nameText = new NPCHealthBar.Configuration.NameText
                    {
                        text = "{name}",
                        anchorMin = "0.17 0.1",
                        anchorMax = "0.975 0.75",
                        color = "1 1 1 1",
                        fadeIn = 0f,
                        fadeOut = 0f,
                        align = TextAnchor.UpperLeft,
                        font = "robotocondensed-regular.ttf"
                    },
                    iconPanel = new NPCHealthBar.Configuration.IconPanel
                    {
                        anchorMin = "0.01 0.07",
                        anchorMax = "0.13 0.88",
                        color = "0 0 0 1",
                        fadeIn = 0f,
                        fadeOut = 0f
                    },

                    /*
                        img - true

                        anchorMin = "-0.12 0",
                        anchorMax = "-0.02 3",
                        color = "0 0 0 1",
                        fadeIn = 0f,
                        fadeOut = 0f
                    */
                };
            }
        }
        #endregion

    }
}