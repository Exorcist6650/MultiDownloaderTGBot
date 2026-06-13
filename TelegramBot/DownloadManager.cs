using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace TelegramBot
{
    public enum DownloadType : byte
    {
        Preview,
        Thumbnail,
        VideoBest,
        VideoMerged,
        Audio,
    }


    public class DownloadManager
    {
        // Dependencies
        private readonly ConsoleLogger _consoleLogger;

        // Fields
        private readonly string ytdlpPath = Path.Combine(Directory.GetCurrentDirectory(), "tools", "yt-dlp.exe");
        private bool isReadyToUse = false;

        private const string STANDARD_IMAGE_FORMAT = "jpg";
        private const string STANDARD_VIDEO_FORMAT = "mp4";
        private const string STANDARD_AUDIO_FORMAT = "mp3";
        public DownloadManager()
        {
            // Dependencies
            _consoleLogger = new ConsoleLogger();
        }

        public async Task<int> Init()
        {
            // Checking yt-dlp existing
            if (File.Exists(ytdlpPath))
            {
                _consoleLogger.Log("yt-dlp was found successfully");
                await FFmpegDownload();
                isReadyToUse = true;
                return 0;
            }
            else
                _consoleLogger.Log("yt-dlp does not exist in directory", LogStatus.Error);

            return 1;
        }

        private async Task FFmpegDownload()
        {
            _consoleLogger.Log("DownloadManager init starting");

            // Download ffmpeg to chache
            await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official);

            _consoleLogger.Log("DownloadManager init complete");
        }

        public async Task<(string filePath, string fileTitle)?> DownloadFileAsync(string url, DownloadType downloadType)
        {
            // If manager is not init
            if (!isReadyToUse) throw new InvalidOperationException("Download manager must be init before using");

            string outputTemplate = Path.Combine(
                Path.GetTempPath(),
                $"{Path.GetFileNameWithoutExtension(Path.GetRandomFileName())}.%(ext)s"
            );

            // Empty url
            if (string.IsNullOrWhiteSpace(url)) return null;

            // Arguments for downloading
            string args = string.Empty;

            switch (downloadType)
            {
                case DownloadType.Thumbnail:
                case DownloadType.Preview:
                    args = $"--no-playlist --newline --print-json --no-warnings --skip-download " +
                        $"--write-thumbnail --convert-thumbnails {STANDARD_IMAGE_FORMAT} -o\"{outputTemplate}\" \"{url}\"";
                    break;
                case DownloadType.VideoBest:
                    args = $"--no-playlist --newline --print-json --no-warnings --merge-output-format {STANDARD_VIDEO_FORMAT} " +
                        $"-f \"bestvideo+bestaudio/best\" -o\"{outputTemplate}\" \"{url}\"";
                    break;
                case DownloadType.VideoMerged:
                    args = $"--no-playlist --newline --print-json --no-warnings -f b -o\"{outputTemplate}\" \"{url}\"";
                    break;
                case DownloadType.Audio:
                    args = $"--no-playlist --newline --print-json --no-warnings " +
                        $"--extract-audio --audio-format {STANDARD_AUDIO_FORMAT} -f bestaudio,best -o\"{outputTemplate}\" \"{url}\"";
                    break;
            }

            // Download run
            var processResults = await RunProcessDownloadingAsync(args);
            if (processResults.ExitCode == 0) // Checking success loading
            {
                // Parsing first JSON string from output
                var firstJsonString = processResults.Stdout.Split("\n", StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (string.IsNullOrEmpty(firstJsonString)) return null; // Null data

                // Parsing string to JSON file
                using var document = JsonDocument.Parse(firstJsonString);
                var root = document.RootElement;


                // Searching file path
                if (root.TryGetProperty("filename", out var filePath) && filePath.ValueKind == JsonValueKind.String)
                {
                    // File path
                    var downloadedFilePath = filePath.ToString();

                    // Change ext to jpg if image
                    if (downloadType == DownloadType.Thumbnail || downloadType == DownloadType.Preview)
                            downloadedFilePath = Path.ChangeExtension(downloadedFilePath, STANDARD_IMAGE_FORMAT);

                    // Change ext to mp4 if best video
                    if (downloadType == DownloadType.VideoBest)
                        downloadedFilePath = Path.ChangeExtension(downloadedFilePath, STANDARD_VIDEO_FORMAT);

                    // Change ext to mp3 if audio
                    if (downloadType == DownloadType.Audio)
                        downloadedFilePath = Path.ChangeExtension(downloadedFilePath, STANDARD_AUDIO_FORMAT);


                    // Searching file title
                    if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                    {
                        return (downloadedFilePath, title.ToString()); // Return a path to downloaded file with video title
                    }
                }
                return null;
            }
            else
                _consoleLogger.Log(processResults.Stderr, LogStatus.Error);

            return null;
        }

        /// <summary>
        /// Run yt-dlp process with arguments
        /// </summary>
        /// <param name="args"></param>
        /// <returns>
        /// Exit code, standard outputs, standard errors
        /// </returns>
        private async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessDownloadingAsync(string args)
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

            // Running
            using var process = new Process() { StartInfo = processStartInfo, EnableRaisingEvents = true };
            process.Start();

            // Reading outputs async
            var outTask = process.StandardOutput.ReadToEndAsync();
            var errTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            return (process.ExitCode, await outTask, await errTask);
        }
    };
}
