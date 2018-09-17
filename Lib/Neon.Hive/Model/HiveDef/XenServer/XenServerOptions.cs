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
        private const bool   defaultSnapshot          = false;

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
        /// Production hive definitions should be configured with an XVA with a specific version
        /// of the host operating system to ensure that hive nodes are provisioned with the same
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
        /// node template as well as the hive virtual machine images.  This defaults to
        /// <b>Local storage</b>.
        /// </summary>
        [JsonProperty(PropertyName = "StorageRepository", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultStorageRepository)]
        public string StorageRepository { get; set; } = defaultStorageRepository;

        /// <summary>
        /// Identifies the XenServer storage repository to be used to for any Ceph OSD
        /// drives created for the cluster.  This defaults to <b>Local storage</b>.
        /// </summary>
        [JsonProperty(PropertyName = "OsdStorageRepository", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultStorageRepository)]
        public string OsdStorageRepository { get; set; } = defaultStorageRepository;

        /// <summary>
        /// Optionally directs XenCenter to create the virtual machines using a snapshot of
        /// the virtual machine template rather than creating a full copy.  This defaults
        /// to <c>false</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Virtual machines created via a snapshot will be ready within seconds where as
        /// creation can take something like 4 minutes on a SSD or 9 minutes on a spinning
        /// drive.  We don't enable snapshots though by default, because some web posts
        /// from around 2014 indicate that operators may encounter problems when something
        /// like 30 virtual machines have been created as snapshots from the same template.
        /// </para>
        /// <para>
        /// We figure that it's best to default to safe setting for production hives and
        /// then allow operators to override this when provisioning temporary test hives 
        /// or when provisioning on a storage repository that doesn't have these limitations.
        /// </para>
        /// <note>
        /// For snapshots to work, the storage repository must support them and the virtual
        /// machine template must reside in the same repository where the virtual machines
        /// are being created.  The current <c>neon-cli</c> implementation persists the
        /// hive VM templates to the local storage repository, so support for non-local
        /// storage repositories is not support out-of-the-box at this time.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "Snapshot", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultSnapshot)]
        public bool Snapshot { get; set; } = defaultSnapshot;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="hiveDefinition">The hive definition.</param>
        /// <exception cref="HiveDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(HiveDefinition hiveDefinition)
        {
            Covenant.Requires<ArgumentNullException>(hiveDefinition != null);

            HostXvaUri           = HostXvaUri ?? defaultHostXvaUri;
            TemplateName         = TemplateName ?? defaultTemplate;
            StorageRepository    = StorageRepository ?? defaultStorageRepository;
            OsdStorageRepository = OsdStorageRepository ?? defaultStorageRepository;

            if (!hiveDefinition.Network.StaticIP)
            {
                throw new HiveDefinitionException($"[{nameof(NetworkOptions)}.{nameof(NetworkOptions.StaticIP)}] must be [true] when deploying to XenServer.");
            }

            if (string.IsNullOrEmpty(HostXvaUri) || !Uri.TryCreate(HostXvaUri, UriKind.Absolute, out Uri uri))
            {
                throw new HiveDefinitionException($"[{nameof(XenServerOptions)}.{nameof(HostXvaUri)}] is required when deploying to XenServer.");
            }

            if (string.IsNullOrEmpty(StorageRepository))
            {
                throw new HiveDefinitionException($"[{nameof(XenServerOptions)}.{nameof(StorageRepository)}] is required when deploying to XenServer.");
            }

            if (string.IsNullOrEmpty(OsdStorageRepository))
            {
                OsdStorageRepository = StorageRepository;
            }

            hiveDefinition.ValidatePrivateNodeAddresses();                                          // Private node IP addresses must be assigned and valid.
            hiveDefinition.Hosting.ValidateHypervisor(hiveDefinition, remoteHypervisors: true);     // Hypervisor options must be valid.
        }
    }
}
