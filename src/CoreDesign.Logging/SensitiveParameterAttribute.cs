namespace CoreDesign.Logging;

/// <summary>
/// Marks an interface method parameter as sensitive. LoggingMiddleware will log
/// "[REDACTED]" in place of the actual value.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class SensitiveParameterAttribute : Attribute { }
