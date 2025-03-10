using Microsoft.Extensions.DependencyInjection;

namespace Flow.Ingoing.Extensions;

public static class FlowIngoingServiceCollectionExtension
{
    public static IServiceCollection AddFlowIngoingS(this IServiceCollection services)
        => services.AddScoped<ApiFlowProcessor>();
}
