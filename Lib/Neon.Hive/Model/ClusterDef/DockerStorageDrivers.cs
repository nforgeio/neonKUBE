//-----------------------------------------------------------------------------
// FILE:	    DockerStorageDrivers.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Hive;

namespace Neon.Hive
{
    /// <summary>
    /// Enumerates the supported Docker engine storage drivers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Storage drivers are used to provide the layered file system used by
    /// Docker to manage container images.  Visit this page for more information:
    /// </para>
    /// <para>
    /// https://docs.docker.com/engine/userguide/storagedriver/selectadriver/
    /// </para>
    /// </remarks>
    public enum DockerStorageDrivers
    {
        /// <summary>
        /// The original Docker file system that is compatible with all Linux
        /// distributions and is typically run on <b>ext4</b> or <b>xfs</b>).
        /// Less efficent and somewhat hacky than other choices.
        /// </summary>
        [EnumMember(Value = "aufs")]
        Aufs,

        /// <summary>
        /// The older Overlay file system (requires Linux Kernel v3.13
        /// or better and is typically run on <b>ext4</b> or <b>xfs</b>).
        /// </summary>
        [EnumMember(Value = "overlay")]
        Overlay,

        /// <summary>
        /// The newer, better performing Overlay file system (requires Linux
        /// Kernel 4.0 or better and is typically run on <b>ext4</b> or <b>xfs</b>)).
        /// </summary>
        [EnumMember(Value = "overlay2")]
        Overlay2
    }
}
