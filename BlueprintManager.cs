using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Blueprint Manager", "http://topplugin.ru/", "1.0.2")]
    public class BlueprintManager : RustPlugin
    {
        #region Vars
        
        private Blueprints data = new Blueprints();
        private const string permLVL1 = "blueprintmanager.lvl1";
        private const string permLVL2 = "blueprintmanager.lvl2";
        private const string permLVL3 = "blueprintmanager.lvl3";
        private const string permLVL4 = "blueprintmanager.lvl4";
        
        #endregion

        #region Oxide Hooks

        private void OnServerInitialized()
        {
            CheckBlueprints();
            permission.RegisterPermission(permLVL1, this);
            permission.RegisterPermission(permLVL2, this);
            permission.RegisterPermission(permLVL3, this);
            permission.RegisterPermission(permLVL4, this);

            foreach (var player in BasePlayer.activePlayerList.ToList())
            {
                CheckPlayer(player);
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            CheckPlayer(player);
        }

        #endregion

        #region Core

        private void CheckBlueprints()
        {
            foreach (var item in ItemManager.bpList.ToList())
            {
                var lvl = item.workbenchLevelRequired;
                var id = item.targetItem.itemid;
                data.lvl4.Add(id);
                
                switch (lvl)
                {
                    case 1:
                        data.lvl1.Add(id);
                        if (config.removeNeedOfLvl1)
                        {
                            item.workbenchLevelRequired = 0;
                        }
                        
                        continue;
                        
                    case 2:
                        data.lvl2.Add(id);
                        if (config.removeNeedOfLvl2)
                        {
                            item.workbenchLevelRequired = 0;
                        }
                        
                        continue;
                        
                    case 3:
                        data.lvl3.Add(id);
                        if (config.removeNeedOfLvl3)
                        {
                            item.workbenchLevelRequired = 0;
                        }
                        
                        continue;
                        
                    default:
                        continue;
                }
            }
        }

        private void CheckPlayer(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, permLVL4))
            {
                GiveBlueprints(player, data.lvl4);
                return;
            }
            
            if (permission.UserHasPermission(player.UserIDString, permLVL3))
            {
                GiveBlueprints(player, data.lvl3);
            }
            if (permission.UserHasPermission(player.UserIDString, permLVL2))
            {
                GiveBlueprints(player, data.lvl2);
            }
            
            if (permission.UserHasPermission(player.UserIDString, permLVL1))
            {
                GiveBlueprints(player, data.lvl1);
            }
        }

        private void GiveBlueprints(BasePlayer player, List<int> blueprints)
        {
            var playerInfo = SingletonComponent<ServerMgr>.Instance.persistance.GetPlayerInfo(player.userID);
            var pending = playerInfo.unlockedItems;
            pending.AddRange(blueprints);
            playerInfo.unlockedItems = pending.Distinct().ToList();
            SingletonComponent<ServerMgr>.Instance.persistance.SetPlayerInfo(player.userID, playerInfo);
            player.SendNetworkUpdate();
        }

        #endregion
        
        #region Configuration 1.0.0

        private static ConfigData config;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Remove need of workbench lvl 1")]
            public bool removeNeedOfLvl1;
            
            [JsonProperty(PropertyName = "Remove need of workbench lvl 2")]
            public bool removeNeedOfLvl2;
            
            [JsonProperty(PropertyName = "Remove need of workbench lvl 3")]
            public bool removeNeedOfLvl3;
        }

        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                removeNeedOfLvl1 = false,
                removeNeedOfLvl2 = false,
                removeNeedOfLvl3 = false,
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();

                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintError("Configuration file is corrupt(or not exists), creating new one!");
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion

        #region Classes

        private class Blueprints
        {
            public List<int> lvl1 = new List<int>();
            public List<int> lvl2 = new List<int>();
            public List<int> lvl3 = new List<int>();
            public List<int> lvl4 = new List<int>();
        }

        #endregion
    }
}