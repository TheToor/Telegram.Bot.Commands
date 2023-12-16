using Telegram.Bot.Commands.Models;

namespace Telegram.Bot.Commands.Events
{
    public class CommandExceptionEventArgs : EventArgs
    {
        public CommandParameters CommandParameters { get; set; }
        public Exception Exception { get; set; }

        public CommandExceptionEventArgs(CommandParameters commandParameters, Exception exception)
        {
            CommandParameters = commandParameters;
            Exception = exception;
        }
    }
}
