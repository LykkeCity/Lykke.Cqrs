using System;
using System.Linq.Expressions;

namespace Lykke.Cqrs
{
    internal abstract class OptionalParameterBase
    {
        public object Value { get; protected set; }
        public Type Type { get; protected set; }
        public string Name { get; protected set; }
        public virtual Expression ValueExpression
        {
            get { return Expression.Constant(Value); }
        }
    }
}