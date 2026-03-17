using Oxide.Core;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Text.RegularExpressions;
using System;
using Oxide.Core.Configuration;
using Newtonsoft.Json.Linq;
using Network;

namespace Oxide.Plugins
{
    [Info("RocketReloader", "Lime", "1.0.0")]
    class RocketReloader : RustPlugin
    {
        #region [DLC] LimeConfig 
        protected override void LoadDefaultConfig()
        {
            PrintError("No config file found, generating a new one.");
            ConfigData.LoadAndSaveAll();
            SaveConfig();
        }
        public static class ConfigData
        {
            public class Rocket
            {
                public int Capacity;
            }

            static Core.Plugins.Plugin _currentPlugin;
            public static Core.Plugins.Plugin CurrentPlugin
            {
                get
                {
                    if (_currentPlugin == null)
                    {
                        Core.Libraries.Plugins plugins = Core.Interface.Oxide.GetLibrary<Core.Libraries.Plugins>();
                        _currentPlugin = plugins.Find(typeof(ConfigData).DeclaringType.Name) ?? plugins.Find(((InfoAttribute)System.Linq.Enumerable.FirstOrDefault(typeof(ConfigData).DeclaringType?.GetCustomAttributes(typeof(InfoAttribute), false)))?.Title);
                    }
                    if (_currentPlugin == null) throw new System.Exception($"Plugin '{typeof(ConfigData).DeclaringType?.Name ?? "NULL"}' not found!..");
                    return _currentPlugin;
                }
            }

            public static ConfigItem<Dictionary<string, Rocket>> RocketList = new ConfigItem<Dictionary<string, Rocket>>("RocketList", new Dictionary<string, Rocket>()
            {
                ["reloader.custom"] = new Rocket() { Capacity = 1 }
            });

            static UnityEngine.Vector3 Convert(string val)
            {
                string[] split = val.Split(' ');
                return new UnityEngine.Vector3(float.Parse(split[0]), float.Parse(split[1]), float.Parse(split[2]));
            }
            static string Convert(UnityEngine.Vector3 val)
            {
                return $"{System.Math.Round(val.x, 5)} {System.Math.Round(val.y, 5)} {System.Math.Round(val.z, 5)}";
            }
            static System.Collections.Generic.List<R> Convert<T, R>(System.Collections.Generic.List<T> vals, System.Func<T, R> convert)
            {
                System.Collections.Generic.List<R> list = new System.Collections.Generic.List<R>();
                vals.ForEach(val => list.Add(convert(val)));
                return list;
            }

            const System.Reflection.BindingFlags All = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
            public static void LoadAndSaveAll()
            {
                new System.Collections.Generic.List<System.Reflection.FieldInfo>(typeof(ConfigData).GetFields(All)).ForEach((f) => ((ConfigItem)f.GetValue(null)).GetSet());
            }
            public static void SetConfig(string key, object value) => SetConfig(new string[] { key }, value);
            public static void SetConfig(string group, string key, object value) => SetConfig(new string[] { group, key }, value);
            public static void SetConfig(string[] path, object value)
            {
                System.Collections.Generic.List<object> list = new System.Collections.Generic.List<object>();
                list.AddRange(path);
                list.Add(value);
                CurrentPlugin.Config.Set(list.ToArray());
                CurrentPlugin.Config.Save();
            }
            public static T GetConfig<T>(string key, T def) => GetConfig(new string[] { key }, def);
            public static T GetConfig<T>(string group, string key, T def) => GetConfig(new string[] { group, key }, def);
            public static T GetConfig<T>(string[] path, T def)
            {
                Newtonsoft.Json.Linq.JObject json = CurrentPlugin.Config.ReadObject<Newtonsoft.Json.Linq.JObject>();
                Newtonsoft.Json.Linq.JToken _path = json;
                try
                {
                    for (int i = 0; i < path.Length; i++) _path = _path[path[i]];
                    if (_path == null) throw new System.Exception();
                }
                catch
                {
                    SetConfig(path, def);
                    return def;
                }
                return _path.ToObject<T>();
            }
            public class ConfigItem<T> : ConfigItem
            {
                public ConfigItem(string group, string key, T defaultValue) : this(() => GetConfig(group, key, defaultValue), (value) => SetConfig(group, key, value)) { }
                public ConfigItem(string key, T defaultValue) : this(() => GetConfig(key, defaultValue), (value) => SetConfig(key, value)) { }

                public ConfigItem(System.Func<T> get, System.Action<T> set)
                {
                    this.get = () => get.Invoke();
                    this.set = (value) => set.Invoke((T)value);
                }
                public T Value { get { return (T)get.Invoke(); } set { set.Invoke(value); } }
                public static implicit operator T(ConfigItem<T> obj) => obj.Value;
            }
            public class ConfigItemConvert<T, R>
            {
                public System.Func<R> get;
                public System.Action<R> set;
                public ConfigItemConvert(string group, string key, T defaultValue, System.Func<T, R> encode, System.Func<R, T> decode) : this(() => GetConfig(group, key, defaultValue), (value) => SetConfig(group, key, value), encode, decode) { }
                public ConfigItemConvert(string key, T defaultValue, System.Func<T, R> encode, System.Func<R, T> decode) : this(() => GetConfig(key, defaultValue), (value) => SetConfig(key, value), encode, decode) { }
                public ConfigItemConvert(System.Func<T> get, System.Action<T> set, System.Func<T, R> encode, System.Func<R, T> decode)
                {
                    this.get = () => encode(get.Invoke());
                    this.set = (value) => set.Invoke(decode(value));
                }
                public static implicit operator ConfigItem<R>(ConfigItemConvert<T, R> obj)
                {
                    return new ConfigItem<R>(obj.get, obj.set);
                }
            }
            public abstract class ConfigItem
            {
                public System.Func<object> get;
                public System.Action<object> set;
                public void GetSet() => set?.Invoke(get?.Invoke());
            }
        }
        #endregion
        public const int rocket_launcher = 442886268;

        class ItemModRocket: ItemMod
        {
            public override void OnParentChanged(Item item)
            {
                ulong userID = item?.parent?.playerOwner?.userID ?? 0;
                if (userID == 0) return;
                GetSettings(userID.ToString()).Set(item.GetHeldEntity() as BaseLauncher);
            }
        }

        protected static new Core.Libraries.Permission permission = Interface.Oxide.GetLibrary<Core.Libraries.Permission>(null);

        public class RocketSettings
        {
            public int capacity;

            public RocketSettings(BaseLauncher launcher)
            {
                capacity = launcher.primaryMagazine.capacity;
            }
            public void Set(BaseLauncher launcher)
            {
                Debug.Log($"{launcher.deployDelay} | {launcher.repeatDelay} | {launcher.animationDelay}");
                launcher.primaryMagazine.capacity = capacity;
            }
        }

        public static Dictionary<string, RocketSettings> settingsList = new Dictionary<string, RocketSettings>();
        public static RocketSettings settingsDefault = null;
        public static RocketSettings GetSettings(string steamID)
        {
            foreach (var kv in settingsList)
                if (permission.UserHasPermission(steamID, kv.Key))
                    return kv.Value;
            return settingsDefault;
        }

        void OnServerInitialized()
        {
            Item item = ItemManager.CreateByItemID(rocket_launcher, 1, 0);
            BaseLauncher launcher = item.GetHeldEntity() as BaseLauncher;
            settingsDefault = new RocketSettings(launcher);
            settingsList.Clear();
            foreach (var kv in ConfigData.RocketList.Value)
            {
                permission.RegisterPermission(kv.Key, this);
                settingsList.Add(kv.Key, new RocketSettings(launcher) { capacity = kv.Value.Capacity });
            }
            ItemDefinition definition = item.info;
            List<ItemMod> mods = new List<ItemMod>(definition.itemMods);
            foreach (var mod in mods.ToArray())
                if (mod.GetType().Name == typeof(ItemModRocket).Name)
                    mods.Remove(mod);
            mods.Add(new ItemModRocket());
            definition.itemMods = mods.ToArray();
        }
        void Unload()
        {
            ItemDefinition definition = ItemManager.FindItemDefinition(rocket_launcher);
            List<ItemMod> mods = new List<ItemMod>(definition.itemMods);
            foreach (var mod in mods.ToArray())
                if (mod.GetType().Name == typeof(ItemModRocket).Name)
                    mods.Remove(mod);
            definition.itemMods = mods.ToArray();
        }
    }
}
