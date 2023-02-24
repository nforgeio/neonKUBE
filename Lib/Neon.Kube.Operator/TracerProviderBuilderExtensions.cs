//-----------------------------------------------------------------------------
// FILE:	    TracerProviderBuilderExtensions.cs
// CONTRIBUTOR: Marcus Bowyer
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
    /// <summary>
    /// Kubernetes Operator Tracing Instrumentation.
    /// </summary>
    public static class TracerProviderBuilderExtensions
    {
        /// <summary>
        /// Adds Kubernetes Operator to the tracing pipeline.
        /// </summary>
        /// <param name="builder">Specifies the trace provider builder.</param>
        /// <returns>The <see cref="TracerProviderBuilder"/>.</returns>
        public static TracerProviderBuilder AddKubernetesOperatorInstrumentation(
            this TracerProviderBuilder builder)
        {
            builder.AddSource(TraceContext.ActivitySourceName);

            return builder;
        }
    }
}
