//-----------------------------------------------------------------------------
// FILE:	    GitHubPackageVisibility.cs
// CONTRIBUTOR: Marcus Bowyer
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
    /// Enumerates the supported GitHub package visibility types.
    /// </summary>
    public static class GitHubPackageVisibility
    {
        /// <summary>
        /// All packages.
        /// </summary>
        public const string All = "all";

        /// <summary>
        /// Public packages.
        /// </summary>
        public const string Public = "public";

        /// <summary>
        /// Private packages.
        /// </summary>
        public const string Private = "private";

        /// <summary>
        /// Internal packages.
        /// </summary>
        public const string Internal = "internal";
    }
}
