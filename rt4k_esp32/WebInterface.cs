using System;
using System.IO;
using System.Net;
using nanoFramework.Hardware.Esp32;

namespace rt4k_esp32
{
    internal class WebInterface
    {
        internal delegate void LogDelegate(string message);

        private readonly FileManager fm;
        private readonly LogDelegate Log;
        byte[] showdownJs;

        public WebInterface(FileManager fileManager, LogDelegate log)
        {
            fm = fileManager;
            Log = log;

            // Pre-cache the showdown library, we've got psram to spare right now
            showdownJs = WebFiles.GetBytes(WebFiles.BinaryResources.showdown_min_js);
        }

        public void Route(HttpListenerContext context)
        {
            context.Response.ContentType = "text/html";
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.KeepAlive = false;

            switch (context.Request.RawUrl)
            {
                case "/":
                case "/index.htm":
                    using (var sw = new StreamWriter(context.Response.OutputStream))
                    {
                        sw.Write(WebFiles.GetString(WebFiles.StringResources.index));
                        sw.WriteLine("<pre>");

                        foreach (string line in Program.webLog)
                        {
                            sw.WriteLine(line);
                        }

                        sw.WriteLine("</pre></body></html>");
                    }
                    break;
                case "/?disableWifi":
                    Log(" ***** Disabling wifi next boot, remove and re-insert SD card to reboot it");
                    File.Create("I:\\disableWifi");

                    Redirect(context, "/");
                    break;
                case "/showdown.min.js":
                    // Special case, this js library is pre-gzipped
                    context.Response.Headers.Add("Content-Encoding", "gzip");
                    context.Response.ContentType = "text/javascript";

                    context.Response.OutputStream.Write(showdownJs, 0, showdownJs.Length);
                    break;
                case "/log":
                    context.Response.ContentType = "text/plain";
                    using (var sw = new StreamWriter(context.Response.OutputStream))
                    {
                        foreach (string line in Program.webLog)
                        {
                            sw.WriteLine(line);
                        }
                    }
                    break;
                default:
                    SendEmptyResponse(context, HttpStatusCode.NotFound);
                    break;
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

        private void SendIndex(HttpListenerContext context)
        {
            using (var sw = new StreamWriter(context.Response.OutputStream))
            {
                sw.Write(WebFiles.GetString(WebFiles.StringResources.index));
            }
        }
    }
}
