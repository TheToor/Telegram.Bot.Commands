using Telegram.Bot.Types;

namespace Telegram.Bot.Commands.Models
{
    public interface IReplyCallback
    {
        public string UniqueIdentifier { get; }
        Task<bool> OnReplyReceived(TelegramBotClient client, Message message);
    }
}
