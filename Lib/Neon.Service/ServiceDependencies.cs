//-----------------------------------------------------------------------------
// FILE:	    ServiceDependencies.cs
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
using System.Data;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Windows;

using Prometheus;

namespace Neon.Service
{
    /// <summary>
    /// Used to specify other services that must be reachable via the network before a
    /// <see cref="NeonService"/> will be allowed to start.  This is exposed via the
    /// <see cref="NeonService.Dependencies"/> where these values can be configured in
    /// code before <see cref="NeonService.RunAsync(bool)"/> is called or they can
    /// also be configured via environment variables as described in the remarks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class will be initialized using the following environment variables when
    /// present:
    /// </para>
    /// <code>
    /// NEON_SERVICE_DEPENDENCIES_URIS=http://foo.com;tcp://10.0.0.55:1234
    /// NEON_SERVICE_DEPENDENCIES_DISABLE_DNS_CHECK=false
    /// NEON_SERVICE_DEPENDENCIES_TIMEOUT_SECONDS=30
    /// NEON_SERVICE_DEPENDENCIES_WAIT_SECONDS=5
    /// </code>
    /// <para>
    /// Where you can specify multiple dependency URIs separated by semicolons <b>(;)</b>
    /// and the timeout and wait times as seconds.
    /// </para>
    /// <note>
    /// Only HTTP, HTTPS, and TCP URIs are supported.
    /// </note>
    /// <para>
    /// We also verify that your service is able to perform DNS queries by default by
    /// performing a DNS name lookup for <see cref="DnsCheckHostName"/> (<b>net-check.neoncloud.io</b>).
    /// It doesn't matter that this host name is actually registered or that you're cluster has 
    /// Internet access.  We're just looking for any response from the upstream DNS server
    /// to determine whether the service has and network connectivity.
    /// </para>
    /// <para>
    /// You can disable this by setting <see cref="DisableDnsCheck"/><b>true</b> or
    /// the <c>NEON_SERVICE_DEPENDENCIES_DISABLE_DNS_CHECK</c> environment variable to
    /// <c>false</c>.
    /// </para>
    /// </remarks>
    public class ServiceDependencies
    {
        /// <summary>
        /// The host name used for the DNS availablity check.  It doesn't matter that this
        /// host name is actually registered or that you're cluster has Internet access.
        /// We're just looking for any response from the upstream DNS server.
        /// </summary>
        public const string DnsCheckHostName = "net-check.neoncloud.io";

        /// <summary>
        /// Constructor.
        /// </summary>
        public ServiceDependencies()
        {
            // $hack(jefflill): This screams for dependency injection.

            var textLogger = new TextLogger(LogManager.Default);

            // Parse: NEON_SERVICE_DEPENDENCIES_URIS

            var urisVar = Environment.GetEnvironmentVariable("NEON_SERVICE_DEPENDENCIES_URIS");

            if (!string.IsNullOrEmpty(urisVar))
            {
                var uris = urisVar.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var item in uris)
                {
                    var uriString = item.Trim();

                    if (string.IsNullOrEmpty(uriString))
                    {
                        continue;
                    }

                    if (Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
                    {
                        Uris.Add(uri);
                    }
                    else
                    {
                        textLogger.LogWarn($"Service Dependency: [{uriString}] is not a valid URI and will be ignored.");
                    }
                }
            }

            // Parse: NEON_SERVICE_DEPENDENCIES_DISABLE_DNS_CHECK

            var disableDnsCheckVar = Environment.GetEnvironmentVariable("NEON_SERVICE_DEPENDENCIES_DISABLE_DNS_CHECK");

            if (!string.IsNullOrEmpty(disableDnsCheckVar))
            {
                if (NeonHelper.TryParseBool(disableDnsCheckVar, out var disableDnsCheck))
                {
                    DisableDnsCheck = disableDnsCheck;
                }
                else
                {
                    textLogger.LogWarn($"Service Dependency: [NEON_SERVICE_DEPENDENCIES_DISABLE_DNS_CHECK={disableDnsCheckVar}] is not a valid and will be ignored.");
                }
            }

            // Parse: NEON_SERVICE_DEPENDENCIES_TIMEOUT_SECONDS

            var timeoutSecondsVar = Environment.GetEnvironmentVariable("NEON_SERVICE_DEPENDENCIES_TIMEOUT_SECONDS");

            if (!string.IsNullOrEmpty(timeoutSecondsVar))
            {
                if (double.TryParse(timeoutSecondsVar, out var timeoutSeconds) && timeoutSeconds >= 0)
                {
                    Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                }
                else
                {
                    textLogger.LogWarn($"Service Dependency: [NEON_SERVICE_DEPENDENCIES_TIMEOUT_SECONDS={timeoutSecondsVar}] is not a valid and will be ignored.");
                }
            }

            // Parse: NEON_SERVICE_DEPENDENCIES_WAIT_SECONDS

            var waitSecondsVar = Environment.GetEnvironmentVariable("NEON_SERVICE_DEPENDENCIES_WAIT_SECONDS");

            if (!string.IsNullOrEmpty(waitSecondsVar))
            {
                if (double.TryParse(waitSecondsVar, out var waitSeconds) && waitSeconds >= 0)
                {
                    Wait = TimeSpan.FromSeconds(waitSeconds);
                }
                else
                {
                    textLogger.LogWarn($"Service Dependency: [NEON_SERVICE_DEPENDENCIES_WAIT_SECONDS={waitSecondsVar}] is not a valid and will be ignored.");
                }
            }
        }

        /// <summary>
        /// Specifies the URIs for external services that must be reachable via the network
        /// before your service will be allowed to start.  Only HTTP, HTTPS, and TCP URIs
        /// are supported.  Any URIs found in the <c>NEON_SERVICE_DEPENDENCIES_URIS</c>
        /// environment variables will be added to this list.
        /// </summary>
        public List<Uri> Uris { get; set; } = new List<Uri>();

        /// <summary>
        /// Use this to disable the DNS availablity check for your service.
        /// </summary>
        /// <remarks>
        /// <para>
        /// We also verify that your service is able to perform DNS queries by default by
        /// performing a DNS name lookup for <see cref="DnsCheckHostName"/> (<b>net-check.neoncloud.io</b>).
        /// It doesn't matter that this host name is actually registered or that you're cluster has 
        /// Internet access.  We're just looking for any response from the upstream DNS server
        /// to determine whether the service has and network connectivity.
        /// </para>
        /// <para>
        /// You can disable this by setting <see cref="DisableDnsCheck"/><b>true</b> or
        /// the <c>NEON_SERVICE_DEPENDENCIES_DISABLE_DNS_CHECK</c> environment variable to
        /// <c>false</c>.
        /// </para>
        /// </remarks>
        public bool DisableDnsCheck { get; set; } = false;

        /// <summary>
        /// The maximum time to wait for the services specified by <see cref="Uris"/> to
        /// be reachable.  You service will be terminated if this is exceeded.  This
        /// defaults to <b>120 seconds</b> or the <c>NEON_SERVICE_DEPENDENCIES_TIMEOUT_SECONDS</c>
        /// environment variable.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(120);

        /// <summary>
        /// Used in internal unit tests to override <see cref="Timeout"/> so tests won't have
        /// to wait so long for things to timeout.
        /// </summary>
        internal TimeSpan? TestTimeout { get; set; } = null;

        /// <summary>
        /// Additional time to wait after the services specified by <see cref="Uris"/> are
        /// ready before the service will be started.  This defaults to <b>0 seconds</b>
        /// or the <c>NEON_SERVICE_DEPENDENCIES_WAIT_SECONDS</c> environment variable.
        /// </summary>
        public TimeSpan Wait { get; set; } = TimeSpan.Zero;
    }
}
