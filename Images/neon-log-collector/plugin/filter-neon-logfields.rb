#------------------------------------------------------------------------------
# FILE:         filter-neon-logfields.rb
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# This Fluentd filter plugin attempts to set the record's [activity-id] and 
# [module] and fields by examining the event [message].  Currently, this supports 
# the optional activity and module format emitted by the NeonStack logging classes,
# like:
#
#		[activity-id:<id>] [module:<name>]
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
	class NeonLogFields < Filter

		Fluent::Plugin.register_filter('neon-logfields', self)

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
					record["module"] = field;
				end

				record["message"] = message[(match.length..-1)];
			end

			return record;
		end
	end
end
