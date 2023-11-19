using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;

namespace rt4k_esp32
{
    internal class FileManager
    {
        internal delegate void LogDelegate(string message);
        internal event EventHandler WifiIniUpdated;

        private const string ROOT_PATH = "D:";

        private readonly LogDelegate Log;
        private readonly SdManager sdManager;
        private readonly Stack writeQueue = new();
        private readonly FileStream dummyFileStream;
        private readonly MethodInfo getLengthNative;
        private readonly MethodInfo readNative;
        private readonly MethodInfo writeNative;

        internal FileManager(LogDelegate logFunc, SdManager sdManager)
        {
            Log = logFunc;

            Log("FileManager starting up");

            this.sdManager = sdManager;

            // We just need a FileStream to reflect into
            dummyFileStream = new("I:\\dummyFile", FileMode.OpenOrCreate);
            getLengthNative = dummyFileStream.GetType().GetMethod("GetLengthNative", BindingFlags.NonPublic | BindingFlags.Instance);
            readNative = dummyFileStream.GetType().GetMethod("ReadNative", BindingFlags.NonPublic | BindingFlags.Instance);
            writeNative = dummyFileStream.GetType().GetMethod("WriteNative", BindingFlags.NonPublic | BindingFlags.Instance);

            Log("FileManager started");
        }

        internal string ReadFile(string path, bool instantRelease = false)
        {
            try
            {
                path = PathToSd(path);
                GrabSD();

                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (StreamReader sr = new(fs))
                {
                    return sr.ReadToEnd();
                }

            }
            catch (Exception ex)
            {
                Log($"EXCEPTION: [{Thread.CurrentThread.ManagedThreadId}] ReadFile(\"{path}\")");
                LogException(ex);
                return null;
            }
            finally
            {
                ReleaseSD(instantRelease);
            }
        }

        internal byte[] ReadFileRaw(string path)
        {
            {
                try
                {
                    path = PathToSd(path);
                    GrabSD();

                    using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                    {
                        var buf = new byte[fs.Length];
                        fs.Read(buf, 0, buf.Length);
                        return buf;
                    }

                }
                catch (Exception ex)
                {
                    Log($"EXCEPTION: [{Thread.CurrentThread.ManagedThreadId}] ReadFileRaw(\"{path}\")");
                    LogException(ex);
                    return null;
                }
                finally
                {
                    ReleaseSD();
                }
            }
        }

        internal void WriteFileRaw(string path, byte[] data)
        {
            {
                try
                {
                    path = PathToSd(path);
                    GrabSD();

                    using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                    {
                        fs.Write(data, 0, data.Length);
                    }

                }
                catch (Exception ex)
                {
                    Log($"EXCEPTION: [{Thread.CurrentThread.ManagedThreadId}] WriteFileRaw(\"{path}\")");
                    LogException(ex);
                }
                finally
                {
                    ReleaseSD();
                }
            }
        }

        internal void WriteFileInternal(string path, string content)
        {
            try
            {
                path = PathToSd(path);

                using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                using (StreamWriter sw = new(fs))
                {
                    sw.Write(content);
                }

            }
            catch (Exception ex)
            {
                Log($"EXCEPTION: [{Thread.CurrentThread.ManagedThreadId}] WriteFile(\"{path}\")");
                LogException(ex);
            }
        }

        private void LogException(Exception ex)
        {
            Log(ex.Message);
            Log(ex.StackTrace);
            if (ex.InnerException != null)
            {
                Log("Inner Exception:");
                Log(ex.InnerException.Message);
                Log(ex.InnerException.StackTrace);
            }
        }

        internal void DeleteFile(string path)
        {
            try
            {
                path = PathToSd(path);
                GrabSD();
                File.Delete(path);
            }
            catch (Exception ex)
            {
                Log($"EXCEPTION: [{Thread.CurrentThread.ManagedThreadId}] DeleteFile(\"{path}\")");
                LogException(ex);
            }
            finally
            {
                ReleaseSD();
            }
        }

        internal void DeleteDirectory(string path)
        {
            try
            {
                path = PathToSd(path);
                GrabSD();
                Directory.Delete(path, true);
            }
            catch (Exception ex)
            {
                Log($"EXCEPTION: [{Thread.CurrentThread.ManagedThreadId}] DeleteDirectory(\"{path}\")");
                LogException(ex);
            }
            finally
            {
                ReleaseSD();
            }
        }

        internal void MoveDirectory(string path, string destination)
        {
            try
            {
                path = PathToSd(path);
                destination = PathToSd(destination);
                GrabSD();
                Directory.Move(path, destination);
            }
            catch (Exception ex)
            {
                Log($"EXCEPTION: [{Thread.CurrentThread.ManagedThreadId}] DeleteDirectory(\"{path}\")");
                LogException(ex);
            }
            finally
            {
                ReleaseSD();
            }
        }

        internal void MoveFile(string path, string destination)
        {
            try
            {
                path = PathToSd(path);
                destination = PathToSd(destination);
                GrabSD();
                File.Move(path, destination);
            }
            catch (Exception ex)
            {
                Log($"EXCEPTION: [{Thread.CurrentThread.ManagedThreadId}] DeleteDirectory(\"{path}\")");
                LogException(ex);
            }
            finally
            {
                ReleaseSD();
            }
        }

        // TODO: Benchmark against native implementation
        internal int MathMin(int left, int right) => left < right ? left : right;

        internal void WriteFileToSdCard(string path, HttpListenerRequest request)
        {
            try
            {
                bool wifiUpdate = path.ToLower().Trim('/') == "wifi.ini";

                path = PathToSd(path);
                GrabSD();

                string nativePath = Path.GetDirectoryName(path);
                string nativeFilename = Path.GetFileName(path);
                byte[] buf = new byte[65536];
                int read;
                int fileSize = (int)request.ContentLength64;
                int position = 0;

                // We've bypassed the FileStream to avoid its write overhead, so we need to create the file ourselves
                File.Create(path);
                while (position < fileSize && (read = FillBuffer(buf, request.InputStream, MathMin(65536, fileSize - position))) != 0)
                {
                    WriteInternal(nativePath, nativeFilename, buf, position, read);
                    position += read;
                }

                if (wifiUpdate)
                {
                    Log("Detected wifi.ini update via WebDAV");
                    WifiIniUpdated?.Invoke(this, null);
                }
            }
            catch (Exception ex)
            {
                // TODO: LogException should in general send 500 or something
                Log($"EXCEPTION: [{Thread.CurrentThread.ManagedThreadId}] WriteFileToSdCard(\"{path}\", request)");
                LogException(ex);
            }
            finally
            {
                ReleaseSD();
            }
        }

        internal void WriteFileToHttpResponse(string path, HttpListenerContext context, bool sendFile = true)
        {
            try
            {
                path = PathToSd(path);
                context.Response.ContentType = "application/octet-stream";
                GrabSD();

                context.Response.ContentLength64 = GetLengthInternal(path);
                DateTime lastModified = File.GetLastWriteTime(path);
                context.Response.Headers.Add("Last-Modified", lastModified.ToString("R"));

                string ifModifiedHeader = context.Request.Headers["If-Modified-Since"];
                DateTime ifModifiedDate = DateTime.MinValue;
                bool hasIfModified = !string.IsNullOrEmpty(ifModifiedHeader) && DateTime.TryParse(ifModifiedHeader, out ifModifiedDate);

                if (hasIfModified && lastModified <= ifModifiedDate)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotModified;
                    return;
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                }

                if (!sendFile)
                {
                    return;
                }

                byte[] buf = new byte[65536];
                int read = -1;
                int pos = 0;
                string nativePath = Path.GetDirectoryName(path);
                string nativeFilename = Path.GetFileName(path);
                while ((read = (int)ReadInternal(nativePath, nativeFilename, buf, pos, buf.Length)) != 0)
                {
                    context.Response.OutputStream.Write(buf, 0, read);
                    pos += read;
                }
            }
            catch (Exception ex)
            {
                Log($"EXCEPTION: [{Thread.CurrentThread.ManagedThreadId}] WriteFileToHttpResponse(\"{path}\", response, {sendFile})");
                LogException(ex);
            }
            finally
            {
                ReleaseSD();
            }
        }

        internal bool FileExists(string path)
        {
            try
            {
                path = PathToSd(path);
                GrabSD();
                return File.Exists(path);
            }
            catch (Exception ex)
            {
                Log($"EXCEPTION: [{Thread.CurrentThread.ManagedThreadId}] FileExists(\"{path}\")");
                LogException(ex);
                return false;
            }
            finally
            {
                ReleaseSD();
            }
        }

        internal bool DirectoryExists(string path)
        {
            try
            {
                path = PathToSd(path);
                GrabSD();
                return Directory.Exists(path);
            }
            catch (Exception ex)
            {
                Log($"EXCEPTION: [{Thread.CurrentThread.ManagedThreadId}] DirectoryExists(\"{path}\")");
                Log(ex.Message);
                Log(ex.StackTrace);
                if (ex.InnerException != null)
                {
                    Log("Inner Exception");
                    Log(ex.InnerException.Message);
                    Log(ex.InnerException.StackTrace);
                }
                return false;
            }
            finally
            {
                ReleaseSD();
            }
        }

        internal string GetDirectoryName(string path)
        {
            try
            {
                path = PathToSd(path);
                return SdToPath(Path.GetDirectoryName(path));
            }
            catch (Exception ex)
            {
                Log($"EXCEPTION: [{Thread.CurrentThread.ManagedThreadId}] DirectoryExists(\"{path}\")");
                LogException(ex);
                throw;
            }
        }

        internal string[] ListDirectories(string path)
        {
            try
            {
                path = PathToSd(path);
                GrabSD();
                string[] results = Directory.GetDirectories(path);
                for (int i = 0; i < results.Length; i++) {
                    results[i] = SdToPath(results[i]);
                }
                return results;
            }
            catch (Exception ex)
            {
                Log($"EXCEPTION: [{Thread.CurrentThread.ManagedThreadId}] ListDirectories(\"{path}\")");
                LogException(ex);
                return null;
            }
            finally
            {
                ReleaseSD();
            }
        }

        internal string[] ListFiles(string path)
        {
            try
            {
                path = PathToSd(path);
                GrabSD();
                string[] results = Directory.GetFiles(path);
                for (int i = 0; i < results.Length; i++)
                {
                    results[i] = SdToPath(results[i]);
                }
                return results;
            }
            catch (Exception ex)
            {
                Log($"EXCEPTION: [{Thread.CurrentThread.ManagedThreadId}] ListFiles(\"{path}\")");
                LogException(ex);
                return null;
            }
            finally
            {
                ReleaseSD();
            }
        }

        internal string[] ListFilesRecursive(string path, string extension)
        {
            try
            {
                path = PathToSd(path);
                GrabSD();
                string[] results = ListFilesRecursiveInternal(path, extension);
                for (int i = 0; i < results.Length; i++)
                {
                    results[i] = SdToPath(results[i]);
                }
                return results;
            }
            catch (Exception ex)
            {
                Log($"EXCEPTION: [{Thread.CurrentThread.ManagedThreadId}] ListFilesRecursive(\"{path}\", \"{extension}\")");
                LogException(ex);
                return null;
            }
            finally
            {
                ReleaseSD();
            }
        }

        static string[] ListFilesRecursiveInternal(string rootPath, string extension)
        {
            ArrayList results = new ArrayList();
            Stack directories = new Stack();
            directories.Push(rootPath);

            while (directories.Count > 0)
            {
                string currentDir = (string)directories.Pop();

                foreach (string file in Directory.GetFiles(currentDir))
                {
                    if (file.EndsWith(extension))
                    {
                        results.Add(file);
                    }
                }

                foreach (string str in Directory.GetDirectories(currentDir))
                {
                    directories.Push(str);
                }
            }

            return (string[])results.ToArray(typeof(string));
        }

        internal FileProperties GetFileProperties(string path)
        {
            try
            {
                path = PathToSd(path);
                GrabSD();

                var fileProperties = new FileProperties
                {
                    FileSize = GetLengthInternal(path),

                    //var storageFile = StorageFile.GetFileFromPath(path);

                    // TODO: Figure out how to get created dates, this doesn't work.
                    //fileProperties.CreatedDate = storageFile.DateCreated;
                    //fileProperties.ContentType = storageFile.ContentType;
                    LastModifiedDate = File.GetLastWriteTime(path)
                };

                return fileProperties;
            }
            catch (Exception ex)
            {
                Log($"EXCEPTION: [{Thread.CurrentThread.ManagedThreadId}] GetFileProperties(\"{path}\")");
                LogException(ex);
                return null;
            }
            finally
            {
                ReleaseSD();
            }
        }

        internal FileProperties GetDirectoryProperties(string path)
        {
            try
            {
                path = PathToSd(path);
                GrabSD();

                return new FileProperties
                {
                    FileSize = 0,
                    LastModifiedDate = Directory.GetLastWriteTime(path)
                };
            }
            catch (Exception ex)
            {
                Log($"EXCEPTION: [{Thread.CurrentThread.ManagedThreadId}] GetFileProperties(\"{path}\")");
                LogException(ex);
                return null;
            }
            finally
            {
                ReleaseSD();
            }
        }

        internal void CreateDirectory(string path)
        {
            try
            {
                path = PathToSd(path);
                GrabSD();

                Directory.CreateDirectory(path);
            }
            catch (Exception ex)
            {
                Log($"EXCEPTION: [{Thread.CurrentThread.ManagedThreadId}] CreateDirectory(\"{path}\")");
                LogException(ex);
            }
            finally
            {
                ReleaseSD();
            }
        }

        internal bool CheckFileValue(string path, int address, byte[] value)
        {
            try
            {
                path = PathToSd(path);
                GrabSD();

                byte[] readBuf = new byte[value.Length];

                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    fs.Seek(address, SeekOrigin.Begin);
                    fs.Read(readBuf, 0, readBuf.Length);
                }

                for (int i = 0; i < value.Length; i++)
                {
                    if (value[i] != readBuf[i])
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Log($"EXCEPTION: [{Thread.CurrentThread.ManagedThreadId}] CheckFileValue(\"{path}\", {address}, value)");
                LogException(ex);
                return false;
            }
            finally
            {
                ReleaseSD();
            }
        }

        private void GrabSD()
        {
            sdManager.GrabSD();

            lock (this)
            {
                while (writeQueue.Count > 0)
                {
                    var queuedItem = (DictionaryEntry)writeQueue.Pop();
                    WriteFileInternal((string)queuedItem.Key, (string)queuedItem.Value);
                }
            }
        }
        private void ReleaseSD(bool instantRelease = false) => sdManager.ReleaseSD(instantRelease);

        private string PathToSd(string path) => Path.Combine(ROOT_PATH + "\\", path.Trim('/').Replace('/', '\\'));

        private string SdToPath(string path) => path.Substring(ROOT_PATH.Length).Replace('\\', '/');

        internal void QueueWrite(string path, string content) => writeQueue.Push(new DictionaryEntry(path, content));

        // This is roughly twice as fast as initializing a new FileStream every time
        internal long GetLengthInternal(string path)
        {
            return (long)getLengthNative.Invoke(dummyFileStream, new string[] { Path.GetDirectoryName(path), Path.GetFileName(path) });
        }

        internal long ReadInternal(string nativePath, string nativeFilename, byte[] buffer, long position, int count)
        {
            return (int)readNative.Invoke(dummyFileStream, new object[] { nativePath, nativeFilename, position, buffer, count });
        }

        internal void WriteInternal(string nativePath, string nativeFilename, byte[] buffer, long position, int count)
        {
            writeNative.Invoke(dummyFileStream, new object[] { nativePath, nativeFilename, position, buffer, count });
        }

        internal int FillBuffer(byte[] buffer, Stream stream, int maxRead)
        {
            int bufLen = MathMin(buffer.Length, maxRead);
            int read = -1;
            int pos = 0;

            while (pos < bufLen && read != 0)
            {
                read = stream.Read(buffer, pos, MathMin(bufLen, bufLen - pos));
                pos += read;
            }

            return pos;
        }
    }
}