//-----------------------------------------------------------------------------
// FILE:	    EasyBusSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;

using EasyNetQ;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using RabbitMQ;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using Neon.Common;
using Neon.Diagnostics;

namespace Neon.HiveMQ
{
    /// <summary>
    /// Used to customize the properties for an EasyNetQ bus connection.
    /// </summary>
    /// <remarks>
    /// All of the properties in the class are nullable and they only override;
    /// the default <see cref="ConnectionConfiguration"/> settings when the
    /// property values aren't <c>null</c>.
    /// </remarks>
    public class EasyBusSettings
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public EasyBusSettings()
        {
        }

        /// <summary>
        /// Specifies that message processing should occur on background threads.
        /// This effectively defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "UseBackgroundThreads", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public bool? UseBackgroundThreads { get; set; }

        /// <summary>
        /// The application name.  This effectively defaults to the name of the application 
        /// executable file.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Name { get; set; }

        /// <summary>
        /// The product name.  This effectively defaults to the name of the application 
        /// executable file.
        /// </summary>
        [JsonProperty(PropertyName = "Product", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Product { get; set; }

        /// <summary>
        /// The platform name.  This effectively defaults to identifying the operating system
        /// and .NET execution environment.
        /// </summary>
        [JsonProperty(PropertyName = "Platform", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Platform { get; set; }

        /// <summary>
        /// Identifies the RabbitMQ client.  This effectively defaults to <b>EasyNetQ</b>.
        /// </summary>
        [JsonProperty(PropertyName = "Client", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("null")]
        public string Client { get; set; }

        /// <summary>
        /// Specifies that the RabbitMQ broker should persist messages sent to it.
        /// This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "PersistentMessages", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public bool? PersistentMessages { get; set; }

        /// <summary>
        /// <para>
        /// Enables published message confirmations.  When <c>true</c>, EasyNetQ will request
        /// that the RabbitMQ broker confirm that it has actually received and begun processing
        /// messages.  This defaults to <c>false</c>.
        /// </para>
        /// <note>
        /// This <a href="https://github.com/EasyNetQ/EasyNetQ/wiki/Publisher-Confirms">article</a>
        /// from 2014  discusses this as an alternative to AMQP transactions which are apparently 
        /// very slow in RabbitMQ.  I wonder if there's been any improvement since 2014.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "PublisherConfirms", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public bool? PublisherConfirms { get; set; }

        /// <summary>
        /// Timeout used when communicating with RabbitMQ.  This is expressed as seconds
        /// within the range of <b>0..65535</b>.  Specify <b>0</b> for an infinite timeout.
        /// This effectively defaults to <b>10 seconds</b>.
        /// </summary>
        [JsonProperty(PropertyName = "Timeout", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public ushort? Timeout { get; set; }

        /// <summary>
        /// The number of messages that will be delivered by RabbitMQ to the EasyNetQ
        /// client before an ACK is sent back to RabbitMQ.  This effectively defaults
        /// to <b>50</b>.
        /// </summary>
        [JsonProperty(PropertyName = "PrefetchCount", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public ushort? PrefetchCount { get; set; }

        /// <summary>
        /// The heartbeat interval to ensure a healthy connection to the backend
        /// RabbitMQ cluster.  This effectively defaults to <b>10 seconds</b>.
        /// </summary>
        [JsonProperty(PropertyName = "RequestedHeartbeat", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public ushort? RequestedHeartbeat { get; set; }

        /// <summary>
        /// Optionally describes the client to RabbitMQ.  This information will be reported
        /// on the RabbitMQ dashboard.  This defaults to <c>null</c>.
        /// </summary>
        [JsonProperty(PropertyName = "ClientProperties", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public IDictionary<string, object> ClientProperties { get; set; }

        /// <summary>
        /// Specifies the interval at which the message bus will attempt to reconnect
        /// to RabbitMQ.  This effectively defaults to <b>5 seconds</b>.
        /// </summary>
        [JsonProperty(PropertyName = "ConnectIntervalAttempt", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public TimeSpan? ConnectIntervalAttempt { get; set; }

        /// <summary>
        /// Applies any overriding properties from this <see cref="EasyBusSettings"/> instance
        /// to a <see cref="ConnectionConfiguration"/>.
        /// </summary>
        /// <param name="target">The target configuration.</param>
        internal void ApplyTo(ConnectionConfiguration target)
        {
            Covenant.Requires<ArgumentNullException>(target != null);

            target.UseBackgroundThreads   = UseBackgroundThreads ?? target.UseBackgroundThreads;
            target.Name                   = Name ?? target.Name;
            target.Product                = Product ?? target.Product;
            target.Platform               = Platform ?? NeonHelper.OsDescription;
            target.PersistentMessages     = PersistentMessages ?? target.PersistentMessages;
            target.PublisherConfirms      = PublisherConfirms ?? target.PublisherConfirms;
            target.Timeout                = Timeout ?? target.Timeout;
            target.PrefetchCount          = PrefetchCount ?? target.PrefetchCount;
            target.RequestedHeartbeat     = RequestedHeartbeat ?? target.RequestedHeartbeat;
            target.ConnectIntervalAttempt = ConnectIntervalAttempt ?? target.ConnectIntervalAttempt;

            if (ClientProperties != null)
            {
                foreach (var item in ClientProperties)
                {
                    target.ClientProperties.Add(item);
                }
            }

            target.ClientProperties["version"]  = Client ?? "EasyNetQ";
            target.ClientProperties["platform"] = Platform ?? $"{NeonHelper.FrameworkDescription}/{NeonHelper.OsDescription}";
            target.ClientProperties["product"]  = Product ?? Path.GetFileNameWithoutExtension(NeonHelper.GetEntryAssemblyPath());
        }
    }
}