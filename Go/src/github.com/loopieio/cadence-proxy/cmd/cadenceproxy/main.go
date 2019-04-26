package main

import (
	"os"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/endpoints"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/connect"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/initialize"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/terminate"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/server"
)

func init() {
	base.InitProxyMessage()
	connect.InitConnect()
	initialize.InitInitialize()
	terminate.InitTerminate()
}

func main() {

	// parse the arguments
	var hostPort string
	if len(os.Args) <= 1 {
		hostPort = "127.0.0.1:3000"
	} else {
		arg := os.Args[1]
		hostPort = arg
	}

	// create the instance, set the routes,
	// and start the server
	instance := server.NewInstance(hostPort)
	endpoints.SetupRoutes(instance.Router)
	instance.Start()
}
