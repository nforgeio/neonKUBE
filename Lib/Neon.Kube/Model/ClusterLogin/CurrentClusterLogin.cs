//-----------------------------------------------------------------------------
// FILE:	    CurrentClusterLogin.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

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

namespace Neon.Kube
{
    /// <summary>
    /// Holds information about currently logged in cluster.  This is persisted
    /// as JSON to the <b>.current</b> file in the folder where the operator's 
    /// cluster login files are stored.
    /// </summary>
    public class CurrentClusterLogin
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Reads the current cluster login information from the file system.
        /// </summary>
        /// <returns>The current login information or <c>null</c> if the operator is not logged in.</returns>
        public static CurrentClusterLogin Load()
        {
            if (!File.Exists(ClusterHelper.CurrentPath))
            {
                return null;    // Not logged in.
            }

            try
            {
                return NeonHelper.JsonDeserialize<CurrentClusterLogin>(File.ReadAllText(ClusterHelper.CurrentPath));
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
            if (File.Exists(ClusterHelper.CurrentPath))
            {
                File.Delete(ClusterHelper.CurrentPath);
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Default constructor.
        /// </summary>
        public CurrentClusterLogin()
        {
        }

        /// <summary>
        /// The login name formatted as <b>username</b>@<b>cluster-name</b>.
        /// </summary>
        [JsonProperty(PropertyName = "Login", Required = Required.Always)]
        public string Login { get; set; }

        /// <summary>
        /// Indicates that the communication with the cluster should be made via the
        /// cluster VPN vs. direct local communication.  This defaults to <c>false</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Operators will always use the VPN to connect to cloud environments such as
        /// Azure and AWS.
        /// </para>
        /// <para>
        /// On-premise hives are somewhat more complex.  VPN deployment is optional for
        /// on-premise deployments and for on-premise deployments with VPN, operators will need 
        /// to be able choose whether to connect via the VPN when they're outside the cluster
        /// network or communicate directly when they are on-premise.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "UseVpn", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool ViaVpn { get; set; }

        /// <summary>
        /// Persists the instance as the currently logged in cluster.
        /// </summary>
        public void Save()
        {
            File.WriteAllText(ClusterHelper.CurrentPath, NeonHelper.JsonSerialize(this, Formatting.Indented));
        }
    }
}
