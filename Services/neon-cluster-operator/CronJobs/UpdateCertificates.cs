//-----------------------------------------------------------------------------
// FILE:	    UpdateCertificates.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Kube;
using Neon.Kube.Resources;

using k8s;
using k8s.Models;


using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Prometheus;

using Quartz;

namespace NeonClusterOperator
{
    public class UpdateCertificates : CronJob, IJob
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public UpdateCertificates()
            : base(typeof(UpdateCertificates))
        {

        }
        
        /// <inheritdoc/>
        public async Task Execute(IJobExecutionContext context)
        {
            using (Tracer.CurrentSpan)
            {
                Tracer.CurrentSpan?.AddEvent("execute", attributes => attributes.Add("cronjob", nameof(UpdateCertificates)));

                var dataMap = context.MergedJobDataMap;
                var k8s = (IKubernetes)dataMap["Kubernetes"];

                var nodes = await k8s.ListNodeAsync();
                var startTime = DateTime.UtcNow.AddSeconds(10);

                foreach (var node in nodes.Items)
                {
                    var nodeTask = new V1NeonNodeTask()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            Name = $"certificate-update-{NeonHelper.CreateBase36Uuid()}"
                        },
                        Spec = new V1NeonNodeTask.TaskSpec()
                        {
                            Node = node.Name(),
                            StartAfterTimestamp = startTime,
                            BashScript = @"update-ca-certificates",
                        }
                    };

                    await k8s.CreateClusterCustomObjectAsync<V1NeonNodeTask>(nodeTask, name: nodeTask.Name());

                    startTime = startTime.AddMinutes(10);
                }
            }
        }
    }
}
