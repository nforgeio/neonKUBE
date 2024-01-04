//-----------------------------------------------------------------------------
// FILE:        OtlpLogExporterWrapper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;

using OpenTelemetry.Logs;

namespace OpenTelemetry.Exporter
{
    /// <summary>
    /// This class wraps the OpenTelemetry <c>internal OtlpLogExporter</c> class via
    /// reflection so the <b>neon-desktop-service</b> can instantiate an instance for
    /// forwarding logs from <b>neon-desktop</b> and <b>neon-cli</b>.
    /// </summary>
    public class OtlpLogExporterWrapper
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Creates a <see cref="OtlpLogExporterWrapper"/> instance.
        /// </summary>
        /// <param name="options">Specifies the exporter options.</param>
        /// <returns>The created <see cref="OtlpLogExporterWrapper"/>.</returns>
        /// <exception cref="NotSupportedException">
        /// Thrown when the internal [OpenTelemetry.Exporter.OtlpLogExporter] type could not
        /// be located or constructed, probably due to OpenTelemetry changes.
        /// </exception>
        public static OtlpLogExporterWrapper Create(OtlpExporterOptions options)
        {
            Covenant.Requires<ArgumentNullException>(options != null, nameof(options));

            // $hack(jefflill):
            //
            // Unfortunately, the [OpenTelemetry.Exporter.OtlpLogExporter] is internal, so we can't
            // instantiate instances directly, so we're going to use reflection instead.  This is
            // FRAGILE in general and probably especially so here because the .NET OpenTelemetry
            // logging API is still in beta.
            //
            // There's a decent chance that this will need to be refactored in the future.

            var assembly            = typeof(OtlpExporterOptions).Assembly;
            var otlpLogExporterType = assembly.GetType("OpenTelemetry.Exporter.OtlpLogExporter");

            if (otlpLogExporterType != null)
            {
                // $hack(jefflill):
                //
                // We're assuming that [OtlpLogExporter] class has a single public constructor
                // with a single [OtlpExporterOptions] parameter.

                var constructorInfo = otlpLogExporterType.GetConstructor(new Type[] { typeof(OtlpExporterOptions) });

                if (constructorInfo != null)
                {
                    // Locate the exporter's Export() and Shutdown() methods.

                    var methods        = otlpLogExporterType.GetMethods();
                    var exportMethod   = methods.Single(method => method.Name == "Export" && method.IsPublic && !method.IsStatic);
                    var shutdownMethod = otlpLogExporterType.GetMethod("Shutdown", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy, new Type[] { typeof(int) });

                    Covenant.Assert(exportMethod != null, "Cannot locate the [OpenTelemetry.Exporter.OtlpLogExporter.Export(Batch<LogRecord>)] method.  NEONFORGE may need to refactor reflection for internal OpenTelemetry changes.");
                    Covenant.Assert(shutdownMethod != null, "Cannot locate the [OpenTelemetry.Exporter.OtlpLogExporter.OnShutdown(int)] method.  NEONFORGE may need to refactor reflection for internal OpenTelemetry changes.");

                    return new OtlpLogExporterWrapper(constructorInfo.Invoke(new object[] { options }), exportMethod, shutdownMethod);
                }
            }

            throw new NotSupportedException("Cannot locate the type or constructor for [OpenTelemetry.Exporter.OtlpLogExporter].  NEONFORGE may need to refactor reflection for internal OpenTelemetry changes.");
        }

        //---------------------------------------------------------------------
        // Instance members

        private object          logExporter;
        private MethodInfo      exportMethod;
        private MethodInfo      shutdownMethod;

        /// <summary>
        /// Private constructor.
        /// </summary>
        /// <param name="logExporter">The [OpenTelemetry.Exporter.OtlpLogExporter] instance obtained above via reflection.</param>
        /// <param name="exportMethod">The exporter's [Export(batch)] method.</param>
        /// <param name="shutdownMethod">The exporter's [Shutdown(int)] method.</param>
        private OtlpLogExporterWrapper(object logExporter, MethodInfo exportMethod, MethodInfo shutdownMethod)
        {
            this.logExporter    = logExporter;
            this.exportMethod   = exportMethod;
            this.shutdownMethod = shutdownMethod;
        }

        /// <summary>
        /// Exports a batch of telemetry objects.
        /// </summary>
        /// <param name="batch">Batch of telemetry objects to export.</param>
        /// <returns>Result of the export operation.</returns>
        public ExportResult Export(Batch<LogRecord> batch)
        {
            return (ExportResult)exportMethod.Invoke(logExporter, new object[] { batch });
        }

        /// <summary>
        /// Attempts to shutdown the exporter, blocks the current thread until
        /// shutdown has completed or timed out.
        /// </summary>
        /// <param name="timeoutMilliseconds">
        /// The number (non-negative) of milliseconds to wait, or
        /// <c>Timeout.Infinite</c> to wait indefinitely.
        /// </param>
        /// <returns>
        /// Returns <c>true</c> when shutdown succeeded; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the <c>timeoutMilliseconds</c> is smaller than -1.
        /// </exception>
        /// <remarks>
        /// This function guarantees thread-safety. Only the first call will
        /// win, subsequent calls will be no-op.
        /// </remarks>
        public bool Shutdown(int timeoutMilliseconds)
        {
            return (bool)shutdownMethod.Invoke(logExporter, new object[] { timeoutMilliseconds });
        }
    }
}
