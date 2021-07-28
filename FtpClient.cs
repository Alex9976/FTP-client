using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

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
            //ftpRequest.Timeout = 7000;

            string response;
            using (ftpResponse = (FtpWebResponse)ftpRequest.GetResponse())
            {
                using (StreamReader stream = new StreamReader(ftpResponse.GetResponseStream(), Encoding.ASCII))
                    response = stream.ReadToEnd();
            }

            DirectoryParser parser = new DirectoryParser(response);
            return parser.FullListing;
        }

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
                                //byte[] buf = new byte[size];
                                //Array.Copy(buffer, 0, buf, 0, size);
                                //await FileIO.AppendTextAsync(file, Encoding.ASCII.GetString(buf));
                                fileStream.Write(buffer, 0, size);
                                bytesRead += size;
                                progress = (int)(((float)bytesRead / fileSize) * 100);
                            }
                        }
                    }
                }
                progress = 100;
            }
            catch (Exception ex)
            {
                //throw new Exception("Download error");
            }
        }

        public async void UploadFile(string path, StorageFile file)
        {
            try
            {
                Windows.Storage.FileProperties.BasicProperties basicProperties = await file.GetBasicPropertiesAsync();
                long fileSize = (long)basicProperties.Size;
                ftpRequest = (FtpWebRequest)WebRequest.Create(Uri.EscapeUriString("ftp://" + Host + path + "/" + file.DisplayName));
                ftpRequest.Credentials = new NetworkCredential(Login, Password);
                ftpRequest.Method = WebRequestMethods.Ftp.UploadFile;
                ftpRequest.EnableSsl = UseSSL;
                ftpRequest.UseBinary = true;
                ftpRequest.UsePassive = true;
                ftpRequest.KeepAlive = true;

                //IBuffer buffer = await FileIO.ReadBufferAsync(file);

                using (Stream stream = await file.OpenStreamForReadAsync())
                {
                    using (Stream requestStream = ftpRequest.GetRequestStream())
                    {
                        stream.CopyTo(requestStream);
                    }
                }
               
                using (ftpResponse = (FtpWebResponse)ftpRequest.GetResponse())
                {
                    //var a = ftpResponse.StatusDescription;
                }

            }
            catch
            {
                //throw new Exception("Upload error");
            }
        }

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

        public void CreateDirectory(string path, string folderName)
        {
            FtpWebRequest ftpRequest = (FtpWebRequest)WebRequest.Create("ftp://" + Host + path + "/" + folderName);

            ftpRequest.Credentials = new NetworkCredential(Login, Password);
            ftpRequest.EnableSsl = UseSSL;
            ftpRequest.Method = WebRequestMethods.Ftp.MakeDirectory;

            FtpWebResponse ftpResponse = (FtpWebResponse)ftpRequest.GetResponse();
            ftpResponse.Close();
        }

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
