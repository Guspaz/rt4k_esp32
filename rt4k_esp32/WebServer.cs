using System;
using System.Net;
using System.Threading;

namespace rt4k_esp32
{
    internal class WebServer
    {
        public delegate void RouteDelegate(HttpListenerContext context);
        internal delegate void LogDelegate(string message);

        private readonly LogDelegate Log;
        private readonly int Port;
        private readonly string Name;
        private readonly RouteDelegate Route;
        private HttpListener httpListener;

        public WebServer(LogDelegate log, int port, string name, RouteDelegate route)
        {
            this.Log = log;
            this.Port = port;
            this.Name = name;
            this.Route = route;

            this.Start();
        }

        private void HandleContext()
        {
            HttpListenerContext context;

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
                context.Response.Close();
                context.Close();
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
    }
}
