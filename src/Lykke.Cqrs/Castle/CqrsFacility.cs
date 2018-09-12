using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Castle.Core;
using Castle.MicroKernel;
using Castle.MicroKernel.Context;
using Castle.MicroKernel.Facilities;
using Castle.MicroKernel.Registration;
using Lykke.Cqrs.Configuration;
using Lykke.Cqrs.Configuration.BoundedContext;
using IRegistration = Lykke.Cqrs.Configuration.IRegistration;

namespace Lykke.Cqrs.Castle
{
    public class CqrsFacility : AbstractFacility, ICqrsEngineBootstrapper
    {
        private readonly string m_EngineComponetName = Guid.NewGuid().ToString();
        private readonly Dictionary<IHandler, Action<IHandler>> m_WaitList = new Dictionary<IHandler, Action<IHandler>>();

        private static bool m_CreateMissingEndpoints = false;

        private IRegistration[] m_BoundedContexts = new IRegistration[0];
        private bool m_InMemory = false;
        private ICqrsEngine m_CqrsEngine;

        public bool HasEventStore { get; set; }

        public CqrsFacility RunInMemory()
        {
            m_InMemory = true;
            return this;
        }

        public CqrsFacility Contexts(params IRegistration[] boundedContexts)
        {
            m_BoundedContexts = boundedContexts;
            return this;
        }

        protected override void Init()
        {
            Kernel.Register(Component.For<ICqrsEngineBootstrapper>().Instance(this));
            Kernel.ComponentRegistered += OnComponentRegistered;
            Kernel.HandlersChanged += (ref bool changed) => ProcessWaitList();
        }

        public CqrsFacility CreateMissingEndpoints(bool createMissingEndpoints = true)
        {
            m_CreateMissingEndpoints = createMissingEndpoints;
            return this;
        }

        private void ProcessWaitList()
        {
            foreach (var pair in m_WaitList.ToArray().Where(pair => pair.Key.CurrentState == HandlerState.Valid && pair.Key.TryResolve(CreationContext.CreateEmpty()) != null))
            {
                pair.Value(pair.Key);
                m_WaitList.Remove(pair.Key);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void OnComponentRegistered(string key, IHandler handler)
        {
            if (handler.ComponentModel.Name == m_EngineComponetName)
            {
                var dependencyModels = m_BoundedContexts.SelectMany(bc => bc.Dependencies).Select(d => new DependencyModel(d.Name, d, false));
                foreach (var dependencyModel in dependencyModels)
                {
                    handler.ComponentModel.Dependencies.Add(dependencyModel);
                }
                return;
            }

            var extendedProps = handler.ComponentModel.ExtendedProperties;

            var dependsOnBoundedContextRepository = extendedProps["dependsOnBoundedContextRepository"];
            if (dependsOnBoundedContextRepository != null)
            {
                handler.ComponentModel.Dependencies.Add(new DependencyModel(m_EngineComponetName, typeof(ICqrsEngine), false));
                return;
            }

            var isCommandsHandler = (bool)(extendedProps["IsCommandsHandler"] ?? false);
            var isProjection = (bool)(extendedProps["IsProjection"] ?? false);
            if (isCommandsHandler && isProjection)
                throw new InvalidOperationException("Component can not be projection and commands handler simultaneousely");

            if (isProjection)
            {
                var projectedBoundContext = (string)(extendedProps["ProjectedBoundContext"]);
                var boundContext = (string)(extendedProps["BoundContext"]);

                var registration = FindBoundedContext(boundContext);
                if (registration == null)
                    throw new ComponentRegistrationException(string.Format("Bounded context {0} was not registered", boundContext));

                var batchSize = (int)(extendedProps["BatchSize"]);
                var applyTimeoutInSeconds = (int)(extendedProps["ApplyTimeoutInSeconds"]);
                var beforeBatchApply = (Func<object, object>)(extendedProps["BeforeBatchApply"]);
                var afterBatchApply = (Action<object,object>)(extendedProps["AfterBatchApply"]);
                var batchContextType = (Type)(extendedProps["BatchContextType"]);

                //TODO: decide which service to use
                registration.WithProjection(
                    handler.ComponentModel.Services.First(),
                    projectedBoundContext,
                    batchSize,
                    applyTimeoutInSeconds,
                    batchContextType,
                    beforeBatchApply,
                    afterBatchApply);
            }

            if (isCommandsHandler)
            {
                var commandsHandlerFor = (string)(extendedProps["CommandsHandlerFor"]);
                var registration = FindBoundedContext(commandsHandlerFor);
                if (registration == null)
                    throw new ComponentRegistrationException(string.Format("Bounded context {0} was not registered", commandsHandlerFor));

                registration.WithCommandsHandler(handler.ComponentModel.Services.First());
            }

            var isProcess = (bool)(extendedProps["isProcess"] ?? false);
            if (isProcess)
            {
                var processFor = (string)(extendedProps["ProcessFor"]);
                var registration = FindBoundedContext(processFor);

                if (registration == null)
                    throw new ComponentRegistrationException(string.Format("Bounded context {0} was not registered", processFor));

                registration.WithProcess(handler.ComponentModel.Services.First());
            }
        }

        private IBoundedContextRegistration FindBoundedContext(string name)
        {
            return m_BoundedContexts
                .Where(r => r is IBoundedContextRegistration)
                .Cast<IBoundedContextRegistration>()
                .Concat(m_BoundedContexts
                    .Where(r => r is IRegistrationWrapper<IBoundedContextRegistration>)
                    .Select(r => (r as IRegistrationWrapper<IBoundedContextRegistration>).Registration))
                .FirstOrDefault(bc => bc.Name == name);
        }

        public void Start()
        {
            var engineReg = m_InMemory
                ? Component.For<ICqrsEngine>().ImplementedBy<InMemoryCqrsEngine>()
                : Component.For<ICqrsEngine>().ImplementedBy<CqrsEngine>().DependsOn(new { createMissingEndpoints = m_CreateMissingEndpoints });
            Kernel.Register(Component.For<IDependencyResolver>().ImplementedBy<CastleDependencyResolver>());

            Kernel.Register(engineReg.Named(m_EngineComponetName).DependsOn(new
            {
                registrations = m_BoundedContexts.ToArray()
            }));
            Kernel.Register(Component.For<ICommandSender>().ImplementedBy<CommandSender>().DependsOn(new { kernel = Kernel }));

            m_CqrsEngine = Kernel.Resolve<ICqrsEngine>();
        }
    }
}