using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Oxide.Plugins
{
    [Info("Building Upgrade", "OxideBro", "1.2.0")]
    class BuildingUpgrade : RustPlugin
    {
        [PluginReference] Plugin Remove, NoEscape, BuildingProtection;

        private void PayForUpgrade(ConstructionGrade g, BasePlayer player)
        {
            List<Item> items = new List<Item>();
            foreach (ItemAmount itemAmount in g.costToBuild)
            {
                player.inventory.Take(items, itemAmount.itemid, (int)itemAmount.amount);
                player.Command(string.Concat(new object[] {
                    "note.inv ", itemAmount.itemid, " ", itemAmount.amount * -1f
                }
                ), new object[0]);
            }
            foreach (Item item in items)
            {
                item.Remove(0f);
            }
        }

        private ConstructionGrade GetGrade(BuildingBlock block, BuildingGrade.Enum iGrade)
        {
            if ((int)block.grade < (int)block.blockDefinition.grades.Length) return block.blockDefinition.grades[(int)iGrade];
            return block.blockDefinition.defaultGrade;
        }

        private bool CanAffordUpgrade(BuildingBlock block, BuildingGrade.Enum iGrade, BasePlayer player)
        {
            bool flag;
            object[] objArray = new object[] { player, block, iGrade };
            object obj = Interface.CallHook("CanAffordUpgrade", objArray);
            if (obj is bool)
            {
                return (bool)obj;
            }
            List<ItemAmount>.Enumerator enumerator = GetGrade(block, iGrade).costToBuild.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    ItemAmount current = enumerator.Current;
                    if ((float)player.inventory.GetAmount(current.itemid) >= current.amount)
                    {
                        continue;
                    }
                    flag = false;
                    return flag;
                }
                return true;
            }
            finally
            {
                ((IDisposable)enumerator).Dispose();
            }
        }

        Dictionary<BuildingGrade.Enum, string> gradesString = new Dictionary<BuildingGrade.Enum, string>() {
                {
                BuildingGrade.Enum.Wood, "<color=#EC402C>Дерева</color>"
            }
            , {
                BuildingGrade.Enum.Stone, "<color=#EC402C>Камня</color>"
            }
            , {
                BuildingGrade.Enum.Metal, "<color=#EC402C>Метала</color>"
            }
            , {
                BuildingGrade.Enum.TopTier, "<color=#EC402C>Армора</color>"
            }
        };

        Dictionary<BasePlayer, BuildingGrade.Enum> grades = new Dictionary<BasePlayer, BuildingGrade.Enum>();
        Dictionary<BasePlayer, int> timers = new Dictionary<BasePlayer, int>();



        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Благодарим за заказ плагина у разработчика OxideBro. Если вы передадите этот плагин сторонним лицам знайте - это лишает вас гарантированных обновлений!");
            config = PluginConfig.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
            if (config.PluginVersion < Version)
                UpdateConfigValues();
            Config.WriteObject(config, true);
        }

        private void UpdateConfigValues()
        {
            PluginConfig baseConfig = PluginConfig.DefaultConfig();
            if (config.PluginVersion < new VersionNumber(0, 1, 0))
            {
                PrintWarning("Config update detected! Updating config values...");
                PrintWarning("Config update completed!");
            }
            config.PluginVersion = Version;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }


        public class MainSettings
        {
            [JsonProperty("Через сколько секунд автоматически выключать улучшение строений")]
            public int resetTime = 40;
            [JsonProperty("Привилегия что бы позволить улучшать объекты при строительстве")]
            public string permissionAutoGrade = "buildingupgrade.build";
            [JsonProperty("Привилегия для улучшения при строительстве и ударе киянкой без траты ресурсов")]
            public string permissionAutoGradeFree = "buildingupgrade.free";
            [JsonProperty("Привилегия что бы позволить улучшать объекты ударом киянки")]
            public string permissionAutoGradeHammer = "buildingupgrade.hammer";
            [JsonProperty("Включить бесплатный Upgrade для администраторов?")]
            public bool permissionAutoGradeAdmin = true;
            [JsonProperty("Запретить Upgrade в Building Block?")]
            public bool getBuild = true;
            [JsonProperty("Включить доступ только по привилегиям?")]
            public bool permissionOn = true;
            [JsonProperty("Включить поддержку NoEscape (Запретить Upgrade в Raid Block)?")]
            public bool useNoEscape = true;
            [JsonProperty("Включить поддержку BuildingProtection (Запретить Upgrade в BuildingProtection)?")]
            public bool useBuildingProtection = false;
            [JsonProperty("Разрешить улучшать повреждённые постройки?")]
            public bool CanUpgradeDamaged = false;
            [JsonProperty("Включить выключение удаления построек при включении авто-улучшения (Поддержка плагина Remove с сайта RustPlugin.ru)")]
            public bool EnabledRemove = false;
            [JsonProperty("Включить переключение типов апгреда клавией E для игроков (При включенной функции может быть небольшая нагрузка из за хука)")]
            public bool EnabledInput = false;
        }

        public class MessagesSettings
        {
            [JsonProperty("No Permissions Hammer:")]
            public string MessageAutoGradePremHammer = "У вас нету доступа к улучшению киянкой!";
            [JsonProperty("No Permissions:")]
            public string MessageAutoGradePrem = "У вас нету доступа к данной команде!";
            [JsonProperty("No Resources:")]
            public string MessageAutoGradeNo = "<color=ffcc00><size=16>Для улучшения нехватает ресурсов!!!</size></color>";
            [JsonProperty("Сообщение при включение Upgrade:")]
            public string MessageAutoGradeOn = "<size=14><color=#EC402C>Upgrade включен!</color> \nДля быстрого переключения используйте: <color=#EC402C>/upgrade 0-4</color></size>";
            [JsonProperty("Сообщение при выключение Upgrade:")]
            public string MessageAutoGradeOff = "<color=ffcc00><size=14>Вы отключили <color=#EC402C>Upgrade!</color></size></color>";
        }

        public class GUISettings
        {
            [JsonProperty("Минимальный отступ:")]
            public string PanelAnchorMin = "0.0 0.908";
            [JsonProperty("Максимальный отступ:")]
            public string PanelAnchorMax = "1 0.958";
            [JsonProperty("Цвет фона:")]
            public string PanelColor = "0 0 0 0.50";
        }

        public class GUISettingsText
        {
            [JsonProperty("Размер текста в gui панели:")]
            public int TextFontSize = 16;
            [JsonProperty("Цвет текста в gui панели:")]
            public string TextСolor = "0 0 0 1";
            [JsonProperty("Минимальный отступ в gui панели:")]
            public string TextAnchorMin = "0.0 0.870";
            [JsonProperty("Максимальный отступ в gui панели:")]
            public string TextAnchorMax = "1 1";
        }


        public class InfoNotiseSettings
        {
            [JsonProperty("Включить GUI оповещение при использование плана постройки")]
            public bool InfoNotice = true;
            [JsonProperty("Размер текста GUI оповещения")]
            public int InfoNoticeSize = 18;
            [JsonProperty("Сообщение GUI")]
            public string InfoNoticeText = "Используйте <color=#EC402C>/upgrade</color> (Или нажмите <color=#EC402C>USE - Клавиша E</color>) для быстрого улучшения при постройке.";
            [JsonProperty("Время показа оповещения")]
            public int InfoNoticeTextTime = 5;
        }

        public class CommandSettings
        {
            [JsonProperty("Чатовая команда включения авто-улучшения при постройки")]
            public string ChatCMD = "upgrade";
            [JsonProperty("Консольная команда включения авто-улучшения при постройки")]
            public string ConsoleCMD = "building.upgrade";
        }

        class PluginConfig
        {
            [JsonProperty("Configuration Version")]
            public VersionNumber PluginVersion = new VersionNumber();
            [JsonProperty("Основные настройки")]
            public MainSettings mainSettings;
            [JsonProperty("Сообщения")]
            public MessagesSettings messagesSettings;
            [JsonProperty("Настройки GUI Panel")]
            public GUISettings gUISettings;
            [JsonProperty("Настройки GUI Text")]
            public GUISettingsText gUISettingsText;

            [JsonProperty("Настройки GUI Оповещения")]
            public InfoNotiseSettings infoNotiseSettings;

            [JsonProperty("Команды")]
            public CommandSettings commandSettings;
            [JsonIgnore]
            [JsonProperty("Server Initialized⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠")]
            public bool Init = false;

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    PluginVersion = new VersionNumber(),
                    commandSettings = new CommandSettings(),
                    gUISettings = new GUISettings(),
                    gUISettingsText = new GUISettingsText(),
                    infoNotiseSettings = new InfoNotiseSettings(),
                    mainSettings = new MainSettings(),
                    messagesSettings = new MessagesSettings()
                };
            }
        }

        public Timer mytimer;

        void cmdAutoGrade(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (config.mainSettings.permissionOn && !permission.UserHasPermission(player.UserIDString, config.mainSettings.permissionAutoGrade))
            {
                SendReply(player, config.messagesSettings.MessageAutoGradePrem);
                return;
            }
            int grade;
            timers[player] = config.mainSettings.resetTime;
            if (config.mainSettings.EnabledRemove)
            {
                var removeEnabled = (bool)Remove.Call("OnRemoveActivate", player.userID);
                if (removeEnabled)
                {
                    Remove.Call("RemoveDeativate", player.userID);
                }
            }
            if (args == null || args.Length <= 0 || args[0] != "1" && args[0] != "2" && args[0] != "3" && args[0] != "4" && args[0] != "0")
            {
                if (!grades.ContainsKey(player))
                {
                    grade = (int)(grades[player] = BuildingGrade.Enum.Wood);
                    SendReply(player, config.messagesSettings.MessageAutoGradeOn);
                }
                else
                {
                    grade = (int)grades[player];
                    grade++;
                    grades[player] = (BuildingGrade.Enum)Mathf.Clamp(grade, 1, 5);
                }
                if (grade > 4)
                {
                    grades.Remove(player);
                    timers.Remove(player);
                    DestroyUI(player);
                    SendReply(player, config.messagesSettings.MessageAutoGradeOff);
                    return;
                }
                timers[player] = config.mainSettings.resetTime;
                DrawUI(player, (BuildingGrade.Enum)grade, config.mainSettings.resetTime);
                return;
            }
            switch (args[0])
            {
                case "1":
                    grade = (int)(grades[player] = BuildingGrade.Enum.Wood);
                    timers[player] = config.mainSettings.resetTime;
                    DrawUI(player, BuildingGrade.Enum.Wood, config.mainSettings.resetTime);
                    return;
                case "2":
                    grade = (int)(grades[player] = BuildingGrade.Enum.Stone);
                    timers[player] = config.mainSettings.resetTime;
                    DrawUI(player, BuildingGrade.Enum.Stone, config.mainSettings.resetTime);
                    return;
                case "3":
                    grade = (int)(grades[player] = BuildingGrade.Enum.Metal);
                    timers[player] = config.mainSettings.resetTime;
                    DrawUI(player, BuildingGrade.Enum.Metal, config.mainSettings.resetTime);
                    return;
                case "4":
                    grade = (int)(grades[player] = BuildingGrade.Enum.TopTier);
                    timers[player] = config.mainSettings.resetTime;
                    DrawUI(player, BuildingGrade.Enum.TopTier, config.mainSettings.resetTime);
                    return;
                case "0":
                    grades.Remove(player);
                    timers.Remove(player);
                    DestroyUI(player);
                    SendReply(player, config.messagesSettings.MessageAutoGradeOff);
                    return;
            }
        }
        void consoleAutoGrade(ConsoleSystem.Arg arg, string[] args)
        {
            var player = arg.Player();
            if (config.mainSettings.permissionOn && !permission.UserHasPermission(player.UserIDString, config.mainSettings.permissionAutoGrade))
            {
                SendReply(player, config.messagesSettings.MessageAutoGradePrem);
                return;
            }
            int grade;
            if (config.mainSettings.EnabledRemove)
            {
                var removeEnabled = (bool)Remove.Call("OnRemoveActivate", player.userID);
                if (removeEnabled)
                {
                    Remove.Call("RemoveDeativate", player.userID);
                }
            }
            timers[player] = config.mainSettings.resetTime;
            if (player == null) return;
            if (args == null || args.Length <= 0)
            {
                if (!grades.ContainsKey(player))
                {
                    grade = (int)(grades[player] = BuildingGrade.Enum.Wood);
                    SendReply(player, config.messagesSettings.MessageAutoGradeOn);
                }
                else
                {
                    grade = (int)grades[player];
                    grade++;
                    grades[player] = (BuildingGrade.Enum)Mathf.Clamp(grade, 1, 5);
                }
                if (grade > 4)
                {
                    grades.Remove(player);
                    timers.Remove(player);
                    DestroyUI(player);
                    SendReply(player, config.messagesSettings.MessageAutoGradeOff);
                    return;
                }
                timers[player] = config.mainSettings.resetTime;
                DrawUI(player, (BuildingGrade.Enum)grade, config.mainSettings.resetTime);
            }
        }

        void OnServerInitialized()
        {
            if (!config.infoNotiseSettings.InfoNotice)
                Unsubscribe("OnActiveItemChanged");
            else
                Subscribe("OnActiveItemChanged");

            if (!config.mainSettings.EnabledInput)
                Unsubscribe("OnPlayerInput");
            else
                Subscribe("OnPlayerInput");

            permission.RegisterPermission(config.mainSettings.permissionAutoGrade, this);
            permission.RegisterPermission(config.mainSettings.permissionAutoGradeFree, this);
            permission.RegisterPermission(config.mainSettings.permissionAutoGradeHammer, this);
            cmd.AddChatCommand(config.commandSettings.ChatCMD, this, cmdAutoGrade);
            cmd.AddConsoleCommand(config.commandSettings.ConsoleCMD, this, "consoleAutoGrade");
            timer.Every(1f, GradeTimerHandler);

            config.Init = true;
        }

        private void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (!grades.ContainsKey(player)) return;
            if (newItem == null || newItem.info.shortname != "building.planner") return;
            CuiHelper.DestroyUi(player, "InfoNotice");
            ShowUIInfo(player);
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || input == null || !config.Init) return;
            Item activeItem = player.GetActiveItem();
            if (input.WasJustPressed(BUTTON.USE))
            {
                if (activeItem == null || activeItem.info.shortname != "building.planner") return;
                if (config.mainSettings.permissionOn && !permission.UserHasPermission(player.UserIDString, config.mainSettings.permissionAutoGrade))
                {
                    SendReply(player, config.messagesSettings.MessageAutoGradePrem);
                    return;
                }
                int grade;
                timers[player] = config.mainSettings.resetTime;
                if (!grades.ContainsKey(player))
                {
                    grade = (int)(grades[player] = BuildingGrade.Enum.Wood);
                    SendReply(player, config.messagesSettings.MessageAutoGradeOn);
                }
                else
                {
                    grade = (int)grades[player];
                    grade++;
                    grades[player] = (BuildingGrade.Enum)Mathf.Clamp(grade, 1, 5);
                }
                if (grade > 4)
                {
                    grades.Remove(player);
                    timers.Remove(player);
                    DestroyUI(player);
                    SendReply(player, config.messagesSettings.MessageAutoGradeOff);
                    return;
                }
                timers[player] = config.mainSettings.resetTime;
                DrawUI(player, (BuildingGrade.Enum)grade, config.mainSettings.resetTime);
                return;
            }
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                DestroyUI(player);
        }

        void ShowUIInfo(BasePlayer player)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Name = "InfoNotice",
                Parent = "Hud",
                FadeOut = 1f,
                Components = {
                    new CuiTextComponent {
                        FadeIn=1f, Text=$"{config.infoNotiseSettings.InfoNoticeText}", FontSize=config.infoNotiseSettings.InfoNoticeSize, Align=TextAnchor.MiddleCenter, Font="robotocondensed-regular.ttf"
                    }
                    , new CuiOutlineComponent {
                        Color="0.0 0.0 0.0 1.0"
                    }
                    , new CuiRectTransformComponent {
                        AnchorMin="0.1 0.2", AnchorMax="0.9 0.25"
                    }
                }
            }
            );
            CuiHelper.AddUi(player, container);

            mytimer = timer.Once(config.infoNotiseSettings.InfoNoticeTextTime, () =>
            {
                if (player == null) return;
                CuiHelper.DestroyUi(player, "InfoNotice");
            }
            );
        }

        void OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null || info?.HitEntity == null || !config.Init) return;
            var buildingBlock = info?.HitEntity as BuildingBlock;
            if (buildingBlock == null || player == null) return;
            if (config.mainSettings.permissionOn && !permission.UserHasPermission(player.UserIDString, config.mainSettings.permissionAutoGradeHammer))
            {
                SendReply(player, config.messagesSettings.MessageAutoGradePremHammer);
                return;
            }
            Grade(buildingBlock, player);
        }

        void OnEntityBuilt(Planner planner, GameObject gameObject)
        {
            if (planner == null || gameObject == null || !config.Init) return;
            var player = planner.GetOwnerPlayer();
            BuildingBlock entity = gameObject.ToBaseEntity() as BuildingBlock;
            if (entity == null || entity.IsDestroyed) return;
            if (player == null) return;
            Grade(entity, player);
        }


        void Grade(BuildingBlock block, BasePlayer player)
        {
            if (config.mainSettings.useNoEscape)
            {
                object can = NoEscape?.Call("IsRaidBlocked", player);
                if (can != null) if ((bool)can == true)
                    {
                        SendReply(player, "Вы не можете использовать Upgrade во время рейд-блока");
                        return;
                    }
            }

            if (config.mainSettings.useBuildingProtection && BuildingProtection && player.GetBuildingPrivilege() != null)
            {
                if ((bool)BuildingProtection?.Call("IsProtection", player.GetBuildingPrivilege().net.ID))
                {
                    SendReply(player, "Строительство при включенной защите запрещено.");
                    return;
                }
            }
            BuildingGrade.Enum grade;
            if (!grades.TryGetValue(player, out grade) || grade == BuildingGrade.Enum.Count) return;

            if (block == null) return;

            if (!((int)grade >= 1 && (int)grade <= 4)) return;

            var targetLocation = player.transform.position + (player.eyes.BodyForward() * 4f);
            var reply = 3260;
            if (reply == 0) { }
            if (config.mainSettings.getBuild && player.IsBuildingBlocked(targetLocation, new Quaternion(0, 0, 0, 0), new Bounds(Vector3.zero, Vector3.zero)))
            {
                player.ChatMessage("<color=ffcc00><size=16><color=#EC402C>Upgrade</color> запрещен в билдинг блоке!!!</size></color>");
                return;
            }
            if (block.blockDefinition.checkVolumeOnUpgrade)
            {
                if (DeployVolume.Check(block.transform.position, block.transform.rotation, PrefabAttribute.server.FindAll<DeployVolume>(block.prefabID), ~(1 << block.gameObject.layer)))
                {
                    player.ChatMessage("Вы не можете улучшить постройку находясь в ней");
                    return;
                }
            }
            var ret = Interface.Call("CanUpgrade", player) as string;
            if (ret != null)
            {
                SendReply(player, ret);
                return;
            }
            if (config.mainSettings.permissionAutoGradeAdmin && player.IsAdmin || config.mainSettings.permissionOn && permission.UserHasPermission(player.UserIDString, config.mainSettings.permissionAutoGradeFree))
            {
                if (block.grade > grade)
                {
                    SendReply(player, "Нельзя понижать уровень строения!");
                    return;
                }
                if (block.grade == grade)
                {
                    SendReply(player, "Уровень строения соответствует выбранному.");
                    return;
                }
                if (block.Health() != block.MaxHealth() && !config.mainSettings.CanUpgradeDamaged)
                {
                    SendReply(player, "Нельзя улучшать повреждённые постройки!");
                    return;
                }
                block.SetGrade(grade);
                block.SetHealthToMax();
                block.UpdateSkin(false);
                Effect.server.Run(string.Concat("assets/bundled/prefabs/fx/build/promote_", grade.ToString().ToLower(), ".prefab"), block, 0, Vector3.zero, Vector3.zero, null, false);
                timers[player] = config.mainSettings.resetTime;
                DrawUI(player, grade, config.mainSettings.resetTime);
                return;
            }

            if (CanAffordUpgrade(block, grade, player))
            {
                if (block.grade > grade)
                {
                    SendReply(player, "Нельзя понижать уровень строения!");
                    return;
                }
                if (block.grade == grade)
                {
                    SendReply(player, "Уровень строения соответствует выбранному.");
                    return;
                }
                if (block.Health() != block.MaxHealth() && !config.mainSettings.CanUpgradeDamaged)
                {
                    SendReply(player, "Нельзя улучшать повреждённые постройки!");
                    return;
                }
                PayForUpgrade(GetGrade(block, grade), player);
                block.SetGrade(grade);
                block.SetHealthToMax();
                block.UpdateSkin(false);
                Effect.server.Run(string.Concat("assets/bundled/prefabs/fx/build/promote_", grade.ToString().ToLower(), ".prefab"), block, 0, Vector3.zero, Vector3.zero, null, false);
                timers[player] = config.mainSettings.resetTime;
                DrawUI(player, grade, config.mainSettings.resetTime);
            }
            else
                SendReply(player, config.messagesSettings.MessageAutoGradeNo);
        }

        void GradeTimerHandler()
        {
            foreach (var player in timers.Keys.ToList())
            {
                var seconds = --timers[player];
                if (seconds <= 0)
                {
                    grades.Remove(player);
                    timers.Remove(player);
                    DestroyUI(player);
                    continue;
                }
                DrawUI(player, grades[player], seconds);
            }
        }

        void DrawUI(BasePlayer player, BuildingGrade.Enum grade, int seconds)
        {
            DestroyUI(player);
            CuiHelper.AddUi(player, GUI.Replace("{0}", gradesString[grade]).Replace("{1}", seconds.ToString()).Replace("{PanelColor}", config.gUISettings.PanelColor).Replace("{PanelAnchorMin}", config.gUISettings.PanelAnchorMin).Replace("{PanelAnchorMax}", config.gUISettings.PanelAnchorMax).Replace("{TextFontSize}", config.gUISettingsText.TextFontSize.ToString()).Replace("{TextСolor}", config.gUISettingsText.TextСolor.ToString()).Replace("{TextAnchorMin}", config.gUISettingsText.TextAnchorMin).Replace("{TextAnchorMax}", config.gUISettingsText.TextAnchorMax));
        }

        void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "autograde.panel");
            CuiHelper.DestroyUi(player, "autogradetext");
        }

        private string GUI = @"[{""name"": ""autograde.panel"",""parent"": ""Hud"",""components"": [{""type"": ""UnityEngine.UI.Image"",""color"": ""{PanelColor}""},{""type"": ""RectTransform"",""anchormin"": ""{PanelAnchorMin}"",""anchormax"": ""{PanelAnchorMax}""}]}, {""name"": ""autogradetext"",""parent"": ""Hud"",""components"": [{""type"": ""UnityEngine.UI.Text"",""text"": ""Режим улучшения строения до {0} выключится через " + @"{1} секунд."",""fontSize"": ""{TextFontSize}"",""align"": ""MiddleCenter""}, {""type"": ""UnityEngine.UI.Outline"",""color"": ""{TextСolor}"",""distance"": ""0.1 -0.1""}, {""type"": ""RectTransform"",""anchormin"": ""{TextAnchorMin}"",""anchormax"": ""{TextAnchorMax}""}]}]";
        void UpdateTimer(BasePlayer player, ulong playerid = 3338240)
        {
            timers[player] = config.mainSettings.resetTime;
            DrawUI(player, grades[player], timers[player]);
        }

        object BuildingUpgradeActivate(ulong id)
        {
            var player = BasePlayer.FindByID(id);
            if (player != null) if (grades.ContainsKey(player)) return true;
            return false;
        }

        void BuildingUpgradeDeactivate(ulong id)
        {
            var player = BasePlayer.FindByID(id);
            if (player != null)
            {
                grades.Remove(player);
                timers.Remove(player);
                DestroyUI(player);
            }
        }
    }
}