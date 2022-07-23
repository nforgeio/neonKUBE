//-----------------------------------------------------------------------------
// FILE:	    DependencyInjectionExtensions.cs
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

using Neon.Common;

using MessagePack;

using NATS.Client;

namespace Neon.Web.SignalR
{
    /// <summary>
    /// Helpers for adding Neon NATS backplane via dependency injection.
    /// </summary>
    public static class DependencyInjectionExtensions
    {
        /// <summary>
        /// Adds scale-out to a <see cref="ISignalRServerBuilder"/>, using a shared Nats server.
        /// </summary>
        /// <param name="signalrBuilder">The <see cref="ISignalRServerBuilder"/>.</param>
        /// <returns>The same instance of the <see cref="IServiceCollection"/> for chaining.</returns>
        public static IServiceCollection AddNeonNats(
            this ISignalRServerBuilder signalrBuilder)
        {
            signalrBuilder.AddMessagePackProtocol()
                .Services.AddResponseCompression(opts =>
                {
                    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                        new[] { "application/octet-stream" });
                })
                .AddSingleton(typeof(HubLifetimeManager<>), typeof(NatsHubLifetimeManager<>));

            return signalrBuilder.Services;
        }

        /// <summary>
        /// Adds scale-out to a <see cref="ISignalRServerBuilder"/>, using a shared Nats server.
        /// </summary>
        /// <param name="signalrBuilder">The <see cref="ISignalRServerBuilder"/>.</param>
        /// <param name="natConnectionString">The nats connection string.</param>
        /// <returns>The same instance of the <see cref="IServiceCollection"/> for chaining.</returns>
        public static IServiceCollection AddNeonNats(
            this ISignalRServerBuilder signalrBuilder,
            string natConnectionString)
        {
            var connectionFactory = new ConnectionFactory();
            var options = ConnectionFactory.GetDefaultOptions();

            options.Servers = new string[] { natConnectionString };

            var connection = connectionFactory.CreateConnection(options);

            return AddNeonNats(signalrBuilder, connection);
        }

        /// <summary>
        /// Adds scale-out to a <see cref="ISignalRServerBuilder"/>, using a shared Nats server.
        /// </summary>
        /// <param name="signalrBuilder">The <see cref="ISignalRServerBuilder"/>.</param>
        /// <param name="connection">The nats <see cref="IConnection"/>.</param>
        /// <returns>The same instance of the <see cref="IServiceCollection"/> for chaining.</returns>
        public static IServiceCollection AddNeonNats(
            this ISignalRServerBuilder signalrBuilder,
            IConnection connection)
        {
            signalrBuilder.Services.AddSingleton(connection);

            return AddNeonNats(signalrBuilder);
        }
    }
}

#endif
