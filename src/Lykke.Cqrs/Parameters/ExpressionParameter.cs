using System;
using System.Linq.Expressions;

namespace Lykke.Cqrs
{
    internal class ExpressionParameter : OptionalParameterBase
    {
        public ExpressionParameter(string name, Type type)
        {
            Type = type;
            Name = name;
            Parameter = Expression.Parameter(typeof(object));
        }

        public ParameterExpression Parameter { get; set; }

        public override Expression ValueExpression
        {
            get { return Expression.Convert(Parameter, Type); }
        }
    }
}