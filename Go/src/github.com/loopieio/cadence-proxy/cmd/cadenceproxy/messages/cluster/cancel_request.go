package cluster

import (
	"fmt"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
)

type (

	// CancelRequest is ProxyRequest of MessageType
	// CancelRequest.
	//
	// A CancelRequest contains a RequestId and a reference to a
	// ProxyReply struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	CancelRequest struct {
		*base.ProxyRequest
		ReplyType messages.MessageType
	}
)

// NewCancelRequest is the default constructor for a CancelRequest
//
// returns *CancelRequest -> a reference to a newly initialized
// CancelRequest in memory
func NewCancelRequest() *CancelRequest {
	request := new(CancelRequest)
	request.ProxyRequest = base.NewProxyRequest()
	request.Type = messages.CancelRequest
	request.ReplyType = messages.CancelReply
	return request
}

// GetTargetRequestID gets a CancelRequest's TargetRequestId value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a CancelRequest's TargetRequestId
func (request *CancelRequest) GetTargetRequestID() *string {
	return request.GetStringProperty("TargetRequestId")
}

// SetTargetRequestID sets a CancelRequest's TargetRequestId value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *CancelRequest) SetTargetRequestID(value *string) {
	request.SetStringProperty("TargetRequestId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (request *CancelRequest) Clone() base.IProxyMessage {
	cancelRequest := NewCancelRequest()

	var messageClone base.IProxyMessage = cancelRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (request *CancelRequest) CopyTo(target base.IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	v, ok := target.(*CancelRequest)
	if ok {
		v.SetTargetRequestID(request.GetTargetRequestID())
	}
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (request *CancelRequest) SetProxyMessage(value *base.ProxyMessage) {
	*request.ProxyMessage = *value
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (request *CancelRequest) GetProxyMessage() *base.ProxyMessage {
	return request.ProxyMessage
}

// String inherits docs from ProxyMessage.String()
func (request *CancelRequest) String() string {
	str := ""
	str = fmt.Sprintf("%s\n{\n", str)
	str = fmt.Sprintf("%s%s", str, request.ProxyRequest.String())
	str = fmt.Sprintf("%s}\n", str)
	return str
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetRequestID inherits docs from ProxyRequest.GetRequestID()
func (request *CancelRequest) GetRequestID() int64 {
	return request.GetLongProperty("RequestId")
}

// SetRequestID inherits docs from ProxyRequest.SetRequestID()
func (request *CancelRequest) SetRequestID(value int64) {
	request.SetLongProperty("RequestId", value)
}

// GetReplyType inherits docs from ProxyRequest.GetReplyType()
func (request *CancelRequest) GetReplyType() messages.MessageType {
	return request.ReplyType
}
