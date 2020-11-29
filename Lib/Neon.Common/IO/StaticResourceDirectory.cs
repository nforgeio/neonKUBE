//-----------------------------------------------------------------------------
// FILE:        StaticResourceDirectory.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.IO
{
    /// <summary>
    /// Implements the <see cref="IStaticDirectory"/> abstraction over as virtual
    /// directory of embedded <see cref="Assembly"/> resources.
    /// </summary>
    internal class StaticResourceDirectory : StaticDirectoryBase
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="root">The root directory or <c>null</c> if this is the root.</param>
        /// <param name="parent">The parent directory or <c>null</c> for the root directory.</param>
        /// <param name="path">Specifies the logical Lunix-style path to directory.</param>
        internal StaticResourceDirectory(StaticResourceDirectory root, StaticResourceDirectory parent, string path)
            : base(root, parent, path)
        {
        }
    }
}
