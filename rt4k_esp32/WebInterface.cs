using System.Diagnostics;
using System.IO;
using System.Net;
using nanoFramework.Hardware.Esp32;
using nanoFramework.Runtime.Native;

namespace rt4k_esp32
{
    internal class WebInterface : WebServer
    {
        private readonly FileManager fm;
        private readonly byte[] showdownJs;
        private readonly string base_template_header;
        private readonly string base_template_footer;

        public WebInterface(FileManager fileManager, LogDelegate log, int port) : base(log, port, "WebUI")
        {
            fm = fileManager;

            // Pre-cache web assets, we've got psram to spare right now
            showdownJs = WebFiles.GetBytes(WebFiles.BinaryResources.showdown_min_js);
            base_template_header = WebFiles.GetString(WebFiles.StringResources.base_template_header).TrimStart('\u0001');
            base_template_footer = WebFiles.GetString(WebFiles.StringResources.base_template_footer);
        }

        protected override void Route(HttpListenerContext context)
        {
            context.Response.ContentType = "text/html";
            //context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.KeepAlive = false;

            // Special case for binary file
            if (context.Request.RawUrl == "/showdown.min.js")
            {
                context.Response.Headers.Add("Content-Encoding", "gzip");
                context.Response.ContentType = "text/javascript";
                context.Response.OutputStream.Write(showdownJs, 0, showdownJs.Length);
                return;
            }

            var sw = new StreamWriter(context.Response.OutputStream);

            switch (context.Request.RawUrl)
            {
                case "/":
                    sw.WriteLine(base_template_header);

                    sw.WriteLine("<h1>Status</h1>");
                    sw.WriteLine("<table class='w3-table-all w3-card' style='max-width: 700px;'>");

                    NativeMemory.GetMemoryInfo(NativeMemory.MemoryType.Internal, out uint totalInt, out uint totalIntFree, out _);
                    NativeMemory.GetMemoryInfo(NativeMemory.MemoryType.SpiRam, out uint totalSpi, out uint totalSpiFree, out _);

                    sw.WriteLine($"<tr><th>Unreserved RAM</th><td>{totalInt / 1024} KiB internal, {totalSpi / 1024} KiB SPI PSRAM</td></tr>");
                    sw.WriteLine($"<tr><th>Unused RAM</th><td>{totalIntFree / 1024} KiB internal, {totalSpiFree / 1024} KiB SPI PSRAM</td></tr>");
                    sw.WriteLine($"<tr><th>Target</th><td>{SystemInfo.Platform} ({SystemInfo.TargetName} - v{SystemInfo.Version})</td></tr>");
                    sw.WriteLine($"<tr><th>OEM</th><td>{SystemInfo.OEMString}</td></tr>");
                    sw.WriteLine($"<tr><th>FPU</th><td>{(int)SystemInfo.FloatingPointSupport switch { 0 => "None", 1 => "SinglePrecisionSoftware", 2 => "SinglePrecisionHardware", 3 => "DoublePrecisionSoftware", 4 => "DoublePrecisionHardware", _ => "Unknown" }}</td></tr>");

                    sw.WriteLine("</table>");

                    sw.WriteLine(base_template_footer);
                    break;

                case "/readme":
                    sw.WriteLine(base_template_header);
                    
                    sw.WriteLine(WebFiles.GetString(WebFiles.StringResources.readme));

                    sw.WriteLine(base_template_footer);
                    break;

                case "/debugLog":
                    sw.WriteLine(base_template_header);

                    sw.WriteLine("<h1>Debug Log</h1>");
                    sw.WriteLine("<pre>");

                    foreach (string line in Program.webLog)
                    {
                        sw.WriteLine(line);
                    }

                    sw.WriteLine("</pre>");

                    sw.WriteLine(base_template_footer);
                    break;

                case "/disableWifi":
                    Log(" ***** Disabling wifi next boot, remove and re-insert SD card to reboot it");
                    File.Create("I:\\disableWifi");

                    Redirect(context, "/actions");
                    break;

                case "/bulkEdit":
                    Log("Bulk edit request received");
                    var formData = ParseFormData(ReadRequest());
                    Log($"Writing value {formData["value"]} to address {formData["address"]}");

                    byte[] profileData = fm.ReadFileRaw("/profile/test_blankcrc.rt4");
                    Profile profile = new Profile(profileData);
                    fm.WriteFileRaw("/profile/test_result.rt4", profile.Save());

                    Redirect(context, "/actions");
                    break;

                default:
                    SendEmptyResponse(context, HttpStatusCode.NotFound);
                    break;
            }

            sw.Flush();
        }

        private void Redirect(HttpListenerContext context, string url)
        {
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
