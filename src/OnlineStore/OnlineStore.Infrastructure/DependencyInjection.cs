using Microsoft.Extensions.DependencyInjection;
using OnlineStore.Application.Abstractions;
using OnlineStore.Infrastructure.Queues;

namespace OnlineStore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string storageConnection)
    {
        services.AddSingleton<IOrderQueue>(_ => new AzureOrderQueue(storageConnection));
        return services;
    }
}