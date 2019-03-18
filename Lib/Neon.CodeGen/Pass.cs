//-----------------------------------------------------------------------------
// FILE:	    Pass.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Runtime.Serialization;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

using Newtonsoft.Json;

using Neon.Serialization;

namespace Neon.CodeGen
{
    /// <summary>
    /// Specifies how a service method parameter is passed within a REST
    /// service request.
    /// </summary>
    internal enum Pass
    {
        /// <summary>
        /// Passes the parameter as a URI query parameter.  This is the
        /// default mechanism.
        /// </summary>
        InQuery = 0,

        /// <summary>
        /// Passes the parameter within the URI route template. 
        /// </summary>
        InRoute,

        /// <summary>
        /// Passes the parameter as an HTTP header.
        /// </summary>
        AsHeader,

        /// <summary>
        /// Passes the parameter as the HTTP request body. 
        /// </summary>
        AsBody
    }
}
