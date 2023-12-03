using System.IO;
using System.Threading;
using nanoFramework.Json;

namespace rt4k_esp32
{
    internal class SettingsManager
    {
        private const string SETTINGS_FILE = "I:\\rt4k_config.json";
        private readonly SettingsFile settingsFile;

        internal delegate void LogDelegate(string message);
        private readonly LogDelegate Log;

        public bool SdWaitOver { get; private set; }

        internal SettingsManager(LogDelegate logFunc)
        {
            Log = logFunc;

            Log("SettingsManager starting up");

            // No need to use the FileManager for internal ESP32 storage
            if (File.Exists(SETTINGS_FILE))
            {
                using (var fs = new FileStream(SETTINGS_FILE, FileMode.Open, FileAccess.Read))
                {
                    settingsFile = (SettingsFile)JsonConvert.DeserializeObject(fs, typeof(SettingsFile));
                }
            }
            else
            {
                settingsFile = new SettingsFile
                {
                    WifiDelay = 30,
                    LockSdForWifiDelay = true
                };

                UpdateSettingsFile();
            }

            new Thread(() => { Thread.Sleep(settingsFile.WifiDelay * 1000); SdWaitOver = true; }).Start();

            Log("SettingsManager started");
        }

        public int WifiDelay
        {
            get => settingsFile.WifiDelay;
            set
            {
                settingsFile.WifiDelay = value;
                Log($"Updating setting wifiDelay: {value}");
                UpdateSettingsFile();
            }
        }

        public bool LockSdForWifiDelay
        {
            get => settingsFile.LockSdForWifiDelay;
            set
            {
                settingsFile.LockSdForWifiDelay = value;
                Log($"Updating setting lockSdForWifiDelay: {value}");
                UpdateSettingsFile();
            }
        }

        private void UpdateSettingsFile()
        {
            using (var fs = new FileStream(SETTINGS_FILE, FileMode.Create, FileAccess.Write))
            using (var sw = new StreamWriter(fs))
            {
                sw.Write(JsonConvert.SerializeObject(settingsFile));
            }
        }
    }
}
