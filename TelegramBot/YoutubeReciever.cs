using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using TelegramBot;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Exceptions;

namespace YoutubeConnect
{
    public struct VideoInfo : IEnumerable
    {
        public string Title { get; set; }

        public string Channel { get; set; }

        public TimeSpan Duration { get; set; }

        public string Description { get; set; }

        public IEnumerator GetEnumerator()
        {
            yield return Title;
            yield return Channel;
            yield return Duration;
            yield return Description;
        }
    }

    public class YoutubeReciever
    {
        private readonly YoutubeClient _youtube;
        static private readonly HttpClient _httpClient;
        private readonly ConsoleLogger _consoleLogger;

        static YoutubeReciever()
        {
            _httpClient = new HttpClient();
        }

        public YoutubeReciever()
        {
            _youtube = new YoutubeClient();
            _consoleLogger = new ConsoleLogger();
        }

        public async Task<VideoInfo?> GetVideoInfoAsync(string url)
        {
            try
            {
                var video = await _youtube.Videos.GetAsync(url); // URL
                return new VideoInfo
                {
                    Title = video.Title ?? "",
                    Channel = video?.Author?.ChannelTitle ?? "",
                    Duration = video?.Duration ?? TimeSpan.Zero,
                    Description = (video?.Description?.Length > 100) ? video.Description.Substring(0, 100) : (video?.Description ?? ""),
                };
            }
            catch (PlaylistUnavailableException)
            {
                return null;
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentNullException)
            {
                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<MemoryStream?> GetVideoPreviewStreamAsync(string url)
        {
            try
            {
                var video = await _youtube.Videos.GetAsync(url);
                var thumbnail = video.Thumbnails.GetWithHighestResolution();

                // Creating stream thumbnail by url
                using var response = await _httpClient.GetAsync(thumbnail.Url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                var ms = new MemoryStream();
                await response.Content.CopyToAsync(ms);
                ms.Position = 0;
                return ms;
            }
            catch (PlaylistUnavailableException)
            {
                return null;
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentNullException)
            {
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Load muxed video to temporary file
        /// </summary>
        /// <param name="url"></param>
        /// <returns>
        /// Path to loaded video
        /// </returns>
        public async Task<string?> LoadVideoMuxedStreamAsync(string url)
        {
            // Temporary file path for downloading
            var tempPath = Path.GetTempFileName();
            try
            {
                // Get manifest 
                var manifest = await _youtube.Videos.Streams.GetManifestAsync(url);
                // Select high quality
                var streamInfo = manifest.GetMuxedStreams().OrderByDescending(s => s.VideoQuality.MaxHeight).FirstOrDefault();

                if (streamInfo == null) return null;


                // Downloading video
                await _youtube.Videos.Streams.DownloadAsync(streamInfo, tempPath);

                return tempPath;
            }
            catch (Exception ex)
            {
                _consoleLogger.Log(ex.Message, LogStatus.Error);
                File.Delete(tempPath);
                return null;
            }
        }

        public async Task<FileStream?> GetAudioStreamAsync(string url)
        {
            try
            {
                // Get manifest 
                var manifest = await _youtube.Videos.Streams.GetManifestAsync(url);
                // Select high quality
                var audioStreamInfo = manifest.GetAudioStreams().OrderByDescending(s => s.Bitrate).FirstOrDefault();

                if (audioStreamInfo == null) return null;

                // Temporary file for downloading initialization
                var tempPath = Path.GetTempFileName();

                // Downloading video
                await _youtube.Videos.Streams.DownloadAsync(audioStreamInfo, tempPath);

                // Open file stream
                var fileStream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                fileStream.Position = 0;

                return fileStream;
            }
            catch (Exception ex)
            {
                _consoleLogger.Log(ex.Message, LogStatus.Error);
                return null;
            }
        }
    }
}
