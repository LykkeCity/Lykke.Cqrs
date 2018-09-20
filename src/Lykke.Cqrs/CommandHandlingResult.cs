using System;

namespace Lykke.Cqrs
{
    //TODO[kn]: rename to EventHandlingResult
    public class CommandHandlingResult
    {
        public long RetryDelay { get; set; }
        public bool Retry { get; set; }

        public static CommandHandlingResult Ok()
        {
            return new CommandHandlingResult { Retry = false, RetryDelay = 0 };
        }

        public static CommandHandlingResult Fail(TimeSpan delay)
        {
            return new CommandHandlingResult { Retry = true, RetryDelay = (int)delay.TotalMilliseconds };
        }
    }
}