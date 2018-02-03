//-----------------------------------------------------------------------------
// FILE:	    HostNodeOptions.cs
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

namespace Neon.Cluster
{
    /// <summary>
    /// Describes cluster host node options.
    /// </summary>
    public class HostNodeOptions
    {
        private const AuthMethods   defaultSshAuth               = AuthMethods.Tls;
        private const OsUpgrade     defaultUpgrade               = OsUpgrade.Full;
        private const int           defaultPasswordLength        = 20;
        private const bool          defaultPasswordAuth          = true;
        private const bool          defaultEnableVolumeNetshare  = true;
        private const string        defaultVolumeNetshareVersion = "0.34";

        /// <summary>
        /// Specifies whether the host node operating system should be upgraded
        /// during cluster preparation.  This defaults to <see cref="OsUpgrade.Full"/>
        /// to pick up most criticial updates.
        /// </summary>
        [JsonProperty(PropertyName = "Upgrade", Required = Required.Default)]
        [DefaultValue(defaultUpgrade)]
        public OsUpgrade Upgrade { get; set; } = defaultUpgrade;

        /// <summary>
        /// <para>
        /// Specifies the authentication method to be used to secure SSH sessions
        /// to the cluster host nodes.  This defaults to <see cref="AuthMethods.Tls"/>  
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
        /// Cluster hosts are configured with a random root account password.
        /// This defaults to <b>20</b> characters.  The minumum non-zero length
        /// is <b>8</b>.  Specify <b>0</b> to leave the root password unchanged.
        /// </summary>
        [JsonProperty(PropertyName = "PasswordLength", Required = Required.Default)]
        [DefaultValue(defaultPasswordLength)]
        public int PasswordLength { get; set; } = defaultPasswordLength;

        /// <summary>
        /// Enables client NFS on the host and installs the Docker ContainX 
        /// <a href="https://github.com/ContainX/docker-volume-netshare">docker-volume-netshare</a> 
        /// volume plugin so that Docker containers can mount NFS, AWS EFS, and Samaba/CIFS based volumes.
        /// This defaults to <c>true</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Enable this to install the <b>NFS Common</b> package on all Docker hosts notes
        /// including the managers, wokers, and pets.  This also installs the ContainX 
        /// <a href="https://github.com/ContainX/docker-volume-netshare">docker-volume-netshare</a>
        /// Docker plugin and configures it to run as a service in NFS mode.  This means that
        /// you'll be able to immediately launch a Docker container or service that mounts
        /// the NFS share like:
        /// </para>
        /// <code lang="none">
        /// docker run -i -t --volume-driver=nfs -v nfshost/path:/mount ubuntu /bin/bash
        /// </code>
        /// <para>
        /// The <b>ContainX</b> plugin is also capable of mounting remote <b>AWS EFS</b> 
        /// and <b>Samba/CIFS</b> file systems.  This is not enabled by default; you'll 
        /// need to customize how you start the ContainX plugin, as described
        /// <a href="https://github.com/ContainX/docker-volume-netshare">here</a>.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "EnableVolumeNetShare", Required = Required.Default)]
        [DefaultValue(defaultEnableVolumeNetshare)]
        public bool EnableVolumeNetShare { get; set; } = defaultEnableVolumeNetshare;

        /// <summary>
        /// Specifies the ContainX <b>docker-volume-netshare</b> package version to install
        /// when <see cref="EnableVolumeNetShare"/> is <c>true</c>. This defaults to a reasonable 
        /// recent version.
        /// </summary>
        [JsonProperty(PropertyName = "VolumeNetShareVersion", Required = Required.Default)]
        [DefaultValue(defaultVolumeNetshareVersion)]
        public string VolumeNetShareVersion = defaultVolumeNetshareVersion;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(ClusterDefinition clusterDefinition)
        {
            if (PasswordLength > 0 && PasswordLength < 8)
            {
                throw new ClusterDefinitionException($"[{nameof(HostNodeOptions)}.{nameof(PasswordLength)}={PasswordLength}] is not zero and is less than the minimum [8].");
            }

            VolumeNetShareVersion = VolumeNetShareVersion ?? defaultVolumeNetshareVersion;
        }
    }
}
