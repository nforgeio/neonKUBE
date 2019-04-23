package initialize

import (
	"fmt"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
)

type (
	InitializeRequest struct {
		*base.ProxyRequest
	}
)

func init() {
	key := int(messages.InitializeRequest)
	base.IntToMessageStruct[key] = NewInitializeRequest()
}

func NewInitializeRequest() *InitializeRequest {
	request := new(InitializeRequest)
	request.ProxyRequest = base.NewProxyRequest()
	request.Type = messages.InitializeRequest
	return request
}

func (request *InitializeRequest) GetLibraryAddress() *string {
	return request.GetStringProperty(base.LibraryAddressKey)
}

func (request *InitializeRequest) SetLibraryAddress(value *string) {
	request.SetStringProperty(base.LibraryAddressKey, value)
}

func (request *InitializeRequest) GetLibraryPort() *string {
	return request.GetStringProperty(base.LibraryPortKey)
}

func (request *InitializeRequest) SetLibraryPort(value *string) {
	request.SetStringProperty(base.LibraryPortKey, value)
}

// Clone inherits docs from ProxyMessage.Clone()
func (request *InitializeRequest) Clone() base.IProxyMessage {
	initializeRequest := NewInitializeRequest()

	var messageClone base.IProxyMessage = initializeRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (request *InitializeRequest) CopyTo(target base.IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	v, ok := target.(*InitializeRequest)
	if ok {
		v.SetLibraryAddress(request.GetLibraryAddress())
		v.SetLibraryPort(request.GetLibraryPort())
		v.SetProxyRequest(request.ProxyRequest)
	}
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (request *InitializeRequest) SetProxyMessage(value *base.ProxyMessage) {
	*request.ProxyMessage = *value
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (request *InitializeRequest) GetProxyMessage() *base.ProxyMessage {
	return request.ProxyMessage
}

// String inherits docs from ProxyMessage.String()
func (request *InitializeRequest) String() string {
	str := ""
	str = fmt.Sprintf("%s\n{\n", str)
	str = fmt.Sprintf("%s%s", str, request.ProxyRequest.String())
	str = fmt.Sprintf("%s}\n", str)
	return str
}

// GetProxyRequest is an interface method that allows all
// structures that extend IProxyRequest to get their nested proxy
// requests
func (request *InitializeRequest) GetProxyRequest() *base.ProxyRequest {
	return request.ProxyRequest
}

// SetProxyRequest is an interface method that allows all
// structures that extend IProxyRequest to set the value of their nested
// proxy requests
func (request *InitializeRequest) SetProxyRequest(value *base.ProxyRequest) {
	*request.ProxyRequest = *value
}
