package connect

import (
	"fmt"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
)

type (
	ConnectReply struct {
		*base.ProxyReply
	}
)

func NewConnectReply() *ConnectReply {
	reply := new(ConnectReply)
	reply.ProxyReply = base.NewProxyReply()
	reply.Type = messages.ConnectReply
	return reply
}

// Clone inherits docs from ProxyMessage.Clone()
func (reply *ConnectReply) Clone() base.IProxyMessage {
	connectReply := NewConnectReply()

	var messageClone base.IProxyMessage = connectReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *ConnectReply) CopyTo(target base.IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
	v, ok := target.(*ConnectReply)
	if ok {
		v.SetProxyReply(reply.ProxyReply)
	}
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (reply *ConnectReply) SetProxyMessage(value *base.ProxyMessage) {
	*reply.ProxyMessage = *value
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (reply *ConnectReply) GetProxyMessage() *base.ProxyMessage {
	return reply.ProxyMessage
}

// String inherits docs from ProxyMessage.String()
func (reply *ConnectReply) String() string {
	str := ""
	str = fmt.Sprintf("%s\n{\n", str)
	str = fmt.Sprintf("%s%s", str, reply.ProxyReply.String())
	str = fmt.Sprintf("%s}\n", str)
	return str
}

// GetProxyReply is an interface method that allows all
// structures that extend IProxyReply to get their nested proxy
// replies
func (reply *ConnectReply) GetProxyReply() *base.ProxyReply {
	return reply.ProxyReply
}

// SetProxyReply is an interface method that allows all
// structures that extend IProxyReply to set the value of their nested
// proxy replies
func (reply *ConnectReply) SetProxyReply(value *base.ProxyReply) {
	*reply.ProxyReply = *value
}
