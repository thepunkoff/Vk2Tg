﻿using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vk2Tg.Http.Handlers;

namespace Vk2Tg.Http
{
    // TODO: move to ASP.NET Core minimal api
    public partial class HttpServerService : BackgroundService
    {
        private readonly int _port;
        private readonly HttpListener _httpListener = new();

        private readonly SettingsHandlerService _settingsHandlerService;
        private readonly ILogger<HttpServerService> _logger;

        public HttpServerService(SettingsHandlerService settingsHandlerService, IConfiguration configuration, ILogger<HttpServerService> logger)
        {
            _port = configuration.GetSection("httpPort").Get<int>();
            _settingsHandlerService = settingsHandlerService;
            _logger = logger;
        }
#region Logging
        [LoggerMessage(1, LogLevel.Information, "Incoming request: '{Url}' from {Endpoint}")]
        partial void LogIncomingRequest(Uri url, IPEndPoint endpoint);
        [LoggerMessage(2, LogLevel.Trace, "Raw url was '{RawUrl}'. responding 400 bad request")]
        partial void LogUnknownUrl(string? rawUrl);
#endregion
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _httpListener.Prefixes.Add($"http://*:{_port}/");
            _httpListener.Start();
            
            _logger.LogInformation("Started listening http requests on port {Port}", _port);
            
            while (!stoppingToken.IsCancellationRequested)
            {
                var context = await _httpListener.GetContextAsync();
                var request = context.Request;

                if (request.HttpMethod != "GET")
                {
                    await context.Response.ReturnBadRequest("This endpoint supports only GET requests.");
                    continue;
                }

                if (request.Url is null)
                {
                    _logger.LogTrace("Incoming request url was null");
                    continue;
                }
                    
                if (request.Url.AbsolutePath != "/favicon.ico")
                    LogIncomingRequest(request.Url, request.RemoteEndPoint);

                switch (request.Url.AbsolutePath)
                {
                    case "/favicon.ico":
                    {
                        await context.Response.ReturnOk();
                        break;
                    }
                    case "/status":
                    {
                        _logger.LogTrace("Processing status request...");
                        await context.Response.ReturnOk();
                        _logger.LogTrace("Status request processed. Ok");
                        break;
                    }
                    case "/settings":
                    {
                        await _settingsHandlerService.HandleSettingsRequest(context);
                        break;
                    }
                    default:
                    {
                        LogUnknownUrl(request.RawUrl);
                        await context.Response.ReturnBadRequest($"Endpoint '{request.Url.AbsolutePath}' not found. Try '/status' or '/settings'.");
                        break;
                    }
                }
            }
            
            _httpListener.Stop();
            _logger.LogTrace("Stopped http server on port {Port}", _port);
        }
    }
}