using UnityEngine;
using Newtonsoft.Json;
using System.Linq;

namespace Oxide.Plugins
{
    //     Update 0.2.6
    //Оптимизация!
    //Добавил возможность поставить метки на карту  g
    //Добавил возможность спавнить лифт в городе нпс и на складах
    //     Update 0.2.9
    //Фикс нагрузки
    //     Update 0.3.0
    //Убрана полностью нагрузка

    [Info("XDCarLiftRadtown", "https://topplugin.ru/ / https://discord.com/invite/5DPTsRmd3G", "0.3.0")]
    [Description("Лифты в супермаркетах и на заправках")]
    class XDCarLiftRadtown : RustPlugin
    {
        #region Hooks
        private void OnServerInitialized()
        {
            NextTick(() => {
                foreach (var lift in UnityEngine.Object.FindObjectsOfType<ModularCarGarage>().Where(p => p.OwnerID == 23423423))
                    lift.KillMessage();

                foreach (var mount in TerrainMeta.Path.Monuments)
                {
                    if (mount.name.Contains("gas_station_1") && config.pluginSettings.gasstation)
                    {
                        var pos = mount.transform.position + mount.transform.rotation * new Vector3(4.2f, 0f, -0.5f);
                        pos.y = TerrainMeta.HeightMap.GetHeight(pos);
                        CrateLift(pos, mount.transform.rotation);
                    }
                    else if (mount.name.Contains("supermarket_1") && config.pluginSettings.supermarket)
                    {
                        var pos = mount.transform.position + mount.transform.rotation * new Vector3(0.2f, 0f, 17.5f);
                        pos.y = TerrainMeta.HeightMap.GetHeight(pos);
                        CrateLift(pos, mount.transform.rotation);
                    }
                    else if (mount.name.Contains("warehouse") && config.pluginSettings.warehouse)
                    {
                        var pos = mount.transform.position + mount.transform.rotation * new Vector3(-14.2f, 0f, -7f);
                        pos.y = TerrainMeta.HeightMap.GetHeight(pos);
                        CrateLift(pos, mount.transform.rotation);
                    }
                    else if (mount.name.Contains("compound") && config.pluginSettings.outpost)
                    {
                        var pos = mount.transform.position + mount.transform.rotation * new Vector3(0f, 0f, 73f);
                        pos.y = TerrainMeta.HeightMap.GetHeight(pos);
                        CrateLift(pos, mount.transform.rotation * new Quaternion(0, 2.0f, 0, 2));
                    }
                }
            });     
        }
        private void Unload()
        {
            if (config.pluginSettings.MapMarkers)
                RemoveMarkers();
        }
        #endregion

        #region Metods
        int i = 1;
        private void CrateLift(Vector3 pos, Quaternion quaternion)
        {
            if (config.pluginSettings.MapMarkers)
            {
                CreateMarker(pos, 10, "ЗАДАНИЯ", $"Подъемник для транспорта #{i}");
            }
            i++;
           
            ModularCarGarage modularCar = GameManager.server.CreateEntity("assets/bundled/prefabs/static/modularcarlift.static.prefab", pos, quaternion) as ModularCarGarage;
            modularCar.Spawn();
            modularCar.OwnerID = 23423423;
        }

        #endregion

        #region Configuration

        public static Configuration config = new Configuration();
        public class Configuration
        {
            public class PluginSettings
            {
                [JsonProperty("Создавать маркер на карте (g) чтобы игрокам проще было их находить")]
                public bool supermarket;
                [JsonProperty("Спавнить у супермаркетов ?")]
                public bool MapMarkers;
                [JsonProperty("Спавнить у заправок ?")]
                public bool gasstation;
                [JsonProperty("Спавнить у складов ?")]
                public bool warehouse;
                [JsonProperty("Спавнить у города нпс ?")]
                public bool outpost;
            }

            [JsonProperty("Настройки спавна")]
            public PluginSettings pluginSettings;

            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    pluginSettings = new PluginSettings
                    {
                        MapMarkers = true,
                        gasstation = true,
                        supermarket = true,
                        warehouse = false,
                        outpost = true
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning("Ошибка#skykey чтения конфигурации 'oxide/config/', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }
            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(config, true);

        #endregion

        #region Метка на g карте 
        private void CreateMarker(Vector3 position, float refreshRate, string name, string displayName,
           float radius = 0.12f, string colorMarker = "60ffa7", string colorOutline = "00FFFFFF")
        {
            var marker = new GameObject().AddComponent<CustomMapMarker>();
            marker.name = name;
            marker.displayName = displayName;
            marker.radius = radius;
            marker.position = position;
            marker.refreshRate = refreshRate;
            ColorUtility.TryParseHtmlString($"#{colorMarker}", out marker.color1);
            ColorUtility.TryParseHtmlString($"#{colorOutline}", out marker.color2);
        }

        private void RemoveMarkers()
        {
            foreach (var marker in UnityEngine.Object.FindObjectsOfType<CustomMapMarker>())
            {
                UnityEngine.Object.Destroy(marker);
            }
        }

        private const string genericPrefab = "assets/prefabs/tools/map/genericradiusmarker.prefab";
        private const string vendingPrefab = "assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab";

        #region Scripts

        private class CustomMapMarker : MonoBehaviour
        {
            private VendingMachineMapMarker vending;
            private MapMarkerGenericRadius generic;
            public BaseEntity parent;
            private bool asChild;

            public float radius;
            public Color color1;
            public Color color2;
            public string displayName;
            public float refreshRate;
            public Vector3 position;
            public bool placedByPlayer;

            private void Start()
            {
                transform.position = position;
                asChild = parent != null;
                CreateMarkers();
            }
            private void CreateMarkers()
            {
                vending = GameManager.server.CreateEntity(vendingPrefab, position)
                    .GetComponent<VendingMachineMapMarker>();
                vending.markerShopName = displayName;
                vending.enableSaving = false;
                vending.Spawn();

                generic = GameManager.server.CreateEntity(genericPrefab).GetComponent<MapMarkerGenericRadius>();
                generic.color1 = color1;
                generic.color2 = color2;
                generic.radius = radius;
                generic.alpha = 1f;
                generic.enableSaving = false;
                generic.SetParent(vending);
                generic.Spawn();

                UpdateMarkers();

                if (refreshRate > 0f)
                {
                    if (asChild)
                    {
                        InvokeRepeating(nameof(UpdatePosition), refreshRate, refreshRate);
                    }
                    else
                    {
                        InvokeRepeating(nameof(UpdateMarkers), refreshRate, refreshRate);
                    }
                }
            }

            private void UpdatePosition()
            {
                if (asChild == true)
                {
                    if (parent.IsValid() == false)
                    {
                        Destroy(this);
                        return;
                    }
                    else
                    {
                        var pos = parent.transform.position;
                        transform.position = pos;
                        vending.transform.position = pos;
                    }
                }
                UpdateMarkers();
            }

            private void UpdateMarkers()
            {
                vending.SendNetworkUpdate();
                generic.SendUpdate();
            }

            private void DestroyMakers()
            {
                if (vending.IsValid())
                {
                    vending.Kill();
                }

                if (generic.IsValid())
                {
                    generic.Kill();
                }
            }

            private void OnDestroy()
            {
                DestroyMakers();
            }
        }

        #endregion

        #endregion
    }
}
