using System.IO;
using nanoFramework.Json;

namespace rt4k_esp32
{
    internal class SettingsManager
    {
        private const string SETTINGS_FILE = "I:\\rt4k_config.json";
        private SettingsFile settingsFile;

        internal delegate void LogDelegate(string message);
        private readonly LogDelegate Log;

        internal SettingsManager(LogDelegate logFunc)
        {
            File.Delete(SETTINGS_FILE);

            Log = logFunc;

            Log("FileManager starting up");

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
                    wifiDelay = 30,
                    lockSdForWifiDelay = true
                };

                UpdateSettingsFile();
            }

            Log("FileManager started");
        }

        public int WifiDelay
        {
            get => settingsFile.wifiDelay;
            set
            {
                settingsFile.wifiDelay = value;
                UpdateSettingsFile();
            }
        }

        public bool LockSdForWifiDelay
        {
            get => settingsFile.lockSdForWifiDelay;
            set
            {
                settingsFile.lockSdForWifiDelay = value;
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
