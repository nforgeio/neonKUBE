using Neon.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Kube.Operator.Generators
{
    internal class RbacGenerator
    {
        public static async Task GenerateRbacAsync()
        {
            await SyncContext.Clear;
        }
    }
}
