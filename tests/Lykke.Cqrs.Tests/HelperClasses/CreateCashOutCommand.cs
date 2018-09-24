using ProtoBuf;

namespace Lykke.Cqrs.Tests
{
    [ProtoContract]
    public class CreateCashOutCommand
    {
        [ProtoMember(1)]
        public string Payload { get; set; }
    }
}