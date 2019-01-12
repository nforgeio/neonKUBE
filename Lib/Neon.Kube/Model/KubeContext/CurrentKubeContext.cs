//-----------------------------------------------------------------------------
// FILE:	    CurrentKubeContext.cs
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

namespace Neon.Kube
{
    /// <summary>
    /// Holds information about currently logged in cluster.  This is persisted
    /// as JSON to the <b>.current</b> file in the folder where the operator's 
    /// cluster login files are stored.
    /// </summary>
    public class CurrentKubeContext
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Reads the current cluster login information from the file system.
        /// </summary>
        /// <returns>The current login information or <c>null</c> if the operator is not logged in.</returns>
        public static CurrentKubeContext Load()
        {
            if (!File.Exists(KubeHelper.CurrentPath))
            {
                return null;    // Not logged in.
            }

            try
            {
                return NeonHelper.JsonDeserialize<CurrentKubeContext>(File.ReadAllText(KubeHelper.CurrentPath));
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
            if (File.Exists(KubeHelper.CurrentPath))
            {
                File.Delete(KubeHelper.CurrentPath);
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Default constructor.
        /// </summary>
        public CurrentKubeContext()
        {
        }

        /// <summary>
        /// The login name formatted as <b>username</b>@<b>hive-name</b>.
        /// </summary>
        [JsonProperty(PropertyName = "Login", Required = Required.Always)]
        public string Login { get; set; }

        /// <summary>
        /// Persists the instance as the currently logged in cluster.
        /// </summary>
        public void Save()
        {
            File.WriteAllText(KubeHelper.CurrentPath, NeonHelper.JsonSerialize(this, Formatting.Indented));
        }
    }
}
