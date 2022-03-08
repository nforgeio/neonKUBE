Neon.Kube.Resources
===================

**INTERNAL USE ONLY:** Defines neonKUBE related custom Kubernetes resources and other useful types.

The custom resources are actually defined by the **Neon.Kube.ResourceDefinitions** project and
the generated CRDs are add to neonKUBE clusters when **neon-cluster-operator** is installed.

This library builds the resources from **Neon.Kube.ResourceDefinitions** via linked source files
and is is published as a nuget package.  **Neon.Kube.ResourceDefinitions** should be referenced
by all non-operator projects.
