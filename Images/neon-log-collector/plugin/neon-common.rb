#------------------------------------------------------------------------------
# FILE:         neon-common.rb
# CONTRIBUTOR:  Jeff Lill, Marcus Bowyer
# COPYRIGHT:    Copyright (c) 2016-2020 by neonFORGE LLC.  All rights reserved.
#
# Common filter methods.

module NeonCommon

    # Logs DEBUG messages to [/td.log]
    #
    def logDebug(method, message)

        open('/td.log', 'a') do |f|
            f.puts method + ": " + message
        end     
    end

    # Attempts to normalize a log level string.  This will return
    # a standard level or "other".
    #
    def normalizeLevel(level)

        if level.nil?
            return "other";
        end

        normalized = normalizeLevelTest(level);

        if normalized.nil?
            return "other";
        else
            return normalized;
        end
    end

    # Attempts to normalize a log level string.  This will return
    # the standard string or NIL if it could not be recognized.
    #
    # NOTE: 
    #
    # Use [normalizeLevel()] if you wish unrecognized or NIL levels 
    # to be returned as "other".
    #
    def normalizeLevelTest(level)

        if level.nil?
            return nil;
        end

        case level.downcase
            when "emergency", "emerg", "e"
                return "emergency";
            when "alert", "a"
                return "alert";
            when "critical", "fatal", "c"
                return "critical";
            when "error", "err", "e"
                return "error";
            when "serror"
                return "serror";
            when "warn", "warning", "w"
                return "warn";
            when "notice", "n"
                return "notice";
            when "info", "information", "i"
                return "info";
            when "sinfo"
                return "sinfo";
            when "transient"
                return "transient"
            when "debug", "trace", "t"
                return "debug";
        end

        return nil;
    end

    # This method sets the record's [level] field to one of the log level strings below
    # by examining one or more of the record's fields:
    #
    #       emergency   System is unusable (not emitted by services).
    #       alert       System is seriously degraded (not emitted by services)..
    #       critical    Service has failed (maps from fatal for Log4Net oriented application logging).
    #       serror      Service has encountered a security related error
    #       error       Service has encountered an error.
    #       warn        Indicates that an error may occur if actions are not taken.
    #       notice      Something unusual has occurred but is not an error.
    #       info        Normal operational messages that require no action.
    #       sinfo       Normal operational security messages that require no action.
    #       transient   Failure that will be retried
    #       debug       Developer/diagnostic information.
    #       other       Log level could not be determined.
    #
    # The current implementation tries the following:
    #
    #       * If the [PRIORITY] field exists then it is assumed to be a SYSLOG/JOURNAL
    #         log level code.
    #
    #       * If the [message] field exists and starts with a pattern like:
    #
    #               /^\s*\[\s*(.*)\s*\]\s*/
    #
    #         where the matched group is a known log level string, then the
    #         level string will be mapped to one of the standard levels.  Note
    #         that the entire pattern will be stripped from the [message] field.
    #
    #       * [level] will be set to [other] if the level couldn't be determined.
    #
    def extractLogLevel(tag, time, record)

        if record.key?("level") && record["level"] != "other"
            return record;
        end

        # First attempt to extract the level from the [message] because it's
        # most likely that the level described there will be more accurate 
        # than the level set by systemd/journal.

        if record.key?("message")

            message = record["message"];
            found   = false;

            if message =~ /^[.*]*(?<match>\s*\[\s*(?<level>[A-Za-z]*)\s*\]:?\s*)/i

                match = $~["match"];
                level = $~["level"];

                level = normalizeLevelTest(level);

                if !level.nil?
                    record["level"]   = level;
                    record["message"] = message[(match.length..-1)];
                    return record;
                end
            end
        end

        # If that didn't work, look for a SYSLOG/JOURNAL style priority field.

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

        # We didn't find a log level so use "other".

        record["level"] = "other";  

        return record;
    end

    # This method handles extraction from what looks like a JSON formatted message.
    #
    def extractJson(tag, time, record)

        message = record["message"];

        begin
            json = JSON.parse(message);
        rescue
            return record;  # Looks like the message isn't JSON after all.
        end
                
        record["json"] = message;  # Persist the original structured JSON

        if json.key?("message")
            record["message"] = json["message"];
        elsif json.key?("msg")
            record["message"] = json["msg"];
        end

        if json.key?("level")
            record["level"] = normalizeLevel(json["level"]);
        else
            record["level"] = "other";
        end

        # Handle timestamps

        begin

            date = nil;
            if json.key?("@timestamp")
                date = json["@timestamp"];
            elsif json.key?("timestamp")
                date = json["timestamp"];
            elsif json.key?("ts")
                date = json["ts"]
            end

            if !date.nil?
                if isNumeric(date)
                  record["@timestamp"] = formatTimestamp(Time.at(date.to_f));
                else
                  record["@timestamp"] = formatTimestamp(Time.parse(date));
                end
            else
                # Use the td-agent injestion time.
                record["@timestamp"] = formatTimestamp(Time.at(time));
            end
            
            return record;

        rescue

            # Handle any parsing errors by recording the td-agent injestion time.

            record["@timestamp"] = formatTimestamp(Time.at(time));
            return record;
        end
    end

    # This method attempts to extract the best timestamp for a log event by looking
    # for a recognized time format within at the beginning of the log message.  The 
    # td-agent event time will be used if the message didn't include a valid time.
    #
    # Recognized timestamps will be stripped from the message.
    #
    # Message timestamps must appear at the beginning of the message and may or
    # may not be embedded within square brackets [...].  The plugin recognizes
    # the following date formats:
    #
    #       2000-01-22 16:30:22 +0000
    #       2000-01-22 16:30:22
    #       2000/01/22 16:30:22
    #       2000-01-22T16:30:22.999
    #       2000-01-22T16:30:22.999Z
    #       2000-01-22T16:30:22.999GMT
    #       2000-01-22T16:30:22+0000
    #       2000-01-22T16:30:22+00:00
    #       2000-01-22T16:30:22.999+00:00
    #
    # Note that we'll also parse commas as well as periods for the optional 
    # fractional seconds.
    #
    def extractTimestamp(tag, time, record)

        if !record.key?("message")
            return record;
        end

        message = record["message"];
        timeFmt = "%Y-%m-%dT%H:%M:%S.%L%z"; # ISO "T" format with milliseconds

        begin

            match = nil;
            date  = nil;

            # Try matching with brackets.
            if message =~ /(?<match>\s*\[\s*(?<date>\d\d\d\d-\d\d-\d\d \d\d:\d\d:\d\d.[\d]+)\s*\]\s*)/
                date  = $~["date"];
                match = $~["match"];
            elsif message =~ /(?<match>\s*\[\s*(?<date>\d\d\d\d-\d\d-\d\d \d\d:\d\d:\d\d (:?\+|-)\d\d\d\d)\s*\]\s*)/
                date  = $~["date"];
                match = $~["match"];
            elsif message =~ /(?<match>\s*\[\s*(?<date>\d\d\d\d-\d\d-\d\d \d\d:\d\d:\d\d)\s*\]\s*)/i
                date  = $~["date"];
                match = $~["match"];
            elsif message =~ /(?<match>\s*\[\s*(?<date>\d\d\d\d-\d\d-\d\dT\d\d:\d\d:\d\d(:?(:?\.|,)\d+)?(:?(:?Z)|(:?GMT)|(:?\+|-)\d\d:?\d\d)?)\s*\]\s*)/i
                date  = $~["date"];
                match = $~["match"];

            # Try matching without brackets.

            elsif message =~ /(?<match>\s*(?<date>\d\d\d\d-\d\d-\d\d \d\d:\d\d:\d\d.[\d]+)\s*)/
                date  = $~["date"];
                match = $~["match"];
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

            if !date.nil?

                # We matched a date so attempt to parse it and update the event.

                record["@timestamp"] = formatTimestamp(Time.parse(date));
                record["message"]    = message[(match.length..-1)];
            else

                # We couldn't match a date, so record the Fluent ingestion time.

                record["@timestamp"] = formatTimestamp(Time.at(time));
            end

            return record;

        rescue

            # Handle any parsing errors by recording the td-agent injestion time.

            record["@timestamp"] = formatTimestamp(Time.at(time));
            return record;
        end
    end

    # This td-agent filter plugin attempts to set the record's [activity-id], [version]
    # [module], and [index] and fields by examining the event [message].  Currently, this 
    # supports  the optional [activity], [version], [module], and [index] fields emitted
    # by the Neon loggers, like:
    #
    #       [activity-id:<id>] [version:<semantic-version>] [module:<name>] [index:#]
    #
    def extractOtherFields(tag, time, record)

        if !record.key?("message")
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

        # Extract the [version], if present.

        message = record["message"];

        if message =~ /^(?<match>\s*\[\s*version:\s*(?<version>[^\]]*)\s*\]\s*)/i
            
            match = $~["match"];
            field = $~["version"];

            if field != ""
                record["version"] = field.strip;
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

        # Extract the [index], if present.

        message = record["message"];

        if message =~ /^(?<match>\s*\[\s*index:\s*(?<index>[^\]]*)\s*\]\s*)/i
            
            match = $~["match"];
            field = $~["index"];

            if field != ""
                record["index"] = field.strip;
            end

            record["message"] = message[(match.length..-1)];
        end

        return record;
    end

    # Unescapes a string that may include simple embedded escapes.
    # This handles escaped single and double quotes, an escapped backslash
    # and escaped control characters.  It does not handle hex, octal, or escaped
    # unicode characters.
    #
    def unescapeString(input)
        
        output = "";

        i = 0;
        while i < input.length do
            
            ch = input[i];
            if ch == '\\'

                i += 1;
                if i >= input.length
                    output = output + ch;
                    break;
                end

                ch = input[i];

                case ch
                    when '0'
                        ch = '\0';
                    when 'a'
                        ch = '\a';
                    when 'b'
                        ch = '\b';
                    when 't'
                        ch = '\t';
                    when 'n'
                        ch = '\n';
                    when 'v'
                        ch = '\v';
                    when 'f'
                        ch = '\f';
                    when 'r'
                        ch = '\r';
                    when 'e'
                        ch = '\e';
                end
            end

            output = output + ch;
            i += 1;
        end

        return output;
    end

    # Converts a Time value into a string format suitable for
    # persisting to Elasticsearch as the [@timestamp] field.
    #
    def formatTimestamp(time)
        return time.strftime("%Y-%m-%dT%H:%M:%S.%L%z"); # ISO "T" format with milliseconds
    end

    # Returns whether a string is numeric or not.
    def isNumeric(string)
      true if Float(string) rescue false
    end

    def validJson(json)
        JSON.parse(json)
        return true
      rescue JSON::ParserError => e
        return false
    end
end
