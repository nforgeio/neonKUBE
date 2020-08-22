//-----------------------------------------------------------------------------
// FILE:	    CloudOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Xml;

namespace Neon.Kube
{
    /// <summary>
    /// Describes cloud related cluster settings.
    /// </summary>
    public class CloudOptions
    {
        /// <summary>
        /// Specifies that cloud resources created for the cluster have their names prefixed
        /// by the cluster name.  This defaults to <c>false</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// neonKUBE cluster resources are deployed to a cluster specific resource group
        /// by default.  This means that there's generally no reason to prefix the cluster
        /// resource names because they are already scoped to the cluster's resource group.
        /// </para>
        /// <para>
        /// It is possible though to deploy a cluster into an existing resource group, along
        /// with other already existing resources (perhaps another neonKUBE cluster).  You'll need to
        /// take care in this situation to avoid resource name conflicts.  To handle this,
        /// set this property to <c>true</c> such that every cluster resource created will 
        /// include the cluster name in the resource name prefix.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "PrefixResourceNames", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "prefixResourceNames", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool PrefixResourceNames { get; set; } = false;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));
        }
    }
}
