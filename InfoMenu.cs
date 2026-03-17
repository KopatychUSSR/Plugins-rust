using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("InfoMenu", "Я и Я", "1.0.3")]
    public class InfoMenu : RustPlugin
    {
        #region Fields
        
        private const string DataFileName = "Temporary/InfoMenu/data";
        private HashSet<ulong> _firstShowedPlayer;
        public string Layer = "UI.Layer";
        public string LayerBlur = "UI.LayerBlur";

        #endregion 

        #region Methods

        private static string HexToRGB(string hex)
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
            }

            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
            var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

            Color color = new Color32(r, g, b, a);
            return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
        }

        #endregion

        #region Hooks

        void Loaded()
        {
            try
            {
                _firstShowedPlayer = Interface.Oxide.DataFileSystem.ReadObject<HashSet<ulong>>(DataFileName);
            }
            catch (Exception ex)
            {
                PrintError($"Failed to read data: {ex}");
            }
            
            if (_firstShowedPlayer == null)
                _firstShowedPlayer = new HashSet<ulong>();
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (_firstShowedPlayer.Add(player.userID))
            {
                player.SendConsoleCommand("chat.say /info");
            }
        }
        
        void Unload()
        { 
            Interface.Oxide.DataFileSystem.WriteObject(DataFileName, _firstShowedPlayer);
            
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Layer);
                CuiHelper.DestroyUi(player, LayerBlur);
            }
        }

        #endregion

        #region Commands

        [ConsoleCommand("UI_Info")]
        void chatCmdInfo(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;

            if (player == null) return; 

            switch (arg.GetString(0))
            {
                case "opencategory":
                    
                    var name = "";

                    for (var i = 1; i < arg.Args.Length; i++)
                    {
                        if (string.IsNullOrEmpty(name))
                            name += arg.GetString(i);
                        else name += $" {arg.GetString(i)}"; 
                    } 
                    
                    CuiElementContainer container = new CuiElementContainer();

                    var category = _config.categories.Find(x => x.name.Equals(name));
                    
                    container.Add(new CuiLabel()  
                        {  
                            RectTransform = { AnchorMin = "0.2057292 0.1009259", AnchorMax = "0.75625 0.912037"}, 
                            Text = { Text = $"<size=28>{category.name.ToUpper()}</size>\n{category.text}", FontSize = 24, Align = TextAnchor.MiddleCenter, Font = "RobotoCondensed-Bold.ttf"}
                        }, Layer, Layer + ".Text");
                     
                    CuiHelper.DestroyUi(player, Layer + ".Text");

                    CuiHelper.AddUi(player, container);

                    break; 
            }
        }

        [ChatCommand("help")]
        void chatCmdHelp(BasePlayer player, string command, string[] args)
        {
            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.DestroyUi(player, LayerBlur);
            
            player.SendConsoleCommand("chat.say /info");
            player.SendConsoleCommand("UI_Info opencategory Команды сервера");
        }

        [ChatCommand("info")] 
        void chatCmdInfo(BasePlayer player, string command, string[] args)
        {
            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.DestroyUi(player, LayerBlur);

            CuiElementContainer container = new CuiElementContainer();
            
            var itemHeight = 0.9907407f - 0.9407406f;
            var itemWidth = 0.5525988f - 0.4171875f;
            var itemMargin = 0.417188f - 0.378643f;
            var countItem = _config.categories.Count;
            var itemMinHeight = 0.940741f;

            var itemMin = 0f;
 
            if (countItem < 5)
            {
                itemMin = 0.5f - countItem / 2f * itemWidth - (countItem - 1) / 2f * itemMargin;
            } else itemMin = 0.5f - 5 / 2f * itemWidth - (5 - 1) / 2f * itemMargin; 
            
            container.Add(new CuiPanel()
            {
                CursorEnabled = true,
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0"},
                Image = {Color = HexToRGB("#202020D8"), Material = "assets/content/ui/uibackgroundblur.mat"}
            }, "Overlay", Layer);
 
            container.Add(new CuiLabel() 
            {  
                RectTransform = { AnchorMin = "0.2057292 0.1009259", AnchorMax = "0.75625 0.912037"}, 
                Text = { Text = $"ДОБРО ПОЖАЛОВАТЬ, {player.displayName.ToUpper()}.\nТы находишься в меню информации. Для получения нужной информации щёлкни по одной из категорий! \n C УВАЖЕНИЕМ КОМАНДА TOP PLUGIN!", FontSize = 24, Align = TextAnchor.MiddleCenter, Font = "RobotoCondensed-Bold.ttf"}
            }, Layer, Layer + ".Text");
  
            container.Add(new CuiButton() 
            { 
                RectTransform = { AnchorMax = $"0.9934375 0.990741", AnchorMin = $"0.9613543 0.940741"},
                Button = { Command = "", Color = HexToRGB("#D620206E"), Close = Layer},
                Text = { Text = "✘", FontSize = 16, Font = "RobotoCondensed-Bold.ttf", Align = TextAnchor.MiddleCenter},   
            }, Layer);


            var count = 0; 
            foreach (var category in _config.categories)
            {
                count++; 
                
                container.Add(new CuiButton()
                {
                   RectTransform = { AnchorMin = $"{itemMin} {itemMinHeight}", AnchorMax = $"{itemMin + itemWidth} {itemMinHeight + itemHeight}"}, 
                    Button = { Color = "0.9686275 0.9215686 0.8823529 0.03529412", Command = $"UI_Info opencategory {category.name}"},  
                    Text = { Text = category.name.ToUpper(), Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "RobotoCondensed-Bold.ttf"} 
                }, Layer);

                itemMin += (itemWidth + itemMargin);

                if (count % 5 == 0)
                {
                    countItem -= 5;
                    
                    if (countItem <= 5)
                    {
                        itemMin = 0.5f - countItem / 2f * itemWidth - (countItem - 1) / 2f * itemMargin;
                    } else itemMin = 0.5f - 6 / 2f * itemWidth - (6 - 1) / 2f * itemMargin;
                    
                    itemMinHeight = 0.0092593f; 
                }
            }

            CuiHelper.AddUi(player, container); 
        }

        #endregion

        #region Config

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration()
            {
                categories = new List<CategoryInfo>()
                {
                    new CategoryInfo()
                    { 
                        name = "Капсулы",
                        text = "" 
                    },
                    new CategoryInfo()
                    {
                        name = "Новый переработчик",
                        text = ""
                    },
                    new CategoryInfo()
                    {
                        name = "Рюкзаки", 
                        text = ""
                    },
                    new CategoryInfo()
                    {
                        name = "Телефон",
                        text = ""
                    }, 
                    new CategoryInfo()
                    {
                        name = "Ивенты",
                        text = ""
                    },
                    new CategoryInfo() 
                    { 
                        name = "Миникарта",
                        text = "" 
                    }, 
                    new CategoryInfo()
                    {
                        name = "авторизация",
                        text = ""
                    }, 
                    new CategoryInfo()
                    {
                        name = "Команды сервера", 
                        text = "" 
                    },
                    new CategoryInfo()
                    {
                        name = "Бинды",
                        text = ""
                    },  
                }
            };

        }

        public Configuration _config;

        public class Configuration
        {
            public List<CategoryInfo> categories = new List<CategoryInfo>();
        }

        public class CategoryInfo
        {
            [JsonProperty("Название")]
            public string name = "";
            
            [JsonProperty("Текст")] 
            public string text = "";

        }

        #endregion

    }
}