package initialize

import (
	"fmt"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
)

type (
	InitializeReply struct {
		*base.ProxyReply
	}
)

func NewInitializeReply() *InitializeReply {
	reply := new(InitializeReply)
	reply.ProxyReply = base.NewProxyReply()
	reply.Type = messages.InitializeReply
	return reply
}

// Clone inherits docs from ProxyMessage.Clone()
func (reply *InitializeReply) Clone() base.IProxyMessage {
	initializeReply := NewInitializeReply()

	var messageClone base.IProxyMessage = initializeReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *InitializeReply) CopyTo(target base.IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
	v, ok := target.(*InitializeReply)
	if ok {
		v.SetProxyReply(reply.ProxyReply)
	}
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (reply *InitializeReply) SetProxyMessage(value *base.ProxyMessage) {
	*reply.ProxyMessage = *value
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (reply *InitializeReply) GetProxyMessage() *base.ProxyMessage {
	return reply.ProxyMessage
}

// String inherits docs from ProxyMessage.String()
func (reply *InitializeReply) String() string {
	str := ""
	str = fmt.Sprintf("%s\n{\n", str)
	str = fmt.Sprintf("%s%s", str, reply.ProxyReply.String())
	str = fmt.Sprintf("%s}\n", str)
	return str
}

// GetProxyReply is an interface method that allows all
// structures that extend IProxyReply to get their nested proxy
// replies
func (reply *InitializeReply) GetProxyReply() *base.ProxyReply {
	return reply.ProxyReply
}

// SetProxyReply is an interface method that allows all
// structures that extend IProxyReply to set the value of their nested
// proxy replies
func (reply *InitializeReply) SetProxyReply(value *base.ProxyReply) {
	*reply.ProxyReply = *value
}
