using nanoFramework.Hardware.Esp32;
using nanoFramework.Networking;
using System;
using System.Device.Wifi;
using System.IO;
using System.Net.NetworkInformation;
using System.Net;
using System.Threading;

namespace rt4k_esp32
{
    internal class WifiManager
    {
        internal delegate void LogDelegate(string message);

        private readonly LogDelegate Log;
        private readonly FileManager fileManager;
        private string cachedWifiCredsHash;

        internal WifiManager(LogDelegate logFunc, FileManager fileManager)
        {
            Log = logFunc;

            Log("WifiManager starting up");

            this.fileManager = fileManager;
            this.fileManager.WifiIniUpdated += UpdateCachedWifiCreds;
        }

        internal void WifiBoot()
        {
            //// DEBUG:
            //using (var fs = new FileStream("I:\\wifi.ini", FileMode.Create, FileAccess.Write))
            //using (var sw = new StreamWriter(fs))
            //{
            //    sw.WriteLine("ssid = spaznet_2.4");
            //    sw.WriteLine("password = 0taku96!");
            //}

            if (File.Exists("I:\\wifiReboot"))
            {
                File.Delete("I:\\wifiReboot");
                Log("Detected wifi reboot, skipping scheduled refresh of cached credentials");

                ConnectWifi();
            }
            else
            {
                Log("Fresh boot, connecting to wifi using cached credentials");
                Log($"Current time after boot: {HighResTimer.GetCurrent() / 1000000f}s");

                ConnectWifi();

                new Thread(() =>
                {
                    Log("Scheduling check of wifi credentials on SD card in 30 seconds");
                    Thread.Sleep(30000);
                    Log($"Current time after boot: {HighResTimer.GetCurrent() / 1000000f}s");

                    Log("Comparing wifi credentials in cache to SD card");

                    UpdateCachedWifiCreds(null, null);
                }).Start();
            }
        }

        internal void UpdateCachedWifiCreds(object sender, EventArgs e)
        {
            string fileContent = fileManager.ReadFile("wifi.ini", true);

            if (fileContent != cachedWifiCredsHash)
            {
                Log("SD card wifi.ini is different from cache");

                var wifiCreds = IniParser.Parse(fileContent);

                // TODO: Can we simplify this?
                if (wifiCreds != null && wifiCreds["ssid"] != null && (string)wifiCreds["ssid"] != string.Empty && wifiCreds["password"] != null && (string)wifiCreds["password"] != string.Empty)
                {
                    Log("New wifi creds seem valid, updating cache and scheduling reboot");
                    using (FileStream fs = new("I:\\wifi.ini", FileMode.Create, FileAccess.Write))
                    using (StreamWriter sw = new(fs))
                    {
                        sw.Write(fileContent);
                    }

                    File.Create("I:\\wifiReboot");
                    new Thread(() =>
                    {
                        Thread.Sleep(5000);
                        // TODO: Release SD on reboot?
                        //SD_SWITCH.Write(PinValue.High);
                        Log("Rebooting");
                        nanoFramework.Runtime.Native.Power.RebootDevice();
                    }).Start();
                }
                else
                {
                    Log("New wifi creds are invalid, skipping cache update");
                }
            }
            else
            {
                Log("Credentials on SD card match cache");
            }
        }

        private void ConnectWifi()
        {
            Log("Attempting to connect to wifi with cached credentials");

            if (File.Exists("I:\\wifi.ini"))
            {
                using (FileStream fs = new FileStream("I:\\wifi.ini", FileMode.Open, FileAccess.Read))
                using (StreamReader sr = new(fs))
                {
                    cachedWifiCredsHash = sr.ReadToEnd();
                }

                var wifiConfig = IniParser.Parse(cachedWifiCredsHash);

                if (wifiConfig.Contains("ssid") && wifiConfig.Contains("password"))
                {
                    Log("Cached wifi credentials look valid, attempting to (re)connect");
                    WifiNetworkHelper.WifiAdapter?.Disconnect();
                    WifiNetworkHelper.Disconnect();

                    try
                    {
                        WifiNetworkHelper.ConnectDhcp((string)wifiConfig["ssid"], (string)wifiConfig["password"], WifiReconnectionKind.Manual, requiresDateTime: false, token: new CancellationTokenSource(10000).Token);
                    }
                    catch (Exception ex)
                    {
                        Log("Exception connecting to wifi:");
                        Log(ex.Message);
                        Log(ex.StackTrace);
                        if (ex.InnerException != null)
                        {
                            Log("Inner Exception:");
                            Log(ex.InnerException.Message);
                            Log(ex.InnerException.StackTrace);
                        }
                    }

                    if (WifiNetworkHelper.Status == NetworkHelperStatus.NetworkIsReady && IPGlobalProperties.GetIPAddress() != IPAddress.Any)
                    {
                        Log($"Wifi connected! IP address: {IPGlobalProperties.GetIPAddress()}");
                        fileManager.QueueWrite("ipAddress.txt", IPGlobalProperties.GetIPAddress().ToString());
                    }
                    else
                    {
                        Log("Wifi connection failed");
                    }
                }
            }
            else
            {
                Log("No cached credentials found");
            }
        }
    }
}