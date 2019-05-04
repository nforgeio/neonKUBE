package activity

import "github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"

type (
	ActivityProxyReply struct {
		*base.ProxyReply
		ContextID string
	}
)
