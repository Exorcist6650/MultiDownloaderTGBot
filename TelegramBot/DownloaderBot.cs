using System.Runtime.InteropServices.JavaScript;
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
        private readonly TelegramLogger _telegramLogger;

        // Fields
        private readonly InlineKeyboardMarkup _inlineKeyboard; // Buttons
        private const string PATH_TO_DEFAULT_IMAGE = "resources\\DefaultImage.jpg";

        public DownloaderBot(Host host, DownloadManager downloadManager, ConsoleLogger consoleLogger, TelegramLogger telegramLogger)
        {
            // Dependencies
            _host = host;
            _downloadManager = downloadManager;
            _telegramLogger = telegramLogger;

            // Events
            _host.OnMessage += OnUserSendMessage;
            _host.OnCallback += OnUserClickButton;

            // Button keyboard init
            _inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData
                    (
                        JSONReader.getValue("ButtonVideo") ?? string.Empty, "action:video"
                    ),
                    InlineKeyboardButton.WithCallbackData
                    (
                        JSONReader.getValue("ButtonAudio") ?? string.Empty, "action:audio"
                    ),
                },
                new []
                {
                    InlineKeyboardButton.WithCallbackData
                    (
                        JSONReader.getValue("ButtonCancel") ?? string.Empty, "action:cancel"
                    ),
                },
            });
        }

        public async Task<int> Init()
        {
            // Checking bot resources 
            if (!File.Exists(PATH_TO_DEFAULT_IMAGE))
            {
                ConsoleLogger.Log("Cannot find default image");
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
                await _telegramLogger.Send(JSONReader.getValue("Greeting") ?? string.Empty, client, chatId);
                return;
            }

            // Sending searching message
            var searchingMessage = await _telegramLogger.Send(JSONReader.getValue("Searching") ?? string.Empty, client, chatId);

            // Loading and sending info message with buttons
            var loadingResutl = await SendDownloadedMediaAsync(client, chatId, userMessage, DownloadType.Preview);
            switch (loadingResutl)
            {
                case LoadingStatus.BiggerThanLimit:
                    await _telegramLogger.Send
                        (
                            JSONReader.getValue("MediaLimit") ?? string.Empty, client, chatId
                        );
                    break;

                case LoadingStatus.NotValidLink:
                    await _telegramLogger.Send
                        (
                            JSONReader.getValue("NotValidLink") ?? string.Empty, client, chatId
                        );
                    break;
            }

            // Delete searching message
            await _telegramLogger.Delete(searchingMessage, client, chatId);
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
                        var loadingVideoMessage = await _telegramLogger.Send
                            (
                                JSONReader.getValue("LoadingVideo") ?? string.Empty, client, chatId
                            );

                        // Loading and sending video
                        var downloadVideoResult = await SendDownloadedMediaAsync(client, chatId, videoUrl, DownloadType.VideoBest);

                        // If video is bigger than 50mb trying to download merged
                        if (downloadVideoResult == LoadingStatus.BiggerThanLimit)
                        {
                            // Limit message for user
                            var biggerThanLimitMessage = await _telegramLogger.Send
                                (
                                    JSONReader.getValue("MediaLimit") ?? string.Empty, client, chatId
                                );

                            // Loading new video message for user
                            var loadingMergedVideoMessage = await _telegramLogger.Send
                                (
                                    JSONReader.getValue("LowQualityDownloadTrying") ?? string.Empty, client, chatId
                                );

                            // Loading and sending merged video
                            var downloadVideoMergedResult = await SendDownloadedMediaAsync(client, chatId, videoUrl, DownloadType.VideoMerged);

                            // Limit message for user
                            if (downloadVideoMergedResult == LoadingStatus.BiggerThanLimit)
                            {
                                await _telegramLogger.Send
                                (
                                    JSONReader.getValue("MediaLimit") ?? string.Empty, client, chatId
                                );
                            }

                            // Deleting message for user
                            await _telegramLogger.Delete(biggerThanLimitMessage, client, chatId);
                            await _telegramLogger.Delete(loadingMergedVideoMessage, client, chatId);
                        }
                        else if (downloadVideoResult == LoadingStatus.NotValidLink)
                        {
                            await _telegramLogger.Send
                                (
                                    JSONReader.getValue("NotValidLink") ?? string.Empty, client, chatId
                                );
                        }

                        // Deleting message for user
                        await _telegramLogger.Delete(loadingVideoMessage, client, chatId);
                        break;


                    // User download audio
                    case "action:audio":
                        // Loading message for user
                        var loadingAudioMessage = await _telegramLogger.Send
                            (
                                JSONReader.getValue("LoadingAudio") ?? string.Empty, client, chatId
                            );

                        // Loading and sending audio
                        var downloadAudioResult = await SendDownloadedMediaAsync(client, chatId, videoUrl, DownloadType.Audio);

                        // Limit message for user
                        if (downloadAudioResult == LoadingStatus.BiggerThanLimit)
                        {
                            await _telegramLogger.Send
                                (
                                    JSONReader.getValue("MediaLimit") ?? string.Empty, client, chatId
                                );
                        }
                        else if (downloadAudioResult == LoadingStatus.NotValidLink)
                        {
                            await _telegramLogger.Send
                                (
                                    JSONReader.getValue("NotValidLink") ?? string.Empty, client, chatId
                                );
                        }

                        // Deleting message for user
                        await _telegramLogger.Delete(loadingAudioMessage, client, chatId);
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
                                        $"\n\n{JSONReader.getValue("InfoText")}" +
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

                            ConsoleLogger.Log("Media send sucсessfully");
                        }
                        catch (Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.ErrorCode == 403)
                        {
                            ConsoleLogger.Log($"Exception: {ex.Message}", LogStatus.Error);

                            result = LoadingStatus.Error;
                        }
                        catch (Exception ex)
                        {
                            _telegramLogger?.Send(ex.Message, client, chatId, LogStatus.Error);
                            ConsoleLogger.Log($"Exception: {ex.Message}", LogStatus.Error);

                            result = LoadingStatus.Error;
                        }
                    }
                    else
                        result = LoadingStatus.BiggerThanLimit;
                }
                else
                {
                    ConsoleLogger.Log("Media stream is null", LogStatus.Error);

                    result = LoadingStatus.Error;
                }
            }
            else
            {
                ConsoleLogger.Log("Media path is null", LogStatus.Error);

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