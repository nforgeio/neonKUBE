// -----------------------------------------------------------------------------
// FILE:	    INeonDesktopApi.cs
// CONTRIBUTOR: NEONFORGE Team
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License").
// You may not use this file except in compliance with the License.
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.ModelGen;

namespace Neon.Kube.Models.Headend
{
    /// <summary>
    /// Defines the NeonDESKTOP cluster management REST APIs.
    /// </summary>
    [Target("all")]
    [Target("headend")]
    [ServiceModel(name: "Headend", group: "NeonDesktop")]
    [Route("")]
    [ApiVersion("2023-04-06")]
    public interface INeonDesktopApi
    {
        /// <summary>
        /// <para>
        /// Returns the current NeonDESKTOP Certificate.
        /// </para>
        /// <note>
        /// All NeonDESKTOP clusters share the same certificate.  We don't consider this to
        /// be a security problem because Desktop Clusters are not reachable from outside the
        /// host machine and also becase desktop cluster should never be used for production.
        /// </note>
        /// </summary>
        /// <returns>A dictionary with the certificate <b>tls.crt</b> and private <b>tls.key</b>.</returns>
        [HttpGet]
        [Route("neondesktop/certificate")]
        IDictionary<string, byte[]> GetNeonDesktopCertificateAsync();
    }
}
