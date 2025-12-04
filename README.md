# Flow.Ingoing

[![NuGet](https://img.shields.io/nuget/v/Flow.Ingoing.svg)](https://www.nuget.org/packages/Flow.Ingoing)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**Flow.Ingoing** is a powerful .NET library that simplifies API integration by providing a flexible and configurable way to process API flows with support for various authentication methods, content types, and nested call stacks.

## ✨ Features

- 🔐 **Multiple Authentication Protocols** — Basic, OAuth2 (Client Credentials, Password, Authorization Code), and API Key
- 📄 **Content Type Handling** — JSON and XML with automatic fallback parsing
- 🔄 **Nested API Call Stacks** — Chain API calls with parent-child relationships
- 🔗 **Dynamic Link Substitution** — Inject response values into subsequent requests
- 🔁 **Automatic Retry Policy** — Built-in retry logic with exponential backoff (5 retries)
- 📝 **Comprehensive Logging** — Full request/response logging via `ILogger`
- ⚡ **Fully Asynchronous** — Non-blocking operations with `CancellationToken` support
- 💉 **Dependency Injection Ready** — Easy integration with ASP.NET Core

---

## 📦 Installation

```bash
dotnet add package Flow.Ingoing
```

Or via the Package Manager Console:

```powershell
Install-Package Flow.Ingoing
```

---

## 🚀 Quick Start

### Basic Example

```csharp
using Flow.Ingoing;
using Flow.Ingoing.Consts;
using Flow.Ingoing.Models;
using Microsoft.Extensions.Logging;

// Create a logger (or inject via DI)
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<RequestLoggingHandler>();

// Define your API flow
var flow = new ApiFlow
{
    Name = "GetUsers",
    BaseUrl = "https://jsonplaceholder.typicode.com",
    ContentType = ContentTypes.Json,
    CallStacks = new List<CallStack>
    {
        new CallStack
        {
            Name = "Users",
            Path = "/users",
            ApiMethod = HttpVerbs.Get
        }
    }
};

// Process the flow
var processor = new ApiFlowProcessor(logger);
string result = await processor.ProcessAsync(flow);

Console.WriteLine(result);
```

### Dependency Injection Setup (ASP.NET Core)

```csharp
using Flow.Ingoing.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Register Flow.Ingoing services
builder.Services.AddFlowIngoingS();

var app = builder.Build();
```

Then inject `ApiFlowProcessor` into your services:

```csharp
public class MyApiService
{
    private readonly ApiFlowProcessor _processor;

    public MyApiService(ApiFlowProcessor processor)
    {
        _processor = processor;
    }

    public async Task<string> FetchDataAsync(CancellationToken cancellationToken = default)
    {
        var flow = new ApiFlow
        {
            Name = "FetchData",
            BaseUrl = "https://api.example.com",
            CallStacks = new List<CallStack>
            {
                new CallStack { Name = "Data", Path = "/data", ApiMethod = HttpVerbs.Get }
            }
        };

        return await _processor.ProcessAsync(flow, cancellationToken);
    }
}
```

---

## 🔐 Authentication

Flow.Ingoing supports multiple authentication protocols with various workflows.

### Basic Authentication

#### Simple Basic Auth (Header)

```csharp
var flow = new ApiFlow
{
    Name = "BasicAuthFlow",
    BaseUrl = "https://api.example.com",
    AuthentificationProtocol = new BasicProtocolParameters
    {
        Workflow = BasicAuthentificationWorkflow.Basic,
        Username = "your_username",
        Password = "your_password"
    },
    CallStacks = new List<CallStack>
    {
        new CallStack { Name = "SecureData", Path = "/secure/data", ApiMethod = HttpVerbs.Get }
    }
};
```

#### Token-Based Basic Auth

Retrieves a token using basic credentials, then uses the token for subsequent requests:

```csharp
var flow = new ApiFlow
{
    Name = "TokenBasedAuth",
    BaseUrl = "https://api.example.com",
    AuthentificationProtocol = new BasicProtocolParameters
    {
        Workflow = BasicAuthentificationWorkflow.Token,
        Url = "https://auth.example.com/token",
        Username = "your_username",
        Password = "your_password",
        Headers = new Dictionary<string, string>
        {
            { "X-Client-Id", "my-client" }
        }
    },
    CallStacks = new List<CallStack>
    {
        new CallStack { Name = "Data", Path = "/api/data", ApiMethod = HttpVerbs.Get }
    }
};
```

### OAuth2 Authentication

#### Client Credentials Flow

```csharp
var flow = new ApiFlow
{
    Name = "OAuth2ClientCredentials",
    BaseUrl = "https://api.example.com",
    AuthentificationProtocol = new OAuth2ProtocolParameters
    {
        Workflow = OAuth2AuthentificationWorkflow.ClientCredentials,
        Url = "https://auth.example.com/oauth/token",
        ClientId = "your_client_id",
        ClientSecret = "your_client_secret"
    },
    CallStacks = new List<CallStack>
    {
        new CallStack { Name = "Resources", Path = "/api/resources", ApiMethod = HttpVerbs.Get }
    }
};
```

#### Password Flow (Resource Owner)

```csharp
var flow = new ApiFlow
{
    Name = "OAuth2Password",
    BaseUrl = "https://api.example.com",
    AuthentificationProtocol = new OAuth2ProtocolParameters
    {
        Workflow = OAuth2AuthentificationWorkflow.Password,
        Url = "https://auth.example.com/oauth/token",
        ClientId = "your_client_id",
        ClientSecret = "your_client_secret",
        Username = "user@example.com",
        Password = "user_password"
    },
    CallStacks = new List<CallStack>
    {
        new CallStack { Name = "Profile", Path = "/api/profile", ApiMethod = HttpVerbs.Get }
    }
};
```

#### Authorization Code Flow

```csharp
var flow = new ApiFlow
{
    Name = "OAuth2AuthCode",
    BaseUrl = "https://api.example.com",
    AuthentificationProtocol = new OAuth2ProtocolParameters
    {
        Workflow = OAuth2AuthentificationWorkflow.AuthorizationCode,
        Url = "https://auth.example.com/oauth/authorize",
        ClientId = "your_client_id",
        ClientSecret = "your_client_secret"
    },
    CallStacks = new List<CallStack>
    {
        new CallStack { Name = "UserData", Path = "/api/user", ApiMethod = HttpVerbs.Get }
    }
};
```

### API Key Authentication

#### Header-Based API Key

```csharp
var flow = new ApiFlow
{
    Name = "ApiKeyAuth",
    BaseUrl = "https://api.example.com",
    AuthentificationProtocol = new ApiKeyProtocolParameters
    {
        Workflow = BasicAuthentificationWorkflow.Basic,
        ApiKey = "your_api_key_here"
    },
    CallStacks = new List<CallStack>
    {
        new CallStack { Name = "Data", Path = "/api/data", ApiMethod = HttpVerbs.Get }
    }
};
```

#### Token-Based API Key

Uses the API key to retrieve a bearer token:

```csharp
var flow = new ApiFlow
{
    Name = "ApiKeyTokenAuth",
    BaseUrl = "https://api.example.com",
    AuthentificationProtocol = new ApiKeyProtocolParameters
    {
        Workflow = BasicAuthentificationWorkflow.Token,
        Url = "https://auth.example.com/api-key/token",
        ApiKey = "your_api_key_here"
    },
    CallStacks = new List<CallStack>
    {
        new CallStack { Name = "SecureData", Path = "/api/secure", ApiMethod = HttpVerbs.Get }
    }
};
```

---

## 🔄 HTTP Methods

Flow.Ingoing supports all common HTTP verbs:

```csharp
var flow = new ApiFlow
{
    Name = "CrudOperations",
    BaseUrl = "https://api.example.com",
    CallStacks = new List<CallStack>
    {
        // GET request
        new CallStack
        {
            Name = "GetUsers",
            Path = "/users",
            ApiMethod = HttpVerbs.Get
        },
        // POST request with body
        new CallStack
        {
            Name = "CreateUser",
            Path = "/users",
            ApiMethod = HttpVerbs.Post,
            Body = new Dictionary<string, string>
            {
                { "name", "John Doe" },
                { "email", "john@example.com" }
            }
        },
        // PUT request
        new CallStack
        {
            Name = "UpdateUser",
            Path = "/users/1",
            ApiMethod = HttpVerbs.Put,
            Body = new Dictionary<string, string>
            {
                { "name", "Jane Doe" }
            }
        },
        // PATCH request
        new CallStack
        {
            Name = "PatchUser",
            Path = "/users/1",
            ApiMethod = HttpVerbs.Patch,
            Body = new Dictionary<string, string>
            {
                { "status", "active" }
            }
        },
        // DELETE request
        new CallStack
        {
            Name = "DeleteUser",
            Path = "/users/1",
            ApiMethod = HttpVerbs.Delete
        }
    }
};
```

---

## 🔗 Advanced Usage

### Custom Headers

Add custom headers to all requests in a flow:

```csharp
var flow = new ApiFlow
{
    Name = "CustomHeadersFlow",
    BaseUrl = "https://api.example.com",
    Headers = new Dictionary<string, string>
    {
        { "X-Custom-Header", "custom-value" },
        { "Accept-Language", "en-US" },
        { "X-Request-Id", Guid.NewGuid().ToString() }
    },
    CallStacks = new List<CallStack>
    {
        new CallStack { Name = "Data", Path = "/api/data", ApiMethod = HttpVerbs.Get }
    }
};
```

### Nested Call Stacks (Parent-Child Relationships)

Chain API calls where child requests use data from parent responses:

```csharp
var flow = new ApiFlow
{
    Name = "NestedCallsFlow",
    BaseUrl = "https://jsonplaceholder.typicode.com",
    BaseTag = "ApiResponse",
    CallStacks = new List<CallStack>
    {
        new CallStack
        {
            Name = "Users",
            Path = "/users",
            ApiMethod = HttpVerbs.Get,
            Childrens = new List<CallStack>
            {
                new CallStack
                {
                    Name = "Posts",
                    Path = "/users/{userId}/posts",
                    ApiMethod = HttpVerbs.Get,
                    // Maps {userId} from parent response's "id" field
                    Links = new Dictionary<string, string>
                    {
                        { "{userId}", "id" }
                    },
                    Childrens = new List<CallStack>
                    {
                        new CallStack
                        {
                            Name = "Comments",
                            Path = "/posts/{postId}/comments",
                            ApiMethod = HttpVerbs.Get,
                            Links = new Dictionary<string, string>
                            {
                                { "{postId}", "id" }
                            }
                        }
                    }
                },
                new CallStack
                {
                    Name = "Todos",
                    Path = "/users/{userId}/todos",
                    ApiMethod = HttpVerbs.Get,
                    Links = new Dictionary<string, string>
                    {
                        { "{userId}", "id" }
                    }
                }
            }
        }
    }
};

// Result will be a nested JSON with Users -> Posts -> Comments and Users -> Todos
string result = await processor.ProcessAsync(flow);
```

### Dynamic Link Substitution

Use links to dynamically inject values into paths, headers, and body:

```csharp
var flow = new ApiFlow
{
    Name = "DynamicLinksFlow",
    BaseUrl = "https://api.example.com",
    Headers = new Dictionary<string, string>
    {
        { "X-Tenant-Id", "{tenantId}" }
    },
    CallStacks = new List<CallStack>
    {
        new CallStack
        {
            Name = "TenantData",
            Path = "/tenants/{tenantId}/resources/{resourceType}",
            ApiMethod = HttpVerbs.Get,
            Links = new Dictionary<string, string>
            {
                { "{tenantId}", "tenant-123" },
                { "{resourceType}", "documents" }
            }
        }
    }
};
```

### Response Mapping

Extract specific properties from API responses using `ResponseToMap`:

```csharp
var flow = new ApiFlow
{
    Name = "ResponseMappingFlow",
    BaseUrl = "https://api.example.com",
    CallStacks = new List<CallStack>
    {
        // Extract nested property
        new CallStack
        {
            Name = "UserProfile",
            Path = "/users/1",
            ApiMethod = HttpVerbs.Get,
            ResponseToMap = "data.profile"  // Maps to response.data.profile
        },
        // Extract single property
        new CallStack
        {
            Name = "Items",
            Path = "/items",
            ApiMethod = HttpVerbs.Get,
            ResponseToMap = "results"  // Maps to response.results
        }
    }
};
```

### Null Substitution

Provide default values when a call stack doesn't make a request:

```csharp
var flow = new ApiFlow
{
    Name = "NullSubstitutionFlow",
    BaseUrl = "https://api.example.com",
    CallStacks = new List<CallStack>
    {
        new CallStack
        {
            Name = "DefaultConfig",
            // No path means no HTTP request
            NullSubstitue = """{"theme": "dark", "language": "en"}"""
        },
        new CallStack
        {
            Name = "UserSettings",
            Path = "/users/1/settings",
            ApiMethod = HttpVerbs.Get
        }
    }
};
```

### XML Content Type

Handle XML APIs with automatic parsing:

```csharp
var flow = new ApiFlow
{
    Name = "XmlApiFlow",
    BaseUrl = "https://xml-api.example.com",
    ContentType = ContentTypes.Xml,
    CallStacks = new List<CallStack>
    {
        new CallStack
        {
            Name = "XmlData",
            Path = "/api/data.xml",
            ApiMethod = HttpVerbs.Get,
            ResponseToMap = "root.items"
        }
    }
};
```

### Base Tag Wrapper

Wrap all results in a root element:

```csharp
var flow = new ApiFlow
{
    Name = "WrappedResultFlow",
    BaseUrl = "https://api.example.com",
    BaseTag = "ApiResponse",  // Wraps result in {"ApiResponse": {...}}
    CallStacks = new List<CallStack>
    {
        new CallStack { Name = "Users", Path = "/users", ApiMethod = HttpVerbs.Get },
        new CallStack { Name = "Products", Path = "/products", ApiMethod = HttpVerbs.Get }
    }
};

// Result: {"ApiResponse": {"Users": [...], "Products": [...]}}
```

---

## ⚠️ Error Handling

The library includes built-in retry logic (5 attempts with incremental delay) and comprehensive error handling:

```csharp
try
{
    var result = await processor.ProcessAsync(flow, cancellationToken);
    Console.WriteLine(result);
}
catch (HttpRequestException ex)
{
    // Handle HTTP-specific errors (401, 403, 404, 500, etc.)
    logger.LogError(ex, "HTTP request failed");
}
catch (InvalidOperationException ex)
{
    // Handle parsing errors (invalid JSON/XML)
    logger.LogError(ex, "Failed to parse response");
}
catch (EntryPointNotFoundException ex)
{
    // Handle empty results
    logger.LogWarning(ex, "No results found");
}
catch (Exception ex)
{
    // Handle general errors
    logger.LogError(ex, "Error processing flow: {Name}", flow.Name);
}
```

### Automatic Re-authentication

When receiving `401 Unauthorized` or `403 Forbidden`, the processor automatically re-authenticates and retries the request:

```csharp
// The processor handles token expiration automatically
var result = await processor.ProcessAsync(flow);
// If token expired during processing, it will re-authenticate and retry
```

---

## 📋 Complete Real-World Example

Here's a complete example fetching users with their posts and comments from JSONPlaceholder:

```csharp
using Flow.Ingoing;
using Flow.Ingoing.Consts;
using Flow.Ingoing.Models;
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder => 
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
var logger = loggerFactory.CreateLogger<RequestLoggingHandler>();

var flow = new ApiFlow
{
    Name = "JSONPlaceholderFullFlow",
    BaseUrl = "https://jsonplaceholder.typicode.com",
    ContentType = ContentTypes.Json,
    BaseTag = "Data",
    Headers = new Dictionary<string, string>
    {
        { "Accept", "application/json" }
    },
    CallStacks = new List<CallStack>
    {
        new CallStack
        {
            Name = "Users",
            Path = "/users",
            ApiMethod = HttpVerbs.Get,
            Childrens = new List<CallStack>
            {
                new CallStack
                {
                    Name = "Posts",
                    Path = "/posts?userId={userId}",
                    ApiMethod = HttpVerbs.Get,
                    Links = new Dictionary<string, string>
                    {
                        { "{userId}", "id" }
                    }
                },
                new CallStack
                {
                    Name = "Albums",
                    Path = "/albums?userId={userId}",
                    ApiMethod = HttpVerbs.Get,
                    Links = new Dictionary<string, string>
                    {
                        { "{userId}", "id" }
                    }
                }
            }
        }
    }
};

var processor = new ApiFlowProcessor(logger);
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

try
{
    string result = await processor.ProcessAsync(flow, cts.Token);
    
    // Parse and use the result
    var data = System.Text.Json.JsonDocument.Parse(result);
    Console.WriteLine($"Fetched {data.RootElement.GetProperty("Data")
        .GetProperty("Users").GetArrayLength()} users with their posts and albums");
}
catch (OperationCanceledException)
{
    Console.WriteLine("Operation was cancelled");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
```

---

## 🧪 Configuration Reference

### ApiFlow Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Identifier for the flow (used in logging) |
| `BaseUrl` | `string` | Base URL for all API calls |
| `BaseTag` | `string` | Optional root wrapper for the result JSON |
| `ContentType` | `ContentTypes` | Expected response format (`Json` or `Xml`) |
| `Headers` | `Dictionary<string, string>` | Custom headers for all requests |
| `AuthentificationProtocol` | `AuthentificationProtocolParameters` | Authentication configuration |
| `CallStacks` | `List<CallStack>` | List of API calls to execute |

### CallStack Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Property name in the result JSON |
| `Path` | `string` | API endpoint path (supports placeholders) |
| `ApiMethod` | `HttpVerbs` | HTTP method (`Get`, `Post`, `Put`, `Patch`, `Delete`) |
| `Links` | `Dictionary<string, string>` | Placeholder-to-value mappings |
| `Body` | `Dictionary<string, string>` | Request body for POST/PUT/PATCH |
| `ResponseToMap` | `string` | Dot-notation path to extract from response |
| `NullSubstitue` | `string` | Default JSON when no request is made |
| `Childrens` | `List<CallStack>` | Nested API calls using parent data |

---

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 👤 Author

Nuno ARAUJO

---

## 🔗 Links

- [GitHub Repository](https://github.com/NunoTek/Flow.Ingoing)
- [NuGet Package](https://www.nuget.org/packages/Flow.Ingoing)
- [Report Issues](https://github.com/NunoTek/Flow.Ingoing/issues)