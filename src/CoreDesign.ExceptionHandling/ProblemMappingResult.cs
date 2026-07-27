namespace CoreDesign.ExceptionHandling;

/// <summary>
/// The resolved RFC 7807 fields for an exception, produced by <see cref="IProblemDetailsMapper"/>.
/// </summary>
public readonly record struct ProblemMappingResult(int StatusCode, string? Title, string? Detail, string? Type);
