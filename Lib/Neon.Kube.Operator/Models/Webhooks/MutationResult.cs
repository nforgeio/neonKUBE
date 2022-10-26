//-----------------------------------------------------------------------------
// FILE:	    MutationResult.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Text;
using System.Threading.Tasks;

using k8s;
using Microsoft.AspNetCore.Http;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Represents the result of a mutating webhook.
    /// </summary>
    public sealed class MutationResult : AdmissionResult
    {
        /// <summary>
        /// The modified 
        /// </summary>
        public object ModifiedObject { get; init; }

        /// <summary>
        /// Utility method that creates a return value that indicates that no changes must be applied.
        /// </summary>
        /// <returns>A <see cref="MutationResult"/> with no changes.</returns>
        public static MutationResult NoChanges()
        {
            return new MutationResult();
        }

        /// <summary>
        /// Utility method that creates a return value that indicates that changes were made
        /// to the object that must be patched.
        /// This creates a json patch (<a href="http://jsonpatch.com/">jsonpatch.com</a>)
        /// that describes the diff from the original object to the modified object.
        /// </summary>
        /// <param name="modifiedEntity">The modified object.</param>
        /// <param name="warnings">
        /// An optional list of warnings/messages given back to the user.
        /// This could contain a reason why an object was mutated.
        /// </param>
        /// <returns>A <see cref="MutationResult"/> with a modified object.</returns>
        public static MutationResult Modified(object modifiedEntity, params string[] warnings) 
        {
            return new MutationResult()
            {
                ModifiedObject = modifiedEntity,
                Warnings = warnings
            };
        }

        internal static MutationResult Fail(int statusCode, string statusMessage)
        {
            return new MutationResult()
            {
                Valid = false,
                StatusCode = statusCode,
                StatusMessage = statusMessage
            };
        }
    }
}