package activity

import (
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
)

type (
	ActivityProxyRequest struct {
		*base.ProxyRequest
		ContextID string
	}
)
