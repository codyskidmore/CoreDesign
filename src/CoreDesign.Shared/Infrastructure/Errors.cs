namespace CoreDesign.Shared.Infrastructure;

public record NotFoundMessage(string Message);

public record BadRequestMessage(string Message);

public record ErrorMessage(string Message);

public record InvalidOperationMessage(string Message);