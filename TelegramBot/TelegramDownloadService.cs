using Managers;
using Services;
using System.Runtime.InteropServices.JavaScript;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Utils;

namespace Services
{
    public class TelegramDownloadService(
        DownloadManager downloadManager, ILogger logger)
    {
        // Fields
        private readonly DownloadManager _downloadManager = downloadManager;
        private readonly ILogger _logger = logger;

        private const string PATH_TO_DEFAULT_IMAGE = "resources\\DefaultImage.jpg";


        // Public
        public async Task Init()
        {
            // Checking bot resources 
            if (!File.Exists(PATH_TO_DEFAULT_IMAGE))
                throw new FileNotFoundException("Default image not found", PATH_TO_DEFAULT_IMAGE);

            // Download manager initialization
            await _downloadManager.Init();
        }

        public async Task DownloadSendVideoProcess(
           ITelegramBotClient client, 
           ChatId chatId, 
           string videoUrl, 
           string botUsername,
           ELanguage language)
        {
            // Loading message for user
            var loadingVideoMessage = await MessageService.Send(client, chatId, new Message
            { Text = ReplyReadService.GetReply("LoadingVideo", language) }, _logger);

            if (loadingVideoMessage is not null)
            {
                // Loading and sending video
                var downloadVideoResult = await DownloadSendProcessAsync(
                    client, 
                    chatId, 
                    videoUrl, 
                    botUsername, 
                    EDownloadType.VideoBest);

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
                            client, 
                            chatId, 
                            videoUrl, 
                            botUsername, 
                            EDownloadType.VideoMerged);

                        // Deleting retry message for user
                        await MessageService.Remove(client, chatId, retryMessage, _logger);

                    }
                }

                // Deleting loading message for user
                await MessageService.Remove(client, chatId, loadingVideoMessage, _logger);
            }
        }

        public async Task DownloadSendAudioProcess(
            ITelegramBotClient client,
            ChatId chatId,
            string videoUrl,
            string botUsername,
            ELanguage language)
        {
            // Loading message for user
            var loadingAudioMessage = await MessageService.Send(client, chatId, new Message
            { Text = ReplyReadService.GetReply("LoadingAudio", language) }, _logger);

            if (loadingAudioMessage is not null)
            {
                // Loading and sending audio
                await DownloadSendProcessAsync(
                    client, 
                    chatId, 
                    videoUrl, 
                    botUsername,
                    EDownloadType.Audio);

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

                if (File.Exists(previewInfo.filePath))
                    File.Delete(previewInfo.filePath); // Clear temp file
            }
        }

        public async Task<ELoadingStatus> DownloadSendProcessAsync(
            ITelegramBotClient client, 
            ChatId chatId, 
            string url, 
            string botUsername,
            EDownloadType downloadType)
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
                                caption: $"@{botUsername}"
                            );
                            break;

                        case EDownloadType.VideoMerged or EDownloadType.VideoBest:

                            await client.SendVideo(
                                chatId,
                                inputFile,
                                caption: $"@{botUsername}"
                            );
                            break;

                        case EDownloadType.Audio:

                            await client.SendAudio(
                                chatId,
                                inputFile,
                                caption: $"@{botUsername}"
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

                if (File.Exists(mediaInfo.filePath))
                    File.Delete(mediaInfo.filePath); // Clear temp file
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
    }
}
