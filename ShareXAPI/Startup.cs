﻿using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShareXAPI.Options;

namespace ShareXAPI
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.Configure<ApiOptions>(Configuration.GetSection("Api"));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env
            , ILoggerFactory loggerFactory, IOptions<ApiOptions> apiOptions)
        {
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor
                                   | ForwardedHeaders.XForwardedProto
                                   | ForwardedHeaders.XForwardedHost
            });

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                loggerFactory.AddDebug(LogLevel.Debug);
            }
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));

            if (apiOptions.Value.UseAzureIntegration)
                loggerFactory.AddAzureWebAppDiagnostics();

            foreach (var apiOption in apiOptions.Value.Uploader)
            {
                Directory.CreateDirectory(apiOption.LocalBasePath);
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(apiOption.LocalBasePath),
                    RequestPath = "/" + apiOption.WebBasePath,
                    ServeUnknownFileTypes = true
                });

            }

            app.UseMvcWithDefaultRoute();
        }
    }
}