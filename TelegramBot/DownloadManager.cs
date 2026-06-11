using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace TelegramBot
{
    public enum DownloadType : byte
    {
        Video,
        Audio,
    }


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

            // Checking yt-dlp existing
            if (File.Exists(ytdlpPath))
            {
                _consoleLogger.Log("yt-dlp was found successfully");
                FFmpegDownload(Path.Combine(Directory.GetCurrentDirectory(), "tools"));
            }
            else
                _consoleLogger.Log("yt-dlp does not exist in directory", LogStatus.Error);
        }

        private async void FFmpegDownload(string targetPath)
        {
            // Download ffmpeg to chache
            await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official);

            //// Chache directory path
            //string chacheDirectory =
            //    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Xabe.FFmpeg");
            //if (!Directory.Exists(chacheDirectory)) throw new DirectoryNotFoundException(chacheDirectory);


            //// Get bin files in chache
            //var files = Directory.EnumerateFiles(chacheDirectory, "*ffmpeg*", SearchOption.AllDirectories)
            //    .Concat(Directory.EnumerateFiles(chacheDirectory, "*ffprobe*", SearchOption.AllDirectories))
            //    .Distinct();

            //// Recursive copy to target folder
            //foreach (var src in files)
            //{
            //    string destinationPath = Path.Combine(targetPath, Path.GetFileName(src));
            //    File.Copy(destinationPath, targetPath, overwrite: true);

            //}
        }

        public async Task<string> DownloadFileAsync(string url, DownloadType downloadType)
        {
            string outputTemplate = Path.Combine(Path.GetTempPath(), "%(title)s.%(ext)s");

            // Empty url
            if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("URL required", nameof(url));

            // Arguments for downloading
            string args = string.Empty;

            switch (downloadType)
            {
                case DownloadType.Video:
                    args = $"--no-playlist --newline -f b -o\"{outputTemplate}\" \"{url}\"";
                    break;
                case DownloadType.Audio:
                    args = $"--no-playlist --newline --extract-audio --audio-format mp3 -f bestaudio,best -o\"{outputTemplate}\" \"{url}\"";
                    break;
            }

            // Download run
            await RunProcessDownloadingAsync(args);

            // Return a reference to dowmloaded file
            return outputTemplate;
        }

        private async Task<int> RunProcessDownloadingAsync(string args)
        {
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

            process.Exited += (sender, e) =>
            { _consoleLogger.Log($"{process.ExitCode}"); process.Dispose(); };

            // Running
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            return process.ExitCode;
        }
    };
}
