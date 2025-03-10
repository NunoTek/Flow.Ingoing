using Flow.Ingoing.Consts;
using Flow.Ingoing.Models;
using IdentityModel.Client;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Text;

namespace Flow.Ingoing.Helpers;

public static class HttpClientFactory
{
    public static async Task SetOAuth2ParametersAsync(HttpClient httpClient, OAuth2ProtocolParameters authProtocol, ILogger<RequestLoggingHandler> logger, CancellationToken cancellation = default)
    {
        var tokenClient = new HttpClient(new RequestLoggingHandler(logger), true);
        AddHeaders(tokenClient, authProtocol.Headers);

        string token = string.Empty;

        // TODO: init only what we need
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

        //if (!string.IsNullOrEmpty(authProtocol.ClientId))
        //{
        //    body = body with
        //    {
        //        ClientId = authProtocol.ClientId,
        //        ClientSecret = authProtocol.ClientSecret,
        //    };
        //}

        //if (!string.IsNullOrEmpty(authProtocol.Username))
        //{
        //    body = body with
        //    {
        //        Email = authProtocol.Username,
        //        Login = authProtocol.Username,
        //        Username = authProtocol.Username,
        //        Password = authProtocol.Password,
        //    };
        //}

        switch (authProtocol.Workflow)
        {
            case OAuth2AuthentificationWorkflow.Password:
                body = body with { GrantType = "password", grant_type = "password" }; // C#10 Net6.0

                var passwordBody = new StringContent(JsonSerialize(body), Encoding.UTF8, "application/json");
                HttpResponseMessage passwordTokenReponse = await tokenClient.PostAsync(new Uri(authProtocol.Url), passwordBody, cancellation);

                if (!passwordTokenReponse.IsSuccessStatusCode)
                    throw new Exception("Error while retrieving authentication token data");

                token = GetToken(await passwordTokenReponse.Content.ReadAsStringAsync(cancellation));
                break;

            case OAuth2AuthentificationWorkflow.ClientCredentials:
                body = body with { GrantType = "client_credentials", grant_type = "client_credentials" }; // C#10 Net6.0

                var clientBody = new StringContent(JsonSerialize(body), Encoding.UTF8, "application/json");
                HttpResponseMessage clientTokenReponse = await tokenClient.PostAsync(new Uri(authProtocol.Url), clientBody, cancellation);

                if (!clientTokenReponse.IsSuccessStatusCode)
                    throw new Exception("Error while retrieving authentication token data");

                token = GetToken(await clientTokenReponse.Content.ReadAsStringAsync(cancellation));
                break;

            case OAuth2AuthentificationWorkflow.AuthorizationCode:
                {
                    body = body with { GrantType = "authorization_code", grant_type = "authorization_code" }; // C#10 Net6.0

                    var codeTokenBody = new StringContent(JsonSerialize(body), Encoding.UTF8, "application/json");
                    HttpResponseMessage codeTokenReponse = await tokenClient.PostAsync(authProtocol.Url, codeTokenBody, cancellation);

                    if (!codeTokenReponse.IsSuccessStatusCode)
                        throw new Exception("Error while retrieving authentication code data");

                    var code = GetCode(await codeTokenReponse.Content.ReadAsStringAsync(cancellation));

                    using var codeClient = new HttpClient(new RequestLoggingHandler(logger), true);
                    AddHeaders(codeClient, authProtocol.Headers);
                    var codeBody = new StringContent(JsonSerialize(new { Code = code }), Encoding.UTF8, "application/json");
                    HttpResponseMessage codeReponse = await codeClient.PostAsync(authProtocol.Url, codeBody, cancellation);

                    if (!codeReponse.IsSuccessStatusCode)
                        throw new Exception("Error while retrieving authentication code_token data");

                    token = GetToken(await codeReponse.Content.ReadAsStringAsync(cancellation));
                    break;
                }

            case OAuth2AuthentificationWorkflow.ResourceOwnerPasswordCredentials:
            default:
                throw new NotImplementedException($"{authProtocol.Workflow}");
        }

        httpClient.SetBearerToken(token);
    }

    public static async Task SetBasicParametersAsync(HttpClient httpClient, BasicProtocolParameters authProtocol, ILogger<RequestLoggingHandler> logger, CancellationToken cancellation = default)
    {
        switch (authProtocol.Workflow)
        {
            case BasicAuthentificationWorkflow.Basic:
                httpClient.DefaultRequestHeaders.Authorization = new BasicAuthenticationHeaderValue(authProtocol.Username ?? string.Empty, authProtocol.Password ?? string.Empty);
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
                    HttpResponseMessage tokenReponse = await tokenClient.PostAsync(authProtocol.Url, codeTokenBody, cancellation);

                    if (!tokenReponse.IsSuccessStatusCode)
                        throw new Exception("Error while retrieving authentication token data");

                    string token = GetToken(await tokenReponse.Content.ReadAsStringAsync(cancellation));
                    httpClient.SetBearerToken(token);
                    break;
                }

            default:
                throw new NotImplementedException($"{authProtocol.Workflow}");
        }
    }

    public static async Task SetApiKeyParametersAsync(HttpClient httpClient, ApiKeyProtocolParameters authProtocol, ILogger<RequestLoggingHandler> logger, CancellationToken cancellation = default)
    {
        switch (authProtocol.Workflow)
        {
            case BasicAuthentificationWorkflow.Basic:
                httpClient.DefaultRequestHeaders.Add("X-Api-Key", authProtocol.ApiKey ?? string.Empty);
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
                    HttpResponseMessage tokenReponse = await tokenClient.PostAsync(authProtocol.Url, apiKeyBody, cancellation);

                    if (!tokenReponse.IsSuccessStatusCode)
                        throw new Exception("Error while retrieving authentication token data");

                    string token = GetToken(await tokenReponse.Content.ReadAsStringAsync(cancellation));
                    httpClient.SetBearerToken(token);
                    break;
                }

            default:
                throw new NotImplementedException($"{authProtocol.Workflow}");
        }
    }



    public static void AddHeaders(HttpClient httpClient, Dictionary<string, string> headers)
    {
        if (headers != null)
            foreach (var header in headers)
                httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
    }

    public static string GetCode(string responseContent)
    {
        dynamic response = JsonConvert.DeserializeObject<dynamic>(responseContent);
        return response.code ?? response.key ?? throw new Exception("Can't find the code.");
    }

    public static string GetToken(string responseContent)
    {
        dynamic response = JsonConvert.DeserializeObject<dynamic>(responseContent);
        return response.access_token ?? response.token ?? response.accessToken ?? throw new Exception("Can't find the access_token.");
    }

    private static string JsonSerialize<T>(T obj)
        => JsonConvert.SerializeObject(obj, new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        });
}