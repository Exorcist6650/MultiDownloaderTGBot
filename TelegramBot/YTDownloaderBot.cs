using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using AngleSharp.Dom;
using Telegram.Bot;
using Telegram.Bot.Types;
using YoutubeConnect;

namespace TelegramBot
{
    public class YTDownloaderBot
    {
        // Dependencies
        private readonly Host _host;
        private readonly YoutubeReciever _ytReciever;
        private readonly ConsoleLogger _consoleLogger;
        private readonly TelegramLogger _telegramLogger;

        public YTDownloaderBot(Host host, YoutubeReciever ytReciever, ConsoleLogger consoleLogger, TelegramLogger telegramLogger)
        {
            // Binding
            _host = host;
            _ytReciever = ytReciever;
            _consoleLogger = consoleLogger;
            _telegramLogger = telegramLogger;

            // Events
            _host.OnMessage += OnUserSendMessage;
        }

        public void Init()
        {
            _host.Start();
        }

        // Delegates
        private async void OnUserSendMessage(ITelegramBotClient client, Update update)
        {
            string? userMessage = update?.Message?.Text;
            if (userMessage == "/start")
            {
                await _telegramLogger.Log("Send youtube video link", client, update.Message.Chat.Id);
                return;
            }

            await Task.Delay(1000);
            DeleteMessage(client, update.Message.Chat.Id, update.Message.Id); // Delete user message

            // Bot loading message
            var loadingBotMessage = await _telegramLogger.Log("Loading info...", client, update.Message.Chat.Id);

            // Loading info
            await SendPreviewInfo(client, update);

            // Deleting bot loading message
            await client.DeleteMessage(update.Message.Chat.Id, loadingBotMessage.MessageId);
        }


        // Functions

        private void DeleteMessage(ITelegramBotClient client, ChatId chatID, int messageID)
        {
            client.DeleteMessage(chatID, messageID);
        }

        private async Task SendPreviewInfo(ITelegramBotClient client, Update update)
        {
            // Get async video
            string url = update.Message.Text;
            var chatID = update.Message.Chat.Id;

            VideoInfo? videoInfo = await _ytReciever.GetVideoInfoAsync(url);
            if (videoInfo != null)
            {

                using var imageStream = await _ytReciever.GetVideoPreviewStreamAsync(url);
                if (imageStream != null)
                {
                    // Image
                    InputFile videoImage = InputFile.FromStream(imageStream);


                    // Text aption
                    string textCaption =
                        $"{videoInfo?.Title}\n" +
                        $"Author: {videoInfo?.Channel}\n" +
                        $"Video duration: {videoInfo?.Duration}\n\n" +
                        videoInfo?.Description +
                        "...";

                    // Sending to chat
                    await client.SendPhoto(chatID, videoImage, textCaption);

                }
                else
                {
                    await _telegramLogger.Log("Preview download failed", client, chatID, LogStatus.Error);
                    _consoleLogger.Log("Preview stream failde", LogStatus.Error);
                }
            }
            else
                await _telegramLogger.Log("It's not a link", client, chatID);
        }
    }
}
