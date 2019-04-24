package terminate

import (
	"fmt"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
)

type (
	TerminateReply struct {
		*base.ProxyReply
	}
)

func NewTerminateReply() *TerminateReply {
	reply := new(TerminateReply)
	reply.ProxyReply = base.NewProxyReply()
	reply.Type = messages.TerminateReply
	return reply
}

// Clone inherits docs from ProxyMessage.Clone()
func (reply *TerminateReply) Clone() base.IProxyMessage {
	terminateReply := NewTerminateReply()

	var messageClone base.IProxyMessage = terminateReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *TerminateReply) CopyTo(target base.IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
	v, ok := target.(*TerminateReply)
	if ok {
		v.SetProxyReply(reply.ProxyReply)
	}
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (reply *TerminateReply) SetProxyMessage(value *base.ProxyMessage) {
	*reply.ProxyMessage = *value
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (reply *TerminateReply) GetProxyMessage() *base.ProxyMessage {
	return reply.ProxyMessage
}

// String inherits docs from ProxyMessage.String()
func (reply *TerminateReply) String() string {
	str := ""
	str = fmt.Sprintf("%s\n{\n", str)
	str = fmt.Sprintf("%s%s", str, reply.ProxyReply.String())
	str = fmt.Sprintf("%s}\n", str)
	return str
}

// GetProxyReply is an interface method that allows all
// structures that extend IProxyReply to get their nested proxy
// replies
func (reply *TerminateReply) GetProxyReply() *base.ProxyReply {
	return reply.ProxyReply
}

// SetProxyReply is an interface method that allows all
// structures that extend IProxyReply to set the value of their nested
// proxy replies
func (reply *TerminateReply) SetProxyReply(value *base.ProxyReply) {
	*reply.ProxyReply = *value
}
