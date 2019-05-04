package workflow

import "github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"

type (
	WorkflowProxyRequest struct {
		*base.ProxyRequest
		ContextID string
	}
)
