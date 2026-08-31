using System.Diagnostics;
using System.Runtime.InteropServices;
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
        private readonly string _toolsDirectory = Path.Combine(AppContext.BaseDirectory, "tools");
        private string _ytdlpPath;

        private readonly ILogger _logger = logger;

        private bool _isInit = false;

        // Formats
        private const string STANDARD_IMAGE_FORMAT = "jpg";
        private const string STANDARD_VIDEO_FORMAT = "mp4";
        private const string STANDARD_AUDIO_FORMAT = "mp3";

        public async Task Init()
        {
            // Path to YT-DLP
            var ytdlpVersion = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "yt-dlp.exe"
                : "yt-dlp";

            _ytdlpPath = Path.Combine(_toolsDirectory, ytdlpVersion);

            // Checking YT-DLP existing
            if (File.Exists(_ytdlpPath))
            {
                Console.WriteLine("Donwloading ffmpeg..."); 
                await FFmpegDownload(); // Downloaded ffmpeg | ffprobe for yt-dlp
                _isInit = true;
            }
            else
                throw new FileNotFoundException("yt-dlp file not found", _ytdlpPath);
        }

        // Download file to temp and return info
        public async Task<(string FilePath, string FileTitle)?> DownloadToTempAsync
            (string url, EDownloadType downloadType)
        {
            if (!_isInit) throw new InvalidOperationException("Download manager isn't init");
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
        public InputFileStream? GetInputFile((string FilePath, string FileTitle) fileInfo)
        {
            if (!_isInit) throw new InvalidOperationException("Download manager isn't init");
            if (!File.Exists(fileInfo.FilePath)) return null;

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
            // Download ffmpeg
            await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, _toolsDirectory);


        // Run YT-DLP
        private async Task<(int ExitCode, string Stdout, string Stderr)>
            RunProcessDownloadingAsync(string args)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = _ytdlpPath,
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
        private static (string FilePath, string FileTitle)?
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
                // Path to temp file
                var loadPath = Path.ChangeExtension(filePath.ToString(), downloadType switch
                {
                    EDownloadType.Thumbnail =>
                        STANDARD_IMAGE_FORMAT,

                    EDownloadType.Video =>
                        STANDARD_VIDEO_FORMAT,

                    EDownloadType.Audio =>
                        STANDARD_AUDIO_FORMAT,

                    _ => throw new ArgumentException("Unknown download type", nameof(downloadType))
                });

                // Searching file title
                if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                {
                    return (loadPath, title.ToString()); // Path to downloaded file with video title
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

            string ffmpegArgs = $"--ffmpeg-location \"{_toolsDirectory}\"";

            const string commonArgs = "--no-playlist --newline --print-json --no-warnings";


            string args = downloadType switch
            {
                EDownloadType.Thumbnail =>
                    $"{commonArgs} " +
                    $"--skip-download --write-thumbnail " +
                    $"--convert-thumbnails {STANDARD_IMAGE_FORMAT} " +
                    $"-o \"{outputTemplate}\" \"{url}\"",

                EDownloadType.Video =>
                    $"{commonArgs} {ffmpegArgs} " +
                    $"--merge-output-format mp4 " +
                    $"--recode-video mp4 " +
                    $"-f \"bv*+ba/b\" " +
                    $"-o \"{outputTemplate}\" \"{url}\"",

                EDownloadType.Audio =>
                    $"{commonArgs} {ffmpegArgs} " +
                    $"--extract-audio " +
                    $"--audio-format {STANDARD_AUDIO_FORMAT} " +
                    $"-f \"bestaudio/best\" " +
                    $"-o \"{outputTemplate}\" \"{url}\"",

                _ => throw new ArgumentException(
                    "Unknown download type",
                    nameof(downloadType))
            };


            return args;
        }
    };
}
