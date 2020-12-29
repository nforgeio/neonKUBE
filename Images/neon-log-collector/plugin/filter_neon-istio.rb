#------------------------------------------------------------------------------
# FILE:         filter-neon-istio.rb
# CONTRIBUTOR:  Jeff Lill, Marcus Bowyer
# COPYRIGHT:    Copyright (c) 2016-2020 by neonFORGE LLC.  All rights reserved.
#
# This td-agent filter plugin handles HAProxy traffic log events formatted using 
# the standard TCP or HTTP log formats.  See [NeonClusterHelper.GetProxyLogFormat()]
# for more information as well as HAProxy status log events.

require 'fluent/filter'
require "browser"
require 'json'
require 'ipaddress'
require_relative 'neon-common'

module Fluent
    class NeonProxyFilter < Filter

        include NeonCommon

        Fluent::Plugin.register_filter('neon-istio', self)

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

            message = record["message"];
            data = nil;

            if validJson(message)
                begin
                    data = JSON.parse(message);
                rescue
                    return record;  # Looks like the message isn't data after all.
                end
            else
                data = parseText(message);

                if data.nil?
                    return record;
                end
            end

            if record["container_name"] == "istio-proxy"
                if data["mode"].include?("HTTP")
                    record = filterHttpV1(tag, record, data);
                    record["message"] = "http-proxy request";
                    return record;
                else
                    record = filterTcpV1(tag, record, data);
                    record["message"] = "tcp-proxy request";
                    return record;
                end
            else
                # Ignore messages that don't look like they came from istio-proxy.

                return nil;
            end
        end

        def filterTcpV1(tag, record, data)

            # Extract the global event fields.

            record["level"]         = "info";
            record["@timestamp"]    = formatTimestamp(Time.at(data["start_time"].to_f));

            # Extract the proxy common TCP/HTTP traffic fields.

            proxy                   = Hash.new;
            record["proxy"]         = proxy;
            
            proxy["mode"]           = "tcp";
            
            if IPAddress.valid? data["client_ip"]
                proxy["client_ip"]      = data["client_ip"];
            end

            proxy["route"]          = data["path"].split('?')[0]
            proxy["server"]         = data["upstream_cluster"];
            
            if IPAddress.valid? data["upstream_host"].split(':')[0]
                proxy["server_ip"]      = data["upstream_host"].split(':')[0];
            end

            proxy["server_port"]    = data["upstream_host"].split(':')[1];

            # Convert TLS "-" values to empty strings.

            tlsVersion = data["downstream_tls_version"].sub(/^TLSv/, '');
            if tlsVersion == "-"
                tlsVersion = "";
            end

            tlsCypher = data["downstream_tls_cipher"];
            if tlsCypher == "-"
                tlsCypher = "";
            end

            proxy["tls_version"]          = tlsVersion;
            proxy["tls_cypher"]           = tlsCypher;

            proxy["bytes_received"]       = data["bytes_received"];
            proxy["bytes_sent"]           = data["bytes_sent"];
            proxy["duration"]             = data["duration"];
            proxy["request_duration"]     = data["request_duration"];
            proxy["response_duration"]    = data["response_duration"];
            proxy["response_tx_duration"] = data["response_tx_duration"];
            proxy["response_flags"]       = data["response_flags"];
            
            return record;
        end

        def filterHttpV1(tag, record, data)

            # Extract the common fields.

            filterTcpV1(tag, record, data);

            # Extract the HTTP specific fields.
            
            record["activity_id"]         = data["request_id"];
            proxy                         = record["proxy"];
            
            proxy["mode"]                 = "http";
            proxy["http_method"]          = data["method"];
            proxy["http_uri"]             = data["path"].split('?')[0];
            proxy["http_uri_query"]       = data["path"].split('?')[1];
            proxy["http_version"]         = data["mode"].split('/')[1];
            proxy["http_status"]          = data["response_code"];
            proxy["http_user_agent"]      = data["user_agent"];
            proxy["http_host"]            = data["host"];
            proxy["http_forwarded_host"]  = data["forwarded_host"];
            proxy["http_forwarded_proto"] = data["forwarded_proto"];
            proxy["http_referer"]         = data["referer"];
            proxy["http_user_agent"]      = data["user_agent"];

            # See what we can extract from the [User-Agent] header.

            if !proxy["http_user_agent"].nil?
            
                browserInfo = Browser.new(proxy["http_user_agent"], accept_language: "en-us");

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

        def parseText(message)
            if message =~ /(?<match>authority=(?<authority>.*)mode=(?<mode>.*) upstream_service_time=(?<upstream_service_time>.*) upstream_local_address=(?<upstream_local_address>.*) duration=(?<duration>.*) request_duration=(?<request_duration>.*) response_duration=(?<response_duration>.*) response_tx_duration=(?<response_tx_duration>.*) downstream_local_address=(?<downstream_local_address>.*) upstream_transport_failure_reason=(?<upstream_tsport_failure_reason>.*) route_name=(?<route_name>.*) response_code=(?<response_code>.*) response_code_details=(?<response_code_details>.*) user_agent=(?<user_agent>.*) response_flags=(?<response_flags>.*) start_time=(?<start_time>.*) method=(?<method>.*) host=(?<host>.*) referer=(?<referer>.*) request_id=(?<request_id>.*) forwarded_host=(?<forwarded_host>.*) forwarded_proto=(?<forwarded_proto>.*) upstream_host=(?<upstream_host>.*) downstream_local_uri_san=(?<downstream_local_uri_san>.*) downstream_peer_uri_san=(?<downstream_peer_uri_san>.*) downstream_local_subject=(?<downstream_local_subject>.*) downstream_peer_subject=(?<downstream_peer_subject>.*) downstream_peer_issuer=(?<downstream_peer_issuer>.*) downstream_tls_session_id=(?<downstream_tls_session_id>.*) downstream_tls_cipher=(?<downstream_tls_cipher>.*) downstream_tls_version=(?<downstream_tls_version>.*) downstream_peer_serial=(?<downstream_peer_serial>.*) downstream_peer_cert=(?<downstream_peer_cert>.*) client_ip=(?<client_ip>.*) requested_server_name=(?<requested_server_name>.*) bytes_received=(?<bytes_received>.*) bytes_sent=(?<bytes_sent>.*) upstream_cluster=(?<upstream_cluster>.*) downstream_remote_address=(?<downstream_remote_address>.*) path=(?<path>.*))/i
                data = Hash.new;
                match = $~["match"];
                data["authority"] = $~["authority"];
                data["mode"] = $~["mode"];
                data["upstream_service_time"] = $~["upstream_service_time"];
                data["upstream_local_address"] = $~["upstream_local_address"];
                data["duration"] = $~["duration"];
                data["request_duration"] = $~["request_duration"];
                data["response_duration"] = $~["response_duration"];
                data["response_tx_duration"] = $~["response_tx_duration"];
                data["downstream_local_address"] = $~["downstream_local_address"];
                data["upstream_transport_failure_reason"] = $~["upstream_tsport_failure_reason"];
                data["route_name"] = $~["route_name"];
                data["response_code"] = $~["response_code"];
                data["response_code_details"] = $~["response_code_details"];
                data["user_agent"] = $~["user_agent"];
                data["response_flags"] = $~["response_flags"];
                data["start_time"] = $~["start_time"];
                data["method"] = $~["method"];
                data["host"] = $~["host"];
                data["referer"] = $~["referer"];
                data["request_id"] = $~["request_id"];
                data["forwarded_host"] = $~["forwarded_host"];
                data["forwarded_proto"] = $~["forwarded_proto"];
                data["upstream_host"] = $~["upstream_host"];
                data["downstream_local_uri_san"] = $~["downstream_local_uri_san"];
                data["downstream_peer_uri_san"] = $~["downstream_peer_uri_san"];
                data["downstream_local_subject"] = $~["downstream_local_subject"];
                data["downstream_peer_subject"] = $~["downstream_peer_subject"];
                data["downstream_peer_issuer"] = $~["downstream_peer_issuer"];
                data["downstream_tls_session_id"] = $~["downstream_tls_session_id"];
                data["downstream_tls_cipher"] = $~["downstream_tls_cipher"];
                data["downstream_tls_version"] = $~["downstream_tls_version"];
                data["downstream_peer_serial"] = $~["downstream_peer_serial"];
                data["downstream_peer_cert"] = $~["downstream_peer_cert"];
                data["client_ip"] = $~["client_ip"];
                data["requested_server_name"] = $~["requested_server_name"];
                data["bytes_received"] = $~["bytes_received"];
                data["bytes_sent"] = $~["bytes_sent"];
                data["upstream_cluster"] = $~["upstream_cluster"];
                data["downstream_remote_address"] = $~["downstream_remote_address"];
                data["path"] = $~["path"];
                return data;
            else
                return nil;
            end
        end
    end
end
