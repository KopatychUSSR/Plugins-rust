using System;
using System.Collections;
using System.Collections.Generic;
using Facepunch;
using UnityEngine;
using UnityEngine.AI;
using Oxide.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.IO;
using UnityEngine.Networking;
using Oxide.Plugins.TriangulationExtensionMethods;

namespace Oxide.Plugins
{
    [Info("Triangulation", "KpucTaJl", "1.1.1")]
    internal class Triangulation : RustPlugin
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
            if (_config.PluginVersion < new VersionNumber(1, 0, 2))
            {
                _config.CooldownOwner = 86400;
            }
            if (_config.PluginVersion < new VersionNumber(1, 0, 7))
            {
                _config.SkillTree = false;
                _config.XPerience = false;
                _config.RustRewards = false;
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
            [JsonProperty(En ? "Give blueprint? [true/false]" : "Это чертеж? [true/false]")] public bool IsBluePrint { get; set; }
            [JsonProperty("SkinID (0 = default)")] public ulong SkinId { get; set; }
            [JsonProperty(En ? "Name (empty = default)" : "Название (empty - default)")] public string Name { get; set; }
        }

        public class LootTableConfig
        {
            [JsonProperty(En ? "Minimum items" : "Минимальное кол-во элементов")] public int Min { get; set; }
            [JsonProperty(En ? "Maximum items" : "Максимальное кол-во элементов")] public int Max { get; set; }
            [JsonProperty(En ? "Enforce minimum and maximum values? [true/false]" : "Использовать минимальное и максимальное значение? [true/false]")] public bool UseCount { get; set; }
            [JsonProperty(En ? "List of items" : "Список предметов")] public List<ItemConfig> Items { get; set; }
        }

        public class PrefabConfig
        {
            [JsonProperty(En ? "Chance [0.0-100.0]" : "Шанс выпадения [0.0-100.0]")] public float Chance { get; set; }
            [JsonProperty(En ? "The path to the prefab (Entity full name)" : "Путь к prefab-у")] public string PrefabDefinition { get; set; }
        }

        public class PrefabLootTableConfig
        {
            [JsonProperty(En ? "Minimum numbers of prefabs" : "Минимальное кол-во prefab-ов")] public int Min { get; set; }
            [JsonProperty(En ? "Maximum numbers of prefabs" : "Максимальное кол-во prefab-ов")] public int Max { get; set; }
            [JsonProperty(En ? "Enforce minimum and maximum values? [true/false]" : "Использовать минимальное и максимальное значение? [true/false]")] public bool UseCount { get; set; }
            [JsonProperty(En ? "List of prefabs (Entity full names)" : "Список prefab-ов")] public List<PrefabConfig> Prefabs { get; set; }
        }

        public class LootTable
        {
            [JsonProperty(En ? "Which loot table should the plugin use? (0 - own custom table; 1 - Rust prefab loot table; 2 - combine both 0 & 1 tables)" : "Какую таблицу лута необходимо использовать? (0 - собственную; 1 - таблица предметов объектов Rust; 2 - совместить 0 и 1 методы)")] public int TypeLootTable { get; set; }
            [JsonProperty(En ? "Rust prefab loot table (if the loot table type is 1 or 2)" : "Таблица предметов из prefab-ов (если тип таблицы предметов - 1 или 2)")] public PrefabLootTableConfig PrefabLootTable { get; set; }
            [JsonProperty(En ? "Own custom table (if the loot table type is 0 or 2)" : "Собственная таблица предметов (если тип таблицы предметов - 0 или 2)")] public LootTableConfig OwnLootTable { get; set; }
        }

        public class ColorConfig
        {
            [JsonProperty("r")] public float R { get; set; }
            [JsonProperty("g")] public float G { get; set; }
            [JsonProperty("b")] public float B { get; set; }
        }

        public class MarkerConfig
        {
            [JsonProperty(En ? "Use map marker? [true/false]" : "Использовать маркер на карте? [true/false]")] public bool Enabled { get; set; }
            [JsonProperty(En ? "Type (0 - simple, 1 - advanced)" : "Тип (0 - упрощенный, 1 - расширенный)")] public int Type { get; set; }
            [JsonProperty(En ? "Background radius (if the marker type is 0)" : "Радиус фона (если тип маркера - 0)")] public float Radius { get; set; }
            [JsonProperty(En ? "Background transparency" : "Прозрачность фона")] public float Alpha { get; set; }
            [JsonProperty(En ? "Color" : "Цвет")] public ColorConfig Color { get; set; }
            [JsonProperty(En ? "Text" : "Текст")] public string Text { get; set; }
        }

        public class PointConfig
        {
            [JsonProperty(En ? "Enabled? [true/false]" : "Включен? [true/false]")] public bool Enabled { get; set; }
            [JsonProperty(En ? "Text" : "Текст")] public string Text { get; set; }
            [JsonProperty(En ? "Size" : "Размер")] public int Size { get; set; }
            [JsonProperty(En ? "Color" : "Цвет")] public string Color { get; set; }
        }

        public class GuiConfig
        {
            [JsonProperty(En ? "Use the GUI? [true/false]" : "Использовать ли GUI? [true/false]")] public bool IsGui { get; set; }
            [JsonProperty("OffsetMin Y")] public string OffsetMinY { get; set; }
        }

        public class ChatConfig
        {
            [JsonProperty(En ? "Use chat messages? [true/false]" : "Использовать ли чат? [true/false]")] public bool IsChat { get; set; }
            [JsonProperty(En ? "Prefix for chat messages" : "Префикс сообщений в чате")] public string Prefix { get; set; }
        }

        public class GameTipConfig
        {
            [JsonProperty(En ? "Use Facepunch Game Tips (notification bar above hotbar)? [true/false]" : "Использовать ли Facepunch Game Tip (оповещения над слотами быстрого доступа игрока)? [true/false]")] public bool IsGameTip { get; set; }
            [JsonProperty(En ? "Style (0 - Blue Normal, 1 - Red Normal, 2 - Blue Long, 3 - Blue Short, 4 - Server Event)" : "Стиль (0 - Blue Normal, 1 - Red Normal, 2 - Blue Long, 3 - Blue Short, 4 - Server Event)")] public int Style { get; set; }
        }

        public class GuiAnnouncementsConfig
        {
            [JsonProperty(En ? "Do you use the plugin GUIAnnouncements? [true/false]" : "Использовать ли GUIAnnouncements? [true/false]")] public bool IsGuiAnnouncements { get; set; }
            [JsonProperty(En ? "Banner color" : "Цвет баннера")] public string BannerColor { get; set; }
            [JsonProperty(En ? "Text color" : "Цвет текста")] public string TextColor { get; set; }
            [JsonProperty(En ? "Adjust Vertical Position" : "Отступ от верхнего края")] public float ApiAdjustVPosition { get; set; }
        }

        public class NotifyConfig
        {
            [JsonProperty(En ? "Do you use the plugin Notify? [true/false]" : "Использовать ли Notify? [true/false]")] public bool IsNotify { get; set; }
            [JsonProperty(En ? "Type" : "Тип")] public int Type { get; set; }
        }

        public class DiscordConfig
        {
            [JsonProperty(En ? "Do you use the plugin DiscordMessages? [true/false]" : "Использовать ли DiscordMessages? [true/false]")] public bool IsDiscord { get; set; }
            [JsonProperty("Webhook URL")] public string WebhookUrl { get; set; }
            [JsonProperty(En ? "Embed Color (DECIMAL)" : "Цвет полосы (DECIMAL)")] public int EmbedColor { get; set; }
            [JsonProperty(En ? "Keys of required messages" : "Ключи необходимых сообщений")] public HashSet<string> Keys { get; set; }
        }

        public class EconomyConfig
        {
            [JsonProperty(En ? "Which economy plugins do you want to use? (Economics, ServerRewards, IQEconomic, XPerience)" : "Какие плагины экономики вы хотите использовать? (Economics, ServerRewards, IQEconomic, XPerience)")] public HashSet<string> Plugins { get; set; }
            [JsonProperty(En ? "The minimum value that a player must collect to get economy reward" : "Минимальное значение, которое игрок должен заработать, чтобы получить баллы за экономику")] public double Min { get; set; }
            [JsonProperty(En ? "Killing animals" : "Убийство животных")] public Dictionary<string, double> Animals { get; set; }
            [JsonProperty(En ? "Start scanning on the signal receiver" : "Начало сканирования на приемнике сигнала")] public double StartScan { get; set; }
            [JsonProperty(En ? "List of commands that are executed in server console at the end of the event ({steamid} - the player who collected the highest number of points)" : "Список команд, которые выполняются в консоли по окончанию ивента ({steamid} - игрок, который набрал наибольшее кол-во баллов)")] public HashSet<string> Commands { get; set; }
        }

        public class AnimalConfig
        {
            [JsonProperty(En ? "Type (1 - Polar Bear, 2 - Bear, 3 - Wolf, 4 - Boar, 5 - Stag, 6 - Chicken)" : "Тип (1 - Полярный медведь, 2 - Медведь, 3 - Волк, 4 - Кабан, 5 - Олень, 6 - Курица)")] public int Type { get; set; }
            [JsonProperty(En ? "Can an animal attack players? [true/false]" : "Может ли атаковать игроков? [true/false]")] public bool CanAttackPlayer { get; set; }
            [JsonProperty(En ? "Health" : "Кол-во ХП")] public float Health { get; set; }
            [JsonProperty(En ? "Attack Range" : "Радиус атаки")] public float AttackRange { get; set; }
            [JsonProperty(En ? "Attack Damage" : "Урон от атаки")] public float AttackDamage { get; set; }
            [JsonProperty(En ? "Animal damage each second to signal receiver" : "Кол-во урона по приемнику сигнала в секунду")] public float DamagePerSec { get; set; }
            [JsonProperty(En ? "Attack Rate [sec.]" : "Минимальное время между атаками [sec.]")] public float AttackRate { get; set; }
            [JsonProperty(En ? "Speed" : "Скорость")] public float Speed { get; set; }
        }

        public class AmountConfig
        {
            [JsonProperty(En ? "Chance probability [0.0-100.0]" : "Шанс [0.0-100.0]")] public float Chance { get; set; }
            [JsonProperty(En ? "Count" : "Кол-во")] public int Count { get; set; }
        }

        public class PresetConfig
        {
            [JsonProperty(En ? "ShortName of the animal preset" : "ShortName набора животного")] public string ShortName { get; set; }
            [JsonProperty(En ? "Setting number of animals depending on probability" : "Настройка кол-ва животных в зависимoсти от вероятности")] public HashSet<AmountConfig> ListAmountConfig { get; set; }
        }

        public class WaveConfig
        {
            [JsonProperty(En ? "Level" : "Уровень")] public int Level { get; set; }
            [JsonProperty(En ? "Preparation time [sec.]" : "Время подготовки [sec.]")] public int TimeToStart { get; set; }
            [JsonProperty(En ? "Duration [sec.]" : "Длительность [sec.]")] public int Duration { get; set; }
            [JsonProperty(En ? "Time until appearance of new animals [sec.]" : "Время появления новых животных [sec.]")] public int TimerAnimal { get; set; }
            [JsonProperty(En ? "Animal sets" : "Наборы животных")] public HashSet<PresetConfig> Presets { get; set; }
        }

        public class ReceiverConfig
        {
            [JsonProperty(En ? "Health" : "Кол-во ХП")] public float Health { get; set; }
            [JsonProperty(En ? "Health to get one battery" : "Кол-во ХП для получения одной батарейки")] public float HealthPerBattery { get; set; }
            [JsonProperty(En ? "The loot table in crate at the end of the scan" : "Таблица предметов в ящике при завершении сканирования")] public LootTable Loot { get; set; }
            [JsonProperty(En ? "List of attack waves" : "Список волн атаки")] public HashSet<WaveConfig> Waves { get; set; }
        }

        public class DrillConfig
        {
            [JsonProperty(En ? "The operating time of the drilling rig from each battery [sec.]" : "Время работы от одной батарейки [sec.]")] public int WorkingTimePerBattery { get; set; }
            [JsonProperty(En ? "Maximum operating time [sec.]" : "Максимальное время работы [sec.]")] public int MaxWorkingTime { get; set; }
            [JsonProperty(En ? "Lifetime [sec.]" : "Время существования [sec.]")] public int LifeTime { get; set; }
            [JsonProperty(En ? "The duration of item acquisition cycle [sec.]" : "Продолжительность цикла получения предметов [sec.]")] public int CycleTime { get; set; }
            [JsonProperty(En ? "Loot table for one item acquisition cycle" : "Таблица предметов для одного цикла получения предметов")] public LootTable Loot { get; set; }
        }

        public class LocationConfig
        {
            [JsonProperty(En ? "Position of the signal receiver or drilling rig" : "Позиция приемника сигнала или буровой установки")] public string Main { get; set; }
            [JsonProperty(En ? "List of positions for animals (if it is a signal receiver)" : "Список позиций для животных (если это приемник сигнала)")] public HashSet<string> Animals { get; set; }
        }

        public class MonumentConfig
        {
            [JsonProperty(En ? "Name of monument" : "Название монумента")] public string Name { get; set; }
            [JsonProperty(En ? "List of locations" : "Список расположений")] public HashSet<LocationConfig> Locations { get; set; }
        }

        public class SpawnConfig
        {
            [JsonProperty(En ? "What type of event appearance? (0 - random, 1 - standard monuments, 2 - custom monuments, 3 - combine 1 & 2 both, 4 - random but will default to 3 if no location is found)" : "Какой тип появления ивента необходимо использовать? (0 - рандомный, 1 - стандартные монументы, 2 - кастомные монументы, 3 - совместить 1 и 2 методы, 4 - рандомный, но если позиция не найдена, то 3 метод)")] public int Type { get; set; }
            [JsonProperty(En ? "List of biomes for the appearance of the event" : "Список биомов для появления ивента")] public HashSet<string> Biomes { get; set; }
            [JsonProperty(En ? "List of positions on standard monuments (all positions are relative to the monument)" : "Список позиций на стандартных монументах (все позиции являются локальными относительно монумента)")] public HashSet<MonumentConfig> Monuments { get; set; }
            [JsonProperty(En ? "List of positions on custom monuments (all positions are relative to map)" : "Список позиций на кастомных монументах (все позиции являются глобальными на карте)")] public HashSet<LocationConfig> CustomMonuments { get; set; }
        }

        private class PluginConfig
        {
            [JsonProperty(En ? "Minimum time between events [sec.]" : "Минимальное время между ивентами [sec.]")] public float MinStartTime { get; set; }
            [JsonProperty(En ? "Maximum time between events [sec.]" : "Максимальное время между ивентами [sec.]")] public float MaxStartTime { get; set; }
            [JsonProperty(En ? "Use minimum and maximum event start values? [true/false]" : "Активен ли таймер для запуска ивента? [true/false]")] public bool EnabledTimer { get; set; }
            [JsonProperty(En ? "Delay of event start from the command or timer running (starts when chat message appears) [sec.]" : "Время до начала ивента после сообщения в чате [sec.]")] public float PreStartTime { get; set; }
            [JsonProperty(En ? "Waiting time for the scan to start after the signal receiver appears [sec.]" : "Время ожидания запуска сканирования после появления приемника сигнала [sec.]")] public float ScanWaitTime { get; set; }
            [JsonProperty(En ? "Time until a new receiver or drilling machine appears (if all receivers have successfully completed scanning) [sec.]" : "Время до появления нового приемника или буровой машины (если все приемники успешно завершили сканирование) [sec.]")] public float PreStartStage { get; set; }
            [JsonProperty(En ? "List of animal presets" : "Список наборов животных")] public Dictionary<string, AnimalConfig> Presets { get; set; }
            [JsonProperty(En ? "Settings for the first receiver" : "Настройки для первого приемника")] public ReceiverConfig FirstReceiver { get; set; }
            [JsonProperty(En ? "Settings for the second receiver" : "Настройки для второго приемника")] public ReceiverConfig SecondReceiver { get; set; }
            [JsonProperty(En ? "Settings for the third receiver" : "Настройки для третьего приемника")] public ReceiverConfig ThirdReceiver { get; set; }
            [JsonProperty(En ? "Drilling Rig Settings" : "Настройки для буровой установки")] public DrillConfig Drill { get; set; }
            [JsonProperty(En ? "Marker configuration on the map" : "Настройка маркера на карте")] public MarkerConfig Marker { get; set; }
            [JsonProperty(En ? "Marker settings for key event points shown on players screen" : "Настройки маркера на экране игрока")] public PointConfig Point { get; set; }
            [JsonProperty(En ? "GUI setting" : "Настройки GUI")] public GuiConfig Gui { get; set; }
            [JsonProperty(En ? "Chat Message setting" : "Настройки сообщений в чате")] public ChatConfig Chat { get; set; }
            [JsonProperty(En ? "Facepunch Game Tips setting" : "Настройка сообщений Facepunch Game Tip")] public GameTipConfig GameTip { get; set; }
            [JsonProperty(En ? "GUI Announcements setting (only for GUIAnnouncements plugin)" : "Настройка GUI Announcements (только для тех, кто использует плагин GUI Announcements)")] public GuiAnnouncementsConfig GuiAnnouncements { get; set; }
            [JsonProperty(En ? "Notify setting (only for Notify plugin)" : "Настройка Notify (только для тех, кто использует плагин Notify)")] public NotifyConfig Notify { get; set; }
            [JsonProperty(En ? "Discord setting (only for DiscordMessages plugin)" : "Настройка оповещений в Discord (только для тех, кто использует плагин Discord Messages)")] public DiscordConfig Discord { get; set; }
            [JsonProperty(En ? "Do you create a PVP zone in the event area? (only for TruePVE plugin) [true/false]" : "Создавать зону PVP в зоне проведения ивента? (только для тех, кто использует плагин TruePVE) [true/false]")] public bool IsCreateZonePvp { get; set; }
            [JsonProperty(En ? "Can a non-owner of the event loot the crates? [true/false]" : "Может ли не владелец ивента открывать ящики? [true/false]")] public bool LootCrate { get; set; }
            [JsonProperty(En ? "Can a non-owner of the event deal damage to the animal? [true/false]" : "Может ли не владелец ивента наносить урон по животным? [true/false]")] public bool DamageNpc { get; set; }
            [JsonProperty(En ? "Can an animal attack a non-owner of the event? [true/false]" : "Могут ли животные атаковать не владельца ивента? [true/false]")] public bool TargetNpc { get; set; }
            [JsonProperty(En ? "Can a non-owner of the event turn on/off the drilling rig? [true/false]" : "Может ли не владелец ивента включать/отключать буровую установку? [true/false]")] public bool SwitchDrill { get; set; }
            [JsonProperty(En ? "The time to save the status of the event owner for the second/third receiver of the signal after its appearance on the map" : "Время сохранения статуса владельца ивента на второй/третий приемник сигнала после его появления на карте [sec.]")] public float OwnerTime { get; set; }
            [JsonProperty(En ? "Prevent the actions of the RestoreUponDeath plugin in the event zone? [true/false]" : "Запрещать работу плагина RestoreUponDeath в зоне действия ивента? [true/false]")] public bool RestoreUponDeath { get; set; }
            [JsonProperty(En ? "The time that the player can`t become the Event Owner, after the end of the event and the player was its owner [sec.]" : "Время, которое игрок не сможет стать владельцем ивента, после того как ивент окончен и игрок был его владельцем [sec.]")] public double CooldownOwner { get; set; }
            [JsonProperty(En ? "Interrupt teleporting in the event area? (only for users NTeleportation plugin) [true/false]" : "Запрещать телепорт в зоне проведения ивента? (только для тех, кто использует плагин NTeleportation) [true/false]")] public bool NTeleportationInterrupt { get; set; }
            [JsonProperty(En ? "Economy setting (total values will be added up and rewarded at the end of the event)" : "Настройка экономики (конечное значение суммируется и будет выдано игрокам по окончанию ивента)")] public EconomyConfig Economy { get; set; }
            [JsonProperty(En ? "List of commands banned in the event zone" : "Список команд запрещенных в зоне ивента")] public HashSet<string> Commands { get; set; }
            [JsonProperty(En ? "Position settings for the event to appear on the map" : "Настройки позиций для появления ивента на карте")] public SpawnConfig Spawn { get; set; }
            [JsonProperty(En ? "Should the Skill Tree plugin work inside the event area? [true/false]" : "Должен ли работать плагин Skill Tree внутри зоны ивента? [true/false]")] public bool SkillTree { get; set; }
            [JsonProperty(En ? "Should the XPerience plugin work inside the event area? [true/false]" : "Должен ли работать плагин XPerience внутри зоны ивента? [true/false]")] public bool XPerience { get; set; }
            [JsonProperty(En ? "Should the Rust Rewards plugin work inside the event area? [true/false]" : "Должен ли работать плагин Rust Rewards внутри зоны ивента? [true/false]")] public bool RustRewards { get; set; }
            [JsonProperty(En ? "Configuration version" : "Версия конфигурации")] public VersionNumber PluginVersion { get; set; }

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig
                {
                    MinStartTime = 14400f,
                    MaxStartTime = 14400f,
                    EnabledTimer = true,
                    PreStartTime = 300f,
                    ScanWaitTime = 600f,
                    PreStartStage = 60f,
                    Presets = new Dictionary<string, AnimalConfig>
                    {
                        ["polarbear1"] = new AnimalConfig
                        {
                            Type = 1,
                            CanAttackPlayer = true,
                            Health = 500f,
                            AttackRange = 2.5f,
                            AttackDamage = 70f,
                            DamagePerSec = 20f,
                            AttackRate = 5f,
                            Speed = 2f
                        },
                        ["bear1"] = new AnimalConfig
                        {
                            Type = 2,
                            CanAttackPlayer = true,
                            Health = 400f,
                            AttackRange = 2.5f,
                            AttackDamage = 50f,
                            DamagePerSec = 15f,
                            AttackRate = 4f,
                            Speed = 2.5f
                        },
                        ["wolf1"] = new AnimalConfig
                        {
                            Type = 3,
                            CanAttackPlayer = true,
                            Health = 100f,
                            AttackRange = 2f,
                            AttackDamage = 50f,
                            DamagePerSec = 10f,
                            AttackRate = 2f,
                            Speed = 11f
                        },
                        ["boar1"] = new AnimalConfig
                        {
                            Type = 4,
                            CanAttackPlayer = true,
                            Health = 250f,
                            AttackRange = 2f,
                            AttackDamage = 35f,
                            DamagePerSec = 10f,
                            AttackRate = 3f,
                            Speed = 4f
                        },
                        ["stag1"] = new AnimalConfig
                        {
                            Type = 5,
                            CanAttackPlayer = true,
                            Health = 80f,
                            AttackRange = 2f,
                            AttackDamage = 35f,
                            DamagePerSec = 10f,
                            AttackRate = 1f,
                            Speed = 15f
                        },
                        ["chicken1"] = new AnimalConfig
                        {
                            Type = 6,
                            CanAttackPlayer = true,
                            Health = 25f,
                            AttackRange = 1.2f,
                            AttackDamage = 20f,
                            DamagePerSec = 10f,
                            AttackRate = 1f,
                            Speed = 6f
                        }
                    },
                    FirstReceiver = new ReceiverConfig
                    {
                        Health = 2000f,
                        HealthPerBattery = 50f,
                        Loot = new LootTable
                        {
                            TypeLootTable = 0,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1,
                                Max = 1,
                                UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 3,
                                Max = 3,
                                UseCount = true,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig { ShortName = "ammo.rifle", MinAmount = 1500, MaxAmount = 1500, Chance = 100f, IsBluePrint = false, SkinId = 0, Name = "" },
                                    new ItemConfig { ShortName = "syringe.medical", MinAmount = 7, MaxAmount = 8, Chance = 100f, IsBluePrint = false, SkinId = 0, Name = "" },
                                    new ItemConfig { ShortName = "largemedkit", MinAmount = 4, MaxAmount = 5, Chance = 100f, IsBluePrint = false, SkinId = 0, Name = "" }
                                }
                            }
                        },
                        Waves = new HashSet<WaveConfig>
                        {
                            new WaveConfig
                            {
                                Level = 1,
                                TimeToStart = 1,
                                Duration = 60,
                                TimerAnimal = 10,
                                Presets = new HashSet<PresetConfig>
                                {
                                    new PresetConfig
                                    {
                                        ShortName = "polarbear1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 2 } }
                                    },
                                    new PresetConfig
                                    {
                                        ShortName = "chicken1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 2 } }
                                    }
                                }
                            },
                            new WaveConfig
                            {
                                Level = 2,
                                TimeToStart = 20,
                                Duration = 120,
                                TimerAnimal = 10,
                                Presets = new HashSet<PresetConfig>
                                {
                                    new PresetConfig
                                    {
                                        ShortName = "wolf1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 2 } }
                                    },
                                    new PresetConfig
                                    {
                                        ShortName = "boar1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 3 } }
                                    }
                                }
                            },
                            new WaveConfig
                            {
                                Level = 3,
                                TimeToStart = 20,
                                Duration = 180,
                                TimerAnimal = 10,
                                Presets = new HashSet<PresetConfig>
                                {
                                    new PresetConfig
                                    {
                                        ShortName = "bear1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 3 } }
                                    },
                                    new PresetConfig
                                    {
                                        ShortName = "stag1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 2 } }
                                    }
                                }
                            }
                        }
                    },
                    SecondReceiver = new ReceiverConfig
                    {
                        Health = 3000f,
                        HealthPerBattery = 100f,
                        Loot = new LootTable
                        {
                            TypeLootTable = 0,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1,
                                Max = 1,
                                UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 3,
                                Max = 3,
                                UseCount = true,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig { ShortName = "ammo.rifle", MinAmount = 3000, MaxAmount = 3000, Chance = 100f, IsBluePrint = false, SkinId = 0, Name = "" },
                                    new ItemConfig { ShortName = "syringe.medical", MinAmount = 15, MaxAmount = 15, Chance = 100f, IsBluePrint = false, SkinId = 0, Name = "" },
                                    new ItemConfig { ShortName = "largemedkit", MinAmount = 9, MaxAmount = 9, Chance = 100f, IsBluePrint = false, SkinId = 0, Name = "" }
                                }
                            }
                        },
                        Waves = new HashSet<WaveConfig>
                        {
                            new WaveConfig
                            {
                                Level = 1,
                                TimeToStart = 1,
                                Duration = 30,
                                TimerAnimal = 10,
                                Presets = new HashSet<PresetConfig>
                                {
                                    new PresetConfig
                                    {
                                        ShortName = "bear1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 3 } }
                                    },
                                    new PresetConfig
                                    {
                                        ShortName = "wolf1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 3 } }
                                    },
                                    new PresetConfig
                                    {
                                        ShortName = "chicken1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 2 } }
                                    },
                                }
                            },
                            new WaveConfig
                            {
                                Level = 2,
                                TimeToStart = 20,
                                Duration = 30,
                                TimerAnimal = 10,
                                Presets = new HashSet<PresetConfig>
                                {
                                    new PresetConfig
                                    {
                                        ShortName = "wolf1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 3 } }
                                    },
                                    new PresetConfig
                                    {
                                        ShortName = "boar1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 3 } }
                                    },
                                    new PresetConfig
                                    {
                                        ShortName = "stag1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 2 } }
                                    },
                                }
                            },
                            new WaveConfig
                            {
                                Level = 3,
                                TimeToStart = 20,
                                Duration = 60,
                                TimerAnimal = 10,
                                Presets = new HashSet<PresetConfig>
                                {
                                    new PresetConfig
                                    {
                                        ShortName = "chicken1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 10 } }
                                    }
                                }
                            },
                            new WaveConfig
                            {
                                Level = 4,
                                TimeToStart = 30,
                                Duration = 60,
                                TimerAnimal = 10,
                                Presets = new HashSet<PresetConfig>
                                {
                                    new PresetConfig
                                    {
                                        ShortName = "polarbear1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 2 } }
                                    },
                                    new PresetConfig
                                    {
                                        ShortName = "bear1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 2 } }
                                    },
                                    new PresetConfig
                                    {
                                        ShortName = "wolf1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 2 } }
                                    },
                                }
                            },
                            new WaveConfig
                            {
                                Level = 5,
                                TimeToStart = 20,
                                Duration = 90,
                                TimerAnimal = 10,
                                Presets = new HashSet<PresetConfig>
                                {
                                    new PresetConfig
                                    {
                                        ShortName = "wolf1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 2 } }
                                    },
                                    new PresetConfig
                                    {
                                        ShortName = "stag1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 3 } }
                                    },
                                    new PresetConfig
                                    {
                                        ShortName = "chicken1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 3 } }
                                    },
                                }
                            },
                            new WaveConfig
                            {
                                Level = 6,
                                TimeToStart = 20,
                                Duration = 90,
                                TimerAnimal = 10,
                                Presets = new HashSet<PresetConfig>
                                {
                                    new PresetConfig
                                    {
                                        ShortName = "polarbear1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 9 } }
                                    }
                                }
                            }
                        }
                    },
                    ThirdReceiver = new ReceiverConfig
                    {
                        Health = 4000f,
                        HealthPerBattery = 100f,
                        Loot = new LootTable
                        {
                            TypeLootTable = 0,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1,
                                Max = 1,
                                UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 3,
                                Max = 3,
                                UseCount = true,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig { ShortName = "ammo.rifle", MinAmount = 4500, MaxAmount = 4500, Chance = 100f, IsBluePrint = false, SkinId = 0, Name = "" },
                                    new ItemConfig { ShortName = "syringe.medical", MinAmount = 22, MaxAmount = 23, Chance = 100f, IsBluePrint = false, SkinId = 0, Name = "" },
                                    new ItemConfig { ShortName = "largemedkit", MinAmount = 13, MaxAmount = 14, Chance = 100f, IsBluePrint = false, SkinId = 0, Name = "" }
                                }
                            }
                        },
                        Waves = new HashSet<WaveConfig>
                        {
                            new WaveConfig
                            {
                                Level = 1,
                                TimeToStart = 1,
                                Duration = 40,
                                TimerAnimal = 10,
                                Presets = new HashSet<PresetConfig>
                                {
                                    new PresetConfig
                                    {
                                        ShortName = "polarbear1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 1 } }
                                    },
                                    new PresetConfig
                                    {
                                        ShortName = "bear1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 2 } }
                                    },
                                    new PresetConfig
                                    {
                                        ShortName = "wolf1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 2 } }
                                    },
                                    new PresetConfig
                                    {
                                        ShortName = "stag1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 2 } }
                                    }
                                }
                            },
                            new WaveConfig
                            {
                                Level = 2,
                                TimeToStart = 20,
                                Duration = 40,
                                TimerAnimal = 10,
                                Presets = new HashSet<PresetConfig>
                                {
                                    new PresetConfig
                                    {
                                        ShortName = "bear1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 2 } }
                                    },
                                    new PresetConfig
                                    {
                                        ShortName = "boar1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 3 } }
                                    },
                                    new PresetConfig
                                    {
                                        ShortName = "stag1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 2 } }
                                    },
                                    new PresetConfig
                                    {
                                        ShortName = "chicken1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 3 } }
                                    },
                                }
                            },
                            new WaveConfig
                            {
                                Level = 3,
                                TimeToStart = 20,
                                Duration = 40,
                                TimerAnimal = 10,
                                Presets = new HashSet<PresetConfig>
                                {
                                    new PresetConfig
                                    {
                                        ShortName = "stag1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 10 } }
                                    }
                                }
                            },
                            new WaveConfig
                            {
                                Level = 4,
                                TimeToStart = 30,
                                Duration = 70,
                                TimerAnimal = 10,
                                Presets = new HashSet<PresetConfig>
                                {
                                    new PresetConfig
                                    {
                                        ShortName = "polarbear1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 2 } }
                                    },
                                    new PresetConfig
                                    {
                                        ShortName = "wolf1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 2 } }
                                    },
                                    new PresetConfig
                                    {
                                        ShortName = "boar1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 2 } }
                                    },
                                    new PresetConfig
                                    {
                                        ShortName = "chicken1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 1 } }
                                    }
                                }
                            },
                            new WaveConfig
                            {
                                Level = 5,
                                TimeToStart = 20,
                                Duration = 70,
                                TimerAnimal = 10,
                                Presets = new HashSet<PresetConfig>
                                {
                                    new PresetConfig
                                    {
                                        ShortName = "polarbear1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 2 } }
                                    },
                                    new PresetConfig
                                    {
                                        ShortName = "bear1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 2 } }
                                    },
                                    new PresetConfig
                                    {
                                        ShortName = "wolf1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 2 } }
                                    },
                                    new PresetConfig
                                    {
                                        ShortName = "stag1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 2 } }
                                    }
                                }
                            },
                            new WaveConfig
                            {
                                Level = 6,
                                TimeToStart = 20,
                                Duration = 70,
                                TimerAnimal = 10,
                                Presets = new HashSet<PresetConfig>
                                {
                                    new PresetConfig
                                    {
                                        ShortName = "wolf1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 10 } }
                                    }
                                }
                            },
                            new WaveConfig
                            {
                                Level = 7,
                                TimeToStart = 20,
                                Duration = 100,
                                TimerAnimal = 10,
                                Presets = new HashSet<PresetConfig>
                                {
                                    new PresetConfig
                                    {
                                        ShortName = "bear1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 2 } }
                                    },
                                    new PresetConfig
                                    {
                                        ShortName = "boar1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 3 } }
                                    },
                                    new PresetConfig
                                    {
                                        ShortName = "stag1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 2 } }
                                    },
                                    new PresetConfig
                                    {
                                        ShortName = "chicken1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 3 } }
                                    }
                                }
                            },
                            new WaveConfig
                            {
                                Level = 8,
                                TimeToStart = 20,
                                Duration = 100,
                                TimerAnimal = 10,
                                Presets = new HashSet<PresetConfig>
                                {
                                    new PresetConfig
                                    {
                                        ShortName = "polarbear1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 2 } }
                                    },
                                    new PresetConfig
                                    {
                                        ShortName = "wolf1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 2 } }
                                    },
                                    new PresetConfig
                                    {
                                        ShortName = "boar1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 2 } }
                                    },
                                    new PresetConfig
                                    {
                                        ShortName = "chicken1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 1 } }
                                    }
                                }
                            },
                            new WaveConfig
                            {
                                Level = 9,
                                TimeToStart = 20,
                                Duration = 100,
                                TimerAnimal = 10,
                                Presets = new HashSet<PresetConfig>
                                {
                                    new PresetConfig
                                    {
                                        ShortName = "boar1",
                                        ListAmountConfig = new HashSet<AmountConfig> { new AmountConfig { Chance = 100f, Count = 10 } }
                                    }
                                }
                            }
                        }
                    },
                    Drill = new DrillConfig
                    {
                        WorkingTimePerBattery = 30,
                        MaxWorkingTime = 3600,
                        LifeTime = 4200,
                        CycleTime = 15,
                        Loot = new LootTable
                        {
                            TypeLootTable = 0,
                            PrefabLootTable = new PrefabLootTableConfig
                            {
                                Min = 1,
                                Max = 1,
                                UseCount = true,
                                Prefabs = new List<PrefabConfig> { new PrefabConfig { Chance = 100f, PrefabDefinition = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab" } }
                            },
                            OwnLootTable = new LootTableConfig
                            {
                                Min = 5,
                                Max = 5,
                                UseCount = true,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig { ShortName = "stones", MinAmount = 1000, MaxAmount = 1000, Chance = 100f, IsBluePrint = false, SkinId = 0, Name = "" },
                                    new ItemConfig { ShortName = "scrap", MinAmount = 10, MaxAmount = 20, Chance = 100f, IsBluePrint = false, SkinId = 0, Name = "" },
                                    new ItemConfig { ShortName = "metal.fragments", MinAmount = 1000, MaxAmount = 1000, Chance = 100f, IsBluePrint = false, SkinId = 0, Name = "" },
                                    new ItemConfig { ShortName = "sulfur", MinAmount = 1000, MaxAmount = 1000, Chance = 100f, IsBluePrint = false, SkinId = 0, Name = "" },
                                    new ItemConfig { ShortName = "metal.refined", MinAmount = 10, MaxAmount = 12, Chance = 100f, IsBluePrint = false, SkinId = 0, Name = "" }
                                }
                            }
                        }
                    },
                    Marker = new MarkerConfig
                    {
                        Enabled = true,
                        Type = 1,
                        Radius = 0.37967f,
                        Alpha = 0.35f,
                        Color = new ColorConfig { R = 0.81f, G = 0.25f, B = 0.15f },
                        Text = "Triangulation"
                    },
                    Point = new PointConfig
                    {
                        Enabled = true,
                        Text = "◈",
                        Size = 45,
                        Color = "#CCFF00"
                    },
                    Gui = new GuiConfig
                    {
                        IsGui = true,
                        OffsetMinY = "-56"
                    },
                    Chat = new ChatConfig
                    {
                        IsChat = true,
                        Prefix = "[Triangulation]"
                    },
                    GameTip = new GameTipConfig
                    {
                        IsGameTip = false,
                        Style = 2
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
                        Type = 0
                    },
                    Discord = new DiscordConfig
                    {
                        IsDiscord = false,
                        WebhookUrl = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks",
                        EmbedColor = 13516583,
                        Keys = new HashSet<string>
                        {
                            "PreStart",
                            "ReceiverSpawn",
                            "DrillSpawn",
                            "NotStartScan",
                            "AllStartScan",
                            "ReceiverKill",
                            "FinishScan"
                        }
                    },
                    IsCreateZonePvp = false,
                    LootCrate = false,
                    DamageNpc = false,
                    TargetNpc = false,
                    SwitchDrill = false,
                    OwnerTime = 600f,
                    RestoreUponDeath = true,
                    CooldownOwner = 86400,
                    NTeleportationInterrupt = true,
                    Economy = new EconomyConfig
                    {
                        Plugins = new HashSet<string> { "Economics", "Server Rewards", "IQEconomic", "XPerience" },
                        Min = 0,
                        Animals = new Dictionary<string, double>
                        {
                            ["polarbear1"] = 0.4,
                            ["bear1"] = 0.3,
                            ["wolf1"] = 0.4,
                            ["boar1"] = 0.2,
                            ["stag1"] = 0.3,
                            ["chicken1"] = 0.1
                        },
                        StartScan = 1.0,
                        Commands = new HashSet<string>()
                    },
                    Commands = new HashSet<string>
                    {
                        "/remove",
                        "remove.toggle"
                    },
                    Spawn = new SpawnConfig
                    {
                        Type = 0,
                        Biomes = new HashSet<string> { "Arctic" },
                        Monuments = new HashSet<MonumentConfig>
                        {
                            new MonumentConfig
                            {
                                Name = "Military Tunnel",
                                Locations = new HashSet<LocationConfig>
                                {
                                    new LocationConfig
                                    {
                                        Main = "(1.5, 29.963, -34)",
                                        Animals = new HashSet<string>
                                        {
                                            "(26.626, 24.550, -8.874)",
                                            "(40.461, 25.389, -14.603)",
                                            "(37.034, 27.779, -34)",
                                            "(33.425, 27.198, -51.254)",
                                            "(25.794, 26.374, -58.150)",
                                            "(9.84, 30.072, -59.066)",
                                            "(-18.072, 29.995, -53.572)",
                                            "(-34.034, 29.966, -34)",
                                            "(-13.738, 29.657, -18.762)",
                                            "(1.5, 18.506, 1.534)"
                                        }
                                    }
                                }
                            },
                            new MonumentConfig
                            {
                                Name = "Airfield",
                                Locations = new HashSet<LocationConfig>
                                {
                                    new LocationConfig
                                    {
                                        Main = "(135, 0.26, -38)",
                                        Animals = new HashSet<string>
                                        {
                                            "(154.56, 0, -80.58)",
                                            "(135, 0.22, -78)",
                                            "(112.914, 0.085, -71.35)",
                                            "(98.554, 0.252, -54.484)",
                                            "(95, 0, -38)",
                                            "(96.032, 0, -12.194)",
                                            "(118.516, 0.212, -1.554)",
                                            "(135, 0.319, 2)",
                                            "(155.91, 0.368, -6.426)",
                                            "(166.023, 0, -23.969)",
                                            "(169.049, 0, -38)",
                                            "(166.574, 0.102, -58.91)"
                                        }
                                    },
                                    new LocationConfig
                                    {
                                        Main = "(-121, 0.3, -27)",
                                        Animals = new HashSet<string>
                                        {
                                            "(-161, 0.3, -27)",
                                            "(-155.325, 0.3, -47.539)",
                                            "(-138.319, 0.3, -63.056)",
                                            "(-121.000, 0.3, -67.000)",
                                            "(-100.462, 0.3, -61.324)",
                                            "(-84.944, 0.3, -44.319)",
                                            "(-81.000, 0.3, -27.000)",
                                            "(-86.676, 0.3, -6.461)",
                                            "(-103.681, 0.3, 9.056)",
                                            "(-121.000, 0.3, 13.000)",
                                            "(-141.539, 0.3, 7.324)",
                                            "(-157.056, 0.3, -9.681)"
                                        }
                                    }
                                }
                            },
                            new MonumentConfig
                            {
                                Name = "Sphere Tank",
                                Locations = new HashSet<LocationConfig>
                                {
                                    new LocationConfig
                                    {
                                        Main = "(4.813, 0.462, 2.061)",
                                        Animals = new HashSet<string>
                                        {
                                            "(-24.427, 5.677, 25.324)",
                                            "(-14.481, 5.677, 31.681)",
                                            "(15.056, 5.856, 28.640)",
                                            "(0.891, 5.677, 35.887)",
                                            "(32.997, 5.677, 13.635)",
                                            "(26.438, 5.825, 20.317)",
                                            "(34.179, 5.702, -1.445)",
                                            "(33.521, 5.677, -13.208)",
                                            "(24.288, 5.677, -24.948)",
                                            "(14.729, 5.648, -31.778)",
                                            "(-0.518, 5.677, -35.668)",
                                            "(-15.739, 5.641, -31.685)"
                                        }
                                    }
                                }
                            },
                            new MonumentConfig
                            {
                                Name = "Trainyard",
                                Locations = new HashSet<LocationConfig>
                                {
                                    new LocationConfig
                                    {
                                        Main = "(-70.7, 0.83, -0.33)",
                                        Animals = new HashSet<string>
                                        {
                                            "(-26.273, 0.453, -6.173)",
                                            "(-32.624, 0.182, -18.188)",
                                            "(-85.816, 0.976, -25.328)",
                                            "(-97.834, 0.129, -24.935)",
                                            "(-107.601, 0.397, 0.763)",
                                            "(-88.713, 0.244, 39.929)",
                                            "(-54.796, -0.742, 38.167)",
                                            "(-44.756, 0.138, 15.142)",
                                            "(-95.127, 0.128, 28.683)",
                                            "(-54.597, 8.271, -36.836)"
                                        }
                                    }
                                }
                            },
                            new MonumentConfig
                            {
                                Name = "Water Treatment",
                                Locations = new HashSet<LocationConfig>
                                {
                                    new LocationConfig
                                    {
                                        Main = "(-35.333, 0.021, 17.454)",
                                        Animals = new HashSet<string>
                                        {
                                            "(0.949, 0.015, 30.325)",
                                            "(-6.420, 0.244, 1.618)",
                                            "(-38.458, 0.244, -4.472)",
                                            "(-59.316, 0.261, -6.377)",
                                            "(-67.341, 0.262, 1.824)",
                                            "(-60.945, 0.157, 49.432)",
                                            "(-38.378, 0, 42.507)",
                                            "(-31.917, 0, 42.780)",
                                            "(-15.744, 0.244, -17.636)",
                                            "(3.843, 0.343, 13.263)",
                                            "(-75.581, 0.332, 29.448)"
                                        }
                                    },
                                    new LocationConfig
                                    {
                                        Main = "(-6.978, 0.285, -124.317)",
                                        Animals = new HashSet<string>
                                        {
                                            "(15.277, 0.136, -132.472)",
                                            "(7.293, 0.244, -158.747)",
                                            "(-10.147, 0.244, -158.238)",
                                            "(-32.573, 0.130, -153.432)",
                                            "(-34.257, 0.318, -135.533)",
                                            "(-38.466, 0.214, -115.041)",
                                            "(-32.005, 0.124, -97.305)",
                                            "(-15.148, 0.244, -99.436)",
                                            "(3.083, 0.244, -95.223)",
                                            "(19.339, 0, -108.883)"
                                        }
                                    }
                                }
                            }
                        },
                        CustomMonuments = new HashSet<LocationConfig>()
                    },
                    SkillTree = false,
                    XPerience = false,
                    RustRewards = false,
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
                ["PreStart"] = "{0} While exploring the arctic area on the island, <color=#55aaff>scientists were attacked</color> by aggressive animals. The scientists <color=#55aaff>evacuated</color> the island and <color=#55aaff>abandoned the drilling rig</color>. It <color=#55aaff>extracts a lot of resources</color>, but it <color=#55aaff>runs on batteries</color> and its <color=#55aaff>location is unknown</color>. After <color=#55aaff>{1}</color> our specialists will place 3 signal receivers. It is necessary to <color=#55aaff>scan</color> each receiver and <color=#55aaff>loot the batteries</color> in crates after scanning. Next, using the triangulation of the signal, we <color=#55aaff>will get the location of the drilling rig</color> and you can use it. The operating time of the drilling rig depends on the number of batteries",
                ["ReceiverSpawn"] = "{0} The <color=#55aaff>receiver of the signal {1}</color> is located in map grid <color=#55aaff>{2}</color>. You have <color=#55aaff>{3}</color> to find the receiver and start scanning on it",
                ["DrillSpawn"] = "{0} The <color=#55aaff>drilling rig</color> <color=#738d43>was found</color> in map grid <color=#55aaff>{1}</color>. You have <color=#55aaff>{2}</color> to use it",
                ["NotStartScan"] = "{0} <color=#ce3f27>No one has started scanning</color> on <color=#55aaff>signal receiver {1}</color>. <color=#55aaff>Drilling rig</color> <color=#ce3f27>not found</color>",
                ["AllStartScan"] = "{0} <color=#55aaff>{1}</color> <color=#738d43>started scanning</color> the signal on <color=#55aaff>receiver {2}</color>",
                ["PlayerStartScan"] = "{0} You <color=#738d43>have started scanning</color> the signal on  <color=#55aaff>receiver {1}</color>. It seems that the <color=#ce3f27>animals are furious</color> with the receiver and they are running to destroy it. You <color=#55aaff>need to protect</color> it until the end of the scan, otherwise you will not be able to find the location of the drilling rig",
                ["ReceiverKill"] = "{0} <color=#55aaff>Signal receiver {1}</color> <color=#ce3f27>has been destroyed</color>. <color=#55aaff>Drilling rig</color> <color=#ce3f27>not found</color>",
                ["FinishScan"] = "{0} The scan on <color=#55aaff>signal receiver {1}</color> <color=#738d43>has been completed</color> successfully. Don't forget to <color=#738d43>loot</color> the <color=#55aaff>batteries</color> in crate!",
                ["NoOwnerCooldown"] = "{0} You <color=#ce3f27>cannot</color> start scanning on this signal receiver because you need to wait another <color=#55aaff>{1}</color> before becoming the owner of the event",
                ["NoOwnerStartScan"] = "{0} You <color=#ce3f27>cannot</color> start scanning on this signal receiver because you <color=#ce3f27>don't own</color> it or you're not on the same team as the owner",
                ["NoOwnerSwitchDrill"] = "{0} You <color=#ce3f27>cannot</color> start a drilling rig because you <color=#ce3f27>don't own</color> it or you're not on the same team as the owner",
                ["NoSwitchDrillMinHealth"] = "{0} You <color=#ce3f27>cannot</color> start the drilling rig because its strength is too worn down",
                ["NoSwitchDrillNoBattery"] = "{0} You <color=#ce3f27>cannot</color> start the drilling rig because there aren't enough batteries to run it",
                ["EventActive"] = "{0} This event is active now. To finish this event (<color=#55aaff>/tstop</color>), then (<color=#55aaff>/tstart</color>) to start the next one!",
                ["EnterPVP"] = "{0} You <color=#ce3f27>have entered</color> the PVP zone, now other players <color=#ce3f27>can damage</color> you!",
                ["ExitPVP"] = "{0} You <color=#738d43>have left</color> the PVP zone, now other players <color=#738d43>cannot damage</color> you!",
                ["NTeleportation"] = "{0} You <color=#ce3f27>cannot</color> teleport into the event zone!",
                ["SendEconomy"] = "{0} You <color=#738d43>have earned</color> <color=#55aaff>{1}</color> for participating in the event",
                ["NoCommand"] = "{0} You <color=#ce3f27>cannot</color> use this command in the event zone!"
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PreStart"] = "{0} При исследования зимней части острова <color=#55aaff>ученых атаковали</color> агрессивные животные. Ученые <color=#55aaff>эвакуировались</color> с острова и <color=#55aaff>бросили бурильную установку</color>. Она <color=#55aaff>добывает много ресурсов</color>, но <color=#55aaff>работает от батареек</color> и ее <color=#55aaff>расположение неизвестно</color>. Через <color=#55aaff>{1}</color> наш специалист разместит 3 приемника сигнала. Необходимо <color=#55aaff>произвести сканирование</color> на каждом приемнике и <color=#55aaff>забрать</color> из ящика <color=#55aaff>батарейки</color> после сканирования. Далее при помощи триангуляции сигнала мы <color=#55aaff>получим расположение бурильной установки</color> и вы сможете ей воспользоваться. Время работы бурильной установки зависит от кол-ва батареек",
                ["ReceiverSpawn"] = "{0} <color=#55aaff>Приемник сигнала {1}</color> находится в квадрате <color=#55aaff>{2}</color>. У вас есть <color=#55aaff>{3}</color>, чтобы найти приемник и начать сканирование на нем",
                ["DrillSpawn"] = "{0} <color=#55aaff>Буровая установка</color> <color=#738d43>найдена</color> в квадрате <color=#55aaff>{1}</color>. У вас есть <color=#55aaff>{2}</color>, чтобы воспользоваться ей",
                ["NotStartScan"] = "{0} Никто <color=#ce3f27>не начал сканирование</color> на <color=#55aaff>приемнике сигнала {1}</color>. <color=#55aaff>Бурильная установка</color> <color=#ce3f27>не найдена</color>",
                ["AllStartScan"] = "{0} <color=#55aaff>{1}</color> <color=#738d43>начал сканирование</color> сигнала на <color=#55aaff>приемнике {2}</color>",
                ["PlayerStartScan"] = "{0} Вы <color=#738d43>начали сканирование</color> сигнала на <color=#55aaff>приемнике {1}</color>. Похоже что <color=#ce3f27>животные в бешенстве</color> от работы приемника и они бегут уничтожить его. Вам <color=#55aaff>необходимо защищать</color> его до окончания сканирования, иначе не получится найти расположение бурильной установки",
                ["ReceiverKill"] = "{0} <color=#55aaff>Приемник сигнала {1}</color> <color=#ce3f27>уничтожен</color>. <color=#55aaff>Бурильная установка</color> <color=#ce3f27>не найдена</color>",
                ["FinishScan"] = "{0} Сканирование на <color=#55aaff>приемнике сигнала {1}</color> успешно <color=#738d43>завершено</color>. Не забудьте <color=#738d43>забрать</color> <color=#55aaff>батарейки</color> из ящика!",
                ["NoOwnerCooldown"] = "{0} Вы <color=#ce3f27>не можете</color> начать сканирование на этом приемнике сигнала, потому что вам необходимо подождать еще <color=#55aaff>{1}</color>, чтобы вы могли стать владельцем ивента",
                ["NoOwnerStartScan"] = "{0} Вы <color=#ce3f27>не можете</color> начать сканирование на этом приемнике сигнала, потому что <color=#ce3f27>не являетесь</color> его <color=#ce3f27>владельцем</color> или не находитесь в одной команде с владельцем",
                ["NoOwnerSwitchDrill"] = "{0} Вы <color=#ce3f27>не можете</color> запустить буровую установку, потому что <color=#ce3f27>не являетесь</color> ее <color=#ce3f27>владельцем</color> или не находитесь в одной команде с владельцем",
                ["NoSwitchDrillMinHealth"] = "{0} Вы <color=#ce3f27>не можете</color> запустить буровую установку, потому что ее прочность достигла минимума",
                ["NoSwitchDrillNoBattery"] = "{0} Вы <color=#ce3f27>не можете</color> запустить буровую установку, потому что недостаточно батареек для ее работы",
                ["EventActive"] = "{0} Ивент в данный момент активен, сначала завершите текущий ивент (<color=#55aaff>/tstop</color>), чтобы начать следующий!",
                ["EnterPVP"] = "{0} Вы <color=#ce3f27>вошли</color> в PVP зону, теперь другие игроки <color=#ce3f27>могут</color> наносить вам урон!",
                ["ExitPVP"] = "{0} Вы <color=#738d43>вышли</color> из PVP зоны, теперь другие игроки <color=#738d43>не могут</color> наносить вам урон!",
                ["NTeleportation"] = "{0} Вы <color=#ce3f27>не можете</color> телепортироваться в зоне ивента!",
                ["SendEconomy"] = "{0} Вы <color=#738d43>получили</color> <color=#55aaff>{1}</color> баллов в экономику за прохождение ивента",
                ["NoCommand"] = "{0} Вы <color=#ce3f27>не можете</color> использовать данную команду в зоне ивента!"
            }, this, "ru");
        }

        private string GetMessage(string langKey, string userId) => lang.GetMessage(langKey, _ins, userId);

        private string GetMessage(string langKey, string userId, params object[] args) => (args.Length == 0) ? GetMessage(langKey, userId) : string.Format(GetMessage(langKey, userId), args);
        #endregion Lang

        #region Data
        private Dictionary<ulong, double> PlayersData { get; set; } = null;

        private void LoadData() => PlayersData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, double>>(Name);

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, PlayersData);

        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0);

        private static double CurrentTime => DateTime.UtcNow.Subtract(Epoch).TotalSeconds;

        private bool CanTimeOwner(ulong steamId)
        {
            double time;
            if (PlayersData.TryGetValue(steamId, out time)) return time + _config.CooldownOwner < CurrentTime;
            else return true;
        }

        private double GetTimeOwner(ulong steamId)
        {
            double time;
            if (PlayersData.TryGetValue(steamId, out time)) return time + _config.CooldownOwner - CurrentTime;
            else return 0;
        }
        #endregion Data

        #region Oxide Hooks
        private static Triangulation _ins;

        private void Init()
        {
            _ins = this;
            ToggleHooks(false);
        }

        private void OnServerInitialized()
        {
            Monuments = TerrainMeta.Path.Monuments.Where(IsNecessaryMonument);
            LoadData();
            CheckAllLootTables();
            ServerMgr.Instance.StartCoroutine(DownloadImages());
            StartTimer();
        }

        private void Unload()
        {
            if (Controller != null) Finish();
            _ins = null;
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return null;
            if (Controller.Receiver != null)
            {
                if (Controller.Receiver.Entities.Contains(entity)) return true;
                if (entity is BaseAnimalNPC)
                {
                    BaseAnimalNPC animal = entity as BaseAnimalNPC;
                    if (Controller.Receiver.Animals.Contains(animal))
                    {
                        BaseEntity weaponPrefab = info.WeaponPrefab;
                        if (weaponPrefab != null && WeaponsBlockedDamage.Contains(weaponPrefab.ShortPrefabName)) return true;
                        if (weaponPrefab == null && info.damageTypes.GetMajorityDamageType() == Rust.DamageType.Heat) return true;
                        BasePlayer attacker = info.InitiatorPlayer;
                        if (attacker.IsPlayer() && !_config.DamageNpc && !IsTeam(attacker.userID, Controller.Owner.userID)) return true;
                    }
                }
            }
            if (Controller.Drill != null && Controller.Drill.Entities.Contains(entity)) return true;
            return null;
        }

        private object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            if (planner == null) return null;
            BasePlayer player = planner.GetOwnerPlayer();
            if (player == null) return null;
            if (Controller.Receiver != null && Controller.Receiver.Players.Contains(player)) return false;
            if (Controller.Drill != null && Controller.Drill.Players.Contains(player)) return false;
            return null;
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (!_config.Marker.Enabled || Controller == null || !player.IsPlayer()) return;
            if (player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot)) timer.In(2f, () => OnPlayerConnected(player));
            else
            {
                if (Controller.Receiver != null) Controller.Receiver.UpdateMapMarkers();
                if (Controller.Drill != null) Controller.Drill.UpdateMapMarkers();
            }
        }

        private object OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (!player.IsPlayer()) return null;
            if (Controller.Receiver != null && Controller.Receiver.Players.Contains(player)) Controller.Receiver.ExitPlayer(player);
            if (Controller.Drill != null && Controller.Drill.Players.Contains(player)) Controller.Drill.ExitPlayer(player);
            return null;
        }

        private object OnItemPickup(Item item, BasePlayer player)
        {
            if (!player.IsPlayer() || item == null || item.info.shortname != "targeting.computer") return null;
            if (Controller.Receiver != null) return Controller.Receiver.InteractLaptop(player, item);
            if (Controller.Drill != null) return Controller.Drill.InteractLaptop(player, item);
            return null;
        }

        private object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (!player.IsPlayer() || container == null || _config.LootCrate) return null;
            if (Controller.Receiver != null && container == Controller.Receiver.Crate && Controller.Owner != null && !IsTeam(player.userID, Controller.Owner.userID)) return true;
            if (Controller.Drill != null && (container == Controller.Drill.InputCrate || container == Controller.Drill.OutputCrate) && Controller.Owner != null && !IsTeam(player.userID, Controller.Owner.userID)) return true;
            return null;
        }

        private object OnPlayerCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsPlayer()) return null;
            if ((Controller.Receiver != null && Controller.Receiver.Players.Contains(player)) || (Controller.Drill != null && Controller.Drill.Players.Contains(player)))
            {
                command = "/" + command;
                if (_config.Commands.Contains(command.ToLower()))
                {
                    AlertToPlayer(player, GetMessage("NoCommand", player.UserIDString, _config.Chat.Prefix));
                    return true;
                }
            }
            return null;
        }

        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (arg == null || arg.cmd == null) return null;
            BasePlayer player = arg.Player();
            if (!player.IsPlayer()) return null;
            if ((Controller.Receiver != null && Controller.Receiver.Players.Contains(player)) || (Controller.Drill != null && Controller.Drill.Players.Contains(player)))
            {
                if (_config.Commands.Contains(arg.cmd.Name.ToLower()) || _config.Commands.Contains(arg.cmd.FullName.ToLower()))
                {
                    AlertToPlayer(player, GetMessage("NoCommand", player.UserIDString, _config.Chat.Prefix));
                    return true;
                }
            }
            return null;
        }
        #endregion Oxide Hooks

        #region Find Positions
        public class Location { public Vector3 Main; public HashSet<Vector3> Animals; }
        internal List<Location> Positions { get; } = new List<Location>();

        private void FindAllPositions()
        {
            Positions.Clear();

            if (_config.Spawn.Type == 0 || _config.Spawn.Type == 4)
            {
                for (int i = 0; i < 29; i++)
                {
                    Location location = GetLocation();
                    if (location != null) Positions.Add(location);
                }
            }

            if (_config.Spawn.Type == 2 || _config.Spawn.Type == 3 || (_config.Spawn.Type == 4 && Positions.Count < 8))
            {
                foreach (LocationConfig location in _config.Spawn.CustomMonuments)
                {
                    Vector3 main = location.Main.ToVector3();
                    if (!IsValidBiome(main)) continue;
                    Positions.Add(new Location { Main = main, Animals = location.Animals.Select(x => x.ToVector3()) });
                }
            }

            if (_config.Spawn.Type == 1 || _config.Spawn.Type == 3 || (_config.Spawn.Type == 4 && Positions.Count < 8))
            {
                foreach (MonumentInfo monument in Monuments)
                {
                    MonumentConfig config = _config.Spawn.Monuments.FirstOrDefault(x => x.Name == GetNameMonument(monument));
                    if (config == null) continue;
                    foreach (LocationConfig location in config.Locations)
                    {
                        Vector3 main = monument.transform.TransformPoint(location.Main.ToVector3());
                        if (!IsValidBiome(main)) continue;
                        Positions.Add(new Location { Main = main, Animals = location.Animals.Select(x => monument.transform.TransformPoint(x.ToVector3())) });
                    }
                }
            }
        }

        private Location GetLocation()
        {
            RaycastHit raycastHit;
            NavMeshHit navMeshHit;

            int attempts = 0;

            while (attempts < 10000)
            {
                attempts++;

                Vector2 random = World.Size * 0.475f * UnityEngine.Random.insideUnitCircle;
                Vector3 center = new Vector3(random.x, 500f, random.y);

                if (!IsValidBiome(center) || !IsAvailableTopology(center, BlockedTopologyMain)) continue;

                if (IsRaycast(center, out raycastHit)) center.y = raycastHit.point.y;
                else continue;

                if (IsNavMesh(center, out navMeshHit)) center = navMeshHit.position;
                else continue;

                if (Math.Abs(center.y - TerrainMeta.HeightMap.GetHeight(center)) > 1f) continue;

                if (IsEntities(center, LargeReceiverRadius, EntityLayers)) continue;

                if (Positions.Any(x => Vector3.Distance(x.Main, center) < SimilarReceiverRadius)) continue;

                bool isContinue = false;
                for (int i = 1; i <= 12; i++)
                {
                    Vector3 pos = new Vector3(center.x + SmallReceiverRadius * Mathf.Sin(i * 30f * Mathf.Deg2Rad), 500f, center.z + SmallReceiverRadius * Mathf.Cos(i * 30f * Mathf.Deg2Rad));

                    if (!IsValidBiome(pos) || !IsAvailableTopology(pos, BlockedTopologyMain))
                    {
                        isContinue = true;
                        break;
                    }

                    if (IsRaycast(pos, out raycastHit)) pos.y = raycastHit.point.y;
                    else
                    {
                        isContinue = true;
                        break;
                    }

                    if (IsNavMesh(pos, out navMeshHit)) pos = navMeshHit.position;
                    else
                    {
                        isContinue = true;
                        break;
                    }

                    if (Math.Abs(center.y - pos.y) > 0.3f)
                    {
                        isContinue = true;
                        break;
                    }
                }
                if (isContinue) continue;

                HashSet<Vector3> list = new HashSet<Vector3>();
                for (int i = 1; i <= 12; i++)
                {
                    int threshold = 0;

                    for (float k = 0.65f; k <= 1f; k += 0.07f)
                    {
                        Vector3 pos = new Vector3(center.x + LargeReceiverRadius * k * Mathf.Sin(i * 30f * Mathf.Deg2Rad), 500f, center.z + LargeReceiverRadius * k * Mathf.Cos(i * 30f * Mathf.Deg2Rad));

                        if (!IsAvailableTopology(pos, BlockedTopologyAnimal)) continue;

                        if (IsRaycast(pos, out raycastHit)) pos.y = raycastHit.point.y;
                        else continue;

                        if (IsNavMesh(pos, out navMeshHit)) pos = navMeshHit.position;
                        else continue;

                        if (Math.Abs(pos.y - TerrainMeta.HeightMap.GetHeight(pos)) > 1f) continue;

                        threshold++;
                        list.Add(pos);
                    }

                    if (threshold < 2)
                    {
                        isContinue = true;
                        break;
                    }
                }
                if (isContinue) continue;

                if (list.Count < 10) continue;

                return new Location { Main = center, Animals = list };
            }

            return null;
        }

        private bool IsValidBiome(Vector3 position)
        {
            TerrainBiome.Enum biome = (TerrainBiome.Enum)TerrainMeta.BiomeMap.GetBiomeMaxType(position);
            return _config.Spawn.Biomes.Any(x => biome == (TerrainBiome.Enum)Enum.Parse(typeof(TerrainBiome.Enum), x, true));
        }

        private const int BlockedTopologyMain = (int)(TerrainTopology.Enum.Cliff | TerrainTopology.Enum.Cliffside | TerrainTopology.Enum.Beach | TerrainTopology.Enum.Beachside | TerrainTopology.Enum.Ocean | TerrainTopology.Enum.Oceanside | TerrainTopology.Enum.River | TerrainTopology.Enum.Riverside | TerrainTopology.Enum.Lake | TerrainTopology.Enum.Lakeside | TerrainTopology.Enum.Road | TerrainTopology.Enum.Roadside | TerrainTopology.Enum.Rail | TerrainTopology.Enum.Railside | TerrainTopology.Enum.Decor | TerrainTopology.Enum.Building | TerrainTopology.Enum.Monument | TerrainTopology.Enum.Summit | TerrainTopology.Enum.Mountain);
        private const int BlockedTopologyAnimal = (int)(TerrainTopology.Enum.Cliff | TerrainTopology.Enum.Cliffside | TerrainTopology.Enum.Ocean | TerrainTopology.Enum.River | TerrainTopology.Enum.Lake | TerrainTopology.Enum.Decor | TerrainTopology.Enum.Building | TerrainTopology.Enum.Monument);

        private static bool IsAvailableTopology(Vector3 position, int blockedTopology) => (TerrainMeta.TopologyMap.GetTopology(position) & blockedTopology) == 0;

        private const int GroundLayers = 1 << 16 | 1 << 23;

        private static bool IsRaycast(Vector3 position, out RaycastHit raycastHit) => Physics.Raycast(position, Vector3.down, out raycastHit, 500f, GroundLayers);

        private static bool IsNavMesh(Vector3 position, out NavMeshHit navMeshHit) => NavMesh.SamplePosition(position, out navMeshHit, 2f, 1);

        private const int EntityLayers = 1 << 8 | 1 << 17 | 1 << 21;

        private static bool IsEntities(Vector3 position, float radius, int layers)
        {
            List<BaseEntity> list = Pool.Get<List<BaseEntity>>();
            Vis.Entities(position, radius, list, layers);
            bool hasEntity = list.Count > 0;
            Pool.FreeUnmanaged(ref list);
            return hasEntity;
        }

        private HashSet<MonumentInfo> Monuments { get; set; } = null;

        private HashSet<string> UnnecessaryMonuments { get; } = new HashSet<string>
        {
            "Substation",
            "Outpost",
            "Bandit Camp",
            "Fishing Village",
            "Large Fishing Village",
            "Ranch",
            "Large Barn",
            "Ice Lake",
            "Mountain"
        };

        private static string GetNameMonument(MonumentInfo monument)
        {
            if (monument.name.Contains("harbor_1")) return "Small " + monument.displayPhrase.english.Replace("\n", string.Empty);
            if (monument.name.Contains("harbor_2")) return "Large " + monument.displayPhrase.english.Replace("\n", string.Empty);
            if (monument.name.Contains("desert_military_base_a")) return monument.displayPhrase.english.Replace("\n", string.Empty) + " A";
            if (monument.name.Contains("desert_military_base_b")) return monument.displayPhrase.english.Replace("\n", string.Empty) + " B";
            if (monument.name.Contains("desert_military_base_c")) return monument.displayPhrase.english.Replace("\n", string.Empty) + " C";
            if (monument.name.Contains("desert_military_base_d")) return monument.displayPhrase.english.Replace("\n", string.Empty) + " D";
            return monument.displayPhrase.english.Replace("\n", string.Empty);
        }

        private bool IsNecessaryMonument(MonumentInfo monument)
        {
            string name = GetNameMonument(monument);
            if (string.IsNullOrEmpty(name) || UnnecessaryMonuments.Contains(name)) return false;
            return _config.Spawn.Monuments.Any(x => x.Name == name);
        }
        #endregion Find Positions

        #region Controller
        internal const float SmallReceiverRadius = 4.24264f;
        internal const float LargeReceiverRadius = 42.4264f;
        internal const float SimilarReceiverRadius = 84.8528f;

        private ControllerTriangulation Controller { get; set; } = null;
        private bool Active { get; set; } = false;

        private void StartTimer()
        {
            if (!_config.EnabledTimer) return;
            timer.In(UnityEngine.Random.Range(_config.MinStartTime, _config.MaxStartTime), () =>
            {
                if (!Active) Start(null);
                else Puts("This event is active now. To finish this event (tstop), then to start the next one");
            });
        }

        private void Start(BasePlayer player)
        {
            if (!PluginExistsForStart("AnimalSpawn")) return;
            CheckVersionPlugin();
            FindAllPositions();
            if (Positions.Count < 4)
            {
                PrintWarning("There were not enough Arctic positions on the map to launch the triangulation event!");
                StartTimer();
                return;
            }
            Active = true;
            AlertToAllPlayers("PreStart", _config.Chat.Prefix, GetTimeFormat((int)_config.PreStartTime));
            timer.In(_config.PreStartTime, () =>
            {
                Puts($"{Name} has begun");
                ToggleHooks(true);
                Controller = new GameObject().AddComponent<ControllerTriangulation>();
                Controller.Init();
                if (player != null) Controller.SetOwner(player);
                Interface.Oxide.CallHook($"On{Name}Start");
            });
        }

        private void Finish()
        {
            ToggleHooks(false);
            if (Controller != null)
            {
                if (Controller.Receiver != null) foreach (BasePlayer player in Controller.Receiver.Players) TryEnableSkillTree(player);
                UnityEngine.Object.Destroy(Controller.gameObject);
            }
            Active = false;
            SendBalance();
            Interface.Oxide.CallHook($"On{Name}End");
            Puts($"{Name} has ended");
            StartTimer();
        }

        internal class ControllerTriangulation : FacepunchBehaviour
        {
            private static PluginConfig _config => _ins._config;

            internal BasePlayer Owner { get; set; } = null;

            internal ControllerReceiver Receiver { get; set; } = null;
            internal ControllerDrill Drill { get; set; } = null;

            internal int CurrentStageEvent { get; set; } = 1;

            internal void Init() => SpawnReceiver(_config.FirstReceiver);

            private void OnDestroy()
            {
                if (Receiver != null) Destroy(Receiver.gameObject);
                if (Drill != null) Destroy(Drill.gameObject);
            }

            private void SpawnReceiver(ReceiverConfig config)
            {
                Location location = GetLocationEvent();
                if (location == null)
                {
                    _ins.FindAllPositions();
                    location = GetLocationEvent();
                }

                ClearEntities(location.Main);

                Receiver = new GameObject().AddComponent<ControllerReceiver>();
                Receiver.transform.position = location.Main;
                Receiver.Config = config;
                foreach (Vector3 pos in location.Animals) Receiver.AnimalPositions.Add(pos);
                Receiver.Init();

                Interface.Oxide.CallHook("OnTriangulationReceiverSpawn", Receiver.transform.position, LargeReceiverRadius);

                _ins.AlertToAllPlayers("ReceiverSpawn", _config.Chat.Prefix, CurrentStageEvent, MapHelper.GridToString(MapHelper.PositionToGrid(Receiver.transform.position)), GetTimeFormat((int)_config.ScanWaitTime));
            }

            private void SpawnDrill()
            {
                Location location = GetLocationEvent();
                if (location == null)
                {
                    _ins.FindAllPositions();
                    location = GetLocationEvent();
                }

                ClearEntities(location.Main);

                Drill = new GameObject().AddComponent<ControllerDrill>();
                Drill.transform.position = location.Main;
                Drill.Init();

                Interface.Oxide.CallHook("OnTriangulationDrillSpawn", Drill.transform.position, LargeReceiverRadius);

                _ins.AlertToAllPlayers("DrillSpawn", _config.Chat.Prefix, MapHelper.GridToString(MapHelper.PositionToGrid(Drill.transform.position)), GetTimeFormat(_config.Drill.LifeTime));
            }

            private static Location GetLocationEvent()
            {
                Location result = null;
                while (result == null)
                {
                    if (_ins.Positions.Count == 0) break;
                    result = _ins.Positions.GetRandom();
                    _ins.Positions.Remove(result);
                    if (IsEntities(result.Main, LargeReceiverRadius, EntityLayers)) result = null;
                }
                return result;
            }

            internal void NextStage()
            {
                if (Receiver != null) Destroy(Receiver.gameObject);
                CurrentStageEvent++;
                if (CurrentStageEvent == 2) SpawnReceiver(_config.SecondReceiver);
                if (CurrentStageEvent == 3) SpawnReceiver(_config.ThirdReceiver);
                if (CurrentStageEvent == 4) SpawnDrill();
            }

            private static void ClearEntities(Vector3 position)
            {
                List<BaseEntity> list = Pool.Get<List<BaseEntity>>();
                Vis.Entities(position, SmallReceiverRadius * 3f, list);
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    BaseEntity ent = list[i];
                    if ((ent is TreeEntity || ent is OreResourceEntity || ent is ResourceEntity || ent is CollectibleEntity || ent is BushEntity || ent is HelicopterDebris || ent is JunkPile || ent is LootContainer) && ent.IsExists()) ent.Kill();
                }
                Pool.FreeUnmanaged(ref list);
            }

            internal void SetOwner(BasePlayer player)
            {
                Owner = player;
                SetCooldown(player.userID);
            }

            private static void SetCooldown(ulong id)
            {
                if (_config.CooldownOwner <= 0) return;
                if (_ins.PlayersData.ContainsKey(id)) _ins.PlayersData[id] = CurrentTime;
                else _ins.PlayersData.Add(id, CurrentTime);
                _ins.SaveData();
            }
        }
        #endregion Controller

        #region Receiver
        private object CanCustomAnimalSpawnCorpse(BaseAnimalNPC animal)
        {
            if (Controller.Receiver == null || animal == null) return null;
            if (Controller.Receiver.KillAnimal(animal)) return true;
            else return null;
        }

        private object OnCustomAnimalTarget(BaseAnimalNPC animal, BasePlayer player)
        {
            if (Controller.Receiver == null || animal == null || !player.IsPlayer() || !Controller.Receiver.Animals.Contains(animal)) return null;

            if (!_config.TargetNpc && !IsTeam(player.userID, Controller.Owner.userID)) return false;

            string shortname = ControllerReceiver.GetAnimalShortname(animal);
            if (string.IsNullOrEmpty(shortname)) return null;
            AnimalConfig config = _config.Presets[shortname];

            if (config.CanAttackPlayer) return null;
            else return false;
        }

        private void OnEntityKill(BaseAnimalNPC entity) => Controller.Receiver?.KillAnimal(entity);

        private void OnEntityDeath(BaseAnimalNPC animal, HitInfo info)
        {
            if (animal == null || info == null || Controller.Receiver == null) return;
            BasePlayer attacker = info.InitiatorPlayer;
            if (!attacker.IsPlayer()) return;
            if (Controller.Receiver.Animals.Contains(animal)) ActionEconomy(attacker.userID, "Animal", ControllerReceiver.GetAnimalShortname(animal));
        }

        private object OnRestoreUponDeath(BasePlayer player)
        {
            if (_config.RestoreUponDeath && Controller.Receiver != null && player.IsPlayer() && Controller.Receiver.Players.Contains(player)) return false;
            else return null;
        }

        internal HashSet<Prefab> ReceiverPrefabs { get; } = new HashSet<Prefab>
        {
            new Prefab { Path = "assets/prefabs/deployable/mailbox/mailbox.deployed.prefab", Pos = new Vector3(0.434f, -0.204f, 0f), Rot = new Vector3(12.522f, 270f, 0f) },
            new Prefab { Path = "assets/prefabs/deployable/mailbox/mailbox.deployed.prefab", Pos = new Vector3(-0.045f, -0.204f, -0.48f), Rot = new Vector3(12.522f, 0f, 0f) },
            new Prefab { Path = "assets/prefabs/deployable/mailbox/mailbox.deployed.prefab", Pos = new Vector3(-0.525f, -0.204f, 0f), Rot = new Vector3(12.522f, 90f, 0f) },
            new Prefab { Path = "assets/prefabs/deployable/mailbox/mailbox.deployed.prefab", Pos = new Vector3(-0.045f, -0.204f, 0.479f), Rot = new Vector3(12.522f, 180f, 0f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/teslacoil/teslacoil.deployed.prefab", Pos = new Vector3(0.504f, -0.434f, 0f), Rot = new Vector3(347.478f, 90f, 0f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/teslacoil/teslacoil.deployed.prefab", Pos = new Vector3(-0.045f, -0.434f, -0.553f), Rot = new Vector3(347.478f, 180f, 0f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/teslacoil/teslacoil.deployed.prefab", Pos = new Vector3(-0.594f, -0.434f, 0f), Rot = new Vector3(347.478f, 270f, 0f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/teslacoil/teslacoil.deployed.prefab", Pos = new Vector3(-0.045f, -0.434f, 0.55f), Rot = new Vector3(347.478f, 0f, 0f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/batteries/smallrechargablebattery.deployed.prefab", Pos = new Vector3(0.245f, -0.3f, -0.294f), Rot = new Vector3(0f, 224.993f, 0f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/detectors/hbhfsensor/hbhfsensor.deployed.prefab", Pos = new Vector3(-0.051f, 0.78f, 0f), Rot = new Vector3(90f, 0f, 0f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/lights/flasherlight/electric.flasherlight.deployed.prefab", Pos = new Vector3(-0.05f, 1.037f, 0f), Rot = new Vector3(0f, 0f, 0f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/gates/rfreceiver/rfreceiver.prefab", Pos = new Vector3(-0.05f, 0.813f, 0f), Rot = new Vector3(0f, 331.876f, 0f) },
            new Prefab { Path = "assets/prefabs/resource/targeting computer/targeting_computer.worldmodel.prefab", Pos = new Vector3(0.268f, 0.714f, 0.301f), Rot = new Vector3(0f, 225f, 0f) },
            new Prefab { Path = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab", Pos = new Vector3(-0.643f, -0.3f, 0.602f), Rot = new Vector3(0f, 314.993f, 0f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/alarms/audioalarm.prefab", Pos = new Vector3(-0.029f, -0.5f, 0.18f), Rot = new Vector3(270f, 0f, 0f) }
        };

        internal HashSet<Vector3> ReceiverMarker { get; } = new HashSet<Vector3>
        {
            new Vector3(48.39286f, 0f, 3.39284f),
            new Vector3(48.39286f, 0f, 1.785697f),
            new Vector3(48.39286f, 0f, 0.1785538f),
            new Vector3(48.39286f, 0f, -1.428589f),
            new Vector3(48.39286f, 0f, -3.035732f),
            new Vector3(46.78571f, 0f, 9.821411f),
            new Vector3(46.78571f, 0f, 8.214269f),
            new Vector3(46.78571f, 0f, 6.607126f),
            new Vector3(46.78571f, 0f, 4.999983f),
            new Vector3(46.78571f, 0f, 3.39284f),
            new Vector3(46.78571f, 0f, 1.785697f),
            new Vector3(46.78571f, 0f, 0.1785538f),
            new Vector3(46.78571f, 0f, -1.428589f),
            new Vector3(46.78571f, 0f, -3.035732f),
            new Vector3(46.78571f, 0f, -4.642875f),
            new Vector3(46.78571f, 0f, -6.250018f),
            new Vector3(46.78571f, 0f, -7.857161f),
            new Vector3(46.78571f, 0f, -9.464304f),
            new Vector3(45.17857f, 0f, 14.64284f),
            new Vector3(45.17857f, 0f, 13.0357f),
            new Vector3(45.17857f, 0f, 11.42855f),
            new Vector3(45.17857f, 0f, 9.821411f),
            new Vector3(45.17857f, 0f, 8.214269f),
            new Vector3(45.17857f, 0f, 6.607126f),
            new Vector3(45.17857f, 0f, 4.999983f),
            new Vector3(45.17857f, 0f, 3.39284f),
            new Vector3(45.17857f, 0f, 1.785697f),
            new Vector3(45.17857f, 0f, 0.1785538f),
            new Vector3(45.17857f, 0f, -1.428589f),
            new Vector3(45.17857f, 0f, -3.035732f),
            new Vector3(45.17857f, 0f, -4.642875f),
            new Vector3(45.17857f, 0f, -6.250018f),
            new Vector3(45.17857f, 0f, -7.857161f),
            new Vector3(45.17857f, 0f, -9.464304f),
            new Vector3(45.17857f, 0f, -11.07145f),
            new Vector3(45.17857f, 0f, -12.67859f),
            new Vector3(45.17857f, 0f, -14.28573f),
            new Vector3(45.17857f, 0f, -15.89287f),
            new Vector3(43.57142f, 0f, 19.46427f),
            new Vector3(43.57142f, 0f, 17.85712f),
            new Vector3(43.57142f, 0f, 16.24998f),
            new Vector3(43.57142f, 0f, 14.64284f),
            new Vector3(43.57142f, 0f, 13.0357f),
            new Vector3(43.57142f, 0f, 11.42855f),
            new Vector3(43.57142f, 0f, 9.821411f),
            new Vector3(43.57142f, 0f, 8.214269f),
            new Vector3(43.57142f, 0f, 6.607126f),
            new Vector3(43.57142f, 0f, -7.857161f),
            new Vector3(43.57142f, 0f, -9.464304f),
            new Vector3(43.57142f, 0f, -11.07145f),
            new Vector3(43.57142f, 0f, -12.67859f),
            new Vector3(43.57142f, 0f, -14.28573f),
            new Vector3(43.57142f, 0f, -15.89287f),
            new Vector3(43.57142f, 0f, -17.50002f),
            new Vector3(43.57142f, 0f, -19.10716f),
            new Vector3(43.57142f, 0f, -20.7143f),
            new Vector3(41.96428f, 0f, 21.07141f),
            new Vector3(41.96428f, 0f, 19.46427f),
            new Vector3(41.96428f, 0f, 17.85712f),
            new Vector3(41.96428f, 0f, 16.24998f),
            new Vector3(41.96428f, 0f, 14.64284f),
            new Vector3(41.96428f, 0f, -15.89287f),
            new Vector3(41.96428f, 0f, -17.50002f),
            new Vector3(41.96428f, 0f, -19.10716f),
            new Vector3(41.96428f, 0f, -20.7143f),
            new Vector3(41.96428f, 0f, -22.32145f),
            new Vector3(40.35714f, 0f, 24.28569f),
            new Vector3(40.35714f, 0f, 22.67855f),
            new Vector3(40.35714f, 0f, 21.07141f),
            new Vector3(40.35714f, 0f, 19.46427f),
            new Vector3(40.35714f, 0f, 17.85712f),
            new Vector3(40.35714f, 0f, -19.10716f),
            new Vector3(40.35714f, 0f, -20.7143f),
            new Vector3(40.35714f, 0f, -22.32145f),
            new Vector3(40.35714f, 0f, -23.92859f),
            new Vector3(40.35714f, 0f, -25.53573f),
            new Vector3(38.74999f, 0f, 27.49998f),
            new Vector3(38.74999f, 0f, 25.89284f),
            new Vector3(38.74999f, 0f, 24.28569f),
            new Vector3(38.74999f, 0f, 22.67855f),
            new Vector3(38.74999f, 0f, 21.07141f),
            new Vector3(38.74999f, 0f, -22.32145f),
            new Vector3(38.74999f, 0f, -23.92859f),
            new Vector3(38.74999f, 0f, -25.53573f),
            new Vector3(38.74999f, 0f, -27.14287f),
            new Vector3(38.74999f, 0f, -28.75002f),
            new Vector3(37.14285f, 0f, 29.10713f),
            new Vector3(37.14285f, 0f, 27.49998f),
            new Vector3(37.14285f, 0f, 25.89284f),
            new Vector3(37.14285f, 0f, 24.28569f),
            new Vector3(37.14285f, 0f, -25.53573f),
            new Vector3(37.14285f, 0f, -27.14287f),
            new Vector3(37.14285f, 0f, -28.75002f),
            new Vector3(37.14285f, 0f, -30.35716f),
            new Vector3(35.5357f, 0f, 30.71427f),
            new Vector3(35.5357f, 0f, 29.10713f),
            new Vector3(35.5357f, 0f, 27.49998f),
            new Vector3(35.5357f, 0f, -27.14287f),
            new Vector3(35.5357f, 0f, -28.75002f),
            new Vector3(35.5357f, 0f, -30.35716f),
            new Vector3(35.5357f, 0f, -31.96431f),
            new Vector3(33.92856f, 0f, 32.32141f),
            new Vector3(33.92856f, 0f, 30.71427f),
            new Vector3(33.92856f, 0f, 29.10713f),
            new Vector3(33.92856f, 0f, 4.999983f),
            new Vector3(33.92856f, 0f, 3.39284f),
            new Vector3(33.92856f, 0f, 1.785697f),
            new Vector3(33.92856f, 0f, 0.1785538f),
            new Vector3(33.92856f, 0f, -1.428589f),
            new Vector3(33.92856f, 0f, -3.035732f),
            new Vector3(33.92856f, 0f, -4.642875f),
            new Vector3(33.92856f, 0f, -6.250018f),
            new Vector3(33.92856f, 0f, -30.35716f),
            new Vector3(33.92856f, 0f, -31.96431f),
            new Vector3(33.92856f, 0f, -33.57145f),
            new Vector3(32.32141f, 0f, 33.92856f),
            new Vector3(32.32141f, 0f, 32.32141f),
            new Vector3(32.32141f, 0f, 30.71427f),
            new Vector3(32.32141f, 0f, 9.821411f),
            new Vector3(32.32141f, 0f, 8.214269f),
            new Vector3(32.32141f, 0f, 6.607126f),
            new Vector3(32.32141f, 0f, 4.999983f),
            new Vector3(32.32141f, 0f, 3.39284f),
            new Vector3(32.32141f, 0f, 1.785697f),
            new Vector3(32.32141f, 0f, 0.1785538f),
            new Vector3(32.32141f, 0f, -1.428589f),
            new Vector3(32.32141f, 0f, -3.035732f),
            new Vector3(32.32141f, 0f, -4.642875f),
            new Vector3(32.32141f, 0f, -6.250018f),
            new Vector3(32.32141f, 0f, -7.857161f),
            new Vector3(32.32141f, 0f, -9.464304f),
            new Vector3(32.32141f, 0f, -11.07145f),
            new Vector3(32.32141f, 0f, -31.96431f),
            new Vector3(32.32141f, 0f, -33.57145f),
            new Vector3(32.32141f, 0f, -35.1786f),
            new Vector3(30.71427f, 0f, 35.5357f),
            new Vector3(30.71427f, 0f, 33.92856f),
            new Vector3(30.71427f, 0f, 32.32141f),
            new Vector3(30.71427f, 0f, 14.64284f),
            new Vector3(30.71427f, 0f, 13.0357f),
            new Vector3(30.71427f, 0f, 11.42855f),
            new Vector3(30.71427f, 0f, 9.821411f),
            new Vector3(30.71427f, 0f, 8.214269f),
            new Vector3(30.71427f, 0f, 6.607126f),
            new Vector3(30.71427f, 0f, 4.999983f),
            new Vector3(30.71427f, 0f, 3.39284f),
            new Vector3(30.71427f, 0f, 1.785697f),
            new Vector3(30.71427f, 0f, 0.1785538f),
            new Vector3(30.71427f, 0f, -1.428589f),
            new Vector3(30.71427f, 0f, -3.035732f),
            new Vector3(30.71427f, 0f, -4.642875f),
            new Vector3(30.71427f, 0f, -6.250018f),
            new Vector3(30.71427f, 0f, -7.857161f),
            new Vector3(30.71427f, 0f, -9.464304f),
            new Vector3(30.71427f, 0f, -11.07145f),
            new Vector3(30.71427f, 0f, -12.67859f),
            new Vector3(30.71427f, 0f, -14.28573f),
            new Vector3(30.71427f, 0f, -15.89287f),
            new Vector3(30.71427f, 0f, -33.57145f),
            new Vector3(30.71427f, 0f, -35.1786f),
            new Vector3(30.71427f, 0f, -36.78574f),
            new Vector3(29.10713f, 0f, 37.14285f),
            new Vector3(29.10713f, 0f, 35.5357f),
            new Vector3(29.10713f, 0f, 33.92856f),
            new Vector3(29.10713f, 0f, 16.24998f),
            new Vector3(29.10713f, 0f, 14.64284f),
            new Vector3(29.10713f, 0f, 13.0357f),
            new Vector3(29.10713f, 0f, 11.42855f),
            new Vector3(29.10713f, 0f, 9.821411f),
            new Vector3(29.10713f, 0f, 8.214269f),
            new Vector3(29.10713f, 0f, 6.607126f),
            new Vector3(29.10713f, 0f, 4.999983f),
            new Vector3(29.10713f, 0f, 3.39284f),
            new Vector3(29.10713f, 0f, 1.785697f),
            new Vector3(29.10713f, 0f, 0.1785538f),
            new Vector3(29.10713f, 0f, -1.428589f),
            new Vector3(29.10713f, 0f, -3.035732f),
            new Vector3(29.10713f, 0f, -4.642875f),
            new Vector3(29.10713f, 0f, -6.250018f),
            new Vector3(29.10713f, 0f, -7.857161f),
            new Vector3(29.10713f, 0f, -9.464304f),
            new Vector3(29.10713f, 0f, -11.07145f),
            new Vector3(29.10713f, 0f, -12.67859f),
            new Vector3(29.10713f, 0f, -14.28573f),
            new Vector3(29.10713f, 0f, -15.89287f),
            new Vector3(29.10713f, 0f, -17.50002f),
            new Vector3(29.10713f, 0f, -19.10716f),
            new Vector3(29.10713f, 0f, -35.1786f),
            new Vector3(29.10713f, 0f, -36.78574f),
            new Vector3(29.10713f, 0f, -38.39288f),
            new Vector3(27.49998f, 0f, 38.74999f),
            new Vector3(27.49998f, 0f, 37.14285f),
            new Vector3(27.49998f, 0f, 35.5357f),
            new Vector3(27.49998f, 0f, 19.46427f),
            new Vector3(27.49998f, 0f, 17.85712f),
            new Vector3(27.49998f, 0f, 16.24998f),
            new Vector3(27.49998f, 0f, 14.64284f),
            new Vector3(27.49998f, 0f, 13.0357f),
            new Vector3(27.49998f, 0f, 11.42855f),
            new Vector3(27.49998f, 0f, -12.67859f),
            new Vector3(27.49998f, 0f, -14.28573f),
            new Vector3(27.49998f, 0f, -15.89287f),
            new Vector3(27.49998f, 0f, -17.50002f),
            new Vector3(27.49998f, 0f, -19.10716f),
            new Vector3(27.49998f, 0f, -20.7143f),
            new Vector3(27.49998f, 0f, -36.78574f),
            new Vector3(27.49998f, 0f, -38.39288f),
            new Vector3(25.89284f, 0f, 38.74999f),
            new Vector3(25.89284f, 0f, 37.14285f),
            new Vector3(25.89284f, 0f, 21.07141f),
            new Vector3(25.89284f, 0f, 19.46427f),
            new Vector3(25.89284f, 0f, 17.85712f),
            new Vector3(25.89284f, 0f, 16.24998f),
            new Vector3(25.89284f, 0f, 14.64284f),
            new Vector3(25.89284f, 0f, 13.0357f),
            new Vector3(25.89284f, 0f, -15.89287f),
            new Vector3(25.89284f, 0f, -17.50002f),
            new Vector3(25.89284f, 0f, -19.10716f),
            new Vector3(25.89284f, 0f, -20.7143f),
            new Vector3(25.89284f, 0f, -22.32145f),
            new Vector3(25.89284f, 0f, -36.78574f),
            new Vector3(25.89284f, 0f, -38.39288f),
            new Vector3(25.89284f, 0f, -40.00003f),
            new Vector3(24.28569f, 0f, 40.35714f),
            new Vector3(24.28569f, 0f, 38.74999f),
            new Vector3(24.28569f, 0f, 37.14285f),
            new Vector3(24.28569f, 0f, 22.67855f),
            new Vector3(24.28569f, 0f, 21.07141f),
            new Vector3(24.28569f, 0f, 19.46427f),
            new Vector3(24.28569f, 0f, 17.85712f),
            new Vector3(24.28569f, 0f, 16.24998f),
            new Vector3(24.28569f, 0f, -17.50002f),
            new Vector3(24.28569f, 0f, -19.10716f),
            new Vector3(24.28569f, 0f, -20.7143f),
            new Vector3(24.28569f, 0f, -22.32145f),
            new Vector3(24.28569f, 0f, -23.92859f),
            new Vector3(24.28569f, 0f, -38.39288f),
            new Vector3(24.28569f, 0f, -40.00003f),
            new Vector3(24.28569f, 0f, -41.60717f),
            new Vector3(22.67855f, 0f, 41.96428f),
            new Vector3(22.67855f, 0f, 40.35714f),
            new Vector3(22.67855f, 0f, 38.74999f),
            new Vector3(22.67855f, 0f, 22.67855f),
            new Vector3(22.67855f, 0f, 21.07141f),
            new Vector3(22.67855f, 0f, 19.46427f),
            new Vector3(22.67855f, 0f, -20.7143f),
            new Vector3(22.67855f, 0f, -22.32145f),
            new Vector3(22.67855f, 0f, -23.92859f),
            new Vector3(22.67855f, 0f, -40.00003f),
            new Vector3(22.67855f, 0f, -41.60717f),
            new Vector3(21.07141f, 0f, 41.96428f),
            new Vector3(21.07141f, 0f, 40.35714f),
            new Vector3(21.07141f, 0f, 38.74999f),
            new Vector3(21.07141f, 0f, 21.07141f),
            new Vector3(21.07141f, 0f, 6.607126f),
            new Vector3(21.07141f, 0f, 4.999983f),
            new Vector3(21.07141f, 0f, 3.39284f),
            new Vector3(21.07141f, 0f, 1.785697f),
            new Vector3(21.07141f, 0f, 0.1785538f),
            new Vector3(21.07141f, 0f, -1.428589f),
            new Vector3(21.07141f, 0f, -3.035732f),
            new Vector3(21.07141f, 0f, -4.642875f),
            new Vector3(21.07141f, 0f, -6.250018f),
            new Vector3(21.07141f, 0f, -7.857161f),
            new Vector3(21.07141f, 0f, -22.32145f),
            new Vector3(21.07141f, 0f, -40.00003f),
            new Vector3(21.07141f, 0f, -41.60717f),
            new Vector3(21.07141f, 0f, -43.21432f),
            new Vector3(19.46427f, 0f, 43.57142f),
            new Vector3(19.46427f, 0f, 41.96428f),
            new Vector3(19.46427f, 0f, 40.35714f),
            new Vector3(19.46427f, 0f, 9.821411f),
            new Vector3(19.46427f, 0f, 8.214269f),
            new Vector3(19.46427f, 0f, 6.607126f),
            new Vector3(19.46427f, 0f, 4.999983f),
            new Vector3(19.46427f, 0f, 3.39284f),
            new Vector3(19.46427f, 0f, 1.785697f),
            new Vector3(19.46427f, 0f, 0.1785538f),
            new Vector3(19.46427f, 0f, -1.428589f),
            new Vector3(19.46427f, 0f, -3.035732f),
            new Vector3(19.46427f, 0f, -4.642875f),
            new Vector3(19.46427f, 0f, -6.250018f),
            new Vector3(19.46427f, 0f, -7.857161f),
            new Vector3(19.46427f, 0f, -9.464304f),
            new Vector3(19.46427f, 0f, -11.07145f),
            new Vector3(19.46427f, 0f, -41.60717f),
            new Vector3(19.46427f, 0f, -43.21432f),
            new Vector3(17.85712f, 0f, 43.57142f),
            new Vector3(17.85712f, 0f, 41.96428f),
            new Vector3(17.85712f, 0f, 40.35714f),
            new Vector3(17.85712f, 0f, 11.42855f),
            new Vector3(17.85712f, 0f, 9.821411f),
            new Vector3(17.85712f, 0f, 8.214269f),
            new Vector3(17.85712f, 0f, 6.607126f),
            new Vector3(17.85712f, 0f, 4.999983f),
            new Vector3(17.85712f, 0f, 3.39284f),
            new Vector3(17.85712f, 0f, 1.785697f),
            new Vector3(17.85712f, 0f, 0.1785538f),
            new Vector3(17.85712f, 0f, -1.428589f),
            new Vector3(17.85712f, 0f, -3.035732f),
            new Vector3(17.85712f, 0f, -4.642875f),
            new Vector3(17.85712f, 0f, -6.250018f),
            new Vector3(17.85712f, 0f, -7.857161f),
            new Vector3(17.85712f, 0f, -9.464304f),
            new Vector3(17.85712f, 0f, -11.07145f),
            new Vector3(17.85712f, 0f, -12.67859f),
            new Vector3(17.85712f, 0f, -14.28573f),
            new Vector3(17.85712f, 0f, -41.60717f),
            new Vector3(17.85712f, 0f, -43.21432f),
            new Vector3(17.85712f, 0f, -44.82146f),
            new Vector3(16.24998f, 0f, 43.57142f),
            new Vector3(16.24998f, 0f, 41.96428f),
            new Vector3(16.24998f, 0f, 13.0357f),
            new Vector3(16.24998f, 0f, 11.42855f),
            new Vector3(16.24998f, 0f, 9.821411f),
            new Vector3(16.24998f, 0f, 8.214269f),
            new Vector3(16.24998f, 0f, 6.607126f),
            new Vector3(16.24998f, 0f, 4.999983f),
            new Vector3(16.24998f, 0f, -7.857161f),
            new Vector3(16.24998f, 0f, -9.464304f),
            new Vector3(16.24998f, 0f, -11.07145f),
            new Vector3(16.24998f, 0f, -12.67859f),
            new Vector3(16.24998f, 0f, -14.28573f),
            new Vector3(16.24998f, 0f, -15.89287f),
            new Vector3(16.24998f, 0f, -43.21432f),
            new Vector3(16.24998f, 0f, -44.82146f),
            new Vector3(14.64284f, 0f, 45.17857f),
            new Vector3(14.64284f, 0f, 43.57142f),
            new Vector3(14.64284f, 0f, 41.96428f),
            new Vector3(14.64284f, 0f, 13.0357f),
            new Vector3(14.64284f, 0f, 11.42855f),
            new Vector3(14.64284f, 0f, 9.821411f),
            new Vector3(14.64284f, 0f, -11.07145f),
            new Vector3(14.64284f, 0f, -12.67859f),
            new Vector3(14.64284f, 0f, -14.28573f),
            new Vector3(14.64284f, 0f, -15.89287f),
            new Vector3(14.64284f, 0f, -43.21432f),
            new Vector3(14.64284f, 0f, -44.82146f),
            new Vector3(13.0357f, 0f, 45.17857f),
            new Vector3(13.0357f, 0f, 43.57142f),
            new Vector3(13.0357f, 0f, 13.0357f),
            new Vector3(13.0357f, 0f, 11.42855f),
            new Vector3(13.0357f, 0f, -12.67859f),
            new Vector3(13.0357f, 0f, -14.28573f),
            new Vector3(13.0357f, 0f, -43.21432f),
            new Vector3(13.0357f, 0f, -44.82146f),
            new Vector3(13.0357f, 0f, -46.4286f),
            new Vector3(11.42855f, 0f, 45.17857f),
            new Vector3(11.42855f, 0f, 43.57142f),
            new Vector3(11.42855f, 0f, -44.82146f),
            new Vector3(11.42855f, 0f, -46.4286f),
            new Vector3(9.821411f, 0f, 46.78571f),
            new Vector3(9.821411f, 0f, 45.17857f),
            new Vector3(9.821411f, 0f, 43.57142f),
            new Vector3(9.821411f, 0f, -44.82146f),
            new Vector3(9.821411f, 0f, -46.4286f),
            new Vector3(8.214269f, 0f, 46.78571f),
            new Vector3(8.214269f, 0f, 45.17857f),
            new Vector3(8.214269f, 0f, 43.57142f),
            new Vector3(8.214269f, 0f, -44.82146f),
            new Vector3(8.214269f, 0f, -46.4286f),
            new Vector3(8.214269f, 0f, -48.03575f),
            new Vector3(6.607126f, 0f, 46.78571f),
            new Vector3(6.607126f, 0f, 45.17857f),
            new Vector3(6.607126f, 0f, 43.57142f),
            new Vector3(6.607126f, 0f, -44.82146f),
            new Vector3(6.607126f, 0f, -46.4286f),
            new Vector3(6.607126f, 0f, -48.03575f),
            new Vector3(4.999983f, 0f, 46.78571f),
            new Vector3(4.999983f, 0f, 45.17857f),
            new Vector3(4.999983f, 0f, -44.82146f),
            new Vector3(4.999983f, 0f, -46.4286f),
            new Vector3(4.999983f, 0f, -48.03575f),
            new Vector3(3.39284f, 0f, 48.39286f),
            new Vector3(3.39284f, 0f, 46.78571f),
            new Vector3(3.39284f, 0f, 45.17857f),
            new Vector3(3.39284f, 0f, -46.4286f),
            new Vector3(3.39284f, 0f, -48.03575f),
            new Vector3(1.785697f, 0f, 48.39286f),
            new Vector3(1.785697f, 0f, 46.78571f),
            new Vector3(1.785697f, 0f, 45.17857f),
            new Vector3(1.785697f, 0f, -46.4286f),
            new Vector3(1.785697f, 0f, -48.03575f),
            new Vector3(0.1785538f, 0f, 48.39286f),
            new Vector3(0.1785538f, 0f, 46.78571f),
            new Vector3(0.1785538f, 0f, 45.17857f),
            new Vector3(0.1785538f, 0f, -46.4286f),
            new Vector3(0.1785538f, 0f, -48.03575f),
            new Vector3(-1.428589f, 0f, 48.39286f),
            new Vector3(-1.428589f, 0f, 46.78571f),
            new Vector3(-1.428589f, 0f, 45.17857f),
            new Vector3(-1.428589f, 0f, -46.4286f),
            new Vector3(-1.428589f, 0f, -48.03575f),
            new Vector3(-3.035732f, 0f, 48.39286f),
            new Vector3(-3.035732f, 0f, 46.78571f),
            new Vector3(-3.035732f, 0f, 45.17857f),
            new Vector3(-3.035732f, 0f, -46.4286f),
            new Vector3(-3.035732f, 0f, -48.03575f),
            new Vector3(-4.642875f, 0f, 48.39286f),
            new Vector3(-4.642875f, 0f, 46.78571f),
            new Vector3(-4.642875f, 0f, 45.17857f),
            new Vector3(-4.642875f, 0f, -46.4286f),
            new Vector3(-4.642875f, 0f, -48.03575f),
            new Vector3(-6.250018f, 0f, 46.78571f),
            new Vector3(-6.250018f, 0f, 45.17857f),
            new Vector3(-6.250018f, 0f, -44.82146f),
            new Vector3(-6.250018f, 0f, -46.4286f),
            new Vector3(-6.250018f, 0f, -48.03575f),
            new Vector3(-7.857161f, 0f, 46.78571f),
            new Vector3(-7.857161f, 0f, 45.17857f),
            new Vector3(-7.857161f, 0f, 43.57142f),
            new Vector3(-7.857161f, 0f, -44.82146f),
            new Vector3(-7.857161f, 0f, -46.4286f),
            new Vector3(-7.857161f, 0f, -48.03575f),
            new Vector3(-9.464304f, 0f, 46.78571f),
            new Vector3(-9.464304f, 0f, 45.17857f),
            new Vector3(-9.464304f, 0f, 43.57142f),
            new Vector3(-9.464304f, 0f, -44.82146f),
            new Vector3(-9.464304f, 0f, -46.4286f),
            new Vector3(-9.464304f, 0f, -48.03575f),
            new Vector3(-11.07145f, 0f, 45.17857f),
            new Vector3(-11.07145f, 0f, 43.57142f),
            new Vector3(-11.07145f, 0f, -44.82146f),
            new Vector3(-11.07145f, 0f, -46.4286f),
            new Vector3(-12.67859f, 0f, 45.17857f),
            new Vector3(-12.67859f, 0f, 43.57142f),
            new Vector3(-12.67859f, 0f, -43.21432f),
            new Vector3(-12.67859f, 0f, -44.82146f),
            new Vector3(-12.67859f, 0f, -46.4286f),
            new Vector3(-14.28573f, 0f, 45.17857f),
            new Vector3(-14.28573f, 0f, 43.57142f),
            new Vector3(-14.28573f, 0f, 13.0357f),
            new Vector3(-14.28573f, 0f, 11.42855f),
            new Vector3(-14.28573f, 0f, 9.821411f),
            new Vector3(-14.28573f, 0f, -11.07145f),
            new Vector3(-14.28573f, 0f, -12.67859f),
            new Vector3(-14.28573f, 0f, -14.28573f),
            new Vector3(-14.28573f, 0f, -43.21432f),
            new Vector3(-14.28573f, 0f, -44.82146f),
            new Vector3(-14.28573f, 0f, -46.4286f),
            new Vector3(-15.89287f, 0f, 45.17857f),
            new Vector3(-15.89287f, 0f, 43.57142f),
            new Vector3(-15.89287f, 0f, 41.96428f),
            new Vector3(-15.89287f, 0f, 14.64284f),
            new Vector3(-15.89287f, 0f, 13.0357f),
            new Vector3(-15.89287f, 0f, 11.42855f),
            new Vector3(-15.89287f, 0f, 9.821411f),
            new Vector3(-15.89287f, 0f, 8.214269f),
            new Vector3(-15.89287f, 0f, -9.464304f),
            new Vector3(-15.89287f, 0f, -11.07145f),
            new Vector3(-15.89287f, 0f, -12.67859f),
            new Vector3(-15.89287f, 0f, -14.28573f),
            new Vector3(-15.89287f, 0f, -15.89287f),
            new Vector3(-15.89287f, 0f, -43.21432f),
            new Vector3(-15.89287f, 0f, -44.82146f),
            new Vector3(-17.50002f, 0f, 43.57142f),
            new Vector3(-17.50002f, 0f, 41.96428f),
            new Vector3(-17.50002f, 0f, 13.0357f),
            new Vector3(-17.50002f, 0f, 11.42855f),
            new Vector3(-17.50002f, 0f, 9.821411f),
            new Vector3(-17.50002f, 0f, 8.214269f),
            new Vector3(-17.50002f, 0f, 6.607126f),
            new Vector3(-17.50002f, 0f, 4.999983f),
            new Vector3(-17.50002f, 0f, 3.39284f),
            new Vector3(-17.50002f, 0f, -4.642875f),
            new Vector3(-17.50002f, 0f, -6.250018f),
            new Vector3(-17.50002f, 0f, -7.857161f),
            new Vector3(-17.50002f, 0f, -9.464304f),
            new Vector3(-17.50002f, 0f, -11.07145f),
            new Vector3(-17.50002f, 0f, -12.67859f),
            new Vector3(-17.50002f, 0f, -14.28573f),
            new Vector3(-17.50002f, 0f, -43.21432f),
            new Vector3(-17.50002f, 0f, -44.82146f),
            new Vector3(-19.10716f, 0f, 43.57142f),
            new Vector3(-19.10716f, 0f, 41.96428f),
            new Vector3(-19.10716f, 0f, 40.35714f),
            new Vector3(-19.10716f, 0f, 11.42855f),
            new Vector3(-19.10716f, 0f, 9.821411f),
            new Vector3(-19.10716f, 0f, 8.214269f),
            new Vector3(-19.10716f, 0f, 6.607126f),
            new Vector3(-19.10716f, 0f, 4.999983f),
            new Vector3(-19.10716f, 0f, 3.39284f),
            new Vector3(-19.10716f, 0f, 1.785697f),
            new Vector3(-19.10716f, 0f, 0.1785538f),
            new Vector3(-19.10716f, 0f, -1.428589f),
            new Vector3(-19.10716f, 0f, -3.035732f),
            new Vector3(-19.10716f, 0f, -4.642875f),
            new Vector3(-19.10716f, 0f, -6.250018f),
            new Vector3(-19.10716f, 0f, -7.857161f),
            new Vector3(-19.10716f, 0f, -9.464304f),
            new Vector3(-19.10716f, 0f, -11.07145f),
            new Vector3(-19.10716f, 0f, -12.67859f),
            new Vector3(-19.10716f, 0f, -41.60717f),
            new Vector3(-19.10716f, 0f, -43.21432f),
            new Vector3(-19.10716f, 0f, -44.82146f),
            new Vector3(-20.7143f, 0f, 43.57142f),
            new Vector3(-20.7143f, 0f, 41.96428f),
            new Vector3(-20.7143f, 0f, 40.35714f),
            new Vector3(-20.7143f, 0f, 8.214269f),
            new Vector3(-20.7143f, 0f, 6.607126f),
            new Vector3(-20.7143f, 0f, 4.999983f),
            new Vector3(-20.7143f, 0f, 3.39284f),
            new Vector3(-20.7143f, 0f, 1.785697f),
            new Vector3(-20.7143f, 0f, 0.1785538f),
            new Vector3(-20.7143f, 0f, -1.428589f),
            new Vector3(-20.7143f, 0f, -3.035732f),
            new Vector3(-20.7143f, 0f, -4.642875f),
            new Vector3(-20.7143f, 0f, -6.250018f),
            new Vector3(-20.7143f, 0f, -7.857161f),
            new Vector3(-20.7143f, 0f, -9.464304f),
            new Vector3(-20.7143f, 0f, -41.60717f),
            new Vector3(-20.7143f, 0f, -43.21432f),
            new Vector3(-22.32145f, 0f, 41.96428f),
            new Vector3(-22.32145f, 0f, 40.35714f),
            new Vector3(-22.32145f, 0f, 38.74999f),
            new Vector3(-22.32145f, 0f, 21.07141f),
            new Vector3(-22.32145f, 0f, 19.46427f),
            new Vector3(-22.32145f, 0f, 3.39284f),
            new Vector3(-22.32145f, 0f, 1.785697f),
            new Vector3(-22.32145f, 0f, 0.1785538f),
            new Vector3(-22.32145f, 0f, -1.428589f),
            new Vector3(-22.32145f, 0f, -3.035732f),
            new Vector3(-22.32145f, 0f, -4.642875f),
            new Vector3(-22.32145f, 0f, -20.7143f),
            new Vector3(-22.32145f, 0f, -22.32145f),
            new Vector3(-22.32145f, 0f, -40.00003f),
            new Vector3(-22.32145f, 0f, -41.60717f),
            new Vector3(-22.32145f, 0f, -43.21432f),
            new Vector3(-23.92859f, 0f, 40.35714f),
            new Vector3(-23.92859f, 0f, 38.74999f),
            new Vector3(-23.92859f, 0f, 22.67855f),
            new Vector3(-23.92859f, 0f, 21.07141f),
            new Vector3(-23.92859f, 0f, 19.46427f),
            new Vector3(-23.92859f, 0f, 17.85712f),
            new Vector3(-23.92859f, 0f, -19.10716f),
            new Vector3(-23.92859f, 0f, -20.7143f),
            new Vector3(-23.92859f, 0f, -22.32145f),
            new Vector3(-23.92859f, 0f, -23.92859f),
            new Vector3(-23.92859f, 0f, -38.39288f),
            new Vector3(-23.92859f, 0f, -40.00003f),
            new Vector3(-23.92859f, 0f, -41.60717f),
            new Vector3(-25.53573f, 0f, 40.35714f),
            new Vector3(-25.53573f, 0f, 38.74999f),
            new Vector3(-25.53573f, 0f, 37.14285f),
            new Vector3(-25.53573f, 0f, 22.67855f),
            new Vector3(-25.53573f, 0f, 21.07141f),
            new Vector3(-25.53573f, 0f, 19.46427f),
            new Vector3(-25.53573f, 0f, 17.85712f),
            new Vector3(-25.53573f, 0f, 16.24998f),
            new Vector3(-25.53573f, 0f, 14.64284f),
            new Vector3(-25.53573f, 0f, -15.89287f),
            new Vector3(-25.53573f, 0f, -17.50002f),
            new Vector3(-25.53573f, 0f, -19.10716f),
            new Vector3(-25.53573f, 0f, -20.7143f),
            new Vector3(-25.53573f, 0f, -22.32145f),
            new Vector3(-25.53573f, 0f, -23.92859f),
            new Vector3(-25.53573f, 0f, -38.39288f),
            new Vector3(-25.53573f, 0f, -40.00003f),
            new Vector3(-25.53573f, 0f, -41.60717f),
            new Vector3(-27.14287f, 0f, 38.74999f),
            new Vector3(-27.14287f, 0f, 37.14285f),
            new Vector3(-27.14287f, 0f, 35.5357f),
            new Vector3(-27.14287f, 0f, 19.46427f),
            new Vector3(-27.14287f, 0f, 17.85712f),
            new Vector3(-27.14287f, 0f, 16.24998f),
            new Vector3(-27.14287f, 0f, 14.64284f),
            new Vector3(-27.14287f, 0f, 13.0357f),
            new Vector3(-27.14287f, 0f, 11.42855f),
            new Vector3(-27.14287f, 0f, -14.28573f),
            new Vector3(-27.14287f, 0f, -15.89287f),
            new Vector3(-27.14287f, 0f, -17.50002f),
            new Vector3(-27.14287f, 0f, -19.10716f),
            new Vector3(-27.14287f, 0f, -20.7143f),
            new Vector3(-27.14287f, 0f, -36.78574f),
            new Vector3(-27.14287f, 0f, -38.39288f),
            new Vector3(-27.14287f, 0f, -40.00003f),
            new Vector3(-28.75002f, 0f, 38.74999f),
            new Vector3(-28.75002f, 0f, 37.14285f),
            new Vector3(-28.75002f, 0f, 35.5357f),
            new Vector3(-28.75002f, 0f, 17.85712f),
            new Vector3(-28.75002f, 0f, 16.24998f),
            new Vector3(-28.75002f, 0f, 14.64284f),
            new Vector3(-28.75002f, 0f, 13.0357f),
            new Vector3(-28.75002f, 0f, 11.42855f),
            new Vector3(-28.75002f, 0f, 9.821411f),
            new Vector3(-28.75002f, 0f, 8.214269f),
            new Vector3(-28.75002f, 0f, 6.607126f),
            new Vector3(-28.75002f, 0f, -7.857161f),
            new Vector3(-28.75002f, 0f, -9.464304f),
            new Vector3(-28.75002f, 0f, -11.07145f),
            new Vector3(-28.75002f, 0f, -12.67859f),
            new Vector3(-28.75002f, 0f, -14.28573f),
            new Vector3(-28.75002f, 0f, -15.89287f),
            new Vector3(-28.75002f, 0f, -17.50002f),
            new Vector3(-28.75002f, 0f, -19.10716f),
            new Vector3(-28.75002f, 0f, -35.1786f),
            new Vector3(-28.75002f, 0f, -36.78574f),
            new Vector3(-28.75002f, 0f, -38.39288f),
            new Vector3(-30.35716f, 0f, 37.14285f),
            new Vector3(-30.35716f, 0f, 35.5357f),
            new Vector3(-30.35716f, 0f, 33.92856f),
            new Vector3(-30.35716f, 0f, 16.24998f),
            new Vector3(-30.35716f, 0f, 14.64284f),
            new Vector3(-30.35716f, 0f, 13.0357f),
            new Vector3(-30.35716f, 0f, 11.42855f),
            new Vector3(-30.35716f, 0f, 9.821411f),
            new Vector3(-30.35716f, 0f, 8.214269f),
            new Vector3(-30.35716f, 0f, 6.607126f),
            new Vector3(-30.35716f, 0f, 4.999983f),
            new Vector3(-30.35716f, 0f, 3.39284f),
            new Vector3(-30.35716f, 0f, 1.785697f),
            new Vector3(-30.35716f, 0f, 0.1785538f),
            new Vector3(-30.35716f, 0f, -1.428589f),
            new Vector3(-30.35716f, 0f, -3.035732f),
            new Vector3(-30.35716f, 0f, -4.642875f),
            new Vector3(-30.35716f, 0f, -6.250018f),
            new Vector3(-30.35716f, 0f, -7.857161f),
            new Vector3(-30.35716f, 0f, -9.464304f),
            new Vector3(-30.35716f, 0f, -11.07145f),
            new Vector3(-30.35716f, 0f, -12.67859f),
            new Vector3(-30.35716f, 0f, -14.28573f),
            new Vector3(-30.35716f, 0f, -15.89287f),
            new Vector3(-30.35716f, 0f, -17.50002f),
            new Vector3(-30.35716f, 0f, -33.57145f),
            new Vector3(-30.35716f, 0f, -35.1786f),
            new Vector3(-30.35716f, 0f, -36.78574f),
            new Vector3(-30.35716f, 0f, -38.39288f),
            new Vector3(-31.96431f, 0f, 35.5357f),
            new Vector3(-31.96431f, 0f, 33.92856f),
            new Vector3(-31.96431f, 0f, 32.32141f),
            new Vector3(-31.96431f, 0f, 11.42855f),
            new Vector3(-31.96431f, 0f, 9.821411f),
            new Vector3(-31.96431f, 0f, 8.214269f),
            new Vector3(-31.96431f, 0f, 6.607126f),
            new Vector3(-31.96431f, 0f, 4.999983f),
            new Vector3(-31.96431f, 0f, 3.39284f),
            new Vector3(-31.96431f, 0f, 1.785697f),
            new Vector3(-31.96431f, 0f, 0.1785538f),
            new Vector3(-31.96431f, 0f, -1.428589f),
            new Vector3(-31.96431f, 0f, -3.035732f),
            new Vector3(-31.96431f, 0f, -4.642875f),
            new Vector3(-31.96431f, 0f, -6.250018f),
            new Vector3(-31.96431f, 0f, -7.857161f),
            new Vector3(-31.96431f, 0f, -9.464304f),
            new Vector3(-31.96431f, 0f, -11.07145f),
            new Vector3(-31.96431f, 0f, -12.67859f),
            new Vector3(-31.96431f, 0f, -14.28573f),
            new Vector3(-31.96431f, 0f, -33.57145f),
            new Vector3(-31.96431f, 0f, -35.1786f),
            new Vector3(-31.96431f, 0f, -36.78574f),
            new Vector3(-33.57145f, 0f, 33.92856f),
            new Vector3(-33.57145f, 0f, 32.32141f),
            new Vector3(-33.57145f, 0f, 30.71427f),
            new Vector3(-33.57145f, 0f, 8.214269f),
            new Vector3(-33.57145f, 0f, 6.607126f),
            new Vector3(-33.57145f, 0f, 4.999983f),
            new Vector3(-33.57145f, 0f, 3.39284f),
            new Vector3(-33.57145f, 0f, 1.785697f),
            new Vector3(-33.57145f, 0f, 0.1785538f),
            new Vector3(-33.57145f, 0f, -1.428589f),
            new Vector3(-33.57145f, 0f, -3.035732f),
            new Vector3(-33.57145f, 0f, -4.642875f),
            new Vector3(-33.57145f, 0f, -6.250018f),
            new Vector3(-33.57145f, 0f, -7.857161f),
            new Vector3(-33.57145f, 0f, -9.464304f),
            new Vector3(-33.57145f, 0f, -30.35716f),
            new Vector3(-33.57145f, 0f, -31.96431f),
            new Vector3(-33.57145f, 0f, -33.57145f),
            new Vector3(-33.57145f, 0f, -35.1786f),
            new Vector3(-35.1786f, 0f, 32.32141f),
            new Vector3(-35.1786f, 0f, 30.71427f),
            new Vector3(-35.1786f, 0f, 29.10713f),
            new Vector3(-35.1786f, 0f, -28.75002f),
            new Vector3(-35.1786f, 0f, -30.35716f),
            new Vector3(-35.1786f, 0f, -31.96431f),
            new Vector3(-35.1786f, 0f, -33.57145f),
            new Vector3(-36.78574f, 0f, 30.71427f),
            new Vector3(-36.78574f, 0f, 29.10713f),
            new Vector3(-36.78574f, 0f, 27.49998f),
            new Vector3(-36.78574f, 0f, 25.89284f),
            new Vector3(-36.78574f, 0f, -27.14287f),
            new Vector3(-36.78574f, 0f, -28.75002f),
            new Vector3(-36.78574f, 0f, -30.35716f),
            new Vector3(-36.78574f, 0f, -31.96431f),
            new Vector3(-38.39288f, 0f, 29.10713f),
            new Vector3(-38.39288f, 0f, 27.49998f),
            new Vector3(-38.39288f, 0f, 25.89284f),
            new Vector3(-38.39288f, 0f, 24.28569f),
            new Vector3(-38.39288f, 0f, -23.92859f),
            new Vector3(-38.39288f, 0f, -25.53573f),
            new Vector3(-38.39288f, 0f, -27.14287f),
            new Vector3(-38.39288f, 0f, -28.75002f),
            new Vector3(-38.39288f, 0f, -30.35716f),
            new Vector3(-40.00003f, 0f, 25.89284f),
            new Vector3(-40.00003f, 0f, 24.28569f),
            new Vector3(-40.00003f, 0f, 22.67855f),
            new Vector3(-40.00003f, 0f, 21.07141f),
            new Vector3(-40.00003f, 0f, -22.32145f),
            new Vector3(-40.00003f, 0f, -23.92859f),
            new Vector3(-40.00003f, 0f, -25.53573f),
            new Vector3(-40.00003f, 0f, -27.14287f),
            new Vector3(-41.60717f, 0f, 24.28569f),
            new Vector3(-41.60717f, 0f, 22.67855f),
            new Vector3(-41.60717f, 0f, 21.07141f),
            new Vector3(-41.60717f, 0f, 19.46427f),
            new Vector3(-41.60717f, 0f, 17.85712f),
            new Vector3(-41.60717f, 0f, -19.10716f),
            new Vector3(-41.60717f, 0f, -20.7143f),
            new Vector3(-41.60717f, 0f, -22.32145f),
            new Vector3(-41.60717f, 0f, -23.92859f),
            new Vector3(-41.60717f, 0f, -25.53573f),
            new Vector3(-43.21432f, 0f, 21.07141f),
            new Vector3(-43.21432f, 0f, 19.46427f),
            new Vector3(-43.21432f, 0f, 17.85712f),
            new Vector3(-43.21432f, 0f, 16.24998f),
            new Vector3(-43.21432f, 0f, 14.64284f),
            new Vector3(-43.21432f, 0f, 13.0357f),
            new Vector3(-43.21432f, 0f, -12.67859f),
            new Vector3(-43.21432f, 0f, -14.28573f),
            new Vector3(-43.21432f, 0f, -15.89287f),
            new Vector3(-43.21432f, 0f, -17.50002f),
            new Vector3(-43.21432f, 0f, -19.10716f),
            new Vector3(-43.21432f, 0f, -20.7143f),
            new Vector3(-43.21432f, 0f, -22.32145f),
            new Vector3(-44.82146f, 0f, 17.85712f),
            new Vector3(-44.82146f, 0f, 16.24998f),
            new Vector3(-44.82146f, 0f, 14.64284f),
            new Vector3(-44.82146f, 0f, 13.0357f),
            new Vector3(-44.82146f, 0f, 11.42855f),
            new Vector3(-44.82146f, 0f, 9.821411f),
            new Vector3(-44.82146f, 0f, 8.214269f),
            new Vector3(-44.82146f, 0f, 6.607126f),
            new Vector3(-44.82146f, 0f, 4.999983f),
            new Vector3(-44.82146f, 0f, -6.250018f),
            new Vector3(-44.82146f, 0f, -7.857161f),
            new Vector3(-44.82146f, 0f, -9.464304f),
            new Vector3(-44.82146f, 0f, -11.07145f),
            new Vector3(-44.82146f, 0f, -12.67859f),
            new Vector3(-44.82146f, 0f, -14.28573f),
            new Vector3(-44.82146f, 0f, -15.89287f),
            new Vector3(-44.82146f, 0f, -17.50002f),
            new Vector3(-44.82146f, 0f, -19.10716f),
            new Vector3(-46.4286f, 0f, 14.64284f),
            new Vector3(-46.4286f, 0f, 13.0357f),
            new Vector3(-46.4286f, 0f, 11.42855f),
            new Vector3(-46.4286f, 0f, 9.821411f),
            new Vector3(-46.4286f, 0f, 8.214269f),
            new Vector3(-46.4286f, 0f, 6.607126f),
            new Vector3(-46.4286f, 0f, 4.999983f),
            new Vector3(-46.4286f, 0f, 3.39284f),
            new Vector3(-46.4286f, 0f, 1.785697f),
            new Vector3(-46.4286f, 0f, 0.1785538f),
            new Vector3(-46.4286f, 0f, -1.428589f),
            new Vector3(-46.4286f, 0f, -3.035732f),
            new Vector3(-46.4286f, 0f, -4.642875f),
            new Vector3(-46.4286f, 0f, -6.250018f),
            new Vector3(-46.4286f, 0f, -7.857161f),
            new Vector3(-46.4286f, 0f, -9.464304f),
            new Vector3(-46.4286f, 0f, -11.07145f),
            new Vector3(-46.4286f, 0f, -12.67859f),
            new Vector3(-46.4286f, 0f, -14.28573f),
            new Vector3(-48.03575f, 0f, 8.214269f),
            new Vector3(-48.03575f, 0f, 6.607126f),
            new Vector3(-48.03575f, 0f, 4.999983f),
            new Vector3(-48.03575f, 0f, 3.39284f),
            new Vector3(-48.03575f, 0f, 1.785697f),
            new Vector3(-48.03575f, 0f, 0.1785538f),
            new Vector3(-48.03575f, 0f, -1.428589f),
            new Vector3(-48.03575f, 0f, -3.035732f),
            new Vector3(-48.03575f, 0f, -4.642875f),
            new Vector3(-48.03575f, 0f, -6.250018f),
            new Vector3(-48.03575f, 0f, -7.857161f),
            new Vector3(-48.03575f, 0f, -9.464304f)
        };

        internal class ControllerReceiver : FacepunchBehaviour
        {
            private static PluginConfig _config => _ins._config;

            internal ReceiverConfig Config { get; set; } = null;
            internal List<Vector3> AnimalPositions { get; } = new List<Vector3>();

            private SphereCollider LargeSphereCollider { get; set; } = null;
            private ReceiverSphereCollider SmallSphereCollider { get; set; } = null;

            private VendingMachineMapMarker VendingMarker { get; set; } = null;
            private HashSet<MapMarkerGenericRadius> Markers { get; set; } = new HashSet<MapMarkerGenericRadius>();

            private DroppedItem Computer { get; set; } = null;
            internal BoxStorage Crate { get; set; } = null;
            internal HashSet<BaseEntity> Entities { get; set; } = new HashSet<BaseEntity>();

            internal HashSet<BasePlayer> Players { get; set; } = new HashSet<BasePlayer>();
            private static BasePlayer Owner => _ins.Controller.Owner;

            internal HashSet<BaseAnimalNPC> Animals { get; set; } = new HashSet<BaseAnimalNPC>();

            internal float Health { get; set; } = 0f;

            private Coroutine WaveCoroutine { get; set; } = null;
            private int CurrentWave { get; set; } = 0;
            private bool IsPreparation { get; set; } = false;
            private int Seconds { get; set; } = 0;

            private bool IsAttack => CurrentWave > 0 && CurrentWave <= Config.Waves.Count;

            private static int CurrentStageEvent => _ins.Controller.CurrentStageEvent;

            internal void Init()
            {
                gameObject.layer = 3;
                LargeSphereCollider = gameObject.AddComponent<SphereCollider>();
                LargeSphereCollider.isTrigger = true;
                LargeSphereCollider.radius = LargeReceiverRadius;

                SmallSphereCollider = new GameObject().gameObject.AddComponent<ReceiverSphereCollider>();
                SmallSphereCollider.transform.position = transform.position;
                SmallSphereCollider.Controller = this;
                SmallSphereCollider.Init();

                SpawnEntities();
                Health = Config.Health;

                SpawnMapMarker(_config.Marker);

                InvokeRepeating(InvokeUpdates, 0f, 1f);
                if (Owner != null) Invoke(ClearOwner, _config.OwnerTime);
                Invoke(NoStartScan, _config.ScanWaitTime);
            }

            private void OnDestroy()
            {
                if (WaveCoroutine != null) ServerMgr.Instance.StopCoroutine(WaveCoroutine);

                CancelInvoke(InvokeUpdates);
                CancelInvoke(NoStartScan);
                CancelInvoke(ClearOwner);

                if (LargeSphereCollider != null) Destroy(LargeSphereCollider);
                if (SmallSphereCollider != null) Destroy(SmallSphereCollider.gameObject);

                if (VendingMarker.IsExists()) VendingMarker.Kill();
                foreach (MapMarkerGenericRadius marker in Markers) if (marker.IsExists()) marker.Kill();

                foreach (BasePlayer player in Players) CuiHelper.DestroyUi(player, "Tabs_KpucTaJl");

                ClearAnimals();
                KillEntities(BaseNetworkable.DestroyMode.None);
            }

            private void OnTriggerEnter(Collider other) => EnterPlayer(other.GetComponentInParent<BasePlayer>());

            internal void EnterPlayer(BasePlayer player)
            {
                if (!player.IsPlayer()) return;
                if (Players.Contains(player)) return;
                Players.Add(player);
                Interface.Oxide.CallHook($"OnPlayerEnter{_ins.Name}", player);
                if (_config.IsCreateZonePvp) _ins.AlertToPlayer(player, _ins.GetMessage("EnterPVP", player.UserIDString, _config.Chat.Prefix));
                if (_config.Gui.IsGui) UpdateGui(player, GetProgress());
                _ins.TryDisableSkillTree(player);
            }

            private void OnTriggerExit(Collider other) => ExitPlayer(other.GetComponentInParent<BasePlayer>());

            internal void ExitPlayer(BasePlayer player)
            {
                if (!player.IsPlayer()) return;
                if (!Players.Contains(player)) return;
                Players.Remove(player);
                Interface.Oxide.CallHook($"OnPlayerExit{_ins.Name}", player);
                if (_config.IsCreateZonePvp) _ins.AlertToPlayer(player, _ins.GetMessage("ExitPVP", player.UserIDString, _config.Chat.Prefix));
                if (_config.Gui.IsGui) CuiHelper.DestroyUi(player, "Tabs_KpucTaJl");
                _ins.TryEnableSkillTree(player);
            }

            private void InvokeUpdates()
            {
                int progress = GetProgress();
                if (_config.Gui.IsGui && IsAttack) foreach (BasePlayer player in Players) UpdateGui(player, progress);
                if (_config.Marker.Enabled) UpdateVendingMarker(progress);
                UpdateMarkerForPlayers();
            }

            private void UpdateGui(BasePlayer player, int progress)
            {
                Dictionary<string, string> dic = new Dictionary<string, string> { ["Indicator_KpucTaJl"] = $"{progress} %", ["Plus_KpucTaJl"] = $"{(int)Health} HP" };
                if (Animals.Count > 0) dic.Add("Animal_KpucTaJl", Animals.Count.ToString());
                _ins.CreateTabs(player, dic);
            }

            private void SpawnMapMarker(MarkerConfig config)
            {
                if (!config.Enabled) return;

                MapMarkerGenericRadius background = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", transform.position) as MapMarkerGenericRadius;
                if (background == null) return;
                
                background.Spawn();
                background.radius = config.Type == 0 ? config.Radius : 0.37967f;
                background.alpha = config.Alpha;
                background.color1 = new Color(config.Color.R, config.Color.G, config.Color.B);
                background.color2 = new Color(config.Color.R, config.Color.G, config.Color.B);
                Markers.Add(background);

                if (config.Type == 1)
                {
                    foreach (Vector3 pos in _ins.ReceiverMarker)
                    {
                        MapMarkerGenericRadius marker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", transform.position + pos) as MapMarkerGenericRadius;
                        if (marker == null) continue;
                        
                        marker.Spawn();
                        marker.radius = 0.008f;
                        marker.alpha = 1f;
                        marker.color1 = new Color(config.Color.R, config.Color.G, config.Color.B);
                        marker.color2 = new Color(config.Color.R, config.Color.G, config.Color.B);
                        Markers.Add(marker);
                    }
                }

                VendingMarker = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", transform.position) as VendingMachineMapMarker;
                if (VendingMarker == null) return;
                
                VendingMarker.Spawn();

                UpdateVendingMarker(0);
                UpdateMapMarkers();
            }

            private void UpdateVendingMarker(int progress)
            {
                if (VendingMarker == null || !VendingMarker.IsExists()) return;
                
                VendingMarker.markerShopName = $"{_config.Marker.Text}\n{progress} %";
                VendingMarker.markerShopName += Owner == null ? "\nNo Owner" : $"\n{Owner.displayName}";
                VendingMarker.SendNetworkUpdate();
            }

            internal void UpdateMapMarkers() 
            { 
                foreach (MapMarkerGenericRadius marker in System.Linq.Enumerable.ToList(Markers)) 
                {
                    if (marker != null && marker.IsExists()) 
                        marker.SendUpdate();
                    else if (marker != null && !marker.IsExists())
                        Markers.Remove(marker);
                }
            }

            private void UpdateMarkerForPlayers()
            {
                if (!_config.Point.Enabled) return;
                if (Players.Count == 0 || IsAttack) return;

                HashSet<Vector3> points = new HashSet<Vector3>();
                if (CurrentWave == 0) points.Add(Computer.transform.position);
                if (CurrentWave > Config.Waves.Count && Crate.inventory.itemList.Count > 0) points.Add(Crate.transform.position);

                if (points.Count > 0) foreach (BasePlayer player in Players) foreach (Vector3 point in points) UpdateMarkerForPlayer(player, point, _config.Point);

                points = null;
            }

            private void SpawnEntities()
            {
                foreach (Prefab prefab in _ins.ReceiverPrefabs)
                {
                    Vector3 pos = transform.TransformPoint(prefab.Pos);
                    Quaternion rot = transform.rotation * Quaternion.Euler(prefab.Rot);

                    if (prefab.Path.Contains("targeting_computer.worldmodel"))
                    {
                        Computer = SpawnDroppedItem("targeting.computer", pos, rot, true);
                        continue;
                    }

                    BaseEntity entity = SpawnEntity(prefab.Path, pos, rot);

                    if (entity is BoxStorage)
                    {
                        Crate = entity as BoxStorage;
                        Crate.skinID = 2000024196;
                        Crate.SendNetworkUpdate();
                        Crate.SetFlag(BaseEntity.Flags.Locked, true);
                    }

                    if (entity is Mailbox || entity is RFReceiver) entity.SetFlag(BaseEntity.Flags.Busy, true);

                    Entities.Add(entity);
                }
            }

            private FlasherLight GetFlasherLight()
            {
                foreach (BaseEntity entity in Entities)
                    if (entity is FlasherLight)
                        return entity as FlasherLight;
                return null;
            }

            private AudioAlarm GetAudioAlarm()
            {
                foreach (BaseEntity entity in Entities)
                    if (entity is AudioAlarm)
                        return entity as AudioAlarm;
                return null;
            }

            internal object InteractLaptop(BasePlayer player, Item item)
            {
                if (Computer == null || item != Computer.item) return null;
                if (CurrentWave != 0) return true;

                if (Owner == null)
                {
                    if (_ins.CanTimeOwner(player.userID)) _ins.Controller.SetOwner(player);
                    else
                    {
                        _ins.AlertToPlayer(player, _ins.GetMessage("NoOwnerCooldown", player.UserIDString, _config.Chat.Prefix, GetTimeFormat((int)_ins.GetTimeOwner(player.userID))));
                        return true;
                    }
                }
                else if (!_ins.IsTeam(player.userID, Owner.userID))
                {
                    _ins.AlertToPlayer(player, _ins.GetMessage("NoOwnerStartScan", player.UserIDString, _config.Chat.Prefix));
                    return true;
                }

                CancelInvoke(NoStartScan);
                CancelInvoke(ClearOwner);

                Computer.allowPickup = false;
                GetFlasherLight()?.UpdateFromInput(1, 0);
                GetAudioAlarm()?.UpdateFromInput(1, 0);

                _ins.ActionEconomy(player.userID, "StartScan");

                _ins.AlertToAllPlayers("AllStartScan", _config.Chat.Prefix, player.displayName, CurrentStageEvent);
                _ins.AlertToPlayer(player, _ins.GetMessage("PlayerStartScan", player.UserIDString, _config.Chat.Prefix, CurrentStageEvent));

                WaveCoroutine = ServerMgr.Instance.StartCoroutine(ProcessWave());

                return true;
            }

            private IEnumerator ProcessWave()
            {
                CurrentWave = 1;

                while (CurrentWave <= Config.Waves.Count)
                {
                    WaveConfig wave = Config.Waves.FirstOrDefault(x => x.Level == CurrentWave);

                    IsPreparation = true;
                    Seconds = wave.TimeToStart;
                    while (Seconds > 0)
                    {
                        if (Health <= 0f) yield break;
                        yield return CoroutineEx.waitForSeconds(1f);
                        Seconds--;
                    }
                    IsPreparation = false;

                    int timerAnimal = Seconds = wave.Duration;
                    while (Seconds > 0)
                    {
                        if (Health <= 0f) yield break;
                        if (timerAnimal == Seconds)
                        {
                            timerAnimal = Seconds - wave.TimerAnimal;
                            float wait = 0f;
                            foreach (PresetConfig preset in wave.Presets)
                            {
                                float chance = UnityEngine.Random.Range(0f, 100f);
                                AmountConfig amountConfig = preset.ListAmountConfig.FirstOrDefault(x => chance <= x.Chance);
                                if (amountConfig == null) continue;
                                int count = amountConfig.Count;
                                if (!_config.Presets.ContainsKey(preset.ShortName)) continue;
                                AnimalConfig config = _config.Presets[preset.ShortName];
                                JObject objectConfig = GetObjectConfig(config);
                                for (int i = 0; i < count; i++)
                                {
                                    BaseAnimalNPC animal = (BaseAnimalNPC)_ins.AnimalSpawn.Call("SpawnAnimal", AnimalPositions.GetRandom(), objectConfig);
                                    if (animal != null)
                                    {
                                        Rigidbody rigidBody = animal.gameObject.AddComponent<Rigidbody>();
                                        rigidBody.collisionDetectionMode = CollisionDetectionMode.Discrete;
                                        rigidBody.isKinematic = true;
                                        Animals.Add(animal);
                                    }
                                    yield return CoroutineEx.waitForSeconds(0.01f);
                                }
                                wait += 0.01f * count;
                            }
                            if (wait < 1f) yield return CoroutineEx.waitForSeconds(1f - wait);
                            else if (wait >= 2f) Seconds -= (int)(wait - 1f);
                        }
                        else yield return CoroutineEx.waitForSeconds(1f);
                        Seconds--;
                    }

                    CurrentWave++;
                }

                ClearAnimals();

                GetFlasherLight()?.UpdateFromInput(0, 0);
                GetAudioAlarm()?.UpdateFromInput(0, 0);

                SpawnLoot();

                foreach (BasePlayer player in Players)
                {
                    CuiHelper.DestroyUi(player, "Tabs_KpucTaJl");
                    _ins.AlertToPlayer(player, _ins.GetMessage("FinishScan", player.UserIDString, _config.Chat.Prefix, CurrentStageEvent));
                }

                yield return CoroutineEx.waitForSeconds(_config.PreStartStage);

                _ins.Controller.NextStage();
            }

            private JObject GetObjectConfig(AnimalConfig config)
            {
                return new JObject
                {
                    ["Prefab"] = GetAnimalPrefab(config.Type),
                    ["Health"] = config.Health,
                    ["RoamRange"] = SmallReceiverRadius,
                    ["ChaseRange"] = LargeReceiverRadius,
                    ["SenseRange"] = LargeReceiverRadius / 2f,
                    ["ListenRange"] = LargeReceiverRadius / 4f,
                    ["AttackRange"] = config.AttackRange,
                    ["CheckVisionCone"] = false,
                    ["VisionCone"] = 135f,
                    ["HostileTargetsOnly"] = false,
                    ["AttackDamage"] = config.AttackDamage,
                    ["AttackRate"] = config.AttackRate,
                    ["TurretDamageScale"] = 0f,
                    ["CanRunAwayWater"] = false,
                    ["CanSleep"] = false,
                    ["SleepDistance"] = 100f,
                    ["Speed"] = config.Speed,
                    ["AreaMask"] = 1,
                    ["AgentTypeID"] = -1372625422,
                    ["HomePosition"] = transform.position.ToString(),
                    ["MemoryDuration"] = 10f,
                    ["States"] = new JArray { "RoamState", "ChaseState", "CombatState", "DestroyState" }
                };
            }

            internal static string GetAnimalPrefab(int type)
            {
                if (type == 1) return "assets/rust.ai/agents/bear/polarbear.prefab";
                if (type == 2) return "assets/rust.ai/agents/bear/bear.prefab";
                if (type == 3) return "assets/rust.ai/agents/wolf/wolf.prefab";
                if (type == 4) return "assets/rust.ai/agents/boar/boar.prefab";
                if (type == 5) return "assets/rust.ai/agents/stag/stag.prefab";
                if (type == 6) return "assets/rust.ai/agents/chicken/chicken.prefab";
                return string.Empty;
            }

            private static int GetAnimalType(string prefab)
            {
                if (prefab == "assets/rust.ai/agents/bear/polarbear.prefab") return 1;
                if (prefab == "assets/rust.ai/agents/bear/bear.prefab") return 2;
                if (prefab == "assets/rust.ai/agents/wolf/wolf.prefab") return 3;
                if (prefab == "assets/rust.ai/agents/boar/boar.prefab") return 4;
                if (prefab == "assets/rust.ai/agents/stag/stag.prefab") return 5;
                if (prefab == "assets/rust.ai/agents/chicken/chicken.prefab") return 6;
                return 0;
            }

            internal static string GetAnimalShortname(BaseAnimalNPC animal)
            {
                int type = GetAnimalType(animal.PrefabName);
                float health = animal.startHealth;
                float attackRange = animal.AttackRange;
                float attackDamage = animal.AttackDamage;
                float attackRate = animal.AttackRate;
                float speed = animal.brain.Navigator.Speed;
                foreach (KeyValuePair<string, AnimalConfig> dic in _ins._config.Presets)
                {
                    if (dic.Value.Type != type) continue;
                    if (!dic.Value.Health.IsEqual(health)) continue;
                    if (!dic.Value.AttackRange.IsEqual(attackRange)) continue;
                    if (!dic.Value.AttackDamage.IsEqual(attackDamage)) continue;
                    if (!dic.Value.AttackRate.IsEqual(attackRate)) continue;
                    if (!dic.Value.Speed.IsEqual(speed)) continue;
                    return dic.Key;
                }
                return string.Empty;
            }

            private void SpawnLoot()
            {
                if (Health >= Config.HealthPerBattery)
                {
                    int amount = (int)(Health / Config.HealthPerBattery);
                    Item item = ItemManager.CreateByName("battery.small", amount);
                    if (!item.MoveToContainer(Crate.inventory)) item.Remove();
                }

                if (Config.Loot.TypeLootTable == 1 || Config.Loot.TypeLootTable == 2) _ins.AddToContainerPrefab(Crate.inventory, Config.Loot.PrefabLootTable);
                if (Config.Loot.TypeLootTable == 0 || Config.Loot.TypeLootTable == 2) _ins.AddToContainerItem(Crate.inventory, Config.Loot.OwnLootTable);

                Crate.SetFlag(BaseEntity.Flags.Locked, false);
            }

            internal void DestroyReceiver()
            {
                if (SmallSphereCollider != null) Destroy(SmallSphereCollider.gameObject);
                ClearAnimals();
                Health = 0f;
                KillEntities(BaseNetworkable.DestroyMode.Gib);
                _ins.AlertToAllPlayers("ReceiverKill", _config.Chat.Prefix, CurrentStageEvent);
                _ins.Finish();
            }

            internal bool KillAnimal(BaseAnimalNPC animal)
            {
                if (animal == null || animal.skinID != 11491311214163) return false;
                if (!Animals.Contains(animal)) return false;
                if (SmallSphereCollider != null) SmallSphereCollider.ExitAnimal(animal);
                Animals.Remove(animal);
                return true;
            }

            private void ClearAnimals()
            {
                if (SmallSphereCollider != null) SmallSphereCollider.ClearAllAnimals();
                foreach (BaseAnimalNPC animal in Animals.ToHashSet()) if (animal.IsExists()) animal.Kill();
                Animals.Clear();
            }

            private void KillEntities(BaseNetworkable.DestroyMode destroyMode)
            {
                if (Computer.IsExists()) Computer.Kill(destroyMode);
                foreach (BaseEntity entity in Entities) if (entity.IsExists()) entity.Kill(destroyMode);
                Entities.Clear();
            }

            private static void NoStartScan()
            {
                _ins.AlertToAllPlayers("NotStartScan", _config.Chat.Prefix, CurrentStageEvent);
                _ins.Finish();
            }

            private static void ClearOwner() => _ins.Controller.Owner = null;

            private int GetProgress()
            {
                if (CurrentWave == 0) return 0;
                if (CurrentWave > Config.Waves.Count) return 100;
                int all = 0, current = 0;
                foreach (WaveConfig wave in Config.Waves)
                {
                    all += wave.TimeToStart + wave.Duration;
                    if (wave.Level < CurrentWave) current += wave.TimeToStart + wave.Duration;
                    else if (wave.Level == CurrentWave)
                    {
                        if (IsPreparation) current += wave.TimeToStart - Seconds;
                        else current += wave.TimeToStart + wave.Duration - Seconds;
                    }
                }
                return (int)(current * 100f / all);
            }
        }

        internal class ReceiverSphereCollider : FacepunchBehaviour
        {
            private static PluginConfig _config => _ins._config;

            private SphereCollider SphereCollider { get; set; } = null;
            internal ControllerReceiver Controller { get; set; } = null;
            private HashSet<ulong> Animals { get; } = new HashSet<ulong>();
            private float DamagePerSec { get; set; } = 0f;

            internal void Init()
            {
                gameObject.layer = 3;
                SphereCollider = gameObject.AddComponent<SphereCollider>();
                SphereCollider.isTrigger = true;
                SphereCollider.radius = SmallReceiverRadius;
            }

            private void OnDestroy()
            {
                CancelInvoke(TakeDamage);
                Destroy(SphereCollider);
            }

            private void OnTriggerEnter(Collider other) => EnterAnimal(other.GetComponentInParent<BaseAnimalNPC>());

            private void OnTriggerExit(Collider other) => ExitAnimal(other.GetComponentInParent<BaseAnimalNPC>());

            private void EnterAnimal(BaseAnimalNPC animal)
            {
                if (!animal.IsExists() || animal.net == null) return;
                if (!Controller.Animals.Contains(animal)) return;

                ulong netId = animal.net.ID.Value;
                if (Animals.Contains(netId)) return;
                Animals.Add(netId);

                string shortname = ControllerReceiver.GetAnimalShortname(animal);
                if (string.IsNullOrEmpty(shortname)) return;
                AnimalConfig config = _config.Presets[shortname];

                if (DamagePerSec == 0f) InvokeRepeating(TakeDamage, 1f, 1f);
                DamagePerSec += config.DamagePerSec;
            }

            internal void ExitAnimal(BaseAnimalNPC animal)
            {
                if (!animal.IsExists() || animal.net == null) return;

                ulong netId = animal.net.ID.Value;
                if (!Animals.Contains(netId)) return;
                Animals.Remove(netId);

                string shortname = ControllerReceiver.GetAnimalShortname(animal);
                if (string.IsNullOrEmpty(shortname)) return;
                AnimalConfig config = _config.Presets[shortname];

                DamagePerSec -= config.DamagePerSec;
                if (DamagePerSec <= 0f)
                {
                    DamagePerSec = 0f;
                    CancelInvoke(TakeDamage);
                }
            }

            private void TakeDamage()
            {
                Controller.Health -= DamagePerSec;
                if (Controller.Health <= 0f) Controller.DestroyReceiver();
            }

            internal void ClearAllAnimals()
            {
                CancelInvoke(TakeDamage);
                DamagePerSec = 0f;
                Animals.Clear();
            }
        }
        #endregion Receiver

        #region Drill
        internal HashSet<Prefab> DrillPrefabs { get; } = new HashSet<Prefab>
        {
            new Prefab { Path = "assets/prefabs/deployable/mailbox/mailbox.deployed.prefab", Pos = new Vector3(-0.005f, -0.615f, 0.538f), Rot = new Vector3(0f, 0f, 0f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/batteries/smallrechargablebattery.deployed.prefab", Pos = new Vector3(0.467f, -0.316f, 0.364f), Rot = new Vector3(0f, 180f, 0f) },
            new Prefab { Path = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab", Pos = new Vector3(0.009f, -0.322f, -0.886f), Rot = new Vector3(0f, 180f, 0f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/waterpump/water.pump.deployed.prefab", Pos = new Vector3(-0.02f, 1.065f, -0.057f), Rot = new Vector3(0f, 270f, 0f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/gates/rfreceiver/rfreceiver.prefab", Pos = new Vector3(-0.024f, 1.244f, -0.149f), Rot = new Vector3(0f, 180f, 0f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/teslacoil/teslacoil.deployed.prefab", Pos = new Vector3(-0.006f, 1.474f, -0.007f), Rot = new Vector3(0f, 270f, 180f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/teslacoil/teslacoil.deployed.prefab", Pos = new Vector3(-0.006f, 1.474f, -0.007f), Rot = new Vector3(0f, 90f, 180f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/teslacoil/teslacoil.deployed.prefab", Pos = new Vector3(-0.006f, 1.474f, -0.007f), Rot = new Vector3(0f, 0f, 180f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/teslacoil/teslacoil.deployed.prefab", Pos = new Vector3(-0.006f, 1.474f, -0.007f), Rot = new Vector3(0f, 180f, 180f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/teslacoil/teslacoil.deployed.prefab", Pos = new Vector3(-0.006f, 1.237f, -0.007f), Rot = new Vector3(0f, 270f, 180f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/teslacoil/teslacoil.deployed.prefab", Pos = new Vector3(-0.006f, 1.237f, -0.007f), Rot = new Vector3(0f, 90f, 180f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/teslacoil/teslacoil.deployed.prefab", Pos = new Vector3(-0.006f, 1.237f, -0.007f), Rot = new Vector3(0f, 0f, 180f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/teslacoil/teslacoil.deployed.prefab", Pos = new Vector3(-0.006f, 1.237f, -0.007f), Rot = new Vector3(0f, 180f, 180f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/teslacoil/teslacoil.deployed.prefab", Pos = new Vector3(-0.247f, -0.383f, 0.464f), Rot = new Vector3(0f, 0f, 0f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/teslacoil/teslacoil.deployed.prefab", Pos = new Vector3(0.217f, -0.383f, 0.464f), Rot = new Vector3(0f, 0f, 0f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/teslacoil/teslacoil.deployed.prefab", Pos = new Vector3(0.217f, -0.383f, -0.439f), Rot = new Vector3(0f, 180f, 0f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/teslacoil/teslacoil.deployed.prefab", Pos = new Vector3(-0.247f, -0.383f, -0.439f), Rot = new Vector3(0f, 180f, 0f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/igniter/igniter.deployed.prefab", Pos = new Vector3(0.004f, -0.383f, -0.004f), Rot = new Vector3(0f, 0f, 0f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/lights/flasherlight/electric.flasherlight.deployed.prefab", Pos = new Vector3(-0.015f, 1.464f, 0.259f), Rot = new Vector3(0f, 270f, 270f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/app/storagemonitor/storagemonitor.deployed.prefab", Pos = new Vector3(-0.261f, 0.269f, -0.908f), Rot = new Vector3(0f, 233.459f, 0f) },
            new Prefab { Path = "assets/prefabs/resource/targeting computer/targeting_computer.worldmodel.prefab", Pos = new Vector3(-0.016f, 0.703f, 0.763f), Rot = new Vector3(0f, 180f, 0f) },
            new Prefab { Path = "assets/prefabs/weapons/homingmissilelauncher/homing_missile_launcher.entity.prefab", Pos = new Vector3(0.186f, 0.072f, 0.358f), Rot = new Vector3(335.564f, 12.002f, 13.979f) },
            new Prefab { Path = "assets/prefabs/weapons/homingmissilelauncher/homing_missile_launcher.entity.prefab", Pos = new Vector3(0.186f, 0.668f, 0.358f), Rot = new Vector3(335.564f, 12.002f, 13.979f) },
            new Prefab { Path = "assets/prefabs/weapons/homingmissilelauncher/homing_missile_launcher.entity.prefab", Pos = new Vector3(-0.174f, 0.668f, -0.43f), Rot = new Vector3(335.564f, 192.002f, 13.979f) },
            new Prefab { Path = "assets/prefabs/weapons/homingmissilelauncher/homing_missile_launcher.entity.prefab", Pos = new Vector3(-0.174f, 0.072f, -0.43f), Rot = new Vector3(335.564f, 192.002f, 13.979f) },
            new Prefab { Path = "assets/prefabs/weapons/speargun/speargun.entity.prefab", Pos = new Vector3(0.34f, 0.205f, 0.476f), Rot = new Vector3(90f, 270f, 0f) },
            new Prefab { Path = "assets/prefabs/weapons/speargun/speargun.entity.prefab", Pos = new Vector3(0.34f, 0.769f, 0.476f), Rot = new Vector3(90f, 270f, 0f) },
            new Prefab { Path = "assets/prefabs/weapons/speargun/speargun.entity.prefab", Pos = new Vector3(0.349f, 0.04f, -0.444f), Rot = new Vector3(90f, 270f, 0f) },
            new Prefab { Path = "assets/prefabs/weapons/speargun/speargun.entity.prefab", Pos = new Vector3(0.349f, 0.605f, -0.444f), Rot = new Vector3(90f, 270f, 0f) },
            new Prefab { Path = "assets/prefabs/weapons/speargun/speargun.entity.prefab", Pos = new Vector3(-0.384f, 0.151f, -0.448f), Rot = new Vector3(90f, 90f, 0f) },
            new Prefab { Path = "assets/prefabs/weapons/speargun/speargun.entity.prefab", Pos = new Vector3(-0.384f, 0.715f, -0.448f), Rot = new Vector3(90f, 90f, 0f) },
            new Prefab { Path = "assets/prefabs/weapons/speargun/speargun.entity.prefab", Pos = new Vector3(-0.384f, 0.605f, 0.477f), Rot = new Vector3(90f, 90f, 0f) },
            new Prefab { Path = "assets/prefabs/weapons/speargun/speargun.entity.prefab", Pos = new Vector3(-0.384f, 0.04f, 0.477f), Rot = new Vector3(90f, 90f, 0f) },
            new Prefab { Path = "assets/prefabs/weapons/speargun/speargun.entity.prefab", Pos = new Vector3(0.341f, -0.598f, 0.476f), Rot = new Vector3(90f, 270f, 0f) },
            new Prefab { Path = "assets/prefabs/weapons/chainsaw/chainsaw.entity.prefab", Pos = new Vector3(0.182f, -0.289f, 0.413f), Rot = new Vector3(34.684f, 17.437f, 279.829f) },
            new Prefab { Path = "assets/prefabs/weapons/chainsaw/chainsaw.entity.prefab", Pos = new Vector3(0.182f, 0.417f, 0.413f), Rot = new Vector3(34.684f, 17.437f, 279.829f) },
            new Prefab { Path = "assets/prefabs/weapons/chainsaw/chainsaw.entity.prefab", Pos = new Vector3(-0.22f, 0.417f, 0.413f), Rot = new Vector3(325.316f, 342.563f, 99.829f) },
            new Prefab { Path = "assets/prefabs/weapons/chainsaw/chainsaw.entity.prefab", Pos = new Vector3(-0.22f, 0.949f, 0.413f), Rot = new Vector3(325.316f, 342.563f, 99.829f) },
            new Prefab { Path = "assets/prefabs/weapons/chainsaw/chainsaw.entity.prefab", Pos = new Vector3(0.182f, -0.289f, -0.495f), Rot = new Vector3(34.684f, 17.437f, 279.829f) },
            new Prefab { Path = "assets/prefabs/weapons/chainsaw/chainsaw.entity.prefab", Pos = new Vector3(-0.22f, 0.417f, -0.495f), Rot = new Vector3(325.316f, 342.563f, 99.829f) },
            new Prefab { Path = "assets/prefabs/weapons/chainsaw/chainsaw.entity.prefab", Pos = new Vector3(0.182f, 0.417f, -0.495f), Rot = new Vector3(34.684f, 17.437f, 279.829f) },
            new Prefab { Path = "assets/prefabs/weapons/chainsaw/chainsaw.entity.prefab", Pos = new Vector3(-0.22f, 0.949f, -0.495f), Rot = new Vector3(325.316f, 342.563f, 99.829f) },
            new Prefab { Path = "assets/prefabs/weapons/toolgun/toolgun.entity.prefab", Pos = new Vector3(0.076f, 0.987f, 0.023f), Rot = new Vector3(0f, 90f, 90f) },
            new Prefab { Path = "assets/prefabs/weapons/toolgun/toolgun.entity.prefab", Pos = new Vector3(-0.084f, 0.987f, -0.026f), Rot = new Vector3(0f, 270f, 90f) },
            new Prefab { Path = "assets/prefabs/weapons/toolgun/toolgun.entity.prefab", Pos = new Vector3(0.022f, 0.987f, -0.082f), Rot = new Vector3(0f, 180f, 90f) },
            new Prefab { Path = "assets/prefabs/weapons/toolgun/toolgun.entity.prefab", Pos = new Vector3(-0.026f, 0.987f, 0.078f), Rot = new Vector3(0f, 0f, 90f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/industrialconveyor/industrialconveyor.deployed.prefab", Pos = new Vector3(0.056f, 0.436f, -0.547f), Rot = new Vector3(0f, 180f, 0f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/industrialcrafter/industrialcrafter.deployed.prefab", Pos = new Vector3(0.149f, 0.088f, 0.467f), Rot = new Vector3(0f, 0f, 270f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/industrialcrafter/industrialcrafter.deployed.prefab", Pos = new Vector3(0.149f, 0.656f, 0.467f), Rot = new Vector3(0f, 0f, 270f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/industrialcrafter/industrialcrafter.deployed.prefab", Pos = new Vector3(0.149f, 1.22f, 0.467f), Rot = new Vector3(0f, 0f, 270f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/industrialcrafter/industrialcrafter.deployed.prefab", Pos = new Vector3(-0.181f, 1.222f, 0.467f), Rot = new Vector3(0f, 0f, 90f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/industrialcrafter/industrialcrafter.deployed.prefab", Pos = new Vector3(-0.181f, 0.653f, 0.467f), Rot = new Vector3(0f, 0f, 90f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/industrialcrafter/industrialcrafter.deployed.prefab", Pos = new Vector3(-0.181f, 0.09f, 0.467f), Rot = new Vector3(0f, 0f, 90f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/industrialcrafter/industrialcrafter.deployed.prefab", Pos = new Vector3(0.149f, 0.09f, -0.44f), Rot = new Vector3(0f, 180f, 90f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/industrialcrafter/industrialcrafter.deployed.prefab", Pos = new Vector3(0.149f, 0.653f, -0.44f), Rot = new Vector3(0f, 180f, 90f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/industrialcrafter/industrialcrafter.deployed.prefab", Pos = new Vector3(0.149f, 1.222f, -0.44f), Rot = new Vector3(0f, 180f, 90f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/industrialcrafter/industrialcrafter.deployed.prefab", Pos = new Vector3(-0.183f, 0.088f, -0.44f), Rot = new Vector3(0f, 180f, 270f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/industrialcrafter/industrialcrafter.deployed.prefab", Pos = new Vector3(-0.183f, 0.656f, -0.44f), Rot = new Vector3(0f, 180f, 270f) },
            new Prefab { Path = "assets/prefabs/deployable/playerioents/industrialcrafter/industrialcrafter.deployed.prefab", Pos = new Vector3(-0.183f, 1.22f, -0.44f), Rot = new Vector3(0f, 180f, 270f) }
        };

        internal HashSet<Vector3> DrillMarker { get; } = new HashSet<Vector3>
        {
            new Vector3(46.78571f, 0f, 9.821411f),
            new Vector3(46.78571f, 0f, 8.214269f),
            new Vector3(46.78571f, 0f, 6.607126f),
            new Vector3(46.78571f, 0f, 4.999983f),
            new Vector3(46.78571f, 0f, 3.39284f),
            new Vector3(46.78571f, 0f, 1.785697f),
            new Vector3(46.78571f, 0f, 0.1785538f),
            new Vector3(46.78571f, 0f, -1.428589f),
            new Vector3(46.78571f, 0f, -3.035732f),
            new Vector3(46.78571f, 0f, -4.642875f),
            new Vector3(46.78571f, 0f, -6.250018f),
            new Vector3(46.78571f, 0f, -7.857161f),
            new Vector3(46.78571f, 0f, -9.464304f),
            new Vector3(45.17857f, 0f, 14.64284f),
            new Vector3(45.17857f, 0f, 13.0357f),
            new Vector3(45.17857f, 0f, 11.42855f),
            new Vector3(45.17857f, 0f, 9.821411f),
            new Vector3(45.17857f, 0f, 8.214269f),
            new Vector3(45.17857f, 0f, 6.607126f),
            new Vector3(45.17857f, 0f, 4.999983f),
            new Vector3(45.17857f, 0f, 3.39284f),
            new Vector3(45.17857f, 0f, 1.785697f),
            new Vector3(45.17857f, 0f, 0.1785538f),
            new Vector3(45.17857f, 0f, -1.428589f),
            new Vector3(45.17857f, 0f, -3.035732f),
            new Vector3(45.17857f, 0f, -4.642875f),
            new Vector3(45.17857f, 0f, -6.250018f),
            new Vector3(45.17857f, 0f, -7.857161f),
            new Vector3(45.17857f, 0f, -9.464304f),
            new Vector3(45.17857f, 0f, -11.07145f),
            new Vector3(45.17857f, 0f, -12.67859f),
            new Vector3(45.17857f, 0f, -14.28573f),
            new Vector3(45.17857f, 0f, -15.89287f),
            new Vector3(43.57142f, 0f, 19.46427f),
            new Vector3(43.57142f, 0f, 17.85712f),
            new Vector3(43.57142f, 0f, 16.24998f),
            new Vector3(43.57142f, 0f, 14.64284f),
            new Vector3(43.57142f, 0f, 13.0357f),
            new Vector3(43.57142f, 0f, 11.42855f),
            new Vector3(43.57142f, 0f, 9.821411f),
            new Vector3(43.57142f, 0f, 8.214269f),
            new Vector3(43.57142f, 0f, 6.607126f),
            new Vector3(43.57142f, 0f, -6.250018f),
            new Vector3(43.57142f, 0f, -7.857161f),
            new Vector3(43.57142f, 0f, -9.464304f),
            new Vector3(43.57142f, 0f, -11.07145f),
            new Vector3(43.57142f, 0f, -12.67859f),
            new Vector3(43.57142f, 0f, -14.28573f),
            new Vector3(43.57142f, 0f, -15.89287f),
            new Vector3(43.57142f, 0f, -17.50002f),
            new Vector3(43.57142f, 0f, -19.10716f),
            new Vector3(41.96428f, 0f, 21.07141f),
            new Vector3(41.96428f, 0f, 19.46427f),
            new Vector3(41.96428f, 0f, 17.85712f),
            new Vector3(41.96428f, 0f, 16.24998f),
            new Vector3(41.96428f, 0f, 14.64284f),
            new Vector3(41.96428f, 0f, -14.28573f),
            new Vector3(41.96428f, 0f, -15.89287f),
            new Vector3(41.96428f, 0f, -17.50002f),
            new Vector3(41.96428f, 0f, -19.10716f),
            new Vector3(41.96428f, 0f, -20.7143f),
            new Vector3(41.96428f, 0f, -22.32145f),
            new Vector3(40.35714f, 0f, 24.28569f),
            new Vector3(40.35714f, 0f, 22.67855f),
            new Vector3(40.35714f, 0f, 21.07141f),
            new Vector3(40.35714f, 0f, 19.46427f),
            new Vector3(40.35714f, 0f, -6.250018f),
            new Vector3(40.35714f, 0f, -19.10716f),
            new Vector3(40.35714f, 0f, -20.7143f),
            new Vector3(40.35714f, 0f, -22.32145f),
            new Vector3(40.35714f, 0f, -23.92859f),
            new Vector3(40.35714f, 0f, -25.53573f),
            new Vector3(38.74999f, 0f, 27.49998f),
            new Vector3(38.74999f, 0f, 25.89284f),
            new Vector3(38.74999f, 0f, 24.28569f),
            new Vector3(38.74999f, 0f, 22.67855f),
            new Vector3(38.74999f, 0f, 21.07141f),
            new Vector3(38.74999f, 0f, -1.428589f),
            new Vector3(38.74999f, 0f, -3.035732f),
            new Vector3(38.74999f, 0f, -4.642875f),
            new Vector3(38.74999f, 0f, -6.250018f),
            new Vector3(38.74999f, 0f, -7.857161f),
            new Vector3(38.74999f, 0f, -22.32145f),
            new Vector3(38.74999f, 0f, -23.92859f),
            new Vector3(38.74999f, 0f, -25.53573f),
            new Vector3(38.74999f, 0f, -27.14287f),
            new Vector3(37.14285f, 0f, 29.10713f),
            new Vector3(37.14285f, 0f, 27.49998f),
            new Vector3(37.14285f, 0f, 25.89284f),
            new Vector3(37.14285f, 0f, 24.28569f),
            new Vector3(37.14285f, 0f, 1.785697f),
            new Vector3(37.14285f, 0f, 0.1785538f),
            new Vector3(37.14285f, 0f, -1.428589f),
            new Vector3(37.14285f, 0f, -3.035732f),
            new Vector3(37.14285f, 0f, -4.642875f),
            new Vector3(37.14285f, 0f, -6.250018f),
            new Vector3(37.14285f, 0f, -7.857161f),
            new Vector3(37.14285f, 0f, -25.53573f),
            new Vector3(37.14285f, 0f, -27.14287f),
            new Vector3(37.14285f, 0f, -28.75002f),
            new Vector3(37.14285f, 0f, -30.35716f),
            new Vector3(35.5357f, 0f, 30.71427f),
            new Vector3(35.5357f, 0f, 29.10713f),
            new Vector3(35.5357f, 0f, 27.49998f),
            new Vector3(35.5357f, 0f, 25.89284f),
            new Vector3(35.5357f, 0f, 4.999983f),
            new Vector3(35.5357f, 0f, 3.39284f),
            new Vector3(35.5357f, 0f, 1.785697f),
            new Vector3(35.5357f, 0f, 0.1785538f),
            new Vector3(35.5357f, 0f, -1.428589f),
            new Vector3(35.5357f, 0f, -3.035732f),
            new Vector3(35.5357f, 0f, -4.642875f),
            new Vector3(35.5357f, 0f, -6.250018f),
            new Vector3(35.5357f, 0f, -27.14287f),
            new Vector3(35.5357f, 0f, -28.75002f),
            new Vector3(35.5357f, 0f, -30.35716f),
            new Vector3(35.5357f, 0f, -31.96431f),
            new Vector3(33.92856f, 0f, 32.32141f),
            new Vector3(33.92856f, 0f, 30.71427f),
            new Vector3(33.92856f, 0f, 29.10713f),
            new Vector3(33.92856f, 0f, 8.214269f),
            new Vector3(33.92856f, 0f, 6.607126f),
            new Vector3(33.92856f, 0f, 4.999983f),
            new Vector3(33.92856f, 0f, 3.39284f),
            new Vector3(33.92856f, 0f, 1.785697f),
            new Vector3(33.92856f, 0f, 0.1785538f),
            new Vector3(33.92856f, 0f, -1.428589f),
            new Vector3(33.92856f, 0f, -3.035732f),
            new Vector3(33.92856f, 0f, -4.642875f),
            new Vector3(33.92856f, 0f, -28.75002f),
            new Vector3(33.92856f, 0f, -30.35716f),
            new Vector3(33.92856f, 0f, -31.96431f),
            new Vector3(33.92856f, 0f, -33.57145f),
            new Vector3(32.32141f, 0f, 33.92856f),
            new Vector3(32.32141f, 0f, 32.32141f),
            new Vector3(32.32141f, 0f, 30.71427f),
            new Vector3(32.32141f, 0f, 9.821411f),
            new Vector3(32.32141f, 0f, 8.214269f),
            new Vector3(32.32141f, 0f, 6.607126f),
            new Vector3(32.32141f, 0f, 4.999983f),
            new Vector3(32.32141f, 0f, 3.39284f),
            new Vector3(32.32141f, 0f, 1.785697f),
            new Vector3(32.32141f, 0f, 0.1785538f),
            new Vector3(32.32141f, 0f, -1.428589f),
            new Vector3(32.32141f, 0f, -3.035732f),
            new Vector3(32.32141f, 0f, -31.96431f),
            new Vector3(32.32141f, 0f, -33.57145f),
            new Vector3(32.32141f, 0f, -35.1786f),
            new Vector3(30.71427f, 0f, 35.5357f),
            new Vector3(30.71427f, 0f, 33.92856f),
            new Vector3(30.71427f, 0f, 32.32141f),
            new Vector3(30.71427f, 0f, 11.42855f),
            new Vector3(30.71427f, 0f, 9.821411f),
            new Vector3(30.71427f, 0f, 8.214269f),
            new Vector3(30.71427f, 0f, 6.607126f),
            new Vector3(30.71427f, 0f, 4.999983f),
            new Vector3(30.71427f, 0f, 3.39284f),
            new Vector3(30.71427f, 0f, 1.785697f),
            new Vector3(30.71427f, 0f, 0.1785538f),
            new Vector3(30.71427f, 0f, -1.428589f),
            new Vector3(30.71427f, 0f, -33.57145f),
            new Vector3(30.71427f, 0f, -35.1786f),
            new Vector3(30.71427f, 0f, -36.78574f),
            new Vector3(29.10713f, 0f, 37.14285f),
            new Vector3(29.10713f, 0f, 35.5357f),
            new Vector3(29.10713f, 0f, 33.92856f),
            new Vector3(29.10713f, 0f, 17.85712f),
            new Vector3(29.10713f, 0f, 13.0357f),
            new Vector3(29.10713f, 0f, 11.42855f),
            new Vector3(29.10713f, 0f, 9.821411f),
            new Vector3(29.10713f, 0f, 8.214269f),
            new Vector3(29.10713f, 0f, 4.999983f),
            new Vector3(29.10713f, 0f, 3.39284f),
            new Vector3(29.10713f, 0f, 1.785697f),
            new Vector3(29.10713f, 0f, 0.1785538f),
            new Vector3(29.10713f, 0f, -35.1786f),
            new Vector3(29.10713f, 0f, -36.78574f),
            new Vector3(29.10713f, 0f, -38.39288f),
            new Vector3(27.49998f, 0f, 38.74999f),
            new Vector3(27.49998f, 0f, 37.14285f),
            new Vector3(27.49998f, 0f, 35.5357f),
            new Vector3(27.49998f, 0f, 19.46427f),
            new Vector3(27.49998f, 0f, 17.85712f),
            new Vector3(27.49998f, 0f, 16.24998f),
            new Vector3(27.49998f, 0f, 14.64284f),
            new Vector3(27.49998f, 0f, 13.0357f),
            new Vector3(27.49998f, 0f, 11.42855f),
            new Vector3(27.49998f, 0f, 9.821411f),
            new Vector3(27.49998f, 0f, 6.607126f),
            new Vector3(27.49998f, 0f, 4.999983f),
            new Vector3(27.49998f, 0f, 3.39284f),
            new Vector3(27.49998f, 0f, 1.785697f),
            new Vector3(27.49998f, 0f, -36.78574f),
            new Vector3(27.49998f, 0f, -38.39288f),
            new Vector3(25.89284f, 0f, 38.74999f),
            new Vector3(25.89284f, 0f, 37.14285f),
            new Vector3(25.89284f, 0f, 35.5357f),
            new Vector3(25.89284f, 0f, 21.07141f),
            new Vector3(25.89284f, 0f, 19.46427f),
            new Vector3(25.89284f, 0f, 17.85712f),
            new Vector3(25.89284f, 0f, 16.24998f),
            new Vector3(25.89284f, 0f, 14.64284f),
            new Vector3(25.89284f, 0f, 13.0357f),
            new Vector3(25.89284f, 0f, 11.42855f),
            new Vector3(25.89284f, 0f, 8.214269f),
            new Vector3(25.89284f, 0f, 6.607126f),
            new Vector3(25.89284f, 0f, 4.999983f),
            new Vector3(25.89284f, 0f, 3.39284f),
            new Vector3(25.89284f, 0f, -36.78574f),
            new Vector3(25.89284f, 0f, -38.39288f),
            new Vector3(25.89284f, 0f, -40.00003f),
            new Vector3(24.28569f, 0f, 40.35714f),
            new Vector3(24.28569f, 0f, 38.74999f),
            new Vector3(24.28569f, 0f, 37.14285f),
            new Vector3(24.28569f, 0f, 22.67855f),
            new Vector3(24.28569f, 0f, 21.07141f),
            new Vector3(24.28569f, 0f, 19.46427f),
            new Vector3(24.28569f, 0f, 17.85712f),
            new Vector3(24.28569f, 0f, 16.24998f),
            new Vector3(24.28569f, 0f, 14.64284f),
            new Vector3(24.28569f, 0f, 13.0357f),
            new Vector3(24.28569f, 0f, 9.821411f),
            new Vector3(24.28569f, 0f, 8.214269f),
            new Vector3(24.28569f, 0f, 6.607126f),
            new Vector3(24.28569f, 0f, 4.999983f),
            new Vector3(24.28569f, 0f, -38.39288f),
            new Vector3(24.28569f, 0f, -40.00003f),
            new Vector3(24.28569f, 0f, -41.60717f),
            new Vector3(22.67855f, 0f, 40.35714f),
            new Vector3(22.67855f, 0f, 38.74999f),
            new Vector3(22.67855f, 0f, 24.28569f),
            new Vector3(22.67855f, 0f, 22.67855f),
            new Vector3(22.67855f, 0f, 21.07141f),
            new Vector3(22.67855f, 0f, 19.46427f),
            new Vector3(22.67855f, 0f, 16.24998f),
            new Vector3(22.67855f, 0f, 14.64284f),
            new Vector3(22.67855f, 0f, 13.0357f),
            new Vector3(22.67855f, 0f, 11.42855f),
            new Vector3(22.67855f, 0f, 9.821411f),
            new Vector3(22.67855f, 0f, 8.214269f),
            new Vector3(22.67855f, 0f, 6.607126f),
            new Vector3(22.67855f, 0f, -40.00003f),
            new Vector3(22.67855f, 0f, -41.60717f),
            new Vector3(21.07141f, 0f, 41.96428f),
            new Vector3(21.07141f, 0f, 40.35714f),
            new Vector3(21.07141f, 0f, 38.74999f),
            new Vector3(21.07141f, 0f, 25.89284f),
            new Vector3(21.07141f, 0f, 24.28569f),
            new Vector3(21.07141f, 0f, 22.67855f),
            new Vector3(21.07141f, 0f, 21.07141f),
            new Vector3(21.07141f, 0f, 14.64284f),
            new Vector3(21.07141f, 0f, 13.0357f),
            new Vector3(21.07141f, 0f, 11.42855f),
            new Vector3(21.07141f, 0f, 9.821411f),
            new Vector3(21.07141f, 0f, 8.214269f),
            new Vector3(21.07141f, 0f, -40.00003f),
            new Vector3(21.07141f, 0f, -41.60717f),
            new Vector3(21.07141f, 0f, -43.21432f),
            new Vector3(19.46427f, 0f, 43.57142f),
            new Vector3(19.46427f, 0f, 41.96428f),
            new Vector3(19.46427f, 0f, 40.35714f),
            new Vector3(19.46427f, 0f, 27.49998f),
            new Vector3(19.46427f, 0f, 25.89284f),
            new Vector3(19.46427f, 0f, 24.28569f),
            new Vector3(19.46427f, 0f, 22.67855f),
            new Vector3(19.46427f, 0f, 13.0357f),
            new Vector3(19.46427f, 0f, 11.42855f),
            new Vector3(19.46427f, 0f, 9.821411f),
            new Vector3(19.46427f, 0f, 8.214269f),
            new Vector3(19.46427f, 0f, -41.60717f),
            new Vector3(19.46427f, 0f, -43.21432f),
            new Vector3(17.85712f, 0f, 43.57142f),
            new Vector3(17.85712f, 0f, 41.96428f),
            new Vector3(17.85712f, 0f, 29.10713f),
            new Vector3(17.85712f, 0f, 27.49998f),
            new Vector3(17.85712f, 0f, 25.89284f),
            new Vector3(17.85712f, 0f, 24.28569f),
            new Vector3(17.85712f, 0f, 11.42855f),
            new Vector3(17.85712f, 0f, 9.821411f),
            new Vector3(17.85712f, 0f, 8.214269f),
            new Vector3(17.85712f, 0f, 6.607126f),
            new Vector3(17.85712f, 0f, -41.60717f),
            new Vector3(17.85712f, 0f, -43.21432f),
            new Vector3(17.85712f, 0f, -44.82146f),
            new Vector3(16.24998f, 0f, 43.57142f),
            new Vector3(16.24998f, 0f, 41.96428f),
            new Vector3(16.24998f, 0f, 27.49998f),
            new Vector3(16.24998f, 0f, 25.89284f),
            new Vector3(16.24998f, 0f, 24.28569f),
            new Vector3(16.24998f, 0f, 22.67855f),
            new Vector3(16.24998f, 0f, 9.821411f),
            new Vector3(16.24998f, 0f, 8.214269f),
            new Vector3(16.24998f, 0f, 6.607126f),
            new Vector3(16.24998f, 0f, 4.999983f),
            new Vector3(16.24998f, 0f, -41.60717f),
            new Vector3(16.24998f, 0f, -43.21432f),
            new Vector3(16.24998f, 0f, -44.82146f),
            new Vector3(14.64284f, 0f, 45.17857f),
            new Vector3(14.64284f, 0f, 43.57142f),
            new Vector3(14.64284f, 0f, 41.96428f),
            new Vector3(14.64284f, 0f, 27.49998f),
            new Vector3(14.64284f, 0f, 25.89284f),
            new Vector3(14.64284f, 0f, 24.28569f),
            new Vector3(14.64284f, 0f, 22.67855f),
            new Vector3(14.64284f, 0f, 21.07141f),
            new Vector3(14.64284f, 0f, 9.821411f),
            new Vector3(14.64284f, 0f, 8.214269f),
            new Vector3(14.64284f, 0f, 6.607126f),
            new Vector3(14.64284f, 0f, 4.999983f),
            new Vector3(14.64284f, 0f, -43.21432f),
            new Vector3(14.64284f, 0f, -44.82146f),
            new Vector3(13.0357f, 0f, 45.17857f),
            new Vector3(13.0357f, 0f, 43.57142f),
            new Vector3(13.0357f, 0f, 29.10713f),
            new Vector3(13.0357f, 0f, 27.49998f),
            new Vector3(13.0357f, 0f, 25.89284f),
            new Vector3(13.0357f, 0f, 24.28569f),
            new Vector3(13.0357f, 0f, 22.67855f),
            new Vector3(13.0357f, 0f, 21.07141f),
            new Vector3(13.0357f, 0f, 19.46427f),
            new Vector3(13.0357f, 0f, 11.42855f),
            new Vector3(13.0357f, 0f, 9.821411f),
            new Vector3(13.0357f, 0f, 8.214269f),
            new Vector3(13.0357f, 0f, 6.607126f),
            new Vector3(13.0357f, 0f, -43.21432f),
            new Vector3(13.0357f, 0f, -44.82146f),
            new Vector3(13.0357f, 0f, -46.4286f),
            new Vector3(11.42855f, 0f, 45.17857f),
            new Vector3(11.42855f, 0f, 43.57142f),
            new Vector3(11.42855f, 0f, 30.71427f),
            new Vector3(11.42855f, 0f, 29.10713f),
            new Vector3(11.42855f, 0f, 27.49998f),
            new Vector3(11.42855f, 0f, 25.89284f),
            new Vector3(11.42855f, 0f, 22.67855f),
            new Vector3(11.42855f, 0f, 21.07141f),
            new Vector3(11.42855f, 0f, 19.46427f),
            new Vector3(11.42855f, 0f, 17.85712f),
            new Vector3(11.42855f, 0f, 13.0357f),
            new Vector3(11.42855f, 0f, 11.42855f),
            new Vector3(11.42855f, 0f, 9.821411f),
            new Vector3(11.42855f, 0f, 8.214269f),
            new Vector3(11.42855f, 0f, 6.607126f),
            new Vector3(11.42855f, 0f, 4.999983f),
            new Vector3(11.42855f, 0f, -44.82146f),
            new Vector3(11.42855f, 0f, -46.4286f),
            new Vector3(9.821411f, 0f, 46.78571f),
            new Vector3(9.821411f, 0f, 45.17857f),
            new Vector3(9.821411f, 0f, 43.57142f),
            new Vector3(9.821411f, 0f, 32.32141f),
            new Vector3(9.821411f, 0f, 30.71427f),
            new Vector3(9.821411f, 0f, 29.10713f),
            new Vector3(9.821411f, 0f, 27.49998f),
            new Vector3(9.821411f, 0f, 24.28569f),
            new Vector3(9.821411f, 0f, 22.67855f),
            new Vector3(9.821411f, 0f, 21.07141f),
            new Vector3(9.821411f, 0f, 19.46427f),
            new Vector3(9.821411f, 0f, 17.85712f),
            new Vector3(9.821411f, 0f, 16.24998f),
            new Vector3(9.821411f, 0f, 14.64284f),
            new Vector3(9.821411f, 0f, 13.0357f),
            new Vector3(9.821411f, 0f, 11.42855f),
            new Vector3(9.821411f, 0f, 9.821411f),
            new Vector3(9.821411f, 0f, 8.214269f),
            new Vector3(9.821411f, 0f, 6.607126f),
            new Vector3(9.821411f, 0f, -44.82146f),
            new Vector3(9.821411f, 0f, -46.4286f),
            new Vector3(8.214269f, 0f, 46.78571f),
            new Vector3(8.214269f, 0f, 45.17857f),
            new Vector3(8.214269f, 0f, 43.57142f),
            new Vector3(8.214269f, 0f, 32.32141f),
            new Vector3(8.214269f, 0f, 30.71427f),
            new Vector3(8.214269f, 0f, 29.10713f),
            new Vector3(8.214269f, 0f, 25.89284f),
            new Vector3(8.214269f, 0f, 24.28569f),
            new Vector3(8.214269f, 0f, 22.67855f),
            new Vector3(8.214269f, 0f, 21.07141f),
            new Vector3(8.214269f, 0f, 19.46427f),
            new Vector3(8.214269f, 0f, 17.85712f),
            new Vector3(8.214269f, 0f, 16.24998f),
            new Vector3(8.214269f, 0f, 14.64284f),
            new Vector3(8.214269f, 0f, 13.0357f),
            new Vector3(8.214269f, 0f, 11.42855f),
            new Vector3(8.214269f, 0f, 9.821411f),
            new Vector3(8.214269f, 0f, -44.82146f),
            new Vector3(8.214269f, 0f, -46.4286f),
            new Vector3(6.607126f, 0f, 46.78571f),
            new Vector3(6.607126f, 0f, 45.17857f),
            new Vector3(6.607126f, 0f, 43.57142f),
            new Vector3(6.607126f, 0f, 33.92856f),
            new Vector3(6.607126f, 0f, 32.32141f),
            new Vector3(6.607126f, 0f, 30.71427f),
            new Vector3(6.607126f, 0f, 27.49998f),
            new Vector3(6.607126f, 0f, 25.89284f),
            new Vector3(6.607126f, 0f, 24.28569f),
            new Vector3(6.607126f, 0f, 22.67855f),
            new Vector3(6.607126f, 0f, 17.85712f),
            new Vector3(6.607126f, 0f, 16.24998f),
            new Vector3(6.607126f, 0f, 14.64284f),
            new Vector3(6.607126f, 0f, 13.0357f),
            new Vector3(6.607126f, 0f, 11.42855f),
            new Vector3(6.607126f, 0f, 9.821411f),
            new Vector3(6.607126f, 0f, -44.82146f),
            new Vector3(6.607126f, 0f, -46.4286f),
            new Vector3(6.607126f, 0f, -48.03575f),
            new Vector3(4.999983f, 0f, 46.78571f),
            new Vector3(4.999983f, 0f, 45.17857f),
            new Vector3(4.999983f, 0f, 35.5357f),
            new Vector3(4.999983f, 0f, 33.92856f),
            new Vector3(4.999983f, 0f, 32.32141f),
            new Vector3(4.999983f, 0f, 30.71427f),
            new Vector3(4.999983f, 0f, 29.10713f),
            new Vector3(4.999983f, 0f, 27.49998f),
            new Vector3(4.999983f, 0f, 25.89284f),
            new Vector3(4.999983f, 0f, 24.28569f),
            new Vector3(4.999983f, 0f, 16.24998f),
            new Vector3(4.999983f, 0f, 14.64284f),
            new Vector3(4.999983f, 0f, 11.42855f),
            new Vector3(4.999983f, 0f, 9.821411f),
            new Vector3(4.999983f, 0f, -44.82146f),
            new Vector3(4.999983f, 0f, -46.4286f),
            new Vector3(4.999983f, 0f, -48.03575f),
            new Vector3(3.39284f, 0f, 46.78571f),
            new Vector3(3.39284f, 0f, 45.17857f),
            new Vector3(3.39284f, 0f, 35.5357f),
            new Vector3(3.39284f, 0f, 33.92856f),
            new Vector3(3.39284f, 0f, 32.32141f),
            new Vector3(3.39284f, 0f, 30.71427f),
            new Vector3(3.39284f, 0f, 29.10713f),
            new Vector3(3.39284f, 0f, 27.49998f),
            new Vector3(3.39284f, 0f, 25.89284f),
            new Vector3(3.39284f, 0f, -44.82146f),
            new Vector3(3.39284f, 0f, -46.4286f),
            new Vector3(3.39284f, 0f, -48.03575f),
            new Vector3(1.785697f, 0f, 46.78571f),
            new Vector3(1.785697f, 0f, 45.17857f),
            new Vector3(1.785697f, 0f, 37.14285f),
            new Vector3(1.785697f, 0f, 35.5357f),
            new Vector3(1.785697f, 0f, 33.92856f),
            new Vector3(1.785697f, 0f, 32.32141f),
            new Vector3(1.785697f, 0f, 30.71427f),
            new Vector3(1.785697f, 0f, 29.10713f),
            new Vector3(1.785697f, 0f, 27.49998f),
            new Vector3(1.785697f, 0f, -46.4286f),
            new Vector3(1.785697f, 0f, -48.03575f),
            new Vector3(0.1785538f, 0f, 46.78571f),
            new Vector3(0.1785538f, 0f, 45.17857f),
            new Vector3(0.1785538f, 0f, 37.14285f),
            new Vector3(0.1785538f, 0f, 35.5357f),
            new Vector3(0.1785538f, 0f, 33.92856f),
            new Vector3(0.1785538f, 0f, 32.32141f),
            new Vector3(0.1785538f, 0f, 30.71427f),
            new Vector3(0.1785538f, 0f, 29.10713f),
            new Vector3(0.1785538f, 0f, -46.4286f),
            new Vector3(0.1785538f, 0f, -48.03575f),
            new Vector3(-1.428589f, 0f, 46.78571f),
            new Vector3(-1.428589f, 0f, 45.17857f),
            new Vector3(-1.428589f, 0f, 38.74999f),
            new Vector3(-1.428589f, 0f, 37.14285f),
            new Vector3(-1.428589f, 0f, 35.5357f),
            new Vector3(-1.428589f, 0f, 33.92856f),
            new Vector3(-1.428589f, 0f, 32.32141f),
            new Vector3(-1.428589f, 0f, 30.71427f),
            new Vector3(-1.428589f, 0f, -46.4286f),
            new Vector3(-1.428589f, 0f, -48.03575f),
            new Vector3(-3.035732f, 0f, 46.78571f),
            new Vector3(-3.035732f, 0f, 45.17857f),
            new Vector3(-3.035732f, 0f, 38.74999f),
            new Vector3(-3.035732f, 0f, 37.14285f),
            new Vector3(-3.035732f, 0f, 35.5357f),
            new Vector3(-3.035732f, 0f, 33.92856f),
            new Vector3(-3.035732f, 0f, 32.32141f),
            new Vector3(-3.035732f, 0f, -46.4286f),
            new Vector3(-3.035732f, 0f, -48.03575f),
            new Vector3(-4.642875f, 0f, 46.78571f),
            new Vector3(-4.642875f, 0f, 45.17857f),
            new Vector3(-4.642875f, 0f, 38.74999f),
            new Vector3(-4.642875f, 0f, 37.14285f),
            new Vector3(-4.642875f, 0f, 35.5357f),
            new Vector3(-4.642875f, 0f, 33.92856f),
            new Vector3(-4.642875f, 0f, -44.82146f),
            new Vector3(-4.642875f, 0f, -46.4286f),
            new Vector3(-4.642875f, 0f, -48.03575f),
            new Vector3(-6.250018f, 0f, 46.78571f),
            new Vector3(-6.250018f, 0f, 45.17857f),
            new Vector3(-6.250018f, 0f, 40.35714f),
            new Vector3(-6.250018f, 0f, 38.74999f),
            new Vector3(-6.250018f, 0f, 37.14285f),
            new Vector3(-6.250018f, 0f, 35.5357f),
            new Vector3(-6.250018f, 0f, -11.07145f),
            new Vector3(-6.250018f, 0f, -12.67859f),
            new Vector3(-6.250018f, 0f, -44.82146f),
            new Vector3(-6.250018f, 0f, -46.4286f),
            new Vector3(-6.250018f, 0f, -48.03575f),
            new Vector3(-7.857161f, 0f, 46.78571f),
            new Vector3(-7.857161f, 0f, 45.17857f),
            new Vector3(-7.857161f, 0f, 43.57142f),
            new Vector3(-7.857161f, 0f, 38.74999f),
            new Vector3(-7.857161f, 0f, 37.14285f),
            new Vector3(-7.857161f, 0f, -11.07145f),
            new Vector3(-7.857161f, 0f, -12.67859f),
            new Vector3(-7.857161f, 0f, -14.28573f),
            new Vector3(-7.857161f, 0f, -44.82146f),
            new Vector3(-7.857161f, 0f, -46.4286f),
            new Vector3(-7.857161f, 0f, -48.03575f),
            new Vector3(-9.464304f, 0f, 46.78571f),
            new Vector3(-9.464304f, 0f, 45.17857f),
            new Vector3(-9.464304f, 0f, 43.57142f),
            new Vector3(-9.464304f, 0f, -11.07145f),
            new Vector3(-9.464304f, 0f, -12.67859f),
            new Vector3(-9.464304f, 0f, -14.28573f),
            new Vector3(-9.464304f, 0f, -15.89287f),
            new Vector3(-9.464304f, 0f, -44.82146f),
            new Vector3(-9.464304f, 0f, -46.4286f),
            new Vector3(-11.07145f, 0f, 46.78571f),
            new Vector3(-11.07145f, 0f, 45.17857f),
            new Vector3(-11.07145f, 0f, 43.57142f),
            new Vector3(-11.07145f, 0f, -6.250018f),
            new Vector3(-11.07145f, 0f, -7.857161f),
            new Vector3(-11.07145f, 0f, -9.464304f),
            new Vector3(-11.07145f, 0f, -12.67859f),
            new Vector3(-11.07145f, 0f, -14.28573f),
            new Vector3(-11.07145f, 0f, -15.89287f),
            new Vector3(-11.07145f, 0f, -17.50002f),
            new Vector3(-11.07145f, 0f, -44.82146f),
            new Vector3(-11.07145f, 0f, -46.4286f),
            new Vector3(-12.67859f, 0f, 45.17857f),
            new Vector3(-12.67859f, 0f, 43.57142f),
            new Vector3(-12.67859f, 0f, -6.250018f),
            new Vector3(-12.67859f, 0f, -7.857161f),
            new Vector3(-12.67859f, 0f, -9.464304f),
            new Vector3(-12.67859f, 0f, -11.07145f),
            new Vector3(-12.67859f, 0f, -14.28573f),
            new Vector3(-12.67859f, 0f, -15.89287f),
            new Vector3(-12.67859f, 0f, -17.50002f),
            new Vector3(-12.67859f, 0f, -19.10716f),
            new Vector3(-12.67859f, 0f, -43.21432f),
            new Vector3(-12.67859f, 0f, -44.82146f),
            new Vector3(-12.67859f, 0f, -46.4286f),
            new Vector3(-14.28573f, 0f, 45.17857f),
            new Vector3(-14.28573f, 0f, 43.57142f),
            new Vector3(-14.28573f, 0f, -7.857161f),
            new Vector3(-14.28573f, 0f, -9.464304f),
            new Vector3(-14.28573f, 0f, -11.07145f),
            new Vector3(-14.28573f, 0f, -12.67859f),
            new Vector3(-14.28573f, 0f, -15.89287f),
            new Vector3(-14.28573f, 0f, -17.50002f),
            new Vector3(-14.28573f, 0f, -19.10716f),
            new Vector3(-14.28573f, 0f, -20.7143f),
            new Vector3(-14.28573f, 0f, -43.21432f),
            new Vector3(-14.28573f, 0f, -44.82146f),
            new Vector3(-14.28573f, 0f, -46.4286f),
            new Vector3(-15.89287f, 0f, 45.17857f),
            new Vector3(-15.89287f, 0f, 43.57142f),
            new Vector3(-15.89287f, 0f, 41.96428f),
            new Vector3(-15.89287f, 0f, -9.464304f),
            new Vector3(-15.89287f, 0f, -11.07145f),
            new Vector3(-15.89287f, 0f, -12.67859f),
            new Vector3(-15.89287f, 0f, -14.28573f),
            new Vector3(-15.89287f, 0f, -17.50002f),
            new Vector3(-15.89287f, 0f, -19.10716f),
            new Vector3(-15.89287f, 0f, -20.7143f),
            new Vector3(-15.89287f, 0f, -22.32145f),
            new Vector3(-15.89287f, 0f, -43.21432f),
            new Vector3(-15.89287f, 0f, -44.82146f),
            new Vector3(-17.50002f, 0f, 43.57142f),
            new Vector3(-17.50002f, 0f, 41.96428f),
            new Vector3(-17.50002f, 0f, -11.07145f),
            new Vector3(-17.50002f, 0f, -12.67859f),
            new Vector3(-17.50002f, 0f, -14.28573f),
            new Vector3(-17.50002f, 0f, -15.89287f),
            new Vector3(-17.50002f, 0f, -19.10716f),
            new Vector3(-17.50002f, 0f, -20.7143f),
            new Vector3(-17.50002f, 0f, -22.32145f),
            new Vector3(-17.50002f, 0f, -23.92859f),
            new Vector3(-17.50002f, 0f, -41.60717f),
            new Vector3(-17.50002f, 0f, -43.21432f),
            new Vector3(-17.50002f, 0f, -44.82146f),
            new Vector3(-19.10716f, 0f, 43.57142f),
            new Vector3(-19.10716f, 0f, 41.96428f),
            new Vector3(-19.10716f, 0f, 40.35714f),
            new Vector3(-19.10716f, 0f, -12.67859f),
            new Vector3(-19.10716f, 0f, -14.28573f),
            new Vector3(-19.10716f, 0f, -15.89287f),
            new Vector3(-19.10716f, 0f, -17.50002f),
            new Vector3(-19.10716f, 0f, -20.7143f),
            new Vector3(-19.10716f, 0f, -22.32145f),
            new Vector3(-19.10716f, 0f, -23.92859f),
            new Vector3(-19.10716f, 0f, -25.53573f),
            new Vector3(-19.10716f, 0f, -41.60717f),
            new Vector3(-19.10716f, 0f, -43.21432f),
            new Vector3(-20.7143f, 0f, 41.96428f),
            new Vector3(-20.7143f, 0f, 40.35714f),
            new Vector3(-20.7143f, 0f, -14.28573f),
            new Vector3(-20.7143f, 0f, -15.89287f),
            new Vector3(-20.7143f, 0f, -17.50002f),
            new Vector3(-20.7143f, 0f, -19.10716f),
            new Vector3(-20.7143f, 0f, -22.32145f),
            new Vector3(-20.7143f, 0f, -23.92859f),
            new Vector3(-20.7143f, 0f, -25.53573f),
            new Vector3(-20.7143f, 0f, -27.14287f),
            new Vector3(-20.7143f, 0f, -41.60717f),
            new Vector3(-20.7143f, 0f, -43.21432f),
            new Vector3(-22.32145f, 0f, 41.96428f),
            new Vector3(-22.32145f, 0f, 40.35714f),
            new Vector3(-22.32145f, 0f, 38.74999f),
            new Vector3(-22.32145f, 0f, -15.89287f),
            new Vector3(-22.32145f, 0f, -17.50002f),
            new Vector3(-22.32145f, 0f, -19.10716f),
            new Vector3(-22.32145f, 0f, -20.7143f),
            new Vector3(-22.32145f, 0f, -23.92859f),
            new Vector3(-22.32145f, 0f, -25.53573f),
            new Vector3(-22.32145f, 0f, -27.14287f),
            new Vector3(-22.32145f, 0f, -28.75002f),
            new Vector3(-22.32145f, 0f, -40.00003f),
            new Vector3(-22.32145f, 0f, -41.60717f),
            new Vector3(-22.32145f, 0f, -43.21432f),
            new Vector3(-23.92859f, 0f, 40.35714f),
            new Vector3(-23.92859f, 0f, 38.74999f),
            new Vector3(-23.92859f, 0f, -17.50002f),
            new Vector3(-23.92859f, 0f, -19.10716f),
            new Vector3(-23.92859f, 0f, -20.7143f),
            new Vector3(-23.92859f, 0f, -22.32145f),
            new Vector3(-23.92859f, 0f, -25.53573f),
            new Vector3(-23.92859f, 0f, -27.14287f),
            new Vector3(-23.92859f, 0f, -28.75002f),
            new Vector3(-23.92859f, 0f, -30.35716f),
            new Vector3(-23.92859f, 0f, -38.39288f),
            new Vector3(-23.92859f, 0f, -40.00003f),
            new Vector3(-23.92859f, 0f, -41.60717f),
            new Vector3(-25.53573f, 0f, 40.35714f),
            new Vector3(-25.53573f, 0f, 38.74999f),
            new Vector3(-25.53573f, 0f, 37.14285f),
            new Vector3(-25.53573f, 0f, -19.10716f),
            new Vector3(-25.53573f, 0f, -20.7143f),
            new Vector3(-25.53573f, 0f, -22.32145f),
            new Vector3(-25.53573f, 0f, -23.92859f),
            new Vector3(-25.53573f, 0f, -25.53573f),
            new Vector3(-25.53573f, 0f, -27.14287f),
            new Vector3(-25.53573f, 0f, -28.75002f),
            new Vector3(-25.53573f, 0f, -30.35716f),
            new Vector3(-25.53573f, 0f, -38.39288f),
            new Vector3(-25.53573f, 0f, -40.00003f),
            new Vector3(-25.53573f, 0f, -41.60717f),
            new Vector3(-27.14287f, 0f, 38.74999f),
            new Vector3(-27.14287f, 0f, 37.14285f),
            new Vector3(-27.14287f, 0f, 35.5357f),
            new Vector3(-27.14287f, 0f, -20.7143f),
            new Vector3(-27.14287f, 0f, -22.32145f),
            new Vector3(-27.14287f, 0f, -23.92859f),
            new Vector3(-27.14287f, 0f, -25.53573f),
            new Vector3(-27.14287f, 0f, -27.14287f),
            new Vector3(-27.14287f, 0f, -28.75002f),
            new Vector3(-27.14287f, 0f, -36.78574f),
            new Vector3(-27.14287f, 0f, -38.39288f),
            new Vector3(-27.14287f, 0f, -40.00003f),
            new Vector3(-28.75002f, 0f, 38.74999f),
            new Vector3(-28.75002f, 0f, 37.14285f),
            new Vector3(-28.75002f, 0f, 35.5357f),
            new Vector3(-28.75002f, 0f, 33.92856f),
            new Vector3(-28.75002f, 0f, -22.32145f),
            new Vector3(-28.75002f, 0f, -23.92859f),
            new Vector3(-28.75002f, 0f, -25.53573f),
            new Vector3(-28.75002f, 0f, -27.14287f),
            new Vector3(-28.75002f, 0f, -28.75002f),
            new Vector3(-28.75002f, 0f, -35.1786f),
            new Vector3(-28.75002f, 0f, -36.78574f),
            new Vector3(-28.75002f, 0f, -38.39288f),
            new Vector3(-30.35716f, 0f, 37.14285f),
            new Vector3(-30.35716f, 0f, 35.5357f),
            new Vector3(-30.35716f, 0f, 33.92856f),
            new Vector3(-30.35716f, 0f, -23.92859f),
            new Vector3(-30.35716f, 0f, -25.53573f),
            new Vector3(-30.35716f, 0f, -33.57145f),
            new Vector3(-30.35716f, 0f, -35.1786f),
            new Vector3(-30.35716f, 0f, -36.78574f),
            new Vector3(-31.96431f, 0f, 35.5357f),
            new Vector3(-31.96431f, 0f, 33.92856f),
            new Vector3(-31.96431f, 0f, 32.32141f),
            new Vector3(-31.96431f, 0f, -31.96431f),
            new Vector3(-31.96431f, 0f, -33.57145f),
            new Vector3(-31.96431f, 0f, -35.1786f),
            new Vector3(-31.96431f, 0f, -36.78574f),
            new Vector3(-33.57145f, 0f, 33.92856f),
            new Vector3(-33.57145f, 0f, 32.32141f),
            new Vector3(-33.57145f, 0f, 30.71427f),
            new Vector3(-33.57145f, 0f, -30.35716f),
            new Vector3(-33.57145f, 0f, -31.96431f),
            new Vector3(-33.57145f, 0f, -33.57145f),
            new Vector3(-33.57145f, 0f, -35.1786f),
            new Vector3(-35.1786f, 0f, 32.32141f),
            new Vector3(-35.1786f, 0f, 30.71427f),
            new Vector3(-35.1786f, 0f, 29.10713f),
            new Vector3(-35.1786f, 0f, -28.75002f),
            new Vector3(-35.1786f, 0f, -30.35716f),
            new Vector3(-35.1786f, 0f, -31.96431f),
            new Vector3(-35.1786f, 0f, -33.57145f),
            new Vector3(-36.78574f, 0f, 30.71427f),
            new Vector3(-36.78574f, 0f, 29.10713f),
            new Vector3(-36.78574f, 0f, 27.49998f),
            new Vector3(-36.78574f, 0f, 25.89284f),
            new Vector3(-36.78574f, 0f, -27.14287f),
            new Vector3(-36.78574f, 0f, -28.75002f),
            new Vector3(-36.78574f, 0f, -30.35716f),
            new Vector3(-36.78574f, 0f, -31.96431f),
            new Vector3(-38.39288f, 0f, 29.10713f),
            new Vector3(-38.39288f, 0f, 27.49998f),
            new Vector3(-38.39288f, 0f, 25.89284f),
            new Vector3(-38.39288f, 0f, 24.28569f),
            new Vector3(-38.39288f, 0f, -23.92859f),
            new Vector3(-38.39288f, 0f, -25.53573f),
            new Vector3(-38.39288f, 0f, -27.14287f),
            new Vector3(-38.39288f, 0f, -28.75002f),
            new Vector3(-40.00003f, 0f, 25.89284f),
            new Vector3(-40.00003f, 0f, 24.28569f),
            new Vector3(-40.00003f, 0f, 22.67855f),
            new Vector3(-40.00003f, 0f, 21.07141f),
            new Vector3(-40.00003f, 0f, -22.32145f),
            new Vector3(-40.00003f, 0f, -23.92859f),
            new Vector3(-40.00003f, 0f, -25.53573f),
            new Vector3(-40.00003f, 0f, -27.14287f),
            new Vector3(-41.60717f, 0f, 24.28569f),
            new Vector3(-41.60717f, 0f, 22.67855f),
            new Vector3(-41.60717f, 0f, 21.07141f),
            new Vector3(-41.60717f, 0f, 19.46427f),
            new Vector3(-41.60717f, 0f, 17.85712f),
            new Vector3(-41.60717f, 0f, -17.50002f),
            new Vector3(-41.60717f, 0f, -19.10716f),
            new Vector3(-41.60717f, 0f, -20.7143f),
            new Vector3(-41.60717f, 0f, -22.32145f),
            new Vector3(-41.60717f, 0f, -23.92859f),
            new Vector3(-41.60717f, 0f, -25.53573f),
            new Vector3(-43.21432f, 0f, 21.07141f),
            new Vector3(-43.21432f, 0f, 19.46427f),
            new Vector3(-43.21432f, 0f, 17.85712f),
            new Vector3(-43.21432f, 0f, 16.24998f),
            new Vector3(-43.21432f, 0f, 14.64284f),
            new Vector3(-43.21432f, 0f, 13.0357f),
            new Vector3(-43.21432f, 0f, -12.67859f),
            new Vector3(-43.21432f, 0f, -14.28573f),
            new Vector3(-43.21432f, 0f, -15.89287f),
            new Vector3(-43.21432f, 0f, -17.50002f),
            new Vector3(-43.21432f, 0f, -19.10716f),
            new Vector3(-43.21432f, 0f, -20.7143f),
            new Vector3(-43.21432f, 0f, -22.32145f),
            new Vector3(-44.82146f, 0f, 17.85712f),
            new Vector3(-44.82146f, 0f, 16.24998f),
            new Vector3(-44.82146f, 0f, 14.64284f),
            new Vector3(-44.82146f, 0f, 13.0357f),
            new Vector3(-44.82146f, 0f, 11.42855f),
            new Vector3(-44.82146f, 0f, 9.821411f),
            new Vector3(-44.82146f, 0f, 8.214269f),
            new Vector3(-44.82146f, 0f, 6.607126f),
            new Vector3(-44.82146f, 0f, 4.999983f),
            new Vector3(-44.82146f, 0f, 3.39284f),
            new Vector3(-44.82146f, 0f, -4.642875f),
            new Vector3(-44.82146f, 0f, -6.250018f),
            new Vector3(-44.82146f, 0f, -7.857161f),
            new Vector3(-44.82146f, 0f, -9.464304f),
            new Vector3(-44.82146f, 0f, -11.07145f),
            new Vector3(-44.82146f, 0f, -12.67859f),
            new Vector3(-44.82146f, 0f, -14.28573f),
            new Vector3(-44.82146f, 0f, -15.89287f),
            new Vector3(-44.82146f, 0f, -17.50002f),
            new Vector3(-46.4286f, 0f, 14.64284f),
            new Vector3(-46.4286f, 0f, 13.0357f),
            new Vector3(-46.4286f, 0f, 11.42855f),
            new Vector3(-46.4286f, 0f, 9.821411f),
            new Vector3(-46.4286f, 0f, 8.214269f),
            new Vector3(-46.4286f, 0f, 6.607126f),
            new Vector3(-46.4286f, 0f, 4.999983f),
            new Vector3(-46.4286f, 0f, 3.39284f),
            new Vector3(-46.4286f, 0f, 1.785697f),
            new Vector3(-46.4286f, 0f, 0.1785538f),
            new Vector3(-46.4286f, 0f, -1.428589f),
            new Vector3(-46.4286f, 0f, -3.035732f),
            new Vector3(-46.4286f, 0f, -4.642875f),
            new Vector3(-46.4286f, 0f, -6.250018f),
            new Vector3(-46.4286f, 0f, -7.857161f),
            new Vector3(-46.4286f, 0f, -9.464304f),
            new Vector3(-46.4286f, 0f, -11.07145f),
            new Vector3(-46.4286f, 0f, -12.67859f),
            new Vector3(-46.4286f, 0f, -14.28573f),
            new Vector3(-48.03575f, 0f, 8.214269f),
            new Vector3(-48.03575f, 0f, 6.607126f),
            new Vector3(-48.03575f, 0f, 4.999983f),
            new Vector3(-48.03575f, 0f, 3.39284f),
            new Vector3(-48.03575f, 0f, 1.785697f),
            new Vector3(-48.03575f, 0f, 0.1785538f),
            new Vector3(-48.03575f, 0f, -1.428589f),
            new Vector3(-48.03575f, 0f, -3.035732f),
            new Vector3(-48.03575f, 0f, -4.642875f),
            new Vector3(-48.03575f, 0f, -6.250018f),
            new Vector3(-48.03575f, 0f, -7.857161f)
        };

        internal class ControllerDrill : FacepunchBehaviour
        {
            private static PluginConfig _config => _ins._config;

            private DrillConfig Config { get; set; } = null;

            private SphereCollider SphereCollider { get; set; } = null;

            private VendingMachineMapMarker VendingMarker { get; set; } = null;
            private HashSet<MapMarkerGenericRadius> Markers { get; } = new HashSet<MapMarkerGenericRadius>();

            private DroppedItem Computer { get; set; } = null;
            private Igniter Igniter { get; set; } = null;
            private FlasherLight Light { get; set; } = null;
            private WaterPump Pump { get; set; } = null;
            internal Mailbox InputCrate { get; set; } = null;
            internal BoxStorage OutputCrate { get; set; } = null;
            private IndustrialConveyor Conveyor { get; set; } = null;
            private HashSet<IndustrialCrafter> Crafters { get; } = new HashSet<IndustrialCrafter>();
            private HashSet<Toolgun> Toolguns { get; } = new HashSet<Toolgun>();
            internal HashSet<BaseEntity> Entities { get; } = new HashSet<BaseEntity>();

            internal HashSet<BasePlayer> Players { get; } = new HashSet<BasePlayer>();
            private static BasePlayer Owner => _ins.Controller.Owner;

            private int CountBatteryPerCycle { get; set; } = 0;
            private float CycleTimeFloat { get; set; } = 0f;
            private int RemainingWorkingTime { get; set; } = 0;
            private int AllWorkingTime { get; set; } = 0;
            private int Seconds { get; set; } = 0;

            internal void Init()
            {
                Config = _config.Drill;

                CountBatteryPerCycle = GetCountBatteryPerCycle();
                CycleTimeFloat = Convert.ToSingle(Config.CycleTime);
                Seconds = Config.LifeTime;

                gameObject.layer = 3;
                SphereCollider = gameObject.AddComponent<SphereCollider>();
                SphereCollider.isTrigger = true;
                SphereCollider.radius = LargeReceiverRadius;

                SpawnEntities();

                SpawnMapMarker(_config.Marker);

                InvokeRepeating(InvokeUpdates, 0f, 1f);
            }

            private void OnDestroy()
            {
                CancelInvoke(InvokeUpdates);
                CancelInvoke(SpawnLootCycle);
                CancelInvoke(EffectSpawn);

                if (SphereCollider != null) Destroy(SphereCollider);

                if (VendingMarker.IsExists()) VendingMarker.Kill();
                foreach (MapMarkerGenericRadius marker in Markers) if (marker.IsExists()) marker.Kill();

                foreach (BasePlayer player in Players) CuiHelper.DestroyUi(player, "Tabs_KpucTaJl");

                foreach (BaseEntity entity in Entities) if (entity.IsExists()) entity.Kill();
            }

            private void OnTriggerEnter(Collider other) => EnterPlayer(other.GetComponentInParent<BasePlayer>());

            internal void EnterPlayer(BasePlayer player)
            {
                if (!player.IsPlayer()) return;
                if (Players.Contains(player)) return;
                Players.Add(player);
                Interface.Oxide.CallHook($"OnPlayerEnter{_ins.Name}", player);
                if (_config.IsCreateZonePvp) _ins.AlertToPlayer(player, _ins.GetMessage("EnterPVP", player.UserIDString, _config.Chat.Prefix));
                if (_config.Gui.IsGui) UpdateGui(player);
            }

            private void OnTriggerExit(Collider other) => ExitPlayer(other.GetComponentInParent<BasePlayer>());

            internal void ExitPlayer(BasePlayer player)
            {
                if (!player.IsPlayer()) return;
                if (!Players.Contains(player)) return;
                Players.Remove(player);
                Interface.Oxide.CallHook($"OnPlayerExit{_ins.Name}", player);
                if (_config.IsCreateZonePvp) _ins.AlertToPlayer(player, _ins.GetMessage("ExitPVP", player.UserIDString, _config.Chat.Prefix));
                if (_config.Gui.IsGui) CuiHelper.DestroyUi(player, "Tabs_KpucTaJl");
            }

            private void InvokeUpdates()
            {
                if (_config.Gui.IsGui) foreach (BasePlayer player in Players) UpdateGui(player);
                if (_config.Marker.Enabled) UpdateVendingMarker();
                UpdateMarkerForPlayers();
                Seconds--;
                if (Seconds == 0)
                {
                    CancelInvoke(InvokeUpdates);
                    _ins.Finish();
                }
            }

            private void UpdateGui(BasePlayer player) => _ins.CreateTabs(player, new Dictionary<string, string> { ["Clock_KpucTaJl"] = GetTimeFormat(Seconds), ["Plus_KpucTaJl"] = $"{GetHealth()} %" });

            private void SpawnMapMarker(MarkerConfig config)
            {
                if (!config.Enabled) return;

                MapMarkerGenericRadius background = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", transform.position) as MapMarkerGenericRadius;
                if (background == null) return;
                
                background.Spawn();
                background.radius = config.Type == 0 ? config.Radius : 0.37967f;
                background.alpha = config.Alpha;
                background.color1 = new Color(config.Color.R, config.Color.G, config.Color.B);
                background.color2 = new Color(config.Color.R, config.Color.G, config.Color.B);
                Markers.Add(background);

                if (config.Type == 1)
                {
                    foreach (Vector3 pos in _ins.DrillMarker)
                    {
                        MapMarkerGenericRadius marker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", transform.position + pos) as MapMarkerGenericRadius;
                        if (marker == null) continue;
                        
                        marker.Spawn();
                        marker.radius = 0.008f;
                        marker.alpha = 1f;
                        marker.color1 = new Color(config.Color.R, config.Color.G, config.Color.B);
                        marker.color2 = new Color(config.Color.R, config.Color.G, config.Color.B);
                        Markers.Add(marker);
                    }
                }

                VendingMarker = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", transform.position) as VendingMachineMapMarker;
                if (VendingMarker == null) return;
                
                VendingMarker.Spawn();

                UpdateVendingMarker();
                UpdateMapMarkers();
            }

            private void UpdateVendingMarker()
            {
                if (VendingMarker == null || !VendingMarker.IsExists()) return;
                
                VendingMarker.markerShopName = $"{_config.Marker.Text}\n{GetTimeFormat(Seconds)}\n{GetHealth()} %";
                if (!_config.SwitchDrill || !_config.LootCrate) VendingMarker.markerShopName += Owner == null ? "\nNo Owner" : $"\n{Owner.displayName}";
                VendingMarker.SendNetworkUpdate();
            }

            internal void UpdateMapMarkers() 
            { 
                foreach (MapMarkerGenericRadius marker in System.Linq.Enumerable.ToList(Markers)) 
                {
                    if (marker != null && marker.IsExists()) 
                        marker.SendUpdate();
                    else if (marker != null && !marker.IsExists())
                        Markers.Remove(marker);
                }
            }

            private void UpdateMarkerForPlayers()
            {
                if (!_config.Point.Enabled) return;
                if (Players.Count == 0) return;

                HashSet<Vector3> points = new HashSet<Vector3>();
                if (!IsOn) points.Add(Computer.transform.position);
                if (OutputCrate.inventory.itemList.Count > 0) points.Add(OutputCrate.transform.position);

                if (points.Count > 0) foreach (BasePlayer player in Players) foreach (Vector3 point in points) UpdateMarkerForPlayer(player, point, _config.Point);

                points = null;
            }

            private void SpawnEntities()
            {
                foreach (Prefab prefab in _ins.DrillPrefabs)
                {
                    Vector3 pos = transform.TransformPoint(prefab.Pos);
                    Quaternion rot = transform.rotation * Quaternion.Euler(prefab.Rot);

                    if (prefab.Path.Contains("targeting_computer.worldmodel"))
                    {
                        Computer = SpawnDroppedItem("targeting.computer", pos, rot, true);
                        Entities.Add(Computer);
                        continue;
                    }

                    BaseEntity entity = SpawnEntity(prefab.Path, pos, rot);

                    if (entity is Igniter) Igniter = entity as Igniter;
                    if (entity is FlasherLight) Light = entity as FlasherLight;
                    if (entity is WaterPump)
                    {
                        Pump = entity as WaterPump;
                        Pump.SetFlag(BaseEntity.Flags.Busy, true);
                    }
                    if (entity is Mailbox)
                    {
                        InputCrate = entity as Mailbox;
                        InputCrate.allowedItems = new[] { ItemManager.FindItemDefinition("battery.small") };
                        InputCrate._inventory.SetOnlyAllowedItems(InputCrate.allowedItems);
                    }
                    if (entity is BoxStorage)
                    {
                        OutputCrate = entity as BoxStorage;
                        OutputCrate.skinID = 2946492931;
                        OutputCrate.SendNetworkUpdate();
                    }
                    if (entity is IndustrialCrafter)
                    {
                        IndustrialCrafter crafter = entity as IndustrialCrafter;
                        crafter.SetFlag(BaseEntity.Flags.Busy, true);
                        Crafters.Add(crafter);
                    }
                    if (entity is IndustrialConveyor)
                    {
                        Conveyor = entity as IndustrialConveyor;
                        Conveyor.SetFlag(BaseEntity.Flags.Busy, true);
                    }
                    if (entity is RFReceiver) entity.SetFlag(BaseEntity.Flags.Busy, true);
                    if (entity is Toolgun)
                    {
                        Toolgun toolgun = entity as Toolgun;
                        Toolguns.Add(toolgun);
                    }

                    Entities.Add(entity);
                }
            }

            internal object InteractLaptop(BasePlayer player, Item item)
            {
                if (Computer == null || item != Computer.item) return null;

                if (!_config.SwitchDrill && Owner != null && !_ins.IsTeam(player.userID, Owner.userID))
                {
                    _ins.AlertToPlayer(player, _ins.GetMessage("NoOwnerSwitchDrill", player.UserIDString, _config.Chat.Prefix));
                    return true;
                }

                if (IsOn) SwitchOff();
                else
                {
                    if (IsProcess) SwitchOn();
                    else
                    {
                        if (AllWorkingTime + Config.CycleTime > Config.MaxWorkingTime)
                        {
                            _ins.AlertToPlayer(player, _ins.GetMessage("NoSwitchDrillMinHealth", player.UserIDString, _config.Chat.Prefix));
                            return true;
                        }
                        UpdateRemainingWorkingTime();
                        if (RemainingWorkingTime < Config.CycleTime)
                        {
                            _ins.AlertToPlayer(player, _ins.GetMessage("NoSwitchDrillNoBattery", player.UserIDString, _config.Chat.Prefix));
                            return true;
                        }
                        SwitchOn();
                        StartDrill();
                    }
                }

                return true;
            }

            private bool IsOn => Light.HasFlag(BaseEntity.Flags.Reserved8);

            private bool IsProcess => Igniter.HasFlag(BaseEntity.Flags.Reserved8);

            private void SwitchOn()
            {
                if (IsOn) return;
                Light.UpdateFromInput(1, 0);
                Pump.UpdateFromInput(5, 0);
            }

            private void SwitchOff()
            {
                if (!IsOn) return;
                Light.UpdateFromInput(0, 0);
                Pump.UpdateFromInput(0, 0);
            }

            private void StartDrill()
            {
                Igniter.UpdateFromInput(2, 0);
                foreach (IndustrialCrafter crafter in Crafters)
                {
                    crafter.SetFlag(BaseEntity.Flags.On, true);
                    crafter.SetFlag(BaseEntity.Flags.Reserved1, true);
                    ClientUpdateCraftTimeRemaining(crafter, CycleTimeFloat);
                }
                InvokeRepeating(EffectSpawn, 0f, 1f);
                Invoke(SpawnLootCycle, CycleTimeFloat);
            }

            private void StopDrill()
            {
                Igniter.UpdateFromInput(0, 0);
                foreach (IndustrialCrafter crafter in Crafters)
                {
                    crafter.SetFlag(BaseEntity.Flags.On, false);
                    crafter.SetFlag(BaseEntity.Flags.Reserved1, false);
                    ClientUpdateCraftTimeRemaining(crafter, 0f);
                }
                CancelInvoke(EffectSpawn);
            }

            private void EffectSpawn() { foreach (Toolgun toolgun in Toolguns) toolgun.ClientRPC<Vector3, Vector3>(null, "EffectSpawn", Igniter.transform.position, Vector3.up); }

            private static void ClientUpdateCraftTimeRemaining(IndustrialCrafter crafter, float time) => crafter.ClientRPC<float, float>(null, "ClientUpdateCraftTimeRemaining", time, time);

            private void SpawnLootCycle()
            {
                if (Config.Loot.TypeLootTable == 1 || Config.Loot.TypeLootTable == 2) _ins.AddToContainerPrefab(OutputCrate.inventory, Config.Loot.PrefabLootTable);
                if (Config.Loot.TypeLootTable == 0 || Config.Loot.TypeLootTable == 2) _ins.AddToContainerItem(OutputCrate.inventory, Config.Loot.OwnLootTable);

                AllWorkingTime += Config.CycleTime;
                RemainingWorkingTime -= Config.CycleTime;

                if (!IsOn)
                {
                    StopDrill();
                    return;
                }

                if (AllWorkingTime + Config.CycleTime > Config.MaxWorkingTime)
                {
                    SwitchOff();
                    StopDrill();
                    return;
                }

                UpdateRemainingWorkingTime();
                if (RemainingWorkingTime < Config.CycleTime)
                {
                    SwitchOff();
                    StopDrill();
                    return;
                }

                foreach (IndustrialCrafter crafter in Crafters) ClientUpdateCraftTimeRemaining(crafter, CycleTimeFloat);
                Invoke(SpawnLootCycle, CycleTimeFloat);
            }

            private void UpdateRemainingWorkingTime()
            {
                if (RemainingWorkingTime >= Config.CycleTime) return;
                if (GetCountBattery() < CountBatteryPerCycle) return;
                RemoveBattery(CountBatteryPerCycle);
                RemainingWorkingTime += Config.WorkingTimePerBattery * CountBatteryPerCycle;
            }

            private int GetCountBattery()
            {
                int result = 0;
                foreach (Item item in InputCrate.inventory.itemList) if (item.info.shortname == "battery.small" && item.skin == 0) result += item.amount;
                return result;
            }

            private void RemoveBattery(int count)
            {
                foreach (Item item in InputCrate.inventory.itemList)
                {
                    if (item.info.shortname != "battery.small" || item.skin != 0) continue;
                    if (item.amount == count)
                    {
                        item.Remove();
                        break;
                    }
                    else if (item.amount < count)
                    {
                        count -= item.amount;
                        item.Remove();
                    }
                    else if (item.amount > count)
                    {
                        item.amount -= count;
                        item.MarkDirty();
                        break;
                    }
                }
            }

            private int GetCountBatteryPerCycle()
            {
                if (Config.CycleTime <= Config.WorkingTimePerBattery) return 1;
                if (Config.CycleTime % Config.WorkingTimePerBattery == 0) return Config.CycleTime / Config.WorkingTimePerBattery;
                else return Config.CycleTime / Config.WorkingTimePerBattery + 1;
            }

            private int GetHealth()
            {
                if (AllWorkingTime == 0) return 100;
                if (Config.MaxWorkingTime - AllWorkingTime < Config.CycleTime) return 0;
                return (int)(AllWorkingTime * 100f / Config.MaxWorkingTime);
            }
        }
        #endregion Drill

        #region Spawn Loot
        private void AddToContainerPrefab(ItemContainer container, PrefabLootTableConfig lootTable)
        {
            if (lootTable.UseCount)
            {
                int count = 0, max = UnityEngine.Random.Range(lootTable.Min, lootTable.Max + 1);
                while (count < max)
                {
                    foreach (PrefabConfig prefab in lootTable.Prefabs)
                    {
                        if (UnityEngine.Random.Range(0f, 100f) > prefab.Chance) continue;
                        SpawnIntoContainer(container, prefab.PrefabDefinition);
                        count++;
                        if (count == max) break;
                    }
                }
            }
            else foreach (PrefabConfig prefab in lootTable.Prefabs) if (UnityEngine.Random.Range(0f, 100f) <= prefab.Chance) SpawnIntoContainer(container, prefab.PrefabDefinition);
        }

        private void SpawnIntoContainer(ItemContainer container, string prefab)
        {
            if (AllLootSpawnSlots.ContainsKey(prefab))
            {
                foreach (LootContainer.LootSpawnSlot lootSpawnSlot in AllLootSpawnSlots[prefab])
                    for (int j = 0; j < lootSpawnSlot.numberToSpawn; j++)
                        if (UnityEngine.Random.Range(0f, 1f) <= lootSpawnSlot.probability)
                            lootSpawnSlot.definition.SpawnIntoContainer(container);
            }
            else AllLootSpawn[prefab].SpawnIntoContainer(container);
        }

        private void AddToContainerItem(ItemContainer container, LootTableConfig lootTable)
        {
            if (lootTable.UseCount)
            {
                HashSet<int> indexMove = new HashSet<int>();
                int count = UnityEngine.Random.Range(lootTable.Min, lootTable.Max + 1);
                while (indexMove.Count < count)
                {
                    for (int i = 0; i < lootTable.Items.Count; i++)
                    {
                        if (indexMove.Contains(i)) continue;
                        if (SpawnIntoContainer(container, lootTable.Items[i]))
                        {
                            indexMove.Add(i);
                            if (indexMove.Count == count) break;
                        }
                    }
                }
                indexMove = null;
            }
            else foreach (ItemConfig item in lootTable.Items) SpawnIntoContainer(container, item);
        }

        private bool SpawnIntoContainer(ItemContainer container, ItemConfig config)
        {
            if (UnityEngine.Random.Range(0f, 100f) > config.Chance) return false;
            Item item = config.IsBluePrint ? ItemManager.CreateByName("blueprintbase") : ItemManager.CreateByName(config.ShortName, UnityEngine.Random.Range(config.MinAmount, config.MaxAmount + 1), config.SkinId);
            if (item == null)
            {
                PrintWarning($"Failed to create item! ({config.ShortName})");
                return false;
            }
            if (config.IsBluePrint) item.blueprintTarget = ItemManager.FindItemDefinition(config.ShortName).itemid;
            if (!string.IsNullOrEmpty(config.Name)) item.name = config.Name;
            if (container.capacity < container.itemList.Count + 1) container.capacity++;
            if (!item.MoveToContainer(container))
            {
                item.Remove();
                return false;
            }
            return true;
        }

        private void CheckAllLootTables()
        {
            CheckPrefabLootTable(_config.FirstReceiver.Loot.PrefabLootTable);
            CheckLootTable(_config.FirstReceiver.Loot.OwnLootTable);

            CheckPrefabLootTable(_config.SecondReceiver.Loot.PrefabLootTable);
            CheckLootTable(_config.SecondReceiver.Loot.OwnLootTable);

            CheckPrefabLootTable(_config.ThirdReceiver.Loot.PrefabLootTable);
            CheckLootTable(_config.ThirdReceiver.Loot.OwnLootTable);

            CheckPrefabLootTable(_config.Drill.Loot.PrefabLootTable);
            CheckLootTable(_config.Drill.Loot.OwnLootTable);
        }

        private void CheckLootTable(LootTableConfig lootTable)
        {
            for (int i = lootTable.Items.Count - 1; i >= 0; i--)
            {
                ItemConfig item = lootTable.Items[i];

                if (!ItemManager.itemList.Any(x => x.shortname == item.ShortName))
                {
                    PrintWarning($"Unknown item removed! ({item.ShortName})");
                    lootTable.Items.Remove(item);
                    continue;
                }
                if (item.Chance <= 0f)
                {
                    PrintWarning($"An item with an incorrect probability has been removed from the loot table ({item.ShortName})");
                    lootTable.Items.Remove(item);
                    continue;
                }

                if (item.MinAmount <= 0) item.MinAmount = 1;
                if (item.MaxAmount < item.MinAmount) item.MaxAmount = item.MinAmount;
            }

            lootTable.Items = lootTable.Items.OrderByQuickSort(x => x.Chance);
            if (lootTable.Items.Any(x => x.Chance >= 100f))
            {
                HashSet<ItemConfig> newItems = new HashSet<ItemConfig>();

                for (int i = lootTable.Items.Count - 1; i >= 0; i--)
                {
                    ItemConfig itemConfig = lootTable.Items[i];
                    if (itemConfig.Chance < 100f) break;
                    newItems.Add(itemConfig);
                    lootTable.Items.Remove(itemConfig);
                }

                int count = newItems.Count;

                if (count > 0)
                {
                    foreach (ItemConfig itemConfig in lootTable.Items) newItems.Add(itemConfig);
                    lootTable.Items.Clear();
                    foreach (ItemConfig itemConfig in newItems) lootTable.Items.Add(itemConfig);
                }

                newItems = null;

                if (lootTable.Min < count) lootTable.Min = count;
                if (lootTable.Max < count) lootTable.Max = count;
            }

            if (lootTable.Max > lootTable.Items.Count) lootTable.Max = lootTable.Items.Count;
            if (lootTable.Min > lootTable.Max) lootTable.Min = lootTable.Max;
            if (lootTable.Items.Count == 0) lootTable.UseCount = false;
        }

        private void CheckPrefabLootTable(PrefabLootTableConfig lootTable)
        {
            HashSet<string> prefabs = new HashSet<string>();

            for (int i = lootTable.Prefabs.Count - 1; i >= 0; i--)
            {
                PrefabConfig prefab = lootTable.Prefabs[i];
                if (prefabs.Any(x => x == prefab.PrefabDefinition))
                {
                    lootTable.Prefabs.Remove(prefab);
                    PrintWarning($"Duplicate prefab removed from loot table! ({prefab.PrefabDefinition})");
                }
                else
                {
                    GameObject gameObject = GameManager.server.FindPrefab(prefab.PrefabDefinition);
                    global::HumanNPC humanNpc = gameObject.GetComponent<global::HumanNPC>();
                    ScarecrowNPC scarecrowNpc = gameObject.GetComponent<ScarecrowNPC>();
                    LootContainer lootContainer = gameObject.GetComponent<LootContainer>();
                    if (humanNpc != null && humanNpc.LootSpawnSlots.Length != 0)
                    {
                        if (!AllLootSpawnSlots.ContainsKey(prefab.PrefabDefinition)) AllLootSpawnSlots.Add(prefab.PrefabDefinition, humanNpc.LootSpawnSlots);
                        prefabs.Add(prefab.PrefabDefinition);
                    }
                    else if (scarecrowNpc != null && scarecrowNpc.LootSpawnSlots.Length != 0)
                    {
                        if (!AllLootSpawnSlots.ContainsKey(prefab.PrefabDefinition)) AllLootSpawnSlots.Add(prefab.PrefabDefinition, scarecrowNpc.LootSpawnSlots);
                        prefabs.Add(prefab.PrefabDefinition);
                    }
                    else if (lootContainer != null && lootContainer.LootSpawnSlots.Length != 0)
                    {
                        if (!AllLootSpawnSlots.ContainsKey(prefab.PrefabDefinition)) AllLootSpawnSlots.Add(prefab.PrefabDefinition, lootContainer.LootSpawnSlots);
                        prefabs.Add(prefab.PrefabDefinition);
                    }
                    else if (lootContainer != null && lootContainer.lootDefinition != null)
                    {
                        if (!AllLootSpawn.ContainsKey(prefab.PrefabDefinition)) AllLootSpawn.Add(prefab.PrefabDefinition, lootContainer.lootDefinition);
                        prefabs.Add(prefab.PrefabDefinition);
                    }
                    else
                    {
                        lootTable.Prefabs.Remove(prefab);
                        PrintWarning($"Unknown prefab removed! ({prefab.PrefabDefinition})");
                    }
                }
            }

            prefabs = null;

            lootTable.Prefabs = lootTable.Prefabs.OrderByQuickSort(x => x.Chance);
            if (lootTable.Prefabs.Any(x => x.Chance >= 100f))
            {
                HashSet<PrefabConfig> newPrefabs = new HashSet<PrefabConfig>();

                for (int i = lootTable.Prefabs.Count - 1; i >= 0; i--)
                {
                    PrefabConfig prefabConfig = lootTable.Prefabs[i];
                    if (prefabConfig.Chance < 100f) break;
                    newPrefabs.Add(prefabConfig);
                    lootTable.Prefabs.Remove(prefabConfig);
                }

                int count = newPrefabs.Count;

                if (count > 0)
                {
                    foreach (PrefabConfig prefabConfig in lootTable.Prefabs) newPrefabs.Add(prefabConfig);
                    lootTable.Prefabs.Clear();
                    foreach (PrefabConfig prefabConfig in newPrefabs) lootTable.Prefabs.Add(prefabConfig);
                }

                newPrefabs = null;

                if (lootTable.Min < count) lootTable.Min = count;
                if (lootTable.Max < count) lootTable.Max = count;
            }

            if (lootTable.Min > lootTable.Max) lootTable.Min = lootTable.Max;
            if (lootTable.Prefabs.Count == 0) lootTable.UseCount = false;
        }

        private Dictionary<string, LootSpawn> AllLootSpawn { get; } = new Dictionary<string, LootSpawn>();
        private Dictionary<string, LootContainer.LootSpawnSlot[]> AllLootSpawnSlots { get; } = new Dictionary<string, LootContainer.LootSpawnSlot[]>();
        #endregion Spawn Loot

        #region TruePVE
        private object CanEntityTakeDamage(BasePlayer victim, HitInfo hitinfo)
        {
            if (!_config.IsCreateZonePvp || victim == null || hitinfo == null || Controller == null) return null;
            BasePlayer attacker = hitinfo.InitiatorPlayer;
            if (Controller.Receiver != null && Controller.Receiver.Players.Contains(victim) && (attacker == null || Controller.Receiver.Players.Contains(attacker))) return true;
            if (Controller.Drill != null && Controller.Drill.Players.Contains(victim) && (attacker == null || Controller.Drill.Players.Contains(attacker))) return true;
            return null;
        }
        #endregion TruePVE

        #region NTeleportation
        private object CanTeleport(BasePlayer player, Vector3 to)
        {
            if (_config.NTeleportationInterrupt && Controller != null && Controller.Receiver != null && (Controller.Receiver.Players.Contains(player) || Vector3.Distance(Controller.transform.position, to) < LargeReceiverRadius)) return GetMessage("NTeleportation", player.UserIDString, _config.Chat.Prefix);
            else return null;
        }

        private void OnPlayerTeleported(BasePlayer player, Vector3 oldPos, Vector3 newPos)
        {
            if (Controller == null || !player.IsPlayer()) return;
            if (Controller.Receiver != null)
            {
                if (!Controller.Receiver.Players.Contains(player) && Vector3.Distance(Controller.transform.position, newPos) < LargeReceiverRadius) Controller.Receiver.EnterPlayer(player);
                if (Controller.Receiver.Players.Contains(player) && Vector3.Distance(Controller.transform.position, newPos) > LargeReceiverRadius) Controller.Receiver.ExitPlayer(player);
            }
            if (Controller.Drill != null)
            {
                if (!Controller.Drill.Players.Contains(player) && Vector3.Distance(Controller.transform.position, newPos) < LargeReceiverRadius) Controller.Drill.EnterPlayer(player);
                if (Controller.Drill.Players.Contains(player) && Vector3.Distance(Controller.transform.position, newPos) > LargeReceiverRadius) Controller.Drill.ExitPlayer(player);
            }
        }
        #endregion NTeleportation

        #region Rust Rewards
        private object OnRustReward(BasePlayer player, string type)
        {
            if (_config.RustRewards || Controller == null || !player.IsPlayer()) return null;
            if (Controller.Receiver != null && Controller.Receiver.Players.Contains(player)) return true;
            return null;
        }
        #endregion Rust Rewards

        #region XPerience
        private bool IsPlayerBlockingLocation(BasePlayer player)
        {
            if (_config.XPerience || Controller == null || !player.IsPlayer()) return false;
            return Controller.Receiver != null && Controller.Receiver.Players.Contains(player);
        }
        #endregion XPerience

        #region Skill Tree
        [PluginReference] private readonly Plugin SkillTree;

        private void TryDisableSkillTree(BasePlayer player)
        {
            if (_config.SkillTree || !plugins.Exists("SkillTree") || Controller == null || !player.IsPlayer()) return;
            SkillTree?.Call("DisableXP", (ulong)player.userID);
        }

        private void TryEnableSkillTree(BasePlayer player)
        {
            if (_config.SkillTree || !plugins.Exists("SkillTree") || Controller == null || !player.IsPlayer()) return;
            SkillTree?.Call("EnableXP", (ulong)player.userID);
        }
        #endregion Skill Tree

        #region Economy
        [PluginReference] private readonly Plugin Economics, ServerRewards, IQEconomic, XPerience;

        private Dictionary<ulong, double> PlayersBalance { get; } = new Dictionary<ulong, double>();

        private void ActionEconomy(ulong playerId, string type, string arg = "")
        {
            switch (type)
            {
                case "Animal":
                    if (_config.Economy.Animals.ContainsKey(arg)) AddBalance(playerId, _config.Economy.Animals[arg]);
                    break;
                case "StartScan":
                    AddBalance(playerId, _config.Economy.StartScan);
                    break;
            }
        }

        private void AddBalance(ulong playerId, double balance)
        {
            if (balance == 0) return;
            if (PlayersBalance.ContainsKey(playerId)) PlayersBalance[playerId] += balance;
            else PlayersBalance.Add(playerId, balance);
        }

        private void SendBalance()
        {
            if (PlayersBalance.Count == 0) return;
            if (_config.Economy.Plugins.Count > 0)
            {
                foreach (KeyValuePair<ulong, double> dic in PlayersBalance)
                {
                    if (dic.Value < _config.Economy.Min) continue;
                    int intCount = Convert.ToInt32(dic.Value);
                    if (_config.Economy.Plugins.Contains("Economics") && plugins.Exists("Economics") && dic.Value > 0) Economics?.Call("Deposit", dic.Key.ToString(), dic.Value);
                    if (_config.Economy.Plugins.Contains("Server Rewards") && plugins.Exists("ServerRewards") && intCount > 0) ServerRewards?.Call("AddPoints", dic.Key, intCount);
                    if (_config.Economy.Plugins.Contains("IQEconomic") && plugins.Exists("IQEconomic") && intCount > 0) IQEconomic?.Call("API_SET_BALANCE", dic.Key, intCount);
                    BasePlayer player = BasePlayer.FindByID(dic.Key);
                    if (player != null)
                    {
                        if (_config.Economy.Plugins.Contains("XPerience") && plugins.Exists("XPerience") && dic.Value > 0) XPerience?.Call("GiveXP", player, dic.Value);
                        AlertToPlayer(player, GetMessage("SendEconomy", player.UserIDString, _config.Chat.Prefix, dic.Value));
                    }
                }
            }
            ulong winnerId = PlayersBalance.Max(x => x.Value).Key;
            Interface.Oxide.CallHook($"On{Name}Winner", winnerId);
            foreach (string command in _config.Economy.Commands) Server.Command(command.Replace("{steamid}", $"{winnerId}"));
            PlayersBalance.Clear();
        }
        #endregion Economy

        #region Alerts
        [PluginReference] private readonly Plugin GUIAnnouncements, DiscordMessages, Notify;

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
            if (!string.IsNullOrEmpty(_config.Chat.Prefix)) message = message.Replace(_config.Chat.Prefix + " ", string.Empty);
            return message;
        }

        private bool CanSendDiscordMessage => _config.Discord.IsDiscord && !string.IsNullOrEmpty(_config.Discord.WebhookUrl) && _config.Discord.WebhookUrl != "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";

        private void AlertToAllPlayers(string langKey, params object[] args)
        {
            if (CanSendDiscordMessage && _config.Discord.Keys.Contains(langKey))
            {
                object fields = new[] { new { name = Title, value = ClearColorAndSize(GetMessage(langKey, null, args)), inline = false } };
                DiscordMessages?.Call("API_SendFancyMessage", _config.Discord.WebhookUrl, "", _config.Discord.EmbedColor, JsonConvert.SerializeObject(fields), null, this);
            }
            foreach (BasePlayer player in BasePlayer.activePlayerList) AlertToPlayer(player, GetMessage(langKey, player.UserIDString, args));
        }

        private void AlertToPlayer(BasePlayer player, string message)
        {
            if (_config.Chat.IsChat) PrintToChat(player, message);
            if (_config.GameTip.IsGameTip) player.SendConsoleCommand("gametip.showtoast", _config.GameTip.Style, ClearColorAndSize(message), string.Empty);
            if (_config.GuiAnnouncements.IsGuiAnnouncements && plugins.Exists("GUIAnnouncements")) GUIAnnouncements?.Call("CreateAnnouncement", ClearColorAndSize(message), _config.GuiAnnouncements.BannerColor, _config.GuiAnnouncements.TextColor, player, _config.GuiAnnouncements.ApiAdjustVPosition);
            if (_config.Notify.IsNotify && plugins.Exists("Notify")) Notify?.Call("SendNotify", player, _config.Notify.Type, ClearColorAndSize(message));
        }
        #endregion Alerts

        #region GUI
        private HashSet<string> Names { get; } = new HashSet<string>
        {
            "Tab_KpucTaJl",
            "Plus_KpucTaJl",
            "Indicator_KpucTaJl",
            "Animal_KpucTaJl",
            "Clock_KpucTaJl"
        };
        private Dictionary<string, string> Images { get; } = new Dictionary<string, string>();

        private IEnumerator DownloadImages()
        {
            foreach (string name in Names)
            {
                string url = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "Images" + Path.DirectorySeparatorChar + name + ".png";
                using (UnityWebRequest unityWebRequest = UnityWebRequestTexture.GetTexture(url))
                {
                    yield return unityWebRequest.SendWebRequest();
                    if (unityWebRequest.result != UnityWebRequest.Result.Success)
                    {
                        PrintError($"Image {name} was not found. Maybe you didn't upload it to the .../oxide/data/Images/ folder");
                        break;
                    }
                    else
                    {
                        Texture2D tex = DownloadHandlerTexture.GetContent(unityWebRequest);
                        Images.Add(name, FileStorage.server.Store(tex.EncodeToPNG(), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString());
                        Puts($"Image {name} download is complete");
                        UnityEngine.Object.DestroyImmediate(tex);
                    }
                }
            }
            if (Images.Count < Names.Count) Interface.Oxide.UnloadPlugin(Name);
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

            foreach (KeyValuePair<string, string> dic in tabs)
            {
                i++;
                float xmin = 109f * (i - 1);
                container.Add(new CuiElement
                {
                    Name = $"Tab_{i}_KpucTaJl",
                    Parent = "Tabs_KpucTaJl",
                    Components =
                    {
                        new CuiRawImageComponent { Png = Images["Tab_KpucTaJl"] },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = $"{xmin} 0", OffsetMax = $"{xmin + 105f} 20" }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = $"Tab_{i}_KpucTaJl",
                    Components =
                    {
                        new CuiRawImageComponent { Png = Images[dic.Key] },
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
        [PluginReference] private readonly Plugin AnimalSpawn, Friends, Clans;

        private HashSet<string> HooksInsidePlugin { get; } = new HashSet<string>
        {
            "OnEntityTakeDamage",
            "CanBuild",
            "OnPlayerConnected",
            "OnPlayerDeath",
            "OnItemPickup",
            "CanLootEntity",
            "OnPlayerCommand",
            "OnServerCommand",
            "CanCustomAnimalSpawnCorpse",
            "OnCustomAnimalTarget",
            "OnEntityKill",
            "OnEntityDeath",
            "OnRestoreUponDeath",
            "CanEntityTakeDamage",
            "CanTeleport",
            "OnPlayerTeleported",
            "OnRustReward",
            "IsPlayerBlockingLocation"
        };

        private void ToggleHooks(bool subscribe)
        {
            foreach (string hook in HooksInsidePlugin)
            {
                if (subscribe) Subscribe(hook);
                else Unsubscribe(hook);
            }
        }

        private HashSet<string> WeaponsBlockedDamage { get; } = new HashSet<string>
        {
            "grenade.beancan.deployed",
            "grenade.f1.deployed",
            "grenade.molotov.deployed",
            "explosive.satchel.deployed",
            "explosive.timed.deployed",
            "rocket_hv",
            "rocket_basic",
            "rocket_fire",
            "rocket_mlrs",
            "rocket_heli",
            "40mm_grenade_he"
        };

        public class Prefab { public string Path; public Vector3 Pos; public Vector3 Rot; }

        private bool IsTeam(ulong playerId, ulong targetId)
        {
            if (playerId == 0 || targetId == 0) return false;
            if (playerId == targetId) return true;
            RelationshipManager.PlayerTeam playerTeam = RelationshipManager.ServerInstance.FindPlayersTeam(playerId);
            if (playerTeam != null && playerTeam.members.Contains(targetId)) return true;
            if (plugins.Exists("Friends") && (bool)Friends.Call("AreFriends", playerId, targetId)) return true;
            if (plugins.Exists("Clans") && Clans.Author == "k1lly0u" && (bool)Clans.Call("IsMemberOrAlly", playerId.ToString(), targetId.ToString())) return true;
            return false;
        }

        private const string StrSec = En ? "sec." : "сек.";
        private const string StrMin = En ? "min." : "мин.";
        private const string StrH = En ? "h." : "ч.";

        private static string GetTimeFormat(int time)
        {
            if (time <= 60) return $"{time} {StrSec}";
            else if (time <= 3600)
            {
                int sec = time % 60;
                int min = (time - sec) / 60;
                return sec == 0 ? $"{min} {StrMin}" : $"{min} {StrMin} {sec} {StrSec}";
            }
            else
            {
                int minSec = time % 3600;
                int hour = (time - minSec) / 3600;
                int sec = minSec % 60;
                int min = (minSec - sec) / 60;
                if (min == 0 && sec == 0) return $"{hour} {StrH}";
                else if (sec == 0) return $"{hour} {StrH} {min} {StrMin}";
                else return $"{hour} {StrH} {min} {StrMin} {sec} {StrSec}";
            }
        }

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

        private static DroppedItem SpawnDroppedItem(string shortname, Vector3 pos, Quaternion rot, bool allowPickup = false)
        {
            DroppedItem droppedItem = GameManager.server.CreateEntity("assets/prefabs/misc/burlap sack/generic_world.prefab", pos, rot) as DroppedItem;
            droppedItem.InitializeItem(ItemManager.CreateByName(shortname));
            droppedItem.enableSaving = false;
            droppedItem.Spawn();

            UnityEngine.Object.Destroy(droppedItem.GetComponent<PhysicsEffects>());
            UnityEngine.Object.Destroy(droppedItem.GetComponent<EntityCollisionMessage>());

            Rigidbody rigidbody = droppedItem.GetComponent<Rigidbody>();
            rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rigidbody.isKinematic = true;

            droppedItem.StickIn();

            droppedItem.CancelInvoke(droppedItem.IdleDestroy);

            droppedItem.allowPickup = allowPickup;

            return droppedItem;
        }

        private static void UpdateMarkerForPlayer(BasePlayer player, Vector3 pos, PointConfig config)
        {
            if (player == null || player.IsSleeping()) return;
            bool isAdmin = player.IsAdmin;
            if (!isAdmin)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                player.SendNetworkUpdateImmediate();
            }
            try
            {
                player.SendConsoleCommand("ddraw.text", 1f, Color.white, pos, $"<size={config.Size}><color={config.Color}>{config.Text}</color></size>");
            }
            finally
            {
                if (!isAdmin)
                {
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                    player.SendNetworkUpdateImmediate();
                }
            }
        }

        private void CheckVersionPlugin()
        {
            webrequest.Enqueue("http://37.153.157.216:5000/Api/GetPluginVersions?pluginName=Triangulation", null, (code, response) =>
            {
                if (code != 200 || string.IsNullOrEmpty(response)) return;
                string[] array = response.Replace("\"", string.Empty).Split('.');
                VersionNumber latestVersion = new VersionNumber(Convert.ToInt32(array[0]), Convert.ToInt32(array[1]), Convert.ToInt32(array[2]));
                if (Version < latestVersion) PrintWarning($"A new version ({latestVersion}) of the plugin is available! You need to update the plugin:\n- https://lone.design/product/triangulation\n- https://codefling.com/plugins/triangulation");
            }, this);
        }

        private bool PluginExistsForStart(string pluginName)
        {
            if (plugins.Exists(pluginName)) return true;
            PrintError($"{pluginName} plugin doesn`t exist! (https://drive.google.com/drive/folders/1-18L-mG7yiGxR-PQYvd11VvXC2RQ4ZCu?usp=sharing)");
            Interface.Oxide.UnloadPlugin(Name);
            return false;
        }
        #endregion Helpers

        #region Commands
        [ChatCommand("tstart")]
        private void ChatStartEvent(BasePlayer player)
        {
            if (player.IsAdmin)
            {
                if (!Active) Start(null);
                else PrintToChat(player, GetMessage("EventActive", player.UserIDString, _config.Chat.Prefix));
            }
        }

        [ChatCommand("tstop")]
        private void ChatStopEvent(BasePlayer player)
        {
            if (player.IsAdmin)
            {
                if (Controller != null) Finish();
                else Interface.Oxide.ReloadPlugin(Name);
            }
        }

        [ConsoleCommand("tstart")]
        private void ConsoleStartEvent(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null) return;
            if (!Active)
            {
                if (arg.Args == null || arg.Args.Length != 1)
                {
                    Start(null);
                    return;
                }
                ulong steamId = Convert.ToUInt64(arg.Args[0]);
                BasePlayer target = BasePlayer.FindByID(steamId);
                if (target == null)
                {
                    Start(null);
                    Puts($"Player with SteamID {steamId} not found!");
                    return;
                }
                Start(target);
            }
            else Puts("This event is active now. To finish this event (tstop), then to start the next one");
        }

        [ConsoleCommand("tstop")]
        private void ConsoleStopEvent(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null)
            {
                if (Controller != null) Finish();
                else Interface.Oxide.ReloadPlugin(Name);
            }
        }
        #endregion Commands
    }
}

namespace Oxide.Plugins.TriangulationExtensionMethods
{
    public static class ExtensionMethods
    {
        public static bool Any<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) return true;
            return false;
        }

        public static HashSet<TSource> Where<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            HashSet<TSource> result = new HashSet<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) result.Add(enumerator.Current);
            return result;
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

        public static TSource Max<TSource>(this IEnumerable<TSource> source, Func<TSource, double> predicate)
        {
            TSource result = default(TSource);
            double resultValue = double.MinValue;
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

        public static HashSet<TSource> ToHashSet<TSource>(this IEnumerable<TSource> source)
        {
            HashSet<TSource> result = new HashSet<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) result.Add(enumerator.Current);
            return result;
        }

        private static void Replace<TSource>(this IList<TSource> source, int x, int y)
        {
            TSource t = source[x];
            source[x] = source[y];
            source[y] = t;
        }

        private static List<TSource> QuickSort<TSource>(this List<TSource> source, Func<TSource, float> predicate, int minIndex, int maxIndex)
        {
            if (minIndex >= maxIndex) return source;

            int pivotIndex = minIndex - 1;
            for (int i = minIndex; i < maxIndex; i++)
            {
                if (predicate(source[i]) < predicate(source[maxIndex]))
                {
                    pivotIndex++;
                    source.Replace(pivotIndex, i);
                }
            }
            pivotIndex++;
            source.Replace(pivotIndex, maxIndex);

            QuickSort(source, predicate, minIndex, pivotIndex - 1);
            QuickSort(source, predicate, pivotIndex + 1, maxIndex);

            return source;
        }

        public static List<TSource> OrderByQuickSort<TSource>(this List<TSource> source, Func<TSource, float> predicate) => source.QuickSort(predicate, 0, source.Count - 1);

        public static bool IsPlayer(this BasePlayer player) => player != null && player.userID.IsSteamId();

        public static bool IsExists(this BaseNetworkable entity) => entity != null && !entity.IsDestroyed;

        public static bool IsEqual(this float value1, float value2) => Math.Abs(value1 - value2) < 0.001f;
    }
}