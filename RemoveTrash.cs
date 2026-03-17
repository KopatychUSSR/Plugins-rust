using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("RemoveTrash", "Инкуб", "1.3.2")]
    class RemoveTrash : RustPlugin
    {
        private Configuration config;

        class Configuration
        {
            public string Версия { get; set; } = "1.3.2";
            public float РадиусУдаления { get; set; } = 10f;
            public float ИнтервалПроверки { get; set; } = 60f;
            public Position ПозицияУдаления { get; set; } = new Position();
            public string[] Исключить { get; set; } = new string[] { "player_corpse", "stash.small", "box.wooden", "box.wooden.large", "rowboat_storage", "workbench1.deployed", "workbench2.deployed", "workbench3.deployed", "rocket_mlrs" };
            public bool УдалятьNPC { get; set; } = false;
            public bool УдалятьИгроков { get; set; } = false;
        }

        class Position
        {
            public float x { get; set; } = 0f;
            public float y { get; set; } = 0f;
            public float z { get; set; } = 0f;
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Создание нового файла конфигурации");
            config = new Configuration();
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null || config.Версия != Version.ToString())
                {
                    PrintWarning("Файл конфигурации не совпадает с текущей версией, обновление...");
                    LoadDefaultConfig();
                }
            }
            catch
            {
                PrintWarning("Файл конфигурации поврежден, создание нового");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config, true);

        private void OnServerInitialized()
        {
            PrintWarning("\n-----------------------------------------------------------------------------------------\n" +
            "     Loading plugin...\n" +
            "     DISCORD: inkub1372\n" +
            "     Enjoy your use!\n" +
            "-----------------------------------------------------------------------------------------");
            timer.Every(config.ИнтервалПроверки, RemoveObjects);
        }

        private void RemoveObjects()
        {
            var colliders = Physics.OverlapSphere(new Vector3(config.ПозицияУдаления.x, config.ПозицияУдаления.y, config.ПозицияУдаления.z), config.РадиусУдаления);
            int removedCount = 0;
            HashSet<string> exclusionSet = new HashSet<string>(config.Исключить);

            foreach (var collider in colliders)
            {
                var entity = collider.GetComponentInParent<BaseEntity>();
                if (entity != null && !entity.IsDestroyed && !(entity is BasePlayer))
                {
                    string entityName = entity.ShortPrefabName;
                    if (!exclusionSet.Contains(entityName) && (config.УдалятьNPC || !(entity is NPCPlayer)) && (config.УдалятьИгроков || !(entity is BasePlayer)))
                    {
                        float distance = Vector3.Distance(new Vector3(config.ПозицияУдаления.x, config.ПозицияУдаления.y, config.ПозицияУдаления.z), entity.transform.position);
                        if (!entity.IsDestroyed)
                        {
                            entity.Kill();
                            removedCount++;
                            string removalMessage = $"Удалено {entityName} на расстоянии {distance}м от позиции {config.ПозицияУдаления.x}, {config.ПозицияУдаления.y}, {config.ПозицияУдаления.z}";
                            PrintWarning(removalMessage);
                            LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: {removalMessage}", this);
                        }
                    }
                }
            }

            if (removedCount > 0)
            {
                string logMessage = $"Удалено {removedCount} объектов в радиусе {config.РадиусУдаления}м от позиции {config.ПозицияУдаления.x}, {config.ПозицияУдаления.y}, {config.ПозицияУдаления.z}.";
                PrintWarning(logMessage);
                LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: {logMessage}", this);
            }
        }
    }
}
