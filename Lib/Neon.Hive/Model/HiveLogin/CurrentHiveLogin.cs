//-----------------------------------------------------------------------------
// FILE:	    CurrentHiveLogin.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.Cryptography;

namespace Neon.Hive
{
    /// <summary>
    /// Holds information about currently logged in hive.  This is persisted
    /// as JSON to the <b>.current</b> file in the folder where the operator's 
    /// hive login files are stored.
    /// </summary>
    public class CurrentHiveLogin
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Reads the current hive login information from the file system.
        /// </summary>
        /// <returns>The current login information or <c>null</c> if the operator is not logged in.</returns>
        public static CurrentHiveLogin Load()
        {
            if (!File.Exists(HiveHelper.CurrentPath))
            {
                return null;    // Not logged in.
            }

            try
            {
                return NeonHelper.JsonDeserialize<CurrentHiveLogin>(File.ReadAllText(HiveHelper.CurrentPath));
            }
            catch
            {
                // The file must be corrupted or possibly deleted since we checked
                // above.  Treat this as if we're not logged in.

                return null;
            }
        }

        /// <summary>
        /// Deletes the current login file, effectively logging out the operator.
        /// </summary>
        public static void Delete()
        {
            if (File.Exists(HiveHelper.CurrentPath))
            {
                File.Delete(HiveHelper.CurrentPath);
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Default constructor.
        /// </summary>
        public CurrentHiveLogin()
        {
        }

        /// <summary>
        /// The login name formatted as <b>username</b>@<b>hive-name</b>.
        /// </summary>
        [JsonProperty(PropertyName = "Login", Required = Required.Always)]
        public string Login { get; set; }

        /// <summary>
        /// Indicates that the communication with the hive should be made via the
        /// hive VPN vs. direct local communication.  This defaults to <c>false</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Operators will always use the VPN to connect to cloud environments such as
        /// Azure and AWS.
        /// </para>
        /// <para>
        /// On-premise hives are somewhat more complex.  VPN deployment is optional for
        /// on-premise deployments and for on-premise deployments with VPN, operators will need 
        /// to be able choose whether to connect via the VPN when they're outside the hive
        /// network or communicate directly when they are on-premise.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "UseVpn", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool ViaVpn { get; set; }

        /// <summary>
        /// Persists the instance as the currently logged in hive.
        /// </summary>
        public void Save()
        {
            File.WriteAllText(HiveHelper.CurrentPath, NeonHelper.JsonSerialize(this, Formatting.Indented));
        }
    }
}
