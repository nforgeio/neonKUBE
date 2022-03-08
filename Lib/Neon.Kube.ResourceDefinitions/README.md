Neon.Kube.ResourceDefinitions
=============================

**INTERNAL USE ONLY:** Defines neonKUBE related custom Kubernetes resources and other useful types.
The CRDs are installed with **neon-cluster-operator** and may be referenced by other operators, services 
and applications.

The **Neon.Kube.ResourceDefinitions** library defines the custom resources implemented by neonKUBE 
operators and also generates the CRDs for these resources.  These resources will also be defined in 
the **Neon.Kube.Resources** library:

* **Neon.Kube.ResourceDefinitions** includes the ASP.NET stack and is really suitable only for use
  by operator implementations.  All other projects should reference **Neon.Kube.Resources** instead
  to avoid referencing the ASP.NET stack.


**Note:**

The CRDs generated when this project builds are installed in neonKUBE clusters by the **neon-cluster-operator**.

### Details:

While this issue is pending over at the **dotnet-operator-sdk** repo, let's investigate a workaround:

https://github.com/buehler/dotnet-operator-sdk/issues/362
https://github.com/nforgeio/neonKUBE/issues/1481

The problem is essentially this:

* We use the [dotnet-operator-sdk](https://github.com/buehler/dotnet-operator-sdk) to generate the CRDs for 
our custom resources from .NET classes defined in the **Neon.Kube.Resources** and **Neon.Kube.ResourceDefinitions** 
libraries:

* **dotnet-operator-sdk** is currently a single library that depends on ASP.NET so we're pulling all of that 
into **Neon.Kube.Resources** just to decorate our custom classes with the necessary metadata attributes.

* This is a problem because ASP.NET ends up also being pulled into **Neon.Kube.Setup** which is also 
  referenced by **neon-cli** and **neon-desktop**, bloating these apps significantly (for no useful reason).

* **dotnet-operator-sdk** targets .NET 6 only but we'd prefer our custom resources to target .NET Standard 2.0.

The workaround involves creating a separate library **Neon.Kube.ResourceDefinitions** where the custom 
resources are defined and the CRDs are generated and then have the **Neon.Kube.Resources** reference and
build the resource classes from the definitions project.

We'd use conditional compilation to ignore the **dotnet-operator-sdk** metadata attributes in the **Neon.Kube.Resources** project.

This solution is actually pretty good and I think we could live with this over the long term.  It effectively
decouples our custom resource classes from **dotnet-operator-sdk** and .NET 6.
