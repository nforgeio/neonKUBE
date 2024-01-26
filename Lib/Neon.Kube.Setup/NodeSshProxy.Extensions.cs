//-----------------------------------------------------------------------------
// FILE:        NodeSshProxy.Extensions.cs
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

// This file includes node configuration methods executed while setting
// up a NEONKUBE cluster.

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

using Neon.Collections;
using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Proxy;
using Neon.Kube.Setup;
using Neon.Kube.SSH;
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Time;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Renci.SshNet;
using Renci.SshNet.Common;

namespace Neon.Kube.SSH
{
    /// <summary>
    /// Extends the <see cref="NodeSshProxy{TMetadata}"/> class by adding cluster setup related methods.
    /// </summary>
    public static class NodeSshProxyExtensions
    {
        /// <summary>
        /// Installs the Helm charts as a single ZIP archive written to the 
        /// NEONKUBE node's Helm folder.
        /// </summary>
        /// <param name="node">The node instance.</param>
        /// <param name="controller">The setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// <note>
        /// This method replaces any <b>$&lt;KubeVersion.NAME&gt;</b> references
        /// within the Helm chart files with the corresponding public constant,
        /// field, or property value from <see cref="KubeVersion"/>.
        /// </note>
        /// </remarks>
        public static async Task NodeInstallHelmArchiveAsync(this ILinuxSshProxy node, ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            using (var ms = new MemoryStream())
            {
                controller.LogProgress(node, verb: "install", message: "helm charts (zip)");

                var helmFolder   = KubeSetup.Resources.GetDirectory("/Helm");    // $hack(jefflill): https://github.com/nforgeio/neonKUBE/issues/1121
                var preprocessor = new ZipPreprocessor(
                    async (path, input) =>
                    {
                        try
                        {
                            var preprocessor = KubeHelper.CreateKubeValuePreprocessor(new StreamReader(input));
                            var output       = new MemoryStream();

                            foreach (var line in preprocessor.Lines())
                            {
                                output.Write(Encoding.UTF8.GetBytes(line));
                                output.WriteByte(NeonHelper.LF);    // Using Linux line endings
                            }

                            return await Task.FromResult(output);
                        }
                        catch (KeyNotFoundException e)
                        {
                            throw new KeyNotFoundException($"{e.Message} file: [{path}]");
                        }
                    });

                await helmFolder.ZipAsync(ms, searchOptions: SearchOption.AllDirectories, zipOptions: StaticZipOptions.LinuxLineEndings, preprocessor: preprocessor);

                ms.Seek(0, SeekOrigin.Begin);
                node.Upload(LinuxPath.Combine(KubeNodeFolder.Helm, "charts.zip"), ms, permissions: "660");
            }
        }
    }
}
