Debugging Cheats
---

```
set DEBUG_ORG=ghcr.io/neonkube-dev
set DEBUG_REPO=neon-node-agent
set DEBUG_TAG=neonkube-0.6.0-alpha

neon helm install neon-node-agent --namespace neon-system -f values.yaml --set logLevel=debug --set image.pullPolicy=Always --set image.organization=%DEBUG_ORG% --set image.repository=%DEBUG_REPO% --set image.tag=%DEBUG_TAG% .

neon helm uninstall neon-node-agent --namespace neon-system

neon delete crd nodetasks.neonkube.io

%NC_ROOT%\User\jefflill\install-crds.cmd
```
