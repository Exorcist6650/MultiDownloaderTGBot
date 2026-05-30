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
            client.DeleteMessage(update.Message.Chat.Id, update.Message.Id);


            // Loading and sending info
            await LoadAndSendPreviewInfo(client, update);
        }


        // Functions
        private async Task<bool> LoadAndSendPreviewInfo(ITelegramBotClient client, Update update)
        {
            // Get async video
            string url = update.Message.Text;
            var chatID = update.Message.Chat.Id;

            VideoInfo? videoInfo = await _ytReciever.GetVideoInfoAsync(url);
            if (videoInfo != null)
            {
                // Preview image stream
                using var memoryStream = await _ytReciever.GetVideoPreviewStreamAsync(url);
                if (memoryStream != null)
                {
                    // Text сaption
                    string textCaption =
                        $"{videoInfo?.Title}\n" +
                        $"Author: {videoInfo?.Channel}\n" +
                        $"Video duration: {videoInfo?.Duration}\n\n" +
                        videoInfo?.Description +
                        "...";

                    // Sending to chat
                    try
                    {
                        await client.SendPhoto(chatID, InputFile.FromStream(memoryStream), textCaption);
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
                    await _telegramLogger.Log("Preview download failed", client, chatID, LogStatus.Error);
                    _consoleLogger.Log("Preview stream failde", LogStatus.Error);
                    return false;
                }
            }
            else
            {
                await _telegramLogger.Log("It's not a link", client, chatID);
                return false;
            }
        }
    }
}
