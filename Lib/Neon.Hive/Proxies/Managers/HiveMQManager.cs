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
using EasyNetQ.Management.Client;

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
        /// Returns a <see cref="ManagementClient"/> instance that can be used to perform
        /// management related operations on the HiveMQ.
        /// </summary>
        public ManagementClient ConnectHiveMQManager()
        {
            return SystemSettings.ConnectManager();
        }

        /// <summary>
        /// <para>
        /// Returns the <see cref="HiveMQSettings"/> for the <see cref="HiveMQOptions.SysadminUser"/>.
        /// You can use this to retrieve a client that can perform messaging operations within the
        /// root <b>/</b>, <see cref="HiveMQOptions.AppVHost"/>, or <see cref="HiveMQOptions.NeonVHost"/>
        /// virtual hosts.
        /// </para>
        /// <note>
        /// This requires that the <b>neon-hivemq-sysadmin</b> Docker secret be mapped into the
        /// executing container or initialized with <see cref="HiveHelper"/>.
        /// </note>
        /// </summary>
        /// <exception cref="HiveException">Thrown if the required Docker secret is not present.</exception>
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
        /// Returns the <see cref="HiveMQSettings"/> for the <see cref="HiveMQOptions.NeonUser"/>.
        /// You can use this to retrieve a client that can perform messaging operations within the
        /// <see cref="HiveMQOptions.NeonVHost"/> virtual host.
        /// </para>
        /// <note>
        /// This requires that the <b>neon-hivemq-neon</b> Docker secret be mapped into the
        /// executing container or initialized with <see cref="HiveHelper"/>.
        /// </note>
        /// </summary>
        /// <exception cref="HiveException">Thrown if the required Docker secret is not present.</exception>
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
        /// Returns the <see cref="HiveMQSettings"/> for the <see cref="HiveMQOptions.AppUser"/>.
        /// You can use this to retrieve a client that can perform messaging operations within the
        /// <see cref="HiveMQOptions.AppVHost"/> virtual host.
        /// </para>
        /// <note>
        /// This requires that the <b>neon-hivemq-app</b> Docker secret be mapped into the
        /// executing container or initialized with <see cref="HiveHelper"/>.
        /// </note>
        /// </summary>
        /// <exception cref="HiveException">Thrown if the required Docker secret is not present.</exception>
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

        /// <summary>
        /// <para>
        /// <b>Internal neonHIVE use only:</b> Returns the bootstrap <see cref="HiveMQSettings"/> 
        /// for the <see cref="HiveMQOptions.NeonVHost"/> that directly reference the HiveMQ
        /// nodes rather than load balancer rules.
        /// </para>
        /// <para>
        /// This works by obtaining the bootstrap settings from the Consul <see cref="HiveGlobals.HiveMQBootstrap"/>
        /// setting combined with the credentials from <see cref="NeonSettings"/>.
        /// </para>
        /// <note>
        /// This requires that the <b>neon-hivemq-neon</b> Docker secret be mapped into the
        /// executing container or initialized with <see cref="HiveHelper"/>.
        /// </note>
        /// </summary>
        /// <exception cref="HiveException">Thrown if the required Docker secret is not present.</exception>
        public HiveMQSettings BootstrapSettings
        {
            get
            {
                if (!hive.Globals.TryGetJson<HiveMQSettings>(HiveGlobals.HiveMQBootstrap, out var bootstrapSettings))
                {
                    throw new HiveException($"Global setting [{HiveGlobals.HiveMQBootstrap}] is not present or could not be parsed.");
                }

                var neonSettings = NeonSettings;

                bootstrapSettings.Username = neonSettings.Username;
                bootstrapSettings.Password = neonSettings.Password;

                return bootstrapSettings;
            }
        }

        /// <summary>
        /// <para>
        /// <b>Internal use by neonHIVE services only:</b> Generates a <see cref="HiveMQSettings"/> instance
        /// that directly references the HiveMQ nodes and then persists this to Consul as 
        /// <see cref="HiveGlobals.HiveMQBootstrap"/>.
        /// </para>
        /// <note>
        /// The persisted settings do not include any credentials.  Consumers will need to 
        /// combine credentials obtained via the <b>neon-hivemq-neon</b> Docker secret or
        /// via <see cref="NeonSettings"/>.
        /// </note>
        /// </summary>
        public void SaveBootstrapSettings()
        {
            var settings = new HiveMQSettings()
            {
                AmqpPort    = HiveHostPorts.ProxyPrivateHiveMQAMPQ,
                AdminPort   = HiveHostPorts.ProxyPrivateHiveMQAdmin,
                TlsEnabled  = false,
                Username    = null,
                Password    = null,
                VirtualHost = hive.Definition.HiveMQ.NeonVHost
            };

            foreach (var node in hive.Definition.SortedNodes.Where(n => n.Labels.HiveMQ))
            {
                settings.AmqpHosts.Add($"{node.Name}.{hive.Definition.Hostnames.HiveMQ}");
            }

            foreach (var node in hive.Definition.SortedNodes.Where(n => n.Labels.HiveMQManager))
            {
                settings.AdminHosts.Add($"{node.Name}.{hive.Definition.Hostnames.HiveMQ}");
            }

            hive.Globals.SetJson(HiveGlobals.HiveMQBootstrap, settings);
        }
    }
}
