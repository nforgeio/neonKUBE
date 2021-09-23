//-----------------------------------------------------------------------------
// FILE:	    Test_NeonKubeKv.cs
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
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Xunit;
using Neon.Service;
using Neon.Xunit;

using Xunit;

namespace TestKube
{
    public class Test_NeonKubeKv : IClassFixture<ComposedFixture>
    {
        private ServiceMap serviceMap;
        private ComposedFixture composedFixture;
        private NeonServiceFixture<NeonKubeKv.Service> neonKubeKvFixture;
        private DockerFixture citusFixture;

        public Test_NeonKubeKv(ComposedFixture composedFixture)
        {
            this.composedFixture = composedFixture;
            this.serviceMap = CreateServiceMap();

            var postgresUser     = "neontest";
            var postgresPassword = NeonHelper.GetCryptoRandomPassword(10);
            var postgresDb       = "neontest";

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
                                   $"POSTGRES_USER={postgresUser}",
                                   $"POSTGRES_PASSWORD={postgresPassword}",
                                   $"POSTGRES_DB={postgresDb}"
                               });
                       });

                   composedFixture.AddServiceFixture<NeonKubeKv.Service>(NeonServices.NeonKubeKvService, new NeonServiceFixture<NeonKubeKv.Service>(), () => CreateNeonKubeKvService());

               });

            this.citusFixture = (DockerFixture)composedFixture[NeonServices.NeonSystemDb];
            this.neonKubeKvFixture = (NeonServiceFixture<NeonKubeKv.Service>)composedFixture[NeonServices.NeonKubeKvService];
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
                    Protocol = ServiceEndpointProtocol.Http,
                    PathPrefix = "/",
                    Port = 5432
                });

            serviceMap.Add(description);

            //---------------------------------------------
            // web-service:

            description = new ServiceDescription()
            {
                Name = NeonServices.NeonKubeKvService,
                Address = "127.0.0.10"
            };

            description.Endpoints.Add(
                new ServiceEndpoint()
                {
                    Protocol = ServiceEndpointProtocol.Http,
                    PathPrefix = "/",
                    Port = 80
                });

            serviceMap.Add(description);

            return serviceMap;
        }


        public NeonKubeKv.Service CreateNeonKubeKvService()
        {
            var service = new NeonKubeKv.Service(NeonServices.NeonKubeKvService, CreateServiceMap());

            return service;
        }

        [Fact]
        public void SetValue()
        {
           
        }
    }
}
