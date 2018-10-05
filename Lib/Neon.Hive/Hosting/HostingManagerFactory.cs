//-----------------------------------------------------------------------------
// FILE:	    HostingManagerFactory.cs
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
    /// Provides for the creation of <see cref="HostingManager"/> implementations
    /// for a target hosting environment.
    /// </summary>
    public class HostingManagerFactory : IHostingManagerFactory
    {
        //---------------------------------------------------------------------

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> The driver providing low-level access to hosting
        /// manager implementations.  This is initialized by a call to <c>HostingLoader.Initialize()</c>
        /// defined withing the <b>Neon.Hive.Hosting</b> assembly.
        /// </summary>
        public static IHostingLoader Loader { get; set; }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="loaderAction">
        /// The optional action that will initialize the static <see cref="Loader"/> property with
        /// the <see cref="IHostingLoader"/> implemention.
        /// </param>
        public HostingManagerFactory(Action loaderAction = null)
        {
            if (loaderAction != null)
            {
                loaderAction();
                Covenant.Assert(Loader != null);
            }
        }

        /// <summary>
        /// Ensures that the factory has been initialized.
        /// </summary>
        private void CheckInitialized()
        {
            Covenant.Assert(Loader != null, $"[{nameof(Loader)}] is not initialized.  You must call [HostingLoader.Initialize()] in the [Neon.Hive.Hosting] assembly first.");
        }

        /// <inheritdoc/>
        public HostingManager GetManager(HiveProxy hive, string logFolder = null)
        {
            CheckInitialized();

            return Loader.GetManager(hive, logFolder);
        }

        /// <inheritdoc/>
        public bool IsCloudEnvironment(HostingEnvironments environment)
        {
            CheckInitialized();

            return Loader.IsCloudEnvironment(environment);
        }

        /// <inheritdoc/>
        public void Validate(HiveDefinition hiveDefinition)
        {
            CheckInitialized();

            Covenant.Requires<ArgumentNullException>(hiveDefinition != null);

            var hive    = new HiveProxy(hiveDefinition);
            var manager = GetManager(hive);

            if (manager == null)
            {
                throw new HiveException($"Cannot locate a [{nameof(IHostingManager)}] implementation for the [{hiveDefinition.Hosting.Environment}] hosting environment.");
            }

            manager.Validate(hiveDefinition);
        }
    }
}
