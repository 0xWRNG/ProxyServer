using System;
using System.Collections.Generic;
using System.Text;

namespace ProxyServer.Monitoring
{
    using System;
    using System.IO;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    public enum LogLevels
    {
        Connection,
        Protocol,
        Transport,
    }
    public class Logger : IDisposable
    {
        private readonly Channel<(LogLevels Level, string Message)> _channel;
        private readonly Task _processorTask;
        private readonly string _logDirectory;

        public Logger(string logDirectory)
        {
            _logDirectory = logDirectory;
            Directory.CreateDirectory(_logDirectory);

            _channel = Channel.CreateUnbounded<(LogLevels, string)>();
            _processorTask = Task.Run(ProcessMessagesAsync);
        }
        private string GetIndent(LogLevels level)
        {
            return level switch
            {
                LogLevels.Connection => "",
                LogLevels.Protocol => " ",
                LogLevels.Transport => "  ",
                _ => ""
            };
        }

        public void Log(LogLevels level, string format, params object[] args)
        {
            var message = string.Format(format, args);
            _channel.Writer.TryWrite((level, message));
        }
        public void Log(string format, params object[] args) => Log(LogLevels.Connection, format, args);

        private async Task ProcessMessagesAsync()
        {
            var reader = _channel.Reader;
            await foreach (var (level, message) in reader.ReadAllAsync())
            {
                try
                {
                    var indent = GetIndent(level);
                    var timestamp = DateTime.Now;
                    var fullMessage = $"{timestamp:HH:mm:ss} │ {indent}{message}";
                    Console.WriteLine(fullMessage);
                    await WriteToFileAsync(fullMessage, timestamp);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LOG] Error: {ex.Message}");
                }
            }
        }

        private async Task WriteToFileAsync(string fullMessage, DateTime timestamp)
        {
            var fileName = $"{timestamp:dd_MM_yyyy}.txt";
            var filePath = Path.Combine(_logDirectory, fileName);
            await File.AppendAllTextAsync(filePath, fullMessage + Environment.NewLine);
        }

        public void Dispose()
        {
            _channel.Writer.Complete();
            try
            {
                _processorTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException) { }
        }
    }
}
