//-----------------------------------------------------------------------------
// FILE:	    VaultPolicy.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
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
    /// Describes a Vault access control policy.
    /// </summary>
    public class VaultPolicy
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Implicitly converts a <see cref="VaultPolicy"/> into a string.
        /// </summary>
        /// <param name="policy">The source policy.</param>
        /// <returns>The converted string.</returns>
        public static implicit operator string(VaultPolicy policy)
        {
            Covenant.Requires<ArgumentNullException>(policy != null);

            return policy.ToString();
        }

        /// <summary>
        /// Appends a comma separated, quoted Vault capability if the corresponding bit is set.
        /// </summary>
        /// <param name="sb">The target string builder.</param>
        /// <param name="capabilities">The capability bits.</param>
        /// <param name="test">The capability we're testing.</param>
        /// <param name="name">The capability name.</param>
        private static void Append(StringBuilder sb, VaultCapabilies capabilities, VaultCapabilies test, string name)
        {
            if ((capabilities & test) == 0)
            {
                return; // Not set
            }

            sb.AppendWithSeparator($"\"{name}\"", ",");
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The policy name.</param>
        /// <param name="path">The policy path.</param>
        /// <param name="capabilities">The policy capabilities.</param>
        public VaultPolicy(string name, string path, VaultCapabilies capabilities)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path));

            this.Name         = name;
            this.Path         = path;
            this.Capabilities = capabilities;
        }

        /// <summary>
        /// Returns the policy name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Returns the policy path.
        /// </summary>
        public string Path { get; private set; }

        /// <summary>
        /// Returns the policy capabilities.
        /// </summary>
        public VaultCapabilies Capabilities { get; private set; }

        /// <summary>
        /// Renders the policy as Vault compatible HCL.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            var sbCapabilities = new StringBuilder();

            Append(sbCapabilities, Capabilities, VaultCapabilies.Create, "create");
            Append(sbCapabilities, Capabilities, VaultCapabilies.Read, "read");
            Append(sbCapabilities, Capabilities, VaultCapabilies.Update, "update");
            Append(sbCapabilities, Capabilities, VaultCapabilies.Delete, "delete");
            Append(sbCapabilities, Capabilities, VaultCapabilies.List, "list");
            Append(sbCapabilities, Capabilities, VaultCapabilies.Sudo, "sudo");
            Append(sbCapabilities, Capabilities, VaultCapabilies.Deny, "deny");

            return $"path \"{Path}\" {{ capabilities = [{sbCapabilities}] }}";
        }
    }
}
