// -----------------------------------------------------------------------------
// FILE:	    HelmMode.cs
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

namespace Neon.Kube.SSH
{
    /// <summary>
    /// Specifies the mode used for <see cref="NodeSshProxy{TMetadata}.InstallHelmChartAsync(Setup.ISetupController, string, string, string, string, string, Dictionary{string, object}, string, TimeSpan, HelmMode)"/>,
    /// controlling whether the chart is installed, performs a dry-run, or just generates manifest without
    /// validating Kubernetes objects.
    /// </summary>
    public enum HelmMode
    {
        /// <summary>
        /// Specifies that the Helm chart is to be installed.  This is the
        /// default behavior.
        /// </summary>
        Install = 0,

        /// <summary>
        /// Specifies that the Helm chart is to be validated by Kubernetes
        /// and a manifest file be created, but that the chart will not be
        /// installed.
        /// </summary>
        DryRun,

        /// <summary>
        /// Specifies that the Helm chart be processed into a manifest
        /// file without any Kubernetes validation.  This is handy for
        /// debugging values and templates.
        /// </summary>
        Template
    }
}