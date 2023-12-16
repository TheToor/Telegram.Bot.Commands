namespace Telegram.Bot.Commands.Models
{
    [AttributeUsage(AttributeTargets.Class)]
    public class Command : Attribute
    {
        public string Name { get; private set; }
        public string? Description { get; set; }
        public bool DebugCommand { get; set; } = false;

        public Command(string name)
        {
            Name = name;
        }
    }
}
