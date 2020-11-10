Neon.SSH
========
This namespace includes the `LinuxSshProxy` and related classes that wrap and extend the base SSH.NET library clients with additional support for managing remote Linux machines via SSH including executing commands, scripts, uploading/downloading files, and performing idempotent operations.  Remote command executions and their results can also be logged locally via a TextWriter (using a completely non-standard but still useful logging format).

The other major type is `CommandBundle`.  Command bundles provide a way to upload a script or executable to a temporary working directory and then run the script or program in the context of the working directory so the script or program will have access to the files.  Command  bundle executions can also tolerate transient network disconnections.
 
NOTE: This package has been tested against remote machines running Ubuntu 18.04+ and will probably run fine on many other Debian-based distributions.  RedHat and other non-Debian distributions probably won't be compatible.
