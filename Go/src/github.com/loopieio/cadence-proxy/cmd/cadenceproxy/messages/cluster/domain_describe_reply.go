package cluster

import (
	"errors"
	"fmt"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
)

type (

	// DomainDescribeReply is a ProxyReply of MessageType
	// DomainDescribeReply.  It holds a reference to a ProxyReply in memory
	DomainDescribeReply struct {
		*base.ProxyReply
	}
)

// NewDomainDescribeReply is the default constructor for
// a DomainDescribeReply
//
// returns *DomainDescribeReply -> a pointer to a newly initialized
// DomainDescribeReply in memory
func NewDomainDescribeReply() *DomainDescribeReply {
	reply := new(DomainDescribeReply)
	reply.ProxyReply = base.NewProxyReply()
	reply.Type = messages.DomainDescribeReply
	return reply
}

// GetDomainInfoName gets the DomainInfoName property as a string
// pointer from a DomainDescribeReply's properties map
//
// returns *string -> pointer to a string from a DomainDescribeReply's properties map
func (reply *DomainDescribeReply) GetDomainInfoName() *string {
	return reply.GetStringProperty("DomainInfoName")
}

// SetDomainInfoName sets the DomainInfoName property as a string
// pointer in a DomainDescribeReply's properties map
//
// param value *string -> pointer to the string value to set
// as the DomainDescribeReply's DomainInfoName in its properties map
func (reply *DomainDescribeReply) SetDomainInfoName(value *string) {
	reply.SetStringProperty("DomainInfoName", value)
}

// GetDomainInfoDescription gets the DomainInfoDescription property as a string
// pointer from a DomainDescribeReply's properties map
//
// returns *string -> pointer to a string from a DomainDescribeReply's properties map
func (reply *DomainDescribeReply) GetDomainInfoDescription() *string {
	return reply.GetStringProperty("DomainInfoDescription")
}

// SetDomainInfoDescription sets the DomainInfoDescription property as a string
// pointer in a DomainDescribeReply's properties map
//
// param value *string -> pointer to the string value to set
// as the DomainDescribeReply's DomainInfoDescription in its properties map
func (reply *DomainDescribeReply) SetDomainInfoDescription(value *string) {
	reply.SetStringProperty("DomainInfoDescription", value)
}

// GetDomainInfoStatus gets the DomainInfoStatus property as a string
// pointer from a DomainDescribeReply's properties map
//
// returns DomainStatus -> the DomainStatus of the Domain being described
// from a DomainDescribeReply's properties map
func (reply *DomainDescribeReply) GetDomainInfoStatus() messages.DomainStatus {
	domainInfoStatusPtr := reply.GetStringProperty("DomainInfoStatus")
	if domainInfoStatusPtr == nil {
		return messages.StatusUnspecified
	}

	// dereference and switch block on the value
	domainStatus := *domainInfoStatusPtr
	switch domainStatus {
	case "REGISTERED":
		return messages.Registered
	case "DEPRECATED":
		return messages.Deprecated
	default:
		err := errors.New("domainStatus not implemented exception")
		panic(err)
	}
}

// SetDomainInfoStatus sets the DomainInfoStatus property as a string
// pointer in a DomainDescribeReply's properties map
//
// param value DomainStatus -> DomainStatus value to set
// as the DomainDescribeReply's DomainInfoStatus in its properties map
func (reply *DomainDescribeReply) SetDomainInfoStatus(value messages.DomainStatus) {
	var statusString string

	// switch block on the param value
	switch value {
	case messages.StatusUnspecified:
		reply.Properties["DomainInfoStatus"] = nil
		return
	case messages.Registered:
		statusString = "REGISTERED"
	case messages.Deprecated:
		statusString = "DEPRECATED"
	default:

		// panic if type is not recognized or implemented yet
		err := errors.New("not implemented exception")
		panic(err)
	}

	// set the string in the properties map
	reply.SetStringProperty("DomainInfoStatus", &statusString)
}

// GetDomainInfoOwnerEmail gets the DomainInfoOwnerEmail property as a string
// pointer from a DomainDescribeReply's properties map
//
// returns *string -> pointer to a string from a DomainDescribeReply's properties map
func (reply *DomainDescribeReply) GetDomainInfoOwnerEmail() *string {
	return reply.GetStringProperty("DomainInfoOwnerEmail")
}

// SetDomainInfoOwnerEmail sets the DomainInfoOwnerEmail property as a string
// pointer in a DomainDescribeReply's properties map
//
// param value *string -> pointer to the string value to set
// as the DomainDescribeReply's DomainInfoOwnerEmail in its properties map
func (reply *DomainDescribeReply) SetDomainInfoOwnerEmail(value *string) {
	reply.SetStringProperty("DomainInfoOwnerEmail", value)
}

// GetConfigurationRetentionDays gets the ConfigurationRetentionDays property
// as an int32 from a DomainDescribeReply's properties map
//
// returns int32 -> int32 domain retention in days value from a DomainDescribeReply's
// properties map
func (reply *DomainDescribeReply) GetConfigurationRetentionDays() int32 {
	return reply.GetIntProperty("ConfigurationRetentionDays")
}

// SetConfigurationRetentionDays sets the ConfigurationRetentionDays property as
// an int32 pointer in a DomainDescribeReply's properties map
//
// param value int32 -> int32 domain retention in days to set
// as the DomainDescribeReply's ConfigurationRetentionDays in its properties map
func (reply *DomainDescribeReply) SetConfigurationRetentionDays(value int32) {
	reply.SetIntProperty("ConfigurationRetentionDays", value)
}

// GetConfigurationEmitMetrics gets the ConfigurationEmitMetrics property
// as a bool from a DomainDescribeReply's properties map
//
// returns bool -> bool ConfigurationEmitMetrics value from a DomainDescribeReply's
// properties map
func (reply *DomainDescribeReply) GetConfigurationEmitMetrics() bool {
	return reply.GetBoolProperty("ConfigurationEmitMetrics")
}

// SetConfigurationEmitMetrics sets the ConfigurationEmitMetrics property as
// a bool in a DomainDescribeReply's properties map
//
// param value bool -> bool that enables metric generation
// as the DomainDescribeReply's ConfigurationEmitMetrics in its properties map
func (reply *DomainDescribeReply) SetConfigurationEmitMetrics(value bool) {
	reply.SetBoolProperty("ConfigurationEmitMetrics", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *DomainDescribeReply) Clone() base.IProxyMessage {
	domainDescribeReply := NewDomainDescribeReply()
	var messageClone base.IProxyMessage = domainDescribeReply

	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *DomainDescribeReply) CopyTo(target base.IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
	if v, ok := target.(*DomainDescribeReply); ok {
		v.SetDomainInfoName(reply.GetDomainInfoName())
		v.SetConfigurationEmitMetrics(reply.GetConfigurationEmitMetrics())
		v.SetConfigurationRetentionDays(reply.GetConfigurationRetentionDays())
		v.SetDomainInfoDescription(reply.GetDomainInfoDescription())
		v.SetDomainInfoStatus(reply.GetDomainInfoStatus())
		v.SetDomainInfoOwnerEmail(reply.GetDomainInfoOwnerEmail())
	}
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (reply *DomainDescribeReply) SetProxyMessage(value *base.ProxyMessage) {
	*reply.ProxyMessage = *value
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (reply *DomainDescribeReply) GetProxyMessage() *base.ProxyMessage {
	return reply.ProxyMessage
}

// String inherits docs from ProxyMessage.String()
func (reply *DomainDescribeReply) String() string {
	str := ""
	str = fmt.Sprintf("%s\n{\n", str)
	str = fmt.Sprintf("%s%s", str, reply.ProxyReply.String())
	str = fmt.Sprintf("%s}\n", str)
	return str
}

// GetRequestID inherits docs from ProxyMessage.GetRequestID()
func (reply *DomainDescribeReply) GetRequestID() int64 {
	return reply.GetLongProperty("RequestId")
}

// SetRequestID inherits docs from ProxyMessage.SetRequestID()
func (reply *DomainDescribeReply) SetRequestID(value int64) {
	reply.SetLongProperty("RequestId", value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from ProxyReply.GetError()
func (reply *DomainDescribeReply) GetError() *string {
	return reply.GetStringProperty("Error")
}

// SetError inherits docs from ProxyReply.SetError()
func (reply *DomainDescribeReply) SetError(value *string) {
	reply.SetStringProperty("Error", value)
}

// GetErrorDetails inherits docs from ProxyReply.GetErrorDetails()
func (reply *DomainDescribeReply) GetErrorDetails() *string {
	return reply.GetStringProperty("ErrorDetails")
}

// SetErrorDetails inherits docs from ProxyReply.SetErrorDetails()
func (reply *DomainDescribeReply) SetErrorDetails(value *string) {
	reply.SetStringProperty("ErrorDetails", value)
}

// GetErrorType inherits docs from ProxyReply.GetErrorType()
func (reply *DomainDescribeReply) GetErrorType() messages.CadenceErrorTypes {

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

// SetErrorType inherits docs from ProxyReply.SetErrorType()
func (reply *DomainDescribeReply) SetErrorType(value messages.CadenceErrorTypes) {
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
