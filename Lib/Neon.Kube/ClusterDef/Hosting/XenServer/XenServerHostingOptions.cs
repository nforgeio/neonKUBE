//-----------------------------------------------------------------------------
// FILE:        XenServerHostingOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// Specifies hosting settings for the Citrix XenServer hypervisor.
    /// </summary>
    public class XenServerHostingOptions
    {
        private const string defaultStorageRepository = "Local storage";
        private const bool   defaultSnapshot          = false;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public XenServerHostingOptions()
        {
        }

        /// <summary>
        /// Identifies the XenServer storage repository to be used to store the XenServer
        /// node template as well as the cluster virtual machine images.  This defaults to
        /// <b>Local storage</b>.
        /// </summary>
        [JsonProperty(PropertyName = "StorageRepository", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "storageRepository", ApplyNamingConventions = false)]
        [DefaultValue(defaultStorageRepository)]
        public string StorageRepository { get; set; } = defaultStorageRepository;

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
        /// We figure that it's best to default to safe setting for production clusters and
        /// then allow operators to override this when provisioning temporary test clusters 
        /// or when provisioning on a storage repository that doesn't have these limitations.
        /// </para>
        /// <note>
        /// For snapshots to work, the storage repository must support them and the virtual
        /// machine template must reside in the same repository where the virtual machines
        /// are being created.  The current <c>neon-cli</c> implementation persists the
        /// cluster VM templates to the local storage repository, so support for non-local
        /// storage repositories is not support out-of-the-box at this time.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "Snapshot", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "snapshot", ApplyNamingConventions = false)]
        [DefaultValue(defaultSnapshot)]
        public bool Snapshot { get; set; } = defaultSnapshot;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            var xenServerHostingOptionsPrefix = $"{nameof(ClusterDefinition.Hosting)}.{nameof(ClusterDefinition.Hosting.XenServer)}";

            StorageRepository = StorageRepository ?? defaultStorageRepository;

            if (string.IsNullOrEmpty(StorageRepository))
            {
                throw new ClusterDefinitionException($"[{xenServerHostingOptionsPrefix}.{nameof(StorageRepository)}] is required when deploying to XenServer.");
            }

            clusterDefinition.ValidatePrivateNodeAddresses();   // Private node IP addresses must be assigned and valid.
        }

        /// <summary>
        /// Clears all hosting related secrets.
        /// </summary>
        public void ClearSecrets()
        {
        }
    }
}
