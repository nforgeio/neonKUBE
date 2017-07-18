#------------------------------------------------------------------------------
# FILE:         filter-neon-systemd.rb
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# This Fluentd filter plugin detects container log events for services
# hosted by systemd and then attempts to extract and parse standard Neon
# fields from the message.

require 'fluent/filter'
require 'json'
require_relative 'neon-common'

module Fluent
    class NeonSystemd < Filter

        include NeonCommon

        Fluent::Plugin.register_filter('neon-systemd', self)

        def configure(conf)
        super
        end

        def start
        super
        end

        def shutdown
        super
        end

        # Implements the filter.
        #
        def filter(tag, time, record)

            # Exclude events that don't have a message or dont specify
            # the systemd unit.

            if !record.key?("MESSAGE") || !record.key?("SYSTEMD_UNIT")
                return nil;
            end

            message     = record["MESSAGE"].strip;
            systemdUnit = record["SYSTEMD_UNIT"];

            record["service_host"] = "systemd";

            # Extract the systemd service name.

            if systemdUnit =~ /^(?<name>.*).service/i

                service           = $~["name"].downcase;
                record["service"] = service;

logDebug("**** syslog service", record["service"]);
            else
                return nil; # Exclude events that weren't emitted by a service.
            end

            # Handle message parsing from specific known services.

            timeFmt = "%Y-%m-%dT%H:%M:%S.%L%z"; # ISO "T" format with milliseconds

            case service

                when "docker"

                    # Docker messages look like:
                    #
                    #       time="2017-07-15T19:08:28.226717603Z" level=error msg="Bulk sync to node test-worker-1-682d3a76b041 timed out"

logDebug("*** DOCKER", "");
                    if message =~ /time="(?<time>.*)"\s*level=(?<level>[^\s]*)\s*msg=(?<msg>.*")$/i

                        time  = $~["time"];
                        level = $~["level"];
                        msg   = $~["msg"].strip;

logDebug("docker-raw-time", time);
logDebug("docker-raw-level", level);
logDebug("docker-raw-msg", msg);

                        # Extract the time.

                        record["@timestamp"] = Time.parse(time).strftime(timeFmt);

                        # Extract the log level.

                        level = normalizeLevel(level);

                        if level.nil?
                            level = "other";
                        end
logDebug("docker-level", level);

                        record["level"] = level;

                        # Extract the message.  Note that the value extracted via the regex has
                        # the surrounding quotes and may include escaped characters.  We're going 
                        # to strip off the outer quotes and convert the escaped characters.

                        record["message"] = unescapeString(msg[1..-1]);
logDebug("docker-msg", msg);

                        return record;
                    end

                when "consul"

logDebug("*** CONSUL", "");
                    if message =~ /\s*(?<time>\d\d\d\d\/\d\d\/\d\d \d\d:\d\d:\d\d)\s+\[(?<level>[^\\]*)\]\s+(?<msg>.*)$/i

                        time  = $~["time"];
                        level = $~["level"];
                        msg   = $~["msg"].strip;

logDebug("consul-raw-time", time);
logDebug("consul-raw-level", level);
logDebug("consul-raw-msg", msg);

                        # Extract the time.

                        record["@timestamp"] = Time.parse(time).strftime(timeFmt);

                        # Extract the log level.

                        level = normalizeLevel(level);

                        if level.nil?
                            level = "other";
                        end
logDebug("consul-level", level);

                        record["level"] = level;

                        # Extract the message.

                        record["message"] = msg;
logDebug("consul-message", record["message"]);
                    end
                    
                when "vault"

logDebug("*** VAULT", "");
                    # Vault appears to be log in two different formats.  The two examples below looks
                    # like they're describing service status events:
                    #
                    # Service status events:
                    # ----------------------
                    #
                    #   ==> Vault server configuration:
                    #                        Cgo: disabled
                    #            Cluster Address: https://test-manager-0.neon-vault.cluster:8201
                    #                 Listener 1: tcp (addr: "0.0.0.0:8200", cluster address: "0.0.0.0:8201", tls: "enabled")
                    #                  Log Level: info
                    #                      Mlock: supported: true, enabled: true
                    #           Redirect Address: https://test-manager-0.neon-vault.cluster:8200
                    #                    Storage: consul (HA available)
                    #                    Version: Vault v0.7.2
                    #                Version Sha: d28dd5a018294562dbc9a18c95554d52b5d12390
                    #   ==> Vault server started! Log data will stream in below:
                    #   2017/07/15 19:18:28.663808 [INFO ] core: vault is unsealed
                    #   2017/07/15 19:18:28.663918 [INFO ] core: entering standby mode
                    #   2017/07/15 19:18:28.691348 [INFO ] core: acquired lock, enabling active operation
                    #   2017/07/15 19:18:28.727458 [INFO ] core: post-unseal setup starting
                    #   2017/07/15 19:18:28.727918 [INFO ] core: loaded wrapping token key
                    #
                    # Audit events:
                    # -------------
                    #
                    #   {"time":"2017-07-16T01:37:24Z","type":"response","auth":{"client_token":"","accessor":"","display_name":"approle","policies":["default","neon-cert-reader","neon-hosting-reader"],"metadata":{}}
                    #   {"time":"2017-07-16T01:42:33Z","type":"request","auth":{"client_token":"","accessor":"","display_name":"approle","policies":["default","neon-cert-reader","neon-hosting-reader"],"metadata":{}},
                    #   {"time":"2017-07-16T01:42:33Z","type":"response","auth":{"client_token":"","accessor":"","display_name":"approle","policies":["default","neon-cert-reader","neon-hosting-reader"],"metadata":{}}
                    #   {"time":"2017-07-16T01:42:33Z","type":"request","auth":{"client_token":"","accessor":"","display_name":"approle","policies":["default","neon-cert-reader","neon-hosting-reader"],"metadata":{}},
                    #
                    # The current implementation is going to ignore the audit events and only process the service status events.
                    # We're going to extract the time and log level if they're present otherwise we'll just take the whole (trimmed)
                    # line as the message.

                    if message =~ /(?<time>\d\d\d\d\/\d\d\/\d\d \d\d:\d\d:\d\d(.\d*)?)\s+\[(?<level>[^\\\s]*)\]\s+(?<msg>.*)$/i

                        time  = $~["time"];
                        level = $~["level"];
                        msg   = $~["msg"].strip;

logDebug("vault-raw-time", time);
logDebug("vault-raw-level", level);
logDebug("vault-raw-msg", msg);

                        # Extract the time.

                        record["@timestamp"] = Time.parse(time).strftime(timeFmt);

                        # Extract the log level.

                        level = normalizeLevel(level);

                        if level.nil?
                            level = "other";
                        end
logDebug("vault-level", level);

                        record["level"] = level;

                        # Extract the message.

                        record["message"] = msg;
logDebug("vault-message", record["message"]);
                    elsif message.length < 2 || message[0] != '{' || message[message.length-1] != '}'
                        record["level"]   = "info"; # Consider all untagged messages as INFO
                        record["message"] = message.strip;
logDebug("vault-message", record["message"]);
                    else
                        # $todo(jeff.lill): 
                        #
                        # Handle Vault audit events.  Should these go to a different 
                        # Elasticsearch index?
                    end
            end

            # Identify messages formatted as JSON and handle them specially.

            message = record["message"];

            if ! message.nil? && message.length >= 2 && message[0] == '{' && message[message.length-1] == '}'
                return extractJson(tag, time, record);
            end

            # Attempt to extract standard fields from the message.

            extractTimestamp(tag, time, record);
            extractLogLevel(tag, time, record);
            extractOtherFields(tag, time, record);

            # Filter out events with an empty [message] or no [json] property

            if (!record.key?("message") || record["message"].length == 0) && !record.key?("json")
                return nil;
            end

logDebug("**** syslog message", record["message"]);

            return record;
        end
    end
end
