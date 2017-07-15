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

            # Extract the message.

            record["message"] = message;

            # Extract the systemd service name.

            if systemdUnit =~ /^(?<name>.*).service/i
                record["service"] = $~["name"];

logDebug("**** syslog service", record["service"]);
            else
                return nil; # Exclude events that aren't coming from a service.
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
