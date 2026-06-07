using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CoreDesign.SeedTool;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

const string Help = """
Usage: dotnet seed <command> [entities...] [options]

Commands:
  setup     Interactive setup: creates coredesign.seedtool.json
  export    Export entity data from the database to seed files
  diff      Compare seed files to the current database state
  help      Show help for a command (e.g. dotnet seed help export)

Options:
  --connection <cs>    Full connection string override
  --password <pwd>     Database password substituted into {password} in connectionString
  --output <dir>       Seed data directory (overrides coredesign.seedtool.json outputDirectory)
  --config <path>      Path to coredesign.seedtool.json (default: searches up from current directory)
  --help, -h           Show this help

Examples:
  dotnet seed setup
  dotnet seed export
  dotnet seed export SiteContent SiteSettings
  dotnet seed diff
  dotnet seed help export
""";

const string HelpSetup = """
Usage: dotnet seed setup

Creates coredesign.seedtool.json in the current directory. Run once per
project after cloning the repository.

Prompts:
  Assembly path        Path to the built application assembly, relative to the
                       current directory.
                       Example: src/MyApp.Api/bin/Debug/net10.0/MyApp.Api.dll

  Seed data directory  Directory where seed files are written and read.
                       Default: SeedData. Relative to coredesign.seedtool.json.

  Entities             Space-separated entity names to export by default.
                       Leave empty to export all entities in the model.

  Connection mode      How the tool connects to the database:

    1  Template + user secrets
       A connection string template is stored in the config file with {password}
       as a placeholder. The password is read at runtime from user secrets in
       a specified project (no password ever stored in the config file).
       Best for Aspire apps where the password is an Aspire parameter.

       Produces:
         "connectionString": "Server=localhost,1433;...;Password={password};..."
         "userSecrets": { "project": "src/MyApp.AppHost", "passwordKey": "Parameters:SqlPassword" }

    2  App config
       The tool loads the specified project's appsettings.json,
       appsettings.Development.json, and user secrets to resolve the connection
       string. The string is never stored in coredesign.seedtool.json; it always
       reads from the same source as the app, so it cannot go stale.
       Any {ParameterName} placeholders in the connection string are resolved
       automatically from Parameters:ParameterName in the same config, matching
       Aspire's own substitution behavior.
       Recommended for Aspire apps: store the connection string template and
       password parameter in AppHost user secrets; point appConfig at the AppHost.
       Also works for non-Aspire apps where the connection string is in appsettings
       or user secrets.

       Produces:
         "appConfig": { "project": "src/MyApp.AppHost", "connectionStringKey": "ConnectionStrings:mydb" }

    3  Template only
       A connection string template with {password} is stored in the config file.
       The password must be supplied at runtime via --password or SEEDTOOL_PASSWORD.

       Produces:
         "connectionString": "Host=localhost;Database=myapp;Username=app;Password={password}"

    4  Direct
       The full connection string (including the password) is stored in the config
       file. Not recommended for configs that are committed to source control.

       Produces:
         "connectionString": "Host=localhost;Database=myapp;Username=app;Password=secret"

After setup, run:
  dotnet seed export

If coredesign.seedtool.json already exists you will be asked whether to overwrite it.
""";

const string HelpExport = """
Usage: dotnet seed export [entities...] [options]

Exports entity data from the database to JSON seed files compatible with
MigrationWorker's seed directory format. Each file is named using the entity's
fully qualified type name (e.g. MyApp.Features.Content.SiteContent.json).
Soft-deleted rows are included so the complete table state is captured.

Arguments:
  entities    One or more entity names (short class name or fully qualified).
              Overrides the entity list in coredesign.seedtool.json.
              If neither the argument list nor the config list is set, all
              non-abstract BaseEntity subtypes in the model are exported.

Options:
  --connection <cs>    Full connection string override (skips all config resolution).
  --password <pwd>     Database password substituted into {password} in connectionString.
                       Overrides SEEDTOOL_PASSWORD and userSecrets config.
  --output <dir>       Output directory. Overrides outputDirectory in
                       coredesign.seedtool.json.
  --config <path>      Explicit path to coredesign.seedtool.json.
  --help, -h           Show this help.

Exit codes:
  0   Export completed successfully.
  1   Error (config not found, assembly not built, connection failed, etc.)

Examples:
  dotnet seed export
  dotnet seed export SiteContent SiteSettings
  dotnet seed export --output SeedData/Overrides
  dotnet seed export --connection "Host=localhost;Database=myapp;Username=u;Password=p"
""";

const string HelpDiff = """
Usage: dotnet seed diff [entities...] [options]

Compares seed files in the configured directory to the current database state
and reports differences per entity. Useful for verifying that a seed apply
succeeded or for detecting database drift before running migrations.

Arguments:
  entities    One or more entity names (short class name or fully qualified).
              Overrides the entity list in coredesign.seedtool.json.
              If neither is set, all .json files present in the seed directory
              are diffed.

Options:
  --connection <cs>    Full connection string override (skips all config resolution).
  --password <pwd>     Database password substituted into {password} in connectionString.
                       Overrides SEEDTOOL_PASSWORD and userSecrets config.
  --output <dir>       Seed directory to read files from.
  --config <path>      Explicit path to coredesign.seedtool.json.
  --help, -h           Show this help.

Output:
  Each entity is reported as OK or with a difference count broken down by:
    only in file    Present in the seed file but missing from the database.
    only in DB      Present in the database but missing from the seed file.
    different       Present in both but at least one field value differs.

Exit codes:
  0   All compared seed files match the database.
  1   Error (config not found, connection failed, etc.)
  2   Differences found. Useful as a CI gate step.

Examples:
  dotnet seed diff
  dotnet seed diff SiteContent
  dotnet seed diff SiteContent SiteSettings
""";

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine(Help);
    return 0;
}

var command = args[0].ToLowerInvariant();
if (command is not ("export" or "diff" or "setup" or "help"))
{
    Console.Error.WriteLine($"Unknown command: {args[0]}");
    Console.Error.WriteLine("Run 'dotnet seed --help' for usage.");
    return 1;
}

var entities = new List<string>();
string? connectionOverride = null;
string? passwordOverride = null;
string? outputOverride = null;
string? configPathArg = null;

for (var i = 1; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--help" or "-h":
            Console.WriteLine(Help);
            return 0;
        case "--connection" when i + 1 < args.Length:
            connectionOverride = args[++i];
            break;
        case "--password" when i + 1 < args.Length:
            passwordOverride = args[++i];
            break;
        case "--output" when i + 1 < args.Length:
            outputOverride = args[++i];
            break;
        case "--config" when i + 1 < args.Length:
            configPathArg = args[++i];
            break;
        default:
            if (args[i].StartsWith("--"))
            {
                Console.Error.WriteLine($"Unknown option: {args[i]}");
                return 1;
            }
            entities.Add(args[i]);
            break;
    }
}

if (command == "help")
{
    var topic = args.Length > 1 ? args[1].ToLowerInvariant() : null;
    Console.WriteLine(topic switch
    {
        "setup"  => HelpSetup,
        "export" => HelpExport,
        "diff"   => HelpDiff,
        _        => Help
    });
    return 0;
}

if (command == "setup")
    return RunSetup();

var configPath = ConfigLoader.FindConfigPath(configPathArg);
if (configPath is null)
{
    Console.Error.WriteLine(configPathArg is not null
        ? $"Config file not found: {configPathArg}"
        : $"{ConfigLoader.ConfigFileName} not found. Run 'dotnet seed setup' to create it.");
    return 1;
}

var config = ConfigLoader.Load(configPath);
var configDir = Path.GetDirectoryName(configPath)!;

var assemblyPath = Path.IsPathRooted(config.AssemblyPath)
    ? config.AssemblyPath
    : Path.GetFullPath(Path.Combine(configDir, config.AssemblyPath));

if (!File.Exists(assemblyPath))
{
    Console.Error.WriteLine($"Assembly not found: {assemblyPath}");
    Console.Error.WriteLine("Build your project before running dotnet seed.");
    return 1;
}

string? connectionString;
if (connectionOverride is not null)
{
    connectionString = connectionOverride;
}
else if (config.AppConfig is not null)
{
    connectionString = ResolveFromAppConfig(config.AppConfig, configDir, passwordOverride);
    if (connectionString is null) return 1;
}
else if (!string.IsNullOrWhiteSpace(config.ConnectionString))
{
    var template = config.ConnectionString;
    if (template.Contains("{password}"))
    {
        var password = ResolvePassword(passwordOverride, config.UserSecrets, configDir);
        if (password is null)
        {
            Console.Error.WriteLine("Connection string template contains {password} but no password was supplied.");
            Console.Error.WriteLine("Options:");
            Console.Error.WriteLine("  Pass --password <pwd>");
            Console.Error.WriteLine("  Set the SEEDTOOL_PASSWORD environment variable");
            Console.Error.WriteLine("  Configure userSecrets in coredesign.seedtool.json");
            return 1;
        }
        connectionString = template.Replace("{password}", password);
    }
    else
    {
        connectionString = template;
    }
}
else
{
    Console.Error.WriteLine("No connection string configured. Run 'dotnet seed setup' to configure one.");
    return 1;
}

var seedDir = outputOverride is not null
    ? (Path.IsPathRooted(outputOverride)
        ? outputOverride
        : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), outputOverride)))
    : (Path.IsPathRooted(config.OutputDirectory)
        ? config.OutputDirectory
        : Path.GetFullPath(Path.Combine(configDir, config.OutputDirectory)));

connectionString = SanitizeConnectionString(connectionString);

try
{
    using var dbContext = ContextLoader.Load(assemblyPath, connectionString);
    var dbContextType = dbContext.GetType();
    var ct = CancellationToken.None;

    if (command == "export")
    {
        var exporterType = typeof(SeedExporter<>).MakeGenericType(dbContextType);
        var exporter = Activator.CreateInstance(exporterType, new object?[] { dbContext, null })!;

        var names = entities.Count > 0 ? entities
                  : config.Entities.Count > 0 ? [.. config.Entities]
                  : (List<string>?)null;

        if (names is not null)
        {
            var method = exporterType.GetMethod("ExportAsync",
                [typeof(IEnumerable<string>), typeof(string), typeof(CancellationToken)])!;
            await (Task)method.Invoke(exporter, [(IEnumerable<string>)names, seedDir, ct])!;
        }
        else
        {
            var method = exporterType.GetMethod("ExportAllAsync",
                [typeof(string), typeof(CancellationToken)])!;
            await (Task)method.Invoke(exporter, [seedDir, ct])!;
        }

        Console.WriteLine($"Export complete. Files written to: {seedDir}");
    }
    else // diff
    {
        List<string> entityList;
        if (entities.Count > 0)
            entityList = entities;
        else if (config.Entities.Count > 0)
            entityList = [.. config.Entities];
        else if (Directory.Exists(seedDir))
            entityList = [.. Directory.GetFiles(seedDir, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .OfType<string>()];
        else
            entityList = [];

        if (entityList.Count == 0)
        {
            Console.Error.WriteLine(
                $"No entities to diff. Specify entities as arguments, add them to {ConfigLoader.ConfigFileName}, " +
                $"or ensure seed files exist in {seedDir}.");
            return 1;
        }

        var diffType = typeof(SeedDiff<>).MakeGenericType(dbContextType);
        var diff = Activator.CreateInstance(diffType, new object?[] { dbContext })!;

        var diffMethod = diffType.GetMethod("DiffAsync",
            [typeof(IEnumerable<string>), typeof(string), typeof(CancellationToken)])!;

        var resultTask = (Task<IReadOnlyDictionary<string, IReadOnlyList<SeedDiffEntry>>>)
            diffMethod.Invoke(diff, [(IEnumerable<string>)entityList, seedDir, ct])!;
        var results = await resultTask;

        if (results.Count == 0)
        {
            Console.WriteLine("No seed files found to compare.");
            return 0;
        }

        var hasIssues = false;
        foreach (var (name, entries) in results.OrderBy(kvp => kvp.Key))
        {
            if (entries.Count == 0)
            {
                Console.WriteLine($"  {name}: OK");
            }
            else
            {
                hasIssues = true;
                var onlyInFile = entries.Count(e => e.Status == SeedDiffStatus.OnlyInFile);
                var onlyInDb   = entries.Count(e => e.Status == SeedDiffStatus.OnlyInDatabase);
                var different  = entries.Count(e => e.Status == SeedDiffStatus.Different);
                Console.WriteLine(
                    $"  {name}: {entries.Count} difference(s)  " +
                    $"[{onlyInFile} only in file, {onlyInDb} only in DB, {different} different]");
            }
        }

        return hasIssues ? 2 : 0;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    var inner = ex.InnerException;
    while (inner is not null)
    {
        Console.Error.WriteLine($"  {inner.Message}");
        inner = inner.InnerException;
    }
    return 1;
}

return 0;

// ---- Connection resolution helpers ----

static string? ResolvePassword(string? passwordOverride, UserSecretsSource? userSecrets, string configDir)
{
    if (passwordOverride is not null) return passwordOverride;

    var envPwd = Environment.GetEnvironmentVariable("SEEDTOOL_PASSWORD");
    if (!string.IsNullOrWhiteSpace(envPwd)) return envPwd;

    if (userSecrets is not null)
        return ReadUserSecretValue(userSecrets.Project, userSecrets.PasswordKey, configDir);

    return null;
}

static string? ResolveFromAppConfig(AppConfigSource appConfig, string configDir, string? passwordOverride)
{
    var projectDir = ResolvePath(configDir, appConfig.Project);

    if (!Directory.Exists(projectDir))
    {
        Console.Error.WriteLine($"App project directory not found: {projectDir}");
        return null;
    }

    var csproj = Directory.GetFiles(projectDir, "*.csproj").FirstOrDefault();
    var userSecretsId = csproj is not null ? ReadUserSecretsId(csproj) : null;

    var builder = new ConfigurationBuilder()
        .SetBasePath(projectDir)
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile("appsettings.Development.json", optional: true);

    if (userSecretsId is not null)
        builder.AddUserSecrets(userSecretsId);

    var cfg = builder.Build();
    var cs = cfg[appConfig.ConnectionStringKey];

    if (string.IsNullOrWhiteSpace(cs))
    {
        Console.Error.WriteLine($"Key '{appConfig.ConnectionStringKey}' not found in app config.");
        Console.Error.WriteLine("Checked: appsettings.json, appsettings.Development.json" +
            (userSecretsId is not null ? ", user secrets." : " (no UserSecretsId in .csproj)."));
        return null;
    }

    // Resolve Aspire-style {ParameterName} placeholders via Parameters:ParameterName in the same config stack
    cs = SubstituteConfigParameters(cs, cfg);
    if (cs is null) return null;

    if (cs.Contains("{password}"))
    {
        var password = passwordOverride ?? Environment.GetEnvironmentVariable("SEEDTOOL_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
        {
            Console.Error.WriteLine("Connection string contains {password} but no password was supplied.");
            Console.Error.WriteLine("Pass --password <pwd> or set SEEDTOOL_PASSWORD.");
            return null;
        }
        cs = cs.Replace("{password}", password);
    }

    return cs;
}

static string? SubstituteConfigParameters(string template, IConfiguration cfg)
{
    var failed = false;
    var result = Regex.Replace(template, @"\{(\w+)\}", match =>
    {
        var token = match.Groups[1].Value;
        if (token.Equals("password", StringComparison.OrdinalIgnoreCase))
            return match.Value;

        var value = cfg[$"Parameters:{token}"];
        if (value is null)
        {
            Console.Error.WriteLine(
                $"Placeholder {{{token}}} in connection string has no matching 'Parameters:{token}' in the project's config or user secrets.");
            failed = true;
            return match.Value;
        }
        return value;
    });

    return failed ? null : result;
}

static string? ReadUserSecretValue(string project, string key, string configDir)
{
    var projectDir = ResolvePath(configDir, project);

    var csproj = Directory.GetFiles(projectDir, "*.csproj").FirstOrDefault();
    if (csproj is null)
    {
        Console.Error.WriteLine($"No .csproj found in: {projectDir}");
        return null;
    }

    var secretsId = ReadUserSecretsId(csproj);
    if (secretsId is null)
    {
        Console.Error.WriteLine($"No <UserSecretsId> in {Path.GetFileName(csproj)}.");
        Console.Error.WriteLine($"Run 'dotnet user-secrets init' in {projectDir}");
        return null;
    }

    var secretsFile = GetSecretsFilePath(secretsId);
    if (!File.Exists(secretsFile))
    {
        Console.Error.WriteLine($"No user secrets found for {Path.GetFileName(csproj)}.");
        Console.Error.WriteLine($"Run 'dotnet user-secrets set \"{key}\" <value>' in {projectDir}");
        return null;
    }

    var value = ReadNestedJsonValue(secretsFile, key);
    if (value is null)
        Console.Error.WriteLine($"Key '{key}' not found in user secrets for {Path.GetFileName(csproj)}.");

    return value;
}

static string? ReadUserSecretsId(string csprojPath)
{
    var doc = XDocument.Load(csprojPath);
    return doc.Descendants("UserSecretsId").FirstOrDefault()?.Value;
}

static string GetSecretsFilePath(string userSecretsId)
{
    var basePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "UserSecrets")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".microsoft", "usersecrets");
    return Path.Combine(basePath, userSecretsId, "secrets.json");
}

static string? ReadNestedJsonValue(string jsonFile, string key)
{
    using var stream = File.OpenRead(jsonFile);
    using var doc = JsonDocument.Parse(stream);

    var current = doc.RootElement;
    foreach (var part in key.Split(':'))
    {
        if (!current.TryGetProperty(part, out current))
            return null;
    }

    return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
}

static string ResolvePath(string basePath, string path) =>
    Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(basePath, path));

static string SanitizeConnectionString(string cs)
{
    // Strip 'Integrated Security=false/no/0' — a SQL Server concept that some providers
    // (e.g. Npgsql) do not support. Removing the "false" case is safe: it means
    // "use password auth", which is already the default when Username/Password are present.
    cs = Regex.Replace(cs,
        @";?\s*Integrated\s+Security\s*=\s*(?:false|no|0)\s*",
        "",
        RegexOptions.IgnoreCase);
    return cs.Trim(';').Trim();
}

// ---- Setup ----

static int RunSetup()
{
    var cwd = Directory.GetCurrentDirectory();
    var configFile = Path.Combine(cwd, ConfigLoader.ConfigFileName);

    Console.WriteLine();
    Console.WriteLine("  CoreDesign Seed Tool Setup");
    Console.WriteLine("  Press Ctrl+C to cancel.");
    Console.WriteLine();

    if (File.Exists(configFile))
    {
        Console.Write($"  {ConfigLoader.ConfigFileName} already exists. Overwrite? [y/N]: ");
        var overwrite = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (overwrite is not "y" and not "yes")
        {
            Console.WriteLine("  Cancelled.");
            return 0;
        }
        Console.WriteLine();
    }

    Console.Write("  Assembly path (relative to this directory): ");
    var assemblyPath = Console.ReadLine()?.Trim() ?? "";
    if (string.IsNullOrWhiteSpace(assemblyPath))
    {
        Console.Error.WriteLine("  Assembly path is required.");
        return 1;
    }

    Console.Write("  Seed data directory [SeedData]: ");
    var outputDir = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(outputDir))
        outputDir = "SeedData";

    Console.Write("  Entities to export, space-separated (leave empty to export all): ");
    var entityInput = Console.ReadLine()?.Trim() ?? "";
    var entityList = entityInput
        .Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)
        .ToArray();

    Console.WriteLine();
    Console.WriteLine("  How should the tool obtain the connection string?");
    Console.WriteLine("    1  Project config      The connection string template and password parameter");
    Console.WriteLine("                           both live in a project's user secrets (or appsettings).");
    Console.WriteLine("                           The tool reads them from that project at runtime and");
    Console.WriteLine("                           resolves {ParameterName} placeholders automatically.");
    Console.WriteLine("                           Use this for Aspire AppHost projects and most others.");
    Console.WriteLine("    2  Template + secrets  You store the connection string template here (with");
    Console.WriteLine("                           {password}). The password is read at runtime from a");
    Console.WriteLine("                           specific key in another project's user secrets.");
    Console.WriteLine("    3  Template            You store the connection string template here (with");
    Console.WriteLine("                           {password}). The password is supplied at runtime via");
    Console.WriteLine("                           --password or SEEDTOOL_PASSWORD.");
    Console.WriteLine("    4  Direct              You store the full connection string here including");
    Console.WriteLine("                           the password. Not safe for committed config files.");
    Console.WriteLine();
    Console.Write("  Choice [1-4]: ");
    var choice = Console.ReadLine()?.Trim();
    Console.WriteLine();

    string? connectionString = null;
    UserSecretsSource? userSecrets = null;
    AppConfigSource? appConfig = null;
    var showPasswordReminder = false;

    switch (choice)
    {
        case "1":
            Console.WriteLine("  Project path (relative to this directory):");
            Console.WriteLine("  For Aspire apps use the AppHost project; for others use the app project.");
            Console.Write("  Project: ");
            var acProject = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(acProject))
            {
                Console.Error.WriteLine("  Project path is required.");
                return 1;
            }

            Console.WriteLine("  Connection string config key:");
            Console.Write("  Key [ConnectionStrings:mydb]: ");
            var acKey = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(acKey))
                acKey = "ConnectionStrings:mydb";

            Console.WriteLine();
            Console.Write("  Validating... ");
            var acSource = new AppConfigSource { Project = acProject, ConnectionStringKey = acKey };
            var resolvedCs = ResolveFromAppConfig(acSource, cwd, null);
            if (resolvedCs is null) return 1;
            Console.WriteLine($"found {acKey}");

            appConfig = acSource;
            break;

        case "2":
            Console.WriteLine("  Connection string template (use {password} for the credential):");
            Console.Write("  Template: ");
            connectionString = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.Error.WriteLine("  Connection string template is required.");
                return 1;
            }
            Console.WriteLine();

            Console.WriteLine("  Project containing the password in user secrets (relative to this directory):");
            Console.Write("  Project: ");
            var usProject = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(usProject))
            {
                Console.Error.WriteLine("  Project path is required.");
                return 1;
            }

            Console.WriteLine("  User secrets key for the password:");
            Console.Write("  Key: ");
            var usKey = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(usKey))
            {
                Console.Error.WriteLine("  Key is required.");
                return 1;
            }

            Console.WriteLine();
            Console.Write("  Validating user secrets... ");
            var usValue = ReadUserSecretValue(usProject, usKey, cwd);
            if (usValue is null) return 1;
            Console.WriteLine($"found {usKey} = ****");

            userSecrets = new UserSecretsSource { Project = usProject, PasswordKey = usKey };
            break;

        case "3":
            Console.WriteLine("  Connection string template (use {password} for the credential):");
            Console.Write("  Template: ");
            connectionString = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.Error.WriteLine("  Connection string template is required.");
                return 1;
            }
            showPasswordReminder = true;
            break;

        case "4":
            Console.WriteLine("  Warning: the password will be stored in coredesign.seedtool.json.");
            Console.WriteLine("  Add this file to .gitignore or use option 1, 2, or 3 instead.");
            Console.Write("  Connection string: ");
            connectionString = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.Error.WriteLine("  Connection string is required.");
                return 1;
            }
            break;

        default:
            Console.Error.WriteLine("  Invalid choice.");
            return 1;
    }

    var configObj = new SeedToolConfig
    {
        AssemblyPath = assemblyPath,
        OutputDirectory = outputDir,
        Entities = entityList,
        ConnectionString = connectionString,
        UserSecrets = userSecrets,
        AppConfig = appConfig
    };

    var json = JsonSerializer.Serialize(configObj, new JsonSerializerOptions
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    });

    File.WriteAllText(configFile, json);
    Console.WriteLine($"  Created {ConfigLoader.ConfigFileName}");

    if (showPasswordReminder)
    {
        Console.WriteLine();
        Console.WriteLine("  Pass the database password at runtime with --password or SEEDTOOL_PASSWORD:");
        Console.WriteLine("    dotnet seed export --password <your-password>");
        Console.WriteLine("    $env:SEEDTOOL_PASSWORD = \"<your-password>\"   # PowerShell, session only");
        Console.WriteLine("    export SEEDTOOL_PASSWORD=\"<your-password>\"    # bash/zsh, session only");
    }

    Console.WriteLine();
    Console.WriteLine("  Setup complete. Run 'dotnet seed export' to export seed data.");
    Console.WriteLine();
    return 0;
}
