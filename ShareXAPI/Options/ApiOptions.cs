using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ShareXAPI.Options
{
    public class ApiOptions
    {
        public bool UseAzureIntegration { get; set; }

        public UploaderOptions[] Uploader { get; set; }
    }
}
