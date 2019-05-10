package activity

import "github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"

type (
	ActivityReply struct {
		*base.ProxyReply
		ContextID string
	}
)
