using Core.Interfaces;
using System.Net;

namespace Infrastructure.Utilities
{
    public static class FtpService
    {
        public static async Task<bool> DirectoryExistsAsync(string host, string path, string username, string password)
        {
            try
            {
                var uri = new Uri($"ftp://{host}{path}");

                var request = (FtpWebRequest)WebRequest.Create(uri);
                request.Method = WebRequestMethods.Ftp.ListDirectory;
                request.Credentials = new NetworkCredential(username, password);
                request.UsePassive = true;
                request.UseBinary = true;
                request.KeepAlive = false;

                using var response = (FtpWebResponse)await request.GetResponseAsync();
                return response.StatusCode == FtpStatusCode.OpeningData || response.StatusCode == FtpStatusCode.DataAlreadyOpen;
            }
            catch (WebException ex)
            {
                if (ex.Response is FtpWebResponse ftpResponse && ftpResponse.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
                    return false;

                // 非存在性錯誤也記錄
                Console.WriteLine($"[FTP ERROR] {ex.Message}");
                return false;
            }
        }
        public static Task<bool> DirectoryExistsOnShareAsync(string sharePath)
        {
            return Task.FromResult(Directory.Exists(sharePath));
        }

        public static  async Task UploadFileAsync(string host, string path, string username, string password, byte[] fileContent)
        {
            try
            {
                var uri = new Uri($"ftp://{host}{path}");
                var request = (FtpWebRequest)WebRequest.Create(uri);
                request.Method = WebRequestMethods.Ftp.UploadFile;
                request.Credentials = new NetworkCredential(username, password);
                request.UsePassive = true;
                request.UseBinary = true;
                request.KeepAlive = false;

                using var reqStream = await request.GetRequestStreamAsync();
                await reqStream.WriteAsync(fileContent, 0, fileContent.Length);

                using var response = (FtpWebResponse)await request.GetResponseAsync();
                Console.WriteLine($"FTP 上傳完成：{response.StatusDescription}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FTP 上傳失敗] {path} - {ex.Message}");
                throw;
            }
        }

        public static async Task<byte[]> DownloadFileAsync(string host, string path, string username, string password)
        {
            try
            {
                var uri = new Uri($"ftp://{host}{path}");
                var request = (FtpWebRequest)WebRequest.Create(uri);
                request.Method = WebRequestMethods.Ftp.DownloadFile;
                request.Credentials = new NetworkCredential(username, password);
                request.UsePassive = true;
                request.UseBinary = true;
                request.KeepAlive = false;

                using var response = (FtpWebResponse)await request.GetResponseAsync();
                using var stream = response.GetResponseStream();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                return ms.ToArray();
            }
            catch
            {
                return null;
            }
        }

        public static async Task<bool> FileExistsAsync(string host, string path, string username, string password)
        {
            try
            {
                var uri = new Uri($"ftp://{host}{path}");
                var request = (FtpWebRequest)WebRequest.Create(uri);
                request.Method = WebRequestMethods.Ftp.GetFileSize;
                request.Credentials = new NetworkCredential(username, password);
                request.UsePassive = true;
                request.UseBinary = true;
                request.KeepAlive = false;

                using var response = (FtpWebResponse)await request.GetResponseAsync();
                return response.StatusCode == FtpStatusCode.FileStatus || response.ContentLength > 0;
            }
            catch (WebException ex)
            {
                if (ex.Response is FtpWebResponse ftpResponse &&
                    ftpResponse.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
                    return false;

                Console.WriteLine($"[FTP FILE CHECK ERROR] {ex.Message}");
                return false;
            }
        }


        public static async Task CreateDirectoryAsync(string host, string path, string username, string password)
        {
            var uri = new Uri($"ftp://{host}{path}");
            var request = (FtpWebRequest)WebRequest.Create(uri);
            request.Method = WebRequestMethods.Ftp.MakeDirectory;
            request.Credentials = new NetworkCredential(username, password);
            request.UsePassive = true;
            request.UseBinary = true;
            request.KeepAlive = false;

            try
            {
                using var response = (FtpWebResponse)await request.GetResponseAsync();
                Console.WriteLine($"FTP 目錄已建立：{path}");
            }
            catch (WebException ex)
            {
                Console.WriteLine($"[FTP 建立目錄失敗] {path} - {ex.Message}");
                throw;
            }
        }
    }
}
