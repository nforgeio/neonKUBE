package base

import (
	"errors"
	"fmt"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"
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
		GetError() *string
		SetError(value *string)
		GetErrorType() messages.CadenceErrorTypes
		SetErrorType(value messages.CadenceErrorTypes)
		GetErrorDetails() *string
		SetErrorDetails(value *string)
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

	// set the ProxyReply error fields
	reply.SetErrorType(messages.None)
	reply.SetError(nil)
	reply.SetErrorDetails(nil)

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
	if v, ok := target.(IProxyReply); ok {
		v.SetErrorType(reply.GetErrorType())
		v.SetError(reply.GetError())
		v.SetErrorDetails(reply.GetErrorDetails())
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
	str = fmt.Sprintf("%s\n", str)
	return str
}

// GetRequestID inherits docs from ProxyMessage.GetRequestID()
func (reply *ProxyReply) GetRequestID() int64 {
	return reply.GetLongProperty("RequestId")
}

// SetRequestID inherits docs from ProxyMessage.SetRequestID()
func (reply *ProxyReply) SetRequestID(value int64) {
	reply.SetLongProperty("RequestId", value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError gets an error message from a ProxyReply's ProxyMessage properties map
//
// returns *string -> a pointer to the string error message in the properties map
func (reply *ProxyReply) GetError() *string {
	return reply.GetStringProperty("Error")
}

// SetError sets an error message in a ProxyReply's ProxyMessage properties map
//
// param value *string -> a pointer to the string value in memory to set in the properties map
func (reply *ProxyReply) SetError(value *string) {
	reply.SetStringProperty("Error", value)
}

// GetErrorDetails gets error details from a ProxyReply's ProxyMessage properties map
//
// returns *string -> a pointer to the string error details in the properties map
func (reply *ProxyReply) GetErrorDetails() *string {
	return reply.GetStringProperty("ErrorDetails")
}

// SetErrorDetails sets error details in a ProxyReply's ProxyMessage properties map
//
// param value *string -> a pointer to the string value in memory to set in the properties map
func (reply *ProxyReply) SetErrorDetails(value *string) {
	reply.SetStringProperty("ErrorDetails", value)
}

// GetErrorType gets the CadenceErrorType as a string
// from a ProxyReply's ProxyMessage properties
// and returns the corresponding CadenceErrorTypes
//
// returns messages.CadenceErrorTypes -> the CadenceErrorTypes in the properties map
func (reply *ProxyReply) GetErrorType() messages.CadenceErrorTypes {

	// Grap the pointer to the error string in the properties map
	errorStringPtr := reply.GetStringProperty("ErrorType")
	if errorStringPtr == nil {
		return messages.None
	}

	// dereference and switch block on the value
	errorString := *errorStringPtr
	switch errorString {
	case "cancelled":
		return messages.Cancelled
	case "custom":
		return messages.Custom
	case "generic":
		return messages.Generic
	case "panic":
		return messages.Panic
	case "terminated":
		return messages.Terminated
	case "timeout":
		return messages.Timeout
	default:
		err := errors.New("not implemented exception")
		panic(err)
	}
}

// SetErrorType sets the string representation of a CadenceErrorTypes
// in a ProxyReply's ProxyMessage properties map
//
// param value messages.CadenceErrorTypes -> the CadenceErrorTypes to set as a property value
// in the properties map
func (reply *ProxyReply) SetErrorType(value messages.CadenceErrorTypes) {
	var typeString string

	// switch block on the param value
	switch value {
	case messages.None:
		reply.Properties["ErrorType"] = nil
		return
	case messages.Cancelled:
		typeString = "cancelled"
	case messages.Custom:
		typeString = "custom"
	case messages.Generic:
		typeString = "generic"
	case messages.Panic:
		typeString = "panic"
	case messages.Terminated:
		typeString = "terminated"
	case messages.Timeout:
		typeString = "timeout"
	default:
		// panic if type is not recognized or implemented yet
		err := errors.New("not implemented exception")
		panic(err)
	}

	// set the string in the properties map
	reply.SetStringProperty("ErrorType", &typeString)
}
