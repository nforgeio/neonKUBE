//-----------------------------------------------------------------------------
// FILE:	    IHostingLoader.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;
using Neon.Time;

namespace Neon.Hive
{
    /// <summary>
    /// <b>INTERNAL USE ONLY:</b> This interface describes a low-level class implementations
    /// that are registered by the <b>Neon.Hive.Hosting</b> class library with 
    /// <see cref="HostingManager"/> to provide a way to access the various hosting
    /// implementations without having to bake this into the <b>Neon.Hive</b> assembly.
    /// </summary>
    public interface IHostingLoader
    {
        /// <summary>
        /// Returns the <see cref="HostingManager"/> for a specific environment.
        /// </summary>
        /// <param name="hive">The hive being managed.</param>
        /// <param name="logFolder">
        /// The folder where log files are to be written, otherwise or <c>null</c> or 
        /// empty if logging is disabled.
        /// </param>
        /// <returns>
        /// The <see cref="HostingManager"/> or <c>null</c> if no hosting manager
        /// could be located for the specified hive environment.
        /// </returns>
        /// <exception cref="HiveException">Thrown if the multiple managers implement support for the same hosting environment.</exception>
        HostingManager GetManager(HiveProxy hive, string logFolder = null);

        /// <summary>
        /// Determines whether a hosting environment is hosted in the cloud.
        /// </summary>
        /// <param name="environment">The target hosting environment.</param>
        /// <returns><c>true</c> for cloud environments.</returns>
        bool IsCloudEnvironment(HostingEnvironments environment);
    }
}
