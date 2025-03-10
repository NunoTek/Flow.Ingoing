using Flow.Ingoing.Consts;

namespace Flow.Ingoing.Models;

public class ApiFlow
{
    public string Name { get; set; }

    public string BaseUrl { get; set; }

    public string BaseTag { get; set; }

    public ContentTypes ContentType { get; set; } = ContentTypes.Json;

    public Dictionary<string, string> Headers { get; set; }


    public AuthentificationProtocolParameters AuthentificationProtocol { get; set; }

    public List<CallStack> CallStacks { get; set; }

}

public class CallStack
{
    public string Name { get; set; }

    public string Path { get; set; }

    public HttpVerbs? ApiMethod { get; set; }

    public Dictionary<string, string> Links { get; set; }

    public Dictionary<string, string> Body { get; set; }

    public string ResponseToMap { get; set; }

    public string NullSubstitue { get; set; }

    public List<CallStack> Childrens { get; set; }

}