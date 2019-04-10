package main

import (
	"github.com/loopieio/go-cadence-proxy/cmd/cadenceproxy/server"
)

func main() {

	// create the instance and start the server
	instance := server.NewInstance(":3000")
	instance.Start()

}
