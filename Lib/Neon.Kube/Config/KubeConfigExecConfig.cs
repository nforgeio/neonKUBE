//-----------------------------------------------------------------------------
// FILE:        KubeConfigExecConfig.cs
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Cryptography;
using Neon.Kube;

namespace Neon.Kube.Config
{
    /// <summary>
    /// Describes a custom exec based authentication plugin.
    /// </summary>
    public class KubeConfigExecConfig
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeConfigExecConfig()
        {
        }

        /// <summary>
        /// Specifies the command to execute.
        /// </summary>
        [JsonProperty(PropertyName = "command", Required = Required.Always)]
        [YamlMember(Alias = "command", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.Preserve)]
        public string Command { get; set; }

        /// <summary>
        /// Specifies the arguments to be passed to <see cref="Command"/>.
        /// </summary>
        [JsonProperty(PropertyName = "args", Required = Required.Always)]
        [YamlMember(Alias = "args", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.Preserve)]
        public List<string> Args { get; set; } = new List<string>();

        /// <summary>
        /// Optionally specifies any environment variables to be set before executing <see cref="Command"/>.
        /// </summary>
        [JsonProperty(PropertyName = "env", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "env", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public List<KubeConfigEnvironmentVariable> Env { get; set; }

        /// <summary>
        /// Specifies the preferred input version of the ExecInfo. The returned ExecCredentials <b>MUST</b>
        /// use the same encoding version as the input.
        /// </summary>
        [JsonProperty(PropertyName = "apiVersion", Required = Required.Always)]
        [YamlMember(Alias = "apiVersion", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.Preserve)]
        public string ApiVersion { get; set; }

        /// <summary>
        /// Specifies text to be displayed to the user when the <see cref="Command"/> executable is not
        /// present.  For example, brew <c>install foo-cli</c> might be a good InstallHint for foo-cli 
        /// on MacOS.
        /// </summary>
        [JsonProperty(PropertyName = "installHint", Required = Required.Always)]
        [YamlMember(Alias = "installHint", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.Preserve)]
        public string InstallHint { get; set; }

        /// <summary>
        /// Indicates whether or not to provide cluster information, which could potentially contain very
        /// large CA data, to this exec plugin as a part of the <c>KUBERNETES_EXEC_INFO</c> environment variable. 
        /// By default, it is set to <c>false</c>. Package k8s.io/client-go/tools/auth/exec provides helper methods
        /// for reading this environment variable.
        /// </summary>
        [JsonProperty(PropertyName = "provideClusterInfo", Required = Required.Always)]
        [YamlMember(Alias = "provideClusterInfo", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.Preserve)]
        public bool ProviderClustyerInfo { get; set; }

        /// <summary>
        /// <para>
        /// InteractiveMode determines this plugin's relationship with standard input. Valid values are
        /// <see cref="KubeConfigExecInteractiveMode.Never"/> (this exec plugin never uses standard input), 
        /// <see cref="KubeConfigExecInteractiveMode.IfAvailable"/>" (this exec plugin wants to use standard
        /// input if it is available), or <see cref="KubeConfigExecInteractiveMode.Always"/> (this exec
        /// plugin requires standard input to function). See ExecInteractiveMode values for more details.
        /// </para>
        /// <para>
        /// If <see cref="ApiVersion"/> is <b>client.authentication.k8s.io/v1alpha1</b> or 
        /// <b>client.authentication.k8s.io/v1beta1</b>, then this field is optional and 
        /// defaults to KubeConfigExecInteractiveMode.Always when unset. Otherwise, this 
        /// field is required.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "interactiveMode", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "interactiveMode", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public KubeConfigExecInteractiveMode? ExecInteractiveMode { get; set; }
    }
}
