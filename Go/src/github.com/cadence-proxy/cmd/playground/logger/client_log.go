package main

import (
	"time"

	"go.uber.org/zap/zapcore"
)

type (
	ClientLog struct {
		TimeUtc     time.Time     `json:"TimeUtc"`
		LogLevel    zapcore.Level `json:"LogLevel"`
		FromCadence bool          `json:"FromCadence"`
		LogMessage  string        `json:"LogMessage"`
	}
)

func NewClientLog(time time.Time, logLevel zapcore.Level, fromCadence bool, logMessage string) *ClientLog {
	return &ClientLog{
		TimeUtc:     time,
		LogLevel:    logLevel,
		FromCadence: fromCadence,
		LogMessage:  logMessage,
	}
}

func (c *ClientLog) MarshalLogObject(enc zapcore.ObjectEncoder) error {
	enc.AddTime("TimeUtc", c.TimeUtc)
	enc.AddString("LogLevel", c.LogLevel.String())
	enc.AddBool("FromCadence", c.FromCadence)
	enc.AddString("LogMessage", c.LogMessage)
	return nil
}
