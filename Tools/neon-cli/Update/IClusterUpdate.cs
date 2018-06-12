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
    /// Describes the behavior of an update that can be used to upgrade a neonCLUSTER.
    /// </summary>
    /// <remarks>
    /// <para>
    /// neonCLUSTERs are versioned using the version number of the <b>neon-cli</b> used
    /// to deploy or update the cluster.  An instance of this class will be able to
    /// upgrade the cluster from an older <b>neon-cli</b> version to a newer version.
    /// Multiple updates may need to be applied to upgrade a cluster.
    /// </para>
    /// </remarks>
    public interface IClusterUpdate
    {
        /// <summary>
        /// Specifies the minimum cluster version to which this update applies.
        /// </summary>
        string FromVersion { get; }

        /// <summary>
        /// Specifies the cluster version after all updates are applied.
        /// </summary>
        string ToVersion { get; }

        /// <summary>
        /// Performs the updates returning information about the changes made.
        /// </summary>
        /// <param name="cluster">The target cluster.</param>
        /// <param name="args">Specifies the update details.</param>
        /// <returns>Strings describing the changes.</returns>
        /// <remarks>
        /// <paramref name="args"/> specifies which components are to be updated
        /// also whether a dry-run is to be performed.
        /// </remarks>
        IEnumerable<string> Update(ClusterProxy cluster, ClusterUpdateArgs args);
    }
}
