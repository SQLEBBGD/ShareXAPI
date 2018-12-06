using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using ShareXAPI.Extensions;
using ShareXAPI.Models;
using ShareXAPI.Options;

namespace ShareXAPI.Controllers
{

    public class UploaderController : Controller
    {
        private readonly ILogger<UploaderController> _logger;
        private readonly ApiOptions _options;
        private readonly FormOptions _defaultFormOptions = new FormOptions();

        public UploaderController(IOptions<ApiOptions> options, ILogger<UploaderController> logger)
        {
            _logger = logger;
            _options = options.Value;
        }

        //[HttpPost("/old/{uploadName}")]
        //public async Task<IActionResult> Post([FromRoute]string uploadName, [FromForm]PostFileModel model)
        //{
        //    IFormFile file = null; //"sss"; //model.File;
        //    //if (file == null)
        //    //{
        //    //    return BadRequest("No file given.");
        //    //}
        //    var apiKey = model.ApiKey;
            

        //    var uploader =
        //        _options.Uploader.FirstOrDefault(
        //            s => s.WebBasePath.Equals(uploadName, StringComparison.OrdinalIgnoreCase)
        //                 && (s.ApiKey.Equals(apiKey) || string.IsNullOrEmpty(s.ApiKey)));
        //    if (uploader == null)
        //    {
        //        return BadRequest("Uploader not found, invalid upload path or invalid API key.");
        //    }

        //    var fileExtension = Path.GetExtension(file.FileName).ToLower();

        //    if (!ValidateFileSize(uploader.MaxFileSize, file.Length))
        //    {
        //        return BadRequest($"File does not meet the Requirements. Maximum size {uploader.MaxFileSize}MB.");
        //    }

        //    if (!uploader.FileExtensions.Contains(fileExtension) && !uploader.FileExtensions.Contains("*"))
        //    {
        //        return BadRequest(
        //            $"File does not meet the requirements. Invalid extension ({fileExtension}), allowed extensions: [{string.Join(", ", uploader.FileExtensions)}]");
        //    }

        //    Directory.CreateDirectory(uploader.LocalBasePath);

        //    var fileName = GetRandomFileName(fileExtension);
        //    var filePath = Path.Combine(uploader.LocalBasePath, fileName);
        //    var fileSize = file.Length;

        //    if (uploader.MaxFolderSize > 0)
        //    {
        //        if (fileSize > uploader.MaxFolderSize * 1024 * 1024)
        //        {
        //            return BadRequest("File bigger than max foldersize");
        //        }

        //        while (GetDirectorySize(uploader.LocalBasePath) + fileSize > uploader.MaxFolderSize * 1024 * 1024)
        //        {
        //            DeleteOldestFile(uploader.LocalBasePath);
        //        }
        //    }

        //    while (System.IO.File.Exists(filePath))
        //    {
        //        fileName = GetRandomFileName(fileExtension);
        //        filePath = Path.Combine(uploader.LocalBasePath, fileName);
        //    }

        //    using (var fs = System.IO.File.Create(filePath))
        //    {
        //        await file.CopyToAsync(fs);
        //    }

        //    if (uploader.ResponseType == ApiResponseType.Redirect)
        //        return LocalRedirect($"/{uploader.WebBasePath}/{fileName}/{file.FileName}");

        //    return Ok(new ResultModel
        //    {
        //        FileUrl = ToAbsoluteUrl(Url.Content(uploader.WebBasePath + "/" + fileName + "/" + file.FileName)),
        //        DeleteUrl = ToAbsoluteUrl(Url.Action("Delete", "Uploader", new { uploadName = uploader.WebBasePath, fileName }))
        //    });
        //}

        [HttpPost("/{uploadName}")]
        [DisableRequestSizeLimit]
        [DisableFormValueModelBinding]
        [AcceptMultipart]
        public async Task<IActionResult> Upload([FromRoute]string uploadName, [FromQuery(Name = "k")] string apiKeyQuery)
        {
            var apiKeys = HttpContext.Request.Headers["ApiKey"];
            var apiKey = apiKeys.Count == 1 ? apiKeys[0] : apiKeyQuery;

            var uploader =
                _options.Uploader.FirstOrDefault(
                    s => s.WebBasePath.Equals(uploadName, StringComparison.OrdinalIgnoreCase)
                         && (s.ApiKey.Equals(apiKey) || string.IsNullOrEmpty(s.ApiKey)));
            if (uploader == null)
            {
                return BadRequest("Uploader not found, invalid upload path or invalid API key.");
            }

            // Used to accumulate all the form url encoded key value pairs in the 
            // request.
            var formAccumulator = new KeyValueAccumulator();
            string targetFilePath = null;

            var boundary = MultipartRequestHelper.GetBoundary(
                MediaTypeHeaderValue.Parse(Request.ContentType),
                _defaultFormOptions.MultipartBoundaryLengthLimit);
            var reader = new MultipartReader(boundary, HttpContext.Request.Body);

            var section = await reader.ReadNextSectionAsync();
            string fileExtension = null;
            while (section != null)
            {
                var hasContentDispositionHeader = ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var contentDisposition);

                
                if (hasContentDispositionHeader)
                {
                    if (MultipartRequestHelper.HasFileContentDisposition(contentDisposition))
                    {
                        fileExtension = Path.GetExtension(contentDisposition.FileName.Value).ToLower();
                        targetFilePath = Path.GetTempFileName();
                        if (!uploader.FileExtensions.Contains(fileExtension) && !uploader.FileExtensions.Contains("*"))
                        {
                            return BadRequest(
                                $"File does not meet the requirements. Invalid extension ({fileExtension}), allowed extensions: [{string.Join(", ", uploader.FileExtensions)}]");
                        }
                        try
                        {
                            using (var targetStream = System.IO.File.Create(targetFilePath))
                            {
                                await section.Body.CopyToAsync(targetStream);

                                _logger.LogInformation($"Copied the uploaded file '{targetFilePath}'");
                            }
                        }
                        catch (Exception)
                        {
                            _logger.LogError("An error occured while streaming the File. Did the client cancel?");
                            DeleteFileIfExists(targetFilePath);
                        }
                        
                    }
                    else if (MultipartRequestHelper.HasFormDataContentDisposition(contentDisposition))
                    {
                        // Content-Disposition: form-data; name="key"
                        //
                        // value

                        // Do not limit the key name length here because the 
                        // multipart headers length limit is already in effect.
                        var key = HeaderUtilities.RemoveQuotes(contentDisposition.Name);
                        var encoding = GetEncoding(section);

                        using (var streamReader = new StreamReader(
                            section.Body,
                            encoding,
                            detectEncodingFromByteOrderMarks: true,
                            bufferSize: 1024,
                            leaveOpen: true))
                        {
                            // The value length limit is enforced by MultipartBodyLengthLimit
                            var value = await streamReader.ReadToEndAsync();
                            if (string.Equals(value, "undefined", StringComparison.OrdinalIgnoreCase))
                            {
                                value = string.Empty;
                            }
                            formAccumulator.Append(key.Value, value);

                            if (formAccumulator.ValueCount > _defaultFormOptions.ValueCountLimit)
                            {
                                throw new InvalidDataException($"Form key count limit {_defaultFormOptions.ValueCountLimit} exceeded.");
                            }
                        }
                    }
                }

                // Drains any remaining section body that has not been consumed and
                // reads the headers for the next section.
                section = await reader.ReadNextSectionAsync();
            }

            // Bind form data to a model
            var model = new PostFileModel();
            var formValueProvider = new FormValueProvider(
                BindingSource.Form,
                new FormCollection(formAccumulator.GetResults()),
                CultureInfo.CurrentCulture);

            var bindingSuccessful = await TryUpdateModelAsync(model, prefix: "",
                valueProvider: formValueProvider);
            if (!bindingSuccessful && !ModelState.IsValid)
                return BadRequest(ModelState);

            
            var file = new FileInfo(targetFilePath ?? throw new InvalidOperationException());

            if (!ValidateFileSize(uploader.MaxFileSize, file.Length))
            {
                file.Delete();
                return BadRequest($"File does not meet the Requirements. Maximum size {uploader.MaxFileSize}MB.");
            }

            Directory.CreateDirectory(uploader.LocalBasePath);
            var fileName = GetRandomFileName(fileExtension);
            var filePath = Path.Combine(uploader.LocalBasePath, fileName);
            var fileSize = file.Length;

            if (uploader.MaxFolderSize > 0)
            {
                if (fileSize > uploader.MaxFolderSize * 1024 * 1024)
                {
                    file.Delete();
                    return BadRequest("File bigger than max foldersize");
                }

                while (GetDirectorySize(uploader.LocalBasePath) + fileSize > uploader.MaxFolderSize * 1024 * 1024)
                {
                    DeleteOldestFile(uploader.LocalBasePath);
                }
            }

            while (System.IO.File.Exists(filePath))
            {
                fileName = GetRandomFileName(fileExtension);
                filePath = Path.Combine(uploader.LocalBasePath, fileName);
            }

            file.MoveTo(filePath);


            if (uploader.ResponseType == ApiResponseType.Redirect)
                return LocalRedirect($"/{uploader.WebBasePath}/{fileName}");
            
            return Ok(new ResultModel
            {
                FileUrl = ToAbsoluteUrl(Url.Content(uploader.WebBasePath + "/" + fileName)),
                DeleteUrl = ToAbsoluteUrl(Url.Action("Delete", "Uploader",
                    new { uploadName = uploader.WebBasePath, fileName }))
            });
            
        }

        [HttpGet("/{container}/{fileId}")]
        public IActionResult Get(string container, string fileId, string fileName)
        {
            var uploader = _options.Uploader.FirstOrDefault(u => u.WebBasePath.Equals(container));
            if (uploader == null)
                return NotFound();

            var filepath = Path.Combine(uploader.LocalBasePath, fileId);
            if (!System.IO.File.Exists(filepath))
                return NotFound();

            var fileStream = System.IO.File.OpenRead(filepath);
            return File(fileStream, "application/octet-stream", fileName);
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
            return Ok("Success!");
        }

        private static string GetRandomFileName(string extension) =>
            Path.ChangeExtension(Guid.NewGuid().ToString("N").Substring(0, 6), extension);

        private static long GetDirectorySize(string directoryPath) =>
            Directory.GetFiles(directoryPath, "*.*").Select(name => new FileInfo(name))
                .Select(currentFile => currentFile.Length).Sum();

        private static void DeleteOldestFile(string directoryPath) =>
            System.IO.File.Delete(Path.Combine(directoryPath,
                Directory.GetFiles(directoryPath).Select(name => new FileInfo(name))
                    .OrderBy(currentFile => currentFile.CreationTime).FirstOrDefault()?.Name));

        private static bool ValidateFileSize(float maxFileSizeMb, long fileSizeByte) => 
            (long)maxFileSizeMb == 0 || fileSizeByte <= maxFileSizeMb * 1024 * 1024;

        private static void DeleteFileIfExists(string filePath)
        {
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        }

        private static Encoding GetEncoding(MultipartSection section)
        {
            var hasMediaTypeHeader = MediaTypeHeaderValue.TryParse(section.ContentType, out var mediaType);
            // UTF-7 is insecure and should not be honored. UTF-8 will succeed in 
            // most cases.
            return !hasMediaTypeHeader || Encoding.UTF7.Equals(mediaType.Encoding)
                ? Encoding.UTF8
                : mediaType.Encoding;
        }
    }

    public static class MultipartRequestHelper
    {
        // Content-Type: multipart/form-data; boundary="----WebKitFormBoundarymx2fSWqWSd0OxQqq"
        // The spec says 70 characters is a reasonable limit.
        public static string GetBoundary(MediaTypeHeaderValue contentType, int lengthLimit)
        {
            var boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary);
            if (string.IsNullOrWhiteSpace(boundary.Value))
            {
                throw new InvalidDataException("Missing content-type boundary.");
            }

            if (boundary.Length > lengthLimit)
            {
                throw new InvalidDataException(
                    $"Multipart boundary length limit {lengthLimit} exceeded.");
            }

            return boundary.Value;
        }

        public static bool HasFormDataContentDisposition(ContentDispositionHeaderValue contentDisposition)
        {
            // Content-Disposition: form-data; name="key";
            return contentDisposition != null
                   && contentDisposition.DispositionType.Equals("form-data")
                   && string.IsNullOrEmpty(contentDisposition.FileName.Value)
                   && string.IsNullOrEmpty(contentDisposition.FileNameStar.Value);
        }

        public static bool HasFileContentDisposition(ContentDispositionHeaderValue contentDisposition)
        {
            // Content-Disposition: form-data; name="myfile1"; filename="Misc 002.jpg"
            return contentDisposition != null
                   && contentDisposition.DispositionType.Equals("form-data")
                   && (!string.IsNullOrEmpty(contentDisposition.FileName.Value)
                       || !string.IsNullOrEmpty(contentDisposition.FileNameStar.Value));
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]


    public class DisableFormValueModelBindingAttribute : Attribute, IResourceFilter
    {
        public void OnResourceExecuting(ResourceExecutingContext context)
        {
            var factories = context.ValueProviderFactories;
            factories.RemoveType<FormValueProviderFactory>();
            factories.RemoveType<JQueryFormValueProviderFactory>();
        }

        public void OnResourceExecuted(ResourceExecutedContext context)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AcceptMultipartAttribute : Attribute, IActionConstraint
    {
        public bool Accept(ActionConstraintContext context)
        {
            var contentType = context.RouteContext.HttpContext.Request.ContentType;
            return !string.IsNullOrEmpty(contentType)
                   && contentType.IndexOf("multipart/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public int Order { get; }
    }
}