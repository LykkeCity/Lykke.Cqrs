using System;
using Autofac;
using JetBrains.Annotations;

namespace Lykke.Cqrs
{
    /// <summary>
    /// Dependency resolver for Autofac container.
    /// </summary>
    [PublicAPI]
    public class AutofacDependencyResolver : IDependencyResolver
    {
        private readonly IComponentContext _context;

        /// <summary>
        /// C-tor
        /// </summary>
        /// <param name="context">Autofac component context.</param>
        public AutofacDependencyResolver([NotNull] IComponentContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <inheritdoc cref="IDependencyResolver"/>>
        public object GetService(Type type)
        {
            return _context.Resolve(type);
        }

        /// <inheritdoc cref="IDependencyResolver"/>>
        public bool HasService(Type type)
        {
            return _context.IsRegistered(type);
        }
    }
}
