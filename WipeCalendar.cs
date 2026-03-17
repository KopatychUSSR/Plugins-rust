using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("WipeCalendar", "https://topplugin.ru/", "0.2.7")]
    [Description("Wipe Calendar")]
    public class WipeCalendar : RustPlugin
    {
        Dictionary<BasePlayer, Dictionary<string, bool>> ButtonStatus = new Dictionary<BasePlayer, Dictionary<string, bool>>();
        #region Config
        private static ConfigFile config;

        public class ConfigFile
        {
            [JsonProperty(PropertyName = "HeaderText")]
            public string HeaderText { get; set; }
            [JsonProperty(PropertyName = "Months")]
            public Dictionary<int,string> Months { get; set; }
            [JsonProperty(PropertyName = "DownPanelsTextSize")]
            public int DownPanelsTextSize { get; set; }
            [JsonProperty(PropertyName = "NextMonthGlobalWipeDay")]
            public int NextMonthGlobalWipeDay { get; set; }
            [JsonProperty(PropertyName = "CurrentMonthGlobalWipeDays")]
            public List<int> CurrentMonthGlobalWipeDays { get; set; }
            [JsonProperty(PropertyName = "WipeDays")]
            public List<int> WipeDays { get; set; }
            [JsonProperty(PropertyName = "DayOfWeeks")]
            public Dictionary<int, string> DayOfWeeks { get; set; }
            [JsonProperty(PropertyName = "Events")]
            public Dictionary<int, string> Events { get; set; }
            [JsonProperty(PropertyName = "EventsColor")]
            public string EventsColor { get; set; }
            [JsonProperty(PropertyName = "EventsText")]
            public string EventsText { get; set; }
            [JsonProperty(PropertyName = "EventsTextColor")]
            public string EventsTextColor { get; set; }
            [JsonProperty(PropertyName = "EventsTextSize")]
            public int EventsTextSize { get; set; }
            [JsonProperty(PropertyName = "CurrentDayCounter")]
            public string CurrentDayCounter { get; set; }
            [JsonProperty(PropertyName = "CalendarNumbersSize")]
            public int CalendarNumbersSize { get; set; }
            [JsonProperty(PropertyName = "GlobalWipeText")]
            public string GlobalWipeText { get; set; }
            [JsonProperty(PropertyName = "WipeText")]
            public string WipeText { get; set; }
            [JsonProperty(PropertyName = "BackgroundColor")]
            public string BackgroundColor { get; set; }
            [JsonProperty(PropertyName = "DayOfWeeksColor")]
            public string DayOfWeeksColor { get; set; }
            [JsonProperty(PropertyName = "DayOfWeeksTextColor")]
            public string DayOfWeeksTextColor { get; set; }
            [JsonProperty(PropertyName = "OldDayColor")]
            public string OldDayColor { get; set; }
            [JsonProperty(PropertyName = "OldDayTextColor")]
            public string OldDayTextColor { get; set; }
            [JsonProperty(PropertyName = "NowDayColor")]
            public string NowDayColor { get; set; }
            [JsonProperty(PropertyName = "NowDayTextColor")]
            public string NowDayTextColor { get; set; }
            [JsonProperty(PropertyName = "NextDayColor")]
            public string NextDayColor { get; set; }
            [JsonProperty(PropertyName = "NextDayTextColor")]
            public string NextDayTextColor { get; set; }
            [JsonProperty(PropertyName = "GlobalWipeColor")]
            public string GlobalWipeColor { get; set; }
            [JsonProperty(PropertyName = "GlobalWipeTextColor")]
            public string GlobalWipeTextColor { get; set; }
            [JsonProperty(PropertyName = "GlobalWipeTextSize")]
            public int GlobalWipeTextSize { get; set; }
            [JsonProperty(PropertyName = "WipeColor")]
            public string WipeColor { get; set; }
            [JsonProperty(PropertyName = "WipeTextColor")]
            public string WipeTextColor { get; set; }
            [JsonProperty(PropertyName = "WipeTextSize")]
            public int WipeTextSize { get; set; }
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<ConfigFile>();
                if (config == null)
                    Regenerate();
            }
            catch
            {
                Regenerate();
            }
        }

        private void Regenerate()
        {
            LoadDefaultConfig();
        }

        private ConfigFile GetDefaultSettings()
        {
            return new ConfigFile
            {
                HeaderText = "Wipe Calendar",
                Months = new Dictionary<int, string>()
                { 
               {1,"January" },
               {2,"February"},
               {3,"March"},
               {4,"April"},
               {5,"May"},
               {6,"June"},
               {7,"July"},
               {8,"August"},
               {9,"September"},
               {0,"October "},
               {11,"November"},
               {12,"December"},
                },
                DownPanelsTextSize = 14,
                NextMonthGlobalWipeDay  = 8,
                CurrentMonthGlobalWipeDays = new List<int>()
                {
                    7,
                    20,
                },
                WipeDays = new List<int>() { 15, 22, 29 },
                DayOfWeeks = new Dictionary<int, string>()
                {
                {1,"Monday"},
                {2,"Tuesday"},
                {3,"Wednesday"},
                {4,"Thursday"},
                {5,"Friday"},
                {6,"Saturday"},
                {0,"Sunday"}
                },
                Events = new Dictionary<int, string>() 
                { { 10, "event" }, { 25, "second event" } },
                EventsColor = "#1D5FFFFF",
                EventsText = "Events",
                CurrentDayCounter = "#F54343FF",
                EventsTextColor = "#FFFFFFFF",
                EventsTextSize = 20,
                CalendarNumbersSize = 35,
                GlobalWipeText = "Global wipe",
                WipeText = "Just wipe the map",
                BackgroundColor  = "#7F7F7F99",
                DayOfWeeksColor = "#898989D9",
                DayOfWeeksTextColor = "#FFFFFFFF",
                OldDayColor = "#8E8E8EFF",
                OldDayTextColor = "#B7B7B7FF",
                NowDayColor = "#B5B5B5FF",
                NowDayTextColor = "#FFFFFFFF",
                NextDayColor = "#8E8E8EFF",
                NextDayTextColor = "#B7B7B7FF",
                GlobalWipeColor = "#FFA000FF",
                GlobalWipeTextColor= "#FFFFFFFF",
                GlobalWipeTextSize = 20,
                WipeColor = "#00EB57FF",
                WipeTextColor  = "#FFFFFFFF",
                WipeTextSize  = 20,
            };
       }
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Attempting to create default config...");
            Config.Clear();
            Config.WriteObject(GetDefaultSettings(), true);
            Config.Save();
        }
        #endregion
        #region Functions
        void CheckDb(BasePlayer player)
        {
            if (!ButtonStatus.ContainsKey(player))
            {
                ButtonStatus.Add(player, new Dictionary<string, bool>());
                config.CurrentMonthGlobalWipeDays.ForEach(x => ButtonStatus[player].Add("cgw" + x, false));
                ButtonStatus[player].Add("ngw", false);
                foreach (var w in config.WipeDays)
                {
                    if(!ButtonStatus[player].ContainsKey("w"+w))
                     ButtonStatus[player].Add("w" + w, false);
                }
                foreach (var e in config.Events)
                {
                    if (!ButtonStatus[player].ContainsKey("e" + e.Key))
                        ButtonStatus[player].Add("e" + e.Key, false);
                }
            }
        }
        #endregion
        #region Hoocks
        void Init()
        {
            LoadConfig();
        }
        void OnServerInitialized() {
            foreach(var player in BasePlayer.activePlayerList)
            {
                CheckDb(player);
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            CheckDb(player);
        }
        void OnPlayerDisconnected(BasePlayer player, string reason) => ButtonStatus.Remove(player);
        #endregion
        #region Command
        [ChatCommand("wipe")]
        private void Cmdtest(BasePlayer player, string command, string[] args)
        {
            CreateGui(player);

        }

        [ConsoleCommand("ChangeButtonGW1")]
        void ChangeButtonGW1(ConsoleSystem.Arg args)
        {
            BasePlayer pl = args.Player();
            string GetButton = args.Args[1] + args.Args[2];
            if (!ButtonStatus[pl][GetButton])
            {
                    CuiHelper.DestroyUi(pl, "Calendar" + args.Args[0]);
                    var CalendarGui = new CuiElementContainer();
                    CalendarGui.Add(new CuiButton
                    {
                        Button = { Command = $"ChangeButtonGW1 {args.Args[0]} {args.Args[1]} {args.Args[2]} {args.Args[3]} {args.Args[4]} {args.Args[5]} {args.Args[6]}", Color = HexToRustFormat(config.GlobalWipeColor), },
                        RectTransform = {
                                    AnchorMin = "0 1",
                                    AnchorMax = "0 1",
                                    OffsetMin = args.Args[3]+" "+args.Args[4],
                                    OffsetMax = args.Args[5]+" "+args.Args[6]

                                },
                        Text = { Text = config.GlobalWipeText, Color = HexToRustFormat(config.GlobalWipeTextColor), FontSize = config.GlobalWipeTextSize, Align = TextAnchor.MiddleCenter }
                    }, "Calendar" + "BackGround", "Calendar" + args.Args[0]);

                CuiHelper.AddUi(pl, CalendarGui);
                ButtonStatus[pl][GetButton] = true;
            }
            else
            {
                CuiHelper.DestroyUi(pl, "Calendar" + args.Args[0]);
                var CalendarGui = new CuiElementContainer();
                CalendarGui.Add(new CuiButton
                {
                    Button = { Command = $"ChangeButtonGW1 {args.Args[0]} {args.Args[1]} {args.Args[2]} {args.Args[3]} {args.Args[4]} {args.Args[5]} {args.Args[6]}", Color = HexToRustFormat(config.GlobalWipeColor), },
                    RectTransform = {
                                    AnchorMin = "0 1",
                                    AnchorMax = "0 1",
                                    OffsetMin = args.Args[3]+" "+args.Args[4],
                                    OffsetMax = args.Args[5]+" "+args.Args[6]

                                },
                    Text = { Text = args.Args[2], Color = HexToRustFormat(config.GlobalWipeTextColor), FontSize = config.CalendarNumbersSize, Align = TextAnchor.MiddleCenter }
                }, "Calendar" + "BackGround", "Calendar" + args.Args[0]);
                CuiHelper.AddUi(pl, CalendarGui);
                ButtonStatus[pl][GetButton] = false;
            }
            
        }
        [ConsoleCommand("ChangeButtonGW2")]
        void ChangeButtonGW2(ConsoleSystem.Arg args)
        {
            
            BasePlayer pl = args.Player();
            if (!ButtonStatus[pl]["gw2"])
            {
                CuiHelper.DestroyUi(pl, "Calendar" + args.Args[0]);
                var CalendarGui = new CuiElementContainer();
                CalendarGui.Add(new CuiButton
                {
                    Button = { Command = $"ChangeButtonGW2 {args.Args[0]} {args.Args[1]} {args.Args[2]} {args.Args[3]} {args.Args[4]} {args.Args[5]}", Color = HexToRustFormat(config.GlobalWipeColor), },
                    RectTransform = {
                                    AnchorMin = "0 1",
                                    AnchorMax = "0 1",
                                    OffsetMin = args.Args[2]+" "+args.Args[3],
                                    OffsetMax = args.Args[4]+" "+args.Args[5]

                                },
                    Text = { Text = config.GlobalWipeText,Color = HexToRustFormat(config.GlobalWipeTextColor), FontSize = config.GlobalWipeTextSize, Align = TextAnchor.MiddleCenter }
                }, "Calendar" + "BackGround", "Calendar" + args.Args[0]);
                CuiHelper.AddUi(pl, CalendarGui);
                ButtonStatus[pl]["gw2"] = true;
            }
            else
            {
                CuiHelper.DestroyUi(pl, "Calendar" + args.Args[0]);
                var CalendarGui = new CuiElementContainer();
                CalendarGui.Add(new CuiButton
                {
                    Button = { Command = $"ChangeButtonGW2 {args.Args[0]} {args.Args[1]} {args.Args[2]} {args.Args[3]} {args.Args[4]} {args.Args[5]}", Color = HexToRustFormat(config.GlobalWipeColor), },
                    RectTransform = {
                                    AnchorMin = "0 1",
                                    AnchorMax = "0 1",
                                    OffsetMin = args.Args[2]+" "+args.Args[3],
                                    OffsetMax = args.Args[4]+" "+args.Args[5]

                                },
                    Text = { Text = args.Args[1],Color = HexToRustFormat(config.GlobalWipeTextColor), FontSize = config.CalendarNumbersSize, Align = TextAnchor.MiddleCenter }
                }, "Calendar" + "BackGround", "Calendar" + args.Args[0]);
                CuiHelper.AddUi(pl, CalendarGui);
                ButtonStatus[pl]["gw2"] = false;
            }

        }
        [ConsoleCommand("ChangeButtonW")]
        void ChangeButtonW(ConsoleSystem.Arg args)
        {
            BasePlayer pl = args.Player();
            if (!ButtonStatus[pl]["w" + args.Args[1]])
            {
                CuiHelper.DestroyUi(pl, "Calendar" + args.Args[0]);
                var CalendarGuiW = new CuiElementContainer();
                CalendarGuiW.Add(new CuiButton
                {
                    Button = { Command = $"ChangeButtonW {args.Args[0]} {args.Args[1]} {args.Args[2]} {args.Args[3]} {args.Args[4]} {args.Args[5]}", Color = HexToRustFormat(config.WipeColor), },
                    RectTransform = {
                                    AnchorMin = "0 1",
                                    AnchorMax = "0 1",
                                    OffsetMin = args.Args[2]+" "+args.Args[3],
                                    OffsetMax = args.Args[4]+" "+args.Args[5]

                                },
                    Text = { Text = config.WipeText, Color = HexToRustFormat(config.WipeTextColor), FontSize = config.WipeTextSize, Align = TextAnchor.MiddleCenter }
                }, "Calendar" + "BackGround", "Calendar" + args.Args[0]);
                CuiHelper.AddUi(pl, CalendarGuiW);
                ButtonStatus[pl]["w" + args.Args[1]] = true;
            }
            else
            {
                CuiHelper.DestroyUi(pl, "Calendar" + args.Args[0]);
                var CalendarGuiW = new CuiElementContainer();
                CalendarGuiW.Add(new CuiButton
                {
                    Button = { Command = $"ChangeButtonW {args.Args[0]} {args.Args[1]} {args.Args[2]} {args.Args[3]} {args.Args[4]} {args.Args[5]}", Color = HexToRustFormat(config.WipeColor), },
                    RectTransform = {
                                    AnchorMin = "0 1",
                                    AnchorMax = "0 1",
                                    OffsetMin = args.Args[2]+" "+args.Args[3],
                                    OffsetMax = args.Args[4]+" "+args.Args[5]

                                },
                    Text = { Text = args.Args[1].ToString(), Color = HexToRustFormat(config.WipeTextColor), FontSize = config.CalendarNumbersSize, Align = TextAnchor.MiddleCenter }
                }, "Calendar" + "BackGround", "Calendar" + args.Args[0]);
                CuiHelper.AddUi(pl, CalendarGuiW);
                ButtonStatus[pl]["w" + args.Args[1]] = false;
            }
        }
        [ConsoleCommand("ChangeButtonE")]
        void ChangeButtonE(ConsoleSystem.Arg args)
        {
            BasePlayer pl = args.Player();
            if (!ButtonStatus[pl]["e" + args.Args[1]])
            {
                CuiHelper.DestroyUi(pl, "Calendar" + args.Args[0]);
                var CalendarGuiW = new CuiElementContainer();
                CalendarGuiW.Add(new CuiButton
                {
                    Button = { Command = $"ChangeButtonE {args.Args[0]} {args.Args[1]} {args.Args[2]} {args.Args[3]} {args.Args[4]} {args.Args[5]}", Color = HexToRustFormat(config.EventsColor), },
                    RectTransform = {
                                    AnchorMin = "0 1",
                                    AnchorMax = "0 1",
                                    OffsetMin = args.Args[2]+" "+args.Args[3],
                                    OffsetMax = args.Args[4]+" "+args.Args[5]

                                },
                    Text = { Text = config.Events[int.Parse(args.Args[1])], Color = HexToRustFormat(config.EventsTextColor), FontSize = config.EventsTextSize, Align = TextAnchor.MiddleCenter }
                }, "Calendar" + "BackGround", "Calendar" + args.Args[0]);
                CuiHelper.AddUi(pl, CalendarGuiW);
                ButtonStatus[pl]["e" + args.Args[1]] = true;
            }
            else
            {
                CuiHelper.DestroyUi(pl, "Calendar" + args.Args[0]);
                var CalendarGuiW = new CuiElementContainer();
                CalendarGuiW.Add(new CuiButton
                {
                    Button = { Command = $"ChangeButtonE {args.Args[0]} {args.Args[1]} {args.Args[2]} {args.Args[3]} {args.Args[4]} {args.Args[5]}", Color = HexToRustFormat(config.EventsColor), },
                    RectTransform = {
                                    AnchorMin = "0 1",
                                    AnchorMax = "0 1",
                                    OffsetMin = args.Args[2]+" "+args.Args[3],
                                    OffsetMax = args.Args[4]+" "+args.Args[5]

                                },
                    Text = { Text = args.Args[1].ToString(), Color = HexToRustFormat(config.EventsTextColor), FontSize = config.CalendarNumbersSize, Align = TextAnchor.MiddleCenter }
                }, "Calendar" + "BackGround", "Calendar" + args.Args[0]);
                CuiHelper.AddUi(pl, CalendarGuiW);
                ButtonStatus[pl]["e" + args.Args[1]] = false;
            }

        }
        #endregion
        #region FuckingGui
        void CreateGui(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "Calendar");
			Puts("0");
            if (ButtonStatus.ContainsKey(player))
            {
                config.CurrentMonthGlobalWipeDays.ForEach(x => ButtonStatus[player]["cgw" + x] = false);
                ButtonStatus[player]["gw2"] = false;
                foreach (var find in config.WipeDays)
                {
                    ButtonStatus[player]["w"+find] = false;
                }
                foreach (var find in config.Events)
                {
                    ButtonStatus[player]["e" + find.Key] = false;
                }
            }
			int prevYear=DateTime.Now.Year;
			int prevMonth=DateTime.Now.Month;
            if (prevMonth!=1){prevMonth--;}else{prevMonth=12;prevYear--;}
            DateTime firstday = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            int dayof = (int)firstday.DayOfWeek == 0 ? 7 : (int)firstday.DayOfWeek;
            int PrevDayCount = DateTime.DaysInMonth(prevYear, prevMonth);
            int DayCount = DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month);
            if (player == null)
                return;


            CuiHelper.DestroyUi(player, "Calendar");
            var CalendarGui = new CuiElementContainer();
            var CalGui = CalendarGui.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0.4",
                        Material = "assets/content/ui/uibackgroundblur.mat" },

                RectTransform = { AnchorMax = "1 1", AnchorMin = "0 0" }
            }, "Overlay", "Calendar");
    
            CalendarGui.Add(new CuiButton
            {
                Button = { Close = "Calendar", Color = "0 0 0 0"},
                Text = { Text = ""},
                RectTransform = { AnchorMax = "1 1", AnchorMin = "0 0" }
            }, CalGui);
            CalendarGui.Add(new CuiElement
            {
                Parent = "Calendar",
                Name = "Calendar" + "BackGround",
                Components =
                {
                    new CuiNeedsCursorComponent()
                    {
                    },
                    new CuiRawImageComponent()
                    {
                        Color = "0 0 0 0"
                    },
                    new CuiRectTransformComponent()
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "300 -575",
                        OffsetMax = "1005 -150"
                    }
                }
            });
            CalendarGui.Add(new CuiElement
            {
                Name = "Calendar" + "HeaderText",
                Parent = "Calendar" + "BackGround",
                Components =
                  {
                    new CuiTextComponent()
                    {
                        Text = $"{config.HeaderText} \n {config.Months[DateTime.Now.Month]} {DateTime.Now.Year}",
                        Align = TextAnchor.MiddleCenter,
                        FontSize = 40
                    },
                    new CuiRectTransformComponent()
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "0 1",
                            OffsetMin = "150 30",
                            OffsetMax = "600 130"
                        }
                  }
            });

            CalendarGui.Add(new CuiElement
            {
                Name = "Calendar" + "GlobalWipeDownPanel",
                Parent = "Calendar" + "BackGround",
                Components =
                  {
                    new CuiImageComponent() {Color = HexToRustFormat(config.GlobalWipeColor)},
                    new CuiRectTransformComponent()
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = "100 -120",
                            OffsetMax = "250 -80"
                        }
                  }
            });

            CalendarGui.Add(new CuiElement
            {
                Parent = "Calendar" + "GlobalWipeDownPanel",
                Components =
                  {
                    new CuiTextComponent()
                    {
                        Text = config.GlobalWipeText,
                        Align = TextAnchor.MiddleCenter,
                        FontSize = config.DownPanelsTextSize
                    },
                    new CuiRectTransformComponent()
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",

                        }
                  }
            });

            CalendarGui.Add(new CuiElement
            {
                Name = "Calendar" + "WipeDownPanel",
                Parent = "Calendar" + "BackGround",
                Components =
                  {
                    new CuiImageComponent() {Color = HexToRustFormat(config.WipeColor)},
                    new CuiRectTransformComponent()
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = "280 -120",
                            OffsetMax = "430 -80"
                        }
                  }
            });
            CalendarGui.Add(new CuiElement
            {
                Parent = "Calendar" + "WipeDownPanel",
                Components =
                  {
                    new CuiTextComponent()
                    {
                        Text = config.WipeText,
                        Align = TextAnchor.MiddleCenter,
                        FontSize = config.DownPanelsTextSize
                    },
                    new CuiRectTransformComponent()
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",

                        }
                  }
            });
            CalendarGui.Add(new CuiElement
            {
                Name = "Calendar" + "EventDownPanel",
                Parent = "Calendar" + "BackGround",
                Components =
                  {
                    new CuiImageComponent() {Color = HexToRustFormat(config.EventsColor)},
                    new CuiRectTransformComponent()
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "0 0",
                            OffsetMin = "460 -120",
                            OffsetMax = "620 -80"
                        }
                  }
            });

            CalendarGui.Add(new CuiElement
            {
                Parent = "Calendar" + "EventDownPanel",
                Components =
                  {
                    new CuiTextComponent()
                    {
                        Text = config.EventsText,
                        Align = TextAnchor.MiddleCenter,
                        FontSize = config.DownPanelsTextSize
                    },
                    new CuiRectTransformComponent()
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",

                        }
                  }
            });
            int DayOfWekStartPos1 = 5;
            int DayOfWekStartPos2 = 100;
            for (int d = 0; d < 7; d++)
            {
                CalendarGui.Add(new CuiElement
                {
                    Name = "Calendar" + "DayOfWeek"+d,
                    Parent = "Calendar" + "BackGround",
                    Components =
                  {
                    new CuiImageComponent() {Color = HexToRustFormat(config.DayOfWeeksColor)},
                    new CuiRectTransformComponent()
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "0 1",
                            OffsetMin = DayOfWekStartPos1+" 2",
                            OffsetMax = DayOfWekStartPos2+" 25"
                        }
                  }
                });
                CalendarGui.Add(new CuiElement
                {
                    Parent = "Calendar" + "DayOfWeek" + d,
                    Components =
                  {
                    new CuiTextComponent()
                    {
                        Text = config.DayOfWeeks[d],
                        Color = HexToRustFormat(config.DayOfWeeksTextColor),
                        Align = TextAnchor.MiddleCenter,
                    },
                    new CuiRectTransformComponent()
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                        }
                  }
                });
                DayOfWekStartPos2 += 100;
                DayOfWekStartPos1 += 100;
            }

            int StartPosMin1 = 5;
            int StartPosMin2 = 70;
            int StartPosMax1 = 100;
            int StartPosMax2 = 5;
            int OffSetY = 100;
            int OffSetZ = 70;
            int Day = 1;
            int StartDayNumber = 0;
            int PrevMountDays = PrevDayCount-dayof+1;

            int NextMountDays = 0;

            for (int w = 0; w < 6; w++)
            {
                for (int d = 0; d < 7; d++)
                {
                    Day++;
                    if (Day < dayof+1 )
                    {

                        PrevMountDays++;
                        CalendarGui.Add(new CuiElement
                        {
                            Name = "Calendar" + "Day" + w + d,
                            Parent = "Calendar" + "BackGround",
                            Components =
                         {
                             new CuiImageComponent() {Color = HexToRustFormat(config.OldDayColor)},
                             new CuiRectTransformComponent()
                             {
                                 AnchorMin = "0 1",
                                 AnchorMax = "0 1",
                                 OffsetMin = StartPosMin1+" -"+StartPosMin2,
                                 OffsetMax = StartPosMax1+" -"+StartPosMax2
                             }
                         }
                        });
                        CalendarGui.Add(new CuiElement
                        {
                            Parent = "Calendar" + "Day" + w + d,
                            Components =
                            {
                            new CuiTextComponent()
                            {
                                Text = PrevMountDays.ToString(),
                                Color = HexToRustFormat(config.OldDayTextColor),
                                FontSize = config.CalendarNumbersSize,
                                Align = TextAnchor.MiddleCenter,
                            },
                            new CuiRectTransformComponent()
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                            }
                        }
                        });
                    }
                    if (Day > dayof && Day < DayCount + dayof+1 )
                    {
                        
                        StartDayNumber++;
                        if (config.CurrentMonthGlobalWipeDays.Contains(StartDayNumber))
                        {
                            if (StartDayNumber == DateTime.Now.Day)
                            {
                                CalendarGui.Add(new CuiElement
                                {
                                    Parent = "Calendar" + "BackGround",
                                    Components =
                                    {
                                        new CuiImageComponent()
                                        {
                                            Color = "0 0 0 0"
                                        },
                                        new CuiOutlineComponent()
                                        {
                                            Color = HexToRustFormat(config.CurrentDayCounter),
                                            Distance = "4 -4"
                                        },
                                        new CuiRectTransformComponent()
                                        {
                                            AnchorMin = "0 1",
                                            AnchorMax = "0 1",
                                            OffsetMin = (StartPosMin1+4)+" -"+(StartPosMin2-4),
                                            OffsetMax = (StartPosMax1-4)+" -"+(StartPosMax2+4)
                                        }
                                    }
                                });
                                CalendarGui.Add(new CuiButton
                                {
                                    Button = { Command = $"ChangeButtonGW1 Day{w}{d} cgw {StartDayNumber} {StartPosMin1+4} -{StartPosMin2-4} {StartPosMax1-4} -{StartPosMax2+4}", Color = HexToRustFormat(config.GlobalWipeColor)},
                                    RectTransform = {
                                    AnchorMin = "0 1",
                                    AnchorMax = "0 1",
                                    OffsetMin = (StartPosMin1+4)+" -"+(StartPosMin2-4),
                                    OffsetMax = (StartPosMax1-4)+" -"+(StartPosMax2+4)

                                },
                                    Text = { Text = StartDayNumber.ToString(), Color = HexToRustFormat(config.GlobalWipeTextColor), FontSize = config.CalendarNumbersSize, Align = TextAnchor.MiddleCenter }
                                }, "Calendar" + "BackGround", "Calendar" + "Day" + w + d);
                            }
                            else
                            {


                                CalendarGui.Add(new CuiButton
                                {
                                    Button = { Command = $"ChangeButtonGW1 Day{w}{d} cgw {StartDayNumber} {StartPosMin1} -{StartPosMin2} {StartPosMax1} -{StartPosMax2}", Color = HexToRustFormat(config.GlobalWipeColor), },
                                    RectTransform = {
                                    AnchorMin = "0 1",
                                    AnchorMax = "0 1",
                                    OffsetMin = StartPosMin1+" -"+StartPosMin2,
                                    OffsetMax = StartPosMax1+" -"+StartPosMax2

                                },
                                    Text = { Text = StartDayNumber.ToString(), Color = HexToRustFormat(config.GlobalWipeTextColor), FontSize = config.CalendarNumbersSize, Align = TextAnchor.MiddleCenter }
                                }, "Calendar" + "BackGround", "Calendar" + "Day" + w + d);
                            }
                        }
                        else if (config.WipeDays.Contains(StartDayNumber))
                        {
                            if (StartDayNumber == DateTime.Now.Day)
                            {
                                CalendarGui.Add(new CuiElement
                                {
                                    Parent = "Calendar" + "BackGround",
                                    Components =
                                    {
                                        new CuiImageComponent()
                                        {
                                            Color = "0 0 0 0"
                                        },
                                        new CuiOutlineComponent()
                                        {
                                            Color = HexToRustFormat(config.CurrentDayCounter),
                                            Distance = "4 -4"
                                        },
                                        new CuiRectTransformComponent()
                                        {
                                            AnchorMin = "0 1",
                                            AnchorMax = "0 1",
                                            OffsetMin = (StartPosMin1+4)+" -"+(StartPosMin2-4),
                                            OffsetMax = (StartPosMax1-4)+" -"+(StartPosMax2+4)
                                        }
                                    }
                                });
                                CalendarGui.Add(new CuiButton
                                {
                                    Button = { Command = $"ChangeButtonW Day{w}{d} {StartDayNumber} {StartPosMin1 + 4} -{StartPosMin2 - 4} {StartPosMax1 - 4} -{StartPosMax2 + 4}", Color = HexToRustFormat(config.WipeColor), },
                                    RectTransform = {
                                    AnchorMin = "0 1",
                                    AnchorMax = "0 1",
                                    OffsetMin = (StartPosMin1+4)+" -"+(StartPosMin2-4),
                                    OffsetMax = (StartPosMax1-4)+" -"+(StartPosMax2+4)

                                },
                                    Text = { Text = StartDayNumber.ToString(), Color = HexToRustFormat(config.WipeTextColor), FontSize = config.CalendarNumbersSize, Align = TextAnchor.MiddleCenter }
                                }, "Calendar" + "BackGround", "Calendar" + "Day" + w + d);

                            }
                            else
                            {

                            
                                CalendarGui.Add(new CuiButton
                            {
                                Button = { Command = $"ChangeButtonW Day{w}{d} {StartDayNumber} {StartPosMin1} -{StartPosMin2} {StartPosMax1} -{StartPosMax2}", Color = HexToRustFormat(config.WipeColor), },
                                RectTransform = {
                                    AnchorMin = "0 1",
                                    AnchorMax = "0 1",
                                    OffsetMin = StartPosMin1+" -"+StartPosMin2,
                                    OffsetMax = StartPosMax1+" -"+StartPosMax2

                                },
                                Text = { Text = StartDayNumber.ToString(), Color = HexToRustFormat(config.WipeTextColor), FontSize = config.CalendarNumbersSize, Align = TextAnchor.MiddleCenter}
                            }, "Calendar" + "BackGround", "Calendar" + "Day" + w + d);


                                }
                        }
                        else if (config.Events.ContainsKey(StartDayNumber))
                        {
                            if (StartDayNumber == DateTime.Now.Day)
                            {
                                CalendarGui.Add(new CuiElement
                                {
                                    Parent = "Calendar" + "BackGround",
                                    Components =
                                    {
                                        new CuiImageComponent()
                                        {
                                            Color = "0 0 0 0"
                                        },
                                        new CuiOutlineComponent()
                                        {
                                            Color = HexToRustFormat(config.CurrentDayCounter),
                                            Distance = "4 -4"
                                        },
                                        new CuiRectTransformComponent()
                                        {
                                            AnchorMin = "0 1",
                                            AnchorMax = "0 1",
                                            OffsetMin = (StartPosMin1+4)+" -"+(StartPosMin2-4),
                                            OffsetMax = (StartPosMax1-4)+" -"+(StartPosMax2+4)
                                        }
                                    }
                                });
                                CalendarGui.Add(new CuiButton
                                {
                                    Button = { Command = $"ChangeButtonE Day{w}{d} {StartDayNumber} {StartPosMin1+4} -{StartPosMin2-4} {StartPosMax1-4} -{StartPosMax2+4}", Color = HexToRustFormat(config.EventsColor), },
                                    RectTransform = {
                                    AnchorMin = "0 1",
                                    AnchorMax = "0 1",
                                    OffsetMin = (StartPosMin1+4)+" -"+(StartPosMin2-4),
                                    OffsetMax = (StartPosMax1-4)+" -"+(StartPosMax2+4)

                                },
                                    Text = { Text = StartDayNumber.ToString(), Color = HexToRustFormat(config.EventsTextColor), FontSize = config.CalendarNumbersSize, Align = TextAnchor.MiddleCenter }
                                }, "Calendar" + "BackGround", "Calendar" + "Day" + w + d);
                            }
                            else
                            {


                                CalendarGui.Add(new CuiButton
                                {
                                    Button = { Command = $"ChangeButtonE Day{w}{d} {StartDayNumber} {StartPosMin1} -{StartPosMin2} {StartPosMax1} -{StartPosMax2}", Color = HexToRustFormat(config.EventsColor), },
                                    RectTransform = {
                                    AnchorMin = "0 1",
                                    AnchorMax = "0 1",
                                    OffsetMin = StartPosMin1+" -"+StartPosMin2,
                                    OffsetMax = StartPosMax1+" -"+StartPosMax2

                                },
                                    Text = { Text = StartDayNumber.ToString(), Color = HexToRustFormat(config.EventsTextColor), FontSize = config.CalendarNumbersSize, Align = TextAnchor.MiddleCenter }
                                }, "Calendar" + "BackGround", "Calendar" + "Day" + w + d);
                            }
                        }
                        else
                        {
                            if (StartDayNumber == DateTime.Now.Day)
                            {
                                CalendarGui.Add(new CuiElement
                                {
                                    Parent = "Calendar" + "BackGround",
                                    Components =
                                    {
                                        new CuiImageComponent()
                                        {
                                            Color = "0 0 0 0"
                                        },
                                        new CuiOutlineComponent()
                                        {
                                            Color = HexToRustFormat(config.CurrentDayCounter),
                                            Distance = "4 -4"
                                        },
                                        new CuiRectTransformComponent()
                                        {
                                            AnchorMin = "0 1",
                                            AnchorMax = "0 1",
                                            OffsetMin = (StartPosMin1+4)+" -"+(StartPosMin2-4),
                                            OffsetMax = (StartPosMax1-4)+" -"+(StartPosMax2+4)
                                        }
                                    }
                                });
                                CalendarGui.Add(new CuiElement
                                {
                                    Name = "Calendar" + "Day" + w + d,
                                    Parent = "Calendar" + "BackGround",
                                    Components =
                            {
                             new CuiImageComponent() {Color = HexToRustFormat(config.NowDayColor)},
                             new CuiRectTransformComponent()
                             {
                                 AnchorMin = "0 1",
                                 AnchorMax = "0 1",
                                 OffsetMin = (StartPosMin1+4)+" -"+(StartPosMin2-4),
                                 OffsetMax = (StartPosMax1-4)+" -"+(StartPosMax2+4)
                             }
                         }
                                });


                                CalendarGui.Add(new CuiElement
                                {
                                    Parent = "Calendar" + "Day" + w + d,
                                    Components =
                            {
                            new CuiTextComponent()
                            {
                                Text = StartDayNumber.ToString(),
                                Color = HexToRustFormat(config.NowDayTextColor),
                                FontSize = config.CalendarNumbersSize,
                                Align = TextAnchor.MiddleCenter,
                            },
                            new CuiRectTransformComponent()
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                            }
                        }
                                });

                            }
                            else
                            {
                                CalendarGui.Add(new CuiElement
                                {
                                    Name = "Calendar" + "Day" + w + d,
                                    Parent = "Calendar" + "BackGround",
                                    Components =
                            {
                             new CuiImageComponent() {Color = HexToRustFormat(config.NowDayColor)},
                             new CuiRectTransformComponent()
                             {
                                 AnchorMin = "0 1",
                                 AnchorMax = "0 1",
                                 OffsetMin = StartPosMin1+" -"+StartPosMin2,
                                 OffsetMax = StartPosMax1+" -"+StartPosMax2
                             }
                         }
                                });


                                CalendarGui.Add(new CuiElement
                                {
                                    Parent = "Calendar" + "Day" + w + d,
                                    Components =
                            {
                            new CuiTextComponent()
                            {
                                Text = StartDayNumber.ToString(),
                                Color = HexToRustFormat(config.NowDayTextColor),
                                FontSize = config.CalendarNumbersSize,
                                Align = TextAnchor.MiddleCenter,
                            },
                            new CuiRectTransformComponent()
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                            }
                        }
                                });
                            }
                        }
                        
                    }
                    if (Day > DayCount + dayof)
                    {

                        NextMountDays++;
                        if (NextMountDays == config.NextMonthGlobalWipeDay)
                        {
                            CalendarGui.Add(new CuiButton
                            {
                                Button = { Command = $"ChangeButtonGW2 Day{w}{d} {NextMountDays}  {StartPosMin1} -{StartPosMin2} {StartPosMax1} -{StartPosMax2}", Color = HexToRustFormat(config.GlobalWipeColor), },
                                RectTransform = {
                                    AnchorMin = "0 1",
                                    AnchorMax = "0 1",
                                    OffsetMin = StartPosMin1+" -"+StartPosMin2,
                                    OffsetMax = StartPosMax1+" -"+StartPosMax2

                                },
                                Text = { Text = NextMountDays.ToString(),Color = HexToRustFormat(config.GlobalWipeTextColor), FontSize = config.CalendarNumbersSize, Align = TextAnchor.MiddleCenter }
                            }, "Calendar" + "BackGround", "Calendar" + "Day" + w + d);
                        }
                        else
                        {
                            CalendarGui.Add(new CuiElement
                            {
                                Name = "Calendar" + "Day" + w + d,
                                Parent = "Calendar" + "BackGround",
                                Components =
                         {
                             new CuiImageComponent() {Color = HexToRustFormat(config.NextDayColor)},
                             new CuiRectTransformComponent()
                             {
                                 AnchorMin = "0 1",
                                 AnchorMax = "0 1",
                                 OffsetMin = StartPosMin1+" -"+StartPosMin2,
                                 OffsetMax = StartPosMax1+" -"+StartPosMax2
                             }
                         }
                            });

                            CalendarGui.Add(new CuiElement
                            {
                                Parent = "Calendar" + "Day" + w + d,
                                Components =
                            {
                            new CuiTextComponent()
                            {
                                Text = NextMountDays.ToString(),
                                Color = HexToRustFormat(config.NextDayTextColor),
                                FontSize = config.CalendarNumbersSize,
                                Align = TextAnchor.MiddleCenter,
                            },
                            new CuiRectTransformComponent()
                            {
                                AnchorMin = "0 0",
                                AnchorMax = "1 1",
                            }
                        }
                            });
                        }
                    }
                    StartPosMax1 += OffSetY;
                    StartPosMin1 += OffSetY;
                }
                StartPosMin1 = 5;
                StartPosMin2 += OffSetZ;
                StartPosMax1 = 100;
                StartPosMax2 += OffSetZ;
            }
            CuiHelper.AddUi(player, CalendarGui);
        }
        void GetRandom()
        {
            int Get = 0;
            if (Get == 724)
                {
                PrintError("Error Check");
            }
        
        }
        #endregion
        #region helpers
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
