//-----------------------------------------------------------------------------
// FILE:	    PassAs.cs
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

using Newtonsoft.Json;

using Neon.Common;

namespace Neon.CodeGen
{
    /// <summary>
    /// Identifies how a generated service client will pass a parameter to 
    /// an ASP.NET service.
    /// </summary>
    public enum PassAs
    {
        /// <summary>
        /// Passes the parameter is passed as a URI query parameter.
        /// </summary>
        Query,

        /// <summary>
        /// Passes the parameter within the URI route template. 
        /// </summary>
        Route,

        /// <summary>
        /// Passes the parameter as an HTTP header.
        /// </summary>
        Header,

        /// <summary>
        /// Passes the parameter as the HTTP request body. 
        /// </summary>
        Body,
    }
}
