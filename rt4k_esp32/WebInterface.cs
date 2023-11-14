using System.Collections;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Web;
using nanoFramework.Hardware.Esp32;
using nanoFramework.Runtime.Native;

namespace rt4k_esp32
{
    internal class WebInterface : WebServer
    {
        private readonly FileManager fm;
        private readonly string base_template_header;
        private readonly string base_template_footer;

        public WebInterface(FileManager fileManager, LogDelegate log, int port) : base(log, port, "WebUI")
        {
            fm = fileManager;

            // Pre-cache web assets, we've got psram to spare right now
            base_template_header = WebFiles.GetString(WebFiles.StringResources.base_template_header).TrimStart('\u0001');
            base_template_footer = WebFiles.GetString(WebFiles.StringResources.base_template_footer).TrimStart('\u0001');
        }

        protected override void Route(HttpListenerContext context)
        {
            context.Response.ContentType = "text/html";
            //context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.KeepAlive = false;

            // Special case for binary files
            if (context.Request.RawUrl == "/showdown.min.js")
            {
                context.Response.Headers.Add("Content-Encoding", "gzip");
                context.Response.ContentType = "text/javascript";
                var showdownJs = WebFiles.GetBytes(WebFiles.BinaryResources.showdown_min_js);
                context.Response.OutputStream.Write(showdownJs, 0, showdownJs.Length);
                return;
            }
            else if(context.Request.RawUrl == "/calc.js")
            {
                context.Response.Headers.Add("Content-Encoding", "gzip");
                context.Response.ContentType = "text/javascript";
                var calcJs = WebFiles.GetBytes(WebFiles.BinaryResources.calc_js);
                context.Response.OutputStream.Write(calcJs, 0, calcJs.Length);
                return;
            }

            var sw = new StreamWriter(context.Response.OutputStream);

            string[] urlComponents = context.Request.RawUrl.Split('?');
            string baseURL = urlComponents[0];
            Hashtable getParams = new Hashtable();

            if (urlComponents.Length > 1)
            {
                getParams = ParseUrlParams(urlComponents[1]);
            }

            try
            {
                // First, handle commands that don't generate a page.
                switch (baseURL)
                {
                    case "/disableWifi":
                        Log(" ***** Disabling wifi next boot, remove and re-insert SD card to reboot it");
                        File.Create("I:\\disableWifi");

                        Redirect(context, "/actions", success: "Disabling wifi next boot, remove and re-insert SD card to reboot it");
                        return;

                    case "/bulkEdit":
                        Log("Bulk edit request received");
                        var formData = ParseUrlParams(ReadRequest());
                        Log($"Writing value {formData["value"]} to address {formData["address"]}");

                        // TODO: proper format validation on fields here
                        if (string.IsNullOrEmpty((string)formData["address"]))
                        {
                            Redirect(context, "/actions", error: "Invalid address provided.");
                            return;
                        }
                        else if (string.IsNullOrEmpty((string)formData["value"]))
                        {
                            Redirect(context, "/actions", error: "Invalid value provided.");
                            return;
                        }

                        //foreach (var file in fm.ListFilesRecursive("/", ".rt4"))
                        //{
                        //    Log($"File found: {file}");
                        //}

                        // TODO: Implement actual bulk edit
                        // TODO: Checksum calculation is slow, so do it in a thread, report progress back, and skip files that don't need to be updated.

                        //byte[] profileData = fm.ReadFileRaw("/profile/test_blankcrc.rt4");
                        //Profile profile = new Profile(profileData);
                        //fm.WriteFileRaw("/profile/test_result.rt4", profile.Save());

                        Redirect(context, "/actions", success: "Bulk profile edit completed");
                        return;

                    case "/orderPizza":
                        Log("Pizza request received");

                        Redirect(context, "/actions", error: "Pizza support to be implemented in a future update");
                        return;
                }

                // Next, handle actual pages

                sw.WriteLine(base_template_header);

                if (getParams.Contains("success") && !string.IsNullOrEmpty((string)getParams["success"]))
                {
                    sw.WriteLine($"<div class=\"w3-panel w3-card w3-green w3-round-large\"><p><b>{((string)getParams["success"]).Replace("<", "&lt;")}</b></p></div>");
                }
                else if (getParams.Contains("error") && !string.IsNullOrEmpty((string)getParams["error"]))
                {
                    sw.WriteLine($"<div class=\"w3-panel w3-card w3-red w3-round-large\"><p><b>ERROR: {((string)getParams["error"]).Replace("<", "&lt;")}</b></p></div>");
                }

                switch (baseURL)
                {
                    case "/":
                        string ip = IPGlobalProperties.GetIPAddress().ToString();
                        sw.WriteLine("<h1>Status</h1>");
                        sw.WriteLine("<div class='w3-panel'><table class='w3-table-all w3-card' style='max-width: 700px;'>");
                        sw.WriteLine($"<tr><th width='200px'>Wifi SSID</th><td>{WifiManager.SSID}</td></tr>");
                        sw.WriteLine($"<tr><th>IP Address</th><td>{ip}</td></tr>");
                        sw.WriteLine($"<tr><th>WebDAV Address</th><td><a href='http://{ip}:{WebDav.Port}'>http://{ip}:{WebDav.Port}</a></td></tr>");
                        sw.WriteLine($"</table></div>");

                        NativeMemory.GetMemoryInfo(NativeMemory.MemoryType.Internal, out uint totalInt, out uint totalIntFree, out _);
                        NativeMemory.GetMemoryInfo(NativeMemory.MemoryType.SpiRam, out uint totalSpi, out uint totalSpiFree, out _);
                        sw.WriteLine("<h1>ESP32 Info</h1>");
                        sw.WriteLine("<div class='w3-panel'><table class='w3-table-all w3-card' style='max-width: 700px;'>");
                        sw.WriteLine($"<tr><th width='200px'>Unreserved RAM</th><td>{totalInt / 1024} KiB internal, {totalSpi / 1024} KiB SPI PSRAM</td></tr>");
                        sw.WriteLine($"<tr><th>Unused RAM</th><td>{totalIntFree / 1024} KiB internal, {totalSpiFree / 1024} KiB SPI PSRAM</td></tr>");
                        sw.WriteLine($"<tr><th>Target</th><td>{SystemInfo.Platform} ({SystemInfo.TargetName} - v{SystemInfo.Version})</td></tr>");
                        sw.WriteLine($"<tr><th>OEM</th><td>{SystemInfo.OEMString}</td></tr>");
                        sw.WriteLine($"<tr><th>FPU</th><td>{(int)SystemInfo.FloatingPointSupport switch { 0 => "None", 1 => "SinglePrecisionSoftware", 2 => "SinglePrecisionHardware", 3 => "DoublePrecisionSoftware", 4 => "DoublePrecisionHardware", _ => "Unknown" }}</td></tr>");
                        sw.WriteLine("</table></div>");

                        break;

                    case "/readme":
                        sw.WriteLine(WebFiles.GetString(WebFiles.StringResources.readme).TrimStart('\u0001'));
                        break;

                    case "/videoTimings":
                        sw.WriteLine(WebFiles.GetString(WebFiles.StringResources.video_timings_calculator).TrimStart('\u0001'));
                        break;

                    case "/actions":
                        sw.WriteLine(WebFiles.GetString(WebFiles.StringResources.actions).TrimStart('\u0001'));
                        break;

                    case "/debugLog":
                        sw.WriteLine("<h1>Debug Log</h1>");
                        sw.WriteLine("<pre>");

                        foreach (string line in Program.webLog)
                        {
                            sw.WriteLine(line);
                        }

                        sw.WriteLine("</pre>");

                        break;

                    default:
                        sw.WriteLine($"<div class=\"w3-panel w3-card w3-red w3-round-large\"><p><b>ERROR: 404: Not Found</b></p></div>");
                        break;
                }

                sw.WriteLine(base_template_footer);
            }
            finally
            {
                sw.Flush();
            }
        }

        private void Redirect(HttpListenerContext context, string url, string success = "", string error = "")
        {
            if (!string.IsNullOrEmpty(success))
            {
                url += $"?success={HttpUtility.UrlEncode(success)}";
            }
            else if (!string.IsNullOrEmpty(error))
            {
                url += $"?error={HttpUtility.UrlEncode(error)}";
            }

            context.Response.Headers.Add("Location", url);
            SendEmptyResponse(context, HttpStatusCode.TemporaryRedirect);
        }

        private void SendEmptyResponse(HttpListenerContext context, HttpStatusCode statusCode)
        {
            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentLength64 = 0;
        }
    }
}
