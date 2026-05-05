namespace CoreDesign.Logging;

/// <summary>
/// Suppresses all LoggingMiddleware output for the decorated interface method.
/// No invocation, result, or exception entries are written.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class NoLogAttribute : Attribute { }
