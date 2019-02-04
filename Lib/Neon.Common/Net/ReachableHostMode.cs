//-----------------------------------------------------------------------------
// FILE:	    ReachableHostMode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Net
{
    /// <summary>
    /// Enumerates how <see cref="NetHelper.GetReachableHost(IEnumerable{string}, ReachableHostMode)"/> should
    /// behave when no there are no healthy hosts.
    /// </summary>
    public enum ReachableHostMode
    {
        /// <summary>
        /// Throw an exception when no hosts respond.
        /// </summary>
        Throw,

        /// <summary>
        /// Return the first host when no hosts respond.
        /// </summary>
        ReturnFirst,

        /// <summary>
        /// Return <c>null</c> when no hosts respond.
        /// </summary>
        ReturnNull
    }
}
