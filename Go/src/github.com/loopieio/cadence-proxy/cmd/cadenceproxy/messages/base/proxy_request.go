package base

import (
	"fmt"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"
)

type (

	// ProxyRequest "extends" ProxyMessage and it is
	// a type of ProxyMessage that comes into the server
	// i.e. a request
	//
	// A ProxyRequest contains a RequestId and a reference to a
	// ProxyMessage struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	ProxyRequest struct {
		*ProxyMessage
		ReplyType messages.MessageType
	}

	// IProxyRequest is an interface for all ProxyRequest message types.
	// It allows any message type that implements the IProxyRequest interface
	// to use any methods defined.  The primary use of this interface is to
	// allow message types that implement it to get and set their nested ProxyRequest
	IProxyRequest interface {
		GetReplyType() messages.MessageType
	}
)

// NewProxyRequest is the default constructor for a ProxyRequest
//
// returns *ProxyRequest -> a pointer to a newly initialized ProxyRequest
// in memory
func NewProxyRequest() *ProxyRequest {
	request := new(ProxyRequest)
	request.ProxyMessage = NewProxyMessage()
	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (request *ProxyRequest) Clone() IProxyMessage {
	proxyRequest := NewProxyRequest()
	var messageClone IProxyMessage = proxyRequest
	request.CopyTo(messageClone)
	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (request *ProxyRequest) CopyTo(target IProxyMessage) {
	request.ProxyMessage.CopyTo(target)
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (request *ProxyRequest) SetProxyMessage(value *ProxyMessage) {
	*request.ProxyMessage = *value
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (request *ProxyRequest) GetProxyMessage() *ProxyMessage {
	return request.ProxyMessage
}

// String inherits docs from ProxyMessage.String()
func (request *ProxyRequest) String() string {
	str := ""
	str = fmt.Sprintf("%s\n", str)
	str = fmt.Sprintf("%s%s", str, request.ProxyMessage.String())
	str = fmt.Sprintf("%s\n", str)
	return str
}

// GetRequestID inherits docs from ProxyMessage.GetRequestID()
func (request *ProxyRequest) GetRequestID() int64 {
	return request.GetLongProperty("RequestId")
}

// SetRequestID inherits docs from ProxyMessage.SetRequestID()
func (request *ProxyRequest) SetRequestID(value int64) {
	request.SetLongProperty("RequestId", value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType gets the MessageType used to reply to a specific
// ProxyRequest
//
// returns messages.MessageType -> the message type to reply to the
// request with
func (request *ProxyRequest) GetReplyType() messages.MessageType {
	return request.ReplyType
}
