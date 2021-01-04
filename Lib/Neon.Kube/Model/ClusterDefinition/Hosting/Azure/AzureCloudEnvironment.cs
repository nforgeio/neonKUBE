//-----------------------------------------------------------------------------
// FILE:	    AzureCloudEnvironment.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Net;

namespace Neon.Kube
{
    /// <summary>
    /// Specifies the target Azure environment.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Microsoft Azure deploys more than one environment for hosting services.
    /// <b>global-cloud</b> identifies their public cloud which is where most users
    /// and companies will deploy services.  Azure also has a few private environments
    /// that are typically used by specialized customers (like governments).
    /// </para>
    /// <para>
    /// The easiest way to use this is by setting the <see cref="Name"/> property to
    /// one of the possible environments:
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><b>global-cloud</b></term>
    ///     <description>
    ///     Public Azure cloud (the default).
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>china-cloud</b></term>
    ///     <description>
    ///     Private Chinese cloud.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>german-cloud</b></term>
    ///     <description>
    ///     Private German cloud.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>us-government</b></term>
    ///     <description>
    ///     Private United States government cloud.
    ///     </description>
    /// </item>
    /// </list>
    /// <para>
    /// Alternatively, you can set <see cref="Name"/> to <b>custom</b> and then set
    /// the specific environment endpoint URLs:
    /// </para>
    /// <para><see cref="AuthenticationEndpoint"/></para>
    /// <para><see cref="ResourceManagerEndpoint"/></para>
    /// <para><see cref="GraphEndpoint"/></para>
    /// <para><see cref="ManagementEnpoint"/></para>
    /// </remarks>
    public class AzureCloudEnvironment
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public AzureCloudEnvironment()
        {
        }

        /// <summary>
        /// Identifies the Azure environment.  This defaults to <see cref="AzureCloudEnvironments.GlobalCloud"/>.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "name", ApplyNamingConventions = false)]
        [DefaultValue(AzureCloudEnvironments.GlobalCloud)]
        public AzureCloudEnvironments Name { get; set; } = AzureCloudEnvironments.GlobalCloud;

        /// <summary>
        /// Environment authentication endpoint.
        /// </summary>
        [JsonProperty(PropertyName = "AuthenticationEndpoint", Required = Required.AllowNull)]
        [YamlMember(Alias = "authenticationEndpoint", ApplyNamingConventions = false)]
        public string AuthenticationEndpoint { get; set; }

        /// <summary>
        /// Environment resource manager endpoint.
        /// </summary>
        [JsonProperty(PropertyName = "ResourceManagerEndpoint", Required = Required.AllowNull)]
        [YamlMember(Alias = "resourceManagerEndpoint", ApplyNamingConventions = false)]
        public string ResourceManagerEndpoint { get; set; }

        /// <summary>
        /// Environment graph endpoint.
        /// </summary>
        [JsonProperty(PropertyName = "GraphEndpoint", Required = Required.AllowNull)]
        [YamlMember(Alias = "graphEndpoint", ApplyNamingConventions = false)]
        public string GraphEndpoint { get; set; }

        /// <summary>
        /// Environment management endpoint.
        /// </summary>
        [JsonProperty(PropertyName = "ManagementEnpoint", Required = Required.AllowNull)]
        [YamlMember(Alias = "managementEnpoint", ApplyNamingConventions = false)]
        public string ManagementEnpoint { get; set; }

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(ClusterDefinition clusterDefinition)
        {
            if (Name == AzureCloudEnvironments.Custom)
            {
                Uri uri;

                if (string.IsNullOrEmpty(AuthenticationEndpoint) || !Uri.TryCreate(AuthenticationEndpoint, UriKind.Absolute, out uri))
                {
                    throw new ClusterDefinitionException($"Invalid Azure environment [{nameof(AuthenticationEndpoint)}={AuthenticationEndpoint}].");
                }

                if (string.IsNullOrEmpty(ResourceManagerEndpoint) || !Uri.TryCreate(ResourceManagerEndpoint, UriKind.Absolute, out uri))
                {
                    throw new ClusterDefinitionException($"Invalid Azure environment [{nameof(ResourceManagerEndpoint)}={ResourceManagerEndpoint}].");
                }

                if (string.IsNullOrEmpty(GraphEndpoint) || !Uri.TryCreate(GraphEndpoint, UriKind.Absolute, out uri))
                {
                    throw new ClusterDefinitionException($"Invalid Azure environment [{nameof(GraphEndpoint)}={GraphEndpoint}].");
                }

                if (string.IsNullOrEmpty(AuthenticationEndpoint) || !Uri.TryCreate(AuthenticationEndpoint, UriKind.Absolute, out uri))
                {
                    throw new ClusterDefinitionException($"Invalid Azure environment [{nameof(ManagementEnpoint)}={ManagementEnpoint}].");
                }
            }
        }
    }
}
