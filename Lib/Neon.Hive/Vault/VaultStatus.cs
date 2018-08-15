//-----------------------------------------------------------------------------
// FILE:	    VaultStatus.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;

using Neon.Common;
using Neon.IO;

namespace Neon.Hive
{
    /// <summary>
    /// Describes the status of a HashiCorp Vault instance parsed
    /// from a <b>vault status</b> command response.
    /// </summary>
    public class VaultStatus
    {
        /// <summary>
        /// Constructs an instance by parsing a <b>vault status</b> command response.
        /// </summary>
        public VaultStatus(string response)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(response));

            // $hack(jeff.lill):
            //
            // This is somewhat fragile because it depends on the output format
            // of the [vault status] command which has changed in the past.

            // Unforunately, we need to parse the command's table formatted
            // output because the JSON/YAML formatted output doesn't include
            // all of the HA related fields for the current Vault build.
            //
            // The command output will look like this when Vault is sealed:
            //
            //      Key                Value
            //      ---                -----
            //      Seal Type          shamir
            //      Sealed             true
            //      Total Shares       1
            //      Threshold          1
            //      Unseal Progress    0/1
            //      Unseal Nonce       n/a
            //      Version            0.9.6
            //      HA Enabled         true
            //
            // and like this when unsealed:
            //
            //      Key             Value
            //      ---             -----
            //      Seal Type       shamir
            //      Sealed          false
            //      Total Shares    1
            //      Threshold       1
            //      Version         0.9.6
            //      Cluster Name    woodinville.home-small
            //      Cluster ID      4d8cc51d-00a3-0d7b-b540-9429d05d77d0
            //      HA Enabled      true
            //      HA Cluster      https://manager-0.neon-vault.HIVENAME.nhive.io:8201
            //      HA Mode         active
            //
            // Note that the column widths vary between the two versions.
            // We're going to determine the starting positions of the two   
            // column headers on the first line to peform the extraction.

            var valuePos = response.IndexOf("Value");

            if (valuePos == -1)
            {
                throw new FormatException("Unexpected HashiCorp Vault status.");
            }

            using (var reader = new StringReader(response))
            {
                foreach (var line in reader.Lines().Skip(2))
                {
                    var key   = line.Substring(0, valuePos).Trim();
                    var value = line.Substring(valuePos).Trim();

                    switch (key)
                    {
                        case "Seal Type":           SealType = value; break;
                        case "Sealed":              Sealed = value == "true"; break;
                        case "Total Shares":        TotalShares = int.Parse(value); break;
                        case "Threshold":           Threshold = int.Parse(value); break;
                        case "Version":             Version = value; break;
                        case "Cluster Name":        ClusterName = value; break;
                        case "Cluster ID":          ClusterId = value; break;
                        case "HA Enabled":          HAEnabled = value == "true"; break;
                        case "HA Cluster":          HACluster = value; break;
                        case "HA Mode":             HAMode = value; break;
                        case "Active Node Address": ActiveNode = value; break;
                    }
                }
            }
        }

        /// <summary>
        /// Identifies the seal method.
        /// </summary>
        public string SealType { get; private set; }

        /// <summary>
        /// Indicates whether the Vault is currently sealed.
        /// </summary>
        public bool Sealed { get; private set; }

        /// <summary>
        /// Returns the number of shared unseal keys generated for the Vault.
        /// </summary>
        public int TotalShares { get; private set; }

        /// <summary>
        /// Rrturns the minimum shared keys required to unseal the Vault.
        /// </summary>
        public int Threshold { get; private set; }

        /// <summary>
        /// Returns the Vault server version.
        /// </summary>
        public string Version { get; private set; }

        /// <summary>
        /// Returns the Vault hive name.
        /// </summary>
        public string ClusterName { get; private set; }

        /// <summary>
        /// Returns the Vault unique cluster ID.
        /// </summary>
        public string ClusterId { get; private set; }

        /// <summary>
        /// Indicates whether high-availability mode is enabled.
        /// </summary>
        public bool HAEnabled { get; private set; }

        /// <summary>
        /// Returns the internal Vault API URL of the Vault node currently acting as the hive leader.
        /// </summary>
        public string HACluster { get; private set; }

        /// <summary>
        /// Returns the Vault node status.
        /// </summary>
        public string HAMode { get; private set; }

        /// <summary>
        /// Returns the Vault API URL of the Vault node currently acting as the hive leader.
        /// </summary>
        public string ActiveNode { get; private set; }
    }
}
