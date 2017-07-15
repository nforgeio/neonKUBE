#------------------------------------------------------------------------------
# FILE:         filter-neon-proxy-geoip.rb
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# This filter attempts to the map the [proxy.client_ip] IP address captured
# by the [neon-proxy] filter into the corresponding physical location information.
# This filter must be applied to the event stream after the [neon-proxy] filter.
#
# This uses a free built-in MaxMind GeoLite2-City database.  Any location 
# information will be persisted in the record's [location] field as:
#
# "location": {
#     "latitude": 6.2518,
#     "longitude": -75.5636,
#     "metro_code": 500,
#     "postal_code": "98072",
#     "time_zone": "America/Bogota",
#
#     "continent": {
#       "code": "SA",
#       "geoname_id": 6255150,
#       "name": "South America"
#     },
#     "country": {
#       "geoname_id": 3686110,
#       "iso_code": "CO",
#       "name": "Colombia"
#     },
#     "city": {
#       "geoname_id": 3674962,
#       "name": "Medellín"
#     },
#     "subdivisions": [{
#       "geoname_id": 3689815,
#       "iso_code": "ANT",
#       "name": "Antioquia"
#     }]
# } 

require 'maxminddb'
require 'json'

module Fluent
    class NeonProxyGeoIPFilter < Filter
        Fluent::Plugin.register_filter('neon-proxy-geoip', self)
    
        def initialize
            super
        end

        def configure(conf)
        super
        
            @database_path = '/geoip/database.mmdb';
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

            proxy = record["proxy"];

            if proxy.nil? then
                return record;
            end

            ip = proxy["client_ip"];

            if ip.nil? then
                return record;
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

            unless geoip.location.latitude.nil? then
                location['latitude'] = geoip.location.latitude;
            end
            unless geoip.location.longitude.nil? then
                location['longitude'] = geoip.location.longitude;
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
            unless geoip.continent.geoname_id.nil? then
                continent['geoname_id'] = geoip.continent.geoname_id;
            end
            unless geoip.continent.iso_code.nil? then
                continent['iso_code'] = geoip.continent.iso_code;
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
            unless geoip.country.geoname_id.nil? then
                country['geoname_id'] = geoip.country.geoname_id;
            end
            unless geoip.country.iso_code.nil? then
                country['iso_code'] = geoip.country.iso_code;
            end
            countryName = geoip.country.name(@locale);
            unless countryName.nil? then
                country['name'] = countryName;
            end
            unless country.empty? then
                location['country'] = country;
            end

            city = {};

            unless geoip.city.code.nil? then
                city['code'] = geoip.city.code;
            end
            unless geoip.city.geoname_id.nil? then
                city['geoname_id'] = geoip.city.geoname_id;
            end
            unless geoip.city.iso_code.nil? then
                city['iso_code'] = geoip.city.iso_code;
            end
            cityName = geoip.city.name(@locale);
            unless cityName.nil? then
                city['name'] = cityName;
            end
            unless city.empty? then
                location['city'] = city;
            end
            subdivisions = [];

            geoip.subdivisions.each do |subdivision|

                division = {}
                unless subdivision.code.nil? then
                    division['code'] = subdivision.code;
                end
                unless subdivision.geoname_id.nil? then
                    division['geoname_id'] = subdivision.geoname_id;
                end
                unless subdivision.iso_code.nil? then
                    division['iso_code'] = subdivision.iso_code;
                end
                subdivisionName = subdivision.name(@locale);
                unless subdivisionName.nil? then
                    division['name'] = subdivisionName;
                end
                unless division.empty? then
                    subdivisions.push(division );
                end
            end

            unless subdivisions.empty? then
                location['subdivisions'] = subdivisions;
            end

            record['location'] = location;

            return record;
        end
    end
end
