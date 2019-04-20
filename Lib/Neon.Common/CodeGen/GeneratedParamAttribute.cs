//-----------------------------------------------------------------------------
// FILE:	    GeneratedParamAttribute.cs
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
    /// Used to tag generated service client method parameters with additional
    /// metadata that will be used when validatating the a generated service
    /// client actually matches an ASP.NET service implementation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class GeneratedParamAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="passAs">Indicates how the client passes the tagged parameter to the service.</param>
        public GeneratedParamAttribute(PassAs passAs)
        {
            this.PassAs = passAs;
        }

        /// <summary>
        /// Indicates how the client passes the tagged parameter to the service.
        /// </summary>
        public PassAs PassAs { get; private set; }

        /// <summary>
        /// Parameter name as it appears on the wire for parameters passed
        /// as a query, header, or route.
        /// </summary>
        public string Name { get; set; }
    }
}
