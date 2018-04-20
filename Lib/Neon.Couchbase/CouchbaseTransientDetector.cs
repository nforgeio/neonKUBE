//-----------------------------------------------------------------------------
// FILE:	    CouchbaseTransientDetector.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Diagnostics.Contracts;

using Couchbase;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.IO;

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
            // $todo(jeff.lill):
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

            if (cbException == null)
            {
                return false;
            }

            if (cbException.Status != ResponseStatus.KeyExists)
            {
                return false;
            }

            // $hack(jeff.lill):
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
