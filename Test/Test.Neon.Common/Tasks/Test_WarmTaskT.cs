//-----------------------------------------------------------------------------
// FILE:	    Test_WarmTaskT.cs
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Retry;
using Neon.Tasks;
using Neon.Xunit;

using Xunit;

#pragma warning disable CS1998  // Missing [await] warning.

namespace TestCommon
{
    public class Test_WarmTaskT
    {
        [Fact]
        public async Task WarmTask_NoHotAction_ColdSync()
        {
            // Verify that a WarmTask without a hot action and with a 
            // cold action that completes synchronously works.

            var coldCompleted = false;

            var warmTask = new WarmTask<string>(null,
                async () =>
                {
                    coldCompleted = true;
                    return await Task.FromResult("Hello World!");
                });

            // Wait a bit to ensure that the cold action is not
            // scheduled until after the task as awaited.

            await Task.Delay(TimeSpan.FromSeconds(1));
            Assert.False(coldCompleted);

            Assert.Equal("Hello World!", await warmTask);
            Assert.True(coldCompleted);
        }

        [Fact]
        public async Task WarmTask_NoHotAction_ColdAsync()
        {
            // Verify that a WarmTask without a hot action and with a 
            // cold action that completes asynchronously works.

            var coldCompleted = false;

            var warmTask = new WarmTask<string>(null,
                async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    coldCompleted = true;
                    return await Task.FromResult("Hello World!");
                });

            // Wait a bit to ensure that the cold action is not
            // scheduled until after the task as awaited.

            await Task.Delay(TimeSpan.FromSeconds(1));
            Assert.False(coldCompleted);
            Assert.Equal("Hello World!", await warmTask);
            Assert.True(coldCompleted);
        }

        [Fact]
        public async Task WarmTask_HotSync_ColdSync()
        {
            // Verify that a WarmTask with a hot and cold actions
            // that both complete synchronously works.

            var hotCompleted  = false;
            var coldCompleted = false;

            var warmTask = new WarmTask<string>(
                hotAction: async () =>
                {
                    hotCompleted = true;
                },
                coldAction: async () =>
                {
                    coldCompleted = true;
                    return await Task.FromResult("Hello World!");
                });

            // Give the hot action a chance to complete and
            // then verify that it did in fact complete but
            // that the cold action is not complete (because
            // we haven't awaited it yet).

            await Task.Delay(TimeSpan.FromSeconds(1));

            Assert.True(hotCompleted);
            Assert.False(coldCompleted);

            Assert.Equal("Hello World!", await warmTask);
            Assert.True(hotCompleted);
            Assert.True(coldCompleted);
        }

        [Fact]
        public async Task WarmTask_HotAsync_ColdSync()
        {
            // Verify that a WarmTask with a hot action that completes
            // asynchronously and a cold action that completes synchronously
            // works.

            var hotCompleted  = false;
            var coldCompleted = false;

            var warmTask = new WarmTask<string>(
                hotAction: async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    hotCompleted = true;
                },
                coldAction: async () =>
                {
                    coldCompleted = true;
                    return await Task.FromResult("Hello World!");
                });

            // Give the hot action a chance to complete and
            // then verify that it did in fact complete but
            // that the cold action is not complete (because
            // we haven't awaited it yet).

            await Task.Delay(TimeSpan.FromSeconds(2));

            Assert.True(hotCompleted);
            Assert.False(coldCompleted);

            Assert.Equal("Hello World!", await warmTask);
            Assert.True(hotCompleted);
            Assert.True(coldCompleted);
        }

        [Fact]
        public async Task WarmTask_HotSync_ColdAsync()
        {
            // Verify that a WarmTask with a hot action that completes
            // synchronously and a cold action that completes asynchronously
            // works.

            var hotCompleted = false;
            var coldCompleted = false;

            var warmTask = new WarmTask<string>(
                hotAction: async () =>
                {
                    hotCompleted = true;
                },
                coldAction: async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    coldCompleted = true;
                    return await Task.FromResult("Hello World!");
                });

            // Give the hot action a chance to complete and
            // then verify that it did in fact complete but
            // that the cold action is not complete (because
            // we haven't awaited it yet).

            await Task.Delay(TimeSpan.FromSeconds(1));

            Assert.True(hotCompleted);
            Assert.False(coldCompleted);

            Assert.Equal("Hello World!", await warmTask);
            Assert.True(hotCompleted);
            Assert.True(coldCompleted);
        }

        [Fact]
        public async Task WarmTask_HotAsync_ColdAsync()
        {
            // Verify that a WarmTask with a hot and cold actions
            // that both complete asynchronously works.

            var hotCompleted = false;
            var coldCompleted = false;

            var warmTask = new WarmTask<string>(
                hotAction: async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    hotCompleted = true;
                },
                coldAction: async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    coldCompleted = true;
                    return await Task.FromResult("Hello World!");
                });

            // Give the hot action a chance to complete and
            // then verify that it did in fact complete but
            // that the cold action is not complete (because
            // we haven't awaited it yet).

            await Task.Delay(TimeSpan.FromSeconds(2));

            Assert.True(hotCompleted);
            Assert.False(coldCompleted);

            await warmTask;

            Assert.True(hotCompleted);
            Assert.True(coldCompleted);
        }
    }
}
