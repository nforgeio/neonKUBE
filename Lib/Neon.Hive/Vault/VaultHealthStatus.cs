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

namespace Neon.Hive
{
    /// <summary>
    /// Describes the current health status of a Vault server instance.
    /// </summary>
    public class VaultHealthStatus
    {
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

        /// <summary>
        /// Returns <c>true</c> if the Vault instance is in data recovery mode.
        /// </summary>
        public bool IsRecovering { get; set; }
        
        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            var other = obj as VaultHealthStatus;

            if (other == null)
            {
                return false;
            }

            return this.IsInitialized == other.IsInitialized &&
                   this.IsSealed == other.IsSealed &&
                   this.IsStandby == other.IsStandby &&
                   this.IsRecovering == other.IsRecovering;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return IsInitialized.GetHashCode() ^ IsSealed.GetHashCode() ^ IsStandby.GetHashCode();
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

            return value;
        }
    }
}
