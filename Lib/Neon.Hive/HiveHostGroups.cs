//-----------------------------------------------------------------------------
// FILE:	    HiveHostGroups.cs
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

using Renci.SshNet;

namespace Neon.Hive
{
    /// <summary>
    /// Defines the built-in hive host groups.
    /// </summary>
    public static class HiveHostGroups
    {
        /// <summary>
        /// Includes all hive hosts.
        /// </summary>
        public const string All = "all";

        /// <summary>
        /// Includes the hive swarm hosts (e.g. the managers and workers).
        /// </summary>
        public const string Swarm = "swarm";

        /// <summary>
        /// Includes the hive manager hosts.
        /// </summary>
        public const string Managers = "managers";

        /// <summary>
        /// Includes the hive worker hosts.
        /// </summary>
        public const string Workers = "workers";

        /// <summary>
        /// Includes the hive pet hosts.
        /// </summary>
        public const string Pets = "pets";

        /// <summary>
        /// Includes all hive hosts running Ceph related backend services.
        /// </summary>
        public const string Ceph = "ceph";

        /// <summary>
        /// Includes all hive hosts running CephMON services.
        /// </summary>
        public const string CephMON = "ceph-mon";

        /// <summary>
        /// Includes all hive hosts running CephMDS services.
        /// </summary>
        public const string CephMDS = "ceph-mds";

        /// <summary>
        /// Includes all hive hosts running CephOSD services.
        /// </summary>
        public const string CephOSD = "ceph-osd";

        /// <summary>
        /// Includes all hive hosts running HiveMQ (aka RabbitMQ) services.
        /// </summary>
        public const string HiveMQ = "hivemq";

        /// <summary>
        /// Includes all hive hosts running HiveMQ (aka RabbitMQ) services that also
        /// enable the management plugin.
        /// </summary>
        public const string HiveMQManagers = "hivemq-managers";

        /// <summary>
        /// Returns the set of the standard built-in hive host groups.
        /// </summary>
        public static HashSet<string> BuiltIn { get; private set; } =
            new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
            {
                All,
                Swarm,
                Managers,
                Workers,
                Pets,
                Ceph,
                CephMON,
                CephMDS,
                CephOSD,
                HiveMQ,
                HiveMQManagers
            };
    }
}
