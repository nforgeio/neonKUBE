#------------------------------------------------------------------------------
# FILE:         filter-neon-istio.rb
# CONTRIBUTOR:  Jeff Lill, Marcus Bowyer
# COPYRIGHT:    Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
#
# This Fluentd filter plugin handles HAProxy traffic log events formatted using 
# the standard TCP or HTTP log formats.  See [NeonClusterHelper.GetProxyLogFormat()]
# for more information as well as HAProxy status log events.

require 'fluent/filter'
require "browser"
require 'json'
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

            begin
                json = JSON.parse(message);
            rescue
                return record;  # Looks like the message isn't JSON after all.
            end

            if record["container_name"] == "istio-proxy"
                if json["mode"].include?("HTTP")
                    record = filterHttpV1(tag, record, json);
                    record["message"] = "http-proxy request";
                    return record;
                else
                    record = filterTcpV1(tag, record, json);
                    record["message"] = "tcp-proxy request";
                    return record;
                end
            else
                # Ignore messages that don't look like they came from istio-proxy.

                return nil;
            end
        end

        def filterTcpV1(tag, record, json)

            # Extract the global event fields.

            record["level"]         = "info";
            record["@timestamp"]    = formatTimestamp(Time.at(json["start_time"].to_f));

            # Extract the proxy common TCP/HTTP traffic fields.

            proxy                   = Hash.new;
            record["proxy"]         = proxy;
            
            proxy["mode"]           = "tcp";
            proxy["client_ip"]      = json["client_ip"];
            proxy["route"]          = json["path"].split('?')[0]
            proxy["server"]         = json["upstream_cluster"];
            proxy["server_ip"]      = json["upstream_host"].split(':')[0];
            proxy["server_port"]    = json["upstream_host"].split(':')[1];

            # Convert TLS "-" values to empty strings.

            tlsVersion = json["downstream_tls_version"].sub(/^TLSv/, '');
            if tlsVersion == "-"
                tlsVersion = "";
            end

            tlsCypher = json["downstream_tls_cipher"];
            if tlsCypher == "-"
                tlsCypher = "";
            end

            proxy["tls_version"]          = tlsVersion;
            proxy["tls_cypher"]           = tlsCypher;

            proxy["bytes_received"]       = json["bytes_received"];
            proxy["bytes_sent"]           = json["bytes_sent"];
            proxy["duration"]             = json["duration"];
            proxy["request_duration"]     = json["request_duration"];
            proxy["response_duration"]    = json["response_duration"];
            proxy["response_tx_duration"] = json["response_tx_duration"];
            proxy["response_flags"]       = json["response_flags"];
            
            return record;
        end

        def filterHttpV1(tag, record, json)

            # Extract the common fields.

            filterTcpV1(tag, record, json);

            # Extract the HTTP specific fields.
            
            record["activity_id"]         = json["request_id"];
            proxy                         = record["proxy"];
            
            proxy["mode"]                 = "http";
            proxy["http_method"]          = json["method"];
            proxy["http_uri"]             = json["path"].split('?')[0];
            proxy["http_uri_query"]       = json["path"].split('?')[1];
            proxy["http_version"]         = json["mode"].split('/')[1];
            proxy["http_status"]          = json["response_code"];
            proxy["http_user_agent"]      = json["user_agent"];
            proxy["http_host"]            = json["host"];
            proxy["http_forwarded_host"]  = json["forwarded_host"];
            proxy["http_forwarded_proto"] = json["forwarded_proto"];
            proxy["http_referer"]         = json["referer"];
            proxy["http_user_agent"]      = json["user_agent"];

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
    end
end
