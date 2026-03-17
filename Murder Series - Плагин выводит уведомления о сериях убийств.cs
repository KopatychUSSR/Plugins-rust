using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Configuration;
using System;

namespace Oxide.Plugins
{
    [Info("Murder Series", "OxideBro", "0.0.2")]
      //  Слив плагинов server-rust by Apolo YouGame
    class MurderSeries : RustPlugin
    {
        #region Reference
        private Timer mytimer;
        #endregion

        #region Configuration

        private int TimerToClear;
        private string Font;
        private int FontSize;
        private int CountStrike;
        private bool EnabledUI;
        private int TimeOut;
        private string AnchorMin;
        private string AnchorMax;
        protected override void LoadDefaultConfig()
        {
            GetConfig(Config, "Время бездействия игрока после чего список убитых будет очищаться", out TimerToClear, 120);
            GetConfig(Config, "UI: Шрифт текста", out Font, "RobotoCondensed-Bold.ttf");
            GetConfig(Config, "UI: Anchor Min", out AnchorMin, "0.35 0.7854173");
            GetConfig(Config, "UI: Anchor Max", out AnchorMax, "0.65 0.9518235");
            GetConfig(Config, "UI: Тайм-аут одного сообщения (После будет удалено)", out TimeOut, 10);
            GetConfig(Config, "UI: Включить UI (Если отключено, сообщения будут выводиться в чат)", out EnabledUI, true);
            GetConfig(Config, "UI: Размер текста", out FontSize, 18);
            GetConfig(Config, "UI: Максимальное количество сообщений на экране", out CountStrike, 4);
            GetConfig(Config, "Время бездействия игрока после чего список убитых будет очищаться", out TimerToClear, 120);
            SaveConfig();
        }

        public static void GetConfig<T>(DynamicConfigFile config, string name, out T value, T defaultValue)
        {
            config[name] = value = config[name] == null ? defaultValue : (T)Convert.ChangeType(config[name], typeof(T));
        }
        #endregion

        #region Data
        public Dictionary<ulong, PlayerKills> PlayersKills = new Dictionary<ulong, PlayerKills>();
        private List<string> _notes = new List<string>();

        public class PlayerKills
        {
            public List<ulong> Players = new List<ulong>();
        }
        #endregion

        #region Oxide
        void OnPlayerInit(BasePlayer player)
        {
            if (!PlayersKills.ContainsKey(player.userID))
            {
                PlayersKills.Add(player.userID, new PlayerKills());
            }
        }

        void OnServerInitialized()
        {
            LoadDefaultConfig();
            lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerInit(player);
            }
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
      //  Слив плагинов server-rust by Apolo YouGame
        {
            if (entity == null || info == null) return;
            BasePlayer initiator = info.InitiatorPlayer;
            BasePlayer victim = entity.ToPlayer();
            if (IsNPC(initiator) || IsNPC(victim)) return;
            if (victim == null || initiator == null) return;
            if (!(info.InitiatorPlayer is BasePlayer)) return;
            PlayerKills PlayerKills;
            if (!PlayersKills.TryGetValue(initiator.userID, out PlayerKills))
            {
                PlayerKills = new PlayerKills();
                PlayersKills.Add(initiator.userID, new PlayerKills());
            }
            if (info.InitiatorPlayer is BasePlayer && !victim.IsSleeping())
            {
                if (PlayerKills.Players.Contains(victim.userID)) return;
                if (mytimer != null) timer.Destroy(ref mytimer);

                PlayerKills.Players.Add(victim.userID);
                if (PlayersKills[initiator.userID].Players.Count == 2)
                {
                    _notes.Add(Messages["doubleKill"].Replace("{NAME}", initiator.displayName));
                    AddNote(Messages["doubleKill"].Replace("{NAME}", initiator.displayName));
                }
                if (PlayersKills[initiator.userID].Players.Count == 3)
                {
                    _notes.Add(Messages["tripleKill"].Replace("{NAME}", initiator.displayName));
                    AddNote(Messages["tripleKill"].Replace("{NAME}", initiator.displayName));
                }
                if (PlayersKills[initiator.userID].Players.Count == 5)
                {
                    _notes.Add(Messages["fiveKill"].Replace("{NAME}", initiator.displayName));
                    AddNote(Messages["fiveKill"].Replace("{NAME}", initiator.displayName));
                }
                if (PlayersKills[initiator.userID].Players.Count == 10)
                {
                    _notes.Add(Messages["tenKill"].Replace("{NAME}", initiator.displayName));
                    AddNote(Messages["tenKill"].Replace("{NAME}", initiator.displayName));
                    PlayersKills[initiator.userID].Players.Clear();
                }
                mytimer = timer.Once(TimerToClear, () =>
                {
                    PlayersKills[initiator.userID].Players.Clear();
                });
            }
        }

        void Unload()
        {
            _notes.Clear();
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyUI(player);
            }
        }
        #endregion

        #region Library
        private bool IsNPC(BasePlayer player)
        {
            if (player == null) return false;
            if (player is NPCPlayer) return true;
            if (!(player.userID >= 76560000000000000L || player.userID <= 0L))
                return true;
            return false;
        }

        private void AddNote(string messages)
        {
            if (_notes.Count > CountStrike)
                _notes.RemoveRange((CountStrike - 1), _notes.Count - CountStrike);
            RefreshUI(messages);
            timer.Once(TimeOut, () =>
            {
                _notes.Remove(messages);
                RefreshUI(messages);
            });
        }
        #endregion

        #region UI
        private void DrawUI(BasePlayer player)
        {
            DestroyUI(player);
            var notes = _notes.Where(x => x.Contains(x)).Take(CountStrike);
            var container = new CuiElementContainer();
            var mainPanel = container.Add(new CuiPanel() { Image = { Color = "0 0 0 0" }, RectTransform = { AnchorMin = AnchorMin, AnchorMax = AnchorMax } }, "Hud", "ui.MurderSeries");
            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" }

            }, mainPanel);

            double index = 1;
            foreach (var note in notes)
            {
                var reply = 478;
                DrawText(container, note, $"0 {index - 0.2}", $"0.99 {index}");
                index -= 0.16;
            }
            CuiHelper.AddUi(player, container);
        }

        private string DrawText(CuiElementContainer container, string text, string anchorMin, string anchorMax)
        {
            string Name = CuiHelper.GetGuid();
            container.Add(new CuiElement
            {
                Name = Name,
                Parent = "ui.MurderSeries",
                Components =
                {
                    new CuiTextComponent { Align = UnityEngine.TextAnchor.MiddleCenter, FontSize = FontSize, Text = text, Font = Font },
                    new CuiRectTransformComponent { AnchorMin = anchorMin, AnchorMax = anchorMax },
                    new CuiOutlineComponent { Color = "0 0 0 1", Distance = "1.0 -0.5" }
                }
            });
            return Name;
        }

        private void RefreshUI(string messages)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (EnabledUI)
                {
                    DestroyUI(player);
                    DrawUI(player);
                }
                else
                    SendReply(player, messages);
            }
        }

        private void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "ui.MurderSeries");
        }
        #endregion

        #region LOCALIZATION

        Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            {"doubleKill", "Игрок <color=#88e892>{NAME}</color> совершил двойное убийство!" },
            {"tripleKill", "Игрок <color=#88e892>{NAME}</color> совершил тройное убийство!" },
            {"fiveKill", "Игрок <color=#88e892>{NAME}</color> совершил серию из 5 убийств!" },
            {"tenKill", "Игрок <color=#88e892>{NAME}</color> совершил серию из 10 убийств!" },
        };
        #endregion
    }
}
                     
