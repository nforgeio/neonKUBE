//-----------------------------------------------------------------------------
// FILE:	    HiveMQManager.cs
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

using Consul;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Cryptography;
using Neon.HiveMQ;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

namespace Neon.Hive
{
    /// <summary>
    /// Handles hive message cluster related operations.
    /// </summary>
    public sealed class HiveMQManager
    {
        private HiveProxy hive;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="hive">The parent <see cref="HiveProxy"/>.</param>
        internal HiveMQManager(HiveProxy hive)
        {
            Covenant.Requires<ArgumentNullException>(hive != null);

            this.hive = hive;
        }

        /// <summary>
        /// <para>
        /// Returns the <see cref="HiveMQSettings"/> for the <see cref="HiveMQOptions.SysadminAccount"/>.
        /// You can use this to retrieve a client that can perform messaging operations within the
        /// root <b>/</b>, <see cref="HiveMQOptions.AppVHost"/>, or <see cref="HiveMQOptions.NeonVHost"/>
        /// virtual hosts.
        /// </para>
        /// <note>
        /// This requires that the <b>neon-hivemq-sysadmin</b> Docker secret be mapped into the
        /// executing container or initialized with <see cref="HiveHelper"/>.
        /// </note>
        /// </summary>
        /// <exception cref="HiveException">Thrown if thr required Docker secret is not present.</exception>
        public HiveMQSettings SystemSettings
        {
            get
            {
                var secretName = "neon-hivemq-sysadmin";
                var settings   = HiveHelper.GetSecret<HiveMQSettings>(secretName);

                if (settings == null)
                {
                    throw new HiveException($"Secret [{secretName}] is not present.");
                }

                return settings;
            }
        }

        /// <summary>
        /// <para>
        /// Returns the <see cref="HiveMQSettings"/> for the <see cref="HiveMQOptions.NeonAccount"/>.
        /// You can use this to retrieve a client that can perform messaging operations within the
        /// <see cref="HiveMQOptions.NeonVHost"/> virtual host.
        /// </para>
        /// <note>
        /// This requires that the <b>neon-hivemq-neon</b> Docker secret be mapped into the
        /// executing container or initialized with <see cref="HiveHelper"/>.
        /// </note>
        /// </summary>
        /// <exception cref="HiveException">Thrown if thr required Docker secret is not present.</exception>
        public HiveMQSettings NeonSettings
        {
            get
            {
                var secretName = "neon-hivemq-neon";
                var settings   = HiveHelper.GetSecret<HiveMQSettings>(secretName);

                if (settings == null)
                {
                    throw new HiveException($"Secret [{secretName}] is not present.");
                }

                return settings;
            }
        }

        /// <summary>
        /// <para>
        /// Returns the <see cref="HiveMQSettings"/> for the <see cref="HiveMQOptions.AppAccount"/>.
        /// You can use this to retrieve a client that can perform messaging operations within the
        /// <see cref="HiveMQOptions.AppVHost"/> virtual host.
        /// </para>
        /// <note>
        /// This requires that the <b>neon-hivemq-app</b> Docker secret be mapped into the
        /// executing container or initialized with <see cref="HiveHelper"/>.
        /// </note>
        /// </summary>
        /// <exception cref="HiveException">Thrown if thr required Docker secret is not present.</exception>
        public HiveMQSettings AppSettings
        {
            get
            {
                var secretName = "neon-hivemq-app";
                var settings   = HiveHelper.GetSecret<HiveMQSettings>(secretName);

                if (settings == null)
                {
                    throw new HiveException($"Secret [{secretName}] is not present.");
                }

                return settings;
            }
        }
    }
}
