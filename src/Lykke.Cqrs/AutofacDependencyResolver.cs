using Autofac;
using JetBrains.Annotations;
using System;

namespace Lykke.Cqrs
{
    [PublicAPI]
    public class AutofacDependencyResolver : IDependencyResolver
    {
        private readonly IComponentContext _context;

        public AutofacDependencyResolver([NotNull] IComponentContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public object GetService(Type type)
        {
            return _context.Resolve(type);
        }

        public bool HasService(Type type)
        {
            return _context.IsRegistered(type);
        }
    }
}
