using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using UnityEngine.UI;


namespace Oxide.Plugins
{
    [Info("Online", "UmkaRust", "0.0.2")]
    public class online : RustPlugin
    {

        #region Cfg
            public class DataConfig
            {
                [JsonProperty("На сколько накрутить визуальный онлайн (если вы не хотите накручивать вставьте 0)")]
                public int onlineplus;

                [JsonProperty("На сколько накрутить визуальные слоты (если вы не хотите накручивать вставьте 0)")]
                public int slotplus;  

                [JsonProperty("Размер шрифта (когда вы ставите слишком большой, текст не будет видно)")]
                public int fontsizee; 
            }
        #endregion

            #region Cfg2
            public DataConfig cfg;
            protected override void LoadConfig()
            {
                base.LoadConfig();
                cfg = Config.ReadObject<DataConfig>();
            }

            protected override void SaveConfig()
            {
                Config.WriteObject(cfg);
            }

            protected override void LoadDefaultConfig()
            {
                cfg = new DataConfig()
                {
                    onlineplus = 0,
                    slotplus = 0,
                    fontsizee = 13
                };
            }
        #endregion

        public int OP;
        public int maxp = ConVar.Server.maxplayers;
        public Timer Timer;

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "FirstPage");
            }
            Timer.Destroy();
        }

        private void OnServerInitialized()
        {           
            Timer = timer.Every(3f, () =>
             {
                OP = BasePlayer.activePlayerList.Count;
                foreach (var player in BasePlayer.activePlayerList)
                {
                    DrawINF(player);
                }
            });
        }


        public void DrawINF(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "FirstPage");
            string Layer = "FirstPage";
            CuiElementContainer Container = new CuiElementContainer();

            Container.Add(new CuiPanel
            {
                CursorEnabled = false,
                RectTransform = { AnchorMin = "0.0075 0.9725", AnchorMax = "0.0993 0.9925", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" },
            }, "Overlay", Layer);


            Container.Add(new CuiElement
            {
                Parent = Layer,
                Components = 
                {
                    new CuiTextComponent() { Text = $"ОНЛАЙН: <color=#82B57A>{OP + cfg.onlineplus}/{maxp + cfg.slotplus}</color>", FontSize = cfg.fontsizee, Align = TextAnchor.UpperLeft, Font = "RobotoCondensed-Bold.ttf" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });


            CuiHelper.AddUi(player, Container);
        }
    }
}

