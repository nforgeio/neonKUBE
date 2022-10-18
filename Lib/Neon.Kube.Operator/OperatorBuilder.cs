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
    /// <summary>
    /// <para>
    /// Used to build a kubernetes operator.
    /// </para>
    /// </summary>
    public class OperatorBuilder : IOperatorBuilder
    {
        /// <inheritdoc/>
        public IServiceCollection Services { get; }
        private ComponentRegister componentRegister { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="services"></param>
        public OperatorBuilder(IServiceCollection services)
        {
            Services = services;
            componentRegister = new ComponentRegister();
        }

        internal IOperatorBuilder AddOperatorBase()
        {
            Services.AddSingleton(componentRegister);
            Services.AddRouting();
            return this;
        }

        /// <inheritdoc/>
        public IOperatorBuilder AddMutationWebhook<TImplementation, TEntity>()
            where TImplementation : class, IMutationWebhook<TEntity>
            where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        {
            Services.TryAddScoped<TImplementation>();
            componentRegister.RegisterMutatingWebhook<TImplementation, TEntity>();

            return this;
        }
    }
}
