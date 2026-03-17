using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Clear Repair", "Clearshot", "1.2.0")]
    [Description("Display insufficient resources required to repair with hammer or toolgun")]
    class ClearRepair : CovalencePlugin
    {
        private PluginConfig _config;
        private Game.Rust.Libraries.Player _rustPlayer = Interface.Oxide.GetLibrary<Game.Rust.Libraries.Player>("Player");
        private StringBuilder _sb = new StringBuilder();
        private readonly Dictionary<string, string> _shortPrefabNameToBuilding = new Dictionary<string, string>();

        private void SendChatMsg(BasePlayer pl, string msg, string prefix = null) =>
            _rustPlayer.Message(pl, msg, prefix != null ? prefix : lang.GetMessage("ChatPrefix", this, pl.UserIDString), Convert.ToUInt64(_config.chatIconID), Array.Empty<object>());

        private void Init()
        {
            permission.RegisterPermission("clearrepair.use", this);
        }

        private void OnServerInitialized()
        {
            foreach (var entityPath in GameManifest.Current.entities)
            {
                var construction = PrefabAttribute.server.Find<Construction>(StringPool.Get(entityPath));
                if (construction != null && construction.deployable == null && !string.IsNullOrEmpty(construction.info.name.english))
                {
                    var shortname = construction.fullName.Substring(construction.fullName.LastIndexOf('/') + 1).Replace(".prefab", "");
                    if (!_shortPrefabNameToBuilding.ContainsKey(shortname))
                        _shortPrefabNameToBuilding.Add(shortname, construction.info.name.english);
                }
            }
        }

        private object OnStructureRepair(BaseCombatEntity ent, BasePlayer pl)
        {
            if (ent == null || pl == null) return null;
            if (_config.usePermission && !permission.UserHasPermission(pl.UserIDString, "clearrepair.use")) return null;

            float num = 30f;
            if (ent.SecondsSinceAttacked <= num)
            {
                return null;
            }
            float num2 = ent.MaxHealth() - ent.health;
            float num3 = num2 / ent.MaxHealth();
            if (num2 <= 0f || num3 <= 0f)
            {
                return null;
            }
            List<ItemAmount> list = ent.RepairCost(num3);
            if (list == null)
            {
                return null;
            }

            float num4 = list.Sum((ItemAmount x) => x.amount);
            if (num4 > 0f)
            {
                float num5 = list.Min((ItemAmount x) => UnityEngine.Mathf.Clamp01(pl.inventory.GetAmount(x.itemid) / x.amount));
                num5 = UnityEngine.Mathf.Min(num5, 50f / num2);
                if (num5 <= 0f)
                {
                    if (_config.defaultChatNotification)
                        ent.OnRepairFailed(pl, lang.GetMessage("DefaultChatNotification", this, pl.UserIDString));

                    _sb.Clear();
                    _sb.AppendLine(string.Format(lang.GetMessage("RepairItemName", this, pl.UserIDString), GetItemName(ent)));
                    _sb.AppendLine(lang.GetMessage("InsufficientRes", this, pl.UserIDString));
                    foreach (ItemAmount itemAmount in list)
                    {
                        string color = pl.inventory.GetAmount(itemAmount.itemid) >= itemAmount.amount ? _config.itemFoundColor : _config.itemNotFoundColor;
                        _sb.AppendLine(string.Format(lang.GetMessage("ItemAmount", this, pl.UserIDString), itemAmount.itemDef.displayName.translated, color, itemAmount.amount));
                    }

                    SendChatMsg(pl, _sb.ToString());
                    return false;
                }
            }

            return null;
        }

        private string GetItemName(BaseCombatEntity ent)
        {
            string itemName = ent.ShortPrefabName;
            if (ent?.repair.itemTarget != null && !string.IsNullOrEmpty(ent.repair.itemTarget.displayName.english))
                itemName = ent.repair.itemTarget.displayName.english;
            if (_shortPrefabNameToBuilding.ContainsKey(ent.ShortPrefabName))
                itemName = _shortPrefabNameToBuilding[ent.ShortPrefabName];
            if (_config.customNames.ContainsKey(ent.ShortPrefabName))
                itemName = _config.customNames[ent.ShortPrefabName];
            return itemName;
        }

        #region Config

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ChatPrefix"] = "<color=#00a7fe>[Clear Repair]</color>",
                ["RepairItemName"] = "<line-height=20>{0}",
                ["InsufficientRes"] = "<size=12>Unable to repair: Insufficient resources.</size><line-indent=5>",
                ["ItemAmount"] = "<size=12>{0}: <color={1}>{2}</color></size>",
                ["DefaultChatNotification"] = "Unable to repair: Insufficient resources."
            }, this);
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(_config, true);
        }

        private class PluginConfig
        {
            public bool usePermission = false;
            public bool defaultChatNotification = false;
            public string chatIconID = "0";
            public string itemFoundColor = "#87b33a";
            public string itemNotFoundColor = "#cb3f2a";
            public Dictionary<string, string> customNames = new Dictionary<string, string> {
                { "minicopter.entity", "Minicopter" },
                { "rowboat", "Row Boat" },
                { "rhib", "RHIB" },
                { "scraptransporthelicopter", "Scrap Transport Helicopter"}
            };
        }

        #endregion
    }
}
