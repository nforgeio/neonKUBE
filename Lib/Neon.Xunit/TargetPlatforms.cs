//-----------------------------------------------------------------------------
// FILE:        TargetPlatforms.cs
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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Xunit;

namespace Neon.Xunit
{
    /// <summary>
    /// Enumerates the platforms that can be targeted by unit tests tagged
    /// with <see cref="PlatformFactAttribute"/>.  Note that these flags may
    /// be bitwise-ORed together.
    /// </summary>
    [Flags]
    public enum TargetPlatforms : uint
    {
        /// <summary>
        /// Target all platforms.
        /// </summary>
        All = 0xffffffff,

        /// <summary>
        /// Target Windows.
        /// </summary>
        Windows = 0x00000001,

        /// <summary>
        /// Target Linux.
        /// </summary>
        Linux = 0x00000002,

        /// <summary>
        /// Target OS/X.
        /// </summary>
        Osx = 0x00000004
    }
}
