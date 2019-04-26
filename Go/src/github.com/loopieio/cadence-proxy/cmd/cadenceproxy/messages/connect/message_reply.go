package connect

import (
	"errors"
	"fmt"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/cadenceclient"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
)

type (

	// ConnectReply is a ProxyReply of MessageType
	// ConnectReply.  It holds a reference to a ProxyReply in memory
	ConnectReply struct {
		*base.ProxyReply
	}
)

// NewConnectReply is the default constructor for
// a ConnectReply
//
// returns *ConnectReply -> a pointer to a newly initialized
// ConnectReply in memory
func NewConnectReply() *ConnectReply {
	reply := new(ConnectReply)
	reply.ProxyReply = base.NewProxyReply()
	reply.Type = messages.ConnectReply
	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *ConnectReply) Clone() base.IProxyMessage {
	connectReply := NewConnectReply()
	var messageClone base.IProxyMessage = connectReply
	reply.CopyTo(messageClone)
	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *ConnectReply) CopyTo(target base.IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (reply *ConnectReply) SetProxyMessage(value *base.ProxyMessage) {
	*reply.ProxyMessage = *value
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (reply *ConnectReply) GetProxyMessage() *base.ProxyMessage {
	return reply.ProxyMessage
}

// String inherits docs from ProxyMessage.String()
func (reply *ConnectReply) String() string {
	str := ""
	str = fmt.Sprintf("%s\n{\n", str)
	str = fmt.Sprintf("%s%s", str, reply.ProxyReply.String())
	str = fmt.Sprintf("%s}\n", str)
	return str
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetRequestID gets a request id from a ProxyReply's ProxyMessage properties map
//
// returns int64 -> A long corresponding to a ProxyReply's request id
func (reply *ConnectReply) GetRequestID() int64 {
	return reply.GetLongProperty("RequestId")
}

// SetRequestID sets the request id in a ProxyReply's ProxyMessage properties map
//
// param value int64 -> the long value to set as a ProxyReply's request id
func (reply *ConnectReply) SetRequestID(value int64) {
	reply.SetLongProperty("RequestId", value)
}

// GetErrorMessage gets an error message from a ProxyReply's ProxyMessage properties map
//
// returns *string -> a pointer to the string error message in the properties map
func (reply *ConnectReply) GetErrorMessage() *string {
	return reply.GetStringProperty("ErrorMessage")
}

// SetErrorMessage sets an error message in a ProxyReply's ProxyMessage properties map
//
// param value *string -> a pointer to the string value in memory to set in the properties map
func (reply *ConnectReply) SetErrorMessage(value *string) {
	reply.SetStringProperty("ErrorMessage", value)
}

// GetErrorType gets the CadenceErrorType as a string
// from a ProxyReply's ProxyMessage properties
// and returns the corresponding CadenceErrorTypes
//
// returns cadenceclient.CadenceErrorTypes -> the CadenceErrorTypes in the properties map
func (reply *ConnectReply) GetErrorType() cadenceclient.CadenceErrorTypes {

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
func (reply *ConnectReply) SetErrorType(value cadenceclient.CadenceErrorTypes) {
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
