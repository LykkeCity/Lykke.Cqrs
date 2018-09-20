using System;

namespace Lykke.Cqrs
{
    internal class FactoryParameter<T> : OptionalParameterBase
    {
        public FactoryParameter(Func<T> func)
        {
            Value = func;
            Type = typeof(T); 
        }
    }
}