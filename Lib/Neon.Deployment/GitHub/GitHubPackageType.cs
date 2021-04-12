//-----------------------------------------------------------------------------
// FILE:	    GitHubPackageType.cs
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
using System.Diagnostics.Contracts;
using System.IO;

using Neon.Common;

namespace Neon.Deployment
{
    /// <summary>
    /// Enumerates the supported GitHub package types.
    /// </summary>
    public static class GitHubPackageType
    {
        /// <summary>
        /// NPM package.
        /// </summary>
        public const string Npm = "npm";

        /// <summary>
        /// Maven package.
        /// </summary>
        public const string Maven = "maven";

        /// <summary>
        /// Ruby Gem.
        /// </summary>
        public const string RubyGems = "rubygems";

        /// <summary>
        /// Nuget package.
        /// </summary>
        public const string Nuget = "nuget";

        /// <summary>
        /// Docker package (use <see cref="Container"/> for packages with namespace <b>https://ghcr.io/owner/package-name</b>).
        /// </summary>
        public const string Docker = "docker";

        /// <summary>
        /// Container image.
        /// </summary>
        public const string Container = "container";
    }
}
