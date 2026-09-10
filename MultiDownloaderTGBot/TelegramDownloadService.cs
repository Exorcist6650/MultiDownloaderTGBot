using Managers;
using System;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Utils;
using Xabe.FFmpeg;

namespace Services
{
    public class TelegramDownloadService(
        DownloadManager downloadManager, ILogger logger)
    {
        // Fields
        private readonly DownloadManager _downloadManager = downloadManager;
        private readonly ILogger _logger = logger;

        public bool IsInit { get; private set; } = false;
        private string _botUsername;

        private const long FILE_BYTES_LIMIT = 49_500 * 1024L;


        // PUBLIC

        public async Task Init(string botUsername)
        {
            // Download manager initialization
            await _downloadManager.Init();

            _botUsername = botUsername;
            IsInit = true;
        }

        public async Task DownloadSendVideoProcess(
           ITelegramBotClient client,
           ChatId chatId,
           string videoUrl,
           ELanguage language)
        {
            if (!IsInit) throw new InvalidOperationException("TelegramDownloadService isn't init");

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
                    EDownloadType.Video);

                // Bot answer to bigger than limit
                if (downloadVideoResult == ELoadingStatus.BiggerThanLimit)
                {
                    await MessageService.Send(client, chatId, new Message
                    { Text = ReplyReadService.GetReply("MediaLimit", language) }, _logger);
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
            if (!IsInit) throw new InvalidOperationException("TelegramDownloadService isn't init");

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

        public async Task<Message> SendLoadingMenuAsync(
            ITelegramBotClient client, ChatId chatId, string url, ELanguage language)
        {
            // Key with link to download video
            string linkToVideo = $"\nLINK: {url}";

            // Text сaption
            string caption =
                $"\n\n{ReplyReadService.GetReply("DownloadInfoText", language)}" +
                $"\n{linkToVideo}";

            return 
                await SendMenuAsync(client, chatId, caption, language);  
        }

        public async Task<ELoadingStatus> DownloadSendProcessAsync(
            ITelegramBotClient client,
            ChatId chatId,
            string url,
            EDownloadType downloadType)
        {
            if (!IsInit) throw new InvalidOperationException("TelegramDownloadService isn't init");

            // Download media to temp and get info
            if (await _downloadManager.DownloadToTempAsync(url, downloadType) is not { } mediaInfo)
                return ELoadingStatus.NotValidLink;

            // Get input file
            if (_downloadManager.GetInputFile(mediaInfo) is not { } inputFile)
            {
                _logger.Log("Input file is null", ELogStatus.Error);
                DeleteTempFile(mediaInfo.FilePath);
                return ELoadingStatus.Error;
            }

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

        private static async Task<Message> SendMenuAsync(
            ITelegramBotClient client,
            ChatId chatId,
            string text,
            ELanguage language)
        {
            // Inline keyboard
            InlineKeyboardMarkup inlineKeyboard = BuildLoadingMenuKeyboard(language);

            // Send loading menu to user
            return await MessageService.SendButtonMenu(
                client,
                chatId,
                text,
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

                EDownloadType.Video =>
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

        private static InlineKeyboardMarkup BuildLoadingMenuKeyboard(ELanguage language) =>
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
