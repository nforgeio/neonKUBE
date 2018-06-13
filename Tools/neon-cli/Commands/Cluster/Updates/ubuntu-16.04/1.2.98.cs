//-----------------------------------------------------------------------------
// FILE:	    IClusterUpdate.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Cluster;
using Neon.Common;
using Neon.IO;

namespace NeonCli
{
    /// <summary>
    /// Updates an Ubuntu cluster from <b>1.2.97</b> to <b>1.2.98</b>.
    /// </summary>
    public class Ubuntu_1604_1_2_98 : IClusterUpdate
    {
        /// <inheritdoc/>
        public string FromVersion => "1.2.97";

        /// <inheritdoc/>
        public string ToVersion => "1.2.98";

        /// <inheritdoc/>
        public IEnumerable<string> Update(ClusterProxy cluster, ClusterUpdateContext context)
        {
            return context.Output;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"Update [{FromVersion}] --> [{ToVersion}]";
        }
    }
}
