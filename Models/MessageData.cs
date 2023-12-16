using Telegram.Bot.Types;

namespace Telegram.Bot.Commands.Models
{
    internal class MessageData
    {
        internal int Message { get; }
        internal long Chat { get; }

        public IReplyCallback? ForceReplyInstance { get; set; }

        public IQueryCallback? QueryCallbackInstance { get; set; }

        internal MessageData(Message message)
        {
            Message = message.MessageId;
            Chat = message.Chat.Id;
        }

        protected bool Equals(MessageData other)
        {
            return Message == other.Message && Chat == other.Chat;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Message, Chat);
        }
    }
}
