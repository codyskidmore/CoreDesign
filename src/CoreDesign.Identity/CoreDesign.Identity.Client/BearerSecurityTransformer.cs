using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi;

namespace CoreDesign.Identity.Client;

public sealed class BearerSecurityTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        };

        HashSet<string> anonymousPaths = [];
        if (context is not null)
        {
            var endpointDataSource = context.ApplicationServices.GetRequiredService<EndpointDataSource>();
            anonymousPaths = endpointDataSource.Endpoints
                .OfType<RouteEndpoint>()
                .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
                .Select(e => "/" + (e.RoutePattern.RawText ?? "").TrimStart('/'))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var requirement = new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        };

        foreach (var (pathKey, path) in document.Paths)
        {
            if (path.Operations is null || anonymousPaths.Contains(pathKey))
                continue;

            foreach (var operation in path.Operations.Values)
                (operation.Security ??= []).Add(requirement);
        }

        return Task.CompletedTask;
    }
}
