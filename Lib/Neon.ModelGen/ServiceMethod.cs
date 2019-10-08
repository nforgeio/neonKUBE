//-----------------------------------------------------------------------------
// FILE:	    ServiceMethod.cs
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
using System.Reflection;
using System.Text;

namespace Neon.ModelGen
{
    /// <summary>
    /// Describes a service method.
    /// </summary>
    internal class ServiceMethod
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="serviceModel">The parent <see cref="ServiceModel"/>.</param>
        public ServiceMethod(ServiceModel serviceModel)
        {
            Covenant.Requires<ArgumentNullException>(serviceModel != null, nameof(serviceModel));

            this.ServiceModel = serviceModel;
            this.Parameters   = new List<MethodParameter>();
        }

        /// <summary>
        /// Returns the parent <see cref="ServiceModel"/>.
        /// </summary>
        public ServiceModel ServiceModel { get; private set; }

        /// <summary>
        /// Describes the low-level method name, parameters, and result.
        /// </summary>
        public MethodInfo MethodInfo { get; set; }

        /// <summary>
        /// Returns <c>true</c> if the method returns <c>void</c>.
        /// </summary>
        public bool IsVoid { get; set; }

        /// <summary>
        /// The method name to use when generating code for this method.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// <para>
        /// Specifies the route template for this method.
        /// This includes the service model's route as well.
        /// </para>
        /// <note>
        /// This does not include the service controller's route prefix.
        /// </note>
        /// </summary>
        public string RouteTemplate { get; set; }

        /// <summary>
        /// Specifies the HTTP method to use for invoking the method.
        /// </summary>
        public string HttpMethod { get; set; }

        /// <summary>
        /// Returns the list of method parameters.
        /// </summary>
        public List<MethodParameter> Parameters { get; private set; }
    }
}
