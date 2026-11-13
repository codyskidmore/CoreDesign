# Updates NuGet dependencies for the CoreDesign 10.x (.NET 10) line.
#
# Package updates ignore global.json, so plain "dotnet outdated --upgrade" pulls .NET 11 packages.
# --version-lock Major keeps every package on its current major version:
#   Microsoft.* stays on 10.0.x and Microsoft.OpenApi stays on 2.x (ASP.NET Core 10 requires 2.x).
# Microsoft.CodeAnalysis.CSharp stays on 5.3.0 on purpose: it is the minimum compiler the source
# generators need, and a newer version disables them for users on older .NET 10 SDKs.
#
# Usage: .\update-dependencies.ps1          (preview)
#        .\update-dependencies.ps1 -Apply   (update the project files)

param([switch]$Apply)

$arguments = @('outdated', $PSScriptRoot, '--version-lock', 'Major', '--exclude', 'Microsoft.CodeAnalysis.CSharp')
if ($Apply) { $arguments += '--upgrade' }

dotnet @arguments
