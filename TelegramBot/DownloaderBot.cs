using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBot
{

    /// <summary>Send message to user status</summary>
    public enum LoadingStatus : byte
    {
        Successfully,
        Error,
        BiggerThanLimit,
        NotValidLink,
    }

    public class DownloaderBot
    {
        // Dependencies
        private readonly Host _host;
        private readonly DownloadManager _downloadManager;
        private readonly ConsoleLogger _consoleLogger;
        private readonly TelegramLogger _telegramLogger;

        // Fields
        private readonly InlineKeyboardMarkup _inlineKeyboard; // Buttons
        private const string PATH_TO_DEFAULT_IMAGE = "resources\\DefaultImage.jpg";

        public DownloaderBot(Host host, DownloadManager downloadManager, ConsoleLogger consoleLogger, TelegramLogger telegramLogger)
        {
            // Dependencies
            _host = host;
            _downloadManager = downloadManager;
            _consoleLogger = consoleLogger;
            _telegramLogger = telegramLogger;

            // Events
            _host.OnMessage += OnUserSendMessage;
            _host.OnCallback += OnUserClickButton;

            // Button keyboard init
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

        public async Task<int> Init()
        {
            // Checking bot resources 
            if (!File.Exists(PATH_TO_DEFAULT_IMAGE))
            {
                _consoleLogger.Log("Cannot find default image");
                return 1;
            }

            // Download manager initialization
            int exitCode = await _downloadManager.Init();

            if (exitCode == 0)
            {
                // Start recieving
                _host.Start();
            }

            return exitCode;
        }

        // Delegates
        private async void OnUserSendMessage(ITelegramBotClient client, Update update)
        {
            var chatId = update?.Message?.Chat.Id ?? 0;
            string userMessage = update?.Message?.Text ?? string.Empty;

            // Default commands
            if (userMessage == "/start")
            {
                await _telegramLogger.Log("Send video link", client, chatId);
                return;
            }

            // Loading and sending info message with buttons
            await SendDownloadedMediaAsync(client, chatId, userMessage, DownloadType.Preview);
        }

        private async void OnUserClickButton(ITelegramBotClient client, CallbackQuery cb)
        {
            var chatId = cb?.Message?.Chat.Id ?? 0;
            var caption = cb?.Message?.Caption ?? string.Empty;

            // Parsing url 
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
                        var loadingVideoMessage = await _telegramLogger.Log("Video has started download...", client, chatId);

                        // Loading and sending video
                        var downloadResult = await SendDownloadedMediaAsync(client, chatId, videoUrl, DownloadType.VideoBest);

                        // If video is bigger than 50mb trying to download merged
                        if (downloadResult == LoadingStatus.BiggerThanLimit)
                        {
                            // Loading message for user
                            var loadingMergedVideoMessage = await _telegramLogger.Log("Trying to download low quality", client, chatId);

                            // Loading and sending merged video
                            var downloadedMergedResult = await SendDownloadedMediaAsync(client, chatId, videoUrl, DownloadType.VideoMerged);

                            if (downloadedMergedResult != LoadingStatus.Successfully)
                                await _telegramLogger.Log("Cannot download the video", client, chatId);

                            // Deleting message for user
                            try
                            {
                                if (loadingMergedVideoMessage != null)
                                    await client.DeleteMessage(chatId, loadingMergedVideoMessage.Id);
                            }
                            catch (Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.ErrorCode == 403)
                            {
                                _consoleLogger.Log($"Exception: {ex.Message}", LogStatus.Error);
                            }
                            catch (Exception ex)
                            {
                                _consoleLogger.Log($"Excertion: {ex.Message}", LogStatus.Error);
                            }
                        }

                        // Deleting message for user
                        try
                        {
                            if (loadingVideoMessage != null)
                                await client.DeleteMessage(chatId, loadingVideoMessage.Id);
                        }
                        catch (Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.ErrorCode == 403)
                        {
                            _consoleLogger.Log($"Exception: {ex.Message}", LogStatus.Error);
                        }
                        catch (Exception ex)
                        {
                            _consoleLogger.Log($"Excertion: {ex.Message}", LogStatus.Error);
                        }
                        break;


                    // User download audio
                    case "action:audio":
                        // Loading message for user
                        var LoadingAudioMessage = await _telegramLogger.Log("Audio has started download...", client, chatId);

                        // Loading and sending audio
                        await SendDownloadedMediaAsync(client, chatId, videoUrl, DownloadType.Audio);

                        // Deleting message for user
                        try
                        {
                            if (LoadingAudioMessage != null)
                                await client.DeleteMessage(chatId, LoadingAudioMessage.Id);
                        }
                        catch (Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.ErrorCode == 403)
                        {
                            _consoleLogger.Log($"Exception: {ex.Message}", LogStatus.Error);
                        }
                        catch (Exception ex)
                        {
                            _consoleLogger.Log($"Exception: {ex.Message}", LogStatus.Error);
                        }
                        break;


                    case "action:cancel":
                        // Deleting info message
                        await client.DeleteMessage(chatId, cb?.Message?.Id ?? 0);
                        break;
                }
            }
        }

        /// <summary>
        /// Downloading media by url and sending to user
        /// </summary>
        /// <param name="client"></param>
        /// <param name="chatId"></param>
        /// <param name="url"></param>
        /// <param name="downloadType"></param>
        /// <returns>Status of sending | downloading media</returns>
        public async Task<LoadingStatus> SendDownloadedMediaAsync(ITelegramBotClient client, ChatId chatId, string url, DownloadType downloadType)
        {
            LoadingStatus result = LoadingStatus.Successfully;

            // Loading path and title
            var mediaData = await _downloadManager.DownloadFileAsync(url, downloadType);

            if (mediaData != null)
            {
                var mediaPath = mediaData?.filePath;

                // Preview may be empty on valid video
                if (downloadType == DownloadType.Preview)
                {
                    if (!File.Exists(mediaPath)) mediaPath = PATH_TO_DEFAULT_IMAGE;
                }

                // Open file stream
                using var fileStream = new FileStream(mediaPath, FileMode.Open, FileAccess.Read, FileShare.Read);

                if (fileStream != null)
                {
                    if (fileStream.Length / 1024 <= 49_500)
                    {
                        try
                        {
                            var inputFile = InputFile.FromStream(fileStream, mediaData?.fileTitle);

                            // Sending media to chat
                            switch (downloadType)
                            {
                                case DownloadType.Preview:

                                    // Key with link to download video
                                    string LinkToVideo = $"\nLINK: {url}";

                                    // Text сaption
                                    string textCaption =
                                        $"{mediaData?.fileTitle}" +
                                        $"\n\nChoose download type:" +
                                        $"\n{LinkToVideo}";

                                    // Telegram message with buttons
                                    await client.SendPhoto(
                                        chatId,
                                        InputFile.FromStream(fileStream),
                                        textCaption,
                                        replyMarkup: _inlineKeyboard
                                    );
                                    break;

                                case DownloadType.Thumbnail:

                                    await client.SendPhoto(
                                        chatId,
                                        inputFile,
                                        caption: $"@{_host.Me.Username}"
                                    );
                                    break;

                                case DownloadType.VideoBest:
                                case DownloadType.VideoMerged:

                                    await client.SendVideo(
                                        chatId,
                                        inputFile,
                                        caption: $"@{_host.Me.Username}"
                                    );
                                    break;

                                case DownloadType.Audio:

                                    await client.SendAudio(
                                        chatId,
                                        inputFile,
                                        caption: $"@{_host.Me.Username}"
                                    );
                                    break;
                                default:
                                    break;
                            }

                            _consoleLogger.Log("Media send sucсessfully");
                        }
                        catch (Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.ErrorCode == 403)
                        {
                            _consoleLogger.Log($"Exception: {ex.Message}", LogStatus.Error);
                        }
                        catch (Exception ex)
                        {
                            _telegramLogger?.Log(ex.Message, client, chatId, LogStatus.Error);
                            _consoleLogger.Log($"Exception: {ex.Message}", LogStatus.Error);
                        }
                    }
                    else
                    {
                        _telegramLogger?.Log("Media limit is 50mb", client, chatId);

                        result = LoadingStatus.BiggerThanLimit;
                    }
                }
                else
                {
                    _consoleLogger.Log("Media stream is null", LogStatus.Error);

                    result = LoadingStatus.Error;
                }
            }
            else
            {
                _consoleLogger.Log("Media path is null", LogStatus.Error);
                await _telegramLogger.Log("Cannot download this. Maybe private access or incorrect link", client, chatId);

                result = LoadingStatus.NotValidLink;
            }

            // Clear file with picture
            DeleteTemporaryFile(mediaData?.filePath);

            return result;
        }
        private void DeleteTemporaryFile(string path)
        {
            // Deleting video file
            if (File.Exists(path))
                File.Delete(path);
        }

    };
}