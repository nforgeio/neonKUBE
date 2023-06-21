//-----------------------------------------------------------------------------
// FILE:        UnitTest1.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright � 2005-2023 by NEONFORGE LLC.  All rights reserved.
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

using k8s;
using k8s.Models;
using Moq;
using Neon.Kube.Resources.Cluster;
using Neon.Kube.Xunit.Operator;

using NeonClusterOperator;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TestNeonClusterOperator
{
    public class Test_Operator : IClassFixture<TestOperatorFixture>
    {
        private TestOperatorFixture fixture;

        public Test_Operator(TestOperatorFixture fixture)
        {
            this.fixture = fixture;
            fixture.Operator.AddController<NeonSsoCallbackUrlController>();
            fixture.RegisterType<V1NeonSsoClient>();
            fixture.RegisterType<V1NeonSsoCallbackUrl>();
            fixture.Start();
        }

        [Fact]
        public async Task NeonSsoCallbackUrl()
        {
            fixture.ClearResources();

            var controller = fixture.Operator.GetController<NeonSsoCallbackUrlController>();

            var ssoClient           = new V1NeonSsoClient().Initialize();
            ssoClient.Metadata.Name = "foo";
            ssoClient.Spec          = new V1SsoClientSpec()
            {
                Id           = "foo",
                Name         = "foo",
                Public       = true,
                RedirectUris = new List<string>(),
                Secret       = "afgvkjdfhn",
                TrustedPeers = new List<string>()
            };

            fixture.Resources.Add(ssoClient);

            var callbackUrl           = new V1NeonSsoCallbackUrl().Initialize();
            callbackUrl.Metadata.Name = "foo";
            callbackUrl.Spec          = new V1SsoCallbackUrlSpec()
            {
                SsoClient = ssoClient.Name(),
                Url       = "foo.bar/callback"
            };

            fixture.Resources.Add(callbackUrl);

            await controller.ReconcileAsync(callbackUrl);

            ssoClient = fixture.Resources.OfType<V1NeonSsoClient>().First();

            Assert.Contains("foo.bar/callback", ssoClient.Spec.RedirectUris);
            Assert.Single(ssoClient.Spec.RedirectUris);

            callbackUrl.Spec.Url = "new.callback";

            await controller.ReconcileAsync(callbackUrl);

            ssoClient = fixture.Resources.OfType<V1NeonSsoClient>().First();

            Assert.Contains("new.callback", ssoClient.Spec.RedirectUris);
            Assert.Single(ssoClient.Spec.RedirectUris);

            ssoClient               = new V1NeonSsoClient().Initialize();
            ssoClient.Metadata.Name = "bar";
            ssoClient.Spec          = new V1SsoClientSpec()
            {
                Id           = "bar",
                Name         = "bar",
                Public       = true,
                RedirectUris = new List<string>(),
                Secret       = "jkhyfkhgdf",
                TrustedPeers = new List<string>()
            };

            fixture.Resources.Add(ssoClient);

            callbackUrl.Spec.SsoClient = "bar";
            callbackUrl.Spec.Url       = "bar.com/callback";

            await controller.ReconcileAsync(callbackUrl);

            ssoClient = fixture.Resources.OfType<V1NeonSsoClient>().Where(sc => sc.Metadata.Name == "foo").Single();

            Assert.Empty(ssoClient.Spec.RedirectUris);

            ssoClient = fixture.Resources.OfType<V1NeonSsoClient>().Where(sc => sc.Metadata.Name == "bar").Single();

            Assert.Contains("bar.com/callback", ssoClient.Spec.RedirectUris);
            Assert.Single(ssoClient.Spec.RedirectUris);


        }
    }
}
