using System.IO;
using System.Net;
using System.Reflection;
using System.Resources;
using System.Text;
using nanoFramework.Runtime.Native;

namespace rt4k_esp32
{
    internal class WebInterface
    {
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
                    }
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
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    break;
            }
        }
    }
}
