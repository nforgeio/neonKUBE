//-----------------------------------------------------------------------------
// FILE:	    OpenEbsOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
        private const string minNfsSize = "10 GiB";

        /// <summary>
        /// <para>
        /// Specifies which OpenEBS engine will be deployed within the cluster.  This defaults
        /// to <see cref="OpenEbsEngine.Default"/>.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "Engine", Required = Required.Default)]
        [YamlMember(Alias = "engine", ApplyNamingConventions = false)]
        [DefaultValue(OpenEbsEngine.Default)]
        public OpenEbsEngine Engine { get; set; } = OpenEbsEngine.Default;

        /// <summary>
        /// The size of the NFS file system to be created for the cluster.  This defaults
        /// to <b>10 GiB</b> and cannot be any smaller.
        /// </summary>
        [JsonProperty(PropertyName = "NfsSize", Required = Required.Default)]
        [YamlMember(Alias = "nfsSize", ApplyNamingConventions = false)]
        [DefaultValue(minNfsSize)]
        public string NfsSize { get; set; } = minNfsSize;

        /// <summary>
        /// Validates the options.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        internal void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            var openEbsOptionsPrefix = $"{nameof(ClusterDefinition.Storage)}.{nameof(ClusterDefinition.Storage.OpenEbs)}";

            NfsSize = NfsSize ?? minNfsSize;

            ClusterDefinition.ValidateSize(NfsSize, typeof(OpenEbsOptions), nameof(NfsSize), minimum: minNfsSize);

            // Clusters require that at least one node has [OpenEbsStorage=true].  We'll set
            // this automatically when the user hasn't already done this.  All workers will have
            // this set to true when there are workers, otherwise we'll set this to true for all
            // control-plane nodes.

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

            switch (Engine)
            {
                case OpenEbsEngine.Default:

                    if (clusterDefinition.Nodes.Count() == 1)
                    {
                        Engine = OpenEbsEngine.HostPath;
                    }
                    else if (clusterDefinition.Nodes.Count(n => n.OpenEbsStorage) > 0)
                    {
                        Engine = OpenEbsEngine.Jiva;
                    }
                    break;

                case OpenEbsEngine.HostPath:

                    if (clusterDefinition.Nodes.Count() > 1)
                    {
                        new ClusterDefinitionException($"[{openEbsOptionsPrefix}.{nameof(Engine)}={Engine}] storage engine is supported only for single-node clusters.");
                    }
                    break;

                case OpenEbsEngine.cStor:

                    break; // NOP

                case OpenEbsEngine.Jiva:

                    break; // NOP

                default:
                case OpenEbsEngine.Mayastor:

                    throw new ClusterDefinitionException($"[{openEbsOptionsPrefix}.{nameof(Engine)}={Engine}] storage engine is not implemented.");
            }
        }
    }
}
