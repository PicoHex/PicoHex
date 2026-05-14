# PicoHex

**AOT ファーストな .NET 汎用最小インフラストラクチャ**

プロダクショングレードの .NET アプリケーションのための最小汎用インフラストラクチャ &mdash; 3 モジュール、11 パッケージ、ランタイムリフレクションゼロ。

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

## パフォーマンス

ベンチマークは **.NET 10.0.5、Windows 10、X64、Native AOT、Release モード** で実行しています。

### PicoDI vs Microsoft.Extensions.DependencyInjection

**20/20 で勝利、平均 2.83 倍高速、最大 4.00 倍（DeepChain × Transient）。**

| シナリオ | PicoDI (ns) | MsDI (ns) | 高速化率 |
|---|---|---|---|
| **DeepChain × Transient** | 156.7 | 626.9 | **4.00 倍** |
| **NoDependency × Singleton** | 14.2 | 55.1 | **3.89 倍** |
| **MultipleResolutions × Singleton** | 1,523.6 | 5,373.9 | **3.53 倍** |
| **MultipleResolutions × Transient** | 4,672.4 | 16,402.1 | **3.51 倍** |
| **NoDependency × Transient** | 27.7 | 97.1 | **3.50 倍** |
| ContainerSetup | 739.9 | 1,919.1 | 2.59 倍 |
| SingleResolution × Transient | 55.3 | 187.8 | 3.39 倍 |
| ScopeCreation | 94.1 | 104.5 | 1.11 倍 |

### PicoCfg vs Microsoft.Extensions.Configuration

**混合ワークロードで 1.35 倍～1.57 倍高速。**

| シナリオ | PicoCfg (ns) | MsConfig (ns) | 高速化率 |
|---|---|---|---|
| Mixed n=100, p=2, l=1 | 5,920.9 | 9,273.9 | **1.57 倍** |
| Mixed n=100, p=2, l=10 | 29,831.8 | 40,218.9 | **1.35 倍** |

### PicoLog vs Microsoft.Extensions.Logging

PicoLog はよりリッチなログエントリ（タイムスタンプ、カテゴリ、スコープ、プロパティ）を構築するため、非同期ハンドオフパスは Microsoft の軽量文字列チャネル方式と同等の性能になります。**コントロールベンチマーク**はキュー／シンクのオーバーヘッドを除いた純粋な効率性を示します：

| コントロールベンチマーク | N=1 | N=10 | N=100 |
|---|---|---|---|
| **TimestampNowOnly** | 4.15 倍 | 5.05 倍 | 4.54 倍 |
| **LogEntryAllocateOnly** | 8.48 倍 | 23.48 倍 | 20.11 倍 |

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

var svc = container.CreateScope().GetService<IService>();
```

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

---

## PicoDI

リフレクションゼロの DI コンテナ。コンパイル時にソース生成を行います。

### 登録

すべての `Register*` メソッドは `ISvcContainer` を返すため、メソッドチェーンによる fluent な記述が可能です。

```csharp
var container = new SvcContainer();

// ファクトリベース（常に動作、ソースジェネレータ不要）
container.RegisterSingleton<IService>(scope => new Service(scope.GetService<IDep>()));
container.RegisterScoped<IRepository>(scope => new Repository());
container.RegisterTransient<IValidator>(scope => new Validator());

// 事前生成インスタンス
container.RegisterSingle<IClock>(SystemClock.Instance);

// オープンジェネリクス
container.Register(typeof(IRepository<>), typeof(SqlRepository<>), SvcLifetime.Scoped);

// 型ベース（PicoDI.Gen ソースジェネレータが必要）
container.RegisterSingleton<IService, Service>();

// ホステッドサービス
container.RegisterHostedSvc<BackgroundWorker>();

container.Build();  // 登録を凍結
```

### 解決

```csharp
using var scope = container.CreateScope();

// 型指定解決（生成された Resolve.* メソッドによるゼロルックアップ）
var svc = scope.GetService<IService>();
var repos = scope.GetServices<IRepository>();

// 型ベースの解決
var instance = scope.GetService(typeof(IService));
```

### ライフタイム

| ライフタイム | インスタンス化 | 破棄 |
|---|---|---|
| **Transient** | 要求のたびに新規作成 | 解決スコープが追跡、LIFO 順で破棄 |
| **Scoped** | スコープごとに 1 回 | スコープ破棄時に破棄 |
| **Singleton** | コンテナごとに 1 回 | コンテナ破棄時に破棄 |

### ソースジェネレータ（PicoDI.Gen）

`PicoDI.Gen` をアナライザとして追加すると、コンパイル時の型ベース登録が有効になります：

```xml
<PackageReference Include="PicoDI.Gen" PrivateAssets="all" />
```

ジェネレータはすべての `Register*` 呼び出しをスキャンし、以下を生成します：

- **`ConfigureGeneratedServices()`** 拡張メソッド &mdash; インラインファクトリデリゲートによりリフレクション不要
- **型指定された `Resolve.*`** メソッド &mdash; ゼロルックアップの解決パス
- **コンパイル時の循環依存検出**
- **アセンブリ間発見のためのオープンジェネリクスメタデータ**
- **`[ModuleInitializer]`** 自動構成 &mdash; `SvcContainerAutoConfiguration` に登録

**Before**（あなたのコード）：
```csharp
var container = new SvcContainer();
container.RegisterSingleton<IService, Service>();
container.Build();
```

**After**（生成コード）：
```csharp
// PicoDI.Gen により自動生成
partial class SvcContainerGeneratedConfiguration
{
    [ModuleInitializer]
    public static void Initialize() =>
        SvcContainerAutoConfiguration.RegisterConfigurator(
            "PicoDI.Generated",
            c => c.RegisterSingleton(typeof(IService),
                _ => new Service(_.GetService<IDep>())));
}
```

### SvcHostBuilder

ホステッドサービスのためのオプショナルな fluent ビルダー：

```csharp
using var hostBuilder = new SvcHostBuilder();
var host = await hostBuilder
    .ConfigureServices(container =>
    {
        container.RegisterSingleton<IService>(_ => new Service());
        container.RegisterHostedSvc<Worker>();
    })
    .BuildAsync();

// アプリケーションは停止されるまで実行
await host.StopAsync();
```

---

## PicoCfg

プロバイダモデルを採用した、非同期ファーストの設定管理。

### プロバイダモデル

```
Sources ──→ Providers ──→ Root ──→ Consumer
```

Source は設定の**生成方法**を定義します。Provider は具体化されたインスタンスです。Root が各プロバイダのスナップショットを合成し、統一されたビューを提供します。Consumer は `TryGetValue` または `GetValue` でクエリします。

### ビルトイン Source

| Source | 説明 |
|---|---|
| **Dictionary** | メモリ内のキー値ペア |
| **Environment Variables** | OS 環境変数、プレフィックスフィルタリング、`__` → `:` マッピング |
| **Command Line** | `--key=value`、`--key value`、`-key value`、`/key value` |
| **Stream** | 行ベースの `key=value` テキスト解析、ファイル監視付き |
| **File Watching** | デバウンス付きファイル変更自動リロード |
| **Chained** | 別の `ICfg` インスタンスへのフォールバック |
| **KeyPerFile** | Kubernetes ConfigMap スタイル &mdash; ファイル名 = キー、内容 = 値 |

### CfgBuilder

```csharp
using PicoCfg;
using PicoCfg.Abs;
using PicoCfg.Extensions;

var cfg = await Cfg.CreateBuilder()
    .AddEnvironmentVariables("APP_")
    .Add(() => File.OpenRead("appsettings.cfg"))
    .Add(() => File.OpenRead($"appsettings.{env}.cfg"),
        watchPath: $"appsettings.{env}.cfg")
    .AddCommandLine(args)
    .BuildAsync();

var name = cfg.GetValue("App:Name");
```

### ソース生成によるバインディング

`PicoCfg.Gen` をアナライザとして追加します。ソースジェネレータは `CfgBind.Bind<T>` / `CfgBind.TryBind<T>` / `CfgBind.BindInto<T>` の呼び出し箇所からコンパイル時にバインディング対象を発見します &mdash; 属性は不要です。生成されたコードは、ランタイムリフレクションを介さずにキーパスで設定値を解決する、厳密に型付けされたバインダーを登録します。

```csharp
using PicoCfg;

public sealed class AppSettings
{
    public string Name { get; init; }
    public int MaxRetries { get; init; } = 3;
    public bool EnableFeature { get; init; }
}

// 呼び出し箇所が AppSettings のソース生成をトリガー
var settings = CfgBind.Bind<AppSettings>(cfg, "App");
```

---

## PicoLog

コンパイル時のメッセージテンプレートによる構造化ロギング。

### ログレベル

| レベル | 値 | 用途 |
|---|---|---|
| **Emergency** | 0 | システム使用不可 |
| **Alert** | 1 | 即時対応が必要 |
| **Critical** | 2 | 重大な状態 |
| **Error** | 3 | エラー状態 |
| **Warning** | 4 | 警告状態 |
| **Notice** | 5 | 通常だが重要な状態 |
| **Info** | 6 | 情報メッセージ |
| **Debug** | 7 | デバッグレベルのメッセージ |
| **Trace** | 8 | 詳細な診断トレース |
| **None** | 255 | すべてのロギングを無効化 |

### メッセージテンプレート

文字列補間と `FormattableString` の両方をサポート。名前付きパラメータにより構造化ロギングを実現：

```csharp
logger.Info($"Processing order {orderId} for {customer}");
logger.Log(LogLevel.Info, $"User {user} logged in from {ipAddress}");

// 明示的なプロパティによる構造化ロギング
logger.LogStructured(LogLevel.Error, "Payment failed",
    properties: [new("OrderId", orderId), new("Amount", amount)],
    exception: ex);
```

### ソース生成メッセージ

`[PicoLogMessage]` 属性を static partial メソッドに付与すると、AOT 互換の型付き拡張メソッドが生成されます：

```csharp
public static partial class AppLogs
{
    [PicoLogMessage(LogLevel.Info, EventId = 1001, Message = "Order {OrderId} placed")]
    public static partial void OrderPlaced(this ILogger logger, string orderId);

    [PicoLogMessage(LogLevel.Error, EventId = 2001, Message = "Payment failed")]
    public static partial void PaymentFailed(this ILogger logger, string orderId, Exception ex);
}

// 使用例
logger.OrderPlaced("ORD-12345");
logger.PaymentFailed("ORD-12345", ex);
```

### ビルトインシンク

```csharp
var sinks = new List<ILogSink>
{
    new ConsoleSink(new ConsoleFormatter()),          // プレーンコンソール
    new ColoredConsoleSink(new ConsoleFormatter()),    // レベル別色分け表示
    new FileSink(new ConsoleFormatter(),               // バッチファイル出力
        new FileSinkOptions { FilePath = "app.log", BatchSize = 100 })
};
using var factory = new LoggerFactory(sinks);
```

### カスタムシンク

```csharp
public sealed class CustomSink : ILogSink
{
    public Task WriteAsync(LogEntry entry, CancellationToken ct = default)
    {
        // 任意のバックエンドに書き込む
        return Task.CompletedTask;
    }

    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

---

## モジュール統合

`PicoDI`、`PicoCfg.DI`、`PicoLog.DI` がすべて参照されている場合、配線は自動的に行われます：

1. 各モジュールのソースジェネレータが `ModuleInitializer` ベースの構成子を登録
2. `new SvcContainer()` が `SvcContainerAutoConfiguration.TryApplyConfiguration` を介して全構成子を実行
3. 手動での配線は不要

### DI 統合 API

**PicoCfg.DI** &mdash; `ISvcContainer` の拡張メソッド：
- `RegisterCfgRoot(ICfgRoot root)` &mdash; `ICfgRoot` と `ICfg` を登録
- `RegisterCfgTransient/Scoped/Singleton<T>(string? section)` &mdash; 設定から POCO をバインド
- `RegisterCfgOptionsSingleton/Scoped<T>(string? section)` &mdash; 型付きオプションのサポート

**PicoLog.DI** &mdash; `ISvcContainer` の拡張メソッド：
- `AddPicoLog(Action<LoggingOptions> configure)` &mdash; `ILoggerFactory` と `ILogger<>` を登録
- `WriteTo.Console()` / `.ColoredConsole()` / `.File(path)` / `.Sink(custom)` &mdash; シンク構成
- `ReadFrom.RegisteredSinks()` &mdash; DI 登録された `ILogSink` インスタンスを含める

### 最小構成の統合例

```shell
dotnet add package PicoDI
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
    .Add(() => File.OpenRead("appsettings.cfg"))
    .BuildAsync();
container.RegisterCfgRoot(cfg);

container.AddPicoLog(o =>
{
    o.MinLevel = LogLevel.Info;
    o.WriteTo.ColoredConsole();
});
var logger = container.CreateScope().GetService<ILogger<Program>>();
logger.Info("App started");
```

---

## 設計哲学

**克制 (Restraint)** &mdash; DI、Config、Logging のみ。Web フレームワークも ORM もメッセージキューもありません。汎用インフラストラクチャでないものは属しません。PicoHex はあらゆるアプリケーションが必要とする共通項 &mdash; それ以上でもそれ以下でもありません。

**专注 (Focus)** &mdash; 各モジュールはひとつのことだけを行います。PicoDI はコンテナであってサービロケータではありません。PicoCfg は設定管理であって機能フラグシステムではありません。PicoLog はロギングであってテレメトリパイプラインではありません。浅い汎用性よりも深い特化を。

**优雅 (Elegance)** &mdash; API は最小限に。ソースジェネレータが配線をコンパイル時に行います。開発者は率直なコードを書くだけで、ツールが複雑さを処理します。`new SvcContainer()` が 100 行を超える `Host.CreateDefaultBuilder()` の儀式を置き換えます。

**高效 (Efficiency)** &mdash; AOT ファーストは後付けではなく基盤です。リフレクションゼロ。ランタイムオーバーヘッドゼロ。コンパイル時に解決可能なものはすべてコンパイル時に解決します。最小のバイナリ、高速なコールドスタート、予測可能なパフォーマンス。

---

## ユースケース

| シナリオ | PicoDI | PicoCfg | PicoLog | 理由 |
|---|---|---|---|---|
| **CLI ツール** | 任意 | 必須 | 必須 | 引数や設定の解析、出力の冗長性制御。必要に応じて DI も。 |
| **サーバーレス / Lambda** | 必須 | 必須 | 必須 | コールドスタートがボトルネック。事前生成された解決パスによる AOT コンパイル DI。 |
| **WASM / Blazor** | 必須 | 必須 | 必須 | ダウンロードサイズが重要。11 パッケージ、トリミング対応、ランタイムの肥大化なし。 |
| **組み込み / IoT** | 必要に応じて | 必須 | 必須 | リソース制約のあるデバイス。最小バイナリ、可能な限りゼロアロケーション。 |

---

## パッケージ一覧

| パッケージ | 説明 | NuGet |
|---|---|---|
| **PicoDI** | リフレクションゼロの DI コンテナ、コンパイル時ソース生成 | [![NuGet](https://img.shields.io/nuget/v/PicoDI)](https://nuget.org/packages/PicoDI) |
| **PicoDI.Abs** | PicoDI の抽象化（`ISvcContainer`、`ISvcScope`、`SvcDescriptor`） | [![NuGet](https://img.shields.io/nuget/v/PicoDI.Abs)](https://nuget.org/packages/PicoDI.Abs) |
| **PicoDI.Gen** | Roslyn ソースジェネレータ &mdash; コンパイル時の登録と解決 | [![NuGet](https://img.shields.io/nuget/v/PicoDI.Gen)](https://nuget.org/packages/PicoDI.Gen) |
| **PicoCfg** | プロバイダモデル採用の非同期ファースト設定管理 | [![NuGet](https://img.shields.io/nuget/v/PicoCfg)](https://nuget.org/packages/PicoCfg) |
| **PicoCfg.Abs** | PicoCfg の抽象化（`ICfg`、`ICfgRoot`、`ICfgSection`） | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.Abs)](https://nuget.org/packages/PicoCfg.Abs) |
| **PicoCfg.Gen** | 型付き設定バインディング用ソースジェネレータ（`CfgBind.Bind<T>`） | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.Gen)](https://nuget.org/packages/PicoCfg.Gen) |
| **PicoCfg.DI** | PicoCfg の PicoDI 統合（`RegisterCfgRoot`、`RegisterCfgSingleton`） | [![NuGet](https://img.shields.io/nuget/v/PicoCfg.DI)](https://nuget.org/packages/PicoCfg.DI) |
| **PicoLog** | コンパイル時メッセージテンプレートによる構造化ロギング | [![NuGet](https://img.shields.io/nuget/v/PicoLog)](https://nuget.org/packages/PicoLog) |
| **PicoLog.Abs** | PicoLog の抽象化（`ILogger`、`ILogSink`、`LogLevel`） | [![NuGet](https://img.shields.io/nuget/v/PicoLog.Abs)](https://nuget.org/packages/PicoLog.Abs) |
| **PicoLog.Gen** | `[PicoLogMessage]` メソッド用ソースジェネレータ | [![NuGet](https://img.shields.io/nuget/v/PicoLog.Gen)](https://nuget.org/packages/PicoLog.Gen) |
| **PicoLog.DI** | PicoLog の PicoDI 統合（`AddPicoLog`、`WriteTo`、`ReadFrom`） | [![NuGet](https://img.shields.io/nuget/v/PicoLog.DI)](https://nuget.org/packages/PicoLog.DI) |

---

## 比較

### vs Autofac

Autofac はプロパティインジェクション、デコレータ、インターセプタ、モジュールを備えた成熟した高機能 DI コンテナです。PicoDI は正反対のアプローチをとります：**ランタイムリフレクションゼロ、コンパイル時のみの登録、設計段階からの AOT 安全性**。実行時の柔軟性が必要なら Autofac を。**AOT と最小オーバーヘッド** が必要なら PicoDI を。

### vs Lamar

Lamar は実行時コード生成（`DynamicAssembly` + IL emit）を使用する高性能 DI コンテナです。PicoDI は Roslyn ソースジェネレータによる**コンパイル時**のコード生成を採用しています。Lamar はより多くの機能（インターセプション、デコレーション）をサポートしますが、PicoDI は最小構成で AOT ファーストです。

### vs Serilog

Serilog は .NET における構造化ロギングのデファクトスタンダードであり、豊富なシンクエコシステムを備えています。PicoLog は Serilog の代替ではなく、**AOT 互換性と最小依存性**を重視するプロジェクトのための軽量な選択肢です。PicoLog のメッセージテンプレート向けソースジェネレータは、同等の構造化ロギング品質を実現します。

### vs Microsoft.Extensions

PicoHex は `Microsoft.Extensions` の拡張ではなく、**代替**です。Native AOT のためにゼロから設計され、ランタイムリフレクションの代わりにコンパイル時コード生成を採用しています。従来の ASP.NET アプリケーションを構築するなら Microsoft.Extensions をそのまま使い続けてください。**CLI ツール、サーバーレス関数、WASM アプリ、組み込みシステム** を構築するなら、PicoHex はあなたのユースケースのために作られています。

---

## ライセンスとコントリビューション

MIT ライセンス。リポジトリ： [https://github.com/PicoHex/PicoHex](https://github.com/PicoHex/PicoHex)
