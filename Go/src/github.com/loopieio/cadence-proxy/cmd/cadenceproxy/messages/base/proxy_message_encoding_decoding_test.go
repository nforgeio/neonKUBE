package base_test

import (
	"bytes"
	"encoding/binary"
	"log"
	"math/rand"
	"testing"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
	connect "github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/connect"
	initialize "github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/initialize"
	terminate "github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/terminate"
)

const (
	letters       = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"
	int32ByteSize = 4
)

func init() {
	base.InitProxyMessage()
	connect.InitConnect()
	initialize.InitInitialize()
	terminate.InitTerminate()
}

func TestProxyMessageEncodingDecoding(t *testing.T) {

	strs := randStrings(50, 8)
	emptyStr := ""
	utf8Str := "õ世界"

	args1 := map[string]*string{
		"snap":    &utf8Str,
		"crackle": nil,
		"pop":     &utf8Str,
	}

	args2 := map[string]*string{
		"laser": nil,
		"l":     &strs[3],
		"":      &emptyStr,
	}

	args3 := map[string]*string{
		"879043*": &strs[5],
		"":        &strs[4],
	}

	args4 := map[string]*string{}

	int1 := Int32ToByteSlice(1)
	//int2 := base.Int32ToByteSlice(-4)
	int3 := Int32ToByteSlice(100000)

	att1 := [][]byte{[]byte(utf8Str), []byte(utf8Str), int1, nil}
	att2 := [][]byte{}
	att3 := [][]byte{[]byte(strs[8]), []byte("")}
	att4 := [][]byte{[]byte(strs[9]), int3, nil}

	op1 := base.ProxyMessage{
		Type:        1,
		Properties:  args1,
		Attachments: att1,
	}

	op2 := base.ProxyMessage{
		Type:        2,
		Properties:  args2,
		Attachments: att2,
	}

	op3 := base.ProxyMessage{
		Type:        3,
		Properties:  args3,
		Attachments: att3,
	}

	op4 := base.ProxyMessage{
		Type:        4,
		Properties:  args4,
		Attachments: att4,
	}

	op5 := base.ProxyMessage{Type: messages.InitializeReply, Properties: args1}

	var tests = []struct {
		input base.ProxyMessage
	}{
		{op1},
		{op2},
		{op3},
		{op4},
		{op5},
	}

	for _, test := range tests {
		log.Println("***Input ProxyMessage***")
		log.Println(test.input.String())

		opBytes, _ := test.input.Serialize(false)
		buf := bytes.NewBuffer(opBytes)
		des, _ := base.Deserialize(buf, false)
		output := des.GetProxyMessage()

		log.Println("***Output ProxyMessage***")
		log.Println(des.String())

		if output.Type != test.input.Type {
			t.Errorf("Test Failed: %v, %v, Types not equal: Expected %d, Got %d\n", test.input, output, test.input.Type, output.Type)
		}

		for k := range test.input.Properties {
			if test.input.Properties[k] == nil && output.Properties[k] == nil {
				break
			} else if *test.input.Properties[k] != *output.Properties[k] {
				t.Errorf("Test Failed: %v, %v, Properties not equal: Expected %s @ key %s, Got %s @ key %s\n", test.input, output, *test.input.Properties[k], k, *output.Properties[k], k)
			}
		}

		for i := 0; i < len(test.input.Attachments); i++ {
			for j := 0; j < len(test.input.Attachments[i]); j++ {
				if test.input.Attachments[i][j] != output.Attachments[i][j] {
					t.Errorf("Test Failed: %v, %v, Attachments not equal: Expected %s @ %d,%d, Got %s @ %d,%d\n", test.input, output, test.input.Attachments[i], i, j, output.Attachments[i], i, j)
				}
			}
		}
	}
}

func randStrings(n int, strLength int) []string {
	strs := make([]string, n)
	for i := 0; i < n; i++ {
		strs[i] = randStringBytes(strLength)
	}

	return strs
}

func randStringBytes(n int) string {
	b := make([]byte, n)
	for i := range b {
		b[i] = letters[rand.Intn(len(letters))]
	}
	return string(b)
}

// Int32ToByteSlice takes an int32
// and converts it into a []byte
// Param num int32 -> int32 to be encoded into a []byte
// Returns []byte representation of num param
// Returns error
func Int32ToByteSlice(num int32) []byte {

	// create the []byte and make it
	// the size of a 32bit int
	blice := make([]byte, 0, int32ByteSize)

	// create a buffer for to read the bytes into
	buf := bytes.NewBuffer(blice)

	// write to the buffer LittleEndian byte order
	err := binary.Write(buf, binary.LittleEndian, num)
	if err != nil {
		panic(err)
	}

	return buf.Bytes()
}
