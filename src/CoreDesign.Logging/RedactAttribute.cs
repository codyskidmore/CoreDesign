namespace CoreDesign.Logging;

/// <summary>
/// Marks an interface method parameter as sensitive. The generated logging decorator logs
/// "[REDACTED]" in place of the actual value.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class RedactAttribute : Attribute { }
