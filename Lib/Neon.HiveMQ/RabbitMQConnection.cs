//-----------------------------------------------------------------------------
// FILE:	    RabbitMQConnection.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

using RabbitMQ;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using Neon.Common;

#pragma warning disable 0618    // Allow calls to wrapped obsolete members.
#pragma warning disable 0067    // Disable warning "unused" warnings for the events.

namespace Neon.HiveMQ
{
    /// <summary>
    /// Wraps RabbitMQ <see cref="IConnection"/> instances to provide additional functionality.
    /// </summary>
    /// <remarks>
    /// Currently this class simply augments <see cref="Dispose"/> so that it also ensures that
    /// the connection is closed (it's a bit weird that the base RabbitMQ class doesn't do this).
    /// </remarks>
    public class RabbitMQConnection : IConnection
    {
        private object          syncLock = new object();
        private IConnection     connection;
        private bool            isDisposed;
        private bool            isClosed;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="connection">The underlying connection.</param>
        public RabbitMQConnection(IConnection connection)
        {
            Covenant.Requires<ArgumentNullException>(connection != null);

            this.connection = connection;
            this.isDisposed = false;
            this.isClosed   = false;
        }

        /// <inheritdoc/>
        public bool AutoClose
        {
            get { return connection.AutoClose; }
            set { connection.AutoClose = value; }
        }

        /// <inheritdoc/>
        public ushort ChannelMax => connection.ChannelMax;

        /// <inheritdoc/>
        public IDictionary<string, object> ClientProperties => connection.ClientProperties;

        /// <inheritdoc/>
        public ShutdownEventArgs CloseReason => connection.CloseReason;

        /// <inheritdoc/>
        public AmqpTcpEndpoint Endpoint => connection.Endpoint;

        /// <inheritdoc/>
        public uint FrameMax => connection.FrameMax;

        /// <inheritdoc/>
        public ushort Heartbeat => connection.Heartbeat;

        /// <inheritdoc/>
        public bool IsOpen => connection.IsOpen;

        /// <inheritdoc/>
        public AmqpTcpEndpoint[] KnownHosts => connection.KnownHosts;

        /// <inheritdoc/>
        public IProtocol Protocol => connection.Protocol;

        /// <inheritdoc/>
        public IDictionary<string, object> ServerProperties => connection.ServerProperties;

        /// <inheritdoc/>
        public IList<ShutdownReportEntry> ShutdownReport => connection.ShutdownReport;

        /// <inheritdoc/>
        public string ClientProvidedName => connection.ClientProvidedName;

        /// <inheritdoc/>
        public ConsumerWorkService ConsumerWorkService => connection.ConsumerWorkService;

        /// <inheritdoc/>
        public int LocalPort => connection.LocalPort;

        /// <inheritdoc/>
        public int RemotePort => connection.RemotePort;

        /// <inheritdoc/>
        public event EventHandler<CallbackExceptionEventArgs> CallbackException
        {
            add { connection.CallbackException += value; }
            remove { connection.CallbackException -= value; }
        }

        /// <inheritdoc/>
        public event EventHandler<EventArgs> RecoverySucceeded
        {
            add { connection.RecoverySucceeded += value; }
            remove { connection.RecoverySucceeded -= value; }
        }

        /// <inheritdoc/>
        public event EventHandler<ConnectionRecoveryErrorEventArgs> ConnectionRecoveryError
        {
            add { connection.ConnectionRecoveryError += value; }
            remove { connection.ConnectionRecoveryError -= value; }
        }

        /// <inheritdoc/>
        public event EventHandler<ConnectionBlockedEventArgs> ConnectionBlocked
        {
            add { connection.ConnectionBlocked += value; }
            remove { connection.ConnectionBlocked -= value; }
        }

        /// <inheritdoc/>
        public event EventHandler<ShutdownEventArgs> ConnectionShutdown
        {
            add { connection.ConnectionShutdown += value; }
            remove { connection.ConnectionShutdown -= value; }
        }

        /// <inheritdoc/>
        public event EventHandler<EventArgs> ConnectionUnblocked
        {
            add { connection.ConnectionUnblocked += value; }
            remove { connection.ConnectionUnblocked -= value; }
        }

        /// <inheritdoc/>
        public void Abort()
        {
            connection.Abort();
        }

        /// <inheritdoc/>
        public void Abort(ushort reasonCode, string reasonText)
        {
            connection.Abort(reasonCode, reasonText);
        }

        /// <inheritdoc/>
        public void Abort(int timeout)
        {
            connection.Abort(timeout);
        }

        /// <inheritdoc/>
        public void Abort(ushort reasonCode, string reasonText, int timeout)
        {
            connection.Abort(reasonCode, reasonText, timeout);
        }

        /// <inheritdoc/>
        public void Close()
        {
            lock (syncLock)
            {
                if (!isClosed)
                {
                    connection.Close();
                    isClosed = true;
                }
            }
        }

        /// <inheritdoc/>
        public void Close(ushort reasonCode, string reasonText)
        {
            lock (syncLock)
            {
                if (!isClosed)
                {
                    connection.Close(reasonCode, reasonText);
                    isClosed = true;
                }
            }
        }

        /// <inheritdoc/>
        public void Close(int timeout)
        {
            lock (syncLock)
            {
                if (!isClosed)
                {
                    connection.Close(timeout);
                    isClosed = true;
                }
            }
        }

        /// <inheritdoc/>
        public void Close(ushort reasonCode, string reasonText, int timeout)
        {
            lock (syncLock)
            {
                if (!isClosed)
                {
                    connection.Close(reasonCode, reasonText, timeout);
                    isClosed = true;
                }
            }
        }

        /// <inheritdoc/>
        public IModel CreateModel()
        {
            return new RabbitMQChannel(connection.CreateModel());
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            lock (syncLock)
            {
                if (!isDisposed)
                {
                    Close();
                    connection.Dispose();
                    isDisposed = true;
                }
            }
        }

        /// <inheritdoc/>
        public void HandleConnectionBlocked(string reason)
        {
            connection.HandleConnectionBlocked(reason);
        }

        /// <inheritdoc/>
        public void HandleConnectionUnblocked()
        {
            connection.HandleConnectionUnblocked();
        }
    }
}
