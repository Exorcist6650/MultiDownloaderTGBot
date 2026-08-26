using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Telegram.Bot.Types;
using Utils;
using Xabe.FFmpeg.Downloader;

namespace Managers
{

    public class DownloadManager(ILogger logger)
    {
        // Fields
        private readonly string ytdlpPath = Path.Combine(Directory.GetCurrentDirectory(), "tools", "yt-dlp.exe");

        private readonly ILogger _logger = logger;

        private bool IsInit = false;

        // Formats
        private const string STANDARD_IMAGE_FORMAT = "jpg";
        private const string STANDARD_VIDEO_FORMAT = "mp4";
        private const string STANDARD_AUDIO_FORMAT = "mp3";

        public async Task Init()
        {
            // Checking YT-DLP existing
            if (File.Exists(ytdlpPath))
            {
                _logger.Log("Donwloading ffmpeg..."); // Log
                await FFmpegDownload(); // Downloaded ffmpeg | ffprobe for yt-dlp
                IsInit = true;
            }
            else
                throw new FileNotFoundException("yt-dlp file not found", ytdlpPath);
        }

        // Download file to temp and return info
        public async Task<(string filePath, string fileTitle)?> DownloadToTempAsync
            (string url, EDownloadType downloadType)
        {
            if (!IsInit) throw new InvalidOperationException("Download manager isn't init");
            if (string.IsNullOrWhiteSpace(url)) return null; // Empty url

            // Args for download
            var processArgs = BuildDownloadArgs(url, downloadType);

            // Execute YT-DLP and collect results  
            var (exitCode, stdout, stderr) = await RunProcessDownloadingAsync(processArgs);

            if (exitCode == 0) // Checking success loading
            {
                // Return file info 
                return GetFileInfoFromOutput(stdout, downloadType);
            }
            else
                _logger.Log(stderr, ELogStatus.Error); // Logging error

            return null;
        }

        // Get input file by file path
        public InputFileStream? GetInputFile((string filePath, string title) fileInfo)
        {
            // Variables from tuple
            var (path, title) = fileInfo;

            // Open file stream
            var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

            // Input file
            return InputFile.FromStream(fileStream, title);
        }

        // PRIVATE

        // Download FFmpeg
        private async Task FFmpegDownload() =>
            // Download ffmpeg to chache
            await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official);


        // Run YT-DLP
        private async Task<(int ExitCode, string Stdout, string Stderr)>
            RunProcessDownloadingAsync(string args)
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


        // Return a file info from YT-DLP output
        private (string filePath, string fileTitle)?
            GetFileInfoFromOutput(string output, EDownloadType downloadType)
        {
            // Parsing JSON string from output
            var jsonString = output.Split("\n", StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrEmpty(jsonString)) return null; // Null data

            // Parsing string to JSON file
            using var document = JsonDocument.Parse(jsonString);
            var root = document.RootElement;

            // Searching file path
            if (root.TryGetProperty("filename", out var filePath) && filePath.ValueKind == JsonValueKind.String)
            {
                // Filepath with .temp format
                var downloadedFilePath = filePath.ToString();

                switch (downloadType)
                {
                    case EDownloadType.Thumbnail:
                        downloadedFilePath = Path.ChangeExtension(downloadedFilePath, STANDARD_IMAGE_FORMAT);
                        break;
                    case EDownloadType.VideoBest:
                        downloadedFilePath = Path.ChangeExtension(downloadedFilePath, STANDARD_VIDEO_FORMAT);
                        break;
                    case EDownloadType.VideoMerged:
                        downloadedFilePath = Path.ChangeExtension(downloadedFilePath, STANDARD_VIDEO_FORMAT);
                        break;
                    case EDownloadType.Audio:
                        downloadedFilePath = Path.ChangeExtension(downloadedFilePath, STANDARD_AUDIO_FORMAT);
                        break;
                }

                // Searching file title
                if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                {
                    return (downloadedFilePath, title.ToString()); // Path to downloaded file with video title
                }
            }

            return null;
        }


        // Build download args from url
        private string BuildDownloadArgs(string url, EDownloadType downloadType)
        {
            // Arguments for downloading
            string outputTemplate = Path.Combine(
                Path.GetTempPath(),
                $"{Path.GetFileNameWithoutExtension(Path.GetRandomFileName())}.%(ext)s");

            const string commonArgs = "--no-playlist --newline --print-json --no-warnings";

            string args = downloadType switch
            {
                EDownloadType.Thumbnail =>
                    $"{commonArgs} --skip-download --write-thumbnail --convert-thumbnails " +
                    $"{STANDARD_IMAGE_FORMAT} -o\"{outputTemplate}\" \"{url}\"",

                EDownloadType.VideoBest =>
                    $"{commonArgs} --merge-output-format {STANDARD_VIDEO_FORMAT} " +
                    $"-f \"bestvideo+bestaudio/best\" -o\"{outputTemplate}\" \"{url}\"",

                EDownloadType.VideoMerged =>
                    $"{commonArgs} -f b -o\"{outputTemplate}\" \"{url}\"",

                EDownloadType.Audio =>
                    $"{commonArgs} --extract-audio --audio-format {STANDARD_AUDIO_FORMAT} " +
                    $"-f bestaudio,best -o\"{outputTemplate}\" \"{url}\"",

                _ => throw new ArgumentException("Unknown download type", nameof(downloadType))
            };

            return args;
        }
    };
}
