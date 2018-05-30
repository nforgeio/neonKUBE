//-----------------------------------------------------------------------------
// FILE:	    DockerConfigManager.cs
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
    /// Handles Docker config related operations for a <see cref="ClusterProxy"/>.
    /// </summary>
    public sealed class DockerConfigManager
    {
        private ClusterProxy cluster;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="cluster">The parent <see cref="ClusterProxy"/>.</param>
        internal DockerConfigManager(ClusterProxy cluster)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null);

            this.cluster = cluster;
        }

        /// <summary>
        /// Creates or updates a cluster Docker string config.
        /// </summary>
        /// <param name="configName">The config name.</param>
        /// <param name="value">The config value.</param>
        /// <param name="options">Command run options.</param>
        public void Set(string configName, string value, RunOptions options = RunOptions.LogOutput | RunOptions.FaultOnError)
        {
            Covenant.Requires<ArgumentException>(ClusterDefinition.IsValidName(configName));
            Covenant.Requires<ArgumentNullException>(value != null);

            Set(configName, Encoding.UTF8.GetBytes(value), options);
        }

        /// <summary>
        /// Creates or updates a cluster Docker binary config.
        /// </summary>
        /// <param name="configName">The config name.</param>
        /// <param name="value">The config value.</param>
        /// <param name="options">Command run options.</param>
        public void Set(string configName, byte[] value, RunOptions options = RunOptions.LogOutput | RunOptions.FaultOnError)
        {
            Covenant.Requires<ArgumentException>(ClusterDefinition.IsValidName(configName));
            Covenant.Requires<ArgumentNullException>(value != null);

            var bundle = new CommandBundle("./create-config.sh");

            bundle.AddFile("config.dat", value);
            bundle.AddFile("create-config.sh",
$@"#!/bin/bash

if docker config inspect {configName} ; then
    echo ""Config already exists; not setting it again.""
else
    cat config.dat | docker config create {configName} -

    # It appears that Docker configs may not be available
    # immediately after they are created.  So, we're going 
    # wait for a while until we can inspect the new config.

    count=0

    while [ $count -le 30 ]
    do
        if docker config inspect {configName} ; then
            exit 0
        fi
        
        sleep 1
        count=$(( count + 1 ))
    done

    echo ""Created config [{configName}] is not ready after 30 seconds."" >&2
    exit 1
fi
",
                isExecutable: true);

            cluster.GetHealthyManager().SudoCommand(bundle, options);
        }

        /// <summary>
        /// Deletes a cluster Docker config.
        /// </summary>
        /// <param name="configName">The config name.</param>
        /// <param name="options">Command run options.</param>
        public void Remove(string configName, RunOptions options = RunOptions.LogOutput | RunOptions.FaultOnError)
        {
            Covenant.Requires<ArgumentException>(ClusterDefinition.IsValidName(configName));

            var bundle = new CommandBundle("./delete-config.sh");

            bundle.AddFile("delete-config.sh",
$@"#!/bin/bash
docker config inspect {configName}

if [ ""$?"" != ""0"" ] ; then
    echo ""Config doesn't exist.""
else
    docker config rm {configName}
fi
",              isExecutable: true);

            cluster.GetHealthyManager().SudoCommand("./delete-config.sh");
        }
    }
}
