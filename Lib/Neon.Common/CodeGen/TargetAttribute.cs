//-----------------------------------------------------------------------------
// FILE:	    TargetAttribute.cs
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
using System.Diagnostics.Contracts;
using System.Text;

namespace Neon.CodeGen
{
    /// <summary>
    /// Used to group data models and service definitions so that the
    /// class included in the generated code can filtered.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Enum, AllowMultiple = true)]
    public class TargetAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The target name.</param>
        public TargetAttribute(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            this.Name = name;
        }

        /// <summary>
        /// Returns the group name.
        /// </summary>
        public string Name { get; private set; }
    }
}
