using System;
using Managers;
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

        private bool _isInit = false;
        private string _botUsername;

        private const string PATH_TO_DEFAULT_IMAGE = "resources\\DefaultImage.jpg";
        private const long FILE_BYTES_LIMIT = 49_500 * 1024L;


        // Public
        public async Task Init(string botUsername)
        {
            // Checking bot resources 
            if (!File.Exists(PATH_TO_DEFAULT_IMAGE))
                throw new FileNotFoundException("Default image not found", PATH_TO_DEFAULT_IMAGE);

            // Download manager initialization
            await _downloadManager.Init();

            _botUsername = botUsername;
            _isInit = true;
        }

        public async Task DownloadSendVideoProcess(
           ITelegramBotClient client,
           ChatId chatId,
           string videoUrl,
           ELanguage language)
        {
            if (!_isInit) throw new InvalidOperationException("TelegramDownloadService isn't init");

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
                        var downloadMergedResult = await DownloadSendProcessAsync(
                            client,
                            chatId,
                            videoUrl,
                            EDownloadType.VideoMerged);

                        // Bot answer to bigger than limit
                        if (downloadMergedResult == ELoadingStatus.BiggerThanLimit)
                            await MessageService.Send(client, chatId, new Message
                            { Text = ReplyReadService.GetReply("MediaLimit", language) }, _logger);

                        // Bot answer to error
                        else if (downloadMergedResult is ELoadingStatus.Error or ELoadingStatus.NotValidLink)
                            await MessageService.Send(client, chatId, new Message
                            { Text = ReplyReadService.GetReply("NotValidLink", language) }, _logger);

                        // Deleting retry message for user
                        await MessageService.Remove(client, chatId, retryMessage, _logger);

                    }
                }

                // Bot answer to error
                if (downloadVideoResult is ELoadingStatus.Error or ELoadingStatus.NotValidLink)
                    await MessageService.Send(client, chatId, new Message
                    { Text = ReplyReadService.GetReply("NotValidLink", language) }, _logger);

                // Deleting loading message for user
                await MessageService.Remove(client, chatId, loadingVideoMessage, _logger);
            }
        }

        public async Task DownloadSendAudioProcess(
            ITelegramBotClient client,
            ChatId chatId,
            string videoUrl,
            ELanguage language)
        {
            if (!_isInit) throw new InvalidOperationException("TelegramDownloadService isn't init");

            // Loading message for user
            var loadingAudioMessage = await MessageService.Send(client, chatId, new Message
            { Text = ReplyReadService.GetReply("LoadingAudio", language) }, _logger);

            if (loadingAudioMessage is not null)
            {
                // Loading and sending audio
                var downloadAudioResult = await DownloadSendProcessAsync(
                    client,
                    chatId,
                    videoUrl,
                    EDownloadType.Audio);

                // Bot answer to bigger than limit
                if (downloadAudioResult == ELoadingStatus.BiggerThanLimit)
                    await MessageService.Send(client, chatId, new Message
                    { Text = ReplyReadService.GetReply("MediaLimit", language) }, _logger);

                // Bot answer to error
                else if (downloadAudioResult is ELoadingStatus.Error or ELoadingStatus.NotValidLink)
                    await MessageService.Send(client, chatId, new Message
                    { Text = ReplyReadService.GetReply("NotValidLink", language) }, _logger);

                // Deleting loading message for user
                await MessageService.Remove(client, chatId, loadingAudioMessage, _logger);
            }
        }

        public async Task<ELoadingStatus> SendLoadingMenuProcess(
            ITelegramBotClient client, ChatId chatId, string url, ELanguage language)
        {
            if (!_isInit) throw new InvalidOperationException("TelegramDownloadService isn't init");

            // Download preview to temp and get info
            if (await _downloadManager.DownloadToTempAsync(url, EDownloadType.Thumbnail) is not { } previewInfo)
                return ELoadingStatus.NotValidLink;

            // Set variables and change to default image if filepath is null
            var (filePath, title) =
                (File.Exists(previewInfo.FilePath) ? previewInfo.FilePath : PATH_TO_DEFAULT_IMAGE,
                previewInfo.FileTitle);

            // Get preview input file
            var inputFile = _downloadManager.GetInputFile((filePath, title));
            
            // Dispose input file
            try
            {
                // Key with link to download video
                string linkToVideo = $"\nLINK: {url}";

                // Text сaption
                string caption =
                    $"{title}" +
                    $"\n\n{ReplyReadService.GetReply("DownloadInfoText", language)}" +
                    $"\n{linkToVideo}";

                await SendLoadingMenuAsync(client, chatId, inputFile, caption, language);

                return ELoadingStatus.Successfully;
            }
            catch(Exception ex)
            {
                _logger.Log(ex.ToString(), ELogStatus.Warning);
                return ELoadingStatus.Error;
            }
            finally
            {
                inputFile.Content.Dispose(); // Dispose filestream

                DeleteTempFile(previewInfo.FilePath); // Clear temp file
            }
        }

        public async Task<ELoadingStatus> DownloadSendProcessAsync(
            ITelegramBotClient client,
            ChatId chatId,
            string url,
            EDownloadType downloadType)
        {
            if (!_isInit) throw new InvalidOperationException("TelegramDownloadService isn't init");

            // Download media to temp and get info
            if (await _downloadManager.DownloadToTempAsync(url, downloadType) is not { } mediaInfo)
                return ELoadingStatus.NotValidLink;

            // Get input file
            var inputFile = _downloadManager.GetInputFile(mediaInfo);

            // Dispose input file
            try
            {
                // Checking weight limit
                if (inputFile.Content.Length > FILE_BYTES_LIMIT)
                {
                    return ELoadingStatus.BiggerThanLimit;
                }

                // Load media to chat
                await SendLoadedAsync(client, chatId, inputFile, downloadType);

                return ELoadingStatus.Successfully;
            }
            catch (Exception ex)
            {
                _logger.Log(ex.ToString(), ELogStatus.Warning);
                return ELoadingStatus.Error;
            }

            finally
            {
                inputFile.Content.Dispose(); // Dispose filestream

                DeleteTempFile(mediaInfo.FilePath); // Clear temp file
            }
        }


        // PRIVATE

        private async Task<Message> SendLoadingMenuAsync(
            ITelegramBotClient client, 
            ChatId chatId, 
            InputFile inputFile,
            string caption, 
            ELanguage language)
        {
            // Inline keyboard
            InlineKeyboardMarkup inlineKeyboard = BuildLoadingMenuKeyboard(language);

            // Send loading menu to user
            return await MessageService.SendButtonMenu(
                client, 
                chatId, 
                inputFile, 
                caption, 
                inlineKeyboard);
        }
        
        private async Task<Message> SendLoadedAsync(
            ITelegramBotClient client, 
            ChatId chatId, 
            InputFile inputFile, 
            EDownloadType downloadType)
        {
            var caption = $"@{_botUsername}";

            // Return loaded media
            return downloadType switch
            {
                EDownloadType.Thumbnail =>  
                await client.SendPhoto(chatId, inputFile, caption),

                EDownloadType.VideoMerged or EDownloadType.VideoBest =>     
                await client.SendVideo(chatId, inputFile, caption),

                EDownloadType.Audio => 
                await client.SendAudio(chatId, inputFile, caption),

                _ => throw new ArgumentException("Unknown download type", nameof(downloadType)),
            };
        }
        private void DeleteTempFile(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                _logger.Log(ex.ToString(), ELogStatus.Error);
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
