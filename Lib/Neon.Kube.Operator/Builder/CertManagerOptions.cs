//-----------------------------------------------------------------------------
// FILE:	    CertManagerOptions.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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

using k8s.Models;
using Neon.Kube.Resources.CertManager;
using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Neon.Kube.Operator.Builder
{
    /// <summary>
    /// Cert manager related options.
    /// </summary>
    public class CertManagerOptions
    {
        /// <summary>
        /// How long the cert should be valid for.
        /// </summary>
        public TimeSpan CertificateDuration { get; set; }

        /// <summary>
        /// The Issuer that should issue the cert.
        /// </summary>
        public IssuerRef IssuerRef { get; set; }
    }
}
