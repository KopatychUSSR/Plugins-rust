using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("Heavy Rig", "NooBlet", "1.3.9")]
    [Description("Spawns a Heavy Oirig Event")]
    public class HeavyRig : RustPlugin
    {
        #region Vars

        readonly string crate = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab";
        public HeavyRig _plugin;
        Timer CrateCheckTimer;
        public bool EventActive = false;
        public bool hackcycleactive = false;
        BaseEntity largeReader = null;
        BaseEntity smallReader = null;
        private Configuration _config;
        public static string _cardName;
        public List<HackableLockedCrate> spawnedCrates = new List<HackableLockedCrate>();
        public Dictionary<Vector3, float> LargeCorrections = new Dictionary<Vector3, float>
        {
              { new Vector3(2.5f, 37f, 1f), 0f },
              { new Vector3(12f, 37f, 1f), -90f },
              { new Vector3(16f, 37f, 12f), -90f },
              { new Vector3(16f, 42f, 12f), -90f }
        };
        public Dictionary<Vector3, float> SmallCorrections = new Dictionary<Vector3, float>
        {
              { new Vector3(14f, 28f,5f), 180f },
              { new Vector3(18.8f, 27.2f,1.5f), -90f },
        };
        public Dictionary<Vector3, float> SmallReaderCorrection = new Dictionary<Vector3, float>
        {
              { new Vector3(24f, 28.7f, -10.78f), 0f },
        };
        public Dictionary<Vector3, float> LargeReaderCorrection = new Dictionary<Vector3, float>
        {
              { new Vector3(-14.5f, 39f-1.35f,5.85f), 180f },
        };

        #endregion Vars

        #region Hooks

        void OnServerInitialized(bool initial)
        {
            _cardName = _config.CardName;
            _plugin = this;
            largeReader = SetReader(SpawnCratePos(LargeReaderCorrection.FirstOrDefault().Key, "Large Oil Rig",null).pos, SpawnCratePos(LargeReaderCorrection.FirstOrDefault().Key, "Large Oil Rig",null).rot);
            smallReader = SetReader(SpawnCratePos(SmallReaderCorrection.FirstOrDefault().Key, "Oil Rig", null).pos, SpawnCratePos(SmallReaderCorrection.FirstOrDefault().Key, "Oil Rig",null).rot);
        }
        void Unload()
        {
            largeReader.Kill();
            smallReader.Kill();
            KillButtons();
            int nCrateCount = spawnedCrates.Count;
            if (nCrateCount > 0)
            {
                for (int n = nCrateCount; n > 0; n--)
                {
                    var c = spawnedCrates[n - 1];
                    if (c != null)
                    {                        
                        c.Kill();                        
                    }
                }

            }
        }

        object CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            if (crate.OwnerID == 0304 || crate._name == "0304") { return false; }           
            if (!EventActive) { return null; }
            else
            {
                TerrainMeta.Path.Monuments.ForEach(monument =>
                {
                    if (monument == null) return;
                    if (Vector3.Distance(crate.transform.position, monument.transform.position) < 100)
                    {
                        if (monument.displayPhrase.english.Contains("Oil Rig"))
                        {
                            if (monument.displayPhrase.english.StartsWith("Large"))
                            {
                                if (EventActive) { StartHackcycle(spawnedCrates);MonitorCrate(crate, monument); }
                                // Puts("Large Hacked");
                            }
                            else
                            {
                                if (EventActive) { StartHackcycle(spawnedCrates); MonitorCrate(crate, monument); }                               
                                // Puts("Small Hacked");
                            }
                            hackcycleactive = true;
                        }
                    }
                });
            }
            return null;
        }

        void HeavyOilRigWaveEventStarted()
        {
            Puts("HeavyOilRigWaveEventStarted");
        }
        void HeavyOilRigWaveEventStopped()
        {
            Puts("HeavyOilRigWaveEventStopped");
        }
       

        void OnButtonPress(PressButton button, BasePlayer player)
        {
            if (button._name == "9999")
            {
                var oilrig = getOilrigName(player);
                var card = GetPlayerCard(player);
                if (card != null)
                {
                    if (EventActive) { player.ChatMessage(GetLang("EventActiveMessage", player)); return ; }                  
                    if (!CrateReady(player.transform.position)) { player.ChatMessage(GetLang("CrateNotReady", player)); return; }
                    card.UseItem(1);
                    Activateevent(GetRig(player.transform.position),player);
                    BroadcastEvent(player, GetRig(player.transform.position));
                    Interface.CallHook("HeavyOilRigWaveEventStarted");

                    timer.Once(300f, () =>
                    {
                        int nCrateCount = spawnedCrates.Count;
                        if (nCrateCount > 0)
                        {
                            for (int n = nCrateCount; n > 0; n--)
                            {
                                var c = spawnedCrates[n - 1];
                                if (!c.IsBeingHacked()&&!hackcycleactive)
                                {
                                    c.Kill();
                                }
                            }
                        }
                        Interface.CallHook("HeavyOilRigWaveEventStopped");
                        EventActive = false;
                        hackcycleactive = false;
                    });
                    return ;
                }
                else
                {
                    player.ChatMessage("You dont seem to have a card!!!");
                }              
            }
         
            return ;
        }

        private Item GetPlayerCard(BasePlayer player)
        {
            foreach (var b in player.inventory.containerBelt.itemList)
            {
                if (b.info.shortname == "keycard_red" && b.skin == 1988408422)
                {                    
                    return b;
                }
            }
            foreach (var m in player.inventory.containerMain.itemList)
            {
                if (m.info.shortname == "keycard_red" && m.skin == 1988408422)
                {
                    return m;
                }
            }
            return null;
        }

        void OnCardSwipe(CardReader cardReader, Keycard card, BasePlayer player)
        {
            if (card.skinID == 1988408422 && GetRig(player.transform.position).Contains("") && cardReader.accessLevel == 3)
            {
                if (EventActive) { player.ChatMessage(GetLang("EventActiveMessage", player)); return; }
                if (!CrateReady(player.transform.position)) { player.ChatMessage(GetLang("CrateNotReady", player)); return; }
               
                timer.Once(1f, () =>
                {
                    var c1 = GetPlayerCard(player);
                    c1.UseItem(1);
                });
                Activateevent(GetRig(player.transform.position), player);
                BroadcastEvent(player, GetRig(player.transform.position));
                Interface.CallHook("HeavyOilRigWaveEventStarted");

                timer.Once(300f, () =>
                {
                    int nCrateCount = spawnedCrates.Count;
                    if (nCrateCount > 0)
                    {
                        for (int n = nCrateCount; n > 0; n--)
                        {
                            var c = spawnedCrates[n - 1];
                            if (!c.IsBeingHacked() && !hackcycleactive)
                            {
                                c.Kill();
                            }
                        }
                    }
                    Interface.CallHook("HeavyOilRigWaveEventStopped");
                    EventActive = false;
                    hackcycleactive = false;
                });
            }            
        }

        private void OnLootSpawn(LootContainer container)
        {
            if (container == null || !_config.EnableSpawn) return;

            var customItem = _config.Drop.Find(x => x.ShortPrefabName.Contains(container.ShortPrefabName));
            if (customItem == null || !(Random.Range(0f, 100f) <= customItem.DropChance)) return;

            timer.In(0.21f, () =>
            {
                if (container.inventory == null) return;

                var count = Random.Range(customItem.MinAmount, customItem.MaxAmount + 1);

                if (container.inventory.capacity <= container.inventory.itemList.Count)
                    container.inventory.capacity = container.inventory.itemList.Count + count;

                for (var i = 0; i < count; i++)
                {
                    var item = Item?.ToItem();
                    if (item == null) break;

                    item.MoveToContainer(container.inventory);
                }
            });
        }

        object OnEntityKill(HackableLockedCrate entity)
        {
            if (spawnedCrates.Contains(entity)) { spawnedCrates.Remove(entity); }
            return null;
        }

        #endregion Hooks

        #region Methods

        private void KillButtons()
        {
            var oilRigMonuments = TerrainMeta.Path.Monuments.Where(monument => monument.displayPhrase.english.Contains("Oil Rig"));
            foreach (var monument in oilRigMonuments)
            {
                if (monument == null) return;
                var buttons = Pool.Get<List<PressButton>>();
                Vis.Entities(monument.transform.position, 200f, buttons);

                foreach (var b in buttons)
                {
                    if (b._name == "9999")
                    {
                        Puts("killing button");
                        b.Kill();
                    }
                }
                Pool.FreeUnmanaged(ref buttons);
            }               
        }
        private void FindCards()
        {
            List<Keycard> keycards = new List<Keycard>();
            foreach(var c in Keycard.serverEntities)
            {
                var card = c as Keycard;                
                if(card != null && card.accessLevel == 3 && card.skinID == 1988408422)
                {
                    Puts($"card Found : {card.GetOwnerItemDefinition().displayName.english}");
                    keycards.Add(card);
                }
            }
            //foreach(var c in keycards)
            //{
            //    var item = c.GetComponent<Item>();
            //    if (item.name != _config.CardName)
            //    {
            //        item.name = _config.CardName;
            //        Puts("card name changed");
            //    }
            //}
        }
        private bool CrateReady(Vector3 position)
        {
            foreach (var c in HackableLockedCrate.serverEntities)
            {
                var crate = c as HackableLockedCrate;
                if (crate == null) continue;
                if (Vector3.Distance(position, crate.transform.position) <= 100)
                {
                    if (crate.IsBeingHacked() || crate.IsFullyHacked())
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void BroadcastEvent(BasePlayer player, string v)
        {
            string monument = "";
            if (v == "large")
            {
                monument = "Large OilRig";
            }
            else
            {
                monument = "Small OilRig";
            }
            player.ChatMessage(GetLang("ActivateEventPlayerMessage", player));
            foreach (var p in BasePlayer.activePlayerList)
            {
                if (p == null) { continue; }
                timer.Repeat(1f, 4, () =>
                {
                    p.ShowToast(GameTip.Styles.Server_Event, $"{GetLang("ActivateEvent", p)} <color=green>{monument}</color>");
                });
            }
        }
        private void Activateevent(string rig,BasePlayer player)
        {
            if (rig == "large")
            {
                foreach (var pos in LargeCorrections)
                {
                    var entity = GameManager.server.CreateEntity(crate, SpawnCratePos(pos.Key, "Large Oil Rig",player).pos, SpawnCratePos(pos.Key, "Large Oil Rig",player).rot);
                    var hack = entity?.GetComponent<HackableLockedCrate>();
                    // hack.OwnerID = 0304;
                    hack._name = "0304";
                    float addedRotationY = pos.Value;
                    Quaternion addedRotation = Quaternion.Euler(0f, addedRotationY, 0f);
                    hack.transform.localRotation *= addedRotation;
                    hack.Spawn();
                    spawnedCrates.Add(hack);
                }
                EventActive = true;
            }
            else if (rig == "small")
            {
                foreach (var pos in SmallCorrections)
                {
                    var entity = GameManager.server.CreateEntity(crate, SpawnCratePos(pos.Key, "Oil Rig",player).pos, SpawnCratePos(pos.Key, "Oil Rig",player).rot);
                    var hack = entity?.GetComponent<HackableLockedCrate>();
                    // hack.OwnerID = 0304;
                    hack._name = "0304";
                    float addedRotationY = pos.Value;
                    Quaternion addedRotation = Quaternion.Euler(0f, addedRotationY, 0f);
                    hack.transform.localRotation *= addedRotation;
                    hack.Spawn();
                    spawnedCrates.Add(hack);
                }
                EventActive = true;
            }
            //timer.Once(30f, () =>
            //{
            //    var list = spawnedCrates.ToList();
            //    var max = list.Count;
            //    for (int n = max; n > 0; n--)
            //    {
            //        var c = list[n - 1];
            //        if (c == null) { continue; }
            //        c.Kill();
            //        spawnedCrates.Remove(c);
            //    }

            //});
        }

        public string GetRig(Vector3 pos)
        {
            var rig = "";
            TerrainMeta.Path.Monuments.ForEach(monument =>
            {
                if (monument == null) return;
                if (Vector3.Distance(pos, monument.transform.position) < 100)
                {
                    if (monument.displayPhrase.english.Contains("Oil Rig"))
                    {
                        if (monument.displayPhrase.english.StartsWith("Large"))
                        {
                            rig = "large";
                        }
                        else
                        {
                            rig = "small";
                        }
                    }
                }
            });
            return rig;
        }

        private String getOilrigName(BasePlayer player)
        {
            var rig = "";
            TerrainMeta.Path.Monuments.ForEach(monument =>
            {
                if (monument == null) return;
                if (Vector3.Distance(player.transform.position, monument.transform.position) < 100)
                {
                    if (monument.displayPhrase.english.Contains("Oil Rig"))
                    {
                        if (monument.displayPhrase.english.StartsWith("Large"))
                        {
                            rig = "Large Oil Rig";
                        }
                        else
                        {
                            rig = "Small Oil Rig";
                        }
                    }
                }
            });
            return rig;
        }
        public GetVecs SpawnCratePos(Vector3 correction, string rig, BasePlayer player)
        {
            Vector3 pos = new Vector3(0, 0, 0);
            Quaternion rot = new Quaternion(0, 0, 0, 0);

            TerrainMeta.Path.Monuments.ForEach(monument =>
            {
                if (monument == null) return;

                bool isPlayerCloseOrWithinBounds = false;
                if (player != null)
                {
                    isPlayerCloseOrWithinBounds =
                        Vector3.Distance(player.transform.position, monument.transform.position) <= 100f ||
                        monument.Bounds.Contains(player.transform.position);
                }

                if (player == null || isPlayerCloseOrWithinBounds)
                {
                    if (monument.displayPhrase.english == rig)
                    {
                        var correct = correction;
                        if (correct == Vector3.zero) return;

                        var transform = monument.transform;
                        rot = transform.rotation;
                        pos = transform.position + rot * correct;
                        return;
                    }
                }
            });

            return new GetVecs { pos = pos, rot = rot };
        }



        public void StartHackcycle(List<HackableLockedCrate> list)
        {
            int currentIndex = 0;

            void HackNextCrate()
            {
                if (currentIndex < list.Count)
                {
                    var crate = list[currentIndex];
                    if (crate != null) { crate.StartHacking(); }
                    currentIndex++;
                }
                else
                {
                    list.Clear();
                    EventActive = false;
                    spawnedCrates.Clear();
                }
            }

            timer.Repeat(30f, list.Count, HackNextCrate);
        }

        BaseEntity SetReader(Vector3 pos, Quaternion rot)
        {
            PressButton changed = null;
            var reader = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/button/button.prefab", pos, rot);
            if (reader == null) { Puts("reader null"); return null; }
            reader.gameObject.SetActive(true);
            if (reader is PressButton)
                changed = reader as PressButton;
            if (changed != null)
            {
                //changed.OwnerID = 0304;
                changed._name = "9999";                
            }
            float addedRotationY = 0;
            if (GetRig(reader.transform.position) == "large")
            {
                addedRotationY = LargeReaderCorrection.FirstOrDefault().Value;
            }
            else
            {
                addedRotationY = SmallReaderCorrection.FirstOrDefault().Value;
            }

            Quaternion addedRotation = Quaternion.Euler(0f, addedRotationY, 0f);
            reader.transform.localRotation *= addedRotation;

            reader.Spawn();
            SpawnRefresh(reader);
            changed._name = "9999";
            reader.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            reader.SendNetworkUpdateImmediate();
            return reader;
        }

        void SpawnRefresh(BaseNetworkable entity1)
        {
            UnityEngine.Object.Destroy(entity1.GetComponent<Collider>());
        }
        #endregion Methods

        #region Config

        private class Configuration
        {

            [JsonProperty(PropertyName = "Enable Card spawn?")]
            public bool EnableSpawn = true;

            [JsonProperty(PropertyName = "Drop Settings", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<DropInfo> Drop = new List<DropInfo>
            {
                new DropInfo
                {
                    ShortPrefabName = "crate_elite",
                    MinAmount = 1,
                    MaxAmount = 1,
                    DropChance = 10
                },
                 new DropInfo
                {
                    ShortPrefabName = "codelockedhackablecrate",
                    MinAmount = 1,
                    MaxAmount = 1,
                    DropChance = 10
                },
            };

            [JsonProperty(PropertyName = "Card Name")]
            public string CardName = "Wave Card";
        }

        public class DropInfo
        {
            [JsonProperty(PropertyName = "Object Short prefab name")]
            public string ShortPrefabName;

            [JsonProperty(PropertyName = "Minimum item to drop")]
            public int MinAmount;

            [JsonProperty(PropertyName = "Maximum item to drop")]
            public int MaxAmount;

            [JsonProperty(PropertyName = "Item Drop Chance")]
            public float DropChance;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
            PrintWarning("Default Configuration File Created");
        }

        #endregion

        #region Lang

        private string GetLang(string key, BasePlayer player)
        {
            return lang.GetMessage(key, this)
                .Replace("{playername}", player.displayName);
        }

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CrateNotReady"] = "Main Crate not Active or Hacked",
                ["ActivateEvent"] = "A Player Activated the Wave Event at :",
                ["ActivateEventPlayerMessage"] = "You have 5min's to hack main crate , or event will fail",
                ["EventActiveMessage"] = "Event already running",

            }, this, "en");
        }

        #endregion Lang       

        #region Extra

        private void MonitorCrate(HackableLockedCrate crate,MonumentInfo monument)
        {
            var crateState = new CrateState();
            crateState.Monument = monument;
            CrateCheckTimer = timer.Repeat(10f, 0, () => CheckCrateStatus(crate,crateState));
        }

        private void CheckCrateStatus(HackableLockedCrate crate, CrateState crateState)
        {           
            if (crate == null || crate.IsDestroyed)
            {
                PrintWarning("Crate is null or destroyed. Stopping timer.");
                CrateCheckTimer.Destroy();
                return;
            }

            float remainingTime = HackableLockedCrate.requiredHackSeconds - crate.hackSeconds;
            float halfTime = HackableLockedCrate.requiredHackSeconds / 2;
            float quarterTime = HackableLockedCrate.requiredHackSeconds / 4;
            float tenthTime = HackableLockedCrate.requiredHackSeconds / 10;

            if (remainingTime <= halfTime && !crateState.Triggered50Percent)
            {
                Puts("50% of the crate timer remaining.");
                crateState.Triggered50Percent = true;
            }
            else if (remainingTime <= quarterTime && !crateState.Triggered25Percent)
            {
                Puts("25% of the crate timer remaining.");
                crateState.Triggered25Percent = true;
            }
            else if (remainingTime <= tenthTime && !crateState.Triggered10Percent)
            {
                Puts("10% of the crate timer remaining. Stopping timer.");
                crateState.Triggered10Percent = true;
                CrateCheckTimer.Destroy();
            }
        }
        private class CrateState
        {
            public bool Triggered50Percent { get; set; } = false;
            public bool Triggered25Percent { get; set; } = false;
            public bool Triggered10Percent { get; set; } = false;
            public MonumentInfo Monument { get; set; }
        }

        [ChatCommand("cmonument")]
        private void CheckMonumentCommand(BasePlayer player, string command, string[] args)
        {
            MonumentInfo nearestMonument = FindNearestMonument(player.transform.position);
            Vector3 correction = new Vector3(0,0,0);
            Vector3 moveCorrection = new Vector3(0,0,0);
            if (nearestMonument == null)
            {
                player.ChatMessage("No monument found nearby.");
                return;
            }

            Vector3 relativePosition = player.transform.position - nearestMonument.transform.position;
            Puts($"Nearest Monument: {nearestMonument.displayPhrase.english}"); 
            Puts($"Relative Position: {relativePosition.ToString()}");

            if (nearestMonument.displayPhrase.english.StartsWith("Large"))
            {
                correction = new Vector3(13.77f, 9.88f, 5.09f);
                moveCorrection = new Vector3(-6.43f, 36.15f, -4.43f);
            }
            else
            {
                correction = new Vector3(7.53f, 13.52f, -3.39f);
                 moveCorrection = new Vector3(9.10f, 27.77f, -19.98f);
            } 
            if(correction != Vector3.zero)
            {
               // SpawnHeavyScientists(nearestMonument, correction,moveCorrection);
            }
        }

        private MonumentInfo FindNearestMonument(Vector3 playerPosition)
        {
            MonumentInfo nearestMonument = null;
            float shortestDistance = float.MaxValue;

            foreach (var monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
            {
                float distance = Vector3.Distance(playerPosition, monument.transform.position);

                if (distance < shortestDistance)
                {
                    shortestDistance = distance;
                    nearestMonument = monument;
                }
            }

            return nearestMonument;
        }

        private void SpawnHeavyScientists(MonumentInfo monument, Vector3 correction,Vector3 movecorrection)
        {
            if (monument == null)
            {
                PrintWarning("Monument is null. Cannot spawn scientists.");
                return;
            }

            Vector3 centerPosition = monument.transform.position + correction;
            Vector3 movePosition = monument.transform.position + movecorrection;
            float radius = 0.5f; // Adjust the radius as needed for the size of the circle
            int numberOfScientists = 5;

            for (int i = 0; i < numberOfScientists; i++)
            {
                float angle = i * Mathf.PI * 2 / numberOfScientists;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                Vector3 spawnPosition = centerPosition + offset;

                // Create and spawn the heavy scientist
                var scientist = GameManager.server.CreateEntity("assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_heavy.prefab", spawnPosition) as ScientistNPC;
                if (scientist != null)
                {
                    scientist.Spawn();
                    Puts($"Spawned heavy scientist at {spawnPosition}");
                    timer.Once(5f, () =>
                    {
                        if (scientist.NavAgent != null)
                        {
                            scientist.NavAgent.enabled = true;
                            scientist.NavAgent.Move(movePosition);
                            scientist.SetDestination(movePosition);     
                        }
                        scientist.TryThink();
                    });
                }
                else
                {
                    PrintWarning("Failed to create heavy scientist entity.");
                }
            }
        }




        #endregion

        #region Commands

        [ChatCommand("testcard")]
        private void testcardCommand(BasePlayer target, string command, string[] args)
        {
            if (!target.IsAdmin) { return; }
            var item = Item?.ToItem();
            if (item == null) return;

            target.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
        }

        [ConsoleCommand("givecard")]
        private void giveplayercardCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.GetString(0);
            var item = Item?.ToItem();
            if (item == null) return;
            if(BasePlayer.FindAwakeOrSleeping(player) == null) { Puts("Player not found!"); return; }
            BasePlayer.FindAwakeOrSleeping(player).GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
        }

        [ConsoleCommand("findcard")]
        private void findcardCommand(ConsoleSystem.Arg arg)
        {
            FindCards();
        }

        #endregion Commands

        #region Classes
        public class GetVecs
        {
            public Vector3 pos { get; set; }
            public Quaternion rot { get; set; }
        }

        public class ItemConfig
        {
            [JsonProperty(PropertyName = "DisplayName")]
            public string DisplayName;

            [JsonProperty(PropertyName = "Discription")]
            public string Discription;

            [JsonProperty(PropertyName = "ShortName")]
            public string ShortName;

            [JsonProperty(PropertyName = "SkinID")]
            public ulong SkinID;
            [JsonProperty(PropertyName = "Name")]
            public string Name;

            public Item ToItem()
            {
                var newItem = ItemManager.CreateByName(ShortName, 1, SkinID);
                if (newItem == null)
                {
                    Debug.LogError($"Error creating item with shortName '{ShortName}'!");
                    return null;
                }
               
                newItem.name = _cardName;
                newItem.info.displayDescription.english = Discription;
                newItem.MarkDirty();
                return newItem;
            }

            public bool IsSame(Item item)
            {
                return item != null && item.info.shortname == ShortName && item.skin == SkinID;
            }
        }

        public new ItemConfig Item = new ItemConfig
        {
            DisplayName = _cardName,
            Discription = "Access Card For OilRig Wave Event",
            ShortName = "keycard_red",
            SkinID = 1988408422,
            Name = "0304",
        };

        #endregion Classes
    }
}
