# Image Tags

CFSSL images are tagged with the language release version plus the image build date and the latest production image will be tagged with `:latest`.

From time-to-time you may see images tagged like `:BRANCH-*` where **BRANCH** identifies the Git source branch where the image was built from.  These images are used for internal development purposes only and **should not be used production** as they may not actually work and may also be removed or updated at any time.

# Description

This image includes the [CloudFlare CFSSL Tools](https://github.com/cloudflare/cfssl) that can be used to manage TLS certificates and certificate authorities:

To use this image, you'll need to mount the working directory with your certificate related files to `/ca` within the container and then specify the specific tool and any arguments.  The image changes the current directory to `/ca` before executing the command.

Here's an example:

&nbsp;&nbsp;&nbsp;&nbsp;`docker run --rm -v WORKING-DIR:/ca nhive/cfssl cfssl ...`
