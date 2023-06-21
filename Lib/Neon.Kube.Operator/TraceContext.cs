//-----------------------------------------------------------------------------
// FILE:        TraceContext.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Neon.Diagnostics;

using OpenTelemetry.Trace;

namespace Neon.Kube.Operator
{
    // $todo(marcusbooyah):
    //
    // I think we should really change this back to the Neon.Common TelemetryHub.
    // Without doing that, there's not really a clean way for other Neon libraries
    // to do tracing out-of-the-box.
    //
    // The Operator SDK is already referencing something like 17 nuget packages.
    // Neon.Common isn't any less special than any of those.  Some of the defaults
    // also kind of suck, for example, we never set our assembly versions so they'll
    // always be 0.0.0.0 and assembly versions don't support semantic versioning,
    // so we'll have no way of specifying alpha, beta, preview,...

    /// <summary>
    /// Tracing context.
    /// </summary>
    internal static class TraceContext
    {
        /// <summary>
        /// Returns the assembly name.
        /// </summary>
        internal static AssemblyName AssemblyName { get; } = typeof(TracerProviderBuilderExtensions).Assembly.GetName();

        /// <summary>
        /// Returns the activity source name.
        /// </summary>
        internal static string ActivitySourceName { get; } = AssemblyName.Name;

        /// <summary>
        /// Returns the the entry assembly version.
        /// </summary>
        internal static Version Version { get; } = AssemblyName.Version;

        /// <summary>
        /// Returns the activity source.
        /// </summary>
        internal static ActivitySource ActivitySource => Cached.Source.Value;

        static class Cached
        {
            internal static readonly Lazy<ActivitySource> Source = new Lazy<ActivitySource>(
            () =>
            {
                return new ActivitySource(ActivitySourceName, Version.ToString());
            });
        }
    }
}
