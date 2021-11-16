//-----------------------------------------------------------------------------
// FILE:	    GitHubPackageVersion.cs
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
using System.Diagnostics.Contracts;
using System.IO;

using Neon.Common;

namespace Neon.Deployment
{
    /// <summary>
    /// Describes a package version.
    /// </summary>
    public class GitHubPackageVersion
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public GitHubPackageVersion()
        {
        }

        /// <summary>
        /// The package version ID.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The package version name.  This will be the SHA256 for container images.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The assigned tags for container images.
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();
    }
}
