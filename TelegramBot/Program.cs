using System;
using AngleSharp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramBot;
using YoutubeConnect;

namespace MyApp
{
    internal class Program
    {
        static string GetBotToken(string localVariableName)
        {
            var builder = new ConfigurationBuilder()
                .AddUserSecrets<Program>()
                .AddEnvironmentVariables();
            
            var config = builder.Build();
            return config[localVariableName] ?? throw new NullReferenceException($"Not find a {localVariableName} key");
        }

        static void Main(string[] args)
        {
            Host host = new Host(GetBotToken("BOT_TOKEN"), new ConsoleLogger());
            YTDownloaderBot YTBot = new YTDownloaderBot(host, 
                new YoutubeReciever(), 
                new ConsoleLogger(), 
                new TelegramLogger());

            YTBot.Init(); // Bot recieving start

            Console.ReadLine();
        }

        
        private static async void LoadVideoDataAsync(ITelegramBotClient client, Update update)
        {
            var ytReciever = new YoutubeReciever();

            VideoInfo? videoInfo = await ytReciever.GetVideoInfoAsync(update.Message.Text);

            
            if (videoInfo != null)
            {
                // Loading vidoe preview
                using var imageStream = await ytReciever.GetVideoPreviewStreamAsync(update.Message.Text);
                
                if (imageStream != null)
                {
                    var videoPreview = InputFile.FromStream(imageStream);

                    // Send message video info with picture
                    await client.SendPhoto(update.Message.Chat.Id, videoPreview, caption:
                        $"{videoInfo?.Title}\n{videoInfo?.Channel}\n{videoInfo?.Duration}\n{videoInfo?.Description[..512]}...");
                }
                else
                    await client.SendMessage(update.Message.Chat.Id, "Error occurred while download video data");
            }
            else
                await client.SendMessage(update.Message.Chat.Id, "It's not a video link!");
        }
    }
}