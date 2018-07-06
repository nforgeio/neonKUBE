//-----------------------------------------------------------------------------
// FILE:	    HostingLoader.cs
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
    /// Hive hosting utilities.
    /// </summary>
    public static class HostingLoader
    {
        /// <summary>
        /// Loads the known hive hosting manager assemblies so they'll be available
        /// to <see cref="HostingManager.GetManager(HiveProxy, string)"/>, 
        /// <see cref="HostingManager.Validate(HiveDefinition)"/>, and
        /// <see cref="HostingManager.ValidateHive(HiveDefinition)"/> when
        /// they are called.
        /// </summary>
        public static void LoadManagers()
        {
            // $todo(jeff.lill):
            //
            // This is hardcoded to load all of the built-in manager assemblies.  In the
            // future, it would be nice if this could be less hardcoded and also support
            // loading custom assemblies so that users could author their own managers.
            //
            // This implemention is also pretty stupid in that it has to load all of the
            // manager assemblies because it doesn't know which manager will be requested.
            // One way to fix this would be to implement some kind of callback that could
            // be registered statically with [HostingManager] before [HostingManager.GetManager()]
            // is called.
            //
            // Another potential problem is that it's possible in the future for hosting
            // manager subassemblies to conflict.  For example, say we have a [Xen] hosting
            // manager that uses the latest Azure class libraries but we also have a
            // [XenLegacy] hosting manager that needs to use an older library for some
            // reason.  It could be possible that we can't reference or load both sets
            // of subassemblies at the same time.
            //
            // I'm going to defer this for now though.  I suspect that the ultimate
            // solution will be to handle this as part of a greater extensibility
            // strategy and this is unlikely to become a problem any time soon.

            AwsHostingManager.Load();
            AzureHostingManager.Load();
            GoogleHostingManager.Load();
            HyperVHostingManager.Load();
            HyperVDevHostingManager.Load();
            MachineHostingManager.Load();
            XenServerHostingManager.Load();
        }
    }
}
