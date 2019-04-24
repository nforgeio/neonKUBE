package base

import (
	"bytes"
	"encoding/binary"
	"fmt"
	"io"
	"log"
	"strconv"
	"time"

	"github.com/a3linux/amazon-ssm-agent/agent/times"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"
	"github.com/pkg/errors"
)

type (
	// ProxyMessage represents an encoded Cadence ProxyMessage
	// Type is the cadence ProxyMessage as an enumeration
	// Properties are the Properties for the ProxyMessages
	// Attachments are any data attachments (in bytes) that
	// are needed to perform the ProxyMessage
	ProxyMessage struct {
		Type        messages.MessageType
		Properties  map[string]*string
		Attachments [][]byte
	}

	IProxyMessage interface {
		Clone() IProxyMessage
		CopyTo(target IProxyMessage)
		SetProxyMessage(value *ProxyMessage)
		GetProxyMessage() *ProxyMessage
		String() string
	}
)

const (

	// ContentType is the content type to be used for HTTP requests
	// encapsulationg a ProxyMessage
	ContentType = "application/x-neon-cadence-proxy"

	// CadenceErrorTypesKey is the string for accessing
	// the error type property in a ProxyMessage's properties map
	CadenceErrorTypesKey = "ErrorType"

	// RequestIDKey is the string key for accessing
	// the RequestId property in a ProxyMessage's properties map
	RequestIDKey = "RequestId"

	// EndpointsKey is the string key for accessing
	// the endpoints property in a ProxyMessage's properties map
	EndpointsKey = "Endpoints"

	// DomainKey is the string key for accessing
	// the domain property in a ProxyMessage's properties map
	DomainKey = "Domain"

	// IdentityKey is the string key for accessing
	// the identity property in a ProxyMessage's properties map
	IdentityKey = "Identity"

	// LibraryAddressKey is the string key for accessing
	// the library address property in a ProxyMessage's properties map
	LibraryAddressKey = "LibraryAddress"

	// LibraryPortKey is the string key for accessing the
	// library port property in a ProxyMessage's properties map
	LibraryPortKey = "LibraryPort"
	int32ByteSize  = 4
)

// IntToMessageStruct is a map that maps a message type to its corresponding Message Struct
var IntToMessageStruct map[int]IProxyMessage

// InitProxyMessage initializes the IntToMessageStruct
// This will allow derived classes to add to IntToMessageStruct
func InitProxyMessage() {
	IntToMessageStruct = make(map[int]IProxyMessage)
}

// NewProxyMessage creates a new ProxyMessage in memory, initializes
// its Properties map and Attachments [][]byte
//
// returns *ProxyMessage -> pointer to the newly created ProxyMessage
// in memory
func NewProxyMessage() *ProxyMessage {
	message := new(ProxyMessage)
	message.Properties = make(map[string]*string)
	message.Attachments = make([][]byte, 0)
	return message
}

// Deserialize takes a pointer to an existing bytes.Buffer of bytes.
// It then reads the bytes from the buffer and deserializes them into
// a ProxyMessage instance
//
// param buf *bytes.Buffer -> bytes.Buffer of bytes holding an encoded
// ProxyMessage
// Param ignoreTypeCode bool -> ignore unspecified message types (for unit testing)
//
// return ProxyMessage -> ProxyMessage initialized using values encoded in
// bytes from the bytes.Buffer
// return Error -> an error deserializing does not work
func Deserialize(buf *bytes.Buffer, ignoreTypeCode bool) (IProxyMessage, error) {
	var message IProxyMessage

	// get the message type
	messageType := messages.MessageType(readInt32(buf))

	if !ignoreTypeCode {
		intMessageType := int(messageType)
		if IntToMessageStruct[intMessageType] == nil {
			errStr := fmt.Sprintf("Unexpected message type %s", messageType.String())
			return message, errors.New(errStr)
		}
		message = IntToMessageStruct[intMessageType].Clone()

	} else {
		message = NewProxyMessage()
	}

	pm := NewProxyMessage()

	// get the message type
	pm.Type = messageType

	// get property count
	propertyCount := int(readInt32(buf))

	for i := 0; i < propertyCount; i++ {
		key := readString(buf)
		value := readString(buf)
		pm.Properties[*key] = value
	}

	// attachment count
	attachmentCount := int(readInt32(buf))
	pm.Attachments = make([][]byte, attachmentCount)
	for i := 0; i < attachmentCount; i++ {
		length := int(readInt32(buf))
		if length == -1 {
			pm.Attachments[i] = nil
		} else if length == 0 {
			pm.Attachments[i] = make([]byte, 0)
		} else {
			pm.Attachments[i] = buf.Next(length)
		}
	}

	message.SetProxyMessage(pm)
	return message, nil
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
			log.Println("binary.Write failed: ", err)
		}
	} else {
		err := binary.Write(buf, binary.LittleEndian, int32(len(*value)))
		if err != nil {
			log.Println("binary.Write failed: ", err)
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
	intBytes := buf.Next(int32ByteSize)
	reader := bytes.NewReader(intBytes)

	// Read the []byte into the byte.Reader
	// LittleEndian byte order
	err := binary.Read(reader, binary.LittleEndian, &num)
	if err != nil {
		log.Println("binary.Read failed: ", err)
	}

	return num
}

// Serialize is called on a ProxyMessage instance and
// serializes it into a []byte for sending over a network
//
// param ignoredTypeCode bool -> Optionally ignore unspecified message types (unit testing)
//
// return []byte -> the ProxyMessage instance encoded as a []byte
// return error -> an error if serialization goes wrong
func (pm *ProxyMessage) Serialize(ignoreTypeCode bool) ([]byte, error) {
	if (!ignoreTypeCode) && (pm.Type == messages.Unspecified) {
		errMessage := fmt.Sprintf("Proxy Message has not initialized its [%s] property", pm.Type.String())
		return nil, errors.New(errMessage)
	}

	buf := new(bytes.Buffer)

	// write to the buffer LittleEndian byte order
	writeInt32(buf, int32(pm.Type))

	// write to the buffer LittleEndian byte order
	writeInt32(buf, int32(len(pm.Properties)))

	for k, v := range pm.Properties {
		writeString(buf, &k)
		writeString(buf, v)
	}

	// write to the buffer LittleEndian byte order
	writeInt32(buf, int32(len(pm.Attachments)))

	for _, attachment := range pm.Attachments {
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

	return buf.Bytes(), nil
}

// ProxyMessageToString is a method for cleanly
// printing an ProxyMessage object to a log console
func (pm *ProxyMessage) String() string {
	str := ""
	str = fmt.Sprintf("%s\n", str)
	str = fmt.Sprintf("%s\tType: (%d)%s\n", str, pm.Type, pm.Type.String())
	str = fmt.Sprintf("%s\tProperties:\n", str)
	for k, v := range pm.Properties {
		if v == nil {
			str = fmt.Sprintf("%s\t\t%s: %s,\n", str, k, "nil")
		} else {
			str = fmt.Sprintf("%s\t\t%s: %s,\n", str, k, *v)
		}
	}

	str = fmt.Sprintf("%s\tAttachments:\n", str)
	for i := 0; i < len(pm.Attachments); i++ {
		str = fmt.Sprintf("%s\t\t%v\n", str, pm.Attachments[i])
	}

	return str
}

// CopyTo implemented by derived classes to copy
// message properties to another message instance
// during a Clone() operation
func (pm *ProxyMessage) CopyTo(target IProxyMessage) {}

// Clone is implemented by derived classes to make a copy of themselves
// for echo testing purposes
func (pm *ProxyMessage) Clone() IProxyMessage {
	return nil
}

// SetProxyMessage is implemented by derived classes to set the value
// of a ProxyMessage in an IProxyMessage interface
func (pm *ProxyMessage) SetProxyMessage(value *ProxyMessage) {
	*pm = *value
}

// GetProxyMessage is implemented by derived classes to get the value of
// a ProxyMessage in an IProxyMessage interface
func (pm *ProxyMessage) GetProxyMessage() *ProxyMessage {
	return pm
}

// -------------------------------------------------------------------------
// Helper methods derived classes can use for retreiving typed message properties

// GetStringProperty is a method for retrieving a string property
func (pm *ProxyMessage) GetStringProperty(key string) *string {
	return pm.Properties[key]
}

// GetIntProperty is a helper method for retrieving a 32-bit integer property
func (pm *ProxyMessage) GetIntProperty(key string) int32 {
	if pm.Properties[key] != nil {
		value, err := strconv.ParseInt(*pm.Properties[key], 10, 32)
		if err != nil {
			panic(err)
		}

		return int32(value)
	}

	return 0
}

// GetLongProperty is a helper method for retrieving a 64-bit long integer property
func (pm *ProxyMessage) GetLongProperty(key string) int64 {
	if pm.Properties[key] != nil {
		value, err := strconv.ParseInt(*pm.Properties[key], 10, 64)
		if err != nil {
			panic(err)
		}

		return value
	}

	return 0
}

// GetBoolProperty is a helper method for retrieving a boolean property
func (pm *ProxyMessage) GetBoolProperty(key string) bool {
	if pm.Properties[key] != nil {
		value, err := strconv.ParseBool(*pm.Properties[key])
		if err != nil {
			panic(err)
		}

		return value
	}

	return false
}

// GetDoubleProperty is a helper method for retrieving a double property
func (pm *ProxyMessage) GetDoubleProperty(key string) float64 {
	if pm.Properties[key] != nil {
		value, err := strconv.ParseFloat(*pm.Properties[key], 64)
		if err != nil {
			panic(err)
		}

		return value
	}

	return 0.0
}

// GetDateTimeProperty is a helper method for retrieving a DateTime property
func (pm *ProxyMessage) GetDateTimeProperty(key string) time.Time {
	if pm.Properties[key] != nil {
		return times.ParseIso8601UTC(*pm.Properties[key])
	}

	zeroTimeStr := times.ToIso8601UTC(time.Time{})
	return times.ParseIso8601UTC(zeroTimeStr)
}

// GetTimeSpanProperty is a helper method for retrieving a timespan property
// timespan is
func (pm *ProxyMessage) GetTimeSpanProperty(key string) time.Duration {
	if pm.Properties[key] != nil {
		ticks, err := strconv.ParseInt(*pm.Properties[key], 10, 64)
		if err != nil {
			panic(err)
		}
		return time.Duration(ticks*100) * time.Nanosecond
	}

	return time.Duration(0) * time.Nanosecond
}

//---------------------------------------------------------------------
// Helper methods derived classes can use for setting typed message properties.

// SetStringProperty is a helper method to set a string property
func (pm *ProxyMessage) SetStringProperty(key string, value *string) {
	pm.Properties[key] = value
}

// SetIntProperty is a helper method to set an int property
func (pm *ProxyMessage) SetIntProperty(key string, value int32) {
	valueInt64 := int64(value)
	n := strconv.FormatInt(valueInt64, 10)
	pm.Properties[key] = &n
}

// SetLongProperty is a helper method to set an int property
func (pm *ProxyMessage) SetLongProperty(key string, value int64) {
	n := strconv.FormatInt(value, 10)
	pm.Properties[key] = &n
}

// SetBoolProperty is a helper method to set a bool property
func (pm *ProxyMessage) SetBoolProperty(key string, value bool) {
	str := strconv.FormatBool(value)
	pm.Properties[key] = &str
}

// SetDoubleProperty is a helper method to set a double property
func (pm *ProxyMessage) SetDoubleProperty(key string, value float64) {
	n := strconv.FormatFloat(value, 'G', -1, 64)
	pm.Properties[key] = &n
}

// SetDateTimeProperty is a helper method to set a date-time property
func (pm *ProxyMessage) SetDateTimeProperty(key string, value time.Time) {
	dateTime := times.ToIso8601UTC(value)
	pm.Properties[key] = &dateTime
}

// SetTimeSpanProperty is a helper method for setting a timespan property
func (pm *ProxyMessage) SetTimeSpanProperty(key string, value time.Duration) {
	timeSpan := strconv.FormatInt(value.Nanoseconds()/100, 10)
	pm.Properties[key] = &timeSpan
}
