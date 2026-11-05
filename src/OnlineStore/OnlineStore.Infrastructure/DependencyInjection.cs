using Microsoft.Extensions.DependencyInjection;
using OnlineStore.Application.Abstractions;
using OnlineStore.Infrastructure.Blobs;
using OnlineStore.Infrastructure.Queues;
using OnlineStore.Infrastructure.Tables;

namespace OnlineStore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string storageConnection)
    {
        services.AddSingleton<IOrderQueue>(_ => new AzureOrderQueue(storageConnection));
        services.AddSingleton<AzureOrderStore>(_ => new AzureOrderStore(storageConnection));
        services.AddSingleton<IOrderWriteStore>(sp => sp.GetRequiredService<AzureOrderStore>());
        services.AddSingleton<IOrderReadStore>(sp => sp.GetRequiredService<AzureOrderStore>());
        services.AddSingleton<IMetricsStore>(_ => new AzureMetricsStore(storageConnection));
        services.AddSingleton<IImageStore>(_ => new AzureImageStore(storageConnection));
        return services;
    }
}