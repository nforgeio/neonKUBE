#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         onconfigchange.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# This is called when the container starts and also whenever the main script
# detects that the proxy configuration has changed.
#
# We'll detect whether we're starting HAProxy for the first time or an existing 
# instance is to be restarted.
#
# NOTE: The script will return a non-zero exit code if there are any errors
#       starting HAProxy for the first time (ultimately terminating the container).
#       Errors will be logged when restarting but we'll leave an existing HAProxy 
#       running with the old configuration for resiliency and will fork a script 
#       that will periodically log warnings while the proxy is out-of-date.

if pidof haproxy ; then
    export RESTARTING=true
fi

. log-info.sh "Proxy change detected."

# Download the new configuration to [/tmp/haproxy/config-new] (after clearing it).

. log-info.sh "Retrieving configuration ZIP archive from Consul path [${CONFIG_KEY}]."

rm -rf ${CONFIG_NEW_FOLDER}/*
mkdir -p ${CONFIG_NEW_FOLDER}
consul kv get ${CONFIG_KEY} > ${CONFIG_NEW_FOLDER}/haproxy.zip

if [ "$?" != "0" ] ; then
    . report-error.sh "Cannot retrieve Consul path [${CONFIG_KEY}]"
fi

# Handle the event. 

if [ "${RESTARTING}" == "true" ] ; then
    . log-info.sh "HAProxy reconfiguration."
else
    . log-info.sh "Initial HAProxy configuration."
fi

# Kill the [logging-loop.sh] script if it's running.

if [ -f /var/run/logging-loop.pid ]; then
    kill $(cat /var/run/logging-loop.pid) &> /dev/nul
    rm -f /var/run/logging-loop.pid
fi

# Unzip the configuration.

. log-info.sh "Unzipping the configuration ZIP archive."

if ! unzip -o ${CONFIG_NEW_FOLDER}/haproxy.zip -d ${CONFIG_NEW_FOLDER} &> /dev/nul ; then
    . report-error.sh "Cannot unzip the configuration.  Is it a valid ZIP archive?"
fi

# Retrieve any certificates from Vault.

if [ -f ${CONFIG_NEW_FOLDER}/.certs ] ; then

    if [ "${VAULT_CREDENTIALS}" == "" ] ; then
        ERROR_MESSAGE="Proxy cannot be updated because VAULT_CREDENTIALS are not available to obtain TLS certificates for one or more HTTPS routes."
        . log-error.sh "${ERROR_MESSAGE}"
        logging-loop.sh "log-warn.sh" "${ERROR_MESSAGE}" &
        exit 0
    fi

    . log-info.sh "Retrieving certificates from Vault."

    # The [.certs] file describes the certificates to be downloaded
    # from Vault.  We called the [vault-auth.sh] script in the
    # main entrypoint, so VAULT_TOKEN should already be set.
    #
    # Each line contains three fields separated by a space:
    # the Vault object path, the relative destination folder 
    # path and the file name.
    #
    # Note that certificates are stored in Vault as JSON using
    # the [TlsCertificate] schema, so we'll need to extract and
    # combine the [cert] and [key] properties.

    cat ${CONFIG_NEW_FOLDER}/.certs | while read LINE
    do
        FIELDS=(${LINE})

        if [ "${FIELDS[0]}" == "" ] ; then
            # Ignore blank lines
            continue
        fi

        CERT_KEY=${FIELDS[0]}
        CERT_DIR=${CONFIG_NEW_FOLDER}/${FIELDS[1]}
        CERT_FILE=${FIELDS[2]}
        CERT_TEMP=${CONFIG_NEW_FOLDER}/__temp-cert.json

        # Ensure that the target directory exists.

        mkdir -p ${CERT_DIR}
        
        # Download the Vault certificate to a temporary file.

        if ! vault read -format=json ${CERT_KEY} > ${CERT_TEMP} ; then
            . report-error.sh "Unable to read certificate [${CERT_KEY}] from Vault."
        fi

        # Extract the [cert] and [key] properties from the
        # temporary JSON file and build the HAProxy certificates
        # by appending the key to the cert.

        cat ${CERT_TEMP} | jq -r '.data.CertPem' > ${CERT_DIR}/${CERT_FILE}
        cat ${CERT_TEMP} | jq -r '.data.KeyPem' >> ${CERT_DIR}/${CERT_FILE}

        if [ "${DEBUG}" != "true" ] ; then
            rm ${CERT_TEMP}
        fi
    done
fi

# Dump the new configuration to the logs when in DEBUG mode.

if [ "${DEBUG}" == "true" ] ; then
    . log-info.sh "*** BEGIN: New HAProxy config ***"
    cat ${CONFIG_NEW_PATH}
    . log-info.sh "*** END: New HAProxy config ***"
fi

# Verify the configuration.  Note that HAProxy will return a
# 0 error code if the configuration is OK and specifies at
# least one route.  It will return 2 if the configuration is
# OK but there are no routes.  In this case, HAProxy won't
# actually launch.  Any other exit code indicates that the
# configuration is not valid.

. log-info.sh "Verifying HAProxy configuration."

export HAPROXY_CONFIG_FOLDER=${CONFIG_NEW_FOLDER}

haproxy -c -q -f ${CONFIG_NEW_PATH}

case $? in

0)
    . log-info.sh "Configuration is valid."
    ;;

2)
    . log-info.sh "Configuration is valid but specifies no routes."

    # Make sure that any existing HAProxy instance is stopped and
    # the config folders are cleared.

    PID=$(pidof haproxy)
    
    if [ "${PID}" != "" ] ; then
        . log-info.sh "Stopping HAProxy."
        kill ${PID}
    fi

    if [ "${DEBUG}" != "true" ] ; then
        rm -rf ${CONFIG_FOLDER}/*
        rm -rf ${CONFIG_NEW_FOLDER}/*
    fi

    exit 0
    ;;

*)
    if [ "${RESTARTING}" == "true" ] ; then
        . report-error.sh "Invalid HAProxy configuration: Continuing with the old configuration."
    else
        . report-error.sh "Invalid HAProxy configuration."
    fi
    ;;

esac

# Purge the current contents of [/dev/shm/secrets/haproxy] and then copy the
# new config files over.

rm -rf ${CONFIG_FOLDER}/*
mkdir -p ${CONFIG_FOLDER}
cp -r ${CONFIG_NEW_FOLDER}/* ${CONFIG_FOLDER}

# Start/Restart HAProxy

if [ "${RESTARTING}" == "true" ] ; then
    if [ -f ${CONFIG_FOLDER}/.hardstop ] ; then
        STOP_TYPE="(hard stop)"
        STOP_OPTION="-st $(pidof haproxy)"
    else
        STOP_TYPE="(soft stop)"
        STOP_OPTION="-sf $(pidof haproxy)"
    fi
else
    STOP_OPTION=""
    STOP_TYPE=""
fi

if [ "${RESTARTING}" == "true" ] ; then
    . log-info.sh "HAProxy is restarting ${STOP_TYPE}"
else
    . log-info.sh "HAProxy is starting ${STOP_TYPE}"
fi

# Enable HAProxy debugging mode to get a better idea of why health
# checks are failing.

DEBUG_OPTION=

if [ "${DEBUG}" == "true" ] ; then
    DEBUG_OPTION=-d
fi

# HAProxy will fork itself below because the generated configuration FILE
# specifies [daemon] mode.  This allows the script to return to the Consul
# client so it can continue monitoring for configuration changes.

export HAPROXY_CONFIG_FOLDER=${CONFIG_FOLDER}

haproxy -f ${CONFIG_PATH} ${STOP_OPTION} ${DEBUG_OPTION} -V

# Give HAProxy a chance to start/restart cleanly before 
# returning to check for another update.

sleep ${START_SECONDS}
. log-info.sh "HAProxy is running"

# Purge the contents of [/dev/shm/secrets/haproxy] and [/dev/shm/secrets/haproxy-new]
# so we don't leave secrets such as TLS key lying around in a file system
# (even on a tmpfs).  We'll leave these intact for DEBUG mode so we can
# manually poke around the config.

if [ "${DEBUG}" != "true" ] ; then
    rm -rf ${CONFIG_FOLDER}/*
    rm -rf ${CONFIG_NEW_FOLDER}/*
fi
