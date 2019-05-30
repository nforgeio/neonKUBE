package main

import (
	"flag"

	"github.com/loopieio/cadence-proxy/internal/endpoints"
	"github.com/loopieio/cadence-proxy/internal/logger"
	"github.com/loopieio/cadence-proxy/internal/server"
)

var (

	// variables to put command line args in
	address, logLevel string
	debugMode         bool
)

func main() {

	// define the flags and parse them
	flag.StringVar(&address, "listen", "127.0.0.2:5000", "Address for the Cadence Proxy Server to listen on")
	flag.StringVar(&logLevel, "log-level", "info", "The log level when running the proxy")
	flag.BoolVar(&debugMode, "debug", true, "Set to debug mode")
	flag.Parse()

	// endpoint debugging
	if debugMode {
		endpoints.Debug = true
	}

	// set the log level and if the program should run in debug mode
	logger.SetLogger(logLevel, debugMode)

	// create the instance, set the routes,
	// and start the server
	instance := server.NewInstance(address)

	// set server instance variable in endpoints package
	endpoints.Instance = instance

	// setup the routes
	endpoints.SetupRoutes(instance.Router)

	// start the server
	instance.Start()
}
