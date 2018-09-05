//-----------------------------------------------------------------------------
// FILE:	    RabbitMQOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.IO;

namespace Neon.Hive
{
    /// <summary>
    /// Specifies the options for configuring the hive integrated
    /// <a href="https://rabbitmq.com/">RabbitMQ message queue</a>
    /// cluster.
    /// </summary>
    public class RabbitMQOptions
    {
        private const string defaultRamLimit         = "250MB";
        private const string defaultCompiledRamLimit = "500MB";
        private const string defaultRamHighWatermark = "0.50";
        private const string defaultUsername         = HiveConst.DefaultUsername;
        private const string defaultPassword         = HiveConst.DefaultPassword;
        private const bool   defaultPrecompile       = false;
        private const string defaultPartitionMode    = "autoheal";
        private const string defaultRabbitMQImage    = HiveConst.NeonPublicRegistry + "/neon-rabbitmq:latest";

        private string ramLimit = null;

        /// <summary>
        /// <para>
        /// Specifies the maximum RAM to be allocated to each RabbitMQ node container.
        /// This can be a long byte count or a long with units like <b>512MB</b> or <b>2GB</b>.
        /// </para>
        /// <para>
        /// This defaults to <b>500MB</b> if <see cref="Precompile"/><c>true</c> or
        /// <b>250MB</b> when precompiling is disabled.  Note that these are the
        /// minimum values for these situations.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "RamLimit", Required = Required.Default)]
        [DefaultValue(null)]
        public string RamLimit
        {
            get
            {
                if (string.IsNullOrEmpty(ramLimit))
                {
                    return Precompile ? defaultCompiledRamLimit : defaultRamLimit;
                }
                else
                {
                    return ramLimit;
                }
            }

            set { ramLimit = value; }
        }

        /// <summary>
        /// <para>
        /// Specifies the how much of <see cref="RamLimit"/> each node can allocate for
        /// caching and internal use.  This may be expressed as a percentage of total RAM
        /// like <b>0.49</b> or <b>49%</b> or as an absolute number of bytes like 
        /// <b>250000000</b> or <b>250MB</b>.  This defaults to <b>0.50</b>.
        /// </para>
        /// <note>
        /// The default value is very conservative especially as you increase <see cref="RamLimit"/>.
        /// For larger RAM values you should be able allocate a larger percentage of RAM for
        /// this data.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "RamHighWatermark", Required = Required.Default)]
        [DefaultValue(defaultRamHighWatermark)]
        public string RamHighWatermark { get; set;  } = defaultRamHighWatermark;

        /// <summary>
        /// <para>
        /// Specifies the minimum allowed free disk space before RabbitMQ will begin throttling
        /// message traffic to avoid fill up the drive.  This can be a long byte count or a long
        /// with units like <b>512MB</b> or <b>2GB</b>.
        /// </para>
        /// <para>
        /// This defaults to twice <see cref="RamLimit"/> plus <b>1GB</b> to avoid having 
        /// RabbitMQ consume so much disk space that the hive host node is impacted.
        /// </para>
        /// <para>
        /// This cannot be less than <b>1GB</b>.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "DiskFreeLimit", Required = Required.Default)]
        [DefaultValue(null)]
        public string DiskFreeLimit { get; set; }

        /// <summary>
        /// Specifies the username used to secure the cluster.  This defaults to <b>sysadmin</b>.
        /// </summary>
        [JsonProperty(PropertyName = "Username", Required = Required.Default)]
        [DefaultValue(defaultUsername)]
        public string Username { get; set; } = defaultUsername;

        /// <summary>
        /// Specifies the password used to secure the cluster.  This defaults to <b>password</b>>.
        /// </summary>
        [JsonProperty(PropertyName = "Password", Required = Required.Default)]
        [DefaultValue(defaultPassword)]
        public string Password { get; set; } = defaultPassword;

        /// <summary>
        /// Specifies the shared secret clustered RabbitMQ nodes will use for mutual authentication.
        /// A secure password will be generated if this isn't specified.
        /// </summary>
        [JsonProperty(PropertyName = "ErlangCookie", Required = Required.Default)]
        [DefaultValue(null)]
        public string ErlangCookie { get; set; }

        /// <summary>
        /// Specifies that RabbitMQ should be precompiled for 20-50% better performance at the
        /// cost of 30-45 seconds longer for the nodes to start and a minimum of 250MB of 
        /// additional RAM per instance.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Precompile", Required = Required.Default)]
        [DefaultValue(defaultPrecompile)]
        public bool Precompile { get; set; } = defaultPrecompile;

        /// <summary>
        /// <para>
        /// Specifies how the RabbitMQ cluster will deal with network partitions.  The possible
        /// values are <b>autoheal</b>, <b>pause_minority</b>, or <b>pause_if_all_down</b>.
        /// This defaults to <b>autoheal</b> to favor availability over the potential for data
        /// loss.  The other modes may require manual intervention to being the cluster back
        /// online even after simply shutting the cluster down.
        /// </para>
        /// <para>
        /// See <a href="https://www.rabbitmq.com/partitions.html">Clustering and Network Partitions</a>
        /// for more information.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "PartitionMode", Required = Required.Default)]
        [DefaultValue(defaultPartitionMode)]
        public string PartitionMode { get; set; } = defaultPartitionMode;

        /// <summary>
        /// The Docker image to be used to provision the <b>neon-rabbitmq</b> service.
        /// This defaults to <b>nhive/rabbitmq:latest</b>.
        /// </summary>
        [JsonProperty(PropertyName = "RabbitMQImage", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(defaultRabbitMQImage)]
        public string RabbitMQImage { get; set; } = defaultRabbitMQImage;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="hiveDefinition">The hive definition.</param>
        /// <exception cref="HiveDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(HiveDefinition hiveDefinition)
        {
            RamLimit         = RamLimit ?? defaultRamLimit;
            RamHighWatermark = RamHighWatermark ?? defaultRamHighWatermark;
            Username         = Username ?? defaultUsername;
            Password         = Password ?? defaultPassword;
            PartitionMode    = PartitionMode ?? defaultPartitionMode;
            PartitionMode    = PartitionMode.ToLowerInvariant();
            RabbitMQImage    = RabbitMQImage ?? defaultRabbitMQImage;

            if (string.IsNullOrWhiteSpace(ErlangCookie))
            {
                ErlangCookie = NeonHelper.GetRandomPassword(20);
            }

            // RamHighWatermark: We're going to keep things simple and convert relative
            // percentage values to a number between [0..1] and we're going to convert
            // absolute bytes units into a simple number (without units).  This simplifies
            // the RabbitMQ Docker entrypoint script so all it needs to do is look for a
            // decimal point to identify a relative limit vs. an absolute limit.  The
            // script won't need to do percentage or unit conversions.

            if (RamHighWatermark.EndsWith("%"))
            {
                // RamHighWatermark is a relative percentage.

                var numberPart = RamHighWatermark.Substring(0, RamHighWatermark.Length - 1);

                if (!double.TryParse(numberPart, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) || number <= 0.0 || number >= 100.0)
                {
                    throw new HiveDefinitionException($"[{nameof(RabbitMQOptions)}.{nameof(RamHighWatermark)}={RamHighWatermark}] is not within: 0% < {nameof(RamHighWatermark)} <= 100%");
                }

                RamHighWatermark = (number / 100).ToString("0.00#", CultureInfo.InvariantCulture);
            }
            else if (RamHighWatermark.Contains('.'))
            {
                // RamHighWatermark is a relative number between [0..1].

                if (!double.TryParse(RamHighWatermark, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) || number <= 0.0 || number >= 1.0)
                {
                    throw new HiveDefinitionException($"[{nameof(RabbitMQOptions)}.{nameof(RamHighWatermark)}={RamHighWatermark}] is not within: 0.0 < {nameof(RamHighWatermark)} <= 1.0");
                }

                RamHighWatermark = number.ToString("0.00#", CultureInfo.InvariantCulture);
            }
            else
            {
                // RamHighWaterMark is absolute.

                var number = HiveDefinition.ValidateSize(RamHighWatermark, this.GetType(), nameof(RamHighWatermark));

                if (number <= 0)
                {
                    throw new HiveDefinitionException($"[{nameof(RabbitMQOptions)}.{nameof(RamHighWatermark)}={RamHighWatermark}] must be greater than 0.");
                }

                RamHighWatermark = number.ToString();
            }

            var ramSize = HiveDefinition.ValidateSize(RamLimit, this.GetType(), nameof(RamLimit));

            if (Precompile && ramSize < 500 * NeonHelper.Mega)
            {
                throw new HiveDefinitionException($"[{nameof(RabbitMQOptions)}.{nameof(RamLimit)}={RamLimit}] cannot be less than [500MB] when [{nameof(RabbitMQOptions)}.{nameof(Precompile)}={Precompile}].");
            }
            else if (!Precompile && ramSize < 250 * NeonHelper.Mega)
            {
                throw new HiveDefinitionException($"[{nameof(RabbitMQOptions)}.{nameof(RamLimit)}={RamLimit}] cannot be less than [250MB] when [{nameof(RabbitMQOptions)}.{nameof(Precompile)}={Precompile}].");
            }

            // DiskFreeLimit: We're going to keep things simple and convert relative
            // percentage values to a number between [0..1] and we're going to convert
            // absolute bytes units into a simple number (without units).  This simplifies
            // the RabbitMQ Docker entrypoint script so all it needs to do is look for a
            // decimal point to identify a relative limit vs. an absolute limit.  The
            // script won't need to do percentage or unit conversions.

            if (string.IsNullOrWhiteSpace(DiskFreeLimit))
            {
                DiskFreeLimit = ((2 * ramSize) + (1 * NeonHelper.Giga)).ToString();
            }

            if (DiskFreeLimit.EndsWith("%"))
            {
                // DiskFreeLimit is a relative percentage.

                var numberPart = DiskFreeLimit.Substring(0, DiskFreeLimit.Length - 1);

                if (!double.TryParse(numberPart, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) || number <= 0.0 || number >= 100.0)
                {
                    throw new HiveDefinitionException($"[{nameof(RabbitMQOptions)}.{nameof(DiskFreeLimit)}={DiskFreeLimit}] is not within: 0% < {nameof(DiskFreeLimit)} <= 100%");
                }

                DiskFreeLimit = (number / 100).ToString("0.00#", CultureInfo.InvariantCulture);
            }
            else if (DiskFreeLimit.Contains('.'))
            {
                // DiskFreeLimit is a relative number between [0..1].

                if (!double.TryParse(DiskFreeLimit, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) || number <= 0.0 || number >= 1.0)
                {
                    throw new HiveDefinitionException($"[{nameof(RabbitMQOptions)}.{nameof(DiskFreeLimit)}={DiskFreeLimit}] is not within: 0.0 < {nameof(DiskFreeLimit)} <= 1.0");
                }

                DiskFreeLimit = number.ToString("0.00#", CultureInfo.InvariantCulture);
            }
            else
            {
                // DiskFreeLimit is absolute.

                var number = HiveDefinition.ValidateSize(DiskFreeLimit, this.GetType(), nameof(DiskFreeLimit));

                if (number < 1 * NeonHelper.Giga)
                {
                    throw new HiveDefinitionException($"[{nameof(RabbitMQOptions)}.{nameof(DiskFreeLimit)}={DiskFreeLimit}] must be greater than [1GB].");
                }

                DiskFreeLimit = number.ToString();
            }

            switch (PartitionMode)
            {
                case "autoheal":
                case "pause_minority":
                case "pause_if_all_down":

                    break;

                default:

                    throw new HiveDefinitionException($"[{nameof(RabbitMQOptions)}.{nameof(PartitionMode)}={PartitionMode}] is not valid.  Specify one of [autoheal], [pause_minority], or [pause_if_all_down].");
            }

            // We need to assign hive nodes to host the RabbitMQ instances.  We're going to do
            // this by examining and possibly setting the RabbitMQ node labels.  If no hive nodes
            // have this label set, then we'll set these for all manager nodes.  Otherwise, we'll
            // deploy to the marked nodes.

            if (hiveDefinition.Nodes.Count(n => n.Labels.RabbitMQ) == 0)
            {
                foreach (var manager in hiveDefinition.Managers)
                {
                    manager.Labels.RabbitMQ = true;
                }
            }
        }
    }
}
