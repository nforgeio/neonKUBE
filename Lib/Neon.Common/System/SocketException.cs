//-----------------------------------------------------------------------------
// FILE:	    SocketException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

// $todo(jeff.lill): Probably can delete this file at some point.

#if FALSE

// $todo(jeff.lill):
//
// The PCL profile doesn't include SocketException but we're using this to
// detect and retry after transient network errors.  I'm going to define
// stub types here but I'll need to come back and actually figure out how
// to map the transient exceptions actually thrown by the platform HTTP
// classes into these exceptions.
//
// I hope we can remove this when we can upgrade to .NET Standard.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Net.Sockets
{

    /// <summary>
    /// The possible <see cref="SocketException"/> error codes.
    /// </summary>
    public enum SocketError
    {
        #pragma warning disable 1591 // Disable code comment warnings

        SocketError = -1,
        Success = 0,
        OperationAborted = 995,
        IOPending = 997,
        Interrupted = 10004,
        AccessDenied = 10013,
        Fault = 10014,
        InvalidArgument = 10022,
        TooManyOpenSockets = 10024,
        WouldBlock = 10035,
        InProgress = 10036,
        AlreadyInProgress = 10037,
        NotSocket = 10038,
        DestinationAddressRequired = 10039,
        MessageSize = 10040,
        ProtocolType = 10041,
        ProtocolOption = 10042,
        ProtocolNotSupported = 10043,
        SocketNotSupported = 10044,
        OperationNotSupported = 10045,
        ProtocolFamilyNotSupported = 10046,
        AddressFamilyNotSupported = 10047,
        AddressAlreadyInUse = 10048,
        AddressNotAvailable = 10049,
        NetworkDown = 10050,
        NetworkUnreachable = 10051,
        NetworkReset = 10052,
        ConnectionAborted = 10053,
        ConnectionReset = 10054,
        NoBufferSpaceAvailable = 10055,
        IsConnected = 10056,
        NotConnected = 10057,
        Shutdown = 10058,
        TimedOut = 10060,
        ConnectionRefused = 10061,
        HostDown = 10064,
        HostUnreachable = 10065,
        ProcessLimit = 10067,
        SystemNotReady = 10091,
        VersionNotSupported = 10092,
        NotInitialized = 10093,
        Disconnecting = 10101,
        TypeNotFound = 10109,
        HostNotFound = 11001,
        TryAgain = 11002,
        NoRecovery = 11003,
        NoData = 11004

        #pragma warning restore 1591
    }

    /// <summary>
    /// Describes a socket level error.
    /// </summary>
    public class SocketException : Exception
    {
        /// <summary>
        /// Constructs an exception with a non-specific error code.
        /// </summary>
        public SocketException()
        {
            SocketErrorCode = SocketError.SocketError;
        }

        /// <summary>
        /// Constructs am exception with a specific error code.
        /// </summary>
        /// <param name="errorCode"></param>
        public SocketException(SocketError errorCode)
        {
            SocketErrorCode = errorCode;
        }

        /// <summary>
        /// Returns the exception message.
        /// </summary>
        public override string Message
        {
            get { return SocketErrorCode.ToString(); }
        }

        /// <summary>
        /// Returns the error code.
        /// </summary>
        public SocketError SocketErrorCode { get; }
    }
}

#endif