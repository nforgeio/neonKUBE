//-----------------------------------------------------------------------------
// FILE:		proxy_message.go
// CONTRIBUTOR: John C Burns
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

package messages

import (
	"bytes"
	"encoding/base64"
	"encoding/binary"
	"encoding/json"
	"fmt"
	"io"
	"math"
	"reflect"
	"strconv"
	"time"

	"github.com/a3linux/amazon-ssm-agent/agent/times"

	messagetypes "github.com/cadence-proxy/internal/messages/types"
)

type (

	// ProxyMessage represents an encoded Cadence ProxyMessage
	// Type is the cadence ProxyMessage as an enumeration
	// Properties are the Properties for the ProxyMessages
	// Attachments are any data attachments (in bytes) that
	// are needed to perform the ProxyMessage
	ProxyMessage struct {
		Type        messagetypes.MessageType
		Properties  map[string]*string
		Attachments [][]byte
	}

	// IProxyMessage is an interface that all message types implement
	// Allows message types that implement this interface to:
	//
	// Clone -> Create a replica of itself in a new IProxyMessage in memory.
	// The replica does not share any pointers to data in the original IProxyMessage
	//
	// CopyTo -> Helper method used by Clone() to copy replica data from the original
	// to a cloned message
	//
	// SetProxyMessage -> Set a message's ProxyMessage to the values of the ProxyMessage
	// passed as a parameter.  It does not set pointers, but the actual values of the
	// input ProxyMessage
	//
	// GetProxyMessage -> Gets a pointer to a message's ProxyMessage
	//
	// String -> Builds a string representation of a message that can be used for debug output
	IProxyMessage interface {
		Clone() IProxyMessage
		CopyTo(target IProxyMessage)
		SetProxyMessage(value *ProxyMessage)
		GetProxyMessage() *ProxyMessage
		GetRequestID() int64
		SetRequestID(value int64)
		GetClientID() int64
		SetClientID(value int64)
		GetType() messagetypes.MessageType
		SetType(value messagetypes.MessageType)
	}
)

// NewProxyMessage creates a new ProxyMessage in memory, initializes
// its Properties map and Attachments [][]byte
//
// returns *ProxyMessage -> pointer to the newly created ProxyMessage
// in memory
func NewProxyMessage() *ProxyMessage {
	message := new(ProxyMessage)
	message.Properties = make(map[string]*string)
	message.Attachments = make([][]byte, 0)
	message.SetType(messagetypes.Unspecified)

	return message
}

// Deserialize takes a pointer to an existing bytes.Buffer of bytes.
// It then reads the bytes from the buffer and deserializes them into
// a ProxyMessage instance
//
// param buf *bytes.Buffer -> bytes.Buffer of bytes holding an encoded
// ProxyMessage
//
// param allowUnspecified -> bool that indicates that allowing a ProxyMessage
// of unspecified message type can be allowed during deserialization.
// This is used for unit testing
//
// param typeCode ...interface{} -> an optional interface type to create a specific message
// type when allowUnspecified is true.
// Used for unit testing only
//
// return ProxyMessage -> ProxyMessage initialized using values encoded in
// bytes from the bytes.Buffer
//
// return Error -> an error deserializing does not work
func Deserialize(buf *bytes.Buffer, allowUnspecified bool, typeCode ...string) (IProxyMessage, error) {

	// New IProxyMessage that will be
	// returned upon a successful deserialization
	var message IProxyMessage

	// get the message type
	messageType := messagetypes.MessageType(readInt32(buf))

	// check for allow unspecified
	if !allowUnspecified {
		message = CreateNewTypedMessage(messageType)
		if message == nil {
			err := fmt.Errorf("unexpected message type %v", messageType)
			return nil, err
		}

	} else {
		message = handleUnspecifiedMessageType(typeCode)
	}

	// point to message's ProxyMessage
	// get property count
	// set the properties
	proxyMessage := message.GetProxyMessage()
	propertyCount := int(readInt32(buf))
	for i := 0; i < propertyCount; i++ {
		key := readString(buf)
		value := readString(buf)
		proxyMessage.Properties[*key] = value
	}

	// get attachment count
	// set the attachments
	attachmentCount := int(readInt32(buf))
	for i := 0; i < attachmentCount; i++ {
		length := int(readInt32(buf))
		if length == -1 {
			proxyMessage.Attachments = append(proxyMessage.Attachments, nil)
		} else if length == 0 {
			proxyMessage.Attachments = append(proxyMessage.Attachments, make([]byte, 0))
		} else {
			proxyMessage.Attachments = append(proxyMessage.Attachments, buf.Next(length))
		}
	}

	return message, nil
}

func handleUnspecifiedMessageType(typeCode []string) IProxyMessage {
	if len(typeCode) > 0 {
		switch typeCode[0] {
		case "ProxyRequest":
			return NewProxyRequest()
		case "ProxyReply":
			return NewProxyReply()
		case "WorkflowRequest":
			return NewWorkflowRequest()
		case "WorkflowReply":
			return NewWorkflowReply()
		case "ActivityReply":
			return NewActivityReply()
		case "ActivityRequest":
			return NewActivityRequest()
		default:
			return NewProxyMessage()
		}
	} else {
		return NewProxyMessage()
	}
}

func writeInt32(w io.Writer, value int32) {
	err := binary.Write(w, binary.LittleEndian, value)
	if err != nil {
		panic(err)
	}
}

func writeString(buf *bytes.Buffer, value *string) {
	if value == nil {
		err := binary.Write(buf, binary.LittleEndian, int32(-1))
		if err != nil {
			panic(err)
		}

	} else {
		err := binary.Write(buf, binary.LittleEndian, int32(len(*value)))
		if err != nil {
			panic(err)
		}
		_, err = buf.WriteString(*value)
		if err != nil {
			panic(err)
		}
	}
}

func readString(buf *bytes.Buffer) *string {
	var strPtr *string
	length := int(readInt32(buf))
	if length == -1 {
		strPtr = nil
	} else if length == 0 {
		str := ""
		strPtr = &str
	} else {
		strBytes := buf.Next(length)
		str := string(strBytes)
		strPtr = &str
	}

	return strPtr
}

func readInt32(buf *bytes.Buffer) int32 {
	var num int32
	intBytes := buf.Next(4)
	reader := bytes.NewReader(intBytes)

	// Read the []byte into the byte.Reader
	// LittleEndian byte order
	err := binary.Read(reader, binary.LittleEndian, &num)
	if err != nil {
		if err.Error() == "EOF" {
			return 0
		}
		panic(err)
	}

	return num
}

// -------------------------------------------------------------------------
// Instance methods for a ProxyMessage type

// Serialize is called on a ProxyMessage instance and
// serializes it into a []byte for sending over a network
//
// param allowUnspecified bool -> a bool indicating whether to allow
// unspecified message types (encoded as []byte) as input.
// Used for unit testing
//
// return []byte -> the ProxyMessage instance encoded as a []byte
//
// return error -> an error if serialization goes wrong
func (proxyMessage *ProxyMessage) Serialize(allowUnspecified bool) ([]byte, error) {

	// if the type code is not to be ignored, but the message
	// type is unspecified, then throw an error
	if (!allowUnspecified) && (proxyMessage.Type == messagetypes.Unspecified) {
		err := fmt.Errorf("proxy message has not initialized its [%v] property", proxyMessage.Type)
		return nil, err
	}

	buf := new(bytes.Buffer)

	// write message type to the buffer LittleEndian byte order
	writeInt32(buf, int32(proxyMessage.Type))

	// write num properties to the buffer LittleEndian byte order
	writeInt32(buf, int32(len(proxyMessage.Properties)))

	// write the properties to the buffer
	for k, v := range proxyMessage.Properties {
		writeString(buf, &k)
		writeString(buf, v)
	}

	// write num of attachments the buffer LittleEndian byte order
	writeInt32(buf, int32(len(proxyMessage.Attachments)))

	for _, attachment := range proxyMessage.Attachments {
		if attachment == nil {
			// write to the buffer LittleEndian byte order
			writeInt32(buf, int32(-1))
		} else {
			// write to the buffer LittleEndian byte order
			writeInt32(buf, int32(len(attachment)))
			_, err := buf.Write(attachment)
			if err != nil {
				return nil, err
			}
		}
	}

	// return the bytes in the buffer as a []byte
	// and a nil error
	return buf.Bytes(), nil
}

// CopyTo implemented by derived classes to copy
// message properties to another message instance
// during a Clone() operation
func (proxyMessage *ProxyMessage) CopyTo(target IProxyMessage) {
	target.SetRequestID(proxyMessage.GetRequestID())
	target.SetClientID(proxyMessage.GetClientID())
	target.SetType(proxyMessage.GetType())
}

// Clone is implemented by derived classes to make a clone of themselves
// for echo testing purposes
// This clone is not a pointer to the ProxyMessage being cloned, but
// a replica with the same values
func (proxyMessage *ProxyMessage) Clone() IProxyMessage {
	return nil
}

// SetProxyMessage is implemented by derived classes to set the value
// of a ProxyMessage in an IProxyMessage interface
func (proxyMessage *ProxyMessage) SetProxyMessage(value *ProxyMessage) {
	*proxyMessage = *value
}

// GetProxyMessage is implemented by derived classes to get the value of
// a ProxyMessage in an IProxyMessage interface
func (proxyMessage *ProxyMessage) GetProxyMessage() *ProxyMessage {
	return proxyMessage
}

// GetRequestID gets a request id from a ProxyMessage properties map
//
// returns int64 -> A long corresponding to a ProxyMessages's request id
func (proxyMessage *ProxyMessage) GetRequestID() int64 {
	return proxyMessage.GetLongProperty("RequestId")
}

// SetRequestID sets the request id in a ProxyMessage properties map
//
// param value int64 -> the long value to set as a ProxyMessage request id
func (proxyMessage *ProxyMessage) SetRequestID(value int64) {
	proxyMessage.SetLongProperty("RequestId", value)
}

// GetClientID gets the ClientId property from a ProxyMessage's properties map
//
// returns int64 -> A int64 corresponding to a ProxyMessage's ClientId
func (proxyMessage *ProxyMessage) GetClientID() int64 {
	return proxyMessage.GetLongProperty("ClientId")
}

// SetClientID sets the ClientId property in a ProxyMessage's properties map
//
// param value int64 -> the int64 value to set as a ProxyMessage's ClientId
func (proxyMessage *ProxyMessage) SetClientID(value int64) {
	proxyMessage.SetLongProperty("ClientId", value)
}

// GetType gets the message type
//
// returns messagetypes.MessageType -> the type of message
func (proxyMessage *ProxyMessage) GetType() messagetypes.MessageType {
	return proxyMessage.Type
}

// SetType sets the message type
//
// param value messagetypes.MessageType -> the message type to set
func (proxyMessage *ProxyMessage) SetType(value messagetypes.MessageType) {
	proxyMessage.Type = value
}

// -------------------------------------------------------------------------
// Helper methods derived classes can use for retreiving typed message properties

// GetStringProperty is a method for retrieving a string property
func (proxyMessage *ProxyMessage) GetStringProperty(key string) *string {
	return proxyMessage.Properties[key]
}

// GetIntProperty is a helper method for retrieving a 32-bit integer property
func (proxyMessage *ProxyMessage) GetIntProperty(key string, def ...int32) int32 {
	if proxyMessage.Properties[key] != nil {
		value, err := strconv.ParseInt(*proxyMessage.Properties[key], 10, 32)
		if err != nil {
			panic(err)
		}

		return int32(value)
	}

	if len(def) > 0 {
		return def[0]
	}

	return 0
}

// GetLongProperty is a helper method for retrieving a 64-bit long integer property
func (proxyMessage *ProxyMessage) GetLongProperty(key string, def ...int64) int64 {
	if proxyMessage.Properties[key] != nil {
		value, err := strconv.ParseInt(*proxyMessage.Properties[key], 10, 64)
		if err != nil {
			panic(err)
		}

		return value
	}

	if len(def) > 0 {
		return def[0]
	}

	return 0
}

// GetBoolProperty is a helper method for retrieving a boolean property
func (proxyMessage *ProxyMessage) GetBoolProperty(key string, def ...bool) bool {
	if proxyMessage.Properties[key] != nil {
		value, err := strconv.ParseBool(*proxyMessage.Properties[key])
		if err != nil {
			panic(err)
		}

		return value
	}

	if len(def) > 0 {
		return def[0]
	}

	return false
}

// GetDoubleProperty is a helper method for retrieving a double property
func (proxyMessage *ProxyMessage) GetDoubleProperty(key string, def ...float64) float64 {
	if proxyMessage.Properties[key] != nil {
		value, err := strconv.ParseFloat(*proxyMessage.Properties[key], 64)
		if err != nil {
			return math.NaN()
		}

		return value
	}

	if len(def) > 0 {
		return def[0]
	}

	return 0.0
}

// GetDateTimeProperty is a helper method for retrieving a DateTime property
func (proxyMessage *ProxyMessage) GetDateTimeProperty(key string, def ...time.Time) time.Time {
	if proxyMessage.Properties[key] != nil {
		return times.ParseIso8601UTC(*proxyMessage.Properties[key])
	}

	t := time.Time{}
	if len(def) > 0 {
		t = def[0]
	}

	zeroTimeStr := times.ToIso8601UTC(t)
	return times.ParseIso8601UTC(zeroTimeStr)
}

// GetTimeSpanProperty is a helper method for retrieving a timespan property
// timespan is
func (proxyMessage *ProxyMessage) GetTimeSpanProperty(key string, def ...time.Duration) time.Duration {
	if proxyMessage.Properties[key] != nil {
		ticks, err := strconv.ParseInt(*proxyMessage.Properties[key], 10, 64)
		if err != nil {
			panic(err)
		}
		return time.Duration(ticks * 100)
	}

	d := time.Duration(0)
	if len(def) > 0 {
		d = def[0]
	}

	return d
}

// GetJSONProperty is a helper method for retrieving a complex property serialized
// as a JSON string
func (proxyMessage *ProxyMessage) GetJSONProperty(key string, deserializeTo interface{}) error {
	if proxyMessage.Properties[key] == nil {
		return fmt.Errorf("nil value error")
	}

	err := json.Unmarshal([]byte(*proxyMessage.Properties[key]), deserializeTo)
	if err != nil {
		panic(err)
	}

	return nil
}

// GetBytesProperty is a helper method for retrieving a []byte property
func (proxyMessage *ProxyMessage) GetBytesProperty(key string) []byte {
	if proxyMessage.Properties[key] == nil {
		return nil
	}

	bytes, err := base64.StdEncoding.DecodeString(*proxyMessage.Properties[key])
	if err != nil {
		panic(err)
	}

	return bytes
}

//---------------------------------------------------------------------
// Helper methods derived classes can use for setting typed message properties.

// SetStringProperty is a helper method to set a string property
func (proxyMessage *ProxyMessage) SetStringProperty(key string, value *string) {
	proxyMessage.Properties[key] = value
}

// SetIntProperty is a helper method to set an int property
func (proxyMessage *ProxyMessage) SetIntProperty(key string, value int32) {
	valueInt64 := int64(value)
	n := strconv.FormatInt(valueInt64, 10)
	proxyMessage.Properties[key] = &n
}

// SetLongProperty is a helper method to set an int property
func (proxyMessage *ProxyMessage) SetLongProperty(key string, value int64) {
	n := strconv.FormatInt(value, 10)
	proxyMessage.Properties[key] = &n
}

// SetBoolProperty is a helper method to set a bool property
func (proxyMessage *ProxyMessage) SetBoolProperty(key string, value bool) {
	str := strconv.FormatBool(value)
	proxyMessage.Properties[key] = &str
}

// SetDoubleProperty is a helper method to set a double property
func (proxyMessage *ProxyMessage) SetDoubleProperty(key string, value float64) {
	n := strconv.FormatFloat(value, 'G', -1, 64)
	proxyMessage.Properties[key] = &n
}

// SetDateTimeProperty is a helper method to set a date-time property
func (proxyMessage *ProxyMessage) SetDateTimeProperty(key string, value time.Time) {
	dateTime := times.ToIso8601UTC(value)
	proxyMessage.Properties[key] = &dateTime
}

// SetTimeSpanProperty is a helper method for setting a timespan property
func (proxyMessage *ProxyMessage) SetTimeSpanProperty(key string, value time.Duration) {
	timeSpan := strconv.FormatInt(value.Nanoseconds()/100, 10)
	proxyMessage.Properties[key] = &timeSpan
}

// SetJSONProperty is a helper method for setting a complex property as JSON
func (proxyMessage *ProxyMessage) SetJSONProperty(key string, value interface{}) {
	var jsonStr string
	if reflect.ValueOf(value).IsNil() {
		proxyMessage.Properties[key] = nil
	} else {

		bytes, err := json.Marshal(value)
		if err != nil {
			panic(err)
		}

		jsonStr = string(bytes)
		proxyMessage.Properties[key] = &jsonStr
	}
}

//SetBytesProperty is a helper method for setting a []byte property
func (proxyMessage *ProxyMessage) SetBytesProperty(key string, value []byte) {
	if value == nil {
		proxyMessage.Properties[key] = nil
	} else {
		str := base64.StdEncoding.EncodeToString(value)
		proxyMessage.Properties[key] = &str
	}
}
