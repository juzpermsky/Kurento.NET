using System;
using Kurento.NET;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace ConsoleApplication1
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            
            //var logger = new ConsoleLogger("KurentoClient",(s, level) => true,false);
             MyLogger logger = new MyLogger(); 
            
              var kurentoClient = new KurentoClient("ws://hive.ru:8888/kurento", logger);
              var serverManager = kurentoClient.GetServerManager();
              var infoAsync = serverManager.GetInfoAsync();
              infoAsync.Wait();
              // var serverInfo = infoAsync.Result;
              // logger.LogDebug(serverInfo.version);

              // while (true)
              // {
              //     
              // }

        }
    }

    public class MyLogger : ILogger
    {
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var message = formatter(state, exception);

            if (!string.IsNullOrEmpty(message) || exception != null)
            {
                Console.WriteLine($"MyLogger: {message}");
            }   
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }
    }
    
}