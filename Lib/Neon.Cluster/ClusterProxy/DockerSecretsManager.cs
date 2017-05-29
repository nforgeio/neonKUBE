//-----------------------------------------------------------------------------
// FILE:	    DockerSecretsManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

namespace Neon.Cluster
{
    /// <summary>
    /// Handles Docker secret related operations for a <see cref="ClusterProxy"/>.
    /// </summary>
    public sealed class DockerSecretsManager
    {
        private ClusterProxy cluster;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="cluster">The parent <see cref="ClusterProxy"/>.</param>
        internal DockerSecretsManager(ClusterProxy cluster)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null);

            this.cluster = cluster;
        }

        /// <summary>
        /// Creates or updates a cluster Docker string secret.
        /// </summary>
        /// <param name="secretName">The secret name.</param>
        /// <param name="value">The secret value.</param>
        /// <param name="options">Command run options.</param>
        public void Set(string secretName, string value, RunOptions options = RunOptions.LogOutput | RunOptions.FaultOnError)
        {
            Covenant.Requires<ArgumentException>(ClusterDefinition.IsValidName(secretName));
            Covenant.Requires<ArgumentNullException>(value != null);

            Set(secretName, Encoding.UTF8.GetBytes(value), options);
        }

        /// <summary>
        /// Creates or updates a cluster Docker binary secret.
        /// </summary>
        /// <param name="secretName">The secret name.</param>
        /// <param name="value">The secret value.</param>
        /// <param name="options">Command run options.</param>
        public void Set(string secretName, byte[] value, RunOptions options = RunOptions.LogOutput | RunOptions.FaultOnError)
        {
            Covenant.Requires<ArgumentException>(ClusterDefinition.IsValidName(secretName));
            Covenant.Requires<ArgumentNullException>(value != null);

            var bundle = new CommandBundle($"cat secret.dat | docker secret create {secretName} -");

            bundle.AddFile("secret.dat", value);

            cluster.FirstManager.SudoCommand(bundle, options);
        }

        /// <summary>
        /// Deletes a cluster Docker secret.
        /// </summary>
        /// <param name="secretName">The secret name.</param>
        /// <param name="options">Command run options.</param>
        public void Remove(string secretName, RunOptions options = RunOptions.LogOutput | RunOptions.FaultOnError)
        {
            Covenant.Requires<ArgumentException>(ClusterDefinition.IsValidName(secretName));

            cluster.FirstManager.SudoCommand($"docker secret rm {secretName}");
        }
    }
}
