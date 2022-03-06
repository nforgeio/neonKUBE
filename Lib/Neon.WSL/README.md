Neon.WSL
========

**INTERNAL USE ONLY:** This library includes some WSL2 related helper classes intended for internal use and is not generally supported at this time.

### Build an Ubuntu-20.04 WSL2 image

We require an Ubuntu-20.04 WSL2 image for builds and unit testing to be located at:

    https://neon-public.s3.us-west-2.amazonaws.com/downloads/ubuntu-20.04.tar

This file will be gzipped and will have [Content-Encoding=gzip] and will have 
SUDO password prompting disabled.

**Steps:** Note that you must be a maintainer to upload the image

1. Install Ubuntu 20.04 WSL from the Windows Store, setting the user 
   credentials to: **sysadmin/sysadmin0000*

2. Make the distro support version 2 and be the default.  In a Windows command window:

   ```
   wsl --distribution Ubuntu-20.04 --set-version 2
   wsl --distribution Ubuntu-20.04 --set-default
   ```

3. Disable SUDO password prompting:

   a. Start Bash in the distro:

      ```
      wsl ----distribution Ubuntu-20.04 -user sysadmin
      ``

   b. At the Bash prompt, execute this command (you'll need to enter **sysadmin0000**
      as the password):

      ```
      echo "%${USER} ALL=(ALL) NOPASSWD:ALL" | sudo EDITOR='tee ' visudo --quiet --file=/etc/sudoers.d/passwordless-sudo
      ```

    c. Exit Bash: `exit`

4. Export the WSL2 image as a TAR file, gzip it, and then upload to S3:

    ```
    mkdir C:\Temp
    wsl --terminate Ubuntu-20.04
    del C:\temp\ubuntu-20.04.tar.gz C:\Temp\ubuntu-20.04.tar
    wsl --export Ubuntu-20.04 C:\temp\ubuntu-20.04.tar
    pigz --best --blocksize 512 C:\Temp\ubuntu-20.04.tar
    ren C:\Temp\ubuntu-20.04.tar.gz ubuntu-20.04.tar

    s3-upload C:\Temp\ubuntu-20.04.tar https://neon-public.s3.us-west-2.amazonaws.com/downloads/ubuntu-20.04.tar -gzip -publicReadAccess

    del C:\Temp\ubuntu-20.04.tar.gz C:\temp\ubuntu-20.04.tar
    
    ```

