package connect

import (
	"fmt"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
)

type (
	ConnectRequest struct {
		*base.ProxyRequest
	}
)

func init() {
	key := int(messages.ConnectRequest)
	base.IntToMessageStruct[key] = NewConnectRequest()
}

func NewConnectRequest() *ConnectRequest {
	request := new(ConnectRequest)
	request.ProxyRequest = base.NewProxyRequest()
	request.Type = messages.ConnectRequest
	return request
}

func (request *ConnectRequest) GetEndpoints() *string {
	return request.GetStringProperty(base.EndpointsKey)
}

func (request *ConnectRequest) SetEndpoints(value *string) {
	request.SetStringProperty(base.EndpointsKey, value)
}

func (request *ConnectRequest) GetDomain() *string {
	return request.GetStringProperty(base.DomainKey)
}

func (request *ConnectRequest) SetDomain(value *string) {
	request.SetStringProperty(base.DomainKey, value)
}

func (request *ConnectRequest) GetIdentity() *string {
	return request.GetStringProperty(base.IdentityKey)
}

func (request *ConnectRequest) SetIdentity(value *string) {
	request.SetStringProperty(base.IdentityKey, value)
}

// Clone inherits docs from ProxyMessage.Clone()
func (request *ConnectRequest) Clone() base.IProxyMessage {
	connectRequest := NewConnectRequest()

	var messageClone base.IProxyMessage = connectRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (request *ConnectRequest) CopyTo(target base.IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	v, ok := target.(*ConnectRequest)
	if ok {
		v.SetEndpoints(request.GetEndpoints())
		v.SetDomain(request.GetDomain())
		v.SetIdentity(request.GetIdentity())
		v.SetProxyRequest(request.ProxyRequest)
	}
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (request *ConnectRequest) SetProxyMessage(value *base.ProxyMessage) {
	*request.ProxyMessage = *value
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (request *ConnectRequest) GetProxyMessage() *base.ProxyMessage {
	return request.ProxyMessage
}

// String inherits docs from ProxyMessage.String()
func (request *ConnectRequest) String() string {
	str := ""
	str = fmt.Sprintf("%s\n{\n", str)
	str = fmt.Sprintf("%s%s", str, request.ProxyRequest.String())
	str = fmt.Sprintf("%s}\n", str)
	return str
}

// GetProxyRequest is an interface method that allows all
// structures that extend IProxyRequest to get their nested proxy
// requests
func (request *ConnectRequest) GetProxyRequest() *base.ProxyRequest {
	return request.ProxyRequest
}

// SetProxyRequest is an interface method that allows all
// structures that extend IProxyRequest to set the value of their nested
// proxy requests
func (request *ConnectRequest) SetProxyRequest(value *base.ProxyRequest) {
	*request.ProxyRequest = *value
}
