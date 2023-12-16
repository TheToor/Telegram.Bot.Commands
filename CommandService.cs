using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Commands.Events;
using Telegram.Bot.Types;
using Telegram.Bot.Commands.Models;

namespace Telegram.Bot.Commands
{
    public class CommandService
    {
        public bool Enabled { get; set; } = true;

        public List<Tuple<string, string?>> RegisteredCommands { get; set; } = new();

        public event EventHandler<CommandExceptionEventArgs>? OnCommandException;
        public event EventHandler<CommandNotFoundEventArgs>? OnCommandNotFound;

        private readonly ILogger<CommandService> _logger;
        private readonly IServiceProvider _serviceProvider;

        private readonly Dictionary<string, ICommand> _commands = new();
        private readonly Dictionary<string, IReplyCallback> _replyCommands = new();

        private readonly List<string> _debugCommands = new();

        private readonly List<MessageData> _messageData = new();

        public CommandService(ILogger<CommandService> logger, IServiceProvider provider)
        {
            _logger = logger;
            _serviceProvider = provider;
        }

        public void LoadCommands()
        {
            _logger.LogTrace("CommandManager starting ...");

            var commandAttribute = typeof(Command);
            var commandInterface = typeof(ICommand);
            var replyInterface = typeof(IReplyCallback);

            var assembly = Assembly.GetExecutingAssembly();

            foreach (var type in assembly.GetTypes())
            {
                try
                {
                    if (type.GetCustomAttribute(commandAttribute) is not Command attribute)
                    {
                        // Not a valid command
                        continue;
                    }

                    _logger.LogTrace("Found command attribute on {type}", type.Name);

                    if (!commandInterface.IsAssignableFrom(type))
                    {
                        _logger.LogWarning("Skipping {type}: Not implementing required ICommand interface", type.Name);
                        continue;
                    }

                    if (_commands.ContainsKey(attribute.Name))
                    {
                        _logger.LogWarning("Skipping {type}: Command '{attribute}' already exists", type.Name, attribute.Name);
                        continue;
                    }

                    var commandName = attribute.Name;
                    if (ActivatorUtilities.CreateInstance(_serviceProvider, type) is not ICommand instance)
                    {
                        continue;
                    }

                    if (replyInterface.IsAssignableFrom(type))
                    {
                        var identifier = ((IReplyCallback)instance).UniqueIdentifier;
                        if (_replyCommands.ContainsKey(identifier))
                        {
                            _logger.LogWarning("Skipping ForceReply implementation of {type}: UniqueIdentifier '{identifier}' already exists", type.Name, identifier);
                        }
                        else
                        {
                            _replyCommands.Add(identifier, (IReplyCallback)instance);
                        }
                    }

                    _commands.Add(commandName, instance);
                    if (attribute.DebugCommand)
                    {
                        _debugCommands.Add(attribute.Name);
                    }

                    RegisteredCommands.Add(new Tuple<string, string?>(attribute.Name, attribute.Description));

                    _logger.LogInformation("Added Command '{commandName}'", commandName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process type {type}", type.Name);
                }
            }

            _logger.LogInformation("CommandManager successfully loaded {commandCount} commands", _commands.Count);
        }

        private static CommandParameters GetParameters(string commandLine)
        {
            var split = commandLine.Split(' ');
            if (commandLine.Contains('_'))
            {
                split = commandLine.Split('_');
            }

            var arguments = split.Length switch
            {
                2 => new[] { split[1] },
                > 2 => split.Skip(1).ToArray(),
                _ => Array.Empty<string>()
            };

            return new CommandParameters
            (
                // Remove leading '/' and normalize to lowercase
                commandName: split[0].Substring(1, split[0].Length - 1).ToLower(),
                arguments: arguments
            );
        }

        public void ExpectForceReply(IReplyCallback instance, Message message)
        {
            var messageData = new MessageData(message);
            lock (_messageData)
            {
                if (!_messageData.Contains(messageData))
                {
                    messageData.ForceReplyInstance = instance;
                    _messageData.Add(messageData);
                }
                else
                {
                    _messageData.First(m => m.Chat == message.Chat.Id && m.Message == message.MessageId).ForceReplyInstance = instance;
                }
            }
        }

        public void ExpectQueryCallback(IQueryCallback instance, Message message)
        {
            var messageData = new MessageData(message);
            lock (_messageData)
            {
                if (!_messageData.Contains(messageData))
                {
                    messageData.QueryCallbackInstance = instance;
                    _messageData.Add(messageData);
                }
                else
                {
                    _messageData.First(m => m.Chat == message.Chat.Id && m.Message == message.MessageId).QueryCallbackInstance = instance;
                }
            }
        }

        private async Task<bool> ProcessForceReply(TelegramBotClient client, Message message)
        {
            if (message.ReplyToMessage == null)
            {
                return false;
            }

            MessageData? messageData;
            lock (_messageData)
            {
                messageData = _messageData.FirstOrDefault(m => m.Chat == message.Chat.Id && m.Message == message.ReplyToMessage.MessageId);
                if (messageData?.ForceReplyInstance == null)
                {
                    return false;
                }
            }

            try
            {
                return await messageData.ForceReplyInstance.OnReplyReceived(client, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process force reply message");
                return false;
            }
            finally
            {
                lock (_messageData)
                {
                    _messageData.Remove(messageData);
                }
            }
        }

        private async Task<bool> ProcessCommandMessage(TelegramBotClient client, Message message)
        {
            var command = GetParameters(message.Text!);
            try
            {
                if (!Enabled && !_debugCommands.Contains(command.CommandName))
                {
                    return false;
                }

                if (_commands.TryGetValue(command.CommandName, out var commandInstance))
                {
                    return await commandInstance.Execute(client, message, command.Arguments);
                }

                OnCommandNotFound?.BeginInvoke(this, new CommandNotFoundEventArgs(command), OnCommandNotFound.EndInvoke, null);
                return false;
            }
            catch (Exception ex)
            {
                OnCommandException?.BeginInvoke(this, new CommandExceptionEventArgs(command, ex), OnCommandException.EndInvoke, null);
                return false;
            }
        }

        public async Task<bool> ProcessMessage(TelegramBotClient client, Message message)
        {
            if (string.IsNullOrEmpty(message.Text))
            {
                return false;
            }

            if (!message.Text.StartsWith("/"))
            {
                return await ProcessForceReply(client, message);
            }

            return await ProcessCommandMessage(client, message);
        }

        public async Task<bool> ProcessCallbackQuery(TelegramBotClient client, CallbackQuery callbackQuery)
        {
            var data = callbackQuery.Data;
            var message = callbackQuery.Message;

            if (message == null)
            {
                return false;
            }

            MessageData? messageData;
            lock (_messageData)
            {
                messageData = _messageData.FirstOrDefault(m => m.Chat == message.Chat.Id && m.Message == message.MessageId);
                if (messageData == null)
                {
                    return false;
                }
            }

            if (messageData.QueryCallbackInstance != null)
            {
                return await messageData.QueryCallbackInstance.OnCallbackQueryReceived(client, callbackQuery);
            }

            lock (_messageData)
            {
                _messageData.Remove(messageData);
            }

            return false;

        }
    }
}
