//-----------------------------------------------------------------------------
// FILE:	    ContainerImages.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;

namespace NeonImage
{
    /// <summary>
    /// Lists the container images to be prepositioned on node VM images.
    /// </summary>
    public static class ContainerImages
    {
        private const string requiredAsText = @"
bakdata/citus-k8s-membership-manager:v0.3
busybox:latest
calico/cni:v3.16.5
calico/kube-controllers:v3.16.5
calico/node:v3.16.5
calico/pod2daemon-flexvol:v3.16.5
citusdata/citus:9.4.0
coredns/coredns:1.6.2
curlimages/curl:7.70.0
docker.elastic.co/elasticsearch/elasticsearch:7.9.1
docker.elastic.co/kibana/kibana:7.9.1
goharbor/chartmuseum-photon:v2.1.1
goharbor/clair-adapter-photon:v2.1.1
goharbor/clair-photon:v2.1.1
goharbor/harbor-core:v2.1.1
goharbor/harbor-jobservice:v2.1.1
goharbor/harbor-portal:v2.1.1
goharbor/harbor-registryctl:v2.1.1
goharbor/notary-server-photon:v2.1.1
goharbor/notary-signer-photon:v2.1.1
goharbor/registry-photon:v2.1.1
grafana/grafana:7.1.5
istio/coredns-plugin:0.2-istio-1.1
istio/install-cni:1.7.1
istio/operator:1.7.1
istio/pilot:1.7.1
istio/proxyv2:1.7.1
jaegertracing/jaeger-agent:1.19.2
jaegertracing/jaeger-collector:1.19.2
jaegertracing/jaeger-query:1.19.2
jettech/kube-webhook-certgen:v1.0.0
k8s.gcr.io/coredns:1.7.0
k8s.gcr.io/etcd:3.4.13-0
k8s.gcr.io/kube-apiserver:v1.19.5
k8s.gcr.io/kube-controller-manager:v1.19.5
k8s.gcr.io/kube-proxy:v1.19.5
k8s.gcr.io/kube-scheduler:v1.19.5
k8s.gcr.io/pause:3.2
kiwigrid/k8s-sidecar:0.1.151
kubernetesui/dashboard:v2.0.4
kubernetesui/metrics-scraper:v1.0.1
ghcr.io/neonrelease-dev/haproxy:latest
ghcr.io/neonrelease-dev/neon-cluster-manager:latest
ghcr.io/neonrelease-dev/neon-log-collector:latest
ghcr.io/neonrelease-dev/neon-log-host:latest
openebs/admission-server:2.1.0
openebs/cspc-operator-amd64:2.1.0
openebs/cstor-csi-driver:2.1.0
openebs/cstor-istgt:2.1.0
openebs/cstor-pool:2.1.0
openebs/cstor-pool-manager-amd64:2.1.0
openebs/cstor-volume-manager-amd64:2.1.0
openebs/cstor-webhook-amd64:2.1.0
openebs/cvc-operator-amd64:2.1.0
openebs/linux-utils:2.1.0
openebs/m-apiserver:2.1.0
openebs/m-exporter:2.1.0
openebs/node-disk-manager:0.8.1
openebs/node-disk-operator:0.8.1
openebs/openebs-k8s-provisioner:2.1.0
openebs/provisioner-localpv:2.1.0
openebs/snapshot-controller:2.1.0
openebs/snapshot-provisioner:2.1.0
quay.io/coreos/configmap-reload:v0.0.1
quay.io/coreos/kube-state-metrics:v1.7.1
quay.io/coreos/prometheus-config-reloader:v0.32.0
quay.io/coreos/prometheus-operator:v0.32.0
quay.io/cortexproject/cortex:v1.5.0
quay.io/k8scsi/csi-attacher:v2.0.0
quay.io/k8scsi/csi-cluster-driver-registrar:v1.0.1
quay.io/k8scsi/csi-node-driver-registrar:v1.0.1
quay.io/k8scsi/csi-provisioner:v1.6.0
quay.io/k8scsi/csi-resizer:v0.4.0
quay.io/k8scsi/csi-snapshotter:v2.0.1
quay.io/k8scsi/snapshot-controller:v2.0.1
quay.io/kiali/kiali:v1.28.0
quay.io/kiali/kiali-operator:v1.27.0
quay.io/kubernetes_incubator/nfs-provisioner:v2.3.0
quay.io/prometheus/alertmanager:v0.19.0
quay.io/prometheus/node-exporter:v0.18.0
quay.io/prometheus/prometheus:v2.12.0
redis:6.0.7-alpine
squareup/ghostunnel:v1.4.1
wrouesnel/postgres_exporter:v0.5.1
";

        private const string requiredAsText2 = @"
busybox:latest
calico/cni:v3.16.5
calico/kube-controllers:v3.16.5
calico/node:v3.16.5
calico/pod2daemon-flexvol:v3.16.5
coredns/coredns:1.6.2
curlimages/curl:7.70.0
ghcr.io/neonrelease-dev/bakdata-citus-membership-manager:0.1.0-alpha
ghcr.io/neonrelease-dev/blacktop-elasticsearch:latest
ghcr.io/neonrelease-dev/blacktop-kibana:latest
ghcr.io/neonrelease-dev/citusdata-citus:0.1.0-alpha
ghcr.io/neonrelease-dev/coredns:1.7.0
ghcr.io/neonrelease-dev/cortexproject-cortex:0.1.0-alpha
ghcr.io/neonrelease-dev/etcd:3.4.13-0
ghcr.io/neonrelease-dev/haproxy:0.1.0-alpha
ghcr.io/neonrelease-dev/install-cni:0.1.0-alpha
ghcr.io/neonrelease-dev/k8scsi-csi-resizer:0.1.0-alpha
ghcr.io/neonrelease-dev/k8scsi-csi-snapshotter:0.1.0-alpha
ghcr.io/neonrelease-dev/k8scsi-snapshot-controller:0.1.0-alpha
ghcr.io/neonrelease-dev/kube-apiserver:v1.19.5
ghcr.io/neonrelease-dev/kube-controller-manager:v1.19.5
ghcr.io/neonrelease-dev/kube-proxy:v1.19.5
ghcr.io/neonrelease-dev/kube-scheduler:v1.19.5
ghcr.io/neonrelease-dev/neon-cluster-manager:0.1.0-alpha
ghcr.io/neonrelease-dev/neon-log-collector:0.1.0-alpha
ghcr.io/neonrelease-dev/neon-log-host:0.1.0-alpha
ghcr.io/neonrelease-dev/openebs-cspc-operator-amd64:0.1.0-alpha
ghcr.io/neonrelease-dev/openebs-cstor-istgt:0.1.0-alpha
ghcr.io/neonrelease-dev/openebs-cstor-pool-manager:0.1.0-alpha
ghcr.io/neonrelease-dev/openebs-cstor-pool:0.1.0-alpha
ghcr.io/neonrelease-dev/openebs-cvc-operator-amd64:0.1.0-alpha
ghcr.io/neonrelease-dev/openebs-linux-utils:0.1.0-alpha
ghcr.io/neonrelease-dev/openebs-m-apiserver:0.1.0-alpha
ghcr.io/neonrelease-dev/openebs-m-exporter:0.1.0-alpha
ghcr.io/neonrelease-dev/openebs-provisioner-localpv:0.1.0-alpha
ghcr.io/neonrelease-dev/openebs-snapshot-controller:0.1.0-alpha
ghcr.io/neonrelease-dev/openebs-snapshot-provisioner:0.1.0-alpha
ghcr.io/neonrelease-dev/pause:3.2
ghcr.io/neonrelease-dev/pilot:0.1.0-alpha
ghcr.io/neonrelease-dev/proxyv2:0.1.0-alpha
goharbor/chartmuseum-photon:v2.1.1
goharbor/clair-adapter-photon:v2.1.1
goharbor/clair-photon:v2.1.1
goharbor/harbor-core:v2.1.1
goharbor/harbor-jobservice:v2.1.1
goharbor/harbor-portal:v2.1.1
goharbor/harbor-registryctl:v2.1.1
goharbor/nginx-photon:v2.1.1
goharbor/notary-server-photon:v2.1.1
goharbor/notary-signer-photon:v2.1.1
goharbor/registry-photon:v2.1.1
grafana/grafana:7.1.5
istio/coredns-plugin:0.2-istio-1.1
istio/operator:1.7.6
jaegertracing/jaeger-agent:1.19.2
jaegertracing/jaeger-collector:1.19.2
jaegertracing/jaeger-query:1.19.2
jettech/kube-webhook-certgen:v1.0.0
kiwigrid/k8s-sidecar:0.1.151
kubernetesui/dashboard:v2.0.4
kubernetesui/metrics-scraper:v1.0.1
openebs/admission-server:2.1.0
openebs/cstor-csi-driver:2.1.0
openebs/cstor-volume-manager-amd64:2.1.0
openebs/cstor-webhook-amd64:2.1.0
openebs/node-disk-manager:0.8.1
openebs/node-disk-operator:0.8.1
openebs/openebs-k8s-provisioner:2.1.0
quay.io/coreos/configmap-reload:v0.0.1
quay.io/coreos/kube-state-metrics:v1.7.1
quay.io/coreos/prometheus-config-reloader:v0.32.0
quay.io/coreos/prometheus-operator:v0.32.0
quay.io/k8scsi/csi-attacher:v2.0.0
quay.io/k8scsi/csi-cluster-driver-registrar:v1.0.1
quay.io/k8scsi/csi-node-driver-registrar:v1.0.1
quay.io/k8scsi/csi-provisioner:v1.6.0
quay.io/kiali/kiali-operator:v1.27.0
quay.io/kiali/kiali:v1.27.0
quay.io/kubernetes_incubator/nfs-provisioner:v2.3.0
quay.io/prometheus/alertmanager:v0.19.0
quay.io/prometheus/node-exporter:v0.18.0
quay.io/prometheus/prometheus:v2.12.0
redis:6.0.7-alpine
squareup/ghostunnel:v1.4.1
wrouesnel/postgres_exporter:v0.5.1
";

        private const string requiredAsText3 = @"
ghcr.io/neonrelease-dev/bakdata-citus-membership-manager:0.1.0-alpha
ghcr.io/neonrelease-dev/blacktop-elasticsearch:0.1.0-alpha
ghcr.io/neonrelease-dev/blacktop-kibana:0.1.0-alpha
ghcr.io/neonrelease-dev/busybox:0.1.0-alpha
ghcr.io/neonrelease-dev/calico-cni:0.1.0-alpha
ghcr.io/neonrelease-dev/calico-kube-controllers:0.1.0-alpha
ghcr.io/neonrelease-dev/calico-node:0.1.0-alpha
ghcr.io/neonrelease-dev/calico-pod2daemon-flexvol:0.1.0-alpha
ghcr.io/neonrelease-dev/citusdata-citus:0.1.0-alpha
ghcr.io/neonrelease-dev/coredns:0.1.0-alpha
ghcr.io/neonrelease-dev/coredns-coredns:0.1.0-alpha
ghcr.io/neonrelease-dev/coredns-plugin:0.1.0-alpha
ghcr.io/neonrelease-dev/coreos-configmap-reload:0.1.0-alpha
ghcr.io/neonrelease-dev/coreos-kube-state-metrics:0.1.0-alpha
ghcr.io/neonrelease-dev/coreos-prometheus-config-reloader:0.1.0-alpha
ghcr.io/neonrelease-dev/coreos-prometheus-operator:0.1.0-alpha
ghcr.io/neonrelease-dev/cortexproject-cortex:0.1.0-alpha
ghcr.io/neonrelease-dev/curlimages-curl:0.1.0-alpha
ghcr.io/neonrelease-dev/etcd:0.1.0-alpha
ghcr.io/neonrelease-dev/goharbor-chartmuseum-photon:0.1.0-alpha
ghcr.io/neonrelease-dev/goharbor-clair-adapter-photon:0.1.0-alpha
ghcr.io/neonrelease-dev/goharbor-clair-photon:0.1.0-alpha
ghcr.io/neonrelease-dev/goharbor-harbor-core:0.1.0-alpha
ghcr.io/neonrelease-dev/goharbor-harbor-jobservice:0.1.0-alpha
ghcr.io/neonrelease-dev/goharbor-harbor-portal:0.1.0-alpha
ghcr.io/neonrelease-dev/goharbor-harbor-registryctl:0.1.0-alpha
ghcr.io/neonrelease-dev/goharbor-notary-server-photon:0.1.0-alpha
ghcr.io/neonrelease-dev/goharbor-registry-photon:0.1.0-alpha
ghcr.io/neonrelease-dev/goharbor-signer-photon:0.1.0-alpha
ghcr.io/neonrelease-dev/grafana:0.1.0-alpha
ghcr.io/neonrelease-dev/install-cni:0.1.0-alpha
ghcr.io/neonrelease-dev/jaegertracing-jaeger-agent:0.1.0-alpha
ghcr.io/neonrelease-dev/jaegertracing-jaeger-collector:0.1.0-alpha
ghcr.io/neonrelease-dev/jaegertracing-jaeger-query:0.1.0-alpha
ghcr.io/neonrelease-dev/jettech-kube-webhook-certgen:0.1.0-alpha
ghcr.io/neonrelease-dev/k8scsi-csi-attacher:0.1.0-alpha
ghcr.io/neonrelease-dev/k8scsi-csi-cluster-driver-registrar:0.1.0-alpha
ghcr.io/neonrelease-dev/k8scsi-csi-node-driver-registrar:0.1.0-alpha
ghcr.io/neonrelease-dev/k8scsi-csi-provisioner:0.1.0-alpha
ghcr.io/neonrelease-dev/k8scsi-csi-resizer:0.1.0-alpha
ghcr.io/neonrelease-dev/k8scsi-csi-snapshotter:0.1.0-alpha
ghcr.io/neonrelease-dev/k8scsi-snapshot-controller:0.1.0-alpha
ghcr.io/neonrelease-dev/kiali-kiali:0.1.0-alpha
ghcr.io/neonrelease-dev/kiali-kiali-operator:0.1.0-alpha
ghcr.io/neonrelease-dev/kiwigrid-sidecar:0.1.0-alpha
ghcr.io/neonrelease-dev/kube-apiserver:0.1.0-alpha
ghcr.io/neonrelease-dev/kube-controller-manager:0.1.0-alpha
ghcr.io/neonrelease-dev/kube-proxy:0.1.0-alpha
ghcr.io/neonrelease-dev/kubernetes_incubator-nfs-provisioner:0.1.0-alpha
ghcr.io/neonrelease-dev/kubernetesui-dashboard:0.1.0-alpha
ghcr.io/neonrelease-dev/kubernetesui-metrics-scraper:0.1.0-alpha
ghcr.io/neonrelease-dev/kube-scheduler:0.1.0-alpha
ghcr.io/neonrelease-dev/openebs-admission-server:0.1.0-alpha
ghcr.io/neonrelease-dev/openebs-cspc-operator-amd64:0.1.0-alpha
ghcr.io/neonrelease-dev/openebs-cstor-base:0.1.0-alpha
ghcr.io/neonrelease-dev/openebs-cstor-istgt:0.1.0-alpha
ghcr.io/neonrelease-dev/openebs-cstor-pool:0.1.0-alpha
ghcr.io/neonrelease-dev/openebs-cstor-pool-manager:0.1.0-alpha
ghcr.io/neonrelease-dev/openebs-cvc-operator-amd64:0.1.0-alpha
ghcr.io/neonrelease-dev/openebs-linux-utils:0.1.0-alpha
ghcr.io/neonrelease-dev/openebs-m-apiserver:0.1.0-alpha
ghcr.io/neonrelease-dev/openebs-m-exporter:0.1.0-alpha
ghcr.io/neonrelease-dev/openebs-provisioner-localpv:0.1.0-alpha
ghcr.io/neonrelease-dev/openebs-snapshot-controller:0.1.0-alpha
ghcr.io/neonrelease-dev/openebs-snapshot-provisioner:0.1.0-alpha
ghcr.io/neonrelease-dev/operator:0.1.0-alpha
ghcr.io/neonrelease-dev/pause:0.1.0-alpha
ghcr.io/neonrelease-dev/pilot:0.1.0-alpha
ghcr.io/neonrelease-dev/prometheus-alertmanager:0.1.0-alpha
ghcr.io/neonrelease-dev/prometheus-node-exporter:0.1.0-alpha
ghcr.io/neonrelease-dev/prometheus-prometheus:0.1.0-alpha
ghcr.io/neonrelease-dev/proxyv2:0.1.0-alpha
ghcr.io/neonrelease-dev/redis:0.1.0-alpha
ghcr.io/neonrelease-dev/squareup-ghostunnel:0.1.0-alpha
ghcr.io/neonrelease-dev/wrouesnel-postgres-exporter:0.1.0-alpha
";

        /// <summary>
        /// Static constructor.
        /// </summary>
        static ContainerImages()
        {
            using (var reader = new StringReader(requiredAsText3))
            {
                foreach (var line in reader.Lines())
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        Required.Add(line.Trim());
                    }
                }
            }
        }

        /// <summary>
        /// Returns the list of required container images.
        /// </summary>
        public static List<string> Required { get; } = new List<string>();
    }
}
