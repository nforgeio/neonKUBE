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
using Newtonsoft.Json;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

namespace Neon.Hive
{
    /// <summary>
    /// Manages hive global settings.
    /// </summary>
    public sealed class GlobalsManager
    {
        private HiveProxy    hive;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="hive">The parent <see cref="HiveProxy"/>.</param>
        internal GlobalsManager(HiveProxy hive)
        {
            Covenant.Requires<ArgumentNullException>(hive != null);

            this.hive = hive;
        }

        /// <summary>
        /// Returns the Consul key for the named global.
        /// </summary>
        /// <param name="name"></param>
        /// <returns>The Consul key path.</returns>
        public string GetKey(string name)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(HiveDefinition.IsValidName(name));

            return $"{HiveConst.GlobalKey}/{name}";
        }

        /// <summary>
        /// Returns the current version of the neonHIVE deployment.
        /// </summary>
        public string Version
        {
            get
            {
                if (!TryGetString(HiveGlobals.Version, out var version))
                {
                    throw new KeyNotFoundException(HiveGlobals.Version);
                }

                return version;
            }
        }

        /// <summary>
        /// Attempts to retrieve a named hive global setting as a <c>string</c>.
        /// </summary>
        /// <typeparam name="T">The type of the object to be returned.</typeparam>
        /// <param name="name">The setting name.</param>
        /// <param name="output">Returns as the setting value.</param>
        /// <returns><c>true</c> if the setting exists, could be parsed, and was returned.</returns>
        /// <remarks>
        /// <note>
        /// Well known hive setting names are defined in <see cref="HiveGlobals"/>.
        /// </note>
        /// </remarks>
        public bool TryGetObject<T>(string name, out T output)
            where T : class, new()
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(HiveDefinition.IsValidName(name));

            output = null;

            var key  = $"{HiveConst.GlobalKey}/{name}";
            var json = hive.Consul.Client.KV.GetStringOrDefault(key).Result;

            if (json == null)
            {
                return false;
            }

            output = NeonHelper.JsonDeserialize<T>(json, strict: false); ;

            return true;
        }

        /// <summary>
        /// Attempts to retrieve a named hive global setting as a <c>string</c>.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="output">Returns as the setting value.</param>
        /// <returns><c>true</c> if the setting exists and was returned.</returns>
        /// <exception cref="FormatException">Thrown if the setting value could not be parsed.</exception>
        /// <remarks>
        /// <note>
        /// Well known hive setting names are defined in <see cref="HiveGlobals"/>.
        /// </note>
        /// </remarks>
        public bool TryGetString(string name, out string output)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(HiveDefinition.IsValidName(name));

            output = null;

            var key   = $"{HiveConst.GlobalKey}/{name}";
            var value = hive.Consul.Client.KV.GetStringOrDefault(key).Result;

            if (value == null)
            {
                return false;
            }

            output = value;

            return true;
        }

        /// <summary>
        /// Attempts to retrieve a named hive global setting as a <c>bool</c>.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="output">Returns as the setting value.</param>
        /// <returns><c>true</c> if the setting exists and was returned.</returns>
        /// <exception cref="FormatException">Thrown if the setting value could not be parsed.</exception>
        /// <remarks>
        /// <note>
        /// Well known hive setting names are defined in <see cref="HiveGlobals"/>.
        /// </note>
        /// </remarks>
        public bool TryGetBool(string name, out bool output)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(HiveDefinition.IsValidName(name));

            output = default(bool);

            var key   = $"{HiveConst.GlobalKey}/{name}";
            var value = hive.Consul.Client.KV.GetStringOrDefault(key).Result;

            if (value == null)
            {
                return false;
            }

            output = NeonHelper.ParseBool(value);

            return true;
        }

        /// <summary>
        /// Attempts to retrieve a named hive global setting as an <c>int</c>.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="output">Returns as the setting value.</param>
        /// <returns><c>true</c> if the setting exists and was returned.</returns>
        /// <exception cref="FormatException">Thrown if the setting value could not be parsed.</exception>
        /// <remarks>
        /// <note>
        /// Well known hive setting names are defined in <see cref="HiveGlobals"/>.
        /// </note>
        /// </remarks>
        public bool TryGetInt(string name, out int output)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(HiveDefinition.IsValidName(name));

            output = default(int);

            var key   = $"{HiveConst.GlobalKey}/{name}";
            var value = hive.Consul.Client.KV.GetStringOrDefault(key).Result;

            if (value == null)
            {
                return false;
            }

            output = int.Parse(value);

            return true;
        }

        /// <summary>
        /// Attempts to retrieve a named hive global setting as a <c>long</c>.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="output">Returns as the setting value.</param>
        /// <returns><c>true</c> if the setting exists and was returned.</returns>
        /// <exception cref="FormatException">Thrown if the setting value could not be parsed.</exception>
        /// <remarks>
        /// <note>
        /// Well known hive setting names are defined in <see cref="HiveGlobals"/>.
        /// </note>
        /// </remarks>
        public bool TryGetLong(string name, out long output)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(HiveDefinition.IsValidName(name));

            output = default(int);

            var key   = $"{HiveConst.GlobalKey}/{name}";
            var value = hive.Consul.Client.KV.GetStringOrDefault(key).Result;

            if (value == null)
            {
                return false;
            }

            output = long.Parse(value);

            return true;
        }

        /// <summary>
        /// Attempts to retrieve a named hive global setting as a <c>double</c>.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="output">Returns as the setting value.</param>
        /// <returns><c>true</c> if the setting exists and was returned.</returns>
        /// <exception cref="FormatException">Thrown if the setting value could not be parsed.</exception>
        /// <remarks>
        /// <note>
        /// Well known hive setting names are defined in <see cref="HiveGlobals"/>.
        /// </note>
        /// </remarks>
        public bool TryGetDouble(string name, out double output)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(HiveDefinition.IsValidName(name));

            output = default(double);

            var key   = $"{HiveConst.GlobalKey}/{name}";
            var value = hive.Consul.Client.KV.GetStringOrDefault(key).Result;

            if (value == null)
            {
                return false;
            }

            output = double.Parse(value);

            return true;
        }

        /// <summary>
        /// Attempts to retrieve a named hive global setting as a <see cref="TimeSpan"/>.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="output">Returns as the setting value.</param>
        /// <returns><c>true</c> if the setting exists and was returned.</returns>
        /// <exception cref="FormatException">Thrown if the setting value could not be parsed.</exception>
        /// <remarks>
        /// <note>
        /// Well known hive setting names are defined in <see cref="HiveGlobals"/>.
        /// </note>
        /// </remarks>
        public bool TryGetTimeSpan(string name, out TimeSpan output)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(HiveDefinition.IsValidName(name));

            output = default(TimeSpan);

            var key   = $"{HiveConst.GlobalKey}/{name}";
            var value = hive.Consul.Client.KV.GetStringOrDefault(key).Result;

            if (value == null)
            {
                return false;
            }

            output = TimeSpan.Parse(value);

            return true;
        }

        /// <summary>
        /// Attempts to retrieve a named hive global setting as a <typeparamref name="T"/>
        /// deserialized from JSON.
        /// </summary>
        /// <typeparam name="T">The type being deserialized.</typeparam>
        /// <param name="name">The setting name.</param>
        /// <param name="output">Returns as the setting value.</param>
        /// <param name="strict">Optionally require that all input properties map to <typeparamref name="T"/> properties.</param>
        /// <returns><c>true</c> if the setting exists and was returned.</returns>
        /// <exception cref="FormatException">Thrown if the setting value could not be parsed.</exception>
        /// <remarks>
        /// <note>
        /// Well known hive setting names are defined in <see cref="HiveGlobals"/>.
        /// </note>
        /// </remarks>
        public bool TryGet<T>(string name, out T output, bool strict = false)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(HiveDefinition.IsValidName(name));

            output = default(T);

            var key   = $"{HiveConst.GlobalKey}/{name}";
            var value = hive.Consul.Client.KV.GetStringOrDefault(key).Result;

            if (value == null)
            {
                return false;
            }

            try
            {
                output = NeonHelper.JsonDeserialize<T>(value, strict);
            }
            catch
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Sets or removes a hive global setting, verifying that the setting 
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
        ///     Indicates whether unit testing via <c>HiveFixture</c> is to be 
        ///     allowed.  Possible values are: <b>true</b>, <b>false</b>, <b>yes</b>,
        ///     <b>no</b>, <b>on</b>, <b>off</b>, <b>1</b>, or <b>0</b>.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>log-retention-days</b></term>
        ///     <description>
        ///     Specifies the number of days the hive should retain <b>logstash</b>
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
                case HiveGlobals.UserAllowUnitTesting:

                    Set(name, NeonHelper.ParseBool(value));
                    break;

                case HiveGlobals.UserDisableAutoUnseal:

                    Set(name, NeonHelper.ParseBool(value));
                    break;

                case HiveGlobals.UserLogRetentionDays:

                    if (!int.TryParse(value, out var logRetentionDays) || logRetentionDays <= 0)
                    {
                        throw new FormatException($"[log-retention-days={value}] is invalid because it's not an integer or not >= 0.");
                    }

                    Set(name, logRetentionDays);
                    break;

                default:

                    throw new ArgumentException($"[name={name}] is not a user modifiable global hive setting.");
            }
        }

        /// <summary>
        /// Sets or removes a named <c>string</c> hive global setting.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="value">The setting value or <c>null</c> to remove the setting if it exists.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// <note>
        /// Well known hive setting names are defined in <see cref="HiveGlobals"/>.
        /// </note>
        /// </remarks>
        public void Set(string name, string value)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(HiveDefinition.IsValidName(name));

            var key = $"{HiveConst.GlobalKey}/{name}";

            if (value == null)
            {
                hive.Consul.Client.KV.Delete(key).Wait();
            }
            else
            {
                hive.Consul.Client.KV.PutString(key, value).Wait();
            }
        }

        /// <summary>
        /// Sets or removes a named <c>bool</c> hive global setting.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="value">The setting value or <c>null</c> to remove the setting if it exists.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// <note>
        /// Well known hive setting names are defined in <see cref="HiveGlobals"/>.
        /// </note>
        /// </remarks>
        public void Set(string name, bool? value)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(HiveDefinition.IsValidName(name));

            var key = $"{HiveConst.GlobalKey}/{name}";

            if (value == null)
            {
                hive.Consul.Client.KV.Delete(key).Wait();
            }
            else
            {
                hive.Consul.Client.KV.PutString(key, value.Value ? "true" : "false").Wait();
            }
        }

        /// <summary>
        /// Sets or removes a named <c>int</c> hive global setting.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="value">The setting value or <c>null</c> to remove the setting if it exists.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// <note>
        /// Well known hive setting names are defined in <see cref="HiveGlobals"/>.
        /// </note>
        /// </remarks>
        public void Set(string name, int? value)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(HiveDefinition.IsValidName(name));

            var key = $"{HiveConst.GlobalKey}/{name}";

            if (value == null)
            {
                hive.Consul.Client.KV.Delete(key).Wait();
            }
            else
            {
                hive.Consul.Client.KV.PutString(key, value.Value.ToString()).Wait();
            }
        }

        /// <summary>
        /// Sets or removes a named <c>long</c> hive global setting.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="value">The setting value or <c>null</c> to remove the setting if it exists.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// <note>
        /// Well known hive setting names are defined in <see cref="HiveGlobals"/>.
        /// </note>
        /// </remarks>
        public void Set(string name, long? value)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(HiveDefinition.IsValidName(name));

            var key = $"{HiveConst.GlobalKey}/{name}";

            if (value == null)
            {
                hive.Consul.Client.KV.Delete(key).Wait();
            }
            else
            {
                hive.Consul.Client.KV.PutString(key, value.Value.ToString()).Wait();
            }
        }

        /// <summary>
        /// Sets or removes a named <c>double</c> hive global setting.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="value">The setting value or <c>null</c> to remove the setting if it exists.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// <note>
        /// Well known hive setting names are defined in <see cref="HiveGlobals"/>.
        /// </note>
        /// </remarks>
        public void Set(string name, double? value)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(HiveDefinition.IsValidName(name));

            var key = $"{HiveConst.GlobalKey}/{name}";

            if (value == null)
            {
                hive.Consul.Client.KV.Delete(key).Wait();
            }
            else
            {
                hive.Consul.Client.KV.PutString(key, value.Value.ToString()).Wait();
            }
        }

        /// <summary>
        /// Sets or removes a named <see cref="TimeSpan"/> hive global setting.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="value">The setting value or <c>null</c> to remove the setting if it exists.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// <note>
        /// Well known hive setting names are defined in <see cref="HiveGlobals"/>.
        /// </note>
        /// </remarks>
        public void Set(string name, TimeSpan? value)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(HiveDefinition.IsValidName(name));

            var key = $"{HiveConst.GlobalKey}/{name}";

            if (value == null)
            {
                hive.Consul.Client.KV.Delete(key).Wait();
            }
            else
            {
                hive.Consul.Client.KV.PutString(key, value.Value.ToString()).Wait();
            }
        }

        /// <summary>
        /// Sets or removes a named hive global setting, serialized saved objects as JSON.
        /// </summary>
        /// <param name="name">The setting name.</param>
        /// <param name="value">The setting value or <c>null</c> to remove the setting if it exists.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// <note>
        /// Well known hive setting names are defined in <see cref="HiveGlobals"/>.
        /// </note>
        /// </remarks>
        public void Set(string name, object value)
        {
            Covenant.Requires(!string.IsNullOrEmpty(name));
            Covenant.Requires(HiveDefinition.IsValidName(name));

            var key = $"{HiveConst.GlobalKey}/{name}";

            if (value == null)
            {
                hive.Consul.Client.KV.Delete(key).Wait();
            }
            else
            {
                hive.Consul.Client.KV.PutString(key, NeonHelper.JsonSerialize(value, Formatting.Indented)).Wait();
            }
        }
    }
}
