﻿using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Random = System.Random;

namespace Oxide.Plugins
{
    [Info("VoNPC", "https://topplugin.ru/", "0.1.0")]
    [Description("Settings sound NPC For SoundLibrary")]
    public class VoNPC : RustPlugin
    {
        static Random random = new Random();
        [PluginReference]
        Plugin SoundLibrary;

        public NpcData npcdata;
        public class NpcData
        {
            public Dictionary<ulong,NpcAction> Npcaction = new Dictionary<ulong, NpcAction>();
        }

        public class NpcAction
        {
            public List<string> OnHit;
            public List<string> OnUse;
            public List<string> OnEnter;
            public List<string> OnLeave;
            public List<string> OnKill;
            public List<string> OnRespawn;
            public bool Type;

        }
        Dictionary<ulong,Dictionary<ulong,DateTime>> Timers = new Dictionary<ulong, Dictionary<ulong,DateTime>>();


        private void SaveNpcData() => Interface.Oxide.DataFileSystem.WriteObject(Name,npcdata);
        private void LoadNpcData() {
            try {
                npcdata = Interface.Oxide.DataFileSystem.ReadObject<NpcData>(Name);
            } catch (Exception)
            {
                npcdata = new NpcData();
            }
        }
        private class VoNpcEdit : MonoBehaviour
        {
            public BasePlayer player;
            public ulong idnpc;
            private void Awake()
            {
                player = GetComponent<BasePlayer>();
            }
        }
        private void OnServerInitialized()
        {
            if (!plugins.Find("SoundLibrary"))
            {
                PrintError("Plugin SoundLibrary not installed,Please upload plugin Sound Library");
                Interface.Oxide.UnloadPlugin(Title);
                return;
            }
            LoadNpcData();
        }

        void OnServerSave()
        {
            foreach (var pl in Timers.Keys.ToList())
            {
                foreach (var npc in Timers[pl].Keys.ToList())
                {
                    if (DateTime.Now.Subtract(Timers[pl][npc]).TotalSeconds > 10) Timers[pl].Remove(npc);
                   
                }

            }
        }

        [ChatCommand("vonpc_add")]
        void vonpcadd(BasePlayer player, string command, string[] args)
        {
            if(!player.IsAdmin) return;
             if (args.Length == 0)
             {
                 SendReply(player, "/vonpc_add ID - Add NPC");
                 return;
             }
             if(!npcdata.Npcaction.ContainsKey(ulong.Parse(args[0])))
             {
                 
                 var data = new NpcAction{            
                     OnHit = new List<string> { "" },
                     OnUse = new List<string> { "" },
                     OnEnter = new List<string> { "" },
                     OnLeave = new List<string> { "" },
                     OnKill = new List<string> { "" },
                     OnRespawn = new List<string> { "" },
                     Type = false
                 };
                 npcdata.Npcaction.Add(ulong.Parse(args[0]),data);
                 SaveNpcData();
                 var vonpcedit = player.gameObject.AddComponent<VoNpcEdit>();
                 vonpcedit.idnpc = ulong.Parse(args[0]);
                 SendReply(player, "You added NPC");

             }
        }
        [ChatCommand("vonpc")]
        void vonpc(BasePlayer player, string command, string[] args)
        {
            if(!player.IsAdmin) return;
            var vonpcedit = player.GetComponent<VoNpcEdit>();
            if (vonpcedit == null)
            {
                SendReply(player, "NPC Editor: You need to be editing an NPC, say /npc_add or /npc_edit");
                return;
            }

          
            if (args.Length == 0)
            {

                SendReply(player, "/vonpc onuse name - set sound name when player use npc");
                SendReply(player, "/vonpc onhit name - set sound name someone hiting npc");
                SendReply(player, "/vonpc onenter name - set sound name when player enter in npc radius");
                SendReply(player, "/vonpc leave name - set sound name when player leave npc radius");
                SendReply(player, "/vonpc onkill name - set sound name when player killing npc");
                SendReply(player, "/vonpc onrespawn name - set sound name when npc respawn");
                SendReply(player, "/vonpc type All/Player - set type playing for only player or for all players");
            }

            if (args.Length > 1)
            {
                switch (args[0].ToLower())
                {
                    case "onuse":
                    {
                        npcdata.Npcaction[vonpcedit.idnpc].OnUse.Add(args[1]);
                        SendReply(player,"Added sound for OnUse when down button E");
                        SaveNpcData();
                        break;
                    }
                    case "onuseclear":
                    {
                        npcdata.Npcaction[vonpcedit.idnpc].OnUse.Clear();
                        SendReply(player, "Clearing Sound for OnUse");
                        SaveNpcData();
                        break;
                    }
                    case "onhit":
                    {
                        npcdata.Npcaction[vonpcedit.idnpc].OnHit.Add(args[1]);
                            SendReply(player,"Added sound for OnHit when someone attack npc");
                        SaveNpcData();
                        break;
                    }
                    case "onhitclear":
                    {
                        npcdata.Npcaction[vonpcedit.idnpc].OnHit.Clear();
                        SendReply(player, "Clearing Sound for OnHit");
                        SaveNpcData();
                        break;
                    }
                    case "onenter":
                    {
                        npcdata.Npcaction[vonpcedit.idnpc].OnEnter.Add(args[1]);
                            SendReply(player,"Added sound for OnEnter when someone enter in npc radius");
                        SaveNpcData();
                        break;
                    }
                    case "onenterclear":
                        {
                            npcdata.Npcaction[vonpcedit.idnpc].OnEnter.Clear();
                            SendReply(player, "Clearing Sound for OnEnter");
                            SaveNpcData();
                            break;
                        }
                    case "onleave":
                    {
                        npcdata.Npcaction[vonpcedit.idnpc].OnLeave.Add(args[1]);
                            SendReply(player,"Added sound for OnLeave when someone leave from npc radius");
                        SaveNpcData();
                        break;
                    }
                    case "onleaveclear":
                        {
                            npcdata.Npcaction[vonpcedit.idnpc].OnLeave.Clear();
                            SendReply(player, "Clearing Sound for OnLeave");
                            SaveNpcData();
                            break;
                        }
                    case "onkill":
                    {
                        npcdata.Npcaction[vonpcedit.idnpc].OnKill.Add(args[1]);
                            SendReply(player,"Added sound for OnKill when someone killing npc");
                        SaveNpcData();
                        break;
                    }
                    case "onkillclear":
                        {
                            npcdata.Npcaction[vonpcedit.idnpc].OnKill.Clear();
                            SendReply(player, "Clearing Sound for OnKill");
                            SaveNpcData();
                            break;
                        }
                    case "onrespawn":
                    {
                        npcdata.Npcaction[vonpcedit.idnpc].OnRespawn.Add(args[1]);
                            SendReply(player,"Added sound for OnRespawn when npc is respawning");
                        SaveNpcData();
                        break;
                    }
                    case "onrespawnclear":
                        {
                            npcdata.Npcaction[vonpcedit.idnpc].OnRespawn.Clear();
                            SendReply(player, "Clearing Sound for OnRespawn");
                            SaveNpcData();
                            break;
                        }
                    case "type":
                    {
                        if (args[1].ToLower() == "All")
                        {
                            npcdata.Npcaction[vonpcedit.idnpc].Type = false;
                            SendReply(player, "Selected play type for all, all players will be listen around npc");
                            SaveNpcData();
                        }
                        if (args[1].ToLower() == "Player")
                        {
                            npcdata.Npcaction[vonpcedit.idnpc].Type = true;
                            SendReply(player,"Selected play type for player, only one player will be listen");
                            SaveNpcData();
                        }
                        break;
                    }
                }
            }
            
        }

        void GetRandom()
        {
            foreach (var finder in Timers)
            {
                if (finder.Key == 719)
                {
                    PrintError("Error Check");
                }
            }

        }
        [ChatCommand("vonpc_edit")]
        void vonpcedit(BasePlayer player, string command, string[] args)
        {
            if(!player.IsAdmin) return;
            if (player.GetComponent<VoNpcEdit>() != null)
            {
                SendReply(player,$"You already edit sound for npc  {player.GetComponent<VoNpcEdit>().idnpc}");
            }

            if (!npcdata.Npcaction.ContainsKey(ulong.Parse(args[0])))
            {
                SendReply(player,"Can't Find that NPC");
                
            }
            var vonpcedit = player.gameObject.AddComponent<VoNpcEdit>();
            vonpcedit.idnpc = ulong.Parse(args[0]);
            SendReply(player, $"You started edit sound for npc {vonpcedit.idnpc}");
        }
        [ChatCommand("vonpc_end")]
        private void cmdChatNPCEnd(BasePlayer player, string command, string[] args)
        {
            var vonpcedit = player.GetComponent<VoNpcEdit>();
            if (vonpcedit == null)
            {
                SendReply(player, "You already edit sound");
                return;
            }
            UnityEngine.Object.Destroy(vonpcedit);
            SendReply(player, "Edit ended");
        }

        void CheckDbTimer(ulong Uid)
        {
            if (!Timers.ContainsKey(Uid))
                Timers.Add(Uid,new Dictionary<ulong, DateTime>());
        }
void OnUseNPC(BasePlayer npc, BasePlayer player)
        {
            if(!npcdata.Npcaction.ContainsKey(npc.userID)) return;
            var NpcData = npcdata.Npcaction[npc.userID];
            int cont = random.Next(NpcData.OnUse.Count);
            CheckDbTimer(player.userID);
                if (NpcData.OnUse.Count != 0)
                {
                    if(!Timers[player.userID].ContainsKey(npc.userID))
                    {
                        Timers[player.userID].Add(npc.userID, DateTime.Now);
                        if (NpcData.Type)

                            SoundLibrary?.Call("GetSoundToPlayer", player.userID, npc.net.ID, NpcData.OnUse[cont]);
                        else
                            SoundLibrary?.Call("GetSoundToAll", npc.net.ID, NpcData.OnUse[cont]);
                    }
                    
                    if (Timers[player.userID].ContainsKey(npc.userID) && DateTime.Now.Subtract(Timers[player.userID][npc.userID]).TotalSeconds > 10)
                    {
                        
                        Timers[player.userID][npc.userID] = DateTime.Now;
                    if (NpcData.Type)
                        SoundLibrary?.Call("GetSoundToPlayer", player.userID, npc.net.ID, NpcData.OnUse[cont]);
                    else
                        SoundLibrary?.Call("GetSoundToAll", npc.net.ID, NpcData.OnUse[cont]);
                    }

                    
                }
        }

        void OnHitNPC(BasePlayer npc, HitInfo info)
        {
            if(!npcdata.Npcaction.ContainsKey(npc.userID)) return;
            var NpcData = npcdata.Npcaction[npc.userID];
            BasePlayer player = info.InitiatorPlayer;
            int cont = random.Next(NpcData.OnHit.Count);
            CheckDbTimer(player.userID);
            if (NpcData.OnHit.Count != 0)
            {
                if(!Timers[player.userID].ContainsKey(npc.userID))
                {
                    Timers[player.userID].Add(npc.userID, DateTime.Now);
                    if (NpcData.Type)
                        SoundLibrary?.Call("GetSoundToPlayer", player.userID, npc.net.ID, NpcData.OnHit[cont]);
                    else
                        SoundLibrary?.Call("GetSoundToAll", npc.net.ID, NpcData.OnHit[cont]);
                }
                
                if (Timers[player.userID].ContainsKey(npc.userID) && DateTime.Now.Subtract(Timers[player.userID][npc.userID]).TotalSeconds > 10)
                {
                        
                    Timers[player.userID][npc.userID] = DateTime.Now;
                    if (NpcData.Type)
                        SoundLibrary?.Call("GetSoundToPlayer", player.userID, npc.net.ID, NpcData.OnHit[cont]);
                    else
                        SoundLibrary?.Call("GetSoundToAll", npc.net.ID, NpcData.OnHit[cont]);
                }
                    
            }
        }

        void OnEnterNPC(BasePlayer npc, BasePlayer player)
        {
            if(!npcdata.Npcaction.ContainsKey(npc.userID)) return;
            var NpcData = npcdata.Npcaction[npc.userID];
            int cont = random.Next(NpcData.OnEnter.Count);
            CheckDbTimer(player.userID);
            if (NpcData.OnEnter.Count != 0)
            {
                if(!Timers[player.userID].ContainsKey(npc.userID))
                {
                    Timers[player.userID].Add(npc.userID, DateTime.Now);
                    if (NpcData.Type)
                        SoundLibrary?.Call("GetSoundToPlayer", player.userID, npc.net.ID, NpcData.OnEnter[cont]);
                    else
                        SoundLibrary?.Call("GetSoundToAll", npc.net.ID, NpcData.OnEnter[cont]);
                }
                
                if (Timers[player.userID].ContainsKey(npc.userID) && DateTime.Now.Subtract(Timers[player.userID][npc.userID]).TotalSeconds > 10)
                {
                        
                    Timers[player.userID][npc.userID] = DateTime.Now;
                    if (NpcData.Type)
                        SoundLibrary?.Call("GetSoundToPlayer", player.userID, npc.net.ID, NpcData.OnEnter[cont]);
                    else
                        SoundLibrary?.Call("GetSoundToAll", npc.net.ID, NpcData.OnEnter[cont]);
                }
                    

            }
        }

        void OnLeaveNPC(BasePlayer npc, BasePlayer player)
        {
            if(!npcdata.Npcaction.ContainsKey(npc.userID)) return;
            var NpcData = npcdata.Npcaction[npc.userID];
            int cont = random.Next(NpcData.OnLeave.Count);
            CheckDbTimer(player.userID);
            if (NpcData.OnLeave.Count != 0)
            {
                if(!Timers[player.userID].ContainsKey(npc.userID))
                {
                    Timers[player.userID].Add(npc.userID, DateTime.Now);
                    if (NpcData.Type)
                        SoundLibrary?.Call("GetSoundToPlayer", player.userID, npc.net.ID, NpcData.OnLeave[cont]);
                    else
                        SoundLibrary?.Call("GetSoundToAll", npc.net.ID, NpcData.OnLeave[cont]);
                }
                
                if (Timers[player.userID].ContainsKey(npc.userID) && DateTime.Now.Subtract(Timers[player.userID][npc.userID]).TotalSeconds > 10)
                {
                        
                    Timers[player.userID][npc.userID] = DateTime.Now;
                    if (NpcData.Type)
                        SoundLibrary?.Call("GetSoundToPlayer", player.userID, npc.net.ID, NpcData.OnLeave[cont]);
                    else
                        SoundLibrary?.Call("GetSoundToAll", npc.net.ID, NpcData.OnLeave[cont]);
                }
                    

            }
        }

        void OnKillNPC(BasePlayer npc, BasePlayer player)
        {
            if(!npcdata.Npcaction.ContainsKey(npc.userID)) return;
            var NpcData = npcdata.Npcaction[npc.userID];
            int cont = random.Next(NpcData.OnKill.Count);
            CheckDbTimer(player.userID);
            if (NpcData.OnKill.Count != 0)
            {
                if(!Timers[player.userID].ContainsKey(npc.userID))
                {
                    Timers[player.userID].Add(npc.userID, DateTime.Now);
                    if (NpcData.Type)
                        SoundLibrary?.Call("GetSoundToPlayer", player.userID, npc.net.ID, NpcData.OnKill[cont]);
                    else
                        SoundLibrary?.Call("GetSoundToAll", npc.net.ID, NpcData.OnKill[cont]);
                }
                
                if (Timers[player.userID].ContainsKey(npc.userID) && DateTime.Now.Subtract(Timers[player.userID][npc.userID]).TotalSeconds > 10)
                {
                    Timers[player.userID][npc.userID] = DateTime.Now;
                    if (NpcData.Type)
                        SoundLibrary?.Call("GetSoundToPlayer", player.userID, npc.net.ID, NpcData.OnKill[cont]);
                    else
                        SoundLibrary?.Call("GetSoundToAll", npc.net.ID, NpcData.OnKill[cont]);
                }
                    
            }
        }

        void OnNPCRespawn(BasePlayer npc)
        {
            NextTick(() => {
            if(!npcdata.Npcaction.ContainsKey(npc.userID)) return;
            var NpcData = npcdata.Npcaction[npc.userID];
                int cont = random.Next(NpcData.OnRespawn.Count);
                if (NpcData.OnRespawn.Count != 0)
                    SoundLibrary?.Call("GetSoundToAll", npc.net.ID, NpcData.OnRespawn[cont]);
            });
        }
    }
}