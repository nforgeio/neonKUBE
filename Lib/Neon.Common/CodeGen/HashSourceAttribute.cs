//-----------------------------------------------------------------------------
// FILE:	    HashSourceAttribute.cs
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
    /// Used to tag data model properties that should be included in the
    /// <see cref="Object.GetHashCode()"/> computation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class HashSourceAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public HashSourceAttribute()
        {
        }
    }
}
