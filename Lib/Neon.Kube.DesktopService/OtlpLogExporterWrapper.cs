//-----------------------------------------------------------------------------
// FILE:	    OtlpLogExporterWrapper.cs
// CONTRIBUTOR: Jeff Lill
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
using System.Diagnostics.Contracts;
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
        /// 
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">
        /// Thrown when the internal [OpenTelemetry.Exporter.OtlpLogExporter] type could not
        /// be located or constructed, probably due to OpenTelemetry changes.
        /// </exception>
        public static OtlpLogExporterWrapper Create(OtlpExporterOptions options)
        {
            Covenant.Requires<ArgumentNullException>(options != null, nameof(options));

            // $hack(jefflill):
            //
            // Unforunately, the [OpenTelemetry.Exporter.OtlpLogExporter] is internal, so we can't
            // instantiate instances directly, so we're going to use reflection instead.  This is
            // FRAGILE in general and probably especially so here because the .NET OpenTelemetry
            // logging API is still alpha.
            //
            // There's a decent chance that this will need to be refactored in the future.

            var assembly            = typeof(OtlpLogExporterHelperExtensions).Assembly;
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
                    var exportMethod = otlpLogExporterType.GetMethod("Export", BindingFlags.Public);

                    if (exportMethod != null)
                    {
                        if (exportMethod.GetParameters().Length == 1 || exportMethod.ReturnParameter.ParameterType == typeof(ExportResult))
                        {
                            return new OtlpLogExporterWrapper(constructorInfo.Invoke(new object[] { options }), exportMethod);
                        }
                    }
                }
            }

            throw new NotSupportedException("Unable to locate the type or constructor for [OpenTelemetry.Exporter.OtlpLogExporter].  You may need to refactor reflection for internal OpenTelemetry changes.");
        }

        //---------------------------------------------------------------------
        // Instance members

        private object      logExporter;
        private MethodInfo  exportMethod;

        /// <summary>
        /// Private constructor.
        /// </summary>
        /// <param name="logExporter">The [OpenTelemetry.Exporter.OtlpLogExporter] instance obtained above via reflection.</param>
        /// <param name="exportMethod">The exporter's [Export] method.</param>
        private OtlpLogExporterWrapper(object logExporter, MethodInfo exportMethod)
        {
            this.logExporter  = logExporter;
            this.exportMethod = exportMethod;
        }

        /// <summary>
        /// Submits the batch of log records passed for delivery via the wrapped exporter.
        /// </summary>
        /// <param name="logRecordBatch">The log record batch.</param>
        /// <returns>The <see cref="ExportResult"/>.</returns>
        public ExportResult Export(Batch<LogRecord> logRecordBatch)
        {
            return (ExportResult)exportMethod.Invoke(logExporter, new object[] { logRecordBatch });
        }
    }
}
