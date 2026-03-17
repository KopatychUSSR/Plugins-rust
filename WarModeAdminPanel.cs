// Requires: WarMode
using static Oxide.Plugins.WarMode;

using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using UnityEngine.UI;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using Oxide.Plugins.WarModeMethods;
using System;
using UnityEngine;
using Oxide.Core.Libraries.Covalence;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("War Mode Admin Panel", "mr01sam", "1.1.1")]
    [Description("Admin UI for configuring War Mode in game.")]
    public partial class WarModeAdminPanel : CovalencePlugin
    {
        [PluginReference]
        private readonly Plugin SimpleStatus, RaidableBases;

        // UI SETTINGS
        private const float w = 1080;
        private const float h = 640;
        private const float padding = 8;
        private const string mainBgColor = "0.08627 0.08627 0.08627 1";

        private const string headerBgColor = "0.15686 0.15686 0.12549 1";
        private const string headerBgColorFaded = "0.15686 0.15686 0.12549 0.4";
        private const string headerTextColor = "0.54509 0.51372 0.4705 1";
        private const float headerHeight = 32;

        private const string inputFieldBgColor = "0.15686 0.15686 0.12549 1";

        private const float bottomHeight = 48;

        private const string generalTextColor = "0.7 0.7 0.7 1";
        private const string generalTextColorFaded = "0.7 0.7 0.7 0.6";
        private const int generalTextSize = 14;

        private const float panelHeaderText = 14;
        private const float panelHeaderHeight = 18;

        private const string poscolor = "0.69804 0.83137 0.46667 1";
        private const string negcolor = "0.77255 0.23922 0.15686 1";
        private const string selcolor = "#f5f542";

        private const float ruleWidth = 212;
        private const float ruleHeaderHeight = 28;
        private float ruleRowHeight = 24;
        private const float ruleGap = 2;

        private const float navHeight = 24;

        private const float modeColGap = 2;

        private const float maxModeColWidth = 80;
        private const float minModeColNum = 6;
        private const float totalModeColWidth = maxModeColWidth * minModeColNum;

        private float modeColWidth = maxModeColWidth;

        private const float toolBtnHeight = 24;
        private const float toolBtnWidth = 64;
        private const float toolBtnGap = 2;
        private const float toolBtnSpriteSize = 12;
        private const int toolBtnTextSize = 8;
        private const float toolBtnPadding = 4;
        private const string toolBtnColor = headerBgColor;
        private const string toolBtnTextColor = headerTextColor;

        private bool descriptive = true;

        public static WarModeAdminPanel INSTANCE = null;

        public WarMode.Configuration ModifiedConfig;

        void OnServerInitialized()
        {
            INSTANCE = this;
            //foreach (var basePlayer in BasePlayer.activePlayerList)
            //{
            //    currentPageRoute = "modes";
            //    ShowUI(basePlayer);
            //}
        }

        void Unload()
        {
            Close();
        }

        private string mainPanel;
        private string pageId;
        private string navBar;

        private string currentModeName = null;
        private string currentPageRoute = "rules";
        private bool isDirty = false;
        private BasePlayer editingPlayer = null;

        [Command("warmode.ui.applychanges")]
        private void CmdWarModeUiApplyChanges(IPlayer player, string command, string[] args)
        {
            // warmode.ui.applychanges
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null || basePlayer != editingPlayer) { return; }

            ApplyChanges();
            ShowUI(basePlayer);
        }

        [Command("warmode.ui.resetchanges")]
        private void CmdWarModeUiResetChanges(IPlayer player, string command, string[] args)
        {
            // warmode.ui.resetchanges
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null || basePlayer != editingPlayer) { return; }

            ResetChanges();
            ShowUI(basePlayer);
        }

        [Command("warmode.ui.close")]
        private void CmdWarModeUiClose(IPlayer player, string command, string[] args)
        {
            // warmode.ui.close
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null || basePlayer.UserIDString != editingPlayer.UserIDString) { return; }

            Close();
        }

        private void Close()
        {
            CuiHelper.DestroyUi(editingPlayer, ID);
            if (isDirty)
            {
                editingPlayer.ChatMessage("Your WarMode config changes have NOT been applied. Reopen the WarMode admin panel and click APPLY CHANGES for them to take effect.");
            }
            editingPlayer = null;
            ElementsLoaded.Clear();
        }

        public void ApplyChanges()
        {
            WarMode.INSTANCE.SaveConfigExternal(ModifiedConfig);

            isDirty = false;
        }

        public void ResetChanges()
        {
            ModifiedConfig = JsonConvert.DeserializeObject<WarMode.Configuration>(JsonConvert.SerializeObject(CONFIG));

            isDirty = false;
        }

        public void CreateBottomButton()
        {
            var container = new CuiElementContainer();

            var offset = 70f;

            var bottomBar = ID + "bottom";
            var bottomBtn = Create(container, bottomBar, new CuiElement
            {
                Name = ID + "bottomBtn",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = $"{-60-offset} {-16}",
                        OffsetMax = $"{60-offset} {16}"
                    },
                    new CuiButtonComponent
                    {
                        Color = isDirty ? $"0.385 0.478 0.228 1" : "0.3 0.3 0.3 0.5",
                        Command = isDirty ? "warmode.ui.applychanges" : ""
                    }
                }
            });

            var bottomBtnText = Create(container, bottomBtn, new CuiElement
            {
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "APPLY CHANGES",
                        Align = UnityEngine.TextAnchor.MiddleCenter,
                        Color = isDirty ? $"0.76078 0.94510 0.41176 1" : "0.5 0.5 0.5 0.5"
                    }
                }
            });

            var resetBtn = Create(container, bottomBar, new CuiElement
            {
                Name = ID + "resetBtn",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = $"{-60+offset} {-16}",
                        OffsetMax = $"{60+offset} {16}"
                    },
                    new CuiButtonComponent
                    {
                        Color = isDirty ? $"0.3 0.3 0.3 1" : "0.3 0.3 0.3 0.5",
                        Command = isDirty ? "warmode.ui.resetchanges" : ""
                    }
                }
            });

            var resetBtnText = Create(container, resetBtn, new CuiElement
            {
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "UNDO CHANGES",
                        Align = UnityEngine.TextAnchor.MiddleCenter,
                        Color = isDirty ? $"0.9 0.9 0.9 1" : "0.5 0.5 0.5 0.5"
                    }
                }
            });
            CuiHelper.DestroyUi(editingPlayer, bottomBtn);
            CuiHelper.DestroyUi(editingPlayer, resetBtn);
            CuiHelper.AddUi(editingPlayer, container);
        }


        public void ShowUI(BasePlayer basePlayer)
        {
            editingPlayer = basePlayer;
            expanededRules = new List<string>();
            if (ModifiedConfig == null)
            {
                ResetChanges();
            }
            currentModeName = ModifiedConfig.Modes.FirstOrDefault()?.Name;

            var container = new CuiElementContainer();

            mainPanel = Create(container, new CuiElement
            {
                Name = ID,
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
                        Color = mainBgColor
                    },
                    new CuiNeedsCursorComponent
                    {

                    }
                }
            });

            var titleBar = Create(container, mainPanel, new CuiElement
            {
                Name = ID + "title",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"{0} {-headerHeight}",
                        OffsetMax = $"{0} {0}"
                    },
                    new CuiImageComponent
                    {
                        Color = headerBgColor
                    }
                }
            });

            var titleBarText = Create(container, titleBar, new CuiElement
            {
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "WAR MODE CONFIGURATION",
                        Color = headerTextColor,
                        Align = UnityEngine.TextAnchor.MiddleCenter
                    }
                }
            });

            var titleBarBtn = Create(container, titleBar, new CuiElement
            {
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "1 0.5",
                        AnchorMax = "1 0.5",
                        OffsetMin = $"{-16-8} {-8}",
                        OffsetMax = $"{-8} {8}"
                    },
                    new CuiButtonComponent
                    {
                        Color = headerTextColor,
                        Sprite = "assets/icons/close.png",
                        Command = "warmode.ui.close"
                    }
                }
            });

            var bottomBar = Create(container, mainPanel, new CuiElement
            {
                Name = ID + "bottom",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 0",
                        OffsetMin = $"{0} {0}",
                        OffsetMax = $"{0} {bottomHeight}"
                    },
                    new CuiImageComponent
                    {
                        Color = headerBgColor
                    }
                }
            });

            CuiHelper.DestroyUi(editingPlayer, ID);
            CuiHelper.AddUi(editingPlayer, container);

            ShowNavBar();

            CreateBottomButton();

            ReloadPageRoute();
        }

        [Command("warmode.ui.setnavpage")]
        private void CmdWarModeUiSetNavPage(IPlayer player, string command, string[] args)
        {
            // warmode.ui.setnavpage <route>
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null || basePlayer != editingPlayer) { return; }
            var route = args[0];
            currentPageRoute = route;
            ReloadPageRoute();
        }

        private void ReloadPageRoute()
        {
            switch (currentPageRoute)
            {
                case "rules":
                    ReloadRulesPage();
                    break;
                case "modes":
                    ReloadModesPage();
                    break;
                case "settings":
                    ReloadSettingsPage();
                    break;
            }
            ShowNavBar();
        }

        private void ShowNavBar()
        {
            var container = new CuiElementContainer();

            var navBar = Create(container, mainPanel, new CuiElement
            {
                Name = ID + "navbar",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"{0} {-headerHeight-navHeight}",
                        OffsetMax = $"{0} {-headerHeight}"
                    },
                    //new CuiImageComponent
                    //{
                    //    Color = DARKEN
                    //}
                }
            });

            var navPages = new[]
            {
                new
                {
                    Title = "Rules",
                    Route = "rules"
                },
                new
                {
                    Title = "Modes",
                    Route = "modes"
                },
                new
                {
                    Title = "Settings",
                    Route = "settings"
                }
            };

            var w = 100;
            var p = 8;
            var left = p;
            foreach(var page in navPages)
            {
                var navBtn = Create(container, navBar, new CuiElement
                {
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 1",
                            OffsetMin = $"{left} {4}",
                            OffsetMax = $"{left+w} {-4}"
                        },
                        new CuiButtonComponent
                        {
                            Color = page.Route == currentPageRoute ? headerBgColor : headerBgColorFaded,
                            Command = $"warmode.ui.setnavpage {page.Route}"
                        }
                    }
                });
                var navTxt = Create(container, navBtn, new CuiElement
                {
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = page.Title,
                            Align = TextAnchor.MiddleCenter,
                            Color = headerTextColor,
                            FontSize = 10
                        }
                    }
                });
                left += p + w;
            }

            CuiHelper.DestroyUi(editingPlayer, navBar);
            CuiHelper.AddUi(editingPlayer, container);
        }



        #region Helper Methods

        public string Create(CuiElementContainer container, CuiElement newElement) => Create(container, "Overlay", newElement);

        public Dictionary<string, CuiElement> ElementsLoaded = new Dictionary<string, CuiElement>();

        public string Create(CuiElementContainer container, string parent, CuiElement newElement)
        {
            newElement.Parent = parent;
            if (string.IsNullOrWhiteSpace(newElement.Name))
            {
                newElement.Name = Guid.NewGuid().ToString();
            }
            ElementsLoaded[newElement.Name] = newElement;
            container.Add(newElement);
            return newElement.Name;
        }

        public void Update(CuiElement newElement)
        {
            var container = new CuiElementContainer();
            container.Add(newElement);
            ElementsLoaded[newElement.Name] = newElement;
            CuiHelper.AddUi(editingPlayer, container);
        }

        public void UpdateButtonText(string id, string text, string textColor)
        {
            var element = ElementsLoaded.GetValueOrDefault(id);
            if (element == null) { return; }
            foreach (var comp in element.Components)
            {
                if (comp is CuiTextComponent textComp)
                {
                    textComp.Text = text;
                    textComp.Color = textColor;
                    element.Update = true;
                }
            }
            Update(element);
        }

        public void UpdateInputText(string id, string text, string textColor)
        {
            var element = ElementsLoaded.GetValueOrDefault(id);
            if (element == null) { return; }
            foreach (var comp in element.Components)
            {
                if (comp is CuiInputFieldComponent textComp)
                {
                    textComp.Text = text;
                    textComp.Color = textColor;
                    element.Update = true;
                }
            }
            Update(element);
        }


        public string Color(string text, string hexColor)
        {
            return $"<color={hexColor}>{text}</color>";
        }

        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
            }, this);
        }

        private string Lang(string key, string id, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        #endregion

        #region Config
        private Configuration config;
        private class Configuration
        {
            public string CustomUI = "";
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

namespace Oxide.Plugins
{
    public partial class WarModeAdminPanel
    {
        public void ReloadModesPage()
        {
            var container = new CuiElementContainer();

            pageId = Create(container, mainPanel, new CuiElement
            {
                Name = ID + "page",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{0} {0}",
                        OffsetMax = $"{0} {-headerHeight-navHeight-padding}"
                    }
                }
            });
            CuiHelper.DestroyUi(editingPlayer, pageId);
            CuiHelper.AddUi(editingPlayer, container);

            ShowModesGrid();
        }

        public struct ColumnDef
        {
            public string Label;
            public string SubLabel;
            public string Property;
            public string Type;
            public float Width;
            public Func<WarMode.ModeConfig, string, bool> Validate;
            public Func<WarMode.ModeConfig, string, string> Clean;
            public Func<WarMode.ModeConfig, string> Value;
            public Action<WarMode.ModeConfig, object> OnSet;
            public Func<WarMode.ModeConfig, bool> Hidden;
            public Func<WarMode.ModeConfig, bool> ReadOnly;
            public Func<bool> HideCol;
            public string HelpText;
        }

        public static readonly string WhiteHex = "#ffffff";

        private const float ShortColWidth = 60;
        private const float DefaultColWidth = 90;
        private const float LongColWidth = 120;

        private const int LabelFontSize = 12;

        ColumnDef[] ModeColumns = new ColumnDef[]
        {
            new ColumnDef
            {
                Label = "",
                Property = null,
                Width = 16
            },
            new ColumnDef
            {
                Label = "Mode ID",
                Property = "mode",
                Width = DefaultColWidth,
                Value = (m) => m.Name,
                Clean = (m, v) =>
                {
                    v.Trim().ToLower().Replace(" ", "_");
                    var invalid = "INVALID";
                    if (string.IsNullOrWhiteSpace(v))
                    {
                        v = invalid;
                    }
                    else if (INSTANCE.ModifiedConfig.Modes.Any(x => x.Name.ToLower().Trim() == v))
                    {
                        v = invalid;
                    }
                    return v;
                },
                OnSet = (m, v) => m.Name = v.ToString(),
                ReadOnly = (m) => (INSTANCE.ModifiedConfig.Settings.NpcMode == m.Name) || (WarMode.CONFIG.Modes.Any(x => x.Name == m.Name)),
                HelpText = "The unique ID of the mode. This is NOT the display text. This should not be changed after it is initially set."
            },
            new ColumnDef
            {
                Label = "Priority",
                Property = "priority",
                Width = ShortColWidth,
                Value = (m) => m.Priority.ToString(),
                Clean = (m, v) =>
                {
                    v = v.Trim();
                    if (!int.TryParse(v, out int intval))
                    {
                        intval = 0;
                    }
                    if (intval <= 0)
                    {
                        intval = 1;
                    }
                    if (INSTANCE.ModifiedConfig.Modes.Any(x => x.Priority == intval && x.Name != m.Name))
                    {
                        intval = (INSTANCE.ModifiedConfig.Modes.Where(x => x.Name != m.Name).Max(x => x.Priority) ?? 0)+1;
                    }
                    return intval.ToString();
                },
                OnSet = (m, v) => m.Priority = int.Parse(v.ToString()),
                Hidden = (m) => INSTANCE.ModifiedConfig.Settings.NpcMode == m.Name,
                HelpText = "A number that is used to determine how important this mode is when compared to other modes. For example this is used when determining what the mode of a base should be when all the owners have different modes. Two modes should NOT have the same priority."
            },
            new ColumnDef
            {
                Label = "Display Name",
                Property = "displayname",
                Width = DefaultColWidth,
                Value = (m) => m.DisplayName,
                OnSet = (m, v) => m.DisplayName = v.ToString(),
                HelpText = "The name of the mode that is displayed to players. This is not a localized value."
            },
            new ColumnDef
            {
                Label = "Chat Color",
                Property = "color",
                Type = "color",
                Width = DefaultColWidth,
                Value = (m) => m.ColorHex,
                Clean = (m, v) =>
                {
                    v = v.Trim();
                    if (!ColorUtility.TryParseHtmlString(v, out UnityEngine.Color color))
                    {
                        color = UnityEngine.Color.white;
                    }
                    return "#" + ColorUtility.ToHtmlStringRGBA(color);
                },
                OnSet = (m, v) => m.ColorHex = v.ToString(),
                HelpText = "The color associated with this mode. When the mode is referenced in chat messages it will appear as this color. This does NOT affect the color the mode appears in SimpleStatus or the SpawnUI. You must edit the color in those respective configs."
            },
            new ColumnDef
            {
                Label = "Permission Group",
                Property = "group",
                Width = LongColWidth,
                Clean = (m, v) =>
                {
                    var invalid = "INVALID";
                    if (v == null) {return invalid; }
                    v.Trim().ToLower().Replace(" ", "");
                    if (string.IsNullOrWhiteSpace(v))
                    {
                        v = invalid;
                    }
                    else if (INSTANCE.ModifiedConfig.Modes.Any(x => x.Group?.ToLower().Trim() == v && x.Name != m.Name))
                    {
                        v = invalid;
                    }
                    return v;
                },
                Value = (m) => m.Group,
                OnSet = (m, v) => m.Group = v.ToString(),
                Hidden = (m) => INSTANCE.ModifiedConfig.Settings.NpcMode == m.Name,
                HelpText = "The oxide/carbon permission group assigned to this mode. All modes except the NPC mode MUST have a permission group assigned to them. All modes must have a unique permission group."
            },
            //new ColumnDef
            //{
            //    Label = "Bar Color",
            //    SubLabel = "SimpleStatus",
            //    Property = "ssbarcolor",
            //    Width = DefaultColWidth,
            //    Type = "color",
            //    HideCol = () => INSTANCE.SimpleStatus?.IsLoaded != true,
            //    Value = (m) =>
            //    {
            //        try
            //        {
            //            return INSTANCE.ModifiedConfig.SimpleStatus.ModeStatusBars.FirstOrDefault(x => x.Key == m.Name).Value.BackgroundColor?.ColorRgbToHex();
            //        } catch(Exception) { return WhiteHex; }
            //    },
            //    OnSet = (m, v) =>
            //    {
            //        try
            //        {
            //            if (!INSTANCE.ModifiedConfig.SimpleStatus.ModeStatusBars.ContainsKey(m.Name))
            //            {
            //                INSTANCE.ModifiedConfig.SimpleStatus.ModeStatusBars[m.Name] = new WarMode.StatusDetailsConfig();
            //            }
            //            INSTANCE.ModifiedConfig.SimpleStatus.ModeStatusBars.FirstOrDefault(x => x.Key == m.Name).Value.BackgroundColor = v.ToString().ColorHexToRgb();
            //        } catch(Exception e) { throw e; }
            //    }
            //},
            //new ColumnDef
            //{
            //    Label = "Title Color",
            //    SubLabel = "SimpleStatus",
            //    Property = "sstitlecolor",
            //    Width = DefaultColWidth,
            //    Type = "color",
            //    HideCol = () => INSTANCE.SimpleStatus?.IsLoaded != true,
            //    Value = (m) =>
            //    {
            //        try
            //        {
            //            return INSTANCE.ModifiedConfig.SimpleStatus.ModeStatusBars.FirstOrDefault(x => x.Key == m.Name).Value.TitleColor?.ColorRgbToHex();
            //        } catch(Exception) { return WhiteHex; }
            //    },
            //    OnSet = (m, v) =>
            //    {
            //        try
            //        {
            //            if (!INSTANCE.ModifiedConfig.SimpleStatus.ModeStatusBars.ContainsKey(m.Name))
            //            {
            //                INSTANCE.ModifiedConfig.SimpleStatus.ModeStatusBars[m.Name] = new WarMode.StatusDetailsConfig();
            //            }
            //            INSTANCE.ModifiedConfig.SimpleStatus.ModeStatusBars.FirstOrDefault(x => x.Key == m.Name).Value.TitleColor = v.ToString().ColorHexToRgb();
            //        } catch(Exception e) { throw e; }
            //    }
            //},
            //new ColumnDef
            //{
            //    Label = "Text Color",
            //    SubLabel = "SimpleStatus",
            //    Property = "sstextcolor",
            //    Width = DefaultColWidth,
            //    Type = "color",
            //    HideCol = () => INSTANCE.SimpleStatus?.IsLoaded != true,
            //    Value = (m) =>
            //    {
            //        try
            //        {
            //            return INSTANCE.ModifiedConfig.SimpleStatus.ModeStatusBars.FirstOrDefault(x => x.Key == m.Name).Value.TextColor?.ColorRgbToHex();
            //        } catch(Exception) { return WhiteHex; }
            //    },
            //    OnSet = (m, v) =>
            //    {
            //        try
            //        {
            //            if (!INSTANCE.ModifiedConfig.SimpleStatus.ModeStatusBars.ContainsKey(m.Name))
            //            {
            //                INSTANCE.ModifiedConfig.SimpleStatus.ModeStatusBars[m.Name] = new WarMode.StatusDetailsConfig();
            //            }
            //            INSTANCE.ModifiedConfig.SimpleStatus.ModeStatusBars.FirstOrDefault(x => x.Key == m.Name).Value.TextColor = v.ToString().ColorHexToRgb();
            //        } catch(Exception e) { throw e; }
            //    }
            //},
            //new ColumnDef
            //{
            //    Label = "Image Color",
            //    SubLabel = "SimpleStatus",
            //    Property = "ssimagecolor",
            //    Width = DefaultColWidth,
            //    Type = "color",
            //    HideCol = () => INSTANCE.SimpleStatus?.IsLoaded != true,
            //    Value = (m) =>
            //    {
            //        try
            //        {
            //            return INSTANCE.ModifiedConfig.SimpleStatus.ModeStatusBars.FirstOrDefault(x => x.Key == m.Name).Value.ImageColor?.ColorRgbToHex();
            //        } catch(Exception) { return WhiteHex; }
            //    },
            //    OnSet = (m, v) =>
            //    {
            //        try
            //        {
            //            if (!INSTANCE.ModifiedConfig.SimpleStatus.ModeStatusBars.ContainsKey(m.Name))
            //            {
            //                INSTANCE.ModifiedConfig.SimpleStatus.ModeStatusBars[m.Name] = new WarMode.StatusDetailsConfig();
            //            }
            //            INSTANCE.ModifiedConfig.SimpleStatus.ModeStatusBars.FirstOrDefault(x => x.Key == m.Name).Value.ImageColor = v.ToString().ColorHexToRgb();
            //        } catch(Exception e) { throw e; }
            //    }
            //}
        };

        private const float modeGridColHeight = 24;
        private const float modeGridRowHeight = 24;
        private const float modeGridColGap = 8;
        private const float modeGridRowGap = 8;

        [Command("warmode.ui.setmodeproperty")]
        private void CmdWarModeUiModeProperty(IPlayer player, string command, string[] args)
        {
            // warmode.ui.setmodeproperty <modeid> <property> <value>
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null || basePlayer != editingPlayer) { return; }
            var modeid = args[0];
            var property = args[1];
            var value = string.Join(" ", args.Skip(2));
            var mode = ModifiedConfig.Modes.First(x => x.Name == modeid);
            var col = ModeColumns.First(x => x.Property == property);
            if (value == null || col.Value(mode) == value.ToString())
            {
                return;
            }
            value = col.Clean?.Invoke(mode, value) ?? value;
            if (!(col.Validate?.Invoke(mode, value) ?? true))
            {
                // TODO invalid
                return;
            }
            col.OnSet.Invoke(mode, value);
            if (!isDirty)
            {
                isDirty = true;
                CreateBottomButton();
            }
            ShowModesGrid();
        }

        [Command("warmode.ui.addmode")]
        private void CmdWarModeUiAddMode(IPlayer player, string command, string[] args)
        {
            // warmode.ui.addmode
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null || basePlayer != editingPlayer) { return; }
            var modes = ModifiedConfig.Modes.ToList();

            var newmode = WarMode.ModeConfig.New();
            newmode.Name = $"mode" + modes.Count;
            newmode.DisplayName = "Mode " + modes.Count;
            newmode.Priority = ModifiedConfig.Modes.Max(x => x.Priority) + 1;
            newmode.Group = newmode.Name;

            modes.Add(newmode);

            ModifiedConfig.Modes = modes.ToArray();
            if (!isDirty)
            {
                isDirty = true;
                CreateBottomButton();
            }
            ShowModesGrid(); // TODO
        }

        [Command("warmode.ui.deletemode")]
        private void CmdWarModeUiDeleteMode(IPlayer player, string command, string[] args)
        {
            // warmode.ui.deletemode <name>
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null || basePlayer != editingPlayer) { return; }

            var modename = args[0];

            var newList = ModifiedConfig.Modes.ToList();
            newList.RemoveAll(x => x.Name == modename);
            ModifiedConfig.Modes = newList.ToArray();

            if (!isDirty)
            {
                isDirty = true;
                CreateBottomButton();
            }

            selectedMode = ModifiedConfig.Modes.FirstOrDefault()?.Name;
            ReloadModesPage();
        }

        [Command("warmode.ui.helptext")]
        private void CmdWarModeUiHelpText(IPlayer player, string command, string[] args)
        {
            // warmode.ui.helptext <titlecount> <title> <msg>
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null || basePlayer != editingPlayer) { return; }

            var skip = int.Parse(args[0]);
            var titletxt = string.Join(" ", args.Skip(1).Take(skip));
            var msg = string.Join(" ", args.Skip(skip+1));

            var container = new CuiElementContainer();
            var shadow = Create(container, ID, new CuiElement
            {
                Name = ID + "helptext",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                    },
                    new CuiButtonComponent
                    {
                        Color = "0 0 0 0.9",
                        Close = ID + "helptext"
                    }
                }
            });
            var window = Create(container, shadow, new CuiElement
            {
                Name = ID + "helptextwindow",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.3 0.4",
                        AnchorMax = "0.7 0.7",
                    },
                    new CuiButtonComponent
                    {
                        Color = mainBgColor,
                        Close = ID + "helptext"
                    }
                }
            });
            var padding = 8;
            var title = Create(container, window, new CuiElement
            {
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"{padding} {-24-padding}",
                        OffsetMax = $"{-padding} {-padding}"
                    },
                    new CuiTextComponent
                    {
                        Color = generalTextColor,
                        Text = titletxt,
                        Align = TextAnchor.MiddleCenter
                    }
                }
            });
            var text = Create(container, window, new CuiElement
            {
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{padding} {padding}",
                        OffsetMax = $"{-padding} {-28-padding}"
                    },
                    new CuiTextComponent
                    {
                        Color = generalTextColor,
                        Text = msg,
                        Align = TextAnchor.MiddleCenter
                    }
                }
            });
            CuiHelper.DestroyUi(editingPlayer, shadow);
            CuiHelper.AddUi(editingPlayer, container);
        }

        private void ShowModesGrid()
        {
            var gridid = ID + "modegrid";
            var container = new CuiElementContainer();
            var modeGrid = Create(container, pageId, new CuiElement
            {
                Name = gridid,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{padding} {padding}",
                        OffsetMax = $"{-padding} {-padding}"
                    }
                }
            });
            var modeGridHeader = Create(container, modeGrid, new CuiElement
            {
                Name = gridid,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"0 {-modeGridColHeight}",
                        OffsetMax = $"0 0"
                    }
                }
            });

            var modes = ModifiedConfig.Modes;

            var left = 0f;
            var colidx = 0;
            foreach (var col in ModeColumns) 
            {
                if (col.HideCol?.Invoke() ?? false) { continue; }
                var colLabel = Create(container, modeGridHeader, new CuiElement
                {
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 1",
                            OffsetMin = $"{left} 0",
                            OffsetMax = $"{left+col.Width} 0"
                        },
                        new CuiTextComponent
                        {
                            Text = col.Label,
                            Color = generalTextColor,
                            FontSize = LabelFontSize,
                            Align = UnityEngine.TextAnchor.UpperLeft
                        }
                    }
                });
                if (col.SubLabel != null)
                {
                    var colSubLabel = Create(container, modeGridHeader, new CuiElement
                    {
                        Components =
                        {
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "0 1",
                                OffsetMin = $"{left} {-4}",
                                OffsetMax = $"{left+col.Width} {-4}"
                            },
                            new CuiTextComponent
                            {
                                Text = "(" + col.SubLabel + ")",
                                FontSize = LabelFontSize-2,
                                Color = generalTextColorFaded,
                                Align = UnityEngine.TextAnchor.LowerLeft
                            }
                        }
                    });
                }
                if (col.HelpText != null)
                {
                    var btnSize = 11;
                    var offset = -2;
                    var helpBtn = Create(container, modeGridHeader, new CuiElement
                    {
                        Components =
                        {
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 1",
                                AnchorMax = "0 1",
                                OffsetMin = $"{left+col.Width-btnSize} {-btnSize+offset}",
                                OffsetMax = $"{left+col.Width} {offset}"
                            },
                            new CuiButtonComponent
                            {
                                Sprite = "assets/icons/info.png",
                                Color = generalTextColor,
                                Command = $"warmode.ui.helptext {col.Label.Split(" ").Length} {col.Label} {col.HelpText}"
                            }
                        }
                    });
                }
                

                var down = modeGridColHeight + modeGridRowGap;
                var modeidx = 0;
                foreach (var mode in modes)
                {
                    if (col.Property == null)
                    {
                        if (ModifiedConfig.Settings.NpcMode != mode.Name)
                        {
                            var btnsize = 12;
                            var addbtnsprite = Create(container, modeGrid, new CuiElement
                            {
                                Components =
                                {
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0 1",
                                        AnchorMax = "0 1",
                                        OffsetMin = $"{left} {-down-modeGridRowHeight/2f-btnsize/2}",
                                        OffsetMax = $"{left+btnsize} {-down-modeGridRowHeight/2f+btnsize/2}"
                                    },
                                    new CuiButtonComponent
                                    {
                                        Sprite = "assets/icons/close.png",
                                        Color = negcolor,
                                        Command = $"warmode.ui.deletemode {mode.Name}"
                                    }
                                }
                            });
                        }
                    }
                    else if (!(col.Hidden?.Invoke(mode) ?? false))
                    {
                        var modeField = Create(container, modeGrid, new CuiElement
                        {
                            Components =
                            {
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 1",
                                    AnchorMax = "0 1",
                                    OffsetMin = $"{left} {-down-modeGridRowHeight}",
                                    OffsetMax = $"{left+col.Width} {-down}"
                                },
                                new CuiImageComponent
                                {
                                    Color = inputFieldBgColor
                                }
                            }
                        });

                        var value = col.Value.Invoke(mode);
                        var readOnly = col.ReadOnly?.Invoke(mode) ?? false;
                        var modeInput = Create(container, modeField, new CuiElement
                        {
                            Components =
                            {
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0",
                                    AnchorMax = "1 1",
                                    OffsetMin = $"{4} {0}",
                                    OffsetMax = $"{-2} {0}"
                                },
                                new CuiInputFieldComponent
                                {
                                    Text = value == null ? string.Empty : value,
                                    FontSize = LabelFontSize,
                                    Align = UnityEngine.TextAnchor.MiddleLeft,
                                    Command = $"warmode.ui.setmodeproperty {mode.Name} {col.Property}",
                                    NeedsKeyboard = true,
                                    Color = readOnly ? "1 1 1 0.1" : generalTextColor,
                                    ReadOnly = readOnly
                                }
                            }
                        });

                        if (col.Type == "color")
                        {
                            UnityEngine.Color color = UnityEngine.Color.white;
                            ColorUtility.TryParseHtmlString(col.Value(mode), out color);
                            var colorbox = Create(container, modeField, new CuiElement
                            {
                                Components =
                                {
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "1 0.5",
                                        AnchorMax = "1 0.5",
                                        OffsetMin = $"{-20} {-8}",
                                        OffsetMax = $"{-4} {8}"
                                    },
                                    new CuiImageComponent
                                    {
                                        Color = color.ToColorString()
                                    }
                                }
                            });
                        }
                    }

                    if (colidx == 1 && modeidx == modes.Length-1)
                    {
                        var addbtn = Create(container, modeGrid, new CuiElement
                        {
                            Components =
                            {
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = $"0 1",
                                    AnchorMax = $"0 1",
                                    OffsetMin = $"{left+padding} {-down-modeGridRowHeight-modeGridRowHeight-modeGridRowGap}",
                                    OffsetMax = $"{left+col.Width-padding} {-down-modeGridRowHeight-modeGridRowGap}"
                                },
                                new CuiButtonComponent
                                {
                                    Color = headerBgColor,
                                    Command = $"warmode.ui.addmode"
                                }
                            }
                        });
                        var addbtnsprite = Create(container, addbtn, new CuiElement
                        {
                            Components =
                            {
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0.5",
                                    AnchorMax = "0 0.5",
                                    OffsetMin = $"{toolBtnPadding} {-toolBtnSpriteSize/2}",
                                    OffsetMax = $"{toolBtnPadding+toolBtnSpriteSize} {toolBtnSpriteSize/2}"
                                },
                                new CuiImageComponent
                                {
                                    Sprite = "assets/icons/add.png",
                                    Color = toolBtnTextColor,
                                }
                            }
                        });
                        var addbtntext = Create(container, addbtn, new CuiElement
                        {
                            Components =
                            {
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0",
                                    AnchorMax = "1 1",
                                    OffsetMin = $"{toolBtnPadding+toolBtnSpriteSize} {0}",
                                    OffsetMax = $"{0} {0}"
                                },
                                new CuiTextComponent
                                {
                                    Color = headerTextColor,
                                    Align = TextAnchor.MiddleCenter,
                                    Text = "ADD MODE",
                                    FontSize = 9
                                }
                            }
                        });
                    }

                    down += modeGridColHeight + modeGridRowGap;
                    modeidx += 1;
                }

                left = left + col.Width + modeGridColGap;
                colidx += 1;
            }


            CuiHelper.DestroyUi(editingPlayer, gridid);
            CuiHelper.AddUi(editingPlayer, container);

        }

    }
}

namespace Oxide.Plugins
{
    public partial class WarModeAdminPanel
    {
        [Command("warmode.config", "wmc")]
        private void CmdWarModeConfig(IPlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.Id, "warmode.admin")) { return; }
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null) { return; }
            if (editingPlayer != null)
            {
                basePlayer.ChatMessage($"The player {editingPlayer.displayName} is currently using the admin panel. Only one person can be using it at a time in order to prevent conflicts.");
                return;
            }
            ShowUI(basePlayer);
        }

        [Command("warmode.ui.setrule")]
        private void CmdWarModeUiSetRule(IPlayer player, string command, string[] args)
        {
            // warmode.ui.setrule <modeName> <ruleIdx> <targetModeName> <ttype>
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null || basePlayer != editingPlayer) { return; }
            var modeName = args[0];
            var ruleid = args[1];
            var targetModeName = args[2];
            var ttype = Enum.Parse<TargetType>(args[3]);
            var editingMode = ModifiedConfig.Modes.First(x => x.Name == modeName);
            var id = $"val.{targetModeName}.{ruleid}.{ttype}";
            var value = false;
            // EDIT HERE
            switch (ruleid)
            {
                case "canattack":
                    editingMode.CanAttackTypes[ttype] = ModifyConfigList(editingMode.CanAttackTypes[ttype], targetModeName);
                    value = editingMode.CanAttackTypes[ttype].Contains(targetModeName);
                    break;
                //case "canraid":
                //    editingMode.CanRaid = ModifyConfigList(editingMode.CanRaid, targetModeName);
                //    value = editingMode.CanRaid.Contains(targetModeName);
                //    break;
                case "canloot":
                    editingMode.CanLootTypes[ttype] = ModifyConfigList(editingMode.CanLootTypes[ttype], targetModeName);
                    value = editingMode.CanLootTypes[ttype].Contains(targetModeName);
                    break;
                case "cantarget":
                    editingMode.CanTargetTypes[ttype] = ModifyConfigList(editingMode.CanTargetTypes[ttype], targetModeName);
                    value = editingMode.CanTargetTypes[ttype].Contains(targetModeName);
                    break;
                case "canmount":
                    editingMode.CanMountTypes[ttype] = ModifyConfigList(editingMode.CanMountTypes[ttype], targetModeName);
                    value = editingMode.CanMountTypes[ttype].Contains(targetModeName);
                    break;
                case "canenter":
                    editingMode.CanEnterTypes[ttype] = ModifyConfigList(editingMode.CanEnterTypes[ttype], targetModeName);
                    value = editingMode.CanEnterTypes[ttype].Contains(targetModeName);
                    break;
                //case "canlootvehicle":
                //    editingMode.CanLootOwnedVehicles = ModifyConfigList(editingMode.CanLootOwnedVehicles, targetModeName);
                //    value = editingMode.CanLootOwnedVehicles.Contains(targetModeName);
                //    break;
                //case "mo":
                //    editingMode.CanEnterOwnedMonuments = ModifyConfigList(editingMode.CanEnterOwnedMonuments, targetModeName);
                //    value = editingMode.CanEnterOwnedMonuments.Contains(targetModeName);
                //    break;
                default:
                    break;
            }
            if (!isDirty)
            {
                isDirty = true;
                CreateBottomButton();
            }
            UpdateButtonText(id, value.ToString(), value ? poscolor : negcolor);
        }

        [Command("warmode.ui.togglemoderule")]
        private void CmdWarModeUiToggleModeRule(IPlayer player, string command, string[] args)
        {
            // warmode.ui.togglemoderule <modeName> <targetMode>
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null || basePlayer != editingPlayer) { return; }
            var modeName = args[0];
            var targetModeName = args[1];
            var editingMode = ModifiedConfig.Modes.First(x => x.Name == modeName);
            if (editingMode.CanAttackTypes.Keys.Count == 0) { return; }
            var removing = editingMode.CanAttackTypes[editingMode.CanAttackTypes.Keys.First()].Contains(targetModeName);
            var modeschanges = new List<string>();
            // EDIT HERE
            foreach (var ttype in (TargetType[])Enum.GetValues(typeof(TargetType)))
            {
                if (editingMode.CanAttackTypes.ContainsKey(ttype))
                    editingMode.CanAttackTypes[ttype] = ModifyConfigList(editingMode.CanAttackTypes[ttype], targetModeName, !removing); modeschanges.Add("canattack");
                if (editingMode.CanLootTypes.ContainsKey(ttype))
                    editingMode.CanLootTypes[ttype] = ModifyConfigList(editingMode.CanLootTypes[ttype], targetModeName, !removing); modeschanges.Add("canloot");
                if (editingMode.CanTargetTypes.ContainsKey(ttype))
                    editingMode.CanTargetTypes[ttype] = ModifyConfigList(editingMode.CanTargetTypes[ttype], targetModeName, !removing); modeschanges.Add("cantarget");
                if (editingMode.CanMountTypes.ContainsKey(ttype))
                    editingMode.CanMountTypes[ttype] = ModifyConfigList(editingMode.CanMountTypes[ttype], targetModeName, !removing); modeschanges.Add("canmount");
                if (editingMode.CanEnterTypes.ContainsKey(ttype))
                    editingMode.CanEnterTypes[ttype] = ModifyConfigList(editingMode.CanEnterTypes[ttype], targetModeName, !removing); modeschanges.Add("canenter");

            }
            //editingMode.CanAttack = ModifyConfigList(editingMode.CanAttack, targetModeName, !removing); modeschanges.Add("canattack");
            //editingMode.CanRaid = ModifyConfigList(editingMode.CanRaid, targetModeName, !removing); modeschanges.Add("canraid");
            //editingMode.CanLoot = ModifyConfigList(editingMode.CanLoot, targetModeName, !removing); modeschanges.Add("canloot");
            //editingMode.CanTargetWithTraps = ModifyConfigList(editingMode.CanTargetWithTraps, targetModeName, !removing); modeschanges.Add("cantarget");
            //editingMode.CanMountOwnedVehicles = ModifyConfigList(editingMode.CanMountOwnedVehicles, targetModeName, !removing); modeschanges.Add("canmount");
            //editingMode.CanLootOwnedVehicles = ModifyConfigList(editingMode.CanLootOwnedVehicles, targetModeName, !removing); modeschanges.Add("canlootvehicle");
            if (!isDirty)
            {
                isDirty = true;
                //CreateBottomButton();
            }
            //foreach (var ruleid in modeschanges.Distinct())
            //{
            //    foreach(var ttype in (TargetType[])Enum.GetValues(typeof(TargetType)))
            //    {
            //        var id = $"val.{targetModeName}.{ruleid}.{ttype}";
            //        UpdateButtonText(id, (!removing).ToString(), !removing ? poscolor : negcolor);
            //    }
            //}
            ReloadRulesPage();
        }


        string selectedMode = null;
        [Command("warmode.ui.setmode")]
        private void CmdWarModeUiSetMode(IPlayer player, string command, string[] args)
        {
            // warmode.ui.setmode <modeName>
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null || basePlayer != editingPlayer) { return; }
            var modeName = args[0];
            selectedMode = modeName;
            //EditRules(ID + "right");
            ReloadRulesPage();
        }

        [Command("warmode.ui.setting")]
        private void CmdWarModeUiSetting(IPlayer player, string command, string[] args)
        {
            // warmode.ui.setting <modeName> <settingId> <inputVal?>
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null || basePlayer != editingPlayer) { return; }
            var modeName = args[0];
            var settingid = args[1];
            string input = null;
            if (args.Length >= 3)
            {
                input = string.Join(' ', args.Skip(2));
            }
            var value = false;
            var modeConfig = ModifiedConfig.Modes.First(x => x.Name == modeName);
            var textColor = generalTextColor;
            switch (settingid)
            {
                case "rbpvp":
                    ModifiedConfig.RaidableBases.CanEnterPvpRaidableBases = ModifyConfigList(ModifiedConfig.RaidableBases.CanEnterPvpRaidableBases, modeName);
                    value = ModifiedConfig.RaidableBases.CanEnterPvpRaidableBases.Contains(modeName);
                    break;
                case "rbpve":
                    ModifiedConfig.RaidableBases.CanEnterPveRaidableBases = ModifyConfigList(ModifiedConfig.RaidableBases.CanEnterPveRaidableBases, modeName);
                    value = ModifiedConfig.RaidableBases.CanEnterPveRaidableBases.Contains(modeName);
                    break;
                case "marker":
                    modeConfig.ShowMarkerWhenAimedAt = !modeConfig.ShowMarkerWhenAimedAt;
                    value = modeConfig.ShowMarkerWhenAimedAt;
                    break;
                case "allowfire":
                    modeConfig.AlwaysAllowFireDamage = !modeConfig.AlwaysAllowFireDamage;
                    value = modeConfig.AlwaysAllowFireDamage;
                    break;
                case "allowheli":
                    modeConfig.CanTakePatrolHeliDamage = !modeConfig.CanTakePatrolHeliDamage;
                    value = modeConfig.CanTakePatrolHeliDamage;
                    break;
                default:
                    break;
            }
            if (!isDirty)
            {
                isDirty = true;
                CreateBottomButton();
            }
            if (input == null)
            {
                var id = $"setting.{settingid}.value";
                UpdateButtonText(id, (value).ToString(), value ? poscolor : negcolor);
            }
            else
            {
                var id = $"setting.{settingid}.value";
                UpdateInputText(id, input, textColor);
            }

            //ReloadUI();
        }

        [Command("warmode.ui.collapse")]
        private void CmdWarModeUiCollapse(IPlayer player, string command, string[] args)
        {
            // warmode.ui.collapse <ruleid> <true/false>
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null || basePlayer != editingPlayer) { return; }
            var ruleid = args[0];
            if (expanededRules.Contains(ruleid))
            {
                expanededRules.Remove(ruleid);
            }
            else
            {
                expanededRules.Add(ruleid);
            }
            ReloadRulesPage();
        }

        ModeConfig copiedRules = null;

        [Command("warmode.ui.copyrules")]
        private void CmdWarModeUiCopyRules(IPlayer player, string command, string[] args)
        {
            // warmode.ui.copyrules
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null || basePlayer != editingPlayer) { return; }
            copiedRules = JsonConvert.DeserializeObject<ModeConfig>((JsonConvert.SerializeObject(ModifiedConfig.Modes.FirstOrDefault(x => x.Name == selectedMode))));
            ShowToolBar();
        }

        [Command("warmode.ui.pasterules")]
        private void CmdWarModeUiPasteRules(IPlayer player, string command, string[] args)
        {
            // warmode.ui.pasterules
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null || basePlayer != editingPlayer) { return; }
            if (copiedRules == null) { return; }
            var currentMode = ModifiedConfig.Modes.FirstOrDefault(x => x.Name == selectedMode);

            // EDIT HERE
            currentMode.CanAttackTypes = copiedRules.CanAttackTypes;
            currentMode.CanLootTypes = copiedRules.CanLootTypes;
            currentMode.CanTargetTypes = copiedRules.CanTargetTypes;
            currentMode.CanMountTypes = copiedRules.CanMountTypes;
            currentMode.CanEnterTypes = copiedRules.CanEnterTypes;
            isDirty = true;
            ReloadRulesPage();
        }

        ModeConfig addingMode = null;

        [Command("warmode.ui.addmode.show")]
        private void CmdWarModeUAddModeShow(IPlayer player, string command, string[] args)
        {
            // warmode.ui.addmode.show
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null || basePlayer != editingPlayer) { return; }

            addingMode = ModeConfig.New();
            addingMode.Priority = ModifiedConfig.Modes.Max(x => x.Priority) + 1;
            addingMode.Name = addingMode.Name + addingMode.Priority;
            addingMode.DisplayName = addingMode.Name.TitleCase();
            addingMode.Group = addingMode.Group + addingMode.Priority;

            ShowAddModeModal();
        }

        [Command("warmode.ui.addmode.confirm")]
        private void CmdWarModeUAddModeConfirm(IPlayer player, string command, string[] args)
        {
            // warmode.ui.addmode.confirm
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null || basePlayer != editingPlayer) { return; }
            if (addingMode == null) { return; }

            // TODO validation
            string errorMsg = null;
            if (ModifiedConfig.Modes.Any(x => x.Name?.ToLower() == addingMode?.Name.ToLower()))
            {
                errorMsg = "A mode with that name already exists.";
            }
            else if (ModifiedConfig.Modes.Any(x => x.Group?.ToLower() == addingMode?.Group?.ToLower()))
            {
                errorMsg = "A mode with this permission group already exists.";
            }
            UpdateModalValidation("");
            if (errorMsg != null)
            {
                timer.In(0.1f, () =>
                {
                    UpdateModalValidation(errorMsg);
                });
                return;
            }

            var modesList = ModifiedConfig.Modes.ToList();
            modesList.Add(addingMode);
            ModifiedConfig.Modes = modesList.ToArray();

            isDirty = true;
            CuiHelper.DestroyUi(editingPlayer, ID + "modalwindow");
            ReloadRulesPage();
        }

        [Command("warmode.ui.addmode.input")]
        private void CmdWarModeUAddModeInput(IPlayer player, string command, string[] args)
        {
            // warmode.ui.addmode.input <property> <value>
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null || basePlayer != editingPlayer) { return; }

            var property = args[0];
            var input = string.Join(" ", args.Skip(1));

            var id = $"addmode.input.{property}";

            // TODO Validation messages

            switch (property)
            {
                case "name":
                    input = input.Trim().Replace(" ", "_").ToLower();
                    addingMode.Name = input;
                    break;
                case "displayname":
                    input = input.Trim();
                    addingMode.DisplayName = input;
                    break;
                case "color":
                    input = input.Trim();
                    UnityEngine.Color color = UnityEngine.Color.white;
                    if (ColorUtility.TryParseHtmlString(input, out color))
                    {
                        addingMode.ColorHex = input.ToUpper();
                    }
                    else
                    {
                        addingMode.ColorHex = "#" + ColorUtility.ToHtmlStringRGBA(color);
                    }
                    break;
                case "priority":
                    var minNum = ModifiedConfig.Modes.Max(x => x.Priority) + 1 ?? 1;
                    var num = minNum;
                    int.TryParse(input.Trim(), out num);
                    if (num < minNum)
                    {
                        num = minNum;
                    }
                    addingMode.Priority = num;
                    break;
                case "group":
                    input = input.Trim().Replace(" ", "_").ToLower();
                    addingMode.Group = input;
                    break;
                default:
                    break;
            }

            // TODO Update Property value
            ShowAddModeModal();
        }

        [Command("warmode.ui.setsummary")]
        private void CmdWarModeUiSetSummary(IPlayer player, string command, string[] args)
        {
            // warmode.ui.togglemoderule <modeName> <targetMode> <ruleid>
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null || basePlayer != editingPlayer) { return; }
            var modeName = args[0];
            var targetModeName = args[1];
            var ruleid = args[2];
            var editingMode = ModifiedConfig.Modes.First(x => x.Name == modeName);
            // EDIT HERE
            Dictionary<TargetType, string[]> dict = null;
            TargetType[] ttypes = null;
            switch (ruleid)
            {
                case "canattack":
                    dict = editingMode.CanAttackTypes;
                    ttypes = TargetTypesForCategory.Attacking;
                    break;
                case "canloot":
                    dict = editingMode.CanLootTypes;
                    ttypes = TargetTypesForCategory.Looting;
                    break;
                case "cantarget":
                    dict = editingMode.CanTargetTypes;
                    ttypes = TargetTypesForCategory.Targeting;
                    break;
                case "canmount":
                    dict = editingMode.CanMountTypes;
                    ttypes = TargetTypesForCategory.Mounting;
                    break;
                case "canenter":
                    dict = editingMode.CanEnterTypes;
                    ttypes = TargetTypesForCategory.Entering;
                    break;
                default:
                    return;
            }
            var firstval = dict.Values.First().Contains(targetModeName);
            var shouldadd = firstval == false;
            foreach (var ttype in ttypes)
            {
                dict[ttype] = ModifyConfigList(dict[ttype], targetModeName, shouldadd);
            }

            if (!isDirty)
            {
                isDirty = true;
                CreateBottomButton();
            }

            ReloadRulesPage(); // TODO
        }

        private string[] ModifyConfigList(string[] original, string value, bool? given = null)
        {
            if (original == null) { return null; }
            bool adding;
            if (given == null)
            {
                adding = !original.Contains(value);
            }
            else
            {
                adding = given.Value;
            }
            var asList = original.ToList();
            if (adding && !original.Contains(value))
            {
                asList.Add(value);
                return asList.ToArray();
            }
            else if (!adding && original.Contains(value))
            {
                asList.Remove(value);
                return asList.ToArray();
            }
            return original;
        }

        private const string ID = "warmodeconfig";

        private List<string> expanededRules = new List<string>();

        public void ReloadRulesPage()
        {
            if (string.IsNullOrWhiteSpace(selectedMode))
            {
                selectedMode = ModifiedConfig.Modes.FirstOrDefault()?.Name;
            }

            if (ModifiedConfig.Modes.Length <= minModeColNum)
            {
                modeColWidth = maxModeColWidth;
            }
            else
            {
                modeColWidth = (totalModeColWidth - (ModifiedConfig.Modes.Length * modeColGap)) / (float)ModifiedConfig.Modes.Length;
            }

            if (descriptive)
            {
                ruleRowHeight = 32;
            }
            else
            {
                ruleRowHeight = 20;
            }


            var container = new CuiElementContainer();

            pageId = Create(container, mainPanel, new CuiElement
            {
                Name = ID + "page",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{0} {0}",
                        OffsetMax = $"{0} {-headerHeight-navHeight-padding}"
                    }
                }
            });

            var leftBar = Create(container, pageId, new CuiElement
            {
                Name = ID + "left",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0.12 1",
                        OffsetMin = $"{padding} {bottomHeight+padding}",
                        OffsetMax = $"{-padding} {padding}"
                    },
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0"
                    }
                }
            });

            var leftText = Create(container, leftBar, new CuiElement
            {
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"{0} {-panelHeaderHeight-padding}",
                        OffsetMax = $"{0} {-padding}"
                    },
                    new CuiTextComponent
                    {
                        Text = "ALL MODES",
                        Color = generalTextColor,
                        FontSize = 14,
                        Align = UnityEngine.TextAnchor.UpperCenter
                    }
                }
            });

            AllModesList(container, leftBar);

            var rightBar = Create(container, pageId, new CuiElement
            {
                Name = ID + "right",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.15 0",
                        AnchorMax = "0.82 1",
                        OffsetMin = $"{0} {bottomHeight+padding}",
                        OffsetMax = $"{-padding} {padding-panelHeaderText}"
                    },
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0"
                    }
                }
            });

            var rightText = Create(container, rightBar, new CuiElement
            {
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"{padding} {-panelHeaderHeight-padding}",
                        OffsetMax = $"{-padding} {-padding}"
                    },
                    new CuiTextComponent
                    {
                        Text = $"Editing rules for {Color(selectedMode.ToUpper(), selcolor)} players",
                        Color = generalTextColor,
                        FontSize = generalTextSize,
                        Align = UnityEngine.TextAnchor.UpperCenter
                    }
                }
            });

            var settingsBar = Create(container, pageId, new CuiElement
            {
                Name = ID + "settings",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.82 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{0} {bottomHeight+padding}",
                        OffsetMax = $"{-padding} {padding-panelHeaderText-38}"
                    },
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0"
                    }
                }
            });

            CuiHelper.DestroyUi(editingPlayer, pageId);
            CuiHelper.AddUi(editingPlayer, container);

            ShowToolBar();
            CreateSettingsBar();
            EditRules(rightBar);
            CreateBottomButton();
        }

        public void ShowToolBar()
        {
            var container = new CuiElementContainer();


            var rightBar = ID + "right";
            // Toolbar
            var buttons = new[]
            {
                new
                {
                    Text = "COPY RULES",
                    Sprite = "assets/icons/refresh.png",
                    Command = "warmode.ui.copyrules"
                },
                new
                {
                    Text = "PASTE RULES",
                    Sprite = "assets/icons/download.png",
                    Command = "warmode.ui.pasterules"
                }
            };
            var toolbarPanel = Create(container, rightBar, new CuiElement
            {
                Name = ID + "toolbar",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = $"{toolBtnPadding} {-toolBtnPadding-toolBtnHeight}",
                        OffsetMax = $"{200} {-toolBtnPadding}"
                    }
                }
            });

            var left = 0f;
            foreach (var btn in buttons)
            {
                var toolbtn = Create(container, toolbarPanel, new CuiElement
                {
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 1",
                            OffsetMin = $"{left} {0}",
                            OffsetMax = $"{left+toolBtnWidth} {0}"
                        },
                        new CuiButtonComponent
                        {
                            Command = btn.Command,
                            Color = toolBtnColor
                        }
                    }
                });
                var toolbtnsprite = Create(container, toolbtn, new CuiElement
                {
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0.5",
                            AnchorMax = "0 0.5",
                            OffsetMin = $"{toolBtnPadding} {-toolBtnSpriteSize/2}",
                            OffsetMax = $"{toolBtnPadding+toolBtnSpriteSize} {toolBtnSpriteSize/2}"
                        },
                        new CuiImageComponent
                        {
                            Sprite = btn.Sprite,
                            Color = toolBtnTextColor,
                        }
                    }
                });
                var toolbtntxt = Create(container, toolbtn, new CuiElement
                {
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                            OffsetMin = $"{toolBtnPadding+toolBtnSpriteSize+toolBtnPadding} {0}",
                            OffsetMax = $"{-toolBtnPadding} {0}"
                        },
                        new CuiTextComponent
                        {
                            Text = btn.Text,
                            Color = toolBtnTextColor,
                            FontSize = toolBtnTextSize,
                            Align = UnityEngine.TextAnchor.MiddleCenter,
                        }
                    }
                });
                left += toolBtnGap + toolBtnWidth;
            }

            CuiHelper.DestroyUi(editingPlayer, ID + "toolbar");
            CuiHelper.AddUi(editingPlayer, container);
        }

        public void CreateSettingsBar()
        {
            var parent = ID + "settings";
            var container = new CuiElementContainer();

            var titleh = 24;

            var panel = Create(container, parent, new CuiElement
            {
                Name = ID + "settingspanel",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    },
                }
            });

            var titlePanel = Create(container, panel, new CuiElement
            {
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"{0} {-titleh}",
                        OffsetMax = $"{0} {0}"
                    },
                    new CuiImageComponent
                    {
                        Color = headerBgColor
                    }
                }
            });
            var titlePanelText = Create(container, titlePanel, new CuiElement
            {
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"{0} {-titleh}",
                        OffsetMax = $"{0} {0}"
                    },
                    new CuiTextComponent
                    {
                        Text = $"{selectedMode.ToUpper()} SETTINGS",
                        Color = headerTextColor,
                        Align = UnityEngine.TextAnchor.MiddleCenter
                    }
                }
            });
            var modeConfig = ModifiedConfig.Modes.First(x => x.Name == selectedMode);

            var settings = new[]
            {
                new
                {
                    ID = "marker",
                    Enabled = true,
                    Label = "Show Marker When Aimed At",
                    Value = (object)modeConfig.ShowMarkerWhenAimedAt
                },
                new
                {
                    ID = "rbpvp",
                    Enabled = RaidableBases.IsLoaded(),
                    Label = "Can Enter PVP Raidable Bases",
                    Value = (object)CONFIG.RaidableBases?.CanEnterPvpRaidableBases?.Contains(selectedMode) ?? false
                },
                new
                {
                    ID = "rbpve",
                    Enabled = RaidableBases.IsLoaded(),
                    Label = "Can Enter PVE Raidable Bases",
                    Value = (object) CONFIG.RaidableBases?.CanEnterPveRaidableBases?.Contains(selectedMode) ?? false
                },
                new
                {
                    ID = "allowfire",
                    Enabled = true,
                    Label = "Always Allow Fire Damage",
                    Value = (object) modeConfig.AlwaysAllowFireDamage
                },
                new
                {
                    ID = "allowheli",
                    Enabled = true,
                    Label = "Can Take Patrol Heli Damage",
                    Value = (object) modeConfig.CanTakePatrolHeliDamage
                },
            };

            var down = titleh + padding;
            var settingh = 32;
            var settingp = 1;
            var valh = 24;
            var valw = 50;
            var inputvalw = 100;
            foreach (var setting in settings)
            {
                if (!setting.Enabled) { continue; }
                var id = $"setting.{setting.ID}";
                var settpanel = Create(container, panel, new CuiElement
                {
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "1 1",
                            OffsetMin = $"{0} {-down-settingh}",
                            OffsetMax = $"{0} {-down}"
                        },
                        new CuiImageComponent
                        {
                            Color = headerBgColor
                        }
                    }
                });
                var setttxt = Create(container, settpanel, new CuiElement
                {
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0.7 1",
                            OffsetMin = $"{8} {4}",
                            OffsetMax = $"{-4} {-4}"
                        },
                        new CuiTextComponent
                        {
                            Text = setting.Label,
                            Align = UnityEngine.TextAnchor.MiddleLeft,
                            FontSize = 9,
                            Color = generalTextColor
                        }
                    }
                });
                string settbtn = null;
                if (setting.Value is bool)
                {
                    settbtn = Create(container, settpanel, new CuiElement
                    {
                        Components =
                        {
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "1 0.5",
                                AnchorMax = "1 0.5",
                                OffsetMin = $"{-4-valw} {-valh/2}",
                                OffsetMax = $"{-4} {valh/2}"
                            },
                            new CuiButtonComponent
                            {
                                Color = "0 0 0 0.7",
                                Command = $"warmode.ui.setting {selectedMode} {setting.ID}"
                            }
                        }
                    });
                }
                else if (setting.Value is string)
                {
                    settbtn = Create(container, settpanel, new CuiElement
                    {
                        Components =
                        {
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "1 0.5",
                                AnchorMax = "1 0.5",
                                OffsetMin = $"{-4-inputvalw} {-valh/2}",
                                OffsetMax = $"{-4} {valh/2}"
                            },
                            new CuiImageComponent
                            {
                                Color = "0 0 0 0.7"
                            }
                        }
                    });
                }

                if (setting.Value is bool boolVal)
                {
                    var setttext = Create(container, settbtn, new CuiElement
                    {
                        Name = $"setting.{setting.ID}.value",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Color = boolVal ? poscolor : negcolor,
                                Text = boolVal.ToString(),
                                Align = UnityEngine.TextAnchor.MiddleCenter,
                                FontSize = 11
                            }
                        }
                    });
                }
                else if (setting.Value is string strVal)
                {
                    var parsedColor = UnityEngine.Color.white;
                    if (setting.ID == "color")
                    {
                        ColorUtility.TryParseHtmlString(strVal, out parsedColor);
                    }

                    var setttext = Create(container, settbtn, new CuiElement
                    {
                        Name = $"setting.{setting.ID}.value",
                        Components =
                        {
                            new CuiRectTransformComponent
                            {
                                OffsetMin = "4 0",
                                OffsetMax = "-4 0"
                            },
                            new CuiInputFieldComponent
                            {
                                NeedsKeyboard = true,
                                LineType = InputField.LineType.SingleLine,
                                Color = setting.ID == "color" ? parsedColor.ToColorString() : generalTextColor,
                                Text = strVal,
                                Align = UnityEngine.TextAnchor.MiddleLeft,
                                FontSize = 10,
                                Command = $"warmode.ui.setting {selectedMode} {setting.ID}"
                            }
                        }
                    });
                }

                down += settingh + settingp;
            }

            //var addbtn = Create(container, panel, new CuiElement
            //{
            //    Components =
            //    {
            //        new CuiRectTransformComponent
            //        {
            //            AnchorMin = "0.5 0",
            //            AnchorMax = "0.5 0",
            //            OffsetMin = $"{-40} {8}",
            //            OffsetMax = $"{40} {24+8}"
            //        },
            //        new CuiButtonComponent
            //        {
            //            Color = headerBgColor,
            //            Command = $"warmode.ui.deletemode"
            //        }
            //    }
            //});
            //var addbtnsprite = Create(container, addbtn, new CuiElement
            //{
            //    Components =
            //    {
            //        new CuiRectTransformComponent
            //        {
            //            AnchorMin = "0 0.5",
            //            AnchorMax = "0 0.5",
            //            OffsetMin = $"{toolBtnPadding} {-toolBtnSpriteSize/2}",
            //            OffsetMax = $"{toolBtnPadding+toolBtnSpriteSize} {toolBtnSpriteSize/2}"
            //        },
            //        new CuiImageComponent
            //        {
            //            Sprite = "assets/icons/clear.png",
            //            Color = negcolor,
            //        }
            //    }
            //});
            //var addbtntext = Create(container, addbtn, new CuiElement
            //{
            //    Components =
            //    {
            //        new CuiRectTransformComponent
            //        {
            //            AnchorMin = "0 0",
            //            AnchorMax = "1 1",
            //            OffsetMin = $"{toolBtnPadding+toolBtnSpriteSize} {0}",
            //            OffsetMax = $"{0} {0}"
            //        },
            //        new CuiTextComponent
            //        {
            //            Color = negcolor,
            //            Align = TextAnchor.MiddleCenter,
            //            Text = "DELETE MODE",
            //            FontSize = 9
            //        }
            //    }
            //});

            CuiHelper.DestroyUi(editingPlayer, panel);
            CuiHelper.AddUi(editingPlayer, container);
        }

        public void AllModesList(CuiElementContainer container, string parent)
        {
            var content = Create(container, parent, new CuiElement
            {
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{0} {padding}",
                        OffsetMax = $"{0} {-padding-panelHeaderHeight-padding}"
                    }
                }
            });
            var mh = 24f;
            var down = 0f;
            foreach (var mode in ModifiedConfig.Modes)
            {
                var entry = Create(container, content, new CuiElement
                {
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "1 1",
                            OffsetMin = $"{0} {-down-mh}",
                            OffsetMax = $"{0} {-down}"
                        },
                        new CuiButtonComponent
                        {
                            //Color = "0 1 0 1",
                            Color = selectedMode == mode.Name ? headerBgColor : "0 0 0 0",
                            Command = $"warmode.ui.setmode {mode.Name}"
                        }
                    }
                });
                var entryText = Create(container, entry, new CuiElement
                {
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                            OffsetMin = $"{padding} {0}",
                            OffsetMax = $"{-padding} {0}"
                        },
                        new CuiTextComponent
                        {
                            Text = selectedMode == mode.Name ? Color(mode.Name.ToUpper(), selcolor) : mode.Name.ToUpper(),
                            Color = generalTextColor,
                            FontSize = 12,
                            Align = UnityEngine.TextAnchor.MiddleLeft
                        }
                    }
                });
                down += mh;
            }
            //var addbtn = Create(container, content, new CuiElement
            //{
            //    Components =
            //    {
            //        new CuiRectTransformComponent
            //        {
            //            AnchorMin = "0.5 0",
            //            AnchorMax = "0.5 0",
            //            OffsetMin = $"{-40} {0}",
            //            OffsetMax = $"{40} {24}"
            //        },
            //        new CuiButtonComponent
            //        {
            //            Color = headerBgColor,
            //            Command = $"warmode.ui.addmode.show"
            //        }
            //    }
            //});
            //var addbtnsprite = Create(container, addbtn, new CuiElement
            //{
            //    Components =
            //    {
            //        new CuiRectTransformComponent
            //        {
            //            AnchorMin = "0 0.5",
            //            AnchorMax = "0 0.5",
            //            OffsetMin = $"{toolBtnPadding} {-toolBtnSpriteSize/2}",
            //            OffsetMax = $"{toolBtnPadding+toolBtnSpriteSize} {toolBtnSpriteSize/2}"
            //        },
            //        new CuiImageComponent
            //        {
            //            Sprite = "assets/icons/add.png",
            //            Color = toolBtnTextColor,
            //        }
            //    }
            //});
            //var addbtntext = Create(container, addbtn, new CuiElement
            //{
            //    Components =
            //    {
            //        new CuiRectTransformComponent
            //        {
            //            AnchorMin = "0 0",
            //            AnchorMax = "1 1",
            //            OffsetMin = $"{toolBtnPadding+toolBtnSpriteSize} {0}",
            //            OffsetMax = $"{0} {0}"
            //        },
            //        new CuiTextComponent
            //        {
            //            Color = headerTextColor,
            //            Align = TextAnchor.MiddleCenter,
            //            Text = "ADD MODE",
            //            FontSize = 9
            //        }
            //    }
            //});
        }

        private const string DARKEN = "0.5 0.4 0.4 0.1";
        private const string LIGHTEN = "1 1 1 0.1";

        public void EditRules(string parent)
        {
            var container = new CuiElementContainer();
            var allModeKeys = ModifiedConfig.Modes.Select(x => x.Name);
            var mode = ModifiedConfig.Modes.FirstOrDefault(x => x.Name == selectedMode);
            var modeNameUpper = Color(mode.Name.ToUpper(), selcolor);

            // EDIT HERE
            // ADD TARGET TYPE
            var rules = new[]
            {
                new
                {
                    ID = "canattack",
                    Enabled = true,
                    Title = "ATTACKING",
                    Text = "Can {0} players attack {1} with these modes?",
                    TextOwned = "Can {0} players attack {1} owned by these modes?",
                    TextWithinBases = "Can {0} players attack {1} within bases owned by players with these modes?",
                    Ttypes = TargetTypesForCategory.Attacking.Where(x => TargetTypesForCategory.IsLoaded(x, ModifiedConfig)).ToArray(),
                    Values = TargetTypesForCategory.Attacking.Select(ttype => new KeyValuePair<TargetType, bool[]>(ttype, allModeKeys.Select(modekey => mode.CanAttackTypes[ttype].Contains(modekey)).ToArray() )).ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                },
                new
                {
                    ID = "canloot",
                    Enabled = true,
                    Title = "LOOTING",
                    Text = "Can {0} players loot {1} with these modes?",
                    TextOwned = "Can {0} players loot {1} owned by these modes?",
                    TextWithinBases = "Can {0} players loot {1} within bases owned by players with these modes?",
                    Ttypes = TargetTypesForCategory.Looting.Where(x => TargetTypesForCategory.IsLoaded(x, ModifiedConfig)).ToArray(),
                    Values = TargetTypesForCategory.Looting.Select(ttype => new KeyValuePair<TargetType, bool[]>(ttype, allModeKeys.Select(modekey => mode.CanLootTypes[ttype].Contains(modekey)).ToArray() )).ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                },
                new
                {
                    ID = "cantarget",
                    Enabled = true,
                    Title = "TARGETING",
                    Text = "Can {1} within bases owned by {0} players target players with these modes?",
                    TextOwned = "Can {1} owned by {0} players target players with these modes?",
                    TextWithinBases = "Can {1} within bases owned by {0} players target players with these modes?",
                    Ttypes = TargetTypesForCategory.Targeting.Where(x => TargetTypesForCategory.IsLoaded(x, ModifiedConfig)).ToArray(),
                    Values = TargetTypesForCategory.Targeting.Select(ttype => new KeyValuePair<TargetType, bool[]>(ttype, allModeKeys.Select(modekey => mode.CanTargetTypes[ttype].Contains(modekey)).ToArray() )).ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                },
                new
                {
                    ID = "canmount",
                    Enabled = true,
                    Title = "MOUNTING",
                    Text = "Can {0} players mount {1} with these modes?",
                    TextOwned = "Can {0} players mount {1} owned by these modes?",
                    TextWithinBases = "Can {0} players mount {1} within bases owned by players with these modes?",
                    Ttypes = TargetTypesForCategory.Mounting.Where(x => TargetTypesForCategory.IsLoaded(x, ModifiedConfig)).ToArray(),
                    Values = TargetTypesForCategory.Mounting.Select(ttype => new KeyValuePair<TargetType, bool[]>(ttype, allModeKeys.Select(modekey => mode.CanMountTypes[ttype].Contains(modekey)).ToArray() )).ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                },
                new
                {
                    ID = "canenter",
                    Enabled = true,
                    Title = "ENTERING",
                    Text = "Can {0} players enter {1} with these modes?",
                    TextOwned = "Can {0} players enter {1} owned by these modes?",
                    TextWithinBases = "Can {0} players enter {1} within bases owned by players with these modes?",
                    Ttypes = TargetTypesForCategory.Entering.Where(x => TargetTypesForCategory.IsLoaded(x, ModifiedConfig)).ToArray(),
                    Values = TargetTypesForCategory.Entering.Select(ttype => new KeyValuePair<TargetType, bool[]>(ttype, allModeKeys.Select(modekey => mode.CanEnterTypes[ttype].Contains(modekey)).ToArray() )).ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                },
                //new
                //{
                //    ID = "canraid",
                //    Enabled = true,
                //    Title = "RAIDING",
                //    Text = $"Can {modeNameUpper} players raid bases that are owned by players with these modes?",
                //    Values = allModeKeys.Select(x => mode.CanRaid.Contains(x)).ToArray()
                //},
                //new
                //{
                //    ID = "canloot",
                //    Enabled = true,
                //    Title = "LOOTING",
                //    Text = $"Can {modeNameUpper} players loot containers that are owned by players with these modes?",
                //    Values = allModeKeys.Select(x => mode.CanLoot.Contains(x)).ToArray()
                //},
                //new
                //{
                //    ID = "cantarget",
                //    Enabled = true,
                //    Title = "TRAP TARGETING",
                //    Text = $"Can {modeNameUpper} players have their traps triggered for players with these modes?",
                //    Values = allModeKeys.Select(x => mode.CanTargetWithTraps.Contains(x)).ToArray()
                //},
                //new
                //{
                //    ID = "canmount",
                //    Enabled = CONFIG.Settings.AllowVehicleModeOwnership,
                //    Title = "MOUNT OWNED VEHICLES",
                //    Text = $"Can {modeNameUpper} players have vehicles they own mounted by players with these modes?",
                //    Values = allModeKeys.Select(x => mode.CanMountOwnedVehicles?.Contains(x) ?? false).ToArray()
                //},
                //new
                //{
                //    ID = "canlootvehicle",
                //    Enabled = CONFIG.Settings.AllowVehicleModeOwnership,
                //    Title = "LOOT OWNED VEHICLES",
                //    Text = $"Can {modeNameUpper} players have vehicles they own looted by players with these modes?",
                //    Values = allModeKeys.Select(x => mode.CanLootOwnedVehicles?.Contains(x) ?? false).ToArray()
                //}
            };

            var leftW = 0.05f;
            var rightW = 1f - leftW;
            // left side
            var leftSide = Create(container, parent, new CuiElement
            {
                Name = ID + "editleft",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = $"{leftW} 1",
                        OffsetMin = $"{padding} {padding}",
                        OffsetMax = $"{0} {-padding-panelHeaderText-padding}"
                    },
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0"
                    }
                }
            });

            var scrollbarh = 14;
            var modeLabelH = 32;
            var modeLabelW = 80;
            var right = 0f;

            var scrollingPanel = Create(container, parent, new CuiElement
            {
                Name = ID + "scrolling",
                Components =
                {
                    new CuiScrollViewComponent
                    {
                        Vertical = true,
                        Horizontal = false,
                        //Horizontal = true,
                        //HorizontalScrollbar = new CuiScrollbar
                        //{
                        //    Invert = true
                        //},
                        VerticalScrollbar = new CuiScrollbar
                        {
                            Size = 10
                        },
                        Elasticity = 0f,
                        MovementType = ScrollRect.MovementType.Clamped,
                        ScrollSensitivity = 50f,
                        ContentTransform = new CuiRectTransform
                        {
                            OffsetMin = "0 -1000",
                            OffsetMax = "0 0"
                        }
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = $"0 0",
                        AnchorMax = $"1 1",
                        OffsetMin = $"{0} {0}",
                        OffsetMax = $"{0} {-modeLabelH-modeLabelH-padding}"
                    },
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0"
                        //Color = "1 0.5 0.5 0.1"
                    }
                }
            });

            // mode headers
            right = modeColGap;
            foreach (var modeKey in allModeKeys)
            {
                var label = Create(container, parent, new CuiElement
                {
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"0 1",
                            AnchorMax = $"0 1",
                            OffsetMin = $"{ruleWidth+right} {-modeLabelH-modeLabelH}",
                            OffsetMax = $"{ruleWidth+right+modeColWidth} {-modeLabelH}"
                        },
                        new CuiButtonComponent
                        {
                            Command = $"warmode.ui.togglemoderule {mode.Name} {modeKey}",
                            Color = "0.5 0.5 0.5 0.1"
                        }
                    }
                });
                var labelText = Create(container, label, new CuiElement
                {
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"0 0",
                            AnchorMax = $"1 1",
                            OffsetMin = $"{2} {2}",
                            OffsetMax = $"{-2} {-2}"
                        },
                        new CuiTextComponent
                        {
                            Text = modeKey.ToUpper(),
                            Color = generalTextColor,
                            Align = UnityEngine.TextAnchor.MiddleCenter,
                            FontSize = 10,
                            VerticalOverflow = VerticalWrapMode.Truncate
                        }
                    }
                });
                right += modeColWidth + modeColGap;
            }

            // rule text
            //var mh = 56f;
            var down = 0f;
            var ruleidx = 0;
            foreach (var rule in rules.Where(x => x.Enabled))
            {
                var collapsed = !expanededRules.Contains(rule.ID);
                var localdown = 0f;
                var totalpanelh = ruleHeaderHeight;
                totalpanelh += ruleGap;
                if (!collapsed)
                {
                    totalpanelh += rule.Ttypes.Length * (ruleRowHeight + ruleGap);
                }
                var rulepanel = Create(container, scrollingPanel, new CuiElement
                {
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "0 1",
                            OffsetMin = $"{0} {-down-totalpanelh}",
                            OffsetMax = $"{ruleWidth} {-down}"
                        },
                        new CuiImageComponent
                        {
                            Color = "0 0 0 0"
                            //Color = ruleidx % 2 == 0 ? DARKEN : LIGHTEN
                            //Color = "0 1 0 1"
                        }
                    }
                });
                var ruleheader = Create(container, rulepanel, new CuiElement
                {
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "1 1",
                            OffsetMin = $"{0} {-localdown-ruleHeaderHeight}",
                            OffsetMax = $"{0} {-localdown}"
                        },
                        new CuiImageComponent
                        {
                            Color = headerBgColor,
                        },
                    }
                });
                var ruleheadertxt = Create(container, ruleheader, new CuiElement
                {
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                            OffsetMin = $"{padding} {0}",
                            OffsetMax = $"{-padding} {0}"
                        },
                        new CuiTextComponent
                        {
                            Text = rule.Title,
                            FontSize = 13,
                            Color = generalTextColor,
                            Align = UnityEngine.TextAnchor.MiddleLeft
                        },
                    }
                });
                var ruleheadercollapsebtn = Create(container, ruleheader, new CuiElement
                {
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "1 0.5",
                            AnchorMax = "1 0.5",
                            OffsetMin = $"{-padding-12} {-6}",
                            OffsetMax = $"{-padding} {6}"
                        },
                        new CuiButtonComponent
                        {
                            Sprite = collapsed ? "assets/icons/add.png" : "assets/icons/subtract.png",
                            Color = headerTextColor,
                            Command = $"warmode.ui.collapse {rule.ID}"
                        }
                    }
                });
                var localright = modeColGap + ruleWidth;
                var summarymodeidx = 0;
                foreach (var modekey in allModeKeys)
                {
                    var targetmode = ModifiedConfig.Modes.FirstOrDefault(x => x.Name == modekey);
                    var alltrue = rule.Values.All(kvp => kvp.Value[summarymodeidx]);
                    var allfalse = rule.Values.All(kvp => !kvp.Value[summarymodeidx]);
                    var summarybtn = Create(container, rulepanel, new CuiElement
                    {
                        Components =
                        {
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 1",
                                AnchorMax = "0 1",
                                OffsetMin = $"{localright} {-localdown-ruleHeaderHeight}",
                                OffsetMax = $"{localright+modeColWidth} {-localdown}"
                            },
                            new CuiButtonComponent
                            {
                                Color = headerBgColor,
                                Command = $"warmode.ui.setsummary {mode.Name} {modekey} {rule.ID}"
                            }
                        }
                    });
                    if (collapsed)
                    {
                        var summarytxt = Create(container, summarybtn, new CuiElement
                        {
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Color = alltrue ? poscolor : allfalse ? negcolor : headerTextColor,
                                    Text = alltrue ? "True" : allfalse ? "False" : "Mixed",
                                    Align = TextAnchor.MiddleCenter,
                                    FontSize = 10
                                }
                            }
                        });
                    }
                    localright += modeColGap + modeColWidth;
                    summarymodeidx++;
                }
                localdown -= (ruleHeaderHeight + ruleGap);
                if (!collapsed)
                {
                    foreach (var ttype in rule.Ttypes)
                    {
                        var ttyperow = Create(container, rulepanel, new CuiElement
                        {
                            Name = ID + $"row.{rule.ID}.{mode.Name}.{ttype}",
                            Components =
                            {
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 1",
                                    AnchorMax = "1 1",
                                    OffsetMin = $"{padding} {localdown-ruleRowHeight}",
                                    OffsetMax = $"{0} {localdown}"
                                },
                                new CuiImageComponent
                                {
                                    Color = LIGHTEN,
                                    //Command = $"warmode.ui.tooltip {ID + $"row.{rule.ID}.{mode.Name}.{ttype}"}"
                                    //Color = "0 0 0 0"
                                    //Color = "0 0 1 1",
                                },
                            }
                        });
                        if (descriptive)
                        {
                            // ADD TARGET TYPE
                            // EDIT HERE
                            var format = rule.Text;
                            switch (ttype)
                            {
                                case TargetType.players:
                                    break;
                                case TargetType.legacyshelters:
                                case TargetType.tugboats:
                                case TargetType.claimedhorses:
                                case TargetType.claimedvehicles:
                                case TargetType.droppedbackpacks:
                                case TargetType.stashes:
                                case TargetType.claimedmonuments:
                                    format = rule.TextOwned;
                                    break;
                                default:
                                    format = rule.TextWithinBases;
                                    break;
                            }
                            var ttypetxt = Create(container, ttyperow, new CuiElement
                            {
                                Components =
                                {
                                    new CuiRectTransformComponent
                                    {
                                        OffsetMin = $"{padding} {0}",
                                        OffsetMax = $"{-padding} {0}"
                                    },
                                    new CuiTextComponent
                                    {
                                        Text = string.Format(format, Color(selectedMode.ToUpper(), selcolor), Color(ttype.ToString().ToUpper(), selcolor)),
                                        Align = UnityEngine.TextAnchor.MiddleLeft,
                                        Color = generalTextColor,
                                        FontSize = 10
                                    },
                                }
                            });
                        }
                        else
                        {
                            var ttypetxt = Create(container, ttyperow, new CuiElement
                            {
                                Components =
                                {
                                    new CuiRectTransformComponent
                                    {
                                        OffsetMin = $"{padding} {0}",
                                        OffsetMax = $"{-padding} {0}"
                                    },
                                    new CuiTextComponent
                                    {
                                        Text = ttype.ToString().ToUpper(),
                                        Align = UnityEngine.TextAnchor.MiddleLeft,
                                        Color = generalTextColor,
                                        FontSize = 11
                                    },
                                }
                            });
                        }

                        // Values
                        int modeidx = 0;
                        var localleft = modeColGap;
                        foreach (var value in rule.Values[ttype])
                        {
                            var modeForCol = allModeKeys.ElementAt(modeidx);
                            var valuebtn = Create(container, ttyperow, new CuiElement
                            {
                                Components =
                                {
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "1 0",
                                        AnchorMax = "1 1",
                                        OffsetMin = $"{localleft} {0}",
                                        OffsetMax = $"{localleft+modeColWidth} {0}"
                                    },
                                    new CuiButtonComponent
                                    {
                                        Color = headerBgColor,
                                        Command = $"warmode.ui.setrule {mode.Name} {rule.ID} {modeForCol} {ttype}"
                                    },
                                }
                            });
                            var valuetxt = Create(container, valuebtn, new CuiElement
                            {
                                Name = $"val.{modeForCol}.{rule.ID}.{ttype}",
                                Components =
                                {
                                    new CuiTextComponent
                                    {
                                        Text = value.ToString().TitleCase(),
                                        Align = UnityEngine.TextAnchor.MiddleCenter,
                                        Color = value ? poscolor : negcolor,
                                        FontSize = 10
                                    },
                                }
                            });
                            modeidx++;
                            localleft += modeColWidth + modeColGap;
                        }

                        localdown -= (ruleRowHeight + ruleGap);
                    }
                }

                //var header = Create(container, leftSide, new CuiElement
                //{
                //    Components =
                //    {
                //        new CuiRectTransformComponent
                //        {
                //            AnchorMin = "0 1",
                //            AnchorMax = "1 1",
                //            OffsetMin = $"{0} {-down-mh-padding-modeLabelH}",
                //            OffsetMax = $"{0} {-down-padding-modeLabelH}"
                //        },
                //        new CuiImageComponent
                //        {
                //            Color = ruleidx % 2 == 0 ? DARKEN : LIGHTEN
                //        }
                //    }
                //});
                //var text = Create(container, entry, new CuiElement
                //{
                //    Components =
                //    {
                //        new CuiTextComponent
                //        {
                //            Text = rule.Text,
                //            FontSize = 9,
                //            Color = generalTextColor,
                //            Font = "RobotoCondensed-Regular.ttf",
                //            Align = UnityEngine.TextAnchor.UpperLeft
                //        },
                //        new CuiRectTransformComponent
                //        {
                //            AnchorMin = "0 0",
                //            AnchorMax = "1 0.55",
                //            OffsetMin = $"{padding} {4}",
                //            OffsetMax = $"{-padding} {0}"
                //        },
                //    }
                //});
                //var title = Create(container, entry, new CuiElement
                //{
                //    Components =
                //    {
                //        new CuiTextComponent
                //        {
                //            Text = rule.Title,
                //            FontSize = 13,
                //            Color = generalTextColor,
                //            Align = UnityEngine.TextAnchor.UpperLeft
                //        },
                //        new CuiRectTransformComponent
                //        {
                //            AnchorMin = "0 0.5",
                //            AnchorMax = "1 1",
                //            OffsetMin = $"{padding} {0}",
                //            OffsetMax = $"{-padding} {-5}"
                //        },
                //    }
                //});

                //var r = 0f;
                //var colIdx = 0;
                //foreach(var modeVal in rule.Values)
                //{
                //    var modeForCol = allModeKeys.ElementAt(colIdx);
                //    var btn = Create(container, rightSide, new CuiElement
                //    {
                //        Components =
                //        {
                //            new CuiRectTransformComponent
                //            {
                //                AnchorMin = "0 1",
                //                AnchorMax = "0 1",
                //                OffsetMin = $"{r} {-down-mh-padding-modeLabelH-0.5f}",
                //                OffsetMax = $"{r+modeLabelW-1} {-down-padding-modeLabelH-0.5f}"
                //            },
                //            new CuiButtonComponent
                //            {
                //                Color = ruleidx % 2 == 0 ? DARKEN : LIGHTEN,
                //                Command = $"warmode.ui.setrule {mode.Name} {rule.ID} {modeForCol}"
                //                //Color = "0 0 0 0"
                //                //Color = "1 1 0 1",
                //            }
                //        }
                //    });
                //    var btnText = Create(container, btn, new CuiElement
                //    {
                //        Name = $"val.{modeForCol}.{rule.ID}",
                //        Components =
                //        {
                //            new CuiTextComponent
                //            {
                //                Text = modeVal.ToString(),
                //                FontSize = 12,
                //                Color = modeVal ? poscolor : negcolor,
                //                Align = UnityEngine.TextAnchor.MiddleCenter
                //            }
                //        }
                //    });
                //    r += modeLabelW;
                //    colIdx++;
                //}

                down += totalpanelh;
                ruleidx++;
            }
            CuiHelper.DestroyUi(editingPlayer, ID + "editleft");
            CuiHelper.DestroyUi(editingPlayer, ID + "editright");
            CuiHelper.AddUi(editingPlayer, container);
        }

        public void UpdateModalValidation(string message)
        {
            Update(new CuiElement
            {
                Parent = ID + "modalcontent",
                Name = ID + "modalvalidation",
                Update = true,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 0",
                        OffsetMin = $"{0} {24+4}",
                        OffsetMax = $"{0} {24+4+16}"
                    },
                    new CuiTextComponent
                    {
                        Color = negcolor,
                        Text = message,
                        FontSize = 10,
                        Align = TextAnchor.UpperCenter
                    }
                }
            });
        }

        public void ShowAddModeModal()
        {
            var container = new CuiElementContainer();
            var width = 350;
            var height = 400;
            var modal = Create(container, ID, new CuiElement
            {
                Name = ID + "addmode",
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0 0 0 0.95"
                    }
                }
            });

            var modalwindow = Create(container, modal, new CuiElement
            {
                Name = ID + "modalwindow",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = $"{-width/2} {-height/2}",
                        OffsetMax = $"{width/2} {height/2}"
                    },
                    new CuiImageComponent
                    {
                        Color = mainBgColor
                    }
                }
            });

            var modalheader = Create(container, modalwindow, new CuiElement
            {
                Name = ID + "modalheader",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"{0} {-headerHeight}",
                        OffsetMax = $"{0} {0}"
                    },
                    new CuiImageComponent
                    {
                        Color = headerBgColor
                    }
                }
            });

            var modalheadertxt = Create(container, modalheader, new CuiElement
            {
                Name = ID + "modalheadertxt",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{padding} {0}",
                        OffsetMax = $"{0} {0}"
                    },
                    new CuiTextComponent
                    {
                        Text = "ADDING NEW MODE",
                        Align = TextAnchor.MiddleLeft,
                        Color = headerTextColor
                    }
                }
            });

            var modalheadercancelbtn = Create(container, modalheader, new CuiElement
            {
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "1 0.5",
                        AnchorMax = "1 0.5",
                        OffsetMin = $"{-padding-12} {-6}",
                        OffsetMax = $"{-padding} {6}"
                    },
                    new CuiButtonComponent
                    {
                        Color = headerTextColor,
                        Sprite = "assets/icons/close.png",
                        Close = modal
                    }
                }
            });

            var modalcontent = Create(container, modalwindow, new CuiElement
            {
                Name = ID + "modalcontent",
                Components =
                {
                    //new CuiScrollViewComponent
                    //{
                    //    Horizontal = false,
                    //    Vertical = true,
                    //    VerticalScrollbar = new CuiScrollbar
                    //    {
                    //        Size = 4
                    //    },
                    //    ContentTransform = new CuiRectTransform
                    //    {
                    //        OffsetMin = "0 -1000",
                    //        OffsetMax = "0 0"
                    //    }
                    //},
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{padding} {padding+40}",
                        OffsetMax = $"{-padding} {-headerHeight}"
                    }
                }
            });

            var labelgap = 2;
            var labelts = 12;
            var labelalign = TextAnchor.LowerLeft;
            var fieldColor = headerBgColor;
            var fieldTextColor = headerTextColor;
            var labelh = 24;
            var fieldh = 24;
            var fieldw = 200;
            var down = 0;


            var fields = new[]
            {
                new
                {
                    Label = "Mode ID:",
                    Property = "name",
                    Value = addingMode.Name,
                    SubText = string.Empty
                },
                new
                {
                    Label = "Display Name:",
                    Property = "displayname",
                    Value = addingMode.DisplayName,
                    SubText = string.Empty
                },
                new
                {
                    Label = "Color:",
                    Property = "color",
                    Value = addingMode.ColorHex,
                    SubText = string.Empty
                },
                new
                {
                    Label = "Priority:",
                    Property = "priority",
                    Value = addingMode.Priority.ToString(),
                    SubText = string.Empty
                },
                new
                {
                    Label = "Permission Group:",
                    Property = "group",
                    Value = addingMode.Group,
                    SubText = "This group will be added on save if it does not exist."
                }
            };

            foreach (var field in fields)
            {
                var lblName = Create(container, modalcontent, new CuiElement
                {
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "1 1",
                            OffsetMin = $"{0} {-down-labelh}",
                            OffsetMax = $"{0} {-down}"
                        },
                        new CuiTextComponent
                        {
                            Text = field.Label,
                            FontSize = labelts,
                            Color = generalTextColor,
                            Align = labelalign
                        }
                    }
                });
                down += labelh + labelgap;
                var fieldName = Create(container, modalcontent, new CuiElement
                {
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "0 1",
                            OffsetMin = $"{0} {-down-fieldh}",
                            OffsetMax = $"{fieldw} {-down}"
                        },
                        new CuiImageComponent
                        {
                            Color = fieldColor
                        }
                    }
                });
                var fieldNameInp = Create(container, fieldName, new CuiElement
                {
                    Name = $"addmode.input.{field.Property}",
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                            OffsetMin = $"{padding} {0}",
                            OffsetMax = $"{-padding} {0}"
                        },
                        new CuiInputFieldComponent
                        {
                            Text = field.Value,
                            Align = TextAnchor.MiddleLeft,
                            CharsLimit = 24,
                            Color = fieldTextColor,
                            FontSize = labelts,
                            NeedsKeyboard = true,
                            LineType = InputField.LineType.SingleLine,
                            Command = $"warmode.ui.addmode.input {field.Property}"
                        }
                    }
                });
                if (field.Property == "color")
                {
                    UnityEngine.Color colorgb = UnityEngine.Color.white;
                    ColorUtility.TryParseHtmlString(field.Value, out colorgb);
                    var colorprev = Create(container, fieldName, new CuiElement
                    {
                        Components =
                        {
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "1 0.5",
                                AnchorMax = "1 0.5",
                                OffsetMin = $"{-20} {-8}",
                                OffsetMax = $"{-4} {8}"
                            },
                            new CuiImageComponent
                            {
                                Color = colorgb.ToColorString()
                            }
                        }
                    });
                }
                down += fieldh + labelgap;
                if (field.SubText != string.Empty)
                {
                    var subtext = Create(container, modalcontent, new CuiElement
                    {
                        Components =
                        {
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 1",
                                AnchorMax = "1 1",
                                OffsetMin = $"{0} {-down-16}",
                                OffsetMax = $"{0} {-down}"
                            },
                            new CuiTextComponent
                            {
                                Text = field.SubText,
                                FontSize = 8,
                                Color = generalTextColor,
                                Align = labelalign
                            }
                        }
                    });
                    down += 16 + labelgap;
                }

            }

            var validationtxt = Create(container, modalwindow, new CuiElement
            {
                Name = ID + "modalvalidation",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 0",
                        OffsetMin = $"{0} {24+labelgap}",
                        OffsetMax = $"{0} {24+labelgap+16}"
                    },
                    new CuiTextComponent
                    {
                        Color = negcolor,
                        Text = string.Empty,
                        FontSize = 10,
                        Align = TextAnchor.UpperCenter
                    }
                }
            });

            var confirmbtn = Create(container, modalwindow, new CuiElement
            {
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.5 0",
                        AnchorMax = "0.5 0",
                        OffsetMin = $"{-50} {padding}",
                        OffsetMax = $"{50} {padding+24}"
                    },
                    new CuiButtonComponent
                    {
                        Color = headerBgColor,
                        Command = "warmode.ui.addmode.confirm"
                    }
                }
            });

            var confirmbtntext = Create(container, confirmbtn, new CuiElement
            {
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "CONFIRM",
                        Color = headerTextColor,
                        Align = TextAnchor.MiddleCenter
                    }
                }
            });

            CuiHelper.DestroyUi(editingPlayer, modal);
            CuiHelper.AddUi(editingPlayer, container);
        }

    }
}

namespace Oxide.Plugins
{
    public partial class WarModeAdminPanel
    {
        public void ReloadSettingsPage()
        {
            var container = new CuiElementContainer();

            pageId = Create(container, mainPanel, new CuiElement
            {
                Name = ID + "page",
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{0} {0}",
                        OffsetMax = $"{0} {-headerHeight-navHeight-padding}"
                    }
                }
            });
            CuiHelper.DestroyUi(editingPlayer, pageId);
            CuiHelper.AddUi(editingPlayer, container);

            ShowSettingsList();
        }

        [Command("warmode.ui.setsetting")]
        private void CmdWarModeUiSetSetting(IPlayer player, string command, string[] args)
        {
            // warmode.ui.setsetting <property>
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null || basePlayer != editingPlayer) { return; }
            var property = args[0];
            var sett = GeneralSettings.First(x => x.Property == property);
            sett.OnToggle();
            if (!isDirty)
            {
                isDirty = true;
                CreateBottomButton();
            }
            ReloadSettingsPage();
        }

        public struct SettingDef
        {
            public string Label;
            public string Property;
            public Action OnToggle;
            public Func<bool> Value;
        }

        SettingDef[] GeneralSettings = new SettingDef[]
        {
            new SettingDef
            {
                Label = "Console Debugging",
                Property = "debug",
                Value = () => INSTANCE.ModifiedConfig.Settings.ShowDebugMessagesInConsole,
                OnToggle = () =>
                {
                    INSTANCE.ModifiedConfig.Settings.ShowDebugMessagesInConsole = !INSTANCE.ModifiedConfig.Settings.ShowDebugMessagesInConsole;
                }
            },
            new SettingDef
            {
                Label = "Vehicle Ownership",
                Property = "vehiclemodeownership",
                Value = () => INSTANCE.ModifiedConfig.Settings.AllowVehicleModeOwnership,
                OnToggle = () =>
                {
                    INSTANCE.ModifiedConfig.Settings.AllowVehicleModeOwnership = !INSTANCE.ModifiedConfig.Settings.AllowVehicleModeOwnership;
                }
            },
            new SettingDef
            {
                Label = "Allow Twig Damage",
                Property = "allowtwigdamage",
                Value = () => INSTANCE.ModifiedConfig.Settings.AlwaysAllowTwigDamage,
                OnToggle = () =>
                {
                    INSTANCE.ModifiedConfig.Settings.AlwaysAllowTwigDamage = !INSTANCE.ModifiedConfig.Settings.AlwaysAllowTwigDamage;
                }
            },
        };

        private void ShowSettingsList()
        {
            var gridid = ID + "settingslist";
            var container = new CuiElementContainer();
            var settingsList = Create(container, pageId, new CuiElement
            {
                Name = gridid,
                Components =
                {
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = $"{padding} {padding}",
                        OffsetMax = $"{-padding} {-padding}"
                    }
                }
            });

            var modes = ModifiedConfig.Modes;

            var fieldw = 60;
            var labelw = 120;
            var labelg = 8;
            var height = 24;
            var gap = 8;
            var down = 0f;
            foreach (var col in GeneralSettings)
            {
                var label = Create(container, gridid, new CuiElement
                {
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "0 1",
                            OffsetMin = $"{0} {-down-height}",
                            OffsetMax = $"{labelw} {-down}"
                        },
                        new CuiTextComponent
                        {
                            Text = col.Label + ":",
                            Align = TextAnchor.MiddleLeft,
                            FontSize = 12,
                            Color = generalTextColor
                        }
                    }
                });
                var btn = Create(container, gridid, new CuiElement
                {
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "0 1",
                            OffsetMin = $"{labelw+labelg} {-down-height}",
                            OffsetMax = $"{labelw+labelg+fieldw} {-down}"
                        },
                        new CuiButtonComponent
                        {
                            Color = headerBgColor,
                            Command = $"warmode.ui.setsetting {col.Property}"
                        }
                    }
                });
                var btntxt = Create(container, btn, new CuiElement
                {
                    Name = ID + "setting." + col.Property,
                    Components =
                    {
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        },
                        new CuiTextComponent
                        {
                            Text = col.Value.Invoke() ? "True" : "False",
                            Align = TextAnchor.MiddleCenter,
                            FontSize = 12,
                            Color = col.Value.Invoke() ? poscolor : negcolor
                        }
                    }
                });
                down += height + gap;
            }


            CuiHelper.DestroyUi(editingPlayer, gridid);
            CuiHelper.AddUi(editingPlayer, container);

        }

    }
}
