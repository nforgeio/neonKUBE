//-----------------------------------------------------------------------------
// FILE:	    GrpcError.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Net;

using ProtoBuf.Grpc;

namespace Neon.Kube.GrpcProto
{
    /// <summary>
    /// Holds information about an exception caught by the neon desktop service.
    /// </summary>
    [DataContract]
    public class GrpcError
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public GrpcError()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="e">The exception being wrapped.</param>
        public GrpcError(Exception e)
        {
            this.ExceptionType = e.GetType().FullName ?? "unknown";
            this.Message       = e.Message;
            this.StackTrace    = e.StackTrace?.ToString();
        }

        /// <summary>
        /// The fully qualified type name of the exception.
        /// </summary>
        [DataMember(Order = 1)]
        public string? ExceptionType { get; set; }

        /// <summary>
        /// The exception message.
        /// </summary>
        [DataMember(Order = 2)]
        public string? Message { get; set; }

        /// <summary>
        /// The stack trace where the exception was thrown or <c>null</c> when not available.
        /// </summary>
        [DataMember(Order = 3)]
        public string? StackTrace { get; set; }
    }

    /// <summary>
    /// <see cref="GrpcError"/> extensions.
    /// </summary>
    public static class GrpcErrorExtensions
    {
        /// <summary>
        /// Throws a <see cref="GrpcServiceException"/> when the <see cref="GrpcError"/> is not <c>null</c>.
        /// </summary>
        /// <param name="error">The error.</param>
        public static void EnsureSuccess(this GrpcError error)
        {
            if (error != null)
            {
                throw new GrpcServiceException($"[{error.ExceptionType}]: {error.Message}");
            }
        }
    }
}
