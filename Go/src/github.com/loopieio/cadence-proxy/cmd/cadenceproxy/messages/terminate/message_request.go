package terminate

import (
	"fmt"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
)

type (
	TerminateRequest struct {
		*base.ProxyRequest
	}
)

func InitTerminate() {
	key := int(messages.TerminateRequest)
	base.IntToMessageStruct[key] = NewTerminateRequest()

	key = int(messages.TerminateReply)
	base.IntToMessageStruct[key] = NewTerminateReply()
}

func NewTerminateRequest() *TerminateRequest {
	request := new(TerminateRequest)
	request.ProxyRequest = base.NewProxyRequest()
	request.Type = messages.TerminateRequest
	return request
}

// Clone inherits docs from ProxyMessage.Clone()
func (request *TerminateRequest) Clone() base.IProxyMessage {
	terminateRequest := NewTerminateRequest()

	var messageClone base.IProxyMessage = terminateRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (request *TerminateRequest) CopyTo(target base.IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	v, ok := target.(*TerminateRequest)
	if ok {
		v.SetProxyRequest(request.ProxyRequest)
	}
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (request *TerminateRequest) SetProxyMessage(value *base.ProxyMessage) {
	*request.ProxyMessage = *value
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (request *TerminateRequest) GetProxyMessage() *base.ProxyMessage {
	return request.ProxyMessage
}

// String inherits docs from ProxyMessage.String()
func (request *TerminateRequest) String() string {
	str := ""
	str = fmt.Sprintf("%s\n{\n", str)
	str = fmt.Sprintf("%s%s", str, request.ProxyRequest.String())
	str = fmt.Sprintf("%s}\n", str)
	return str
}

// GetProxyRequest is an interface method that allows all
// structures that extend IProxyRequest to get their nested proxy
// requests
func (request *TerminateRequest) GetProxyRequest() *base.ProxyRequest {
	return request.ProxyRequest
}

// SetProxyRequest is an interface method that allows all
// structures that extend IProxyRequest to set the value of their nested
// proxy requests
func (request *TerminateRequest) SetProxyRequest(value *base.ProxyRequest) {
	*request.ProxyRequest = *value
}
