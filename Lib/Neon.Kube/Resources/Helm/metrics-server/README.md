## Calico Helm Chart
This is a helm chart installs [Calico Networking](https://www.projectcalico.org/). 

### Installing the Chart

```bash
Add installation instructions once charts repo is created
```

### Configuration
The following table lists the configurable parameters for this chart. Required values must be set based on the deployment for the chart to work. 

| Parameter  | Description | Type | Default | Required |
| ------------- | ------------- | ------------- | ------------- | ------------- |
| `datastore`  |  [Datastore for Calico](https://docs.projectcalico.org/getting-started/kubernetes/hardway/the-calico-datastore) | `string` | `kubernetes` | yes |
| `vxlan`  |  Enables VxLAN. Read more about [overlaynetworking](https://docs.projectcalico.org/networking/vxlan-ipip)  | `boolean` | `true` | no  |
| `network`  | Network to use. Read more about [determine best networking option](https://docs.projectcalico.org/networking/determine-best-networking) | `calico` | yes |
| `ipam`  | IP address management for Calico. Read more about [IPAM](https://docs.projectcalico.org/networking/get-started-ip-addresses) | `string` | `calico-ipam`  | yes  |
| `bpf` | Variable to enable eBPF. Read more about [eBPF](https://docs.projectcalico.org/maintenance/enabling-bpf) | `boolean` | `` | no |
| `etcd.endpoints` | Calico datastore endpoints | `string` | `` | no( Required if using etcd datastore) |
| `etcd.tls.ca` |The root certificate of the certificate authority (CA) that issued the etcd server certificate | `string` | ` ` | no( Required if using etcd datastore) |
| `etcd.tls.crt` | The client certificate issued to Calico for datastore connectivity | `string` | `` | no( Required if using etcd datastore) |
| `etcd.tls.key` | Private key matching client certificate| `string` | `` | no( Required if using etcd datastore) |
| `typha.enabled`  | Enables or disabled Typha. Read more about [Typha](https://docs.projectcalico.org/reference/typha/) | `string` | `false` | yes |
| `typha.image`  | Image name of typha | `string` | `calico/typha` | no (Required if typha is enabled) |
| `typha.tag`  | Tag value of Typha  | `string` | `v3.8.0` | no(Required if typha is enabled) |
| `typha.env`  | Additional env variables. Read more about [Typha env variables](https://docs.projectcalico.org/reference/typha/configuration) | `dict` | {} | no(Required if typha is enabled) |
| `cni.image`  | Calico CNI image name  | `string` | `docker.io/calico/cni` | yes |
| `cni.tag`  | Calico CNI image tag  | `string` | `v3.17.1` | yes |
| `cni.env`  | Calico CNI additional env variables | `dict` |  {} | no |
| `node.image`  | Calico Node image name | `string`| `docker.io/calico/node` | yes |
| `node.tag`  | Calico Node image tag  | `string` | `v3.17.1` | yes |
| `node.env`  | Calico Node additional env variables. Read more about [node env variables](https://docs.projectcalico.org/reference/node/configuration) | `dict` |  {} | no |
| `flannel_migration` | Variable that enables migration from Flannel. Read more about [Flannel Migration](https://docs.projectcalico.org/getting-started/kubernetes/flannel/migration-from-flannel) | `boolean` | `false` | no |
| `flannel.image`  | Flannel image name | `string` | `quay.io/coreos/flannel` | no(Required if Flannel is being deployed) |
| `flannel.tag`  | Flannel image tag  | `string` | `v0.12.0` | no(Required if Flannel is being deployed) |
| `flannel.env`  | Flannel additional env variables | `dict` | {} | no |
| `flexvol.image`  |  Flexvol Image Name | `string` | `docker.io/calico/pod2daemon-flexvol` | yes |
| `flexvol.tag`  |  Flexvol Image tag | `string` | `v3.17.1` | yes |
| `kubeControllers.image`  | Calico KubeControllers image name. Read more about [Calico KubeControllers](https://docs.projectcalico.org/reference/kube-controllers/configuration) | `string` | `docker.io/calico/kube-controllers` | yes |
| `kubeControllers.tag`  | Calico KubeControllers image tag | `string` | `v3.17.1` | yes |
| `kubeControllers.serviceAccount.create`  | Calico KubeControllers ServiceAccount creation  | `true` | `bollean`| `true` |yes |
| `kubeControllers.serviceAccount.name`  | Calico KubeControllers ServiceAccount name  | `string` | `calico-kube-controllers` | yes |
| `kubeControllers.serviceAccount.rbac`  | Calico KubeControllers ServiceAccount rbac permissions  | `boolean` | `true` | yes |
| `kubeControllers.annotations`  | Calico KubeControllers ServiceAccount annotations  | `dict` | {} | no |
| `nodeName.serviceAccount.create`  | Depending on the network choosen, the chart creates a SA account with respective name | `boolean` | `true` | yes |
| `nodeName.serviceAccount.rbac`  | Calico network SA RBAC permissions | `boolean` | `true` | yes |
| `nodeName.annotations`  | Calico network SA annotations | `dict` | {} | no |
