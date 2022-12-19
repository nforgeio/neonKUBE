//-----------------------------------------------------------------------------
// FILE:	    NodeSshProxy.Runtime.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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

// This file includes node methods that work after the cluster has been
// prepared.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Time;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Renci.SshNet;
using Renci.SshNet.Common;

namespace Neon.Kube.Proxy
{
    public partial class NodeSshProxy<TMetadata> : LinuxSshProxy<TMetadata>
        where TMetadata : class
    {
        /// <summary>
        /// <para>
        /// Installs one of the Helm charts that was pre-positioned on the node
        /// VM image.  These can be fond in the <see cref="KubeNodeFolder.Helm"/>
        /// with a folder for each chart. 
        /// </para>
        /// <note>
        /// This command <b>DOES NOT WAIT</b> for the Helm chart to be completely 
        /// installed and any target services or assets to be running because that
        /// does not appear to be reliable.  You'll need to explicitly verify that
        /// deployment has completed when necessary.
        /// </note>
        /// </summary>
        /// <param name="chartName">The Helm chart folder name.</param>
        /// <param name="releaseName">Optional component release name.  This defaults to <paramref name="chartName"/>.</param>
        /// <param name="namespace">
        /// Optional namespace where Kubernetes namespace where the Helm chart should
        /// be installed. This defaults to <b>"default"</b>.
        /// </param>
        /// <param name="timeout">Optional timeout.  This defaults to <b>unlimited</b>.</param>
        /// <param name="values">Optional Helm chart value overrides.</param>
        public void InstallProvisionedHelmChart(
            string                              chartName,
            string                              releaseName = null,
            string                              @namespace  = "default",
            TimeSpan                            timeout     = default,
            List<KeyValuePair<string, object>>  values      = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(chartName), nameof(chartName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(@namespace), nameof(@namespace));

            if (string.IsNullOrEmpty(releaseName))
            {
                releaseName = chartName;
            }

            var valueArgs = new StringBuilder();

            if (values != null)
            {
                foreach (var value in values)
                {
                    var valueType = value.Value.GetType();

                    if (valueType == typeof(string))
                    {
                        valueArgs.AppendWithSeparator($"--set-string {value.Key}=\"{value.Value}\"", @"\n");
                    }
                    else if (valueType == typeof(int))
                    {
                        valueArgs.AppendWithSeparator($"--set {value.Key}={value.Value}", @"\n");
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
            }

            var timeoutArg = string.Empty;

            if (timeout > TimeSpan.Zero)
            {
                timeoutArg = $"--timeout {(int)Math.Ceiling(timeout.TotalSeconds)}s";
            }

            var chartFolderPath = LinuxPath.Combine(KubeNodeFolder.Helm, chartName);
            var chartValuesPath = LinuxPath.Combine(chartFolderPath, "values.yaml");

            SudoCommand($"helm install {releaseName} {chartFolderPath} --namespace {@namespace} -f {chartValuesPath} {valueArgs} {timeoutArg}", RunOptions.Defaults | RunOptions.FaultOnError);
        }
    }
}
