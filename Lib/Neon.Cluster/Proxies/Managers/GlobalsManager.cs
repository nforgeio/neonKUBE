//-----------------------------------------------------------------------------
// FILE:	    GlobalsManager.cs
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

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

namespace Neon.Cluster
{
    /// <summary>
    /// Manages cluster global settings.
    /// </summary>
    public sealed class GlobalsManager
    {
        private ClusterProxy cluster;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="cluster">The parent <see cref="ClusterProxy"/>.</param>
        internal GlobalsManager(ClusterProxy cluster)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null);

            this.cluster = cluster;
        }

        /// <summary>
        /// Attempts to retrieve a named cluster global setting as a <c>string</c>.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="output">Returns as the setting value.</param>
        /// <returns><c>true</c> if the setting exists and was returned.</returns>
        /// <remarks>
        /// <note>
        /// Well known cluster setting names are defined in <see cref="NeonClusterGlobals"/>.
        /// </note>
        /// </remarks>
        public bool TryGetString(string name, out string output)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(ClusterDefinition.IsValidName(name));

            output = null;

            var key   = $"{NeonClusterConst.ClusterGlobalsKey}/{name}";
            var value = cluster.Consul.KV.GetStringOrDefault(key).Result;

            if (value == null)
            {
                return false;
            }

            output = value;

            return true;
        }

        /// <summary>
        /// Attempts to retrieve a named cluster global setting as a <c>bool</c>.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="output">Returns as the setting value.</param>
        /// <returns><c>true</c> if the setting exists and was returned.</returns>
        /// <exception cref="FormatException">Thrown if the setting value could not be parsed.</exception>
        /// <remarks>
        /// <note>
        /// Well known cluster setting names are defined in <see cref="NeonClusterGlobals"/>.
        /// </note>
        /// </remarks>
        public bool TryGetBool(string name, out bool output)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(ClusterDefinition.IsValidName(name));

            output = default(bool);

            var key   = $"{NeonClusterConst.ClusterGlobalsKey}/{name}";
            var value = cluster.Consul.KV.GetStringOrDefault(key).Result;

            if (value == null)
            {
                return false;
            }

            output = NeonHelper.ParseBool(value);

            return true;
        }

        /// <summary>
        /// Attempts to retrieve a named cluster global setting as an <c>int</c>.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="output">Returns as the setting value.</param>
        /// <returns><c>true</c> if the setting exists and was returned.</returns>
        /// <exception cref="FormatException">Thrown if the setting value could not be parsed.</exception>
        /// <remarks>
        /// <note>
        /// Well known cluster setting names are defined in <see cref="NeonClusterGlobals"/>.
        /// </note>
        /// </remarks>
        public bool TryGetInt(string name, out int output)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(ClusterDefinition.IsValidName(name));

            output = default(int);

            var key   = $"{NeonClusterConst.ClusterGlobalsKey}/{name}";
            var value = cluster.Consul.KV.GetStringOrDefault(key).Result;

            if (value == null)
            {
                return false;
            }

            output = int.Parse(value);

            return true;
        }

        /// <summary>
        /// Attempts to retrieve a named cluster global setting as a <c>long</c>.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="output">Returns as the setting value.</param>
        /// <returns><c>true</c> if the setting exists and was returned.</returns>
        /// <exception cref="FormatException">Thrown if the setting value could not be parsed.</exception>
        /// <remarks>
        /// <note>
        /// Well known cluster setting names are defined in <see cref="NeonClusterGlobals"/>.
        /// </note>
        /// </remarks>
        public bool TryGetLong(string name, out long output)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(ClusterDefinition.IsValidName(name));

            output = default(int);

            var key   = $"{NeonClusterConst.ClusterGlobalsKey}/{name}";
            var value = cluster.Consul.KV.GetStringOrDefault(key).Result;

            if (value == null)
            {
                return false;
            }

            output = long.Parse(value);

            return true;
        }

        /// <summary>
        /// Attempts to retrieve a named cluster global setting as a <c>double</c>.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="output">Returns as the setting value.</param>
        /// <returns><c>true</c> if the setting exists and was returned.</returns>
        /// <exception cref="FormatException">Thrown if the setting value could not be parsed.</exception>
        /// <remarks>
        /// <note>
        /// Well known cluster setting names are defined in <see cref="NeonClusterGlobals"/>.
        /// </note>
        /// </remarks>
        public bool TryGetDouble(string name, out double output)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(ClusterDefinition.IsValidName(name));

            output = default(double);

            var key   = $"{NeonClusterConst.ClusterGlobalsKey}/{name}";
            var value = cluster.Consul.KV.GetStringOrDefault(key).Result;

            if (value == null)
            {
                return false;
            }

            output = double.Parse(value);

            return true;
        }

        /// <summary>
        /// Attempts to retrieve a named cluster global setting as a <see cref="TimeSpan"/>.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="output">Returns as the setting value.</param>
        /// <returns><c>true</c> if the setting exists and was returned.</returns>
        /// <exception cref="FormatException">Thrown if the setting value could not be parsed.</exception>
        /// <remarks>
        /// <note>
        /// Well known cluster setting names are defined in <see cref="NeonClusterGlobals"/>.
        /// </note>
        /// </remarks>
        public bool TryGetTimeSpan(string name, out TimeSpan output)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(ClusterDefinition.IsValidName(name));

            output = default(TimeSpan);

            var key   = $"{NeonClusterConst.ClusterGlobalsKey}/{name}";
            var value = cluster.Consul.KV.GetStringOrDefault(key).Result;

            if (value == null)
            {
                return false;
            }

            output = TimeSpan.Parse(value);

            return true;
        }

        /// <summary>
        /// Sets or removes a cluster global setting, verifying that the setting 
        /// is intended to be modified by end users, that it is allowed to be
        /// removed and that the value is reasonable.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="value">The setting value.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if either the <paramref name="name"/> or <paramref name="value"/> are <c>null</c>.</exception>
        /// <exception cref="FormatException">Thrown if the name or value are invalid.</exception>
        /// <remarks>
        /// <para>
        /// This method works much like the various <see cref="Set(string, string)"/>
        /// methods except that it is restricted to modifying only 
        /// settings that most end-users will consider modifying:
        /// </para>
        /// <list type="table">
        /// <item>
        ///     <term><b>allow-unit-testing</b></term>
        ///     <description>
        ///     Indicates whether unit testing via <c>ClusterFixture</c> is to be 
        ///     allowed.  Possible values are: <b>true</b>, <b>false</b>, <b>yes</b>,
        ///     <b>no</b>, <b>on</b>, <b>off</b>, <b>1</b>, or <b>0</b>.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>log-rentention-days</b></term>
        ///     <description>
        ///     Specifies the number of days the cluster should retain <b>logstash</b>
        ///     and <b>metricbeat</b> logs.  This must be a positive integer.
        ///     </description>
        /// </item>
        /// </list>
        /// </remarks>
        public void SetUser(string name, string value)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            Covenant.Requires<ArgumentNullException>(value != null);

            switch (name)
            {
                case NeonClusterGlobals.UserAllowUnitTesting:

                    Set(name, NeonHelper.ParseBool(value));
                    break;

                case NeonClusterGlobals.UserLogRetentionDays:

                    if (!int.TryParse(value, out var logRetentionDays) || logRetentionDays <= 0)
                    {
                        throw new FormatException($"[log-rentention-days={value}] is invalid because it's not an integer or not >= 0.");
                    }

                    Set(name, logRetentionDays);
                    break;

                default:

                    throw new ArgumentException($"[name={name}] is not a user modifiable global cluster setting.");
            }
        }

        /// <summary>
        /// Sets or removes a named <c>string</c> cluster global setting.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="value">The setting value or <c>null</c> to remove the setting if it exists.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// <note>
        /// Well known cluster setting names are defined in <see cref="NeonClusterGlobals"/>.
        /// </note>
        /// </remarks>
        public void Set(string name, string value)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(ClusterDefinition.IsValidName(name));

            var key = $"{NeonClusterConst.ClusterGlobalsKey}/{name}";

            if (value == null)
            {
                cluster.Consul.KV.Delete(key).Wait();
            }
            else
            {
                cluster.Consul.KV.PutString(key, value).Wait();
            }
        }

        /// <summary>
        /// Sets or removes a named <c>bool</c> cluster global setting.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="value">The setting value or <c>null</c> to remove the setting if it exists.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// <note>
        /// Well known cluster setting names are defined in <see cref="NeonClusterGlobals"/>.
        /// </note>
        /// </remarks>
        public void Set(string name, bool? value)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(ClusterDefinition.IsValidName(name));

            var key = $"{NeonClusterConst.ClusterGlobalsKey}/{name}";

            if (value == null)
            {
                cluster.Consul.KV.Delete(key).Wait();
            }
            else
            {
                cluster.Consul.KV.PutString(key, value.Value ? "true" : "false").Wait();
            }
        }

        /// <summary>
        /// Sets or removes a named <c>int</c> cluster global setting.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="value">The setting value or <c>null</c> to remove the setting if it exists.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// <note>
        /// Well known cluster setting names are defined in <see cref="NeonClusterGlobals"/>.
        /// </note>
        /// </remarks>
        public void Set(string name, int? value)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(ClusterDefinition.IsValidName(name));

            var key = $"neon/cluster/{name}";

            if (value == null)
            {
                cluster.Consul.KV.Delete(key).Wait();
            }
            else
            {
                cluster.Consul.KV.PutString(key, value.Value.ToString()).Wait();
            }
        }

        /// <summary>
        /// Sets or removes a named <c>long</c> cluster global setting.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="value">The setting value or <c>null</c> to remove the setting if it exists.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// <note>
        /// Well known cluster setting names are defined in <see cref="NeonClusterGlobals"/>.
        /// </note>
        /// </remarks>
        public void Set(string name, long? value)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(ClusterDefinition.IsValidName(name));

            var key = $"neon/cluster/{name}";

            if (value == null)
            {
                cluster.Consul.KV.Delete(key).Wait();
            }
            else
            {
                cluster.Consul.KV.PutString(key, value.Value.ToString()).Wait();
            }
        }

        /// <summary>
        /// Sets or removes a named <c>double</c> cluster global setting.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="value">The setting value or <c>null</c> to remove the setting if it exists.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// <note>
        /// Well known cluster setting names are defined in <see cref="NeonClusterGlobals"/>.
        /// </note>
        /// </remarks>
        public void Set(string name, double? value)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(ClusterDefinition.IsValidName(name));

            var key = $"neon/cluster/{name}";

            if (value == null)
            {
                cluster.Consul.KV.Delete(key).Wait();
            }
            else
            {
                cluster.Consul.KV.PutString(key, value.Value.ToString()).Wait();
            }
        }

        /// <summary>
        /// Sets or removes a named <see cref="TimeSpan"/> cluster global setting.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="value">The setting value or <c>null</c> to remove the setting if it exists.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// <note>
        /// Well known cluster setting names are defined in <see cref="NeonClusterGlobals"/>.
        /// </note>
        /// </remarks>
        public void Set(string name, TimeSpan? value)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(ClusterDefinition.IsValidName(name));

            var key = $"neon/cluster/{name}";

            if (value == null)
            {
                cluster.Consul.KV.Delete(key).Wait();
            }
            else
            {
                cluster.Consul.KV.PutString(key, value.Value.ToString()).Wait();
            }
        }
    }
}
