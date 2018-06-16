//-----------------------------------------------------------------------------
// FILE:	    UpdateManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
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
    /// Manages the available cluster updates.
    /// </summary>
    public static class UpdateManager
    {
        /// <summary>
        /// Static constuctor.
        /// </summary>
        static UpdateManager()
        {
            Updates = new List<IClusterUpdate>()
            {
                new Update_010297_010298()
            };
        }

        /// <summary>
        /// Returns the available cluster updates (in no particular order).
        /// </summary>
        public static IEnumerable<IClusterUpdate> Updates { get; private set; }

        /// <summary>
        /// Scans the cluster and adds the steps to a <see cref="SetupController"/> required
        /// to update the cluster to the most recent version.
        /// </summary>
        /// <param name="cluster">The target cluster proxy.</param>
        /// <param name="controller">The setup controller.</param>
        /// <returns>The list of updates that will be applied or an empty list if no updates are available.</returns>
        /// <exception cref="ClusterException">Thrown if there was an error selecting the updates.</exception>
        public static IEnumerable<IClusterUpdate> AddUpdateSteps(ClusterProxy cluster, SetupController<NodeDefinition> controller)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null);

            // Obtain and parse the current cluster version.

            if (!cluster.Globals.TryGetString(NeonClusterGlobals.NeonCliVersion, out var versionString) ||
                !SemanticVersion.TryParse(versionString, out var clusterVersion))
            {
                throw new ClusterException($"Unable to retrieve or parse the cluster version global [{NeonClusterGlobals.NeonCliVersion}].");
            }

            // Scan for the first update that applies, returning if there
            // are no pending updates.

            var updates = new List<IClusterUpdate>();

            if (Updates.Count() == 0)
            {
                return updates;
            }

            var firstUpdate = Updates
                .Where(u => u.FromVersion >= clusterVersion)
                .OrderBy(u => u.FromVersion)
                .FirstOrDefault();

            if (firstUpdate == null)
            {
                return updates;
            }

            // Determine which updates apply.  We're going to sort the available updates
            // in ascending order by [FromVersion] and then in decending order by [ToVersion]
            // to favor overlapping updates that advance the cluster the most.

            var nextVersion = firstUpdate.FromVersion;

            foreach (var update in Updates
                .OrderBy(u => u.FromVersion)
                .ThenByDescending(u => u.ToVersion))
            {
                if (update.FromVersion >= nextVersion)
                {
                    update.Cluster = cluster;
                    updates.Add(update);

                    nextVersion = update.ToVersion;
                }
            }

            return updates;
        }
    }
}
