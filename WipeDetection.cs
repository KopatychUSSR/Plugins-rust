using Oxide.Game.Rust.Cui;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
namespace Oxide.Plugins
{
    [Info("WipeDetection", "TopPlugin.ru", "0.1.1")]

    class WipeDetection : RustPlugin
    {
        #region Fields
        DateTime NextWipeDate;
        static WipeDetection instance;
        bool init;
        #endregion

        #region Oxide Hooks 
        void OnNewSave()
        {
            configData.LastWipe = DateTime.Now.Date.ToString("MM/dd/yyyy H:m");
            configData.NextWipe = DateTime.Now.AddDays(configData.DaysBetweenWipes).ToString("MM/dd/yyyy H:m");
            SaveConfig(configData);
            UpdateWipeDates();
        }

        void OnServerInitialized()
        {
            instance = this;
            LoadVariables();
            LoadWipeDates();
            InitFileManager();
            CommunityEntity.ServerInstance.StartCoroutine(m_FileManager.LoadFile("logo", configData.Images));
            timer.Every(60f, () =>
            {
                UpdateTime();
            });
            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerInit(player);
            }
        }

        void UpdateTime()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                DrawGUI(player);
            }
        }

        void OnPlayerInit(BasePlayer player)
        {
            DrawGUI(player);
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyUI(player);
            }
        }

        string UI = "[{\"name\":\"wipe_bp\",\"parent\":\"Hud\",\"components\":[{\"type\":\"UnityEngine.UI.RawImage\",\"png\":\"{images}\"},{\"type\":\"RectTransform\",\"anchormin\":\"0.6442167 0.0234375\",\"anchormax\":\"0.8316253 0.1132813\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 1\"}]},{\"name\":\"wipe_text\",\"parent\":\"wipe_bp\",\"components\":[{\"type\":\"UnityEngine.UI.Text\",\"text\":\"{text}\",\"fontSize\":16,\"align\":\"MiddleCenter\"},{\"type\":\"UnityEngine.UI.Outline\",\"color\":\"0 0 0 1\",\"distance\":\"0.5 -0.8\"},{\"type\":\"RectTransform\",\"anchormin\":\"0 0\",\"anchormax\":\"1 0.7971016\",\"offsetmin\":\"0 0\",\"offsetmax\":\"1 1\"}]}]";
        

        void DrawGUI(BasePlayer player)
        {
            if (!init)
            {
                timer.Every(1f, () => DrawGUI(player));
                return;
            }
            var message = NextWipeDays(NextWipeDate);
            CuiHelper.DestroyUi(player, "wipe_bp");
            CuiHelper.AddUi(player, UI
                  .Replace("{text}", message)
                  .Replace("{images}", m_FileManager.GetPng("logo")));
        }

        void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "wipe_bp");
        }
        #endregion

        #region Functions        
        private DateTime ParseTime(string time) => DateTime.ParseExact(time, "MM/dd/yyyy H:m", CultureInfo.InvariantCulture);

        private void UpdateWipeDates()
        {
            var lastWipe = ParseTime(configData.LastWipe);
            NextWipeDate = lastWipe.AddDays(configData.DaysBetweenWipes);
        }

        private void LoadWipeDates()
        {
            NextWipeDate = ParseTime(configData.NextWipe);
        }

        private string NextWipeDays(DateTime WipeDate)
        {
            TimeSpan t = WipeDate.Subtract(DateTime.Now);
            return string.Format(FormatTime(TimeSpan.FromDays(t.TotalDays)));
        }
        
        public static string FormatTime(TimeSpan time)
        {
            string result = string.Empty;
            if (time.Days != 0)
                result += $"{Format(time.Days, "дней", "дня", "день")} ";

            if (time.Hours != 0)
                result += $"{Format(time.Hours, "часов", "часа", "час")} ";

            if (time.Minutes != 0)
                result += $"{Format(time.Minutes, "минут", "минуты", "минута")} ";
            
            return result;
        }

        private static string Format(int units, string form1, string form2, string form3)
        {
            var tmp = units % 10;

            if (units >= 5 && units <= 20 || tmp >= 5 && tmp <= 9)
                return $"{units} {form1}";

            if (tmp >= 2 && tmp <= 4)
                return $"{units} {form2}";

            return $"{units} {form3}";
        }
        #endregion
        
        #region Config        
        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty("Частота вайпов (Раз в N дней)")]
            public int DaysBetweenWipes { get; set; }
            [JsonProperty("Последний вайп был 'MM/dd/yyyy H:m'")]
            public string LastWipe { get; set; }
            [JsonProperty("Следующий вайп будет 'MM/dd/yyyy H:m'")]
            public string NextWipe { get; set; }
            [JsonProperty("Фоновое изображение для UI")]
            public string Images { get; set; }
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            if (string.IsNullOrEmpty(configData.LastWipe))
                configData.LastWipe = DateTime.Now.ToString("MM/dd/yyyy H:mm");
            if (string.IsNullOrEmpty(configData.NextWipe))
                configData.NextWipe = DateTime.Now.AddDays(configData.DaysBetweenWipes).ToString("MM/dd/yyyy H:mm");
            SaveConfig(configData);
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                DaysBetweenWipes = 7,
                LastWipe = "",
                NextWipe = "",
                Images = "https://i.imgur.com/awxjvAp.png"
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region LoadImages
        private GameObject FileManagerObject;
        private FileManager m_FileManager;

        /// <summary>
        /// Инициализация скрипта взаимодействующего с файлами сервера
        /// </summary>
        void InitFileManager()
        {
            FileManagerObject = new GameObject("FileManagerObject");
            m_FileManager = FileManagerObject.AddComponent<FileManager>();

        }

        class FileManager : MonoBehaviour
        {
            int loaded = 0;
            int needed = 0;
           
            public bool IsFinished => needed == loaded;
            const ulong MaxActiveLoads = 10;
            Dictionary<string, FileInfo> files = new Dictionary<string, FileInfo>();

            private class FileInfo
            {
                public string Url;
                public string Png;
            }


            public string GetPng(string name) => files[name].Png;


            public IEnumerator LoadFile(string name, string url, int size = -1)
            {
                if (files.ContainsKey(name) && files[name].Url == url && !string.IsNullOrEmpty(files[name].Png)) yield break;
                files[name] = new FileInfo() { Url = url };
                needed++;
                yield return StartCoroutine(LoadImageCoroutine(name, url, size));
            }

            IEnumerator LoadImageCoroutine(string name, string url, int size = -1)
            {
                using (WWW www = new WWW(url))
                {
                    yield return www;
                    if (string.IsNullOrEmpty(www.error))
                    {
                        var bytes = size == -1 ? www.bytes : Resize(www.bytes, size);


                        var entityId = CommunityEntity.ServerInstance.net.ID;
                        var crc32 = FileStorage.server.Store(bytes, FileStorage.Type.png, entityId).ToString();
                        files[name].Png = crc32;
                    }
                    
                }
                instance.init = true;
                loaded++;

            }

            static byte[] Resize(byte[] bytes, int size)
            {
                Image img = (Bitmap)(new ImageConverter().ConvertFrom(bytes));
                Bitmap cutPiece = new Bitmap(size, size);
                System.Drawing.Graphics graphic = System.Drawing.Graphics.FromImage(cutPiece);
                graphic.DrawImage(img, new Rectangle(0, 0, size, size), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel);
                graphic.Dispose();
                MemoryStream ms = new MemoryStream();
                cutPiece.Save(ms, ImageFormat.Jpeg);
                return ms.ToArray();

            }
        }
        #endregion
    }
}
            