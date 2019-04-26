package base

import (
	"fmt"
)

type (

	// ProxyRequest "extends" ProxyMessage and it is
	// a type of ProxyMessage that comes into the server
	// i.e. a request
	//
	// A ProxyRequest contains a RequestId and a reference to a
	// ProxyMessage struct
	ProxyRequest struct {

		// ProxyMessage is a reference to a ProxyMessage in memory
		*ProxyMessage
	}

	// IProxyRequest is an interface for all ProxyRequest message types.
	// It allows any message type that implements the IProxyRequest interface
	// to use any methods defined.  The primary use of this interface is to
	// allow message types that implement it to get and set their nested ProxyRequest
	IProxyRequest interface {
		GetRequestID() int64
		SetRequestID(value int64)
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
	v, ok := target.(IProxyRequest)
	if ok {
		v.SetRequestID(request.GetRequestID())
	}
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

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetRequestID gets a request id from a ProxyMessage's properties
//
// returns int64 -> long as a ProxyRequest's request id from the properties map
func (request *ProxyRequest) GetRequestID() int64 {
	return request.GetLongProperty("RequestId")
}

// SetRequestID sets a request id in a ProxyRequest's ProxyMessage
// properties
//
// param value int64 -> the long representation of a ProxyRequest's
// request id to be set in the properties map
func (request *ProxyRequest) SetRequestID(value int64) {
	request.SetLongProperty("RequestId", value)
}
