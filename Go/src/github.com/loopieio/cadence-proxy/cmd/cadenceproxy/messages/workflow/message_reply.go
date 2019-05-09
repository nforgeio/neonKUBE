package workflow

import "github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"

type (
	WorkflowProxyReply struct {
		*base.ProxyReply
		ContextID string
	}
)
