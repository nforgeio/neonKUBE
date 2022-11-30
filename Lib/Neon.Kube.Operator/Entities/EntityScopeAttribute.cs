using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Kube.Operator
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class EntityScopeAttribute : Attribute
    {
        public EntityScope Scope { get; }

        public EntityScopeAttribute(EntityScope scope = EntityScope.Namespaced)
        {
            Scope = scope;
        }
    }
}