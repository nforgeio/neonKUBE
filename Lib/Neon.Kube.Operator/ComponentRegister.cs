using k8s.Models;
using k8s;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Neon.Kube.Operator
{
    internal class ComponentRegister
    {
        public HashSet<MutatingWebhookRegistration> MutatingWebhookRegistrations { get; set; }

        public ComponentRegister() 
        {
            MutatingWebhookRegistrations = new HashSet<MutatingWebhookRegistration>();
        }
        public void RegisterMutatingWebhook<TMutator, TEntity>()
            where TMutator : class, IMutationWebhook<TEntity>
            where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        {
            MutatingWebhookRegistrations.Add(new MutatingWebhookRegistration(typeof(TMutator), typeof(TEntity)));

            return;
        }
    }

    internal record MutatingWebhookRegistration(Type WebhookType, Type EntityType);

}
