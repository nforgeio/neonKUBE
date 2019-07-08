//-----------------------------------------------------------------------------
// FILE:	    InternalProxyMessageAttribute.cs
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
using System.ComponentModel;

using Neon.Cadence;
using Neon.Common;

// $todo(jeff.lill)
//
// Performance could be improved by maintaining output stream and buffer pools
// rather than allocating these every time.

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// <b>INTERNAL USE ONLY:</b> Used to tag proxy message class implementations 
    /// and also associate the message class with the message type code.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    internal class InternalProxyMessageAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="type">Specifies the message type to be used when serializing the tagged message.</param>
        public InternalProxyMessageAttribute(InternalMessageTypes type)
        {
            this.Type = type;
        }

        /// <summary>
        /// Returns the associated message type code.
        /// </summary>
        public InternalMessageTypes Type { get; private set; }
    }
}
