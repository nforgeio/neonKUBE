//-----------------------------------------------------------------------------
// FILE:        OpenEbsOptions.cs
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
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.IO;

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// Specifies cluster OpenEBS options.
    /// </summary>
    public class OpenEbsOptions
    {
        /// <summary>
        /// <para>
        /// Specifies which OpenEBS engine will be deployed within the cluster.  This defaults
        /// to <see cref="OpenEbsEngine.Default"/> which selects the <see cref="OpenEbsEngine.HostPath"/>
        /// engine for single node clusters, <see cref="OpenEbsEngine.Mayastor"/> otherwise.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "Engine", Required = Required.Default)]
        [YamlMember(Alias = "engine", ApplyNamingConventions = false)]
        [DefaultValue(OpenEbsEngine.Default)]
        public OpenEbsEngine Engine { get; set; } = OpenEbsEngine.Default;

        /// <summary>
        /// Validates the options.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        internal void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            var openEbsOptionsPrefix = $"{nameof(ClusterDefinition.Storage)}.{nameof(ClusterDefinition.Storage.OpenEbs)}";

            // Choose an actual engine when [Default] is specified.

            if (clusterDefinition.Storage.OpenEbs.Engine == OpenEbsEngine.Default)
            {
                if (clusterDefinition.Nodes.Count() == 1)
                {
                    clusterDefinition.Storage.OpenEbs.Engine = OpenEbsEngine.HostPath;
                }
                else
                {
                    clusterDefinition.Storage.OpenEbs.Engine = OpenEbsEngine.Mayastor;
                }
            }

            // $todo(jefflill): This logic should probably be relocated to cluster advice.

            // Clusters require that at least one node has [OpenEbsStorage=true] for the Mayastor engine.
            // We'll set this automatically when the user hasn't already done this.

            switch (clusterDefinition.Storage.OpenEbs.Engine)
            {
                case OpenEbsEngine.Mayastor:

                    if (!clusterDefinition.Nodes.Any(node => node.OpenEbsStorage))
                    {
                        if (clusterDefinition.Workers.Count() > 0)
                        {
                            foreach (var worker in clusterDefinition.Workers)
                            {
                                worker.OpenEbsStorage = true;
                            }
                        }
                        else
                        {
                            foreach (var controlNode in clusterDefinition.ControlNodes)
                            {
                                controlNode.OpenEbsStorage = true;
                            }
                        }
                    }
                    break;
            }
        }
    }
}
