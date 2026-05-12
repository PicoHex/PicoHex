# Contributing to PicoHex

Thanks for your interest in contributing. PicoHex is a minimal, AOT-first infrastructure library for .NET. This guide covers the basics to get you started.

## Build Commands

Build the entire solution:

```shell
dotnet build PicoHex.slnx
```

Build with AOT validation:

```shell
dotnet build PicoHex.slnx -p:PublishAot=true
```

## Test Commands

Run tests for individual modules:

```shell
dotnet test PicoDI/tests/PicoDI.Test/PicoDI.Test.csproj
dotnet test PicoCfg/tests/PicoCfg.Tests/PicoCfg.Tests.csproj
dotnet test PicoCfg/tests/PicoCfg.DI.Tests/PicoCfg.DI.Tests.csproj
dotnet test PicoCfg/tests/PicoCfg.Gen.Tests/PicoCfg.Gen.Tests.csproj
dotnet test PicoLog/tests/PicoLog.Tests/PicoLog.Tests.csproj
```

Or run all tests at once:

```shell
dotnet test PicoHex.slnx
```

## PR Workflow

1. Fork the repository on GitHub.
2. Create a feature branch from `main`:

   ```shell
   git checkout -b feat/your-feature-name
   ```

3. Make your changes. Keep commits atomic and well-described.
4. Run the build and all tests:

   ```shell
   dotnet build PicoHex.slnx && dotnet test PicoHex.slnx
   ```

5. Push your branch and open a pull request against `main`.
6. In the PR description, explain what the change does and why it is needed.
7. A maintainer will review your PR. Address any feedback by pushing additional commits.

## Coding Conventions

This project uses the following C# conventions:

- **Nullable enabled**: All projects have `<Nullable>enable</Nullable>`. Write null-safe code.
- **File-scoped namespaces**: Use `namespace PicoHex.Foo;` not block-scoped namespaces.
- **Target-typed new**: Use `new()` instead of repeating the type name when the type is obvious.
- **Sealed types**: Prefer `sealed class` unless the type is designed for inheritance.
- **Primary constructors**: Use primary constructors for simple types that take dependencies.
- **No reflection**: Use source generators or factory delegates instead of `Activator.CreateInstance`, `Expression`, or runtime emit.
- **XML docs**: Public APIs must have XML doc comments. Internal and private APIs should have them where helpful.

Run `dotnet format` before committing to catch style issues automatically:

```shell
dotnet format PicoHex.slnx --verbosity normal
```

## AOT Testing Guide

PicoHex is AOT-first. Before submitting, verify your changes compile with Native AOT:

```shell
# Publish a test project with AOT enabled
dotnet publish PicoDI/tests/PicoDI.Test/PicoDI.Test.csproj -c Release -p:PublishAot=true

# Or for a specific project
dotnet publish <project>.csproj -c Release -p:PublishAot=true
```

If your change introduces new dependencies, make sure they are AOT-compatible (no runtime code generation, no unsupported reflection).

Key things to watch for:
- No calls to `Activator.CreateInstance` or `RuntimeHelpers.GetUninitializedObject`.
- No `Expression.Compile` or dynamic method generation.
- Generic types used with value types should not trigger unexpected reflection.
- Trimming warnings should be treated as errors.
