using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Kube.Resources
{
    /// <summary>
    /// Defines the scope of a kubernetes resource.
    /// </summary>
    public enum EntityScope
    {
        /// <summary>
        /// The resource is Namespaced.
        /// </summary>
        [EnumMember(Value = "Namespaced")]
        Namespaced,
        
        /// <summary>
        /// The resource is cluster wide.
        /// </summary>
        [EnumMember(Value = "Cluster")]
        Cluster
    }
}