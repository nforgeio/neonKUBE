#------------------------------------------------------------------------------
# FILE:         filter-neon-timestamp.rb
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# This Fluentd filter plugin that attempts to extract the best timestamp for
# a log event by looking for a recognized time format within at the beginning
# of the log message.  The Fluentd event time will be used if the the message
# didn't include a valid time.
#
# Recognized timestamps will be stripped from the message.
#
# Message timestamps must appear at the beginning of the message and may or
# may not be embedded within square brackets [...].  The plugin recognizes
# the following date formats:
#
#		2000-01-22 16:30:22 +0000
#		2000-01-22 16:30:22
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
require	'time'

module Fluent
	class NeonTimestampFilter < Filter

		Fluent::Plugin.register_filter('neon-timestamp', self)

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

			if ! record.key?("message")
				return record;
			end

			message = record["message"];
			dateFmt = "%FT%T.%L%:z";

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
	end
end
