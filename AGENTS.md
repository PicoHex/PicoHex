# AGENTS.md — PicoHex

AOT-first .NET 10 monorepo: three modules (PicoDI, PicoCfg, PicoLog), eleven packages, zero runtime reflection.

## Quick Commands

```bash
# Build everything
dotnet build PicoHex.slnx

# Build single project
dotnet build PicoDI/src/PicoDI/PicoDI.csproj

# Run all tests
dotnet test PicoHex.slnx

# Run single test project (TUnit + Microsoft.Testing.Platform)
dotnet test PicoDI/tests/PicoDI.Test/PicoDI.Test.csproj

# Format code
dotnet csharpier PicoHex.slnx

# AOT publish a sample (validate AOT compat)
dotnet publish PicoDI/samples/PicoDI.Sample.Host/ -c Release -r win-x64 -p:PublishAot=true
```

## Architecture

```
Module/            Package        TFM              Purpose
PicoDI/src/        .Abs           netstandard2.0   Interfaces (ISvcContainer, ISvcScope, SvcDescriptor)
                   .Gen           netstandard2.0   Roslyn source gen (analyzer + ModuleInitializer)
                   /              net10.0          DI container (references .Abs + .Gen as analyzer)
PicoCfg/src/       .Abs           netstandard2.0   Interfaces (ICfg, ICfgRoot)
                   .Gen           netstandard2.0   CfgBind.Bind<T> source generator
                   /              net10.0          Configuration runtime
                   .DI            net10.0          DI integration (references PicoDI + PicoCfg)
PicoLog/src/       .Abs           netstandard2.0   Interfaces
                   .Gen           netstandard2.0   [PicoLogMessage] source generator
                   /              net10.0          Logging runtime
                   .DI            net10.0          DI integration
```

**Pack layer order**: Abstractions first → Generators second → Core/DI third. Never break this.

## Critical Build Config

- **Central Package Management** (CPM): `Directory.Packages.props` owns all versions. Add `<PackageVersion>` there, not in csproj.
- **UseProjectReferences**: Defaults `true` (local dev uses `ProjectReference`). CI/release sets `false` to validate PackageReference consumption. Always pass `-p:UseProjectReferences=true` for local test/build commands.
- **Version**: Default `0.0.0-dev`. CI/release override via `-p:Version=<tag>`.
- **AOT**: Every project PublishAot=true by default (via Directory.Build.props). `.Abs` and `.Gen` projects must override with `<TreatAsLocalProperty="PublishAot">` + `<PublishAot>false</PublishAot>` since they target netstandard2.0.
- **Solution file**: `.slnx` XML format, not `.sln`. VS/Rider 2024+ required.
- **SDK**: .NET 10.0.x (see global.json).

## Testing

- **Framework**: TUnit (not xUnit/NUnit). Runner: Microsoft.Testing.Platform (see `global.json` `test.runner`).
- **No `--filter`**: TUnit uses its own filter syntax after `--`. Use `dotnet test --project ... -- --filter "FullyQualifiedName~MyTest"`.
- **Coverage**: `dotnet test --project ... --coverage --coverage-output-format cobertura`.
- **Generator tests**: Use `CSharpGeneratorDriver.RunGeneratorsAndUpdateCompilation()` in-process, then verify the output compilation emits successfully (no golden files — compile-and-verify is more robust than text diff).
- **Test project pattern**: `OutputType=Exe`, references `TUnit` package, `TestingPlatformDotnetTestSupport=true`.

## Source Generators

- Target `netstandard2.0` with Roslyn analyzer references.
- Must have `EnforceExtendedAnalyzerRules=true`, `IsRoslynComponent=true`, `IncludeBuildOutput=false`.
- Generator DLL delivered via `buildTransitive/<name>/analyzers/dotnet/cs/` in the NuGet package + a companion `.props` file.
- Generator tests use `CSharpGeneratorDriver.RunGeneratorsAndUpdateCompilation()` to exercise generators in-process.

## Code Conventions

- **File-scoped namespaces** (`namespace Foo;` not `namespace Foo { }`).
- **`var`** for built-in types and when type is apparent. Explicit type elsewhere.
- **Sealed classes** by default (unless designed for inheritance).
- **Nullable enabled** everywhere.
- **XML docs** required on public APIs (`CS1591` suppressed for non-packable projects).
- **No reflection**: No `Activator.CreateInstance`, `Expression.Compile`, or runtime emit. Use source generators or factory delegates.
- **Trimming warnings as errors**: AOT-compatible code only.
- `dotnet csharpier PicoHex.slnx` before commit (CSharpier installed as global dotnet tool, no config needed).

## CI

- **5-platform matrix**: win-x64, win-arm64, linux-x64, linux-arm64, osx-arm64.
- **Path-filtered**: Each module's CI job only runs when its files or root build files change.
- **Linux AOT needs**: `clang`, `zlib1g-dev` (and `gcc-aarch64-linux-gnu` for arm64).
- **CI package validation**: Packs with `UseProjectReferences=false` to validate consumer experience.
- **Release**: Tag `v*` triggers pack→publish. Phase 1: Abs → Phase 2: Gen → Phase 3: Core + .DI packages (inter-package dependencies via `RestoreAdditionalProjectSources` pointing to just-built nupkgs).

## Local NuGet

`NuGet.config` adds `artifacts/nupkg/` as a local source. When working with inter-package dependencies locally, `dotnet pack` to that directory first, then restore consumers.

## Do NOT

- Add runtime reflection or `Activator.CreateInstance`
- Use block-scoped namespaces
- Change a .Abs or .Gen project's TFM away from netstandard2.0
- Add package versions directly to csproj (use Directory.Packages.props)
- Run `dotnet test` with `--filter` (TUnit uses `--` separator)
- Commit `.verified.g.cs` golden files without checking they match current generator output
- Change package layering (Abs → Gen → Core/DI dependency chain)
