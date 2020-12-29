#------------------------------------------------------------------------------
# FILE:         filter-neon-docker.rb
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2020 by neonFORGE LLC.  All rights reserved.
#
# This td-agent filter plugin detects container log events forwarded by Docker 
# and then attempts to extract and parse standard Neon fields from the message.

require 'fluent/filter'
require 'json'
require_relative 'neon-common'

module Fluent
    class NeonDocker < Filter

        include NeonCommon

        Fluent::Plugin.register_filter('neon-docker', self)

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

            # Detect Docker events.
            #
            # Note that I'm explicitly excluding tags like [systemd.**]
            # because there doesn't seem to be a way to specify an inverse
            # filter in a TD_AGENT config.

            if !record.key?("container_id") || !record.key?("container_name") || tag.start_with?("systemd")
                return record;  # Not from Docker
            end

            record["service_host"] = "docker";

            # Copy [log] to [message], trimming any whitespace.

            record["message"] = record["log"].strip;

            # Set [service] to the container name, stripping off the leading
            # forward slash and everything from the first period to the end
            # of the string.

            container_name = record["container_name"];

            if container_name =~ /^\/?([^.]*).*/
                record["service"] = $~[1];
            else
                record["service"] = container_name;
            end

            service = record["service"];

            # We're going to convert the [container_id] into a short 12-character
            # field named [cid] and the [cid_full] with the full ID.

            container_id = record["container_id"];

            if container_id.length <= 12
                record["cid"] = container_id;
            else
                record["cid"] = container_id[0,12];
            end

            record["cid_full"] = container_id;

            # Identify messages formatted as JSON and handle them specially.

            message = record["message"];

            if !message.nil? && message.length >= 2 && message[0] == '{' && message[message.length-1] == '}'
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

            return record;
        end
    end
end
