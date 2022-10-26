//-----------------------------------------------------------------------------
// FILE:	    KubeContainerRegistry.cs
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
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Neon.Kube.BuildInfo;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Diagnostics;

namespace Neon.Kube
{
    /// <summary>
    /// Describes the location of the neonKUBE related container registries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// neonKUBE uses four container registries for testing and deployment purposes.  
    /// <see cref="MainProdRegistry"/> and <see cref="MainDevRegistry"/> hold the 
    /// container images that will be used for deploying clusters via prepositioning
    /// the images on the cluster VM images.  The images in these cluster will be
    /// tagged like <b>neonkube-0.1.0-alpha</b>, specifying the cluster version
    /// with <see cref="MainProdRegistry"/> holding the released production images
    /// and <see cref="MainDevRegistry"/> holding intermediate development images.
    /// </para>
    /// <para>
    /// The <see cref="BaseProdRegistry"/> and <see cref="BaseDevRegistry"/> registries
    /// are used the base and layer images used for the creating the main container
    /// images as well as for deploying clusters without prepositioning images (typically
    /// while working on setting up new cluster features).
    /// </para>
    /// <para>
    /// The <see cref="MainBranchRegistry"/> and <see cref="BaseBranchRegistry"/> properties
    /// return the corresponding registry to use based on the the git branch the code was
    /// built on.  These return the production registries for release branches whose names 
    /// start with <b>release-</b> (by convention) otherwise the development registry will
    /// be returned.
    /// </para>
    /// </remarks>
    public static class KubeContainerRegistry
    {
        /// <summary>
        /// Identifies the production neonFORGE container image registry.  This is a public
        /// registry that holds non-cluster setup related images.
        /// </summary>
        public const string MainProdRegistry = "ghcr.io/neonkube";

        /// <summary>
        /// Identifies the development neonFORGE container image registry.  This is a public
        /// registry that holds non-cluster setup related images during development between
        /// releases.
        /// </summary>
        public const string MainDevRegistry = "ghcr.io/neonkube-dev";

        /// <summary>
        /// Returns the appropriate public container neonFORGE registry to be used for the git 
        /// branch the assembly was built from.  This returns <see cref="MainProdRegistry"/> for
        /// release branches and <see cref="MainDevRegistry"/> for all other branches.
        /// </summary>
        public static string MainBranchRegistry => ThisAssembly.Git.Branch.StartsWith("release-", StringComparison.InvariantCultureIgnoreCase) ? MainProdRegistry : MainDevRegistry;

        /// <summary>
        /// Identifies the production neonFORGE container image registry.  This is a public
        /// registry that holds non-cluster setup related images.
        /// </summary>
        public const string BaseProdRegistry = "ghcr.io/neonkube-base";

        /// <summary>
        /// Identifies the development neonFORGE container image registry.  This is a public
        /// registry that holds non-cluster setup related images during development between
        /// releases.
        /// </summary>
        public const string BaseDevRegistry = "ghcr.io/neonkube-base-dev";

        /// <summary>
        /// Returns the appropriate public container neonFORGE registry to be used for the git 
        /// branch the assembly was built from.  This returns <see cref="MainProdRegistry"/> for
        /// release branches and <see cref="MainDevRegistry"/> for all other branches.
        /// </summary>
        public static string BaseBranchRegistry => ThisAssembly.Git.Branch.StartsWith("release-", StringComparison.InvariantCultureIgnoreCase) ? BaseProdRegistry : BaseDevRegistry;
    }
}
