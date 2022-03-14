//-----------------------------------------------------------------------------
// FILE:	    DockerClient.Node.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Tasks;

namespace Neon.Docker
{
    public partial class DockerClient
    {
        //---------------------------------------------------------------------
        // Implements Docker Node related operations.

        /// <summary>
        /// Lists the swarm nodes.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The node list.</returns>
        public async Task<List<DockerNode>> NodeListAsync(CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear();

            var response  = await JsonClient.GetAsync(GetUri("nodes"), cancellationToken: cancellationToken);
            var nodes     = new List<DockerNode>();
            var nodeArray = response.As<JArray>();

            foreach (var node in nodeArray)
            {
                nodes.Add(new DockerNode(node));
            }

            return nodes;
        }
    }
}