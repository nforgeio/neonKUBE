//-----------------------------------------------------------------------------
// FILE:	    HubProtocolHelpers.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.SignalR.Protocol;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.SignalR;
using Neon.Xunit;

using NATS.Client;

using Xunit;

namespace TestNeonSignalR
{
    public static class HubProtocolHelpers
    {
        public static readonly IHubProtocol NewtonsoftJsonHubProtocol = new NewtonsoftJsonHubProtocol();

        public static readonly IHubProtocol MessagePackHubProtocol = new MessagePackHubProtocol();

        public static readonly List<string> AllProtocolNames = new List<string>
        {
            NewtonsoftJsonHubProtocol.Name,
            MessagePackHubProtocol.Name
        };

        public static readonly IList<IHubProtocol> AllProtocols = new List<IHubProtocol>()
        {
            NewtonsoftJsonHubProtocol,
            MessagePackHubProtocol
        };

        public static IHubProtocol GetHubProtocol(string name)
        {
            var protocol = AllProtocols.SingleOrDefault(p => p.Name == name);
            if (protocol == null)
            {
                throw new InvalidOperationException($"Could not find protocol with name '{name}'.");
            }

            return protocol;
        }
    }
}
