using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
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

        static YoutubeReciever()
        {
            _httpClient = new HttpClient();
        }

        public YoutubeReciever()
        {
            _youtube = new YoutubeClient();
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
                    Duration = video?.Duration.Value ?? TimeSpan.Zero,
                    Description = video?.Description.Length > 100 ? video.Description.Substring(0, 100) : video.Description ?? "",
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
    }
}
