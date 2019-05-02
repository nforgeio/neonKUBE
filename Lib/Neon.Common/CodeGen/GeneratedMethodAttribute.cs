//-----------------------------------------------------------------------------
// FILE:	    GeneratedMethodAttribute.cs
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
    /// Used to tag generated service client methods with additional
    /// metadata that will be used when validatating the a generated service
    /// client actually matches an ASP.NET service implementation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class GeneratedMethodAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public GeneratedMethodAttribute()
        {
        }

        /// <summary>
        /// The method name from the service model definition.
        /// </summary>
        public string DefinedAs { get; set; }

        /// <summary>
        /// The method result type.
        /// </summary>
        public Type Returns { get; set; }

        /// <summary>
        /// The route template.
        /// </summary>
        public string RouteTemplate { get; set; }

        /// <summary>
        /// The HTTP method for the endpoint.
        /// </summary>
        public string HttpMethod { get; set; }
    }
}
