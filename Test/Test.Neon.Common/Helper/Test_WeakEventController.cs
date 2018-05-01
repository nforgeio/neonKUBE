//-----------------------------------------------------------------------------
// FILE:	    Test_WeakEventController.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public class Test_WeakEventController
    {
        //---------------------------------------------------------------------
        // Private types

        private class EventSource
        {
            public event EventHandler<EventArgs> Event;

            public void RaiseEvent()
            {
                Event?.Invoke(this, new EventArgs());
            }
        }

        private class EventListener
        {
            public EventListener()
            {
            }

            ~EventListener()
            {
                collected = true;
            }

            private void OnEvent(object sender, EventArgs args)
            {
                received = true;
            }

            public void AddListener(EventSource source)
            {
                WeakEventController.AddHandler<EventSource, EventArgs>(source, nameof(EventSource.Event), OnEvent);
            }

            public void RemoveListener(EventSource source)
            {
                WeakEventController.RemoveHandler<EventSource, EventArgs>(source, nameof(EventSource.Event), OnEvent);
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private static bool received;
        private static bool collected;
        
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Basics()
        {
            // Verify that we can:
            //
            //      1. Add an event listener
            //      2. Receive events
            //      3. Remove the listener
            //      4. Stop receiving events

            var source   = new EventSource();
            var listener = new EventListener();

            received = false;
            listener.AddListener(source);
            source.RaiseEvent();
            Assert.True(received);

            received = false;
            listener.RemoveListener(source);
            source.RaiseEvent();
            Assert.False(received);
        }

        private void TriggerGC()
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        }

        [Fact(Skip = "Investigate .NET Core GC Behavior")]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void VerifyGC()
        {
            // Verify that we can:
            //
            //      1. Add an event listener
            //      2. Receive events
            //      3. Force a GC and still receive events
            //      4. Clear references to the listener
            //         (without removing the handler)
            //      5. Force a GC again and verify that the 
            //         listener was collected.

            var source   = new EventSource();
            var listener = new EventListener();

            received = false;
            listener.AddListener(source);
            source.RaiseEvent();
            Assert.True(received);

            TriggerGC();

            received = false;
            source.RaiseEvent();
            Assert.True(received);

            listener  = null;
            collected = false;
            TriggerGC();
            Assert.True(collected);
        }
    }
}
