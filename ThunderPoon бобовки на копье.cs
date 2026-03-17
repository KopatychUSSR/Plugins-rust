using System.Collections.Generic;
using UnityEngine;
using Rust;
using System;
using Oxide.Core;
using System.Reflection;
using System.Security.Policy;
using System.ComponentModel;

namespace Oxide.Plugins
{
    [Info("ThunderPoon", "Death", "1.0.1")]
    class ThunderPoon : RustPlugin
    {
        #region Declarations
        const string perm = "thunderpoon.use";
        #endregion

        #region Hooks
        void Init()
        {
            LoadConfigVariables();
            permission.RegisterPermission(perm, this);
        }

        object OnCreateWorldProjectile(HitInfo info, Item item)
        {
            if (item.info.itemid != 1602646136 && item.info.itemid != 1540934679 || item.skin != 2186193876)
            {
                return null;
            }

            NextTick(() => DeployPoon(info.Initiator, item));

            return null;
        }

        void CanStackItem(Item spear, Item explosive)
        {
            if (spear.info.itemid != 1602646136 && spear.info.itemid != 1540934679 || explosive.info.itemid != 1840822026)
            {
                return;
            }

            if (spear.parent.playerOwner == null)
            {
                return;
            }

            if (configData.Options.UsePermission && !permission.UserHasPermission(spear.parent.playerOwner.UserIDString, perm))
            {
                return;
            }

            if (spear.amount > explosive.amount)
            {
                spear.parent.playerOwner.GiveItem(ItemManager.Create(spear.info, spear.amount - explosive.amount, spear.skin));

                spear.amount = explosive.amount;
                spear.MarkDirty();
            }

            explosive.amount -= spear.amount;

            if (explosive.amount <= 0)
            {
                explosive.Remove();
            }
            else explosive.MarkDirty();

            spear.skin = 2186193876;
            Effect.server.Run("assets/bundled/prefabs/fx/repairbench/itemrepair.prefab", spear.parent.playerOwner.transform.position);
        }
        #endregion

        #region Functions
        void DeployPoon(BaseEntity player, Item item)
        {
            var worldEntity = item.GetWorldEntity();

            if (worldEntity == null)
            {
                return;
            }

            Effect.server.Run("assets/prefabs/weapons/beancan grenade/effects/beancan_grenade_explosion.prefab", worldEntity.transform.position);
            DamageUtil.RadiusDamage(player, worldEntity.LookupPrefab(), worldEntity.transform.position, configData.Properties.minRange, configData.Properties.maxRange, new List<DamageTypeEntry> { new DamageTypeEntry { type = DamageType.Explosion, amount = configData.Properties.damage } }, 1076005120, true);

            item.RemoveFromWorld();
        }

        #region Config
        ConfigData configData;

        class ConfigData
        {
            public Options Options = new Options();
            public Properties Properties = new Properties();
        }

        class Options
        {
            public bool UsePermission = true;
        }

        class Properties
        {
            public float damage = 100f;
            public float minRange = 0f;
            public float maxRange = 10f;
        }

        void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();
            SaveConfig(configData);
        }

        protected override void LoadDefaultConfig()
        {
            SaveConfig(new ConfigData());
        }

        void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
        #endregion

        #endregion
    }
}