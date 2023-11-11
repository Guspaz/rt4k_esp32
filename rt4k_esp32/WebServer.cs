using System;
using System.Collections;
using System.Net;
using System.Text;
using System.Threading;
using System.Web;

namespace rt4k_esp32
{
    internal abstract class WebServer
    {
        internal delegate void LogDelegate(string message);

        protected readonly LogDelegate Log;
        private readonly int Port;
        private readonly string Name;
        private HttpListener httpListener;
        private HttpListenerContext context;

        public WebServer(LogDelegate log, int port, string name)
        {
            this.Log = log;
            this.Port = port;
            this.Name = name;

            this.Start();
        }

        protected abstract void Route(HttpListenerContext context);

        private void HandleContext()
        {
            // First, wait for a request
            try
            {
                context = httpListener.GetContext();
            }
            catch (Exception ex)
            {
                Log($"[{Thread.CurrentThread.ManagedThreadId}:{Name}] Uncaught Exception in HttpListener.GetContext()");
                Log(ex.ToString());
                throw;
            }

            // Second, service the request
            try
            {
                Log($"{Name}: {context.Request.HttpMethod} {context.Request.RawUrl}");
                Route(context);

                if (context.Response.StatusCode != (int)HttpStatusCode.OK)
                {
                    Log($"Response: {context.Response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Log($"[{Thread.CurrentThread.ManagedThreadId}:{Name}] Uncaught Exception in {Name}.Route()");
                Log(ex.ToString());
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }

            // Third, clean up after the request
            try
            {
                context?.Response?.Close();
                context?.Close();
            }
            catch (Exception ex)
            {
                Log("Exception closing HttpListenerContext");
                Log(ex.Message);
                Log(ex.StackTrace);
                if (ex.InnerException != null)
                {
                    Log("Inner Exception:");
                    Log(ex.InnerException.Message);
                    Log(ex.InnerException.StackTrace);
                }
            }
        }

        private void Start()
        {
            new Thread(() =>
            {
                Log($"Starting {Name} server on port {Port}");

                while (true)
                {
                    httpListener = new HttpListener("http", Port);
                    httpListener.Start();

                    Log($"{Name} server started");

                    while (httpListener.IsListening)
                    {
                        try
                        {
                            HandleContext();
                        }
                        catch
                        {
                            break;
                        }
                    }

                    // Something went wrong, prepare to restart
                    Log($"{Name} server failed, restarting");

                    try
                    {
                        httpListener?.Stop();
                        httpListener?.Abort();
                    }
                    catch { }
                }
            }).Start();
        }

        protected string ReadRequest()
        {
            byte[] buf = new byte[context.Request.ContentLength64];
            context.Request.InputStream.Read(buf, 0, buf.Length);
            return Encoding.UTF8.GetString(buf, 0, buf.Length);
        }

        protected static Hashtable ParseUrlParams(string postData)
        {
            var formData = new Hashtable();

            foreach (var pair in postData.Split('&'))
            {
                var keyValue = pair.Split('=');
                if (keyValue.Length == 2)
                {
                    formData.Add(keyValue[0], HttpUtility.UrlDecode(keyValue[1], Encoding.UTF8));
                }
            }

            return formData;
        }
    }
}
