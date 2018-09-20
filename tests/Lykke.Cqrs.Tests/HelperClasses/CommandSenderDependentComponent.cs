namespace Lykke.Cqrs.Tests
{
    public class CommandSenderDependentComponent
    {
        public ICommandSender CommandSender { get; private set; }

        public CommandSenderDependentComponent(ICommandSender commandSender)
        {
            CommandSender = commandSender;
        }
    }
}