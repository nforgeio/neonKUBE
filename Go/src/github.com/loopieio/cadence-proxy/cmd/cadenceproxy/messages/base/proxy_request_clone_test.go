package base_test

import (
	"log"
	"testing"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
	connect "github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/connect"
	initialize "github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/initialize"
	terminate "github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/terminate"
)

func TestProxyRequestClone(t *testing.T) {

	connect.InitConnect()
	initialize.InitInitialize()
	terminate.InitTerminate()

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

	var tests = []struct {
		input base.ProxyMessage
	}{
		{op1},
		{op2},
		{op3},
		{op4},
	}

	for _, test := range tests {
		var requestId int64 = 48484839458743
		pr := base.ProxyRequest{
			RequestId:    requestId,
			ProxyMessage: &test.input,
		}

		log.Println("***ProxyRequest***")
		log.Println(pr.String())

		prClone := pr.Clone()

		log.Println("***Copy ProxyRequest***")
		log.Println(prClone.String())

		v, ok := prClone.(*base.ProxyRequest)
		if ok {
			if v.Type != pr.Type {
				t.Errorf("Test Failed: %v, %v, Types not equal: Expected %d, Got %d\n", pr, v, pr.Type, v.Type)
			}

			for k := range pr.Properties {
				if pr.Properties[k] == nil && v.Properties[k] == nil {
					break
				} else if *pr.Properties[k] != *v.Properties[k] {
					t.Errorf("Test Failed: %v, %v, Properties not equal: Expected %s @ key %s, Got %s @ key %s\n", pr, v, *pr.Properties[k], k, *v.Properties[k], k)
				}
			}

			for i := 0; i < len(pr.Attachments); i++ {
				for j := 0; j < len(pr.Attachments[i]); j++ {
					if pr.Attachments[i][j] != v.Attachments[i][j] {
						t.Errorf("Test Failed: %v, %v, Attachments not equal: Expected %s @ %d,%d, Got %s @ %d,%d\n", pr, v, pr.Attachments[i], i, j, v.Attachments[i], i, j)
					}
				}
			}
		}
	}
}
