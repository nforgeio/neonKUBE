//-----------------------------------------------------------------------------
// FILE:	    CronJob.cs
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
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Kube.Resources;

using k8s;
using k8s.Models;

using Prometheus;

using Quartz;
using OpenTelemetry.Trace;

namespace NeonClusterOperator
{
    /// <summary>
    /// 
    /// </summary>
    public class CronJob
    {
        /// <summary>
        /// The name of the cron job.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The group.
        /// </summary>
        public string Group { get; set; } = "ClusterSettingsCron";

        /// <summary>
        /// The Job type.
        /// </summary>
        public Type Type { get; set; }   

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="type"></param>
        public CronJob(Type type)
        {
            Type = type;
            Name = Type.Name;
        }

        /// <summary>
        /// Adds the cron job to a specified scheduler.
        /// </summary>
        /// <param name="scheduler"></param>
        /// <param name="k8s"></param>
        /// <param name="cronSchedule"></param>
        /// <returns></returns>
        public Task AddToSchedulerAsync(
            IScheduler scheduler, 
            IKubernetes k8s, 
            string cronSchedule,
            Dictionary<string, object> data = null)
        {
            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                Tracer.CurrentSpan?.AddEvent("add-to-scheduler");

                var job = JobBuilder.Create(Type)
                    .WithIdentity(Name, Group)
                    .Build();

                job.JobDataMap.Put("Kubernetes", k8s);

                if (data != null)
                {
                    foreach (var kv in data)
                    {
                        job.JobDataMap.Put(kv.Key, kv.Value);
                    }
                }

                // Trigger the job to run now, and then repeat every 10 seconds

                ITrigger trigger = TriggerBuilder.Create()
                    .WithIdentity(Name, Group)
                    .WithCronSchedule(cronSchedule)
                    .Build();

                // Tell quartz to schedule the job using our trigger

                return scheduler.ScheduleJob(job, trigger);
            }
        }

        /// <summary>
        /// Removes the job from the specified scheduler.
        /// </summary>
        /// <param name="scheduler"></param>
        /// <returns></returns>
        public async Task DeleteFromSchedulerAsync(IScheduler scheduler)
        {
            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                Tracer.CurrentSpan?.AddEvent("delete-from-scheduler");

                try
                {
                    await scheduler.DeleteJob(new JobKey(Name, Group));
                }
                catch (NullReferenceException)
                {
                    return;
                }
            }
        }
    }
}
