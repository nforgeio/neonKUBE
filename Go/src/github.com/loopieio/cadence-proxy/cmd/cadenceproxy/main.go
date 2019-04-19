package main

import (
	"os"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/server"
)

func main() {

	// parse the arguments
	var hostPort string
	if len(os.Args) <= 1 {
		hostPort = "127.0.0.1:3000"
	} else {
		arg := os.Args[1]
		hostPort = arg
	}

	// create the instance and start the server
	instance := server.NewInstance(hostPort)
	instance.Start()
}
