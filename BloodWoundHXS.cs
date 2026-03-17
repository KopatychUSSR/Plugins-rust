using System;
using UnityEngine;
using System.Globalization;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;
using Color = UnityEngine.Color;
using System.Collections.Generic;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("BloodWoundHXS", "https://topplugin.ru/", "1.0.0")]
    [Description("BloodWoundHXS для вашего сервера RUST.")]
    class BloodWoundHXS : RustPlugin
    {
        #region |Переменные|
        private string bleeding = "assets/icons/bleeding.png";
        private string Layer = "Box";
        private string coords1 = "0.6338536 0.02592593";
        private string coords2 = "0.6807286 0.1083333";
        #endregion
        
        #region |Интерфейс|
        void DrawBlood(BasePlayer player)
        {
            CuiElementContainer Gui = new CuiElementContainer();
            Gui.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image =
                {
                    Color = "0 0 0 0"
                },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1",
                }
            }, "Overlay", Layer);
            Gui.Add(new CuiButton
            {
                Button =
                {
                    Command = "chat.say /wakeup",
                    Color = HexToRustFormat("#FF4A4AFF"),
                    Sprite = bleeding
                },
                Text =
                {
                    Text = ""
                },
                RectTransform =
                {
                    AnchorMin = coords1,
                    AnchorMax = coords2,
                }
            }, Layer, "BloodButton");
            CuiHelper.AddUi(player, Gui);
        }
        #endregion
        
        #region |Команды|
        [ChatCommand("wakeup")]
        void WakeUPCMD(BasePlayer player)
        {
            if (player.IsWounded())
            {
                player.inventory.Take(null, 1776460938, 1);
                player.StopWounded();
                SendReply(player, Message("useblood"));
                CuiHelper.DestroyUi(player, Layer);
            }
            else
            {
                SendReply(player, Message("noblood"));
            }
        }
        [ChatCommand("blood")]
        void Blood(BasePlayer player)
        {
            if (config.BloodWound.UseCraft)
            {
                int meat = player.inventory.GetAmount(config.BloodWound.ItemCraft);
                if (meat >= 40)
                {
                    var blood = ItemManager.CreateByItemID(1776460938, 1, 0);
                    player.inventory.Take(null, config.BloodWound.ItemCraft, config.BloodWound.ItemCraftAmmount);
                    SendReply(player, Message("craft"));
                    player.GiveItem(blood);
                }
                else
                {
                    SendReply(player, Message("nocraft"));
                }
            }
        }
        #endregion
            
        #region |Хуки|
            private void OnPlayerWound(BasePlayer player)
            {
                if (config.BloodWound.UseBlood)
                {
                    int blood = player.inventory.GetAmount(1776460938);
                    if (blood >= 1) DrawBlood(player);
                }
            }

            object OnPlayerRecover(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, Layer);
                return null;
            }

            object OnPlayerDie(BasePlayer player, HitInfo info)
            {
                CuiHelper.DestroyUi(player, Layer);
                return null;
            }

            void OnServerInitialized(BasePlayer player) 
            {
                PrintError("*******************************************");
                PrintError("");
                PrintError("*** Author: hxs | *** Discord: hxs#4372 ***");
                PrintError("");
                PrintError("*******************************************");
            }

            void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
            {
                if (dispenser.gatherType == ResourceDispenser.GatherType.Flesh)
                {
                    if (Random.Range(0, 100) <= 4)
                    {
                        if (dispenser.GetComponent<BaseEntity>().ShortPrefabName == "bear.corpse") 
                        {
                            (entity as BasePlayer).inventory.GiveItem(ItemManager.CreateByName("blood", 1));
                            SendReply((entity as BasePlayer), "<color=red>[BloodHXS]</color> Вы добыли пакетик с кровью");
                        }
                    }
                }
            }

            #endregion
        
        #region |Конфигурация|
        public ConfigData config;
        public class ConfigData
        {
            public BCFG BloodWound = new BCFG();
            public class BCFG
            { 
                [JsonProperty(PropertyName = "Включить функцию воскрешения с помощью пакетика с кровью?")] public bool UseBlood = true; 
                [JsonProperty(PropertyName = "Включить функцию крафта крови")] public bool UseCraft = true;
                [JsonProperty(PropertyName = "ID Предмета для крафта крови")] public int ItemCraft = 1325935999;
                [JsonProperty(PropertyName = "Кол-во предмета трубуется для крафта")] public int ItemCraftAmmount = 40;
            }
        }
        public ConfigData GetDefaultConfig() { return new ConfigData { }; }
        protected override void LoadConfig() { base.LoadConfig(); try { config = Config.ReadObject<ConfigData>();}
            catch { LoadDefaultConfig(); } SaveConfig(); }
                protected override void LoadDefaultConfig()
                {
                    PrintError("Файл конфига не найден, создаю новый");
                    config = GetDefaultConfig();
                }
                protected override void SaveConfig() {Config.WriteObject(config); }
                #endregion
        
        #region |Локализация|
        
        string Message(string key, string playerId = null) => lang.GetMessage(key, this, playerId);

        private Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["useblood"] = "Вы <color=#90EE90>успешно</color> сделали переливание крови.",
            ["noblood"] = "Вы не ранены.",
            ["craft"] = "Вы <color=#90EE90>успешно</color> скрафтили пакетик с кровью.",
            ["nocraft"] = "Не хватает ресурсов на крафт <color=#FA8072>крови</color>."
        };
        #endregion
        
        #region |Вспомогательный код|
        private static string HexToRustFormat(string hex) { if (string.IsNullOrEmpty(hex)) { hex = "#FFFFFFFF"; } var str = hex.Trim('#'); if (str.Length == 6) str += "FF"; if (str.Length != 8) { throw new Exception(hex); throw new InvalidOperationException("Cannot convert a wrong format."); } var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber); var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber); var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber); var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber); Color color = new Color32(r, g, b, a); return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a); }
        #endregion
    }
}