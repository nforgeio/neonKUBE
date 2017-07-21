#------------------------------------------------------------------------------
# FILE:         Dockerfile.template
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# This image is the default implementation of the [neon-proxy-vault] service responsible
# for forwarding requests to the HashiCorp Vault instances running on the cluster's
# manager nodes.

FROM        neoncluster/haproxy
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM

COPY docker-entrypoint.sh   /
COPY check-vault.sh         /
COPY _common/*.sh           /

RUN chmod 700 /*.sh \
    && apk add --update curl jq

#ENTRYPOINT ["sleep","10000"]
ENTRYPOINT ["/tini", "-g", "--", "/docker-entrypoint.sh"]
