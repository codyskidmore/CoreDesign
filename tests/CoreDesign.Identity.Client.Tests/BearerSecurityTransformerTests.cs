using CoreDesign.Identity.Client;
using Microsoft.OpenApi;

namespace CoreDesign.Identity.Client.Tests;

public class BearerSecurityTransformerTests
{
    private static OpenApiDocument BuildDocument(bool withOperations = true)
    {
        var doc = new OpenApiDocument
        {
            Paths = new OpenApiPaths()
        };

        if (withOperations)
        {
            doc.Paths["/test"] = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>
                {
                    [HttpMethod.Get] = new OpenApiOperation(),
                    [HttpMethod.Post] = new OpenApiOperation()
                }
            };
            doc.Paths["/other"] = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>
                {
                    [HttpMethod.Delete] = new OpenApiOperation()
                }
            };
        }

        return doc;
    }

    [Fact]
    public async Task TransformAsync_AddsBearerSecurityScheme()
    {
        var transformer = new BearerSecurityTransformer();
        var doc = BuildDocument();

        await transformer.TransformAsync(doc, null!, CancellationToken.None);

        Assert.NotNull(doc.Components);
        Assert.NotNull(doc.Components.SecuritySchemes);
        Assert.True(doc.Components.SecuritySchemes.ContainsKey("Bearer"));
    }

    [Fact]
    public async Task TransformAsync_BearerScheme_HasHttpType()
    {
        var transformer = new BearerSecurityTransformer();
        var doc = BuildDocument();

        await transformer.TransformAsync(doc, null!, CancellationToken.None);

        var scheme = (OpenApiSecurityScheme)doc.Components.SecuritySchemes["Bearer"];
        Assert.Equal(SecuritySchemeType.Http, scheme.Type);
        Assert.Equal("bearer", scheme.Scheme);
        Assert.Equal("JWT", scheme.BearerFormat);
    }

    [Fact]
    public async Task TransformAsync_AddsSecurityRequirement_ToAllOperations()
    {
        var transformer = new BearerSecurityTransformer();
        var doc = BuildDocument();

        await transformer.TransformAsync(doc, null!, CancellationToken.None);

        foreach (var path in doc.Paths.Values)
        foreach (var operation in path.Operations!.Values)
        {
            Assert.NotNull(operation.Security);
            Assert.NotEmpty(operation.Security);
        }
    }

    [Fact]
    public async Task TransformAsync_HandlesPath_WithNoOperations()
    {
        var transformer = new BearerSecurityTransformer();
        var doc = BuildDocument(withOperations: false);
        doc.Paths["/empty"] = new OpenApiPathItem { Operations = null };

        var exception = await Record.ExceptionAsync(() =>
            transformer.TransformAsync(doc, null!, CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task TransformAsync_CreatesComponentsSection_WhenNull()
    {
        var transformer = new BearerSecurityTransformer();
        var doc = BuildDocument();
        doc.Components = null;

        await transformer.TransformAsync(doc, null!, CancellationToken.None);

        Assert.NotNull(doc.Components);
    }
}
