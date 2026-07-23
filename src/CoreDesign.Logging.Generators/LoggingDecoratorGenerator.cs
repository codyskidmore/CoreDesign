using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CoreDesign.Logging.Generators;

[Generator]
public sealed class LoggingDecoratorGenerator : IIncrementalGenerator
{
    private const string AttributeFullName = "CoreDesign.Logging.LoggingDecoratorAttribute";
    private const string SuppressFullName   = "CoreDesign.Logging.SuppressAttribute";
    private const string RedactFullName     = "CoreDesign.Logging.RedactAttribute";
    // [Union] is synthesized only at emit time and isn't visible via GetAttributes() during
    // same-compilation analysis; IUnion is the compiler's compile-time-visible union marker.
    private const string UnionInterfaceFullName = "System.Runtime.CompilerServices.IUnion";

    // FullyQualifiedFormat omits the '?' nullable-reference-type modifier by default,
    // which desyncs generated signatures from the source interface (CS8613/CS8603).
    private static readonly SymbolDisplayFormat FullyQualifiedNullableFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var infos = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeFullName,
                predicate: static (node, _) => node is InterfaceDeclarationSyntax,
                transform: static (ctx, ct) => Transform(ctx, ct))
            .Where(static x => x is not null)
            .Select(static (x, _) => x!);

        context.RegisterSourceOutput(infos, static (spc, info) =>
            spc.AddSource($"{info.DecoratorName}.g.cs", GenerateDecorator(info)));

        context.RegisterSourceOutput(infos.Collect(), static (spc, all) =>
        {
            if (!all.IsEmpty)
                spc.AddSource("LoggingDecoratorExtensions.g.cs", GenerateRegistration(all));
        });
    }

    // ---- Transform --------------------------------------------------------

    private static DecoratorInfo? Transform(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol iface) return null;

        var ns = iface.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : iface.ContainingNamespace.ToDisplayString();

        var ifaceName     = iface.Name;
        var decoratorName = ifaceName.Length > 1 && ifaceName[0] == 'I'
            ? ifaceName.Substring(1) + "LoggingDecorator"
            : ifaceName + "LoggingDecorator";
        var loggerCategory = string.IsNullOrEmpty(ns) ? ifaceName : ns + "." + ifaceName;

        // Type parameters (Gap 1 fix)
        var typeParams = ImmutableArray.CreateBuilder<TypeParamInfo>(iface.TypeParameters.Length);
        foreach (var tp in iface.TypeParameters)
        {
            ct.ThrowIfCancellationRequested();
            typeParams.Add(new TypeParamInfo(tp.Name, BuildConstraints(tp)));
        }

        // Methods
        var methods = ImmutableArray.CreateBuilder<MethodInfo>();
        foreach (var member in iface.GetMembers())
        {
            ct.ThrowIfCancellationRequested();
            if (member is not IMethodSymbol method || method.MethodKind != MethodKind.Ordinary) continue;

            var suppressed = HasAttr(method, SuppressFullName);
            var returnKind = ClassifyReturn(method.ReturnType, out var arms);
            var returnType = method.ReturnType.ToDisplayString(FullyQualifiedNullableFormat);

            var parameters = ImmutableArray.CreateBuilder<ParamInfo>();
            foreach (var p in method.Parameters)
            {
                parameters.Add(new ParamInfo(
                    p.Type.ToDisplayString(FullyQualifiedNullableFormat),
                    p.Name,
                    HasAttr(p, RedactFullName),
                    IsCancellationToken(p.Type)));
            }

            methods.Add(new MethodInfo(method.Name, suppressed, returnKind, returnType, arms, parameters.ToImmutable()));
        }

        // Properties (Gap 2 fix)
        var properties = ImmutableArray.CreateBuilder<PropertyInfo>();
        foreach (var member in iface.GetMembers())
        {
            ct.ThrowIfCancellationRequested();
            if (member is not IPropertySymbol prop) continue;

            var indexParams = ImmutableArray.CreateBuilder<ParamInfo>();
            foreach (var p in prop.Parameters)
            {
                indexParams.Add(new ParamInfo(
                    p.Type.ToDisplayString(FullyQualifiedNullableFormat),
                    p.Name,
                    false,
                    false));
            }

            properties.Add(new PropertyInfo(
                prop.Type.ToDisplayString(FullyQualifiedNullableFormat),
                prop.Name,
                prop.GetMethod is not null,
                prop.SetMethod is not null,
                prop.IsIndexer,
                indexParams.ToImmutable()));
        }

        return new DecoratorInfo(
            ns, ifaceName, decoratorName, loggerCategory,
            typeParams.ToImmutable(),
            methods.ToImmutable(),
            properties.ToImmutable());
    }

    private static bool HasAttr(ISymbol symbol, string fullName)
    {
        foreach (var a in symbol.GetAttributes())
            if (a.AttributeClass?.ToDisplayString() == fullName) return true;
        return false;
    }

    private static bool IsCancellationToken(ITypeSymbol type) =>
        type is INamedTypeSymbol { Name: "CancellationToken" } named &&
        named.ContainingNamespace?.ToDisplayString() == "System.Threading";

    private static ImmutableArray<string> BuildConstraints(ITypeParameterSymbol tp)
    {
        var b = ImmutableArray.CreateBuilder<string>();
        if (tp.HasReferenceTypeConstraint)      b.Add("class");
        else if (tp.HasUnmanagedTypeConstraint) b.Add("unmanaged");
        else if (tp.HasValueTypeConstraint)     b.Add("struct");
        if (tp.HasNotNullConstraint)            b.Add("notnull");
        foreach (var ct in tp.ConstraintTypes)
            b.Add(ct.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        if (tp.HasConstructorConstraint)        b.Add("new()");
        return b.ToImmutable();
    }

    // ---- Return type classification ----------------------------------------

    private static ReturnKind ClassifyReturn(ITypeSymbol type, out ImmutableArray<ArmInfo> arms)
    {
        arms = ImmutableArray<ArmInfo>.Empty;

        if (type.SpecialType == SpecialType.System_Void) return ReturnKind.Void;
        if (type is not INamedTypeSymbol named) return ReturnKind.Value;

        var orig = named.OriginalDefinition.ToDisplayString();

        if (orig == "System.Threading.Tasks.Task") return ReturnKind.Task;
        if (orig == "System.Threading.Tasks.ValueTask") return ReturnKind.Task;

        if (orig == "System.Threading.Tasks.Task<TResult>" ||
            orig == "System.Threading.Tasks.ValueTask<TResult>")
        {
            var inner = named.TypeArguments[0];
            if (inner is INamedTypeSymbol innerNamed && IsUnion(innerNamed))
            {
                arms = BuildUnionArms(innerNamed);
                return ReturnKind.TaskOfUnion;
            }
            return ReturnKind.TaskOfValue;
        }

        return ReturnKind.Value;
    }

    private static bool IsUnion(INamedTypeSymbol t) =>
        t.AllInterfaces.Any(i => i.ToDisplayString() == UnionInterfaceFullName);

    // A union's case types are the parameter types of its public single-parameter
    // constructors (the "union creation members" per the language spec).
    private static ImmutableArray<ArmInfo> BuildUnionArms(INamedTypeSymbol union)
    {
        var b = ImmutableArray.CreateBuilder<ArmInfo>();
        foreach (var ctor in union.Constructors)
        {
            if (ctor.DeclaredAccessibility != Accessibility.Public) continue;
            if (ctor.Parameters.Length != 1) continue;
            var caseType = ctor.Parameters[0].Type;
            b.Add(new ArmInfo(
                caseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                IsWarningType(caseType.Name)));
        }
        return b.ToImmutable();
    }

    private static bool IsWarningType(string name) =>
        name.Contains("NotFound")        || name.Contains("BadRequest")      ||
        name.Contains("Unauthorized")    || name.Contains("Forbidden")       ||
        name.Contains("Conflict")        || name.Contains("Error")           ||
        name.Contains("Failure")         || name.Contains("InvalidOperation");

    // ---- Decorator code generation -----------------------------------------

    private static string GenerateDecorator(DecoratorInfo info)
    {
        var sb = new StringBuilder(1024);

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using Microsoft.Extensions.Logging;");
        sb.AppendLine("using System;");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(info.Namespace))
        {
            sb.AppendLine("namespace " + info.Namespace + ";");
            sb.AppendLine();
        }

        var tpSuffix    = TypeParamSuffix(info.TypeParameters);
        var constraints = ConstraintClauses(info.TypeParameters);
        var ifaceRef    = info.InterfaceName + tpSuffix;
        var classDecl   = info.DecoratorName + tpSuffix + " : " + ifaceRef + constraints;

        sb.AppendLine("public sealed class " + classDecl);
        sb.AppendLine("{");
        sb.AppendLine("    private readonly " + ifaceRef + " _inner;");
        sb.AppendLine("    private readonly ILogger _logger;");
        sb.AppendLine();
        sb.AppendLine("    public " + info.DecoratorName + "(" + ifaceRef + " inner, ILoggerFactory loggerFactory)");
        sb.AppendLine("    {");
        sb.AppendLine("        _inner = inner;");
        sb.AppendLine("        _logger = loggerFactory.CreateLogger(\"" + info.LoggerCategory + "\");");
        sb.AppendLine("    }");

        foreach (var method in info.Methods)
        {
            sb.AppendLine();
            AppendMethod(sb, method);
        }

        foreach (var prop in info.Properties)
        {
            sb.AppendLine();
            AppendProperty(sb, prop);
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void AppendMethod(StringBuilder sb, MethodInfo method)
    {
        var paramList   = BuildParamList(method.Parameters);
        var argList     = BuildArgList(method.Parameters);
        var isAsyncKind = method.ReturnKind == ReturnKind.Task ||
                          method.ReturnKind == ReturnKind.TaskOfValue ||
                          method.ReturnKind == ReturnKind.TaskOfUnion;
        var asyncKw     = !method.IsSuppressed && isAsyncKind ? "async " : "";

        sb.AppendLine("    public " + asyncKw + method.ReturnTypeDisplay + " " + method.Name + "(" + paramList + ")");
        sb.AppendLine("    {");

        if (method.IsSuppressed)
        {
            var prefix = method.ReturnKind != ReturnKind.Void ? "return " : "";
            sb.AppendLine("        " + prefix + "_inner." + method.Name + "(" + argList + ");");
            sb.AppendLine("    }");
            return;
        }

        var (tmpl, args) = BuildInvocationLog(method);
        sb.AppendLine("        _logger.LogInformation(" + tmpl + ", " + args + ");");
        sb.AppendLine("        try");
        sb.AppendLine("        {");

        switch (method.ReturnKind)
        {
            case ReturnKind.Void:
                sb.AppendLine("            _inner." + method.Name + "(" + argList + ");");
                sb.AppendLine("            _logger.LogInformation(\"{Method} completed\", \"" + method.Name + "\");");
                break;

            case ReturnKind.Value:
                sb.AppendLine("            var __result = _inner." + method.Name + "(" + argList + ");");
                sb.AppendLine("            _logger.LogInformation(\"{Method} returned {@Result}\", \"" + method.Name + "\", __result);");
                sb.AppendLine("            return __result;");
                break;

            case ReturnKind.Task:
                sb.AppendLine("            await _inner." + method.Name + "(" + argList + ");");
                sb.AppendLine("            _logger.LogInformation(\"{Method} completed\", \"" + method.Name + "\");");
                break;

            case ReturnKind.TaskOfValue:
                sb.AppendLine("            var __result = await _inner." + method.Name + "(" + argList + ");");
                sb.AppendLine("            _logger.LogInformation(\"{Method} returned {@Result}\", \"" + method.Name + "\", __result);");
                sb.AppendLine("            return __result;");
                break;

            case ReturnKind.TaskOfUnion:
                sb.AppendLine("            var __result = await _inner." + method.Name + "(" + argList + ");");
                AppendUnionSwitch(sb, method.Arms, method.Name);
                sb.AppendLine("            return __result;");
                break;
        }

        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception __ex)");
        sb.AppendLine("        {");
        sb.AppendLine("            _logger.LogError(__ex, \"{Method} threw an exception\", \"" + method.Name + "\");");
        sb.AppendLine("            throw;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }

    private static void AppendProperty(StringBuilder sb, PropertyInfo prop)
    {
        if (prop.IsIndexer)
        {
            var paramList = BuildParamList(prop.IndexParameters);
            var argList   = BuildArgList(prop.IndexParameters);
            if (prop.HasGetter && prop.HasSetter)
            {
                sb.AppendLine("    public " + prop.TypeDisplay + " this[" + paramList + "]");
                sb.AppendLine("    {");
                sb.AppendLine("        get => _inner[" + argList + "];");
                sb.AppendLine("        set => _inner[" + argList + "] = value;");
                sb.AppendLine("    }");
            }
            else if (prop.HasGetter)
            {
                sb.AppendLine("    public " + prop.TypeDisplay + " this[" + paramList + "] => _inner[" + argList + "];");
            }
            else
            {
                sb.AppendLine("    public " + prop.TypeDisplay + " this[" + paramList + "]");
                sb.AppendLine("    {");
                sb.AppendLine("        set => _inner[" + argList + "] = value;");
                sb.AppendLine("    }");
            }
        }
        else
        {
            if (prop.HasGetter && prop.HasSetter)
            {
                sb.AppendLine("    public " + prop.TypeDisplay + " " + prop.Name);
                sb.AppendLine("    {");
                sb.AppendLine("        get => _inner." + prop.Name + ";");
                sb.AppendLine("        set => _inner." + prop.Name + " = value;");
                sb.AppendLine("    }");
            }
            else if (prop.HasGetter)
            {
                sb.AppendLine("    public " + prop.TypeDisplay + " " + prop.Name + " => _inner." + prop.Name + ";");
            }
            else
            {
                sb.AppendLine("    public " + prop.TypeDisplay + " " + prop.Name);
                sb.AppendLine("    {");
                sb.AppendLine("        set => _inner." + prop.Name + " = value;");
                sb.AppendLine("    }");
            }
        }
    }

    private static void AppendUnionSwitch(StringBuilder sb, ImmutableArray<ArmInfo> arms, string methodName)
    {
        sb.AppendLine("            switch (__result)");
        sb.AppendLine("            {");
        for (var i = 0; i < arms.Length; i++)
        {
            sb.AppendLine("                case " + arms[i].TypeDisplay + " __t" + i + ":");
            sb.AppendLine("                    _logger.Log" + arms[i].LogLevel +
                          "(\"{Method} returned {@Result}\", \"" + methodName + "\", __t" + i + ");");
            sb.AppendLine("                    break;");
        }
        sb.AppendLine("            }");
    }

    private static string BuildParamList(ImmutableArray<ParamInfo> parameters)
    {
        if (parameters.IsEmpty) return string.Empty;
        var parts = new List<string>(parameters.Length);
        foreach (var p in parameters)
            parts.Add(p.TypeDisplay + " " + p.Name);
        return string.Join(", ", parts);
    }

    private static string BuildArgList(ImmutableArray<ParamInfo> parameters)
    {
        if (parameters.IsEmpty) return string.Empty;
        var parts = new List<string>(parameters.Length);
        foreach (var p in parameters)
            parts.Add(p.Name);
        return string.Join(", ", parts);
    }

    private static (string template, string args) BuildInvocationLog(MethodInfo method)
    {
        var loggable = new List<ParamInfo>();
        foreach (var p in method.Parameters)
            if (!p.IsCancellationToken)
                loggable.Add(p);

        var tmplSb = new StringBuilder();
        tmplSb.Append('"');
        tmplSb.Append("Invoking {Method}");
        for (var i = 0; i < loggable.Count; i++)
        {
            tmplSb.Append(i == 0 ? " with {@" : ", {@");
            tmplSb.Append(loggable[i].Name);
            tmplSb.Append('}');
        }
        tmplSb.Append('"');

        var argsSb = new StringBuilder();
        argsSb.Append('"');
        argsSb.Append(method.Name);
        argsSb.Append('"');
        foreach (var p in loggable)
        {
            argsSb.Append(", ");
            argsSb.Append(p.IsRedacted ? "\"[REDACTED]\"" : p.Name);
        }

        return (tmplSb.ToString(), argsSb.ToString());
    }

    private static string TypeParamSuffix(ImmutableArray<TypeParamInfo> tps)
    {
        if (tps.IsEmpty) return string.Empty;
        var sb = new StringBuilder();
        sb.Append('<');
        for (var i = 0; i < tps.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(tps[i].Name);
        }
        sb.Append('>');
        return sb.ToString();
    }

    private static string ConstraintClauses(ImmutableArray<TypeParamInfo> tps)
    {
        if (tps.IsEmpty) return string.Empty;
        var sb = new StringBuilder();
        foreach (var tp in tps)
        {
            if (tp.Constraints.IsEmpty) continue;
            sb.Append(" where ");
            sb.Append(tp.Name);
            sb.Append(" : ");
            for (var i = 0; i < tp.Constraints.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(tp.Constraints[i]);
            }
        }
        return sb.ToString();
    }

    // ---- Registration extension code generation ----------------------------

    private static string GenerateRegistration(ImmutableArray<DecoratorInfo> infos)
    {
        var sb = new StringBuilder(512);
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine();
        sb.AppendLine("public static partial class LoggingDecoratorExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Applies generated logging decorators for all interfaces marked with [LoggingDecorator].");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection DecorateWithLogging(");
        sb.AppendLine("        this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        sb.AppendLine("    {");

        foreach (var info in infos)
        {
            var prefix   = string.IsNullOrEmpty(info.Namespace) ? "global::" : "global::" + info.Namespace + ".";
            var ifaceRef = prefix + info.InterfaceName;
            var decorRef = prefix + info.DecoratorName;

            if (info.IsGeneric)
            {
                // Open generic registration
                var openSuffix = "<" + new string(',', info.TypeParameters.Length - 1) + ">";
                sb.AppendLine("        global::CoreDesign.Logging.LoggingDecoratorRegistration.Decorate(services, typeof(" + ifaceRef + openSuffix + "), typeof(" + decorRef + openSuffix + "));");
            }
            else
            {
                sb.AppendLine("        global::CoreDesign.Logging.LoggingDecoratorRegistration.Decorate<" + ifaceRef + ", " + decorRef + ">(services);");
            }
        }

        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }
}

// ---- Model -----------------------------------------------------------------

internal enum ReturnKind { Void, Value, Task, TaskOfValue, TaskOfUnion }

internal sealed class DecoratorInfo
{
    public DecoratorInfo(
        string ns, string ifaceName, string decoratorName, string loggerCategory,
        ImmutableArray<TypeParamInfo> typeParameters,
        ImmutableArray<MethodInfo> methods,
        ImmutableArray<PropertyInfo> properties)
    {
        Namespace      = ns;
        InterfaceName  = ifaceName;
        DecoratorName  = decoratorName;
        LoggerCategory = loggerCategory;
        TypeParameters = typeParameters;
        Methods        = methods;
        Properties     = properties;
    }

    public string Namespace      { get; }
    public string InterfaceName  { get; }
    public string DecoratorName  { get; }
    public string LoggerCategory { get; }
    public ImmutableArray<TypeParamInfo> TypeParameters { get; }
    public ImmutableArray<MethodInfo>    Methods        { get; }
    public ImmutableArray<PropertyInfo>  Properties     { get; }
    public bool IsGeneric => !TypeParameters.IsEmpty;
}

internal sealed class TypeParamInfo
{
    public TypeParamInfo(string name, ImmutableArray<string> constraints)
    {
        Name        = name;
        Constraints = constraints;
    }

    public string                  Name        { get; }
    public ImmutableArray<string>  Constraints { get; }
}

internal sealed class MethodInfo
{
    public MethodInfo(string name, bool suppressed, ReturnKind kind, string returnType,
                      ImmutableArray<ArmInfo> arms, ImmutableArray<ParamInfo> parameters)
    {
        Name              = name;
        IsSuppressed      = suppressed;
        ReturnKind        = kind;
        ReturnTypeDisplay = returnType;
        Arms              = arms;
        Parameters        = parameters;
    }

    public string     Name              { get; }
    public bool       IsSuppressed      { get; }
    public ReturnKind ReturnKind        { get; }
    public string     ReturnTypeDisplay { get; }
    public ImmutableArray<ArmInfo>   Arms       { get; }
    public ImmutableArray<ParamInfo> Parameters { get; }
}

internal sealed class PropertyInfo
{
    public PropertyInfo(string typeDisplay, string name, bool hasGetter, bool hasSetter,
                        bool isIndexer, ImmutableArray<ParamInfo> indexParameters)
    {
        TypeDisplay      = typeDisplay;
        Name             = name;
        HasGetter        = hasGetter;
        HasSetter        = hasSetter;
        IsIndexer        = isIndexer;
        IndexParameters  = indexParameters;
    }

    public string TypeDisplay     { get; }
    public string Name            { get; }
    public bool   HasGetter       { get; }
    public bool   HasSetter       { get; }
    public bool   IsIndexer       { get; }
    public ImmutableArray<ParamInfo> IndexParameters { get; }
}

internal sealed class ArmInfo
{
    public ArmInfo(string typeDisplay, bool isWarning) { TypeDisplay = typeDisplay; IsWarning = isWarning; }
    public string TypeDisplay { get; }
    public bool   IsWarning   { get; }
    public string LogLevel    => IsWarning ? "Warning" : "Information";
}

internal sealed class ParamInfo
{
    public ParamInfo(string typeDisplay, string name, bool isRedacted, bool isCt)
    {
        TypeDisplay         = typeDisplay;
        Name                = name;
        IsRedacted          = isRedacted;
        IsCancellationToken = isCt;
    }

    public string TypeDisplay         { get; }
    public string Name                { get; }
    public bool   IsRedacted          { get; }
    public bool   IsCancellationToken { get; }
}
