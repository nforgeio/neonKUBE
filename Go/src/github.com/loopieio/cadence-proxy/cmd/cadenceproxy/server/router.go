package server

import (
	"github.com/go-chi/chi"
	"github.com/go-chi/chi/middleware"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/endpoints"
)

const (
	version  = "/v1"
	rootPath = "/cadence-proxy"

	// PathPrefix is the global prefix for all calls to this api
	PathPrefix = "/api" + version + rootPath
)

// SetupRoutes sets up the chi middleware
// and the route tree
func (s *Instance) SetupRoutes() {
	s.Router.Group(func(router chi.Router) {
		router.Use(middleware.RequestID)
		router.Use(middleware.Logger)
		router.Use(middleware.Recoverer)
		router.Route(PathPrefix, func(router chi.Router) {

			// cadence-proxy endpoints
			router.Post("/", endpoints.ProxyMessageHandler)
			router.Post("/echo", endpoints.EchoHandler)

			// endpoints for test paths
			router.Mount("/test", endpoints.TestRouter())
		})
	})
}
