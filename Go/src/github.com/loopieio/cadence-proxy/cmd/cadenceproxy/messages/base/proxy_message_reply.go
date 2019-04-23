package base

import (
	"errors"
	"fmt"

	cadenceclient "github.com/loopieio/cadence-proxy/cmd/cadenceproxy/cadence"
)

type (
	ProxyReply struct {
		*ProxyMessage
	}

	IProxyReply interface {
		GetProxyReply() *ProxyReply
		SetProxyReply(value *ProxyReply)
	}
)

func NewProxyReply() *ProxyReply {
	reply := new(ProxyReply)
	reply.ProxyMessage = NewProxyMessage()
	return reply
}

// GetRequestID gets a request id from a ProxyReply's ProxyMessage properties map
func (reply *ProxyReply) GetRequestID() int64 {
	return reply.GetLongProperty(RequestIDKey)
}

// SetRequestID sets the request id in a ProxyReply's ProxyMessage properties map
func (reply *ProxyReply) SetRequestID(value int64) {
	reply.SetLongProperty(RequestIDKey, value)
}

// GetErrorMessage gets an error message from a ProxyReply's ProxyMessage properties map
func (reply *ProxyReply) GetErrorMessage() *string {
	return reply.GetStringProperty(CadenceErrorTypesKey)
}

// SetErrorMessage sets an error message in a ProxyReply's ProxyMessage properties map
func (reply *ProxyReply) SetErrorMessage(value *string) {
	reply.SetStringProperty(CadenceErrorTypesKey, value)
}

// GetErrorType gets the CadenceErrorType as a string
// from a ProxyReply's ProxyMessage properties
// and returns the corresponding CadenceErrorTypes
func (reply *ProxyReply) GetErrorType() cadenceclient.CadenceErrorTypes {
	errorStringPtr := reply.GetStringProperty(CadenceErrorTypesKey)
	if errorStringPtr == nil {
		return cadenceclient.None
	}

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
func (reply *ProxyReply) SetErrorType(value cadenceclient.CadenceErrorTypes) {
	var typeString string
	switch value {
	case cadenceclient.None:
		reply.Properties[CadenceErrorTypesKey] = nil
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
		err := errors.New("Not implemented exception")
		panic(err)
	}

	reply.SetStringProperty(CadenceErrorTypesKey, &typeString)
}

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
	v, ok := target.(*ProxyReply)
	if ok {
		v.SetRequestID(reply.GetRequestID())
		v.SetErrorType(reply.GetErrorType())
		v.SetErrorMessage(reply.GetErrorMessage())
		v.SetProxyMessage(reply.ProxyMessage)
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

// GetProxyReply is an interface method that allows all
// structures that extend IProxyReply to get their nested proxy
// replies
func (reply *ProxyReply) GetProxyReply() *ProxyReply {
	return nil
}

// SetProxyReply is an interface method that allows all
// structures that extend IProxyReply to set the value of their nested
// proxy replies
func (reply *ProxyReply) SetProxyReply(value *ProxyReply) {}
