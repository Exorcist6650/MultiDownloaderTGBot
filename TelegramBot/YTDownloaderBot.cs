using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using AngleSharp.Dom;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using YoutubeConnect;
using YoutubeExplode.Videos.Streams;

namespace TelegramBot
{
    public class YTDownloaderBot
    {
        // Dependencies
        private readonly Host _host;
        private readonly YoutubeReciever _ytReciever;
        private readonly ConsoleLogger _consoleLogger;
        private readonly TelegramLogger _telegramLogger;

        // Fields
        private readonly InlineKeyboardMarkup _inlineKeyboard;

        public YTDownloaderBot(Host host, YoutubeReciever ytReciever, ConsoleLogger consoleLogger, TelegramLogger telegramLogger)
        {
            // Binding
            _host = host;
            _ytReciever = ytReciever;
            _consoleLogger = consoleLogger;
            _telegramLogger = telegramLogger;

            // Events
            _host.OnMessage += OnUserSendMessage;
            _host.OnCallback += OnUserClickButton;

            // Fields init
            _inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Download video", "action:video"),
                    InlineKeyboardButton.WithCallbackData("Download audio", "action:audio"),
                },
                new []
                {
                    InlineKeyboardButton.WithCallbackData("Cancel", "action:cancel"),

                },
            });
        }


        public void Init()
        {
            _host.Start();
        }

        // Delegates
        private async void OnUserSendMessage(ITelegramBotClient client, Update update)
        {
            var chatId = update?.Message?.Chat.Id ?? 0;
            string userMessage = update?.Message?.Text ?? string.Empty;

            if (userMessage == "/start")
            {
                await _telegramLogger.Log("Send youtube video link", client, chatId);
                return;
            }

            // Loading and sending info message with buttons
            await LoadAndSendPreviewInfoAsync(client, chatId, userMessage);
        }

        private async void OnUserClickButton(ITelegramBotClient client, CallbackQuery cb)
        {
            var chatId = cb?.Message?.Chat.Id ?? 0;
            var caption = cb?.Message?.Caption ?? string.Empty;

            // Parcing url 
            string key = "\nLINK: ";
            var prefix = caption.IndexOf(key);
            if (prefix != -1)
            {
                var videoUrl = caption.Substring(prefix + key.Length);

                switch (cb?.Data)
                {
                    // User download video
                    case "action:video":
                        // Loading message for user
                        var LoadingVideoMessage = await _telegramLogger.Log("Video has started download...", client, chatId);

                        // Loading and sending video
                        await LoadAndSendMuxedVideoAsync(client, chatId, videoUrl);

                        // Deleting message for user
                        await client.DeleteMessage(chatId, LoadingVideoMessage.Id);
                        break;


                    // User download audio
                    case "action:audio":
                        // Loading message for user
                        var LoadingAudioMessage = await _telegramLogger.Log("Video has started download...", client, chatId);

                        // Loading and sending audio
                        await LoadAndSendingAudioAsync(client, chatId, videoUrl);

                        // Deleting message for user
                        await client.DeleteMessage(chatId, LoadingAudioMessage.Id);
                        break;


                    case "action:cancel":
                        // Deleting info message
                        await client.DeleteMessage(chatId, cb?.Message?.Id ?? 0);
                        break;
                }
            }
        }

        // Functions
        private async Task<bool> LoadAndSendPreviewInfoAsync(ITelegramBotClient client, ChatId chatId, string url)
        {
            // Get async video
            VideoInfo? videoInfo = await _ytReciever.GetVideoInfoAsync(url);
            if (videoInfo != null)
            {
                // Preview image stream
                using var memoryStream = await _ytReciever.GetVideoPreviewStreamAsync(url);
                if (memoryStream != null)
                {
                    // Key with link to download video
                    string LinkToVideo = $"\nLINK: {url}";

                    // Text сaption
                    string textCaption =
                        $"{videoInfo?.Title}" +
                        $"\nAuthor: {videoInfo?.Channel}" +
                        $"\nVideo duration: {videoInfo?.Duration}\n\n" +
                        videoInfo?.Description +
                        "..." +
                        $"\n{LinkToVideo}";

                    // Sending to chat
                    try
                    {
                        // Telegram message with buttons
                        await client.SendPhoto(chatId, InputFile.FromStream(memoryStream), textCaption, replyMarkup: _inlineKeyboard);
                        _consoleLogger.Log("Preview sending sucсessfully");
                    }
                    catch (Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.ErrorCode == 403)
                    {
                        _consoleLogger.Log($"Exceprtion: {ex.Message}", LogStatus.Error);
                    }
                    catch (Exception ex)
                    {
                        _consoleLogger.Log($"Exceprtion: {ex.Message}", LogStatus.Error);
                    }

                    return true;
                }
                else
                {
                    await _telegramLogger.Log("Preview download failed", client, chatId, LogStatus.Error);
                    _consoleLogger.Log("Preview stream failed", LogStatus.Error);
                    return false;
                }
            }
            else
            {
                await _telegramLogger.Log("Cannot download this. Please, send a link to youtube video", client, chatId);
                return false;
            }
        }

        private async Task LoadAndSendMuxedVideoAsync(ITelegramBotClient client, ChatId chatId, string url)
        {
            // Load video
            var videoPath = await _ytReciever.LoadVideoMuxedStreamAsync(url);

            if (videoPath != null)
            {
                // Open file stream
                using var fileStream = new FileStream(videoPath, FileMode.Open, FileAccess.Read, FileShare.Read);

                if (fileStream != null)
                {
                    if (fileStream.Length / 1024 <= 49_500)
                    {

                        var inputFile = InputFile.FromStream(fileStream, "Video");
                        if (inputFile != null)
                        {
                            try
                            {
                                // Sending video to chat
                                await client.SendVideo(
                                    chatId,
                                    new InputFileStream(fileStream, "Video"),
                                    caption: $"@{_host.Me.Username}",
                                    supportsStreaming: true
                                );

                                _consoleLogger.Log("Video send sucсessfully");
                            }
                            catch (Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.ErrorCode == 403)
                            {
                                _consoleLogger.Log($"Exceprtion: {ex.Message}", LogStatus.Error);
                            }
                            catch (Exception ex)
                            {
                                _telegramLogger?.Log(ex.Message, client, chatId, LogStatus.Error);
                                _consoleLogger.Log($"Exceprtion: {ex.Message}", LogStatus.Error);
                            }
                        }
                        else
                            _consoleLogger.Log($"Video convertation from stream failed", LogStatus.Error);
                    }
                    else
                        _telegramLogger?.Log("File must be less than 50mb", client, chatId);
                }
                else
                    _consoleLogger.Log($"Video stream is null", LogStatus.Error);

            }
            else
                _consoleLogger.Log($"Video path is null", LogStatus.Error);

            // Deleting video file
            if (File.Exists(videoPath))
                File.Delete(videoPath);
        }
        private async Task<bool> LoadAndSendingAudioAsync(ITelegramBotClient client, ChatId chatId, string url)
        {
            // Load video
            using var fileStream = await _ytReciever.GetAudioStreamAsync(url);
            if (fileStream != null)
            {
                // Loading info for file name
                var videoInfo = await _ytReciever.GetVideoInfoAsync(url);

                var inputFile = InputFile.FromStream(fileStream, videoInfo?.Title ?? "Unknown");
                if (inputFile != null)
                {
                    try
                    {
                        // Sending audio to chat
                        await client.SendAudio(chatId, inputFile, $"@{_host.Me.Username}");
                        _consoleLogger.Log("Audio send sucсessfully");
                        return true;
                    }
                    catch (Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.ErrorCode == 403)
                    {
                        _consoleLogger.Log($"Exceprtion: {ex.Message}", LogStatus.Error);
                        return false;
                    }
                    catch (Exception ex)
                    {
                        _telegramLogger?.Log(ex.Message, client, chatId, LogStatus.Error);
                        _consoleLogger.Log($"Exceprtion: {ex.Message}", LogStatus.Error);
                        return false;
                    }
                }
                else
                {
                    _consoleLogger.Log($"Audio convertation from stream failed", LogStatus.Error);
                    return false;
                }
            }
            else
            {
                _consoleLogger.Log($"Audio stream is null", LogStatus.Error);
                return false;
            }
        }
    }
}
