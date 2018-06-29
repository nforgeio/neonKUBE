//-----------------------------------------------------------------------------
// FILE:	    XenServerOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Neon.Hive
{
    /// <summary>
    /// Specifies hosting settings for the Citrix XenServer hypervisor.
    /// </summary>
    public class XenServerOptions
    {
        private const string defaultHostXvaUri        = "http://s3-us-west-2.amazonaws.com/neonforge/neoncluster/neon-ubuntu-16.04.latest.xva";
        private const string defaultTemplate          = "neon-ubuntu-16.04-template";
        private const string defaultStorageRepository = "Local storage";

        /// <summary>
        /// Default constructor.
        /// </summary>
        public XenServerOptions()
        {
        }

        /// <summary>
        /// <para>
        /// URI to the XenServer XVA image to use as a template for creating the virtual machines.  This defaults to
        /// <b>http://s3-us-west-2.amazonaws.com/neonforge/neoncluster/neon-ubuntu-16.04.latest.xva</b>
        /// which is the latest supported Ubuntu 16.04 image.
        /// </para>
        /// <note>
        /// Production cluster definitions should be configured with an XVA with a specific version
        /// of the host operating system to ensure that cluster nodes are provisioned with the same
        /// operating system version.
        /// </note>
        /// <note>
        /// The XenServer <b>xe</b> CLI <b>does not support</b> downloading XVA images <b>via HTTPS</b>.
        /// You'll need to use HTTP or FTP.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "HostXvaUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultHostXvaUri)]
        public string HostXvaUri { get; set; } = defaultHostXvaUri;

        /// <summary>
        /// Names the XenServer template to be used when creating neonHIVE nodes.  This defaults
        /// to <b>ubuntu-template</b>.
        /// </summary>
        [JsonProperty(PropertyName = "TemplateName", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultTemplate)]
        public string TemplateName { get; set; } = defaultTemplate;

        /// <summary>
        /// Identifies the XenServer storage repository to be used to store the XenServer
        /// node template as well as the cluster virtual machine images.  This may be the
        /// name of the target repository or its UUID.  This defaults to <b>Local storage</b>.
        /// </summary>
        [JsonProperty(PropertyName = "StorageRepository", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultStorageRepository)]
        public string StorageRepository { get; set; } = defaultStorageRepository;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null);

            HostXvaUri        = HostXvaUri ?? defaultHostXvaUri;
            TemplateName      = TemplateName ?? defaultTemplate;
            StorageRepository = StorageRepository ?? defaultStorageRepository;

            if (!clusterDefinition.Network.StaticIP)
            {
                throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}.{nameof(NetworkOptions.StaticIP)}] must be [true] when deploying to XenServer.");
            }

            if (string.IsNullOrEmpty(HostXvaUri) || !Uri.TryCreate(HostXvaUri, UriKind.Absolute, out Uri uri))
            {
                throw new ClusterDefinitionException($"[{nameof(XenServerOptions)}.{nameof(HostXvaUri)}] is required when deploying to XenServer.");
            }

            if (string.IsNullOrEmpty(StorageRepository))
            {
                throw new ClusterDefinitionException($"[{nameof(XenServerOptions)}.{nameof(StorageRepository)}] is required when deploying to XenServer.");
            }

            clusterDefinition.ValidatePrivateNodeAddresses();                                           // Private node IP addresses must be assigned and valid.
            clusterDefinition.Hosting.ValidateHypervisor(clusterDefinition, remoteHypervisors: true);   // Hypervisor options must be valid.
        }
    }
}
