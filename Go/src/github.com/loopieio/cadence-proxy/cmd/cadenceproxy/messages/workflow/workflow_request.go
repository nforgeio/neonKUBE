package workflow

import "github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"

type (
	WorkflowRequest struct {
		*base.ProxyRequest
		ContextID string
	}
)
