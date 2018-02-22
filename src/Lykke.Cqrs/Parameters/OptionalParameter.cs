namespace Lykke.Cqrs
{
    internal class OptionalParameter<T> : OptionalParameterBase
    {
        public OptionalParameter(string name, T value)
        {
            Name = name;
            Value = value;
            Type = typeof (T);
        }

        public OptionalParameter(T value)
        {
            Value = value;
            Type = typeof (T);
        }
    }
}