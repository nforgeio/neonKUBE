//-----------------------------------------------------------------------------
// FILE:	    MethodParameter.cs
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
using System.Linq;
using System.Reflection;
using System.Text;

namespace Neon.CodeGen
{
    /// <summary>
    /// Holds information about a service model method parameter.
    /// </summary>
    internal class MethodParameter
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="parameterInfo">The .NET parameter information.</param>
        public MethodParameter(ParameterInfo parameterInfo)
        {
            Covenant.Requires<ArgumentNullException>(parameterInfo != null);

            this.ParameterInfo = parameterInfo;
        }

        /// <summary>
        /// Returns the low-level .NET parameter information.
        /// </summary>
        public ParameterInfo ParameterInfo { get; private set; }

        /// <summary>
        /// Returns the parameter name.
        /// </summary>
        public string Name => ParameterInfo.Name;

        /// <summary>
        /// Specifies how the parameter shoud be passed to the service endpoint.
        /// </summary>
        public Pass Pass { get; set; } = Pass.InQuery;

        /// <summary>
        /// The parameter or HTTP header name to use when passing the parameter as <see cref="Pass.InQuery"/>
        /// <see cref="Pass.InRoute"/>, or <see cref="Pass.AsHeader"/>.  This is ignored for <see cref="Pass.AsBody"/>.
        /// </summary>
        public string SeralizedName { get; set; }
    }
}
