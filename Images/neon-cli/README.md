# Image Tags

Production **neon-cli** images are tagged with the version of the tool.

From time-to-time you may see images tagged like `:BRANCH-*` where **BRANCH** identifies the Git source branch where the image was built from.  These images are used for internal development purposes only and **should not be used production** as they may not actually work and may also be removed or updated at any time.

# Description

This image includes the **neon** tool designed to deploy and manage a neonHIVE to dedicated servers or to Microsoft Azure, Amazon AWS, or the Google Cloud.

This is intended to work in conjunction with a wrapper tool deployed to the operator's workstation.  This wrapper tool is also called **neon** and is in fact, compiled from the same code base.  The wrapper tool simply uses Docker to invoke the image passing thru the command line options (and packaging files for some commands).

The **neon** wrapper tool is designed to work on Windows, Linux as Apple OSX workstations. 
