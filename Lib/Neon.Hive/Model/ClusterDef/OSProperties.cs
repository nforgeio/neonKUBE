//-----------------------------------------------------------------------------
// FILE:	    OSProperties.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Hive;

namespace Neon.Hive
{
    /// <summary>
    /// Operating system related properties.
    /// </summary>
    public class OSProperties
    {
        //---------------------------------------------------------------------
        // Static members.

        /// <summary>
        /// Returns the properties for an operating system.
        /// </summary>
        /// <param name="operatingSystem">The target operating system.</param>
        /// <returns>The <see cref="OSProperties"/>.</returns>
        public static OSProperties For(TargetOS operatingSystem)
        {
            switch (operatingSystem)
            {
                case TargetOS.Ubuntu_16_04:

                    return new OSProperties()
                    {
                        TargetOS       = operatingSystem,
                        ServiceManager = ServiceManager.Systemd,
                        StorageDriver  = DockerStorageDrivers.Overlay2
                    };

                default:

                    throw new NotImplementedException();
            }
        }

        //---------------------------------------------------------------------
        // Instance members.

        /// <summary>
        /// Private constructor.
        /// </summary>
        private OSProperties()
        {
        }

        /// <summary>
        /// Identifies the target operating system.
        /// </summary>
        public TargetOS TargetOS { get; private set; }

        /// <summary>
        /// Identifies the storage driver to be used by Docker container images.
        /// </summary>
        public DockerStorageDrivers StorageDriver { get; private set; }

        /// <summary>
        /// Identifies the service manager used by the node host operating system.
        /// </summary>
        public ServiceManager ServiceManager { get; private set; }
    }
}
