Notes
-----
This project holds the Docker files and Powershell scripts for building the NeonKUBE 
container images.  Use the **publish.ps1** script to publish multiple images (controlled
by optional switch parameters) or you can CD into the individual image directories and
execute the **publish.ps1** script there to build and publish just that image.

Container images are tagged like `neonkube-0.3.0-alpha` for NeonKUBE release branches
like **release-***, where the semantic version is specified by **KubeVersion.NeonKube** 
in `$/NeonKUBE/Lib/Neon.Kube/KubeVersion.cs`.

For non-release NeonKUBE branches, images will be tagged like: `neonkube-0.3.0-alpha.BRANCH`
