using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{

    [Info("FastMenu", "BeeRust", "1.0.0")]
    public class FastMenu : RustPlugin
    {

        void OnPlayerConnected(BasePlayer player)
        {
            UpdatePanel(player);
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "Menu");
            CuiHelper.DestroyUi(player, "Menu2");
            CuiHelper.DestroyUi(player, "Menu3");
            CuiHelper.DestroyUi(player, "Menu4");
        }



        void OnServerInitialized()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                UpdatePanel(player);
            }
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
            CuiHelper.DestroyUi(player, "Menu2");
            CuiHelper.DestroyUi(player, "Menu3");
            CuiHelper.DestroyUi(player, "Menu4");
            }
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null) return;

            CuiHelper.DestroyUi(player, "Menu2");
            CuiHelper.DestroyUi(player, "Menu3");
            CuiHelper.DestroyUi(player, "Menu4");
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null)
            {

            }
            UpdatePanel(player);

        }

        void UpdatePanel(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "Menu2");
            var container2 = new CuiElementContainer();

            container2.Add(new CuiButton
            {
                Button = { Color = "1 0.96 0.88 0.033", Material = "assets/icons/greyout.mat", Command = $"chat.say /remove" },
                Text = { Text = "<color=#ffb433> • </color>Удаление постройки", Align = TextAnchor.MiddleLeft, FontSize = 11 },
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "184.7 60.2", OffsetMax = "244.7 78.2" },
            },  "Overlay", "Menu2");
            CuiHelper.DestroyUi(player, "Menu3");
            container2.Add(new CuiButton
            {
                Button = { Color = "1 0.96 0.88 0.033", Material = "assets/icons/greyout.mat", Command = $"chat.say /fmenu" },
                Text = { Text = "<color=#ffb433> • </color>Друзья", Align = TextAnchor.MiddleLeft, FontSize = 11 },
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "184.7 38.8", OffsetMax = "244.7 57" },
            },  "Overlay", "Menu3");
            CuiHelper.DestroyUi(player, "Menu4");
            container2.Add(new CuiButton
            {
                Button = { Color = "1 0.96 0.88 0.033", Material = "assets/icons/greyout.mat", Command = $"chat.say /info" },
                Text = { Text = "<color=#ffb433> • </color>Помощь", Align = TextAnchor.MiddleLeft, FontSize = 11 },
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "184.7 17.8", OffsetMax = "244.7 35.8" },
            },  "Overlay", "Menu4");

            CuiHelper.AddUi(player, container2);
        }
        
    }
}