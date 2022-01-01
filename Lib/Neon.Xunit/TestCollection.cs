//-----------------------------------------------------------------------------
// FILE:	    TestCollection.cs
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Windows;
using Neon.Xunit;

using Xunit;

namespace Neon.Xunit
{
    /// <summary>
    /// Enumerates the neonLIBRARY related xUnit test collections.
    /// </summary>
    public static class TestCollection
    {
        /// <summary>
        /// Identifies the test collection where tests <b>are never</b> executed in parallel.
        /// </summary>
        public const string NonParallel = "Non-parallel";
    }
}
