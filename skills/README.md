# CoreDesign skill for AI coding agents

The `coredesign` folder is an agent skill that teaches AI coding agents (Claude Code and other tools that support the skill format) how to use the CoreDesign 11 packages correctly: how to set up a new solution with every CoreDesign feature, how to add features in the vertical slice style, and how to avoid the mistakes that show up in real projects.

## What it covers

| File | Contents |
|---|---|
| `SKILL.md` | The design, the feature recipe, and the rules that prevent common mistakes |
| `references/project-setup.md` | A complete solution from scratch: shared settings, AppHost, API, migration service, identity server, a full CRUD feature, seed data, and a checklist of every feature |
| `references/shared-and-unions.md` | Result records and `Union<...>` |
| `references/data.md` | Entities, repositories, audit fields, soft delete, write patterns, migration worker, seeding |
| `references/logging.md` | `[LoggingDecorator]`, `DecorateWithLogging()`, `[Redact]`, `[Suppress]` |
| `references/exception-handling.md` | Global ProblemDetails and `[ProblemMapping]` |
| `references/identity.md` | Development identity server, permissions, Entra, Blazor and SPA sign-in |
| `references/seedtool.md` | `dotnet seed export` and `diff` |
| `references/testing.md` | Handler and endpoint tests |
| `references/upgrading.md` | Moving from CoreDesign 1.x and OneOf to 11.x |

The code in `project-setup.md` and `testing.md` was compiled and run on .NET 11 against the CoreDesign 11.0.0 packages: the solution builds, EF tooling creates the migration, the migration service migrates and seeds, and every endpoint, permission and error path returns the documented result.

## Install

For Claude Code, copy the `coredesign` folder into your personal skills folder to use it in every project:

```
# Windows (PowerShell)
Copy-Item -Recurse skills\coredesign "$env:USERPROFILE\.claude\skills\coredesign"

# macOS or Linux
cp -r skills/coredesign ~/.claude/skills/coredesign
```

Or copy it into a repository's `.claude/skills/` folder to share it with everyone working in that repository. The agent loads the skill automatically when a task involves CoreDesign.

## Versions

The skill matches CoreDesign 11.x on .NET 11. Update it with each CoreDesign release.
