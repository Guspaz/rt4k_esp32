using System;
using System.Diagnostics;
using System.Threading;
using System.Collections;
using nanoFramework.Hardware.Esp32;
using nanoFramework.Runtime.Native;
using System.Net;
using System.IO;

namespace rt4k_esp32
{
    public class Program
    {
        public static Queue webLog;

        private static SdManager sdManager;
        private static FileManager fileManager;
        private static WifiManager wifiManager;

        public static void Main()
        {
            webLog = new Queue();
            Log("RT4K SD booting. Hello from the .NET nanoFramework!");


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

            NativeMemory.GetMemoryInfo(NativeMemory.MemoryType.Internal, out uint totalInt, out _, out _);
            NativeMemory.GetMemoryInfo(NativeMemory.MemoryType.SpiRam, out uint totalSpi, out _, out _);

            Log($"Total RAM: {totalInt / 1024} KiB internal, {totalSpi / 1024} KiB SPI PSRAM");
            Log($"Platform: {SystemInfo.Platform}");
            Log($"OEM: {SystemInfo.OEMString}");
            Log($"Target: {SystemInfo.TargetName}");
            Log($"Version: {SystemInfo.Version}");
            Log($"FPU: {(int)SystemInfo.FloatingPointSupport switch { 0 => "None", 1 => "SinglePrecisionSoftware", 2 => "SinglePrecisionHardware", 3 => "DoublePrecisionSoftware", 4 => "DoublePrecisionHardware", _ => "Unknown" }}");

            if (File.Exists("I:\\disableWifi"))
            {
                File.Delete("I:\\disableWifi");
                Log("disableWifi file detected, disabling wifi until next boot");
                Thread.Sleep(Timeout.Infinite);
            }

            sdManager = new SdManager(Log);
            fileManager = new FileManager(Log, sdManager);
            wifiManager = new WifiManager(Log, fileManager);

            wifiManager.WifiBoot();

            var webInterface = new WebInterface(fileManager, Log);
            StartServer(80, "WebUI", webInterface.Route);

            var webDAV = new WebDav(fileManager, Log);
            StartServer(81, "WebDAV", webDAV.Route);

            // TODO: Do we need to keep this thread alive?
            Thread.Sleep(Timeout.Infinite);
        }

        public delegate void RouteDelegate(HttpListenerContext context);

        public static void StartServer(int port, string name, RouteDelegate route)
        {
            new Thread(() =>
            {
                Log($"Starting {name} server on port {port}");
                HttpListener httpServer = null;

                while (true)
                {
                    try
                    {
                        httpServer = new HttpListener("http", port);
                        httpServer.Start();

                        Log($"{name} server started");

                        while (httpServer.IsListening)
                        {
                            var context = httpServer.GetContext();
                            try
                            {
                                Log($"{name}: {context.Request.HttpMethod} {context.Request.RawUrl}");
                                route(context);

                                if (context.Response.StatusCode != (int)HttpStatusCode.OK)
                                {
                                    Log($"Response: {context.Response.StatusCode}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Log($"[{Thread.CurrentThread.ManagedThreadId}:{name}] Uncaught Exception in {name}.Route()");
                                Log(ex.ToString());
                                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                            }

                            try
                            {
                                context.Response.Close();
                                context.Close();
                            }
                            catch { }
                            Thread.Sleep(0);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"[{Thread.CurrentThread.ManagedThreadId}:{name}] Uncaught Exception in HttpListener.GetContext()");
                        Log(ex.ToString());
                    }
                    finally
                    {
                        // Try forcing a restart
                        try
                        {
                            httpServer?.Stop();
                            httpServer?.Abort();
                        }
                        catch { }
                    }

                    // If we ever fail, sleep a bit and restart
                    Log($"{name} server failed, restarting");
                    Thread.Sleep(1000);
                }
            }).Start();
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

            while (webLog.Count > 100)
            {
                webLog.Dequeue();
            }
        }
    }
}

