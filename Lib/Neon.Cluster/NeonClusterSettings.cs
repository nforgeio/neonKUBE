//-----------------------------------------------------------------------------
// FILE:	    NeonClusterSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Cluster
{
    /// <summary>
    /// Identifies the global cluster Consul settings.  These are located
    /// under <b>neon/cluster/settings</b>.
    /// </summary>
    public static class NeonClusterSettings
    {
        /// <summary>
        /// Enables unit testing on the cluster via <b>ClusterFixture</b> (bool).
        /// </summary>
        public const string AllowUnitTesting = "allow-unit-testing";

        /// <summary>
        /// Cluster's globally unique ID assigned during cluster setup.
        /// </summary>
        public const string Uuid = "uuid";

        /// <summary>
        /// Cluster creation date (UTC).
        /// </summary>
        public const string CreateDate = "create-date";
    }
}
