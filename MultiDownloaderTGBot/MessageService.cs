using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Utils;

namespace Services
{
    public static class MessageService
    {
        public static async Task<Message?> Send(
            ITelegramBotClient client, ChatId chatId, Message message, ILogger logger)
        {
            try
            {
                return await SendMessage(client, chatId, message, GetMessageType(message));
            }
            catch (Exception ex)
            {
                logger.Log(ex.ToString(), ELogStatus.Warning);
            }
            return null;
        }

        public static async Task<Message> SendButtonMenu(
            ITelegramBotClient client,
            ChatId chatId,
            InputFile inputFile,
            string caption,
            InlineKeyboardMarkup inlineKeyboard)
        {
            return await client.SendPhoto(
                chatId,
                inputFile,
                caption,
                replyMarkup: inlineKeyboard);
        }

        public static async Task Remove
            (ITelegramBotClient client, ChatId chatId, Message message, ILogger logger)
        {
            try
            {
                await client.DeleteMessage(chatId, message.Id);
            }
            catch (Exception ex)
            {
                logger.Log(ex.ToString(), ELogStatus.Warning);
            }
        }


        // PRIVATE

        private static EMessageType? GetMessageType(Message message)
        {
            if (message.Text != null) return EMessageType.Message;
            else if (message.Photo != null && message.Photo.Length > 0) return EMessageType.Photo;
            else if (message.Video != null) return EMessageType.Video;
            else if (message.Animation != null) return EMessageType.Animation;
            else if (message.Audio != null) return EMessageType.Audio;
            else if (message.Voice != null) return EMessageType.Voice;
            else if (message.Document != null) return EMessageType.Document;
            else if (message.Sticker != null) return EMessageType.Sticker;
            else return null;
        }

        private static async Task<Message?> SendMessage(ITelegramBotClient client, ChatId chatId,
            Message message, EMessageType? type)
        {
            if (type is null) return null;

            return type switch
            {
                EMessageType.Animation => await client.SendAnimation(chatId, message.Animation!,
                                        caption: message.Caption, replyMarkup: message.ReplyMarkup),

                EMessageType.Audio => await client.SendAudio(chatId, message.Audio!,
                                        caption: message.Caption, replyMarkup: message.ReplyMarkup),

                EMessageType.Document => await client.SendDocument(chatId, message.Document!,
                                        caption: message.Caption, replyMarkup: message.ReplyMarkup),

                EMessageType.Message => await client.SendMessage(chatId, message.Text!,
                                        replyMarkup: message.ReplyMarkup),

                EMessageType.Photo => await client.SendPhoto(chatId, message.Photo![^1],
                                        caption: message.Caption, replyMarkup: message.ReplyMarkup),

                EMessageType.Sticker => await client.SendSticker(chatId, message.Sticker!,
                                        replyMarkup: message.ReplyMarkup),

                EMessageType.Video => await client.SendVideo(chatId, message.Video!,
                                        caption: message.Caption, replyMarkup: message.ReplyMarkup),

                EMessageType.Voice => await client.SendVoice(chatId, message.Voice!,
                                        caption: message.Caption, replyMarkup: message.ReplyMarkup),

                _ => null,
            };
        }
    }
}
