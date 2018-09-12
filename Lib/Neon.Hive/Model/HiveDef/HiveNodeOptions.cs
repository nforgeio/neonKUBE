//-----------------------------------------------------------------------------
// FILE:	    HiveNodeOptions.cs
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
    /// Describes hive host node options.
    /// </summary>
    public class HiveNodeOptions
    {
        private const TargetOS      defaultOperatingSystem         = TargetOS.Ubuntu_16_04;
        private const AuthMethods   defaultSshAuth                 = AuthMethods.Tls;
        private const OsUpgrade     defaultUpgrade                 = OsUpgrade.Full;
        private const int           defaultPasswordLength          = 20;
        private const bool          defaultPasswordAuth            = true;
        private const bool          defaultEnableVolumeNetshare    = true;
        private const bool          defaultAllowPackageManagerIPv6 = false;
        private const int           defaultPackageManagerRetries   = 5;

        /// <summary>
        /// Specifies the target host operating system.  This currently defaults
        /// to <see cref="TargetOS.Ubuntu_16_04"/>.
        /// </summary>
        [JsonProperty(PropertyName = "OperatingSystem", Required = Required.Default)]
        [DefaultValue(defaultOperatingSystem)]
        public TargetOS OperatingSystem { get; set; } = defaultOperatingSystem;

        /// <summary>
        /// Specifies whether the host node operating system should be upgraded
        /// during hive preparation.  This defaults to <see cref="OsUpgrade.Full"/>
        /// to pick up most criticial updates.
        /// </summary>
        [JsonProperty(PropertyName = "Upgrade", Required = Required.Default)]
        [DefaultValue(defaultUpgrade)]
        public OsUpgrade Upgrade { get; set; } = defaultUpgrade;

        /// <summary>
        /// <para>
        /// Specifies the authentication method to be used to secure SSH sessions
        /// to the hive host nodes.  This defaults to <see cref="AuthMethods.Tls"/>  
        /// for better security.
        /// </para>
        /// <note>
        /// Some <b>neon-cli</b> features such as the <b>Ansible</b> commands require 
        /// <see cref="AuthMethods.Tls"/> (the default) to function.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "SshAuth", Required = Required.Default)]
        [DefaultValue(defaultSshAuth)]
        public AuthMethods SshAuth { get; set; } = defaultSshAuth;

        /// <summary>
        /// Hive hosts are configured with a random root account password.
        /// This defaults to <b>20</b> characters.  The minumum non-zero length
        /// is <b>8</b>.  Specify <b>0</b> to leave the root password unchanged.
        /// </summary>
        [JsonProperty(PropertyName = "PasswordLength", Required = Required.Default)]
        [DefaultValue(defaultPasswordLength)]
        public int PasswordLength { get; set; } = defaultPasswordLength;

        /// <summary>
        /// Allow the Linux package manager to use IPv6 when communicating with
        /// package mirrors.  This defaults to <c>false</c> to restrict access
        /// to IPv4.
        /// </summary>
        [JsonProperty(PropertyName = "AllowPackageManagerIPv6", Required = Required.Default)]
        [DefaultValue(defaultAllowPackageManagerIPv6)]
        public bool AllowPackageManagerIPv6 { get; set; } = defaultAllowPackageManagerIPv6;

        /// <summary>
        /// Specifies the number of times the host package manager should retry
        /// failed index or package downloads.  This defaults to <b>5</b>.
        /// </summary>
        [JsonProperty(PropertyName = "PackageManagerRetries", Required = Required.Default)]
        [DefaultValue(defaultPackageManagerRetries)]
        public int PackageManagerRetries { get; set; } = defaultPackageManagerRetries;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="hiveDefinition">The hive definition.</param>
        /// <exception cref="HiveDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(HiveDefinition hiveDefinition)
        {
            if (PasswordLength > 0 && PasswordLength < 8)
            {
                throw new HiveDefinitionException($"[{nameof(HiveNodeOptions)}.{nameof(PasswordLength)}={PasswordLength}] is not zero and is less than the minimum [8].");
            }
        }
    }
}
