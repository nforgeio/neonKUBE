using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Kube.Operator.Attributes;
using Neon.Kube.Operator.Builder;
using Neon.Kube.Operator.Controller;
using Neon.Kube.Operator.Finalizer;
using Neon.Kube.Operator.Webhook;

namespace Neon.Kube.Operator.Util
{
    internal class AssemblyScanner
    {
        public ComponentRegister ComponentRegister { get; set; }
        public List<Type>        EntityTypes { get; set;}

        public AssemblyScanner()
        {
            this.ComponentRegister     = new ComponentRegister();
            this.EntityTypes           = new List<Type>();
        }

        public void Add(Assembly assembly)
        {
            Scan(assembly);
        }

        public void Add(Assembly[] assemblies)
        {
            foreach (var assembly in assemblies)
            {
                Scan(assembly);
            }
        }

        public void Add(string assemblyPath)
        {
            Scan(Assembly.LoadFrom(assemblyPath));
        }

        private void Scan(Assembly assembly)
        {
            List<Type> assemblyTypes = new List<Type>();

            try
            {
                assemblyTypes = assembly.GetTypes().Where(type => type != null).ToList();
            }
            catch (ReflectionTypeLoadException e)
            {
                assemblyTypes = e.Types.Where(type => type != null).ToList();
            }

            var types = assemblyTypes
                .Where(type => type.GetInterfaces().Count() > 0 && type.GetInterfaces().Any(@interface => @interface.GetCustomAttributes<OperatorComponentAttribute>()
                    .Any())).ToList();

            foreach (var type in types)
            {
                switch (type.GetInterfaces()
                    .Where(@interface => @interface.GetCustomAttributes<OperatorComponentAttribute>()
                    .Any())
                    .Select(@interface => @interface.GetCustomAttribute<OperatorComponentAttribute>())
                    .FirstOrDefault().ComponentType)
                {
                    case OperatorComponentType.Controller:

                        if (type.GetCustomAttribute<ControllerAttribute>()?.Ignore == true ||
                            (!NeonHelper.IsKubernetes && type.GetCustomAttribute<ControllerAttribute>()?.IgnoreWhenNotInPod == true) ||
                            type == typeof(ResourceControllerBase<>))
                        {
                            break;
                        }

                        var entityTypes = type.GetInterfaces()
                            .Where(@interface => @interface.IsConstructedGenericType && @interface.GetGenericTypeDefinition().IsEquivalentTo(typeof(IResourceController<>)))
                            .Select(@interface => @interface.GenericTypeArguments[0]);


                        foreach (var entityType in entityTypes)
                        {
                            ComponentRegister.RegisterController(type, entityType);
                            EntityTypes.Add(entityType);
                        }

                        break;

                    case OperatorComponentType.Finalizer:

                        if (type.GetCustomAttribute<FinalizerAttribute>()?.Ignore == true)
                        {
                            break;
                        }

                        var finalizerEntityTypes = type.GetInterfaces()
                            .Where(@interface => @interface.IsConstructedGenericType && @interface.GetGenericTypeDefinition().IsEquivalentTo(typeof(IResourceFinalizer<>)))
                            .Select(@interface => @interface.GenericTypeArguments[0]);

                        foreach (var entityType in finalizerEntityTypes)
                        {
                            ComponentRegister.RegisterFinalizer(type, entityType);
                            EntityTypes.Add(entityType);
                        }

                        break;

                    case OperatorComponentType.MutationWebhook:

                        if (type.GetCustomAttribute<MutatingWebhookAttribute>()?.Ignore == true)
                        {
                            break;
                        }

                        var mutatingWebhookEntityTypes = type.GetInterfaces()
                            .Where(@interface => @interface.IsConstructedGenericType && @interface.GetGenericTypeDefinition().IsEquivalentTo(typeof(IMutatingWebhook<>)))
                            .Select(@interface => @interface.GenericTypeArguments[0]);

                        foreach (var entityType in mutatingWebhookEntityTypes)
                        {
                            ComponentRegister.RegisterMutatingWebhook(type, entityType);
                            EntityTypes.Add(entityType);
                        }

                        break;

                    case OperatorComponentType.ValidationWebhook:

                        if (type.GetCustomAttribute<ValidatingWebhookAttribute>()?.Ignore == true)
                        {
                            break;
                        }

                        var validatingWebhookEntityTypes = type.GetInterfaces()
                            .Where(@interface => @interface.IsConstructedGenericType && @interface.GetGenericTypeDefinition().IsEquivalentTo(typeof(IValidatingWebhook<>)))
                            .Select(@interface => @interface.GenericTypeArguments[0]);

                        foreach (var entityType in validatingWebhookEntityTypes)
                        {
                            ComponentRegister.RegisterValidatingWebhook(type, entityType);
                            EntityTypes.Add(entityType);
                        }

                        break;
                }
            }
        }
    }
}
