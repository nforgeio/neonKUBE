using k8s.Models;
using Microsoft.AspNetCore.Routing;
using Neon.Kube;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neon.Kube.Operator;
using Quartz.Logging;
using Neon.Diagnostics;
using Neon.Common;
using Microsoft.Extensions.Logging;

namespace NeonClusterOperator.Webhooks
{
    public class PodWebhook : IMutationWebhook<V1Pod>
    {
        public ILogger Logger;
        public PodWebhook()
        {
        }
        public PodWebhook(ILogger logger)
        {
            this.Logger = logger;
        }

        public AdmissionOperations Operations => AdmissionOperations.Create;
        
        public V1MutatingWebhookConfiguration WebhookConfiguration { get =>
                new V1MutatingWebhookConfiguration()
                {
                    Metadata = new V1ObjectMeta()
                    {
                        Name = $"{KubeService.NeonClusterOperator}-pod-policy",
                        Annotations = new Dictionary<string, string>()
                        {
                            { "cert-manager.io/inject-ca-from", $"{KubeNamespace.NeonSystem}/{KubeService.NeonClusterOperator}" }
                        }
                    },
                    Webhooks = new List<V1MutatingWebhook>()
                    {
                        new V1MutatingWebhook()
                        {
                            Name = "pod-policy.neonkube.io",
                            Rules = new List<V1RuleWithOperations>()
                            {
                                new V1RuleWithOperations()
                                {
                                    ApiGroups = new string[]{ "" },
                                    ApiVersions = new string[]{ "v1" },
                                    Operations =  new string[]{ "CREATE" },
                                    Resources =  new string[]{ "pods" },
                                    Scope = "*"
                                }
                            },
                            ClientConfig = new Admissionregistrationv1WebhookClientConfig()
                            {
                                Service = new Admissionregistrationv1ServiceReference()
                                {
                                    Name = KubeService.NeonClusterOperator,
                                    NamespaceProperty = KubeNamespace.NeonSystem,
                                    Path = WebhookHelper.CreateEndpoint<V1Pod>(this.GetType(), WebhookType.Mutate)
                                }
                            },
                            AdmissionReviewVersions = new string[] {"v1"},
                            FailurePolicy = "Ignore",
                            SideEffects = "None",
                            TimeoutSeconds = 5
                        }
                    }
                };
        }

        public async Task<MutationResult> CreateAsync(V1Pod entity, bool dryRun)
        {
            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                Logger?.LogInformationEx(() => $"Received request for pod {entity.Namespace()}/{entity.Name()}");

                if (!entity.Metadata.Namespace().StartsWith("neon-"))
                {
                    Logger?.LogInformationEx(() => $"Pod not in neon- namespace.");

                    return MutationResult.NoChanges();
                }

                if (string.IsNullOrEmpty(entity.Spec.PriorityClassName)
                    || entity.Spec.PriorityClassName == PriorityClass.UserMedium.Name)
                {
                    if (entity.Metadata.Labels.ContainsKey("core.goharbor.io/name"))
                    {
                        Logger?.LogInformationEx(() => $"Setting priority class for harbor pod.");

                        entity.Spec.PriorityClassName = PriorityClass.NeonStorage.Name;
                    }
                    else
                    {
                        Logger?.LogInformationEx(() => $"Setting default priority class to neon-min.");

                        entity.Spec.PriorityClassName = PriorityClass.NeonMin.Name;
                    }
                    return MutationResult.Modified(entity);
                }

                return Task.FromResult(MutationResult.NoChanges());
            }
        }
    }
}