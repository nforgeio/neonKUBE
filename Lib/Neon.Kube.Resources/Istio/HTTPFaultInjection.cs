//-----------------------------------------------------------------------------
// FILE:	    HTTPFaultInjection.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

using k8s;
using k8s.Models;

using Newtonsoft.Json;

namespace Neon.Kube.Resources.Istio
{
    /// <summary>
    /// <para>
    /// HTTPFaultInjection can be used to specify one or more faults to inject while forwarding HTTP requests to the destination specified in a route. Fault specification is part of a VirtualService rule. Faults include aborting the Http request from downstream service, and/or delaying proxying of requests. A fault rule MUST HAVE delay or abort or both.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Note: Delay and abort faults are independent of one another, even if both are specified simultaneously.
    /// </remarks>
    public class HTTPFaultInjection : IValidate
    {
        /// <summary>
        /// Initializes a new instance of the HTTPFaultInjection class.
        /// </summary>
        public HTTPFaultInjection()
        {
        }

        /// <summary>
        /// <para>
        /// Delay requests before forwarding, emulating various failures such as network issues, overloaded upstream service, etc.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "delay", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Delay Delay { get; set; }

        /// <summary>
        /// <para>
        /// Abort Http request attempts and return error codes back to downstream service, giving the impression that the upstream service is faulty.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "abort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Abort Abort { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">Thrown if validation fails.</exception>
        public virtual void Validate()
        {
        }
    }
}
