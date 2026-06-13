using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using YoutubeConnect;

namespace TelegramBot
{
    public class DownloaderBot
    {
        // Dependencies
        private readonly Host _host;
        private readonly YoutubeReciever _ytReciever;
        private readonly DownloadManager _downloadManager;
        private readonly ConsoleLogger _consoleLogger;
        private readonly TelegramLogger _telegramLogger;

        // Fields
        private readonly InlineKeyboardMarkup _inlineKeyboard;
        private const string _PATH_TO_DEFAULT_IMAGE = "resources\\DefaultImage.jpg";

        public DownloaderBot(Host host, YoutubeReciever ytReciever, DownloadManager downloadManager, ConsoleLogger consoleLogger, TelegramLogger telegramLogger)
        {
            // Binding
            _host = host;
            _ytReciever = ytReciever;
            _downloadManager = downloadManager;
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


        public async Task<int> Init()
        {
            // Checking bot resources 
            if (!File.Exists(_PATH_TO_DEFAULT_IMAGE))
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
                        var LoadingVideoMessage = await _telegramLogger.Log("Video has started download...", client, chatId);

                        // Loading and sending video
                        await SendDownloadedMediaAsync(client, chatId, videoUrl, DownloadType.VideoMerged);

                        // Deleting message for user
                        try
                        {
                            if (LoadingVideoMessage != null)
                                await client.DeleteMessage(chatId, LoadingVideoMessage.Id);
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
                        await client.SendPhoto(
                            chatId,
                            InputFile.FromStream(memoryStream),
                            textCaption,
                            replyMarkup: _inlineKeyboard
                        );

                        _consoleLogger.Log("Preview sending sucсessfully");
                    }
                    catch (Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.ErrorCode == 403)
                    {
                        _consoleLogger.Log($"Exception: {ex.Message}", LogStatus.Error);
                    }
                    catch (Exception ex)
                    {
                        _consoleLogger.Log($"Exception: {ex.Message}", LogStatus.Error);
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

        private async Task SendPreviewMessageAsync(ITelegramBotClient client, ChatId chatId, string url)
        {
            // Get async video
            var thumbnailData = await _downloadManager.DownloadFileAsync(url, DownloadType.Thumbnail);
            if (thumbnailData != null)
            {
                // Open file stream
                using var fileStream = new FileStream(thumbnailData?.filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                if (fileStream != null)
                {
                    // Key with link to download video
                    string LinkToVideo = $"\nLINK: {url}";

                    // Text сaption
                    string textCaption =
                        $"{thumbnailData?.fileTitle}" +
                        $"\n\nChoose download type:" +
                        $"\n{LinkToVideo}";

                    // Sending to chat
                    try
                    {

                        // Telegram message with buttons
                        await client.SendPhoto(
                            chatId,
                            InputFile.FromStream(fileStream),
                            textCaption,
                            replyMarkup: _inlineKeyboard
                        );

                        _consoleLogger.Log("Preview sending sucсessfully");
                    }
                    catch (Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.ErrorCode == 403)
                    {
                        _consoleLogger.Log($"Exception: {ex.Message}", LogStatus.Error);
                    }
                    catch (Exception ex)
                    {
                        _consoleLogger.Log($"Exception: {ex.Message}", LogStatus.Error);
                    }
                }
                else
                {
                    await _telegramLogger.Log("Preview download failed", client, chatId, LogStatus.Error);
                    _consoleLogger.Log("Preview stream failed", LogStatus.Error);
                }

                // Clear file with picture
                DeleteTemporaryFile(thumbnailData?.filePath);
            }
            else
                await _telegramLogger.Log("Cannot download this. Maybe private access or incorrect link", client, chatId);

        }

        private async Task SendDownloadedMediaAsync(ITelegramBotClient client, ChatId chatId, string url, DownloadType downloadType)
        {
            // Loading path and title
            var mediaData = await _downloadManager.DownloadFileAsync(url, downloadType);

            if (mediaData != null)
            {
                var mediaPath = mediaData?.filePath;

                // Preview may be empty on valid video
                if (downloadType == DownloadType.Preview)
                {
                    if (!File.Exists(mediaPath)) mediaPath = _PATH_TO_DEFAULT_IMAGE;
                }

                // Open file stream
                using var fileStream = new FileStream(mediaPath, FileMode.Open, FileAccess.Read, FileShare.Read);

                if (fileStream != null)
                {
                    if (fileStream.Length / 1024 <= 49_500)
                    {
                        try
                        {
                            var inputFile = InputFile.FromStream(fileStream);

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

                            _consoleLogger.Log("Video send sucсessfully");
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
                        _telegramLogger?.Log("Media limit is 50mb", client, chatId);
                }
                else
                    _consoleLogger.Log("Media stream is null", LogStatus.Error);

                // Clear file with picture
                DeleteTemporaryFile(mediaData?.filePath);
            }
            else
            {
                _consoleLogger.Log("Media path is null", LogStatus.Error);
                await _telegramLogger.Log("Cannot download this. Maybe private access or incorrect link", client, chatId);
            }
        }
        private void DeleteTemporaryFile(string path)
        {
            // Deleting video file
            if (File.Exists(path))
                File.Delete(path);
        }

        private async Task LoadAndSendMuxedVideoAsync(ITelegramBotClient client, ChatId chatId, string url)
        {
            // Temp file path
            var videoPath = await _ytReciever.LoadTempVideoMuxedAsync(url);

            if (videoPath != null)
            {
                // Open file stream
                using var fileStream = new FileStream(videoPath, FileMode.Open, FileAccess.Read, FileShare.Read);

                if (fileStream != null)
                {
                    if (fileStream.Length / 1024 <= 49_500)
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
                            _consoleLogger.Log($"Exception: {ex.Message}", LogStatus.Error);
                        }
                        catch (Exception ex)
                        {
                            _telegramLogger?.Log(ex.Message, client, chatId, LogStatus.Error);
                            _consoleLogger.Log($"Exception: {ex.Message}", LogStatus.Error);
                        }
                    }
                    else
                        _telegramLogger?.Log("Video limit is 50mb", client, chatId);
                }
                else
                    _consoleLogger.Log($"Video stream is null", LogStatus.Error);

            }
            else
                _consoleLogger.Log($"Video path is null", LogStatus.Error);
        }
        private async Task LoadAndSendingAudioAsync(ITelegramBotClient client, ChatId chatId, string url)
        {
            // Temp file path
            var audioPath = await _ytReciever.LoadTempAudioAsync(url);

            if (audioPath != null)
            {

                // Open file stream
                using var fileStream = new FileStream(audioPath, FileMode.Open, FileAccess.Read, FileShare.Read);

                if (fileStream != null)
                {
                    if (fileStream.Length / 1024 <= 49_500)
                    {
                        // Loading video info 
                        var videoInfo = await _ytReciever.GetVideoInfoAsync(url);

                        try
                        {
                            // Sending audio to chat
                            await client.SendAudio(
                                chatId,
                                new InputFileStream(fileStream, videoInfo?.Title ?? "Unknown"),
                                caption: $"@{_host.Me.Username}"
                            );

                            _consoleLogger.Log("Audio send sucсessfully");
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
                        _telegramLogger?.Log("Audio limit is 50mb", client, chatId);
                }
                else
                    _consoleLogger.Log($"Audio stream is null", LogStatus.Error);
            }
            else
                _consoleLogger.Log($"Audio path is null", LogStatus.Error);

            // Deleting audio file
            if (System.IO.File.Exists(audioPath))
                System.IO.File.Delete(audioPath);
        }
    }
}
