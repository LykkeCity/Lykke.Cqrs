using System;
using Lykke.Messaging.Configuration;
using Lykke.Messaging.Contract;

namespace Lykke.Cqrs
{
    public class DefaultEndpointProvider : IEndpointProvider
    {
        public bool Contains(string endpointName)
        {
            return false;
        }

        public Endpoint Get(string endpointName)
        {
            throw new ApplicationException(string.Format("Endpoint '{0}' not found",endpointName));
        }
    }
}