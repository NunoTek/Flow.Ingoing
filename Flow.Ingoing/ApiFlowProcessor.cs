using Flow.Ingoing.Consts;
using Flow.Ingoing.Extensions;
using Flow.Ingoing.Helpers;
using Flow.Ingoing.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Polly;
using System.Text;
using System.Xml;

namespace Flow.Ingoing;

public class ApiFlowProcessor
{
    private readonly ILogger<RequestLoggingHandler> _logger;

    public ApiFlowProcessor(ILogger<RequestLoggingHandler> logger)
    {
        _logger = logger;
    }

    public virtual async Task<HttpClient> AuthenticateClientAsync(ApiFlow root, ILogger<RequestLoggingHandler> logger, CancellationToken cancellation = default)
    {
        try
        {
            var httpClient = new HttpClient(new RequestLoggingHandler(logger), true)
            {
                BaseAddress = new Uri(root.BaseUrl),
                Timeout = TimeSpan.FromMinutes(2)
            };

            //HttpClientFactory.AddHeaders(httpClient, root.Headers);

            if (root.AuthentificationProtocol is BasicProtocolParameters basicProtocol)
            {
                await HttpClientFactory.SetBasicParametersAsync(httpClient, basicProtocol, logger, cancellation);
            }
            if (root.AuthentificationProtocol is OAuth2ProtocolParameters oauth2Protocol)
            {
                await HttpClientFactory.SetOAuth2ParametersAsync(httpClient, oauth2Protocol, logger, cancellation);
            }
            if (root.AuthentificationProtocol is ApiKeyProtocolParameters apiKeyProtocol)
            {
                await HttpClientFactory.SetApiKeyParametersAsync(httpClient, apiKeyProtocol, logger, cancellation);
            }

            return httpClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "#-- Processing Authentification Stacks: {name} Error: {error}", root.Name, ex.InnerException?.Message ?? ex.Message);
            throw;
        }
    }

    public async Task<string> ProcessAsync(ApiFlow flow, CancellationToken cancellation = default)
    {
        _logger.LogInformation("#-- Process Stack '{name}' Starting", flow.Name);
        HttpClient httpClient = await AuthenticateClientAsync(flow, _logger, cancellation);

        try
        {
            _logger.LogDebug("#-- Processing Stacks '{name}'", flow.Name);
            var results = await ProcessStackAsync(flow, httpClient, flow.CallStacks, cancellation);

            _logger.LogDebug("#-- Processing Stacks '{name}' Results: {count}", flow.Name, results.Count);
            if (results.Count == 0)
                throw new EntryPointNotFoundException("No results found");

            var root = MergeJToken(new JObject(), results) as JObject;

            if (flow.BaseTag != "")
            {
                flow.BaseTag ??= "FlowRootEntity";

                root = new JObject
                {
                    { flow.BaseTag, root }
                };
            }

            return JsonConvert.SerializeObject(root);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "#-- Processing Stacks: {name} Error: {error}", flow.Name, ex.InnerException?.Message ?? ex.Message);
            throw;
        }
        finally
        {
            httpClient.Dispose();
            _logger.LogInformation("#-- Processing Stacks '{name}' Ended", flow.Name);
        }
    }

    // JToken, JContainer, JObject, JArray - From NewtonSoft Json Linq -  https://www.newtonsoft.com/json/help/html/N_Newtonsoft_Json_Linq.htm
    private async Task<List<JToken>> ProcessStackAsync(ApiFlow flow, HttpClient httpClient, List<CallStack> callStack, CancellationToken cancellation = default)
    {
        List<JToken> results = new();

        if (callStack == null || !callStack.Any())
            return results;

        foreach (var item in callStack)
        {
            try
            {
                string rawResponse = null;

                if (!string.IsNullOrEmpty(item.Path))
                {
                    // Process Links

                    SetRelationLinks(flow, item, httpClient);

                    // Processing Request

                    var requestUrl = new Uri(new Uri(flow.BaseUrl), item.Path).ToString();
                    var requestResponse = await RequestAsync(requestUrl, item, httpClient, flow, cancellation);

                    if (!requestResponse.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("#-- Processing Stack: {statusCode}:{requestUrl} - Error", requestResponse.StatusCode, requestUrl);
                        requestResponse.EnsureSuccessStatusCodeOrThrow();
                    }
                    else if (requestResponse.StatusCode == System.Net.HttpStatusCode.NoContent)
                    {
                        _logger.LogInformation("#-- Processing Stack: {statusCode}:{requestUrl} - No Content", requestResponse.StatusCode, requestUrl);
                    }
                    else
                    {
                        rawResponse = await requestResponse.Content.ReadAsStringAsync(cancellation);
                        _logger.LogDebug("#-- Processing Stack: {statusCode}:{requestUrl} - Success:\r\n{response}", requestResponse.StatusCode, requestUrl, rawResponse);
                    }
                }

                // Processing Response

                JToken result = null;
                try
                {
                    result = ParseValue(flow, item, rawResponse ?? item.NullSubstitue);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "#-- Processing Stack: Error processing value for {name}", item.Name);
                }

                // Process Childrens

                if (item.Childrens != null && item.Childrens.Any())
                {
                    async Task<List<JToken>> ProcessChildsAsync(JToken currentResult, List<CallStack> childs, CancellationToken cancellation = default)
                    {
                        var parent = currentResult is JObject ? currentResult as JObject : null;
                        childs.ForEach(child => SetRelationLinks(flow, child, httpClient, parent));
                        return await ProcessStackAsync(flow, httpClient, childs, cancellation);
                    }

                    if (result is JArray dtos)
                    {
                        foreach (var dto in dtos)
                            MergeJToken(dto, await ProcessChildsAsync(dto, item.Childrens, cancellation));
                    }
                    else
                    {
                        MergeJToken(result, await ProcessChildsAsync(result, item.Childrens, cancellation));
                    }
                }

                // Naming object
                if (!string.IsNullOrEmpty(item.Name))
                {
                    result = SetPropertyValue(item.Name, result);
                }

                results.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "#-- Processing Stack: Error while processing {name}: {path}", item.Name, item.Path);
            }
        }

        return results;
    }

    private async Task<JToken> ProcessAndMergeStacksAsync(JToken result, List<Func<CancellationToken, Task<List<JToken>>>> actions, CancellationToken cancellation = default)
    {
        var tasks = actions.Select(async action => await action(cancellation));

        await Task.WhenAll(tasks);

        var results = tasks.SelectMany(x => x.Result).ToList();

        result = MergeJToken(result, results);

        return result;
    }

    private static JContainer MergeJToken(JToken root, List<JToken> contents)
    {
        var container = root as JContainer;
        var settings = new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Concat };

        foreach (var content in contents)
            container.Merge(content, settings);

        return container;
    }

    private static bool HasProperty(dynamic obj, string propertyName)
    {
        if (obj is JObject jObject)
            return jObject.ContainsKey(propertyName);

        if (obj is JArray jArray)
            return jArray.Any(x => HasProperty(x, propertyName));

        return false;
    }

    private static JToken GetPropertyValue(dynamic obj, string propertyName)
    {
        if (obj is JObject jObject)
            return jObject[propertyName];

        if (obj is JArray jArray)
            return jArray.FirstOrDefault(x => HasProperty(x, propertyName));

        return null;
    }

    private static JToken SetPropertyValue(string propertyName, JToken obj)
    {
        if (obj is JArray jArray)
            return new JObject
            {
                { propertyName, MergeJToken(new JArray(), new List<JToken>() { obj }) }
            };

        if (obj is JObject jObject)
            return new JObject { { propertyName, jObject } };

        return obj;
    }


    public virtual JToken ParseValue(ApiFlow flow, CallStack item, string rawContent)
    {
        JToken ParseJsonValue(string json) => JsonConvert.DeserializeObject<JToken>(json);
        JToken ParseXmlValue(string xml)
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            var json = JsonConvert.SerializeXmlNode(doc);
            return ParseJsonValue(json);
        }

        JToken result = null;
        if (flow.ContentType == ContentTypes.Json)
        {
            result = ParseJsonValue(rawContent);
        }
        if (flow.ContentType == ContentTypes.Xml)
        {
            result = ParseXmlValue(rawContent);
        }

        if (!string.IsNullOrEmpty(item.ResponseToMap))
            return HasProperty(result, item.ResponseToMap) ? GetPropertyValue(result, item.ResponseToMap) : null;

        return result;
    }


    public virtual void SetRelationLinks(ApiFlow root, CallStack item, HttpClient httpClient, JObject response = null)
    {
        // Clone Headers
        var headers = root.Headers?.ToDictionary(e => e.Key, e => e.Value);

        if (item.Links == null || !item.Links.Any())
        {
            HttpClientFactory.AddHeaders(httpClient, headers);
            return;
        }

        foreach (var link in item.Links)
        {
            try
            {
                var mappedValue = link.Value;
                if (response != null && HasProperty(response, link.Value))
                    mappedValue = GetPropertyValue(response, link.Value).ToString();

                item.Path = item.Path.Replace(link.Key, mappedValue);

                if (headers != null && headers.ContainsKey(link.Key))
                    headers[link.Key] = mappedValue;

                if (item.Body != null && item.Body.Values.Any())
                    item.Body = item.Body.ToDictionary(e => e.Key.Replace(link.Key, mappedValue), e => e.Value.Replace(link.Key, mappedValue));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while loading links for {name}", item.Name);
            }
        }

        HttpClientFactory.AddHeaders(httpClient, headers);
    }

    private async Task<HttpResponseMessage> RequestAsync(string requestUrl, CallStack item, HttpClient httpClient, ApiFlow root, CancellationToken cancellation = default)
        => await RetryOnErrorAsync(async (cancellation) =>
            {
                var serializedBody = JsonConvert.SerializeObject(item.Body ?? new Dictionary<string, string>()); // TODO: Create a body or empty in property CallStack
                var content = new StringContent(serializedBody, Encoding.UTF8, "application/json");

                HttpResponseMessage response;
                switch (item.ApiMethod)
                {
                    case HttpVerbs.Post:
                        response = await httpClient.PostAsync(requestUrl, content, cancellation);
                        break;
                    case HttpVerbs.Patch:
                        response = await httpClient.PatchAsync(requestUrl, content, cancellation);
                        break;
                    case HttpVerbs.Put:
                        response = await httpClient.PutAsync(requestUrl, content, cancellation);
                        break;
                    case HttpVerbs.Delete:
                        response = await httpClient.DeleteAsync(requestUrl, cancellation);
                        break;
                    case HttpVerbs.Get:
                    default:
                        response = await httpClient.GetAsync(requestUrl, cancellation);
                        break;
                }

                switch (response.StatusCode)
                {
                    case System.Net.HttpStatusCode.Forbidden:
                    case System.Net.HttpStatusCode.Unauthorized:
                        httpClient = await AuthenticateClientAsync(root, _logger, cancellation);
                        response.EnsureSuccessStatusCodeOrThrow();
                        break;
                }

                return response;
            }, cancellation);

    private static async Task<HttpResponseMessage> RetryOnErrorAsync(Func<CancellationToken, Task<HttpResponseMessage>> action, CancellationToken cancellation = default)
        => await Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(5, (i) => TimeSpan.FromSeconds(i * 1))
            .ExecuteAsync(action, cancellation);

}