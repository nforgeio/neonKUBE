using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using k8s;
using k8s.Models;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Operator  builder interface.
    /// </summary>
    public interface IOperatorBuilder
    {
        /// <summary>
        /// Returns the original service collection.
        /// </summary>
        IServiceCollection Services { get; }

        /// <summary>
        /// <para>
        /// Adds a mutating webhook to the operator.
        /// </para>
        /// </summary>
        /// <typeparam name="TImplementation">The type of the webhook to register.</typeparam>
        /// <typeparam name="TEntity">The type of the entity to associate the webhook with.</typeparam>
        /// <returns>The builder for chaining.</returns>
        IOperatorBuilder AddMutationWebhook<TImplementation, TEntity>()
            where TImplementation : class, IMutationWebhook<TEntity>
            where TEntity : IKubernetesObject<V1ObjectMeta>, new();
    }
}
