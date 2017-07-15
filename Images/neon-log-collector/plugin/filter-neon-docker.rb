#------------------------------------------------------------------------------
# FILE:         filter-neon-log.rb
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# This Fluentd filter plugin detects container log events forwarded by Docker 
# and then attempts to extract and parse standard Neon fields from the message.

require 'fluent/filter'
require 'json'

module Fluent
    class NeonDocker < Filter

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

            if !record.key?("container_id") || !record.key?("container_name")
                return record;	# Not from Docker
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

            if ! message.nil? && message.length >= 2 && message[0] == '{' && message[message.length-1] == '}'
                return extractJson(tag, time, record);
            end

            # Attempt to extract standard fields from the message.

            extractTimestamp(tag, time, record);
            extractLogLevel(tag, time, record);
            extractOtherFields(tag, time, record);

            # Filter out events with an empty message.

            if record["message"].length == 0
                return null;
            end

logDebug("message", record["message"]);

            return record;
        end

        # This method handles extraction from what looks like a JSON formatted message.
		#
        def extractJson(tag, time, record)

		    message = record["message"];

			begin
			    json = JSON.parse(message);
				rescue
					return record;	# Looks like the message isn't JSON after all.
				end
			end
				
			record["json"] = json;	# Persist the structured JSON

			if json.key?("message")
				record["message"] = json["message"];
			elsif json.key?("msg")
				record["message"] = json["msg"];
			end

			if json.key?("level")
				level = json["level"];
                case level.downcase
                    when "emergency", "emerg"
                        record["level"] = "emergency";
                    when "alert"
                        record["level"] = "alert";
                    when "critical", "fatal"
                        record["level"] = "critical";
                    when "error", "err"
                        record["level"] = "error";
                    when "warn", "warning"
                        record["level"] = "warn";
                    when "notice"
                        record["level"] = "notice";
                    when "info", "information"
                        record["level"] = "info";
                    when "debug", "trace"
                        record["level"] = "debug";
                end

logDebug("message (from JSON)", record["message"].to_s);
logDebug("level (from JSON)", record["level"].to_s);

            return record;
        end

        # This method attempts to extract the best timestamp for a log event by looking
        # for a recognized time format within at the beginning of the log message.  The 
        # Fluentd event time will be used if the the message didn't include a valid time.
        #
        # Recognized timestamps will be stripped from the message.
        #
        # Message timestamps must appear at the beginning of the message and may or
        # may not be embedded within square brackets [...].  The plugin recognizes
        # the following date formats:
        #
        #		2000-01-22 16:30:22 +0000
        #		2000-01-22 16:30:22
        #		2000/01/22 16:30:22
        #       2000-01-22T16:30:22.999
        #       2000-01-22T16:30:22.999Z
        #		2000-01-22T16:30:22.999GMT
        #       2000-01-22T16:30:22+0000
        #       2000-01-22T16:30:22+00:00
        #       2000-01-22T16:30:22.999+00:00
        #
        # Note that we'll also parse commas as well as periods for the optional 
        # fractional seconds.
        #
        def extractTimestamp(tag, time, record)

            if ! record.key?("message")
                return record;
            end

            message = record["message"];
            dateFmt = "%Y-%m-%dT%H:%M:%S.%L%z";	# ISO "T" format with milliseconds

            begin

                match = nil;
                date  = nil;

                # Try matching with brackets.

                if message =~ /(?<match>\s*\[\s*(?<date>\d\d\d\d-\d\d-\d\d \d\d:\d\d:\d\d (:?\+|-)\d\d\d\d)\s*\]\s*)/
                    date  = $~["date"];
                    match = $~["match"];
                elsif message =~ /(?<match>\s*\[\s*(?<date>\d\d\d\d-\d\d-\d\d \d\d:\d\d:\d\d)\s*\]\s*)/i
                    date  = $~["date"];
                    match = $~["match"];
                elsif message =~ /(?<match>\s*\[\s*(?<date>\d\d\d\d-\d\d-\d\dT\d\d:\d\d:\d\d(:?(:?\.|,)\d+)?(:?(:?Z)|(:?GMT)|(:?\+|-)\d\d:?\d\d)?)\s*\]\s*)/i
                    date  = $~["date"];
                    match = $~["match"];

                # Try matching without brackets.

                elsif message =~ /(?<match>\s*(?<date>\d\d\d\d-\d\d-\d\d \d\d:\d\d:\d\d (:?\+|-)\d\d\d\d)\s*)/
                    date  = $~["date"];
                    match = $~["match"];
                elsif message =~ /(?<match>\s*(?<date>\d\d\d\d-\d\d-\d\d \d\d:\d\d:\d\d)\s*)/i
                    date  = $~["date"];
                    match = $~["match"];
                elsif message =~ /(?<match>\s*(?<date>\d\d\d\d-\d\d-\d\dT\d\d:\d\d:\d\d(:?(:?\.|,)\d+)?(:?(:?Z)|(:?GMT)|(:?\+|-)\d\d:?\d\d)?)\s*)/i
                    date  = $~["date"];
                    match = $~["match"];
                elsif message =~ /(?<match>\s*(?<date>\d\d\d\d\/\d\d\/\d\d \d\d:\d\d:\d\d\s*))/i
                    date  = $~["date"];
                    match = $~["match"];
                end

                if ! date.nil?

                    # We matched a date so attempt to parse it and update the event.

                    record["@timestamp"] = Time.parse(date).strftime(dateFmt);
                    record["message"]    = message[(match.length..-1)];
                else

                    # We couldn't match a date, so record the Fluent ingestion time.

                    record["@timestamp"] = Time.at(time).strftime(dateFmt);
                end

                return record;

            # Handle any parsing errors by recording the Fluentd injestion time.

            rescue Exception => e

                record["@timestamp"] = Time.at(time).strftime(dateFmt);
                return record;
            end
        end

        # This method sets the record's [level] field one of the log level strings below
        # by examining one or more of the record's fields:
        #
        #		emergency	System is unusable (not emitted by services).
        #		alert		System is seriously degraded (not emitted by services)..
        #		critical	Service has failed (maps from fatal for Log4Net oriented application logging).
        #		error		Service has encountered an error.
        #		warn		Indicates that an error may occur if actions are not taken.
        #		notice		Something unusual has occurred but is not an error.
        #		info		Normal operational messages that require no action.
        #		debug		Developer/diagnostic information.
        #		unknown		Log level could not be determined.
        #
        # The current implementation tries the following:
        #
        #		* If the [PRIORITY] field exists then it is assumed to be a SYSLOG/JOURNAL
        #		  log level code.
        #
        #		* If the [message] field exists and starts with a pattern like:
        #
        #				/^\s*\[\s*(.*)\s*\]\s*/
        #
        #		  where the matched group is a known log level string, then the
        #		  level string will be mapped to one of the standard levels.  Note
        #		  that the entire pattern will be stripped from the [message] field.
        #
        #		* [level] will be set to [other] if the level couldn't be determined.
        #
        def extractLogLevel(tag, time, record)

            # First attempt to extract the level from the [message] because it's
            # most likely that the level described there will be more accurate 
            # than the level set by systemd/journal.

            if record.key?("message")

                message = record["message"];
                found   = false;

                if message =~ /^(?<match>\s*\[\s*(?<level>[a-z]*)\s*\]:?\s*)/i

                    match = $~["match"];
                    level = $~["level"];

                    case level.downcase
                        when "emergency", "emerg"
                            record["level"] = "emergency";
                            found = true;
                        when "alert"
                            record["level"] = "alert";
                            found = true;
                        when "critical", "fatal"
                            record["level"] = "critical";
                            found = true;
                        when "error", "err"
                            record["level"] = "error";
                            found = true;
                        when "warn", "warning"
                            record["level"] = "warn";
                            found = true;
                        when "notice"
                            record["level"] = "notice";
                            found = true;
                        when "info", "information"
                            record["level"] = "info";
                            found = true;
                        when "debug", "trace"
                            record["level"] = "debug";
                            found = true;
                    end

                    if found == true
                        record["message"] = message[(match.length..-1)];
                        return record;
                    end
                end
            end

            # Then look for a SYSLOG/JOURNAL style priority field.

            if record.key?("PRIORITY")
                case record["PRIORITY"]
                    when "0"
                        record["level"] = "emergency";
                        return record;
                    when "1"
                        record["level"] = "alert";
                        return record;
                    when "2"
                        record["level"] = "critical";
                        return record;
                    when "3"
                        record["level"] = "error";
                        return record;
                    when "4"
                        record["level"] = "warn";
                        return record;
                    when "5"
                        record["level"] = "notice";
                        return record;
                    when "6"
                        record["level"] = "info";
                        return record;
                    when "7"
                        record["level"] = "debug";
                        return record;
                end
            end

            # We didn't find a log level.

            record["level"] = "other";	

            return record;
        end

        # This Fluentd filter plugin attempts to set the record's [activity-id], 
        # [module], and [order] and fields by examining the event [message].  Currently, this 
        # supports  the optional activity, module, and order format emitted by the Neon 
        # logging classes, like:
        #
        #		[activity-id:<id>] [module:<name>] [order:#]
        #
        def extractOtherFields(tag, time, record)
            if ! record.key?("message")
                return record;
            end

            # Extract the [activity-id], if present.

            message = record["message"];

            if message =~ /^(?<match>\s*\[\s*activity-id:\s*(?<id>[^\]]*)\s*\]\s*)/i
            
                match = $~["match"];
                field = $~["id"];

                if field != ""
                    record["activity_id"] = field;
                end

                record["message"] = message[(match.length..-1)];
            end

            # Extract the [module], if present.

            message = record["message"];

            if message =~ /^(?<match>\s*\[\s*module:\s*(?<module>[^\]]*)\s*\]\s*)/i
            
                match = $~["match"];
                field = $~["module"];

                if field != ""
                    record["module"] = field.strip;
                end

                record["message"] = message[(match.length..-1)];
            end

            # Extract the [order], if present.

            message = record["message"];

            if message =~ /^(?<match>\s*\[\s*order:\s*(?<order>[^\]]*)\s*\]\s*)/i
            
                match = $~["match"];
                field = $~["order"];

                if field != ""
                    record["order"] = field.strip;
                end

                record["message"] = message[(match.length..-1)];
            end

            return record;
        end

        # Logs DEBUG messages to [/td.log]
        def logDebug(method, message)
            open('/td.log', 'a') do |f|
              f.puts method + ": " + message
            end		
        end
    end
end
