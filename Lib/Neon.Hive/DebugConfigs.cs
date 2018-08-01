//-----------------------------------------------------------------------------
// FILE:	    DebugConfigs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Consul;
using Newtonsoft.Json;

using Neon.Common;

namespace Neon.Hive
{
    /// <summary>
    /// Used to emulate Docker service configs when debugging an application using 
    /// <see cref="HiveHelper.OpenHiveRemote(DebugSecrets, DebugConfigs, string, bool)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Add simple text configs to the collection using <see cref="Add(string, string)"/>.
    /// </para>
    /// </remarks>
    public class DebugConfigs : Dictionary<string, string>
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public DebugConfigs()
            : base(StringComparer.InvariantCultureIgnoreCase)
        {
        }

        /// <summary>
        /// Adds a named text config.
        /// </summary>
        /// <param name="name">The config name.</param>
        /// <param name="value">The config text.</param>
        /// <returns>The current instance to support fluent-style coding.</returns>
        public new DebugConfigs Add(string name, string value)
        {
            value = value ?? string.Empty;

            base.Add(name, value);

            return this;
        }

        /// <summary>
        /// Adds a config object as JSON.
        /// </summary>
        /// <param name="name">The secret name.</param>
        /// <param name="value">The secret value.</param>
        /// <returns>The current instance to support fluent-style coding.</returns>
        public DebugConfigs Add(string name, object value)
        {
            value = value ?? string.Empty;

            base.Add(name, NeonHelper.JsonSerialize(value, Formatting.Indented));

            return this;
        }

        /// <summary>
        /// Called internally by <see cref="HiveHelper.OpenHiveRemote(DebugSecrets, DebugConfigs, string, bool)"/> 
        /// to create any requested configs and add them to the dictionary.
        /// </summary>
        /// <param name="hive">The attached hive.</param>
        /// <param name="hiveLogin">The hive login.</param>
        internal void Realize(HiveProxy hive, HiveLogin hiveLogin)
        {
            // This is a NOP because we already added all of the configs
            // to the base dictionary in the [Add()] methods.
        }
    }
}
