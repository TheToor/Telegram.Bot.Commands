using Telegram.Bot.Types;

namespace Telegram.Bot.Commands
{
    internal interface ICommand
    {
        Task<bool> Execute(TelegramBotClient client, Message message, string[] arguments);
    }
}
