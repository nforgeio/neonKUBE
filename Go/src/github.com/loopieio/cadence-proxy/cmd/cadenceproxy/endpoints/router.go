package endpoints

import (
	"github.com/go-chi/chi"
	"github.com/go-chi/chi/middleware"
)

var Debug bool

// SetupRoutes sets up the chi middleware
// and the route tree
func SetupRoutes(router *chi.Mux) {

	// Group the 2 endpoint routes together to utilize
	// same middleware stack
	router.Group(func(router chi.Router) {

		// Set middleware for the chi.Router to use:
		// RequestID
		// Recoverer
		router.Use(middleware.RequestID)
		router.Use(middleware.Recoverer)

		if Debug {
			router.Use(middleware.Logger)
		}

		// cadence-proxy endpoints
		router.Put("/", ProxyMessageHandler)
		router.Put("/echo", EchoHandler)

		// endpoints for test paths
		router.Mount("/test", TestRouter())
	})
}
