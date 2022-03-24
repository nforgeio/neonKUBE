//-----------------------------------------------------------------------------
// FILE:	    XenClient.Template.cs
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.XenServer
{
    public partial class XenClient
    {
        /// <summary>
        /// Implements the <see cref="XenClient"/> virtual machine template operations.
        /// </summary>
        public class TemplateOperations
        {
            private XenClient client;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="client">The XenServer client instance.</param>
            internal TemplateOperations(XenClient client)
            {
                this.client = client;
            }

            /// <summary>
            /// Lists the XenServer virtual machine templates.
            /// </summary>
            /// <returns>The list of templates.</returns>
            /// <exception cref="XenException">Thrown if the operation failed.</exception>
            public List<XenTemplate> List()
            {
                var response  = client.SafeInvokeItems("template-list", "params=all");
                var templates = new List<XenTemplate>();

                foreach (var result in response.Items)
                {
                    templates.Add(new XenTemplate(result));
                }

                return templates;
            }

            /// <summary>
            /// Finds a specific virtual machine template by name or unique ID.
            /// </summary>
            /// <param name="name">Specifies the target name.</param>
            /// <param name="uuid">Specifies the target unique ID.</param>
            /// <returns>The named item or <c>null</c> if it doesn't exist.</returns>
            /// <exception cref="XenException">Thrown if the operation failed.</exception>
            /// <remarks>
            /// <note>
            /// One of <paramref name="name"/> or <paramref name="uuid"/> must be specified.
            /// </note>
            /// </remarks>
            public XenTemplate Find(string name = null, string uuid = null)
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(uuid));

                if (!string.IsNullOrWhiteSpace(name))
                {
                    return List().FirstOrDefault(item => item.NameLabel == name);
                }
                else if (!string.IsNullOrWhiteSpace(uuid))
                {
                    return List().FirstOrDefault(item => item.Uuid == uuid);
                }
                else
                {
                    throw new ArgumentException($"One of [{nameof(name)}] or [{nameof(uuid)}] must be specified.");
                }
            }

            /// <summary>
            /// Installs from an XVA or OVA virtual machine template file on the local workstation to the 
            /// remote XenServer host an XVA or OVA virtual machine template, optionally renaming it.
            /// </summary>
            /// <param name="path">The path to the template file on the local workstation.</param>
            /// <param name="name">The optional template name.</param>
            /// <param name="repositoryNameOrUuid">
            /// Optionally specifies the target storage repository by name or UUID.  
            /// This defaults to <b>Local storage</b>.
            /// </param>
            /// <returns>The installed template.</returns>
            /// <exception cref="XenException">Thrown if the operation failed.</exception>
            public XenTemplate ImportVmTemplate(string path, string name = null, string repositoryNameOrUuid = "Local storage")
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path), nameof(path));
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(repositoryNameOrUuid), nameof(repositoryNameOrUuid));

                var sr = client.Repository.GetTargetStorageRepository(repositoryNameOrUuid);

                var response = client.SafeInvoke("vm-import", $"filename={path}", $"sr-uuid={sr.Uuid}");
                var uuid     = response.OutputText.Trim();
                var template = Find(uuid: uuid);

                if (!string.IsNullOrEmpty(name))
                {
                    template = Rename(template, name);
                }

                return template;
            }

            /// <summary>
            /// Renames a virtual machine template.
            /// </summary>
            /// <param name="template">The target template.</param>
            /// <param name="newName">The new template name.</param>
            /// <returns>The modified template.</returns>
            /// <exception cref="XenException">Thrown if the operation failed.</exception>
            public XenTemplate Rename(XenTemplate template, string newName)
            {
                Covenant.Requires<ArgumentNullException>(template != null, nameof(template));
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(newName), nameof(newName));

                client.SafeInvoke("template-param-set", $"uuid={template.Uuid}", $"name-label={newName}");

                return Find(uuid: template.Uuid);
            }

            /// <summary>
            /// Removes a virtual machine template.
            /// </summary>
            /// <param name="template">The target template.</param>
            /// <exception cref="XenException">Thrown if the operation failed.</exception>
            public void Destroy(XenTemplate template)
            {
                Covenant.Requires<ArgumentNullException>(template != null, nameof(template));

                client.SafeInvoke("vm-destroy", $"uuid={template.Uuid}");
            }
        }
    }
}
