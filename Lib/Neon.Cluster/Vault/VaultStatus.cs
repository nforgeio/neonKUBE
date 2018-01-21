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
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.IO;

namespace Neon.Cluster
{
    /// <summary>
    /// Describes the current status of a Vault server instance.
    /// </summary>
    public class VaultStatus
    {
        /// <summary>
        /// The server version.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Returns <c>true</c> if Vault is initialized.
        /// </summary>
        public bool IsInitialized { get; set; }

        /// <summary>
        /// Returns <c>true</c> if Vault is sealed.
        /// </summary>
        public bool IsSealed { get; set; }

        /// <summary>
        /// Returns <c>true</c> if Vault is operating as a standby instance.
        /// </summary>
        public bool IsStandby { get; set; }
        
        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            var other = obj as VaultStatus;

            if (other == null)
            {
                return false;
            }

            return this.Version == other.Version &&
                   this.IsInitialized == other.IsInitialized &&
                   this.IsSealed == other.IsSealed &&
                   this.IsStandby == other.IsStandby;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var hash = 0;

            if (Version != null)
            {
                hash = Version.GetHashCode();
            }

            return hash ^ IsInitialized.GetHashCode() ^ IsSealed.GetHashCode() ^ IsStandby.GetHashCode();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var value = string.Empty;

            if (!IsInitialized)
            {
                if (value.Length > 0)
                {
                    value += " ";
                }

                value += "NOT-INITIALIZED";
            }

            if (IsSealed)
            {
                if (value.Length > 0)
                {
                    value += " ";
                }

                value += "SEALED";
            }
            else
            {
                if (value.Length > 0)
                {
                    value += " ";
                }

                value += "UNSEALED";
            }

            if (IsStandby)
            {
                if (value.Length > 0)
                {
                    value += " ";
                }

                value += "STANDBY";
            }

            if (value.Length > 0)
            {
                value += " ";
            }

            return value + $"[version={Version}]";
        }
    }
}
