using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("SoundLibrary", "https://topplugin.ru/", "0.0.8")]
    [Description("Библиотека звуков")]
    public class SoundLibrary : RustPlugin
    {
        public StoredData DataBase = new StoredData();

        public class StoredData
        {
            public List<string> SoundNames = new List<string>();
        }

        public class SoundFile
        {
            public List<byte[]> SoundData = new List<byte[]>();
        }

        public Dictionary<string, Dictionary<DateTime, List<byte[]>>> LoadedSounds =
            new Dictionary<string, Dictionary<DateTime, List<byte[]>>>();

        public Dictionary<ulong, bool> status = new Dictionary<ulong, bool>();
        public List<byte[]> timed = new List<byte[]>();
        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject("SoundLibrary/DataSounds", DataBase);

        private void LoadData()
        {
            try
            {
                DataBase = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("SoundLibrary/DataSounds");
            }
            catch (Exception e)
            {
                DataBase = new StoredData();
            }
        }

        private object LoadDataFiles(string name)
        {
            var SFile = Interface.Oxide.DataFileSystem.GetDatafile($"SoundLibrary/{name}");
            var SoundFile = new SoundFile();
            SoundFile = SFile.ReadObject<SoundFile>();
            var soundList = SoundFile.SoundData;
            return soundList;
        }

        private void SaveDataFile(string name, List<byte[]> data)
        {
            var CheckFile = Interface.Oxide.DataFileSystem.ExistsDatafile($"SoundLibrary/{name}");
            var File = Interface.Oxide.DataFileSystem.GetFile($"SoundLibrary/{name}");
            var SoundFile = new SoundFile();
            foreach (var bt in data)
            {
                SoundFile.SoundData.Add(bt);
            }

            if (CheckFile != null && !DataBase.SoundNames.Contains(name))
            {
                DataBase.SoundNames.Add(name);
                File.WriteObject(SoundFile);
            }
            else
            {
                File.Clear();
                File.WriteObject(SoundFile);
            }
        }

        private void OnServerInitialized()
        {
            LoadData();
        }

        object OnPlayerVoice(BasePlayer player, byte[] data)
        {
            if (player != null && status.ContainsKey(player.userID))
            {
                timed.Add(data);
            }

            return null;
        }

        [ChatCommand("vorec")]
        void startcmd(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            if (args.Length == 0)
            {
                SendReply(player, "/vorec start - Starting Recording");
                SendReply(player, "/vorec clear - Clearing Record");
                SendReply(player, "/vorec stop - Stop Recording");
                SendReply(player, "/vorec save - Save Record");
                return;
            }
            switch (args[0].ToLower())
            {
                case "start":
                {
                    if (!status.ContainsKey(player.userID))
                    {
                        status.Add(player.userID, true);
                        SendReply(player, "Запись включена говорите в микрофон");
                        return;
                    }

                    if (status.ContainsKey(player.userID))
                    {
                        SendReply(player, "Запись уже идёт");
                        return;
                    }

                    if (status[player.userID] == false)
                    {
                        SendReply(player, "Запись уже сделана сохраните её");
                    }

                    break;
                }
                case "clear":
                {
                    if (status.ContainsKey(player.userID))
                    {
                        timed.Clear();
                        SendReply(player, "Запись стёрта попробуйте ещё");
                    }
                    else
                    {
                        SendReply(player, "Вы не начали запись");
                    }

                    break;
                }
                case "stop":
                {
                    if (status.ContainsKey(player.userID))
                    {
                        status[player.userID] = false;
                        SendReply(player, "Запись остановлена вы можете сохранить запись");
                    }
                    else
                    {
                        SendReply(player, "Вы не начали запись");
                    }

                    break;
                }
                case "save":
                {
                    if (args.Length < 2)
                    {
                        SendReply(player, "Введите название записи");
                        return;
                    }

                    if (!status.ContainsKey(player.userID))
                    {
                        SendReply(player, "Вы ничего не записали");
                        return;
                    }

                    if (DataBase.SoundNames.Contains(args[1]))
                    {
                        SendReply(player, "Запись с таким названием уже есть");
                    }

                    if (status[player.userID] == false)
                    {
                        status.Remove(player.userID);
                        DataBase.SoundNames.Add(args[1]);
                        SaveDataFile(args[1], timed);
                        timed.Clear();
                        SaveData();
                        LoadDataFiles(args[1]);
                        SendReply(player, "Запись успешно сохранена");
                    }

                    break;
                }
            }
        }

        void OnServerSave()
        {
            foreach (var finder in LoadedSounds.ToList())
            {
                var LastUseData = finder.Value.Keys.ToList()[0];
                if (DateTime.Now.Subtract(LastUseData).TotalMinutes > 5) LoadedSounds.Remove(finder.Key);
                if (LoadedSounds.Count == 0) break;
            }
        }

        private void GetSoundToPlayer(ulong playerid, uint netid, string name)
        {
            if (!LoadedSounds.ContainsKey(name))
            {
                if (Interface.Oxide.DataFileSystem.ExistsDatafile($"SoundLibrary/{name}"))
                {
                    var Sfile = Interface.Oxide.DataFileSystem.ReadObject<SoundFile>($"SoundLibrary/{name}");
                    var SoundFl = new SoundFile();
                    SoundFl = Sfile;
                    LoadedSounds.Add(name, new Dictionary<DateTime, List<byte[]>>());
                    LoadedSounds[name].Add(DateTime.Now, Sfile.SoundData);
                }
                else
                {
                    PrintError($"Не могу найти звук с именем {name}");
                    return;
                }
            }

            if (playerid != null)
            {
                BasePlayer player = BasePlayer.FindByID(playerid);
                LoadedSounds[name].Keys.ToList()[0] = DateTime.Now;
                foreach (var f in LoadedSounds[name].Values.ToList()[0])
                {
                    SendToPlayer(player, netid, f);
                }
            }
        }

        private void GetSoundToAll(uint netid, string name)
        {
            if (!LoadedSounds.ContainsKey(name))
            {
                if (Interface.Oxide.DataFileSystem.ExistsDatafile($"SoundLibrary/{name}"))
                {
                    var Sfile = Interface.Oxide.DataFileSystem.ReadObject<SoundFile>($"SoundLibrary/{name}");
                    var SoundFl = new SoundFile();
                    SoundFl = Sfile;
                    LoadedSounds.Add(name, new Dictionary<DateTime, List<byte[]>>());
                    LoadedSounds[name].Add(DateTime.Now, Sfile.SoundData);
                }
                else
                {
                    PrintError($"Не могу найти звук с именем {name}");
                    return;
                }
            }

            LoadedSounds[name].Keys.ToList()[0] = DateTime.Now;
            foreach (var f in LoadedSounds[name].Values.ToList()[0])
            {
                SendToAll(netid, f);
            }
        }

        public void SendToPlayer(BasePlayer player, uint netid, byte[] data)
        {
            if (Network.Net.sv.write.Start())
            {
                Network.Net.sv.write.PacketID(Network.Message.Type.VoiceData);
                Network.Net.sv.write.UInt32(netid);
                Network.Net.sv.write.BytesWithSize(data);
                Network.Net.sv.write.Send(new Network.SendInfo(player.Connection)
                    {priority = Network.Priority.Immediate});
            }
        }

        public void SendToAll(uint netid, byte[] data)
        {
            foreach (var pl in BasePlayer.activePlayerList)
            {
                if (Network.Net.sv.write.Start())
                {
                    Network.Net.sv.write.PacketID(Network.Message.Type.VoiceData);
                    Network.Net.sv.write.UInt32(netid);
                    Network.Net.sv.write.BytesWithSize(data);
                    Network.Net.sv.write.Send(new Network.SendInfo(pl.Connection)
                        {priority = Network.Priority.Immediate});
                }
            }
        }
    }
}