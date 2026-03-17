using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Oxide.Plugins.WaterEventExtensionMethods;

namespace Oxide.Plugins
{
    [Info("WaterEvent", "KpucTaJl", "2.1.0")]
    internal class WaterEvent : RustPlugin
    {
        #region Config
        private const bool En = true;

        private PluginConfig _config;

        protected override void LoadDefaultConfig()
        {
            Puts("Creating a default config...");
            _config = PluginConfig.DefaultConfig();
            _config.PluginVersion = Version;
            SaveConfig();
            Puts("Creation of the default config completed!");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();
            if (_config.PluginVersion < Version) UpdateConfigValues();
        }

        private void UpdateConfigValues()
        {
            Puts("Config update detected! Updating config values...");
            if (_config.PluginVersion < new VersionNumber(2, 0, 3))
            {
                _config.Gui = new GuiConfig
                {
                    IsGui = true,
                    OffsetMinY = "-56"
                };
                foreach (PresetConfig preset in _config.OutsideNpc) foreach (NpcBelt belt in preset.Config.BeltItems) belt.Ammo = string.Empty;
                foreach (DoorsToScientists doorsToScientists in _config.DoorsToScientists) foreach (PresetConfig preset in doorsToScientists.Scientists) foreach (NpcBelt belt in preset.Config.BeltItems) belt.Ammo = string.Empty;
            }
            if (_config.PluginVersion < new VersionNumber(2, 0, 6))
            {
                _config.Commands = new HashSet<string>
                {
                    "/remove",
                    "remove.toggle"
                };
            }
            if (_config.PluginVersion < new VersionNumber(2, 1, 0))
            {
                _config.Radius = 70f;
                _config.Marker.Name = "WaterEvent ({time})";
            }
            _config.PluginVersion = Version;
            Puts("Config update completed!");
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        public class ItemConfig
        {
            [JsonProperty("ShortName")] public string ShortName { get; set; }
            [JsonProperty(En ? "Minimum" : "Минимальное кол-во")] public int MinAmount { get; set; }
            [JsonProperty(En ? "Maximum" : "Максимальное кол-во")] public int MaxAmount { get; set; }
            [JsonProperty(En ? "Chance [0.0-100.0]" : "Шанс выпадения предмета [0.0-100.0]")] public float Chance { get; set; }
            [JsonProperty(En ? "Is this a blueprint? [true/false]" : "Это чертеж? [true/false]")] public bool IsBluePrint { get; set; }
            [JsonProperty("SkinID (0 - default)")] public ulong SkinID { get; set; }
            [JsonProperty(En ? "Name (empty - default)" : "Название (empty - default)")] public string Name { get; set; }
        }

        public class LootTableConfig
        {
            [JsonProperty(En ? "Minimum numbers of items" : "Минимальное кол-во элементов")] public int Min { get; set; }
            [JsonProperty(En ? "Maximum numbers of items" : "Максимальное кол-во элементов")] public int Max { get; set; }
            [JsonProperty(En ? "Use minimum and maximum values? [true/false]" : "Использовать минимальное и максимальное значение? [true/false]")] public bool UseCount { get; set; }
            [JsonProperty(En ? "List of items" : "Список предметов")] public List<ItemConfig> Items { get; set; }
        }

        public class PrefabConfig
        {
            [JsonProperty(En ? "Chance [0.0-100.0]" : "Шанс выпадения [0.0-100.0]")] public float Chance { get; set; }
            [JsonProperty(En ? "The path to the prefab" : "Путь к prefab-у")] public string PrefabDefinition { get; set; }
        }

        public class PrefabLootTableConfig
        {
            [JsonProperty(En ? "Minimum numbers of prefabs" : "Минимальное кол-во prefab-ов")] public int Min { get; set; }
            [JsonProperty(En ? "Maximum numbers of prefabs" : "Максимальное кол-во prefab-ов")] public int Max { get; set; }
            [JsonProperty(En ? "Use minimum and maximum values? [true/false]" : "Использовать минимальное и максимальное значение? [true/false]")] public bool UseCount { get; set; }
            [JsonProperty(En ? "List of prefabs" : "Список prefab-ов")] public List<PrefabConfig> Prefabs { get; set; }
        }

        public class EntityCrateConfig
        {
            [JsonProperty("Prefab")] public string Prefab { get; set; }
            [JsonProperty(En ? "Is it necessary for loot to appear in the container? [true/false]" : "Могут ли появляться предметы в этом ящике? [true/false]")] public bool IsOwnLootTable { get; set; }
            [JsonProperty(En ? "Own loot table" : "Собственная таблица лута")] public LootTableConfig LootTable { get; set; }
        }

        public class CrateConfig
        {
            [JsonProperty("Prefab")] public string Prefab { get; set; }
            [JsonProperty(En ? "Which loot table should the plugin use? (0 - default; 1 - own; 2 - AlphaLoot; 3 - CustomLoot; 4 - loot table of the Rust objects; 5 - combine the 1 and 4 methods)" : "Какую таблицу лута необходимо использовать? (0 - стандартную; 1 - собственную; 2 - AlphaLoot; 3 - CustomLoot; 4 - таблица предметов объектов Rust; 5 - совместить 1 и 4 методы)")] public int TypeLootTable { get; set; }
            [JsonProperty(En ? "Loot table from prefabs (if the loot table type is 4 or 5)" : "Таблица предметов из prefab-ов (если тип таблицы предметов - 4 или 5)")] public PrefabLootTableConfig PrefabLootTable { get; set; }
            [JsonProperty(En ? "Own loot table (if the loot table type is 1 or 5)" : "Собственная таблица предметов (если тип таблицы предметов - 1 или 5)")] public LootTableConfig OwnLootTable { get; set; }
            [JsonProperty(En ? "Item Multipliers" : "Множители предметов")] public Dictionary<string, float> ScaleItems { get; set; }
        }

        public class HackCrateConfig
        {
            [JsonProperty(En ? "Location of all Crates" : "Расположение всех ящиков")] public HashSet<CoordConfig> Coordinates { get; set; }
            [JsonProperty(En ? "Time to unlock the Crates [sec.]" : "Время разблокировки ящиков [sec.]")] public float UnlockTime { get; set; }
            [JsonProperty(En ? "Increase the event time if it's not enough to unlock the locked crate? [true/false]" : "Увеличивать время ивента, если недостаточно чтобы разблокировать заблокированный ящик? [true/false]")] public bool IncreaseEventTime { get; set; }
            [JsonProperty(En ? "Which loot table should the plugin use? (0 - default; 1 - own; 2 - AlphaLoot; 3 - CustomLoot; 4 - loot table of the Rust objects; 5 - combine the 1 and 4 methods)" : "Какую таблицу лута необходимо использовать? (0 - стандартную; 1 - собственную; 2 - AlphaLoot; 3 - CustomLoot; 4 - таблица предметов объектов Rust; 5 - совместить 1 и 4 методы)")] public int TypeLootTable { get; set; }
            [JsonProperty(En ? "Loot table from prefabs (if the loot table type is 4 or 5)" : "Таблица предметов из prefab-ов (если тип таблицы предметов - 4 или 5)")] public PrefabLootTableConfig PrefabLootTable { get; set; }
            [JsonProperty(En ? "Own loot table (if the loot table type is 1 or 5)" : "Собственная таблица предметов (если тип таблицы предметов - 1 или 5)")] public LootTableConfig OwnLootTable { get; set; }
            [JsonProperty(En ? "Item Multipliers" : "Множители предметов")] public Dictionary<string, float> ScaleItems { get; set; }
        }

        public class ColorConfig
        {
            [JsonProperty("r")] public float R { get; set; }
            [JsonProperty("g")] public float G { get; set; }
            [JsonProperty("b")] public float B { get; set; }
        }

        public class MarkerConfig
        {
            [JsonProperty(En ? "Name" : "Название")] public string Name { get; set; }
            [JsonProperty(En ? "Radius" : "Радиус")] public float Radius { get; set; }
            [JsonProperty(En ? "Alpha" : "Прозрачность")] public float Alpha { get; set; }
            [JsonProperty(En ? "Marker color" : "Цвет маркера")] public ColorConfig Color { get; set; }
        }

        public class GuiConfig
        {
            [JsonProperty(En ? "Do you use the countdown GUI? [true/false]" : "Использовать ли GUI обратного отсчета? [true/false]")] public bool IsGui { get; set; }
            [JsonProperty("OffsetMin Y")] public string OffsetMinY { get; set; }
        }

        public class GuiAnnouncementsConfig
        {
            [JsonProperty(En ? "Do you use the GUI Announcements? [true/false]" : "Использовать ли GUI Announcements? [true/false]")] public bool IsGuiAnnouncements { get; set; }
            [JsonProperty(En ? "Banner color" : "Цвет баннера")] public string BannerColor { get; set; }
            [JsonProperty(En ? "Text color" : "Цвет текста")] public string TextColor { get; set; }
            [JsonProperty(En ? "Adjust Vertical Position" : "Отступ от верхнего края")] public float ApiAdjustVPosition { get; set; }
        }

        public class NotifyConfig
        {
            [JsonProperty(En ? "Do you use the Notify? [true/false]" : "Использовать ли Notify? [true/false]")] public bool IsNotify { get; set; }
            [JsonProperty(En ? "Type" : "Тип")] public string Type { get; set; }
        }

        public class DiscordConfig
        {
            [JsonProperty(En ? "Do you use the Discord? [true/false]" : "Использовать ли Discord? [true/false]")] public bool IsDiscord { get; set; }
            [JsonProperty("Webhook URL")] public string WebhookUrl { get; set; }
            [JsonProperty(En ? "Embed Color (DECIMAL)" : "Цвет полосы (DECIMAL)")] public int EmbedColor { get; set; }
            [JsonProperty(En ? "Keys of required messages" : "Ключи необходимых сообщений")] public HashSet<string> Keys { get; set; }
        }

        public class ScaleDamageConfig
        {
            [JsonProperty(En ? "Type of target" : "Тип цели")] public string Type { get; set; }
            [JsonProperty(En ? "Damage Multiplier" : "Множитель урона")] public float Scale { get; set; }
        }

        public class PveModeConfig
        {
            [JsonProperty(En ? "Use the PVE mode of the plugin? [true/false]" : "Использовать PVE режим работы плагина? [true/false]")] public bool Pve { get; set; }
            [JsonProperty(En ? "The amount of damage that the player has to do to become the Event Owner" : "Кол-во урона, которое должен нанести игрок, чтобы стать владельцем ивента")] public float Damage { get; set; }
            [JsonProperty(En ? "Damage coefficients for calculate to become the Event Owner" : "Коэффициенты урона для подсчета, чтобы стать владельцем события")] public HashSet<ScaleDamageConfig> ScaleDamage { get; set; }
            [JsonProperty(En ? "Can the non-owner of the event loot the crates? [true/false]" : "Может ли не владелец ивента грабить ящики? [true/false]")] public bool LootCrate { get; set; }
            [JsonProperty(En ? "Can the non-owner of the event hack locked crates? [true/false]" : "Может ли не владелец ивента взламывать заблокированные ящики? [true/false]")] public bool HackCrate { get; set; }
            [JsonProperty(En ? "Can the non-owner of the event loot NPC corpses? [true/false]" : "Может ли не владелец ивента грабить трупы NPC? [true/false]")] public bool LootNpc { get; set; }
            [JsonProperty(En ? "Can the non-owner of the event deal damage to the NPC? [true/false]" : "Может ли не владелец ивента наносить урон по NPC? [true/false]")] public bool DamageNpc { get; set; }
            [JsonProperty(En ? "Can an Npc attack a non-owner of the event? [true/false]" : "Может ли Npc атаковать не владельца ивента? [true/false]")] public bool TargetNpc { get; set; }
            [JsonProperty(En ? "Allow the non-owner of the event to enter the event zone? [true/false]" : "Разрешать входить внутрь зоны ивента не владельцу ивента? [true/false]")] public bool CanEnter { get; set; }
            [JsonProperty(En ? "Allow a player who has an active cooldown of the Event Owner to enter the event zone? [true/false]" : "Разрешать входить внутрь зоны ивента игроку, у которого активен кулдаун на получение статуса владельца ивента? [true/false]")] public bool CanEnterCooldownPlayer { get; set; }
            [JsonProperty(En ? "The time that the Event Owner may not be inside the event zone [sec.]" : "Время, которое владелец ивента может не находиться внутри зоны ивента [сек.]")] public int TimeExitOwner { get; set; }
            [JsonProperty(En ? "The time until the end of Event Owner status when it is necessary to warn the player [sec.]" : "Время таймера до окончания действия статуса владельца ивента, когда необходимо предупредить игрока [сек.]")] public int AlertTime { get; set; }
            [JsonProperty(En ? "Prevent the actions of the RestoreUponDeath plugin in the event zone? [true/false]" : "Запрещать работу плагина RestoreUponDeath в зоне действия ивента? [true/false]")] public bool RestoreUponDeath { get; set; }
            [JsonProperty(En ? "The time that the player can`t become the Event Owner, after the end of the event and the player was its owner [sec.]" : "Время, которое игрок не сможет стать владельцем ивента, после того как ивент окончен и игрок был его владельцем [sec.]")] public double CooldownOwner { get; set; }
            [JsonProperty(En ? "Darkening the dome (0 - disables the dome)" : "Затемнение купола (0 - отключает купол)")] public int Darkening { get; set; }
        }

        public class NpcBelt
        {
            [JsonProperty("ShortName")] public string ShortName { get; set; }
            [JsonProperty(En ? "Amount" : "Кол-во")] public int Amount { get; set; }
            [JsonProperty("SkinID (0 - default)")] public ulong SkinID { get; set; }
            [JsonProperty(En ? "Mods" : "Модификации на оружие")] public HashSet<string> Mods { get; set; }
            [JsonProperty(En ? "Ammo" : "Боеприпасы")] public string Ammo { get; set; }
        }

        public class NpcWear
        {
            [JsonProperty("ShortName")] public string ShortName { get; set; }
            [JsonProperty("SkinID (0 - default)")] public ulong SkinID { get; set; }
        }

        public class PresetConfig
        {
            [JsonProperty(En ? "Minimum" : "Минимальное кол-во")] public int Min { get; set; }
            [JsonProperty(En ? "Maximum" : "Максимальное кол-во")] public int Max { get; set; }
            [JsonProperty(En ? "List of locations" : "Список расположений")] public List<string> Positions { get; set; }
            [JsonProperty(En ? "NPCs setting" : "Настройки NPC")] public NpcConfig Config { get; set; }
            [JsonProperty(En ? "Which loot table should the plugin use? (0 - default; 1 - own; 2 - AlphaLoot; 3 - CustomLoot; 4 - loot table of the Rust objects; 5 - combine the 1 and 4 methods)" : "Какую таблицу предметов необходимо использовать? (0 - стандартную; 1 - собственную; 2 - AlphaLoot; 3 - CustomLoot; 4 - таблица предметов объектов Rust; 5 - совместить 1 и 4 методы)")] public int TypeLootTable { get; set; }
            [JsonProperty(En ? "Loot table from prefabs (if the loot table type is 4 or 5)" : "Таблица предметов из prefab-ов (если тип таблицы предметов - 4 или 5)")] public PrefabLootTableConfig PrefabLootTable { get; set; }
            [JsonProperty(En ? "Own loot table (if the loot table type is 1 or 5)" : "Собственная таблица предметов (если тип таблицы предметов - 1 или 5)")] public LootTableConfig OwnLootTable { get; set; }
        }

        public class NpcConfig
        {
            [JsonProperty(En ? "Name" : "Название")] public string Name { get; set; }
            [JsonProperty(En ? "Health" : "Кол-во ХП")] public float Health { get; set; }
            [JsonProperty(En ? "Attack Range Multiplier" : "Множитель радиуса атаки")] public float AttackRangeMultiplier { get; set; }
            [JsonProperty(En ? "Sense Range" : "Радиус обнаружения цели")] public float SenseRange { get; set; }
            [JsonProperty(En ? "Target Memory Duration [sec.]" : "Длительность памяти цели [sec.]")] public float MemoryDuration { get; set; }
            [JsonProperty(En ? "Scale damage" : "Множитель урона")] public float DamageScale { get; set; }
            [JsonProperty(En ? "Aim Cone Scale" : "Множитель разброса")] public float AimConeScale { get; set; }
            [JsonProperty(En ? "Detect the target only in the NPC's viewing vision cone? [true/false]" : "Обнаруживать цель только в углу обзора NPC? [true/false]")] public bool CheckVisionCone { get; set; }
            [JsonProperty(En ? "Vision Cone" : "Угол обзора")] public float VisionCone { get; set; }
            [JsonProperty(En ? "Disable radio effects? [true/false]" : "Отключать эффекты рации? [true/false]")] public bool DisableRadio { get; set; }
            [JsonProperty(En ? "Remove a corpse after death? (it is recommended to use the true value to improve performance) [true/false]" : "Удалять труп после смерти? (рекомендуется использовать значение true для повышения производительности) [true/false]")] public bool IsRemoveCorpse { get; set; }
            [JsonProperty(En ? "Wear items" : "Одежда")] public HashSet<NpcWear> WearItems { get; set; }
            [JsonProperty(En ? "Belt items" : "Быстрые слоты")] public HashSet<NpcBelt> BeltItems { get; set; }
            [JsonProperty(En ? "Kit (it is recommended to use the previous 2 settings to improve performance)" : "Kit (рекомендуется использовать предыдущие 2 пункта настройки для повышения производительности)")] public string Kit { get; set; }
        }

        public class EconomyConfig
        {
            [JsonProperty(En ? "Which economy plugins do you want to use? (Economics, Server Rewards, IQEconomic)" : "Какие плагины экономики вы хотите использовать? (Economics, Server Rewards, IQEconomic)")] public HashSet<string> Plugins { get; set; }
            [JsonProperty(En ? "The minimum value that a player must collect to get points for the economy" : "Минимальное значение, которое игрок должен заработать, чтобы получить баллы за экономику")] public double Min { get; set; }
            [JsonProperty(En ? "Looting of crates" : "Ограбление ящиков")] public Dictionary<string, double> Crates { get; set; }
            [JsonProperty(En ? "Destruction of doors" : "Уничтожение дверей")] public Dictionary<string, double> Doors { get; set; }
            [JsonProperty(En ? "Killing an NPC" : "Убийство NPC")] public double Npc { get; set; }
            [JsonProperty(En ? "Hacking a locked crate" : "Взлом заблокированного ящика")] public double LockedCrate { get; set; }
            [JsonProperty(En ? "Opening a security door with a card" : "Открытие двери карточкой")] public Dictionary<string, double> Cards { get; set; }
            [JsonProperty(En ? "List of commands that are executed in the console at the end of the event ({steamid} - the player who collected the highest number of points)" : "Список команд, которые выполняются в консоли по окончанию ивента ({steamid} - игрок, который набрал наибольшее кол-во баллов)")] public HashSet<string> Commands { get; set; }
        }

        public class CoordConfig
        {
            [JsonProperty(En ? "Position" : "Позиция")] public string Position { get; set; }
            [JsonProperty(En ? "Rotation" : "Вращение")] public string Rotation { get; set; }
        }

        public class DoorsToScientists
        {
            [JsonProperty(En ? "Position of doors (not edited)" : "Координаты дверей (не редактируется)")] public HashSet<string> Doors { get; set; }
            [JsonProperty(En ? "NPCs setting" : "Настройка NPC")] public HashSet<PresetConfig> Scientists { get; set; }
        }

        public class TurretConfig
        {
            [JsonProperty(En ? "Can Auto Turret appear? [true/false]" : "Использовать турели? [true/false]")] public bool IsTurret { get; set; }
            [JsonProperty(En ? "Health" : "Кол-во ХП")] public float Hp { get; set; }
            [JsonProperty(En ? "Weapon ShortName" : "ShortName оружия")] public string ShortNameWeapon { get; set; }
            [JsonProperty(En ? "Ammo ShortName" : "ShortName патронов")] public string ShortNameAmmo { get; set; }
            [JsonProperty(En ? "Number of ammo" : "Кол-во патронов")] public int CountAmmo { get; set; }
        }

        private class PluginConfig
        {
            [JsonProperty(En ? "Minimum time between events [sec.]" : "Минимальное время между ивентами [sec.]")] public float MinStartTime { get; set; }
            [JsonProperty(En ? "Maximum time between events [sec.]" : "Максимальное время между ивентами [sec.]")] public float MaxStartTime { get; set; }
            [JsonProperty(En ? "Is active the timer on to start the event? [true/false]" : "Активен ли таймер для запуска ивента? [true/false]")] public bool EnabledTimer { get; set; }
            [JsonProperty(En ? "Duration of the event [sec.]" : "Время проведения ивента [sec.]")] public int FinishTime { get; set; }
            [JsonProperty(En ? "Time before the starting of the event after receiving a chat message [sec.]" : "Время до начала ивента после сообщения в чате [sec.]")] public float PreStartTime { get; set; }
            [JsonProperty(En ? "Notification time until the end of the event [sec.]" : "Время оповещения до окончания ивента [sec.]")] public int PreFinishTime { get; set; }
            [JsonProperty(En ? "Time to spawn each object during a submarine appears on the map [sec.]" : "Время для спавна каждого объекта при появлении подводной лодки на карте [sec.]")] public float Delay { get; set; }
            [JsonProperty(En ? "Deployed Crates setting" : "Настройка появляемого лута в шкафах для переодевания, маленьких/больших ящиках и ящиках для хранения")] public HashSet<EntityCrateConfig> EntityCrates { get; set; }
            [JsonProperty(En ? "Crates setting" : "Настройка ящиков")] public HashSet<CrateConfig> DefaultCrates { get; set; }
            [JsonProperty(En ? "Locked Crates setting" : "Настройка заблокированных ящиков")] public HackCrateConfig HackCrate { get; set; }
            [JsonProperty(En ? "Marker configuration on the map" : "Настройка маркера на карте")] public MarkerConfig Marker { get; set; }
            [JsonProperty(En ? "Prefix of chat messages" : "Префикс сообщений в чате")] public string Prefix { get; set; }
            [JsonProperty(En ? "Do you use the chat? [true/false]" : "Использовать ли чат? [true/false]")] public bool IsChat { get; set; }
            [JsonProperty(En ? "GUI setting" : "Настройки GUI")] public GuiConfig Gui { get; set; }
            [JsonProperty(En ? "GUI Announcements setting" : "Настройка GUI Announcements")] public GuiAnnouncementsConfig GuiAnnouncements { get; set; }
            [JsonProperty(En ? "Notify setting" : "Настройка Notify")] public NotifyConfig Notify { get; set; }
            [JsonProperty(En ? "Discord setting (only for users DiscordMessages plugin)" : "Настройка оповещений в Discord (только для тех, кто использует плагин DiscordMessages)")] public DiscordConfig Discord { get; set; }
            [JsonProperty(En ? "Radius of the event zone" : "Радиус зоны ивента")] public float Radius { get; set; }
            [JsonProperty(En ? "Do you create a PVP zone in the event area? (only for users TruePVE plugin) [true/false]" : "Создавать зону PVP в зоне проведения ивента? (только для тех, кто использует плагин TruePVE) [true/false]")] public bool IsCreateZonePvp { get; set; }
            [JsonProperty(En ? "PVE Mode Setting (only for users PveMode plugin)" : "Настройка PVE режима работы плагина (только для тех, кто использует плагин PveMode)")] public PveModeConfig PveMode { get; set; }
            [JsonProperty(En ? "Interrupt the teleport in a submarine? (only for users NTeleportation plugin) [true/false]" : "Запрещать телепорт на подводной лодке? (только для тех, кто использует плагин NTeleportation) [true/false]")] public bool NTeleportationInterrupt { get; set; }
            [JsonProperty(En ? "NPCs setting outside" : "Настройка NPC снаружи")] public HashSet<PresetConfig> OutsideNpc { get; set; }
            [JsonProperty(En ? "List of doors and NPCs inside the submarine" : "Список дверей и NPC внутри подводной лодки")] public HashSet<DoorsToScientists> DoorsToScientists { get; set; }
            [JsonProperty(En ? "Economy setting (total values will be added up and rewarded at the end of the event)" : "Настройка экономики (конечное значение суммируется и будет выдано игрокам по окончанию ивента)")] public EconomyConfig Economy { get; set; }
            [JsonProperty(En ? "Setting the doors damage coefficient" : "Настройка коэффициента наносимого урона по дверям")] public Dictionary<string, float> ScaleDamage { get; set; }
            [JsonProperty(En ? "Door Health" : "Кол-во HP дверей")] public float DoorHealth { get; set; }
            [JsonProperty(En ? "Distance from the submarine to the building block" : "Расстояние от подводной лодки до постройки игрока")] public float DistanceToBlock { get; set; }
            [JsonProperty(En ? "Do Workbenches work in a submarine? [true/false]" : "Работают ли верстаки на подводной лодке? [true/false]")] public bool IsWorkbench { get; set; }
            [JsonProperty(En ? "Do Repair Bench work in a submarine? [true/false]" : "Работают ли ремонтные верстаки на подводной лодке? [true/false]")] public bool IsRepairBench { get; set; }
            [JsonProperty(En ? "Do Research Table work in a submarine? [true/false]" : "Работают ли столы для исследования на подводной лодке? [true/false]")] public bool IsResearchTable { get; set; }
            [JsonProperty(En ? "Do Mixing Table work in a submarine? [true/false]" : "Работают ли столы для смешивания на подводной лодке? [true/false]")] public bool IsMixingTable { get; set; }
            [JsonProperty(En ? "Do Locker work in a submarine? [true/false]" : "Работают ли шкафы для переодевания на подводной лодке? [true/false]")] public bool IsLocker { get; set; }
            [JsonProperty(En ? "Do Storage Box work in a submarine? [true/false]" : "Работают ли ящики на подводной лодке? [true/false]")] public bool IsBoxStorage { get; set; }
            [JsonProperty(En ? "Setting AutoTurrets" : "Настройка турелей")] public TurretConfig Turret { get; set; }
            [JsonProperty(En ? "List of commands banned in the event zone" : "Список команд запрещенных в зоне ивента")] public HashSet<string> Commands { get; set; }
            [JsonProperty(En ? "Configuration version" : "Версия конфигурации")] public VersionNumber PluginVersion { get; set; }

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    MinStartTime = 10800f,
                    MaxStartTime = 10800f,
                    EnabledTimer = true,
                    FinishTime = 5400,
                    PreStartTime = 600f,
                    PreFinishTime = 300,
                    Delay = 0.001f,
                    EntityCrates = new HashSet<EntityCrateConfig>
                    {
                        new EntityCrateConfig
                        {
                            Prefab = "assets/prefabs/deployable/locker/locker.deployed.prefab",
                            IsOwnLootTable = false,
                            LootTable = new LootTableConfig
                            {
                                Min = 0,
                                Max = 1,
                                Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 100, MaxAmount = 200, Chance = 50.0f, IsBluePrint = false, SkinID = 0, Name = "" } }
                            }
                        },
                        new EntityCrateConfig
                        {
                            Prefab = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab",
                            IsOwnLootTable = false,
                            LootTable = new LootTableConfig
                            {
                                Min = 1,
                                Max = 1,
                                Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 100, MaxAmount = 200, Chance = 50.0f, IsBluePrint = false, SkinID = 0, Name = "" } }
                            }
                        },
                        new EntityCrateConfig
                        {
                            Prefab = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab",
                            IsOwnLootTable = false,
                            LootTable = new LootTableConfig
                            {
                                Min = 1,
                                Max = 1,
                                Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 100, MaxAmount = 200, Chance = 50.0f, IsBluePrint = false, SkinID = 0, Name = "" } }
                            }
                        }
                    },
                    DefaultCrates = new HashSet<CrateConfig>
                    {
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/underwater_labs/tech_parts_1.prefab",
                            TypeLootTable = 5,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 50.0f, PrefabDefinition = "assets/bundled/prefabs/radtown/underwater_labs/tech_parts_1.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 3, Max = 3, UseCount = true,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig { ShortName = "explosive.timed", MinAmount = 1, MaxAmount = 2, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "ammo.rocket.basic", MinAmount = 2, MaxAmount = 4, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "scrap", MinAmount = 8, MaxAmount = 8, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" }
                                }
                            },
                            ScaleItems = new Dictionary<string, float> { ["explosive.timed"] = 1f }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/underwater_labs/tech_parts_2.prefab",
                            TypeLootTable = 5,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/underwater_labs/tech_parts_2.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 3, Max = 3, UseCount = true,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig { ShortName = "explosive.timed", MinAmount = 1, MaxAmount = 2, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "ammo.rocket.basic", MinAmount = 2, MaxAmount = 4, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "scrap", MinAmount = 8, MaxAmount = 8, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" }
                                }
                            },
                            ScaleItems = new Dictionary<string, float> { ["explosive.timed"] = 1f }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/underwater_labs/crate_ammunition.prefab",
                            TypeLootTable = 5,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/underwater_labs/crate_ammunition.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 2, Max = 2, UseCount = true,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig { ShortName = "explosive.timed", MinAmount = 1, MaxAmount = 2, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "ammo.rocket.basic", MinAmount = 2, MaxAmount = 4, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" }
                                }
                            },
                            ScaleItems = new Dictionary<string, float> { ["explosive.timed"] = 1f }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/underwater_labs/crate_fuel.prefab",
                            TypeLootTable = 5,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/underwater_labs/crate_fuel.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 2, Max = 2, UseCount = true,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig { ShortName = "explosive.timed", MinAmount = 1, MaxAmount = 2, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "ammo.rocket.basic", MinAmount = 2, MaxAmount = 4, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" }
                                }
                            },
                            ScaleItems = new Dictionary<string, float> { ["explosive.timed"] = 1f }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/underwater_labs/crate_tools.prefab",
                            TypeLootTable = 5,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/underwater_labs/crate_tools.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 3, Max = 3, UseCount = true,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig { ShortName = "explosive.timed", MinAmount = 1, MaxAmount = 2, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "ammo.rocket.basic", MinAmount = 2, MaxAmount = 4, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 5, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" }
                                }
                            },
                            ScaleItems = new Dictionary<string, float> { ["explosive.timed"] = 1f }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/crate_underwater_basic.prefab",
                            TypeLootTable = 5,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/crate_underwater_basic.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 2, Max = 2, UseCount = true,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig { ShortName = "explosive.timed", MinAmount = 1, MaxAmount = 2, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "ammo.rocket.basic", MinAmount = 2, MaxAmount = 4, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" }
                                }
                            },
                            ScaleItems = new Dictionary<string, float> { ["explosive.timed"] = 1f }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/crate_underwater_advanced.prefab",
                            TypeLootTable = 5,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/crate_underwater_advanced.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 2, Max = 2, UseCount = true,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig { ShortName = "explosive.timed", MinAmount = 1, MaxAmount = 2, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "ammo.rocket.basic", MinAmount = 2, MaxAmount = 4, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" }
                                }
                            },
                            ScaleItems = new Dictionary<string, float> { ["explosive.timed"] = 1f }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/underwater_labs/crate_medical.prefab",
                            TypeLootTable = 5,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/underwater_labs/crate_medical.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 2, Max = 2, UseCount = true,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig { ShortName = "explosive.timed", MinAmount = 1, MaxAmount = 2, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "ammo.rocket.basic", MinAmount = 2, MaxAmount = 4, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" }
                                }
                            },
                            ScaleItems = new Dictionary<string, float> { ["explosive.timed"] = 1f }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/crate_elite.prefab",
                            TypeLootTable = 5,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/crate_elite.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 3, Max = 3, UseCount = true,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig { ShortName = "explosive.timed", MinAmount = 2, MaxAmount = 4, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "ammo.rocket.basic", MinAmount = 4, MaxAmount = 8, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "scrap", MinAmount = 25, MaxAmount = 25, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" }
                                }
                            },
                            ScaleItems = new Dictionary<string, float> { ["explosive.timed"] = 1f }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/underwater_labs/crate_elite.prefab",
                            TypeLootTable = 5,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/underwater_labs/crate_elite.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 3, Max = 3, UseCount = true,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig { ShortName = "explosive.timed", MinAmount = 2, MaxAmount = 4, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "ammo.rocket.basic", MinAmount = 4, MaxAmount = 8, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "scrap", MinAmount = 25, MaxAmount = 25, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" }
                                }
                            },
                            ScaleItems = new Dictionary<string, float> { ["explosive.timed"] = 1f }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/underwater_labs/crate_food_1.prefab",
                            TypeLootTable = 5,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/underwater_labs/crate_food_1.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 2, Max = 2, UseCount = true,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig { ShortName = "explosive.timed", MinAmount = 1, MaxAmount = 2, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "ammo.rocket.basic", MinAmount = 2, MaxAmount = 4, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" }
                                }
                            },
                            ScaleItems = new Dictionary<string, float> { ["explosive.timed"] = 1f }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/underwater_labs/crate_food_2.prefab",
                            TypeLootTable = 5,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/underwater_labs/crate_food_2.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 2, Max = 2, UseCount = true,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig { ShortName = "explosive.timed", MinAmount = 1, MaxAmount = 2, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "ammo.rocket.basic", MinAmount = 2, MaxAmount = 4, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" }
                                }
                            },
                            ScaleItems = new Dictionary<string, float> { ["explosive.timed"] = 1f }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/crate_normal.prefab",
                            TypeLootTable = 5,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/crate_normal.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 3, Max = 3, UseCount = true,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig { ShortName = "explosive.timed", MinAmount = 1, MaxAmount = 2, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "ammo.rocket.basic", MinAmount = 2, MaxAmount = 4, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 5, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" }
                                }
                            },
                            ScaleItems = new Dictionary<string, float> { ["explosive.timed"] = 1f }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/underwater_labs/crate_normal.prefab",
                            TypeLootTable = 5,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/underwater_labs/crate_normal.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 3, Max = 3, UseCount = true,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig { ShortName = "explosive.timed", MinAmount = 1, MaxAmount = 2, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "ammo.rocket.basic", MinAmount = 2, MaxAmount = 4, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "scrap", MinAmount = 8, MaxAmount = 8, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" }
                                }
                            },
                            ScaleItems = new Dictionary<string, float> { ["explosive.timed"] = 1f }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/crate_normal_2.prefab",
                            TypeLootTable = 5,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/crate_normal_2.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 3, Max = 3, UseCount = true,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig { ShortName = "explosive.timed", MinAmount = 1, MaxAmount = 2, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "ammo.rocket.basic", MinAmount = 2, MaxAmount = 4, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "scrap", MinAmount = 8, MaxAmount = 8, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" }
                                }
                            },
                            ScaleItems = new Dictionary<string, float> { ["explosive.timed"] = 1f }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/underwater_labs/crate_normal_2.prefab",
                            TypeLootTable = 5,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/underwater_labs/crate_normal_2.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 3, Max = 3, UseCount = true,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig { ShortName = "explosive.timed", MinAmount = 1, MaxAmount = 2, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "ammo.rocket.basic", MinAmount = 2, MaxAmount = 4, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 5, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" }
                                }
                            },
                            ScaleItems = new Dictionary<string, float> { ["explosive.timed"] = 1f }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/crate_normal_2_food.prefab",
                            TypeLootTable = 5,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/crate_normal_2_food.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 2, Max = 2, UseCount = true,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig { ShortName = "explosive.timed", MinAmount = 1, MaxAmount = 2, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "ammo.rocket.basic", MinAmount = 2, MaxAmount = 4, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" }
                                }
                            },
                            ScaleItems = new Dictionary<string, float> { ["explosive.timed"] = 1f }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/crate_normal_2_medical.prefab",
                            TypeLootTable = 5,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/crate_normal_2_medical.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 2, Max = 2, UseCount = true,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig { ShortName = "explosive.timed", MinAmount = 1, MaxAmount = 2, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "ammo.rocket.basic", MinAmount = 2, MaxAmount = 4, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" }
                                }
                            },
                            ScaleItems = new Dictionary<string, float> { ["explosive.timed"] = 1f }
                        }
                    },
                    HackCrate = new HackCrateConfig
                    {
                        Coordinates = new HashSet<CoordConfig>
                        {
                            new CoordConfig { Position = "(-3.768, 3.1, 22.509)", Rotation = "(0, 90, 0)" },
                            new CoordConfig { Position = "(3.028, 0.082, 5.223)", Rotation = "(0, 0, 0)" }
                        },
                        UnlockTime = 600f,
                        IncreaseEventTime = true,
                        TypeLootTable = 5,
                        PrefabLootTable = new PrefabLootTableConfig
                        {
                            Min = 1,
                            Max = 1,
                            UseCount = true,
                            Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100.0f, PrefabDefinition = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab" } }
                        },
                        OwnLootTable = new LootTableConfig
                        {
                            Min = 2,
                            Max = 2,
                            UseCount = true,
                            Items = new List<ItemConfig>
                            {
                                new ItemConfig { ShortName = "explosive.timed", MinAmount = 4, MaxAmount = 8, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" },
                                new ItemConfig { ShortName = "ammo.rocket.basic", MinAmount = 8, MaxAmount = 16, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" }
                            }
                        },
                        ScaleItems = new Dictionary<string, float> { ["explosive.timed"] = 1f },
                    },
                    Marker = new MarkerConfig
                    {
                        Name = "WaterEvent ({time})",
                        Radius = 0.4f,
                        Alpha = 0.6f,
                        Color = new ColorConfig { R = 0.81f, G = 0.25f, B = 0.15f }
                    },
                    Prefix = "[WaterEvent]",
                    IsChat = true,
                    Gui = new GuiConfig
                    {
                        IsGui = true,
                        OffsetMinY = "-56"
                    },
                    GuiAnnouncements = new GuiAnnouncementsConfig
                    {
                        IsGuiAnnouncements = false,
                        BannerColor = "Orange",
                        TextColor = "White",
                        ApiAdjustVPosition = 0.03f
                    },
                    Notify = new NotifyConfig
                    {
                        IsNotify = false,
                        Type = "0"
                    },
                    Discord = new DiscordConfig
                    {
                        IsDiscord = false,
                        WebhookUrl = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks",
                        EmbedColor = 13516583,
                        Keys = new HashSet<string>
                        {
                            "PreStart",
                            "Start",
                            "PreFinish",
                            "Finish",
                            "OpenBlueDoor",
                            "OpenRedDoor",
                            "HackCrate"
                        }
                    },
                    Radius = 70f,
                    IsCreateZonePvp = false,
                    PveMode = new PveModeConfig
                    {
                        Pve = false,
                        Damage = 500f,
                        ScaleDamage = new HashSet<ScaleDamageConfig>
                        {
                            new ScaleDamageConfig { Type = "NPC", Scale = 1f }
                        },
                        LootCrate = false,
                        HackCrate = false,
                        LootNpc = false,
                        DamageNpc = false,
                        TargetNpc = false,
                        CanEnter = false,
                        CanEnterCooldownPlayer = true,
                        TimeExitOwner = 450,
                        AlertTime = 120,
                        RestoreUponDeath = true,
                        CooldownOwner = 86400,
                        Darkening = 12
                    },
                    NTeleportationInterrupt = false,
                    OutsideNpc = new HashSet<PresetConfig>
                    {
                        new PresetConfig
                        {
                            Min = 4,
                            Max = 4,
                            Positions = new List<string>
                            {
                                "(0.0, 8.8, -9.1)",
                                "(2.4, 8.4, 19.5)",
                                "(-2.2, 8.4, 19.4)",
                                "(0.1, 8.8, -18.3)",
                                "(-0.1, 8.8, 39.7)",
                                "(-0.1, 8.8, 0.1)"
                            },
                            Config = new NpcConfig
                            {
                                Name = "SeaDevil",
                                Health = 120f,
                                AttackRangeMultiplier = 5f,
                                SenseRange = 75f,
                                MemoryDuration = 20f,
                                DamageScale = 0.7f,
                                AimConeScale = 0f,
                                CheckVisionCone = false,
                                VisionCone = 135f,
                                DisableRadio = true,
                                IsRemoveCorpse = true,
                                WearItems = new HashSet<NpcWear>
                                {
                                    new NpcWear { ShortName = "burlap.shirt", SkinID = 2216143685 },
                                    new NpcWear { ShortName = "burlap.trousers", SkinID = 2216144342 },
                                    new NpcWear { ShortName = "shoes.boots", SkinID = 0 },
                                    new NpcWear { ShortName = "burlap.gloves", SkinID = 0 },
                                    new NpcWear { ShortName = "hat.dragonmask", SkinID = 0 }
                                },
                                BeltItems = new HashSet<NpcBelt>
                                {
                                    new NpcBelt { ShortName = "rifle.m39", Amount = 1, SkinID = 0, Mods = new HashSet<string> { "weapon.mod.flashlight", "weapon.mod.small.scope", "weapon.mod.silencer" }, Ammo = string.Empty },
                                    new NpcBelt { ShortName = "syringe.medical", Amount = 5, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty },
                                    new NpcBelt { ShortName = "grenade.f1", Amount = 2, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty },
                                    new NpcBelt { ShortName = "grenade.smoke", Amount = 1, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
                                },
                                Kit = ""
                            },
                            TypeLootTable = 5,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 50f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "syringe.medical", MinAmount = 1, MaxAmount = 2, Chance = 70.0f, IsBluePrint = false, SkinID = 0, Name = "" }
                                }
                            }
                        },
                        new PresetConfig
                        {
                            Min = 1,
                            Max = 1,
                            Positions = new List<string>
                            {
                                "(12.3, 0.6, -0.7)",
                                "(-12.3, 0.6, -0.9)"
                            },
                            Config = new NpcConfig
                            {
                                Name = "SeaDevil",
                                Health = 200f,
                                AttackRangeMultiplier = 5f,
                                SenseRange = 150f,
                                MemoryDuration = 10f,
                                DamageScale = 0.2f,
                                AimConeScale = 0f,
                                CheckVisionCone = false,
                                VisionCone = 135f,
                                DisableRadio = true,
                                IsRemoveCorpse = true,
                                WearItems = new HashSet<NpcWear>
                                {
                                    new NpcWear { ShortName = "burlap.shirt", SkinID = 2216143685 },
                                    new NpcWear { ShortName = "burlap.trousers", SkinID = 2216144342 },
                                    new NpcWear { ShortName = "shoes.boots", SkinID = 0 },
                                    new NpcWear { ShortName = "burlap.gloves", SkinID = 0 },
                                    new NpcWear { ShortName = "hat.dragonmask", SkinID = 0 }
                                },
                                BeltItems = new HashSet<NpcBelt>
                                {
                                    new NpcBelt { ShortName = "rifle.bolt", Amount = 1, SkinID = 0, Mods = new HashSet<string> { "weapon.mod.flashlight", "weapon.mod.small.scope", "weapon.mod.silencer" }, Ammo = string.Empty }
                                },
                                Kit = ""
                            },
                            TypeLootTable = 5,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 50f, IsBluePrint = false, SkinID = 0, Name = "" },
                                    new ItemConfig { ShortName = "syringe.medical", MinAmount = 1, MaxAmount = 2, Chance = 70.0f, IsBluePrint = false, SkinID = 0, Name = "" }
                                }
                            }
                        }
                    },
                    DoorsToScientists = new HashSet<DoorsToScientists>
                    {
                        new DoorsToScientists
                        {
                            Doors = new HashSet<string>
                            {
                                "(-6.568, 5.511, -19.545)",
                                "(-1.5, 3, -18)"
                            },
                            Scientists = new HashSet<PresetConfig>
                            {
                                new PresetConfig
                                {
                                    Min = 1,
                                    Max = 1,
                                    Positions = new List<string>
                                    {
                                        "(-3.0, 3.1, -18.3)"
                                    },
                                    Config = new NpcConfig
                                    {
                                        Name = "Mariner",
                                        Health = 125f,
                                        AttackRangeMultiplier = 1f,
                                        SenseRange = 30f,
                                        MemoryDuration = 20f,
                                        DamageScale = 1.35f,
                                        AimConeScale = 1f,
                                        CheckVisionCone = false,
                                        VisionCone = 135f,
                                        DisableRadio = true,
                                        IsRemoveCorpse = true,
                                        WearItems = new HashSet<NpcWear>
                                        {
                                            new NpcWear { ShortName = "metal.facemask", SkinID = 2296503845 },
                                            new NpcWear { ShortName = "hoodie", SkinID = 2304560839 },
                                            new NpcWear { ShortName = "pants", SkinID = 2304559261 },
                                            new NpcWear { ShortName = "shoes.boots", SkinID = 0 }
                                        },
                                        BeltItems = new HashSet<NpcBelt>
                                        {
                                            new NpcBelt { ShortName = "rifle.lr300", Amount = 1, SkinID = 0, Mods = new HashSet<string> { "weapon.mod.flashlight", "weapon.mod.holosight" }, Ammo = string.Empty },
                                            new NpcBelt { ShortName = "syringe.medical", Amount = 5, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty },
                                            new NpcBelt { ShortName = "grenade.f1", Amount = 1, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
                                        },
                                        Kit = ""
                                    },
                                    TypeLootTable = 5,
                                    PrefabLootTable = new PrefabLootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                                    },
                                    OwnLootTable = new LootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Items = new List<ItemConfig>
                                        {
                                            new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 50f, IsBluePrint = false, SkinID = 0, Name = "" },
                                            new ItemConfig { ShortName = "syringe.medical", MinAmount = 1, MaxAmount = 2, Chance = 70.0f, IsBluePrint = false, SkinID = 0, Name = "" }
                                        }
                                    }
                                }
                            }
                        },
                        new DoorsToScientists
                        {
                            Doors = new HashSet<string>
                            {
                                "(-1.5, 3, -18)",
                                "(1.5, 3, -18)",
                                "(0, 3, -16.5)"
                            },
                            Scientists = new HashSet<PresetConfig>
                            {
                                new PresetConfig
                                {
                                    Min = 1,
                                    Max = 1,
                                    Positions = new List<string>
                                    {
                                        "(0.0, 3.1, -17.9)"
                                    },
                                    Config = new NpcConfig
                                    {
                                        Name = "Mariner",
                                        Health = 125f,
                                        AttackRangeMultiplier = 1f,
                                        SenseRange = 30f,
                                        MemoryDuration = 20f,
                                        DamageScale = 1.35f,
                                        AimConeScale = 1f,
                                        CheckVisionCone = false,
                                        VisionCone = 135f,
                                        DisableRadio = true,
                                        IsRemoveCorpse = true,
                                        WearItems = new HashSet<NpcWear>
                                        {
                                            new NpcWear { ShortName = "metal.facemask", SkinID = 2296503845 },
                                            new NpcWear { ShortName = "hoodie", SkinID = 2304560839 },
                                            new NpcWear { ShortName = "pants", SkinID = 2304559261 },
                                            new NpcWear { ShortName = "shoes.boots", SkinID = 0 }
                                        },
                                        BeltItems = new HashSet<NpcBelt>
                                        {
                                            new NpcBelt { ShortName = "rifle.lr300", Amount = 1, SkinID = 0, Mods = new HashSet<string> { "weapon.mod.flashlight", "weapon.mod.holosight" }, Ammo = string.Empty },
                                            new NpcBelt { ShortName = "syringe.medical", Amount = 5, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty },
                                            new NpcBelt { ShortName = "grenade.f1", Amount = 1, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
                                        },
                                        Kit = ""
                                    },
                                    TypeLootTable = 5,
                                    PrefabLootTable = new PrefabLootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                                    },
                                    OwnLootTable = new LootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Items = new List<ItemConfig>
                                        {
                                            new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 50f, IsBluePrint = false, SkinID = 0, Name = "" },
                                            new ItemConfig { ShortName = "syringe.medical", MinAmount = 1, MaxAmount = 2, Chance = 70.0f, IsBluePrint = false, SkinID = 0, Name = "" }
                                        }
                                    }
                                }
                            }
                        },
                        new DoorsToScientists
                        {
                            Doors = new HashSet<string>
                            {
                                "(1.5, 3, -18)",
                                "(6.568, 5.511, -19.5)"
                            },
                            Scientists = new HashSet<PresetConfig>
                            {
                                new PresetConfig
                                {
                                    Min = 1,
                                    Max = 1,
                                    Positions = new List<string>
                                    {
                                        "(2.9, 3.1, -18.3)"
                                    },
                                    Config = new NpcConfig
                                    {
                                        Name = "Mariner",
                                        Health = 125f,
                                        AttackRangeMultiplier = 1f,
                                        SenseRange = 30f,
                                        MemoryDuration = 20f,
                                        DamageScale = 1.35f,
                                        AimConeScale = 1f,
                                        CheckVisionCone = false,
                                        VisionCone = 135f,
                                        DisableRadio = true,
                                        IsRemoveCorpse = true,
                                        WearItems = new HashSet<NpcWear>
                                        {
                                            new NpcWear { ShortName = "metal.facemask", SkinID = 2296503845 },
                                            new NpcWear { ShortName = "hoodie", SkinID = 2304560839 },
                                            new NpcWear { ShortName = "pants", SkinID = 2304559261 },
                                            new NpcWear { ShortName = "shoes.boots", SkinID = 0 }
                                        },
                                        BeltItems = new HashSet<NpcBelt>
                                        {
                                            new NpcBelt { ShortName = "rifle.lr300", Amount = 1, SkinID = 0, Mods = new HashSet<string> { "weapon.mod.flashlight", "weapon.mod.holosight" }, Ammo = string.Empty },
                                            new NpcBelt { ShortName = "syringe.medical", Amount = 5, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty },
                                            new NpcBelt { ShortName = "grenade.f1", Amount = 1, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
                                        },
                                        Kit = ""
                                    },
                                    TypeLootTable = 5,
                                    PrefabLootTable = new PrefabLootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                                    },
                                    OwnLootTable = new LootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Items = new List<ItemConfig>
                                        {
                                            new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 50f, IsBluePrint = false, SkinID = 0, Name = "" },
                                            new ItemConfig { ShortName = "syringe.medical", MinAmount = 1, MaxAmount = 2, Chance = 70.0f, IsBluePrint = false, SkinID = 0, Name = "" }
                                        }
                                    }
                                }
                            }
                        },
                        new DoorsToScientists
                        {
                            Doors = new HashSet<string>
                            {
                                "(0, 3, -16.5)",
                                "(0, 3, -7.5)",
                                "(0, 3, -10.5)"
                            },
                            Scientists = new HashSet<PresetConfig>
                            {
                                new PresetConfig
                                {
                                    Min = 3,
                                    Max = 3,
                                    Positions = new List<string>
                                    {
                                        "(-3.1, 3.1, -14.5)",
                                        "(3.5, 3.1, -14.5)",
                                        "(0.0, 3.1, -9.2)"
                                    },
                                    Config = new NpcConfig
                                    {
                                        Name = "Mariner",
                                        Health = 125f,
                                        AttackRangeMultiplier = 1f,
                                        SenseRange = 30f,
                                        MemoryDuration = 20f,
                                        DamageScale = 1.35f,
                                        AimConeScale = 1f,
                                        CheckVisionCone = false,
                                        VisionCone = 135f,
                                        DisableRadio = true,
                                        IsRemoveCorpse = true,
                                        WearItems = new HashSet<NpcWear>
                                        {
                                            new NpcWear { ShortName = "metal.facemask", SkinID = 2296503845 },
                                            new NpcWear { ShortName = "hoodie", SkinID = 2304560839 },
                                            new NpcWear { ShortName = "pants", SkinID = 2304559261 },
                                            new NpcWear { ShortName = "shoes.boots", SkinID = 0 }
                                        },
                                        BeltItems = new HashSet<NpcBelt>
                                        {
                                            new NpcBelt { ShortName = "rifle.lr300", Amount = 1, SkinID = 0, Mods = new HashSet<string> { "weapon.mod.flashlight", "weapon.mod.holosight" }, Ammo = string.Empty },
                                            new NpcBelt { ShortName = "syringe.medical", Amount = 5, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty },
                                            new NpcBelt { ShortName = "grenade.f1", Amount = 1, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
                                        },
                                        Kit = ""
                                    },
                                    TypeLootTable = 5,
                                    PrefabLootTable = new PrefabLootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                                    },
                                    OwnLootTable = new LootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Items = new List<ItemConfig>
                                        {
                                            new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 50f, IsBluePrint = false, SkinID = 0, Name = "" },
                                            new ItemConfig { ShortName = "syringe.medical", MinAmount = 1, MaxAmount = 2, Chance = 70.0f, IsBluePrint = false, SkinID = 0, Name = "" }
                                        }
                                    }
                                }
                            }
                        },
                        new DoorsToScientists
                        {
                            Doors = new HashSet<string>
                            {
                                "(0, 3, -7.5)",
                                "(0, 3, 28.5)"
                            },
                            Scientists = new HashSet<PresetConfig>
                            {
                                new PresetConfig
                                {
                                    Min = 6,
                                    Max = 6,
                                    Positions = new List<string>
                                    {
                                        "(-2.9, 3.1, -6.1)",
                                        "(-2.7, 3.1, 11.4)",
                                        "(2.7, 3.1, 12.3)",
                                        "(-1.2, 3.1, 16.9)",
                                        "(-1.3, 3.1, 22.5)",
                                        "(3.0, 3.1, 27.2)"
                                    },
                                    Config = new NpcConfig
                                    {
                                        Name = "Mariner",
                                        Health = 125f,
                                        AttackRangeMultiplier = 1f,
                                        SenseRange = 30f,
                                        MemoryDuration = 20f,
                                        DamageScale = 1.35f,
                                        AimConeScale = 1f,
                                        CheckVisionCone = false,
                                        VisionCone = 135f,
                                        DisableRadio = true,
                                        IsRemoveCorpse = true,
                                        WearItems = new HashSet<NpcWear>
                                        {
                                            new NpcWear { ShortName = "metal.facemask", SkinID = 2296503845 },
                                            new NpcWear { ShortName = "hoodie", SkinID = 2304560839 },
                                            new NpcWear { ShortName = "pants", SkinID = 2304559261 },
                                            new NpcWear { ShortName = "shoes.boots", SkinID = 0 }
                                        },
                                        BeltItems = new HashSet<NpcBelt>
                                        {
                                            new NpcBelt { ShortName = "rifle.lr300", Amount = 1, SkinID = 0, Mods = new HashSet<string> { "weapon.mod.flashlight", "weapon.mod.holosight" }, Ammo = string.Empty },
                                            new NpcBelt { ShortName = "syringe.medical", Amount = 5, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty },
                                            new NpcBelt { ShortName = "grenade.f1", Amount = 1, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
                                        },
                                        Kit = ""
                                    },
                                    TypeLootTable = 5,
                                    PrefabLootTable = new PrefabLootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                                    },
                                    OwnLootTable = new LootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Items = new List<ItemConfig>
                                        {
                                            new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 50f, IsBluePrint = false, SkinID = 0, Name = "" },
                                            new ItemConfig { ShortName = "syringe.medical", MinAmount = 1, MaxAmount = 2, Chance = 70.0f, IsBluePrint = false, SkinID = 0, Name = "" }
                                        }
                                    }
                                }
                            }
                        },
                        new DoorsToScientists
                        {
                            Doors = new HashSet<string>
                            {
                                "(-1.5, 3, 0)",
                                "(-1.5, 3, 3)"
                            },
                            Scientists = new HashSet<PresetConfig>
                            {
                                new PresetConfig
                                {
                                    Min = 3,
                                    Max = 3,
                                    Positions = new List<string>
                                    {
                                        "(1.8, 3.1, -1.8)",
                                        "(1.9, 3.1, 3.2)",
                                        "(1.5, 3.1, 7.8)"
                                    },
                                    Config = new NpcConfig
                                    {
                                        Name = "Mariner",
                                        Health = 125f,
                                        AttackRangeMultiplier = 1f,
                                        SenseRange = 30f,
                                        MemoryDuration = 20f,
                                        DamageScale = 1.35f,
                                        AimConeScale = 1f,
                                        CheckVisionCone = false,
                                        VisionCone = 135f,
                                        DisableRadio = true,
                                        IsRemoveCorpse = true,
                                        WearItems = new HashSet<NpcWear>
                                        {
                                            new NpcWear { ShortName = "metal.facemask", SkinID = 2296503845 },
                                            new NpcWear { ShortName = "hoodie", SkinID = 2304560839 },
                                            new NpcWear { ShortName = "pants", SkinID = 2304559261 },
                                            new NpcWear { ShortName = "shoes.boots", SkinID = 0 }
                                        },
                                        BeltItems = new HashSet<NpcBelt>
                                        {
                                            new NpcBelt { ShortName = "rifle.lr300", Amount = 1, SkinID = 0, Mods = new HashSet<string> { "weapon.mod.flashlight", "weapon.mod.holosight" }, Ammo = string.Empty },
                                            new NpcBelt { ShortName = "syringe.medical", Amount = 5, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty },
                                            new NpcBelt { ShortName = "grenade.f1", Amount = 1, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
                                        },
                                        Kit = ""
                                    },
                                    TypeLootTable = 5,
                                    PrefabLootTable = new PrefabLootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                                    },
                                    OwnLootTable = new LootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Items = new List<ItemConfig>
                                        {
                                            new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 50f, IsBluePrint = false, SkinID = 0, Name = "" },
                                            new ItemConfig { ShortName = "syringe.medical", MinAmount = 1, MaxAmount = 2, Chance = 70.0f, IsBluePrint = false, SkinID = 0, Name = "" }
                                        }
                                    }
                                }
                            }
                        },
                        new DoorsToScientists
                        {
                            Doors = new HashSet<string>
                            {
                                "(0, 3, 28.5)",
                                "(0, 3, 37.5)",
                                "(0, 3, 31.5)"
                            },
                            Scientists = new HashSet<PresetConfig>
                            {
                                new PresetConfig
                                {
                                    Min = 2,
                                    Max = 2,
                                    Positions = new List<string>
                                    {
                                        "(2.9, 3.1, 30.3)",
                                        "(-3.3, 3.1, 34.5)"
                                    },
                                    Config = new NpcConfig
                                    {
                                        Name = "Mariner",
                                        Health = 125f,
                                        AttackRangeMultiplier = 1f,
                                        SenseRange = 30f,
                                        MemoryDuration = 20f,
                                        DamageScale = 1.35f,
                                        AimConeScale = 1f,
                                        CheckVisionCone = false,
                                        VisionCone = 135f,
                                        DisableRadio = true,
                                        IsRemoveCorpse = true,
                                        WearItems = new HashSet<NpcWear>
                                        {
                                            new NpcWear { ShortName = "metal.facemask", SkinID = 2296503845 },
                                            new NpcWear { ShortName = "hoodie", SkinID = 2304560839 },
                                            new NpcWear { ShortName = "pants", SkinID = 2304559261 },
                                            new NpcWear { ShortName = "shoes.boots", SkinID = 0 }
                                        },
                                        BeltItems = new HashSet<NpcBelt>
                                        {
                                            new NpcBelt { ShortName = "rifle.lr300", Amount = 1, SkinID = 0, Mods = new HashSet<string> { "weapon.mod.flashlight", "weapon.mod.holosight" }, Ammo = string.Empty },
                                            new NpcBelt { ShortName = "syringe.medical", Amount = 5, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty },
                                            new NpcBelt { ShortName = "grenade.f1", Amount = 1, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
                                        },
                                        Kit = ""
                                    },
                                    TypeLootTable = 5,
                                    PrefabLootTable = new PrefabLootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                                    },
                                    OwnLootTable = new LootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Items = new List<ItemConfig>
                                        {
                                            new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 50f, IsBluePrint = false, SkinID = 0, Name = "" },
                                            new ItemConfig { ShortName = "syringe.medical", MinAmount = 1, MaxAmount = 2, Chance = 70.0f, IsBluePrint = false, SkinID = 0, Name = "" }
                                        }
                                    }
                                }
                            }
                        },
                        new DoorsToScientists
                        {
                            Doors = new HashSet<string>
                            {
                                "(0, 3, 37.5)",
                                "(1.5, 3, 39)",
                                "(-1.5, 3, 39)"
                            },
                            Scientists = new HashSet<PresetConfig>
                            {
                                new PresetConfig
                                {
                                    Min = 1,
                                    Max = 1,
                                    Positions = new List<string>
                                    {
                                        "(0.0, 3.1, 39.0)"
                                    },
                                    Config = new NpcConfig
                                    {
                                        Name = "Mariner",
                                        Health = 125f,
                                        AttackRangeMultiplier = 1f,
                                        SenseRange = 30f,
                                        MemoryDuration = 20f,
                                        DamageScale = 1.35f,
                                        AimConeScale = 1f,
                                        CheckVisionCone = false,
                                        VisionCone = 135f,
                                        DisableRadio = true,
                                        IsRemoveCorpse = true,
                                        WearItems = new HashSet<NpcWear>
                                        {
                                            new NpcWear { ShortName = "metal.facemask", SkinID = 2296503845 },
                                            new NpcWear { ShortName = "hoodie", SkinID = 2304560839 },
                                            new NpcWear { ShortName = "pants", SkinID = 2304559261 },
                                            new NpcWear { ShortName = "shoes.boots", SkinID = 0 }
                                        },
                                        BeltItems = new HashSet<NpcBelt>
                                        {
                                            new NpcBelt { ShortName = "rifle.lr300", Amount = 1, SkinID = 0, Mods = new HashSet<string> { "weapon.mod.flashlight", "weapon.mod.holosight" }, Ammo = string.Empty },
                                            new NpcBelt { ShortName = "syringe.medical", Amount = 5, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty },
                                            new NpcBelt { ShortName = "grenade.f1", Amount = 1, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
                                        },
                                        Kit = ""
                                    },
                                    TypeLootTable = 5,
                                    PrefabLootTable = new PrefabLootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                                    },
                                    OwnLootTable = new LootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Items = new List<ItemConfig>
                                        {
                                            new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 50f, IsBluePrint = false, SkinID = 0, Name = "" },
                                            new ItemConfig { ShortName = "syringe.medical", MinAmount = 1, MaxAmount = 2, Chance = 70.0f, IsBluePrint = false, SkinID = 0, Name = "" }
                                        }
                                    }
                                }
                            }
                        },
                        new DoorsToScientists
                        {
                            Doors = new HashSet<string>
                            {
                                "(-1.5, 3, 39)",
                                "(-6.568, 5.511, 37.541)"
                            },
                            Scientists = new HashSet<PresetConfig>
                            {
                                new PresetConfig
                                {
                                    Min = 1,
                                    Max = 1,
                                    Positions = new List<string>
                                    {
                                        "(-3.0, 3.1, 39.4)"
                                    },
                                    Config = new NpcConfig
                                    {
                                        Name = "Mariner",
                                        Health = 125f,
                                        AttackRangeMultiplier = 1f,
                                        SenseRange = 30f,
                                        MemoryDuration = 20f,
                                        DamageScale = 1.35f,
                                        AimConeScale = 1f,
                                        CheckVisionCone = false,
                                        VisionCone = 135f,
                                        DisableRadio = true,
                                        IsRemoveCorpse = true,
                                        WearItems = new HashSet<NpcWear>
                                        {
                                            new NpcWear { ShortName = "metal.facemask", SkinID = 2296503845 },
                                            new NpcWear { ShortName = "hoodie", SkinID = 2304560839 },
                                            new NpcWear { ShortName = "pants", SkinID = 2304559261 },
                                            new NpcWear { ShortName = "shoes.boots", SkinID = 0 }
                                        },
                                        BeltItems = new HashSet<NpcBelt>
                                        {
                                            new NpcBelt { ShortName = "rifle.lr300", Amount = 1, SkinID = 0, Mods = new HashSet<string> { "weapon.mod.flashlight", "weapon.mod.holosight" }, Ammo = string.Empty },
                                            new NpcBelt { ShortName = "syringe.medical", Amount = 5, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty },
                                            new NpcBelt { ShortName = "grenade.f1", Amount = 1, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
                                        },
                                        Kit = ""
                                    },
                                    TypeLootTable = 5,
                                    PrefabLootTable = new PrefabLootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                                    },
                                    OwnLootTable = new LootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Items = new List<ItemConfig>
                                        {
                                            new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 50f, IsBluePrint = false, SkinID = 0, Name = "" },
                                            new ItemConfig { ShortName = "syringe.medical", MinAmount = 1, MaxAmount = 2, Chance = 70.0f, IsBluePrint = false, SkinID = 0, Name = "" }
                                        }
                                    }
                                }
                            }
                        },
                        new DoorsToScientists
                        {
                            Doors = new HashSet<string>
                            {
                                "(1.5, 3, 39)",
                                "(6.568, 5.511, 37.5)"
                            },
                            Scientists = new HashSet<PresetConfig>
                            {
                                new PresetConfig
                                {
                                    Min = 1,
                                    Max = 1,
                                    Positions = new List<string>
                                    {
                                        "(3.4, 3.1, 39.2)"
                                    },
                                    Config = new NpcConfig
                                    {
                                        Name = "Mariner",
                                        Health = 125f,
                                        AttackRangeMultiplier = 1f,
                                        SenseRange = 30f,
                                        MemoryDuration = 20f,
                                        DamageScale = 1.35f,
                                        AimConeScale = 1f,
                                        CheckVisionCone = false,
                                        VisionCone = 135f,
                                        DisableRadio = true,
                                        IsRemoveCorpse = true,
                                        WearItems = new HashSet<NpcWear>
                                        {
                                            new NpcWear { ShortName = "metal.facemask", SkinID = 2296503845 },
                                            new NpcWear { ShortName = "hoodie", SkinID = 2304560839 },
                                            new NpcWear { ShortName = "pants", SkinID = 2304559261 },
                                            new NpcWear { ShortName = "shoes.boots", SkinID = 0 }
                                        },
                                        BeltItems = new HashSet<NpcBelt>
                                        {
                                            new NpcBelt { ShortName = "rifle.lr300", Amount = 1, SkinID = 0, Mods = new HashSet<string> { "weapon.mod.flashlight", "weapon.mod.holosight" }, Ammo = string.Empty },
                                            new NpcBelt { ShortName = "syringe.medical", Amount = 5, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty },
                                            new NpcBelt { ShortName = "grenade.f1", Amount = 1, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
                                        },
                                        Kit = ""
                                    },
                                    TypeLootTable = 5,
                                    PrefabLootTable = new PrefabLootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                                    },
                                    OwnLootTable = new LootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Items = new List<ItemConfig>
                                        {
                                            new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 50f, IsBluePrint = false, SkinID = 0, Name = "" },
                                            new ItemConfig { ShortName = "syringe.medical", MinAmount = 1, MaxAmount = 2, Chance = 70.0f, IsBluePrint = false, SkinID = 0, Name = "" }
                                        }
                                    }
                                }
                            }
                        },
                        new DoorsToScientists
                        {
                            Doors = new HashSet<string>
                            {
                                "(-3, 0, -16.5)",
                                "(0, 0, -16.5)",
                                "(3, 0, -16.5)",
                                "(0, 0, -10.5)",
                                "(6, 0, -7.5)",
                                "(0, 0, -7.5)"
                            },
                            Scientists = new HashSet<PresetConfig>
                            {
                                new PresetConfig
                                {
                                    Min = 4,
                                    Max = 4,
                                    Positions = new List<string>
                                    {
                                        "(-5.6, 0.1, -14.7)",
                                        "(5.7, 0.1, -14.4)",
                                        "(-5.5, 0.1, -9.5)",
                                        "(5.6, 0.1, -9.5)"
                                    },
                                    Config = new NpcConfig
                                    {
                                        Name = "Mariner",
                                        Health = 125f,
                                        AttackRangeMultiplier = 1f,
                                        SenseRange = 30f,
                                        MemoryDuration = 20f,
                                        DamageScale = 1.35f,
                                        AimConeScale = 1f,
                                        CheckVisionCone = false,
                                        VisionCone = 135f,
                                        DisableRadio = true,
                                        IsRemoveCorpse = true,
                                        WearItems = new HashSet<NpcWear>
                                        {
                                            new NpcWear { ShortName = "metal.facemask", SkinID = 2296503845 },
                                            new NpcWear { ShortName = "hoodie", SkinID = 2304560839 },
                                            new NpcWear { ShortName = "pants", SkinID = 2304559261 },
                                            new NpcWear { ShortName = "shoes.boots", SkinID = 0 }
                                        },
                                        BeltItems = new HashSet<NpcBelt>
                                        {
                                            new NpcBelt { ShortName = "rifle.lr300", Amount = 1, SkinID = 0, Mods = new HashSet<string> { "weapon.mod.flashlight", "weapon.mod.holosight" }, Ammo = string.Empty },
                                            new NpcBelt { ShortName = "syringe.medical", Amount = 5, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty },
                                            new NpcBelt { ShortName = "grenade.f1", Amount = 1, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
                                        },
                                        Kit = ""
                                    },
                                    TypeLootTable = 5,
                                    PrefabLootTable = new PrefabLootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                                    },
                                    OwnLootTable = new LootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Items = new List<ItemConfig>
                                        {
                                            new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 50f, IsBluePrint = false, SkinID = 0, Name = "" },
                                            new ItemConfig { ShortName = "syringe.medical", MinAmount = 1, MaxAmount = 2, Chance = 70.0f, IsBluePrint = false, SkinID = 0, Name = "" }
                                        }
                                    }
                                }
                            }
                        },
                        new DoorsToScientists
                        {
                            Doors = new HashSet<string>
                            {
                                "(0, 0, -7.5)",
                                "(0, 0, 1.5)",
                                "(4.5, 0, -3)"
                            },
                            Scientists = new HashSet<PresetConfig>
                            {
                                new PresetConfig
                                {
                                    Min = 2,
                                    Max = 2,
                                    Positions = new List<string>
                                    {
                                        "(-4.0, 0.1, -3.0)",
                                        "(0.1, 0.1, -2.6)"
                                    },
                                    Config = new NpcConfig
                                    {
                                        Name = "Mariner",
                                        Health = 125f,
                                        AttackRangeMultiplier = 1f,
                                        SenseRange = 30f,
                                        MemoryDuration = 20f,
                                        DamageScale = 1.35f,
                                        AimConeScale = 1f,
                                        CheckVisionCone = false,
                                        VisionCone = 135f,
                                        DisableRadio = true,
                                        IsRemoveCorpse = true,
                                        WearItems = new HashSet<NpcWear>
                                        {
                                            new NpcWear { ShortName = "metal.facemask", SkinID = 2296503845 },
                                            new NpcWear { ShortName = "hoodie", SkinID = 2304560839 },
                                            new NpcWear { ShortName = "pants", SkinID = 2304559261 },
                                            new NpcWear { ShortName = "shoes.boots", SkinID = 0 }
                                        },
                                        BeltItems = new HashSet<NpcBelt>
                                        {
                                            new NpcBelt { ShortName = "rifle.lr300", Amount = 1, SkinID = 0, Mods = new HashSet<string> { "weapon.mod.flashlight", "weapon.mod.holosight" }, Ammo = string.Empty },
                                            new NpcBelt { ShortName = "syringe.medical", Amount = 5, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty },
                                            new NpcBelt { ShortName = "grenade.f1", Amount = 1, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
                                        },
                                        Kit = ""
                                    },
                                    TypeLootTable = 5,
                                    PrefabLootTable = new PrefabLootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                                    },
                                    OwnLootTable = new LootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Items = new List<ItemConfig>
                                        {
                                            new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 50f, IsBluePrint = false, SkinID = 0, Name = "" },
                                            new ItemConfig { ShortName = "syringe.medical", MinAmount = 1, MaxAmount = 2, Chance = 70.0f, IsBluePrint = false, SkinID = 0, Name = "" }
                                        }
                                    }
                                }
                            }
                        },
                        new DoorsToScientists
                        {
                            Doors = new HashSet<string>
                            {
                                "(6, 0, -7.5)",
                                "(0, 0, 1.5)",
                                "(4.5, 0, -3)",
                                "(-3, 0, 4.5)"
                            },
                            Scientists = new HashSet<PresetConfig>
                            {
                                new PresetConfig
                                {
                                    Min = 2,
                                    Max = 2,
                                    Positions = new List<string>
                                    {
                                        "(6.0, 0.1, -3.9)",
                                        "(5.9, 0.1, 2.6)"
                                    },
                                    Config = new NpcConfig
                                    {
                                        Name = "Mariner",
                                        Health = 125f,
                                        AttackRangeMultiplier = 1f,
                                        SenseRange = 30f,
                                        MemoryDuration = 20f,
                                        DamageScale = 1.35f,
                                        AimConeScale = 1f,
                                        CheckVisionCone = false,
                                        VisionCone = 135f,
                                        DisableRadio = true,
                                        IsRemoveCorpse = true,
                                        WearItems = new HashSet<NpcWear>
                                        {
                                            new NpcWear { ShortName = "metal.facemask", SkinID = 2296503845 },
                                            new NpcWear { ShortName = "hoodie", SkinID = 2304560839 },
                                            new NpcWear { ShortName = "pants", SkinID = 2304559261 },
                                            new NpcWear { ShortName = "shoes.boots", SkinID = 0 }
                                        },
                                        BeltItems = new HashSet<NpcBelt>
                                        {
                                            new NpcBelt { ShortName = "rifle.lr300", Amount = 1, SkinID = 0, Mods = new HashSet<string> { "weapon.mod.flashlight", "weapon.mod.holosight" }, Ammo = string.Empty },
                                            new NpcBelt { ShortName = "syringe.medical", Amount = 5, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty },
                                            new NpcBelt { ShortName = "grenade.f1", Amount = 1, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
                                        },
                                        Kit = ""
                                    },
                                    TypeLootTable = 5,
                                    PrefabLootTable = new PrefabLootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                                    },
                                    OwnLootTable = new LootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Items = new List<ItemConfig>
                                        {
                                            new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 50f, IsBluePrint = false, SkinID = 0, Name = "" },
                                            new ItemConfig { ShortName = "syringe.medical", MinAmount = 1, MaxAmount = 2, Chance = 70.0f, IsBluePrint = false, SkinID = 0, Name = "" }
                                        }
                                    }
                                }
                            }
                        },
                        new DoorsToScientists
                        {
                            Doors = new HashSet<string>
                            {
                                "(-3, 0, 4.5)",
                                "(-3, 0, 28.5)"
                            },
                            Scientists = new HashSet<PresetConfig>
                            {
                                new PresetConfig
                                {
                                    Min = 5,
                                    Max = 5,
                                    Positions = new List<string>
                                    {
                                        "(-5.3, 0.1, 5.9)",
                                        "(2.9, 0.1, 9.1)",
                                        "(6.7, 0.1, 15.0)",
                                        "(-6.1, 0.1, 25.6)",
                                        "(3.0, 0.1, 27.0)"
                                    },
                                    Config = new NpcConfig
                                    {
                                        Name = "Mariner",
                                        Health = 125f,
                                        AttackRangeMultiplier = 1f,
                                        SenseRange = 30f,
                                        MemoryDuration = 20f,
                                        DamageScale = 1.35f,
                                        AimConeScale = 1f,
                                        CheckVisionCone = false,
                                        VisionCone = 135f,
                                        DisableRadio = true,
                                        IsRemoveCorpse = true,
                                        WearItems = new HashSet<NpcWear>
                                        {
                                            new NpcWear { ShortName = "metal.facemask", SkinID = 2296503845 },
                                            new NpcWear { ShortName = "hoodie", SkinID = 2304560839 },
                                            new NpcWear { ShortName = "pants", SkinID = 2304559261 },
                                            new NpcWear { ShortName = "shoes.boots", SkinID = 0 }
                                        },
                                        BeltItems = new HashSet<NpcBelt>
                                        {
                                            new NpcBelt { ShortName = "rifle.lr300", Amount = 1, SkinID = 0, Mods = new HashSet<string> { "weapon.mod.flashlight", "weapon.mod.holosight" }, Ammo = string.Empty },
                                            new NpcBelt { ShortName = "syringe.medical", Amount = 5, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty },
                                            new NpcBelt { ShortName = "grenade.f1", Amount = 1, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
                                        },
                                        Kit = ""
                                    },
                                    TypeLootTable = 5,
                                    PrefabLootTable = new PrefabLootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                                    },
                                    OwnLootTable = new LootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Items = new List<ItemConfig>
                                        {
                                            new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 50f, IsBluePrint = false, SkinID = 0, Name = "" },
                                            new ItemConfig { ShortName = "syringe.medical", MinAmount = 1, MaxAmount = 2, Chance = 70.0f, IsBluePrint = false, SkinID = 0, Name = "" }
                                        }
                                    }
                                }
                            }
                        },
                        new DoorsToScientists
                        {
                            Doors = new HashSet<string>
                            {
                                "(-3, 0, 28.5)",
                                "(0, 0, 31.5)",
                                "(-3, 0, 37.5)",
                                "(0, 0, 37.5)"
                            },
                            Scientists = new HashSet<PresetConfig>
                            {
                                new PresetConfig
                                {
                                    Min = 3,
                                    Max = 3,
                                    Positions = new List<string>
                                    {
                                        "(5.5, 0.1, 31.1)",
                                        "(3.8, 0.1, 35.4)",
                                        "(-2.7, 0.1, 35.5)"
                                    },
                                    Config = new NpcConfig
                                    {
                                        Name = "Mariner",
                                        Health = 125f,
                                        AttackRangeMultiplier = 1f,
                                        SenseRange = 30f,
                                        MemoryDuration = 20f,
                                        DamageScale = 1.35f,
                                        AimConeScale = 1f,
                                        CheckVisionCone = false,
                                        VisionCone = 135f,
                                        DisableRadio = true,
                                        IsRemoveCorpse = true,
                                        WearItems = new HashSet<NpcWear>
                                        {
                                            new NpcWear { ShortName = "metal.facemask", SkinID = 2296503845 },
                                            new NpcWear { ShortName = "hoodie", SkinID = 2304560839 },
                                            new NpcWear { ShortName = "pants", SkinID = 2304559261 },
                                            new NpcWear { ShortName = "shoes.boots", SkinID = 0 }
                                        },
                                        BeltItems = new HashSet<NpcBelt>
                                        {
                                            new NpcBelt { ShortName = "rifle.lr300", Amount = 1, SkinID = 0, Mods = new HashSet<string> { "weapon.mod.flashlight", "weapon.mod.holosight" }, Ammo = string.Empty },
                                            new NpcBelt { ShortName = "syringe.medical", Amount = 5, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty },
                                            new NpcBelt { ShortName = "grenade.f1", Amount = 1, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
                                        },
                                        Kit = ""
                                    },
                                    TypeLootTable = 5,
                                    PrefabLootTable = new PrefabLootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                                    },
                                    OwnLootTable = new LootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Items = new List<ItemConfig>
                                        {
                                            new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 50f, IsBluePrint = false, SkinID = 0, Name = "" },
                                            new ItemConfig { ShortName = "syringe.medical", MinAmount = 1, MaxAmount = 2, Chance = 70.0f, IsBluePrint = false, SkinID = 0, Name = "" }
                                        }
                                    }
                                }
                            }
                        },
                        new DoorsToScientists
                        {
                            Doors = new HashSet<string>
                            {
                               "(-4.5, 0, 36)"
                            },
                            Scientists = new HashSet<PresetConfig>
                            {
                                new PresetConfig
                                {
                                    Min = 1,
                                    Max = 1,
                                    Positions = new List<string>
                                    {
                                        "(-5.2, 0.1, 30.4)"
                                    },
                                    Config = new NpcConfig
                                    {
                                        Name = "Mariner",
                                        Health = 125f,
                                        AttackRangeMultiplier = 1f,
                                        SenseRange = 30f,
                                        MemoryDuration = 20f,
                                        DamageScale = 1.35f,
                                        AimConeScale = 1f,
                                        CheckVisionCone = false,
                                        VisionCone = 135f,
                                        DisableRadio = true,
                                        IsRemoveCorpse = true,
                                        WearItems = new HashSet<NpcWear>
                                        {
                                            new NpcWear { ShortName = "metal.facemask", SkinID = 2296503845 },
                                            new NpcWear { ShortName = "hoodie", SkinID = 2304560839 },
                                            new NpcWear { ShortName = "pants", SkinID = 2304559261 },
                                            new NpcWear { ShortName = "shoes.boots", SkinID = 0 }
                                        },
                                        BeltItems = new HashSet<NpcBelt>
                                        {
                                            new NpcBelt { ShortName = "rifle.lr300", Amount = 1, SkinID = 0, Mods = new HashSet<string> { "weapon.mod.flashlight", "weapon.mod.holosight" }, Ammo = string.Empty },
                                            new NpcBelt { ShortName = "syringe.medical", Amount = 5, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty },
                                            new NpcBelt { ShortName = "grenade.f1", Amount = 1, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
                                        },
                                        Kit = ""
                                    },
                                    TypeLootTable = 5,
                                    PrefabLootTable = new PrefabLootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                                    },
                                    OwnLootTable = new LootTableConfig
                                    {
                                        Min = 1, Max = 1, UseCount = true,
                                        Items = new List<ItemConfig>
                                        {
                                            new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 50f, IsBluePrint = false, SkinID = 0, Name = "" },
                                            new ItemConfig { ShortName = "syringe.medical", MinAmount = 1, MaxAmount = 2, Chance = 70.0f, IsBluePrint = false, SkinID = 0, Name = "" }
                                        }
                                    }
                                }
                            }
                        }
                    },
                    Economy = new EconomyConfig
                    {
                        Plugins = new HashSet<string> { "Economics", "Server Rewards", "IQEconomic" },
                        Min = 0,
                        Crates = new Dictionary<string, double>
                        {
                            ["tech_parts_1"] = 0.1,
                            ["tech_parts_2"] = 0.1,
                            ["crate_ammunition"] = 0.2,
                            ["crate_fuel"] = 0.1,
                            ["crate_tools"] = 0.1,
                            ["crate_underwater_basic"] = 0.2,
                            ["crate_underwater_advanced"] = 0.3,
                            ["crate_medical"] = 0.1,
                            ["crate_elite"] = 0.4,
                            ["crate_food_1"] = 0.1,
                            ["crate_food_2"] = 0.1,
                            ["crate_normal"] = 0.2,
                            ["crate_normal_2"] = 0.1,
                            ["crate_normal_2_food"] = 0.1,
                            ["crate_normal_2_medical"] = 0.1
                        },
                        Doors = new Dictionary<string, double>
                        {
                            ["door.double.hinged.toptier"] = 0.4,
                            ["door.hinged.toptier"] = 0.4,
                            ["wall.frame.cell.gate"] = 0.2
                        },
                        Npc = 0.3,
                        LockedCrate = 0.5,
                        Cards = new Dictionary<string, double>
                        {
                            ["blue"] = 0.4,
                            ["red"] = 0.5
                        },
                        Commands = new HashSet<string>()
                    },
                    ScaleDamage = new Dictionary<string, float>
                    {
                        ["torpedostraight"] = 1f,
                        ["explosive.timed.deployed"] = 2f,
                        ["explosive.satchel.deployed"] = 1f,
                        ["rocket_basic"] = 2f,
                        ["40mm_grenade_he"] = 1f,
                        ["grenade.f1.deployed"] = 1f,
                        ["grenade.beancan.deployed"] = 1f,
                        ["rocket_hv"] = 1f,
                        ["lr300.entity"] = 1f,
                        ["m249.entity"] = 1f,
                        ["ak47u.entity"] = 1f,
                        ["m39.entity"] = 1f,
                        ["semi_auto_rifle.entity"] = 1f
                    },
                    DoorHealth = 800f,
                    DistanceToBlock = 0f,
                    IsWorkbench = true,
                    IsRepairBench = false,
                    IsResearchTable = false,
                    IsMixingTable = false,
                    IsLocker = false,
                    IsBoxStorage = false,
                    Turret = new TurretConfig
                    {
                        IsTurret = true,
                        Hp = 500f,
                        ShortNameWeapon = "rifle.ak",
                        ShortNameAmmo = "ammo.rifle",
                        CountAmmo = 150
                    },
                    Commands = new HashSet<string>
                    {
                        "/remove",
                        "remove.toggle"
                    },
                    PluginVersion = new VersionNumber()
                };
            }
        }
        #endregion Config

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PreStart"] = "{0} The Submarine will traverse the seas near the island in <color=#55aaff>{1} sec.</color>!",
                ["Start"] = "{0} The Submarine has breached. It's en route to grid <color=#55aaff>{1}</color>\nThe Submarine <color=#ce3f27>will self destruct</color> in <color=#55aaff>{2} sec.</color>!\nCCTVs: <color=#55aaff>Submarine1, Submarine2, Submarine3, Submarine4</color>",
                ["PreFinish"] = "{0} The Submarine <color=#ce3f27>will self destruct</color> in <color=#55aaff>{1} sec.</color>!",
                ["Finish"] = "{0} The Submarine <color=#ce3f27>self destructed</color>!",
                ["EventActive"] = "{0} This event is active now. To finish this event (<color=#55aaff>/waterstop</color>), then (<color=#55aaff>/waterstart</color>) to start the next one!",
                ["EnterPVP"] = "{0} You <color=#ce3f27>have entered</color> the PVP zone, now other players <color=#ce3f27>can damage</color> you!",
                ["ExitPVP"] = "{0} You <color=#738d43>have left</color> the PVP zone, now other players <color=#738d43>cannot damage</color> you!",
                ["OpenBlueDoor"] = "{0} <color=#55aaff>{1}</color> <color=#738d43>has opened</color> The Blue Security Door on The Submarine!",
                ["OpenRedDoor"] = "{0} <color=#55aaff>{1}</color> <color=#738d43>has opened</color> The Red Security Door on The Submarine!",
                ["HackCrate"] = "{0} <color=#55aaff>{1}</color> <color=#738d43>is hacking</color> a locked crate inside The Submarine!",
                ["NTeleportation"] = "{0} You <color=#ce3f27>cannot</color> teleport into the event zone!",
                ["SendEconomy"] = "{0} You <color=#738d43>have earned</color> <color=#55aaff>{1}</color> points in economics for participating in the event",
                ["NoCommand"] = "{0} You <color=#ce3f27>cannot</color> use this command in the event zone!"
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PreStart"] = "{0} Через <color=#55aaff>{1} сек.</color> подводная лодка с учеными будет проплывать мимо острова!",
                ["Start"] = "{0} Подводная лодка пробита и терпит крушение в квадрате <color=#55aaff>{1}</color>\nЧерез <color=#55aaff>{2} сек.</color> подводная лодка <color=#ce3f27>уничтожится</color>!\nКамеры: <color=#55aaff>Submarine1, Submarine2, Submarine3, Submarine4</color>",
                ["PreFinish"] = "{0} Подводная лодка <color=#ce3f27>уничтожится</color> через <color=#55aaff>{1} сек.</color>!",
                ["Finish"] = "{0} Подводная лодка <color=#ce3f27>уничтожена</color>!",
                ["EventActive"] = "{0} Ивент в данный момент активен, сначала завершите текущий ивент (<color=#55aaff>/waterstop</color>), чтобы начать следующий!",
                ["EnterPVP"] = "{0} Вы <color=#ce3f27>вошли</color> в PVP зону, теперь другие игроки <color=#ce3f27>могут</color> наносить вам урон!",
                ["ExitPVP"] = "{0} Вы <color=#738d43>вышли</color> из PVP зоны, теперь другие игроки <color=#738d43>не могут</color> наносить вам урон!",
                ["OpenBlueDoor"] = "{0} <color=#55aaff>{1}</color> <color=#738d43>открыл</color> синюю дверь на подводной лодке!",
                ["OpenRedDoor"] = "{0} <color=#55aaff>{1}</color> <color=#738d43>открыл</color> красную дверь на подводной лодке!",
                ["HackCrate"] = "{0} <color=#55aaff>{1}</color> <color=#738d43>начал</color> взлом заблокированного ящика на подводной лодке!",
                ["NTeleportation"] = "{0} Вы <color=#ce3f27>не можете</color> телепортироваться в зоне ивента!",
                ["SendEconomy"] = "{0} Вы <color=#738d43>получили</color> <color=#55aaff>{1}</color> баллов в экономику за прохождение ивента",
                ["NoCommand"] = "{0} Вы <color=#ce3f27>не можете</color> использовать данную команду в зоне ивента!"
            }, this, "ru");
        }

        private string GetMessage(string langKey, string userID) => lang.GetMessage(langKey, _ins, userID);

        private string GetMessage(string langKey, string userID, params object[] args) => (args.Length == 0) ? GetMessage(langKey, userID) : string.Format(GetMessage(langKey, userID), args);
        #endregion Lang

        #region Data
        public class Prefab { public string prefab; public string pos; public string rot; }

        internal HashSet<Prefab> Prefabs = new HashSet<Prefab>();
        internal HashSet<Prefab> Crates = new HashSet<Prefab>();

        private void LoadData()
        {
            Prefabs = Interface.Oxide.DataFileSystem.ReadObject<HashSet<Prefab>>("WaterEvent/submarine");
            if (Prefabs == null || Prefabs.Count == 0)
            {
                PrintError("The submarine.json file is empty or it doesn't exist");
                Server.Command($"o.unload {Name}");
                return;
            }
            Crates = Interface.Oxide.DataFileSystem.ReadObject<HashSet<Prefab>>("WaterEvent/crates");
            if (Crates == null || Crates.Count == 0)
            {
                PrintError("The crates.json file is empty or it doesn't exist");
                Server.Command($"o.unload {Name}");
                return;
            }
        }
        #endregion Data

        #region Oxide Hooks
        private static WaterEvent _ins;

        private void Init()
        {
            _ins = this;
            Unsubscribes();
        }

        private void OnServerInitialized()
        {
            LoadDefaultMessages();
            LoadData();
            CheckAllLootTables();
            DownloadImage();
            if (_config.EnabledTimer)
            {
                timer.In(UnityEngine.Random.Range(_config.MinStartTime, _config.MaxStartTime), () =>
                {
                    if (!_active) Start();
                    else Puts("This event is active now. To finish this event (waterstop), then to start the next one");
                });
            }
        }

        private void Unload()
        {
            if (_active) Finish();
            _ins = null;
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return null;
            if (_controller.Entities.Contains(entity))
            {
                if (entity is AutoTurret)
                {
                    if (entity.health - info.damageTypes.Total() <= 0f) (entity as AutoTurret).inventory.ClearItemsContainer();
                    return null;
                }
                else if (entity.ShortPrefabName.Contains("barrel")) return null;
                else if (entity is Door)
                {
                    if (_config.PveMode.Pve && plugins.Exists("PveMode"))
                    {
                        BasePlayer attacker = info.InitiatorPlayer;
                        if (attacker.IsPlayer() && PveMode.Call("CanActionEvent", Name, attacker) != null) return true;
                    }
                    if (info.WeaponPrefab != null && _config.ScaleDamage.ContainsKey(info.WeaponPrefab.ShortPrefabName)) info.damageTypes.ScaleAll(_config.ScaleDamage[info.WeaponPrefab.ShortPrefabName]);
                }
                else return true;
            }
            return null;
        }

        private void OnEntityDeath(BasePlayer player, HitInfo info)
        {
            if (player != null && _controller.Players.Contains(player))
            {
                _controller.Players.Remove(player);
                if (_config.Gui.IsGui) CuiHelper.DestroyUi(player, "Tabs_KpucTaJl");
            }
        }

        private void OnEntityDeath(ScientistNPC npc, HitInfo info)
        {
            if (npc == null || info == null) return;
            BasePlayer attacker = info.InitiatorPlayer;
            if (_controller.Scientists.Contains(npc) && attacker.IsPlayer()) ActionEconomy(attacker.userID, "Npc");
        }

        private void OnEntityDeath(Door door, HitInfo info)
        {
            if (door == null || info == null) return;
            BasePlayer attacker = info.InitiatorPlayer;
            if (_controller.Doors.Contains(door) && attacker.IsPlayer()) ActionEconomy(attacker.userID, "Doors", door.ShortPrefabName);
        }

        private object OnOvenToggle(BaseOven oven, BasePlayer player)
        {
            if (oven != null && _controller.Entities.Contains(oven)) return true;
            else return null;
        }

        private object CanUseWires(BasePlayer player)
        {
            if (player != null && _controller.Players.Contains(player)) return true;
            else return null;
        }

        private object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            if (planner == null) return null;
            BasePlayer player = planner.GetOwnerPlayer();
            if (player != null && _controller.Players.Contains(player)) return false;
            return null;
        }

        private object CanAffordUpgrade(BasePlayer player, BuildingBlock block, BuildingGrade.Enum grade)
        {
            if (block != null && _controller.Entities.Contains(block)) return false;
            else return null;
        }

        private object OnStructureRotate(BuildingBlock block, BasePlayer player)
        {
            if (block != null && _controller.Entities.Contains(block)) return true;
            else return null;
        }

        private void OnCardSwipe(CardReader cardReader, Keycard card, BasePlayer player)
        {
            if (cardReader == null || card == null || player == null) return;
            if (cardReader == _controller.CardReaderBlue && !_controller.DoorBlue.IsOpen())
            {
                object hook = Interface.Oxide.CallHook("OnCardSwipeWaterEvent", cardReader, card, player);
                if (cardReader.accessLevel == card.accessLevel || (hook is bool && (bool)hook))
                {
                    if (_config.PveMode.Pve && plugins.Exists("PveMode") && PveMode.Call("CanActionEvent", Name, player) != null) return;
                    _controller.DoorBlue.SetOpen(true);
                    ActionEconomy(player.userID, "Cards", "blue");
                    timer.In(10f, () =>
                    {
                        _controller.DoorBlue.SetOpen(false);
                        cardReader.ResetIOState();
                    });
                    AlertToAllPlayers("OpenBlueDoor", _config.Prefix, player.displayName);
                }
            }
            else if (cardReader == _controller.CardReaderRed && !_controller.DoorRed.IsOpen())
            {
                object hook = Interface.Oxide.CallHook("OnCardSwipeWaterEvent", cardReader, card, player);
                if (cardReader.accessLevel == card.accessLevel || (hook is bool && (bool)hook))
                {
                    if (_config.PveMode.Pve && plugins.Exists("PveMode") && PveMode.Call("CanActionEvent", Name, player) != null) return;
                    _controller.DoorRed.SetOpen(true);
                    ActionEconomy(player.userID, "Cards", "red");
                    timer.In(10f, () =>
                    {
                        _controller.DoorRed.SetOpen(false);
                        cardReader.ResetIOState();
                    });
                    AlertToAllPlayers("OpenRedDoor", _config.Prefix, player.displayName);
                }
            }
        }

        private void OnButtonPress(PressButton button, BasePlayer player)
        {
            if (button == null || player == null) return;
            if (button == _controller.ButtonBlue && !_controller.DoorBlue.IsOpen())
            {
                if (_config.PveMode.Pve && plugins.Exists("PveMode") && PveMode.Call("CanActionEvent", Name, player) != null) return;
                _controller.DoorBlue.SetOpen(true);
                timer.In(10f, () => _controller.DoorBlue.SetOpen(false));
            }
            else if (button == _controller.ButtonRed && !_controller.DoorBlue.IsOpen())
            {
                if (_config.PveMode.Pve && plugins.Exists("PveMode") && PveMode.Call("CanActionEvent", Name, player) != null) return;
                _controller.DoorRed.SetOpen(true);
                timer.In(10f, () => _controller.DoorRed.SetOpen(false));
            }
        }

        private object OnEntityKill(BaseEntity entity)
        {
            if (entity == null) return null;
            if (entity is LootContainer)
            {
                if (_lootableCrates.Contains(entity.net.ID.Value)) _lootableCrates.Remove(entity.net.ID.Value);
                if (_controller.Crates.Contains(entity as LootContainer)) _controller.Crates.Remove(entity as LootContainer);
                if (entity is HackableLockedCrate && _controller.HackCrates.Contains(entity as HackableLockedCrate)) _controller.HackCrates.Remove(entity as HackableLockedCrate);
                return null;
            }
            if (entity is Door && _controller.Doors.Contains(entity as Door))
            {
                _controller.KillDoor(entity.transform.position);
                _controller.Doors.Remove(entity as Door);
                return null;
            }
            if (_controller.Entities.Contains(entity))
            {
                if (entity.ShortPrefabName.Contains("barrel")) return null;
                if (entity is AutoTurret) return null;
                if (!_controller.KillEntities) return true;
            }
            return null;
        }

        private readonly Dictionary<ulong, BasePlayer> _startHackCrates = new Dictionary<ulong, BasePlayer>();

        private void CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            if (player == null || crate == null) return;
            if (_controller.HackCrates.Contains(crate))
            {
                if (_startHackCrates.ContainsKey(crate.net.ID.Value)) _startHackCrates[crate.net.ID.Value] = player;
                else _startHackCrates.Add(crate.net.ID.Value, player);
            }
        }

        private void OnCrateHack(HackableLockedCrate crate)
        {
            if (crate == null) return;
            ulong crateId = crate.net.ID.Value;
            BasePlayer player;
            if (_startHackCrates.TryGetValue(crateId, out player))
            {
                _startHackCrates.Remove(crateId);
                if (_config.HackCrate.IncreaseEventTime && _controller.TimeToFinish < (int)_config.HackCrate.UnlockTime) _controller.TimeToFinish += (int)_config.HackCrate.UnlockTime;
                ActionEconomy(player.userID, "LockedCrate");
                AlertToAllPlayers("HackCrate", _config.Prefix, player.displayName);
            }
        }

        private object CanHideStash(BasePlayer player, StashContainer stash)
        {
            if (stash != null && _controller.Entities.Contains(stash)) return true;
            else return null;
        }

        private object OnTurretTarget(AutoTurret turret, BaseCombatEntity entity)
        {
            if (turret == null || entity == null) return null;
            if (_controller.Turrets.Contains(turret))
            {
                if ((entity as BasePlayer).IsPlayer()) return null;
                else return true;
            }
            return null;
        }

        private object OnPlayerCommand(BasePlayer player, string command, string[] args)
        {
            if (player != null && _controller.Players.Contains(player))
            {
                command = "/" + command;
                if (_config.Commands.Contains(command.ToLower()))
                {
                    AlertToPlayer(player, GetMessage("NoCommand", player.UserIDString, _config.Prefix));
                    return true;
                }
            }
            return null;
        }

        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (arg == null || arg.cmd == null) return null;
            BasePlayer player = arg.Player();
            if (player != null && _controller.Players.Contains(player))
            {
                if (_config.Commands.Contains(arg.cmd.Name.ToLower()) || _config.Commands.Contains(arg.cmd.FullName.ToLower()))
                {
                    AlertToPlayer(player, GetMessage("NoCommand", player.UserIDString, _config.Prefix));
                    return true;
                }
            }
            return null;
        }
        #endregion Oxide Hooks

        #region Controller
        private ControllerWaterEvent _controller;
        private bool _active = false;

        private void Start()
        {
            if (!plugins.Exists("NpcSpawn"))
            {
                PrintError("NpcSpawn plugin doesn`t exist! (https://drive.google.com/drive/folders/1-18L-mG7yiGxR-PQYvd11VvXC2RQ4ZCu?usp=sharing)");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }
            _active = true;
            AlertToAllPlayers("PreStart", _config.Prefix, _config.PreStartTime);
            timer.In(_config.PreStartTime, () =>
            {
                Puts("WaterEvent has begun");
                RandomSpawnPos();
                if (SpawnPos == Vector3.zero)
                {
                    _active = false;
                    Puts("WaterEvent has ended");
                    timer.In(UnityEngine.Random.Range(_config.MinStartTime, _config.MaxStartTime), () =>
                    {
                        if (!_active) Start();
                        else Puts("This event is active now. To finish this event (waterstop), then to start the next one");
                    });
                    return;
                }
                foreach (Collider collider in Physics.OverlapSphere(SpawnPos, _config.Radius))
                {
                    BaseEntity entity = collider.ToBaseEntity();
                    if (entity.IsExists() && (entity is DiveSite || entity is JunkPileWater || entity is RHIB || entity is MotorRowboat || entity is BaseSubmarine)) entity.Kill();
                }
                Subscribes();
                _controller = new GameObject().AddComponent<ControllerWaterEvent>();
                AlertToAllPlayers("Start", _config.Prefix, PhoneController.PositionToGridCoord(SpawnPos), _config.FinishTime);
            });
        }

        private void Finish()
        {
            Unsubscribes();
            if (_config.PveMode.Pve && plugins.Exists("PveMode")) PveMode.Call("EventRemovePveMode", Name, true);
            if (_controller != null) UnityEngine.Object.Destroy(_controller.gameObject);
            _active = false;
            SpawnPos = Vector3.zero;
            SendBalance();
            AlertToAllPlayers("Finish", _config.Prefix);
            Interface.Oxide.CallHook("OnWaterEventEnd");
            Puts("WaterEvent has ended");
            if (_config.EnabledTimer)
            {
                timer.In(UnityEngine.Random.Range(_config.MinStartTime, _config.MaxStartTime), () =>
                {
                    if (!_active) Start();
                    else Puts("This event is active now. To finish this event (waterstop), then to start the next one");
                });
            }
        }

        internal class ControllerWaterEvent : FacepunchBehaviour
        {
            private MapMarkerGenericRadius _mapmarker;
            private VendingMachineMapMarker _vendingMarker;
            private SphereCollider _sphereCollider;

            internal bool KillEntities = false;
            internal HashSet<BaseEntity> Entities = new HashSet<BaseEntity>();
            internal HashSet<AutoTurret> Turrets = new HashSet<AutoTurret>();
            internal HashSet<Door> Doors = new HashSet<Door>();

            internal HashSet<LootContainer> Crates = new HashSet<LootContainer>();
            internal HashSet<HackableLockedCrate> HackCrates = new HashSet<HackableLockedCrate>();

            private int _countCctv = 0;

            internal Door DoorBlue;
            internal Door DoorRed;
            internal CardReader CardReaderBlue;
            internal CardReader CardReaderRed;
            internal PressButton ButtonBlue;
            internal PressButton ButtonRed;

            private Coroutine _spawnEntitiesCoroutine = null;

            internal HashSet<ScientistNPC> Scientists = new HashSet<ScientistNPC>();

            private HashSet<DoorsToPresets> _doorsToPresets = new HashSet<DoorsToPresets>();

            internal int TimeToFinish;
            internal HashSet<BasePlayer> Players = new HashSet<BasePlayer>();

            private void Awake()
            {
                transform.position = _ins.SpawnPos;

                SpawnMapMarker();

                gameObject.layer = 3;
                _sphereCollider = gameObject.AddComponent<SphereCollider>();
                _sphereCollider.isTrigger = true;
                _sphereCollider.radius = _ins._config.Radius;

                TimeToFinish = _ins._config.FinishTime;

                CalcDoorsToPresets();

                _spawnEntitiesCoroutine = ServerMgr.Instance.StartCoroutine(SpawnEntities());
            }

            private void OnDestroy()
            {
                if (_spawnEntitiesCoroutine != null) ServerMgr.Instance.StopCoroutine(_spawnEntitiesCoroutine);

                CancelInvoke(UpdateMapMarker);
                if (_mapmarker.IsExists()) _mapmarker.Kill();
                if (_vendingMarker.IsExists()) _vendingMarker.Kill();

                KillEntities = true;
                foreach (BaseEntity entity in Entities) if (entity.IsExists()) entity.Kill();
                foreach (LootContainer crate in Crates) if (crate.IsExists()) crate.Kill();
                foreach (HackableLockedCrate crate in HackCrates) if (crate.IsExists()) crate.Kill();

                foreach (ScientistNPC npc in Scientists) if (npc.IsExists()) npc.Kill();

                CancelInvoke(ChangeToFinishTime);
                foreach (BasePlayer player in Players) CuiHelper.DestroyUi(player, "Tabs_KpucTaJl");
            }

            private void OnTriggerEnter(Collider other)
            {
                BasePlayer player = other.GetComponentInParent<BasePlayer>();
                if (player.IsPlayer())
                {
                    Players.Add(player);
                    if (_ins._config.Gui.IsGui) _ins.CreateTabs(player, new Dictionary<string, string> { ["Clock_KpucTaJl"] = GetTimeFormat(), ["Crate_KpucTaJl"] = $"{Crates.Count + HackCrates.Count}" });
                    if (_ins._config.IsCreateZonePvp) _ins.AlertToPlayer(player, _ins.GetMessage("EnterPVP", player.UserIDString, _ins._config.Prefix));
                }
            }

            private void OnTriggerExit(Collider other)
            {
                BasePlayer player = other.GetComponentInParent<BasePlayer>();
                if (player.IsPlayer())
                {
                    Players.Remove(player);
                    if (_ins._config.Gui.IsGui) CuiHelper.DestroyUi(player, "Tabs_KpucTaJl");
                    if (_ins._config.IsCreateZonePvp) _ins.AlertToPlayer(player, _ins.GetMessage("ExitPVP", player.UserIDString, _ins._config.Prefix));
                }
            }

            private void ChangeToFinishTime()
            {
                TimeToFinish--;
                if (_ins._config.Gui.IsGui) foreach (BasePlayer player in Players) _ins.CreateTabs(player, new Dictionary<string, string> { ["Clock_KpucTaJl"] = GetTimeFormat(), ["Crate_KpucTaJl"] = $"{Crates.Count + HackCrates.Count}" });
                if ((TimeToFinish == _ins._config.PreFinishTime || Crates.Count + HackCrates.Count == 0) && TimeToFinish >= _ins._config.PreFinishTime)
                {
                    TimeToFinish = _ins._config.PreFinishTime;
                    _ins.AlertToAllPlayers("PreFinish", _ins._config.Prefix, _ins._config.PreFinishTime);
                }
                else if (TimeToFinish == 0)
                {
                    CancelInvoke(ChangeToFinishTime);
                    _ins.Finish();
                }
            }

            private void SpawnMapMarker()
            {
                _mapmarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", transform.position) as MapMarkerGenericRadius;
                _mapmarker.Spawn();
                _mapmarker.radius = _ins._config.Marker.Radius;
                _mapmarker.alpha = _ins._config.Marker.Alpha;
                _mapmarker.color1 = new Color(_ins._config.Marker.Color.R, _ins._config.Marker.Color.G, _ins._config.Marker.Color.B);

                _vendingMarker = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", transform.position) as VendingMachineMapMarker;
                _vendingMarker.markerShopName = _ins._config.Marker.Name.Replace("{time}", GetTimeFormat());
                _vendingMarker.Spawn();

                InvokeRepeating(UpdateMapMarker, 0, 1f);
            }

            private void UpdateMapMarker()
            {
                _mapmarker.SendUpdate();
                _vendingMarker.markerShopName = _ins._config.Marker.Name.Replace("{time}", GetTimeFormat());
                _vendingMarker.SendNetworkUpdate();
            }

            private static void GetGlobal(Transform Transform, Vector3 localPosition, Vector3 localRotation, out Vector3 globalPosition, out Quaternion globalRotation)
            {
                globalPosition = Transform.TransformPoint(localPosition);
                globalRotation = Transform.rotation * Quaternion.Euler(localRotation);
            }

            internal Vector3 GetGlobalPosition(Vector3 localPosition) => transform.TransformPoint(localPosition);

            private IEnumerator SpawnEntities()
            {
                foreach (Prefab prefab in _ins.Prefabs)
                {
                    if (prefab.prefab == "assets/prefabs/npc/autoturret/autoturret_deployed.prefab" && !_ins._config.Turret.IsTurret) continue;

                    Vector3 pos; Quaternion rot;
                    GetGlobal(transform, prefab.pos.ToVector3(), prefab.rot.ToVector3(), out pos, out rot);
                    BaseEntity entity = GameManager.server.CreateEntity(prefab.prefab, pos, rot);
                    entity.enableSaving = false;

                    if (entity is CardReader)
                    {
                        CardReader reader = entity as CardReader;
                        if (Vector3.Distance(prefab.pos.ToVector3(), new Vector3(1.538f, 3f, 16.985f)) < 1f)
                        {
                            reader.accessLevel = 2;
                            CardReaderBlue = reader;
                        }
                        else
                        {
                            reader.accessLevel = 3;
                            CardReaderRed = reader;
                        }
                    }

                    GroundWatch groundWatch = entity.GetComponent<GroundWatch>();
                    if (groundWatch != null) UnityEngine.Object.DestroyImmediate(groundWatch);

                    DestroyOnGroundMissing destroyOnGroundMissing = entity.GetComponent<DestroyOnGroundMissing>();
                    if (destroyOnGroundMissing != null) UnityEngine.Object.DestroyImmediate(destroyOnGroundMissing);

                    entity.Spawn();

                    if (entity is StabilityEntity) (entity as StabilityEntity).grounded = true;
                    if (entity is BaseCombatEntity) (entity as BaseCombatEntity).pickup.enabled = false;

                    if (entity is BuildingBlock)
                    {
                        BuildingBlock buildingBlock = entity as BuildingBlock;
                        buildingBlock.SetGrade(BuildingGrade.Enum.TopTier);
                        buildingBlock.SetHealthToMax();
                    }

                    if (entity is Door)
                    {
                        Door door = entity as Door;
                        if (door.ShortPrefabName == "door.hinged.security.blue") DoorBlue = door;
                        else if (door.ShortPrefabName == "door.hinged.security.red") DoorRed = door;
                        else if (door.ShortPrefabName == "door.hinged.toptier" || door.ShortPrefabName == "door.double.hinged.toptier")
                        {
                            door.canTakeCloser = false;
                            door.canTakeKnocker = false;
                            door.canTakeLock = false;
                            door.canHandOpen = false;
                            door.hasHatch = false;
                            door.InitializeHealth(_ins._config.DoorHealth, _ins._config.DoorHealth);
                        }
                        Doors.Add(door);
                    }

                    if (entity is Workbench && !_ins._config.IsWorkbench)
                    {
                        entity.SetFlag(BaseEntity.Flags.Locked, true);
                        (entity as Workbench).Workbenchlevel = 0;
                    }

                    if (entity is RepairBench && !_ins._config.IsRepairBench) entity.SetFlag(BaseEntity.Flags.Locked, true);

                    if (entity is ResearchTable && !_ins._config.IsResearchTable) entity.SetFlag(BaseEntity.Flags.Locked, true);

                    if (entity is MixingTable && !_ins._config.IsMixingTable) entity.SetFlag(BaseEntity.Flags.Locked, true);

                    if (entity is Locker)
                    {
                        if (_ins._config.IsLocker)
                        {
                            EntityCrateConfig crateConfig = _ins._config.EntityCrates.FirstOrDefault(x => x.Prefab == prefab.prefab);
                            if (crateConfig != null && crateConfig.IsOwnLootTable)
                            {
                                _ins.NextTick(() =>
                                {
                                    int countLootInContainerWear = 0;
                                    int countLootInContainerBelt = 0;
                                    foreach (ItemConfig item in crateConfig.LootTable.Items)
                                    {
                                        if (UnityEngine.Random.Range(0.0f, 100.0f) <= item.Chance)
                                        {
                                            int amount = UnityEngine.Random.Range(item.MinAmount, item.MaxAmount + 1);
                                            Item newItem = item.IsBluePrint ? ItemManager.CreateByName("blueprintbase") : ItemManager.CreateByName(item.ShortName, amount, item.SkinID);
                                            if (item.IsBluePrint) newItem.blueprintTarget = ItemManager.FindItemDefinition(item.ShortName).itemid;
                                            if (item.Name != "") newItem.name = item.Name;
                                            if (newItem.info.category == ItemCategory.Attire && countLootInContainerWear < 21)
                                            {
                                                int targetPos = countLootInContainerWear;
                                                if (targetPos > 6 && targetPos < 13) targetPos = 13 + countLootInContainerWear - 7;
                                                else if (targetPos > 19 && targetPos < 26) targetPos = 26 + countLootInContainerWear - 14;
                                                if (newItem.MoveToContainer((entity as Locker).inventory, targetPos)) countLootInContainerWear++;
                                                else newItem.Remove();
                                            }
                                            else if (countLootInContainerBelt < 18)
                                            {
                                                int targetPos = countLootInContainerBelt + 7;
                                                if (targetPos > 12 && targetPos < 20) targetPos = 20 + countLootInContainerWear - 6;
                                                else if (targetPos > 25 && targetPos < 33) targetPos = 33 + countLootInContainerWear - 12;
                                                if (newItem.MoveToContainer((entity as Locker).inventory, targetPos)) countLootInContainerBelt++;
                                                else newItem.Remove();
                                            }
                                            if (countLootInContainerWear + countLootInContainerBelt >= crateConfig.LootTable.Max) break;
                                        }
                                    }
                                });
                            }
                        }
                        else entity.SetFlag(BaseEntity.Flags.Locked, true);
                    }

                    if (entity is BoxStorage)
                    {
                        if (_ins._config.IsBoxStorage)
                        {
                            EntityCrateConfig crateConfig = _ins._config.EntityCrates.FirstOrDefault(x => x.Prefab == prefab.prefab);
                            if (crateConfig != null && crateConfig.IsOwnLootTable) _ins.NextTick(() => _ins.AddToContainerItem((entity as BoxStorage).inventory, crateConfig.LootTable));
                        }
                        else entity.SetFlag(BaseEntity.Flags.Locked, true);
                    }

                    if (entity is BaseOven)
                    {
                        entity.SetFlag(BaseEntity.Flags.Locked, true);
                        if (entity.ShortPrefabName == "lantern.deployed") entity.SetFlag(BaseEntity.Flags.On, true);
                    }

                    if (entity is CCTV_RC)
                    {
                        _countCctv++;
                        CCTV_RC cctv = entity as CCTV_RC;
                        cctv.UpdateFromInput(5, 0);
                        cctv.rcIdentifier = $"Submarine{_countCctv}";
                    }

                    if (entity is PlanterBox || entity is StashContainer || entity is GunTrap || entity is DropBox) entity.SetFlag(BaseEntity.Flags.Locked, true);

                    if (entity is SkullTrophy)
                    {
                        entity.SetFlag(BaseEntity.Flags.Locked, true);
                        Item item = ItemManager.CreateByName("skull.human", 1);
                        int number = UnityEngine.Random.Range(1, 101);
                        if (number < 50) item.name = "SKULL OF \u0022KpucTaJl\u0022";
                        else if (number > 80) item.name = "SKULL OF \u0022Gruber\u0022";
                        else item.name = "SKULL OF \u0022Jtedal\u0022";
                        item.MoveToContainer((entity as SkullTrophy).inventory);
                    }

                    if (entity is PoweredWaterPurifier) (entity as PoweredWaterPurifier).inventory.capacity = 0;
                    if (entity is FuelGenerator) (entity as FuelGenerator).inventory.capacity = 0;

                    if (entity is ElectricalHeater) (entity as ElectricalHeater).UpdateFromInput(3, 0);
                    if (entity is AudioAlarm) (entity as AudioAlarm).UpdateFromInput(1, 0);
                    if (entity is SirenLight) (entity as SirenLight).UpdateFromInput(1, 0);

                    if (entity is CardReader) (entity as CardReader).UpdateFromInput(1, 0);

                    if (entity is PressButton)
                    {
                        if (Vector3.Distance(prefab.pos.ToVector3(), new Vector3(1.466f, 2.859f, 17.164f)) < 1f) ButtonBlue = entity as PressButton;
                        else ButtonRed = entity as PressButton;
                    }

                    if (entity is ComputerStation)
                    {
                        ComputerStation computer = entity as ComputerStation;
                        for (int i = 1; i <= 4; i++) computer.ForceAddBookmark($"Submarine{i}");
                    }

                    if (entity is AutoTurret)
                    {
                        AutoTurret turret = entity as AutoTurret;
                        turret.inventory.Insert(ItemManager.CreateByName(_ins._config.Turret.ShortNameWeapon));
                        turret.inventory.Insert(ItemManager.CreateByName(_ins._config.Turret.ShortNameAmmo, _ins._config.Turret.CountAmmo));
                        turret.SendNetworkUpdate();
                        turret.UpdateFromInput(10, 0);
                        turret.InitializeHealth(_ins._config.Turret.Hp, _ins._config.Turret.Hp);
                        Turrets.Add(turret);
                    }

                    Entities.Add(entity);

                    yield return CoroutineEx.waitForSeconds(_ins._config.Delay);
                }
                SpawnCrates();
                SpawnHackCrates();
                foreach (PresetConfig preset in _ins._config.OutsideNpc) SpawnPreset(preset);
                if (_ins._config.PveMode.Pve && _ins.plugins.Exists("PveMode"))
                {
                    JObject config = new JObject
                    {
                        ["Damage"] = _ins._config.PveMode.Damage,
                        ["ScaleDamage"] = new JArray { _ins._config.PveMode.ScaleDamage.Select(x => new JObject { ["Type"] = x.Type, ["Scale"] = x.Scale }) },
                        ["LootCrate"] = _ins._config.PveMode.LootCrate,
                        ["HackCrate"] = _ins._config.PveMode.HackCrate,
                        ["LootNpc"] = _ins._config.PveMode.LootNpc,
                        ["DamageNpc"] = _ins._config.PveMode.DamageNpc,
                        ["DamageTank"] = false,
                        ["DamageHelicopter"] = false,
                        ["TargetNpc"] = _ins._config.PveMode.TargetNpc,
                        ["TargetTank"] = false,
                        ["TargetHelicopter"] = false,
                        ["CanEnter"] = _ins._config.PveMode.CanEnter,
                        ["CanEnterCooldownPlayer"] = _ins._config.PveMode.CanEnterCooldownPlayer,
                        ["TimeExitOwner"] = _ins._config.PveMode.TimeExitOwner,
                        ["AlertTime"] = _ins._config.PveMode.AlertTime,
                        ["RestoreUponDeath"] = _ins._config.PveMode.RestoreUponDeath,
                        ["CooldownOwner"] = _ins._config.PveMode.CooldownOwner,
                        ["Darkening"] = _ins._config.PveMode.Darkening
                    };
                    HashSet<ulong> crates = Crates.Select(x => x.net.ID.Value);
                    foreach (HackableLockedCrate crate in HackCrates) crates.Add(crate.net.ID.Value);
                    _ins.PveMode.Call("EventAddPveMode", _ins.Name, config, transform.position, _ins._config.Radius, crates, Scientists.Select(x => x.net.ID.Value), new HashSet<ulong>(), new HashSet<ulong>(), new HashSet<ulong>(), null);
                }
                InvokeRepeating(ChangeToFinishTime, 1f, 1f);
                Interface.Oxide.CallHook("OnWaterEventStart", Entities, transform.position, _ins._config.Radius);
            }

            private void SpawnCrates()
            {
                foreach (Prefab prefab in _ins.Crates)
                {
                    Vector3 pos; Quaternion rot;
                    GetGlobal(transform, prefab.pos.ToVector3(), prefab.rot.ToVector3(), out pos, out rot);
                    LootContainer crate = GameManager.server.CreateEntity(prefab.prefab, pos, rot) as LootContainer;
                    crate.enableSaving = false;
                    crate.Spawn();
                    Crates.Add(crate);
                    CrateConfig crateConfig = _ins._config.DefaultCrates.FirstOrDefault(x => x.Prefab == prefab.prefab);
                    if (crateConfig == null) return;
                    if (crateConfig.TypeLootTable == 1 || crateConfig.TypeLootTable == 4 || crateConfig.TypeLootTable == 5)
                    {
                        _ins.NextTick(() =>
                        {
                            crate.inventory.ClearItemsContainer();
                            if (crateConfig.TypeLootTable == 4 || crateConfig.TypeLootTable == 5) _ins.AddToContainerPrefab(crate.inventory, crateConfig.PrefabLootTable);
                            if (crateConfig.TypeLootTable == 1 || crateConfig.TypeLootTable == 5) _ins.AddToContainerItem(crate.inventory, crateConfig.OwnLootTable);
                        });
                    }
                }
            }

            private void SpawnHackCrates()
            {
                foreach (CoordConfig coord in _ins._config.HackCrate.Coordinates)
                {
                    Vector3 pos; Quaternion rot;
                    GetGlobal(transform, coord.Position.ToVector3(), coord.Rotation.ToVector3(), out pos, out rot);
                    HackableLockedCrate hackCrate = GameManager.server.CreateEntity("assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab", pos, rot) as HackableLockedCrate;
                    hackCrate.enableSaving = false;
                    hackCrate.Spawn();
                    hackCrate.hackSeconds = HackableLockedCrate.requiredHackSeconds - _ins._config.HackCrate.UnlockTime;
                    HackCrates.Add(hackCrate);
                    if (_ins._config.HackCrate.TypeLootTable == 1 || _ins._config.HackCrate.TypeLootTable == 4 || _ins._config.HackCrate.TypeLootTable == 5)
                    {
                        _ins.NextTick(() =>
                        {
                            hackCrate.inventory.ClearItemsContainer();
                            if (_ins._config.HackCrate.TypeLootTable == 4 || _ins._config.HackCrate.TypeLootTable == 5) _ins.AddToContainerPrefab(hackCrate.inventory, _ins._config.HackCrate.PrefabLootTable);
                            if (_ins._config.HackCrate.TypeLootTable == 1 || _ins._config.HackCrate.TypeLootTable == 5) _ins.AddToContainerItem(hackCrate.inventory, _ins._config.HackCrate.OwnLootTable);
                        });
                    }
                }
            }

            private void SpawnPreset(PresetConfig preset)
            {
                int count = UnityEngine.Random.Range(preset.Min, preset.Max + 1);
                List<Vector3> positions = preset.Positions.Select(x => transform.TransformPoint(x.ToVector3()));
                JObject config = GetObjectConfig(preset.Config);
                for (int i = 0; i < count; i++)
                {
                    Vector3 pos = positions.GetRandom();
                    positions.Remove(pos);
                    ScientistNPC npc = (ScientistNPC)_ins.NpcSpawn.Call("SpawnNpc", pos, config);
                    Scientists.Add(npc);
                }
            }

            private static JObject GetObjectConfig(NpcConfig config)
            {
                return new JObject
                {
                    ["Name"] = config.Name,
                    ["WearItems"] = new JArray { config.WearItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["SkinID"] = x.SkinID }) },
                    ["BeltItems"] = new JArray { config.BeltItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["Amount"] = x.Amount, ["SkinID"] = x.SkinID, ["Mods"] = new JArray { x.Mods }, ["Ammo"] = x.Ammo }) },
                    ["Kit"] = config.Kit,
                    ["Health"] = config.Health,
                    ["RoamRange"] = 0f,
                    ["ChaseRange"] = 0f,
                    ["SenseRange"] = config.SenseRange,
                    ["ListenRange"] = config.SenseRange / 2f,
                    ["AttackRangeMultiplier"] = config.AttackRangeMultiplier,
                    ["CheckVisionCone"] = config.CheckVisionCone,
                    ["VisionCone"] = config.VisionCone,
                    ["DamageScale"] = config.DamageScale,
                    ["TurretDamageScale"] = 1f,
                    ["AimConeScale"] = config.AimConeScale,
                    ["DisableRadio"] = config.DisableRadio,
                    ["CanRunAwayWater"] = false,
                    ["CanSleep"] = false,
                    ["Speed"] = 0f,
                    ["AreaMask"] = 1,
                    ["AgentTypeID"] = -1372625422,
                    ["HomePosition"] = string.Empty,
                    ["MemoryDuration"] = config.MemoryDuration,
                    ["States"] = new JArray { "IdleState", "CombatStationaryState" }
                };
            }

            public class DoorsToPresets { public HashSet<Vector3> doors; public HashSet<PresetConfig> presets; }

            private void CalcDoorsToPresets()
            {
                foreach (DoorsToScientists doorsToScientists in _ins._config.DoorsToScientists)
                {
                    _doorsToPresets.Add(new DoorsToPresets
                    {
                        doors = doorsToScientists.Doors.Select(x => GetGlobalPosition(x.ToVector3())),
                        presets = doorsToScientists.Scientists
                    });
                }
            }

            internal void KillDoor(Vector3 positionDoor)
            {
                DoorsToPresets doorsToPresets = _doorsToPresets.FirstOrDefault(x => x.doors.Any(y => Vector3.Distance(positionDoor, y) < 1f));
                if (doorsToPresets == null) return;
                foreach (PresetConfig preset in doorsToPresets.presets) SpawnPreset(preset);
                if (_ins._config.PveMode.Pve && _ins.plugins.Exists("PveMode")) _ins.PveMode.Call("EventAddScientists", _ins.Name, Scientists.Select(x => x.net.ID.Value));
                _doorsToPresets.Remove(doorsToPresets);
            }

            private string GetTimeFormat()
            {
                if (TimeToFinish <= 60) return $"{TimeToFinish} sec.";
                else
                {
                    int sec = TimeToFinish % 60;
                    int min = (TimeToFinish - sec) / 60;
                    return $"{min} min. {sec} sec.";
                }
            }
        }
        #endregion Controller

        #region Position Submarine
        internal Vector3 SpawnPos;

        private void RandomSpawnPos()
        {
            Dictionary<Vector3, float> monuments = new Dictionary<Vector3, float>();
            foreach (MonumentInfo monument in TerrainMeta.Path.Monuments)
            {
                if (monument.name.Contains("OilrigAI")) monuments.Add(monument.transform.position, 70f);
                else if (monument.name.Contains("OilrigAI2")) monuments.Add(monument.transform.position, 90f);
                else if (monument.name.Contains("fishing_village_a")) monuments.Add(monument.transform.position, 50f);
                else if (monument.name.Contains("fishing_village_b")) monuments.Add(monument.transform.position, 40f);
                else if (monument.name.Contains("fishing_village_c")) monuments.Add(monument.transform.position, 40f);
                else if (monument.name.Contains("harbor_1")) monuments.Add(monument.transform.position, 130f);
                else if (monument.name.Contains("harbor_2")) monuments.Add(monument.transform.position, 150f);
            }

            int attempts = 0;

            while (SpawnPos == Vector3.zero && attempts < 1000)
            {
                attempts++;

                SpawnPos = new Vector3(UnityEngine.Random.Range(-World.Size * 2 / 4f, World.Size * 2 / 4f), 0.5f, UnityEngine.Random.Range(-World.Size * 2 / 4f, World.Size * 2 / 4f));

                if (GetMajorityBiome(SpawnPos) == BiomeType.Arctic)
                {
                    SpawnPos = Vector3.zero;
                    continue;
                }

                if (!IsValidHeight(SpawnPos) ||
                    !IsValidHeight(SpawnPos + new Vector3(_config.Radius, 0f, 0f)) ||
                    !IsValidHeight(SpawnPos + new Vector3(-_config.Radius, 0f, 0f)) ||
                    !IsValidHeight(SpawnPos + new Vector3(0f, 0f, _config.Radius)) ||
                    !IsValidHeight(SpawnPos + new Vector3(0f, 0f, -_config.Radius)) ||
                    !IsValidHeight(SpawnPos + new Vector3(_config.Radius, 0f, _config.Radius)) ||
                    !IsValidHeight(SpawnPos + new Vector3(-_config.Radius, 0f, -_config.Radius)) ||
                    !IsValidHeight(SpawnPos + new Vector3(-_config.Radius, 0f, _config.Radius)) ||
                    !IsValidHeight(SpawnPos + new Vector3(_config.Radius, 0f, -_config.Radius)))
                {
                    SpawnPos = Vector3.zero;
                    continue;
                }

                bool next = false;

                foreach (KeyValuePair<Vector3, float> dic in monuments)
                {
                    if (Vector3.Distance(dic.Key, SpawnPos) <= dic.Value + _config.Radius)
                    {
                        next = true;
                        break;
                    }
                }
                if (next)
                {
                    SpawnPos = Vector3.zero;
                    continue;
                }

                foreach (Collider collider in Physics.OverlapSphere(SpawnPos, _config.Radius + _config.DistanceToBlock, 1 << 17 | 1 << 21))
                {
                    BaseEntity entity = collider.ToBaseEntity();
                    if (entity != null && (entity is BasePlayer || entity is BuildingBlock))
                    {
                        next = true;
                        break;
                    }
                }
                if (next)
                {
                    SpawnPos = Vector3.zero;
                    continue;
                }

                if (GetDistanceToOceanPath(SpawnPos) <= _config.Radius + 50f)
                {
                    SpawnPos = Vector3.zero;
                    continue;
                }
            }
        }

        private enum BiomeType { Arid, Arctic, Temperate, Tundra }

        private static BiomeType GetMajorityBiome(Vector3 position)
        {
            Dictionary<BiomeType, float> biomes = new Dictionary<BiomeType, float>
            {
                {BiomeType.Arctic, TerrainMeta.BiomeMap.GetBiome(position, TerrainBiome.ARCTIC) },
                {BiomeType.Arid, TerrainMeta.BiomeMap.GetBiome(position, TerrainBiome.ARID) },
                {BiomeType.Temperate, TerrainMeta.BiomeMap.GetBiome(position, TerrainBiome.TEMPERATE) },
                {BiomeType.Tundra, TerrainMeta.BiomeMap.GetBiome(position, TerrainBiome.TUNDRA) }
            };
            return biomes.Max(x => x.Value).Key;
        }

        private static bool IsValidHeight(Vector3 pos)
        {
            float height = TerrainMeta.HeightMap.GetHeight(pos);
            if (height > 0 || Math.Abs(height) <= 10f) return false;
            else return true;
        }

        private static double GetDistanceToOceanPath(Vector3 pos)
        {
            int index = TerrainMeta.Path.OceanPatrolFar.IndexOf(TerrainMeta.Path.OceanPatrolFar.OrderBy(i => Vector3.Distance(i, pos)).First());
            int indexNext = TerrainMeta.Path.OceanPatrolFar.Count - 1 == index ? 0 : index + 1;
            int indexPrevious = index == 0 ? TerrainMeta.Path.OceanPatrolFar.Count - 1 : index - 1;
            double distanceNext = GetDistanceForIndexs(indexNext, index, pos);
            double distancePrevious = GetDistanceForIndexs(index, indexPrevious, pos);
            return distanceNext < distancePrevious ? distanceNext : distancePrevious;
        }

        private static double GetDistanceForIndexs(int indexNext, int indexPrevious, Vector3 pos)
        {
            Vector3 posNext = TerrainMeta.Path.OceanPatrolFar[indexNext];
            Vector3 posPrevious = TerrainMeta.Path.OceanPatrolFar[indexPrevious];

            Vector3 vectorNext = GetVectorCoord(pos, posNext);
            Vector3 vectorPrevious = GetVectorCoord(pos, posPrevious);
            Vector3 vectorBetween = GetVectorCoord(posPrevious, posNext);

            double distanceNext = Math.Sqrt(Math.Pow(vectorNext.x, 2) + Math.Pow(vectorNext.y, 2) + Math.Pow(vectorNext.z, 2));
            double distancePrevious = Math.Sqrt(Math.Pow(vectorPrevious.x, 2) + Math.Pow(vectorPrevious.y, 2) + Math.Pow(vectorPrevious.z, 2));
            double distanceBetween = Math.Sqrt(Math.Pow(vectorBetween.x, 2) + Math.Pow(vectorBetween.y, 2) + Math.Pow(vectorBetween.z, 2));

            double p = (distanceNext + distancePrevious + distanceBetween) / 2;

            return ((2 / distanceBetween) * Math.Sqrt(p * (p - distanceNext) * (p - distancePrevious) * (p - distanceBetween)));
        }

        private static Vector3 GetVectorCoord(Vector3 a, Vector3 b) => new Vector3(b.x - a.x, b.y - a.y, b.z - a.z);
        #endregion Position Submarine

        #region Spawn Loot
        #region NPC
        private void OnCorpsePopulate(ScientistNPC entity, NPCPlayerCorpse corpse)
        {
            if (entity == null) return;
            if (_controller.Scientists.Contains(entity))
            {
                _controller.Scientists.Remove(entity);
                PresetConfig preset = GetPresetNpc(entity.displayName);
                NextTick(() =>
                {
                    if (corpse == null) return;
                    ItemContainer container = corpse.containers[0];
                    if (preset.TypeLootTable == 0)
                    {
                        for (int i = container.itemList.Count - 1; i >= 0; i--)
                        {
                            Item item = container.itemList[i];
                            if (preset.Config.WearItems.Any(x => x.ShortName == item.info.shortname))
                            {
                                item.RemoveFromContainer();
                                item.Remove();
                            }
                        }
                        return;
                    }
                    if (preset.TypeLootTable == 2 || preset.TypeLootTable == 3)
                    {
                        if (preset.Config.IsRemoveCorpse && !corpse.IsDestroyed) corpse.Kill();
                        return;
                    }
                    container.ClearItemsContainer();
                    if (preset.TypeLootTable == 4 || preset.TypeLootTable == 5) AddToContainerPrefab(container, preset.PrefabLootTable);
                    if (preset.TypeLootTable == 1 || preset.TypeLootTable == 5) AddToContainerItem(container, preset.OwnLootTable);
                    if (preset.Config.IsRemoveCorpse && !corpse.IsDestroyed) corpse.Kill();
                });
            }
        }

        private object CanPopulateLoot(ScientistNPC entity, NPCPlayerCorpse corpse)
        {
            if (entity == null || corpse == null || _controller == null) return null;
            if (_controller.Scientists.Contains(entity))
            {
                PresetConfig preset = GetPresetNpc(entity.displayName);
                if (preset.TypeLootTable == 2) return null;
                else return true;
            }
            return null;
        }

        private object OnCustomLootNPC(NetworkableId netID)
        {
            if (_controller == null) return null;
            ScientistNPC entity = _controller.Scientists.FirstOrDefault(x => x.IsExists() && x.net.ID.Value == netID.Value);
            if (entity != null)
            {
                PresetConfig preset = GetPresetNpc(entity.displayName);
                if (preset.TypeLootTable == 3) return null;
                else return true;
            }
            return null;
        }

        private PresetConfig GetPresetNpc(string name)
        {
            PresetConfig preset = _config.OutsideNpc.FirstOrDefault(x => x.Config.Name == name);
            if (preset == null)
            {
                DoorsToScientists doorsToScientists = _config.DoorsToScientists.FirstOrDefault(x => x.Scientists.Any(y => y.Config.Name == name));
                preset = doorsToScientists.Scientists.FirstOrDefault(x => x.Config.Name == name);
            }
            return preset;
        }
        #endregion NPC

        #region Crates
        private readonly HashSet<ulong> _lootableCrates = new HashSet<ulong>();

        private void OnLootEntity(BasePlayer player, LootContainer container)
        {
            if (player == null || container == null || _lootableCrates.Contains(container.net.ID.Value)) return;
            if (_controller.Crates.Contains(container))
            {
                _lootableCrates.Add(container.net.ID.Value);
                ActionEconomy(player.userID, "Crates", container.ShortPrefabName);
                CrateConfig crateConfig = _config.DefaultCrates.FirstOrDefault(x => x.Prefab == container.PrefabName);
                if (crateConfig == null || crateConfig.ScaleItems.Count == 0) return;
                foreach (Item item in container.inventory.itemList)
                {
                    float scale;
                    if (crateConfig.ScaleItems.TryGetValue(item.info.shortname, out scale))
                    {
                        item.amount = (int)(item.amount * scale);
                        item.MarkDirty();
                    }
                }
            }
            else if (container is HackableLockedCrate && _controller.HackCrates.Contains(container as HackableLockedCrate))
            {
                _lootableCrates.Add(container.net.ID.Value);
                if (_config.HackCrate.ScaleItems.Count == 0) return;
                foreach (Item item in container.inventory.itemList)
                {
                    float scale;
                    if (_config.HackCrate.ScaleItems.TryGetValue(item.info.shortname, out scale))
                    {
                        item.amount = (int)(item.amount * scale);
                        item.MarkDirty();
                    }
                }
            }
        }

        private object CanPopulateLoot(LootContainer container)
        {
            if (container == null || _controller == null) return null;
            if (_controller.Crates.Contains(container))
            {
                CrateConfig crateConfig = _config.DefaultCrates.FirstOrDefault(x => x.Prefab == container.PrefabName);
                if (crateConfig == null) return true;
                if (crateConfig.TypeLootTable == 2) return null;
                else return true;
            }
            else if (container is HackableLockedCrate && _controller.HackCrates.Contains(container as HackableLockedCrate))
            {
                if (_config.HackCrate.TypeLootTable == 2) return null;
                else return true;
            }
            else return null;
        }

        private object OnCustomLootContainer(NetworkableId netID)
        {
            if (_controller == null) return null;
            LootContainer crate = _controller.Crates.FirstOrDefault(x => x.IsExists() && x.net.ID.Value == netID.Value);
            if (crate != null)
            {
                CrateConfig crateConfig = _config.DefaultCrates.FirstOrDefault(x => x.Prefab == crate.PrefabName);
                if (crateConfig == null) return true;
                if (crateConfig.TypeLootTable == 3) return null;
                else return true;
            }
            else if (_controller.HackCrates.Any(x => x.IsExists() && x.net.ID.Value == netID.Value))
            {
                if (_config.HackCrate.TypeLootTable == 3) return null;
                else return true;
            }
            return null;
        }

        private object OnContainerPopulate(LootContainer container)
        {
            if (container == null || _controller == null) return null;
            if (_controller.Crates.Contains(container))
            {
                CrateConfig crateConfig = _config.DefaultCrates.FirstOrDefault(x => x.Prefab == container.PrefabName);
                if (crateConfig == null) return true;
                if (crateConfig.TypeLootTable == 6) return null;
                else return true;
            }
            else if (container is HackableLockedCrate && _controller.HackCrates.Contains(container as HackableLockedCrate))
            {
                if (_config.HackCrate.TypeLootTable == 6) return null;
                else return true;
            }
            else return null;
        }
        #endregion Crates

        private void AddToContainerPrefab(ItemContainer container, PrefabLootTableConfig lootTable)
        {
            HashSet<string> prefabsInContainer = new HashSet<string>();
            if (lootTable.UseCount)
            {
                int count = 0, max = UnityEngine.Random.Range(lootTable.Min, lootTable.Max + 1);
                while (count < max)
                {
                    foreach (PrefabConfig prefab in lootTable.Prefabs)
                    {
                        if (prefabsInContainer.Count < lootTable.Prefabs.Count && prefabsInContainer.Contains(prefab.PrefabDefinition)) continue;
                        if (UnityEngine.Random.Range(0f, 100f) > prefab.Chance) continue;
                        SpawnIntoContainer(container, prefab.PrefabDefinition);
                        if (!prefabsInContainer.Contains(prefab.PrefabDefinition)) prefabsInContainer.Add(prefab.PrefabDefinition);
                        count++; if (count == max) return;
                    }
                }
            }
            else
            {
                foreach (PrefabConfig prefab in lootTable.Prefabs)
                {
                    if (prefabsInContainer.Contains(prefab.PrefabDefinition)) continue;
                    if (UnityEngine.Random.Range(0f, 100f) > prefab.Chance) continue;
                    SpawnIntoContainer(container, prefab.PrefabDefinition);
                    prefabsInContainer.Add(prefab.PrefabDefinition);
                }
            }
        }

        private void SpawnIntoContainer(ItemContainer container, string prefab)
        {
            if (_allLootSpawnSlots.ContainsKey(prefab))
            {
                foreach (LootContainer.LootSpawnSlot lootSpawnSlot in _allLootSpawnSlots[prefab])
                    for (int j = 0; j < lootSpawnSlot.numberToSpawn; j++)
                        if (UnityEngine.Random.Range(0f, 1f) <= lootSpawnSlot.probability)
                            lootSpawnSlot.definition.SpawnIntoContainer(container);
            }
            else _allLootSpawn[prefab].SpawnIntoContainer(container);
        }

        private void AddToContainerItem(ItemContainer container, LootTableConfig lootTable)
        {
            HashSet<int> indexMove = new HashSet<int>();
            if (lootTable.UseCount)
            {
                int count = UnityEngine.Random.Range(lootTable.Min, lootTable.Max + 1);
                while (indexMove.Count < count)
                {
                    foreach (ItemConfig item in lootTable.Items)
                    {
                        if (indexMove.Contains(lootTable.Items.IndexOf(item))) continue;
                        if (UnityEngine.Random.Range(0.0f, 100.0f) <= item.Chance)
                        {
                            Item newItem = item.IsBluePrint ? ItemManager.CreateByName("blueprintbase") : ItemManager.CreateByName(item.ShortName, UnityEngine.Random.Range(item.MinAmount, item.MaxAmount + 1), item.SkinID);
                            if (newItem == null)
                            {
                                PrintWarning($"Failed to create item! ({item.ShortName})");
                                continue;
                            }
                            if (item.IsBluePrint) newItem.blueprintTarget = ItemManager.FindItemDefinition(item.ShortName).itemid;
                            if (!string.IsNullOrEmpty(item.Name)) newItem.name = item.Name;
                            if (container.capacity < container.itemList.Count + 1) container.capacity++;
                            if (!newItem.MoveToContainer(container)) newItem.Remove();
                            else
                            {
                                indexMove.Add(lootTable.Items.IndexOf(item));
                                if (indexMove.Count == count) return;
                            }
                        }
                    }
                }
            }
            else
            {
                foreach (ItemConfig item in lootTable.Items)
                {
                    if (indexMove.Contains(lootTable.Items.IndexOf(item))) continue;
                    if (UnityEngine.Random.Range(0.0f, 100.0f) <= item.Chance)
                    {
                        Item newItem = item.IsBluePrint ? ItemManager.CreateByName("blueprintbase") : ItemManager.CreateByName(item.ShortName, UnityEngine.Random.Range(item.MinAmount, item.MaxAmount + 1), item.SkinID);
                        if (newItem == null)
                        {
                            PrintWarning($"Failed to create item! ({item.ShortName})");
                            continue;
                        }
                        if (item.IsBluePrint) newItem.blueprintTarget = ItemManager.FindItemDefinition(item.ShortName).itemid;
                        if (!string.IsNullOrEmpty(item.Name)) newItem.name = item.Name;
                        if (container.capacity < container.itemList.Count + 1) container.capacity++;
                        if (!newItem.MoveToContainer(container)) newItem.Remove();
                        else indexMove.Add(lootTable.Items.IndexOf(item));
                    }
                }
            }
        }

        private void CheckAllLootTables()
        {
            foreach (EntityCrateConfig entityCrateConfig in _config.EntityCrates) CheckLootTable(entityCrateConfig.LootTable);

            foreach (CrateConfig crateConfig in _config.DefaultCrates)
            {
                CheckLootTable(crateConfig.OwnLootTable);
                CheckPrefabLootTable(crateConfig.PrefabLootTable);
            }

            CheckLootTable(_config.HackCrate.OwnLootTable);
            CheckPrefabLootTable(_config.HackCrate.PrefabLootTable);

            foreach (PresetConfig preset in _config.OutsideNpc)
            {
                CheckLootTable(preset.OwnLootTable);
                CheckPrefabLootTable(preset.PrefabLootTable);
            }
            foreach (DoorsToScientists doorsToScientists in _config.DoorsToScientists)
            {
                foreach (PresetConfig preset in doorsToScientists.Scientists)
                {
                    CheckLootTable(preset.OwnLootTable);
                    CheckPrefabLootTable(preset.PrefabLootTable);
                }
            }

            SaveConfig();
        }

        private static void CheckLootTable(LootTableConfig lootTable)
        {
            lootTable.Items = lootTable.Items.OrderBy(x => x.Chance);
            if (lootTable.Max > lootTable.Items.Count) lootTable.Max = lootTable.Items.Count;
            if (lootTable.Min > lootTable.Max) lootTable.Min = lootTable.Max;
        }

        private void CheckPrefabLootTable(PrefabLootTableConfig lootTable)
        {
            HashSet<PrefabConfig> prefabs = new HashSet<PrefabConfig>();
            foreach (PrefabConfig prefabConfig in lootTable.Prefabs)
            {
                if (prefabs.Any(x => x.PrefabDefinition == prefabConfig.PrefabDefinition)) PrintWarning($"Duplicate prefab removed from loot table! ({prefabConfig.PrefabDefinition})");
                else
                {
                    GameObject gameObject = GameManager.server.FindPrefab(prefabConfig.PrefabDefinition);
                    global::HumanNPC humanNpc = gameObject.GetComponent<global::HumanNPC>();
                    ScarecrowNPC scarecrowNPC = gameObject.GetComponent<ScarecrowNPC>();
                    LootContainer lootContainer = gameObject.GetComponent<LootContainer>();
                    if (humanNpc != null && humanNpc.LootSpawnSlots.Length != 0)
                    {
                        if (!_allLootSpawnSlots.ContainsKey(prefabConfig.PrefabDefinition)) _allLootSpawnSlots.Add(prefabConfig.PrefabDefinition, humanNpc.LootSpawnSlots);
                        prefabs.Add(prefabConfig);
                    }
                    else if (scarecrowNPC != null && scarecrowNPC.LootSpawnSlots.Length != 0)
                    {
                        if (!_allLootSpawnSlots.ContainsKey(prefabConfig.PrefabDefinition)) _allLootSpawnSlots.Add(prefabConfig.PrefabDefinition, scarecrowNPC.LootSpawnSlots);
                        prefabs.Add(prefabConfig);
                    }
                    else if (lootContainer != null && lootContainer.LootSpawnSlots.Length != 0)
                    {
                        if (!_allLootSpawnSlots.ContainsKey(prefabConfig.PrefabDefinition)) _allLootSpawnSlots.Add(prefabConfig.PrefabDefinition, lootContainer.LootSpawnSlots);
                        prefabs.Add(prefabConfig);
                    }
                    else if (lootContainer != null && lootContainer.lootDefinition != null)
                    {
                        if (!_allLootSpawn.ContainsKey(prefabConfig.PrefabDefinition)) _allLootSpawn.Add(prefabConfig.PrefabDefinition, lootContainer.lootDefinition);
                        prefabs.Add(prefabConfig);
                    }
                    else PrintWarning($"Unknown prefab removed! ({prefabConfig.PrefabDefinition})");
                }
            }
            lootTable.Prefabs = prefabs.OrderBy(x => x.Chance);
            if (lootTable.Min > lootTable.Max) lootTable.Min = lootTable.Max;
        }

        private readonly Dictionary<string, LootSpawn> _allLootSpawn = new Dictionary<string, LootSpawn>();

        private readonly Dictionary<string, LootContainer.LootSpawnSlot[]> _allLootSpawnSlots = new Dictionary<string, LootContainer.LootSpawnSlot[]>();
        #endregion Spawn Loot

        #region TruePVE
        private object CanEntityTakeDamage(BasePlayer victim, HitInfo hitinfo)
        {
            if (!victim.IsPlayer() || hitinfo == null || _controller == null) return null;
            BaseEntity attacker = hitinfo.Initiator;
            if (attacker is AutoTurret && _controller.Turrets.Contains(attacker as AutoTurret)) return true;
            else if (attacker is BasePlayer && _config.IsCreateZonePvp && _controller.Players.Contains(victim) && (attacker == null || _controller.Players.Contains(attacker as BasePlayer))) return true;
            else return null;
        }

        private object CanEntityTakeDamage(Door victim, HitInfo hitinfo)
        {
            if (victim == null || hitinfo == null || _controller == null) return null;
            BasePlayer attacker = hitinfo.InitiatorPlayer;
            if (attacker.IsPlayer() && _controller.Entities.Contains(victim)) return true;
            else return null;
        }

        private object CanEntityTakeDamage(AutoTurret victim, HitInfo hitinfo)
        {
            if (victim == null || hitinfo == null || _controller == null) return null;
            BasePlayer attacker = hitinfo.InitiatorPlayer;
            if (attacker.IsPlayer() && _controller.Turrets.Contains(victim)) return true;
            else return null;
        }

        private object CanEntityBeTargeted(BasePlayer player, AutoTurret turret)
        {
            if (!player.IsPlayer() || turret == null || _controller == null) return null;
            if (_controller.Turrets.Contains(turret)) return true;
            else return null;
        }
        #endregion TruePVE

        #region NTeleportation
        private object CanTeleport(BasePlayer player, Vector3 to)
        {
            if (_config.NTeleportationInterrupt && _controller != null && (_controller.Players.Contains(player) || Vector3.Distance(_controller.transform.position, to) < _config.Radius)) return GetMessage("NTeleportation", player.UserIDString, _config.Prefix);
            else return null;
        }
        #endregion NTeleportation

        #region Economy
        [PluginReference] private readonly Plugin Economics, ServerRewards, IQEconomic;

        private readonly Dictionary<ulong, double> _playersBalance = new Dictionary<ulong, double>();

        private void ActionEconomy(ulong playerId, string type, string arg = "")
        {
            switch (type)
            {
                case "Crates":
                    if (_config.Economy.Crates.ContainsKey(arg)) AddBalance(playerId, _config.Economy.Crates[arg]);
                    break;
                case "Doors":
                    if (_config.Economy.Doors.ContainsKey(arg)) AddBalance(playerId, _config.Economy.Doors[arg]);
                    break;
                case "Npc":
                    AddBalance(playerId, _config.Economy.Npc);
                    break;
                case "LockedCrate":
                    AddBalance(playerId, _config.Economy.LockedCrate);
                    break;
                case "Cards":
                    if (_config.Economy.Cards.ContainsKey(arg)) AddBalance(playerId, _config.Economy.Cards[arg]);
                    break;
            }
        }

        private void AddBalance(ulong playerId, double balance)
        {
            if (balance == 0) return;
            if (_playersBalance.ContainsKey(playerId)) _playersBalance[playerId] += balance;
            else _playersBalance.Add(playerId, balance);
        }

        private void SendBalance()
        {
            if (_playersBalance.Count == 0) return;
            if (_config.Economy.Plugins.Count > 0)
            {
                foreach (KeyValuePair<ulong, double> dic in _playersBalance)
                {
                    if (dic.Value < _config.Economy.Min) continue;
                    int intCount = Convert.ToInt32(dic.Value);
                    if (_config.Economy.Plugins.Contains("Economics") && plugins.Exists("Economics") && dic.Value > 0) Economics.Call("Deposit", dic.Key.ToString(), dic.Value);
                    if (_config.Economy.Plugins.Contains("Server Rewards") && plugins.Exists("ServerRewards") && intCount > 0) ServerRewards.Call("AddPoints", dic.Key, intCount);
                    if (_config.Economy.Plugins.Contains("IQEconomic") && plugins.Exists("IQEconomic") && intCount > 0) IQEconomic.Call("API_SET_BALANCE", dic.Key, intCount);
                    BasePlayer player = BasePlayer.FindByID(dic.Key);
                    if (player != null) AlertToPlayer(player, GetMessage("SendEconomy", player.UserIDString, _config.Prefix, dic.Value));
                }
            }
            ulong winnerId = _playersBalance.Max(x => x.Value).Key;
            Interface.Oxide.CallHook("OnWaterEventWinner", winnerId);
            foreach (string command in _config.Economy.Commands) Server.Command(command.Replace("{steamid}", $"{winnerId}"));
            _playersBalance.Clear();
        }
        #endregion Economy

        #region Alerts
        [PluginReference] private readonly Plugin GUIAnnouncements, DiscordMessages;

        private string ClearColorAndSize(string message)
        {
            message = message.Replace("</color>", string.Empty);
            message = message.Replace("</size>", string.Empty);
            while (message.Contains("<color="))
            {
                int index = message.IndexOf("<color=", StringComparison.Ordinal);
                message = message.Remove(index, message.IndexOf(">", index, StringComparison.Ordinal) - index + 1);
            }
            while (message.Contains("<size="))
            {
                int index = message.IndexOf("<size=", StringComparison.Ordinal);
                message = message.Remove(index, message.IndexOf(">", index, StringComparison.Ordinal) - index + 1);
            }
            if (!string.IsNullOrEmpty(_config.Prefix)) message = message.Replace(_config.Prefix + " ", string.Empty);
            return message;
        }

        private bool CanSendDiscordMessage() => _config.Discord.IsDiscord && !string.IsNullOrEmpty(_config.Discord.WebhookUrl) && _config.Discord.WebhookUrl != "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";

        private void AlertToAllPlayers(string langKey, params object[] args)
        {
            if (CanSendDiscordMessage() && _config.Discord.Keys.Contains(langKey))
            {
                object fields = new[] { new { name = Title, value = ClearColorAndSize(GetMessage(langKey, null, args)), inline = false } };
                DiscordMessages?.Call("API_SendFancyMessage", _config.Discord.WebhookUrl, "", _config.Discord.EmbedColor, JsonConvert.SerializeObject(fields), null, this);
            }
            foreach (BasePlayer player in BasePlayer.activePlayerList) AlertToPlayer(player, GetMessage(langKey, player.UserIDString, args));
        }

        private void AlertToPlayer(BasePlayer player, string message)
        {
            if (_config.IsChat) PrintToChat(player, message);
            if (_config.GuiAnnouncements.IsGuiAnnouncements) GUIAnnouncements?.Call("CreateAnnouncement", ClearColorAndSize(message), _config.GuiAnnouncements.BannerColor, _config.GuiAnnouncements.TextColor, player, _config.GuiAnnouncements.ApiAdjustVPosition);
            if (_config.Notify.IsNotify) player.SendConsoleCommand($"notify.show {_config.Notify.Type} {ClearColorAndSize(message)}");
        }
        #endregion Alerts

        #region GUI
        public class ImageURL { public string Name; public string Url; }

        private readonly HashSet<ImageURL> _urls = new HashSet<ImageURL>
        {
            new ImageURL { Name = "Tab_KpucTaJl", Url = "Images/Tab_KpucTaJl.png" },
            new ImageURL { Name = "Clock_KpucTaJl", Url = "Images/Clock_KpucTaJl.png" },
            new ImageURL { Name = "Crate_KpucTaJl", Url = "Images/Crate_KpucTaJl.png" }
        };

        private readonly HashSet<string> _failedImages = new HashSet<string>();

        private readonly Dictionary<string, string> _images = new Dictionary<string, string>();

        private void DownloadImage()
        {
            ImageURL image = _urls.FirstOrDefault(x => !_images.ContainsKey(x.Name) && !_failedImages.Contains(x.Name));
            if (image != null)
            {
                Puts($"Downloading image {image.Name}...");
                ServerMgr.Instance.StartCoroutine(ProcessDownloadImage(image));
            }
            else if (_failedImages.Count > 0) Interface.Oxide.UnloadPlugin(Name);
        }

        IEnumerator ProcessDownloadImage(ImageURL image)
        {
            string url = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + image.Url;
            using (WWW www = new WWW(url))
            {
                yield return www;
                if (www.error != null)
                {
                    _failedImages.Add(image.Name);
                    PrintError($"Image {image.Name} was not found. Maybe you didn't upload it to the .../oxide/data/Images/ folder");
                }
                else
                {
                    Texture2D tex = www.texture;
                    _images.Add(image.Name, FileStorage.server.Store(tex.EncodeToPNG(), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString());
                    Puts($"Image {image.Name} download is complete");
                    UnityEngine.Object.DestroyImmediate(tex);
                }
                DownloadImage();
            }
        }

        private void CreateTabs(BasePlayer player, Dictionary<string, string> tabs)
        {
            CuiHelper.DestroyUi(player, "Tabs_KpucTaJl");

            CuiElementContainer container = new CuiElementContainer();

            float border = 52.5f + 54.5f * (tabs.Count - 1);
            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = $"{-border} {_config.Gui.OffsetMinY}", OffsetMax = $"{border} {_config.Gui.OffsetMinY + 20}" },
                CursorEnabled = false,
            }, "Under", "Tabs_KpucTaJl");

            int i = 0;

            foreach (var dic in tabs)
            {
                i++;
                float xmin = 109f * (i - 1);
                container.Add(new CuiElement
                {
                    Name = $"Tab_{i}_KpucTaJl",
                    Parent = "Tabs_KpucTaJl",
                    Components =
                    {
                        new CuiRawImageComponent { Png = _images["Tab_KpucTaJl"] },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = $"{xmin} 0", OffsetMax = $"{xmin + 105f} 20" }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = $"Tab_{i}_KpucTaJl",
                    Components =
                    {
                        new CuiRawImageComponent { Png = _images[dic.Key] },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "9 3", OffsetMax = "23 17" }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = $"Tab_{i}_KpucTaJl",
                    Components =
                    {
                        new CuiTextComponent() { Color = "1 1 1 1", Text = dic.Value, Align = TextAnchor.MiddleCenter, FontSize = 10, Font = "robotocondensed-bold.ttf" },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "28 0", OffsetMax = "100 20" }
                    }
                });
            }

            CuiHelper.AddUi(player, container);
        }
        #endregion GUI

        #region Helpers
        [PluginReference] private readonly Plugin NpcSpawn, PveMode;

        private HashSet<BaseEntity> IsWaterEventInProgress() => _active ? _controller.Entities : null;

        private readonly HashSet<string> _hooks = new HashSet<string>
        {
            "OnEntityTakeDamage",
            "OnEntityDeath",
            "OnOvenToggle",
            "CanUseWires",
            "CanBuild",
            "CanAffordUpgrade",
            "OnStructureRotate",
            "OnCardSwipe",
            "OnButtonPress",
            "OnEntityKill",
            "CanHackCrate",
            "OnCrateHack",
            "CanHideStash",
            "OnTurretTarget",
            "OnPlayerCommand",
            "OnServerCommand",
            "OnCorpsePopulate",
            "CanPopulateLoot",
            "OnCustomLootNPC",
            "OnLootEntity",
            "OnCustomLootContainer",
            "OnContainerPopulate",
            "CanEntityTakeDamage",
            "CanEntityBeTargeted",
            "CanTeleport"
        };

        private void Unsubscribes() { foreach (string hook in _hooks) Unsubscribe(hook); }

        private void Subscribes()
        {
            foreach (string hook in _hooks)
            {
                if (hook == "CanTeleport" && !_config.NTeleportationInterrupt) continue;
                if (hook == "OnTurretTarget" && !_config.Turret.IsTurret) continue;
                if (hook == "CanEntityBeTargeted" && !_config.Turret.IsTurret) continue;
                Subscribe(hook);
            }
        }
        #endregion Helpers

        #region Commands
        [ChatCommand("waterstart")]
        private void ChatStartEvent(BasePlayer player)
        {
            if (player.IsAdmin)
            {
                if (!_active) Start();
                else PrintToChat(player, GetMessage("EventActive", player.UserIDString, _config.Prefix));
            }
        }

        [ChatCommand("waterstop")]
        private void ChatStopEvent(BasePlayer player)
        {
            if (player.IsAdmin)
            {
                if (_controller != null) Finish();
                else Interface.Oxide.ReloadPlugin(Name);
            }
        }

        [ChatCommand("waterpos")]
        private void ChatCommandPos(BasePlayer player)
        {
            if (!player.IsAdmin || _controller == null) return;
            Vector3 pos = _controller.transform.InverseTransformPoint(player.transform.position);
            Puts($"Position: {pos}");
            PrintToChat(player, $"Position: {pos}");
        }

        [ConsoleCommand("waterstart")]
        private void ConsoleStartEvent(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
            {
                if (!_active) Start();
                else Puts("This event is active now. To finish this event (waterstop), then to start the next one");
            }
        }

        [ConsoleCommand("waterstop")]
        private void ConsoleStopEvent(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
            {
                if (_controller != null) Finish();
                else Interface.Oxide.ReloadPlugin(Name);
            }
        }
        #endregion Commands
    }
}

namespace Oxide.Plugins.WaterEventExtensionMethods
{
    public static class ExtensionMethods
    {
        public static bool Any<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) return true;
            return false;
        }

        public static TSource FirstOrDefault<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) return enumerator.Current;
            return default(TSource);
        }

        public static HashSet<TResult> Select<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> predicate)
        {
            HashSet<TResult> result = new HashSet<TResult>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) result.Add(predicate(enumerator.Current));
            return result;
        }

        public static List<TResult> Select<TSource, TResult>(this IList<TSource> source, Func<TSource, TResult> predicate)
        {
            List<TResult> result = new List<TResult>();
            for (int i = 0; i < source.Count; i++)
            {
                TSource element = source[i];
                result.Add(predicate(element));
            }
            return result;
        }

        public static List<TSource> ToList<TSource>(this IEnumerable<TSource> source)
        {
            List<TSource> result = new List<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) result.Add(enumerator.Current);
            return result;
        }

        public static TSource First<TSource>(this IList<TSource> source) => source[0];

        public static TSource Max<TSource>(this IEnumerable<TSource> source, Func<TSource, double> predicate)
        {
            TSource result = source.ElementAt(0);
            double resultValue = predicate(result);
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    TSource element = enumerator.Current;
                    double elementValue = predicate(element);
                    if (elementValue > resultValue)
                    {
                        result = element;
                        resultValue = elementValue;
                    }
                }
            }
            return result;
        }

        public static TSource Max<TSource>(this IEnumerable<TSource> source, Func<TSource, float> predicate)
        {
            TSource result = source.ElementAt(0);
            float resultValue = predicate(result);
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    TSource element = enumerator.Current;
                    float elementValue = predicate(element);
                    if (elementValue > resultValue)
                    {
                        result = element;
                        resultValue = elementValue;
                    }
                }
            }
            return result;
        }

        public static TSource ElementAt<TSource>(this IEnumerable<TSource> source, int index)
        {
            int movements = 0;
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (movements == index) return enumerator.Current;
                    movements++;
                }
            }
            return default(TSource);
        }

        public static List<TSource> OrderBy<TSource>(this IEnumerable<TSource> source, Func<TSource, float> predicate)
        {
            List<TSource> result = source.ToList();
            for (int i = 0; i < result.Count; i++)
            {
                for (int j = 0; j < result.Count - 1; j++)
                {
                    if (predicate(result[j]) > predicate(result[j + 1]))
                    {
                        TSource z = result[j];
                        result[j] = result[j + 1];
                        result[j + 1] = z;
                    }
                }
            }
            return result;
        }

        public static bool IsPlayer(this BasePlayer player) => player != null && player.userID.IsSteamId();

        public static bool IsExists(this BaseNetworkable entity) => entity != null && !entity.IsDestroyed;

        public static void ClearItemsContainer(this ItemContainer container)
        {
            for (int i = container.itemList.Count - 1; i >= 0; i--)
            {
                Item item = container.itemList[i];
                item.RemoveFromContainer();
                item.Remove();
            }
        }
    }
}