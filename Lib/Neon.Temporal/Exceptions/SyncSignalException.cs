//-----------------------------------------------------------------------------
// FILE:	    SyncSignalException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;

using Neon.Temporal.Internal;

namespace Neon.Temporal
{
    /// <summary>
    /// Thrown when a synchronous signal sent to a workflow fails.
    /// </summary>
    public class SyncSignalException : Exception
    {
        //---------------------------------------------------------------------
        // Static members

        private static char[] colon = new char[] { ':' };

        /// <summary>
        /// Converts an exception into a string suitable for using to construct
        /// a <see cref="SyncSignalException"/>.
        /// </summary>
        /// <param name="e">The exception.</param>
        /// <returns>The formatted error string.</returns>
        internal static string GetError(Exception e)
        {
            Covenant.Requires<ArgumentNullException>(e != null, nameof(e));

            return $"{e.GetType().FullName}:{e.Message}";
        }

        /// <summary>
        /// Converts an exception type and message into a string suitable for using to construct
        /// a <see cref="SyncSignalException"/>.
        /// </summary>
        /// <typeparam name="TException">The exception type.</typeparam>
        /// <param name="message">The error message.</param>
        /// <returns>The formatted error string.</returns>
        internal static string GetError<TException>(string message)
            where TException : Exception
        {
            return $"{typeof(TException).FullName}:{message}";
        }

        /// <summary>
        /// Extracts the message from the error string.
        /// </summary>
        /// <param name="error">The error string.</param>
        /// <returns>The message.</returns>
        private static string GetMessage(string error)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(error), nameof(error));

            var fields = error.Split(colon, 2);

            if (fields.Length != 2)
            {
                throw new FormatException($"Invalid error string: {error}.");
            }

            return fields[1];
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="error">The error information as formatted by <see cref="GetError(Exception)"/>.</param>
        internal SyncSignalException(string error)
            : base(GetMessage(error))
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(error), nameof(error));

            var fields = error.Split(colon, 2);

            if (fields.Length != 2)
            {
                throw new FormatException($"Invalid error string: {error}.");
            }

            ExceptionName = fields[0];
        }

        /// <summary>
        /// The fully qualified name of the exception thrown by the target signal method.
        /// </summary>
        public string ExceptionName { get; private set; }
    }
}
