//-----------------------------------------------------------------------------
// FILE:	    RemoteOperation.cs
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
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Win32;

using Couchbase;
using Newtonsoft.Json;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Data;
using Neon.Diagnostics;
using Neon.Net;
using Neon.Retry;
using Neon.Windows;

namespace Neon.Kube
{
    /// <summary>
    /// The payload passed to the desktop API server via <see cref="DesktopClient.StartOperationAsync(string)"/>
    /// and <see cref="DesktopClient.EndOperationAsync"/>.
    /// </summary>
    public class RemoteOperation
    {
        /// <summary>
        /// The caller's process ID.  The desktop application uses this to
        /// determine whether the caller has terminated before signalling
        /// that the operation has completed.
        /// </summary>
        [JsonProperty(PropertyName = "ProcessId", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "ProcessId", ApplyNamingConventions = false)]
        [DefaultValue(0)]
        public int ProcessId { get; set; }

        /// <summary>
        /// A brief summary of the operation being performed.
        /// </summary>
        [JsonProperty(PropertyName = "Summary", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "summary", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Summary { get; set; }

        /// <summary>
        /// Optionally specifies the text to be displayed by the desktop application
        /// as toast for calls to <see cref="DesktopClient.EndOperationAsync"/>.
        /// </summary>
        [JsonProperty(PropertyName = "CompletedToast", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "completedToast", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string CompletedToast { get; set; }

        /// <summary>
        /// Indicates whether the operation failed.
        /// </summary>
        [JsonProperty(PropertyName = "Failed", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "failed", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public bool Failed { get; set; }
    }
}
