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
using Neon.Diagnostics;
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
        //---------------------------------------------------------------------
        // Static members

        private static INeonLogger                  log = LogManager.Default.GetLogger<HiveMQManager>();

        //---------------------------------------------------------------------
        // Local types

        /// <summary>
        /// Provides messaging related services for internal neonHIVE components.
        /// </summary>
        public class InternalManager
        {
            private object                          syncLock = new object();
            private HiveMQManager                   parent;
            private HiveBus                         neonHiveBus;
            private HiveMQSettings                  bootstrapSettings;
            private EventHandler<HiveMQSettings>    bootstrapChangedEvent;
            private Task                            bootstrapChangeDetector;

            /// <summary>
            /// Internal constructor.
            /// </summary>
            /// <param name="parent">The parent <see cref="HiveMQManager"/>.</param>
            internal InternalManager(HiveMQManager parent)
            {
                this.parent = parent;
            }

            /// <summary>
            /// <para>
            /// <b>INTERNAL USE ONLY:</b> Returns a <see cref="HiveBus"/> connected to the hive
            /// <b>neon</b> virtual host.  This property is intended for use only by neonHIVE
            /// tools, services and containers.
            /// </para>
            /// <note>
            /// <b>WARNING:</b> The <see cref="HiveBus"/> instance returned should <b>NEVER BE DISPOSED</b>.
            /// </note>
            /// </summary>
            /// <param name="useBootstrap">
            /// Optionally specifies that the settings returned should directly
            /// reference to the HiveMQ cluster nodes rather than routing traffic
            /// through the <b>private</b> traffic manager.  This is used internally
            /// to resolve chicken-and-the-egg dilemmas for the traffic manager and
            /// proxy implementations that rely on HiveMQ messaging.
            /// </param>
            public HiveBus NeonHiveBus(bool useBootstrap = false)
            {
                var bus = neonHiveBus;

                if (bus != null)
                {
                    return bus;
                }

                lock (syncLock)
                {
                    if (neonHiveBus != null)
                    {
                        return neonHiveBus;
                    }

                    if (!useBootstrap)
                    {
                        return neonHiveBus = parent.GetNeonSettings(useBootstrap: false).ConnectHiveBus();
                    }

                    // Remember the bootstrap settings and then start a task that
                    // periodically polls Consul for settings changes to raise the
                    // [HiveMQBootstrapChanged] event when changes happen.

                    bootstrapSettings       = parent.GetNeonSettings(useBootstrap);
                    neonHiveBus             = bootstrapSettings.ConnectHiveBus();
                    bootstrapChangeDetector = Task.Run(() => BootstrapChangeDetector());

                    return neonHiveBus;
                }
            }

            /// <summary>
            /// Polls Consul for changes to the <b>neon</b> virtual host HiveMQ 
            /// bootstrap settings and raises the <see cref="HiveMQBootstrapChanged"/>
            /// when this happens.
            /// </summary>
            /// <returns>The tracking <see cref="Task"/>.</returns>
            private async Task BootstrapChangeDetector()
            {
                var pollInterval = TimeSpan.FromSeconds(120);

                // Delay for a random period of time between [0..pollInterval].  This
                // will help prevent Consul traffic spikes when services are started 
                // at the same time.

                await Task.Delay(NeonHelper.RandTimespan(pollInterval));

                // This will spin forever once started when a NeonHiveBus using bootstrap
                // settings is created above.  This polls Consul for changes to the [neon]
                // HiveMQ virtual host settings stored in Consul.  We'll be performing two
                // Consul lookups for each poll (one to get the [neon] vhost settings and 
                // the other to obtain the bootstrap settings.
                //
                // When a settings change is detected, we'll first ensure that we've 
                // establisted a new [neonHiveBus] connection using the new settings and 
                // then we'll raise the change event.

                while (true)
                {
                    try
                    {
                        var latestBootstrapSettngs = parent.GetNeonSettings(useBootstrap: true);

                        if (!NeonHelper.JsonEquals(bootstrapSettings, latestBootstrapSettngs))
                        {
                            // The latest bootstrap settings don't match what we used to
                            // connect the current [bus].

                            lock (syncLock)
                            {
                                bootstrapSettings = latestBootstrapSettngs;
                                neonHiveBus       = bootstrapSettings.ConnectHiveBus();
                            }

                            var handler = bootstrapChangedEvent;

                            handler?.Invoke(this, latestBootstrapSettngs);
                        }
                    }
                    catch (Exception e)
                    {
                        log.LogError(e);
                    }

                    await Task.Delay(pollInterval);
                }
            }

            /// <summary>
            /// <para>
            /// Raised when a change to the HiveMQ bootstrap settings is detected after
            /// <see cref="NeonHiveBus(bool)"/> has been called with <c>useBootstrap: true</c>.
            /// This is used by internal neonHIVE services to handle changes to the HiveMQ
            /// cluster topology cleanly.  The event arguments will be the new
            /// <see cref="HiveMQSettings"/>.
            /// </para>
            /// <note>
            /// It may take a minute or two for the event to fire after the bootstrap setting
            /// change actually happened because change detection polling happens on a
            /// 120 second interval.
            /// </note>
            /// </summary>
            public event EventHandler<HiveMQSettings> HiveMQBootstrapChanged
            {
                add
                {
                    lock (syncLock)
                    {
                        bootstrapChangedEvent += value;
                    }
                }

                remove
                {
                    lock (syncLock)
                    {
                        bootstrapChangedEvent -= value;
                    }
                }
            }

            /// <summary>
            /// <b>INTERNAL USE ONLY:</b> Creates the <see cref="HiveMQChannels.ProxyNotify"/> 
            /// channel if it doesn't already exist and returns it.
            /// </summary>
            /// <param name="useBootstrap">
            /// Optionally specifies that the settings returned should directly
            /// reference to the HiveMQ cluster nodes rather than routing traffic
            /// through the <b>private</b> traffic manager.  This is used internally
            /// to resolve chicken-and-the-egg dilemmas for the traffic manager and
            /// proxy implementations that rely on HiveMQ messaging.
            /// </param>
            /// <param name="publishOnly">
            /// Optionally specifies that the channel instance returned will only be able
            /// to publish messages and not consume them.  Enabling this avoid the creation
            /// of a queue that will unnecessary for this situation.
            /// </param>
            /// <returns>The requested <see cref="BroadcastChannel"/>.</returns>
            /// <remarks>
            /// <note>
            /// You'll need to register any consumers on the channel returned and
            /// then call <see cref="BroadcastChannel.Open()"/> to open the channel.
            /// </note>
            /// <note>
            /// The instance returned should be disposed when you're done with it.
            /// </note>
            /// </remarks>
            public BroadcastChannel GetProxyNotifyChannel(bool useBootstrap = false, bool publishOnly = false)
            {
                // WARNING:
                //
                // Changing any of the channel properties will require that underlying
                // queue be removed and recreated and all services and containers that
                // use the queue be restarted.

                return NeonHiveBus(useBootstrap).GetBroadcastChannel(
                    name: HiveMQChannels.ProxyNotify,
                    durable: true,
                    autoDelete: false,
                    messageTTL: null,
                    maxLength: null,
                    maxLengthBytes: null,
                    publishOnly: publishOnly);
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private HiveProxy   hive;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="hive">The parent <see cref="HiveProxy"/>.</param>
        internal HiveMQManager(HiveProxy hive)
        {
            Covenant.Requires<ArgumentNullException>(hive != null);

            this.hive     = hive;
            this.Internal = new InternalManager(this);
        }

        /// <summary>
        /// Returns a <see cref="ManagementClient"/> instance that can be used to perform
        /// management related operations on the HiveMQ.
        /// </summary>
        /// <param name="useBootstrap">
        /// Optionally specifies that the client returned should connect
        /// directly to the HiveMQ cluster nodes rather than routing traffic
        /// through the <b>private</b> traffic manager.  This is used internally
        /// to resolve chicken-and-the-egg dilemmas for the traffic manager and
        /// proxy implementations that rely on HiveMQ messaging.
        /// </param>
        public ManagementClient ConnectHiveMQManager(bool useBootstrap = false)
        {
            return GetRootSettings(useBootstrap).ConnectManager();
        }

        /// <summary>
        /// Parses and returns the specified hive global as a <see cref="HiveMQSettings"/>.
        /// </summary>
        /// <param name="globalName">Identifies the source hive global name.</param>
        /// <returns>The parsed <see cref="HiveMQSettings"/>.</returns>
        /// <exception cref="HiveException">Thrown if the required global setting is not present or could not be deserialized.</exception>
        private HiveMQSettings GetHiveMQSettings(string globalName)
        {
            if (!hive.Globals.TryGetObject<HiveMQSettings>(globalName, out var settings))
            {
                throw new HiveException($"The [{globalName}] hive global does not exist in Consul or could not be parsed as a [{nameof(HiveMQSettings)}..");
            }

            return settings;
        }

        /// <summary>
        /// <para>
        /// Returns the <see cref="HiveMQSettings"/> for the <see cref="HiveConst.HiveMQSysadminUser"/>.
        /// You can use this to retrieve a client that can perform messaging operations within the
        /// root <b>/</b>, <see cref="HiveConst.HiveMQAppVHost"/>, or <see cref="HiveConst.HiveMQNeonVHost"/>
        /// virtual hosts.
        /// </para>
        /// </summary>
        /// <param name="useBootstrap">
        /// Optionally specifies that the settings returned should directly
        /// reference to the HiveMQ cluster nodes rather than routing traffic
        /// through the <b>private</b> traffic manager.  This is used internally
        /// to resolve chicken-and-the-egg dilemmas for the traffic manager and
        /// proxy implementations that rely on HiveMQ messaging.
        /// </param>
        /// <exception cref="HiveException">Thrown if the required global setting is not present.</exception>
        public HiveMQSettings GetRootSettings(bool useBootstrap = false)
        {
            var settings = GetHiveMQSettings(HiveGlobals.HiveMQSettingSysadmin);

            if (!useBootstrap)
            {
                return settings;
            }

            var bootstrap = GetBootstrapSettings();

            bootstrap.Username    = settings.Username;
            bootstrap.Password    = settings.Password;
            bootstrap.VirtualHost = settings.VirtualHost;

            return bootstrap;
        }

        /// <summary>
        /// <para>
        /// Returns the <see cref="HiveMQSettings"/> for the <see cref="HiveConst.HiveMQNeonUser"/>.
        /// You can use this to retrieve a client that can perform messaging operations within the
        /// <see cref="HiveConst.HiveMQNeonVHost"/> virtual host.
        /// </para>
        /// </summary>
        /// <param name="useBootstrap">
        /// Optionally specifies that the settings returned should directly
        /// reference to the HiveMQ cluster nodes rather than routing traffic
        /// through the <b>private</b> traffic manager.  This is used internally
        /// to resolve chicken-and-the-egg dilemmas for the traffic manager and
        /// proxy implementations that rely on HiveMQ messaging.
        /// </param>
        /// <exception cref="HiveException">Thrown if the required global setting is not present.</exception>
        public HiveMQSettings GetNeonSettings(bool useBootstrap = false)
        {
            var settings = GetHiveMQSettings(HiveGlobals.HiveMQSettingsNeon);

            if (!useBootstrap)
            {
                return settings;
            }

            var bootstrap = GetBootstrapSettings();

            bootstrap.Username    = settings.Username;
            bootstrap.Password    = settings.Password;
            bootstrap.VirtualHost = settings.VirtualHost;

            return bootstrap;
        }

        /// <summary>
        /// <para>
        /// Returns the <see cref="HiveMQSettings"/> for the <see cref="HiveConst.HiveMQAppUser"/>.
        /// You can use this to retrieve a client that can perform messaging operations within the
        /// <see cref="HiveConst.HiveMQAppVHost"/> virtual host.
        /// </para>
        /// </summary>
        /// <param name="useBootstrap">
        /// Optionally specifies that the settings returned should directly
        /// reference to the HiveMQ cluster nodes rather than routing traffic
        /// through the <b>private</b> traffic manager.  This is used internally
        /// to resolve chicken-and-the-egg dilemmas for the traffic manager and
        /// proxy implementations that rely on HiveMQ messaging.
        /// </param>
        /// <exception cref="HiveException">Thrown if the required global setting is not present.</exception>
        public HiveMQSettings GetAppSettings(bool useBootstrap = false)
        {
            var settings = GetHiveMQSettings(HiveGlobals.HiveMQSettingsApp);

            if (!useBootstrap)
            {
                return settings;
            }

            var bootstrap = GetBootstrapSettings();

            bootstrap.Username    = settings.Username;
            bootstrap.Password    = settings.Password;
            bootstrap.VirtualHost = settings.VirtualHost;

            return bootstrap;
        }

        /// <summary>
        /// <para>
        /// <b>INTERNAL USE ONLY:</b> Returns the bootstrap <see cref="HiveMQSettings"/> 
        /// for the <see cref="HiveConst.HiveMQNeonVHost"/> that directly reference the HiveMQ
        /// nodes rather than traffic manager rules.
        /// </para>
        /// <para>
        /// This works by obtaining the bootstrap settings from the Consul <see cref="HiveGlobals.HiveMQSettingsBootstrap"/>
        /// setting combined with the credentials from <see cref="GetNeonSettings(bool)"/>.
        /// </para>
        /// </summary>
        /// <exception cref="HiveException">Thrown if the required global setting is not present.</exception>
        public HiveMQSettings GetBootstrapSettings()
        {
            return GetHiveMQSettings(HiveGlobals.HiveMQSettingsBootstrap);
        }

        /// <summary>
        /// <para>
        /// <b>Internal use by neonHIVE services only:</b> Generates a <see cref="HiveMQSettings"/> instance
        /// that directly references the HiveMQ nodes and then persists this to Consul as 
        /// <see cref="HiveGlobals.HiveMQSettingsBootstrap"/>.
        /// </para>
        /// <note>
        /// The persisted settings do not include any credentials.
        /// </note>
        /// </summary>
        public void SaveBootstrapSettings()
        {
            var settings = new HiveMQSettings()
            {
                AmqpPort    = HiveHostPorts.HiveMQAMQP,
                AdminPort   = HiveHostPorts.HiveMQManagement,
                TlsEnabled  = false,
                Username    = null,
                Password    = null,
                VirtualHost = HiveConst.HiveMQNeonVHost
            };

            foreach (var node in hive.Definition.SortedNodes.Where(n => n.Labels.HiveMQ))
            {
                settings.AmqpHosts.Add($"{node.Name}.{hive.Definition.Hostnames.HiveMQ}");
            }

            foreach (var node in hive.Definition.SortedNodes.Where(n => n.Labels.HiveMQManager))
            {
                settings.AdminHosts.Add($"{node.Name}.{hive.Definition.Hostnames.HiveMQ}");
            }

            hive.Globals.Set(HiveGlobals.HiveMQSettingsBootstrap, settings);
        }

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Returns a manager used internally by hive components
        /// and services.
        /// </summary>
        public InternalManager Internal { get; private set; }
    }
}
