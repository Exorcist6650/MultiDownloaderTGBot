using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace TelegramBot
{
    public class DownloadManager
    {
        // Dependencies
        private readonly ConsoleLogger _consoleLogger;

        // Fields
        private readonly string ytdlpPath = Path.Combine(Directory.GetCurrentDirectory(), "tools", "yt-dlp.exe");

        public DownloadManager()
        {
            // Dependencies
            _consoleLogger = new ConsoleLogger();

            if (File.Exists(ytdlpPath))
                _consoleLogger.Log("yt-dlp was found successfully");
            else
                _consoleLogger.Log("yt-dlp does not exist in directory", LogStatus.Error);
        }

        public async Task<int> DownloadVideoAsync(string url)
        {
            string outputTemplate = Path.Combine(Path.GetTempPath(), "%(title)s.%(ext)s");

            // Empty url
            if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("URL required", nameof(url));

            string args = $"-f b -o\"{outputTemplate}\" --newline \"{url}\"";

            var processStartInfo = new ProcessStartInfo
            {
                FileName = ytdlpPath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            using var process = new Process() { StartInfo = processStartInfo, EnableRaisingEvents = true };

            // Events for logging 
            process.OutputDataReceived += (sender, e) => 
            { if (e.Data != null) _consoleLogger.Log(e.Data); };

            process.ErrorDataReceived += (sender, e) => 
            { if (e.Data != null) _consoleLogger.Log(e.Data, LogStatus.Error); };

            // Running
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();
            
            return 0;
        }
    };
}
