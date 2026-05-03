namespace CoreDesign.Shared.Infrastructure;

/// <summary>
///     HostName is the Container Host name. Aspire calls this the Resource Name.
/// </summary>
public record DatabaseOptions(
    string HostName,
    string DatabaseName,
    int HostPort,
    string ConnectionStringName);