using System.Diagnostics;
using System.Threading;
using System.Collections;
using nanoFramework.Hardware.Esp32;
using nanoFramework.Runtime.Native;
using System.IO;

namespace rt4k_esp32
{
    public class Program
    {
        public const string VERSION = "v1.0.0";

        public static Queue webLog;

        private static SdManager sdManager;
        private static FileManager fileManager;
        private static WifiManager wifiManager;
        private static SettingsManager settingsManager;

        public static void Main()
        {
            webLog = new Queue();
            Log($"RT4K ESP32 {VERSION} booting. Hello from the .NET nanoFramework!");

            //// Dump the boot log to ONLY the debug log if it exists
            //try
            //{
            //    if (File.Exists("I:\\boot.log"))
            //    {
            //        Debug.WriteLine("========== PREVIOUS BOOT LOG ==========");
            //        using (FileStream fs = new FileStream("I:\\boot.log", FileMode.Open, FileAccess.Read))
            //        using (StreamReader sr = new(fs))
            //        {
            //            Debug.Write(sr.ReadToEnd());
            //        }
            //        Debug.WriteLine("========== END PREVIOUS BOOT LOG ==========");

            //        File.Delete("I:\\boot.log");
            //    }
            //}
            //catch { }

            // The GPIO controller used by FileManager can't be initialized in a constructor, so delay creating it to here.

            NativeMemory.GetMemoryInfo(NativeMemory.MemoryType.Internal, out uint totalInt, out uint totalIntFree, out _);
            NativeMemory.GetMemoryInfo(NativeMemory.MemoryType.SpiRam, out uint totalSpi, out uint totalSpiFree, out _);

            Log($"Unreserved RAM: {totalInt / 1024} KiB internal, {totalSpi / 1024} KiB SPI PSRAM");
            Log($"Unused RAM: {totalIntFree / 1024} KiB internal, {totalSpiFree / 1024} KiB SPI PSRAM");
            Log($"Target: {SystemInfo.Platform} ({SystemInfo.TargetName} - v{SystemInfo.Version})");
            Log($"OEM: {SystemInfo.OEMString}");
            Log($"FPU: {(int)SystemInfo.FloatingPointSupport switch { 0 => "None", 1 => "SinglePrecisionSoftware", 2 => "SinglePrecisionHardware", 3 => "DoublePrecisionSoftware", 4 => "DoublePrecisionHardware", _ => "Unknown" }}");

            if (File.Exists("I:\\disableWifi"))
            {
                File.Delete("I:\\disableWifi");
                Log("disableWifi file detected, disabling wifi until next boot");
                Thread.Sleep(Timeout.Infinite);
            }

            settingsManager = new SettingsManager(Log);
            sdManager = new SdManager(Log, settingsManager);
            fileManager = new FileManager(Log, sdManager);
            wifiManager = new WifiManager(Log, fileManager, settingsManager);

            wifiManager.WifiBoot();

            // Wait until we're connected
            while (!wifiManager.IsConnected)
            {
                Thread.Sleep(0);
            }

            var webInterface = new WebInterface(fileManager, settingsManager, Log, 80);
            var webDAV = new WebDav(fileManager, Log, 81);

            Thread.Sleep(Timeout.Infinite);

            // TODO: Add support for reboot after a long idle period (like 12 or 16 hours) in case there's any slow memory leaks or something.
        }

        public static void Log(string message)
        {
            Debug.WriteLine(message);
            webLog.Enqueue(message);

            //using (var fs = new FileStream("I:\\boot.log", FileMode.OpenOrCreate, FileAccess.Write))
            //using (var sw = new StreamWriter(fs))
            //{
            //    if (fs.Length <= 32 * 1024)
            //    {
            //        fs.Seek(0, SeekOrigin.End);
            //        sw.WriteLine(message);
            //    }
            //}

            while (webLog.Count > 150)
            {
                webLog.Dequeue();
            }
        }
    }
}

