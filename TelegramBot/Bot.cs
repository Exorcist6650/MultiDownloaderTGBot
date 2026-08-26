using Managers;
using Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Utils;

namespace TelegramBot
{
    public class Bot
    {
        // Dependencies
        private readonly TgHost _host;
        private readonly DownloadManager _downloadManager;
        private readonly ILogger _logger;

        // Fields
        private const string PATH_TO_DEFAULT_IMAGE = "resources\\DefaultImage.jpg";

        public Bot(TgHost host, DownloadManager downloadManager, ILogger logger)
        {
            // Dependencies
            _host = host;
            _downloadManager = downloadManager;
            _logger = logger;

            // Events
            _host.OnMessage += OnMessage;
            _host.OnCallback += OnUserClickButton;
        }

        public async Task Init()
        {
            // Checking bot resources 
            if (!File.Exists(PATH_TO_DEFAULT_IMAGE))
                throw new FileNotFoundException("Default image not found", PATH_TO_DEFAULT_IMAGE);

            // Download manager initialization
            await _downloadManager.Init();

            await _host.Init();
        }

        // Delegates
        private async void OnMessage(ITelegramBotClient client, Update update)
        {
            if (update?.Message is not { } message) return;
            if (update?.Message.Chat.Id is not { } chatId) return;

            // User language
            var lang = message.From?.LanguageCode switch
            {
                "en" => ELanguage.En,
                "ru" => ELanguage.Ru,
                _ => ELanguage.En
            };

            // Delete user message
            if (message is not null)
                await MessageService.Remove(client, message.Chat.Id, message, _logger);

            // Default commands
            if (message?.Text == "/start")
            {
                await MessageService.Send(client, chatId, new Message
                { Text = ReplyReadService.GetReply("Greeting", lang) }, _logger);

                return;
            }

            // Checking message a link
            if (string.IsNullOrEmpty(message?.Text) || !message.Text.Contains("http") || !message.Text.Contains("https"))
            {
                await MessageService.Send(client, chatId, new Message
                { Text = ReplyReadService.GetReply("NotALink", lang) }, _logger);

                return;
            }

            // Sending searching message
            var searchMessage = await MessageService.Send(client, chatId, new Message
            { Text = ReplyReadService.GetReply("Search", lang) }, _logger);

            if (searchMessage is not null)
            {
                // Sending load menu
                var loadResult = await SendLoadingMenuProcess(client, chatId, message.Text, lang);

                // If problems
                if (loadResult == ELoadingStatus.NotValidLink)
                    // Bot answer to not valid link
                    await MessageService.Send(client, chatId, new Message
                    { Text = ReplyReadService.GetReply("NotValidLink", lang) }, _logger);

                // Delete searching message
                await MessageService.Remove(client, chatId, searchMessage, _logger);
            }

        }

        private async void OnUserClickButton(ITelegramBotClient client, CallbackQuery cb)
        {
            if (cb?.Message is not { } message) return;
            if (message.Chat?.Id is not { } chatId) return;
            if (message.Caption is not { } caption) return;

            // User language
            var lang = message.From?.LanguageCode switch
            {
                "en" => ELanguage.En,
                "ru" => ELanguage.Ru,
                _ => ELanguage.En
            };

            // Parsing url 
            string key = "\nLINK: ";
            var prefix = caption.IndexOf(key);

            if (prefix >= 0)
            {
                var videoUrl = caption[(prefix + key.Length)..]; // URL from caption

                switch (cb?.Data)
                {
                    // Download video
                    case "action:video":

                        await DownloadSendVideoProcess(videoUrl, client, chatId, lang);
                        break;

                    // Download audio
                    case "action:audio":
                        await DownloadSendAudioProcess(videoUrl, client, chatId, lang);
                        break;


                    // Deleting info message
                    case "action:cancel":
                        await MessageService.Remove(client, chatId, cb.Message, _logger);
                        break;
                }
            }
        }

        public async Task DownloadSendVideoProcess(
            string videoUrl, ITelegramBotClient client, ChatId chatId, ELanguage language)
        {
            // Loading message for user
            var loadingVideoMessage = await MessageService.Send(client, chatId, new Message
            { Text = ReplyReadService.GetReply("LoadingVideo", language) }, _logger);

            if (loadingVideoMessage is not null)
            {
                // Loading and sending video
                var downloadVideoResult = await DownloadSendProcessAsync(
                    client, chatId, videoUrl, EDownloadType.VideoBest);

                // If video is bigger than 50mb trying to download merged
                if (downloadVideoResult == ELoadingStatus.BiggerThanLimit)
                {
                    // Retry message for user
                    var retryMessage = await MessageService.Send(client, chatId, new Message
                    { Text = ReplyReadService.GetReply("LowQualityDownloadTrying", language) }, _logger);

                    if (retryMessage is not null)
                    {
                        // Loading and sending merged video
                        await DownloadSendProcessAsync(
                            client, chatId, videoUrl, EDownloadType.VideoMerged);

                        // Deleting retry message for user
                        await MessageService.Remove(client, chatId, retryMessage, _logger);

                    }
                }

                // Deleting loading message for user
                await MessageService.Remove(client, chatId, loadingVideoMessage, _logger);
            }
        }

        public async Task DownloadSendAudioProcess(
            string videoUrl, ITelegramBotClient client, ChatId chatId, ELanguage language)
        {
            // Loading message for user
            var loadingAudioMessage = await MessageService.Send(client, chatId, new Message
            { Text = ReplyReadService.GetReply("LoadingVideo", language) }, _logger);

            if (loadingAudioMessage is not null)
            {
                // Loading and sending audio
                await DownloadSendProcessAsync(
                    client, chatId, videoUrl, EDownloadType.Audio);

                // Deleting loading message for user
                await MessageService.Remove(client, chatId, loadingAudioMessage, _logger);
            }
        }

        public async Task<ELoadingStatus> SendLoadingMenuProcess(
            ITelegramBotClient client, ChatId chatId, string url, ELanguage language)
        {
            // Download preview to temp and get info
            if (await _downloadManager.DownloadToTempAsync(url, EDownloadType.Thumbnail) is not { } previewInfo)
            {
                // Media was not downloaded
                _logger.Log("Media path is null", ELogStatus.Error);
                return ELoadingStatus.NotValidLink;
            }

            // Set default image if not 
            if (!File.Exists(previewInfo.filePath)) previewInfo.filePath = PATH_TO_DEFAULT_IMAGE;

            // Get preview input file
            if (_downloadManager.GetInputFile(previewInfo) is not { } inputFile)
            {
                // Input file is null
                _logger.Log("Input file is null", ELogStatus.Error);
                return ELoadingStatus.Error;
            }

            // Dispose input file
            try
            {

                // Key with link to download video
                string LinkToVideo = $"\nLINK: {url}";

                // Text сaption
                string textCaption =
                    $"{previewInfo.fileTitle}" +
                    $"\n\n{ReplyReadService.GetReply("DownloadInfoText", language)}" +
                    $"\n{LinkToVideo}";

                // Inline keyboard
                InlineKeyboardMarkup inlineKeyboard = BuildLoadingMenuKeyboard(language);

                // Send loading menu to user
                if (await MessageService.SendButtonMenu(
                    client, chatId, inputFile, inlineKeyboard, textCaption, _logger) is not null)
                {
                    return ELoadingStatus.Successfully;
                }

                return ELoadingStatus.Error;
            }
            finally
            {
                inputFile.Content.Dispose(); // Dispose filestream

                DeleteTemporaryFile(previewInfo.filePath); // Clear temp file
            }
        }


        public async Task<ELoadingStatus> DownloadSendProcessAsync(
            ITelegramBotClient client, ChatId chatId, string url, EDownloadType downloadType)
        {
            const int fileBytesLimit = 1024 * 49500; // Media weight limit

            // Download media to temp and get info
            if (await _downloadManager.DownloadToTempAsync(url, downloadType) is not { } mediaInfo)
            {
                return ELoadingStatus.NotValidLink;
            }

            // Get input file
            if (_downloadManager.GetInputFile(mediaInfo) is not { } inputFile)
            {
                // Input file is null
                _logger.Log("Input file is null", ELogStatus.Error);
                return ELoadingStatus.Error;
            }

            // Dispose input file
            try
            {
                // Checking weight limit
                if (inputFile.Content.Length > fileBytesLimit)
                {
                    return ELoadingStatus.BiggerThanLimit;
                }

                // Sending media to chat
                try
                {
                    switch (downloadType)
                    {
                        case EDownloadType.Thumbnail:

                            await client.SendPhoto(
                                chatId,
                                inputFile,
                                caption: $"@{_host.Me.Username}"
                            );
                            break;

                        case EDownloadType.VideoMerged or EDownloadType.VideoBest:

                            await client.SendVideo(
                                chatId,
                                inputFile,
                                caption: $"@{_host.Me.Username}"
                            );
                            break;

                        case EDownloadType.Audio:

                            await client.SendAudio(
                                chatId,
                                inputFile,
                                caption: $"@{_host.Me.Username}"
                            );
                            break;
                    }

                }
                catch (Exception ex)
                {
                    _logger.Log(ex.Message, ELogStatus.Warning);
                    return ELoadingStatus.Error;
                }

                return ELoadingStatus.Successfully;
            }
            finally
            {
                inputFile.Content.Dispose(); // Dispose filestream

                DeleteTemporaryFile(mediaInfo.filePath); // Clear temp file
            }
        }

        private InlineKeyboardMarkup BuildLoadingMenuKeyboard(ELanguage language) =>
            new(
            [
                [
                    InlineKeyboardButton.WithCallbackData
                    (
                        ReplyReadService.GetReply("ButtonVideo", language), "action:video"
                    ),
                    InlineKeyboardButton.WithCallbackData
                    (
                        ReplyReadService.GetReply("ButtonAudio", language), "action:audio"
                    ),
                ],

                [
                    InlineKeyboardButton.WithCallbackData
                    (
                        ReplyReadService.GetReply("ButtonCancel", language), "action:cancel"
                    ),
                ],
            ]);

        private void DeleteTemporaryFile(string path)
        {
            // Deleting video file
            if (File.Exists(path))
                File.Delete(path);
        }

    };
}