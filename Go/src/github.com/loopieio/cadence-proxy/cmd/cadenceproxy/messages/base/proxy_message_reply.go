package base

import (
	"errors"
	"fmt"

	cadenceclient "github.com/loopieio/cadence-proxy/cmd/cadenceproxy/cadenceclient"
)

type (

	// ProxyReply is a IProxyMessage type used for replying to
	// a proxy message request.  It implements the IProxyMessage interface
	// and holds a reference to a ProxyMessage
	ProxyReply struct {

		// ProxyMessage is a reference to a ProxyMessage type
		*ProxyMessage
	}

	// IProxyReply is an interface for all ProxyReply message types.
	// It allows any message type that implements the IProxyReply interface
	// to use any methods defined.  The primary use of this interface is to
	// allow message types that implement it to get and set their nested ProxyReply
	IProxyReply interface {
		GetRequestID() int64
		SetRequestID(value int64)
		GetErrorMessage() *string
		SetErrorMessage(value *string)
		GetErrorType() cadenceclient.CadenceErrorTypes
		SetErrorType(value cadenceclient.CadenceErrorTypes)
	}
)

// NewProxyReply is the default constructor for ProxyReply.
// It creates a new ProxyReply in memory and then creates and sets
// a reference to a new ProxyMessage in the ProxyReply.
//
// returns *ProxyReply -> a pointer to a new ProxyReply in memory
func NewProxyReply() *ProxyReply {
	reply := new(ProxyReply)
	reply.ProxyMessage = NewProxyMessage()
	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *ProxyReply) Clone() IProxyMessage {
	proxyReply := NewProxyReply()
	var messageClone IProxyMessage = proxyReply
	reply.CopyTo(messageClone)
	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *ProxyReply) CopyTo(target IProxyMessage) {
	reply.ProxyMessage.CopyTo(target)
	v, ok := target.(IProxyReply)
	if ok {
		v.SetRequestID(reply.GetRequestID())
		v.SetErrorType(reply.GetErrorType())
		v.SetErrorMessage(reply.GetErrorMessage())
	}
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (reply *ProxyReply) SetProxyMessage(value *ProxyMessage) {
	*reply.ProxyMessage = *value
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (reply *ProxyReply) GetProxyMessage() *ProxyMessage {
	return reply.ProxyMessage
}

// String inherits docs from ProxyMessage.String()
func (reply *ProxyReply) String() string {
	str := ""
	str = fmt.Sprintf("%s\n", str)
	str = fmt.Sprintf("%s%s", str, reply.ProxyMessage.String())
	str = fmt.Sprintf("%s", str)
	return str
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetRequestID gets a request id from a ProxyReply's ProxyMessage properties map
//
// returns int64 -> A long corresponding to a ProxyReply's request id
func (reply *ProxyReply) GetRequestID() int64 {
	return reply.GetLongProperty("RequestId")
}

// SetRequestID sets the request id in a ProxyReply's ProxyMessage properties map
//
// param value int64 -> the long value to set as a ProxyReply's request id
func (reply *ProxyReply) SetRequestID(value int64) {
	reply.SetLongProperty("RequestId", value)
}

// GetErrorMessage gets an error message from a ProxyReply's ProxyMessage properties map
//
// returns *string -> a pointer to the string error message in the properties map
func (reply *ProxyReply) GetErrorMessage() *string {
	return reply.GetStringProperty("ErrorMessage")
}

// SetErrorMessage sets an error message in a ProxyReply's ProxyMessage properties map
//
// param value *string -> a pointer to the string value in memory to set in the properties map
func (reply *ProxyReply) SetErrorMessage(value *string) {
	reply.SetStringProperty("ErrorMessage", value)
}

// GetErrorType gets the CadenceErrorType as a string
// from a ProxyReply's ProxyMessage properties
// and returns the corresponding CadenceErrorTypes
//
// returns cadenceclient.CadenceErrorTypes -> the CadenceErrorTypes in the properties map
func (reply *ProxyReply) GetErrorType() cadenceclient.CadenceErrorTypes {

	// Grap the pointer to the error string in the properties map
	errorStringPtr := reply.GetStringProperty("ErrorType")
	if errorStringPtr == nil {
		return cadenceclient.None
	}

	// dereference and switch block on the value
	errorString := *errorStringPtr
	switch errorString {
	case "cancelled":
		return cadenceclient.Cancelled
	case "custom":
		return cadenceclient.Custom
	case "generic":
		return cadenceclient.Generic
	case "panic":
		return cadenceclient.Panic
	case "terminated":
		return cadenceclient.Terminated
	case "timeout":
		return cadenceclient.Timeout
	default:
		err := errors.New("Not implemented exception")
		panic(err)
	}
}

// SetErrorType sets the string representation of a CadenceErrorTypes
// in a ProxyReply's ProxyMessage properties map
//
// param value cadenceclient.CadenceErrorTypes -> the CadenceErrorTypes to set as a property value
// in the properties map
func (reply *ProxyReply) SetErrorType(value cadenceclient.CadenceErrorTypes) {
	var typeString string

	// switch block on the param value
	switch value {
	case cadenceclient.None:
		reply.Properties["ErrorType"] = nil
		return
	case cadenceclient.Cancelled:
		typeString = "cancelled"
		break
	case cadenceclient.Custom:
		typeString = "custom"
		break
	case cadenceclient.Generic:
		typeString = "generic"
		break
	case cadenceclient.Panic:
		typeString = "panic"
		break
	case cadenceclient.Terminated:
		typeString = "terminated"
		break
	case cadenceclient.Timeout:
		typeString = "timeout"
		break
	default:
		// panic if type is not recognized or implemented yet
		err := errors.New("Not implemented exception")
		panic(err)
	}

	// set the string in the properties map
	reply.SetStringProperty("ErrorType", &typeString)
}
