Notes
-----
This project holds the Docker files and Powershell scripts for building the neonKUBE 
container images.  Use the **publish.ps1** script to publish multiple images (controlled
by optional switch parameters) or you can CD into the individual image directories and
execute the **publish.ps1** script there to build and publish just that image.

Container images are tagged like `neonkube-0.3.0-alpha` by default, where the semantic 
version is specified by **KubeVersions.NeonKube** in `$/neonKUBE/Lib/Neon.Kube/KubeVersions.cs`.

The image tags can be overridden for development and testing purposes by setting a temporary
environment variable like:

```
set NEON_CONTAINER_TAG_OVERRIDE=jeff
```

and then rebuilding the image and then installing the Helm chart like:

```
neon helm install --set image.tag=%NEON_CONTAINER_TAG_OVERRIDE% --set image.pullPolicy=Always --set logLevel=debug NAME .
```

Doing this allows developers to push development images to the container repository for
testing without impacting the images in use by other developers.

We don't currently have a comprehensive isolation mechanism, so developers will need to
manually specify the temporary image tag as a value when deploying Helm charts while
debugging.
