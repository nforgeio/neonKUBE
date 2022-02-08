# etcd
Helm chart for coreos etcd 3.4+

## Overview

This is a chart based on the incubator chart from helm/charts. Dusted off to function with etcd 3.4 and updated for helm3. It also
adds support for:
* Restore from snapshot
* CronJob to make snapshots
* Prometheus operator support

## Configuration

The following table lists the configurable parameters of the etcd chart and their default values.

| Parameter                           | Description                          | Default                                            |
| ----------------------------------- | ------------------------------------ | -------------------------------------------------- |
| `image.repository`                  | Container image repository           | `quay.io/coreos/etcd`                              |
| `image.tag`                         | Container image tag                  | `v3.4.15`                                          |
| `image.pullPolicy`                  | Container pull policy                | `IfNotPresent`                                     |
| `replicas`                          | k8s statefulset replicas             | `3`                                                |
| `resources`                         | container required resources         | `{}`                                               |
| `clientPort`                        | k8s service port                     | `2379`                                             |
| `peerPorts`                         | Container listening port             | `2380`                                             |
| `storage`                           | Persistent volume size               | `1Gi`                                              |
| `storageClass`                      | Persistent volume storage class      | `anything`                                         |
| `affinity`                          | affinity settings for pod assignment | `{}`                                               |
| `nodeSelector`                      | Node labels for pod assignment       | `{}`                                               |
| `tolerations`                       | Toleration labels for pod assignment | `[]`                                               |
| `extraEnv`                          | Optional environment variables       | `[]`                                               |
| `memoryMode`                        | Using memory as backend storage      | `false`                                            |
| `auth.client.enableAuthentication`  | Enables host authentication using TLS certificates. Existing secret is required.    | `false` |
| `auth.client.secureTransport`       | Enables encryption of client communication using TLS certificates | `false` |
| `auth.peer.useAutoTLS`              | Automatically create the TLS certificates | `false` |
| `auth.peer.secureTransport`         | Enables encryption peer communication using TLS certificates **(At the moment works only with Auto TLS)** | `false` |
| `auth.peer.enableAuthentication`     | Enables host authentication using TLS certificates. Existing secret required | `false` |
| `serviceAccount.create`              | Create serviceaccount | `true` |
| `podMonitor.enabled`                 | Enable podMonitor | `false` |
| `prometheusRules.enabled`            | Enable prometheusRules | `false` |
| `snapshot.restore.enabled`           | Enabled restore from snapshot | false |
| `snapshot.restore.claimName`         | PVC claim name where snapshot is stored | `""` |
| `snapshot.restore.fileName`          | Snapshot filename to restore from on PVC | `""` |
| `snapshot.backup.enabled`            | Enable CronJob to make snapshots and save to PVC | `""` |
| `snapshot.backup.schedule`           | Snapshot schedule | `0/30 * * * *` |
| `snapshot.backup.historyLimit`        | Snapshot job history | `1` |
| `snapshot.backup.snapshotHistoryLimit` | Snapashot file history limit | `1` |
| `snapshot.backup.claimName`          | Existing claim name name | `""` |
| `snapshot.backup.size`               | Create pvc with size | `10Gi` |
| `snapshot.backup.storageClassName`   | Create pvc storageclass | `default` |
| `snapshot.backup.resources`          | Snapshot CronJob resources | `{}` |

Specify each parameter using the `--set key=value[,key=value]` argument to `helm install`.

Alternatively, a YAML file that specifies the values for the parameters can be provided while installing the chart. For example,

```bash
$ helm install --name my-release -f values.yaml path/to/etcd
```
> **Tip**: You can use the default [values.yaml](values.yaml)
# To install the chart with secure transport enabled
First you must create a secret which would contain the client certificates: cert, key and the CA which was to used to sign them.
Create the secret using this command:
```bash
$ kubectl create secret generic etcd-client-certs --from-file=ca.crt=path/to/ca.crt --from-file=cert.pem=path/to/cert.pem --from-file=key.pem=path/to/key.pem
```
Deploy the chart with the following flags enabled:
```bash
$ helm install --name my-release --set auth.client.secureTransport=true --set auth.client.enableAuthentication=true --set auth.client.existingSecret=etcd-client-certs --set auth.peer.useAutoTLS=true path/to/etcd
```
Reference to how to generate the needed certificate:
> Ref: https://coreos.com/os/docs/latest/generate-self-signed-certificates.html

# Deep dive

## Cluster Health

```
$ for i in <0..n>; do kubectl exec <release-podname-$i> -- sh -c 'etcdctl endpoint health'; done
```
eg.
```
$ for i in {0..2}; do kubectl -n ops exec named-lynx-etcd-$i -- sh -c 'etcdctl endpoint health'; done
127.0.0.1:2379 is healthy: successfully committed proposal: took = 2.880348ms
127.0.0.1:2379 is healthy: successfully committed proposal: took = 2.616944ms
127.0.0.1:2379 is healthy: successfully committed proposal: took = 3.2329ms
```

## Failover

If any etcd member fails it gets re-joined eventually.
You can test the scenario by killing process of one of the replicas:

```shell
$ ps aux | grep etcd-1
$ kill -9 ETCD_1_PID
```

```shell
$ kubectl get pods -l "app.kubernetes.io/instance=${RELEASE-NAME},app.kubernetes.io/name=etcd"
NAME                 READY     STATUS        RESTARTS   AGE
etcd-0               1/1       Running       0          54s
etcd-2               1/1       Running       0          51s
```

After a while:

```shell
$ kubectl get pods -l "app.kubernetes.io/instance=${RELEASE-NAME},app.kubernetes.io/name=etcd"
NAME                 READY     STATUS    RESTARTS   AGE
etcd-0               1/1       Running   0          1m
etcd-1               1/1       Running   0          20s
etcd-2               1/1       Running   0          1m
```

You can check state of re-joining from ``etcd-1``'s logs:

```shell
$ kubectl logs etcd-1
Waiting for etcd-0.etcd to come up
Waiting for etcd-1.etcd to come up
ping: bad address 'etcd-1.etcd'
Waiting for etcd-1.etcd to come up
Waiting for etcd-2.etcd to come up
Re-joining etcd member
Updated member with ID 7fd61f3f79d97779 in cluster
2016-06-20 11:04:14.962169 I | etcdmain: etcd Version: 2.2.5
2016-06-20 11:04:14.962287 I | etcdmain: Git SHA: bc9ddf2
...
```

## Scaling using kubectl

This is for reference. Scaling should be managed by `helm upgrade`

The etcd cluster can be scale up by running ``kubectl patch`` or ``kubectl edit``. For instance,

```sh
$ kubectl get pods -l "app.kubernetes.io/instance=${RELEASE-NAME},app.kubernetes.io/name=etcd"
NAME      READY     STATUS    RESTARTS   AGE
etcd-0    1/1       Running   0          7m
etcd-1    1/1       Running   0          7m
etcd-2    1/1       Running   0          6m

$ kubectl patch statefulset/etcd -p '{"spec":{"replicas": 5}}'
"etcd" patched

$ kubectl get pods -l "app.kubernetes.io/instance=${RELEASE-NAME},app.kubernetes.io/name=etcd"
NAME      READY     STATUS    RESTARTS   AGE
etcd-0    1/1       Running   0          8m
etcd-1    1/1       Running   0          8m
etcd-2    1/1       Running   0          8m
etcd-3    1/1       Running   0          4s
etcd-4    1/1       Running   0          1s
```

Scaling-down is similar. For instance, changing the number of replicas to ``4``:

```sh
$ kubectl edit statefulset/etcd
statefulset "etcd" edited

$ kubectl get pods -l "app.kubernetes.io/instance=${RELEASE-NAME},app.kubernetes.io/name=etcd"
NAME      READY     STATUS    RESTARTS   AGE
etcd-0    1/1       Running   0          8m
etcd-1    1/1       Running   0          8m
etcd-2    1/1       Running   0          8m
etcd-3    1/1       Running   0          4s
```

Once a replica is terminated (either by running ``kubectl delete pod etcd-ID`` or scaling down),
content of ``/var/run/etcd/`` directory is cleaned up.
If any of the etcd pods restarts (e.g. caused by etcd failure or any other),
the directory is kept untouched so the pod can recover from the failure.
