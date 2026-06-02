# PicoHex Code Review

**Date:** 2026-06-01  
**Reviewed:** 2026-06-02  
**Scope:** 全仓库 5 模块 18 包  
**Conclusion:** 约 60% 真实缺陷，25% 设计权衡（文档不足），15% 误判  
**Fixes applied:** PICO014/PICO015 发射，GeneratedDispatch.Switch 列表式注册

---

## Severity Key

| Level | Meaning |
|:---:|------|
| 🔴 Critical | 生产环境会崩溃/泄漏/数据丢失 |
| 🟡 Medium | 功能缺口、诊断缺失、语义不完整 |
| 🟢 Low | 文档、代码异味、未来优化 |

---

## PicoDI

| # | Severity | 发现 | 判定 |
|:---:|:---:|------|------|
| 1 | 🟡 | Singleton 创建-销毁竞态（use-after-free） | 已文档化，设计权衡 |
| 2 | 🟡 | sync-over-async 销毁 `.GetAwaiter().GetResult()` | 已文档化，AOT 无 SynchronizationContext |
| 3 | 🟡 | 孤儿 Scope 异常吞入 `Trace.WriteLine` | best-effort 清理路径 |
| 4 | 🟡 | `ISvcScope` 接口缺 `TryGetService/TryGetServices` | 设计选择，接口最小但 README 有——不一致 |
| 5 | 🟡 | 生命周期不匹配无编译期/运行时检查 | 真实缺口 |
| 6 | 🟢 | `OnError` 回调缺上下文（服务类型/生命周期） | 增强诊断 |
| 7 | 🟢 | 托管服务 one-shot 未强制 | 静默 no-op |
| 8 | 🟢 | 代码引用未记录的 "Bug 8/Bug 9" | 维护障碍 |
| 9 | 🟢 | `ClearForTesting()` 全局可变状态 | `Clear()` 内部有 lock 是线程安全的；测试并行时全局状态互相干扰是架构问题 |

## PicoCfg

| # | Severity | 发现 | 判定 |
|:---:|:---:|------|------|
| 10 | 🟡 | `CfgChangeSignal` 每次分配 TCS | GC 压力 |
| 11 | 🟡 | `Lock + Volatile` 混用在 `CfgRoot` | 意图清晰但 review 成本高 |
| 12 | 🟡 | 重载时指纹重算 | 性能优化点 |
| 13 | 🟡 | 文件监听错误只进 Trace，无公共错误钩子 | 运维盲区 |
| 14 | 🟡 | `key=value` 格式错误静默跳过 | 缺少诊断 |
| 15 | 🟢 | 处置超时 10 秒硬编码 | 配置化 |
| 16 | 🟢 | `ChainedCfgProvider` 不传播变更通知 | 已文档化 |
| 17 | 🟢 | `CompositeCfgSnapshot.GetAllValues()` 静默跳过自定义快照 | 行为不透明 |

## PicoLog

| # | Severity | 发现 | 判定 |
|:---:|:---:|------|------|
| 18 | 🟡 | `SeqSink` fire-and-forget + 锁内异步 → 异常丢失 | 缓冲 drain 在锁内同步完成，无竞态；fire-and-forget 导致未观察异常 |
| 19 | 🟡 | `SeqSink` 缺少定期 Flush → Seq 宕机时内存泄漏 | 缓冲区无定期刷新，低流量下堆积至 Dispose |
| 20 | 🟡 | `LogEntryPool` 回收 TOCTOU 竞态 | 256 上限下影响极小 |
| 21 | 🟡 | `ThreadStatic` `StringBuilder` 过 `await` 失效 | 异步场景复用失败 |
| 22 | 🟡 | `FileSink` 无轮转/大小/保留策略 | 设计选择——最小 Sink |
| 23 | 🟡 | 各 Sink 异常契约不一致 | 调用方不可预测 |
| 24 | 🟢 | `SeqSink` 无超时/重试配置 | 硬编码 |
| 25 | 🟢 | `PicoLogMessageGenerator` 不验证 EventId/Message | 编译期不报错 |

## PicoAop

| # | Severity | 发现 | 判定 |
|:---:|:---:|------|------|
| 26 | 🔴 | PICO014/PICO015 定义但从未发射 | **已修复——添加 Diagnostic.Create 发射逻辑** |
| 27 | 🟡 | 所有诊断用 `Location.None` | IDE 导航失效 |
| 28 | 🟡 | `IInvocation<TResult>` 接口不暴露方法参数 | **误判**——生成的 struct 有参数，可强转访问 |
| 29 | 🟡 | init-only 属性静默 no-op | 文档不足 |
| 30 | 🟡 | ref/out/in 无警告代理 | **已修复——PICO016** |
| 31 | 🟢 | README 矛盾（泛型方法标记"不支持"但已实现） | 文档过时 |

## PicoMediator

| # | Severity | 发现 | 判定 |
|:---:|:---:|------|------|
| 32 | 🔴 | `GeneratedDispatch.Switch` 多 ModuleInitializer 同时赋值覆盖 | **已修复——列表式注册 + RegisterSwitch** |
| 33 | 🟡 | 生成代码 `handler.Handle()` 无 null 检查 → NRE | **已修复——添加防御性 null 检查** |
| 34 | 🟢 | `Publish` 只 catch `PicoDiException` → 其他异常传播 | 有意设计——仅 silence 未注册场景 |
| 35 | ❌ 误判 | `PublishParallel` Exception?[] 读回无内存屏障 | `await Task.WhenAll` 提供完整 happens-before 保证 |
| 36 | 🟢 | PublishParallel 零 Handler 仍分配数组 | 微小浪费 |
| 37 | 🟢 | README 过时（"AggregateException (future)" 已实现） | 文档过时 |

## 跨模块主题

| # | Severity | 主题 |
|:---:|:---:|------|
| 38 | 🟡 | 全模块静默错误吞入 Trace/Debug（无统一观察钩子） |
| 39 | 🟡 | PicoLog Seq / PicoCfg 文件监听——无界缓冲/无背压 |
| 40 | 🟡 | PicoDI 孤儿路径——sync-over-async 销毁 |
| 41 | 🟡 | PicoAop + PicoDI + PicoMediator 生成器诊断未发射 |
| 42 | 🟡 | 全模块测试缺并发/竞态/压力覆盖 |
| 43 | 🟢 | 全模块 README 与代码不完全同步 |

---

## 误判说明

| 被发现 | 实际 | 审核结论 |
|------|------|------|
| "拦截器不能读写方法参数" | `IInvocation<TResult>` 接口确实不暴露参数，且**生成的 struct 字段为 `private readonly`**，外部拦截器无法通过强转访问。原发现正确，判定"误判"本身有误 | 文档误判错误 → 保留原发现 🟡 |
| "无开放泛型 Handler 支持" | 生成器**设计上**跳过泛型 Handler 实现，由 PicoDI 运行时处理。不是"不支持"，是"生成器不参与" |
| "无请求上下文/correlation-id" | 不是框架职责——用户自建。ZeroMQ 的 REQ/REP 协议本身不含上下文传递 |

---

## 修复记录 (2026-06-02)

| # | 修复内容 | 涉及文件 |
|:---:|------|------|
| 26 | PICO014 `AmbiguousInterceptBy` 添加发射逻辑 | `InterceptorGenerator.Syntax.cs`, `InterceptorGenerator.cs` |
| 26 | PICO015 `NameCollision` 添加发射逻辑 | `InterceptorGenerator.cs` |
| 30 | PICO016 `RefLikeMethodDelegated` 新增诊断 | `InterceptorDiagParams.cs`, `InterceptorGenerator.cs` |
| 32 | `GeneratedDispatch.Switch` 改为列表式 `RegisterSwitch` | `GeneratedDispatch.cs`, `MediatorGenerator.cs` |
| 33 | 生成代码添加防御性 null 检查 | `MediatorGenerator.cs` |
| 18-19 | `SeqSink` 添加定期 Flush 定时器 | `SeqSink.cs` |
