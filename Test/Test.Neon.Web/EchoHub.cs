//-----------------------------------------------------------------------------
// FILE:	    EchoHub.cs
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

#if !NETCOREAPP3_1

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Primitives;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Service;
using Neon.Web.SignalR;
using Neon.Xunit;

using Xunit;

namespace Test.Neon.SignalR
{
    public class EchoHub : Hub
    {
        public string Echo(string message)
        {
            return message;
        }
        public Task SayHello(string optional = null)
        {
            return Clients.All.SendAsync("Echo", "Hello, World!");
        }

        public Task EchoGroup(string groupName, string message)
        {
            return Clients.Group(groupName).SendAsync("Echo", message);
        }

        public Task EchoUser(string userName, string message)
        {
            return Clients.User(userName).SendAsync("Echo", message);
        }

        public Task AddSelfToGroup(string groupName)
        {
            return Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }

        public Task AddUserToGroup(string connectionId, string groupName)
        {
            return Groups.AddToGroupAsync(connectionId, groupName);
        }

        public Task RemoveUserFromGroup(string connectionId, string groupName)
        {
            return Groups.RemoveFromGroupAsync(connectionId, groupName);
        }

        public Task RemoveSelfFromGroup(string groupName)
        {
            return Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }
    }
}

#endif
