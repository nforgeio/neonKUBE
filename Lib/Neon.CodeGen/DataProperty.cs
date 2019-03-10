//-----------------------------------------------------------------------------
// FILE:	    DataProperty.cs
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
    /// Describes a <see cref="DataModel"/> property.
    /// </summary>
    public class DataProperty
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public DataProperty()
        {
        }

        /// <summary>
        /// True when this property is not to be serialized.
        /// </summary>
        public bool Ignore { get; set; }

        /// <summary>
        /// The property type.
        /// </summary>
        public Type Type { get; set; }

        /// <summary>
        /// The property name for generated code.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The property name to be used for serialization.
        /// </summary>
        public string SerializedName { get; set; }

        /// <summary>
        /// Controls the order for which this property will be serialized.
        /// </summary>
        public int Order { get; set; }
    }
}
