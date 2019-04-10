package main

import (
	"log"
	"math/rand"
	"testing"

	"github.com/loopieio/go-cadence-proxy/cmd/cadenceproxy/common"
)

const (
	letters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"
)

func TestPayload(t *testing.T) {

	strs := randStrings(50, 8)

	args1 := map[string]*string{
		"snap":    &strs[0],
		"crackle": &strs[1],
		"pop":     &strs[2],
	}

	args2 := map[string]*string{
		"laser": nil,
		"l":     &strs[3],
		"":      nil,
	}

	args3 := map[string]*string{
		"879043*": &strs[5],
		"":        &strs[4],
	}

	args4 := map[string]*string{}

	int1, err := common.Int32ToByteSlice(1)
	int2, err := common.Int32ToByteSlice(-4)
	int3, err := common.Int32ToByteSlice(100000)

	if err != nil {
		panic(err)
	}

	att1 := [][]byte{[]byte(strs[6]), []byte(strs[7]), int1, int2}
	att2 := [][]byte{}
	att3 := [][]byte{[]byte(strs[8])}
	att4 := [][]byte{[]byte(strs[9]), int3}

	op1 := common.Operation{
		OpCode:      1,
		Arguments:   args1,
		Attachments: att1,
	}

	op2 := common.Operation{
		OpCode:      2,
		Arguments:   args2,
		Attachments: att2,
	}

	op3 := common.Operation{
		OpCode:      3,
		Arguments:   args3,
		Attachments: att3,
	}

	op4 := common.Operation{
		OpCode:      4,
		Arguments:   args4,
		Attachments: att4,
	}

	op5 := common.Operation{}

	var tests = []struct {
		input common.Operation
	}{
		{op1},
		{op2},
		{op3},
		{op4},
		{op5},
	}

	for _, test := range tests {
		log.Println("***Input Operation***")
		test.input.OperationToString()

		opBytes := common.OperationToByteSlice(test.input)
		output := common.ByteSliceToOperation(opBytes)

		log.Println("***Output Operation***")
		output.OperationToString()

		if output.OpCode != test.input.OpCode {
			t.Errorf("Test Failed: %v, %v, OpCodes not equal: Expected %d, Got %d\n", test.input, output, test.input.OpCode, output.OpCode)
		}

		for k := range test.input.Arguments {
			if test.input.Arguments[k] == nil && output.Arguments[k] == nil {
				break
			} else if *test.input.Arguments[k] != *output.Arguments[k] {
				t.Errorf("Test Failed: %v, %v, Arguments not equal: Expected %s @ key %s, Got %s @ key %s\n", test.input, output, *test.input.Arguments[k], k, *output.Arguments[k], k)
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
	strs := make([]string, n, n)
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
