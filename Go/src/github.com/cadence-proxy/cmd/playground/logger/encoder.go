package main

import (
	"go.uber.org/zap/buffer"
	"go.uber.org/zap/zapcore"
)

type (
	Encoder struct {
		zapcore.ObjectEncoder
	}
)

func NewEncoder(enc zapcore.ObjectEncoder) zapcore.Encoder {
	return &Encoder{
		ObjectEncoder: enc,
	}
}

func (e Encoder) Clone() zapcore.Encoder {
	return e
}

func (e Encoder) EncodeEntry(entry zapcore.Entry, fields []zapcore.Field) (*buffer.Buffer, error) {
	return nil, nil
}
