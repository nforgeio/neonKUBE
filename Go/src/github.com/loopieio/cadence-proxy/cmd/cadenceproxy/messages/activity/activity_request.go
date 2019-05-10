package activity

import (
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
)

type (
	ActivityRequest struct {
		*base.ProxyRequest
		ContextID string
	}
)
