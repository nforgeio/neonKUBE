#------------------------------------------------------------------------------
# FILE:         filter-neon-kube.rb
# CONTRIBUTOR:  Jeff Lill, Marcus Bowyer
# COPYRIGHT:    Copyright (c) 2016-2020 by neonFORGE LLC.  All rights reserved.
#
# This td-agent filter plugin detects container log events forwarded by Kubernetes 
# and then attempts to extract and parse standard Neon fields from the message.

require 'fluent/filter'
require 'json'
require_relative 'neon-common'

module Fluent
    class NeonKube < Filter

        include NeonCommon

        Fluent::Plugin.register_filter('neon-kube', self)

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

            # Detect Kubernetes events.
            #
            # Note that I'm explicitly excluding tags like [systemd.**]
            # because there doesn't seem to be a way to specify an inverse
            # filter in a td-agent config.

            if !record.key?("kubernetes") || tag.start_with?("systemd")
                return record;  # Not from Kubernetes
            end

            record["service_host"] = "kubernetes";

            # Copy [log] to [message], trimming any whitespace.

            record["message"] = record["log"].strip;

            # Try to set [service] to be something useful.

            pod_name = record["kubernetes"]["pod_name"];
            container_name = record["kubernetes"]["container_name"];

            if record["kubernetes"].dig("labels", "app").kind_of?(String)
               service = record["kubernetes"]["labels"]["app"];
            elsif record["kubernetes"].dig("labels", "k8s-app").kind_of?(String)
               service = record["kubernetes"]["labels"]["k8s-app"];
            elsif record["kubernetes"].dig("labels", "app_kubernetes_io_name").kind_of?(String)
               service = record["kubernetes"]["labels"]["app_kubernetes_io_name"];
            end

            if service.nil?
                if pod_name =~ /(?<service>[A-Za-z0-9\-_.]+[A-Za-z0-9]+)(?<num>\-?\d+)$/i
                    service = $~["service"];
                end
            end

            if !service.nil?
                record["service"] = service;
            end

            record["container_name"]       = container_name
            record["container_image"]      = record.dig("kubernetes", "container_image") rescue nil;
            record["container_hash"]       = record.dig("kubernetes", "container_hash") rescue nil;

            record["pod_name"]             = record.dig("kubernetes", "pod_name") rescue nil;
            record["pod_ip"]               = record.dig("kubernetes", "annotations", "cni_projectcalico_org_podIP") rescue nil;
            record["kubernetes_namespace"] = record.dig("kubernetes", "namespace_name") rescue nil;
            
            container_hash = record.dig("kubernetes", "container_name") rescue nil;

            # We're going to convert the [container_id] into a short 12-character
            # field named [cid] and the [cid_full] with the full ID.

            container_id = record.dig("kubernetes", "docker_id") rescue nil;

            if !container_id.nil? && container_id.length <= 12
                record["cid"] = container_id;
            else
                record["cid"] = container_id[0,12];
            end

            record["cid_full"] = container_id;

            # Same for Pod ID

            pod_id = record["kubernetes"]["pod_id"];

            if !pod_id.nil? && pod_id.length <= 12
                record["pid"] = pod_id;
            else
                record["pid"] = pod_id[0,12] rescue nil;
            end

            record["pid_full"] = pod_id;

            # Identify messages formatted as JSON and handle them specially.

            message = record["message"];

            if !message.nil? && message.length >= 2 && message[0] == '{' && message[message.length-1] == '}'
                return extractJson(tag, time, record);
            end

            # Attempt to extract standard fields from the message.

            extractTimestamp(tag, time, record);

            if !container_name.nil? && container_name == "istio-proxy"
                message = record["message"];

                if message =~ /^(?<match>\[[0-9]*\]\s*\[\s*(?<level>[a-z]*)\s*\])/i
                    match = $~["match"];
                    level = $~["level"];
                  
                    level = normalizeLevelTest(level);
                end

                if level.nil?
                    level = normalizeLevelTest(message.split(' ')[0]);
                end

                if !level.nil?
                    record["level"]   = level;
                    record["message"] = message.split(' ', 2)[1];
                end
            elsif !container_name.nil? && container_name.include?("etcd")
                message = record["message"];

                if message =~ /^(?<match>\s?(?<level>[A-Z]))\s\|\s/i
                    match = $~["match"];
                    level = $~["level"];
                  
                    level = normalizeLevelTest(level);
                end

                if level.nil?
                    level = normalizeLevelTest(message.split(' ')[0]);
                end

                if !level.nil?
                    record["level"]   = level;
                    record["message"] = message.split('|', 2)[1].strip;
                end
            end

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
