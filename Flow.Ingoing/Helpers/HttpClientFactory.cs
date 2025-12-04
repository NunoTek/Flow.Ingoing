using Duende.IdentityModel.Client;
using Flow.Ingoing.Consts;
using Flow.Ingoing.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Diagnostics;
using System.Text;

namespace Flow.Ingoing.Helpers;

public static class HttpClientFactory
{
    public static async Task SetOAuth2ParametersAsync(HttpClient httpClient, OAuth2ProtocolParameters authProtocol, ILogger<RequestLoggingHandler> logger, CancellationToken cancellation = default)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogDebug("Starting OAuth2 authentication with workflow: {Workflow}, URL: {Url}", authProtocol.Workflow, authProtocol.Url);

        var tokenClient = new HttpClient(new RequestLoggingHandler(logger), true);
        AddHeaders(tokenClient, authProtocol.Headers);

        string token = string.Empty;

        // TODO: init only what we need by Workflow
        var body = new
        {
            authProtocol.ClientId,
            authProtocol.ClientSecret,

            GrantType = "client_credentials",
            grant_type = "client_credentials",
            Scope = "email",

            Email = authProtocol.Username,
            Login = authProtocol.Username,
            authProtocol.Username,
            authProtocol.Password,
        };

        switch (authProtocol.Workflow)
        {
            case OAuth2AuthentificationWorkflow.Password:
                body = body with { GrantType = "password", grant_type = "password" }; // C#10 Net6.0

                var passwordBody = new StringContent(JsonSerialize(body), Encoding.UTF8, "application/json");
                logger.LogDebug("Sending OAuth2 Password request to {Url}", authProtocol.Url);
                HttpResponseMessage passwordTokenReponse = await tokenClient.PostAsync(new Uri(authProtocol.Url), passwordBody, cancellation);

                if (!passwordTokenReponse.IsSuccessStatusCode)
                {
                    var errorContent = await passwordTokenReponse.Content.ReadAsStringAsync(cancellation);
                    logger.LogError("OAuth2 Password flow failed. Status: {StatusCode}, Response: {Response}", passwordTokenReponse.StatusCode, errorContent);
                    throw new HttpRequestException($"OAuth2 Password authentication failed with status {passwordTokenReponse.StatusCode}: {errorContent}");
                }

                token = GetToken(await passwordTokenReponse.Content.ReadAsStringAsync(cancellation), logger);
                break;

            case OAuth2AuthentificationWorkflow.ClientCredentials:
                body = body with { GrantType = "client_credentials", grant_type = "client_credentials" }; // C#10 Net6.0

                var clientBody = new StringContent(JsonSerialize(body), Encoding.UTF8, "application/json");
                logger.LogDebug("Sending OAuth2 ClientCredentials request to {Url}", authProtocol.Url);
                HttpResponseMessage clientTokenReponse = await tokenClient.PostAsync(new Uri(authProtocol.Url), clientBody, cancellation);

                if (!clientTokenReponse.IsSuccessStatusCode)
                {
                    var errorContent = await clientTokenReponse.Content.ReadAsStringAsync(cancellation);
                    logger.LogError("OAuth2 ClientCredentials flow failed. Status: {StatusCode}, Response: {Response}", clientTokenReponse.StatusCode, errorContent);
                    throw new HttpRequestException($"OAuth2 ClientCredentials authentication failed with status {clientTokenReponse.StatusCode}: {errorContent}");
                }

                token = GetToken(await clientTokenReponse.Content.ReadAsStringAsync(cancellation), logger);
                break;

            case OAuth2AuthentificationWorkflow.AuthorizationCode:
                {
                    body = body with { GrantType = "authorization_code", grant_type = "authorization_code" }; // C#10 Net6.0

                    var codeTokenBody = new StringContent(JsonSerialize(body), Encoding.UTF8, "application/json");
                    logger.LogDebug("Sending OAuth2 AuthorizationCode request (step 1: get code) to {Url}", authProtocol.Url);
                    HttpResponseMessage codeTokenReponse = await tokenClient.PostAsync(authProtocol.Url, codeTokenBody, cancellation);

                    if (!codeTokenReponse.IsSuccessStatusCode)
                    {
                        var errorContent = await codeTokenReponse.Content.ReadAsStringAsync(cancellation);
                        logger.LogError("OAuth2 AuthorizationCode flow (step 1) failed. Status: {StatusCode}, Response: {Response}", codeTokenReponse.StatusCode, errorContent);
                        throw new HttpRequestException($"OAuth2 AuthorizationCode authentication (step 1) failed with status {codeTokenReponse.StatusCode}: {errorContent}");
                    }

                    var code = GetCode(await codeTokenReponse.Content.ReadAsStringAsync(cancellation), logger);
                    logger.LogDebug("OAuth2 AuthorizationCode received code, proceeding to step 2");

                    using var codeClient = new HttpClient(new RequestLoggingHandler(logger), true);
                    AddHeaders(codeClient, authProtocol.Headers);
                    var codeBody = new StringContent(JsonSerialize(new { Code = code }), Encoding.UTF8, "application/json");
                    logger.LogDebug("Sending OAuth2 AuthorizationCode request (step 2: exchange code) to {Url}", authProtocol.Url);
                    HttpResponseMessage codeReponse = await codeClient.PostAsync(authProtocol.Url, codeBody, cancellation);

                    if (!codeReponse.IsSuccessStatusCode)
                    {
                        var errorContent = await codeReponse.Content.ReadAsStringAsync(cancellation);
                        logger.LogError("OAuth2 AuthorizationCode flow (step 2) failed. Status: {StatusCode}, Response: {Response}", codeReponse.StatusCode, errorContent);
                        throw new HttpRequestException($"OAuth2 AuthorizationCode authentication (step 2) failed with status {codeReponse.StatusCode}: {errorContent}");
                    }

                    token = GetToken(await codeReponse.Content.ReadAsStringAsync(cancellation), logger);
                    break;
                }

            case OAuth2AuthentificationWorkflow.ResourceOwnerPasswordCredentials:
            default:
                throw new NotImplementedException($"{authProtocol.Workflow}");
        }

        httpClient.SetBearerToken(token);
        stopwatch.Stop();
        logger.LogDebug("OAuth2 authentication completed successfully in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
    }

    public static async Task SetBasicParametersAsync(HttpClient httpClient, BasicProtocolParameters authProtocol, ILogger<RequestLoggingHandler> logger, CancellationToken cancellation = default)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogDebug("Starting Basic authentication with workflow: {Workflow}", authProtocol.Workflow);

        switch (authProtocol.Workflow)
        {
            case BasicAuthentificationWorkflow.Basic:
                httpClient.DefaultRequestHeaders.Authorization = new BasicAuthenticationHeaderValue(authProtocol.Username ?? string.Empty, authProtocol.Password ?? string.Empty);
                logger.LogDebug("Basic authentication header set for user: {Username}", authProtocol.Username);
                break;

            case BasicAuthentificationWorkflow.Token:
                {
                    using var tokenClient = new HttpClient(new RequestLoggingHandler(logger), true);
                    AddHeaders(tokenClient, authProtocol.Headers);

                    tokenClient.DefaultRequestHeaders.Authorization = new BasicAuthenticationHeaderValue(authProtocol.Username ?? string.Empty, authProtocol.Password ?? string.Empty);

                    //var data = new List<KeyValuePair<string, string>>
                    //{
                    //    new KeyValuePair<string, string>("username", authProtocol.Username),
                    //    new KeyValuePair<string, string>("password", authProtocol.Password),
                    //};
                    //HttpResponseMessage tokenReponse = await tokenClient.PostAsync(authProtocol.Url, new FormUrlEncodedContent(data), cancellation);


                    var body = new { GrantType = "client_credentials", grant_type = "client_credentials" };
                    var codeTokenBody = new StringContent(JsonSerialize(body), Encoding.UTF8, "application/json");
                    logger.LogDebug("Sending Basic Token request to {Url}", authProtocol.Url);
                    HttpResponseMessage tokenReponse = await tokenClient.PostAsync(authProtocol.Url, codeTokenBody, cancellation);

                    if (!tokenReponse.IsSuccessStatusCode)
                    {
                        var errorContent = await tokenReponse.Content.ReadAsStringAsync(cancellation);
                        logger.LogError("Basic Token flow failed. Status: {StatusCode}, Response: {Response}", tokenReponse.StatusCode, errorContent);
                        throw new HttpRequestException($"Basic Token authentication failed with status {tokenReponse.StatusCode}: {errorContent}");
                    }

                    string token = GetToken(await tokenReponse.Content.ReadAsStringAsync(cancellation), logger);
                    httpClient.SetBearerToken(token);
                    break;
                }

            default:
                throw new NotImplementedException($"{authProtocol.Workflow}");
        }

        stopwatch.Stop();
        logger.LogDebug("Basic authentication completed successfully in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
    }

    public static async Task SetApiKeyParametersAsync(HttpClient httpClient, ApiKeyProtocolParameters authProtocol, ILogger<RequestLoggingHandler> logger, CancellationToken cancellation = default)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogDebug("Starting ApiKey authentication with workflow: {Workflow}", authProtocol.Workflow);

        switch (authProtocol.Workflow)
        {
            case BasicAuthentificationWorkflow.Basic:
                httpClient.DefaultRequestHeaders.Add("X-Api-Key", authProtocol.ApiKey ?? string.Empty);
                logger.LogDebug("X-Api-Key header set");
                break;

            case BasicAuthentificationWorkflow.Token:
                {
                    var body = new
                    {
                        Key = authProtocol.ApiKey,
                        authProtocol.ApiKey,
                        SecretKey = authProtocol.ApiKey,
                    };
                    var apiKeyBody = new StringContent(JsonSerialize(body), Encoding.UTF8, "application/json");

                    using var tokenClient = new HttpClient(new RequestLoggingHandler(logger), true);
                    AddHeaders(tokenClient, authProtocol.Headers);
                    httpClient.DefaultRequestHeaders.Add("X-Api-Key", authProtocol.ApiKey ?? string.Empty);
                    logger.LogDebug("Sending ApiKey Token request to {Url}", authProtocol.Url);
                    HttpResponseMessage tokenReponse = await tokenClient.PostAsync(authProtocol.Url, apiKeyBody, cancellation);

                    if (!tokenReponse.IsSuccessStatusCode)
                    {
                        var errorContent = await tokenReponse.Content.ReadAsStringAsync(cancellation);
                        logger.LogError("ApiKey Token flow failed. Status: {StatusCode}, Response: {Response}", tokenReponse.StatusCode, errorContent);
                        throw new HttpRequestException($"ApiKey Token authentication failed with status {tokenReponse.StatusCode}: {errorContent}");
                    }

                    string token = GetToken(await tokenReponse.Content.ReadAsStringAsync(cancellation), logger);
                    httpClient.SetBearerToken(token);
                    break;
                }

            default:
                throw new NotImplementedException($"{authProtocol.Workflow}");
        }

        stopwatch.Stop();
        logger.LogDebug("ApiKey authentication completed successfully in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
    }



    public static void AddHeaders(HttpClient httpClient, Dictionary<string, string> headers)
    {
        if (headers != null)
            foreach (var header in headers)
                httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
    }

    public static string GetCode(string responseContent, ILogger logger = null)
    {
        logger.LogDebug("Parsing code from response: {Response}", responseContent);

        var jObject = JsonConvert.DeserializeObject<JObject>(responseContent);
        if (jObject is null)
            throw new InvalidOperationException($"Failed to deserialize code response: {responseContent}");

        var code = jObject["code"]?.Value<string>() ?? jObject["key"]?.Value<string>();
        if (code is null)
        {
            var availableProperties = string.Join(", ", jObject.Properties().Select(p => p.Name));
            logger.LogError("Could not find 'code' or 'key' in response. Available properties: {Properties}", availableProperties);
            throw new InvalidOperationException($"Can't find the code. Available properties: [{availableProperties}]. Response: {responseContent}");
        }

        logger.LogDebug("Successfully extracted code from response");
        return code;
    }

    public static string GetToken(string responseContent, ILogger logger = null)
    {
        logger.LogDebug("Parsing token from response: {Response}", responseContent);

        var jObject = JsonConvert.DeserializeObject<JObject>(responseContent);
        if (jObject is null)
            throw new InvalidOperationException($"Failed to deserialize token response: {responseContent}");

        var token = jObject["access_token"]?.Value<string>() ?? jObject["token"]?.Value<string>() ?? jObject["accessToken"]?.Value<string>();
        if (token is null)
        {
            var availableProperties = string.Join(", ", jObject.Properties().Select(p => p.Name));
            logger.LogError("Could not find 'access_token', 'token', or 'accessToken' in response. Available properties: {Properties}", availableProperties);
            throw new InvalidOperationException($"Can't find the access_token. Available properties: [{availableProperties}]. Response: {responseContent}");
        }

        logger.LogDebug("Successfully extracted token from response");
        return token;
    }

    private static string JsonSerialize<T>(T obj)
        => JsonConvert.SerializeObject(obj, new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        });
}