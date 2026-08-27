using Microsoft.Extensions.Hosting;

using Managers;
using Services;
using Utils;
using Microsoft.Extensions.DependencyInjection;

namespace TelegramBot
{
    public class Program
    {
        
        static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder().ConfigureServices((context, services) =>
            {
                var token = context.Configuration["BOT_TOKEN"];

                if (string.IsNullOrWhiteSpace(token))
                    throw new InvalidOperationException("Configuration value 'BOT_TOKEN' was not found");

                services.AddSingleton<ConsoleLogger>();

                services.AddSingleton<FileLogger>();

                services.AddSingleton<ILogger>(sp =>
                sp.GetRequiredService<FileLogger>());

                services.AddSingleton(sp => 
                new TgHost(token, sp.GetRequiredService<ILogger>()));

                services.AddSingleton<DownloadManager>();

                services.AddSingleton<TelegramDownloadService>();

                services.AddSingleton<Bot>();
            }).Build();

            var bot = host.Services.GetRequiredService<Bot>();

            await bot.Init(); // Bot start

            Console.ReadLine();
        }
    }
}