package cluster

import (
	"fmt"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
)

type (

	// ConnectRequest is ConnectRequest of MessageType
	// ConnectRequest.
	//
	// A ConnectRequest contains a RequestId and a reference to a
	// ProxyRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	ConnectRequest struct {
		*base.ProxyRequest
		ReplyType messages.MessageType
	}
)

// NewConnectRequest is the default constructor for a ConnectRequest
//
// returns *ConnectRequest -> a reference to a newly initialized
// ConnectRequest in memory
func NewConnectRequest() *ConnectRequest {
	request := new(ConnectRequest)
	request.ProxyRequest = base.NewProxyRequest()
	request.Type = messages.ConnectRequest
	request.ReplyType = messages.ConnectReply
	return request
}

// GetEndpoints gets a ConnectRequest's endpoints value from
// its nested properties map
//
// returns *string -> a pointer to a string in memory holding the value
// of a ConnectRequest's endpoints
func (request *ConnectRequest) GetEndpoints() *string {
	return request.GetStringProperty("Endpoints")
}

// SetEndpoints sets a ConnectionRequest's endpoints in
// its nested properties map
//
// param value *string -> a pointer to a string in memory
// that holds the value to be set in the properties map
func (request *ConnectRequest) SetEndpoints(value *string) {
	request.SetStringProperty("Endpoints", value)
}

// GetIdentity gets a ConnectRequest's identity value from
// its nested properties map
//
// returns *string -> a pointer to a string in memory holding the value
// of a ConnectRequest's identity
func (request *ConnectRequest) GetIdentity() *string {
	return request.GetStringProperty("Identity")
}

// SetIdentity sets a ConnectionRequest's identity in
// its nested properties map
//
// param value *string -> a pointer to a string in memory
// that holds the value to be set in the properties map
func (request *ConnectRequest) SetIdentity(value *string) {
	request.SetStringProperty("Identity", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

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
	if v, ok := target.(*ConnectRequest); ok {
		v.SetEndpoints(request.GetEndpoints())
		v.SetIdentity(request.GetIdentity())
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

// GetRequestID inherits docs from ProxyMessage.GetRequestID()
func (request *ConnectRequest) GetRequestID() int64 {
	return request.GetLongProperty("RequestId")
}

// SetRequestID inherits docs from ProxyMessage.SetRequestID()
func (request *ConnectRequest) SetRequestID(value int64) {
	request.SetLongProperty("RequestId", value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from ProxyRequest.GetReplyType()
func (request *ConnectRequest) GetReplyType() messages.MessageType {
	return request.ReplyType
}
