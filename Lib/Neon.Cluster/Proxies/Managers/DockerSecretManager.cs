//-----------------------------------------------------------------------------
// FILE:	    DockerSecretManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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
    public sealed class DockerSecretManager
    {
        private ClusterProxy cluster;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="cluster">The parent <see cref="ClusterProxy"/>.</param>
        internal DockerSecretManager(ClusterProxy cluster)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null);

            this.cluster = cluster;
        }

        /// <summary>
        /// Determines whether a Docker secret exists.
        /// </summary>
        /// <param name="secretName">The secret name.</param>
        /// <returns><c>true</c> if the secret exists.</returns>
        /// <exception cref="NeonClusterException">Thrown if the operation failed.</exception>
        public bool Exists(string secretName)
        {
            var manager  = cluster.GetHealthyManager();
            var response = manager.DockerCommand(RunOptions.None, "docker secret inspect", secretName);

            if (response.ExitCode == 0)
            {
                return true;
            }
            else
            {
                // $todo(jeff.lill): 
                //
                // I'm trying to distinguish between a a failure because the secret doesn't
                // exist and other potential failures (e.g. Docker is not running).
                //
                // This is a bit fragile.

                if (response.ErrorText.StartsWith("Status: Error: no such secret:", StringComparison.InvariantCultureIgnoreCase))
                {
                    return false;
                }
                else
                {
                    throw new NeonClusterException(response.ErrorSummary);
                }
            }
        }

        /// <summary>
        /// Creates or updates a cluster Docker string secret.
        /// </summary>
        /// <param name="secretName">The secret name.</param>
        /// <param name="value">The secret value.</param>
        /// <param name="options">Optional command run options.</param>
        /// <exception cref="NeonClusterException">Thrown if the operation failed.</exception>
        public void Set(string secretName, string value, RunOptions options = RunOptions.None)
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
        /// <param name="options">Optional command run options.</param>
        /// <exception cref="NeonClusterException">Thrown if the operation failed.</exception>
        public void Set(string secretName, byte[] value, RunOptions options = RunOptions.None)
        {
            Covenant.Requires<ArgumentException>(ClusterDefinition.IsValidName(secretName));
            Covenant.Requires<ArgumentNullException>(value != null);

            var bundle = new CommandBundle("./create-secret.sh");

            bundle.AddFile("secret.dat", value);
            bundle.AddFile("create-secret.sh",
$@"#!/bin/bash

if docker secret inspect {secretName} ; then
    echo ""Secret already exists; not setting it again.""
else
    cat secret.dat | docker secret create {secretName} -

    # It appears that Docker secrets may not be available
    # immediately after they are created.  So, we're going 
    # wait for a while until we can inspect the new secret.

    count=0

    while [ $count -le 30 ]
    do
        if docker secret inspect {secretName} ; then
            exit 0
        fi
        
        sleep 1
        count=$(( count + 1 ))
    done

    echo ""Created secret [{secretName}] is not ready after 30 seconds."" >&2
    exit 1
fi
",
                isExecutable: true);

            var response = cluster.GetHealthyManager().SudoCommand(bundle, options);

            if (response.ExitCode != 0)
            {
                throw new NeonClusterException(response.ErrorSummary);
            }
        }

        /// <summary>
        /// Deletes a cluster Docker secret.
        /// </summary>
        /// <param name="secretName">The secret name.</param>
        /// <param name="options">Optional command run options.</param>
        /// <exception cref="NeonClusterException">Thrown if the operation failed.</exception>
        public void Remove(string secretName, RunOptions options = RunOptions.None)
        {
            Covenant.Requires<ArgumentException>(ClusterDefinition.IsValidName(secretName));

            var bundle = new CommandBundle("./delete-secret.sh");

            bundle.AddFile("delete-secret.sh",
$@"#!/bin/bash
docker secret inspect {secretName}

if [ ""$?"" != ""0"" ] ; then
    echo ""Secret doesn't exist.""
else
    docker secret rm {secretName}
fi
",              isExecutable: true);

            var response = cluster.GetHealthyManager().SudoCommand(bundle, RunOptions.None);

            if (response.ExitCode != 0)
            {
                throw new NeonClusterException(response.ErrorSummary);
            }
        }
    }
}
