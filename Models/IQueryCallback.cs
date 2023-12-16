using Telegram.Bot.Types;

namespace Telegram.Bot.Commands.Models
{
    public interface IQueryCallback
    {
        Task<bool> OnCallbackQueryReceived(TelegramBotClient client, CallbackQuery callbackQuery);
    }
}
