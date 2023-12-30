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
                    BootLockDelay = 30,
                    BootLockWifiOnly = false
                };

                UpdateSettingsFile();
            }

            new Thread(() => { Thread.Sleep(settingsFile.BootLockDelay * 1000); SdWaitOver = true; Log($"Releasing SD lock ({settingsFile.BootLockDelay} seconds elapsed)."); }).Start();

            Log("SettingsManager started");
        }

        public int BootLockDelay
        {
            get => settingsFile.BootLockDelay;
            set
            {
                settingsFile.BootLockDelay = value;
                Log($"Updating setting bootLockDelay: {value}");
                UpdateSettingsFile();
            }
        }

        public bool BootLockWifiOnly
        {
            get => settingsFile.BootLockWifiOnly;
            set
            {
                settingsFile.BootLockWifiOnly = value;
                Log($"Updating setting bootLockWifiOnly: {value}");
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
