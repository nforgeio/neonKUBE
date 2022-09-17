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

using OpenTelemetry.Logs;

namespace OpenTelemetry.Exporter
{
    /// <summary>
    /// This class wraps the OpenTelemetry <c>internal OtlpLogExporter</c> class via
    /// reflection so the <b>neon-desktop-server</b> can instantiate an instance for
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
        public static OtlpLogExporterWrapper Create(OtlpExporterOptions options)
        {
            throw new NotImplementedException();
        }

        //---------------------------------------------------------------------
        // Instance members
    }
}
