//-----------------------------------------------------------------------------
// FILE:	    INormalizable.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Runtime.Serialization;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Neon.Docker
{
    /// <summary>
    /// Describes types that implement the <see cref="Normalize()"/> method that
    /// recursively ensures that any <b>null</b> class or list related properties 
    /// are replaced with instances with default values or empty lists.
    /// </summary>
    internal interface INormalizable
    {
        /// <summary>
        /// Recursively ensures ensures that any <b>null</b> class or list
        /// related properties are replaced with instances with default 
        /// values or empty lists.
        /// </summary>
        void Normalize();
    }
}
