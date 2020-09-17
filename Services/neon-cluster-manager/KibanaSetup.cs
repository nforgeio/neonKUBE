//-----------------------------------------------------------------------------
// FILE:	    Program.LogPurger.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.Net;
using Neon.Retry;
using Neon.Service;
using Neon.Tasks;

using k8s;

namespace NeonClusterManager
{
    public partial class NeonClusterManager : NeonService
    {
        /// <summary>
        /// Handles purging of old <b>logstash</b> and <b>metricbeat</b> Elasticsearch indexes.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public void KibanaSetup()
        {
            Log.LogInfo("Setting up Kibana index patterns.");
            using (var jsonClient = new JsonClient())
            {
                jsonClient.BaseAddress = KubernetesClientConfiguration.IsInCluster() ?
                    this.ServiceMap[NeonServices.Kibana].Endpoints.Default.Uri : new Uri($"http://localhost:{this.ServiceMap[NeonServices.Kibana].Endpoints.Default.Port}");

                Log.LogInfo(jsonClient.BaseAddress.ToString());

                var timeout = TimeSpan.FromMinutes(5);
                var retry = new LinearRetryPolicy(TransientDetector.Http, maxAttempts: 30, retryInterval: TimeSpan.FromSeconds(2));

                // The Kibana API calls below require the [kbn-xsrf] header.

                jsonClient.DefaultRequestHeaders.Add("kbn-xsrf", "true");

                // Ensure that Kibana is ready before we submit any API requests.

                retry.InvokeAsync(
                    async () =>
                    {
                        var response = await jsonClient.GetAsync<dynamic>($"api/status");

                        if (response.status.overall.state != "green")
                        {
                            Log.LogInfo("Kibana is not ready.");
                            throw new TransientException($"Kibana [state={response.status.overall.state}]");
                        }

                    }).Wait();

                // Add the index pattern to Kibana.

                retry.InvokeAsync(
                    async () =>
                    {
                        dynamic indexPattern = new ExpandoObject();
                        dynamic attributes = new ExpandoObject();

                        attributes.title = "logstash-*";
                        attributes.timeFieldName = "@timestamp";

                        indexPattern.attributes = attributes;

                        await jsonClient.PostAsync($"api/saved_objects/index-pattern/logstash-*?overwrite=true", indexPattern);

                    }).Wait();

                // Now we need to save a Kibana config document so that [logstash-*] will be
                // the default index and the timestamp will be displayed as UTC and have a
                // more useful terse format.

                retry.InvokeAsync(
                    async () =>
                    {
                        dynamic setting = new ExpandoObject();

                        setting.value = "logstash-*";
                        await jsonClient.PostAsync($"api/kibana/settings/defaultIndex", setting);

                        setting.value = "HH:mm:ss.SSS MM-DD-YYYY";
                        await jsonClient.PostAsync($"api/kibana/settings/dateFormat", setting);

                        setting.value = "UTC";
                        await jsonClient.PostAsync($"api/kibana/settings/dateFormat:tz", setting);

                    }).Wait();
            }
            Log.LogInfo("Kibana index patterns configured.");
        }
    }
}
