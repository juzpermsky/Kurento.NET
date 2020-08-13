using System;
using Microsoft.Extensions.Logging;

namespace ConsoleApp1 {
	public class MyLogger : ILogger {
		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) {
			var message = formatter(state, exception);

			if (!string.IsNullOrEmpty(message) || exception != null) {
				Console.WriteLine($"MyLogger: {message}");
			}
		}

		public bool IsEnabled(LogLevel logLevel) {
			return true;
		}

		public IDisposable BeginScope<TState>(TState state) {
			return null;
		}
	}
}