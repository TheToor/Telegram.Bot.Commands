namespace Telegram.Bot.Commands.Models
{
    public class CommandParameters
    {
        public string CommandName { get; set; }
        public string[] Arguments { get; set; }

        public CommandParameters(string commandName, string[] arguments)
        {
            CommandName = commandName;
            Arguments = arguments;
        }
    }
}
