using Flow.Ingoing.Consts;

namespace Flow.Ingoing.Models;

public abstract class AuthentificationProtocolParameters
{
    public string Url { get; set; }

    public Dictionary<string, string> Headers { get; set; }
}

public class OAuth2ProtocolParameters : AuthentificationProtocolParameters
{
    public OAuth2AuthentificationWorkflow Workflow { get; set; }

    public string ClientId { get; set; }
    public string ClientSecret { get; set; }

    public string Username { get; set; }
    public string Password { get; set; }
}

public class ApiKeyProtocolParameters : AuthentificationProtocolParameters
{
    public BasicAuthentificationWorkflow Workflow { get; set; }

    public string ApiKey { get; set; }
}

public class BasicProtocolParameters : AuthentificationProtocolParameters
{
    public BasicAuthentificationWorkflow Workflow { get; set; }

    public string Username { get; set; }
    public string Password { get; set; }
}
