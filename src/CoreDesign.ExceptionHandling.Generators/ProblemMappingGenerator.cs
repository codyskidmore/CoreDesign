using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CoreDesign.ExceptionHandling.Generators;

[Generator]
public sealed class ProblemMappingGenerator : IIncrementalGenerator
{
    private const string AttributeFullName = "CoreDesign.ExceptionHandling.ProblemMappingAttribute";

    private static readonly DiagnosticDescriptor DuplicateMapping = new(
        id: "CDEH001",
        title: "Duplicate ProblemMapping",
        messageFormat: "Multiple [ProblemMapping] attributes target '{0}'. Remove the duplicate mapping.",
        category: "CoreDesign.ExceptionHandling",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingExceptionType = new(
        id: "CDEH002",
        title: "Missing ExceptionType",
        messageFormat: "[assembly: ProblemMapping] must set ExceptionType when applied at the assembly level",
        category: "CoreDesign.ExceptionHandling",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classMappings = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeFullName,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, ct) => TransformClass(ctx, ct))
            .SelectMany(static (x, _) => x);

        var assemblyMappings = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeFullName,
                predicate: static (node, _) => node is CompilationUnitSyntax,
                transform: static (ctx, ct) => TransformAssembly(ctx, ct))
            .SelectMany(static (x, _) => x);

        var all = classMappings.Collect().Combine(assemblyMappings.Collect())
            .Select(static (pair, _) => pair.Left.AddRange(pair.Right));

        context.RegisterSourceOutput(all, static (spc, mappings) => Generate(spc, mappings));
    }

    // ---- Transform ----------------------------------------------------------

    private static ImmutableArray<MappingInfo> TransformClass(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol classSymbol) return ImmutableArray<MappingInfo>.Empty;

        var b = ImmutableArray.CreateBuilder<MappingInfo>();
        foreach (var attr in ctx.Attributes)
        {
            ct.ThrowIfCancellationRequested();
            var info = BuildMappingInfo(attr, classSymbol, attr.ApplicationSyntaxReference?.GetSyntax(ct).GetLocation());
            if (info is not null) b.Add(info);
        }
        return b.ToImmutable();
    }

    private static ImmutableArray<MappingInfo> TransformAssembly(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        var b = ImmutableArray.CreateBuilder<MappingInfo>();
        foreach (var attr in ctx.Attributes)
        {
            ct.ThrowIfCancellationRequested();
            var location = attr.ApplicationSyntaxReference?.GetSyntax(ct).GetLocation();

            var exceptionType = attr.NamedArguments
                .Where(kv => kv.Key == "ExceptionType")
                .Select(kv => kv.Value.Value as ITypeSymbol)
                .FirstOrDefault();

            if (exceptionType is not INamedTypeSymbol targetType)
            {
                b.Add(MappingInfo.FromDiagnostic(MissingExceptionType, location));
                continue;
            }

            var info = BuildMappingInfo(attr, targetType, location);
            if (info is not null) b.Add(info);
        }
        return b.ToImmutable();
    }

    private static MappingInfo? BuildMappingInfo(AttributeData attr, INamedTypeSymbol targetType, Location? location)
    {
        if (attr.ConstructorArguments.Length == 0 || attr.ConstructorArguments[0].Value is not int statusCode)
            return null;

        string? title = null, type = null;
        var matchDerived = true;
        var includeMessage = true;

        foreach (var kv in attr.NamedArguments)
        {
            switch (kv.Key)
            {
                case "Title": title = kv.Value.Value as string; break;
                case "Type": type = kv.Value.Value as string; break;
                case "MatchDerived": matchDerived = kv.Value.Value is bool md && md; break;
                case "IncludeMessage": includeMessage = kv.Value.Value is not bool im || im; break;
            }
        }

        return MappingInfo.Mapping(targetType, statusCode, title, type, matchDerived, includeMessage, location);
    }

    private static int InheritanceDepth(ITypeSymbol type)
    {
        var depth = 0;
        var current = type.BaseType;
        while (current is not null)
        {
            depth++;
            current = current.BaseType;
        }
        return depth;
    }

    // ---- Generate -------------------------------------------------------------

    private static void Generate(SourceProductionContext spc, ImmutableArray<MappingInfo> mappings)
    {
        foreach (var diag in mappings.Where(m => m.Diagnostic is not null))
            spc.ReportDiagnostic(Diagnostic.Create(diag.Diagnostic!, diag.Location ?? Location.None));

        var candidates = mappings.Where(m => m.Diagnostic is null).ToList();

        var byType = candidates
            .GroupBy(m => m.TargetType!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .ToList();

        foreach (var group in byType.Where(g => g.Count() > 1))
        {
            var first = group.First();
            spc.ReportDiagnostic(Diagnostic.Create(DuplicateMapping, first.Location ?? Location.None, first.TargetType!.ToDisplayString()));
        }

        var unique = byType
            .Where(g => g.Count() == 1)
            .Select(g => g.Single())
            .OrderByDescending(m => InheritanceDepth(m.TargetType!))
            .ToImmutableArray();

        spc.AddSource("GeneratedProblemDetailsMapper.g.cs", GenerateMapper(unique));
        spc.AddSource("GeneratedProblemMappingExtensions.g.cs", GenerateRegistration());
    }

    private static string GenerateMapper(ImmutableArray<MappingInfo> mappings)
    {
        var sb = new StringBuilder(1024);
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace CoreDesign.ExceptionHandling.Generated;");
        sb.AppendLine();
        sb.AppendLine("internal sealed class GeneratedProblemDetailsMapper : global::CoreDesign.ExceptionHandling.IProblemDetailsMapper");
        sb.AppendLine("{");
        sb.AppendLine("    public bool TryMap(global::System.Exception exception, out global::CoreDesign.ExceptionHandling.ProblemMappingResult result)");
        sb.AppendLine("    {");

        if (mappings.IsEmpty)
        {
            sb.AppendLine("        result = default;");
            sb.AppendLine("        return false;");
        }
        else
        {
            sb.AppendLine("        switch (exception)");
            sb.AppendLine("        {");

            foreach (var m in mappings)
            {
                var typeRef = m.TargetType!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var whenClause = m.MatchDerived ? "" : " when e.GetType() == typeof(" + typeRef + ")";
                var title  = m.Title is null ? "null" : Literal(m.Title);
                var type   = m.Type  is null ? "null" : Literal(m.Type);
                var detail = m.IncludeMessage ? "e.Message" : "null";

                sb.AppendLine("            case " + typeRef + " e" + whenClause + ":");
                sb.AppendLine("                result = new global::CoreDesign.ExceptionHandling.ProblemMappingResult(" +
                               m.StatusCode + ", " + title + ", " + detail + ", " + type + ");");
                sb.AppendLine("                return true;");
            }

            sb.AppendLine("            default:");
            sb.AppendLine("                result = default;");
            sb.AppendLine("                return false;");
            sb.AppendLine("        }");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateRegistration()
    {
        var sb = new StringBuilder(512);
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine();
        sb.AppendLine("internal static class GeneratedProblemMappingExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Registers the compile-time exception-to-ProblemDetails mapping table built from every");
        sb.AppendLine("    /// [ProblemMapping] attribute in this compilation. Superseds the zero-config fallback");
        sb.AppendLine("    /// registered by AddCoreDesignExceptionHandling(); call order does not matter.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddGeneratedProblemMappings(");
        sb.AppendLine("        this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        sb.AppendLine("    {");
        sb.AppendLine("        services.AddSingleton<global::CoreDesign.ExceptionHandling.IProblemDetailsMapper, global::CoreDesign.ExceptionHandling.Generated.GeneratedProblemDetailsMapper>();");
        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string Literal(string value)
    {
        return Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(value, quote: true);
    }
}

// ---- Model -----------------------------------------------------------------

internal sealed class MappingInfo
{
    private MappingInfo() { }

    public INamedTypeSymbol? TargetType { get; private set; }
    public int StatusCode { get; private set; }
    public string? Title { get; private set; }
    public string? Type { get; private set; }
    public bool MatchDerived { get; private set; }
    public bool IncludeMessage { get; private set; }
    public Location? Location { get; private set; }
    public DiagnosticDescriptor? Diagnostic { get; private set; }

    public static MappingInfo Mapping(
        INamedTypeSymbol targetType, int statusCode, string? title, string? type,
        bool matchDerived, bool includeMessage, Location? location) => new()
    {
        TargetType = targetType,
        StatusCode = statusCode,
        Title = title,
        Type = type,
        MatchDerived = matchDerived,
        IncludeMessage = includeMessage,
        Location = location
    };

    public static MappingInfo FromDiagnostic(DiagnosticDescriptor descriptor, Location? location) => new()
    {
        Diagnostic = descriptor,
        Location = location
    };
}
