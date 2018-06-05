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
    /// under <b>neon/cluster</b>.
    /// </summary>
    public static class NeonClusterGlobals
    {
        /// <summary>
        /// Enables unit testing on the cluster via <b>ClusterFixture</b> (bool).
        /// </summary>
        public const string AllowUnitTesting = "allow-unit-testing";

        /// <summary>
        /// Cluster creation date (UTC).
        /// </summary>
        public const string CreateDateUtc = "create-date-utc";

        /// <summary>
        /// Current cluster definition as compressed JSON.
        /// </summary>
        public const string DefinitionDeflate = "definition-deflate";

        /// <summary>
        /// MD5 hash of the current cluster definition.
        /// </summary>
        public const string DefinitionHash = "definition-hash";

        /// <summary>
        /// Disables automatic Vault unsealing (bool).
        /// </summary>
        public const string DisableAutoUnseal = "disable-auto-unseal";

        /// <summary>
        /// Version of the <b>neon-cli</b> that created or last upgraded the cluster.
        /// </summary>
        public const string NeonCliVersion = "neon-cli-version";

        /// <summary>
        /// Minimum <b>neon-cli</b> version allowed to manage the cluster.
        /// </summary>
        public const string NeonCliVersionMinimum = "neon-cli-version-minimum";

        /// <summary>
        /// Current cluster pets definition.
        /// </summary>
        public const string PetsDefinition = "pets-definition";

        /// <summary>
        /// Cluster's globally unique ID assigned during cluster setup.
        /// </summary>
        public const string Uuid = "uuid";
    }
}
