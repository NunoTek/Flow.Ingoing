using Flow.Ingoing.Extensions;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Flow.Ingoing.Helpers;

public class RequestLoggingHandler : DelegatingHandler
{
    private readonly ILogger _logger;

    public long LogContentLengthLimit { get; set; } = 10000;

    public RequestLoggingHandler(ILogger<RequestLoggingHandler> logger, HttpMessageHandler innerHandler = null)
        : base(innerHandler ?? new HttpClientHandler() { ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; } })
        => _logger = logger;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("#--- Requesting service: {Method} {RequestUri}", request.Method, request.RequestUri);

        Stopwatch watch = null;
        if (_logger.IsEnabled(LogLevel.Information))
        {
            watch = new Stopwatch();
        }

        HttpResponseMessage response;
        try
        {
            request.Headers.TryGetValues("X-Api-Key", out var requestApiKey);
            if (requestApiKey?.Any() == true && !string.IsNullOrEmpty(requestApiKey.FirstOrDefault()))
            {
                _logger.LogDebug("#---- -- Request ApiKey : {requestApiKey}", requestApiKey);
            }

            request.Headers.TryGetValues("Authorization", out var requestAuthKey);
            if (requestAuthKey?.Any() == true && !string.IsNullOrEmpty(requestAuthKey.FirstOrDefault()))
            {
                _logger.LogDebug("#---- -- Request Authorization : {requestAuthKey}", requestAuthKey);
            }

            _logger.LogDebug("#---- -- Headers: {RequestHeader}", request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()));

            if (_logger.IsEnabled(LogLevel.Debug) && request.Content != null)
            {
                long contentLength = (request.Headers?.ToString()?.Length ?? 0) + (request.Content?.Headers.ContentLength ?? 0);
                if (contentLength < LogContentLengthLimit)
                {
                    string requestBody = await request.Content.ReadAsStringAsync(cancellationToken);
                    if (!string.IsNullOrEmpty(requestBody) && requestBody.Length < LogContentLengthLimit)
                    {
                        _logger.LogDebug("#---- -- Body: {@RequestBody}", requestBody);
                    }
                }
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                watch?.Start();
            }

            response = await base.SendAsync(request, cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                watch?.Stop();
                _logger.LogInformation("#---- -- Request to {Method} {Url} took {MilliSeconds}ms", request.Method, request.RequestUri, watch?.ElapsedMilliseconds);
            }

            response.EnsureSuccessStatusCodeOrLog(_logger);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "#---- -- Error result : {message}", string.Concat(e.Message, " ", e.InnerException?.Message ?? "no-inner-message"));
            throw;
        }

        if (_logger.IsEnabled(LogLevel.Debug) && response.Content != null)
        {
            long contentLength = (response.Headers?.ToString()?.Length ?? 0) + (response.Content?.Headers.ContentLength ?? 0);
            if (contentLength < LogContentLengthLimit)
            {
                string responseMessage = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!string.IsNullOrEmpty(responseMessage) && responseMessage.Length < LogContentLengthLimit)
                {
                    _logger.LogDebug("#---- -- Response: {ResponseMessage}", responseMessage);
                }
            }
        }

        return response;
    }
}
