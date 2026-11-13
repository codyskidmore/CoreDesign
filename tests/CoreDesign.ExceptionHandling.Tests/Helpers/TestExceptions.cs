namespace CoreDesign.ExceptionHandling.Tests.Helpers;

// A small hierarchy exercising every generator behavior:
// - DomainException / EntityNotFoundException cover MatchDerived precedence (most-derived wins).
// - SecretException covers IncludeMessage = false.
// - ExactOnlyException / ExactOnlySubException cover MatchDerived = false (exact type only).

public class DomainException(string message) : Exception(message);

public sealed class EntityNotFoundException(string message) : DomainException(message);

public sealed class SecretException(string message) : Exception(message);

public class ExactOnlyException(string message) : Exception(message);

public sealed class ExactOnlySubException(string message) : ExactOnlyException(message);

// A BadHttpRequestException subtype with its own mapping, proving an explicit mapping still wins
// over the built-in BadHttpRequestException status passthrough.
public sealed class MappedBadHttpRequestException(string message)
    : Microsoft.AspNetCore.Http.BadHttpRequestException(message, 400);
