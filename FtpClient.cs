using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using Windows.Storage;

namespace FTP_client
{
    class FtpClient
    {
        public string Host { get; set; }
        public string Login { get; set; }
        public string Password { get; set; }
        private FtpWebRequest ftpRequest { get; set; }
        private FtpWebResponse ftpResponse { get; set; }
        public bool UseSSL { get; set; } = false;
        public string CurrentDirectory { get; set; } = "";
        private readonly int BUFFER_SIZE = 4096 * 2;
        public int progress { get; set; } = 0;
        public delegate void ProgressStatusUpdate();
        public event ProgressStatusUpdate Notify;
        public event ProgressStatusUpdate TransferComplete;

        /// <summary>
        /// LIST method
        /// </summary>
        /// <param name="path">Directory path</param>
        /// <returns>Array of files and directories</returns>
        public ItemInfo[] ListDirectory(string path)
        {
            path = string.IsNullOrWhiteSpace(path) ? "/" : path;
            ftpRequest = (FtpWebRequest)WebRequest.Create(Uri.EscapeUriString("ftp://" + Host + path));
            ftpRequest.Credentials = new NetworkCredential(Login, Password);
            ftpRequest.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
            ftpRequest.EnableSsl = UseSSL;
            ftpRequest.UseBinary = true;
            ftpRequest.UsePassive = true;
            ftpRequest.KeepAlive = true;

            string response;
            using (ftpResponse = (FtpWebResponse)ftpRequest.GetResponse())
            {
                using (StreamReader stream = new StreamReader(ftpResponse.GetResponseStream(), Encoding.ASCII))
                    response = stream.ReadToEnd();
            }

            DirectoryParser parser = new DirectoryParser(response);
            return parser.FullListing;
        }

        /// <summary>
        /// RETR method
        /// </summary>
        /// <param name="path">Directory path</param>
        /// <param name="fileName">File name</param>
        /// <param name="file">StorageFile object</param>
        /// <param name="fileSize">Size of downloading file</param>
        public async void DownloadFile(string path, string fileName, StorageFile file, long fileSize)
        {
            try
            {
                ftpRequest = (FtpWebRequest)WebRequest.Create(Uri.EscapeUriString("ftp://" + Host + path + "/" + fileName));
                ftpRequest.Credentials = new NetworkCredential(Login, Password);
                ftpRequest.Method = WebRequestMethods.Ftp.DownloadFile;
                ftpRequest.EnableSsl = UseSSL;
                ftpRequest.UseBinary = true;
                ftpRequest.UsePassive = true;
                ftpRequest.KeepAlive = true;
                
                using (ftpResponse = (FtpWebResponse)ftpRequest.GetResponse())
                {
                    progress = 0;
                    using (Stream responseStream = ftpResponse.GetResponseStream())
                    {
                        byte[] buffer = new byte[BUFFER_SIZE];
                        int size;
                        int bytesRead = 0;
                        using (Stream fileStream = await file.OpenStreamForWriteAsync())
                        {
                            while ((size = responseStream.Read(buffer, 0, BUFFER_SIZE)) > 0)
                            {
                                fileStream.Write(buffer, 0, size);
                                bytesRead += size;
                                progress = (int)((float)bytesRead / fileSize * 100);
                                Notify();
                            }
                        }
                    }
                }
                progress = 100;
                Notify();
                TransferComplete();
            }
            catch
            {
                Debug.WriteLine("Download error");
            }
        }

        /// <summary>
        /// STOR method
        /// </summary>
        /// <param name="path">Directory path</param>
        /// <param name="file">StorageFile object</param>
        public async void UploadFile(string path, StorageFile file)
        {
            try
            {
                Windows.Storage.FileProperties.BasicProperties basicProperties = await file.GetBasicPropertiesAsync();
                long fileSize = (long)basicProperties.Size;
                ftpRequest = (FtpWebRequest)WebRequest.Create(Uri.EscapeUriString("ftp://" + Host + path + "/" + file.Name));
                ftpRequest.Credentials = new NetworkCredential(Login, Password);
                ftpRequest.Method = WebRequestMethods.Ftp.UploadFile;
                ftpRequest.EnableSsl = UseSSL;
                ftpRequest.UseBinary = true;
                ftpRequest.UsePassive = true;
                ftpRequest.KeepAlive = true;

                using (Stream stream = await file.OpenStreamForReadAsync())
                {
                    using (Stream requestStream = ftpRequest.GetRequestStream())
                    {
                        stream.CopyTo(requestStream);
                    }
                }
                ftpResponse = (FtpWebResponse)ftpRequest.GetResponse();
            }
            catch
            {
                Debug.WriteLine("Upload error");
            }
        }

        /// <summary>
        /// RMD & DELE methods
        /// </summary>
        /// <param name="path">File/directory path</param>
        /// <param name="itemType">Item type (file or directory)</param>
        public void Delete(string path, ItemType itemType)
        {
            ftpRequest = (FtpWebRequest)WebRequest.Create("ftp://" + Host + path);
            ftpRequest.Credentials = new NetworkCredential(Login, Password);
            ftpRequest.EnableSsl = UseSSL;

            if (itemType == ItemType.File)
                ftpRequest.Method = WebRequestMethods.Ftp.DeleteFile;
            else
                ftpRequest.Method = WebRequestMethods.Ftp.RemoveDirectory;

            FtpWebResponse ftpResponse = (FtpWebResponse)ftpRequest.GetResponse();
            ftpResponse.Close();
        }

        /// <summary>
        /// RENAME method
        /// </summary>
        /// <param name="path">File/directory path</param>
        /// <param name="name">New name</param>
        public void Rename(string path, string name)
        {
            ftpRequest = (FtpWebRequest)WebRequest.Create("ftp://" + Host + path);
            ftpRequest.Credentials = new NetworkCredential(Login, Password);
            ftpRequest.EnableSsl = UseSSL;
            ftpRequest.Method = WebRequestMethods.Ftp.Rename;
            ftpRequest.RenameTo = name;

            FtpWebResponse ftpResponse = (FtpWebResponse)ftpRequest.GetResponse();
            ftpResponse.Close();
        }

        /// <summary>
        /// MKD method
        /// </summary>
        /// <param name="path">Directory path</param>
        /// <param name="folderName">Name of the new directory</param>
        public void CreateDirectory(string path, string folderName)
        {
            FtpWebRequest ftpRequest = (FtpWebRequest)WebRequest.Create("ftp://" + Host + path + "/" + folderName);

            ftpRequest.Credentials = new NetworkCredential(Login, Password);
            ftpRequest.EnableSsl = UseSSL;
            ftpRequest.Method = WebRequestMethods.Ftp.MakeDirectory;

            FtpWebResponse ftpResponse = (FtpWebResponse)ftpRequest.GetResponse();
            ftpResponse.Close();
        }

        /// <summary>
        /// Returns the path of the parent directory
        /// </summary>
        /// <param name="path">Directory path</param>
        /// <returns>Parent directory</returns>
        public string GetParentDirectory(string path)
        {
            path.TrimEnd('/');
            if (path.LastIndexOf('/') >= 0)
            {
                path = path.Remove(path.LastIndexOf('/'));
            }
            else
            {
                path = string.Empty;
            }

            return path;
        }
    }
}
