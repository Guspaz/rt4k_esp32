using System;
using System.IO;
using System.Net;
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

        internal FileManager(LogDelegate logFunc, SdManager sdManager)
        {
            Log = logFunc;

            Log("FileManager starting up");

            this.sdManager = sdManager;
        }

        internal string GetFile(string path, bool instantRelease = false)
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
                Log($"EXCEPTION: [{Thread.CurrentThread.ManagedThreadId}] GetFile(\"{path}\")");
                LogException(ex);
                return null;
            }
            finally
            {
                ReleaseSD(instantRelease);
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

        internal void WriteFileToSdCard(string path, HttpListenerRequest request)
        {
            try
            {
                bool wifiUpdate = path.ToLower().Trim('/') == "wifi.ini";

                path = PathToSd(path);
                GrabSD();

                using (var file = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    byte[] buffer = new byte[4096];
                    int read;
                    long fileSize = request.ContentLength64;
                    int totalRead = 0;

                    while (totalRead < fileSize && (read = request.InputStream.Read(buffer, 0, Math.Min(4096, (int)(fileSize-totalRead)))) != 0)
                    {
                        file.Write(buffer, 0, read);
                        totalRead += read;
                    }
                }

                if (wifiUpdate)
                {
                    Log("Detected wifi.ini update via WebDAV");
                    WifiIniUpdated?.Invoke(this, null);
                }
            }
            catch (Exception ex)
            {
                Log($"EXCEPTION: [{Thread.CurrentThread.ManagedThreadId}] WriteFileToSdCard(\"{path}\", request)");
                LogException(ex);
            }
            finally
            {
                ReleaseSD();
            }
        }

        internal void WriteFileToHttpResponse(string path, HttpListenerResponse response, bool sendFile = true)
        {
            try
            {
                path = PathToSd(path);
                response.ContentType = "application/octet-stream";
                GrabSD();

                using (var file = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    response.ContentLength64 = file.Length;

                    if (sendFile)
                    {
                        byte[] buffer = new byte[4 * 1024];
                        int read;

                        while ((read = file.Read(buffer, 0, 4 * 1024)) != 0)
                        {
                            response.OutputStream.Write(buffer, 0, read);
                        }
                    }
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

        internal string[] GetDirectories(string path)
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
                Log($"EXCEPTION: [{Thread.CurrentThread.ManagedThreadId}] GetDirectories(\"{path}\")");
                LogException(ex);
                return null;
            }
            finally
            {
                ReleaseSD();
            }
        }

        internal string[] GetFiles(string path)
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
                Log($"EXCEPTION: [{Thread.CurrentThread.ManagedThreadId}] GetFiles(\"{path}\")");
                LogException(ex);
                return null;
            }
            finally
            {
                ReleaseSD();
            }
        }

        internal FileProperties GetFileProperties(string path)
        {
            try
            {
                path = PathToSd(path);
                GrabSD();

                var fileProperties = new FileProperties();

                // TODO: There's got to be a better way to do this.
                using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
                    fileProperties.FileSize = fileStream.Length;

                //var storageFile = StorageFile.GetFileFromPath(path);

                // TODO: Figure out how to get created dates, this doesn't .
                //fileProperties.CreatedDate = storageFile.DateCreated;
                //fileProperties.ContentType = storageFile.ContentType;
                fileProperties.LastModifiedDate = File.GetLastWriteTime(path);

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
                    //fileProperties.CreatedDate = DateTime.MinValue; // TODO: Figure out how to get directory created dates
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

        private void GrabSD() => sdManager.GrabSD();
        private void ReleaseSD(bool instantRelease = false) => sdManager.ReleaseSD(instantRelease);

        private string PathToSd(string path)
        {
            return Path.Combine(ROOT_PATH + "\\", path.Trim('/').Replace('/', '\\'));
        }

        private string SdToPath(string path)
        {
            return path.Substring(ROOT_PATH.Length).Replace('\\', '/');
        }
    }
}