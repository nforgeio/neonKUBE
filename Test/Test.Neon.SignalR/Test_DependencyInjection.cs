//-----------------------------------------------------------------------------
// FILE:	    NatsDependencyInjectionExtensionsTests.cs
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

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.SignalR;
using Neon.Xunit;

using NATS.Client;

using Xunit;

namespace TestNeonSignalR
{
    public class NatsDependencyInjectionExtensionsTests
    {
        [Fact]
        public void AddNatsWithConnectionStringProperlyParsesOptions()
        {
            var defaultOptions = ConnectionFactory.GetDefaultOptions(); 
            var services = new ServiceCollection();
            services.AddSignalR().AddNeonNats(ConnectionFactory.GetDefaultOptions());
            var provider = services.BuildServiceProvider();

            var options = provider.GetService<IOptions<NATS.Client.Options>>();
            Assert.NotNull(options.Value);
            Assert.Equal(defaultOptions.Url, options.Value.Url);
            Assert.Equal(defaultOptions.Servers.Length, options.Value.Servers.Length);
        }
    }
}
