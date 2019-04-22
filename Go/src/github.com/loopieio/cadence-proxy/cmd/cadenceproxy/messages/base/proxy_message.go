package base

import (
	"bytes"
	"encoding/binary"
	"errors"
	"fmt"
	"io"
	"log"
	"strconv"
	"time"

	"github.com/a3linux/amazon-ssm-agent/agent/times"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"
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
		Clone() *IProxyMessage
		CopyTo(target *IProxyMessage)
	}
)

const (

	// ContentType is the content type to be used for HTTP requests
	// encapsulationg a ProxyMessage
	ContentType   = "application/x-neon-cadence-proxy"
	int32ByteSize = 4
)

// intToMessagStruct is a map that maps a message type to its corresponding
// Message Struct
var intToMessageStruct map[int]IProxyMessage

// initialize the intToMessageStruct map
func init() {
	initIntToMessageStruct()
}

// Deserialize takes a pointer to an existing bytes.Buffer of bytes.
// It then reads the bytes from the buffer and deserializes them into
// a ProxyMessage instance
//
// param b *bytes.Buffer -> bytes.Buffer of bytes holding an encoded
// ProxyMessage
//
// return ProxyMessage -> ProxyMessage initialized using values encoded in
// bytes from the bytes.Buffer
func Deserialize(b *bytes.Buffer) ProxyMessage {
	pm := ProxyMessage{
		Properties: make(map[string]*string),
	}

	// get the message type
	pm.Type = messages.MessageType(readInt32(b))

	// get property count
	propertyCount := int(readInt32(b))

	for i := 0; i < propertyCount; i++ {
		key := readString(b)
		value := readString(b)
		pm.Properties[*key] = value
	}

	// attachment count
	attachmentCount := int(readInt32(b))
	pm.Attachments = make([][]byte, attachmentCount)
	for i := 0; i < attachmentCount; i++ {
		length := int(readInt32(b))
		if length == -1 {
			pm.Attachments[i] = nil
		} else if length == 0 {
			pm.Attachments[i] = make([]byte, 0)
		} else {
			pm.Attachments[i] = b.Next(length)
		}
	}

	return pm
}

func writeInt32(w io.Writer, value int32) {
	err := binary.Write(w, binary.LittleEndian, value)
	if err != nil {
		panic(err)
	}
}

func writeString(b *bytes.Buffer, value *string) {
	if value == nil {
		err := binary.Write(b, binary.LittleEndian, int32(-1))
		if err != nil {
			log.Println("binary.Write failed: ", err)
		}
	} else {
		err := binary.Write(b, binary.LittleEndian, int32(len(*value)))
		if err != nil {
			log.Println("binary.Write failed: ", err)
		}
		_, err = b.WriteString(*value)
		if err != nil {
			panic(err)
		}
	}
}

func readString(b *bytes.Buffer) *string {

	var strPtr *string
	length := int(readInt32(b))
	if length == -1 {
		strPtr = nil
	} else if length == 0 {
		str := ""
		strPtr = &str
	} else {
		strBytes := b.Next(length)
		str := string(strBytes)
		strPtr = &str
	}

	return strPtr
}

func readInt32(b *bytes.Buffer) int32 {
	var num int32
	intBytes := b.Next(int32ByteSize)
	buf := bytes.NewReader(intBytes)

	// Read the []byte into the byte.Reader
	// LittleEndian byte order
	err := binary.Read(buf, binary.LittleEndian, &num)
	if err != nil {
		log.Println("binary.Read failed: ", err)
	}

	return num
}

// Serialize is called on a ProxyMessage instance and
// serializes it into a []byte for sending over a network
//
// return []byte -> the ProxyMessage instance encoded as a []byte
func (pm *ProxyMessage) Serialize() []byte {
	b := new(bytes.Buffer)

	// write to the buffer LittleEndian byte order
	writeInt32(b, int32(pm.Type))

	// write to the buffer LittleEndian byte order
	writeInt32(b, int32(len(pm.Properties)))

	for k, v := range pm.Properties {
		writeString(b, &k)
		writeString(b, v)
	}

	// write to the buffer LittleEndian byte order
	writeInt32(b, int32(len(pm.Attachments)))

	for _, attachment := range pm.Attachments {
		if attachment == nil {
			// write to the buffer LittleEndian byte order
			writeInt32(b, int32(-1))
		} else {
			// write to the buffer LittleEndian byte order
			writeInt32(b, int32(len(attachment)))
			_, err := b.Write(attachment)
			if err != nil {
				panic(err)
			}
		}
	}

	return b.Bytes()
}

// CopyTo implemented by derived classes to copy
// message properties to another message instance
// during a Clone() operation
func (pm *ProxyMessage) CopyTo(target *IProxyMessage) {}

// Clone is implemented by derived classes to make a copy of themselves
// for echo testing purposes
func (pm *ProxyMessage) Clone() *IProxyMessage {
	return nil
}

// ProxyMessageToString is a method for cleanly
// printing an ProxyMessage object to a log console
func (pm *ProxyMessage) String() {
	log.Print("{\n")
	log.Println()
	log.Printf("\tType: (%d)%s\n", pm.Type, pm.Type.String())

	log.Print("\tProperties:\n")
	for k, v := range pm.Properties {
		if v == nil {
			log.Printf("\t\t%s: %s,\n", k, "nil")
		} else {
			log.Printf("\t\t%s: %s,\n", k, *v)
		}
	}

	log.Print("\tAttachments:")
	for i := 0; i < len(pm.Attachments); i++ {
		log.Printf("\t\t%v\n", pm.Attachments[i])
	}

	log.Print("}\n\n")
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
func (pm *ProxyMessage) SetStringProperty(key string, value string) {
	pm.Properties[key] = &value
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

//---------------------------------------------------------------------
// Helper method that builds the intToMessageStruct map
func initIntToMessageStruct() {
	intToMessageStruct = make(map[int]IProxyMessage)

	// iterate through MessageTypesArray
	for i, messageType := range messages.MessageTypeSlice {

		// check to make sure that the MessageTypeSlice indexes are correct
		if int(messageType) != i {
			err := errors.New("Message type mapping incorrect: MessageType--" + string(messageType) + ", SliceIndex: " + string(i))
			panic(err)
		}

		switch messageType {
		case messages.Unspecified:
			intToMessageStruct[i] = new(ProxyMessage)
		case messages.InitializeRequest:
			fmt.Println("Need to fill with initializeRequest struct")
		case messages.InitializeReply:
			fmt.Println("Need to fill with initializeReply struct")
		case messages.ConnectRequest:
			fmt.Println("Need to fill with ConnectionRequest struct")
		case messages.ConnectReply:
			fmt.Println("Need to fill with ConnectionRequest struct")
		case messages.TerminateRequest:
			fmt.Println("Need to fill with TerminateRequest struct")
		case messages.TerminateReply:
			fmt.Println("Need to fill with TerminateReply struct")
		}
	}
}
