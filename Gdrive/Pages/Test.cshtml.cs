using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Upwork.Pages
{
    public class TestModel : PageModel
    {
        private static readonly string[] Scopes = { DriveService.Scope.Drive, DriveService.Scope.DriveFile };
        private string ApplicationName = "Gdrive Upwork";
        private readonly IWebHostEnvironment webHostEnvironment;
        private UserCredential credential;

        public TestModel(IWebHostEnvironment _webHostEnvironment)
        {
            webHostEnvironment = _webHostEnvironment;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPost(IFormCollection form)
        {
            var file = form;
            var filePath = string.Empty;
            string contentType = string.Empty;
            string fileName = string.Empty;
            if (file.Files.Count > 0)
            {
                string root = webHostEnvironment.WebRootPath + "/creds/";
                foreach (var item in file.Files)
                {
                    filePath = root + DateTime.UtcNow.Ticks.ToString();
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                    fileName = item.FileName;
                    contentType = item.ContentType;
                    using var fileStream = new FileStream(filePath, FileMode.Create);
                    await item.CopyToAsync(fileStream);
                }
            }

            credential = GetCredentials();
            DriveService service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });
            service.HttpClient.Timeout = TimeSpan.FromMinutes(100);
            var folderID = await CreateFolder(form["txtFolderName"], service);
            if (folderID.Length > 0)
            {
                await UploadFile(filePath, contentType, fileName, folderID, service);
            }
            return Page();
        }

        #region Utility Methods

        private async Task<string> CreateFolder(string folderName, DriveService service)
        {
            #region Query GDrive for existing folder with name.

            var req = service.Files.List();
            req.Q = $"mimeType='application/vnd.google-apps.folder' and trashed=false and name='{folderName}'";
            req.Fields = "nextPageToken, files(id, name,parents,mimeType)";
            var res = await req.ExecuteAsync();

            if (res.Files.Count > 0)
            {
                return res.Files[0].Id;
            }

            #endregion Query GDrive for existing folder with name.

            #region Create Folder if doesn't exists.

            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = folderName,
                MimeType = "application/vnd.google-apps.folder"
            };

            var request = service.Files.Create(fileMetadata);
            request.Fields = "id";
            var file = await request.ExecuteAsync();
            return file.Id;

            #endregion Create Folder if doesn't exists.
        }

        private async Task<string> UploadFile(string path, string contentType, string fileName, string folderID, DriveService service)
        {
            var fileMetadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = fileName,
                MimeType = contentType
            };

            fileMetadata.Parents = new List<string> { folderID };

            FilesResource.CreateMediaUpload request;
            using (var stream = new System.IO.FileStream(path, System.IO.FileMode.Open))
            {
                request = service.Files.Create(fileMetadata, stream, contentType);
                request.Fields = "id";
                await request.UploadAsync();
            }

            var file = request.ResponseBody;

            return file.Id;
        }

        private UserCredential GetCredentials()
        {
            UserCredential credential;

            using (var stream = new FileStream(webHostEnvironment.WebRootPath + "/creds/credentials.json", FileMode.Open, FileAccess.Read))
            {
                // The file token.json stores the user's access and refresh tokens, and is created
                // automatically when the authorization flow completes for the first time.
                string credPath = webHostEnvironment.WebRootPath + "/creds/token.json";
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                Console.WriteLine("Credential file saved to: " + credPath);
            }

            return credential;
        }

        #endregion Utility Methods
    }
}