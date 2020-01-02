//-----------------------------------------------------------------------------
// FILE:	    CouchbaseTransientDetector.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Linq;

using Couchbase;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.IO;
using Couchbase.N1QL;

using Neon.Common;
using Neon.Data;
using Neon.Retry;

namespace Couchbase
{
    /// <summary>
    /// Determines whether an exception thrown by a Couchbase client operation
    /// indicates a potentially transistent problem.
    /// </summary>
    public static class CouchbaseTransientDetector
    {
        /// <summary>
        /// Returns <c>true</c> if the exception passed should be considered to be a
        /// potentially transient Couchbase error.
        /// </summary>
        /// <param name="e">The exception being tested.</param>
        /// <returns><c>true</c> if the error was potentially transient.</returns>
        public static bool IsTransient(Exception e)
        {
            // $todo(jefflill):
            //
            // I'm making a guess at these for now.  I'm not sure if this is the
            // complete list of potentially transient exceptions and Couchbase 
            // I'm not sure if they all should acutually be considered as transient.
            // We need to come back and do a deeper analysis.

            var cbException = e.Find<CouchbaseKeyValueResponseException>();

            if (cbException != null)
            {
                switch (cbException.Status)
                {
                    case ResponseStatus.TemporaryFailure:

                        return true;
                }
            }

            var cbQueryResponseException = e.Find<CouchbaseQueryResponseException>();

            if (cbQueryResponseException != null)
            {
                // Sometimes we see [CouchbaseQueryResponseException] with error
                // when we drop and then immediately recreate an index.  These
                // have an error with:
                // 
                //      CODE:    5000
                //      MESSAGE: GSI CreatePrimaryIndex() - cause: Encounter errors during create index.  Error=Indexer In Recovery
                //
                // We're going to consider these to be transient.

                // $heck(jefflill):
                //
                // Note that CODE=5000 looks like a generic code, so we need to key off of the
                // message text too.  This will be fragile.

                if (cbQueryResponseException != null && cbQueryResponseException.Errors.Count == 1)
                {
                    var error = cbQueryResponseException.Errors.First();

                    if (error.Code == 5000 && error.Message.IndexOf("Error=Indexer In Recovery", StringComparison.InvariantCultureIgnoreCase) != -1)
                    {
                        return true;
                    }
                }
            }

            return e.Contains<TransientException>()
                || e.Contains<ServerUnavailableException>()
                || e.Contains<TemporaryLockFailureException>()
                || e.Contains<BufferUnavailableException>()
                || e.Contains<ConnectionUnavailableException>()
                || e.Contains<RemoteHostClosedException>()
                || e.Contains<RemoteHostTimeoutException>()
                || e.Contains<SendTimeoutExpiredException>()
                || e.Contains<TransportFailureException>();
        }

        /// <summary>
        /// Returns <c>true</c> if the exception passed should be considered to be a
        /// CAS (check-and-set) error that could be retried in application code.
        /// </summary>
        /// <param name="e">The exception being tested.</param>
        /// <returns><c>true</c> if the error was potentially transient.</returns>
        public static bool IsCasTransient(Exception e)
        {
            if (IsTransient(e))
            {
                return true;
            }

            var cbException = e.Find<CouchbaseKeyValueResponseException>();

            if (cbException == null || cbException.Status != ResponseStatus.KeyExists)
            {
                return false;
            }

            // $hack(jefflill):
            //
            // I'm not entirely convinced that the [ResponseStatus.KeyExists] status
            // code by itself indicates that this was due to a CAS failure.  It seems
            // that we'd also see this code for an insert operation when the key already
            // exists.
            //
            // I'm going to examine the inner exception's message to be sure.  This
            // is going to be somewhat fragile.

            if (e.InnerException == null)
            {
                return false;
            }

            return e.InnerException.Message.Contains("CAS");
        }
    }
}
