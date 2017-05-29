#------------------------------------------------------------------------------
# FILE:         filter-neon-loglevel.rb
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# This Fluentd filter plugin sets the record's [level] field one of the log 
# level strings below, by examining one or more of the record's fields:
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
# IMPORTANT:
# ----------
# This filter is intended to work in conjunction with other filters to parse
# and then strip fields from the beginning of the message string.  For this
# to work properly, you'll need to stream events through following filters
# in the order below:
#
#		neon-docker
#		neon-timestamp
#		neon-loglevel
#		neon-logfields

require 'fluent/filter'

module Fluent
	class NeonLogLevelFilter < Filter

		Fluent::Plugin.register_filter('neon-loglevel', self)

		def configure(conf)
		super
		end

		def start
		super
		end

		def shutdown
		super
		end

		def filter(tag, time, record)

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
	end
end
