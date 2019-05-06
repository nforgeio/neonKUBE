//-----------------------------------------------------------------------------
// FILE:	    ServiceApiInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;

namespace Neon.Service
{
    /// <summary>
    /// <para>
    /// Human readable metadata for a service API.  This maps pretty closely
    /// to the <c>Microsoft.OpenApi.Models.OpenApiInfo</c> class which is used
    /// to by Swagger when generating ASP.NET documentation.
    /// </para>
    /// <note>
    /// We're not referencing the <b>Microsoft.OpenApi</b> nuget package to
    /// avoid adding about 166KB to applications using the <b>Neon.Common</b>
    /// assembly.f
    /// </note>
    /// </summary>
    public class ServiceApiInfo
    {
        /// <summary>
        /// API documentation title.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// More detailed API description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// API version.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// References the API terms of service.
        /// </summary>
        public Uri TermsOfService { get; set; }

        /// <summary>
        /// API contact information.
        /// </summary>
        public ServiceApiContact Contact { get; set;}

        /// <summary>
        /// API licence information.
        /// </summary>
        public ServiceApiLicense License { get; set; }
    }
}
