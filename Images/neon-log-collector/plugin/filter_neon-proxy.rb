#------------------------------------------------------------------------------
# FILE:         filter-neon-proxy.rb
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2020 by neonFORGE LLC.  All rights reserved.
#
# This td-agent filter plugin handles HAProxy traffic log events formatted using 
# the standard TCP or HTTP log formats.  See [NeonClusterHelper.GetProxyLogFormat()]
# for more information as well as HAProxy status log events.

require 'fluent/filter'
require "browser"
require 'json'
require_relative 'neon-common'

module Fluent
    class NeonProxyFilter < Filter

        include NeonCommon

        Fluent::Plugin.register_filter('neon-proxy', self)

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

            # Ensure that the record has a message

            if not record.key?("message")
                return record;
            end

            rawMessage = record["message"];

            # We expect the message to hold the raw syslog message (without the priority).
            # fields are separated by the caret (^) character.  HAProxy related messages will
            # prefix the HAProxy message part with:
            #
            #       "haproxy[#]: "
            #
            # where # is the process ID integer.

            if rawMessage =~ /haproxy\[\d+\]:\s(?<message>.*$)/

                message = $~["message"];

                # Handle traffic and status events differently.

                if message.start_with?("traffic^")

                    # Traffic events originated from services running on Docker, so
                    # change the [service_host] field from [systemd] to [docker].
                    # We need to do this because HAProxy logs events via syslog
                    # and the log pipeline assumes that syslog events come from
                    # services hosted directly by systemd.

                    record["service_host"] = "docker";

                    # Note that for valid formats, we're going to remove the original
                    # message from the event since this just duplicates what we're going
                    # to record in the [proxy] property.

                    fields = message.split('^');

                    case fields[1]

                        when "tcp-v1"

                            record = filterTcpV1(tag, record, fields);
                            record["message"] = "tcp-proxy request";
                            return record;

                        when "http-v1"

                            record = filterHttpV1(tag, record, fields);
                            record["message"] = "http-proxy request";
                            return record;

                        else
                            return record;
                    end
                else
                    return filterOther(tag, record, message);
                end
            else

                # Ignore messages that don't look like they came from HAProxy.

                return nil;
            end
        end

        def filterTcpV1(tag, record, fields)

            # Extract the global event fields.

            time = Time.strptime(fields[3], "%d/%b/%Y:%H:%M:%S.%L");    # HAProxy sample timestamp: 09/Feb/2017:15:40:44.638

            record["level"]         = "info";
            record["service"]       = fields[2];
            record["@timestamp"]    = formatTimestamp(time);

            # Extract the proxy common TCP/HTTP traffic fields.

            proxy                   = Hash.new;
            record["proxy"]         = proxy;
            
            proxy["mode"]           = "tcp";
            proxy["client_ip"]      = fields[4];
            proxy["route"]          = fields[5].gsub(/.*:/, "");        # Route name appears after the first colon.
            proxy["server"]         = fields[6];
            proxy["server_ip"]      = fields[7];
            proxy["server_port"]    = fields[8];

            # Convert TLS "-" values to empty strings.

            tlsVersion = fields[9];
            if tlsVersion == "-"
                tlsVersion = "";
            end

            tlsCypher = fields[10];
            if tlsCypher == "-"
                tlsCypher = "";
            end

            proxy["tls_version"]    = tlsVersion;
            proxy["tls_cypher"]     = tlsCypher;

            proxy["bytes_received"] = fields[11];
            proxy["bytes_sent"]     = fields[12];
            proxy["time_queue"]     = fields[13].to_f() / 1000.0;
            proxy["time_connect"]   = fields[14].to_f() / 1000.0;
            proxy["time_session"]   = fields[15].to_f() / 1000.0;
            proxy["termination"]    = fields[16];
            proxy["conn_proxy"]     = fields[17];
            proxy["conn_frontend"]  = fields[18];
            proxy["conn_backend"]   = fields[19];
            proxy["conn_server"]    = fields[20];
            proxy["retries"]        = fields[21];
            proxy["queue_server"]   = fields[22];
            proxy["queue_backend"]  = fields[23];
            
            return record;
        end

        def filterHttpV1(tag, record, fields)

            # Extract the common fields.

            filterTcpV1(tag, record, fields);

            # Extract the HTTP specific fields.
            
            record["activity_id"]       = fields[24];
            proxy                       = record["proxy"];
            
            proxy["mode"]               = "http";
            proxy["http_time_idle"]     = fields[25].to_f() / 1000.0;
            proxy["http_time_request"]  = fields[26].to_f() / 1000.0;
            proxy["http_time_response"] = fields[27].to_f() / 1000.0;
            proxy["http_time_active"]   = fields[28].to_f() / 1000.0;
            proxy["http_method"]        = fields[29];
            proxy["http_uri"]           = fields[30];
            proxy["http_uri_query"]     = fields[31];
            proxy["http_version"]       = fields[32];
            proxy["http_status"]        = fields[33];

            rawHeaders = fields[34];

            userAgent = nil;

            if rawHeaders.length > 0

                rawHeaders = rawHeaders[1..rawHeaders.length-2];    # Strip off the leading and trailing "{...}"
                headers    = rawHeaders.split('|');

                if headers.length > 0

                    # Many (all?) HTTP clients include the port number in the host header for
                    # non-standard ports other than 80 and 443, like:
                    #
                    #       mysite.com:8080
                    #
                    # This is allowed in the HTTP standard but isn't helpful for Kibana 
                    # because we already have the port available as another field.  We're
                    # going to go ahead and strip the colon and port off if present.
                    #
                    #       https://github.com/jefflill/NeonForge/issues/342

                    host     = headers[0];
                    colonPos = host.index(':')

                    if !colonPos.nil?
                        host = host[0..colonPos-1]
                    end

                    proxy["http_host"] = host;

                    if headers.length > 1
                        userAgent                = headers[1];
                        proxy["http_user_agent"] = userAgent;
                    end
                end
            end

            # See what we can extract from the [User-Agent] header.

            if !userAgent.nil?
            
                browserInfo = Browser.new(userAgent, accept_language: "en-us");

                if browserInfo.known?
                
                    browser = Hash.new;

                    if browserInfo.bot?
                        browser["bot"] = "true";
                    else
                        browser["bot"] = "false";
                    end

                    browser["device"]     = browserInfo.device.name;
                    browser["os_name"]    = browserInfo.platform.name;
                    browser["os_version"] = browserInfo.platform.version;
                    browser["name"]       = browserInfo.name;
                    browser["version"]    = browserInfo.version;

                    proxy["browser"]      = browser;
                end
            end

            return record;
        end

        def filterOther(tag, record, message)

            # I've noticed that HAProxy spams the log with [info] events when
            # TCP connections are established to backend servers.  We're going
            # filter these out.
            #
            # Note that syslog tags will look like: 
            #
            #       syslog.local7.info
    
            tag_parts = tag.split('.');

            if tag_parts.length < 3
                return nil;     # Invalid tag
            end

            case tag_parts[2].downcase
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
                    return nil;
                when "debug", "trace"
                    return nil;
            end

            # All we need to do here is set the event message to HAProxy message
            # after stripping out the extra syslog junk.

            record["message"] = message;
            return record;
        end
    end
end
