//-----------------------------------------------------------------------------
// FILE:	    CouchbaseTransientDetector.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Diagnostics.Contracts;

using Couchbase;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.IO;

using Neon.Cluster;
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
            Console.WriteLine(NeonHelper.ExceptionError(e));

            // $todo(jeff.lill):
            //
            // I'm making a guess at these for now.  I'm not sure if this is the
            // complete list of potentially transient exceptions and Couchbase 
            // I'm not sure if they all should acutually be considered as transient.
            // We need to come back and do a deeper analysis.

            return e.TriggeredBy<TransientException>()
                || e.TriggeredBy<ServerUnavailableException>()
                || e.TriggeredBy<TemporaryLockFailureException>()
                || e.TriggeredBy<BufferUnavailableException>()
                || e.TriggeredBy<ConnectionUnavailableException>()
                || e.TriggeredBy<RemoteHostClosedException>()
                || e.TriggeredBy<RemoteHostTimeoutException>()
                || e.TriggeredBy<SendTimeoutExpiredException>()
                || e.TriggeredBy<TransportFailureException>();
        }
    }
}
