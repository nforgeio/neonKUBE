package workflow

import "github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"

type (
	WorkflowReply struct {
		*base.ProxyReply
		ContextID string
	}
)
