using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Represents admission controller operations.
    /// </summary>
    [Flags]
    public enum AdmissionOperations
    {
        /// <summary>
        /// None.
        /// </summary>
        None = 0,

        /// <summary>
        /// All.
        /// </summary>
        All = 1 << 0,

        /// <summary>
        /// Create.
        /// </summary>
        Create = 1 << 1,

        /// <summary>
        /// Update.
        /// </summary>
        Update = 1 << 2,

        /// <summary>
        /// Delete.
        /// </summary>
        Delete = 1 << 3,
    }
}
