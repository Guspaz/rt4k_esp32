using System;
using System.Collections;
using System.IO;
using System.Net;
using nanoFramework.Hardware.Esp32;

namespace rt4k_esp32
{
    internal class WebInterface : WebServer
    {
        private readonly FileManager fm;
        private readonly byte[] showdownJs;
        private readonly string indexHtml;

        public WebInterface(FileManager fileManager, LogDelegate log, int port) : base(log, port, "WebUI")
        {
            fm = fileManager;

            // Pre-cache web assets, we've got psram to spare right now
            showdownJs = WebFiles.GetBytes(WebFiles.BinaryResources.showdown_min_js);
            indexHtml = WebFiles.GetString(WebFiles.StringResources.index);
        }

        protected override void Route(HttpListenerContext context)
        {
            context.Response.ContentType = "text/html";
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.KeepAlive = false;

            switch (context.Request.RawUrl)
            {
                case "/":
                case "/index.htm":
                {
                    var sw = new StreamWriter(context.Response.OutputStream);
                    sw.Write(indexHtml);
                    sw.WriteLine("<pre>");

                    foreach (string line in Program.webLog)
                    {
                        sw.WriteLine(line);
                    }

                    sw.WriteLine("</pre></details></p></body></html>");
                    sw.Flush();
                    break;
                }
                case "/disableWifi":
                {
                    Log(" ***** Disabling wifi next boot, remove and re-insert SD card to reboot it");
                    File.Create("I:\\disableWifi");

                    Redirect(context, "/");
                    break;
                }
                case "/bulkEdit":
                {
                    Log("Bulk edit request received");
                    var formData = ParseFormData(ReadRequest());
                    Log($"Writing value {formData["value"]} to address {formData["address"]}");

                    byte[] profileData = fm.ReadFileRaw("/profile/test_blankcrc.rt4");
                    Profile profile = new Profile(profileData);
                    fm.WriteFileRaw("/profile/test_result.rt4", profile.Save());

                    Redirect(context, "/");
                    break;
                }
                case "/showdown.min.js":
                {
                    // Special case, this js library is pre-gzipped
                    context.Response.Headers.Add("Content-Encoding", "gzip");
                    context.Response.ContentType = "text/javascript";

                    context.Response.OutputStream.Write(showdownJs, 0, showdownJs.Length);
                    break;
                }
                case "/log":
                {
                    context.Response.ContentType = "text/plain";
                    var sw = new StreamWriter(context.Response.OutputStream);
                    foreach (string line in Program.webLog)
                    {
                        sw.WriteLine(line);
                    }
                    sw.Flush();
                    break;
                }
                default:
                {
                    SendEmptyResponse(context, HttpStatusCode.NotFound);
                    break;
                }
            }
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
