using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VLB;

namespace Oxide.Plugins
{
    [Info("MagicTree", "OxideBro", "1.2.0")]
    public class MagicTree : RustPlugin
    {
        #region Configuration
        public class Seed
        {
            public string shortname;
            public string name;
            public ulong skinId;
        }

        public class Wood
        {
            [JsonProperty("UID Дерева")]
            public uint woodId;
            [JsonProperty("Осталось времени")]
            public int NeedTime;

            [JsonProperty("Оставшееся время до разрушения")]
            public int NeedTimeToDestroy = -1;
            [JsonProperty("Этап")]
            public int CurrentStage;
            [JsonProperty("Позиция")]
            public Vector3 woodPos;
            [JsonProperty("Ящики")]
            public List<uint> BoxListed = new List<uint>();
            [JsonIgnore] public List<BaseEntity> boxes = new List<BaseEntity>();
        }

        public class BoxItemsList
        {
            [JsonProperty("Shortname предмета")]
            public string ShortName;
            [JsonProperty("Минимальное количество")]
            public int MinAmount;
            [JsonProperty("Максимальное количество")]
            public int MaxAmount;
            [JsonProperty("Шанс что предмет будет добавлен (максимально 100%)")]
            public int Change;
            [JsonProperty("SkinID предмета")]
            public ulong SkinID;
            [JsonProperty("Имя предмета при создании (Оставьте поле пустым чтобы использовать стандартное название итема)")]
            public string Name;
            [JsonProperty("Это чертеж")]
            public bool IsBlueprnt;
        }


        public Dictionary<ulong, Dictionary<uint, Wood>> WoodsList = new Dictionary<ulong, Dictionary<uint, Wood>>();

        public Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            {"CmdError", "Неправильно ввели команду." },
            {"DisablePlantSeed", "Семена разрешено садить только в землю" },
            {"CountError", "Неверное кол-во!" },
            {"Permission", "У вас нет прав!" },
            {"SeedGived", "Вам выпала семечка магического дерева!\nПосадите ее и у вас выростет необычное дерево на каком растут ящики с ценными предметами!" },
            {"Wood", "Вы посадили магическое дерево\nСкоро оно вырастет, и даст плоды!" },
            {"InfoTextFull",  "<size=25><b>Магическое дерево</b></size>\n<size=17>\nПЛОДЫ ДОЗРЕЛИ, ВЫ МОЖЕТЕ ИХ СОБРАТЬ</size>"},
            {"InfoDdraw", "<size=25><b>Магическое дерево</b></size>\n<size=17>Этап созревания дерева: {0}/{1}\n\nВремя до полного созревания: {2}</size>" }
        };

        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Благодарим за покупку плагина на сайте RustPlugin.ru. Если вы передадите этот плагин сторонним лицам знайте - это лишает вас гарантированных обновлений!");
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
            if (config.PluginVersion < Version)
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

        private class PluginConfig
        {
            [JsonProperty("Время роста дерева в секундах")]
            public int Time;

            [JsonProperty("Время существования дерева после полного созревания")]
            public int TimetoDestroy = 3600;

            [JsonProperty("Посадка деревьев разрешена только в земле (запрещены плантации и прочее)")]
            public bool PlanterBoxDisable = true;

            [JsonProperty("Множитель добычи при финальной срубке магического дерева")]
            public int Bonus = 1;

            [JsonProperty("Количество вещей в ящике")]
            public int ItemsCount;

            [JsonProperty("Кол-во ящиков на дереве")]
            public int BoxCount;

            [JsonProperty("Список префабов этапов дерева")]
            public List<string> Stages;

            [JsonProperty("Права на выдачу")]
            public string Permission = "seed.perm";

            [JsonProperty("Тип ящика")]
            public string CrateBasic = "assets/bundled/prefabs/radtown/crate_basic.prefab";

            [JsonProperty("Шанс выпадения зерна с дерева (макс-100)")]
            public int Chance;

            [JsonProperty("Настройка лута в ящиках")]
            public List<BoxItemsList> casesItems;
            [JsonProperty("Ссылка на удачный эффект")]
            public string SucEffect;
            [JsonProperty("Ссылка на эффект ошибки")]
            public string ErrorEffect;
            [JsonProperty("Настройка зерна")]
            public Seed seed;
            [JsonProperty("Версия конфигурации")]
            public VersionNumber PluginVersion = new VersionNumber();

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    PluginVersion = new VersionNumber(),
                    ItemsCount = 2,
                    Permission = "MagicTree.perm",
                    CrateBasic = "assets/bundled/prefabs/radtown/crate_underwater_basic.prefab",
                    BoxCount = 4,
                    Chance = 5,
                    Time = 10,
                    seed = new Seed()
                    {
                        shortname = "seed.hemp",
                        name = "Семена магического дерева",
                        skinId = 1787823357
                    },
                    casesItems = new List<BoxItemsList>()
                {
                new BoxItemsList
                {
                ShortName = "stones",
                MinAmount = 300,
                MaxAmount = 1000,
                Change = 100,
                Name = "",
                SkinID = 0,
                IsBlueprnt = false
                },
                },
                    SucEffect = "assets/prefabs/misc/xmas/candy cane club/effects/hit.prefab",
                    ErrorEffect = "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab",
                    Stages = new List<string>()
                    {
                      "assets/prefabs/plants/hemp/hemp.entity.prefab",
                      "assets/bundled/prefabs/autospawn/resource/v2_tundra_forest_small/american_beech_e_dead.prefab",
                      "assets/bundled/prefabs/autospawn/resource/v2_tundra_forest_small/american_beech_d_dead.prefab",
                      "assets/bundled/prefabs/autospawn/resource/v2_tundra_forest_small/oak_a_tundra.prefab",
                      "assets/bundled/prefabs/autospawn/resource/v2_tundra_forest/oak_b_tundra.prefab"
                    },
                };
            }
        }
        #endregion

        #region Oxide

        void LoadData()
        {
            try
            {
                WoodsList = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<uint, Wood>>>($"MagiсTree_Players");
                if (WoodsList == null)
                    WoodsList = new Dictionary<ulong, Dictionary<uint, Wood>>();
            }
            catch
            {
                WoodsList = new Dictionary<ulong, Dictionary<uint, Wood>>();
            }
        }

        void SaveData()
        {
            if (WoodsList != null)
                Interface.Oxide.DataFileSystem.WriteObject($"MagiсTree_Players", WoodsList);
        }

        public static MagicTree ins;

        void OnEntityKill(TreeEntity entity)
        {
            if (entity == null || entity?.net.ID == null || entity.OwnerID == 0) return;
            if (entity.GetComponent<TreeEntity>() != null && entity.GetComponent<TreeConponent>() != null)
            {
                var tree = entity.GetComponent<TreeEntity>();
                if (WoodsList.ContainsKey(tree.OwnerID) && WoodsList[tree.OwnerID].ContainsKey(tree.net.ID))
                    WoodsList[tree.OwnerID].Remove(tree.net.ID);
            }
        }

        void OnServerSave()
        {
            SaveData();
        }

        void Loaded()
        {
            ins = this;
            permission.RegisterPermission(config.Permission, this);
            lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
            LoadData();
        }

        private void OnServerInitialized()
        {
            foreach (var tree in WoodsList)
            {
                foreach (var entity in tree.Value.Keys)
                {
                    BaseNetworkable entitys = BaseNetworkable.serverEntities.Find(entity);
                    if (entitys != null && entitys is TreeEntity)
                        AddOrRemoveComponent("add", null, entitys.GetComponent<TreeEntity>(), entitys.GetComponent<TreeEntity>().OwnerID);
                    else if (entitys != null && entitys is GrowableEntity)
                        AddOrRemoveComponent("add", null, entitys.GetComponent<GrowableEntity>(), entitys.GetComponent<GrowableEntity>().OwnerID);
                    else
                        NextTick(() => { tree.Value.Remove(entity); });
                }
            }
        }

        void AddOrRemoveComponent(string type = "⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠", TreeConponent component = null, BaseEntity tree = null, ulong playerid = 3651584)
        {
            if (!WoodsList.ContainsKey(playerid)) return;
            switch (type)
            {
                case "add":
                    if (!WoodsList[playerid].ContainsKey(tree.net.ID)) return;
                    var data = WoodsList[playerid][tree.net.ID];
                    if (tree != null && data != null)
                    {
                        if (WoodsList[playerid][tree.net.ID].CurrentStage > 2 && WoodsList[playerid][tree.net.ID].BoxListed.Count > 0)
                        {
                            if (tree.GetComponent<TreeConponent>() == null)
                            {
                                tree.gameObject.AddComponent<TreeConponent>().Init(WoodsList[playerid][tree.net.ID]);
                                SpawnBox(data, WoodsList[playerid][tree.net.ID].BoxListed.Count, tree.GetComponent<TreeConponent>().tree, playerid, WoodsList[playerid][tree.net.ID].BoxListed.Count);
                            }
                            return;
                        }
                        else
                            if (tree.GetComponent<TreeConponent>() == null)
                            tree.gameObject.AddComponent<TreeConponent>().Init(WoodsList[playerid][tree.net.ID]);
                    }
                    break;
                case "remove":
                    if (WoodsList[playerid][tree.net.ID].BoxListed.Count > 0)
                    {
                        foreach (var ent in WoodsList[playerid][tree.net.ID].boxes)
                        {
                            if (ent != null && !ent.IsDestroyed)
                                ent.Kill();
                        }
                        WoodsList[playerid][tree.net.ID].boxes.Clear();
                        if (component != null)
                            component.DestroyComponent();
                    }
                    else
                    {
                        if (component != null)
                            component.DestroyComponent();
                    }
                    break;
            }
        }

        void OnEntityBuilt(Planner planner, GameObject gameobject, Vector3 Pos)
        {
            if (planner == null || gameobject == null) return;
            var player = planner.GetOwnerPlayer();
            if (player == null) return;
            BaseEntity entity = gameobject.ToBaseEntity();
            if (entity == null) return;
            if (entity.skinID == config.seed.skinId)
            {
                NextTick(() =>
                {
                    if (entity != null && !entity.IsDestroyed)
                    {
                        if (config.PlanterBoxDisable && entity.GetParentEntity() != null)
                        {
                            if (player == null) return;
                            SendReply(player, Messages["DisablePlantSeed"]);
                            AddSeed(player, 1, false);
                            entity.Kill();
                            return;
                        }

                        SpawnWood(player.userID, entity.transform.position, null, entity);
                        SendReply(player, string.Format(Messages["Wood"]));
                    }


                });
            }

        }

        object OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (dispenser == null || player == null || item == null) return null;
            switch (item.info.shortname)
            {
                case "wood":
                    if (UnityEngine.Random.Range(0f, 100f) < config.Chance)
                    {
                        var activeitem = player.GetActiveItem();
                        if (activeitem != null && !activeitem.info.shortname.Contains("chainsaw"))
                            AddSeed(player, 1);
                    }
                    if (config.Bonus > 1)
                    {
                        TreeEntity wood1 = dispenser.GetComponentInParent<TreeEntity>();
                        if (wood1 != null && wood1.GetComponent<TreeConponent>() != null)
                            item.amount = item.amount * config.Bonus;
                    }
                    break;
            }
            return null;
        }

        object OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (dispenser == null || item == null) return null;
            BasePlayer player = entity?.ToPlayer();
            if (player == null) return null;
            switch (item.info.shortname)
            {
                case "wood":
                    TreeEntity wood1 = dispenser.GetComponentInParent<TreeEntity>();
                    if (wood1 != null && wood1.GetComponent<TreeConponent>() != null)
                    {
                        var component = wood1.GetComponent<TreeConponent>();
                        if (component.data.boxes.Count > 0 && component.data.boxes.Count < 5)
                        {
                            var box = component.data.boxes.ToList().GetRandom();

                            if (box != null && component.data.CurrentStage == config.Stages.Count)
                            {
                                box.SetFlag(BaseEntity.Flags.Busy, false, true);
                                Rigidbody rigidbody = box.gameObject.GetOrAddComponent<Rigidbody>();
                                rigidbody.useGravity = true;
                                rigidbody.isKinematic = false;
                                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                                rigidbody.mass = 2f;
                                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                                rigidbody.angularVelocity = Vector3Ex.Range(-1.75f, 1.75f);
                                rigidbody.drag = 0.5f * (rigidbody.mass / 5f);
                                rigidbody.angularDrag = 0.2f * (rigidbody.mass / 5f);
                                component.data.BoxListed.Remove(box.net.ID);
                                component.data.boxes.Remove(box);

                            }
                            return false;
                        }
                        else if (component.data.BoxListed.Count > 5 && component.data.CurrentStage == config.Stages.Count)
                        {
                            foreach (var box in component.data.boxes)
                            {
                                if (box != null)
                                {
                                    box.SetFlag(BaseEntity.Flags.Busy, false, true);
                                    Rigidbody rigidbody = box.gameObject.GetOrAddComponent<Rigidbody>();
                                    rigidbody.useGravity = true;
                                    rigidbody.isKinematic = false;
                                    rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                                    rigidbody.mass = 2f;
                                    rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                                    rigidbody.angularVelocity = Vector3Ex.Range(-1.75f, 1.75f);
                                    rigidbody.drag = 0.5f * (rigidbody.mass / 5f);
                                    rigidbody.angularDrag = 0.2f * (rigidbody.mass / 5f);
                                }
                            }
                            component.data.BoxListed.Clear();
                            component.data.boxes.Clear();
                            return false;
                        }
                        else
                        {
                            if (component.data.CurrentStage == config.Stages.Count)
                            {
                                dispenser.AssignFinishBonus(player, 1);
                                HitInfo hitInfo = new HitInfo(player, wood1, Rust.DamageType.Generic, wood1.Health(), wood1.transform.position);
                                wood1.OnAttacked(hitInfo);
                                return false;
                            }

                        }
                    }
                    break;
            }
            return null;
        }

        void Unload()
        {
            var AllTree = GameObject.FindObjectsOfType<TreeConponent>();
            if (AllTree != null)
                foreach (var tree in AllTree)
                    AddOrRemoveComponent("remove", tree, tree.tree, tree.tree.OwnerID);
            SaveData();
        }

        #endregion

        #region MyMethods

        public void SpawnWood(ulong player, Vector3 pos, BaseEntity tree, BaseEntity seed)
        {
            if (tree == null)
            {
                if (!WoodsList.ContainsKey(player))

                    WoodsList.Add(player, new Dictionary<uint, Wood>()
                    {
                        [seed.net.ID] = new Wood() { woodId = seed.net.ID, CurrentStage = 0, NeedTime = config.Time / config.Stages.Count, woodPos = seed.transform.position }
                    });

                else
                    WoodsList[player].Add(seed.net.ID, new Wood() { woodId = seed.net.ID, CurrentStage = 0, NeedTime = config.Time / config.Stages.Count, woodPos = seed.transform.position });
                seed.gameObject.AddComponent<TreeConponent>()?.Init(WoodsList[player][seed.net.ID]);
            }
            else
            {
                if (tree == null) return;
                var old = WoodsList[player][tree.net.ID];
                var current = ++old.CurrentStage;
                TreeEntity Wood = GameManager.server.CreateEntity(config.Stages[current], pos) as TreeEntity;
                WoodsList[player].Remove(tree.net.ID);
                Wood.Spawn();
                Wood.GetComponent<TreeEntity>().OwnerID = player;
                WoodsList[player].Add(Wood.net.ID, new Wood() { woodId = Wood.net.ID, CurrentStage = current, NeedTime = config.Time / config.Stages.Count, woodPos = Wood.transform.position });
                Wood.gameObject.AddComponent<TreeConponent>()?.Init(WoodsList[player][Wood.net.ID]);
                Wood.SendNetworkUpdateImmediate();
                tree.KillMessage();
            }
        }


        [ChatCommand("seed")]
        void GiveSeed(BasePlayer player, string command, string[] args)
        {
            if (player.IsAdmin || permission.UserHasPermission(player.UserIDString, config.Permission))
            {
                if (args.Length == 1)
                {
                    int amount;
                    if (!int.TryParse(args[0], out amount))
                    {
                        SendReply(player, "Вы не указали количество, используйте /seed AMOUNT");

                        return;
                    }
                    AddSeed(player, amount, false);
                    return;
                }
                if (args.Length > 0 && args.Length == 2)
                {
                    var target = BasePlayer.Find(args[0]);
                    if (target == null)
                    {
                        SendReply(player, "Данный игрок не найден, попробуйте уточнить имя или SteamID, используйте /seed TARGETNAME/ID AMOUNT");
                        return;
                    }

                    int amount;
                    if (!int.TryParse(args[1], out amount))
                    {
                        SendReply(player, "Вы не указали количество, используйте /seed TARGETNAME/ID AMOUNT");
                        return;
                    }
                    AddSeed(target, amount);
                }
            }
            else
            {
                SendReply(player, string.Format(Messages["Permission"]));
                Effect.server.Run(config.ErrorEffect, player, 0, Vector3.zero, Vector3.forward);
            }
        }

        void AddSeed(BasePlayer player, int amount, bool messages = true)
        {
            if (player == null) return;
            Item sd = ItemManager.CreateByName(config.seed.shortname, amount, config.seed.skinId);
            sd.name = config.seed.name;
            player.GiveItem(sd, BaseEntity.GiveItemReason.Crafted);
            if (messages) SendReply(player, string.Format(Messages["SeedGived"]));
            Effect.server.Run(config.SucEffect, player, 0, Vector3.zero, Vector3.forward);
        }

        public void SpawnBox(Wood wood, int i, BaseEntity tree, ulong ownerID, int countBox = 0)
        {
            if (wood == null) return;
            if (wood != null)
            {
                wood.BoxListed.Clear();
                wood.boxes.Clear();
                if (countBox == 0) countBox = config.BoxCount;
                for (int count = 0; count < countBox; count++)
                {
                    var reply = 3566;
                    if (reply == 0) { }
                    Vector3 pos = new Vector3(UnityEngine.Random.Range(-9, 9), UnityEngine.Random.Range(5f, 9.0f), UnityEngine.Random.Range(-9, 9));
                    BaseEntity boxed = GameManager.server.CreateEntity(config.CrateBasic, wood.woodPos + pos);
                    boxed.GetComponent<LootContainer>().initialLootSpawn = false;
                    boxed.enableSaving = false;
                    boxed.Spawn();
                    AddLoot(boxed);
                    boxed.SetFlag(BaseEntity.Flags.Reserved8, false, true);
                    boxed.SetFlag(BaseEntity.Flags.Busy, true, true);
                    boxed.SendNetworkUpdateImmediate();
                    wood.BoxListed.Add(boxed.net.ID);
                    wood.boxes.Add(boxed);
                }
            }
        }

        public void AddLoot(BaseEntity box)
        {
            if (box == null) return;
            LootContainer container = box.GetComponent<LootContainer>();
            if (container == null) return;
            container.inventory.itemList.Clear();
            var List = new List<string>();
            for (int i = 0; i < (config.ItemsCount > config.casesItems.Count ? config.casesItems.Count : config.ItemsCount); i++)
            {
                var random = UnityEngine.Random.Range(1, 100);
                var item = config.casesItems.OrderBy(p => p.Change).Where(p => p.Change >= random && !List.Contains(p.ShortName)).ToList().GetRandom();
                if (item == null)
                    item = config.casesItems.OrderBy(p => p.Change).LastOrDefault(p => !List.Contains(p.ShortName));
                List.Add(item.ShortName);
                var amount = UnityEngine.Random.Range(item.MinAmount, item.MaxAmount);
                var newItem = item.IsBlueprnt ? ItemManager.CreateByName("blueprintbase") : ItemManager.CreateByName(item.ShortName, amount, item.SkinID);
                if (newItem == null)
                {
                    PrintError($"Предмет {item.ShortName} не найден!");
                    return;
                }

                if (item.IsBlueprnt)
                {
                    var bpItemDef = ItemManager.FindItemDefinition(ItemManager.CreateByName(item.ShortName, amount, item.SkinID).info.itemid);
                    if (bpItemDef == null)
                    {
                        PrintError($"Предмет {item.ShortName} для создания чертежа не найден!");
                        return;
                    }
                    newItem.blueprintTarget = bpItemDef.itemid;
                }

                if (!string.IsNullOrEmpty(item.Name))
                    newItem.name = item.Name;

                if (container.inventory.IsFull())
                    container.inventory.capacity++;
                newItem.MoveToContainer(container.inventory, -1);
            }
        }

        class TreeConponent : BaseEntity
        {
            public Dictionary<BasePlayer, bool> ColliderPlayersList = new Dictionary<BasePlayer, bool>();
            public BaseEntity tree;
            SphereCollider sphereCollider;

            public Wood data;

            void Awake()
            {
                tree = gameObject.GetComponent<BaseEntity>();
                sphereCollider = gameObject.AddComponent<SphereCollider>();
                sphereCollider.gameObject.layer = (int)Rust.Layer.Reserved1;
                sphereCollider.isTrigger = true;
                sphereCollider.radius = 4f;
                InvokeRepeating(DrawInfo, 1f, 1);
            }

            public void Init(Wood wood)
            {
                data = wood;
            }

            private void OnTriggerEnter(Collider other)
            {
                var target = other.GetComponentInParent<BasePlayer>();
                if (target != null && !ColliderPlayersList.ContainsKey(target))
                    ColliderPlayersList.Add(target, !target.IsAdmin);
            }

            private void OnTriggerExit(Collider other)
            {
                var target = other.GetComponentInParent<BasePlayer>();
                if (target != null && ColliderPlayersList.ContainsKey(target))
                    ColliderPlayersList.Remove(target);
            }


            void DrawInfo()
            {
                if (data == null) return;
                if (data.NeedTime <= 0 && data.CurrentStage == ins.config.Stages.FindIndex(x => x == ins.config.Stages.Last()) && data.BoxListed.ToList().Count <= 0)
                {
                    ins.SpawnBox(data, 3, tree, tree.OwnerID);
                    CreateInfo(tree.OwnerID);
                    data.CurrentStage = ins.config.Stages.Count;
                }
                if (data.NeedTime <= 0 && data.CurrentStage < ins.config.Stages.FindIndex(x => x == ins.config.Stages.Last()))
                {
                    ins.SpawnWood(tree.OwnerID, tree.transform.position, tree, null);
                }


                if (data.CurrentStage == ins.config.Stages.Count && data.BoxListed.ToList().Count > 0)
                {
                    data.NeedTimeToDestroy++;
                    if (data.NeedTimeToDestroy > ins.config.TimetoDestroy)
                    {
                        ins.WoodsList[tree.OwnerID].Remove(tree.net.ID);
                        HitInfo hitInfo = new HitInfo(new BaseEntity(), tree, Rust.DamageType.Generic, tree.Health(), tree.transform.position);
                        tree.OnAttacked(hitInfo);
                        Destroy(this);
                    }
                }

                foreach (var player in ColliderPlayersList)
                {
                    if (data.CurrentStage == ins.config.Stages.Count && data.BoxListed.ToList().Count > 0)
                    {
                        if (player.Value) SetPlayerFlag(player.Key, BasePlayer.PlayerFlags.IsAdmin, true);
                        player.Key.SendConsoleCommand("ddraw.text", 1.01f, Color.white, tree.transform.position + Vector3.up, ins.Messages["InfoTextFull"]);
                        if (player.Value) SetPlayerFlag(player.Key, BasePlayer.PlayerFlags.IsAdmin, false);
                        continue;
                    }

                    if (data.NeedTime > 0)
                    {
                        if (player.Value) SetPlayerFlag(player.Key, BasePlayer.PlayerFlags.IsAdmin, true);
                        player.Key.SendConsoleCommand("ddraw.text", 1.01f, Color.white, tree.transform.position + Vector3.up, string.Format(ins.Messages["InfoDdraw"], data.CurrentStage + 1, ins.config.Stages.Count, FormatShortTime(TimeSpan.FromSeconds(data.NeedTime))));
                        if (player.Value) SetPlayerFlag(player.Key, BasePlayer.PlayerFlags.IsAdmin, false);
                    }
                }
                data.NeedTime--;
            }

            void SetPlayerFlag(BasePlayer player, BasePlayer.PlayerFlags f, bool b)
            {
                if (b)
                {
                    if (player.HasPlayerFlag(f)) return;
                    player.playerFlags |= f;
                }
                else
                {
                    if (!player.HasPlayerFlag(f)) return;
                    player.playerFlags &= ~f;
                }
                player.SendNetworkUpdateImmediate(false);
            }

            public static string FormatShortTime(TimeSpan time)
            {
                string result = string.Empty;
                result += $"{time.Hours.ToString("00")}:";
                result += $"{time.Minutes.ToString("00")}:";
                result += $"{time.Seconds.ToString("00")}";
                return result;
            }

            private static string Format(int units, string form1 = "", string form2 = "", string form3 = "")
            {
                var tmp = units % 10;
                if (units >= 5 && units <= 20 || tmp >= 5 && tmp <= 9)
                    return $"{units} {form1}";
                if (tmp >= 2 && tmp <= 4)
                    return $"{units} {form2}";
                return $"{units} {form3}";
            }

            public void DestroyComponent() => Destroy(this);

            void OnDestroy()
            {
                if (data != null && data.BoxListed != null && data.BoxListed.Count > 0)
                    foreach (var box in data.boxes.Where(p => p != null))
                    {
                        box.SetFlag(BaseEntity.Flags.Busy, false, true);
                        Rigidbody rigidbody = box.gameObject.GetOrAddComponent<Rigidbody>();
                        rigidbody.useGravity = true;
                        rigidbody.isKinematic = false;
                        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                        rigidbody.mass = 2f;
                        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                        rigidbody.angularVelocity = Vector3Ex.Range(-1.75f, 1.75f);
                        rigidbody.drag = 0.5f * (rigidbody.mass / 5f);
                        rigidbody.angularDrag = 0.2f * (rigidbody.mass / 5f);
                    }
            }
        }

        static void CreateInfo(ulong playerid = 3651584)
        {
            var player = BasePlayer.FindByID(playerid);
            if (player != null)
            {
                CuiHelper.DestroyUi(player, "MagicTree");
                CuiElementContainer container = new CuiElementContainer();
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.3447913 0.112037", AnchorMax = "0.640625 0.15", OffsetMax = "0 0" },
                    Image = { Color = "1 1 1 0.2" }
                }, "Hud", "MagicTree");
                container.Add(new CuiLabel
                {
                    FadeOut = 2,
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Text = { Text = "ВАШЕ ДЕРЕВО СОЗРЕЛО, И ДАЛО ПЛОДЫ!", FontSize = 17, Align = TextAnchor.MiddleCenter, FadeIn = 2, Color = "1 1 1 0.8", Font = "robotocondensed-regular.ttf" }
                }, "MagicTree");

                CuiHelper.AddUi(player, container);
                ins.timer.Once(5f, () => { if (player != null) CuiHelper.DestroyUi(player, "MagicTree"); });
            }
        }
        #endregion
    }
}