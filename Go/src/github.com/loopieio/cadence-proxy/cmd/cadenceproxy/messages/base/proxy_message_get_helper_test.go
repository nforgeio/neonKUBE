package base_test

import (
	"log"
	"testing"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
)

func ProxyMessageGetHelperTest(t *testing.T) {
	pm := base.ProxyMessage{}
	var mt messages.MessageType = 1
	var properties map[string]*string = make(map[string]*string)
	var attachments [][]byte = make([][]byte, 0)

	// set proxy message message type, properties, and attachments
	pm.Type = mt
	pm.Properties = properties
	pm.Attachments = attachments

	// values to test
	str := "test_string_õ世界"
	strInt32Negative := "-45"
	strInt32Positive := "45"
	strLongPositive := "21474836470"
	strLongNegative := "-21474836470"
	strBoolTrue := "true"
	strBoolFalse := "false"
	strDoubleNegative := "-12.00067909"
	strDoublePositive := "12.00067909"
	strDateTime := "2006-01-02T15:04:05.8546Z"
	strDurationPositive := "1"
	strDurationNegative := "-2"

	// put the values into the proxy
	// message properties
	pm.Properties["0"] = &str
	pm.Properties["1"] = nil
	pm.Properties["2"] = &strInt32Positive
	pm.Properties["3"] = &strInt32Negative
	pm.Properties["4"] = nil
	pm.Properties["5"] = &strLongPositive
	pm.Properties["6"] = &strLongNegative
	pm.Properties["7"] = nil
	pm.Properties["8"] = &strBoolTrue
	pm.Properties["9"] = &strBoolFalse
	pm.Properties["10"] = nil
	pm.Properties["11"] = &strDoublePositive
	pm.Properties["12"] = &strDoubleNegative
	pm.Properties["13"] = nil
	pm.Properties["14"] = &strDateTime
	pm.Properties["15"] = nil
	pm.Properties["16"] = &strDurationPositive
	pm.Properties["17"] = &strDurationNegative
	pm.Properties["18"] = nil

	// print empty ProxyMessage
	pm.String()

	log.Println(*pm.GetStringProperty("0"))
	log.Println(pm.GetStringProperty("1"))
	log.Println(pm.GetIntProperty("2"))
	log.Println(pm.GetIntProperty("3"))
	log.Println(pm.GetIntProperty("4"))
	log.Println(pm.GetLongProperty("5"))
	log.Println(pm.GetLongProperty("6"))
	log.Println(pm.GetLongProperty("7"))
	log.Println(pm.GetBoolProperty("8"))
	log.Println(pm.GetBoolProperty("9"))
	log.Println(pm.GetBoolProperty("10"))
	log.Println(pm.GetDoubleProperty("11"))
	log.Println(pm.GetDoubleProperty("12"))
	log.Println(pm.GetDoubleProperty("13"))
	log.Println(pm.GetDateTimeProperty("14"))
	log.Println(pm.GetDateTimeProperty("15"))
	log.Println(pm.GetTimeSpanProperty("16"))
	log.Println(pm.GetTimeSpanProperty("17"))
	log.Println(pm.GetTimeSpanProperty("18"))

}
