#------------------------------------------------------------------------------
# FILE:         filter-neon-log.rb
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# This Fluentd filter plugin detects container logs forwarded by Docker and 
# normalizes the properties for the events detected.
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

		def filter(tag, time, record)

			# Detect Docker events.

			if !record.key?("container_id") || !record.key?("container_name")
				return record;	# Not from Docker
			end

			record["service_host"] = "docker";

			# Copy[log] to [message], trimming any whitespace.

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
			# field named [cid] and the [cid_full] with the full ID and then remove
			# the original [container_id].

			container_id = record["container_id"];
			record.delete("container_id");

			if container_id.length <= 12
				record["cid"] = container_id;
			else
				record["cid"] = container_id[0,12];
			end

			record["cid_full"] = container_id;

			return record;
		end
	end
end
