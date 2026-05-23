# PicoHex

**AOT ファーストな .NET 汎用最小インフラストラクチャ**

3 モジュール、11 パッケージ、ランタイムリフレクションゼロ。

[![NuGet](https://img.shields.io/nuget/v/PicoDI)](https://nuget.org/packages/PicoDI)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![CI](https://github.com/PicoHex/PicoHex/actions/workflows/ci.yml/badge.svg)](https://github.com/PicoHex/PicoHex/actions/workflows/ci.yml)

[English](README.md) | [简体中文](README.zh.md) | [日本語](README.ja.md) | [Español](README.es.md) | [Português](README.pt.md) | [繁體中文](README.zh-tw.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Русский](README.ru.md)

---

## 計算モデル

```
設定 (Configuration)  ──→  依存性注入 (DI)  ──→  ロギング (Logging)
  (入力)                     (中核)                (出力)
```

あらゆるアプリケーションは設定を読み込み、内部構造を組み立て、出力を生成します。**PicoHex** は、.NET Native AOT 向けにこれら 3 つのメタ操作を最小限で実装したものです &mdash; ランタイムリフレクションではなく、コンパイル時のコード生成を前提にゼロから設計されています。

---

## PicoHex を選ぶ理由

| | Microsoft.Extensions | PicoHex |
|---|---|---|
| **パッケージ数** | 40+ | 11 |
| **ランタイムリフレクション** | 多用（`Activator.CreateInstance`、式木） | ゼロ（ソースジェネレータ） |
| **AOT 対応** | オプトインと注意深い設定が必要 | AOT ファースト &mdash; そのままネイティブコンパイル可能 |
| **HostBuilder 必須** | はい &mdash; DI + Config + Logging の結合に必須 | いいえ &mdash; `new SvcContainer()` だけで十分 |
| **モジュール統合** | ランタイム登録（`IServiceCollection`） | コンパイル時（`ModuleInitializer` + ソースジェネレータ） |
| **コールドスタート** | 低速（JIT + リフレクション） | 高速（事前生成されたコードパス） |
| **バイナリサイズ** | 大（多数のアセンブリを取り込む） | 最小（トリミング可能、リンカフレンドリ） |

---

## クイックスタート

### 設定のみ

```shell
dotnet add package PicoCfg
```

```csharp
using PicoCfg;
using PicoCfg.Abs;
using PicoCfg.Extensions;

var cfg = await Cfg.CreateBuilder()
    .Add("App:Name=MyApp\nApp:Version=1.0")
    .BuildAsync();

var value = cfg.GetValue("App:Name");
```

### DI のみ

```shell
dotnet add package PicoDI
```

```csharp
using PicoDI;
using PicoDI.Abs;

var container = new SvcContainer();
container.RegisterSingleton<IService>(scope => new MyService());
container.Build();

await using var scope = container.CreateScope();
var svc = scope.GetService<IService>();
```

PicoDI は**コンパイル時 AOP / インターセプター**にも対応しています — Register() の後ろに .InterceptBy<TInterceptor>() をチェーンすると、ソースジェネレーターがビルド時にデコレータークラスを生成します。[詳細 →](PicoDI/README.md#interceptor--aop-compile-time-decorators)

### ロギングのみ

```shell
dotnet add package PicoLog
```

```csharp
using PicoLog;
using PicoLog.Abs;

var sink = new ColoredConsoleSink(new ConsoleFormatter());
using var factory = new LoggerFactory([sink],
    new LoggerFactoryOptions { MinLevel = LogLevel.Info });
var logger = factory.CreateLogger("App");
logger.Info("Application started");
```

### すべてを統合

```shell
dotnet add package PicoCfg.DI
dotnet add package PicoLog.DI
```

```csharp
using PicoDI;
using PicoDI.Abs;
using PicoCfg;
using PicoCfg.Abs;
using PicoCfg.DI;
using PicoCfg.Extensions;
using PicoLog.Abs;
using PicoLog.DI;

var container = new SvcContainer();
var cfg = await Cfg.CreateBuilder()
    .AddEnvironmentVariables("APP_")
    .AddCommandLine(args)
    .BuildAsync();
container.RegisterCfgRoot(cfg);
container.AddPicoLog(o => { o.MinLevel = LogLevel.Info; o.WriteTo.ColoredConsole(); });
await using var scope = container.CreateScope();
var logger = scope.GetService<ILogger<Program>>();
```

PicoDI は**コンパイル時 AOP / インターセプター**にも対応しています — Register() の後ろに .InterceptBy<TInterceptor>() をチェーンすると、ソースジェネレーターがビルド時にデコレータークラスを生成します。[詳細 →](PicoDI/README.md#interceptor--aop-compile-time-decorators)

---

## パッケージ一覧

| パッケージ | 説明 |
|---|---|
| **PicoDI** | リフレクションゼロの DI コンテナ |
| **PicoDI.Abs** | DI 抽象化 |
| **PicoDI.Gen** | コンパイル時登録用ソースジェネレータ |
| **PicoCfg** | 非同期ファースト設定管理 |
| **PicoCfg.Abs** | 設定抽象化 |
| **PicoCfg.Gen** | 型付きバインディング用ソースジェネレータ |
| **PicoCfg.DI** | PicoCfg の DI 統合 |
| **PicoLog** | 構造化ロギング |
| **PicoLog.Abs** | ロギング抽象化 |
| **PicoLog.Gen** | `[PicoLogMessage]` ソースジェネレータ |
| **PicoLog.DI** | PicoLog の DI 統合 |

---

## 設計哲学

**克制 (Restraint)** &mdash; DI、Config、Logging のみ。Web フレームワークも ORM もメッセージキューもありません。あらゆるアプリケーションが必要とする共通項。

**专注 (Focus)** &mdash; 各モジュールはひとつのことだけを行います。浅い汎用性よりも深い特化を。

**优雅 (Elegance)** &mdash; `new SvcContainer()` が 100 行を超える `Host.CreateDefaultBuilder()` の儀式を置き換えます。ソースジェネレータが配線をコンパイル時に行います。

**高效 (Efficiency)** &mdash; AOT ファースト。リフレクションゼロ。コンパイル時に解決可能なものはすべてコンパイル時に解決します。

---

## 詳細情報

- [PicoDI](PicoDI/README.md) — DI コンテナ、登録、ソースジェネレータ
- [PicoCfg](PicoCfg/README.md) — 設定プロバイダ、バインディング、ファイル監視
- [PicoLog](PicoLog/README.md) — 構造化ロギング、シンク、メッセージテンプレート
- [コントリビューション](CONTRIBUTING.md)
- [セキュリティ](SECURITY.md)

---

MIT ライセンス。 [https://github.com/PicoHex/PicoHex](https://github.com/PicoHex/PicoHex)