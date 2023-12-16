using Telegram.Bot.Commands.Models;

namespace Telegram.Bot.Commands.Events
{
    public class CommandNotFoundEventArgs : EventArgs
    {
        public CommandParameters CommandParameters { get; set; }

        public CommandNotFoundEventArgs(CommandParameters commandParameters)
        {
            CommandParameters = commandParameters;
        }
    }
}
