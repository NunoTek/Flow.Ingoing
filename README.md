# Flow.Ingoing

Flow.Ingoing is a powerful .NET library that simplifies API integration by providing a flexible and configurable way to process API flows with support for various authentication methods, content types, and nested call stacks.

## Features

- 🔐 Multiple authentication protocols support (Basic, OAuth2, API Key)
- 📄 JSON and XML content type handling
- 🔄 Nested API call stacks processing
- 🔗 Dynamic link substitution
- 🔁 Automatic retry policy on failures
- 📝 Comprehensive logging
- ⚡ Asynchronous operations

## Installation

```bash
dotnet add package Flow.Ingoing
```

## Quick Start

```csharp
using Flow.Ingoing;
using Flow.Ingoing.Models;

// Create API flow configuration
var flow = new ApiFlow
{
    Name = "MyApiFlow",
    BaseUrl = "https://api.example.com",
    ContentType = ContentTypes.Json,
    CallStacks = new List<CallStack>
    {
        new CallStack
        {
            Name = "Users",
            Path = "/api/users",
            ApiMethod = HttpVerbs.Get
        }
    }
};

// Initialize processor
var processor = new ApiFlowProcessor(logger);

// Process the flow
var result = await processor.ProcessAsync(flow);
```

## Authentication

The library supports multiple authentication protocols:

### Basic Authentication
```csharp
var flow = new ApiFlow
{
    AuthentificationProtocol = new BasicProtocolParameters
    {
        Username = "user",
        Password = "pass"
    }
};
```

### OAuth2
```csharp
var flow = new ApiFlow
{
    AuthentificationProtocol = new OAuth2ProtocolParameters
    {
        TokenEndpoint = "https://auth.example.com/token",
        ClientId = "client_id",
        ClientSecret = "client_secret"
    }
};
```

### API Key
```csharp
var flow = new ApiFlow
{
    AuthentificationProtocol = new ApiKeyProtocolParameters
    {
        Key = "api_key",
        Value = "your_api_key"
    }
};
```

## Advanced Usage

### Nested Call Stacks

```csharp
var flow = new ApiFlow
{
    CallStacks = new List<CallStack>
    {
        new CallStack
        {
            Name = "Users",
            Path = "/api/users",
            Childrens = new List<CallStack>
            {
                new CallStack
                {
                    Name = "UserPosts",
                    Path = "/api/users/{userId}/posts",
                    Links = new Dictionary<string, string>
                    {
                        {"{userId}", "id"}
                    }
                }
            }
        }
    }
};
```

### Dynamic Link Substitution

```csharp
var callStack = new CallStack
{
    Path = "/api/resources/{resourceId}",
    Links = new Dictionary<string, string>
    {
        {"{resourceId}", "dynamicValue"}
    }
};
```

### Error Handling

The library includes built-in retry logic for failed requests:

```csharp
try
{
    var result = await processor.ProcessAsync(flow);
}
catch (Exception ex)
{
    // Handle errors
    logger.LogError(ex, "Error processing flow");
}
```

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Author

Nuno ARAUJO

## Repository

[GitHub Repository](https://github.com/NunoTek/Flow.Ingoing) 