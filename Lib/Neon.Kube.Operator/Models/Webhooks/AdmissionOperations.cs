using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Kube.Operator
{
    [Flags]
    public enum AdmissionOperations
    {
        None = 0,

        All = 1 << 0,

        Create = 1 << 1,

        Update = 1 << 2,

        Delete = 1 << 3,
    }
}
