using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ShareXAPI.Extensions;
using ShareXAPI.Models;
using ShareXAPI.Options;

namespace ShareXAPI.Controllers
{
    
    public class UploaderController : Controller
    {
        private readonly ApiOptions _options;

        public UploaderController(IOptions<ApiOptions> options)
        {
            _options = options.Value;
        }

        [HttpPost("/{uploadName}")]
        public async Task<IActionResult> Post([FromRoute]string uploadName, [FromForm]PostFileModel model)
        {
            var file = model.File;
            var apiKey = model.ApiKey;
            if (file == null)
            {
                return BadRequest("No file given");
            }

            var uploaders =
                _options.Uploader.Where(
                    s => s.WebBasePath.Equals(uploadName, StringComparison.OrdinalIgnoreCase));
            if (uploaders == null)
            {
                return BadRequest(
                    $"Uploader not Found - Wrong path?");
            } 
            if (!uploaders.Any(s => s.ApiKey == apiKey && !string.IsNullOrEmpty(s.ApiKey)))
            {
                return new UnauthorizedResult();
            }

            var uploader =
                _options.Uploader.FirstOrDefault(
                    s => s.WebBasePath.Equals(uploadName, StringComparison.OrdinalIgnoreCase) && s.ApiKey == apiKey);

            var fileExtension = Path.GetExtension(file.FileName).ToLower();

            if (!uploader.FileExtensions.Contains(fileExtension) && !uploader.FileExtensions.Contains("*") || file.Length > (1024 * 1024) * uploader.MaxFileSize)
            {
                return BadRequest(
                    $"File does not meet the Requirements. Maximum size {uploader.MaxFileSize}MB, Allowed Extensions [{string.Join(", ", uploader.FileExtensions)}]");
            }

            Directory.CreateDirectory(uploader.LocalBasePath);

            var fileName = GetRandomFileName(fileExtension);
            var filePath = Path.Combine(uploader.LocalBasePath, fileName);
            var fileSize = file.Length;

            if (uploader.MaxFolderSize > 0)
            {
                if (fileSize > uploader.MaxFolderSize*1024*1024)
                {
                    return BadRequest("File bigger than max foldersize");
                }

                while (GetDirectorySize(uploader.LocalBasePath) + fileSize > uploader.MaxFolderSize*1024*1024)
                {
                    DeleteOldestFile(uploader.LocalBasePath);
                }
            }
            
            while (System.IO.File.Exists(filePath))
            {
                fileName = GetRandomFileName(fileExtension);
                filePath = Path.Combine(uploader.LocalBasePath, fileName);
            }

            using (var fs = System.IO.File.Create(filePath))
            {
                await file.CopyToAsync(fs);
            }
            
            if(uploader.ResponseType == ApiResponseType.Redirect)
                return LocalRedirect($"/{uploader.WebBasePath}/{fileName}");

            return Ok(new ResultModel
            {
                FileUrl = ToAbsoluteUrl(Url.Content(uploader.WebBasePath + "/" + fileName)),
                DeleteUrl = ToAbsoluteUrl(Url.Action("Delete", "Uploader", new {uploadName= uploader.WebBasePath, fileName}))
            });
        }

        private string ToAbsoluteUrl(string relativeUrl)
        {
            var relativeUri = new Uri(relativeUrl, UriKind.Relative);
            return relativeUri.ToAbsolute(HttpContext.Request.Scheme + "://" + HttpContext.Request.Host);
        }

        [HttpGet("/delete/{uploadName}/{fileName}")]
        public IActionResult Delete([FromRoute]string uploadName, [FromRoute]string fileName)
        {
            var uploader =
                _options.Uploader.FirstOrDefault(
                    s => s.WebBasePath.Equals(uploadName, StringComparison.OrdinalIgnoreCase));
            if (uploader == null)
                return BadRequest();

            var filePath = Path.Combine(uploader.LocalBasePath, fileName);
            if (!System.IO.File.Exists(filePath))
                return NotFound();

            System.IO.File.Delete(filePath);
            return Ok();
        }

        private static string GetRandomFileName(string extension) =>
            Path.ChangeExtension(Guid.NewGuid().ToString("N").Substring(0, 10), extension);

        private static long GetDirectorySize(string directoryPath) =>
            Directory.GetFiles(directoryPath, "*.*").Select(name => new FileInfo(name))
                .Select(currentFile => currentFile.Length).Sum();

        private static void DeleteOldestFile(string directoryPath) =>
            System.IO.File.Delete(Path.Combine(directoryPath,
                Directory.GetFiles(directoryPath).Select(name => new FileInfo(name))
                    .OrderBy(currentFile => currentFile.CreationTime).FirstOrDefault()?.Name));
    }
}