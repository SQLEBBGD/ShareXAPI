using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShareXAPI.Models;
using ShareXAPI.Options;

namespace ShareXAPI.Controllers
{
    public class HomeController : Controller
    {
        private ILogger<HomeController> _logger;
        private ApiOptions _options;

        public HomeController(IOptions<ApiOptions> options, ILogger<HomeController> logger)
        {
            _logger = logger;
            _options = options.Value;
        }

        [HttpGet("/upload")]
        public IActionResult Upload(string uploader, string apiKey)
        {
            var selList = new SelectList(_options.Uploader.Select(u => u.WebBasePath));
            ViewData["uploaders"] = selList;
            var model = new UploadFileModel
            {
                Uploader = uploader,
                ApiKey = apiKey
            };
            return View(model);
        }
    }
}
