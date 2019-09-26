using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using google = Google.Apis.Drive.v3.Data;
using Google.Apis.Drive.v3;
using Google.Apis.Auth.OAuth2;
using System.Linq;
using System.Threading;
using Google.Apis.Util.Store;
using Google.Apis.Services;
using Google.Apis.Upload;
using Microsoft.Extensions.Configuration;
using CoreMoveHubspotData;
using Newtonsoft.Json.Linq;

namespace MoveHubspotToOntraport
{
    class GoogleDrive
    {
        public static IConfigurationRoot app = AppConfiguration.GetConfig();
        public static string GoogleAppName = Convert.ToString(app["MySettings:GoogleAppName"]);
        public static string[] Scopes = { DriveService.Scope.DriveFile };

        public static string UploadByGoogleDrive(byte[] bytes, string fileName, string mimeType, out bool status)
        {
            status = false;
            var fileDownloadLink = string.Empty;

            var workingDirectory = Environment.CurrentDirectory;
            string projectDirectory = Directory.GetParent(workingDirectory).Parent.FullName;
            var _filePath = $"{projectDirectory}/files/";
            var filesPath = Path.Combine(_filePath, fileName);

            DriveService service = GetService();

            using (FileStream file = File.Create(filesPath))
            {
                file.Write(bytes, 0, bytes.Length);
            }

            var FileMetaData = new google.File()
            {
                Name = fileName,
                MimeType = mimeType,
                Parents = new List<string>
                {
                    //"1xT8DV-wjRRFzV9v8gjP0WbllAuJMK_Mi"//google drive folder id
                    "1hyvrLDIOddffCwGqJt_fAlo8PrFlD5hS"
                }
            };

            IUploadProgress response = null;
            FilesResource.CreateMediaUpload request;
            using (var stream = new System.IO.FileStream(filesPath,
            System.IO.FileMode.Open))
            {
                request = service.Files.Create(FileMetaData, stream,
                FileMetaData.MimeType);
                request.Fields = "id";
                request.Fields = "name";
                request.Fields = "mimeType";
                request.Fields = "webViewLink";
                response = request.Upload();
            }
            var result = (int)response.Status;
            if (result == (int)UploadStatus.Completed)
            {
                status = true;
                //Console.WriteLine($"File is successfully uploaded on Google Drive");
            }
            else
            {
                ErrorLog.InfoMessage("File is not uploaded on Google Drive for this contact");
                Console.WriteLine("File is not uploaded on Google Drive for this contact");
            }
            var fileResponse = request.ResponseBody;
            if (fileResponse != null)
            {
                fileDownloadLink = fileResponse.WebViewLink;
                ErrorLog.InfoMessage($"File is successfully uploaded on Google Drive {fileDownloadLink}");
                Console.WriteLine($"File is successfully uploaded on Google Drive {fileDownloadLink}");
            }

            return fileDownloadLink;
        }

        public static JObject CheckFiles(string fileName, string mimeType)
        {
            var jo = new JObject();
            try
            {
                DriveService service = GetService();

                // Define the parameters of the request.    
                FilesResource.ListRequest FileListRequest = service.Files.List();
                FileListRequest.Fields = "nextPageToken, files(*)";

                // List files.    
                IList<Google.Apis.Drive.v3.Data.File> files = FileListRequest.Execute().Files;

                //For getting only folders    
                var _file = files.Where(x => x.MimeType == mimeType && x.Name == fileName && x.Trashed == false).FirstOrDefault();

                if (_file != null)
                {
                    jo["webViewLink"] = _file.WebViewLink;
                    jo["name"] = _file.Name;
                    jo["id"] = _file.Id;
                    jo["mimeType"] = _file.MimeType;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return jo;
        }

        public static void CreateFolderOnDrive(string Folder_Name)
        {
            DriveService service = GetService();

            var FileMetaData = new Google.Apis.Drive.v3.Data.File();
            FileMetaData.Name = Folder_Name;
            FileMetaData.MimeType = "application/vnd.google-apps.folder";

            Google.Apis.Drive.v3.FilesResource.CreateRequest request;

            request = service.Files.Create(FileMetaData);
            request.Fields = "id";
            var file = request.Execute();
        }

        public static DriveService GetService()
        {
            //get Credentials from client_secret.json file     
            UserCredential credential;
            var workingDirectory = Environment.CurrentDirectory;
            string projectDirectory = Directory.GetParent(workingDirectory).Parent.FullName;
            string tokenFilePath = $"{projectDirectory}/OauthToken/";
            var credentialFile = Path.Combine(tokenFilePath, "credentials.json");

            using (var stream = new FileStream(credentialFile, FileMode.Open, FileAccess.Read))
            {
                String FilePath = Path.Combine(tokenFilePath, "DriveServiceCredential.json");

                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(FilePath, true)).Result;
            }

            //create Drive API service.    
            DriveService service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = GoogleAppName,
            });
            return service;
        }

        [DllImport("kernel32.dll", ExactSpelling = true)]
        public static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        public void BringConsoleToFront()
        {
            SetForegroundWindow(GetConsoleWindow());
        }

    }
}

