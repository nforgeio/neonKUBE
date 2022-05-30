Debugging Cheats
---

```
neon helm install neon-node-agent --namespace neon-system -f values.yaml --set logLevel=debug --set image.organization=ghcr.io/neonkube-dev --set image.repository=neon-node-agent --set image.pullPolicy=Always --set image.tag=neonkube-0.6.0-alpha .

neon helm uninstall neon-node-agent --namespace neon-system

neon delete crd nodetasks.neonkube.io

%NC_ROOT%\User\jefflill\install-crds.cmd
```
