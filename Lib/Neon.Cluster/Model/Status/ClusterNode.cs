//-----------------------------------------------------------------------------
// FILE:	    ClusterNode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.Cryptography;

namespace Neon.Cluster
{
    /// <summary>
    /// Describes the current state of a cluster node.
    /// </summary>
    public class ClusterNode
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ClusterNode()
        {
        }

        /// <summary>
        /// The node name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Indicates the node status.
        /// </summary>
        public ClusterNodeStatus Status { get; set; }

        /// <summary>
        /// Indicates the node's availability to schedule swarm tasks.
        /// </summary>
        public ClusterNodeAvailability Availability { get; set; }

        /// <summary>
        /// Indicates whether the node is a manager and it's manager status.
        /// </summary>
        public ClusterNodeManagerStatus ManagerStatus { get; set; }

        /// <summary>
        /// The version of the Docker engine deployed to this node.
        /// </summary>
        public string EngineVersion { get; set; }
    }
}
