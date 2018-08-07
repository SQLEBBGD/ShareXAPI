namespace ShareXAPI.Options
{
    public class UploaderOptions
    {
        public ApiResponseType ResponseType { get; set; } = ApiResponseType.Redirect;
        public string WebBasePath { get; set; }
        public string LocalBasePath { get; set; }
        public string[] FileExtensions { get; set; }
        public float MaxFileSize { get; set; } // in MB
        public float MaxFolderSize { get; set; } // in MB
        public string ApiKey { get; set; }
    }
}