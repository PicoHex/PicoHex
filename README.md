# PicoHex

---

A fully asynchronous edge computing MVP (Minimum Viable Product) built on AOT (Ahead-of-Time) compilation. Implements core functionality **without dependencies** on external libraries for DI containers, logging frameworks, configuration systems, web servers, or similar components.

## Pico.DI

AOT-compatible IoC container supporting **Transient/Scoped/Singleton** lifetimes with circular dependency detection. Future implementation will utilize a source generator.

## Pico.Cfg

Stream-based configuration framework.

## Pico.Logger

Logging framework with **console and file sinks**.
