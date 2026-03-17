using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using System.IO;
using Oxide.Plugins.HarborEventExtensionMethods;

namespace Oxide.Plugins
{
    [Info("HarborEvent", "KpucTaJl", "2.1.5")]
    internal class HarborEvent : RustPlugin
    {
        #region Config
        private const bool En = false;

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

        public class CrateConfig
        {
            [JsonProperty("Prefab")] public string Prefab { get; set; }
            [JsonProperty(En ? "Which loot table should the plugin use? (0 - default; 1 - own; 2 - AlphaLoot; 3 - CustomLoot; 4 - loot table of the Rust objects; 5 - combine the 1 and 4 methods)" : "Какую таблицу предметов необходимо использовать? (0 - стандартную; 1 - собственную; 2 - AlphaLoot; 3 - CustomLoot; 4 - таблица предметов объектов Rust; 5 - совместить 1 и 4 методы)")] public int TypeLootTable { get; set; }
            [JsonProperty(En ? "Loot table from prefabs (if the loot table type is 4 or 5)" : "Таблица предметов из prefab-ов (если тип таблицы предметов - 4 или 5)")] public PrefabLootTableConfig PrefabLootTable { get; set; }
            [JsonProperty(En ? "Own loot table (if the loot table type is 1 or 5)" : "Собственная таблица предметов (если тип таблицы предметов - 1 или 5)")] public LootTableConfig OwnLootTable { get; set; }
        }

        public class HackCrateConfig
        {
            [JsonProperty(En ? "Time to unlock the Crates [sec.]" : "Время разблокировки ящиков [sec.]")] public float UnlockTime { get; set; }
            [JsonProperty(En ? "Increase the event time if it's not enough to unlock the locked crate? [true/false]" : "Увеличивать время ивента, если недостаточно чтобы разблокировать заблокированный ящик? [true/false]")] public bool IncreaseEventTime { get; set; }
            [JsonProperty(En ? "Calling a patrol helicopter when the unlock begins?" : "Вызывать патрульный вертолет, когда начинается взлом? [true/false]")] public bool CallHelicopter { get; set; }
            [JsonProperty(En ? "Which loot table should the plugin use? (0 - default; 1 - own; 2 - AlphaLoot; 3 - CustomLoot; 4 - loot table of the Rust objects; 5 - combine the 1 and 4 methods)" : "Какую таблицу лута необходимо использовать? (0 - стандартную; 1 - собственную; 2 - AlphaLoot; 3 - CustomLoot; 4 - таблица предметов объектов Rust; 5 - совместить 1 и 4 методы)")] public int TypeLootTable { get; set; }
            [JsonProperty(En ? "Loot table from prefabs (if the loot table type is 4 or 5)" : "Таблица предметов из prefab-ов (если тип таблицы предметов - 4 или 5)")] public PrefabLootTableConfig PrefabLootTable { get; set; }
            [JsonProperty(En ? "Own loot table (if the loot table type is 1 or 5)" : "Собственная таблица предметов (если тип таблицы предметов - 1 или 5)")] public LootTableConfig OwnLootTable { get; set; }
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

        public class BradleyConfig
        {
            [JsonProperty(En ? "Can Bradley appear? [true/false]" : "Должен ли появляться Bradley? [true/false]")] public bool IsBradley { get; set; }
            [JsonProperty(En ? "Health" : "Кол-во ХП")] public float Hp { get; set; }
            [JsonProperty(En ? "The viewing distance" : "Дальность обзора")] public float ViewDistance { get; set; }
            [JsonProperty(En ? "Radius of search" : "Радиус поиска")] public float SearchRange { get; set; }
            [JsonProperty(En ? "Scale damage" : "Множитель урона")] public float ScaleDamage { get; set; }
            [JsonProperty(En ? "The multiplier of Machine-gun aim cone" : "Множитель разброса пулемёта")] public float CoaxAimCone { get; set; }
            [JsonProperty(En ? "The multiplier of Machine-gun fire rate" : "Множитель скорострельности пулемёта")] public float CoaxFireRate { get; set; }
            [JsonProperty(En ? "Amount of Machine-gun burst shots" : "Кол-во выстрелов очереди пулемёта")] public int CoaxBurstLength { get; set; }
            [JsonProperty(En ? "Time that Bradley holds in memory the position of its last target [sec.]" : "Время, которое Bradley помнит позицию своей последней цели [sec.]")] public float MemoryDuration { get; set; }
            [JsonProperty(En ? "The time between shots of the main gun [sec.]" : "Время между залпами основного орудия [sec.]")] public float NextFireTime { get; set; }
            [JsonProperty(En ? "The time between shots of the main gun in a fire rate [sec.]" : "Время между выстрелами основного орудия в залпе [sec.]")] public float TopTurretFireRate { get; set; }
            [JsonProperty(En ? "Numbers of Crates" : "Кол-во ящиков после уничтожения")] public int CountCrates { get; set; }
            [JsonProperty(En ? "Which loot table should the plugin use? (0 - default; 1 - own; 2 - AlphaLoot; 3 - CustomLoot; 4 - loot table of the Rust objects; 5 - combine the 1 and 4 methods)" : "Какую таблицу лута необходимо использовать? (0 - стандартную; 1 - собственную; 2 - AlphaLoot; 3 - CustomLoot; 4 - таблица предметов объектов Rust; 5 - совместить 1 и 4 методы)")] public int TypeLootTable { get; set; }
            [JsonProperty(En ? "Loot table from prefabs (if the loot table type is 4 or 5)" : "Таблица предметов из prefab-ов (если тип таблицы предметов - 4 или 5)")] public PrefabLootTableConfig PrefabLootTable { get; set; }
            [JsonProperty(En ? "Own loot table (if the loot table type is 1 or 5)" : "Собственная таблица предметов (если тип таблицы предметов - 1 или 5)")] public LootTableConfig OwnLootTable { get; set; }
        }

        public class HelicopterConfig
        {
            [JsonProperty(En ? "Health" : "Кол-во ХП")] public float Hp { get; set; }
            [JsonProperty(En ? "Health Main Rotor" : "Кол-во ХП основного винта")] public float HpMainRotor { get; set; }
            [JsonProperty(En ? "Health Tail Rotor" : "Кол-во ХП хвостового винта")] public float HpTailRotor { get; set; }
            [JsonProperty(En ? "Numbers of Crates" : "Кол-во ящиков после уничтожения")] public int CountCrates { get; set; }
            [JsonProperty(En ? "Time between firing rockets" : "Время между выстрелом ракет")] public float TimeBetweenRockets { get; set; }
            [JsonProperty(En ? "Time between turret shots" : "Время между выстрелами пулемета")] public float FireRate { get; set; }
            [JsonProperty(En ? "Time between turret bursts" : "Время между очередями пулемета")] public float TimeBetweenBursts { get; set; }
            [JsonProperty(En ? "Duration of the burst turret" : "Продолжительность очереди пулемета")] public float BurstLength { get; set; }
            [JsonProperty(En ? "Turret firing radius" : "Дистанция стрельбы пулемета")] public float MaxTargetRange { get; set; }
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
            [JsonProperty(En ? "Can the non-owner of the event do damage to Bradley? [true/false]" : "Может ли не владелец ивента наносить урон по Bradley? [true/false]")] public bool DamageTank { get; set; }
            [JsonProperty(En ? "Can the non-owner of the event do damage to Patrol Helicopter? [true/false]" : "Может ли не владелец ивента наносить урон по патрульному вертолету? [true/false]")] public bool DamageHelicopter { get; set; }
            [JsonProperty(En ? "Can an Npc attack a non-owner of the event? [true/false]" : "Может ли Npc атаковать не владельца ивента? [true/false]")] public bool TargetNpc { get; set; }
            [JsonProperty(En ? "Can Bradley attack a non-owner of the event? [true/false]" : "Может ли Bradley атаковать не владельца ивента? [true/false]")] public bool TargetTank { get; set; }
            [JsonProperty(En ? "Can Patrol Helicopter attack a non-owner of the event? [true/false]" : "Может ли патрульный вертолет атаковать не владельца ивента? [true/false]")] public bool TargetHelicopter { get; set; }
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
            [JsonProperty(En ? "Roam Range" : "Дальность патрулирования местности")] public float RoamRange { get; set; }
            [JsonProperty(En ? "Chase Range" : "Дальность погони за целью")] public float ChaseRange { get; set; }
            [JsonProperty(En ? "Attack Range Multiplier" : "Множитель радиуса атаки")] public float AttackRangeMultiplier { get; set; }
            [JsonProperty(En ? "Sense Range" : "Радиус обнаружения цели")] public float SenseRange { get; set; }
            [JsonProperty(En ? "Target Memory Duration [sec.]" : "Длительность памяти цели [sec.]")] public float MemoryDuration { get; set; }
            [JsonProperty(En ? "Scale damage" : "Множитель урона")] public float DamageScale { get; set; }
            [JsonProperty(En ? "Aim Cone Scale" : "Множитель разброса")] public float AimConeScale { get; set; }
            [JsonProperty(En ? "Detect the target only in the NPC's viewing vision cone? [true/false]" : "Обнаруживать цель только в углу обзора NPC? [true/false]")] public bool CheckVisionCone { get; set; }
            [JsonProperty(En ? "Vision Cone" : "Угол обзора")] public float VisionCone { get; set; }
            [JsonProperty(En ? "Speed" : "Скорость")] public float Speed { get; set; }
            [JsonProperty(En ? "Disable radio effects? [true/false]" : "Отключать эффекты рации? [true/false]")] public bool DisableRadio { get; set; }
            [JsonProperty(En ? "Is this a stationary NPC? [true/false]" : "Это стационарный NPC? [true/false]")] public bool Stationary { get; set; }
            [JsonProperty(En ? "Remove a corpse after death? (it is recommended to use the true value to improve performance) [true/false]" : "Удалять труп после смерти? (рекомендуется использовать значение true для повышения производительности) [true/false]")] public bool IsRemoveCorpse { get; set; }
            [JsonProperty(En ? "Wear items" : "Одежда")] public HashSet<NpcWear> WearItems { get; set; }
            [JsonProperty(En ? "Belt items" : "Быстрые слоты")] public HashSet<NpcBelt> BeltItems { get; set; }
            [JsonProperty(En ? "Kit (it is recommended to use the previous 2 settings to improve performance)" : "Kit (рекомендуется использовать предыдущие 2 пункта настройки для повышения производительности)")] public string Kit { get; set; }
        }

        public class NpcConfigCargo
        {
            [JsonProperty(En ? "Name" : "Название")] public string Name { get; set; }
            [JsonProperty(En ? "Health" : "Кол-во ХП")] public float Health { get; set; }
            [JsonProperty(En ? "Roam Range" : "Дальность патрулирования местности")] public float RoamRange { get; set; }
            [JsonProperty(En ? "Chase Range" : "Дальность погони за целью")] public float ChaseRange { get; set; }
            [JsonProperty(En ? "Attack Range Multiplier" : "Множитель радиуса атаки")] public float AttackRangeMultiplier { get; set; }
            [JsonProperty(En ? "Sense Range" : "Радиус обнаружения цели")] public float SenseRange { get; set; }
            [JsonProperty(En ? "Target Memory Duration [sec.]" : "Длительность памяти цели [sec.]")] public float MemoryDuration { get; set; }
            [JsonProperty(En ? "Scale damage" : "Множитель урона")] public float DamageScale { get; set; }
            [JsonProperty(En ? "Aim Cone Scale" : "Множитель разброса")] public float AimConeScale { get; set; }
            [JsonProperty(En ? "Detect the target only in the NPC's viewing vision cone? [true/false]" : "Обнаруживать цель только в углу обзора NPC? [true/false]")] public bool CheckVisionCone { get; set; }
            [JsonProperty(En ? "Vision Cone" : "Угол обзора")] public float VisionCone { get; set; }
            [JsonProperty(En ? "Speed" : "Скорость")] public float Speed { get; set; }
            [JsonProperty(En ? "Disable radio effects? [true/false]" : "Отключать эффекты рации? [true/false]")] public bool DisableRadio { get; set; }
            [JsonProperty(En ? "Remove a corpse after death? (it is recommended to use the true value to improve performance) [true/false]" : "Удалять труп после смерти? (рекомендуется использовать значение true для повышения производительности) [true/false]")] public bool IsRemoveCorpse { get; set; }
            [JsonProperty(En ? "Wear items" : "Одежда")] public HashSet<NpcWear> WearItems { get; set; }
            [JsonProperty(En ? "Belt items" : "Быстрые слоты")] public HashSet<NpcBelt> BeltItems { get; set; }
            [JsonProperty(En ? "Kit (it is recommended to use the previous 2 settings to improve performance)" : "Kit (рекомендуется использовать предыдущие 2 пункта настройки для повышения производительности)")] public string Kit { get; set; }
            [JsonProperty(En ? "Which loot table should the plugin use? (0 - default; 1 - own; 2 - AlphaLoot; 3 - CustomLoot; 4 - loot table of the Rust objects; 5 - combine the 1 and 4 methods)" : "Какую таблицу предметов необходимо использовать? (0 - стандартную; 1 - собственную; 2 - AlphaLoot; 3 - CustomLoot; 4 - таблица предметов объектов Rust; 5 - совместить 1 и 4 методы)")] public int TypeLootTable { get; set; }
            [JsonProperty(En ? "Loot table from prefabs (if the loot table type is 4 or 5)" : "Таблица предметов из prefab-ов (если тип таблицы предметов - 4 или 5)")] public PrefabLootTableConfig PrefabLootTable { get; set; }
            [JsonProperty(En ? "Own loot table (if the loot table type is 1 or 5)" : "Собственная таблица предметов (если тип таблицы предметов - 1 или 5)")] public LootTableConfig OwnLootTable { get; set; }
        }

        public class EconomyConfig
        {
            [JsonProperty(En ? "Which economy plugins do you want to use? (Economics, Server Rewards, IQEconomic)" : "Какие плагины экономики вы хотите использовать? (Economics, Server Rewards, IQEconomic)")] public HashSet<string> Plugins { get; set; }
            [JsonProperty(En ? "The minimum value that a player must collect to get points for the economy" : "Минимальное значение, которое игрок должен заработать, чтобы получить баллы за экономику")] public double Min { get; set; }
            [JsonProperty(En ? "Looting of crates" : "Ограбление ящиков")] public Dictionary<string, double> Crates { get; set; }
            [JsonProperty(En ? "Destruction of Bradley" : "Уничтожение Bradley")] public double Bradley { get; set; }
            [JsonProperty(En ? "Killing an NPC" : "Убийство NPC")] public double Npc { get; set; }
            [JsonProperty(En ? "Hacking a locked crate" : "Взлом заблокированного ящика")] public double LockedCrate { get; set; }
            [JsonProperty(En ? "Pressing the button" : "Нажатие кнопки")] public double Button { get; set; }
            [JsonProperty(En ? "List of commands that are executed in the console at the end of the event ({steamid} - the player who collected the highest number of points)" : "Список команд, которые выполняются в консоли по окончанию ивента ({steamid} - игрок, который набрал наибольшее кол-во баллов)")] public HashSet<string> Commands { get; set; }
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
            [JsonProperty(En ? "Can an event appear in a Small Harbor? [true/false]" : "Должен ли ивент появляться в Малом Порту? [true/false]")] public bool IsSmallHarbor { get; set; }
            [JsonProperty(En ? "Can an event appear in a Large Harbor? [true/false]" : "Должен ли ивент появляться в Большом Порту? [true/false]")] public bool IsLargeHarbor { get; set; }
            [JsonProperty(En ? "Crates settings in Cargo Container" : "Настройка ящиков в контейнере")] public HashSet<CrateConfig> ContainerCrates { get; set; }
            [JsonProperty(En ? "Crates settings on Cargo Ship" : "Настройка ящиков на корабле")] public HashSet<CrateConfig> CargoCrates { get; set; }
            [JsonProperty(En ? "Locked crates settings in Cargo Container" : "Настройка заблокированных ящиков в контейнере")] public HackCrateConfig ContainerHackCrates { get; set; }
            [JsonProperty(En ? "Locked crates settings on Cargo Ship" : "Настройка заблокированных ящиков на корабле")] public HackCrateConfig CargoHackCrates { get; set; }
            [JsonProperty(En ? "The CCTV camera" : "Название камеры")] public string Cctv { get; set; }
            [JsonProperty(En ? "Marker configuration on the map" : "Настройка маркера на карте")] public MarkerConfig Marker { get; set; }
            [JsonProperty(En ? "Prefix of chat messages" : "Префикс сообщений в чате")] public string Prefix { get; set; }
            [JsonProperty(En ? "Do you use the chat? [true/false]" : "Использовать ли чат? [true/false]")] public bool IsChat { get; set; }
            [JsonProperty(En ? "GUI setting" : "Настройки GUI")] public GuiConfig Gui { get; set; }
            [JsonProperty(En ? "GUI Announcements setting" : "Настройка GUI Announcements")] public GuiAnnouncementsConfig GuiAnnouncements { get; set; }
            [JsonProperty(En ? "Notify setting" : "Настройка Notify")] public NotifyConfig Notify { get; set; }
            [JsonProperty(En ? "Discord setting (only for users DiscordMessages plugin)" : "Настройка оповещений в Discord (только для тех, кто использует плагин DiscordMessages)")] public DiscordConfig Discord { get; set; }
            [JsonProperty(En ? "List of commands banned in the event zone" : "Список команд запрещенных в зоне ивента")] public HashSet<string> Commands { get; set; }
            [JsonProperty(En ? "Bradley setting" : "Настройка танка")] public BradleyConfig Bradley { get; set; }
            [JsonProperty(En ? "Helicopter setting" : "Настройка вертолета")] public HelicopterConfig Helicopter { get; set; }
            [JsonProperty(En ? "Radius of the event zone" : "Радиус зоны ивента")] public float Radius { get; set; }
            [JsonProperty(En ? "Do you create a PVP zone in the event area? (only for users TruePVE plugin) [true/false]" : "Создавать зону PVP в зоне проведения ивента? (только для тех, кто использует плагин TruePVE) [true/false]")] public bool IsCreateZonePvp { get; set; }
            [JsonProperty(En ? "PVE Mode Setting (only for users PveMode plugin)" : "Настройка PVE режима работы плагина (только для тех, кто использует плагин PveMode)")] public PveModeConfig PveMode { get; set; }
            [JsonProperty(En ? "Interrupt the teleport in harbor? (only for users NTeleportation plugin) [true/false]" : "Запрещать телепорт в порту? (только для тех, кто использует плагин NTeleportation) [true/false]")] public bool NTeleportationInterrupt { get; set; }
            [JsonProperty(En ? "Disable NPCs from the BetterNpc plugin on the monument while the event is on? [true/false]" : "Отключать NPC из плагина BetterNpc на монументе пока проходит ивент? [true/false]")] public bool RemoveBetterNpc { get; set; }
            [JsonProperty(En ? "NPCs settings in Small Harbor" : "Настройка NPC в Малом Порту")] public HashSet<PresetConfig> NpcSmall { get; set; }
            [JsonProperty(En ? "NPCs settings in Large Harbor" : "Настройка NPC в Большом Порту")] public HashSet<PresetConfig> NpcLarge { get; set; }
            [JsonProperty(En ? "Mobile NPCs settings on Cargo Ship" : "Настройка двигающихся NPC на корабле")] public NpcConfigCargo NpcMovingCargo { get; set; }
            [JsonProperty(En ? "Stationary NPCs settings inside Cargo Ship" : "Настройка стационарных NPC внутри корабля")] public NpcConfigCargo NpcStationaryInsideCargo { get; set; }
            [JsonProperty(En ? "Stationary NPCs settings outside Cargo Ship" : "Настройка стационарных NPC снаружи корабля")] public NpcConfigCargo NpcStationaryOutsideCargo { get; set; }
            [JsonProperty(En ? "Economy setting (total values will be added up and rewarded at the end of the event)" : "Настройка экономики (конечное значение суммируется и будет выдано игрокам по окончанию ивента)")] public EconomyConfig Economy { get; set; }
            [JsonProperty(En ? "Should Cargo Ship sail away after timer ends? (сan bug out if Harbor is covered or between Islands) [true/false]" : "Корабль уплывает с карты? (возможен баг с прохождением сквозь землю, если порт находится в заливе или между островами) [true/false]")] public bool LeavingShip { get; set; }
            [JsonProperty(En ? "Can SAM Site turrets appear? [true/false]" : "Должны ли появляться Sam Site турели? [true/false]")] public bool IsSamSites { get; set; }
            [JsonProperty(En ? "Setting AutoTurrets" : "Настройка турелей")] public TurretConfig Turret { get; set; }
            [JsonProperty(En ? "Configuration version" : "Версия конфигурации")] public VersionNumber PluginVersion { get; set; }

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    MinStartTime = 10800f,
                    MaxStartTime = 10800f,
                    EnabledTimer = true,
                    FinishTime = 3600,
                    PreStartTime = 300f,
                    PreFinishTime = 300,
                    IsSmallHarbor = true,
                    IsLargeHarbor = true,
                    ContainerCrates = new HashSet<CrateConfig>
                    {
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/crate_elite.prefab",
                            TypeLootTable = 0,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/crate_elite.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 25, MaxAmount = 25, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" } }
                            }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/crate_normal.prefab",
                            TypeLootTable = 0,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/crate_normal.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 8, MaxAmount = 8, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" } }
                            }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/dmloot/dm c4.prefab",
                            TypeLootTable = 0,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/dmloot/dm c4.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" } }
                            }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/dmloot/dm ammo.prefab",
                            TypeLootTable = 0,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/dmloot/dm ammo.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" } }
                            }
                        }
                    },
                    CargoCrates = new HashSet<CrateConfig>
                    {
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/crate_elite.prefab",
                            TypeLootTable = 0,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/crate_elite.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 25, MaxAmount = 25, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" } }
                            }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/crate_normal.prefab",
                            TypeLootTable = 0,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/crate_normal.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 8, MaxAmount = 8, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" } }
                            }
                        },
                        new CrateConfig
                        {
                            Prefab = "assets/bundled/prefabs/radtown/crate_normal_2.prefab",
                            TypeLootTable = 0,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/bundled/prefabs/radtown/crate_normal_2.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 1, Max = 1, UseCount = true,
                                Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 5, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" } }
                            }
                        }
                    },
                    ContainerHackCrates = new HackCrateConfig
                    {
                        UnlockTime = 600f,
                        IncreaseEventTime = true,
                        CallHelicopter = true,
                        TypeLootTable = 0,
                        PrefabLootTable = new PrefabLootTableConfig
                        {
                            Min = 1,
                            Max = 1,
                            UseCount = true,
                            Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab" } }
                        },
                        OwnLootTable = new LootTableConfig
                        {
                            Min = 1,
                            Max = 1,
                            UseCount = true,
                            Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" } }
                        }
                    },
                    CargoHackCrates = new HackCrateConfig
                    {
                        UnlockTime = 600f,
                        IncreaseEventTime = true,
                        TypeLootTable = 0,
                        CallHelicopter = false,
                        PrefabLootTable = new PrefabLootTableConfig
                        {
                            Min = 1,
                            Max = 1,
                            UseCount = true,
                            Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab" } }
                        },
                        OwnLootTable = new LootTableConfig
                        {
                            Min = 1,
                            Max = 1,
                            UseCount = true,
                            Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" } }
                        }
                    },
                    Cctv = "Harbor",
                    Marker = new MarkerConfig
                    {
                        Name = "HarborEvent ({time})",
                        Radius = 0.4f,
                        Alpha = 0.6f,
                        Color = new ColorConfig { R = 0.81f, G = 0.25f, B = 0.15f }
                    },
                    Prefix = "[HarborEvent]",
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
                            "KillBradley",
                            "OpenDoor"
                        }
                    },
                    Commands = new HashSet<string>
                    {
                        "/remove",
                        "remove.toggle"
                    },
                    Bradley = new BradleyConfig
                    {
                        IsBradley = true,
                        Hp = 1000f,
                        ViewDistance = 100.0f,
                        SearchRange = 100.0f,
                        ScaleDamage = 1.0f,
                        CoaxAimCone = 1.1f,
                        CoaxFireRate = 1.0f,
                        CoaxBurstLength = 10,
                        MemoryDuration = 20f,
                        NextFireTime = 10f,
                        TopTurretFireRate = 0.25f,
                        CountCrates = 3,
                        TypeLootTable = 0,
                        PrefabLootTable = new PrefabLootTableConfig
                        {
                            Min = 1,
                            Max = 1,
                            UseCount = true,
                            Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/prefabs/npc/m2bradley/bradley_crate.prefab" } }
                        },
                        OwnLootTable = new LootTableConfig
                        {
                            Min = 1,
                            Max = 1,
                            UseCount = true,
                            Items = new List<ItemConfig> { new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 100f, IsBluePrint = false, SkinID = 0, Name = "" } }
                        }
                    },
                    Helicopter = new HelicopterConfig
                    {
                        Hp = 1000f,
                        HpMainRotor = 750f,
                        HpTailRotor = 375f,
                        CountCrates = 4,
                        TimeBetweenRockets = 0.2f,
                        FireRate = 0.125f,
                        TimeBetweenBursts = 3f,
                        BurstLength = 3f,
                        MaxTargetRange = 300f
                    },
                    Radius = 200f,
                    IsCreateZonePvp = false,
                    PveMode = new PveModeConfig
                    {
                        Pve = false,
                        Damage = 500f,
                        ScaleDamage = new HashSet<ScaleDamageConfig>
                        {
                            new ScaleDamageConfig { Type = "NPC", Scale = 1f },
                            new ScaleDamageConfig { Type = "Bradley", Scale = 2f },
                            new ScaleDamageConfig { Type = "Helicopter", Scale = 2f }
                        },
                        LootCrate = false,
                        HackCrate = false,
                        LootNpc = false,
                        DamageNpc = false,
                        DamageTank = false,
                        DamageHelicopter = false,
                        TargetNpc = false,
                        TargetTank = false,
                        TargetHelicopter = false,
                        CanEnter = false,
                        CanEnterCooldownPlayer = true,
                        TimeExitOwner = 300,
                        AlertTime = 60,
                        RestoreUponDeath = true,
                        CooldownOwner = 86400,
                        Darkening = 12
                    },
                    NTeleportationInterrupt = false,
                    RemoveBetterNpc = true,
                    NpcSmall = new HashSet<PresetConfig>
                    {
                        new PresetConfig
                        {
                            Min = 2,
                            Max = 2,
                            Positions = new List<string>
                            {
                                "(35.0, 4.8, 77.5)",
                                "(31.4, 9.0, 82.7)"
                            },
                            Config = new NpcConfig
                            {
                                Name = "Soldier",
                                Health = 200f,
                                RoamRange = 0f,
                                ChaseRange = 0f,
                                AttackRangeMultiplier = 5f,
                                SenseRange = 100f,
                                MemoryDuration = 10f,
                                DamageScale = 2f,
                                AimConeScale = 0f,
                                CheckVisionCone = false,
                                VisionCone = 135f,
                                Speed = 0f,
                                DisableRadio = false,
                                Stationary = true,
                                IsRemoveCorpse = true,
                                WearItems = new HashSet<NpcWear>
                                {
                                    new NpcWear { ShortName = "hoodie", SkinID = 2563940111 },
                                    new NpcWear { ShortName = "pants", SkinID = 2563935722 },
                                    new NpcWear { ShortName = "shoes.boots", SkinID = 2575506021 },
                                    new NpcWear { ShortName = "roadsign.jacket", SkinID = 2570233552 },
                                    new NpcWear { ShortName = "burlap.gloves", SkinID = 2582714399 },
                                    new NpcWear { ShortName = "coffeecan.helmet", SkinID = 2570227850 },
                                    new NpcWear { ShortName = "roadsign.kilt", SkinID = 2570237224 }
                                },
                                BeltItems = new HashSet<NpcBelt>
                                {
                                    new NpcBelt { ShortName = "rifle.m39", Amount = 1, SkinID = 0, Mods = new HashSet<string> { "weapon.mod.flashlight", "weapon.mod.small.scope" }, Ammo = string.Empty },
                                    new NpcBelt { ShortName = "syringe.medical", Amount = 10, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty },
                                    new NpcBelt { ShortName = "grenade.f1", Amount = 10, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
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
                                "(37.1, 16.2, 83.3)"
                            },
                            Config = new NpcConfig
                            {
                                Name = "Sniper",
                                Health = 200f,
                                RoamRange = 0f,
                                ChaseRange = 0f,
                                AttackRangeMultiplier = 1f,
                                SenseRange = 150f,
                                MemoryDuration = 30f,
                                DamageScale = 0.25f,
                                AimConeScale = 1f,
                                CheckVisionCone = false,
                                VisionCone = 135f,
                                Speed = 0f,
                                DisableRadio = false,
                                Stationary = true,
                                IsRemoveCorpse = true,
                                WearItems = new HashSet<NpcWear>
                                {
                                    new NpcWear { ShortName = "hat.boonie", SkinID = 1275532550 },
                                    new NpcWear { ShortName = "mask.bandana", SkinID = 1623665052 },
                                    new NpcWear { ShortName = "burlap.gloves", SkinID = 1113475533 },
                                    new NpcWear { ShortName = "hoodie", SkinID = 1275521888 },
                                    new NpcWear { ShortName = "pants", SkinID = 1277403128 },
                                    new NpcWear { ShortName = "shoes.boots", SkinID = 0 }
                                },
                                BeltItems = new HashSet<NpcBelt>
                                {
                                    new NpcBelt { ShortName = "rifle.bolt", Amount = 1, SkinID = 897867582, Mods = new HashSet<string> { "weapon.mod.flashlight", "weapon.mod.8x.scope" }, Ammo = string.Empty },
                                    new NpcBelt { ShortName = "syringe.medical", Amount = 5, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
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
                            Min = 4,
                            Max = 4,
                            Positions = new List<string>
                            {
                                "(40.2, 2.3, 69.1)",
                                "(30.0, 2.3, 69.1)",
                                "(26.8, 2.3, 90.9)",
                                "(43.7, 2.3, 90.4)"
                            },
                            Config = new NpcConfig
                            {
                                Name = "Porter",
                                Health = 175f,
                                RoamRange = 10f,
                                ChaseRange = 30f,
                                AttackRangeMultiplier = 2.5f,
                                SenseRange = 70f,
                                MemoryDuration = 120f,
                                DamageScale = 1.7f,
                                AimConeScale = 0.6f,
                                CheckVisionCone = false,
                                VisionCone = 135f,
                                Speed = 7.5f,
                                DisableRadio = true,
                                Stationary = false,
                                IsRemoveCorpse = true,
                                WearItems = new HashSet<NpcWear>
                                {
                                    new NpcWear { ShortName = "hoodie", SkinID = 1819497052 },
                                    new NpcWear { ShortName = "shoes.boots", SkinID = 0 },
                                    new NpcWear { ShortName = "hat.beenie", SkinID = 0 },
                                    new NpcWear { ShortName = "movembermoustache", SkinID = 0 },
                                    new NpcWear { ShortName = "burlap.gloves", SkinID = 0 },
                                    new NpcWear { ShortName = "pants", SkinID = 1819498178 }
                                },
                                BeltItems = new HashSet<NpcBelt>
                                {
                                    new NpcBelt { ShortName = "smg.mp5", Amount = 1, SkinID = 0, Mods = new HashSet<string> { "weapon.mod.flashlight", "weapon.mod.holosight" }, Ammo = string.Empty },
                                    new NpcBelt { ShortName = "syringe.medical", Amount = 5, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty },
                                    new NpcBelt { ShortName = "grenade.f1", Amount = 2, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
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
                    NpcLarge = new HashSet<PresetConfig>
                    {
                        new PresetConfig
                        {
                            Min = 2,
                            Max = 2,
                            Positions = new List<string>
                            {
                                "(91.0, 11.7, -23.6)",
                                "(85.7, 7.6, -26.7)"
                            },
                            Config = new NpcConfig
                            {
                                Name = "Soldier",
                                Health = 200f,
                                RoamRange = 0f,
                                ChaseRange = 0f,
                                AttackRangeMultiplier = 5f,
                                SenseRange = 100f,
                                MemoryDuration = 10f,
                                DamageScale = 2f,
                                AimConeScale = 0f,
                                CheckVisionCone = false,
                                VisionCone = 135f,
                                Speed = 0f,
                                DisableRadio = false,
                                Stationary = true,
                                IsRemoveCorpse = true,
                                WearItems = new HashSet<NpcWear>
                                {
                                    new NpcWear { ShortName = "hoodie", SkinID = 2563940111 },
                                    new NpcWear { ShortName = "pants", SkinID = 2563935722 },
                                    new NpcWear { ShortName = "shoes.boots", SkinID = 2575506021 },
                                    new NpcWear { ShortName = "roadsign.jacket", SkinID = 2570233552 },
                                    new NpcWear { ShortName = "burlap.gloves", SkinID = 2582714399 },
                                    new NpcWear { ShortName = "coffeecan.helmet", SkinID = 2570227850 },
                                    new NpcWear { ShortName = "roadsign.kilt", SkinID = 2570237224 }
                                },
                                BeltItems = new HashSet<NpcBelt>
                                {
                                    new NpcBelt { ShortName = "rifle.m39", Amount = 1, SkinID = 0, Mods = new HashSet<string> { "weapon.mod.flashlight", "weapon.mod.small.scope" }, Ammo = string.Empty },
                                    new NpcBelt { ShortName = "syringe.medical", Amount = 10, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty },
                                    new NpcBelt { ShortName = "grenade.f1", Amount = 10, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
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
                                "(91.4, 19.0, -29.2)"
                            },
                            Config = new NpcConfig
                            {
                                Name = "Sniper",
                                Health = 200f,
                                RoamRange = 0f,
                                ChaseRange = 0f,
                                AttackRangeMultiplier = 1f,
                                SenseRange = 150f,
                                MemoryDuration = 30f,
                                DamageScale = 0.25f,
                                AimConeScale = 1f,
                                CheckVisionCone = false,
                                VisionCone = 135f,
                                Speed = 0f,
                                DisableRadio = false,
                                Stationary = true,
                                IsRemoveCorpse = true,
                                WearItems = new HashSet<NpcWear>
                                {
                                    new NpcWear { ShortName = "hat.boonie", SkinID = 1275532550 },
                                    new NpcWear { ShortName = "mask.bandana", SkinID = 1623665052 },
                                    new NpcWear { ShortName = "burlap.gloves", SkinID = 1113475533 },
                                    new NpcWear { ShortName = "hoodie", SkinID = 1275521888 },
                                    new NpcWear { ShortName = "pants", SkinID = 1277403128 },
                                    new NpcWear { ShortName = "shoes.boots", SkinID = 0 }
                                },
                                BeltItems = new HashSet<NpcBelt>
                                {
                                    new NpcBelt { ShortName = "rifle.bolt", Amount = 1, SkinID = 897867582, Mods = new HashSet<string> { "weapon.mod.flashlight", "weapon.mod.8x.scope" }, Ammo = string.Empty },
                                    new NpcBelt { ShortName = "syringe.medical", Amount = 5, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
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
                            Min = 3,
                            Max = 3,
                            Positions = new List<string>
                            {
                                "(77.1, 5.0, -32.7)",
                                "(77.1, 5.0, -23.8)",
                                "(74.1, 5.0, -23.8)"
                            },
                            Config = new NpcConfig
                            {
                                Name = "Porter",
                                Health = 175f,
                                RoamRange = 10f,
                                ChaseRange = 30f,
                                AttackRangeMultiplier = 2.5f,
                                SenseRange = 70f,
                                MemoryDuration = 120f,
                                DamageScale = 1.7f,
                                AimConeScale = 0.6f,
                                CheckVisionCone = false,
                                VisionCone = 135f,
                                Speed = 7.5f,
                                DisableRadio = true,
                                Stationary = false,
                                IsRemoveCorpse = true,
                                WearItems = new HashSet<NpcWear>
                                {
                                    new NpcWear { ShortName = "hoodie", SkinID = 1819497052 },
                                    new NpcWear { ShortName = "shoes.boots", SkinID = 0 },
                                    new NpcWear { ShortName = "hat.beenie", SkinID = 0 },
                                    new NpcWear { ShortName = "movembermoustache", SkinID = 0 },
                                    new NpcWear { ShortName = "burlap.gloves", SkinID = 0 },
                                    new NpcWear { ShortName = "pants", SkinID = 1819498178 }
                                },
                                BeltItems = new HashSet<NpcBelt>
                                {
                                    new NpcBelt { ShortName = "smg.mp5", Amount = 1, SkinID = 0, Mods = new HashSet<string> { "weapon.mod.flashlight", "weapon.mod.holosight" }, Ammo = string.Empty },
                                    new NpcBelt { ShortName = "syringe.medical", Amount = 5, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty },
                                    new NpcBelt { ShortName = "grenade.f1", Amount = 2, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
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
                    NpcMovingCargo = new NpcConfigCargo
                    {
                        Name = "Scientist",
                        Health = 250f,
                        RoamRange = 30f,
                        ChaseRange = 60f,
                        AttackRangeMultiplier = 1f,
                        SenseRange = 50f,
                        MemoryDuration = 30f,
                        DamageScale = 1.15f,
                        AimConeScale = 1.3f,
                        CheckVisionCone = false,
                        VisionCone = 135f,
                        Speed = 7.5f,
                        DisableRadio = false,
                        IsRemoveCorpse = true,
                        WearItems = new HashSet<NpcWear>
                        {
                            new NpcWear { ShortName = "hat.cap", SkinID = 2891590451 },
                            new NpcWear { ShortName = "hoodie", SkinID = 2882740093 },
                            new NpcWear { ShortName = "pants", SkinID = 2882737241 },
                            new NpcWear { ShortName = "shoes.boots", SkinID = 826587881 },
                            new NpcWear { ShortName = "sunglasses", SkinID = 0 }
                        },
                        BeltItems = new HashSet<NpcBelt>
                        {
                            new NpcBelt { ShortName = "rifle.lr300", Amount = 1, SkinID = 0, Mods = new HashSet<string> { "weapon.mod.flashlight", "weapon.mod.holosight" }, Ammo = string.Empty },
                            new NpcBelt { ShortName = "syringe.medical", Amount = 10, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty },
                            new NpcBelt { ShortName = "grenade.f1", Amount = 10, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
                        },
                        Kit = "",
                        TypeLootTable = 5,
                        PrefabLootTable = new PrefabLootTableConfig
                        {
                            Min = 1,
                            Max = 1,
                            UseCount = true,
                            Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                        },
                        OwnLootTable = new LootTableConfig
                        {
                            Min = 1,
                            Max = 1,
                            UseCount = true,
                            Items = new List<ItemConfig>
                            {
                                new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 50f, IsBluePrint = false, SkinID = 0, Name = "" },
                                new ItemConfig { ShortName = "syringe.medical", MinAmount = 1, MaxAmount = 2, Chance = 70.0f, IsBluePrint = false, SkinID = 0, Name = "" }
                            }
                        }
                    },
                    NpcStationaryInsideCargo = new NpcConfigCargo
                    {
                        Name = "Scientist",
                        Health = 250f,
                        RoamRange = 10f,
                        ChaseRange = 100f,
                        AttackRangeMultiplier = 1f,
                        SenseRange = 50f,
                        MemoryDuration = 30f,
                        DamageScale = 1.15f,
                        AimConeScale = 1.3f,
                        CheckVisionCone = false,
                        VisionCone = 135f,
                        Speed = 7.5f,
                        DisableRadio = false,
                        IsRemoveCorpse = true,
                        WearItems = new HashSet<NpcWear>
                        {
                            new NpcWear { ShortName = "hat.cap", SkinID = 0 },
                            new NpcWear { ShortName = "hoodie", SkinID = 2408787588 },
                            new NpcWear { ShortName = "pants", SkinID = 2408786118 },
                            new NpcWear { ShortName = "shoes.boots", SkinID = 826587881 },
                            new NpcWear { ShortName = "sunglasses", SkinID = 0 }
                        },
                        BeltItems = new HashSet<NpcBelt>
                        {
                            new NpcBelt { ShortName = "rifle.lr300", Amount = 1, SkinID = 0, Mods = new HashSet<string> { "weapon.mod.flashlight", "weapon.mod.holosight" }, Ammo = string.Empty },
                            new NpcBelt { ShortName = "syringe.medical", Amount = 10, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty },
                            new NpcBelt { ShortName = "grenade.f1", Amount = 10, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
                        },
                        Kit = "",
                        TypeLootTable = 5,
                        PrefabLootTable = new PrefabLootTableConfig
                        {
                            Min = 1,
                            Max = 1,
                            UseCount = true,
                            Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                        },
                        OwnLootTable = new LootTableConfig
                        {
                            Min = 1,
                            Max = 1,
                            UseCount = true,
                            Items = new List<ItemConfig>
                            {
                                new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 50f, IsBluePrint = false, SkinID = 0, Name = "" },
                                new ItemConfig { ShortName = "syringe.medical", MinAmount = 1, MaxAmount = 2, Chance = 70.0f, IsBluePrint = false, SkinID = 0, Name = "" }
                            }
                        }
                    },
                    NpcStationaryOutsideCargo = new NpcConfigCargo
                    {
                        Name = "Scientist",
                        Health = 175f,
                        RoamRange = 10f,
                        ChaseRange = 100f,
                        AttackRangeMultiplier = 1f,
                        SenseRange = 50f,
                        MemoryDuration = 30f,
                        DamageScale = 1.15f,
                        AimConeScale = 1.3f,
                        CheckVisionCone = false,
                        VisionCone = 135f,
                        Speed = 7.5f,
                        DisableRadio = false,
                        IsRemoveCorpse = true,
                        WearItems = new HashSet<NpcWear>
                        {
                            new NpcWear { ShortName = "hazmatsuit_scientist", SkinID = 0 }
                        },
                        BeltItems = new HashSet<NpcBelt>
                        {
                            new NpcBelt { ShortName = "rifle.lr300", Amount = 1, SkinID = 0, Mods = new HashSet<string> { "weapon.mod.flashlight", "weapon.mod.holosight" }, Ammo = string.Empty },
                            new NpcBelt { ShortName = "syringe.medical", Amount = 10, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty },
                            new NpcBelt { ShortName = "grenade.f1", Amount = 10, SkinID = 0, Mods = new HashSet<string>(), Ammo = string.Empty }
                        },
                        Kit = "",
                        TypeLootTable = 5,
                        PrefabLootTable = new PrefabLootTableConfig
                        {
                            Min = 1,
                            Max = 1,
                            UseCount = true,
                            Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                        },
                        OwnLootTable = new LootTableConfig
                        {
                            Min = 1,
                            Max = 1,
                            UseCount = true,
                            Items = new List<ItemConfig>
                            {
                                new ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 50f, IsBluePrint = false, SkinID = 0, Name = "" },
                                new ItemConfig { ShortName = "syringe.medical", MinAmount = 1, MaxAmount = 2, Chance = 70.0f, IsBluePrint = false, SkinID = 0, Name = "" }
                            }
                        }
                    },
                    Economy = new EconomyConfig
                    {
                        Plugins = new HashSet<string> { "Economics", "Server Rewards", "IQEconomic" },
                        Min = 0,
                        Crates = new Dictionary<string, double>
                        {
                            ["crate_elite"] = 0.4,
                            ["crate_normal"] = 0.2,
                            ["crate_normal_2"] = 0.1,
                            ["dm c4"] = 0.4,
                            ["dm ammo"] = 0.3
                        },
                        Bradley = 0.8,
                        Npc = 0.3,
                        LockedCrate = 0.5,
                        Button = 0.4,
                        Commands = new HashSet<string>()
                    },
                    LeavingShip = false,
                    IsSamSites = true,
                    Turret = new TurretConfig
                    {
                        IsTurret = true,
                        Hp = 1000f,
                        ShortNameWeapon = "rifle.ak",
                        ShortNameAmmo = "ammo.rifle",
                        CountAmmo = 1000
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
                ["PreStart"] = "{0} Loading of the Valuable Cargo on the Cargo Ship will begin in the Harbor in <color=#55aaff>{1} sec.</color>!",
                ["Start"] = "{0} The Cargo Ship <color=#738d43>has arrived</color> at the Harbor in grid <color=#55aaff>{1}</color>\nThe loading of the cargo <color=#738d43>has begun</color>!\nCCTV: <color=#55aaff>{2}</color>",
                ["PreFinish"] = "{0} Loading of the Valuable Cargo <color=#ce3f27>will end</color> in <color=#55aaff>{1} sec.</color>!",
                ["Finish"] = "{0} Loading of the Valuable Cargo <color=#ce3f27>has concluded</color>! The ship is leaving the map!",
                ["EventActive"] = "{0} This event is active now. To finish this event (<color=#55aaff>/harborstop</color>), then (<color=#55aaff>/harborstart</color>) to start the next one!",
                ["EnterPVP"] = "{0} You <color=#ce3f27>have entered</color> the PVP zone, now other players <color=#ce3f27>can damage</color> you!",
                ["ExitPVP"] = "{0} You <color=#738d43>have left</color> the PVP zone, now other players <color=#738d43>cannot damage</color> you!",
                ["HeliArrive"] = "{0} A Patrol Helicopter <color=#ce3f27>was called</color> to defend the Loading of the Valuable Cargo!",
                ["KillBradley"] = "{0} <color=#55aaff>{1}</color> <color=#738d43>destroyed</color> the tank!",
                ["OpenDoor"] = "{0} <color=#55aaff>{1}</color> <color=#738d43>opened</color> the loot container door!",
                ["NTeleportation"] = "{0} You <color=#ce3f27>cannot</color> teleport into the event zone!",
                ["SendEconomy"] = "{0} You <color=#738d43>have earned</color> <color=#55aaff>{1}</color> points in economics for participating in the event",
                ["NoCommand"] = "{0} You <color=#ce3f27>cannot</color> use this command in the event zone!"
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PreStart"] = "{0} Через <color=#55aaff>{1} сек.</color> начнется погрузка ценного груза на корабль в порту!",
                ["Start"] = "{0} Корабль <color=#738d43>прибыл</color> в порт на квадрате <color=#55aaff>{1}</color>\nПогрузка груза <color=#738d43>началась</color>!\nКамера: <color=#55aaff>{2}</color>",
                ["PreFinish"] = "{0} Погрузка груза <color=#ce3f27>закончится</color> через <color=#55aaff>{1} сек.</color>!",
                ["Finish"] = "{0} Погрузка груза <color=#ce3f27>закончена</color>! Корабль уплывает с карты",
                ["EventActive"] = "{0} Ивент в данный момент активен, сначала завершите текущий ивент (<color=#55aaff>/harborstop</color>), чтобы начать следующий!",
                ["EnterPVP"] = "{0} Вы <color=#ce3f27>вошли</color> в PVP зону, теперь другие игроки <color=#ce3f27>могут</color> наносить вам урон!",
                ["ExitPVP"] = "{0} Вы <color=#738d43>вышли</color> из PVP зоны, теперь другие игроки <color=#738d43>не могут</color> наносить вам урон!",
                ["HeliArrive"] = "{0} К месту погрузки ценного груза в порту <color=#ce3f27>вылетел</color> патрульный вертолет!",
                ["KillBradley"] = "{0} <color=#55aaff>{1}</color> <color=#738d43>уничтожил</color> танк!",
                ["OpenDoor"] = "{0} <color=#55aaff>{1}</color> <color=#738d43>открыл</color> дверь в контейнер!",
                ["NTeleportation"] = "{0} Вы <color=#ce3f27>не можете</color> телепортироваться в зоне ивента!",
                ["SendEconomy"] = "{0} Вы <color=#738d43>получили</color> <color=#55aaff>{1}</color> баллов в экономику за прохождение ивента",
                ["NoCommand"] = "{0} Вы <color=#ce3f27>не можете</color> использовать данную команду в зоне ивента!"
            }, this, "ru");
        }

        private string GetMessage(string langKey, string userID) => lang.GetMessage(langKey, _ins, userID);

        private string GetMessage(string langKey, string userID, params object[] args) => (args.Length == 0) ? GetMessage(langKey, userID) : string.Format(GetMessage(langKey, userID), args);
        #endregion Lang

        #region Oxide Hooks
        private static HarborEvent _ins;

        private void Init()
        {
            _ins = this;
            Unsubscribes();
        }

        private void OnServerInitialized()
        {
            LoadDefaultMessages();
            CheckAllLootTables();
            DownloadImage();
            foreach (MonumentInfo monument in TerrainMeta.Path.Monuments)
            {
                if (monument.name.Contains("harbor_1") && _config.IsSmallHarbor) StartLocations.Add(new Location { type = 1, pos = monument.transform.position, rot = monument.transform.rotation.eulerAngles });
                else if (monument.name.Contains("harbor_2") && _config.IsLargeHarbor) StartLocations.Add(new Location { type = 2, pos = monument.transform.position, rot = monument.transform.rotation.eulerAngles });
            }
            if (StartLocations.Count == 0)
            {
                PrintError("The harbor location is missing on the map. The plugin cannot be loaded!");
                NextTick(() => Interface.Oxide.UnloadPlugin(Name));
                return;
            }
            if (_config.EnabledTimer)
            {
                timer.In(UnityEngine.Random.Range(_config.MinStartTime, _config.MaxStartTime), () =>
                {
                    if (!_active) Start();
                    else Puts("This event is active now. To finish this event (harborstop), then to start the next one");
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
                if (entity is SamSite) return null;
                if (entity is AutoTurret)
                {
                    if (entity.health - info.damageTypes.Total() <= 0f) (entity as AutoTurret).inventory.ClearItemsContainer();
                    return null;
                }
                return true;
            }
            if (info.Initiator == _controller.Bradley) info.damageTypes.ScaleAll(_config.Bradley.ScaleDamage);
            return null;
        }

        private object CanAffordUpgrade(BasePlayer player, BuildingBlock block, BuildingGrade.Enum grade)
        {
            if (block != null && _controller.Entities.Contains(block)) return false;
            else return null;
        }

        private object OnStructureRotate(BaseCombatEntity entity, BasePlayer player)
        {
            if (entity != null && _controller.Entities.Contains(entity)) return true;
            else return null;
        }

        private object OnButtonPress(PressButton button, BasePlayer player)
        {
            if (button == null || player == null) return null;
            if (button == _controller.Button && _controller.Door.IsExists())
            {
                if (_config.PveMode.Pve && plugins.Exists("PveMode") && PveMode.Call("CanActionEvent", Name, player) != null) return true;
                _controller.Door.Kill();
                ActionEconomy(player.userID, "Button");
                AlertToAllPlayers("OpenDoor", _config.Prefix, player.displayName);
            }
            return null;
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null) return;
            if (_controller.Players.Contains(player))
            {
                _controller.Players.Remove(player);
                if (_config.Gui.IsGui) CuiHelper.DestroyUi(player, "Tabs_KpucTaJl");
            }
        }

        private void OnEntityDeath(BradleyAPC bradley, HitInfo info)
        {
            if (bradley == null || info == null) return;
            if (bradley == _controller.Bradley)
            {
                BasePlayer attacker = info.InitiatorPlayer;
                if (attacker != null)
                {
                    ActionEconomy(attacker.userID, "Bradley");
                    AlertToAllPlayers("KillBradley", _config.Prefix, attacker.displayName);
                }
            }
        }

        private void OnEntityDeath(ScientistNPC npc, HitInfo info)
        {
            if (npc == null || info == null) return;
            BasePlayer attacker = info.InitiatorPlayer;
            if (_controller.Scientists.Contains(npc) && attacker.IsPlayer()) ActionEconomy(attacker.userID, "Npc");
        }

        private object CanBradleyApcTarget(BradleyAPC bradley, BaseEntity entity)
        {
            if (bradley == null || entity == null) return null;
            if (bradley == _controller.Bradley)
            {
                if ((entity as BasePlayer).IsPlayer()) return null;
                else return false;
            }
            return null;
        }

        private object OnEntityKill(BaseEntity entity)
        {
            if (entity == null || _controller == null) return null;
            if (_controller.Entities.Contains(entity))
            {
                if (entity is SamSite || entity is AutoTurret || entity is Door) return null;
                if (!_controller.KillEntities) return true;
            }
            return null;
        }

        private object OnEntityKill(CargoShip entity)
        {
            if (entity == null || _controller == null) return null;
            if (entity == _controller.Cargo) return true;
            return null;
        }

        private void OnEntityKill(LootContainer entity)
        {
            if (entity == null || _controller == null) return;
            if (_controller.Crates.ContainsKey(entity)) _controller.Crates.Remove(entity);
            else if (entity is HackableLockedCrate)
            {
                HackableLockedCrate hackcrate = entity as HackableLockedCrate;
                if (_controller.HackCrates.ContainsKey(hackcrate)) _controller.HackCrates.Remove(hackcrate);
            }
        }

        private readonly Dictionary<ulong, ulong> _startHackCrates = new Dictionary<ulong, ulong>();

        private void CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            if (player == null || crate == null) return;
            if (_controller.HackCrates.ContainsKey(crate))
            {
                if (_startHackCrates.ContainsKey(crate.net.ID.Value)) _startHackCrates[crate.net.ID.Value] = player.userID;
                else _startHackCrates.Add(crate.net.ID.Value, player.userID);
            }
        }

        private void OnCrateHack(HackableLockedCrate crate)
        {
            if (crate == null) return;
            ulong crateId = crate.net.ID.Value;
            ulong playerId;
            if (_startHackCrates.TryGetValue(crateId, out playerId))
            {
                _startHackCrates.Remove(crateId);
                ActionEconomy(playerId, "LockedCrate");
                if (_controller.IsMainHackCrate(crate))
                {
                    if (_config.ContainerHackCrates.IncreaseEventTime && _controller.TimeToFinish < (int)_config.ContainerHackCrates.UnlockTime) _controller.TimeToFinish += (int)_config.ContainerHackCrates.UnlockTime;
                    if (_config.ContainerHackCrates.CallHelicopter && !_controller.Helicopter.IsExists())
                    {
                        AlertToAllPlayers("HeliArrive", _config.Prefix);
                        _controller.SpawnHelicopter();
                    }
                }
                else
                {
                    if (_config.CargoHackCrates.IncreaseEventTime && _controller.TimeToFinish < (int)_config.CargoHackCrates.UnlockTime) _controller.TimeToFinish += (int)_config.CargoHackCrates.UnlockTime;
                    if (_config.CargoHackCrates.CallHelicopter && !_controller.Helicopter.IsExists())
                    {
                        AlertToAllPlayers("HeliArrive", _config.Prefix);
                        _controller.SpawnHelicopter();
                    }
                }
            }
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

        private readonly HashSet<ulong> _lootableCrates = new HashSet<ulong>();

        private void OnLootEntity(BasePlayer player, LootContainer container)
        {
            if (!player.IsPlayer() || !container.IsExists() || _lootableCrates.Contains(container.net.ID.Value)) return;
            if (_controller.Crates.ContainsKey(container))
            {
                _lootableCrates.Add(container.net.ID.Value);
                ActionEconomy(player.userID, "Crates", container.ShortPrefabName);
            }
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

        private void OnEntitySpawned(ScientistNPC entity)
        {
            if (entity != null && (entity.ShortPrefabName == "scientistnpc_cargo_turret_lr300" || entity.ShortPrefabName == "scientistnpc_cargo_turret_any" || entity.ShortPrefabName == "scientistnpc_cargo") && Vector3.Distance(entity.transform.position, _controller.transform.position) < _config.Radius)
                timer.In(1f, () => _controller.SpawnCargoScientist(entity));
        }

        private void OnEntitySpawned(LootContainer entity)
        {
            if (entity == null) return;
            if (entity.ShortPrefabName != "codelockedhackablecrate" && entity.ShortPrefabName != "crate_elite" && entity.ShortPrefabName != "crate_normal" && entity.ShortPrefabName != "crate_normal_2") return;
            if (Vector3.Distance(entity.transform.position, _controller.transform.position) > _config.Radius) return;
            timer.In(1f, () => _controller.SpawnCargoCrate(entity));
        }
        #endregion Oxide Hooks

        #region Controller
        private ControllerHarborEvent _controller;
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
                Puts("HarborEvent has begun");
                Subscribes();
                _controller = new GameObject().AddComponent<ControllerHarborEvent>();
                if (_config.RemoveBetterNpc && plugins.Exists("BetterNpc")) BetterNpc.Call("DestroyController", _controller.Type == 1 ? "Small Harbor" : "Large Harbor");
                AlertToAllPlayers("Start", _config.Prefix, PhoneController.PositionToGridCoord(_controller.transform.position), _config.Cctv);
            });
        }

        private void Finish()
        {
            Unsubscribes();
            if (_config.RemoveBetterNpc && plugins.Exists("BetterNpc") && _controller != null) BetterNpc.Call("CreateController", _controller.Type == 1 ? "Small Harbor" : "Large Harbor");
            if (_config.PveMode.Pve && plugins.Exists("PveMode")) PveMode.Call("EventRemovePveMode", Name, true);
            if (_controller != null) UnityEngine.Object.Destroy(_controller.gameObject);
            _active = false;
            SendBalance();
            AlertToAllPlayers("Finish", _config.Prefix);
            Interface.Oxide.CallHook("OnHarborEventEnd");
            Puts("HarborEvent has ended");
            if (_config.EnabledTimer)
            {
                timer.In(UnityEngine.Random.Range(_config.MinStartTime, _config.MaxStartTime), () =>
                {
                    if (!_active) Start();
                    else Puts("This event is active now. To finish this event (harborstop), then to start the next one");
                });
            }
        }

        internal class Prefab { public string prefab; public Vector3 pos; public Vector3 rot; }

        internal HashSet<Prefab> PrefabsHarborSmall = new HashSet<Prefab>
        {
            //wall
            new Prefab { prefab = "assets/prefabs/building core/wall/wall.prefab", pos = new Vector3(33.460f, 17.358f, 102.607f), rot = new Vector3(0f, 180f, 90f) },
            new Prefab { prefab = "assets/prefabs/building core/wall/wall.prefab", pos = new Vector3(33.460f, 17.358f, 105.607f), rot = new Vector3(0f, 180f, 90f) },
            new Prefab { prefab = "assets/prefabs/building core/wall/wall.prefab", pos = new Vector3(33.460f, 17.358f, 108.607f), rot = new Vector3(0f, 180f, 90f) },
            new Prefab { prefab = "assets/prefabs/building core/wall/wall.prefab", pos = new Vector3(36.460f, 14.358f, 102.607f), rot = new Vector3(0f, 0f, 0f) },
            new Prefab { prefab = "assets/prefabs/building core/wall/wall.prefab", pos = new Vector3(36.460f, 14.358f, 105.607f), rot = new Vector3(0f, 0f, 0f) },
            new Prefab { prefab = "assets/prefabs/building core/wall/wall.prefab", pos = new Vector3(36.460f, 14.358f, 108.607f), rot = new Vector3(0f, 0f, 0f) },
            new Prefab { prefab = "assets/prefabs/building core/wall/wall.prefab", pos = new Vector3(33.460f, 14.358f, 102.607f), rot = new Vector3(0f, 180f, 0f) },
            new Prefab { prefab = "assets/prefabs/building core/wall/wall.prefab", pos = new Vector3(33.460f, 14.358f, 105.607f), rot = new Vector3(0f, 180f, 0f) },
            new Prefab { prefab = "assets/prefabs/building core/wall/wall.prefab", pos = new Vector3(33.460f, 14.358f, 108.607f), rot = new Vector3(0f, 180f, 0f) },
            new Prefab { prefab = "assets/prefabs/building core/wall/wall.prefab", pos = new Vector3(36.460f, 14.358f, 102.607f), rot = new Vector3(0f, 180f, 270f) },
            new Prefab { prefab = "assets/prefabs/building core/wall/wall.prefab", pos = new Vector3(36.460f, 14.358f, 105.607f), rot = new Vector3(0f, 180f, 270f) },
            new Prefab { prefab = "assets/prefabs/building core/wall/wall.prefab", pos = new Vector3(36.460f, 14.358f, 108.607f), rot = new Vector3(0f, 180f, 270f) },
            new Prefab { prefab = "assets/prefabs/building core/wall/wall.prefab", pos = new Vector3(34.960f, 14.358f, 101.107f), rot = new Vector3(0f, 90f, 0f) },
            //floor.triangle
            new Prefab { prefab = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", pos = new Vector3(46.958f, 1.999f, 132.434f), rot = new Vector3(0f, 0f, 0f) },
            new Prefab { prefab = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", pos = new Vector3(66.624f, 1.999f, 132.434f), rot = new Vector3(0f, 0f, 0f) },
            new Prefab { prefab = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", pos = new Vector3(103.751f, 1.999f, 132.434f), rot = new Vector3(0f, 0f, 0f) },
            new Prefab { prefab = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", pos = new Vector3(124.836f, 1.999f, 132.434f), rot = new Vector3(0f, 0f, 0f) },
            new Prefab { prefab = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", pos = new Vector3(144.475f, 1.999f, 132.434f), rot = new Vector3(0f, 0f, 0f) },
            new Prefab { prefab = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", pos = new Vector3(154.811f, 7.526f, 126.887f), rot = new Vector3(0f, 90f, 0f) },
            new Prefab { prefab = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", pos = new Vector3(154.811f, 7.526f, 114.205f), rot = new Vector3(0f, 90f, 0f) },
            //wall.doorway
            new Prefab { prefab = "assets/prefabs/building core/wall.doorway/wall.doorway.prefab", pos = new Vector3(34.959f, 14.358f, 110.104f), rot = new Vector3(0f, 270f, 0f) },
            //barricade.concrete
            new Prefab { prefab = "assets/prefabs/deployable/barricades/barricade.concrete.prefab", pos = new Vector3(37.582f, 2.232f, 70.936f), rot = new Vector3(0f, 270f, 0f) },
            new Prefab { prefab = "assets/prefabs/deployable/barricades/barricade.concrete.prefab", pos = new Vector3(37.582f, 2.232f, 67.406f), rot = new Vector3(0f, 270f, 0f) },
            new Prefab { prefab = "assets/prefabs/deployable/barricades/barricade.concrete.prefab", pos = new Vector3(34.994f, 2.249f, 64.757f), rot = new Vector3(0f, 0f, 0f) },
            new Prefab { prefab = "assets/prefabs/deployable/barricades/barricade.concrete.prefab", pos = new Vector3(32.359f, 2.232f, 67.406f), rot = new Vector3(0f, 270f, 0f) },
            new Prefab { prefab = "assets/prefabs/deployable/barricades/barricade.concrete.prefab", pos = new Vector3(32.359f, 2.232f, 70.936f), rot = new Vector3(0f, 270f, 0f) },
            //ladder.wooden.wall
            new Prefab { prefab = "assets/prefabs/building/ladder.wall.wood/ladder.wooden.wall.prefab", pos = new Vector3(36.455f, 14.775f, 82.395f), rot = new Vector3(0f, 0f, 0f) },
            new Prefab { prefab = "assets/prefabs/building/ladder.wall.wood/ladder.wooden.wall.prefab", pos = new Vector3(36.455f, 11.762f, 82.395f), rot = new Vector3(0f, 0f, 0f) },
            new Prefab { prefab = "assets/prefabs/building/ladder.wall.wood/ladder.wooden.wall.prefab", pos = new Vector3(36.455f, 8.757f, 82.395f), rot = new Vector3(0f, 0f, 0f) },
            //door.hinged.security.red
            new Prefab { prefab = "assets/bundled/prefabs/static/door.hinged.security.red.prefab", pos = new Vector3(34.958f, 14.355f, 110.095f), rot = new Vector3(0f, 270f, 0f) },
            //button
            new Prefab { prefab = "assets/prefabs/deployable/playerioents/button/button.prefab", pos = new Vector3(36.102f, 16.272f, 84.702f), rot = new Vector3(0f, 90f, 0f) },
            //cctv_deployed
            new Prefab { prefab = "assets/prefabs/deployable/cctvcamera/cctv_deployed.prefab", pos = new Vector3(34.981f, 16.977f, 110.049f), rot = new Vector3(17.766f, 180f, 0f) },
            //wall.frame.netting
            new Prefab { prefab = "assets/prefabs/building/wall.frame.netting/wall.frame.netting.prefab", pos = new Vector3(34.964f, 11.361f, 110.185f), rot = new Vector3(0f, 270f, 0f) },
            new Prefab { prefab = "assets/prefabs/building/wall.frame.netting/wall.frame.netting.prefab", pos = new Vector3(34.964f, 8.562f, 110.185f), rot = new Vector3(0f, 270f, 0f) },
            //sam_static
            new Prefab { prefab = "assets/prefabs/npc/sam_site_turret/sam_static.prefab", pos = new Vector3(20.664f, 8.7f, 120.541f), rot = new Vector3(0f, 270f, 0f) },
            new Prefab { prefab = "assets/prefabs/npc/sam_site_turret/sam_static.prefab", pos = new Vector3(137.663f, 26.7f, 126.55f), rot = new Vector3(0f, 0f, 0f) },
            new Prefab { prefab = "assets/prefabs/npc/sam_site_turret/sam_static.prefab", pos = new Vector3(137.648f, 26.7f, 114.545f), rot = new Vector3(0f, 180f, 0f) },
            //autoturret_deployed
            new Prefab { prefab = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab", pos = new Vector3(46.955f, 2.1f, 133.746f), rot = new Vector3(0f, 0f, 0f) },
            new Prefab { prefab = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab", pos = new Vector3(66.621f, 2.1f, 133.746f), rot = new Vector3(0f, 0f, 0f) },
            new Prefab { prefab = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab", pos = new Vector3(103.748f, 2.1f, 133.746f), rot = new Vector3(0f, 0f, 0f) },
            new Prefab { prefab = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab", pos = new Vector3(124.833f, 2.1f, 133.746f), rot = new Vector3(0f, 0f, 0f) },
            new Prefab { prefab = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab", pos = new Vector3(144.472f, 2.1f, 133.746f), rot = new Vector3(0f, 0f, 0f) },
            new Prefab { prefab = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab", pos = new Vector3(156.124f, 7.627f, 126.890f), rot = new Vector3(0f, 90f, 0f) },
            new Prefab { prefab = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab", pos = new Vector3(156.124f, 7.627f, 114.208f), rot = new Vector3(0f, 90f, 0f) }
        };

        internal HashSet<Prefab> PrefabsHarborLarge = new HashSet<Prefab>
        {
            //wall
            new Prefab { prefab = "assets/prefabs/building core/wall/wall.prefab", pos = new Vector3(115.511f, 20.266f, -31.090f), rot = new Vector3(0f, 0f, 90f) },
            new Prefab { prefab = "assets/prefabs/building core/wall/wall.prefab", pos = new Vector3(115.511f, 20.266f, -28.090f), rot = new Vector3(0f, 0f, 90f) },
            new Prefab { prefab = "assets/prefabs/building core/wall/wall.prefab", pos = new Vector3(115.511f, 20.266f, -25.090f), rot = new Vector3(0f, 0f, 90f) },
            new Prefab { prefab = "assets/prefabs/building core/wall/wall.prefab", pos = new Vector3(112.511f, 17.266f, -31.090f), rot = new Vector3(0f, 0f, 270f) },
            new Prefab { prefab = "assets/prefabs/building core/wall/wall.prefab", pos = new Vector3(112.511f, 17.266f, -28.090f), rot = new Vector3(0f, 0f, 270f) },
            new Prefab { prefab = "assets/prefabs/building core/wall/wall.prefab", pos = new Vector3(112.511f, 17.266f, -25.090f), rot = new Vector3(0f, 0f, 270f) },
            new Prefab { prefab = "assets/prefabs/building core/wall/wall.prefab", pos = new Vector3(115.511f, 17.266f, -25.090f), rot = new Vector3(0f, 0f, 0f) },
            new Prefab { prefab = "assets/prefabs/building core/wall/wall.prefab", pos = new Vector3(115.511f, 17.266f, -28.090f), rot = new Vector3(0f, 0f, 0f) },
            new Prefab { prefab = "assets/prefabs/building core/wall/wall.prefab", pos = new Vector3(115.511f, 17.266f, -31.090f), rot = new Vector3(0f, 0f, 0f) },
            new Prefab { prefab = "assets/prefabs/building core/wall/wall.prefab", pos = new Vector3(112.511f, 17.266f, -31.090f), rot = new Vector3(0f, 180f, 0f) },
            new Prefab { prefab = "assets/prefabs/building core/wall/wall.prefab", pos = new Vector3(112.511f, 17.266f, -28.090f), rot = new Vector3(0f, 180f, 0f) },
            new Prefab { prefab = "assets/prefabs/building core/wall/wall.prefab", pos = new Vector3(112.511f, 17.266f, -25.090f), rot = new Vector3(0f, 180f, 0f) },
            new Prefab { prefab = "assets/prefabs/building core/wall/wall.prefab", pos = new Vector3(114.011f, 17.266f, -23.590f), rot = new Vector3(0f, 270f, 0f) },
            //floor.triangle
            new Prefab { prefab = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", pos = new Vector3(127.462f, 1.999f, -12.477f), rot = new Vector3(0f, 90f, 0f) },
            new Prefab { prefab = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", pos = new Vector3(127.462f, 1.999f, -32.223f), rot = new Vector3(0f, 90f, 0f) },
            new Prefab { prefab = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", pos = new Vector3(127.462f, 1.999f, -69.428f), rot = new Vector3(0f, 90f, 0f) },
            new Prefab { prefab = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", pos = new Vector3(127.462f, 1.999f, -90.086f), rot = new Vector3(0f, 90f, 0f) },
            new Prefab { prefab = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", pos = new Vector3(127.462f, 1.999f, -109.74f), rot = new Vector3(0f, 90f, 0f) },
            new Prefab { prefab = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", pos = new Vector3(121.838f, 7.528f, -120.257f), rot = new Vector3(0f, 180f, 0f) },
            new Prefab { prefab = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab", pos = new Vector3(109.133f, 7.528f, -120.257f), rot = new Vector3(0f, 180f, 0f) },
            //wall.doorway
            new Prefab { prefab = "assets/prefabs/building core/wall.doorway/wall.doorway.prefab", pos = new Vector3(114.023f, 17.240f, -32.587f), rot = new Vector3(0f, 90f, 0f) },
            //barricade.concrete
            new Prefab { prefab = "assets/prefabs/deployable/barricades/barricade.concrete.prefab", pos = new Vector3(78.970f, 4.983f, -31.049f), rot = new Vector3(0f, 0f, 0f) },
            new Prefab { prefab = "assets/prefabs/deployable/barricades/barricade.concrete.prefab", pos = new Vector3(75.440f, 4.983f, -31.049f), rot = new Vector3(0f, 0f, 0f) },
            new Prefab { prefab = "assets/prefabs/deployable/barricades/barricade.concrete.prefab", pos = new Vector3(72.791f, 5f, -28.461f), rot = new Vector3(0f, 90f, 0f) },
            new Prefab { prefab = "assets/prefabs/deployable/barricades/barricade.concrete.prefab", pos = new Vector3(75.440f, 4.983f, -25.826f), rot = new Vector3(0f, 0f, 0f) },
            new Prefab { prefab = "assets/prefabs/deployable/barricades/barricade.concrete.prefab", pos = new Vector3(78.970f, 4.983f, -25.826f), rot = new Vector3(0f, 0f, 0f) },
            //ladder.wooden.wall
            new Prefab { prefab = "assets/prefabs/building/ladder.wall.wood/ladder.wooden.wall.prefab", pos = new Vector3(90.542f, 11.658f, -28.498f), rot = new Vector3(0f, 90f, 0f) },
            new Prefab { prefab = "assets/prefabs/building/ladder.wall.wood/ladder.wooden.wall.prefab", pos = new Vector3(90.542f, 14.602f, -28.498f), rot = new Vector3(0f, 90f, 0f) },
            new Prefab { prefab = "assets/prefabs/building/ladder.wall.wood/ladder.wooden.wall.prefab", pos = new Vector3(90.542f, 17.590f, -28.498f), rot = new Vector3(0f, 90f, 0f) },
            //door.hinged.security.red
            new Prefab { prefab = "assets/bundled/prefabs/static/door.hinged.security.red.prefab", pos = new Vector3(114.024f, 17.237f, -32.578f), rot = new Vector3(0f, 90f, 0f) },
            //button
            new Prefab { prefab = "assets/prefabs/deployable/playerioents/button/button.prefab", pos = new Vector3(92.911f, 18.997f, -28.235f), rot = new Vector3(0f, 180f, 0f) },
            //cctv_deployed
            new Prefab { prefab = "assets/prefabs/deployable/cctvcamera/cctv_deployed.prefab", pos = new Vector3(114.008f, 19.979f, -32.538f), rot = new Vector3(22.497f, 0f, 0f) },
            ////wall.frame.netting
            new Prefab { prefab = "assets/prefabs/building/wall.frame.netting/wall.frame.netting.prefab", pos = new Vector3(114.007f, 14.268f, -32.668f), rot = new Vector3(0f, 90f, 0f) },
            new Prefab { prefab = "assets/prefabs/building/wall.frame.netting/wall.frame.netting.prefab", pos = new Vector3(114.007f, 11.469f, -32.668f), rot = new Vector3(0f, 90f, 0f) },
            new Prefab { prefab = "assets/prefabs/building/wall.frame.netting/wall.frame.netting.prefab", pos = new Vector3(114.007f, 8.670f, -32.668f), rot = new Vector3(0f, 90f, 0f) },
            //sam_static
            new Prefab { prefab = "assets/prefabs/npc/sam_site_turret/sam_static.prefab", pos = new Vector3(115.486f, 8.45f, 16.882f), rot = new Vector3(0f, 0f, 0f) },
            new Prefab { prefab = "assets/prefabs/npc/sam_site_turret/sam_static.prefab", pos = new Vector3(121.478f, 26.45f, -103.129f), rot = new Vector3(0f, 90f, 0f) },
            new Prefab { prefab = "assets/prefabs/npc/sam_site_turret/sam_static.prefab", pos = new Vector3(109.478f, 26.45f, -103.112f), rot = new Vector3(0f, 270f, 0f) },
            //autoturret_deployed
            new Prefab { prefab = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab", pos = new Vector3(128.768f, 2.1f, -12.491f), rot = new Vector3(0f, 90f, 0f) },
            new Prefab { prefab = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab", pos = new Vector3(128.768f, 2.1f, -32.237f), rot = new Vector3(0f, 90f, 0f) },
            new Prefab { prefab = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab", pos = new Vector3(128.768f, 2.1f, -69.442f), rot = new Vector3(0f, 90f, 0f) },
            new Prefab { prefab = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab", pos = new Vector3(128.768f, 2.1f, -90.1f), rot = new Vector3(0f, 90f, 0f) },
            new Prefab { prefab = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab", pos = new Vector3(128.768f, 2.1f, -109.754f), rot = new Vector3(0f, 90f, 0f) },
            new Prefab { prefab = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab", pos = new Vector3(121.824f, 7.628f, -121.562f), rot = new Vector3(0f, 180f, 0f) },
            new Prefab { prefab = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab", pos = new Vector3(109.119f, 7.628f, -121.562f), rot = new Vector3(0f, 180f, 0f) }
        };

        internal HashSet<Prefab> CratesHarborSmall = new HashSet<Prefab>
        {
            new Prefab { prefab = "assets/bundled/prefabs/radtown/crate_elite.prefab", pos = new Vector3(34.027f, 14.465f, 104.138f), rot = new Vector3(0f, 270f, 0f) },
            new Prefab { prefab = "assets/bundled/prefabs/radtown/crate_elite.prefab", pos = new Vector3(33.977f, 14.465f, 107.138f), rot = new Vector3(0f, 270f, 0f) },
            new Prefab { prefab = "assets/bundled/prefabs/radtown/crate_normal.prefab", pos = new Vector3(33.989f, 15.096f, 104.130f), rot = new Vector3(0f, 270f, 0f) },
            new Prefab { prefab = "assets/bundled/prefabs/radtown/crate_normal.prefab", pos = new Vector3(33.989f, 15.096f, 107.130f), rot = new Vector3(0f, 270f, 0f) },
            new Prefab { prefab = "assets/bundled/prefabs/radtown/dmloot/dm c4.prefab", pos = new Vector3(35.884f, 14.443f, 104.104f), rot = new Vector3(0f, 180f, 0f) },
            new Prefab { prefab = "assets/bundled/prefabs/radtown/dmloot/dm ammo.prefab", pos = new Vector3(35.884f, 14.443f, 107.105f), rot = new Vector3(0f, 180f, 0f) },
            new Prefab { prefab = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab", pos = new Vector3(34.941f, 14.440f, 101.856f), rot = new Vector3(0f, 0f, 0f) }
        };

        internal HashSet<Prefab> CratesHarborLarge = new HashSet<Prefab>
        {
            new Prefab { prefab = "assets/bundled/prefabs/radtown/crate_elite.prefab", pos = new Vector3(114.944f, 17.373f, -26.621f), rot = new Vector3(0f, 90f, 0f) },
            new Prefab { prefab = "assets/bundled/prefabs/radtown/crate_elite.prefab", pos = new Vector3(114.944f, 17.373f, -29.621f), rot = new Vector3(0f, 90f, 0f) },
            new Prefab { prefab = "assets/bundled/prefabs/radtown/crate_normal.prefab", pos = new Vector3(114.982f, 18.004f, -26.613f), rot = new Vector3(0f, 90f, 0f) },
            new Prefab { prefab = "assets/bundled/prefabs/radtown/crate_normal.prefab", pos = new Vector3(114.982f, 18.004f, -29.613f), rot = new Vector3(0f, 90f, 0f) },
            new Prefab { prefab = "assets/bundled/prefabs/radtown/dmloot/dm c4.prefab", pos = new Vector3(113.087f, 17.351f, -26.587f), rot = new Vector3(0f, 0f, 0f) },
            new Prefab { prefab = "assets/bundled/prefabs/radtown/dmloot/dm ammo.prefab", pos = new Vector3(113.087f, 17.351f, -29.589f), rot = new Vector3(0f, 0f, 0f) },
            new Prefab { prefab = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab", pos = new Vector3(114.030f, 17.348f, -24.339f), rot = new Vector3(0f, 180f, 0f) }
        };

        internal HashSet<string> TrashList = new HashSet<string>
        {
            "minicopter.entity",
            "scraptransporthelicopter",
            "hotairballoon",
            "rowboat",
            "rhib",
            "submarinesolo.entity",
            "submarineduo.entity",
            "sled.deployed",
            "magnetcrane.entity",
            "sedantest.entity",
            "2module_car_spawned.entity",
            "3module_car_spawned.entity",
            "4module_car_spawned.entity",
            "wolf",
            "chicken",
            "boar",
            "stag",
            "bear",
            "testridablehorse",
            "servergibs_bradley",
            "servergibs_patrolhelicopter"
        };

        internal class ControllerHarborEvent : FacepunchBehaviour
        {
            private MapMarkerGenericRadius _mapmarker;
            private VendingMachineMapMarker _vendingMarker;
            private SphereCollider _sphereCollider;

            internal int Type;

            private bool _leavingCargo;
            internal CargoShip Cargo;
            private int _cargoTime = 5;

            internal BradleyAPC Bradley;
            internal Vector3 PositionBradley;

            internal BaseHelicopter Helicopter;

            internal PressButton Button;
            internal Door Door;

            internal bool KillEntities = false;
            internal HashSet<BaseEntity> Entities = new HashSet<BaseEntity>();
            internal HashSet<AutoTurret> Turrets = new HashSet<AutoTurret>();
            internal HashSet<SamSite> SamSites = new HashSet<SamSite>();

            private Coroutine _spawnEntitiesCoroutine = null;

            internal Dictionary<LootContainer, int> Crates = new Dictionary<LootContainer, int>();
            internal Dictionary<HackableLockedCrate, int> HackCrates = new Dictionary<HackableLockedCrate, int>();

            internal HashSet<ScientistNPC> Scientists = new HashSet<ScientistNPC>();

            internal int TimeToFinish;
            internal HashSet<BasePlayer> Players = new HashSet<BasePlayer>();

            private void Awake()
            {
                Location location = _ins.StartLocations.GetRandom();
                transform.position = location.pos;
                transform.rotation = Quaternion.Euler(location.rot);
                Type = location.type;

                gameObject.layer = 3;
                _sphereCollider = gameObject.AddComponent<SphereCollider>();
                _sphereCollider.isTrigger = true;
                _sphereCollider.radius = _ins._config.Radius;

                TimeToFinish = _ins._config.FinishTime;

                SpawnMapMarker();

                _spawnEntitiesCoroutine = ServerMgr.Instance.StartCoroutine(SpawnEntities());
            }

            private void OnDestroy()
            {
                if (_spawnEntitiesCoroutine != null) ServerMgr.Instance.StopCoroutine(_spawnEntitiesCoroutine);

                CancelInvoke(ChangeToFinishTime);

                CancelInvoke(UpdateMapMarker);
                if (_mapmarker.IsExists()) _mapmarker.Kill();
                if (_vendingMarker.IsExists()) _vendingMarker.Kill();

                KillEntities = true;

                foreach (BaseEntity entity in Entities) if (entity.IsExists()) entity.Kill();

                foreach (KeyValuePair<LootContainer, int> dic in Crates) if (dic.Key.IsExists()) dic.Key.Kill();
                foreach (KeyValuePair<HackableLockedCrate, int> dic in HackCrates) if (dic.Key.IsExists()) dic.Key.Kill();

                if (Bradley.IsExists()) Bradley.Kill();

                CancelInvoke(CheckMoveHelicopter);
                if (Helicopter.IsExists()) Helicopter.Kill();

                foreach (ScientistNPC npc in Scientists) if (npc.IsExists()) npc.Kill();

                if (Cargo.IsExists())
                {
                    if (_leavingCargo)
                    {
                        Cargo.FindInitialNode();
                        Cargo.egressing = false;
                        Cargo.StartEgress();
                    }
                    else Cargo.Kill();
                }

                foreach (BasePlayer player in Players) CuiHelper.DestroyUi(player, "Tabs_KpucTaJl");
            }

            private void OnTriggerEnter(Collider other)
            {
                BasePlayer player = other.GetComponentInParent<BasePlayer>();
                if (player.IsPlayer())
                {
                    Players.Add(player);
                    if (_ins._config.Gui.IsGui)
                    {
                        Dictionary<string, string> dic = new Dictionary<string, string> { ["Clock_KpucTaJl"] = GetTimeFormat() };
                        if (Crates.Count + HackCrates.Count > 0) dic.Add("Crate_KpucTaJl", $"{Crates.Count + HackCrates.Count}");
                        if (Scientists.Count > 0) dic.Add("Npc_KpucTaJl", Scientists.Count.ToString());
                        _ins.CreateTabs(player, dic);
                    }
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
                if (_cargoTime > 0 && Cargo.lootRoundsPassed >= CargoShip.loot_rounds) _cargoTime--;
                if (_cargoTime == 0 && Crates.Count == 0 && HackCrates.Count == 0 && TimeToFinish > _ins._config.PreFinishTime) TimeToFinish = _ins._config.PreFinishTime;
                else TimeToFinish--;
                if (_ins._config.Gui.IsGui)
                {
                    Dictionary<string, string> dic = new Dictionary<string, string> { ["Clock_KpucTaJl"] = GetTimeFormat() };
                    if (Crates.Count + HackCrates.Count > 0) dic.Add("Crate_KpucTaJl", $"{Crates.Count + HackCrates.Count}");
                    if (Scientists.Count > 0) dic.Add("Npc_KpucTaJl", Scientists.Count.ToString());
                    foreach (BasePlayer player in Players) _ins.CreateTabs(player, dic);
                }
                if (TimeToFinish == _ins._config.PreFinishTime) _ins.AlertToAllPlayers("PreFinish", _ins._config.Prefix, _ins._config.PreFinishTime);
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

            private IEnumerator SpawnEntities()
            {
                Vector3 pos; Quaternion rot;

                _leavingCargo = _ins._config.LeavingShip;

                if (Type == 1) GetGlobal(transform, new Vector3(95.651f, 0f, 120.546f), new Vector3(0f, 270f, 0f), out pos, out rot);
                else GetGlobal(transform, new Vector3(115.485f, 0f, -61.115f), new Vector3(0f, 0f, 0f), out pos, out rot);

                Cargo = GameManager.server.CreateEntity("assets/content/vehicles/boats/cargoship/cargoshiptest.prefab", pos, rot) as CargoShip;
                Cargo.enableSaving = false;
                Cargo.Spawn();

                Cargo.skinID = 81182151852251420;
                Cargo.CancelInvoke(Cargo.FindInitialNode);
                Cargo.egressing = true;

                RHIB rhib = Cargo.children.FirstOrDefault(x => x.ShortPrefabName == "rhib") as RHIB;
                if (rhib != null) rhib.EnableSaving(false);

                yield return CoroutineEx.waitForSeconds(3f);

                Cargo.transform.rotation = rot;
                Cargo.transform.RotateAround(pos, new Vector3(0, 1, 0), 0f);

                yield return CoroutineEx.waitForSeconds(1f);

                foreach (Prefab prefab in Type == 1 ? _ins.PrefabsHarborSmall : _ins.PrefabsHarborLarge)
                {
                    if (prefab.prefab == "assets/prefabs/npc/sam_site_turret/sam_static.prefab" && !_ins._config.IsSamSites) continue;
                    if (prefab.prefab == "assets/prefabs/npc/autoturret/autoturret_deployed.prefab" && !_ins._config.Turret.IsTurret) continue;

                    GetGlobal(transform, prefab.pos, prefab.rot, out pos, out rot);
                    BaseEntity entity = SpawnEntity(prefab.prefab, pos, rot);

                    if (entity is DecayEntity && !(entity is SamSite) && !(entity is CCTV_RC)) (entity as DecayEntity).lifestate = BaseCombatEntity.LifeState.Dead;

                    if (entity is BuildingBlock)
                    {
                        BuildingBlock buildingBlock = entity as BuildingBlock;
                        buildingBlock.SetGrade(BuildingGrade.Enum.Metal);
                        buildingBlock.SetHealthToMax();
                    }

                    if (entity is Door) Door = entity as Door;

                    if (entity is PressButton) Button = entity as PressButton;

                    if (entity is CCTV_RC)
                    {
                        CCTV_RC cctv = entity as CCTV_RC;
                        cctv.UpdateFromInput(5, 0);
                        cctv.rcIdentifier = _ins._config.Cctv;
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

                    if (entity is SamSite) SamSites.Add(entity as SamSite);

                    Entities.Add(entity);
                }

                SpawnCrates();

                if (_ins._config.Bradley.IsBradley)
                {
                    if (Type == 1) GetGlobal(transform, new Vector3(34.947f, 2.185f, 69.194f), new Vector3(0f, 180f, 0f), out pos, out rot);
                    else GetGlobal(transform, new Vector3(76.715f, 4.767f, -28.421f), new Vector3(0f, 270f, 0f), out pos, out rot);
                    PositionBradley = pos;

                    ChechTrash(pos, 10f);

                    SpawnSmoke(pos);

                    Bradley = GameManager.server.CreateEntity("assets/prefabs/npc/m2bradley/bradleyapc.prefab", pos, rot) as BradleyAPC;
                    Bradley.enableSaving = false;
                    Bradley.Spawn();

                    Bradley.skinID = 81182151852251420;

                    Bradley.InstallPatrolPath(new BasePath());
                    Bradley.patrolPath = null;

                    Bradley._maxHealth = _ins._config.Bradley.Hp;
                    Bradley.health = Bradley._maxHealth;

                    Bradley.maxCratesToSpawn = _ins._config.Bradley.CountCrates;

                    Bradley.viewDistance = _ins._config.Bradley.ViewDistance;
                    Bradley.searchRange = _ins._config.Bradley.SearchRange;

                    Bradley.coaxAimCone *= _ins._config.Bradley.CoaxAimCone;
                    Bradley.coaxFireRate *= _ins._config.Bradley.CoaxFireRate;
                    Bradley.coaxBurstLength = _ins._config.Bradley.CoaxBurstLength;

                    Bradley.nextFireTime = _ins._config.Bradley.NextFireTime;
                    Bradley.topTurretFireRate = _ins._config.Bradley.TopTurretFireRate;

                    Bradley.memoryDuration = _ins._config.Bradley.MemoryDuration;
                }

                foreach (PresetConfig preset in Type == 1 ? _ins._config.NpcSmall : _ins._config.NpcLarge) SpawnPreset(preset);

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
                        ["DamageTank"] = _ins._config.PveMode.DamageTank,
                        ["DamageHelicopter"] = _ins._config.PveMode.DamageHelicopter,
                        ["TargetNpc"] = _ins._config.PveMode.TargetNpc,
                        ["TargetTank"] = _ins._config.PveMode.TargetTank,
                        ["TargetHelicopter"] = _ins._config.PveMode.TargetHelicopter,
                        ["CanEnter"] = _ins._config.PveMode.CanEnter,
                        ["CanEnterCooldownPlayer"] = _ins._config.PveMode.CanEnterCooldownPlayer,
                        ["TimeExitOwner"] = _ins._config.PveMode.TimeExitOwner,
                        ["AlertTime"] = _ins._config.PveMode.AlertTime,
                        ["RestoreUponDeath"] = _ins._config.PveMode.RestoreUponDeath,
                        ["CooldownOwner"] = _ins._config.PveMode.CooldownOwner,
                        ["Darkening"] = _ins._config.PveMode.Darkening
                    };
                    HashSet<ulong> crates = Crates.Select(x => x.Key.net.ID.Value);
                    foreach (KeyValuePair<HackableLockedCrate, int> dic in HackCrates) crates.Add(dic.Key.net.ID.Value);
                    _ins.PveMode.Call("EventAddPveMode", _ins.Name, config, transform.position, _ins._config.Radius, crates, Scientists.Select(x => x.net.ID.Value), new HashSet<ulong> { Bradley.net.ID.Value }, new HashSet<ulong>(), new HashSet<ulong>(), null);
                }

                InvokeRepeating(ChangeToFinishTime, 0f, 1f);

                Interface.Oxide.CallHook("OnHarborEventStart", transform.position, _ins._config.Radius);
            }

            private void SpawnCrates()
            {
                foreach (Prefab prefab in Type == 1 ? _ins.CratesHarborSmall : _ins.CratesHarborLarge)
                {
                    Vector3 pos; Quaternion rot;
                    GetGlobal(transform, prefab.pos, prefab.rot, out pos, out rot);

                    LootContainer crate = GameManager.server.CreateEntity(prefab.prefab, pos, rot) as LootContainer;
                    crate.enableSaving = false;
                    crate.Spawn();

                    if (crate is HackableLockedCrate)
                    {
                        HackableLockedCrate hackcrate = crate as HackableLockedCrate;
                        HackCrates.Add(hackcrate, _ins._config.ContainerHackCrates.TypeLootTable);
                        hackcrate.hackSeconds = HackableLockedCrate.requiredHackSeconds - _ins._config.ContainerHackCrates.UnlockTime;
                        if (_ins._config.ContainerHackCrates.TypeLootTable == 1 || _ins._config.ContainerHackCrates.TypeLootTable == 4 || _ins._config.ContainerHackCrates.TypeLootTable == 5)
                        {
                            _ins.NextTick(() =>
                            {
                                crate.inventory.ClearItemsContainer();
                                if (_ins._config.ContainerHackCrates.TypeLootTable == 4 || _ins._config.ContainerHackCrates.TypeLootTable == 5) _ins.AddToContainerPrefab(crate.inventory, _ins._config.ContainerHackCrates.PrefabLootTable);
                                if (_ins._config.ContainerHackCrates.TypeLootTable == 1 || _ins._config.ContainerHackCrates.TypeLootTable == 5) _ins.AddToContainerItem(crate.inventory, _ins._config.ContainerHackCrates.OwnLootTable);
                            });
                        }
                    }
                    else
                    {
                        CrateConfig config = _ins._config.ContainerCrates.FirstOrDefault(x => x.Prefab == prefab.prefab);
                        Crates.Add(crate, config.TypeLootTable);
                        if (config.TypeLootTable == 1 || config.TypeLootTable == 4 || config.TypeLootTable == 5)
                        {
                            _ins.NextTick(() =>
                            {
                                crate.inventory.ClearItemsContainer();
                                if (config.TypeLootTable == 4 || config.TypeLootTable == 5) _ins.AddToContainerPrefab(crate.inventory, config.PrefabLootTable);
                                if (config.TypeLootTable == 1 || config.TypeLootTable == 5) _ins.AddToContainerItem(crate.inventory, config.OwnLootTable);
                            });
                        }
                    }
                }
            }

            internal bool IsMainHackCrate(HackableLockedCrate crate)
            {
                Vector3 pos = Type == 1 ? transform.TransformPoint(new Vector3(34.941f, 14.440f, 101.856f)) : transform.TransformPoint(new Vector3(114.030f, 17.348f, -24.339f));
                return Vector3.Distance(pos, crate.transform.position) < 1f;
            }

            internal void SpawnCargoCrate(LootContainer crate)
            {
                if (Cargo == null || crate.GetParentEntity() != Cargo) return;
                if (crate is HackableLockedCrate)
                {
                    HackableLockedCrate hackcrate = crate as HackableLockedCrate;
                    if (HackCrates.ContainsKey(hackcrate)) return;
                    HackCrates.Add(hackcrate, _ins._config.CargoHackCrates.TypeLootTable);
                    hackcrate.hackSeconds = HackableLockedCrate.requiredHackSeconds - _ins._config.CargoHackCrates.UnlockTime;
                    if (_ins._config.CargoHackCrates.TypeLootTable == 1 || _ins._config.CargoHackCrates.TypeLootTable == 4 || _ins._config.CargoHackCrates.TypeLootTable == 5)
                    {
                        crate.inventory.ClearItemsContainer();
                        if (_ins._config.CargoHackCrates.TypeLootTable == 4 || _ins._config.CargoHackCrates.TypeLootTable == 5) _ins.AddToContainerPrefab(crate.inventory, _ins._config.CargoHackCrates.PrefabLootTable);
                        if (_ins._config.CargoHackCrates.TypeLootTable == 1 || _ins._config.CargoHackCrates.TypeLootTable == 5) _ins.AddToContainerItem(crate.inventory, _ins._config.CargoHackCrates.OwnLootTable);
                    }
                }
                else
                {
                    if (Crates.ContainsKey(crate)) return;
                    CrateConfig config = _ins._config.CargoCrates.FirstOrDefault(x => x.Prefab == crate.PrefabName);
                    Crates.Add(crate, config.TypeLootTable);
                    if (config.TypeLootTable == 1 || config.TypeLootTable == 4 || config.TypeLootTable == 5)
                    {
                        crate.inventory.ClearItemsContainer();
                        if (config.TypeLootTable == 4 || config.TypeLootTable == 5) _ins.AddToContainerPrefab(crate.inventory, config.PrefabLootTable);
                        if (config.TypeLootTable == 1 || config.TypeLootTable == 5) _ins.AddToContainerItem(crate.inventory, config.OwnLootTable);
                    }
                }
                if (_ins._config.PveMode.Pve && _ins.plugins.Exists("PveMode")) _ins.PveMode.Call("EventAddCrates", _ins.Name, new HashSet<ulong> { crate.net.ID.Value });
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
                HashSet<string> states = config.Stationary ? new HashSet<string> { "IdleState", "CombatStationaryState" } : new HashSet<string> { "RoamState", "ChaseState", "CombatState" };
                if (config.BeltItems.Any(x => x.ShortName == "rocket.launcher" || x.ShortName == "explosive.timed")) states.Add("RaidState");
                return new JObject
                {
                    ["Name"] = config.Name,
                    ["WearItems"] = new JArray { config.WearItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["SkinID"] = x.SkinID }) },
                    ["BeltItems"] = new JArray { config.BeltItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["Amount"] = x.Amount, ["SkinID"] = x.SkinID, ["Mods"] = new JArray { x.Mods }, ["Ammo"] = x.Ammo }) },
                    ["Kit"] = config.Kit,
                    ["Health"] = config.Health,
                    ["RoamRange"] = config.RoamRange,
                    ["ChaseRange"] = config.ChaseRange,
                    ["SenseRange"] = config.SenseRange,
                    ["ListenRange"] = config.SenseRange / 2f,
                    ["AttackRangeMultiplier"] = config.AttackRangeMultiplier,
                    ["CheckVisionCone"] = config.CheckVisionCone,
                    ["VisionCone"] = config.VisionCone,
                    ["DamageScale"] = config.DamageScale,
                    ["TurretDamageScale"] = 1f,
                    ["AimConeScale"] = config.AimConeScale,
                    ["DisableRadio"] = config.DisableRadio,
                    ["CanRunAwayWater"] = true,
                    ["CanSleep"] = false,
                    ["Speed"] = config.Speed,
                    ["AreaMask"] = 1,
                    ["AgentTypeID"] = -1372625422,
                    ["HomePosition"] = string.Empty,
                    ["MemoryDuration"] = config.MemoryDuration,
                    ["States"] = new JArray { states }
                };
            }

            internal void SpawnCargoScientist(ScientistNPC entity)
            {
                if (entity.skinID == 11162132011012 || Cargo == null || entity.GetParentEntity() != Cargo) return;
                bool isStationary = entity.ShortPrefabName == "scientistnpc_cargo_turret_lr300" || entity.ShortPrefabName == "scientistnpc_cargo_turret_any";
                NpcConfigCargo config = entity.ShortPrefabName == "scientistnpc_cargo_turret_lr300" ? _ins._config.NpcStationaryOutsideCargo : entity.ShortPrefabName == "scientistnpc_cargo_turret_any" ? _ins._config.NpcStationaryInsideCargo : _ins._config.NpcMovingCargo;
                ScientistNPC npc = (ScientistNPC)_ins.NpcSpawn.Call("SpawnNpc", entity.transform.position, GetObjectConfigCargo(config, isStationary));
                Scientists.Add(npc);
                _ins.NextTick(() =>
                {
                    _ins.NpcSpawn.Call("AddParentEntity", npc, Cargo, Cargo.transform.InverseTransformPoint(entity.transform.position));
                    npc.Brain.Navigator.CanUseNavMesh = false;
                    if (!isStationary)
                    {
                        npc.Brain.Navigator.AStarGraph = entity.Brain.Navigator.AStarGraph;
                        npc.Brain.Navigator.CanUseAStar = true;
                    }
                    entity.Kill();
                });
            }

            private static JObject GetObjectConfigCargo(NpcConfigCargo config, bool isStationary)
            {
                HashSet<string> states = isStationary ? new HashSet<string> { "IdleState", "CombatStationaryState" } : new HashSet<string> { "RoamState", "ChaseState", "CombatState" };
                if (config.BeltItems.Any(x => x.ShortName == "rocket.launcher" || x.ShortName == "explosive.timed")) states.Add("RaidState");
                return new JObject
                {
                    ["Name"] = config.Name,
                    ["WearItems"] = new JArray { config.WearItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["SkinID"] = x.SkinID }) },
                    ["BeltItems"] = new JArray { config.BeltItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["Amount"] = x.Amount, ["SkinID"] = x.SkinID, ["Mods"] = new JArray { x.Mods }, ["Ammo"] = x.Ammo }) },
                    ["Kit"] = config.Kit,
                    ["Health"] = config.Health,
                    ["RoamRange"] = config.RoamRange,
                    ["ChaseRange"] = config.ChaseRange,
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
                    ["Speed"] = config.Speed,
                    ["AreaMask"] = 25,
                    ["AgentTypeID"] = 0,
                    ["HomePosition"] = string.Empty,
                    ["MemoryDuration"] = config.MemoryDuration,
                    ["States"] = new JArray { states }
                };
            }

            private static void SpawnSmoke(Vector3 pos)
            {
                SmokeGrenade grenade = GameManager.server.CreateEntity("assets/prefabs/tools/smoke grenade/grenade.smoke.deployed.prefab", pos) as SmokeGrenade;
                grenade.enableSaving = false;
                grenade.Spawn();
                grenade.GetComponent<Rigidbody>().useGravity = false;
            }

            private void ChechTrash(Vector3 pos, float radius) { foreach (BaseEntity entity in GetEntities<BaseEntity>(pos, radius, -1)) if (_ins.TrashList.Contains(entity.ShortPrefabName) && entity.IsExists()) entity.Kill(); }

            private static HashSet<T> GetEntities<T>(Vector3 position, float radius, int layerMask) where T : BaseEntity
            {
                HashSet<T> result = new HashSet<T>();
                foreach (Collider collider in Physics.OverlapSphere(position, radius, layerMask))
                {
                    BaseEntity entity = collider.ToBaseEntity();
                    if (entity.IsExists() && entity is T) result.Add(entity as T);
                }
                return result;
            }

            internal void SpawnHelicopter()
            {
                Helicopter = GameManager.server.CreateEntity("assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab") as BaseHelicopter;
                Helicopter.enableSaving = false;
                Helicopter.Spawn();

                Helicopter.skinID = 81182151852251420;

                Helicopter.startHealth = _ins._config.Helicopter.Hp;
                Helicopter.InitializeHealth(_ins._config.Helicopter.Hp, _ins._config.Helicopter.Hp);
                BaseHelicopter.weakspot[] weakspots = Helicopter.weakspots;
                weakspots[0].maxHealth = _ins._config.Helicopter.HpMainRotor;
                weakspots[1].maxHealth = _ins._config.Helicopter.HpTailRotor;
                weakspots[0].health = _ins._config.Helicopter.HpMainRotor;
                weakspots[1].health = _ins._config.Helicopter.HpTailRotor;

                Helicopter.maxCratesToSpawn = _ins._config.Helicopter.CountCrates;

                Helicopter.myAI.timeBetweenRockets = _ins._config.Helicopter.TimeBetweenRockets;
                Helicopter.myAI.leftGun.fireRate = Helicopter.myAI.rightGun.fireRate = _ins._config.Helicopter.FireRate;
                Helicopter.myAI.leftGun.burstLength = Helicopter.myAI.rightGun.burstLength = _ins._config.Helicopter.BurstLength;
                Helicopter.myAI.leftGun.timeBetweenBursts = Helicopter.myAI.rightGun.timeBetweenBursts = _ins._config.Helicopter.TimeBetweenBursts;
                Helicopter.myAI.leftGun.maxTargetRange = Helicopter.myAI.rightGun.maxTargetRange = _ins._config.Helicopter.MaxTargetRange;

                Helicopter.transform.position = transform.position + new Vector3(0f, 70f, 0f);

                if (_ins._config.PveMode.Pve && _ins.plugins.Exists("PveMode")) _ins.PveMode.Call("EventAddHelicopters", _ins.Name, new HashSet<ulong> { Helicopter.net.ID.Value });

                GoToNewPosHelicopter();

                InvokeRepeating(CheckMoveHelicopter, 1f, 1f);
            }

            private void CheckMoveHelicopter()
            {
                if (!Helicopter.IsExists())
                {
                    CancelInvoke(CheckMoveHelicopter);
                    return;
                }
                if (Helicopter.myAI._currentState == PatrolHelicopterAI.aiState.PATROL) GoToNewPosHelicopter();
            }

            private void GoToNewPosHelicopter()
            {
                Vector2 vector2 = UnityEngine.Random.insideUnitCircle * 70f;
                Vector3 pos = Cargo.transform.position + new Vector3(vector2.x, 70f, vector2.y);
                Helicopter.myAI.hasInterestZone = true;
                Helicopter.myAI.interestZoneOrigin = pos;
                Helicopter.myAI.ExitCurrentState();
                Helicopter.myAI.State_Move_Enter(pos);
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

        #region Spawn Loot
        #region NPC
        private void OnCorpsePopulate(ScientistNPC entity, NPCPlayerCorpse corpse)
        {
            if (entity == null) return;
            if (_controller.Scientists.Contains(entity))
            {
                _controller.Scientists.Remove(entity);
                int typeLootTable;
                PrefabLootTableConfig prefabLootTable;
                LootTableConfig ownTableConfig;
                bool isRemoveCorpse;
                if (entity.displayName == _config.NpcMovingCargo.Name)
                {
                    typeLootTable = _config.NpcMovingCargo.TypeLootTable;
                    prefabLootTable = _config.NpcMovingCargo.PrefabLootTable;
                    ownTableConfig = _config.NpcMovingCargo.OwnLootTable;
                    isRemoveCorpse = _config.NpcMovingCargo.IsRemoveCorpse;
                }
                else if (entity.displayName == _config.NpcStationaryInsideCargo.Name)
                {
                    typeLootTable = _config.NpcStationaryInsideCargo.TypeLootTable;
                    prefabLootTable = _config.NpcStationaryInsideCargo.PrefabLootTable;
                    ownTableConfig = _config.NpcStationaryInsideCargo.OwnLootTable;
                    isRemoveCorpse = _config.NpcStationaryInsideCargo.IsRemoveCorpse;
                }
                else if (entity.displayName == _config.NpcStationaryOutsideCargo.Name)
                {
                    typeLootTable = _config.NpcStationaryOutsideCargo.TypeLootTable;
                    prefabLootTable = _config.NpcStationaryOutsideCargo.PrefabLootTable;
                    ownTableConfig = _config.NpcStationaryOutsideCargo.OwnLootTable;
                    isRemoveCorpse = _config.NpcStationaryOutsideCargo.IsRemoveCorpse;
                }
                else
                {
                    PresetConfig preset = _controller.Type == 1 ? _config.NpcSmall.FirstOrDefault(x => x.Config.Name == entity.displayName) : _config.NpcLarge.FirstOrDefault(x => x.Config.Name == entity.displayName);
                    typeLootTable = preset.TypeLootTable;
                    prefabLootTable = preset.PrefabLootTable;
                    ownTableConfig = preset.OwnLootTable;
                    isRemoveCorpse = preset.Config.IsRemoveCorpse;
                }
                NextTick(() =>
                {
                    if (corpse == null) return;
                    ItemContainer container = corpse.containers[0];
                    if (typeLootTable == 1 || typeLootTable == 4 || typeLootTable == 5)
                    {
                        container.ClearItemsContainer();
                        if (typeLootTable == 4 || typeLootTable == 5) AddToContainerPrefab(container, prefabLootTable);
                        if (typeLootTable == 1 || typeLootTable == 5) AddToContainerItem(container, ownTableConfig);
                    }
                    if (isRemoveCorpse && corpse.IsExists()) corpse.Kill();
                });
            }
        }

        private object CanPopulateLoot(ScientistNPC entity, NPCPlayerCorpse corpse)
        {
            if (entity == null || corpse == null || _controller == null) return null;
            if (_controller.Scientists.Contains(entity))
            {
                if (GetTypeLootTableNpc(entity) == 2) return null;
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
                if (GetTypeLootTableNpc(entity) == 3) return null;
                else return true;
            }
            return null;
        }

        private int GetTypeLootTableNpc(ScientistNPC entity)
        {
            if (_config.NpcMovingCargo.Name == entity.displayName) return _config.NpcMovingCargo.TypeLootTable;
            else if (_config.NpcStationaryInsideCargo.Name == entity.displayName) return _config.NpcStationaryInsideCargo.TypeLootTable;
            else if (_config.NpcStationaryOutsideCargo.Name == entity.displayName) return _config.NpcStationaryOutsideCargo.TypeLootTable;
            else
            {
                PresetConfig preset = _controller.Type == 1 ? _config.NpcSmall.FirstOrDefault(x => x.Config.Name == entity.displayName) : _config.NpcLarge.FirstOrDefault(x => x.Config.Name == entity.displayName);
                return preset.TypeLootTable;
            }
        }
        #endregion NPC

        #region Crates
        private bool IsEventBradleyCrate(LootContainer container) => container is LockedByEntCrate && container.ShortPrefabName == "bradley_crate" && Vector3.Distance(container.transform.position, _controller.PositionBradley) < 10f;

        private void OnEntitySpawned(LockedByEntCrate crate)
        {
            if (crate != null && IsEventBradleyCrate(crate) && (_config.Bradley.TypeLootTable == 1 || _config.Bradley.TypeLootTable == 4 || _config.Bradley.TypeLootTable == 5))
            {
                NextTick(() =>
                {
                    crate.inventory.ClearItemsContainer();
                    if (_config.Bradley.TypeLootTable == 4 || _config.Bradley.TypeLootTable == 5) AddToContainerPrefab(crate.inventory, _config.Bradley.PrefabLootTable);
                    if (_config.Bradley.TypeLootTable == 1 || _config.Bradley.TypeLootTable == 5) AddToContainerItem(crate.inventory, _config.Bradley.OwnLootTable);
                });
            }
        }

        private object CanPopulateLoot(LootContainer container)
        {
            if (container == null || _controller == null) return null;
            else return GetResultLoot(container, 2);
        }

        private object OnCustomLootContainer(NetworkableId netID)
        {
            if (_controller == null) return null;
            LootContainer container = BaseNetworkable.serverEntities.Find(netID) as LootContainer;
            if (container == null) return null;
            return GetResultLoot(container, 3);
        }

        private object OnContainerPopulate(LootContainer container)
        {
            if (container == null || _controller == null) return null;
            else return GetResultLoot(container, 6);
        }

        private object GetResultLoot(LootContainer container, int type)
        {
            int typeLootTable;
            if (_controller.Crates.TryGetValue(container, out typeLootTable))
            {
                if (typeLootTable == type) return null;
                else return true;
            }
            else if (container is HackableLockedCrate && _controller.HackCrates.TryGetValue(container as HackableLockedCrate, out typeLootTable))
            {
                if (typeLootTable == type) return null;
                else return true;
            }
            else if (IsEventBradleyCrate(container))
            {
                if (_config.Bradley.TypeLootTable == type) return null;
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
            foreach (CrateConfig crateConfig in _config.ContainerCrates)
            {
                CheckLootTable(crateConfig.OwnLootTable);
                CheckPrefabLootTable(crateConfig.PrefabLootTable);
            }
            foreach (CrateConfig crateConfig in _config.CargoCrates)
            {
                CheckLootTable(crateConfig.OwnLootTable);
                CheckPrefabLootTable(crateConfig.PrefabLootTable);
            }

            CheckLootTable(_config.ContainerHackCrates.OwnLootTable);
            CheckPrefabLootTable(_config.ContainerHackCrates.PrefabLootTable);

            CheckLootTable(_config.CargoHackCrates.OwnLootTable);
            CheckPrefabLootTable(_config.CargoHackCrates.PrefabLootTable);

            CheckLootTable(_config.Bradley.OwnLootTable);
            CheckPrefabLootTable(_config.Bradley.PrefabLootTable);

            foreach (PresetConfig preset in _config.NpcSmall)
            {
                CheckLootTable(preset.OwnLootTable);
                CheckPrefabLootTable(preset.PrefabLootTable);
            }

            foreach (PresetConfig preset in _config.NpcLarge)
            {
                CheckLootTable(preset.OwnLootTable);
                CheckPrefabLootTable(preset.PrefabLootTable);
            }

            CheckLootTable(_config.NpcMovingCargo.OwnLootTable);
            CheckPrefabLootTable(_config.NpcMovingCargo.PrefabLootTable);

            CheckLootTable(_config.NpcStationaryInsideCargo.OwnLootTable);
            CheckPrefabLootTable(_config.NpcStationaryInsideCargo.PrefabLootTable);

            CheckLootTable(_config.NpcStationaryOutsideCargo.OwnLootTable);
            CheckPrefabLootTable(_config.NpcStationaryOutsideCargo.PrefabLootTable);

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
            else if (attacker is SamSite && _controller.SamSites.Contains(attacker as SamSite)) return true;
            else if (attacker is BasePlayer && _config.IsCreateZonePvp && _controller.Players.Contains(victim) && _controller.Players.Contains(attacker as BasePlayer)) return true;
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

        #region BetterNpc
        private object CanBradleySpawnNpc(BradleyAPC bradley)
        {
            if (_controller == null) return null;
            if (Vector3.Distance(bradley.transform.position, _controller.transform.position) < _config.Radius) return true;
            else return null;
        }

        private object CanHelicopterSpawnNpc(BaseHelicopter helicopter)
        {
            if (_controller == null) return null;
            if (Vector3.Distance(helicopter.transform.position, _controller.transform.position) < _config.Radius) return true;
            else return null;
        }
        #endregion BetterNpc

        #region Bradley Tiers
        private object CanBradleyTiersEdit(BradleyAPC bradley)
        {
            if (_controller == null) return null;
            if (Vector3.Distance(bradley.transform.position, _controller.transform.position) < _config.Radius) return true;
            else return null;
        }
        #endregion Bradley Tiers

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
                case "Bradley":
                    AddBalance(playerId, _config.Economy.Bradley);
                    break;
                case "Npc":
                    AddBalance(playerId, _config.Economy.Npc);
                    break;
                case "LockedCrate":
                    AddBalance(playerId, _config.Economy.LockedCrate);
                    break;
                case "Button":
                    AddBalance(playerId, _config.Economy.Button);
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
            Interface.Oxide.CallHook("OnHarborEventWinner", winnerId);
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
            new ImageURL { Name = "Crate_KpucTaJl", Url = "Images/Crate_KpucTaJl.png" },
            new ImageURL { Name = "Npc_KpucTaJl", Url = "Images/Npc_KpucTaJl.png" }
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

        private IEnumerator ProcessDownloadImage(ImageURL image)
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
        [PluginReference] private readonly Plugin NpcSpawn, BetterNpc, PveMode;

        private readonly HashSet<string> _hooks = new HashSet<string>
        {
            "OnEntityTakeDamage",
            "CanAffordUpgrade",
            "OnStructureRotate",
            "OnButtonPress",
            "OnPlayerDeath",
            "OnEntityDeath",
            "CanBradleyApcTarget",
            "OnEntityKill",
            "CanHackCrate",
            "OnCrateHack",
            "OnTurretTarget",
            "OnLootEntity",
            "OnPlayerCommand",
            "OnServerCommand",
            "OnCorpsePopulate",
            "CanPopulateLoot",
            "OnCustomLootNPC",
            "OnEntitySpawned",
            "OnCustomLootContainer",
            "OnContainerPopulate",
            "CanEntityTakeDamage",
            "CanEntityBeTargeted",
            "CanTeleport",
            "CanBradleySpawnNpc",
            "CanHelicopterSpawnNpc",
            "CanBradleyTiersEdit"
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

        internal class Location { public int type; public Vector3 pos; public Vector3 rot; }

        internal List<Location> StartLocations = new List<Location>();

        private static BaseEntity SpawnEntity(string prefab, Vector3 pos, Quaternion rot)
        {
            BaseEntity entity = GameManager.server.CreateEntity(prefab, pos, rot);
            entity.enableSaving = false;

            GroundWatch groundWatch = entity.GetComponent<GroundWatch>();
            if (groundWatch != null) UnityEngine.Object.DestroyImmediate(groundWatch);

            DestroyOnGroundMissing destroyOnGroundMissing = entity.GetComponent<DestroyOnGroundMissing>();
            if (destroyOnGroundMissing != null) UnityEngine.Object.DestroyImmediate(destroyOnGroundMissing);

            entity.Spawn();

            if (entity is StabilityEntity) (entity as StabilityEntity).grounded = true;
            if (entity is BaseCombatEntity) (entity as BaseCombatEntity).pickup.enabled = false;

            return entity;
        }
        #endregion Helpers

        #region Commands
        [ChatCommand("harborstart")]
        private void ChatStartEvent(BasePlayer player)
        {
            if (player.IsAdmin)
            {
                if (!_active) Start();
                else PrintToChat(player, GetMessage("EventActive", player.UserIDString, _config.Prefix));
            }
        }

        [ChatCommand("harborstop")]
        private void ChatStopEvent(BasePlayer player)
        {
            if (player.IsAdmin)
            {
                if (_controller != null) Finish();
                else Interface.Oxide.ReloadPlugin(Name);
            }
        }

        [ChatCommand("harborpos")]
        private void ChatCommandPos(BasePlayer player)
        {
            if (!player.IsAdmin || _controller == null) return;
            Vector3 pos = _controller.transform.InverseTransformPoint(player.transform.position);
            Puts($"Position: {pos}");
            PrintToChat(player, $"Position: {pos}");
        }

        [ConsoleCommand("harborstart")]
        private void ConsoleStartEvent(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
            {
                if (!_active) Start();
                else Puts("This event is active now. To finish this event (harborstop), then to start the next one");
            }
        }

        [ConsoleCommand("harborstop")]
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

namespace Oxide.Plugins.HarborEventExtensionMethods
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