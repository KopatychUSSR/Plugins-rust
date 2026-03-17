using System;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using System.Globalization;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Help", "http://topplugin.ru/", "1.0.0")]
    class Help : RustPlugin
    {
        #region Variables
        [JsonProperty("Системный слой")] private string Layer = "UI_Help";
        #endregion

        #region Command
        [ChatCommand("help")]
        void cmdHelp(BasePlayer player, string command, string[] args) => HelpUI(player);

        [ChatCommand("info")]
        void cmdChatInfoPlayer(BasePlayer player, string command, string[] args) => InfoPlayerUI(player);

        [ChatCommand("newplayer")]
        void cmdChatInfoPlayers(BasePlayer player, string command, string[] args) => InfoNewPlayerUI(player);
        #endregion

        #region UI
        private void HelpUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0.8", Sprite = "Assets/Content/UI/UI.Background.Tile.psd", Material = "assets/content/ui/uibackgroundblur.mat", FadeIn = 0.5f },
                FadeOut = 0.4f
            }, "Overlay", Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.3 0.59", AnchorMax = $"0.7 0.67", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0" },
                Text = { Text = "Вы новый игрок в Rust?", Font = "robotocondensed-bold.ttf", FontSize = 40, Align = TextAnchor.MiddleCenter, FadeIn = 0.5f }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.3 0.54", AnchorMax = $"0.7 0.585", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", FadeIn = 1f },
                Text = { Text = "Мы понимаем, что нас посетят много новых игроков\nЕсли это относится и к вам, нажмите на одну из двух кнопок!", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#FFFFFF5A"), FadeIn = 0.5f },
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Close = Layer },
                Text = { Text = "" }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.344 0.335", AnchorMax = $"0.46 0.52", OffsetMax = "0 0" },
                Button = { Color = "0.16 0.71 0.39 1", Material = "assets/content/ui/uibackgroundblur.mat", Command = "chat.say /newplayer", Close = Layer, FadeIn = 0.5f },
                Text = { Text = "Да", Font = "robotocondensed-bold.ttf", FontSize = 55, Align = TextAnchor.MiddleCenter, FadeIn = 0.5f }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.54 0.335", AnchorMax = $"0.656 0.52", OffsetMax = "0 0" },
                Button = { Color = "0.72 0.24 0.24 1", Material = "assets/content/ui/uibackgroundblur.mat", Command = "chat.say /info", Close = Layer, FadeIn = 0.5f },
                Text = { Text = "Нет", Font = "robotocondensed-bold.ttf", FontSize = 55, Align = TextAnchor.MiddleCenter, FadeIn = 0.5f }
            }, Layer);

            CuiHelper.AddUi(player, container);
        }

        #region NewPlayer
        private void InfoNewPlayerUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0.8", Sprite = "Assets/Content/UI/UI.Background.Tile.psd", Material = "assets/content/ui/uibackgroundblur.mat", FadeIn = 0.5f },
                FadeOut = 0.4f
            }, "Overlay", Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0" },
                Text = { Text = "<b><size=22>С чего начать?</size></b>\nПервым делом Вам понадобится орудие для добычи дерева и камня, и к примеру лук для защиты. Изучите меню <b>''TAB''</b> с\nинвентарем. По мере добычи ресурсов в меню крафта <b>''Q''</b> Вы увидите разблокированные предметы для создания.\nРазбивая бочки Вам будут выпадать компоненты (используются для крафта) и скрап, который также можно потратить на\nИзучение предметов. Чем больше у вас рецептов - тем больше возможностей.\n\n<b><size=22>Что здесь делать?</size></b>\nМир открыт перед Вами, что делать - решаете Вы. Добыча ресурсов, крафт и коммуникация с другими обитателями\nпомогут вам выжить и обзавестись крепкими стенами.\n\n<b><size=22>Как построить дом?</size></b>\nДля строительства Вам понадобится план постройки, киянка и первоначальные ресурсы (дерево и камень). Для первого\nобустройства жилища вы можете получить набор <b>''Обустройство дома''</b>.(/kit). Не забудьте про <b>шкаф</b>!\n\n<b><size=22>Где мне найти сожителя?</size></b>\nВыжить в одиночку очень тяжело. Обзавестись знакомыми можно на ближайшем морском пляже, если вас не зарубят\nкамнем при первом диалоге, то у вас есть шансы! (<b>''V'' - голосовой чат</b>)\nТакже напарника можно найти в обсуждении нашей группы: <b>ГРУППА</b>\n\n<b><size=22>Куда мне обратиться за помощью?</size></b>\nДля получения помощи Вы можете обратиться в группе в вк сервера или ввести команду: <b>/menu</b> в чат.\nГруппа: <b>группа</b>\n\n<b><size=22>Приятной игры на проекте проект</size></b>", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#FFFFFF5A"), FadeIn = 0.5f }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Close = Layer },
                Text = { Text = "" }
            }, Layer);

            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region Player
        private void InfoPlayerUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0.8", Sprite = "Assets/Content/UI/UI.Background.Tile.psd", Material = "assets/content/ui/uibackgroundblur.mat", FadeIn = 0.5f },
                FadeOut = 0.4f
            }, "Overlay", Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0" },
                Text = { Text = "<b><size=22>Добро пожаловать на Проект, друг.</size></b>\n\n<b><size=22>ОСНОВНЫЕ КОМАНДЫ</size></b>\n/kit - открыть меню доступных наборов.\n/report ''ник игрока'' - совершить донос на нечестного работягу.\n/skin - текстильная мастерская.\n\n<b><size=22>ТЕЛЕПОРТАЦИЯ</size></b>\n/tpr ''ник игрока'' - отправить запрос на телепортацию к игроку.\n/tpa(/tpc) - принять/отклонить запрос на телепортацию.\n/sethome(/removehome) ''название'' - создать/удалить точку спавна.\n/home list - открыть список своих жилищ.\n/home ''название'' - телепортация на хату с указанным названием.\n\n<b><size=22>КООПЕРАТИВ</size></b>\n/team add ''имя игрока''\n/team tag ''название''\n\n<b><size=22>Подробнее на САЙТ.RU</size></b>", Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#FFFFFF5A"), FadeIn = 0.5f }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                Button = { Color = "0 0 0 0", Close = Layer },
                Text = { Text = "" }
            }, Layer);

            CuiHelper.AddUi(player, container);
        }
        #endregion

        #endregion

        #region Oxide
        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Layer);
            }
        }
        #endregion

        #region Helpers
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
        #endregion
    }
}