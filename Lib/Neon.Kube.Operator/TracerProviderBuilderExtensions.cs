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
        /// The assembly name.
        /// </summary>
        internal static readonly AssemblyName AssemblyName = typeof(TracerProviderBuilderExtensions).Assembly.GetName();

        /// <summary>
        /// The activity source name.
        /// </summary>
        internal static readonly string ActivitySourceName = AssemblyName.Name;

        /// <summary>
        /// The version.
        /// </summary>
        internal static readonly Version Version = AssemblyName.Version;

        /// <summary>
        /// Adds Kubernetes Operator to the tracing pipeline.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static TracerProviderBuilder AddKubernetesOperatorInstrumentation(
            this TracerProviderBuilder builder)
        {
            if (TelemetryHub.ActivitySource == null) 
            {
                TelemetryHub.ActivitySource = new ActivitySource(ActivitySourceName, Version.ToString());

                builder.AddSource(ActivitySourceName);
            }

            return builder;
        }
    }
}
