﻿using PicoHex.Logger.Console;

namespace PicoHex.Logger.NG.Test;

public static class LogFrameworkTests
{
    public class LogEntryTests
    {
        [Fact]
        public void Constructor_InitializesPropertiesCorrectly()
        {
            // Arrange
            var exception = new Exception("Test exception");

            // Act
            var entry = new LogEntry(
                new DateTime(2023, 1, 1),
                LogLevel.Error,
                "Tests",
                "Test message",
                exception
            );

            // Assert
            Assert.Equal(new DateTime(2023, 1, 1), entry.Timestamp);
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.Equal("Tests", entry.Category);
            Assert.Equal("Test message", entry.Message);
            Assert.Same(exception, entry.Exception);
        }
    }

    public class FormatterTests
    {
        [Fact]
        public void SimpleFormatter_FormatsCorrectly()
        {
            // Arrange
            var formatter = new ConsoleFormatter();
            var entry = new LogEntry(
                new DateTime(2023, 1, 1, 12, 0, 0),
                LogLevel.Information,
                "TestCategory",
                "Test Message"
            );

            // Act
            var result = formatter.Format(entry);

            // Assert
            Assert.Equal("2023-01-01 12:00:00 [Information] TestCategory: Test Message", result);
        }

        [Fact]
        public void JsonFormatter_SerializesCorrectly()
        {
            // Arrange
            var formatter = new JsonFormatter();
            var entry = new LogEntry(
                new DateTime(2023, 1, 1, 12, 0, 0),
                LogLevel.Error,
                "TestCategory",
                "Test Message",
                new Exception("Test exception")
            );

            // Act
            var result = formatter.Format(entry);

            // Assert
            Assert.Contains("\"Level\":\"Error\"", result);
            Assert.Contains("\"Message\":\"Test Message\"", result);
            Assert.Contains("\"Exception\":", result);
        }
    }

    public class SinkTests
    {
        [Fact]
        public void ConsoleSink_Emit_WritesToConsole()
        {
            // Arrange
            var formatter = new ConsoleFormatter();
            var sink = new ConsoleSink(formatter);
            var entry = new LogEntry(
                new DateTime(2023, 1, 1, 12, 0, 0),
                LogLevel.Error,
                Message: "Test"
            );

            using var sw = new StringWriter();
            System.Console.SetOut(sw);

            // Act
            sink.Emit(entry);

            // Assert
            Assert.Contains("Test", sw.ToString());
        }

        [Fact]
        public void FileSink_Emit_CreatesFileAndWrites()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            var formatter = new ConsoleFormatter();
            var sink = new FileSink(formatter, tempFile);
            var entry = new LogEntry(
                new DateTime(2023, 1, 1, 12, 0, 0),
                LogLevel.Error,
                Message: "File test"
            );

            try
            {
                // Act
                sink.Emit(entry);
                var content = File.ReadAllText(tempFile);

                // Assert
                Assert.Contains("File test", content);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }

    public class LoggerProviderTests
    {
        [Fact]
        public void CreateLogger_LogsAboveMinimumLevel()
        {
            // Arrange
            var testSink = new TestSink { MinimumLevel = LogLevel.Warning };
            var provider = new LoggerProvider(testSink);
            var logger = provider.CreateLogger("Test");

            // Act
            logger.Log(LogLevel.Information, "Should not log");
            logger.Log(LogLevel.Warning, "Should log");

            // Assert
            Assert.Single(testSink.Entries);
            Assert.Equal("Should log", testSink.Entries[0].Message);
        }

        private class TestSink : ILogSink
        {
            public LogLevel MinimumLevel { get; set; }
            public List<LogEntry> Entries { get; } = new();

            public void Emit(LogEntry entry)
            {
                Entries.Add(entry);
            }

            public ValueTask EmitAsync(
                LogEntry entry,
                CancellationToken cancellationToken = default
            )
            {
                Entries.Add(entry);
                return ValueTask.CompletedTask;
            }

            public void Dispose()
            {
                // TODO release managed resources here
            }

            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }
        }
    }

    public class InternalLoggerTests
    {
        [Fact]
        public void Log_ForwardsToAllLoggers()
        {
            // Arrange
            var logger1 = new TestGenericTypeLogger();
            var logger2 = new TestGenericTypeLogger();
            var composite = new InternalLogger([logger1, logger2]);

            // Act
            composite.Log(LogLevel.Debug, "Test");

            // Assert
            Assert.True(logger1.LogCalled);
            Assert.True(logger2.LogCalled);
        }

        private class TestGenericTypeLogger : ILogger
        {
            public bool LogCalled { get; private set; }

            public void Log(LogLevel level, string message, Exception? exception = null)
            {
                LogCalled = true;
            }

            public ValueTask LogAsync(
                LogLevel level,
                string message,
                Exception? exception = null,
                CancellationToken cancellationToken = default
            )
            {
                LogCalled = true;
                return ValueTask.CompletedTask;
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                throw new NotImplementedException();
            }
        }
    }

    public class LoggerFactoryTests
    {
        [Fact]
        public void CreateLogger_CombinesMultipleProviders()
        {
            // Arrange
            var factory = new LoggerFactory();
            var provider1 = new TestProvider();
            var provider2 = new TestProvider();
            factory.AddProvider(provider1);
            factory.AddProvider(provider2);

            // Act
            var logger = factory.CreateLogger("Test");
            logger.Log(LogLevel.Information, "Test");

            // Assert
            Assert.Equal(2, provider1.LogCount + provider2.LogCount);
        }

        private class TestProvider : ILoggerProvider
        {
            public int LogCount { get; private set; }

            public ILogger CreateLogger(string category)
            {
                return new TestGenericTypeLogger(() => LogCount++);
            }

            public void Dispose() { }

            private class TestGenericTypeLogger(Action logAction) : ILogger
            {
                public void Log(LogLevel level, string message, Exception? exception = null)
                {
                    logAction();
                }

                public ValueTask LogAsync(
                    LogLevel level,
                    string message,
                    Exception? exception = null,
                    CancellationToken cancellationToken = default
                )
                {
                    logAction();
                    return ValueTask.CompletedTask;
                }

                public IDisposable BeginScope<TState>(TState state)
                {
                    throw new NotImplementedException();
                }
            }
        }
    }

    public class GenericTypeLoggerTests
    {
        [Fact]
        public void GenericLogger_UsesTypeNameAsCategory()
        {
            // Arrange
            var factory = new TestLoggerFactory();
            var logger = factory.CreateLogger<GenericTypeLoggerTests>();

            // Assert
            Assert.Equal(typeof(GenericTypeLoggerTests).FullName, factory.LastCategory);
        }

        private class TestLoggerFactory : ILoggerFactory
        {
            public string LastCategory { get; private set; }

            public ILogger CreateLogger(string category)
            {
                LastCategory = category;
                return new NullLogger();
            }

            public ILogger<T> CreateLogger<T>()
            {
                return new GenericTypeLogger<T>(this);
            }

            public void AddProvider(ILoggerProvider provider) { }

            private class NullLogger : ILogger
            {
                public void Log(LogLevel level, string message, Exception? exception = null) { }

                public ValueTask LogAsync(
                    LogLevel level,
                    string message,
                    Exception? exception = null,
                    CancellationToken cancellationToken = default
                )
                {
                    return ValueTask.CompletedTask;
                }

                public IDisposable BeginScope<TState>(TState state)
                {
                    throw new NotImplementedException();
                }
            }
        }
    }
}
