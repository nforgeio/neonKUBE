//-----------------------------------------------------------------------------
// FILE:	    Test_NeonClusterApi.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Xunit;
using Neon.Net;
using Neon.Service;
using Neon.Xunit;

using Xunit;

namespace TestKube
{
    [Trait(TestTrait.Category, TestArea.NeonKube)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_NeonClusterApi : IClassFixture<ComposedFixture>
    {
        private ServiceMap serviceMap;
        private ComposedFixture composedFixture;
        private NeonServiceFixture<NeonClusterApi.Service> NeonClusterApiFixture;
        private DockerFixture citusFixture;
        private KubeKV client;

        private string citusUser;
        private string citusPassword;
        private string citusDb;

        public Test_NeonClusterApi(ComposedFixture composedFixture)
        {
            this.composedFixture = composedFixture;
            this.serviceMap = CreateServiceMap();

            citusUser     = "neontest";
            citusPassword = NeonHelper.GetCryptoRandomPassword(10);
            citusDb       = "postgres";

            composedFixture.Start(
               () =>
               {
                   composedFixture.AddFixture(NeonServices.NeonSystemDb, new DockerFixture(),
                       dockerFixture =>
                       {
                           dockerFixture.CreateService(
                               name: NeonServices.NeonSystemDb,
                               image: "citusdata/citus",
                               dockerArgs: new string[]{
                                    "-p",
                                    "5432:5432",
                               },
                               env: new string[] {
                                   $"POSTGRES_USER={citusUser}",
                                   $"POSTGRES_PASSWORD={citusPassword}",
                                   $"POSTGRES_DB={citusDb}"
                               });
                       });

                   composedFixture.AddServiceFixture<NeonClusterApi.Service>(NeonServices.NeonClusterApiService, new NeonServiceFixture<NeonClusterApi.Service>(), () => CreateNeonClusterApiService(), startTimeout: TimeSpan.FromMinutes(5));

               });

            this.citusFixture = (DockerFixture)composedFixture[NeonServices.NeonSystemDb];
            this.NeonClusterApiFixture = (NeonServiceFixture<NeonClusterApi.Service>)composedFixture[NeonServices.NeonClusterApiService];

            client = new KubeKV();
        }

        /// <summary>
        /// Returns the service map.
        /// </summary>
        private ServiceMap CreateServiceMap()
        {
            var serviceMap = new ServiceMap();

            //---------------------------------------------
            // system database:

            var description = new ServiceDescription()
            {
                Name = NeonServices.NeonSystemDb,
                Address = "127.0.0.10"
            };

            description.Endpoints.Add(
                new ServiceEndpoint()
                {
                    Protocol = ServiceEndpointProtocol.Tcp,
                    Port = 5432
                });

            serviceMap.Add(description);

            //---------------------------------------------
            // web-service:

            description = new ServiceDescription()
            {
                Name = NeonServices.NeonClusterApiService,
                Address = "127.0.0.10"
            };

            description.Endpoints.Add(
                new ServiceEndpoint()
                {
                    Protocol = ServiceEndpointProtocol.Http,
                    PathPrefix = "/",
                    Port = 1234
                });

            serviceMap.Add(description);

            return serviceMap;
        }

        /// <summary>
        /// Returns the neon-cluster-api.
        /// </summary>
        /// <returns></returns>
        public NeonClusterApi.Service CreateNeonClusterApiService()
        {
            var service = new NeonClusterApi.Service(NeonServices.NeonClusterApiService, CreateServiceMap());

            service.SetEnvironmentVariable("CITUS_USER", citusUser);
            service.SetEnvironmentVariable("CITUS_PASSWORD", citusPassword);

            service.SetEnvironmentVariable("NEON_USER", KubeConst.NeonSystemDbAdminUser);
            service.SetEnvironmentVariable("NEON_PASSWORD", NeonHelper.GetCryptoRandomPassword(10));

            return service;
        }

        /// <summary>
        /// Delete all values in the state table.
        /// </summary>
        /// <returns></returns>
        private async Task ClearDatabaseAsync()
        {
            await client.RemoveAsync("*", regex: true);
        }

        public class JsonTestPerson
        {
            public string Name { get; set; }
            public int Age { get; set; }
        }

        [Fact]
        public async void GetValue()
        {
            // Verify that get works and returns null when key doesn't exist.

            await ClearDatabaseAsync();

            var key = "foo";

            await Assert.ThrowsAsync<KubeKVException>(async () => await client.GetAsync<string>(key));

            var value = "bar";

            await client.SetAsync(key, value);
            var result = await client.GetAsync<string>(key);

            Assert.Equal(value, result);
        }

        [Fact]
        public async void SetValue()
        {
            // Verify we can set different types of values.

            await ClearDatabaseAsync();

            var key = "foo";
            var value = "bar";

            await client.SetAsync(key, value);
            var result = await client.GetAsync<string>(key);

            Assert.Equal(value, result);

            var marcus = new JsonTestPerson()
            {
                Name = "Marcus",
                Age = 28
            };
            await client.SetAsync("marcus", marcus);
            var person = await client.GetAsync<JsonTestPerson>("marcus");

            Assert.Equal(marcus.Age, person.Age);
            Assert.Equal(marcus.Name, person.Name);
        }

        [Fact]
        public async void UpsertValue()
        {
            // Verify that we can update existing values.

            await ClearDatabaseAsync();

            var key = "foo";
            var value = "bar";

            await client.SetAsync(key, value);
            var result = await client.GetAsync<string>(key);

            Assert.Equal(value, result);

            value = "baz";
            await client.SetAsync(key, value);
            result = await client.GetAsync<string>(key);

            Assert.Equal(value, result);
        }

        [Fact]
        public async void GetDefault()
        {
            // Verify that we get default value when key doesn't exist.

            await ClearDatabaseAsync();

            var key = "foo";

            var result = await client.GetAsync<int>(key, 101);
            Assert.Equal(101, result);

            var value = 1;

            await client.SetAsync(key, value);
            result = await client.GetAsync<int>(key, 101);


            Assert.Equal(1, result);
        }

        [Fact]
        public async Task DeleteValue()
        {
            // Verify deleting with and without regex works

            await ClearDatabaseAsync();

            await client.SetAsync("foo-0", "0");
            await client.SetAsync("foo-1", "1");
            await client.SetAsync("foo-2", "2");
            await client.SetAsync("bar-0", "0");
            await client.SetAsync("bar-1", "1");
            await client.SetAsync("bar-2", "2");
            await client.SetAsync("0-baz-0", "0");
            await client.SetAsync("1-baz-2", "0000");

            await client.RemoveAsync("foo-1", regex: false);
            await Assert.ThrowsAsync<KubeKVException>(async () => await client.GetAsync<string>("foo-1"));

            var results = await client.ListAsync<dynamic>("*");
            Assert.Equal(7, results.Count);

            await client.RemoveAsync("^bar-*", regex: true);
            results = await client.ListAsync<dynamic>("*");
            Assert.Equal(4, results.Count);

            await client.RemoveAsync("[0-9]+-[a-z]+-[0-9]", regex: true);
            results = await client.ListAsync<dynamic>("*");
            Assert.Equal(2, results.Count);
        }

        [Fact]
        public async void ListValues()
        {
            // Verify list with/without regex works.

            await ClearDatabaseAsync();

            await client.SetAsync("foo-0", "0");
            await client.SetAsync("foo-1", "1");
            await client.SetAsync("foo-2", "2");
            await client.SetAsync("bar-0", "0");
            await client.SetAsync("bar-1", "1");
            await client.SetAsync("bar-2", "2");
            await client.SetAsync("0-baz-0", "0");
            await client.SetAsync("1-baz-2", "0000");

            
            var result = await client.ListAsync<dynamic>("foo*");

            Assert.Equal(3, result.Count);

            var results = await client.ListAsync<dynamic>("*");
            Assert.Equal(8, results.Count);

            results = await client.ListAsync<dynamic>("^bar-*");
            Assert.Equal(3, results.Count);

            results = await client.ListAsync<dynamic>("[0-9]+-[a-z]+-[0-9]");
            Assert.Equal(2, results.Count);
        }
    }
}