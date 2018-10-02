//-----------------------------------------------------------------------------
// FILE:	    RabbitMQChannel.cs
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
    /// Wraps RabbbitMQ <see cref="IModel"/> instances to provide additional functionality.
    /// </summary>
    /// <remarks>
    /// Currently this class simply augments <see cref="Dispose"/> so that it also ensures that
    /// the channel is closed (it's a bit weird that the base RabbitMQ class doesn't do this).
    /// </remarks>
    public class RabbitMQChannel : IModel
    {
        private object          syncLock = new object();
        private IModel          channel;
        private bool            isDisposed;
        private bool            isClosed;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="channel">The underlying channel.</param>
        public RabbitMQChannel(IModel channel)
        {
            Covenant.Requires<ArgumentNullException>(channel != null);

            this.channel    = channel;
            this.isDisposed = false;
            this.isClosed   = false;
        }

        /// <inheritdoc/>
        public int ChannelNumber => channel.ChannelNumber;

        /// <inheritdoc/>
        public ShutdownEventArgs CloseReason => channel.CloseReason;

        /// <inheritdoc/>
        public IBasicConsumer DefaultConsumer
        {
            get { return channel.DefaultConsumer; }
            set { channel.DefaultConsumer = value; }
        }

        /// <inheritdoc/>
        public bool IsClosed => channel.IsClosed;

        /// <inheritdoc/>
        public bool IsOpen => channel.IsOpen;

        /// <inheritdoc/>
        public ulong NextPublishSeqNo => channel.NextPublishSeqNo;

        /// <inheritdoc/>
        public TimeSpan ContinuationTimeout
        {
            get { return channel.ContinuationTimeout; }
            set { channel.ContinuationTimeout = value; }
        }

        /// <inheritdoc/>
        public event EventHandler<BasicAckEventArgs> BasicAcks
        {
            add { channel.BasicAcks += value; }
            remove { channel.BasicAcks -= value; }
        }

        /// <inheritdoc/>
        public event EventHandler<BasicNackEventArgs> BasicNacks
        {
            add { channel.BasicNacks += value; }
            remove { channel.BasicNacks -= value; }
        }

        /// <inheritdoc/>
        public event EventHandler<EventArgs> BasicRecoverOk
        {
            add { channel.BasicRecoverOk += value; }
            remove { channel.BasicRecoverOk -= value; }
        }

        /// <inheritdoc/>
        public event EventHandler<BasicReturnEventArgs> BasicReturn
        {
            add { channel.BasicReturn += value; }
            remove { channel.BasicReturn -= value; }
        }

        /// <inheritdoc/>
        public event EventHandler<CallbackExceptionEventArgs> CallbackException
        {
            add { channel.CallbackException += value; }
            remove { channel.CallbackException -= value; }
        }

        /// <inheritdoc/>
        public event EventHandler<FlowControlEventArgs> FlowControl
        {
            add { channel.FlowControl += value; }
            remove { channel.FlowControl -= value; }
        }

        /// <inheritdoc/>
        public event EventHandler<ShutdownEventArgs> ModelShutdown
        {
            add { channel.ModelShutdown += value; }
            remove { channel.ModelShutdown -= value; }
        }

        /// <inheritdoc/>
        public void Abort()
        {
            channel.Abort();
        }

        /// <inheritdoc/>
        public void Abort(ushort replyCode, string replyText)
        {
            channel.Abort(replyCode, replyText);
        }

        /// <inheritdoc/>
        public void BasicAck(ulong deliveryTag, bool multiple)
        {
            channel.BasicAck(deliveryTag, multiple);
        }

        /// <inheritdoc/>
        public void BasicCancel(string consumerTag)
        {
            channel.BasicCancel(consumerTag);
        }

        /// <inheritdoc/>
        public string BasicConsume(string queue, bool autoAck, string consumerTag, bool noLocal, bool exclusive, IDictionary<string, object> arguments, IBasicConsumer consumer)
        {
            return channel.BasicConsume(queue, autoAck, consumerTag, noLocal, exclusive, arguments, consumer);
        }

        /// <inheritdoc/>
        public BasicGetResult BasicGet(string queue, bool autoAck)
        {
            return channel.BasicGet(queue, autoAck);
        }

        /// <inheritdoc/>
        public void BasicNack(ulong deliveryTag, bool multiple, bool requeue)
        {
            channel.BasicNack(deliveryTag, multiple, requeue);
        }

        /// <inheritdoc/>
        public void BasicPublish(string exchange, string routingKey, bool mandatory, IBasicProperties basicProperties, byte[] body)
        {
            channel.BasicPublish(exchange, routingKey, basicProperties, body);
        }

        /// <inheritdoc/>
        public void BasicQos(uint prefetchSize, ushort prefetchCount, bool global)
        {
            channel.BasicQos(prefetchSize, prefetchCount, global);
        }

        /// <inheritdoc/>
        public void BasicRecover(bool requeue)
        {
            channel.BasicRecover(requeue);
        }

        /// <inheritdoc/>
        public void BasicRecoverAsync(bool requeue)
        {
            channel.BasicRecoverAsync(requeue);
        }

        /// <inheritdoc/>
        public void BasicReject(ulong deliveryTag, bool requeue)
        {
            channel.BasicReject(deliveryTag, requeue);
        }

        /// <inheritdoc/>
        public void Close()
        {
            lock (syncLock)
            {
                if (!isClosed)
                {
                    channel.Close();
                    isClosed = true;
                }
            }
        }

        /// <inheritdoc/>
        public void Close(ushort replyCode, string replyText)
        {
            lock (syncLock)
            {
                if (!isClosed)
                {
                    channel.Close(replyCode, replyText);
                    isClosed = true;
                }
            }
        }

        /// <inheritdoc/>
        public void ConfirmSelect()
        {
            channel.ConfirmSelect();
        }

        /// <inheritdoc/>
        public uint ConsumerCount(string queue)
        {
            return channel.ConsumerCount(queue);
        }

        /// <inheritdoc/>
        public IBasicProperties CreateBasicProperties()
        {
            return channel.CreateBasicProperties();
        }

        /// <inheritdoc/>
        public IBasicPublishBatch CreateBasicPublishBatch()
        {
            return channel.CreateBasicPublishBatch();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            lock (syncLock)
            {
                if (!isDisposed)
                {
                    Close();
                    channel.Dispose();
                    isDisposed = true;
                }
            }
        }

        /// <inheritdoc/>
        public void ExchangeBind(string destination, string source, string routingKey, IDictionary<string, object> arguments)
        {
            channel.ExchangeBind(destination, source, routingKey, arguments);
        }

        /// <inheritdoc/>
        public void ExchangeBindNoWait(string destination, string source, string routingKey, IDictionary<string, object> arguments)
        {
            channel.ExchangeBindNoWait(destination, source, routingKey, arguments);
        }

        /// <inheritdoc/>
        public void ExchangeDeclare(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object> arguments)
        {
            channel.ExchangeDeclare(exchange, type, durable, autoDelete, arguments);
        }

        /// <inheritdoc/>
        public void ExchangeDeclareNoWait(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object> arguments)
        {
            channel.ExchangeDeclareNoWait(exchange, type, durable, autoDelete, arguments);
        }

        /// <inheritdoc/>
        public void ExchangeDeclarePassive(string exchange)
        {
            channel.ExchangeDeclarePassive(exchange);
        }

        /// <inheritdoc/>
        public void ExchangeDelete(string exchange, bool ifUnused)
        {
            channel.ExchangeDelete(exchange, ifUnused);
        }

        /// <inheritdoc/>
        public void ExchangeDeleteNoWait(string exchange, bool ifUnused)
        {
            channel.ExchangeDeleteNoWait(exchange, ifUnused);
        }

        /// <inheritdoc/>
        public void ExchangeUnbind(string destination, string source, string routingKey, IDictionary<string, object> arguments)
        {
            channel.ExchangeUnbind(destination, source, routingKey, arguments);
        }

        /// <inheritdoc/>
        public void ExchangeUnbindNoWait(string destination, string source, string routingKey, IDictionary<string, object> arguments)
        {
            channel.ExchangeUnbindNoWait(destination, source, routingKey, arguments);
        }

        /// <inheritdoc/>
        public uint MessageCount(string queue)
        {
            return channel.MessageCount(queue);
        }

        /// <inheritdoc/>
        public void QueueBind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
        {
            channel.QueueBind(queue, exchange, routingKey, arguments);
        }

        /// <inheritdoc/>
        public void QueueBindNoWait(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
        {
            channel.QueueBindNoWait(queue, exchange, routingKey, arguments);
        }

        /// <inheritdoc/>
        public QueueDeclareOk QueueDeclare(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments)
        {
            return channel.QueueDeclare(queue, durable, exclusive, autoDelete, arguments);
        }

        /// <inheritdoc/>
        public void QueueDeclareNoWait(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object> arguments)
        {
            channel.QueueDeclareNoWait(queue, durable, exclusive, autoDelete, arguments);
        }

        /// <inheritdoc/>
        public QueueDeclareOk QueueDeclarePassive(string queue)
        {
            return channel.QueueDeclarePassive(queue);
        }

        /// <inheritdoc/>
        public uint QueueDelete(string queue, bool ifUnused, bool ifEmpty)
        {
            return channel.QueueDelete(queue, ifUnused, ifEmpty);
        }

        /// <inheritdoc/>
        public void QueueDeleteNoWait(string queue, bool ifUnused, bool ifEmpty)
        {
            channel.QueueDeleteNoWait(queue, ifUnused, ifEmpty);
        }

        /// <inheritdoc/>
        public uint QueuePurge(string queue)
        {
            return channel.QueuePurge(queue);
        }

        /// <inheritdoc/>
        public void QueueUnbind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments)
        {
            channel.QueueUnbind(queue, exchange, routingKey, arguments);
        }

        /// <inheritdoc/>
        public void TxCommit()
        {
            channel.TxCommit();
        }

        /// <inheritdoc/>
        public void TxRollback()
        {
            channel.TxRollback();
        }

        /// <inheritdoc/>
        public void TxSelect()
        {
            channel.TxSelect();
        }

        /// <inheritdoc/>
        public bool WaitForConfirms()
        {
            return channel.WaitForConfirms();
        }

        /// <inheritdoc/>
        public bool WaitForConfirms(TimeSpan timeout)
        {
            return channel.WaitForConfirms(timeout);
        }

        /// <inheritdoc/>
        public bool WaitForConfirms(TimeSpan timeout, out bool timedOut)
        {
            return channel.WaitForConfirms(timeout, out timedOut);
        }

        /// <inheritdoc/>
        public void WaitForConfirmsOrDie()
        {
            channel.WaitForConfirmsOrDie();
        }

        /// <inheritdoc/>
        public void WaitForConfirmsOrDie(TimeSpan timeout)
        {
            channel.WaitForConfirmsOrDie(timeout);
        }
    }
}
