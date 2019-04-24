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

		// RequestId is the unique id of the ProxyRequest
		RequestId int64
	}

	// IProxyRequest is an interface for all ProxyRequest message types.
	// It allows any message type that implements the IProxyRequest interface
	// to use any methods defined.  The primary use of this interface is to
	// allow message types that implement it to get and set their nested ProxyRequest
	IProxyRequest interface {
		GetProxyRequest() *ProxyRequest
		SetProxyRequest(value *ProxyRequest)
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

// GetRequestID gets a request id from a ProxyMessage's properties
//
// returns int64 -> long as a ProxyRequest's request id from the properties map
func (request *ProxyRequest) GetRequestID() int64 {
	return request.GetLongProperty(RequestIDKey)
}

// SetRequestID sets a request id in a ProxyRequest's ProxyMessage
// properties
//
// param value int64 -> the long representation of a ProxyRequest's
// request id to be set in the properties map
func (request *ProxyRequest) SetRequestID(value int64) {
	request.SetLongProperty(RequestIDKey, value)
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
	v, ok := target.(*ProxyRequest)
	if ok {
		v.SetRequestID(request.GetRequestID())
		v.SetProxyMessage(request.ProxyMessage)
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

// GetProxyRequest is an interface method that allows all
// structures that extend IProxyRequest to get their nested proxy
// requests
//
// returns *ProxyRequest -> a pointer to a IProxyRequest's nested proxy request
func (request *ProxyRequest) GetProxyRequest() *ProxyRequest {
	return nil
}

// SetProxyRequest is an interface method that allows all
// structures that extend IProxyRequest to set the value of their nested
// proxy requests
//
// param value *ProxyRequest -> a pointer to the ProxyRequest in memory that
// you want to set the instance IProxyRequest's value as
func (request *ProxyRequest) SetProxyRequest(value *ProxyRequest) {}
