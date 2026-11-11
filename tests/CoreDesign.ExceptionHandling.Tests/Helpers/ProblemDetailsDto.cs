namespace CoreDesign.ExceptionHandling.Tests.Helpers;

public sealed record ProblemDetailsDto(int? Status, string? Title, string? Detail, string? Type);
