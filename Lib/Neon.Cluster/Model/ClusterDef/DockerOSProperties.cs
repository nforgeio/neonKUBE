//-----------------------------------------------------------------------------
// FILE:	    DockerOSProperties.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Cluster;
using Neon.Common;

namespace Neon.Cluster
{
    /// <summary>
    /// Operating system related Docker properties.
    /// </summary>
    public class DockerOSProperties
    {
        /// <summary>
        /// Identifies the target operating system.
        /// </summary>
        public TargetOS TargetOS { get; set; }

        /// <summary>
        /// Identifies the storage driver to be used by Docker container images.
        /// </summary>
        public DockerStorageDrivers StorageDriver { get; set; }
    }
}
