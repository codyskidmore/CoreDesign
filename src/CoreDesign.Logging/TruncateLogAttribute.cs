namespace CoreDesign.Logging;

/// <summary>
/// Overrides the maximum character length of the serialized return value written to the log
/// for the decorated method. The default limit is defined by
/// <see cref="LoggingMiddleware.DefaultMaxResultLength"/>.
/// Set <see cref="MaxLength"/> to 0 to disable truncation for this method.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TruncateLogAttribute(int maxLength = LoggingMiddleware.DefaultMaxResultLength) : Attribute
{
    public int MaxLength { get; } = maxLength;
}
