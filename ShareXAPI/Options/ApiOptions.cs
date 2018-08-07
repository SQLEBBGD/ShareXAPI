namespace ShareXAPI.Options
{
    public class ApiOptions
    {
        public bool UseAzureIntegration { get; set; }

        public UploaderOptions[] Uploader { get; set; }
    }
}
