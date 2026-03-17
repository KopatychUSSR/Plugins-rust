using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("RustyDeath", "__red", "1.28.1688")]
    public class RustyDeath : RustPlugin
    {
        #region Variables
        private RustyDeathConfig m_Config;

        private List<DeathInfo> m_DeathInformation;
        private Dictionary<BasePlayer, DeathHistory> m_PlayerDeathHistory;
        private Dictionary<ulong, HitInfo> m_LastPlayersHits;
        private Dictionary<uint, HitInfo> m_LastHelicopterHits;
        #endregion

        #region Types & Config
        public enum Animal
        {
            Bear,
            Boar,
            Chicken,
            Wolf,
            Horse,
            Stag
        }
        public enum Trap
        {
            Guntrap,
            Minetrap,
            Spikes,
            Snaptrap
        }
        public enum Barricades
        {
            WoodenBarricade,
            BarbenWoodenBarricade,
            MetalBarricade,
            GatesExternalHighStone,
            GatesExternalHighWood,
            WallExternalHighStone,
            WallExternalHighWood
        }
        public enum EntityType
        {
            Invalid,
            Player,
            Npc,
            Animal,
            Helicopter,
            Tank,
            Turret,
            Building,
            Trap,
            Zombie,
            Sleeper,
            Scientist,
            Barrel
        }
        public enum DeathType
        {
            PlayerToPlayer,
            PlayerToNpc,
            NpcToPlayer,
            PlayerToAnimal,
            AnimalToPlayer,
            PlayerToHelicopter,
            HelicopterToPlayer,
            PlayerToTank,
            TankToPlayer,
            TurretToPlayer,
            GuntrapToPlayer,
            BuildingToPlayer,
            TrapToPlayer,
            PlayerToZombie,
            ZombieToPlayer,
            PlayerToSleeper,
            Fall,
            Suicide,
            Hunger,
            Cold,
            Bleeding,
            Poison,
            Radiation,
            Explosion,
            ExplosionPlayer,
            Drowned,
            Thirst,
            Heat,
            Stab,
            Arrow,
            Default
        }

        private class RustyDeathConfig
        {
            [JsonProperty("Время обновления интерфейса")]
            public int ShowTiming { get; set; }

            [JsonProperty("Размер шрифта в отображаемом интерфейсе")]
            public int FontSize { get; set; }

            [JsonProperty("Положение левых углов:")]
            public string AnchorMin { get; set; }

            [JsonProperty("Положение правых углов:")]
            public string AnchorMax { get; set; }

            [JsonProperty("Ровнять текст по правому краю? (false = будет ровнять по левому краю.)")]
            public bool FixedInRight { get; set; }

            [JsonProperty("Показывать смерти и убийства связанные с животными ?")]
            public bool ShowDeathAnimals { get; set; }

            [JsonProperty("Показывать смерти и убийства связанные с НПС ?")]
            public bool ShowDeathNpcs { get; set; }

            [JsonProperty("Показывать смерти и убийства связанные со слиперами ?")]
            public bool ShowDeathSleepers { get; set; }

            [JsonProperty("Код подсветки убийцы по умолчанию")]
            public string HighlightKillerDefault { get; set; }

            [JsonProperty("Код подсветки жертвы по умолчанию")]
            public string HighlightVictimDefault { get; set; }

            [JsonProperty("Код подсветки животных по умолчанию")]
            public string HighlightAnimalsDefault { get; set; }

            [JsonProperty("Код подсветки НПС по умолчанию")]
            public string HighlightNpcDefault { get; set; }

            [JsonProperty("Код подсветки дистанции по умолчанию")]
            public string HighlightDistanceDefault { get; set; }

            [JsonProperty("Вести историю убийств и смертей игроков ?")]
            public bool EnableDeathHistory { get; set; }

            [JsonProperty("Максимальное количество объектов для истории смертей")]
            public int MaxDeathHistoryMembers { get; set; }

            [JsonProperty("Включить особую подсветку сообщений связанных со мной ?")]
            public bool EnableSelfHighlight { get; set; }

            [JsonProperty("Цвет особой подсветки")]
            public string SelfHighlightColor { get; set; }

            [JsonProperty("Динамические привилегии: EXAMPLE \"rustydeath.vip\" = \"#FF0000\"")]
            public Dictionary<string, string> Permissions { get; set; }

            [JsonProperty("Динамическая подсветка дистанций: EXAMPLE 10 = \"#FF00FF\"")]
            public Dictionary<float, string> DistanceHighlights { get; set; }

            [JsonProperty("Динамические имена. Указывайте свои имена для разных типов сущностей")]
            public Dictionary<string, Dictionary<string, string>> CustomEntitiesNames { get; set; }

            [JsonProperty("Динамические строки вывода информации под тип убийства")]
            public Dictionary<string, List<string>> PlayerDeathMessages { get; set; }

            [JsonProperty("Динамические имена и подсветки для оружия")]
            public Dictionary<string, Dictionary<string, string>> CustomWeaponSettings { get; set; }

            [JsonProperty("Динамическая подсветка символов в подстроке. Указывайте сколько угодно, главное чтобы они были в PlayerDeathMessages")]
            public Dictionary<string, string> DynamicalSymbolsHighlight { get; set; }

            public static RustyDeathConfig Prototype()
            {
                return new RustyDeathConfig()
                {
                    ShowTiming = 7,
                    FontSize = 15,
                    AnchorMin = "0.5 0.8",
                    AnchorMax = "0.99 0.995",
                    FixedInRight = true,
                    ShowDeathAnimals = true,
                    ShowDeathNpcs = true,
                    ShowDeathSleepers = true,
                    HighlightKillerDefault = "#FFAE42",
                    HighlightVictimDefault = "#00A1E0",
                    HighlightAnimalsDefault = "#99FF99",
                    HighlightNpcDefault = "#FF0000",
                    HighlightDistanceDefault = "#FFCD8F",
                    EnableDeathHistory = true,
                    MaxDeathHistoryMembers = 15,
                    EnableSelfHighlight = true,
                    SelfHighlightColor = "#000000",
                    Permissions = new Dictionary<string, string>()
                    {
                        ["rustydeath.vip"] = "#848484",
                    },
                    DistanceHighlights = new Dictionary<float, string>()
                    {
                        [10f]  = "#ff9c00",
                        [50f]  = "#ffffff",
                        [120f] = "#01A9DB",
                        [200f] = "#F7FE2E",
                        [300f] = "#FF0000"
                    },
                    CustomEntitiesNames = new Dictionary<string, Dictionary<string, string>>()
                    {
                        ["Npc"] = new Dictionary<string, string>()
                        {
                            ["Npc"] = "Ученый НПС"
                        },
                        ["Animal"] = new Dictionary<string, string>()
                        {
                            ["Bear"] = "Медведь",
                            ["Boar"] = "Кабан",
                            ["Horse"] = "Лошадь",
                            ["Wolf"] = "Волк",
                            ["Chicken"] = "Курица",
                            ["Stag"] = "Олень"
                        },
                        ["Helicopter"] = new Dictionary<string, string>()
                        {
                            ["Helicopter"] = "Вертолет"
                        },
                        ["Tank"] = new Dictionary<string, string>()
                        {
                            ["Tank"] = "Танк"
                        },
                        ["Building"] = new Dictionary<string, string>()
                        {
                            ["WoodenBarricade"] = "Деревянную баррикаду",
                            ["BarbedWooden Barricade"] = "Колючую деревянную баррикаду",
                            ["MetalBarricade"] = "Металлическую баррикаду",
                            ["GatesExternalHighWood"] = "Высокие деревянные ворота",
                            ["GatesExternalHighStone"] = "Высокие каменные ворота",
                            ["WallExternalHighWood"] = "Высокую деревянную стену",
                            ["WallExternalHighStone"] = "Высокую каменную стену"
                        },
                        ["Trap"] = new Dictionary<string, string>()
                        {
                            ["Guntrap"] = "Дробьловушку",
                            ["Minetrap"] = "Мину",
                            ["Snaptrap"] = "Капкан",
                            ["Spikes"] = "Деревянные колья"
                        },
                        ["Zombie"] = new Dictionary<string, string>()
                        {
                            ["Zombie"] = "Автоматическая турель",
                        },
                        ["Sleeper"] = new Dictionary<string, string>()
                        {
                            ["Sleeper"] = "Спящий",
                        },
                        ["Turret"] = new Dictionary<string, string>()
                        {
                            ["Autoturret"]  = "Автоматическая турель",
                            ["Flameturret"] = "Огненная турель",
                            ["Scientist"]   = "Турель ученых"
                        },
                    },
                    PlayerDeathMessages = new Dictionary<string, List<string>>()
                    {
                        ["PlayerToPlayer"] = new List<string>()
                        {
                            "<color={0}>{1}</color> убил <color={2}>{3}</color> (<color={4}>{5}</color> - <color={6}>Дистанция > {7}м</color>)",
                        },
                        ["PlayerToNpc"] = new List<string>()
                        {
                            "<color={0}>{1}</color> убил <color={2}>{3}</color> (<color={4}>{5}</color> - <color={6}>Дистанция > {7}м</color>)",
                        },
                        ["NpcToPlayer"] = new List<string>()
                        {
                            "<color={0}>{1}</color> убил <color={2}>{3}</color> (<color={6}>Дистанция > {7}м</color>)",
                        },
                        ["PlayerToAnimal"] = new List<string>()
                        {
                            "<color={0}>{1}</color> убил <color={2}>{3}</color> (<color={4}>{5}</color> - <color={6}>Дистанция > {7}м</color>)",
                        },
                        ["AnimalToPlayer"] = new List<string>()
                        {
                            "<color={0}>{1}</color> сьел <color={2}>{3}</color>",
                        },
                        ["PlayerToTank"] = new List<string>()
                        {
                            "<color={0}>{1}</color> убил <color={2}>{3}</color> (<color={4}>{5}</color> - <color={6}>Дистанция > {7}м</color>)",
                        },
                        ["TankToPlayer"] = new List<string>()
                        {
                            "<color={0}>{1}</color> убил <color={2}>{3}</color> (<color={6}>Дистанция > {7}м</color>)",
                        },
                        ["PlayerToHelicopter"] = new List<string>()
                        {
                            "<color={0}>{1}</color> убил <color={2}>{3}</color> (<color={4}>{5}</color> - <color={6}>Дистанция > {7}м</color>)",
                        },
                        ["HelicopterToPlayer"] = new List<string>()
                        {
                            "<color={0}>{1}</color> убил <color={2}>{3}</color> (<color={6}>Дистанция > {7}м</color>)",
                        },
                        ["PlayerToSleeper"] = new List<string>()
                        {
                            "<color={0}>{1}</color> убил <color={2}>{3}</color> (<color={4}>{5}</color> - <color={6}>Дистанция > {7}м</color>)",
                        },
                        ["TurretToPlayer"] = new List<string>()
                        {
                            "<color={0}>{1}</color> убила <color={2}>{3}</color>",
                        },
                        ["GuntrapToPlayer"] = new List<string>()
                        {
                            "<color={0}>{1}</color> убил <color={2}>{3}</color>",
                        },
                        ["BuildingToPlayer"] = new List<string>()
                        {
                            "Игрок: <color={2}>{3}</color> зацепился за <color={0}>{1}</color>",
                        },
                        ["TrapToPlayer"] = new List<string>()
                        {
                            "Игрок: <color={2}>{3}</color> попался в ловушку (<color={0}>{1}</color>)",
                        },
                        ["Zombie"] = new List<string>()
                        {
                            "<color={0}>{1}</color> убил <color={2}>{3}</color> (<color={6}>Дистанция > {7}м</color>)",
                        },
                        ["Fall"] = new List<string>()
                        {
                            "Игрок: <color={2}>{3}</color> <color=#6d41f4>упал c большой высоты и разбился</color>",
                        },
                        ["Suicide"] = new List<string>()
                        {
                            "Игрок: <color={2}>{3}</color> <color=#eb41f4>самоуничтожился</color>",
                        },
                        ["Cold"] = new List<string>()
                        {
                            "Игрок: <color={2}>{3}</color> <color=#41e2f4>умер от холода</color>",
                        },
                        ["Bleedeing"] = new List<string>()
                        {
                            "Игрок: <color={2}>{3}</color> <color=#f44141>истек кровью</color>",
                        },
                        ["Poison"] = new List<string>()
                        {
                            "Игрок: <color={2}>{3}</color> <color=#c4f441>умер от отравления</color>",
                        },
                        ["Radiation"] = new List<string>()
                        {
                            "Игрок: <color={2}>{3}</color> <color=#eef441>умер от радиации</color>",
                        },
                        ["Hungry"] = new List<string>()
                        {
                            "Игрок: <color={2}>{3}</color> <color=#41f488>умер от голода</color>",
                        },
                        ["Explosion"] = new List<string>()
                        {
                            "Игрок: <color={2}>{3}</color> <color=#f4ca41>взорвался</color>",
                        },
                        ["ExplosionPlayer"] = new List<string>()
                        {
                            "<color={0}>{1}</color> взорвал <color={2}>{3}</color>(<color={4}>{5}</color> - <color={6}>Дистанция > {7}м</color>)",
                        },
                        ["Drowned"] = new List<string>()
                        {
                            "Игрок: <color={2}>{3}</color> <color=#415ef4>утонул</color>",
                        },
                        ["Heat"] = new List<string>()
                        {
                            "Игрок: <color={2}>{3}</color> <color=#f49a41>сгорел заживо</color>",
                        },
                        ["Stab"] = new List<string>()
                        {
                            "<color={0}>{1}</color> убил <color={2}>{3}</color>(<color={4}>{5}</color> - <color={6}>Дистанция > {7}м</color>)",
                        },
                        ["Arrow"] = new List<string>()
                        {
                            "<color={0}>{1}</color> убил <color={2}>{3}</color>(<color={4}>{5}</color> - <color={6}>Дистанция > {7}м</color>)",
                        },
                        ["Slash"] = new List<string>()
                        {
                            "<color={0}>{1}</color> разобрал на куски <color={2}>{3}</color>(<color={4}>{5}</color> - <color={6}>Дистанция > {7}м</color>)",
                        }
                    },
                    CustomWeaponSettings = new Dictionary<string, Dictionary<string, string>>()
                    {
                        ["Assault Rifle"] = new Dictionary<string, string>()
                        {
                            ["АК-74"] = "#F50000"
                        },
                        ["Beancan Grenade"] = new Dictionary<string, string>()
                        {
                            ["Бобовая граната"] = "#F50000"
                        },
                        ["Bolt Action Rifle"] = new Dictionary<string, string>()
                        {
                            ["Винтовка"] = "#F50000"
                        },
                        ["Bone Club"] = new Dictionary<string, string>()
                        {
                            ["Дубина"] = "#00BFFF"
                        },
                        ["Bone Knife"] = new Dictionary<string, string>()
                        {
                            ["Нож"] = "#00BFFF"
                        },
                        ["Rock"] = new Dictionary<string, string>()
                        {
                            ["Камень"] = "#00BFFF"
                        },
                        ["Crossbow"] = new Dictionary<string, string>()
                        {
                            ["Арбалет"] = "#00BFFF"
                        },
                        ["Custom SMG"] = new Dictionary<string, string>()
                        {
                            ["SMG"] = "#F50000"
                        },
                        ["Double Barrel Shotgun"] = new Dictionary<string, string>()
                        {
                            ["Двустволка"] = "#F50000"
                        },
                        ["Eoka Pistol"] = new Dictionary<string, string>()
                        {
                            ["ЭОКА"] = "#F50000"
                        },
                        ["F1 Grenade"] = new Dictionary<string, string>()
                        {
                            ["F1 Граната"] = "#F50000"
                        },
                        ["Flame Thrower"] = new Dictionary<string, string>()
                        {
                            ["Огнемёт"] = "#F50000"
                        },
                        ["Hunting Bow"] = new Dictionary<string, string>()
                        {
                            ["Лук"] = "#00BFFF"
                        },
                        ["Compound Bow"] = new Dictionary<string, string>()
                        {
                            ["Блочный Лук"] = "#00BFFF"
                        },
                        ["Longsword"] = new Dictionary<string, string>()
                        {
                            ["Меч"] = "#00BFFF"
                        },
                        ["Candy Cane Club"] = new Dictionary<string, string>()
                        {
                            ["Праздничная палка"] = "#00BFFF"
                        },
                        ["Nailgun"] = new Dictionary<string, string>()
                        {
                            ["Гвоздомет"] = "#00BFFF"
                        },
                        ["LR-300 Assault Rifle"] = new Dictionary<string, string>()
                        {
                            ["LR-300"] = "#F50000"
                        },
                        ["M249"] = new Dictionary<string, string>()
                        {
                            ["Пулемет М249"] = "#F50000"
                        },
                        ["M92 Pistol"] = new Dictionary<string, string>()
                        {
                            ["Пистолет М92"] = "#F50000"
                        },
                        ["Mace"] = new Dictionary<string, string>()
                        {
                            ["Булава"] = "#00BFFF"
                        },
                        ["Machete"] = new Dictionary<string, string>()
                        {
                            ["Мачете"] = "#00BFFF"
                        },
                        ["MP5A4"] = new Dictionary<string, string>()
                        {
                            ["MP5A4"] = "#F50000"
                        },
                        ["Pump Shotgun"] = new Dictionary<string, string>()
                        {
                            ["Помповый дробовик"] = "#F50000"
                        },
                        ["Spas-12 Shotgun"] = new Dictionary<string, string>()
                        {
                            ["Спас-12 Дробовик"] = "#F50000"
                        },
                        ["Flashlight"] = new Dictionary<string, string>()
                        {
                            ["Фонарик"] = "#00BFFF"
                        },
                        ["Python Revolver"] = new Dictionary<string, string>()
                        {
                            ["Питон"] = "#F50000"
                        },
                        ["Revolver"] = new Dictionary<string, string>()
                        {
                            ["Револьвер"] = "#F50000"
                        },
                        ["Salvaged Cleaver"] = new Dictionary<string, string>()
                        {
                            ["Самодельный тесак"] = "#00BFFF"
                        },
                        ["Salvaged Sword"] = new Dictionary<string, string>()
                        {
                            ["Самодельный мечь"] = "#00BFFF"
                        },
                        ["Semi-Automatic Pistol"] = new Dictionary<string, string>()
                        {
                            ["Пистолет P250"] = "#F50000"
                        },
                        ["Semi-Automatic Rifle"] = new Dictionary<string, string>()
                        {
                            ["Берданка"] = "#F50000"
                        },
                        ["Stone Spear"] = new Dictionary<string, string>()
                        {
                            ["Каменное Копье"] = "#00BFFF"
                        },
                        ["Thompson"] = new Dictionary<string, string>()
                        {
                            ["Томпсон"] = "#F50000"
                        },
                        ["Waterpipe Shotgun"] = new Dictionary<string, string>()
                        {
                            ["Самодельный Пайп"] = "#F50000"
                        },
                        ["Wooden Spear"] = new Dictionary<string, string>()
                        {
                            ["Деревянное Копье"] = "#00BFFF"
                        },
                        ["Hatchet"] = new Dictionary<string, string>()
                        {
                            ["Топор"] = "#00BFFF"
                        },
                        ["Pick Axe"] = new Dictionary<string, string>()
                        {
                            ["Кирка"] = "#00BFFF"
                        },
                        ["Salvaged Axe"] = new Dictionary<string, string>()
                        {
                            ["Топор"] = "#00BFFF"
                        },
                        ["Chainsaw"] = new Dictionary<string, string>()
                        {
                            ["Бензопила"] = "#00BFFF"
                        },
                        ["Jackhammer"] = new Dictionary<string, string>()
                        {
                            ["Дробилка камней"] = "#00BFFF"
                        },
                        ["Salvaged Hammer"] = new Dictionary<string, string>()
                        {
                            ["Молот"] = "#00BFFF"
                        },
                        ["Salvaged Icepick"] = new Dictionary<string, string>()
                        {
                            ["Ледоруб"] = "#00BFFF"
                        },
                        ["Satchel Charge"] = new Dictionary<string, string>()
                        {
                            ["Сумка с зарядом"] = "#00BFFF"
                        },
                        ["Stone Hatchet"] = new Dictionary<string, string>()
                        {
                            ["Каменный топор"] = "#00BFFF"
                        },
                        ["Snowball"] = new Dictionary<string, string>()
                        {
                            ["Снежок"] = "#00BFFF"
                        },
                        ["Stone Pickaxe"] = new Dictionary<string, string>()
                        {
                            ["Каменная кирка"] = "#00BFFF"
                        },
                        ["Survey Charge"] = new Dictionary<string, string>()
                        {
                            ["Геозаряд"] = "#00BFFF"
                        },
                        ["Timed Explosive Charge"] = new Dictionary<string, string>()
                        {
                            ["Таймер С4"] = "#F50000"
                        },
                        ["Torch"] = new Dictionary<string, string>()
                        {
                            ["Факел"] = "#00BFFF"
                        },
                        ["RocketSpeed"] = new Dictionary<string, string>()
                        {
                            ["Скоростная ракета"] = "#F50000"
                        },
                        ["Incendiary Rocket"] = new Dictionary<string, string>()
                        {
                            ["Зажигательная ракета"] = "#F50000"
                        },
                        ["Rocket"] = new Dictionary<string, string>()
                        {
                            ["Обычная ракета"] = "#F50000"
                        },
                        ["RocketHeli"] = new Dictionary<string, string>()
                        {
                            ["Вертолетный напалм"] = "#F50000"
                        },
                        ["RocketBradley"] = new Dictionary<string, string>()
                        {
                            ["Танковый напалм"] = "#F50000"
                        },
                        ["MainCannonShell"] = new Dictionary<string, string>()
                        {
                            ["Танковые ракеты"] = "#F50000"
                        },
                        ["L96 Rifle"] = new Dictionary<string, string>()
                        {
                            ["AWP"] = "#F50000"
                        }
                    },
                    DynamicalSymbolsHighlight = new Dictionary<string, string>()
                    {
                        ["→"] = "#000000",
                        ["("] = "#000000",
                        ["|"] = "#000000",
                        [")"] = "#000000",
                    },
                };
            }
        }
        protected override void LoadDefaultConfig()
        {
            m_Config = RustyDeathConfig.Prototype();

            PrintWarning("Creating default a configuration file ...");
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();

            m_Config = Config.ReadObject<RustyDeathConfig>();
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(m_Config);
        }
        #endregion

        #region Custom
        public class DeathPlayerData
        {
            public BaseEntity Entity;
            public BasePlayer Object;
            public string Name;
            public EntityType Type;
            public Dictionary<string, Dictionary<string, string>> CustomEntityNames;

            public DeathPlayerData(BaseEntity entity, Dictionary<string, Dictionary<string, string>> customNames)
            {
                Entity = entity;
                CustomEntityNames = customNames;

                Boot();
            }

            private void Boot()
            {
                if (Entity == null)
                {
                    return;
                }
                else
                {
                    Object = Entity?.ToPlayer();
                    Type = SetType();
                    Name = SetName();
                }
            }
            private string SetName()
            {
                switch (Type)
                {
                    case EntityType.Player:
                        {
                            return Object.displayName ?? "UnknownPlayer";
                        }

                    case EntityType.Animal:
                        {
                            if (CustomEntityNames.ContainsKey(Type.ToString()))
                            {
                                if (Entity.name.Contains(Animal.Bear.ToString().ToLower()) && !Entity.name.Contains("beartrap.prefab"))
                                {
                                    if (CustomEntityNames[Type.ToString()].ContainsKey(Animal.Bear.ToString()))
                                    {
                                        return CustomEntityNames[Type.ToString()][Animal.Bear.ToString()];
                                    }
                                    else
                                    {
                                        return "Bear";
                                    }
                                }
                                else if (Entity.name.Contains(Animal.Boar.ToString().ToLower()))
                                {
                                    if (CustomEntityNames[Type.ToString()].ContainsKey(Animal.Boar.ToString()))
                                    {
                                        return CustomEntityNames[Type.ToString()][Animal.Boar.ToString()];
                                    }
                                    else
                                    {
                                        return "Boar";
                                    }
                                }
                                else if (Entity.name.Contains(Animal.Wolf.ToString().ToLower()))
                                {
                                    if (CustomEntityNames[Type.ToString()].ContainsKey(Animal.Wolf.ToString()))
                                    {
                                        return CustomEntityNames[Type.ToString()][Animal.Wolf.ToString()];
                                    }
                                    else
                                    {
                                        return "Wolf";
                                    }
                                }
                                else if (Entity.name.Contains(Animal.Horse.ToString().ToLower()))
                                {
                                    if (CustomEntityNames[Type.ToString()].ContainsKey(Animal.Horse.ToString()))
                                    {
                                        return CustomEntityNames[Type.ToString()][Animal.Horse.ToString()];
                                    }
                                    else
                                    {
                                        return "Horse";
                                    }
                                }
                                else if (Entity.name.Contains(Animal.Chicken.ToString().ToLower()))
                                {
                                    if (CustomEntityNames[Type.ToString()].ContainsKey(Animal.Chicken.ToString()))
                                    {
                                        return CustomEntityNames[Type.ToString()][Animal.Chicken.ToString()];
                                    }
                                    else
                                    {
                                        return "Boar";
                                    }
                                }
                                else if (Entity.name.Contains(Animal.Stag.ToString().ToLower()))
                                {
                                    if (CustomEntityNames[Type.ToString()].ContainsKey(Animal.Stag.ToString()))
                                    {
                                        return CustomEntityNames[Type.ToString()][Animal.Stag.ToString()];
                                    }
                                    else
                                    {
                                        return "Stag";
                                    }
                                }
                                else
                                {
                                    return "UnknownAnimal";
                                }
                            }
                            else
                            {
                                return "UnknownAnimal";
                            }
                        }

                    case EntityType.Turret:
                        {
                            if (CustomEntityNames.ContainsKey(Type.ToString()))
                            {
                                if (Entity.name.Contains("autoturret_deployed"))
                                {
                                    if (CustomEntityNames[Type.ToString()].ContainsKey("Autoturret"))
                                    {
                                        return CustomEntityNames[Type.ToString()]["Autoturret"];
                                    }
                                    else
                                    {
                                        return "Autoturret";
                                    }
                                }
                                else if (Entity.name.Contains("flameturret.deployed"))
                                {
                                    if (CustomEntityNames[Type.ToString()].ContainsKey("Flameturret"))
                                    {
                                        return CustomEntityNames[Type.ToString()]["Flameturret"];
                                    }
                                    else
                                    {
                                        return "Flametuuret";
                                    }
                                }
                                else if (Entity.name.Contains("sentry.scientist.static"))
                                {
                                    if (CustomEntityNames[Type.ToString()].ContainsKey("Scientist"))
                                    {
                                        return CustomEntityNames[Type.ToString()]["Scientist"];
                                    }
                                    else
                                    {
                                        return "Scientist";
                                    }
                                }
                                else
                                {
                                    return "UnknownTurret";
                                }
                            }
                            else
                            {
                                return "UnknownTurret";
                            }
                        }

                    case EntityType.Helicopter:
                    case EntityType.Tank:
                    case EntityType.Npc:
                    case EntityType.Zombie:
                        {
                            if (CustomEntityNames.ContainsKey(Type.ToString()))
                            {
                                if (CustomEntityNames[Type.ToString()].ContainsKey(Type.ToString()))
                                {
                                    return CustomEntityNames[Type.ToString()][Type.ToString()];
                                }
                                else
                                {
                                    return $"Unknown{Type.ToString()}";
                                }
                            }
                            else
                            {
                                return $"Unknown{Type.ToString()}";
                            }
                        }

                    case EntityType.Building:
                        {
                            if (CustomEntityNames.ContainsKey(Type.ToString()))
                            {
                                if (Entity.name.Contains("barricade.metal"))
                                {
                                    if (CustomEntityNames.ContainsKey(Barricades.MetalBarricade.ToString()))
                                    {
                                        return CustomEntityNames[Type.ToString()][Barricades.MetalBarricade.ToString()];
                                    }
                                    else
                                    {
                                        return "Metal Barricade";
                                    }
                                }
                                else if (Entity.name.Contains("barricade.wood"))
                                {
                                    if (CustomEntityNames.ContainsKey(Barricades.WoodenBarricade.ToString()))
                                    {
                                        return CustomEntityNames[Type.ToString()][Barricades.WoodenBarricade.ToString()];
                                    }
                                    else
                                    {
                                        return "Wooden Barricade";
                                    }
                                }
                                else if (Entity.name.Contains("barricade.woodwire"))
                                {
                                    if (CustomEntityNames[Type.ToString()].ContainsKey(Barricades.BarbenWoodenBarricade.ToString()))
                                    {
                                        return CustomEntityNames[Type.ToString()][Barricades.BarbenWoodenBarricade.ToString()];
                                    }
                                    else
                                    {
                                        return "Barben Barricade";
                                    }
                                }
                                else if (Entity.name.Contains("wall.external.high.wood"))
                                {
                                    if (CustomEntityNames[Type.ToString()].ContainsKey(Barricades.WallExternalHighWood.ToString()))
                                    {
                                        return CustomEntityNames[Type.ToString()][Barricades.WallExternalHighWood.ToString()];
                                    }
                                    else
                                    {
                                        return "Wooden Wall";
                                    }
                                }
                                else if (Entity.name.Contains("wall.external.high.stone"))
                                {
                                    if (CustomEntityNames[Type.ToString()].ContainsKey(Barricades.WallExternalHighStone.ToString()))
                                    {
                                        return CustomEntityNames[Type.ToString()][Barricades.WallExternalHighStone.ToString()];
                                    }
                                    else
                                    {
                                        return "Stone Wall";
                                    }
                                }
                                else if (Entity.name.Contains("gates.external.high.wood"))
                                {
                                    if (CustomEntityNames[Type.ToString()].ContainsKey(Barricades.WallExternalHighStone.ToString()))
                                    {
                                        return CustomEntityNames[Type.ToString()][Barricades.WallExternalHighStone.ToString()];
                                    }
                                    else
                                    {
                                        return "Wooden Gates";
                                    }
                                }
                                else if (Entity.name.Contains("gates.external.high.stone"))
                                {
                                    if (CustomEntityNames[Type.ToString()].ContainsKey(Barricades.WallExternalHighStone.ToString()))
                                    {
                                        return CustomEntityNames[Type.ToString()][Barricades.WallExternalHighStone.ToString()];
                                    }
                                    else
                                    {
                                        return "Stone Gates";
                                    }
                                }
                                else
                                {
                                    return "UnknownBarricade";
                                }
                            }
                            else
                            {
                                return "UnknownBarricade";
                            }
                        }

                    case EntityType.Trap:
                        {
                            if (CustomEntityNames.ContainsKey(Type.ToString()))
                            {
                                if (Entity.name.Contains("beartrap.prefab"))
                                {
                                    if (CustomEntityNames[Type.ToString()].ContainsKey(Trap.Snaptrap.ToString()))
                                    {
                                        return CustomEntityNames[Type.ToString()][Trap.Snaptrap.ToString()];
                                    }
                                    else
                                    {
                                        return "Snap Trap";
                                    }
                                }
                                else if (Entity.name.Contains("landmine"))
                                {
                                    if (CustomEntityNames[Type.ToString()].ContainsKey(Trap.Minetrap.ToString()))
                                    {
                                        return CustomEntityNames[Type.ToString()][Trap.Minetrap.ToString()];
                                    }
                                    else
                                    {
                                        return "Mine Trap";
                                    }
                                }
                                else if (Entity.name.Contains("guntrap.deployed"))
                                {
                                    if (CustomEntityNames[Type.ToString()].ContainsKey(Trap.Guntrap.ToString()))
                                    {
                                        return CustomEntityNames[Type.ToString()][Trap.Guntrap.ToString()];
                                    }
                                    else
                                    {
                                        return "Gun Trap";
                                    }
                                }
                                else if (Entity.name.Contains("spikes.floor"))
                                {
                                    if (CustomEntityNames[Type.ToString()].ContainsKey(Trap.Spikes.ToString()))
                                    {
                                        return CustomEntityNames[Type.ToString()][Trap.Spikes.ToString()];
                                    }
                                    else
                                    {
                                        return "Gun Trap";
                                    }
                                }
                                else
                                {
                                    return "UnknownTrap";
                                }
                            }
                            else
                            {
                                return "UnknownTrap";
                            }
                        }

                    default:
                        {
                            return "UnknownEntity";
                        }
                }
            }
            private EntityType SetType()
            {
                if (Entity == null) throw new Exception("Don't bootable entity. Please check the code");
                if (Entity.name.Contains("machete.weapon")) return EntityType.Zombie;
                if (Entity is NPCMurderer) return EntityType.Zombie;
                if (Entity is NPCPlayer) return EntityType.Npc;
                if (Entity is BasePlayer) return EntityType.Player;
                if (Entity is BaseHelicopter) return EntityType.Helicopter;
                if (Entity is BradleyAPC) return EntityType.Tank;
                if (Entity.name.Contains("agents/")) return EntityType.Animal;
                if (Entity.name.Contains("barricades/") || Entity.name.Contains("gates.external.high") || Entity.name.Contains("wall.external.high")) return EntityType.Building;
                if (Entity.name.Contains("autoturret_deployed.prefab") || Entity.name.Contains("flameturret_deployed.prefab") || Entity.name.Contains("sentry.scientist.static")) return EntityType.Turret;
                if (Entity.name.Contains("beartrap.prefab") || Entity.name.Contains("landmine.prefab") || Entity.name.Contains("guntrap.deployed") || Entity.name.Contains("spikes.floor.prefab")) return EntityType.Trap;
                if (Entity.name.Contains("scientist")) return EntityType.Scientist;
                if (Entity.name.Contains("barrel")) return EntityType.Barrel;

                return EntityType.Invalid;
            }
        }
        public class DeathInfo
        {
            public DeathPlayerData Attacker;
            public DeathPlayerData Victim;
            public string Weapon;
            public string RawWeapon;
            public string Name;
            public float Distance;
            public DeathType Reason;

            public DeathInfo(DeathPlayerData killer, DeathPlayerData victim, float dist, string weapon, string dmgType, string raw)
            {
                Attacker = killer;
                Victim = victim;
                Weapon = weapon;
                Name = Attacker.Name;
                Distance = dist;
                RawWeapon = raw;
                Reason = GetReason(dmgType);
            }

            private DeathType GetReason(string dmgType)
            {
                if ((Attacker.Type == EntityType.Player) && (Attacker.Object != Victim.Object))
                {
                    if (RawWeapon == "Rocket" || RawWeapon == "RocketSpeed" || RawWeapon == "RocketHeli" || RawWeapon == "RocketBradley" || RawWeapon == "F1 Grenade" || RawWeapon == "Survey Charge" || RawWeapon == "Timed Explosive Charge" || RawWeapon == "Satchel Charge" || RawWeapon == "Beancan Grenade")
                    {
                        return DeathType.ExplosionPlayer;
                    }
                }

                var reasons = (Enum.GetValues(typeof(DeathType)) as DeathType[]).Where(x => x.ToString().Contains(dmgType));

                if (reasons.Count() != 0)
                {
                    if (Attacker.Type != EntityType.Building)
                    {
                        return reasons.First();
                    }
                    else if ((reasons.First() == DeathType.Explosion) && (Victim.Name == "UnknownEntity"))
                    {
                        return DeathType.Default;
                    }
                }
  

                switch (Attacker.Type)
                {
                    case EntityType.Player:
                        {
                            switch (Victim.Type)
                            {
                                case EntityType.Player:
                                    {
                                        return DeathType.PlayerToPlayer;
                                    }

                                case EntityType.Npc:
                                    {
                                        return DeathType.PlayerToNpc;
                                    }

                                case EntityType.Animal:
                                    {
                                        return DeathType.PlayerToAnimal;
                                    }

                                case EntityType.Helicopter:
                                    {
                                        return DeathType.PlayerToHelicopter;
                                    }

                                case EntityType.Tank:
                                    {
                                        return DeathType.PlayerToTank;
                                    }

                                case EntityType.Sleeper:
                                    {
                                        return DeathType.PlayerToSleeper;
                                    }

                                case EntityType.Zombie:
                                    {
                                        return DeathType.PlayerToZombie;
                                    }

                                default:
                                    {
                                        return DeathType.Default;
                                    }
                            }
                        }

                    case EntityType.Npc:
                        {
                            if (Victim.Type == EntityType.Player)
                            {
                                return DeathType.NpcToPlayer;
                            }

                            return DeathType.Default;
                        }

                    case EntityType.Animal:
                        {
                            if (Victim.Type == EntityType.Player)
                            {
                                return DeathType.AnimalToPlayer;
                            }

                            return DeathType.Default;
                        }

                    case EntityType.Building:
                        {
                            if (Victim.Type == EntityType.Player)
                            {
                                return DeathType.BuildingToPlayer;
                            }

                            return DeathType.Default;
                        }

                    case EntityType.Helicopter:
                        {
                            if (Victim.Type == EntityType.Player)
                            {
                                return DeathType.HelicopterToPlayer;
                            }

                            return DeathType.Default;
                        }

                    case EntityType.Tank:
                        {
                            if (Victim.Type == EntityType.Player)
                            {
                                return DeathType.TankToPlayer;
                            }

                            return DeathType.Default;
                        }

                    case EntityType.Trap:
                        {
                            if (Victim.Type == EntityType.Player)
                            {
                                return DeathType.TrapToPlayer;
                            }

                            return DeathType.Default;
                        }

                    case EntityType.Turret:
                        {
                            if (Victim.Type == EntityType.Player)
                            {
                                return DeathType.TurretToPlayer;
                            }

                            return DeathType.Default;
                        }

                    case EntityType.Zombie:
                        {
                            if (Victim.Type == EntityType.Player)
                            {
                                return DeathType.ZombieToPlayer;
                            }

                            return DeathType.Default;
                        }

                    default:
                        {
                            return DeathType.Default;
                        }
                }
            }
        }
        public class DeathHistory
        {
            public List<DeathInfo> History { get; private set; }

            private int LastRewriteIndex = 0;
            private int MaxCount = -1;

            public DeathHistory(int max)
            {
                MaxCount = max;
                History = new List<DeathInfo>();
            }

            public void Push(DeathInfo info)
            {
                if (MaxCount == -1)
                {
                    History.Add(info);
                }
                else
                {
                    if (History.Count >= MaxCount)
                    {
                        History[LastRewriteIndex] = info;
                        LastRewriteIndex++;

                        if (LastRewriteIndex >= MaxCount)
                        {
                            LastRewriteIndex = 0;
                        }
                    }
                    else
                    {
                        History.Add(info);
                    }
                }
            }
        }
        #endregion

        #region Initialization

        public RustyDeath()
        {
            m_DeathInformation   = new List<DeathInfo>();
            m_PlayerDeathHistory = new Dictionary<BasePlayer, DeathHistory>();
            m_LastPlayersHits    = new Dictionary<ulong, HitInfo>();
            m_LastHelicopterHits = new Dictionary<uint, HitInfo>();
        }
        private void RegisterPermissions()
        {
            try
            {
                foreach (var perm in m_Config.Permissions)
                {
                    if (!permission.PermissionExists(perm.Key, this))
                    {
                        permission.RegisterPermission(perm.Key, this);
                        PrintWarning($"Adding new permission. Name: '{perm.Key}' Color:'{perm.Value}' ");
                    }
                }
            }
            catch(Exception ex)
            {
                PrintError(ex.Message + "\n" + ex.StackTrace);
            }
        }
        #endregion

        #region Hooks
        private void OnServerInitialized()
        {
            m_Config = Config.ReadObject<RustyDeathConfig>();
        }
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BasePlayer) m_LastPlayersHits[entity.ToPlayer().userID] = info;
            if (entity is BaseHelicopter && info.InitiatorPlayer != null) m_LastHelicopterHits[entity.net.ID] = info;
        }
        private void OnEntityDeath(BaseCombatEntity entity, HitInfo hit)
        {
            try
            {
                if (hit == null)
                    if (!entity.ToPlayer().IsWounded() || !m_LastPlayersHits.TryGetValue(entity.ToPlayer().userID, out hit))
                        return;

                DeathPlayerData victim = new DeathPlayerData(entity, m_Config.CustomEntitiesNames);

                if (victim.Type == EntityType.Invalid) return;
                if (victim.Type == EntityType.Barrel)  return;
                if (victim.Type == EntityType.Building) return;
                if (entity is BaseCorpse) return;

                DeathPlayerData killer = null;
                if (victim.Type == EntityType.Helicopter)
                {
                    if (m_LastHelicopterHits.ContainsKey(entity.net.ID))
                    {
                        hit = m_LastHelicopterHits[entity.net.ID];
                        killer = new DeathPlayerData(hit.Initiator, m_Config.CustomEntitiesNames);
                    }
                }
                else
                {
                    killer = new DeathPlayerData(hit.Initiator, m_Config.CustomEntitiesNames);
                }

                if (victim == null) return;
                if (killer == null) return;
                if(BasePlayer.sleepingPlayerList.Contains(victim.Object))
                {
                    victim.Type = EntityType.Sleeper;
                }
                if(killer.Type != EntityType.Player && victim.Type != EntityType.Player)
                {
                    return;
                }
                if ((killer.Type == EntityType.Animal && !m_Config.ShowDeathAnimals) ||
                    (victim.Type == EntityType.Animal && !m_Config.ShowDeathAnimals)) return;


                if ((killer.Type == EntityType.Npc && !m_Config.ShowDeathNpcs) ||
                    (victim.Type == EntityType.Npc && !m_Config.ShowDeathNpcs)) return;


                if ((killer != null) && (killer.Type == EntityType.Player) && (victim.Type == EntityType.Sleeper) && !m_Config.ShowDeathSleepers) return;

                string rawWeapon = hit?.Weapon?.GetItem()?.info?.displayName?.english ?? FormatName(hit?.WeaponPrefab?.name);
                string weapon = GetWeaponCustomName(rawWeapon);
                float distance = hit.ProjectileDistance;

                if (distance == 0)
                {
                    if (killer?.Entity != null)
                    {
                        distance = killer.Entity.Distance(entity);
                    }
                }

                DeathInfo info = new DeathInfo(killer, victim, distance, weapon, entity.lastDamage.ToString(), rawWeapon);

                if (m_Config.EnableDeathHistory && (killer.Type == EntityType.Player && victim.Type == EntityType.Player))
                {
                    if (!m_PlayerDeathHistory.ContainsKey(info.Attacker.Object))
                    {
                        m_PlayerDeathHistory.Add(info.Attacker.Object, new DeathHistory(m_Config.MaxDeathHistoryMembers));
                    }
                    m_PlayerDeathHistory[info.Attacker.Object].Push(info);

                    if (!m_PlayerDeathHistory.ContainsKey(info.Victim.Object))
                    {
                        m_PlayerDeathHistory.Add(info.Victim.Object, new DeathHistory(m_Config.MaxDeathHistoryMembers));
                    }
                    m_PlayerDeathHistory[info.Victim.Object].Push(info);

                }

                AddDeathInfo(info);
            }
            catch(NullReferenceException) { }
        }
        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                DestroyUI(player);
        }
        #endregion

        #region Instruments
        private string FormatName(string prefab)
        {
            if (string.IsNullOrEmpty(prefab))
                return string.Empty;
            var reply = 1044;
            if (reply == 0) { }
            var formatedPrefab = FirstUpper(prefab.Split('/').Last().Replace(".prefab", "").Replace(".entity", "").Replace(".weapon", "").Replace(".deployed", "").Replace("_", "."));
            switch (formatedPrefab)
            {
                case "Stone.hatchet": return "Stone Hatchet";
                case "bow.hunting": return "Hunting Bow";
                case "Bone.club": return "Bone Club";
                case "Stone.pickaxe": return "Stone Pickaxe";
                case "Survey.charge": return "Survey Charge";
                case "Explosive.satchel": return "Satchel Charge";
                case "Explosive.timed": return "Timed Explosive Charge";
                case "Grenade.beancan": return "Beancan Grenade";
                case "Grenade.f1": return "F1 Grenade";
                case "Candy.cane": return "Candy Cane Club";
                case "Snowball": return "Snowball";
                case "Hammer.salvaged": return "Salvaged Hammer";
                case "Axe.salvaged": return "Salvaged Axe";
                case "Icepick.salvaged": return "Salvaged Icepick";
                case "Spear.stone": return "Stone Spear";
                case "Spear.wooden": return "Wooden Spear";
                case "Knife.bone": return "Bone Knife";
                case "Rocket.basic": return "Rocket";
                case "Flamethrower": return "Flamethrower";
                case "Rocket.hv": return "RocketSpeed";
                case "Rocket.heli": return "RocketHeli";
                case "Rocket.bradley": return "RocketBradley";
                case "sentry.scientist.static": return "Static Turret";
                case "M249": return "M249";
                case "L96": return "L96 Rifle";
                default: return formatedPrefab;
            }
        }
        private string FirstUpper(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;

            return string.Join(" ", str.Split(' ').Select(x => x.Substring(0, 1).ToUpper() + x.Substring(1, x.Length - 1)).ToArray());
        }
        private string GetWeaponCustomName(string engWeaponName)
        {
            string custom = "Unknown Weapon";

            try
            {
                if(m_Config.CustomWeaponSettings.ContainsKey(engWeaponName))
                {
                    custom = m_Config.CustomWeaponSettings[engWeaponName].First().Key;
                }
            }
            catch(Exception)
            {
                return custom;
            }

            return custom;
        }
        private string GetWeaponCustomColor(string engWeaponName)
        {
            string custom = "#FDF00F";

            try
            {
                if (m_Config.CustomWeaponSettings.ContainsKey(engWeaponName))
                {
                    custom = m_Config.CustomWeaponSettings[engWeaponName].First().Value;
                }
            }
            catch (Exception)
            {
                return custom;
            }

            return custom;
        }

        private void AddDeathInfo(DeathInfo info)
        {
            if (m_DeathInformation.Count > 8)
                m_DeathInformation.RemoveRange(7, m_DeathInformation.Count - 8);

            m_DeathInformation.Insert(m_DeathInformation.Count(), info);

            RefreshUI(info);
            timer.Once(m_Config.ShowTiming, () =>
            {
                m_DeathInformation.Remove(info);
                RefreshUI(info);
            });
        }
        private string GetCustomMessage(string deathType)
        {
            if(m_Config.PlayerDeathMessages.ContainsKey(deathType))
            {
                return m_Config.PlayerDeathMessages[deathType].GetRandom();
            }

            return $"";
        }
        private string[] GetHighlights(DeathInfo info)
        {
            string[] highlts = new string[4];

            highlts[0] = GetPlayerColor(info.Attacker.Object, m_Config.HighlightKillerDefault);
            highlts[1] = GetPlayerColor(info.Victim.Object, m_Config.HighlightVictimDefault);
            highlts[2] = GetWeaponCustomColor(info.RawWeapon);
            highlts[3] = GetDistanceColor(info.Distance);

            return highlts;
        }
        private string GetDistanceColor(float distance)
        {
            string color = m_Config.HighlightDistanceDefault;
            try
            {
                foreach(var distances in m_Config.DistanceHighlights)
                {
                    if(distance <= distances.Key)
                    {
                        color = distances.Value;

                        break;
                    }
                }
            }
            catch(Exception)
            {
                return color;
            }

            return color;
        }
        private string GetPlayerColor(BasePlayer obj, string @default)
        {
            RegisterPermissions();

            try
            {
                foreach (var perm in m_Config.Permissions)
                {
                    if (permission.UserHasPermission(obj.UserIDString, perm.Key))
                    {
                        @default = perm.Value;

                        break;
                    }
                }
            }
            catch (Exception)
            {
                return @default;
            }

            return @default;
        }
        private string GetReadyCustomMessage(DeathInfo info, bool attackerSelf = false, bool victimSelf = false)
        {
            string[] highlights = GetHighlights(info);
            string raw = GetCustomMessage(info.Reason.ToString());

            if (attackerSelf && m_Config.EnableSelfHighlight)
            {
                try
                {
                    string ready = string.Format(raw, highlights[0], info.Attacker.Name, highlights[1], info.Victim.Name, highlights[2], info.Weapon, highlights[3], (int)info.Distance);
                    foreach(var symbol in m_Config.DynamicalSymbolsHighlight)
                    {
                        ready = ready.Replace(symbol.Key, $"<color={symbol.Value}>{symbol.Key}</color>");
                    }

                    return ready;
                }
                catch(Exception)
                {
                    return string.Format(raw, m_Config.SelfHighlightColor, info.Attacker.Name, highlights[1], info.Victim.Name, highlights[2], info.Weapon, highlights[3], (int)info.Distance);
                }
            }
            else if (victimSelf && m_Config.EnableSelfHighlight)
            {
                try
                {
                    string ready = string.Format(raw, highlights[0], info.Attacker.Name, highlights[1], info.Victim.Name, highlights[2], info.Weapon, highlights[3], (int)info.Distance);
                    foreach (var symbol in m_Config.DynamicalSymbolsHighlight)
                    {
                        ready = ready.Replace(symbol.Key, $"<color={symbol.Value}>{symbol.Key}</color>");
                    }

                    return ready;
                }
                catch(Exception)
                {
                    return string.Format(raw, highlights[0], info.Attacker.Name, m_Config.SelfHighlightColor, info.Victim.Name, highlights[2], info.Weapon, highlights[3], (int)info.Distance);
                }
            }
            else
            {
                return string.Format(raw, highlights[0], info.Attacker.Name, highlights[1], info.Victim.Name, highlights[2], info.Weapon, highlights[3], (int)info.Distance);
            }
        }
        #endregion

        #region CUI
        private void RefreshUI(DeathInfo note)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyUI(player);
                InitilizeUI(player);
            }
        }
        private void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "rustydeathUI");
        }
        private void InitilizeUI(BasePlayer player)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = m_Config.AnchorMin, AnchorMax = m_Config.AnchorMax }
            }, name: "rustydeathUI");

            double index = 1;
            foreach (var note in m_DeathInformation)
            {
                if (note.Attacker.Object == player)
                {
                    InitilizeLabel(container, note, $"0 {index - 0.2}", $"0.99 {index}", true, false);
                }
                else if(note.Victim.Object == player)
                {
                    InitilizeLabel(container, note, $"0 {index - 0.2}", $"0.99 {index}", false, true);
                }
                else
                {
                    InitilizeLabel(container, note, $"0 {index - 0.2}", $"0.99 {index}", false, false);
                }

                index -= 0.14;
            }
 
            CuiHelper.AddUi(player, container);
        }
        private string InitilizeLabel(CuiElementContainer container, DeathInfo info, string anchorMin, string anchorMax, bool attackerSelf, bool victimSelf)
        {
            string Name = CuiHelper.GetGuid();
            string[] highlights = GetHighlights(info);

            string text = GetReadyCustomMessage(info, attackerSelf, victimSelf);

            UnityEngine.TextAnchor anchor = UnityEngine.TextAnchor.MiddleLeft;

            if (m_Config.FixedInRight)
            {
                anchor = UnityEngine.TextAnchor.MiddleRight;
            }

            container.Add(new CuiElement
            {
                Name = Name,
                Parent = "rustydeathUI",
                Components =
                {
                    new CuiTextComponent { Align = anchor,Font = "robotocondensed-bold.ttf", FontSize = m_Config.FontSize, Text = text },
                    new CuiRectTransformComponent { AnchorMin = anchorMin, AnchorMax = anchorMax },
                    new CuiOutlineComponent { Color = "0 0 0 1", Distance = "1.0 -0.5" }
                }
            });

            return Name;
        }
        #endregion

        #region Checkers
        private bool IsNormallyDeath(DeathType type)
        {
            if ((type == DeathType.AnimalToPlayer) ||
                (type == DeathType.PlayerToAnimal) ||
                (type == DeathType.BuildingToPlayer) ||
                (type == DeathType.GuntrapToPlayer) ||
                (type == DeathType.HelicopterToPlayer) ||
                (type == DeathType.NpcToPlayer) ||
                (type == DeathType.PlayerToHelicopter) ||
                (type == DeathType.PlayerToNpc) || 
                (type == DeathType.PlayerToPlayer) ||
                (type == DeathType.PlayerToSleeper) ||
                (type == DeathType.PlayerToTank) ||
                (type == DeathType.PlayerToZombie) ||
                (type == DeathType.TankToPlayer) ||
                (type == DeathType.TrapToPlayer) ||
                (type == DeathType.TurretToPlayer) ||
                (type == DeathType.ZombieToPlayer)) return true;

            else return false;
        }
        #endregion

        #region Commands
        public string Green = "#95BB42";
        public string Red   = "#ff0000ff";

        [ChatCommand("rd.history")]
        void CmdChatDeathHistory(BasePlayer player, string command, string[] args)
        {
            if (m_PlayerDeathHistory.ContainsKey(player))
            {
                SendReply(player, $"История убийств и смертей: ({m_PlayerDeathHistory[player].History.Count} элементов)");

                foreach (var msg in m_PlayerDeathHistory[player].History)
                {
                    if (msg.Victim.Object == player)
                    {
                        SendReply(player, $"<color={Red}>Атакующий: '{msg.Attacker.Name}', жертва: '{msg.Victim.Name}', дистанция: '{msg.Distance}', оружие: '{msg.Weapon}'</color>");
                    }
                    else
                    {
                        SendReply(player, $"<color={Green}>Атакующий: '{msg.Attacker.Name}', жертва: '{msg.Victim.Name}', дистанция: '{msg.Distance}', оружие: '{msg.Weapon}'</color>");
                    }
                }
            }
            else
            {
                SendReply(player, "Нет доступной истории убийств");
            }
        }
        #endregion
    }
}
