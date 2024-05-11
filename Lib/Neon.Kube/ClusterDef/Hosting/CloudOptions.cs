//-----------------------------------------------------------------------------
// FILE:        CloudOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using System.Net;
using System.Net.Sockets;
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

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// Describes cloud related cluster settings.
    /// </summary>
    public class CloudOptions
    {
        /// <summary>
        /// <para>
        /// Specifies that cloud resources created for the cluster have their names prefixed
        /// by the cluster name.  This is a tri-state value that may be <see cref="TriState.Default"/>,
        /// <see cref="TriState.True"/> or <see cref="TriState.False"/>.  <c>Default</c> indicates that
        /// cloud hosting manager will decide whether it makes sense to prefix resource names by default 
        /// (see the remarks for details), otherwise you can explicitly control this by specifying
        /// <c>True</c> or <c>False</c>.
        /// </para>
        /// <para>
        /// This defaults to <c>null</c>.
        /// </para>
        /// </summary>
        /// <remarks>
        /// <para>
        /// When this property is <c>null</c> (the default), the hosting manager for the target
        /// cloud decides whether or not to prefix resource names with the cluster name.
        /// </para>
        /// <list type="table">
        /// <item>
        ///     <term><b>AWS</b></term>
        ///     <description>
        ///     Resource names are <b>always</b> prefixed for AWS deployments.  This makes sense because
        ///     AWS resource names are globally scoped and also because load balancer names are
        ///     required to be unique within an AWS account and region.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Azure</b></term>
        ///     <description>
        ///     Resource names are not prefixed by default.  NeonKUBE clusters deployed to Azure
        ///     are always created in a resource group and Azure scopes resource names to the group.
        ///     This means that the prefix really isn't necessary. 
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Google Cloud</b></term>
        ///     <description>
        ///     $todo(jefflill): Update this once we've implemented the Google hosting manager.
        ///     </description>
        /// </item>
        /// </list>
        /// <note>
        /// It is possible though to deploy a cluster into an existing resource group, along
        /// with other already existing resources (perhaps another NeonKUBE cluster).  You'll 
        /// need to take care in this situation to avoid resource name conflicts.  To handle this,
        /// set this property to <c>true</c> such that every cluster resource created will 
        /// include the cluster name in the resource name prefix.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "PrefixResourceNames", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "prefixResourceNames", ApplyNamingConventions = false)]
        [DefaultValue(TriState.Default)]
        public TriState PrefixResourceNames { get; set; } = TriState.Default;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));
        }
    }
}
