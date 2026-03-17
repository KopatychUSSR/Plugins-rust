using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NameBonus", "https://topplugin.ru/", "0.0.4")]
    public class NameBonus : RustPlugin
    {
        [PluginReference] private Plugin ImageLibrary;
        
        public List<ulong> NameRewards = new List<ulong>();
        
        

        private PluginConfig config;
        void OnServerInitialized()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("NameBonus/PlayerList"))
            { 
                NameRewards = Interface.Oxide.DataFileSystem.ReadObject<List<ulong>>("NameBonus/PlayerList");
            }
            else
            {
                NameRewards = new List<ulong>();
            }

            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
            timer.Every(30, SaveData);
        }
        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("NameBonus/PlayerList", NameRewards);
        } 
        void Unload()
        {
            Interface.Oxide.DataFileSystem.WriteObject("NameBonus/PlayerList", NameRewards);
        }
        void OnServerSave()
        {
            Interface.Oxide.DataFileSystem.WriteObject("NameBonus/PlayerList", NameRewards);
        }
        class PluginConfig
        {
            [JsonProperty("Текст, который должен быть в нике")]
            public string Text;
            [JsonProperty("Оповещение(true = в уи меню, false - текстом")]
            public bool Notifer;
            [JsonProperty("Сколько денег будет выдано за награду")]
            public int PriceForOne;
            [JsonProperty("Номер магазина!")] 
            public string ShopID = "19579";
            [JsonProperty("Секретный ключ")]
            public string APIKey = "8539aa63271a814674f64751c56fd71e";
            [JsonProperty("Какой магазин юзаем(true = gamestores, false = moscow.ovh)")]
            public bool gamestores = true;
            [JsonProperty("Сколько времени должно пройти, что бы игрок получил награду(В СЕКУНДАХ)")]
            public int timerGive;
        }
        
        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig()
            {
                Text = "тачдаун",
                Notifer = true,
                PriceForOne = 60,
                timerGive = 120
            };
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
            
        }
        private void OnPlayerConnected(BasePlayer player)
        {
            if (NameRewards.Contains(player.userID))
                return; 
            OnUserConnected(player);
        }

        void TimerGive(BasePlayer player)
        {
            timer.Once(config.timerGive, () => 
            { 
                if (config.gamestores)
                    {
                        MoneyPlus(player.userID, config.PriceForOne);
                    }
                    else
                    {
                        APIChangeUserBalance(player.userID, config.PriceForOne, null);
                    }
                if (config.Notifer)
                {
                    GiveMoney(player);
                }
                else
                {
                    SendReply(player, $"Поздравляю, вы получили {config.PriceForOne} рублей на игровой магазин за приставку к нику!");
                }
                NameRewards.Add(player.userID);
            });
        }
        void OnUserConnected(BasePlayer player)
        {
            Regex checker = new Regex(config.Text, RegexOptions.IgnoreCase); 
            if (checker.IsMatch(player.displayName))
            {
                if (player.IsConnected)
                    TimerGive(player);
            }
        }
        void MoneyPlus(ulong userId, int amount)
        {
            ExecuteApiRequest(new Dictionary<string, string>()
            {
                { "action", "moneys" },
                { "type", "plus" },
                { "steam_id", userId.ToString() },
                { "amount", amount.ToString() }
            });
        }
        void ExecuteApiRequest(Dictionary<string, string> args)
        {
            string url = $"http://gamestores.ru/api?shop_id={config.ShopID}&secret={config.APIKey}" +
                         $"{string.Join("", args.Select(arg => $"&{arg.Key}={arg.Value}").ToArray())}";
            webrequest.EnqueueGet(url, (i, s) =>
            {
                if (i != 200)
                {
                    LogToFile("NameBonus", $"Код ошибки: {i}, подробности:\n{s}", this);
                }
                else
                {
                    if (s.Contains("fail"))
                    {
                        return;
                    }
                }
            }, this);
        }
        void APIChangeUserBalance(ulong steam, int balanceChange, Action<string> callback)
        {
            plugins.Find("RustStore").CallHook("APIChangeUserBalance", steam, balanceChange, new Action<string>((result) =>
            {
                if (result == "SUCCESS")
                {
                    Interface.Oxide.LogDebug($"Баланс пользователя {steam} увеличен на {balanceChange}");
                    return;
                }
                Interface.Oxide.LogDebug($"Баланс не был изменен, ошибка: {result}");
            }));
        }
        
        public string NameBonus1 = "UI_NameBonus";


        void GiveMoney(BasePlayer player)
        {
            var container = new CuiElementContainer();
            var Panel = container.Add(new CuiPanel
            {
                Image = {Color = HexToCuiColor("#FFFFFF00")},
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                CursorEnabled = true,
            }, "Overlay", NameBonus1);
            
            container.Add(new CuiElement
            {
                Parent = NameBonus1,
                Components =
                {
                    new CuiImageComponent {FadeIn = 0.25f, Color =  "0.2745098 0.2745098 0.2745098 0.7921569"},
                    new CuiRectTransformComponent {AnchorMin = "0.2979168 0.7250001", AnchorMax = "0.6583335 0.9555556"}
                }
            });
            container.Add(new CuiElement
            {
                Parent = NameBonus1,
                Components = {
                    new CuiTextComponent() { Color = "1 1 1 0.4420246", FadeIn = 0.25f, Text = $"ПОЗДРАВЛЯЮ, ВАМ ВЫДАНО {config.PriceForOne} РУБЛЕЙ НА ИГРОВОЙ МАГАЗИН ЗА ПРИСТАВКУ К НИКУ!", FontSize = 22, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf"  },
                    new CuiRectTransformComponent { AnchorMin = "0.4145833 0.7287037", AnchorMax = "0.6578125 0.9527777" },
                }
            });
    
            container.Add(new CuiElement
            {
                Parent = NameBonus1,
                Components =
                {
                    new CuiRawImageComponent { Png = (string) ImageLibrary.Call("GetImage", player.UserIDString) },
                    new CuiRectTransformComponent { AnchorMin = "0.2968749 0.7240741", AnchorMax = "0.4130208 0.9583333", OffsetMax = "0 0" }
                }
            });
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.6458334 0.724999", AnchorMax = "0.6578125 0.7453704" },
                Button = { Close = NameBonus1, Color = "0.1575961 0.1575961 0.1575961 0.5372549"},
                Text = { Text = "X", Color = "1 1 1 0.4420246",FontSize = 14, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" },
            }, NameBonus1);
			
			CuiHelper.AddUi(player, container);
			
		
        }
        
        private static string HexToCuiColor(string hex)
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

            return $"{color.r:F2} {color.g:F2} {color.b:F2} {color.a:F2}";
        }
        
    }
}