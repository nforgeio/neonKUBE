using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Kube.Resources
{
    /// <summary>
    /// Indicates whether the defined custom resource is cluster- or namespace-scoped. 
    /// Allowed values are `Cluster` and `Namespaced`.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class EntityScopeAttribute : Attribute
    {
        /// <summary>
        /// The <see cref="EntityScope"/>.
        /// </summary>
        public EntityScope Scope { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="scope"></param>
        public EntityScopeAttribute(EntityScope scope = EntityScope.Namespaced)
        {
            Scope = scope;
        }
    }
}