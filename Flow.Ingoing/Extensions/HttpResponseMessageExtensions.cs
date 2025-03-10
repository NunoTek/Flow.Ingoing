using Microsoft.Extensions.Logging;

namespace Flow.Ingoing.Extensions;

public static class HttpResponseMessageExtensions
{
    public static void EnsureSuccessStatusCodeOrThrow(this HttpResponseMessage response)
    {
        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch
        {
            var content = response.Content?.ReadAsStringAsync().Result ?? "";
            var message = $"{(int)response.StatusCode}-{response.StatusCode} : \r\n {content}";
            throw new Exception(message);
        }
    }

    public static void EnsureSuccessStatusCodeOrLog(this HttpResponseMessage response, ILogger logger)
    {
        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            var content = response.Content?.ReadAsStringAsync().Result ?? "";
            var message = $"{(int)response.StatusCode}-{response.StatusCode} : \r\n {content}";
            logger.LogWarning(e, "#---- -- Error result : {content}\r\n{message}", message, string.Concat(e.Message, " ", e.InnerException?.Message ?? "no-inner-message"));
        }
    }
}