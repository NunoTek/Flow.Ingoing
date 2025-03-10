namespace Flow.Ingoing.Consts;

public enum HttpVerbs
{
    Get,
    Post,
    Put,
    Patch,
    Delete
}

public enum ContentTypes
{
    Json,
    Xml
}



public enum BasicAuthentificationWorkflow
{
    Basic = 0,
    Token
}

public enum OAuth2AuthentificationWorkflow
{
    ClientCredentials = 0,
    AuthorizationCode,
    Password,
    ResourceOwnerPasswordCredentials,
}