//-----------------------------------------------------------------------------
// FILE:	    NeonClusterGlobals.cs
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
    /// Identifies the global cluster Consul globals and settings.  These are located
    /// under <b>neon/cluster</b>.  Settings with constant names prefixed by <b>User</b>
    /// are considered to be user-modifiable.  The other settings are generally managed
    /// only by the cluster and its tools.
    /// </summary>
    public static class NeonClusterGlobals
    {
        /// <summary>
        /// Cluster creation date (UTC).
        /// </summary>
        public const string CreateDateUtc = "create-date-utc";

        /// <summary>
        /// Current cluster definition as compressed JSON.
        /// </summary>
        public const string DefinitionDeflate = "definition-deflated";

        /// <summary>
        /// MD5 hash of the current cluster definition.
        /// </summary>
        public const string DefinitionHash = "definition-hash";

        /// <summary>
        /// Minimum <b>neon-cli</b> version allowed to manage the cluster.
        /// </summary>
        public const string NeonCli = "neon-cli";

        /// <summary>
        /// Current cluster pets definition.
        /// </summary>
        public const string PetsDefinition = "pets-definition";

        /// <summary>
        /// Cluster globally unique ID assigned during cluster setup.
        /// </summary>
        public const string Uuid = "uuid";

        /// <summary>
        /// Version of the cluster.  This is actually the version of <b>neon-cli</b> 
        /// that created or last upgraded the cluster.
        /// </summary>
        public const string Version = "version";

        //---------------------------------------------------------------------
        // These settings are considered to be user modifiable.

        /// <summary>
        /// Enables unit testing on the cluster via <b>ClusterFixture</b> (bool).
        /// </summary>
        public const string UserAllowUnitTesting = "allow-unit-testing";

        /// <summary>
        /// Disables automatic Vault unsealing (bool).
        /// </summary>
        public const string UserDisableAutoUnseal = "disable-auto-unseal";

        /// <summary>
        /// Specifies the number of days to retain <b>logstash</b> and
        /// <b>metricbeat</b> logs.
        /// </summary>
        public const string UserLogRetentionDays = "log-rentention-days";
    }
}
