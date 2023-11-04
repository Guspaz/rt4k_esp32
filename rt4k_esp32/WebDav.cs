﻿
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace rt4k_esp32
{
    internal class WebDav
    {
        internal delegate void LogDelegate(string message);

        private readonly FileManager fm;
        private readonly LogDelegate Log;

        public WebDav(FileManager fileManager, LogDelegate log)
        {
            fm = fileManager;
            Log = log;
        }

        private void SendEmptyResponse(HttpListenerContext context, HttpStatusCode statusCode)
        {
            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentLength64 = 0;
        }

        public void Route(HttpListenerContext context)
        {
            if (context.Request.Headers["Depth"] != null) { Log($"Depth: {context.Request.Headers["Depth"]}"); }
            if (context.Request.Headers["Range"] != null) { Log($"Range: {context.Request.Headers["Range"]}"); }
            if (context.Request.Headers["Destination"] != null) { Log($"Destination: {context.Request.Headers["Destination"]}"); }
            if (context.Request.ContentLength64 > 0) { Log($"ContentLength: {context.Request.ContentLength64}"); }
            context.Response.KeepAlive = false;
            context.Response.Headers.Add("DAV", "1");
            //context.Response.Headers.Add("Accept-Ranges", "bytes");

            //foreach (var headerName in e.Context.Request.Headers.AllKeys)
            //{
            //    Log($"{headerName}: {e.Context.Request.Headers[headerName]}");
            //}

            switch (context.Request.HttpMethod)
            {
                case "GET":
                    Get(context);
                    return;
                case "HEAD":
                    Get(context, true);
                    return;
                case "DELETE":
                    Delete(context);
                    return;
                case "PUT":
                    Put(context);
                    return;
                case "MKCOL":
                    MkCol(context);
                    return;
                case "LOCK":
                    Lock(context);
                    return;
                case "UNLOCK":
                    Unlock(context);
                    return;
                case "PROPFIND":
                    PropFind(context);
                    return;
                case "PROPPATCH":
                    PropPatch(context);
                    return;
                case "MOVE":
                    Move(context);
                    return;
                case "OPTIONS":
                    Options(context);
                    return;
                default:
                    SendEmptyResponse(context, HttpStatusCode.MethodNotAllowed);
                    return;
            }
        }

        private void PropPatch(HttpListenerContext context)
        {
            // We basically don't have any properties we can set, so just fake the response
            // TODO: This is dangerous, reading a huge string buffer!
            byte[] buf = new byte[context.Request.ContentLength64];
            context.Request.InputStream.Read(buf, 0, buf.Length);
            string input = Encoding.UTF8.GetString(buf, 0, buf.Length);

            // We don't have any XML parsers. So just use a regex to grab the property names
            var properties = Regex.Matches(input, "<Z:(\\w+)>");
            var path = GetPath(context);

            var sw = new StreamWriter(context.Response.OutputStream);
            
            sw.WriteLine( "<D:multistatus xmlns:D=\"DAV:\" xmlns:Z=\"urn:schemas-microsoft-com:\">");
            sw.WriteLine( "  <D:response>");
            sw.WriteLine($"    <D:href>http://192.168.1.34:81{EncodePath(path)}</D:href>");
            sw.WriteLine( "    <D:propstat>");
            sw.WriteLine( "      <D:status>HTTP/1.1 200 OK</D:status>");
            sw.WriteLine( "      <D:prop>");
            foreach (Match prop in properties)
            {
                sw.WriteLine($"        <Z:{prop.Groups[1].Value}/>");
                Log($"PROP: {prop.Groups[1].Value}");
            }
            sw.WriteLine( "      </D:prop>");
            sw.WriteLine( "    </D:propstat>");
            sw.WriteLine( "  </D:response>");
            sw.WriteLine( "</D:multistatus>");
            sw.Flush();
        }

        private void Move(HttpListenerContext context)
        {
            var path = GetPath(context);

            if (string.IsNullOrEmpty(context.Request.Headers["Destination"]))
            {
                SendEmptyResponse(context, HttpStatusCode.BadRequest);
                return;
            }

            string destination = HttpUtility.UrlDecode(new Uri(context.Request.Headers["Destination"]).AbsolutePath);

            if (path == destination)
            {
                SendEmptyResponse(context, HttpStatusCode.Forbidden);
                return;
            }

            if (!fm.DirectoryExists(fm.GetDirectoryName(destination)))
            {
                SendEmptyResponse(context, HttpStatusCode.Conflict);
                return;
            }

            if (fm.FileExists(path))
            {
                fm.MoveFile(path, destination);
                context.Response.Headers.Add("Location", context.Request.Headers["Destination"]);
                SendEmptyResponse(context, HttpStatusCode.NoContent);
            }
            else if (fm.DirectoryExists(path))
            {
                fm.MoveDirectory(path, destination);
                context.Response.Headers.Add("Location", context.Request.Headers["Destination"]);
                SendEmptyResponse(context, HttpStatusCode.NoContent);
            }
            else
            {
                SendEmptyResponse(context, HttpStatusCode.NotFound);
            }
        }

        private void MkCol(HttpListenerContext context)
        {
            var path = GetPath(context);

            if (fm.DirectoryExists(path))
            {
                // TODO: What's the right answer here?
                SendEmptyResponse(context, HttpStatusCode.Conflict);
            }
            else
            {
                fm.CreateDirectory(path);
                SendEmptyResponse(context, HttpStatusCode.Created);
            }
        }

        private void Delete(HttpListenerContext context)
        {
            var path = GetPath(context);

            if (fm.FileExists(path))
            {
                fm.DeleteFile(path);
                SendEmptyResponse(context, HttpStatusCode.NoContent);
            }
            else if (fm.DirectoryExists(path))
            {
                fm.DeleteDirectory(path);
                SendEmptyResponse(context, HttpStatusCode.NoContent);
            }
            else
            {
                SendEmptyResponse(context, HttpStatusCode.NotFound);
            }
        }

        private void Lock(HttpListenerContext context)
        {
            // We don't actually support locking, so just give Windows a random Guid to pretend we do.
            context.Response.ContentType = "text/xml";
            var sw = new StreamWriter(context.Response.OutputStream);

            sw.WriteLine("<D:prop xmlns:D=\"DAV:\">");
            sw.WriteLine("  <D:lockdiscovery>");
            sw.WriteLine("    <D:activelock>");
            sw.WriteLine($"      <D:locktoken><D:href>urn:uuid:{Guid.NewGuid()}</D:href></D:locktoken>");
            sw.WriteLine("    </D:activelock>");
            sw.WriteLine("  </D:lockdiscovery>");
            sw.WriteLine("</D:prop>");
            sw.Flush();
        }

        private void Unlock(HttpListenerContext context)
        {
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
        }

        private void Get(HttpListenerContext context, bool headMode = false)
        {
            var path = GetPath(context);

            if (fm.FileExists(path))
            {
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                fm.WriteFileToHttpResponse(path, context.Response, !headMode);
                context.Response.OutputStream.Flush();
            }
            else
            {
                SendEmptyResponse(context, HttpStatusCode.NotFound);
            }
        }

        private void Put(HttpListenerContext context)
        {
            var path = GetPath(context);

            context.Response.StatusCode = (int)HttpStatusCode.Created;
            fm.WriteFileToSdCard(path, context.Request);
            context.Response.OutputStream.Flush();
        }

        private void PropFind(HttpListenerContext context)
        {
            var path = GetPath(context);
            context.Response.ContentType = "text/xml";

            var depthHeader = context.Request.Headers["Depth"];
            bool includeChildren = depthHeader == "1" || depthHeader == "infinity";

            var sw = new StreamWriter(context.Response.OutputStream);

            sw.WriteLine("<D:multistatus xmlns:D=\"DAV:\">");

            if (fm.DirectoryExists(path))
            {
                AppendResponseForItem(sw, path, true); // For the directory itself

                if (includeChildren)
                {
                    foreach (var dir in fm.GetDirectories(path))
                    {
                        AppendResponseForItem(sw, dir, true);
                    }
                    foreach (var file in fm.GetFiles(path))
                    {
                        AppendResponseForItem(sw, file, false);
                    }
                }
            }
            else if (fm.FileExists(path))
            {
                AppendResponseForItem(sw, path, false);
            }
            else
            {
                SendEmptyResponse(context, HttpStatusCode.NotFound);
                return;
            }

            sw.WriteLine("</D:multistatus>");
            sw.Flush();
        }

        private void AppendResponseForItem(StreamWriter sw, string path, bool isDirectory)
        {
            var fp = isDirectory ? fm.GetDirectoryProperties(path) : fm.GetFileProperties(path);

            sw.WriteLine($"  <D:response>");
            sw.WriteLine($"    <D:href>http://192.168.1.34:81{EncodePath(path)}{(isDirectory ? "/" : "")}</D:href>");
            sw.WriteLine($"    <D:propstat>");
            sw.WriteLine($"      <D:prop>");
            //sb.AppendLine($"        <D:creationdate>{fp.CreatedDate.ToString("o")}</D:creationdate>");
            sw.WriteLine($"        <D:getlastmodified>{fp.LastModifiedDate.ToString("R")}</D:getlastmodified>");
            //sb.AppendLine($"        <D:displayname>{Path.GetFileName(path)}</D:displayname>");
            if (!isDirectory)
            {
                sw.WriteLine($"        <D:getcontentlength b:dt=\"int\">{fp.FileSize}</D:getcontentlength>");
                // TODO: Do MIME types matter?
                sw.WriteLine($"        <D:getcontenttype>application/octet-stream</D:getcontenttype>");
            }
            sw.WriteLine($"        <D:resourcetype>{(isDirectory ? "<D:collection/>" : "")}</D:resourcetype>");
            sw.WriteLine($"      </D:prop>");
            sw.WriteLine($"      <D:status>HTTP/1.1 200 OK</D:status>");
            sw.WriteLine($"    </D:propstat>");
            sw.WriteLine($"  </D:response>");
        }

        private void Options(HttpListenerContext context)
        {
            context.Response.Headers.Add("Allow", "OPTIONS, GET, HEAD, PUT, LOCK, UNLOCK, PROPFIND, PROPPATCH, DELETE, MKCOL, MOVE");
        }

        private string EncodePath(string path)
        {
            return HttpUtility.UrlEncode(path).Replace("+", "%20").Replace("%2F", "/").TrimEnd('/');
        }

        private string GetPath(HttpListenerContext context)
        {
            return HttpUtility.UrlDecode(context.Request.RawUrl);
        }
    }
}