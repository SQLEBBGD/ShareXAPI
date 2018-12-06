using Microsoft.AspNetCore.Http;

namespace ShareXAPI.Models
{
    public class UploadFileModel
    {
        public string Uploader { get; set; }

        public string ApiKey { get; set; }
    }
}