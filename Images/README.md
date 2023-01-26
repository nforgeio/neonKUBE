Notes
-----
This project holds the Docker files and Powershell scripts for building the neonKUBE 
container images.  Use the **publish.ps1** script to publish multiple images (controlled
by optional switch parameters) or you can CD into the individual image directories and
execute the **publish.ps1** script there to build and publish just that image.

Container images are tagged like `neonkube-0.3.0-alpha` for neonKUBE release branches
like **release-***, where the semantic version is specified by **KubeVersions.NeonKube** 
in `$/neonKUBE/Lib/Neon.Kube/KubeVersions.cs`.

For non-release neonKUBE branches, images will be tagged like: `neonkube-0.3.0-alpha.BRANCH`
