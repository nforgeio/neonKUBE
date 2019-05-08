//-----------------------------------------------------------------------------
// FILE:	    ServiceApiContact.cs
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
    /// Holds the contact information for a service API.  This maps closely
    /// to the <b>Microsoft.OpenApi.Models.OpenApiContact</b> class.
    /// </summary>
    public class ServiceApiContact
    {
        /// <summary>
        /// The name of the contact person or organiztion.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The URL pointing to the contact information.
        /// </summary>
        public Uri Url { get; set; }

        /// <summary>
        /// The email address of the contact person or organization formatted
        /// as a URL like: <b>mailto:joe@blow.com</b>
        /// </summary>
        public string Email { get; set; }
    }
}
