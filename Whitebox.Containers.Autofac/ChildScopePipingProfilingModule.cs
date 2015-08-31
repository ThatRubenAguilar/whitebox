using System;
using System.Diagnostics;
using Autofac;
using Autofac.Core;
using Autofac.Core.Resolving;
using Whitebox.Connector;
using Whitebox.Messages;
using Whitebox.Model;

namespace Whitebox.Containers.Autofac
{
    public class ChildScopePipingProfilingModule : Module, IStartable
    {
        readonly WhiteboxProfilingModule _rootModule;

        public ChildScopePipingProfilingModule(WhiteboxProfilingModule rootModule)
        {
            _rootModule = rootModule;
        }


        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);
            builder.RegisterInstance(this)
                .As<IStartable>()
                .OnActivated(e => e.Instance.Start(e.Context.Resolve<ILifetimeScope>()));

        }

        protected override void AttachToComponentRegistration(IComponentRegistry componentRegistry, IComponentRegistration registration)
        {
            base.AttachToComponentRegistration(componentRegistry, registration);

            var includedTypes = ModelMapper.GetReferencedTypes(registration);
            foreach (var includedType in includedTypes)
                SendTypeModelIfNeeded(includedType);
            var message = new ComponentAddedMessage(ModelMapper.GetComponentModel(registration));
            Send(message);
        }

        void SendTypeModelIfNeeded(Type type)
        {
            TypeModel typeModel;
            if (ModelMapper.GetOrAddTypeModel(type, out typeModel))
            {
                var message = new TypeDiscoveredMessage(typeModel);
                Send(message);
            }
        }

        protected override void AttachToRegistrationSource(IComponentRegistry componentRegistry, IRegistrationSource registrationSource)
        {
            base.AttachToRegistrationSource(componentRegistry, registrationSource);

            var message = new RegistrationSourceAddedMessage(ModelMapper.GetRegistrationSourceModel(registrationSource));
            Send(message);
        }

        public void Start() { }

        public void Start(ILifetimeScope childLifetimeScope)
        {
            if (childLifetimeScope == null) throw new ArgumentNullException("childLifetimeScope");
            AttachToLifetimeScope(childLifetimeScope);
        }

        void AttachToLifetimeScope(ILifetimeScope lifetimeScope, ILifetimeScope parent = null)
        {
            var lifetimeScopeModel = ModelMapper.GetLifetimeScopeModel(lifetimeScope, parent);
            var message = new LifetimeScopeBeginningMessage(lifetimeScopeModel);
            Send(message);

            lifetimeScope.CurrentScopeEnding += (s, e) =>
            {
                Send(new LifetimeScopeEndingMessage(lifetimeScopeModel.Id));
                ModelMapper.IdTracker.ForgetId(lifetimeScope);
            };

            lifetimeScope.ChildLifetimeScopeBeginning += (s, e) => AttachToLifetimeScope(e.LifetimeScope, lifetimeScope);
            lifetimeScope.ResolveOperationBeginning += (s, e) => AttachToResolveOperation(e.ResolveOperation, lifetimeScopeModel);
        }

        void AttachToResolveOperation(IResolveOperation resolveOperation, LifetimeScopeModel lifetimeScope)
        {

            var resolveOperationModel = ModelMapper.GetResolveOperationModel(resolveOperation, lifetimeScope, new StackTrace());
            Send(new ResolveOperationBeginningMessage(resolveOperationModel));
            resolveOperation.CurrentOperationEnding += (s, e) =>
            {
                var message = e.Exception != null ?
                    new ResolveOperationEndingMessage(resolveOperationModel.Id, e.Exception.GetType().AssemblyQualifiedName, e.Exception.Message) :
                    new ResolveOperationEndingMessage(resolveOperationModel.Id);
                Send(message);
            };
            resolveOperation.InstanceLookupBeginning += (s, e) => AttachToInstanceLookup(e.InstanceLookup, resolveOperationModel);
        }

        void AttachToInstanceLookup(IInstanceLookup instanceLookup, ResolveOperationModel resolveOperation)
        {
            var instanceLookupModel = ModelMapper.GetInstanceLookupModel(instanceLookup, resolveOperation);
            Send(new InstanceLookupBeginningMessage(instanceLookupModel));
            instanceLookup.InstanceLookupEnding += (s, e) => Send(new InstanceLookupEndingMessage(instanceLookupModel.Id, e.NewInstanceActivated));
            instanceLookup.CompletionBeginning += (s, e) => Send(new InstanceLookupCompletionBeginningMessage(instanceLookupModel.Id));
            instanceLookup.CompletionEnding += (s, e) => Send(new InstanceLookupCompletionEndingMessage(instanceLookupModel.Id));
        }

        void Send(object message)
        {
            Queue.Enqueue(message);
        }

        internal IWriteQueue Queue { get { return _rootModule.Queue; } }

        internal ModelMapper ModelMapper { get { return _rootModule.ModelMapper; } }
    }
}