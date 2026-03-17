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
    [Info("EntityHP", "Lime", "1.0.0")]
    class EntityHP : RustPlugin
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

            public static ConfigItem<Dictionary<string, Dictionary<string, int>>> MaxHP = new ConfigItem<Dictionary<string, Dictionary<string, int>>>("MaxHP", new Dictionary<string, Dictionary<string, int>>()
            {
                ["ehp.custom"] = new Dictionary<string, int>()
                {
                    ["assets/prefabs/building/wall.external.high.stone/wall.external.high.stone.prefab"] = 500,
                    ["assets/prefabs/building/wall.external.high.wood/wall.external.high.wood.prefab"] = 500,

                    ["assets/prefabs/building/gates.external.high/gates.external.high.stone/gates.external.high.stone.prefab"] = 500,
                    ["assets/prefabs/building/gates.external.high/gates.external.high.wood/gates.external.high.wood.prefab"] = 500,

                    ["assets/prefabs/building/door.double.hinged/door.double.hinged.metal.prefab"] = 250,
                    ["assets/prefabs/building/door.double.hinged/door.double.hinged.toptier.prefab"] = 800,
                    ["assets/prefabs/building/door.double.hinged/door.double.hinged.wood.prefab"] = 200,

                    ["assets/prefabs/building/door.hinged/door.hinged.metal.prefab"] = 250,
                    ["assets/prefabs/building/door.hinged/door.hinged.toptier.prefab"] = 800,
                    ["assets/prefabs/building/door.hinged/door.hinged.wood.prefab"] = 200,

                    ["assets/prefabs/building/wall.frame.garagedoor/wall.frame.garagedoor.prefab"] = 600,
                    ["assets/prefabs/building/floor.ladder.hatch/floor.ladder.hatch.prefab"] = 250,
                }
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
        Dictionary<string, Dictionary<string, int>> maxHP = null;
        bool loading = true;
        void OnServerInitialized()
        {
            loading = false;
            maxHP = ConfigData.MaxHP;
            foreach (var list in maxHP) permission.RegisterPermission(list.Key, this);
            foreach (var entity in BaseNetworkable.serverEntities) OnEntitySpawned(entity as BaseCombatEntity);
        }
        void OnEntitySpawned(BaseCombatEntity entity)
        {
            if (loading) return;
            if (entity == null) return;
            string steamID = entity?.OwnerID.ToString();
            if (steamID == null) return;
            foreach (var list in maxHP)
            {
                if (permission.UserHasPermission(steamID, list.Key))
                {
                    int HP;
                    if (!list.Value.TryGetValue(entity.PrefabName, out HP)) return;
                    if (entity._health == entity._maxHealth) entity._health = HP;
                    entity.startHealth = HP;
                    entity._maxHealth = HP;
                    return;
                }
            }
        }
    }
}
