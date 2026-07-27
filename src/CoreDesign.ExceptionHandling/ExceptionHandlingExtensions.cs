using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CoreDesign.ExceptionHandling;

public static class ExceptionHandlingExtensions
{
    /// <summary>
    /// Registers <see cref="CoreDesignExceptionHandler"/> as the global exception handler and adds
    /// RFC 7807 <c>ProblemDetails</c> support. Works with zero configuration, returning a generic
    /// 500 for every exception, until <c>AddGeneratedProblemMappings()</c> (emitted by the
    /// <see cref="ProblemMappingAttribute"/> source generator) is also called; call order does not
    /// matter.
    ///
    /// The middleware pipeline still needs <c>app.UseExceptionHandler()</c>.
    /// </summary>
    public static IServiceCollection AddCoreDesignExceptionHandling(this IServiceCollection services)
    {
        services.TryAddSingleton<IProblemDetailsMapper, NullProblemDetailsMapper>();
        services.AddExceptionHandler<CoreDesignExceptionHandler>();
        services.AddProblemDetails();
        return services;
    }
}
