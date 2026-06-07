using Microsoft.Extensions.DependencyInjection;

namespace CoreDesign.Data.Infrastructure;

public static class CoreDesignDataExtensions
{
    public static IServiceCollection AddCoreDesignData<TCurrentUserAccessor>(this IServiceCollection services)
        where TCurrentUserAccessor : class, ICurrentUserAccessor
    {
        services.AddSingleton<ICurrentUserAccessor, TCurrentUserAccessor>();
        services.AddSingleton<AuditInterceptor>();
        return services;
    }
}
