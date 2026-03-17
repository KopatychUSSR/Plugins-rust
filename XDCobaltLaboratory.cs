using Oxide.Game.Rust.Cui;
using Oxide.Core.Libraries;
using Oxide.Core;
using System.Text;
using Rust;
using Random = Oxide.Core.Random;
using System.Collections.Generic;
using System;
using System.Collections;
using UnityEngine;
using Oxide.Plugins.XDCobaltLaboratoryExtensionMethods;
using System.Globalization;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using Facepunch;
using Newtonsoft.Json.Linq;
using Network;
		   		 		  						  	   		  		 			   					  	  			   			
namespace Oxide.Plugins.XDCobaltLaboratoryExtensionMethods
{
    public static class ExtensionMethods
    {
        public static bool Any<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (IEnumerator<TSource> enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (predicate(enumerator.Current))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        public static TSource FirstOrDefault<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (IEnumerator<TSource> enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (predicate(enumerator.Current))
                    {
                        return enumerator.Current;
                    }
                }
            }

            return default(TSource);
        }

                public static HashSet<TResult> Select<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> predicate)
        {
            HashSet<TResult> result = new HashSet<TResult>();
            using (IEnumerator<TSource> enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    result.Add(predicate(enumerator.Current));
                }
            }
		   		 		  						  	   		  		 			   					  	  			   			
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
            using (IEnumerator<TSource> enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    result.Add(enumerator.Current);
                }
            }
		   		 		  						  	   		  		 			   					  	  			   			
            return result;
        }
		   		 		  						  	   		  		 			   					  	  			   			
        public static TSource Min<TSource>(this IEnumerable<TSource> source, Func<TSource, float> predicate)
        {
            TSource result = source.ElementAt(0);
            float resultValue = predicate(result);
            using (IEnumerator<TSource> enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    TSource element = enumerator.Current;
                    float elementValue = predicate(element);
                    if (elementValue < resultValue)
                    {
                        result = element;
                        resultValue = elementValue;
                    }
                }
            }
            return result;
        }

        public static TSource ElementAt<TSource>(this IEnumerable<TSource> source, int index)
        {
            int movements = 0;
            using (IEnumerator<TSource> enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (movements == index)
                    {
                        return enumerator.Current;
                    }

                    movements++;
                }
            }
            return default(TSource);
        }
    }
}
namespace Oxide.Plugins
{
    [Info("XDCobaltLaboratory", "DezLife", "2.5.6")]
    internal class XDCobaltLaboratory : RustPlugin
    {
        private HackableLockedCrate CrateEnt;
        
        
        private Configuration config;

        static float GetGroundPosition(Vector3 pos)
        {
            float y = TerrainMeta.HeightMap.GetHeight(pos);
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(pos.x, pos.y + 200f, pos.z), Vector3.down, out hit, Mathf.Infinity, LayerMask.GetMask(new[] { "Terrain", "World", "Default", "Construction", "Deployed" })) && !hit.collider.name.Contains("rock_cliff"))
            {
                return Mathf.Max(hit.point.y, y);
            }

            return y;
        }
        private List<BaseEntity> HouseCobaltLab = new List<BaseEntity>();
        private void SendChatPlayer(string Message, BasePlayer player, ConVar.Chat.ChatChannel channel = ConVar.Chat.ChatChannel.Global)
        {
            if (IQChat)
            {
                IQChat?.Call("API_ALERT_PLAYER", player, Message);
            }
            else
            {
                player?.SendConsoleCommand("chat.add", channel, 0, Message);
            }
        }
        private const bool Ru = true;
        void Unsubscribes()
        {
            foreach (string hook in hooks)
            {
                Unsubscribe(hook);
            }
        }

        void Subscribes()
        {
            foreach (string hook in hooks)
            {
                if ((hook == "CanEntityTakeDamage" || hook == "OnEntityDeath") && !config.iventController.UseZonePVP)
                {
                    continue;
                }

                if (hook == "CanUILootSpawn" && config.boxSetting.TypeLootTable != 3)
                {
                    continue;
                }

                Subscribe(hook);
            }
        }

        private void ValidateConfig()
        {
            if (config.pasteSettings.pasteBuilds.Count == 0)
            {
                config.pasteSettings.pasteBuilds = new List<Configuration.BuildingPasteSettings.PasteBuild>
                {
                    new Configuration.BuildingPasteSettings.PasteBuild
                    {
                        housepath = "HouseCobalt",
                        heightBuilding = 0,
                        copterChance = 50,
                        NpcPressets = new HashSet<Configuration.BuildingPasteSettings.PresetConfig>
                        {
                            new Configuration.BuildingPasteSettings.PresetConfig
                            {
                                Min = 3,
                                Max = 7,
                                helpBot = true,
                                helpCount = 3,
                                Config = new Configuration.BuildingPasteSettings.NpcConfig
                                {
                                    Name = "Cobalt Defense",
                                    Health = 230f,
                                    RoamRange = 35f,
                                    ChaseRange = 10f,
                                    AttackRangeMultiplier = 3f,
                                    SenseRange = 50f,
                                    MemoryDuration = 40f,
                                    DamageScale = 2f,
                                    AimConeScale = 1f,
                                    CheckVisionCone = false,
                                    VisionCone = 135f,
                                    Speed = 8f,
                                    DisableRadio = false,
                                    Stationary = false,
                                    IsRemoveCorpse = false,
                                    WearItems = new HashSet<Configuration.BuildingPasteSettings.NpcConfig.NpcWear>
                                    {
                                        new Configuration.BuildingPasteSettings.NpcConfig.NpcWear { ShortName = "roadsign.kilt", SkinID = 1121447954 },
                                        new Configuration.BuildingPasteSettings.NpcConfig.NpcWear { ShortName = "burlap.shirt", SkinID = 2076298726 },
                                        new Configuration.BuildingPasteSettings.NpcConfig.NpcWear { ShortName = "shoes.boots", SkinID = 0 },
                                        new Configuration.BuildingPasteSettings.NpcConfig.NpcWear { ShortName = "roadsign.gloves", SkinID = 0 },
                                        new Configuration.BuildingPasteSettings.NpcConfig.NpcWear { ShortName = "burlap.trousers", SkinID = 2076292 },
                                        new Configuration.BuildingPasteSettings.NpcConfig.NpcWear { ShortName = "metal.facemask", SkinID = 835028125 }
                                    },
                                    BeltItems = new HashSet<Configuration.BuildingPasteSettings.NpcConfig.NpcBelt>
                                    {
                                        new Configuration.BuildingPasteSettings.NpcConfig.NpcBelt { ShortName = "rifle.lr300", Amount = 1, SkinID = 1837473292, Mods = new HashSet<string> { "weapon.mod.flashlight"} },
                                        new Configuration.BuildingPasteSettings.NpcConfig.NpcBelt { ShortName = "pistol.semiauto", Amount = 1, SkinID = 1557105240, Mods = new HashSet<string>() },
                                        new Configuration.BuildingPasteSettings.NpcConfig.NpcBelt { ShortName = "syringe.medical", Amount = 10, SkinID = 0, Mods = new HashSet<string>() },
                                        new Configuration.BuildingPasteSettings.NpcConfig.NpcBelt { ShortName = "grenade.smoke", Amount = 3, SkinID = 0, Mods = new HashSet<string>() }
                                    },
                                    Kit = ""
                                },
                                TypeLootTable = 1,
                                OwnLootTable = new Configuration.LootTableConfig
                                {
                                    Min = 1,
                                    Max = 1,
                                    UseCount = true,
                                    Items =new List<Configuration.ItemConfig>
                                    {
                                        new Configuration.ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 50f, IsBluePrint = false, SkinID = 0, Name = "" },
                                        new Configuration.ItemConfig { ShortName = "supply.signal", MinAmount = 1, MaxAmount = 1, Chance = 20f, IsBluePrint = false, SkinID = 0, Name = "" },
                                        new Configuration.ItemConfig { ShortName = "syringe.medical", MinAmount = 1, MaxAmount = 2, Chance = 70.0f, IsBluePrint = false, SkinID = 0, Name = "" }
                                    }
                                }
                            },

                            new Configuration.BuildingPasteSettings.PresetConfig
                            {
                                Min = 1,
                                Max = 3,
                                helpBot = false,
                                helpCount = 0,
                                Config = new Configuration.BuildingPasteSettings.NpcConfig
                                {
                                    Name = "Cobalt Guard",
                                    Health = 150f,
                                    RoamRange = 30.0f,
                                    ChaseRange = 90f,
                                    AttackRangeMultiplier = 2.0f,
                                    SenseRange = 50.0f,
                                    MemoryDuration = 30f,
                                    DamageScale = 1f,
                                    AimConeScale = 1f,
                                    CheckVisionCone = false,
                                    VisionCone = 135f,
                                    Speed = 7f,
                                    DisableRadio = true,
                                    Stationary = false,
                                    IsRemoveCorpse = false,
                                    WearItems = new HashSet<Configuration.BuildingPasteSettings.NpcConfig.NpcWear>
                                    {
                                        new Configuration.BuildingPasteSettings.NpcConfig.NpcWear { ShortName = "roadsign.kilt", SkinID = 1121447954 },
                                        new Configuration.BuildingPasteSettings.NpcConfig.NpcWear { ShortName = "burlap.shirt", SkinID = 2076298726 },
                                        new Configuration.BuildingPasteSettings.NpcConfig.NpcWear { ShortName = "shoes.boots", SkinID = 0 },
                                        new Configuration.BuildingPasteSettings.NpcConfig.NpcWear { ShortName = "roadsign.gloves", SkinID = 0 },
                                        new Configuration.BuildingPasteSettings.NpcConfig.NpcWear { ShortName = "burlap.trousers", SkinID = 2076292007 },
                                        new Configuration.BuildingPasteSettings.NpcConfig.NpcWear { ShortName = "metal.facemask", SkinID = 835028125 }
                                    },
                                    BeltItems = new HashSet<Configuration.BuildingPasteSettings.NpcConfig.NpcBelt>
                                    {
                                        new Configuration.BuildingPasteSettings.NpcConfig.NpcBelt { ShortName = "rifle.lr300", Amount = 1, SkinID = 1837473292, Mods = new HashSet<string> { "weapon.mod.flashlight"} },
                                        new Configuration.BuildingPasteSettings.NpcConfig.NpcBelt { ShortName = "pistol.semiauto", Amount = 1, SkinID = 1557105240, Mods = new HashSet<string>() },
                                        new Configuration.BuildingPasteSettings.NpcConfig.NpcBelt { ShortName = "syringe.medical", Amount = 10, SkinID = 0, Mods = new HashSet<string>() },
                                        new Configuration.BuildingPasteSettings.NpcConfig.NpcBelt { ShortName = "grenade.f1", Amount = 3, SkinID = 0, Mods = new HashSet<string>() }
                                    },
                                    Kit = ""
                                },
                                TypeLootTable = 1,
                                OwnLootTable = new Configuration.LootTableConfig
                                {
                                    Min = 1,
                                    Max = 1,
                                    UseCount = true,
                                    Items =new List<Configuration.ItemConfig>
                                    {
                                        new Configuration.ItemConfig { ShortName = "scrap", MinAmount = 5, MaxAmount = 10, Chance = 50f, IsBluePrint = false, SkinID = 0, Name = "" },
                                        new Configuration.ItemConfig { ShortName = "supply.signal", MinAmount = 1, MaxAmount = 1, Chance = 20f, IsBluePrint = false, SkinID = 0, Name = "" },
                                        new Configuration.ItemConfig { ShortName = "syringe.medical", MinAmount = 1, MaxAmount = 2, Chance = 70.0f, IsBluePrint = false, SkinID = 0, Name = "" }
                                    }
                                }
                            }
                        }
                    }
                };
            }
            if (config.boxSetting.OwnLootTable.Items.Count == 0)
            {
                config.boxSetting.OwnLootTable.Items = new List<Configuration.ItemConfig>
                {
                    new Configuration.ItemConfig
                    {
                        ShortName = "pistol.python",
                        SkinID = 0,
                        Name = "",
                        IsBluePrint = false,
                        MinAmount = 1,
                        MaxAmount = 1,
                        Chance = 60,
                    },
                    new Configuration.ItemConfig
                    {
                        ShortName = "multiplegrenadelauncher",
                        SkinID = 0,
                        Name = "",
                        IsBluePrint = false,
                        MinAmount = 1,
                        MaxAmount = 1,
                        Chance = 15,
                    },
                    new Configuration.ItemConfig
                    {
                        ShortName = "sulfur",
                        SkinID = 0,
                        Name = "",
                        IsBluePrint = false,
                        MinAmount = 500,
                        MaxAmount = 800,
                        Chance = 40,
                    },
                    new Configuration.ItemConfig
                    {
                        ShortName = "wall.external.high.ice",
                        SkinID = 0,
                        Name = "",
                        IsBluePrint = false,
                        MinAmount = 1,
                        MaxAmount = 5,
                        Chance = 75,
                    },
                };
            }
        }
        
                object CanUILootSpawn(LootContainer container)
        {
            if (container == null || CrateEnt == null)
            {
                return null;
            }

            if (container == CrateEnt)
            {
                if (config.boxSetting.TypeLootTable == 3)
                {
                    return null;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                return null;
            }
        }

        
                private void LoadDataCopyPaste()
        {
            foreach (var building in config.pasteSettings.pasteBuilds)
            {
                if (!Interface.Oxide.DataFileSystem.ExistsDatafile("copypaste/" + building.housepath))
                {
                    PrintError(GetLang("XD_EVENT_BUILDING_NOTFOUND", null, building.housepath));
                    NextTick(() =>
                    {
                        Interface.Oxide.UnloadPlugin(Name);
                    });
                    return;
                }
            }
            
        }
        private object GetSpawnPoints()
        {
            if (busyPoints3D.ToList().Count <= 3)
            {
                if (FindPositions == null)
                {
                    PrintWarning(GetLang("XD_IVENT_CLCONTROLLER_THE_POINTS_ARE_ENDED"));
                    busyPoints3D.Clear();
                    FindPositions = ServerMgr.Instance.StartCoroutine(GenerateSpawnPoints());
                    GenerateIvent();
                }
                return Vector3.zero;
            }

            Vector3 targetPos = busyPoints3D.ToList().GetRandom();
            if (targetPos == Vector3.zero)
            {
                busyPoints3D.Remove(targetPos);
                return GetSpawnPoints();
            }

            bool valid = Is3DPointValid(targetPos, 1 << 8 | 1 << 9 | 1 << 17 | 1 << 21);
            if (!valid || !HasBuildingPrivilege(targetPos, config.spawnPositionGenerateSetting.buildingBlockFindRadius))
            {
                busyPoints3D.Remove(targetPos);
                return GetSpawnPoints();
            }
            busyPoints3D.Remove(targetPos);
            ClearArea(ref targetPos);
            return targetPos;
        }
		   		 		  						  	   		  		 			   					  	  			   			
                Vector3 RandomCircle(Vector3 center, float radius, int npcCount, int i)
        {
            float ang = 360 / npcCount * i;
            Vector3 pos;
            pos.x = center.x + radius * Mathf.Sin(ang * Mathf.Deg2Rad);
            pos.z = center.z + radius * Mathf.Cos(ang * Mathf.Deg2Rad);
            pos.y = center.y;
            pos.y = GetGroundPosition(pos);
		   		 		  						  	   		  		 			   					  	  			   			
            return pos;
        }
		   		 		  						  	   		  		 			   					  	  			   			
        private void OnCorpsePopulate(ScientistNPC entity, NPCPlayerCorpse corpse)
        {
            if (entity == null)
            {
                return;
            }

            if (npcs.Contains(entity))
            {
                npcs.Remove(entity);
                Configuration.BuildingPasteSettings.PresetConfig preset = labIndex.NpcPressets.FirstOrDefault(x => x.Config.Name == entity.displayName);
                NextTick(() =>
                {
                    if (corpse == null)
                    {
                        return;
                    }

                    ItemContainer container = corpse.containers[0];
                    if (preset.TypeLootTable == 0)
                    {
                        for (int i = container.itemList.Count - 1; i >= 0; i--)
                        {
                            Item item = container.itemList[i];
                            if (preset.Config.WearItems.Any(x => x.ShortName == item.info.shortname))
                            {
                                item.RemoveFromContainer();
                                item.Remove();
                            }
                        }
                        return;
                    }
                    if (preset.TypeLootTable == 2)
                    {
                        if (preset.Config.IsRemoveCorpse && !corpse.IsDestroyed)
                        {
                            corpse.Kill();
                        }

                        return;
                    }
                    for (int i = container.itemList.Count - 1; i >= 0; i--)
                    {
                        Item item = container.itemList[i];
                        item.RemoveFromContainer();
                        item.Remove();
                    }
                    if (preset.TypeLootTable == 1)
                    {
                        AddToContainerItem(container, preset.OwnLootTable);
                    }

                    if (preset.Config.IsRemoveCorpse && !corpse.IsDestroyed)
                    {
                        corpse.Kill();
                    }
                });
            }
        }
        private readonly Quaternion[] directions =
        {
            Quaternion.Euler(90, 0, 0),
            Quaternion.Euler(0, 0, 90),
            Quaternion.Euler(0, 0, 180)
        };

        public class Fields
        {
            public string name
            {
                get; set;
            }
            public string value
            {
                get; set;
            }
            public bool inline
            {
                get; set;
            }
            public Fields(string name, string value, bool inline)
            {
                this.name = name;
                this.value = value;
                this.inline = inline;
            }
        }
        private class Configuration
        {
            internal class MapMarkers
            {
                [JsonProperty(Ru ? "Отметить ивент на карте G (Требуется https://umod.org/plugins/marker-manager)" : "Mark the event on the G card (Requires FREE https://umod.org/plugins/marker-manager)")]
                public bool MapUse = true;
                [JsonProperty(Ru ? "Текст для карты G" : "Text for map G")]
                public string MapTxt = "Cobalt lab";
                [JsonProperty(Ru ? "Цвет маркера (без #)" : "Marker color (without #)")]
                public string colorMarker = "f3ecad";
                [JsonProperty(Ru ? "Цвет обводки (без #)" : "Outline color (without #)")]
                public string colorOutline = "ff3535";
            }

            internal class RadiationConroller
            {
                [JsonProperty(Ru ? "Количество радиационных частиц" : "Number of radiation particles")]
                public int radCount = 20;
                [JsonProperty(Ru ? "Радиус зоны поражения (Не более чем радиус обноружения игроков)" : "Radius of the affected area (No more than the radius of detection of players)")]
                public int radRadius = 20;
                [JsonProperty(Ru ? "Включить радиацию ?" : "Turn on radiation?")]
                public bool radUse = true;
            }
            [JsonProperty(Ru ? "Настройка подбора позиций для спавна (Для опытных пользователей)" : "Setting up the selection of positions for spawn (For experienced users)")]
            public SpawnPositionGenerateSetting spawnPositionGenerateSetting = new SpawnPositionGenerateSetting();
            public class CommandReward
            {
                [JsonProperty(Ru ? "Список команд, которые выполняются в консоли (%STEAMID% - игрок который залутает ящик )" : "List of commands that are executed in the console (%STEAMID% - the player who looted the box)")]
                public List<string> Commands = new List<string>();
                [JsonProperty(Ru ? "Сообщения который игрок получит (Здесь можно написать о том , что получил игрок)" : "Messages that the player will receive (Here you can write about what the player received)")]
                public string MessagePlayerReward = "";
            }
            [JsonProperty(Ru ? "Настройка радиации в зоне ивента" : "Setting up radiation in the event area")]
            public RadiationConroller radiationConroller = new RadiationConroller();
            internal class NotiferSettings
            {
                [JsonProperty(Ru ? "ВебХук дискорда (Если не нужны уведомления в дискорд, оставьте поле пустым)" : "Discord WebHook (If you do not need discord notifications, leave the field blank)")]
                public string weebHook = string.Empty;
                [JsonProperty(Ru ? "Включить UI Уведомления ?" : "Enable UI Notifications?")]
                public bool useUiNotifi = true;
                [JsonProperty(Ru ? "Скрывать автоматически UI уведомления?" : "Auto hide UI notifications?")]
                public bool hideUiNotifi = true;
                [JsonProperty(Ru ? "Через сколько после показа будет скрываться? (сек)" : "How long after the show will it hide? (sec)")]
                public float hideUiNotifiTime = 15f;
                [JsonProperty(Ru ? "Цвет заднего фона окна UI" : "UI window background color")]
                public string colorBackground = "0.8 0.28 0.2 0.8";
                [JsonProperty(Ru ? "Цвет Кнопки закрытия UI" : "UI Close Button Color")]
                public string colorBtnCloseUi = "0.6784314 0.254902 0.1843137 0.8";
            }
            internal class IventController
            {
                [JsonProperty(Ru ? "Минимальное количество игроков для запуска ивента" : "The minimum number of players to start an event")]
                public int minPlayedPlayers = 0;
                [JsonProperty(Ru ? "Время до начала ивента (Минимальное в секундах)" : "Time before the start of the event (Minimum in seconds)")]
                public int minSpawnIvent = 3000;
                [JsonProperty(Ru ? "Время до начала ивента (Максимальное в секундах)" : "Time before the start of the event (Maximum in seconds)")]
                public int maxSpawnIvent = 7200;
                [JsonProperty(Ru ? "Время до удаления ивента если никто не откроет ящик (Секунды)" : "Time until the event is deleted if no one opens the box (Seconds)")]
                public int timeRemoveHouse = 900;
                [JsonProperty(Ru ? "Время до удаления ивента после того как разблокируется ящик" : "The time until the event is deleted after the box is unlocked")]
                public int timeRemoveHouse2 = 300;
                [JsonProperty(Ru ? "Создавать PVP зону в радиусе ивента ? (Требуется TruePVE)" : "Create a PVP zone within the radius of the event? (Requires TruePVE)")]
                public bool UseZonePVP = false;
                [JsonProperty(Ru ? "Используете ли вы купол" : "Do you use a dome ?")]
                public bool useSphere = false;
                [JsonProperty(Ru ? "Прозрачность купола (чем меньше число тем более он прозрачный. Значения должно быть не более 5)" : "Transparency of the dome (the smaller the number, the more transparent it is. The values should be no more than 5)")]
                public int transperent = 3;

            }

            internal class BuildingPasteSettings
            {
                [JsonProperty(Ru ? "Постройки для спавна. (Если более 1 то выбирается рандомная)" : "Spawn buildings. (If more than 1 then random is selected)")]
                public List<PasteBuild> pasteBuilds = new List<PasteBuild>();
                internal class PasteBuild
                {
                    [JsonProperty(Ru ? "Настройка высоты постройки (Требуется в настройке, если вы хотите ставить свою постройку)" : "Setting the height of the building (Required in the setting if you want to place your building)")]
                    public int heightBuilding;
                    [JsonProperty(Ru ? "Названия файла в папке `/oxide/data/copypaste` с вашей постройкой" : "The names of the file in the folder `/oxide/data/copypaste` with your building")]
                    public string housepath;
                    [JsonProperty(Ru ? "Шанс спавна коптера (Если присутствует в постройке)" : "Copter spawn chance (if present in a building)")]
                    public int copterChance;
                    [JsonProperty(Ru ? "Настройка NPC" : "NPCs setting")]
                    public HashSet<PresetConfig> NpcPressets;

                }

                public class PresetConfig
                {
                    [JsonProperty(Ru ? "Минимальное кол-во" : "Minimum")]
                    public int Min;
                    [JsonProperty(Ru ? "Максимальное кол-во" : "Maximum")]
                    public int Max;
                    [JsonProperty(Ru ? "Появятся ли они в качестве помощи после взлома запертого ящика?" : "Will they spawn as help after hacking locked crate?")]
                    public bool helpBot;
                    [JsonProperty(Ru ? "Колличевство нпс (Подмога)" : "Number of NPCs (Help)")]
                    public int helpCount;
                    [JsonProperty(Ru ? "Настройки NPC" : "NPCs setting")]
                    public NpcConfig Config;
                    [JsonProperty(Ru ? "Какую таблицу предметов необходимо использовать? (0 - стандартную; 1 - собственную; 2 - AlphaLoot;" : "Which loot table should the plugin use? (0 - default; 1 - own; 2 - AlphaLoot;")]
                    public int TypeLootTable;
                    [JsonProperty(Ru ? "Собственная таблица предметов (если тип таблицы предметов - 1)" : "Own loot table (if the loot table type is 1)")]
                    public LootTableConfig OwnLootTable = new LootTableConfig();
                }
		   		 		  						  	   		  		 			   					  	  			   			
                internal class NpcConfig
                {
                    [JsonProperty(Ru ? "Название" : "Name")]
                    public string Name;
                    [JsonProperty(Ru ? "Кол-во ХП" : "Health")]
                    public float Health;
                    [JsonProperty(Ru ? "Дальность патрулирования местности" : "Roam Range")]
                    public float RoamRange;
                    [JsonProperty(Ru ? "Дальность погони за целью" : "Chase Range")]
                    public float ChaseRange;
                    [JsonProperty(Ru ? "Множитель радиуса атаки" : "Attack Range Multiplier")]
                    public float AttackRangeMultiplier;
                    [JsonProperty(Ru ? "Радиус обнаружения цели" : "Sense Range")]
                    public float SenseRange;
                    [JsonProperty(Ru ? "Длительность памяти цели [sec.]" : "Target Memory Duration [sec.]")]
                    public float MemoryDuration;
                    [JsonProperty(Ru ? "Множитель урона" : "Scale damage")]
                    public float DamageScale;
                    [JsonProperty(Ru ? "Множитель разброса" : "Aim Cone Scale")]
                    public float AimConeScale;
                    [JsonProperty(Ru ? "Обнаруживать цель только в углу обзора NPC? [true/false]" : "Detect the target only in the NPC's viewing vision cone? [true/false]")]
                    public bool CheckVisionCone;
                    [JsonProperty(Ru ? "Угол обзора" : "Vision Cone")]
                    public float VisionCone;
                    [JsonProperty(Ru ? "Скорость" : "Speed")]
                    public float Speed;
                    [JsonProperty(Ru ? "Отключать эффекты рации? [true/false]" : "Disable radio effects? [true/false]")]
                    public bool DisableRadio;
                    [JsonProperty(Ru ? "Это стационарный NPC? [true/false]" : "Is this a stationary NPC? [true/false]")]
                    public bool Stationary;
                    [JsonProperty(Ru ? "Удалять труп после смерти? (рекомендуется использовать значение true для повышения производительности) [true/false]" : "Remove a corpse after death? (it is recommended to use the true value to improve performance) [true/false]")]
                    public bool IsRemoveCorpse;
                    [JsonProperty(Ru ? "Одежда" : "Wear items")]
                    public HashSet<NpcWear> WearItems;
                    [JsonProperty(Ru ? "Быстрые слоты" : "Belt items")]
                    public HashSet<NpcBelt> BeltItems;
                    [JsonProperty(Ru ? "Kit" : "Kit")]
                    public string Kit;
		   		 		  						  	   		  		 			   					  	  			   			
                    internal class NpcBelt
                    {
                        [JsonProperty("ShortName")]
                        public string ShortName;
                        [JsonProperty(Ru ? "Кол-во" : "Amount")]
                        public int Amount;
                        [JsonProperty("SkinID (0 - default)")]
                        public ulong SkinID;
                        [JsonProperty(Ru ? "Модификации на оружие" : "Mods")]
                        public HashSet<string> Mods;
                    }

                    internal class NpcWear
                    {
                        [JsonProperty("ShortName")]
                        public string ShortName;
                        [JsonProperty("SkinID (0 - default)")]
                        public ulong SkinID;
                    }
                }
            }
            [JsonProperty(Ru ? "Награда в виде команды, игроку который 1 открыл груз" : "Reward in the form of a team to the player who 1 opened the cargo")]
            public CommandReward commandReward = new CommandReward();
            [JsonProperty(Ru ? "Отображения ивента на картах" : "Event display on maps")]
            public MapMarkers mapMarkers = new MapMarkers();
            [JsonProperty(Ru ? "Настройка ящика" : "Customizing the box")]
            public BoxSetting boxSetting = new BoxSetting();
            [JsonProperty(Ru ? "Настройка постройки для ивента (CopyPaste) и нпс" : "Setting up buildings for the event (Copypaste) and NPCs")]
            public BuildingPasteSettings pasteSettings = new BuildingPasteSettings();
            [JsonProperty(Ru ? "Настройка запуска и остановки ивента" : "Setting up and stopping an event")]
            public IventController iventController = new IventController();


            internal class BoxSetting
            {
                [JsonProperty(Ru ? "Время разблокировки ящиков [sec.]" : "Time to unlock the Crates [sec.]")]
                public float UnlockTime = 900f;
                [JsonProperty(Ru ? "Какую таблицу лута необходимо использовать? (0 - стандартную; 1 - собственную; 2 - AlphaLoot; 3 - EcoLootUI; " : "Which loot table should the plugin use? (0 - default; 1 - own; 2 - AlphaLoot; 3 - EcoLootUI;")]
                public int TypeLootTable = 0;
                [JsonProperty(Ru ? "Собственная таблица предметов (если тип таблицы предметов - 1)" : "Own loot table (if the loot table type is 1)")]
                public LootTableConfig OwnLootTable = new LootTableConfig();
                [JsonProperty(Ru ? "Включить сигнализацию при взломе запертого язика ?" : "Turn on the alarm when breaking into a locked box ?")]
                public bool signaling = true;
            }
            [JsonProperty(Ru ? "Настройка уведомлений" : "Configuring notifications")]
            public NotiferSettings notiferSettings = new NotiferSettings();
            internal class SpawnPositionGenerateSetting
            {
                [JsonProperty(Ru ? "Разрешить спавн на дорогах ?" : "Allow spawn on the roads ?")]
                public bool spawnOnRoad = true;
                [JsonProperty(Ru ? "Разрешить спавн на реках ?" : "Allow spawn on rivers ?")]
                public bool spawnOnRiver = true;
                [JsonProperty(Ru ? "Радиус обноружения монументов" : "Radius of monument detection")]
                public float monumentFindRadius = 40f;
                [JsonProperty(Ru ? "Радиус обноружения шкафов (Building Block)" : "Detection radius of the tool cupboard (Building Block)")]
                public float buildingBlockFindRadius = 90f;
            }

            public class ItemConfig
            {
                [JsonProperty("ShortName")]
                public string ShortName;
                [JsonProperty(Ru ? "Минимальное кол-во" : "Minimum")]
                public int MinAmount;
                [JsonProperty(Ru ? "Максимальное кол-во" : "Maximum")]
                public int MaxAmount;
                [JsonProperty(Ru ? "Шанс выпадения предмета [0.0-100.0]" : "Chance [0.0-100.0]")]
                public float Chance;
                [JsonProperty(Ru ? "Это чертеж? [true/false]" : "Is this a blueprint? [true/false]")]
                public bool IsBluePrint;
                [JsonProperty("SkinID (0 - default)")]
                public ulong SkinID;
                [JsonProperty(Ru ? "Название (empty - default)" : "Name (empty - default)")]
                public string Name;
                [JsonProperty(Ru ? "Умножать количество предмета на количество дней с начала вайпа (на 3й день - лута будет в 3 раза больше)" : "Multiply the amount of the item by the number of days since the beginning of the wipe (on the 3rd day, the loot will be 3 times more)")]
                public bool lootWipePlus = false;
            }
            public class LootTableConfig
            {
                [JsonProperty(Ru ? "Минимальное кол-во элементов" : "Minimum numbers of items")]
                public int Min;
                [JsonProperty(Ru ? "Максимальное кол-во элементов" : "Maximum numbers of items")]
                public int Max;
                [JsonProperty(Ru ? "Использовать минимальное и максимальное значение? [true/false]" : "Use minimum and maximum values? [true/false]")]
                public bool UseCount;
                [JsonProperty(Ru ? "Список предметов" : "List of items")]
                public List<ItemConfig> Items = new List<ItemConfig>();
            }
        }
        private bool Is3DPointValid(Vector3 point, int layer)
        {
            List<Collider> colliders = Pool.GetList<Collider>();

            Vis.Colliders(point, 25f, colliders, layer, QueryTriggerInteraction.Collide);

            int count = colliders.Count;

            foreach (Collider collider in colliders)
            {
                if (collider == null || collider.transform == null)
                {
                    count--;
                    continue;
                }

                if (collider.name.Contains("SafeZone"))
                {
                    count = int.MaxValue;
                    continue;
                }

                BaseEntity e = collider.ToBaseEntity();

                if (IsAsset(collider.name) && (e == null || e.name.Contains("/treessource/")))
                {
                    count = int.MaxValue;
                    break;
                }

                if (e.IsValid())
                {
                    if (e is BasePlayer)
                    {
                        BasePlayer player = e as BasePlayer;
		   		 		  						  	   		  		 			   					  	  			   			
                        if (player.IsSleeping())
                        {
                            count--;
                        }
                        else if (player.IsNpc || player.IsFlying)
                        {
                            count--;
                        }
                        else
                        {
                            count = int.MaxValue;
                            break;
                        }
                    }
                    else if (e.OwnerID.IsSteamId())
                    {
                        count--;
                    }
                    else if (e.IsNpc || e is SleepingBag)
                    {
                        count--;
                    }
                    else if (e is BaseOven)
                    {
                        if (e.bounds.size.Max() > 1.6f)
                        {
                            count = int.MaxValue;
                            break;
                        }
                        else
                        {
                            count--;
                        }
                    }
                    else if (e is PlayerCorpse)
                    {
                        PlayerCorpse corpse = e as PlayerCorpse;
		   		 		  						  	   		  		 			   					  	  			   			
                        if (corpse.playerSteamID == 0 || corpse.playerSteamID.IsSteamId())
                        {
                            count = int.MaxValue;
                            break;
                        }
                        else
                        {
                            count--;
                        }
                    }
                    else if (e is DroppedItemContainer && e.prefabID != 545786656)
                    {
                        DroppedItemContainer backpack = e as DroppedItemContainer;

                        if (backpack.playerSteamID == 0 || backpack.playerSteamID.IsSteamId())
                        {
                            count = int.MaxValue;
                            break;
                        }
                        else
                        {
                            count--;
                        }
                    }
                    else if (e.OwnerID == 0)
                    {
                        if (e is BuildingBlock)
                        {
                            count = int.MaxValue;
                            break;
                        }
                        else
                        {
                            count--;
                        }
                    }
                    else
                    {
                        count = int.MaxValue;
                        break;
                    }
                }
                else if (collider.gameObject.layer == (int)Layer.World)
                {
                    if (collider.name.Contains("rock_") || collider.name.Contains("formation_", CompareOptions.OrdinalIgnoreCase))
                    {
                        float height = GetRockHeight(collider.transform.position);

                        if (height > 2f)
                        {
                            count = int.MaxValue;
                            break;
                        }
                        else
                        {
                            count--;
                        }
                    }
                    else if (!config.spawnPositionGenerateSetting.spawnOnRoad && collider.name.StartsWith("road_"))
                    {
                        count = int.MaxValue;
                        break;
                    }
                    else if (collider.name.StartsWith("ice_sheet"))
                    {
                        count = int.MaxValue;
                        break;
                    }
                    else
                    {
                        count--;
                    }
                }
                else if (collider.gameObject.layer == (int)Layer.Water)
                {
                    if (!config.spawnPositionGenerateSetting.spawnOnRiver && collider.name.StartsWith("River Mesh"))
                    {
                        count = int.MaxValue;
                        break;
                    }

                    count--;
                }
                else
                {
                    count--;
                }
            }

            Pool.FreeList(ref colliders);
		   		 		  						  	   		  		 			   					  	  			   			
            return count == 0;
        }
		   		 		  						  	   		  		 			   					  	  			   			
        private void StartIvent()
        {
            if (!NpcSpawn)
            {
                PrintError(GetLang("XD_IVENT_CLCONTROLLER_NOT_NPSSPAWN"));
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }
            if (BasePlayer.activePlayerList.Count < config.iventController.minPlayedPlayers)
            {
                Puts(GetLang("XD_IVENT_CLCONTROLLER_NOT_ENOUGH_PLAYERS"));
                GenerateIvent();
                return;
            }
            if (RemoveHouseTime != null)
            {
                RemoveHouseTime.Destroy();
            }
		   		 		  						  	   		  		 			   					  	  			   			
            if (SpawnHouseTime != null)
            {
                SpawnHouseTime.Destroy();
            }

            Subscribes();
            labIndex = config.pasteSettings.pasteBuilds[0];
            if (config.pasteSettings.pasteBuilds.Count > 1)
            {
                labIndex = config.pasteSettings.pasteBuilds.GetRandom();
            }
            GenerateBuilding(labIndex);
        }
        
        
                public class FancyMessage
        {
            public string content
            {
                get; set;
            }
            public bool tts
            {
                get; set;
            }
            public Embeds[] embeds
            {
                get; set;
            }

            public class Embeds
            {
                public string title
                {
                    get; set;
                }
                public int color
                {
                    get; set;
                }
                public List<Fields> fields
                {
                    get; set;
                }

                public Embeds(string title, int color, List<Fields> fields)
                {
                    this.title = title;
                    this.color = color;
                    this.fields = fields;
                }
            }

            public FancyMessage(string content, bool tts, Embeds[] embeds)
            {
                this.content = content;
                this.tts = tts;
                this.embeds = embeds;
            }

            public string toJSON()
            {
                return JsonConvert.SerializeObject(this);
            }
        }

        private bool IsAsset(string value)
        {
            foreach (string asset in assets)
            {
                if (value.Contains(asset))
                {
                    return true;
                }
            }

            return false;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new Exception();
                }
		   		 		  						  	   		  		 			   					  	  			   			
                SaveConfig();
            }
            catch
            {
                for (int i = 0; i < 3; i++)
                {
                    PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                }

                LoadDefaultConfig();
            }
            ValidateConfig();
            SaveConfig();
        }

        private void GenerateMapMarker(Vector3 pos)
        {
            if (config.mapMarkers.MapUse)
            {
                Interface.CallHook("API_CreateMarker", pos, "xdcobaltlab", 0, 3f, 0.3f, config.mapMarkers.MapTxt, config.mapMarkers.colorMarker, config.mapMarkers.colorOutline);
            }
        }
        private object CanPopulateLoot(LootContainer container)
        {
            if (container == null || CrateEnt == null)
            {
                return null;
            }

            if (container == CrateEnt)
            {
                if (config.boxSetting.TypeLootTable == 2)
                {
                    return null;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                return null;
            }
        }
        private const float MaxElevation = 2.5f;
        private static string GetGridString(Vector3 position)
        {
            return PhoneController.PositionToGridCoord(position);
        }
        
                protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["XD_IVENT_START"] = "<color=#008000>[Cobalt Lab Event] </color>Ученые разбили на этом острове свою лабораторию под названием Кобальт, скорее всего там находятся ценные вещи, ведь она охраняется!\nКвадрат: <color=#00FF00>{0}</color>",
                ["XD_IVENT_STARTUI"] = "Ученые развернули свою Кобальтовую лабораторию!\nКвадрат : {0}",
                ["XD_IVENT_NOPLAYER"] = "<color=#008000>[Cobalt Lab Event] </color>Ученые закончили свой эксперимент и успешно покинули остров без происшествий",
                ["XD_IVENT_CRATEHACK"] = "<color=#008000>[Cobalt Lab Event] </color>В лаборатории кобальт <color=#FFD700>{0}</color> начал взлом секретного ящика в квадрате <color=#00FF00>{1}</color>",
                ["XD_IVENT_CRATEHACKHELP"] = "<color=#008000>[Cobalt Lab Event] </color>В лаборатории кобальт <color=#FFD700>{0}</color> начал взлом секретного ящика в квадрате <color=#00FF00>{1}</color>\nНа это место уже прибыла подмога! Будте осторожней",
                ["XD_IVENT_CRATEHACKEND"] = "<color=#008000>[Cobalt Lab Event] </color>В лаборатории кобальт был взломан секретный ящик, ученые начинают эвакуацию с острова, у вас осталось <color=#9ACD32>{0} минут</color>, чтобы забрать его!",
                ["XD_IVENT_CRATELOOTFOUND"] = " <color=#008000>[Cobalt Lab Event] </color>В лаборатории кобальт никто не успел залутать взломанный ящик, лаборатория была эвакуирована и постройка разрушена",
                ["XD_IVENT_CRATELOOTPLAYER"] = "<color=#008000>[Cobalt Lab Event] </color><color=#FFD700>{0}</color> успешно ограбил лабораторию кобальт и забрал ценные вещи с секретного ящика",
                ["XD_IVENT_HOUSECOBALT"] = "Cobalt laboratory",
                ["XD_IVENT_START_DISCORD"] = "Ученые разбили на этом острове свою лабораторию под названием Кобальт,скорее всего там находится ценные вещи, ведь он охраняется!\nКвадрат : {0}",
                ["XD_IVENT_NOPLAYER_DISCORD"] = "Ученые закончили свой эксперимент и успешно покинули остров без происшествий",
                ["XD_IVENT_CRATEHACK_DISCORD"] = "В лаборатории кобальт {0} начал взлом секретного ящика в квадрате {1}\nСоберитесь с силами и отбейте его",
                ["XD_IVENT_CRATEHACKHELP_DISCORD"] = "В лаборатории кобальт {0} начал взлом секретного ящика в квадрате {1}\nСоберитесь с силами и отбейте его\nНа это место уже прибыла подмога! Будте осторожней",
                ["XD_IVENT_CRATEHACKEND_DISCORD"] = "В лаборатории кобальт был взломан секретный ящик, ученые начинают эвакуацию с острова, у вас осталось {0} минут, чтобы забрать его!",
                ["XD_IVENT_CRATELOOTFOUND_DISCORD"] = "В лаборатории кобальт никто не успел залутать взломанный ящик, лаборатория была эвакуирована и постройка разрушена",
                ["XD_IVENT_CRATELOOTPLAYER_DISCORD"] = "{0}  успешно ограбил лабораторию кобальт и забрал ценные вещи с секретного ящика",
                ["XD_IVENT_CL_ITEM_SYNTAX"] = "Используйте:\n/cl.items add - добавить лут к существующему\n/cl.items reset - заменить старый лут на новый",
                ["XD_IVENT_CL_ITEM_ADDED"] = "Вы успешно добавили новые предметы для ящика.\nОбязательно настройте их в конфиге",
                ["XD_IVENT_CL_ITEM_REPLACED"] = "Вы успешно заменили все предметы на новые.\nОбязательно настройте их в конфиге",
                ["XD_IVENT_CLBOT_ITEM_SYNTAX"] = "Используйте:\n/cl.botitems add \"названия присета\" - добавить лут к существующему\n/cl.botitems reset - заменить старый лут на новый",
                ["XD_IVENT_CLBOT_ITEM_ADDED"] = "Вы успешно добавили новые предметы для npc.\nОбязательно настройте их в конфиге",
                ["XD_IVENT_CLBOT_ITEM__PRESET_NOTFOUND"] = "Присета с именем `{0}`, не существует.",
                ["XD_IVENT_CLBOT_ITEM_REPLACED"] = "Вы успешно заменили все предметы на новые.\nОбязательно настройте их в конфиге",
                ["XD_IVENT_CLCONTROLLER_SYNTAX"] = "Используйте:\n/cl start - Запуск ивента досрочно\n/cl stop - отменить ивент досрочно",
                ["XD_IVENT_CLCONTROLLER_ACTIVE"] = "Ивент уже активен!",
                ["XD_IVENT_CLCONTROLLER_STOPADMIN"] = "Ивент окончен досрочно администратором!",
                ["XD_IVENT_CLCONTROLLER_NOT_ACTIVE"] = "Нет активных ивентов",
                ["XD_IVENT_CLCONTROLLER_NOT_ENOUGH_PLAYERS"] = "Недостаточно игроков для запуска ивента!",
                ["XD_IVENT_CLCONTROLLER_BUILDING_ERROR"] = "Ошибка #1 \nПостройка не смогла заспавнится\nОбратитесь к разработчику\nDezLife#1480\nvk.com/dezlife",
                ["XD_IVENT_CLCONTROLLER_NOT_BUILDING_BOX"] = "Ошибка #3, В постройке не найден ящик",
                ["XD_IVENT_CLCONTROLLER_THE_POINTS_ARE_ENDED"] = "Все точки закончены!\nНачинаем генерировать новые...",
                ["XD_IVENT_CLCONTROLLER_ENTER_PVP"] = "Вы <color=#ce3f27>вошли</color> в PVP зону, теперь другие игроки <color=#ce3f27>могут</color> наносить вам урон!",
                ["XD_IVENT_CLCONTROLLER_EXIT_PVP"] = "Вы <color=#738d43>вышли</color> из PVP зоны, теперь другие игроки <color=#738d43>не могут</color> наносить вам урон!",
                ["XD_IVENT_CLCONTROLLER_NOT_NPSSPAWN"] = "У вас отсутсвует плагин NpcSpawn. Пожалуйста установите его для коректной работы",
                ["XD_EVENT_BUILDING_NOTFOUND"] = "Не найден файл постройки : {0}. Переместите файл с постройкой в папку `/oxide/data/copypaste`",
            }, this, "ru");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["XD_IVENT_START"] = "<color=#008000>[Cobalt Lab Event] </color>Scientists have set up their laboratory on this island called Cobalt, most likely there are valuable things there, because it is protected!\nGrid: <color=#00FF00>{0}</color>",
                ["XD_IVENT_STARTUI"] = "Scientists have deployed their Cobalt Lab!\nGrid : {0}",
                ["XD_IVENT_NOPLAYER"] = "<color=#008000>[Cobalt Lab Event] </color>The scientists completed their experiment and successfully left the island without incident",
                ["XD_IVENT_CRATEHACK"] = "<color=#008000>[Cobalt Lab Event] </color>In the cobalt lab <color=#FFD700>{0}</color> started hacking a secret crate in the grid <color=#00FF00>{1}</color>",
                ["XD_IVENT_CRATEHACKHELP"] = "<color=#008000>[Cobalt Lab Event] </color>In the cobalt lab <color=#FFD700>{0}</color> started hacking a secret crate in the grid <color=#00FF00>{1}</color>\nHelp has already arrived at this place! Be careful",
                ["XD_IVENT_CRATEHACKEND"] = "<color=#008000>[Cobalt Lab Event] </color>A secret crate has been hacked in the cobalt lab, scientists are beginning to evacuate the island, you have <color=#9ACD32>{0} minutes</color> left to pick it up!",
                ["XD_IVENT_CRATELOOTFOUND"] = " <color=#008000>[Cobalt Lab Event] </color>In the cobalt laboratory, no one had time to hack the secret crate, the laboratory was evacuated and the building was destroyed",
                ["XD_IVENT_CRATELOOTPLAYER"] = "<color=#008000>[Cobalt Lab Event] </color><color=#FFD700>{0}</color> successfully robbed the cobalt lab and took valuables from a secret crate",
                ["XD_IVENT_HOUSECOBALT"] = "Cobalt laboratory",
                ["XD_IVENT_START_DISCORD"] = "Scientists have set up their laboratory on this island called Cobalt, most likely there are valuable things there, because it is guarded!\nКвадрат : {0}",
                ["XD_IVENT_NOPLAYER_DISCORD"] = "Scientists finished their experiment and successfully left the island without incident.",
                ["XD_IVENT_CRATEHACK_DISCORD"] = "In the laboratory, cobalt {0} started breaking into a secret box in square {1}\nGather your strength and fight it off",
                ["XD_IVENT_CRATEHACKHELP_DISCORD"] = "In the laboratory, cobalt {0} started breaking into a secret box in square {1}\nGather your strength and fight it off\nHelp has already arrived at this place! Be careful",
                ["XD_IVENT_CRATEHACKEND_DISCORD"] = "A secret box has been broken in the cobalt laboratory, scientists are starting to evacuate the island, you have {0} minutes left to pick it up!",
                ["XD_IVENT_CRATELOOTFOUND_DISCORD"] = " In the cobalt laboratory, no one had time to patch up the cracked box, the laboratory was evacuated and the building was destroyed",
                ["XD_IVENT_CRATELOOTPLAYER_DISCORD"] = "{0}  successfully robbed the cobalt laboratory and took valuable items from the secret box",
                ["XD_IVENT_CL_ITEM_SYNTAX"] = "Use:\n/cl.items add - add loot to the existing one\n/cl.items reset - replace old loot with a new one",
                ["XD_IVENT_CL_ITEM_ADDED"] = "You have successfully added new items to the crate.\nBe sure to configure them in the config",
                ["XD_IVENT_CL_ITEM_REPLACED"] = "You have successfully replaced all items with new ones.\nBe sure to configure them in the config",
                ["XD_IVENT_CLBOT_ITEM_SYNTAX"] = "Use:\n/cl.botitems add \"preset name\" - add loot to the existing one\n/cl.botitems reset - replace old loot with a new one",
                ["XD_IVENT_CLBOT_ITEM_ADDED"] = "You have successfully added new items for the npc.\nBe sure to configure them in the config",
                ["XD_IVENT_CLBOT_ITEM__PRESET_NOTFOUND"] = "There is no preset named `{0}`.",
                ["XD_IVENT_CLBOT_ITEM_REPLACED"] = "You have successfully replaced all items with new ones.\nBe sure to configure them in the config",
                ["XD_IVENT_CLCONTROLLER_SYNTAX"] = "Use:\n/cl start - Launching the event ahead of schedule\n/cl stop - cancel the event ahead of schedule",
                ["XD_IVENT_CLCONTROLLER_ACTIVE"] = "The event is already active!",
                ["XD_IVENT_CLCONTROLLER_STOPADMIN"] = "Event ended ahead of schedule by the administrator!",
                ["XD_IVENT_CLCONTROLLER_NOT_ACTIVE"] = "No active events",
                ["XD_IVENT_CLCONTROLLER_NOT_ENOUGH_PLAYERS"] = "Not enough players to start the event!",
                ["XD_IVENT_CLCONTROLLER_BUILDING_ERROR"] = "Error #1 \nThe building could not spawn\nContact the developer\nDezLife#1480\nvk.com/dezlife",
                ["XD_IVENT_CLCONTROLLER_NOT_BUILDING_BOX"] = "Error #3,No crate found in the building",
                ["XD_IVENT_CLCONTROLLER_THE_POINTS_ARE_ENDED"] = "All points are completed!\nWe start generating new ...",
                ["XD_IVENT_CLCONTROLLER_ENTER_PVP"] = "You <color=#ce3f27>entered</color> In the PVP zone, now other players <color=#ce3f27>can</color> do damage to you!",
                ["XD_IVENT_CLCONTROLLER_EXIT_PVP"] = "You <color=#738d43>exited</color> from the PVP zone, now other players <color=#738d43>can not</color> do damage to you!",
                ["XD_IVENT_CLCONTROLLER_NOT_NPSSPAWN"] = "You are missing the Npc Spawn plugin. Please install it to work correctly",
                ["XD_EVENT_BUILDING_NOTFOUND"] = "The construction file was not found : {0}. Move the file with the building to the folder `/oxide/data/copypaste`",

            }, this);
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
        }
        private static bool ContainsTopology(TerrainTopology.Enum mask, Vector3 position, float radius)
        {
            return (TerrainMeta.TopologyMap.GetTopology(position, radius) & (int)mask) != 0;
        }
        void Init()
        {
            Unsubscribes();
        }

        private void OnServerInitialized()
        {
            if (!CopyPaste)
            {
                NextTick(() =>
                {
                    PrintError("Check if you have the 'Copy Paste'plugin installed");
                    Interface.Oxide.UnloadPlugin(Name);
                });
                return;
            }
            else if (CopyPaste.Version < new VersionNumber(4, 1, 27))
            {
                NextTick(() =>
                {
                    PrintError("You have an old version of Copy Paste!\nplease update the plugin to the latest version (4.1.27 or higher) - https://umod.org/plugins/copy-paste");
                    Interface.Oxide.UnloadPlugin(Name);
                });
                return;
            }
            if (!NpcSpawn)
            {
                NextTick(() =>
                {
                    PrintError(GetLang("XD_IVENT_CLCONTROLLER_NOT_NPSSPAWN"));
                    Interface.Oxide.UnloadPlugin(Name);
                });
                return;
            }
            else if (NpcSpawn.Version < new VersionNumber(2, 2, 7))
            {
                NextTick(() =>
                {
                    PrintError("You have an old version of NpcSpawn!\nplease update the plugin to the latest version (2.2.7 or higher) - ReadMe.txt");
                    Interface.Oxide.UnloadPlugin(Name);
                });
                return;
            }
            _ = this;
            LoadDataCopyPaste();

            FillPatterns();
            NextTick(() =>
            {
                FindPositions = ServerMgr.Instance.StartCoroutine(GenerateSpawnPoints());
            });
            GenerateIvent();
        }
        [ChatCommand("cl")]
        void CLCommand(BasePlayer player, string cmd, string[] Args)
        {
            if (!player.IsAdmin)
            {
                return;
            }

            if (Args == null || Args.Length == 0)
            {
                SendChatPlayer(GetLang("XD_IVENT_CLCONTROLLER_SYNTAX", player.UserIDString), player);
                return;
            }
            switch (Args[0])
            {
                case "start":
                    {
                        if (SpawnHouseTime.Destroyed)
                        {
                            PrintToChat(player, GetLang("XD_IVENT_CLCONTROLLER_ACTIVE", player.UserIDString));
                        }
                        else
                        {
                            SpawnHouseTime.Destroy();
                            StartIvent();
                        }
                        break;
                    }
                case "stop":
                    {
                        if (SpawnHouseTime.Destroyed)
                        {
                            StopIvent();
                            SendChatAll(GetLang("XD_IVENT_CLCONTROLLER_STOPADMIN", player.UserIDString));
                        }
                        else
                        {

                            SendChatPlayer(GetLang("XD_IVENT_CLCONTROLLER_NOT_ACTIVE", player.UserIDString), player);
                        }
                        break;
                    }
            }
		   		 		  						  	   		  		 			   					  	  			   			
        }
        private void SendChatAll(string Message, params object[] args)
        {
            if (!string.IsNullOrEmpty(config.notiferSettings.weebHook))
            {
                string msg = GetLang(Message, null, args);
                SendDiscordMsg(GetLang(Message + "_DISCORD", null, args));
            }
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                SendChatPlayer(GetLang(Message, player.UserIDString, args), player);
            }
        }
        
                private void ClearArea(ref Vector3 pos)
        {
            List<ResourceEntity> ResourceList = new List<ResourceEntity>();
            Vis.Entities(pos, 25f, ResourceList);
            foreach (ResourceEntity item in ResourceList)
            {
                item?.KillMessage();
            }
        }
        private List<ScientistNPC> npcs = new List<ScientistNPC>();
        private BasePlayer PlayerHaked;
        void CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (container is HackableLockedCrate && container == CrateEnt)
            {
                SendChatAll("XD_IVENT_CRATELOOTPLAYER", player.displayName);
                if (config.commandReward.Commands.Count > 0)
                {
                    foreach (string command in config.commandReward.Commands)
                    {
                        Server.Command(command.Replace("%STEAMID%", $"{player.userID}"));
                    }

                    SendChatPlayer(config.commandReward.MessagePlayerReward, player);
                }
                if (RemoveHouseTime != null)
                {
                    RemoveHouseTime.Destroy();
                }

                RemoveHouseTime = timer.Once(300, () =>
                {
                    StopIvent();
                });
                Unsubscribe("CanLootEntity");
            }
        }
        
                        private List<Vector3>[] patternPositionsAboveWater = new List<Vector3>[MaxRadius];
                
        
                        List<string> hooks = new List<string>
        {
            "OnEntityTakeDamage",
            "OnCorpsePopulate",
            "CanLootEntity",
            "OnCrateHackEnd",
            "OnCrateHack",
            "CanHackCrate",
            "OnEntityMounted",
            "CanEntityTakeDamage",
            "OnEntityDeath",
            "CanPopulateLoot",
            "CanUILootSpawn",
            "CanCh47SpawnNpc",
            "OnBotReSpawnCrateDropped"
        };

        private void AddToContainerItem(ItemContainer container, Configuration.LootTableConfig lootTable)
        {
            if (lootTable.UseCount)
            {
                int count = Random.Range(lootTable.Min, lootTable.Max + 1);
                HashSet<int> indexMove = new HashSet<int>();
                while (indexMove.Count < count)
                {
                    foreach (Configuration.ItemConfig item in lootTable.Items)
                    {
                        if (indexMove.Contains(lootTable.Items.IndexOf(item)))
                        {
                            continue;
                        }

                        if (Random.Range(0.0f, 100.0f) <= item.Chance)
                        {
                            Item newItem = item.IsBluePrint ? ItemManager.CreateByName("blueprintbase") : ItemManager.CreateByName(item.ShortName, Random.Range(item.MinAmount, item.MaxAmount + 1), item.SkinID);
                            if (newItem == null)
                            {
                                PrintWarning($"Failed to create item! ({item.ShortName})");
                                continue;
                            }
                            if (item.IsBluePrint)
                            {
                                newItem.blueprintTarget = ItemManager.FindItemDefinition(item.ShortName).itemid;
                            }
		   		 		  						  	   		  		 			   					  	  			   			
                            if (!string.IsNullOrEmpty(item.Name))
                            {
                                newItem.name = item.Name;
                            }

                            if (container.capacity < container.itemList.Count + 1)
                            {
                                container.capacity++;
                            }

                            if (!newItem.MoveToContainer(container))
                            {
                                newItem.Remove();
                            }
                            else
                            {
                                indexMove.Add(lootTable.Items.IndexOf(item));
                                if (indexMove.Count == count)
                                {
                                    return;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                HashSet<int> indexMove = new HashSet<int>();
                foreach (Configuration.ItemConfig item in lootTable.Items)
                {
                    if (indexMove.Contains(lootTable.Items.IndexOf(item)))
                    {
                        continue;
                    }

                    if (Random.Range(0.0f, 100.0f) <= item.Chance)
                    {
                        Item newItem = item.IsBluePrint ? ItemManager.CreateByName("blueprintbase") : ItemManager.CreateByName(item.ShortName, Random.Range(item.MinAmount, item.MaxAmount + 1), item.SkinID);
                        if (newItem == null)
                        {
                            PrintWarning($"Failed to create item! ({item.ShortName})");
                            continue;
                        }
                        if (item.IsBluePrint)
                        {
                            newItem.blueprintTarget = ItemManager.FindItemDefinition(item.ShortName).itemid;
                        }

                        if (!string.IsNullOrEmpty(item.Name))
                        {
                            newItem.name = item.Name;
                        }
		   		 		  						  	   		  		 			   					  	  			   			
                        if (container.capacity < container.itemList.Count + 1)
                        {
                            container.capacity++;
                        }

                        if (!newItem.MoveToContainer(container))
                        {
                            newItem.Remove();
                        }
                        else
                        {
                            indexMove.Add(lootTable.Items.IndexOf(item));
                        }
                    }
                }
            }
        }

        private void FillPatterns()
        {
            Vector3[] startPositions = { new Vector3(1, 0, 1), new Vector3(-1, 0, 1), new Vector3(-1, 0, -1), new Vector3(1, 0, -1) };

            patternPositionsAboveWater[0] = new List<Vector3> { new Vector3(0, MaxElevation, 0) };
            for (int loop = 1; loop < MaxRadius; loop++)
            {
                patternPositionsAboveWater[loop] = new List<Vector3>();
		   		 		  						  	   		  		 			   					  	  			   			
                for (int step = 0; step < loop * 2; step++)
                {
                    for (int pos = 0; pos < 4; pos++)
                    {
                        Vector3 sPos = startPositions[pos] * step;
                        for (int rot = 0; rot < 3; rot++)
                        {
                            Vector3 rPos = directions[rot] * sPos;
                            rPos.y = -MaxElevation;
                            patternPositionsAboveWater[loop].Add(rPos);
                        }
                    }
                }
            }

            for (int i = 0; i < patternPositionsAboveWater.Length; i++)
            {
                patternPositionsUnderWater[i] = new List<Vector3>();
                foreach (Vector3 vPos in patternPositionsAboveWater[i])
                {
                    Vector3 rPos = new Vector3(vPos.x, MaxElevation, vPos.z);
                    patternPositionsUnderWater[i].Add(rPos);
                }
            }
        }
        void OnEntityDeath(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null)
            {
                return;
            }
            else if (npcZones != null && npcZones.playersInZone.Contains(player))
            {
                npcZones.playersInZone.Remove(player);
            }
        }

        void Unload()
        {
            StopIvent(true);
            _ = null;
        }
        private void SpawnPreset(Configuration.BuildingPasteSettings.PresetConfig preset, Vector3 pos, bool helpNpc = false)
        {
            int count = Random.Range(preset.Min, preset.Max + 1);
            JObject config = GetObjectConfig(preset.Config);
            for (int i = 0; i < count; i++)
            {
                ScientistNPC npc = (ScientistNPC)NpcSpawn.Call("SpawnNpc", RandomCircle(pos, 11, count, i), config);
                npcs.Add(npc);
            }
        }

        private static JObject GetObjectConfig(Configuration.BuildingPasteSettings.NpcConfig config)
        {
            HashSet<string> states = config.Stationary ? new HashSet<string> { "IdleState", "CombatStationaryState" } : new HashSet<string> { "RoamState", "ChaseState", "CombatState" };
            if (config.BeltItems.Any(x => x.ShortName == "rocket.launcher" || x.ShortName == "explosive.timed")) states.Add("RaidState");

            return new JObject
            {
                ["Name"] = config.Name,
                ["WearItems"] = new JArray { config.WearItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["SkinID"] = x.SkinID }) },
                ["BeltItems"] = new JArray { config.BeltItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["Amount"] = x.Amount, ["SkinID"] = x.SkinID, ["Mods"] = new JArray { x.Mods }, ["Ammo"] = string.Empty }) },
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

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        private Vector3 GetCenterFromMultiplePoints(List<Vector3> foundations)
        {
            if (foundations.Count <= 1)
            {
                return Vector3.zero;
            }
		   		 		  						  	   		  		 			   					  	  			   			
            float x = 0f;
            float z = 0f;

            foreach (Vector3 position in foundations)
            {
                x += position.x;
                z += position.z;
            }

            Vector3 vector = new Vector3(x / foundations.Count, 0f, z / foundations.Count);

            vector.y = GetSpawnHeight(vector);

            return vector;
        }

        
                [ConsoleCommand("HideUi")]
        void CMDHideUi(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (Player != null)
            {
                if (!HideUIUser.Contains(player.userID))
                {
                    HideUIUser.Add(player.userID);
                    CuiHelper.DestroyUi(player, "CobaltPanel");
                }
                else
                {
                    HideUIUser.Remove(player.userID);
                    Cui.MainUI(player);
                }
            }
        }
        private List<ulong> HideUIUser = new List<ulong>();
        private IEnumerator GenerateSpawnPoints()
        {
            int minPos = (int)(World.Size / -2f);
            int maxPos = (int)(World.Size / 2f);
            int checks = 0;

            for (float x = minPos; x < maxPos; x += 20f)
            {
                for (float z = minPos; z < maxPos; z += 20f)
                {
                    Vector3 pos = new Vector3(x, 0f, z);

                    pos.y = GetSpawnHeight(pos);

                    PosValidation(pos);

                    if (++checks >= 75)
                    {
                        checks = 0;
                        yield return CoroutineEx.waitForSeconds(0.05f);
                    }
                }
            }
            PrintWarning($"{busyPoints3D.Count} POINTS FOUND!");
            ServerMgr.Instance.StopCoroutine(FindPositions);
            FindPositions = null;
        }

                public class NpcZones : MonoBehaviour
        {
            private float Radius;
            private bool radiation;
            private TriggerRadiation rads;
            private List<BaseEntity> spheres = new List<BaseEntity>();
            internal List<BasePlayer> playersInZone = Pool.GetList<BasePlayer>();

            private void Awake()
            {
                gameObject.layer = (int)Rust.Layer.Reserved1;
                gameObject.name = "NpcZonesOrRadiation";
                enabled = false;
            }
            public void Activate(Vector3 eventPosition, float radius, bool rad)
            {
                Radius = radius;
                radiation = rad;
                transform.position = eventPosition;
                transform.rotation = new Quaternion();
                UpdateCollider();
                if (radiation)
                {
                    InitializeRadiationZone();
                }

                enabled = true;
                if (_.config.iventController.useSphere)
                {
                    CreateSphere();
                }
            }

            private void OnTriggerEnter(Collider other)
            {
                BasePlayer player = other.GetComponent<BasePlayer>();
                if (player != null && player.IsNpc == false)
                {
                    if (radiation && rads != null)
                    {
                        if (rads.entityContents == null)
                        {
                            rads.entityContents = new HashSet<BaseEntity>();
                        }
		   		 		  						  	   		  		 			   					  	  			   			
                        rads.entityContents.Add(player);
                        player.EnterTrigger(rads);
                    }
                    playersInZone.Add(player);
                    if (_.config.iventController.UseZonePVP)
                    {
                        _.SendChatPlayer(_.GetLang("XD_IVENT_CLCONTROLLER_ENTER_PVP"), player);
                    }
                }
            }

            private void OnTriggerExit(Collider other)
            {
                BasePlayer player = other.GetComponent<BasePlayer>();
                if (player != null && player.IsNpc == false)
                {
                    if (radiation && rads != null)
                    {
                        if (rads.entityContents != null)
                        {
                            rads.entityContents.Remove(player);
                        }

                        player.LeaveTrigger(rads);
                    }
                    playersInZone.Remove(player);
                    if (_.config.iventController.UseZonePVP)
                    {
                        _.SendChatPlayer(_.GetLang("XD_IVENT_CLCONTROLLER_EXIT_PVP"), player);
                    }
                }
            }
            private void OnDestroy()
            {
                foreach (BaseEntity sphere in spheres)
                {
                    if (!sphere.IsDestroyed)
                    {
                        sphere.Kill();
                    }
                }

                RemoveAllPlayers();
                Pool.FreeList(ref playersInZone);
            }

            void CreateSphere()
            {
                for (int i = 0; i < _.config.iventController.transperent; i++)
                {
                    BaseEntity sphere = GameManager.server.CreateEntity("assets/prefabs/visualization/sphere.prefab", transform.position);
                    SphereEntity entity = sphere.GetComponent<SphereEntity>();
                    entity.currentRadius = Radius * 2;
                    entity.lerpSpeed = 0f;
                    sphere.enableSaving = false;
                    sphere.Spawn();
                    spheres.Add(sphere);
                }
            }
                        private void InitializeRadiationZone()
            {
                rads = gameObject.AddComponent<TriggerRadiation>();
                rads.RadiationAmountOverride = _.config.radiationConroller.radCount;
                rads.interestLayers = LayerMask.GetMask("Player (Server)");
                rads.gameObject.SetActive(true);
            }
            private void UpdateCollider()
            {
                SphereCollider sphereCollider = gameObject.GetComponent<SphereCollider>() ?? gameObject.AddComponent<SphereCollider>();
                sphereCollider.isTrigger = true;
                sphereCollider.radius = Radius;
            }

            private void RemoveAllPlayers()
            {
                for (int i = 0; i < playersInZone.Count; i++)
                {
                    BasePlayer player = playersInZone[i];

                    if (rads != null)
                    {
                        if (rads.entityContents != null && rads.entityContents.Contains(player))
                        {
                            rads.entityContents.Remove(player);
                            player.LeaveTrigger(rads);
                        }
                    }
                }
            }
                    }
        private static bool HasBuildingPrivilege(Vector3 target, float radius)
        {
            Vector3 vector = Vector3.zero;
            List<BuildingPrivlidge> list = Pool.GetList<BuildingPrivlidge>();
            Vis.Entities(target, radius, list);
            foreach (BuildingPrivlidge tc in list)
            {
                if (tc.IsValid())
                {
                    vector = tc.transform.position;
                    break;
                }
            }
            Pool.FreeList(ref list);
            return vector == Vector3.zero;
        }
        
        void SendDiscordMsg(string msg)
        {
            List<Fields> fields = new List<Fields>
            {
                new Fields(lang.GetMessage("XD_IVENT_HOUSECOBALT", this), msg, true),
            };
            FancyMessage newMessage = new FancyMessage(null, false, new FancyMessage.Embeds[1] { new FancyMessage.Embeds(null, 16775936, fields) });
            Request(config.notiferSettings.weebHook, newMessage.toJSON());
        }
        private string GetLang(string LangKey, string userID = null, params object[] args)
        {
            sb.Clear();
            if (args != null)
            {
                sb.AppendFormat(lang.GetMessage(LangKey, this, userID), args);
                return sb.ToString();
            }
            return lang.GetMessage(LangKey, this, userID);
        }
        
                private Coroutine FindPositions;
        
                void GenerateBuilding(Configuration.BuildingPasteSettings.PasteBuild build)
        {
            string[] options = { "stability", "false", "deployables", "true", "autoheight", "false", "height", "1.5", "entityowner", "false" };
            Vector3 resultVector = (Vector3)GetSpawnPoints();
            if (resultVector == null || resultVector == Vector3.zero)
            {
                return;
            }

            object success = CopyPaste.Call("TryPasteFromVector3", new Vector3(resultVector.x, resultVector.y + build.heightBuilding, resultVector.z), 0f, !string.IsNullOrWhiteSpace(build.housepath) ? build.housepath : "HouseCobalt", options);
		   		 		  						  	   		  		 			   					  	  			   			
            if (success is string)
            {
                PrintWarning(GetLang("XD_IVENT_CLCONTROLLER_BUILDING_ERROR"));
                GenerateIvent();
                return;
            }
        }

                private bool CanSetupEntity(BaseEntity e)
        {
            BaseEntity.saveList.Remove(e);
		   		 		  						  	   		  		 			   					  	  			   			
            if (e == null || e.IsDestroyed)
            {
                HouseCobaltLab?.Remove(e);
                return false;
            }

            if (e.net == null)
            {
                e.net = Net.sv.CreateNetworkable();
            }

            e.enableSaving = false;
            return true;
        }

        private void Request(string url, string payload, Action<int> callback = null)
        {
            Dictionary<string, string> header = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" }
            };
            webrequest.Enqueue(url, payload, (code, response) =>
            {
                if (code != 200 && code != 204)
                {
                    if (response != null)
                    {
                        try
                        {
                            JObject json = JObject.Parse(response);
                            if (code == 429)
                            {
                                float seconds = float.Parse(Math.Ceiling((double)(int)json["retry_after"] / 1000).ToString());
                            }
                            else
                            {
                                PrintWarning($" Discord rejected that payload! Responded with \"{json["message"]}\" Code: {code}");
                            }
                        }
                        catch
                        {
                            PrintWarning($"Failed to get a valid response from discord! Error: \"{response}\" Code: {code}");
                        }
                    }
                    else
                    {
                        PrintWarning($"Discord didn't respond (down?) Code: {code}");
                    }
                }
                try
                {
                    callback?.Invoke(code);
                }
                catch (Exception) { }

            }, this, RequestMethod.POST, header);
        }
        private HashSet<Vector3> busyPoints3D = new HashSet<Vector3>();
        private NpcZones npcZones = null;
        private static XDCobaltLaboratory _;
        void OnCrateHackEnd(HackableLockedCrate crate)
        {
            if (crate == CrateEnt)
            {
                if (RemoveHouseTime != null)
                {
                    RemoveHouseTime.Destroy();
                }

                SendChatAll("XD_IVENT_CRATEHACKEND", (config.iventController.timeRemoveHouse2 / 60));
                RemoveHouseTime = timer.Once(config.iventController.timeRemoveHouse2, () =>
                {
                    SendChatAll("XD_IVENT_CRATELOOTFOUND");
                    StopIvent();
                });
            }
        }
        private Timer SpawnHouseTime;

        
                object CanCh47SpawnNpc(HackableLockedCrate crate)
        {
            if (crate == CrateEnt)
            {
                return true;
            }
            else
            {
                return null;
            }
        }
        private const int MaxRadius = 7;
                
                object CanEntityTakeDamage(BasePlayer victim, HitInfo hitinfo)
        {
            if (!config.iventController.UseZonePVP || victim == null || hitinfo == null || npcZones == null)
            {
                return null;
            }

            BasePlayer attacker = hitinfo.InitiatorPlayer;
            if (npcZones.playersInZone.Contains(victim) && (attacker == null || (attacker != null && npcZones.playersInZone.Contains(attacker))))
            {
                return true;
            }
            else
            {
                return null;
            }
        }

        public bool IsFlat(ref Vector3 position)
        {
            List<Vector3>[] AboveWater = new List<Vector3>[MaxRadius];

            Array.Copy(patternPositionsAboveWater, AboveWater, patternPositionsAboveWater.Length);

            for (int i = 0; i < AboveWater.Length; i++)
            {
                for (int j = 0; j < AboveWater[i].Count; j++)
                {
                    Vector3 pPos = AboveWater[i][j];
                    Vector3 resultAbovePos = new Vector3(pPos.x + position.x, position.y + MaxElevation, pPos.z + position.z);
                    Vector3 resultUnderPos = new Vector3(pPos.x + position.x, position.y - MaxElevation, pPos.z + position.z);

                    if (resultAbovePos.y >= TerrainMeta.HeightMap.GetHeight(resultAbovePos) && resultUnderPos.y <= TerrainMeta.HeightMap.GetHeight(resultUnderPos))
                    {
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        
                private void GenerateIvent()
        {
            if (RemoveHouseTime != null)
            {
                RemoveHouseTime.Destroy();
            }

            if (SpawnHouseTime != null)
            {
                SpawnHouseTime.Destroy();
            }

            SpawnHouseTime = timer.Once(Random.Range(config.iventController.minSpawnIvent, config.iventController.maxSpawnIvent), () =>
            {
                StartIvent();
            });
        }
        private IEnumerator SetupBuilding()
        {
            BaseEntity box = null;
            List<CCTV_RC> cam = new List<CCTV_RC>();
            List<Vector3> foundations = new List<Vector3>();
            ComputerStation comp = null;
            foreach (BaseEntity ent in HouseCobaltLab.ToArray())
            {
                if (!CanSetupEntity(ent))
                {
                    yield return CoroutineEx.waitForSeconds(0.01f);
                    continue;
                }

                if (ent is BaseCombatEntity)
                {
                    (ent as BaseCombatEntity).pickup.enabled = false;
                }
                if (ent is MiniCopter)
                {
                    if (Random.Range(0, 100) >= (100 - labIndex.copterChance))
                    {
                        MiniCopter copter = (ent as MiniCopter);
                        copter.GetFuelSystem().AddStartingFuel(50);
                        copter.transform.position = new Vector3(copter.transform.position.x, copter.transform.position.y, copter.transform.position.z);
                    }
                    else
                    {
                        NextTick(() =>
                        {
                            HouseCobaltLab.Remove(ent);
                            ent.Kill();
                        });
                    }
		   		 		  						  	   		  		 			   					  	  			   			
                    continue;
                }
                ent.OwnerID = 342968945867;
                if (Light.Contains(ent.prefabID))
                {
                    ent.SetFlag(BaseEntity.Flags.On, true);
                }

                if (ent.skinID == 22848)
                {
                    BaseEntity CrateEnt = GameManager.server.CreateEntity("assets/prefabs/io/electric/lights/sirenlightorange.prefab", ent.transform.position, ent.transform.rotation, true);
                    CrateEnt.Spawn();
                    if (CrateEnt is IOEntity)
                    {
                        CrateEnt.SetFlag(BaseEntity.Flags.Reserved8, true);
                        CrateEnt.SetFlag(BaseEntity.Flags.On, true);
                    }
                    NextTick(() =>
                    {
                        HouseCobaltLab.Remove(ent);
                        ent.Kill();
                        HouseCobaltLab.Add(CrateEnt);

                    });
                }
                if (ent is Signage)
                {
                    Signage ents = ent as Signage;
                    if (ents == null)
                    {
                        continue;
                    }

                    ents?.SetFlag(BaseEntity.Flags.Locked, true);
                    ents?.SetFlag(BaseEntity.Flags.Busy, true);
                    ents.SendNetworkUpdate(global::BasePlayer.NetworkQueue.Update);
                }
                if (ent is Workbench || ent is ResearchTable || ent is MixingTable || ent is BaseArcadeMachine
                || ent is IOEntity || ent is ComputerStation || ent is CCTV_RC)
                {
                    if (ent is IOEntity)
                    {
                        ent.SetFlag(BaseEntity.Flags.Reserved8, true);
                        ent.SetFlag(BaseEntity.Flags.On, true);
                    }
                    if (ent is ComputerStation)
                    {
                        comp = ent as ComputerStation;
                    }

                    if (ent is CCTV_RC)
                    {
                        CCTV_RC cams = ent as CCTV_RC;
                        cams.UpdateIdentifier("Cobalt" + cam.Count + 1);
                        cam.Add(cams);
                    }
                    continue;
                }
                if (ent is VendingMachine)
                {
                    VendingMachine ents = ent as VendingMachine;
                    if (ents == null)
                    {
                        continue;
                    }

                    ents.SetFlag(BaseEntity.Flags.Reserved4, false);
                    ents.UpdateMapMarker();
                }
                if (ent is FogMachine)
                {
                    FogMachine ents = ent as FogMachine;
                    if (ents == null)
                    {
                        continue;
                    }

                    ents.SetFlag(BaseEntity.Flags.Reserved8, true);
                    ents.SetFlag(BaseEntity.Flags.Reserved7, false);
                    ents.SetFlag(BaseEntity.Flags.Reserved6, false);
                }
                if (ent.prefabID == 2206646561)
                {
                    box = CrateHackableLocked(ent);
                    NextTick(() =>
                    {
                        HouseCobaltLab.Remove(ent);
                        ent.Kill();
                        HouseCobaltLab.Add(box);
                    });
                    continue;
                }
                if (ent is Door)
                {
                    Door ents = ent as Door;
                    if (ents == null)
                    {
                        continue;
                    }

                    ents.pickup.enabled = false;
                    ents.canTakeLock = false;
                    ents.canTakeCloser = false;
                    continue;
                }
                if (ent is ElectricGenerator)
                {
                    (ent as ElectricGenerator).electricAmount = 400;
                }
                if (ent as BuildingBlock)
                {
                    BuildingBlock build = ent as BuildingBlock;

                    if (build.prefabID == 3234260181 || build.prefabID == 72949757 || build.prefabID == 2925153068 || build.prefabID == 916411076)
                    {
                        foundations.Add(build.transform.position);
                    }

                    build.StopBeingDemolishable();
                    build.StopBeingRotatable();
                }
                DecayEntity decayEntety = ent as DecayEntity;
                if (decayEntety != null)
                {
                    decayEntety.decay = null;
                    decayEntety.decayVariance = 0;
                    decayEntety.ResetUpkeepTime();
                    decayEntety.DecayTouch();
                }
                ent?.SetFlag(BaseEntity.Flags.Busy, true);
                ent?.SetFlag(BaseEntity.Flags.Locked, true);
                yield return CoroutineEx.waitForSeconds(0.02f);
            }
            if (comp != null && cam.Count > 0)
            {
                foreach (CCTV_RC sd in cam)
                {
                    comp.controlBookmarks.Add(sd.GetIdentifier());
                }
            }
            Vector3 vector = GetCenterFromMultiplePoints(foundations);
            if (vector == Vector3.zero)
            {
                PrintError("ERROR3");
                StopIvent();
                GenerateIvent();
                setupBuildingRoutine = null;
                yield break;
            }
            if (box == null)
            {
                PrintError(GetLang("XD_IVENT_CLCONTROLLER_NOT_BUILDING_BOX"));
                StopIvent();
                GenerateIvent();
                setupBuildingRoutine = null;
                yield break;
            }
            npcZones = new GameObject().AddComponent<NpcZones>();
            npcZones.Activate(vector, config.radiationConroller.radRadius, config.radiationConroller.radUse);
            foreach (Configuration.BuildingPasteSettings.PresetConfig preset in labIndex.NpcPressets)
            {
                SpawnPreset(preset, vector);
            }

            yield return CoroutineEx.waitForSeconds(0.02f);
            PosIvent = GetGridString(vector);
            GenerateMapMarker(vector);
            SendChatAll("XD_IVENT_START", PosIvent);
            if (config.notiferSettings.useUiNotifi)
            {
                Cui.CreateUIAllPlayer();
            }

            RemoveHouseTime = timer.Once(config.iventController.timeRemoveHouse, () =>
            {
                SendChatAll("XD_IVENT_NOPLAYER");
                StopIvent();
            });
            setupBuildingRoutine = null;
        }
        private List<Vector3>[] patternPositionsUnderWater = new List<Vector3>[MaxRadius];
        private string PosIvent;
        private object CanPopulateLoot(ScientistNPC entity, NPCPlayerCorpse corpse)
        {
            if (entity == null || corpse == null || labIndex == null)
            {
                return null;
            }

            if (npcs.Contains(entity))
            {
                Configuration.BuildingPasteSettings.PresetConfig preset = labIndex.NpcPressets.FirstOrDefault(x => x.Config.Name == entity.displayName);
                if (preset.TypeLootTable == 2)
                {
                    return null;
                }
                else
                {
                    return true;
                }
            }
            return null;
        }
        [ChatCommand("cl.botitems")]
        void NpcLootCommand(BasePlayer player, string cmd, string[] Args)
        {
            if (Args == null || Args.Length < 2)
            {
                SendChatPlayer(GetLang("XD_IVENT_CLBOT_ITEM_SYNTAX", player.UserIDString), player);
                return;
            }
            switch (Args[0])
            {
                case "add":
                    {
                        foreach (Configuration.BuildingPasteSettings.PasteBuild preset in config.pasteSettings.pasteBuilds)
                        {
                            Configuration.BuildingPasteSettings.PresetConfig presets = preset.NpcPressets.FirstOrDefault(x => x.Config.Name == Args[1]);
                            if (presets != null)
                            {
                                foreach (Item item in player.inventory.containerMain.itemList)
                                {
                                    presets.OwnLootTable.Items.Add(new Configuration.ItemConfig
                                    {
                                        IsBluePrint = item.IsBlueprint(),
                                        ShortName = item.IsBlueprint() ? item.blueprintTargetDef.shortname : item.info.shortname,
                                        SkinID = item.skin,
                                        Name = string.Empty,
                                        Chance = 30,
                                        MinAmount = 1,
                                        MaxAmount = 1,
                                        lootWipePlus = false
                                    });
                                }
                                SendChatPlayer(GetLang("XD_IVENT_CLBOT_ITEM_ADDED", player.UserIDString), player);
                                SaveConfig();
                            }
                            else
                            {
                                SendChatPlayer(GetLang("XD_IVENT_CLBOT_ITEM__PRESET_NOTFOUND", player.UserIDString, Args[1]), player);
                            }
                        }
                        break;
                    }
                case "reset":
                    {
                        foreach (Configuration.BuildingPasteSettings.PasteBuild preset in config.pasteSettings.pasteBuilds)
                        {
                            Configuration.BuildingPasteSettings.PresetConfig presets = preset.NpcPressets.FirstOrDefault(x => x.Config.Name == Args[1]);
                            if (presets != null)
                            {
                                presets.OwnLootTable.Items.Clear();
                                foreach (Item item in player.inventory.containerMain.itemList)
                                {
                                    presets.OwnLootTable.Items.Add(new Configuration.ItemConfig
                                    {
                                        IsBluePrint = item.IsBlueprint(),
                                        ShortName = item.IsBlueprint() ? item.blueprintTargetDef.shortname : item.info.shortname,
                                        SkinID = item.skin,
                                        Name = string.Empty,
                                        Chance = 30,
                                        MinAmount = 1,
                                        MaxAmount = 1,
                                        lootWipePlus = false
                                    });
                                }
                                SendChatPlayer(GetLang("XD_IVENT_CLBOT_ITEM_REPLACED", player.UserIDString), player);
                                SaveConfig();
                            }
                            else
                            {
                                SendChatPlayer(GetLang("XD_IVENT_CLBOT_ITEM__PRESET_NOTFOUND", player.UserIDString, Args[1]), player);
                            }
                        }
                        break;
                    }
            }
        }
        private List<uint> Light = new List<uint> { 3887352222, 1889323056, 630866573, 172822834, 4027991414, 1392608348, 1748062128 };
        object OnBotReSpawnCrateDropped(HackableLockedCrate crate)
        {
            if (crate == CrateEnt)
            {
                return true;
            }
            else
            {
                return null;
            }
        }

        private void RemoveMapMarker()
        {
            if (config.mapMarkers.MapUse)
            {
                Interface.CallHook("API_RemoveMarker", "xdcobaltlab");
            }
        }

        void OnPasteFinished(List<BaseEntity> pastedEntities, string fileName)
        {
            if (fileName != "HouseCobalt" && !config.pasteSettings.pasteBuilds.Any(x => x.housepath == fileName))
            {
                return;
            }
		   		 		  						  	   		  		 			   					  	  			   			
            try
            {
                HouseCobaltLab.AddRange(pastedEntities);
                setupBuildingRoutine = ServerMgr.Instance.StartCoroutine(SetupBuilding());
            }
            catch (Exception ex)
            {
                PrintError(ex.InnerException.Message);
                StopIvent();
                GenerateIvent();
                setupBuildingRoutine = null;
            }
        }
        private bool IsAlive(BaseNetworkable entity)
        {
            return entity != null && !entity.IsDestroyed;
        }
        private readonly List<string> assets = new List<string>
        {
            "/props/", "/structures/", "/building/", "train_", "powerline_", "dune", "candy-cane", "assets/content/nature/", "walkway", "invisible_collider"
        };
        private void PosValidation(Vector3 pos)
        {
            if (!IsFlat(ref pos))
            {
                return;
            }

            if (TerrainMeta.WaterMap.GetHeight(pos) - TerrainMeta.HeightMap.GetHeight(pos) > 0.5f)
            {
                return;
            }

            if (!Is3DPointValid(pos, 1 << 8 | 1 << 16 | 1 << 18))
            {
                return;
            }

            if (!config.spawnPositionGenerateSetting.spawnOnRiver && ContainsTopology(TerrainTopology.Enum.River | TerrainTopology.Enum.Riverside, pos, 12.5f))
            {
                return;
            }

            if (!config.spawnPositionGenerateSetting.spawnOnRoad && ContainsTopology(TerrainTopology.Enum.Road | TerrainTopology.Enum.Roadside, pos, 12.5f))
            {
                return;
            }
            
            if (ContainsTopology(TerrainTopology.Enum.Rail | TerrainTopology.Enum.Railside, pos, 12.5f))
            {
                return;
            }
		   		 		  						  	   		  		 			   					  	  			   			
            if (ContainsTopology(TerrainTopology.Enum.Monument, pos, config.spawnPositionGenerateSetting.monumentFindRadius))
            {
                return;
            }

            if (ContainsTopology(TerrainTopology.Enum.Building, pos, 25f))
            {
                return;
            }

            if (pos != Vector3.zero)
            {
                AcceptValue(ref pos);
            }
        }
        
                public static class Cui
        {
            public static void CreateUIAllPlayer()
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    ButtonClose(player);
                    if (_.HideUIUser.Contains(player.userID))
                    {
                        continue;
                    }

                    MainUI(player);
                    if (_.config.notiferSettings.hideUiNotifi)
                    {
                        _.timer.Once(_.config.notiferSettings.hideUiNotifiTime, () => { player?.SendConsoleCommand("HideUi"); });
                    }
                }
            }

            public static void MainUI(BasePlayer player)
            {
                CuiElementContainer container = new CuiElementContainer
                {
                    {
                        new CuiPanel
                        {
                            CursorEnabled = false,
                            Image = { Color = _.config.notiferSettings.colorBackground, FadeIn = 0.2f },
                            RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-342.195 -15.973", OffsetMax = "-13.805 59.667" }
                        },
                        "Overlay",
                        "CobaltPanel"
                    },

                    new CuiElement
                    {
                        Name = "CobaltImg",
                        Parent = "CobaltPanel",
                        Components = {
                    new CuiRawImageComponent { Color = "0.95686 0.7254 0 1", Material = "assets/icons/iconmaterial.mat", Sprite = "assets/icons/radiation.png", FadeIn = 0.2f },
                    new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "6.5 -17.5", OffsetMax = "41.5 17.5" }
                }
                    },

                    new CuiElement
                    {
                        Name = "CobaltTitle",
                        Parent = "CobaltPanel",
                        Components = {
                    new CuiTextComponent { Text =  _.GetLang("XD_IVENT_HOUSECOBALT", player.UserIDString).ToUpper(), Font = "robotocondensed-bold.ttf", FontSize = 15, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", FadeIn = 0.2f },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "0.5 0.5" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = "-103.801 -23.938", OffsetMax = "103.801 -2.861" }
                }
                    },

                    new CuiElement
                    {
                        Name = "CobaltInfo",
                        Parent = "CobaltPanel",
                        Components = {
                    new CuiTextComponent { Text =_.GetLang("XD_IVENT_STARTUI", player.UserIDString, _.PosIvent), Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", FadeIn = 0.2f },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-118.84 -33.077", OffsetMax = "151.44 13.881" }
                }
                    }
                };
                CuiHelper.AddUi(player, CuiHelper.ToJson(container));
            }

            public static void ButtonClose(BasePlayer player)
            {
                CuiElementContainer container = new CuiElementContainer
                {
                    {
                        new CuiPanel
                        {
                            CursorEnabled = false,
                            Image = { Color = _.config.notiferSettings.colorBtnCloseUi },
                            RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-13.854 -15.973", OffsetMax = "0 59.667" }
                        },
                        "Overlay",
                        "CobaltClosePanel"
                    },

                    new CuiElement
                    {
                        Name = "ButtonClodedUI",
                        Parent = "CobaltClosePanel",
                        Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Material = "assets/icons/iconmaterial.mat", Sprite = "assets/icons/chevron_right.png" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-8.739 -11.998", OffsetMax = "8.74 11.998" }
                }
                    },

                    {
                        new CuiButton
                        {
                            Text = { Text = "" },
                            Button = { Command = "HideUi", Color = "0 0 0 0" },
                            RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-8.739 -37.82", OffsetMax = "8.74 37.82" }
                        },
                        "CobaltClosePanel",
                        "Closed"
                    }
                };

                CuiHelper.AddUi(player, CuiHelper.ToJson(container));
            }

            public static void DestroyAllPlayer()
            {
                CommunityEntity.ServerInstance.ClientRPCEx(new SendInfo(Net.sv.connections), null, "DestroyUI", "CobaltClosePanel");
                CommunityEntity.ServerInstance.ClientRPCEx(new SendInfo(Net.sv.connections), null, "DestroyUI", "CobaltPanel");
            }
        }

        private void StopIvent(bool unload = false)
        {
            labIndex = null;
            if (unload && FindPositions != null)
            {
                ServerMgr.Instance.StopCoroutine(FindPositions);
                FindPositions = null;
            }
            if (setupBuildingRoutine != null)
            {
                ServerMgr.Instance.StopCoroutine(setupBuildingRoutine);
                setupBuildingRoutine = null;
            }
            foreach (BaseEntity iventEnt in HouseCobaltLab)
            {
                if (!iventEnt.IsDestroyed)
                {
                    iventEnt?.Kill();
                }
            }

            foreach (ScientistNPC npc in npcs)
            {
                if (IsAlive(npc))
                {
                    npc.Kill();
                }
            }

            if (config.notiferSettings.useUiNotifi)
            {
                Cui.DestroyAllPlayer();
            }

            if (SpawnHouseTime != null)
            {
                SpawnHouseTime.Destroy();
            }

            if (RemoveHouseTime != null)
            {
                RemoveHouseTime.Destroy();
            }

            DestroyZone();
            Unsubscribes();
            RemoveMapMarker();
            HideUIUser.Clear();
            HouseCobaltLab.Clear();
            if (!unload)
            {
                GenerateIvent();
            }
        }

        private void DestroyZone()
        {
            if (npcZones != null)
            {
                UnityEngine.Object.Destroy(npcZones.gameObject);
            }
        }

        public static StringBuilder sb = new StringBuilder();
        private Configuration.BuildingPasteSettings.PasteBuild labIndex = null;

        private BaseEntity CrateHackableLocked(BaseEntity box)
        {
            CrateEnt = GameManager.server.CreateEntity("assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab", new Vector3(box.transform.position.x, box.transform.position.y + 1f, box.transform.position.z), box.transform.rotation, true) as HackableLockedCrate;
            CrateEnt.enableSaving = false;
            CrateEnt.Spawn();
            CrateEnt.hackSeconds = HackableLockedCrate.requiredHackSeconds - config.boxSetting.UnlockTime;
            if (config.boxSetting.TypeLootTable == 1)
            {
                NextTick(() =>
                {
                    for (int i = CrateEnt.inventory.itemList.Count - 1; i >= 0; i--)
                    {
                        Item item = CrateEnt.inventory.itemList[i];
                        item.RemoveFromContainer();
                        item.Remove();
                    }
                    AddToContainerItem(CrateEnt.inventory, config.boxSetting.OwnLootTable);
                });
            }

            return CrateEnt;
        }
        private float GetRockHeight(Vector3 a)
        {
            RaycastHit hit;
            if (Physics.Raycast(a + new Vector3(0f, 50f, 0f), Vector3.down, out hit, a.y + 51f, Layers.Mask.World, QueryTriggerInteraction.Ignore))
            {
                return Mathf.Abs(hit.point.y - a.y);
            }
            return 0f;
        }
                [PluginReference] Plugin CopyPaste, IQChat, NpcSpawn;
        private void CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            if (crate == CrateEnt)
            {
                PlayerHaked = player;
            }
        }

        private void AcceptValue(ref Vector3 point)
        {
            busyPoints3D.Add(point);
        }

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (hitInfo == null)
            {
                return;
            }

            if (entity?.OwnerID == 342968945867)
            {
                hitInfo.damageTypes.ScaleAll(0);
            }
        }
        private Coroutine setupBuildingRoutine { get; set; } = null;
        private Timer RemoveHouseTime;

        private static float GetSpawnHeight(Vector3 target, bool flag = true)
        {
            float y = TerrainMeta.HeightMap.GetHeight(target);
            float w = TerrainMeta.WaterMap.GetHeight(target);
            float p = TerrainMeta.HighestPoint.y + 250f;
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(target.x, p, target.z), Vector3.down, out hit, target.y + p, Layers.Mask.World | Layers.Mask.Terrain, QueryTriggerInteraction.Ignore))
            {
                y = Mathf.Max(y, hit.point.y);
            }
		   		 		  						  	   		  		 			   					  	  			   			
            return flag ? Mathf.Max(y, w) : y;
        }
        
                [ChatCommand("cl.items")]
        void BoxItemCommand(BasePlayer player, string cmd, string[] Args)
        {
            if (Args == null || Args.Length == 0)
            {
                SendChatPlayer(GetLang("XD_IVENT_CL_ITEM_SYNTAX", player.UserIDString), player);
                return;
            }
            switch (Args[0])
            {
                case "add":
                    {
                        foreach (Item item in player.inventory.containerMain.itemList)
                        {
                            config.boxSetting.OwnLootTable.Items.Add(new Configuration.ItemConfig
                            {
                                IsBluePrint = item.IsBlueprint(),
                                ShortName = item.IsBlueprint() ? item.blueprintTargetDef.shortname : item.info.shortname,
                                SkinID = item.skin,
                                Name = string.Empty,
                                Chance = 30,
                                MinAmount = 1,
                                MaxAmount = 1,
                                lootWipePlus = false
                            });
                        }
                        SaveConfig();
                        SendChatPlayer(GetLang("XD_IVENT_CL_ITEM_ADDED", player.UserIDString), player);
                        break;
                    }
                case "reset":
                    {
                        config.boxSetting.OwnLootTable.Items.Clear();
                        foreach (Item item in player.inventory.containerMain.itemList)
                        {
                            config.boxSetting.OwnLootTable.Items.Add(new Configuration.ItemConfig
                            {
                                IsBluePrint = item.IsBlueprint(),
                                ShortName = item.IsBlueprint() ? item.blueprintTargetDef.shortname : item.info.shortname,
                                SkinID = item.skin,
                                Name = string.Empty,
                                Chance = 30,
                                MinAmount = 1,
                                MaxAmount = 1,
                                lootWipePlus = false
                            });
                        }
                        SaveConfig();
                        SendChatPlayer(GetLang("XD_IVENT_CL_ITEM_REPLACED", player.UserIDString), player);
                        break;
                    }
            }
        }
        void OnEntityMounted(BaseMountable entity, BasePlayer player)
        {
            if (HouseCobaltLab.Contains(entity?.GetParentEntity()))
            {
                HouseCobaltLab.Remove(entity.GetParentEntity());
            }
        }
        private void OnCrateHack(HackableLockedCrate crate)
        {
            bool helpSpawn = false;
            if (crate == CrateEnt)
            {
                if (RemoveHouseTime != null)
                {
                    RemoveHouseTime.Destroy();
                }

                if (config.boxSetting.signaling)
                {
                    BaseEntity Alarm = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/alarms/audioalarm.prefab", crate.transform.position, default(Quaternion), true);
                    Alarm.Spawn();
                    Alarm.SetFlag(BaseEntity.Flags.Reserved8, true);
                    Alarm.gameObject.Identity();
                    Alarm.SetParent(crate);

                    BaseEntity Light = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/lights/sirenlight/electric.sirenlight.deployed.prefab", crate.transform.position, Quaternion.identity, false);
                    Light.enableSaving = true;
                    Light.Spawn();
                    Light.SetParent(crate);
                    Light.transform.localPosition = new Vector3(0.4f, 1.45f, -0.3f);
                    Light.transform.hasChanged = true;
                    Light.SendNetworkUpdate();

                    Light.SetFlag(BaseEntity.Flags.Reserved8, true);
                }
                foreach (Configuration.BuildingPasteSettings.PresetConfig presset in labIndex.NpcPressets)
                {
                    if (presset.helpBot && presset.helpCount > 0)
                    {
                        SpawnPreset(presset, crate.transform.position, true);
                        helpSpawn = true;
                    }
                }
                SendChatAll(helpSpawn ? "XD_IVENT_CRATEHACKHELP" : "XD_IVENT_CRATEHACK", PlayerHaked?.displayName, PosIvent);
                Interface.CallHook("OnHackCobaltCrate", PlayerHaked);
            }
        }
            }
}
