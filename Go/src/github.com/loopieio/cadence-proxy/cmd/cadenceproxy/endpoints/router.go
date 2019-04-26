package endpoints

import (
	"github.com/go-chi/chi"
	"github.com/go-chi/chi/middleware"
)

const (
	_version    = "/v1"
	_rootPath   = "/cadence-proxy"
	_pathPrefix = "/api" + _version + _rootPath
)

// SetupRoutes sets up the chi middleware
// and the route tree
func SetupRoutes(router *chi.Mux) {

	// Group the 2 endpoint routes together to utilize
	// same middleware stack
	router.Group(func(router chi.Router) {

		// Set middleware for the chi.Router to use:
		// RequestID
		// Logger
		// Recoverer
		router.Use(middleware.RequestID)
		router.Use(middleware.Logger)
		router.Use(middleware.Recoverer)

		// Set the route for the chi.Router to pathPrefix
		router.Route(_pathPrefix, func(router chi.Router) {

			// cadence-proxy endpoints
			router.Put("/", ProxyMessageHandler)
			router.Put("/echo", EchoHandler)

			// endpoints for test paths
			router.Mount("/test", TestRouter())
		})
	})
}
