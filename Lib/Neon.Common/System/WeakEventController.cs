//-----------------------------------------------------------------------------
// FILE:	    WeakEventController.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;

namespace System
{
    /// <summary>
    /// Implements a weak event listener that allows the owner to be garbage
    /// collected if it is the only remaining link is an event handler.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is intended to be a drop-in replacement for the <b>WeakEventManager</b> 
    /// class that is available in .NET 4.5 but is not currently present in the Xamarin
    /// Mono class libraries.  Use the <see cref="AddHandler{TEventSource, TEventArgs}(TEventSource, string, EventHandler{TEventArgs})"/>
    /// and <see cref="RemoveHandler{TEventSource, TEventArgs}(TEventSource, string, EventHandler{TEventArgs})"/>
    /// to add or remove event handlers.
    /// </para>
    /// <note>
    /// <b>Important:</b> Take care to remove any handlers when an event listener instance is disposed 
    /// and/or finalized.  Neglecting to do this will orhpan the objects <see cref="WeakEventController"/>
    /// uses to track the handler references.
    /// </note>
    /// <para>
    /// This code was adapted from a Code Project article by <b>Samuel Cragg</b> called 
    /// <a href="www.codeproject.com/Articles/786606/WeakEventManager-for-WinRT">WeakEventManager for WinRT</a>.
    /// The code is licensed under the <a href="http://www.codeproject.com/info/cpol10.aspx">The Code Project Open License (CPOL)</a>.
    /// </para>
    /// <para>
    /// I enhanced the code by making it threadsafe.
    /// </para>
    /// </remarks>
    public static class WeakEventController
    {
        //---------------------------------------------------------------------
        // Private types

        private class WeakEvent
        {
            //-----------------------------------------------------------------
            // Static members

            private static readonly MethodInfo onEventInfo = typeof(WeakEvent).GetTypeInfo().GetDeclaredMethod("OnEvent");

            //-----------------------------------------------------------------
            // Instance members

            private EventInfo                   eventInfo;
            private object                      eventRegistration;
            private MethodInfo                  handlerMethod;
            private WeakReference<object>       handlerTarget;
            private object                      source;
            private int                         bucketIndex;

            public WeakEvent(object source, EventInfo eventInfo, Delegate handler, int bucketIndex)
            {
                this.source      = source;
                this.eventInfo   = eventInfo;
                this.bucketIndex = bucketIndex;

                // We can't store a reference to the handler (as that will keep
                // the target alive) but we also can't store a WeakReference to
                // handler, as that could be GC'd before its target.
                
                handlerMethod = handler.GetMethodInfo();
                handlerTarget = new WeakReference<object>(handler.Target);

                var onEventHandler = this.CreateHandler();

                eventRegistration = eventInfo.AddMethod.Invoke(
                    source,
                    new object[] { onEventHandler });

                // If the AddMethod returned null then it was void - to
                // unregister we simply need to pass in the same delegate.

                if (eventRegistration == null)
                {
                    eventRegistration = onEventHandler;
                }
            }

            public void Detach()
            {
                if (eventInfo != null)
                {
                    var bucketList = registeredEventBuckets[bucketIndex];

                    lock (bucketList)
                    {
                        bucketList.Remove(this);
                    }

                    eventInfo.RemoveMethod.Invoke(
                        this.source,
                        new object[] { eventRegistration });

                    eventInfo         = null;
                    eventRegistration = null;
                }
            }

            public bool IsEqualTo(object source, EventInfo eventInfo, Delegate handler)
            {
                if (source == this.source && eventInfo == this.eventInfo)
                {
                    object target;

                    if (handlerTarget.TryGetTarget(out target))
                    {
                        return handler.Target == target && handler.GetMethodInfo() == handlerMethod;
                    }
                }

                return false;
            }

            public void OnEvent<T>(object sender, T args)
            {
                object instance;

                if (handlerTarget.TryGetTarget(out instance))
                {
                    handlerMethod.Invoke(instance, new object[] { sender, args });
                }
                else
                {
                    Detach();
                }
            }

            private object CreateHandler()
            {
                var eventType = this.eventInfo.EventHandlerType;

                var parameters = eventType.GetTypeInfo()
                                    .GetDeclaredMethod("Invoke")
                                    .GetParameters();

                return onEventInfo.MakeGenericMethod(parameters[1].ParameterType)
                            .CreateDelegate(eventType, this);
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private const int BucketCount = 100;

        /// <summary>
        /// We're going to assign events to buckets using the hash of their property
        /// values.  Each bucket will hold a list of the weak events that were dropped
        /// in the bucket.  This should improve scalability when there are a lot
        /// of events.
        /// </summary>
        private readonly static List<WeakEvent>[] registeredEventBuckets;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static WeakEventController()
        {
            registeredEventBuckets = new List<WeakEvent>[BucketCount];

            for (int i = 0; i < BucketCount; i++)
            {
                registeredEventBuckets[i] = new List<WeakEvent>();
            }
        }

        /// <summary>
        /// Hashes the event properties to an event bucket index.
        /// </summary>
        /// <param name="source">The source object or <c>null</c>.</param>
        /// <param name="eventName">The event name.</param>
        /// <param name="handler">The event handler.</param>
        /// <returns>The event bucket index.</returns>
        private static int HashToBucket(object source, string eventName, object handler)
        {
            var hash = 0;

            if (source != null)
            {
                hash = source.GetHashCode();
            }

            hash ^= eventName.GetHashCode();
            hash ^= handler.GetHashCode();
            hash &= 0x7FFFFFFF;

            return hash % BucketCount;
        }

        /// <summary>
        /// Adds the specified event handler to the specified event.
        /// </summary>
        /// <typeparam name="TEventSource">The type that raises the event.</typeparam>
        /// <typeparam name="TEventArgs">The type that holds the event data.</typeparam>
        /// <param name="source">
        /// The source object that raises the specified event or <c>null</c>.
        /// </param>
        /// <param name="eventName">The name of the event to subscribe to.</param>
        /// <param name="handler">The delegate that handles the event.</param>
        public static void AddHandler<TEventSource, TEventArgs>(TEventSource source, string eventName, EventHandler<TEventArgs> handler)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(eventName));
            Covenant.Requires<ArgumentNullException>(handler != null);

            var eventInfo   = typeof(TEventSource).GetRuntimeEvent(eventName);
            var bucketIndex = HashToBucket(source, eventName, handler);
            var bucketList  = registeredEventBuckets[bucketIndex];;

            lock (bucketList)
            {
                bucketList.Add(new WeakEvent(source, eventInfo, handler, bucketIndex));
            }
        }

        /// <summary>
        /// Removes the specified event handler from the specified event.
        /// </summary>
        /// <typeparam name="TEventSource">The type that raises the event.</typeparam>
        /// <typeparam name="TEventArgs">The type that holds the event data.</typeparam>
        /// <param name="source">
        /// The source object that raises the specified event, or null if it's
        /// a static event.
        /// </param>
        /// <param name="eventName">
        /// The name of the event to remove the handler from.
        /// </param>
        /// <param name="handler">The delegate to remove.</param>
        public static void RemoveHandler<TEventSource, TEventArgs>(TEventSource source, string eventName, EventHandler<TEventArgs> handler)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(eventName));
            Covenant.Requires<ArgumentNullException>(handler != null);

            var eventInfo   = typeof(TEventSource).GetRuntimeEvent(eventName);
            var bucketIndex = HashToBucket(source, eventName, handler);
            var bucketList  = registeredEventBuckets[bucketIndex]; ;

            lock (bucketList)
            {
                foreach (var weakEvent in bucketList)
                {
                    if (weakEvent.IsEqualTo(source, eventInfo, handler))
                    {
                        weakEvent.Detach();
                        break;
                    }
                }
            }
        }
    }
}
