using System.Text;

namespace Utils
{
    public class ConsoleLogger : ILogger
    {
        public void Log(string text, ELogStatus status = ELogStatus.Text)
        {
            var logMessage = $"{text} | {DateTime.UtcNow}";

            switch (status)
            {
                // text standard
                case ELogStatus.Text:
                    Console.WriteLine(logMessage);
                    break;

                // Warning yellow color
                case ELogStatus.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(logMessage);
                    break;

                // Error red color
                case ELogStatus.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(logMessage);
                    break;
            }

            Console.ResetColor(); // Reset console color
        }
    }

    public class FileLogger : ILogger
    {
        private readonly object _lock = new();

        private readonly string _logsDirectory = "logs";
        const long MAX_BYTES_FILE_SIZE = 10 * 1024 * 1024L;

        public FileLogger()
        {
            Directory.CreateDirectory(_logsDirectory);
        }

        public void Log(string text, ELogStatus status = ELogStatus.Text)
        {
            var statusText = status switch
            {
                ELogStatus.Text => "INFO",
                ELogStatus.Warning => "WARNING",
                ELogStatus.Error => "ERROR",
                _ => throw new ArgumentException("Unknown log status", nameof(status))
            };

            var fileName = status switch
            {
                ELogStatus.Text => "info.log",
                ELogStatus.Warning => "warnings.log",
                ELogStatus.Error => "errors.log",
                _ => throw new ArgumentException("Unknown log status", nameof(status))
            };

            var logMessage = $"{statusText}: {text} | {DateTime.UtcNow}";

            lock (_lock)
            {
                var filePath = Path.Combine(_logsDirectory, fileName);

                if (File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);  

                    // Checking for file limit
                    if (fileInfo.Length + Encoding.UTF8.GetByteCount(logMessage) > MAX_BYTES_FILE_SIZE)
                    {
                        // Archive file path
                        var rotatedFilePath = Path.Combine(
                            _logsDirectory,
                            $"{fileName}_{DateTime.Now:yyyyMMdd_HHmmssfff}");

                        // Move big file to archive
                        File.Move(filePath, rotatedFilePath);
                    }
                }

                // Write to new file
                File.AppendAllText(
                    filePath,
                    logMessage + Environment.NewLine,
                    Encoding.UTF8);
            }
        }
    }
}
