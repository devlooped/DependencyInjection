# AGENTS.md

`Devlooped.Extensions.DependencyInjection` is a compile-time source generator (Roslyn analyzer)
that emits `IServiceCollection.AddServices()` registrations from `[Service]` attributes or
conventions. There is no runnable server; the "application" is the generator itself, exercised by
the test projects and by the `src/Samples/ConsoleApp` demo.

General build/test/format commands and conventions are documented in
[`.github/copilot-instructions.md`](.github/copilot-instructions.md) and [`readme.md`](readme.md).

## Cursor Cloud specific instructions

The update script keeps NuGet packages restored. The .NET SDK/runtimes are provisioned once during
environment setup and persist in the VM snapshot (`~/.dotnet`, added to `PATH` via `~/.bashrc`).

Toolchain required (see `.github/dotnet.json`): the **.NET 10 SDK** plus runtimes for **6.0, 8.0,
and 10.0**. All three runtimes matter for running things, even though SDK 10 alone compiles every
target framework:
- `net10.0` runtime → `DependencyInjection.Tests` and `NoAddServices`.
- `net8.0` runtime → `CodeAnalysis.Tests`. Without it, `dnx retest` silently runs only the
  net10 project (41 vs 30 tests) and `dotnet test` aborts that assembly with an "8.0.0 not found" error.
- `net6.0` runtime → the `src/Samples/ConsoleApp` demo (its TFM is `net6.0`).

Commands (run from repo root):
- Build: `dotnet build -m:1` (single-threaded per `Directory.Build.rsp`). The first build downloads
  and installs the `dotnet-ilrepack` global tool — this needs network access.
- Test (all 41): `dnx --yes retest -- --no-build`, or `dotnet test --no-build` for per-project results.
- Format/lint: `dotnet format whitespace --verify-no-changes -v:diag --exclude ~/.nuget` and
  `dotnet format style --verify-no-changes -v:diag --exclude ~/.nuget`.

Gotchas:
- The `src/Samples/*` projects are **not** part of `DependencyInjection.slnx`. They consume the
  package via a floating `Version="42.*"` from the local `bin/` feed, so to build/run the
  `ConsoleApp` demo you must first produce the package:
  `dotnet pack src/Package/DependencyInjection.Package.msbuildproj -m:1` (outputs to `bin/`).
  Then `cd src/Samples/ConsoleApp && dotnet run` (prints `Hello`).
- Recurring harmless build warnings: `NuGetizer.PackageMetadata.targets ... cannot be imported
  again` (MSB4011), `net6.0` EOL/support warnings on the sample, and an `NU1608` version-constraint
  warning in `CodeAnalysis.Tests`. None of these fail the build/tests.
