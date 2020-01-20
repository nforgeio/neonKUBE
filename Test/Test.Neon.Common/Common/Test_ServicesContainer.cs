//-----------------------------------------------------------------------------
// FILE:	    Test_ServicesContainer.cs
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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Collections;
using Neon.Net;
using Neon.Retry;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public class Test_ServicesContainer
    {
        public interface IService1
        {
        }

        public class Service1 : IService1
        {
        }

        public interface IService2
        {
        }

        public class Service2 : IService2
        {
        }

        public interface IService3
        {
        }

        public class Service3 : IService3
        {
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Empty()
        {
            var services = new ServiceContainer();

            Assert.Empty(services);
            Assert.False(services.IsReadOnly);
            Assert.DoesNotContain(new ServiceDescriptor(typeof(IService1), new Service1()), services);
            Assert.NotNull(services.CreateScope());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Singelton()
        {
            var services = new ServiceContainer();
            var service1 = new Service1();
            var service2 = new Service2();

            Assert.Empty(services);

            services.AddSingleton<IService1>(service1);
            services.AddSingleton<IService2>(service2);

            Assert.Equal(2, services.Count);

            Assert.Same(service1, services.GetService<IService1>());
            Assert.Same(service2, services.GetService<IService2>());
            Assert.Null(services.GetService<IService3>());

            Assert.Throws<InvalidOperationException>(() => services.GetRequiredService<IService3>());

            services.GetService<IService1>();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Transient()
        {
            var services = new ServiceContainer();
            var service1 = new Service1();
            var service2 = new Service2();

            Assert.Empty(services);

            services.AddTransient<IService1>(provider => new Service1());
            services.AddTransient<IService2>(provider => new Service2());

            Assert.Equal(2, services.Count);

            Assert.NotNull(services.GetService<IService1>());
            Assert.NotSame(service1, services.GetService<IService1>());

            Assert.NotNull(services.GetService<IService1>());
            Assert.NotSame(service2, services.GetService<IService2>());

            Assert.Null(services.GetService<IService3>());

            Assert.Throws<InvalidOperationException>(() => services.GetRequiredService<IService3>());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Replace()
        {
            var services = new ServiceContainer();
            var serviceA = new Service1();
            var serviceB = new Service1();
            var service2 = new Service2();

            Assert.Empty(services);

            services.AddSingleton<IService1>(serviceA);
            services.AddSingleton<IService2>(service2);

            Assert.Equal(2, services.Count);
            Assert.Same(serviceA, services.GetService<IService1>());
            Assert.Same(service2, services.GetService<IService2>());

            // Verify that adding service with the same type as an existing
            // service, replaces the existing service rather than throwing 
            // an exception.

            services.AddSingleton<IService1>(serviceB);

            Assert.Equal(2, services.Count);
            Assert.Same(serviceB, services.GetService<IService1>());
            Assert.Same(service2, services.GetService<IService2>());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Snapshot()
        {
            var services = new ServiceContainer();
            var service1 = new Service1();
            var service2 = new Service2();
            var service3 = new Service3();
            var snapshot = services.BuildServiceProvider();

            Assert.Empty(services);

            services.AddSingleton<IService1>(service1);
            services.AddSingleton<IService2>(service2);

            Assert.Equal(2, services.Count);

            Assert.Same(service1, services.GetService<IService1>());
            Assert.Same(service2, services.GetService<IService2>());
            Assert.Null(services.GetService<IService3>());

            Assert.Throws<InvalidOperationException>(() => services.GetRequiredService<IService3>());

            // These should all return NULL because the snapshot was
            // taken before the services were added.

            Assert.Null(snapshot.GetService<IService1>());
            Assert.Null(snapshot.GetService<IService2>());
            Assert.Null(snapshot.GetService<IService3>());

            // Get a new snapshot and test again.

            snapshot = services.BuildServiceProvider();

            Assert.Same(service1, services.GetService<IService1>());
            Assert.Same(service2, services.GetService<IService2>());
            Assert.Null(snapshot.GetService<IService3>());

            // Make sure that the [ServicesContainer] is still returning
            // the correct services.

            Assert.Same(service1, services.GetService<IService1>());
            Assert.Same(service2, services.GetService<IService2>());
            Assert.Null(services.GetService<IService3>());

            Assert.Throws<InvalidOperationException>(() => services.GetRequiredService<IService3>());

            // Add a third service and verify the [ServicesContainer].

            services.AddSingleton<IService3>(service3);

            Assert.Equal(3, services.Count);
            Assert.Same(service1, services.GetService<IService1>());
            Assert.Same(service2, services.GetService<IService2>());
            Assert.Same(service3, services.GetService<IService3>());

            // The new service shouldn't be in the last snapshot.

            Assert.Null(snapshot.GetService<IService3>());

            // Get a new snapshot and verify all three services.

            snapshot = services.BuildServiceProvider();

            Assert.Same(service1, services.GetService<IService1>());
            Assert.Same(service2, services.GetService<IService2>());
            Assert.Same(service3, services.GetService<IService3>());
        }
    }
}
