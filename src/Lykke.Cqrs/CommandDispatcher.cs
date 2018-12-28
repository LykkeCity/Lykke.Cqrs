using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Common;
using Common.Log;
using Lykke.Common.Log;
using Lykke.Messaging.Contract;
using Lykke.Cqrs.InfrastructureCommands;
using Lykke.Cqrs.Middleware;
using Lykke.Cqrs.Utils;

namespace Lykke.Cqrs
{
    internal class CommandDispatcher : IDisposable
    {
        private readonly Dictionary<Type, CommandHandlerInfo> _handlers = new Dictionary<Type, CommandHandlerInfo>();
        private readonly string _boundedContext;
        private readonly ILog _log;
        private readonly CommandInterceptorsQueue _commandInterceptorsProcessor;
        private readonly MethodInfo _getAwaiterInfo;
        private readonly long _failedCommandRetryDelay;

        internal const long FailedCommandRetryDelay = 60000;

        [Obsolete]
        internal CommandDispatcher(
            ILog log,
            string boundedContext,
            CommandInterceptorsQueue commandInterceptorsProcessor,
            long failedCommandRetryDelay = FailedCommandRetryDelay)
        {
            _log = log;
            _commandInterceptorsProcessor = commandInterceptorsProcessor;
            _failedCommandRetryDelay = failedCommandRetryDelay;
            _boundedContext = boundedContext;

            var taskMethods = typeof(Task<CommandHandlingResult>).GetMethods(BindingFlags.Public | BindingFlags.Instance);
            var awaiterResultType = typeof(TaskAwaiter<CommandHandlingResult>);
            _getAwaiterInfo = taskMethods.First(m => m.Name == "GetAwaiter" && m.ReturnType == awaiterResultType);
        }

        internal CommandDispatcher(
            ILogFactory logFactory,
            string boundedContext,
            CommandInterceptorsQueue commandInterceptorsProcessor = null,
            long failedCommandRetryDelay = 60000)
        {
            _log = logFactory.CreateLog(this);
            _commandInterceptorsProcessor = commandInterceptorsProcessor ?? new CommandInterceptorsQueue();
            _failedCommandRetryDelay = failedCommandRetryDelay;
            _boundedContext = boundedContext;

            var taskMethods = typeof(Task<CommandHandlingResult>).GetMethods(BindingFlags.Public | BindingFlags.Instance);
            var awaiterResultType = typeof(TaskAwaiter<CommandHandlingResult>);
            _getAwaiterInfo = taskMethods.First(m => m.Name == "GetAwaiter" && m.ReturnType == awaiterResultType);
        }

        public void Wire(object o, params OptionalParameterBase[] parameters)
        {
            if (o == null)
                throw new ArgumentNullException(nameof(o));

            parameters = parameters
                .Concat(new OptionalParameterBase[] { new OptionalParameter<string>("boundedContext", _boundedContext) })
                .ToArray();

            var handleMethods = o.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.Name == "Handle" &&
                    !m.IsGenericMethod &&
                    m.GetParameters().Length > 0 &&
                    !m.GetParameters().First().ParameterType.IsInterface)
                .Select(m => new
                {
                    method = m,
                    returnType = m.ReturnType,
                    commandType = m.GetParameters().First().ParameterType,
                    callParameters = m.GetParameters().Skip(1).Select(p => new
                    {
                        parameter = p,
                        optionalParameter = parameters.FirstOrDefault(par => par.Name == p.Name || par.Name == null && p.ParameterType == par.Type),
                    })
                })
                .Where(m => m.callParameters.All(p => p.parameter != null));

            var eventPublisher = (IEventPublisher)parameters?.FirstOrDefault(p => p.Type == typeof(IEventPublisher))?.Value;

            foreach (var method in handleMethods)
            {
                RegisterHandler(
                    method.commandType,
                    o,
                    method.callParameters.ToDictionary(p => p.parameter, p => p.optionalParameter?.Value),
                    eventPublisher,
                    method.returnType);
            }
        }

        internal List<Type> GetUnhandledCommandTypes(Type[] commandTypes)
        {
            var notHandledCommandTypes = new List<Type>();
            foreach (var commandType in commandTypes)
            {
                if (!_handlers.ContainsKey(commandType))
                    notHandledCommandTypes.Add(commandType);
            }

            return notHandledCommandTypes;
        }

        private Expression InvokeFunc(object o)
        {
            return Expression.Call(Expression.Constant(o), o.GetType().GetMethod("Invoke"));
        }

        private bool IsFunc(Type type)
        {
            return (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Func<>));
        }

        private void RegisterHandler(
            Type commandType,
            object o,
            Dictionary<ParameterInfo, object> optionalParameters,
            IEventPublisher eventPublisher,
            Type returnType)
        {
            var isRoutedCommandHandler = commandType.IsGenericType && commandType.GetGenericTypeDefinition() == typeof (RoutedCommand<>);
            var command = Expression.Parameter(typeof(object));
            var eventPublisherParam = Expression.Parameter(typeof(IEventPublisher));
            var endpoint = Expression.Parameter(typeof(Endpoint));
            var route = Expression.Parameter(typeof(string));

            Expression commandParameter;
            Type handledType;
            if (!isRoutedCommandHandler)
            {
                commandParameter = Expression.Convert(command, commandType);
                handledType = commandType;
            }
            else
            {
                handledType = commandType.GetGenericArguments()[0];
                var ctor = commandType.GetConstructor(new[] { handledType, typeof(Endpoint) ,typeof(string)});
                commandParameter = Expression.New(ctor, Expression.Convert(command, handledType), endpoint, route);
            }

            if (_handlers.ContainsKey(handledType))
                throw new InvalidOperationException(
                    $"Command handler for {commandType} is already registered in bound context {_boundedContext}. Can not register {o.GetType().Name} as handler for it");

            // prepare variables expressions
            var variables = optionalParameters
                 .Where(p => p.Value != null && IsFunc(p.Value.GetType()))
                 .ToDictionary(p => p.Key.Name, p => Expression.Variable(p.Key.ParameterType, p.Key.Name));

            //prepare parameters expression to make handle call
            var parameters = new[] { commandParameter }
                 .Concat(optionalParameters.Select(p => 
                     variables.ContainsKey(p.Key.Name)
                         ? variables[p.Key.Name]
                         : (p.Key.ParameterType == typeof(IEventPublisher)
                               ? eventPublisherParam
                               : (Expression)Expression.Constant(p.Value, p.Key.ParameterType))))
                .ToArray();

            var handleCall = Expression.Call(Expression.Constant(o), "Handle", null, parameters);
            MethodCallExpression resultCall;
            if (returnType == typeof(Task<CommandHandlingResult>))
            {
                var awaiterCall = Expression.Call(handleCall, _getAwaiterInfo);
                resultCall = Expression.Call(awaiterCall, "GetResult", null);
            }
            else if (returnType == typeof(Task))
            {
                var awaiterCall = Expression.Call(handleCall, "GetAwaiter", null);
                resultCall = Expression.Call(awaiterCall, "GetResult", null);
            }
            else
            {
                resultCall = handleCall;
            }

            var disposableType = typeof (IDisposable);
            var call = Expression.Block(
                 variables.Values.AsEnumerable(), //declare variables to populate from func factoreis
                 variables
                     .Select(p => Expression.Assign(p.Value, InvokeFunc(optionalParameters.Single(x => x.Key.Name == p.Key).Value))) // invoke func and assign result to variable
                     .Cast<Expression>()
                     .Concat(new[]
                         {
                            Expression.TryFinally(
                                resultCall,
                                Expression.Block(variables
                                    .Select(v =>
                                        Expression.IfThen( //dispose variable if disposable and not null
                                            Expression.And(Expression.NotEqual(v.Value, Expression.Constant(null)), Expression.TypeIs(v.Value, disposableType)),
                                            Expression.Call(Expression.Convert(v.Value, disposableType) , disposableType.GetMethod("Dispose"))))
                                    .Cast<Expression>()
                                    .DefaultIfEmpty(Expression.Empty())))
                         }));

            if (returnType != typeof(Task<CommandHandlingResult>) && returnType != typeof(CommandHandlingResult))
            {
                var returnLabel = Expression.Label(
                    Expression.Label(typeof(CommandHandlingResult)),
                    Expression.Constant(new CommandHandlingResult { Retry = false, RetryDelay = 0 }));
                call = Expression.Block(call, returnLabel);
            }
            var lambda = (Expression<Func<object, IEventPublisher, Endpoint, string, CommandHandlingResult>>)Expression.Lambda(
                call,
                command,
                eventPublisherParam,
                endpoint,
                route);

            _handlers.Add(
                handledType,
                new CommandHandlerInfo(
                    o,
                    eventPublisher,
                    lambda.Compile()
                ));
        }

        public void Dispatch(
            object command,
            AcknowledgeDelegate acknowledge,
            Endpoint commandOriginEndpoint,
            string route)
        {
            if (!_handlers.TryGetValue(command.GetType(), out var handlerInfo))
            {
                _log.WriteWarning(
                    nameof(CommandDispatcher),
                    nameof(Dispatch),
                    $"Failed to handle command of type {command.GetType().Name} in bound context {_boundedContext}, no handler was registered for it");
                acknowledge(_failedCommandRetryDelay, false);
                return;
            }

            Handle(
                command,
                acknowledge,
                handlerInfo,
                commandOriginEndpoint,
                route);
        }

        private void Handle(
            object command,
            AcknowledgeDelegate acknowledge,
            CommandHandlerInfo commandHandlerInfo,
            Endpoint commandOriginEndpoint,
            string route)
        {
            string commandType = command?.GetType().Name ?? "Unknown command type";

            var telemtryOperation = TelemetryHelper.InitTelemetryOperation(
                "Cqrs handle command",
                commandHandlerInfo.HandlerTypeName,
                commandType,
                _boundedContext);
            try
            {
                var result = _commandInterceptorsProcessor.RunInterceptorsAsync(
                    command,
                    commandHandlerInfo.HandlerObject,
                    commandHandlerInfo.EventPublisher,
                    (c, e) => commandHandlerInfo.Handler(c, e, commandOriginEndpoint, route))
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();
                acknowledge(result.RetryDelay, !result.Retry);
            }
            catch (Exception e)
            {
                _log.WriteError(commandHandlerInfo.HandlerTypeName, commandType, e);

                acknowledge(_failedCommandRetryDelay, false);

                TelemetryHelper.SubmitException(telemtryOperation, e);
            }
            finally
            {
                TelemetryHelper.SubmitOperationResult(telemtryOperation);
            }
        }

        public void Dispose()
        {
        }
    }
}