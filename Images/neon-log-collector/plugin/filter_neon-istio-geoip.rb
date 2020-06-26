#------------------------------------------------------------------------------
# FILE:         filter-neon-istio-geoip.rb
# CONTRIBUTOR:  Jeff Lill, Marcus Bowyer
# COPYRIGHT:    Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
#
# This filter attempts to the map the [proxy.client_ip] IP address captured
# by the [neon-proxy] filter into the corresponding physical location information.
# This filter must be applied to the event stream after the [neon-proxy] filter.
#
# This uses a free built-in MaxMind GeoLite2-City database.  Any location 
# information will be persisted in the record's [location] field as:
#
# "location": {
#     "latitude": 45.523,
#     "longitude": -122.676,
#     "metro_code": 500,
#     "postal_code": "98072",
#     "time_zone": "America/Los_Angeles",
#
#     "continent": {
#       "code": "NA",
#       "name": "North America"
#     },
#     "country": {
#       "code": "UA",
#       "name": "United States"
#     },
#     "city": {
#       "code": "???"
#       "name": "Seattle"
#     },
#     "state": {
#       "code": "WA"
#       "name": "Washington",
#     },
#     "county": {
#       "code": "???"
#       "name": "King",
#     }
# } 

require 'maxminddb'
require 'json'
require_relative 'neon-common'

module Fluent
    class NeonProxyGeoIPFilter < Filter
        
        include NeonCommon

        Fluent::Plugin.register_filter('neon-istio-geoip', self)
    
        def initialize
            super
        end

        def configure(conf)
        super
        
            @database_path = Dir['/geoip/*.mmdb'].first;
            @locale        = 'en';

            begin
                @database = MaxMindDB.new(@database_path);
            rescue
                log.warn "Cannot open the [/geoip/database.mmdb] database.";
                @database = nil;
            end
        end

        def filter(tag, time, record)

            if @database.nil? then
                return record;
            end

            proxy = record["proxy"]

            if proxy.nil? then
                return record;
            end

            ip = proxy["client_ip"];

            if ip.nil? then
                return record;
            end

            if ip == "-"

                # Seems like we're seeing a dash (-) instead of a client IP
                # sometimes and this prevents Elasticsearch from parsing
                # and persisting the event.  Set IP=0.0.0.0 in these cases
                # so we'll still have an event we can look at.

                ip = "0.0.0.0";
            end

            begin
                geoip = @database.lookup(ip);
            rescue IPAddr::InvalidAddressError => e
                # Abort the lookup.
                return record;
            end

            unless geoip.found? then
                return record;
            end

            location = {};

            unless geoip.location.longitude.nil? then
                location['coordinates'] = [geoip.location.longitude, geoip.location.latitude] ;
            end
            unless geoip.location.metro_code.nil? then
                location['metro_code'] = geoip.location.metro_code;
            end
            unless geoip.postal.code.nil? then
                location['postal_code'] = geoip.postal.code;
            end
            unless geoip.location.time_zone.nil? then
                location['time_zone'] = geoip.location.time_zone;
            end

            continent = {};

            unless geoip.continent.code.nil? then
                continent['code'] = geoip.continent.code;
            end
            unless geoip.continent.iso_code.nil? then
                continent['code'] = geoip.continent.iso_code;
            end
            continentName = geoip.continent.name(@locale)
            unless continentName.nil? then
                continent['name'] = continentName;
            end
            unless continent.empty? then
                location['continent'] = continent;
            end

            country = {}

            unless geoip.country.code.nil? then
                country['code'] = geoip.country.code;
            end
            unless geoip.country.iso_code.nil? then
                country['code'] = geoip.country.iso_code;
            end
            countryName = geoip.country.name(@locale);
            unless countryName.nil? then
                country['name'] = countryName;
            end
            unless country.empty? then
                location['country'] = country;
            end

            city = {};

            unless geoip.city.iso_code.nil? then
                city['code'] = geoip.city.iso_code;
            end
            cityName = geoip.city.name(@locale);
            unless cityName.nil? then
                city['name'] = cityName;
            end
            unless city.empty? then
                location['city'] = city;
            end

            # I'm going to assume that the subdivisions are ordered
            # such that encompassing regions appear first, e.g. that
            # a state appears before the county within the state.
            #
            # I'm only going to go two levels in, assuming that the
            # first subdivision is the state and the second it the
            # county (if either are present).

            if geoip.subdivisions.length > 0

                subdivision = geoip.subdivisions[0];
                state       = {};

                unless subdivision.iso_code.nil? then
                    state['code'] = subdivision.iso_code;
                end
                subdivision_name = subdivision.name(@locale);
                unless subdivision_name.nil? then
                    state['name'] = subdivision_name;
                end
                unless state.empty? then
                    location['state'] = state;
                end

                if geoip.subdivisions.length > 1

                    subdivision = geoip.subdivisions[1];
                    county      = {};

                    unless subdivision.iso_code.nil? then
                        county['code'] = subdivision.iso_code;
                    end
                    subdivision_name = subdivision.name(@locale);
                    unless subdivision_name.nil? then
                        county['name'] = subdivision_name;
                    end
                    unless county.empty? then
                        location['county'] = county;
                    end
                end
            end

            record['location'] = location;

            return record;
        end
    end
end
