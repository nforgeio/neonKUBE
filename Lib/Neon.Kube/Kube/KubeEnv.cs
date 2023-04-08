//-----------------------------------------------------------------------------
// FILE:	    KubeEnv.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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

using Neon.Common;

namespace Neon.Kube
{
    /// <summary>
    /// Defines the neonKUBE related environment variables as well as properties
    /// that access them.
    /// </summary>
    public static class KubeEnv
    {
        /// <summary>
        /// Environment variable used to disable telemetry from neonKUBE client applications.
        /// The supported values are <b>true</b> or <b>false</b>.
        /// </summary>
        public const string DisableTelemetryVariable = "NEONKUBE_DISABLE_TELEMETRY";

        /// <summary>
        /// Determines whether the user has disabled Neon telemetry by the presence
        /// of the <c>NEONKUBE_DISABLE_TELEMETRY=true</c> environment variable.
        /// </summary>
        public static bool IsTelemetryDisabled
        {
            get => "true".Equals(Environment.GetEnvironmentVariable(DisableTelemetryVariable), StringComparison.InvariantCultureIgnoreCase);
            set => Environment.SetEnvironmentVariable(DisableTelemetryVariable, NeonHelper.ToBoolString(value));
        }

        /// <summary>
        /// Environment variable used by developers to redirect client application telemetry
        /// to a non-production cluster for testing purposes.
        /// </summary>
        public const string TelemetryUriVariable = "NEONKUBE_TELEMETRY_URI";

        /// <summary>
        /// Returns the OTEL Collector Log endpoint URI where neonKUBE related clients should direct
        /// their telemetry.  This defaults to <b>https://telemetry.neoncloud.io</b> but can
        /// be modified by developers for testing purposes by setting the <c>NEONKUBE_TELEMETRY_URI</c>
        /// environment variable.
        /// </summary>
        public static Uri TelemetryLogsUri => new Uri(Environment.GetEnvironmentVariable(TelemetryUriVariable) ?? "https://telemetry.neoncloud.io/v1/logs", UriKind.Absolute);

        /// <summary>
        /// Returns the OTEL Collector Trace endpoint URI where neonKUBE related clients should direct
        /// their telemetry.  This defaults to <b>https://telemetry.neoncloud.io</b> but can
        /// be modified by developers for testing purposes by setting the <c>NEONKUBE_TELEMETRY_URI</c>
        /// environment variable.
        /// </summary>
        public static Uri TelemetryTracesUri => new Uri(Environment.GetEnvironmentVariable(TelemetryUriVariable) ?? "https://telemetry.neoncloud.io/v1/traces", UriKind.Absolute);

        /// <summary>
        /// Environment variable used by developers to redirect cloent application headend service
        /// requests for testing purposes.
        /// </summary>
        public const string HeadendUriVariable = "NEONKUBE_HEADEND_URI";

        /// <summary>
        /// Returns the URI neonKUBE related headend services.  This defaults to <b>https://headend.neoncloud.io</b>
        /// but can be modified bhy developers for testing purposes by setting the <c>NEONKUBE_HEADEND_URI</c>
        /// environment variable.
        /// </summary>
        public static Uri HeadendUri => new Uri(Environment.GetEnvironmentVariable(HeadendUriVariable) ?? "https://headend.neoncloud.io", UriKind.Absolute);
    }
}
