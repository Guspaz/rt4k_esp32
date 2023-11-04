using System.IO;
using System.Net;

namespace rt4k_esp32
{
    internal class WebInterface
    {
        internal delegate void LogDelegate(string message);

        private readonly FileManager fm;
        private readonly LogDelegate Log;

        public WebInterface(FileManager fileManager, LogDelegate log)
        {
            fm = fileManager;
            Log = log;
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
                    SendIndex(context);
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

                    byte[] showdown = WebFiles.GetBytes(WebFiles.BinaryResources.showdown_min_js);
                    context.Response.OutputStream.Write(showdown, 0, showdown.Length);
                    break;
                case "/log":
                    using (var sw = new StreamWriter(context.Response.OutputStream))
                    {
                        sw.WriteLine("<pre>");

                        foreach (string line in Program.webLog)
                        {
                            sw.WriteLine(line);
                        }

                        sw.WriteLine("</pre>");
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
