package main

import (
	"go.uber.org/zap/zapcore"
)

type (
	Core struct {
		zapcore.LevelEnabler
		enc zapcore.Encoder
		out zapcore.WriteSyncer
	}
)

func NewCore(enc zapcore.Encoder, ws zapcore.WriteSyncer, enab zapcore.LevelEnabler) zapcore.Core {
	return &Core{
		LevelEnabler: enab,
		enc:          enc,
		out:          ws,
	}
}

func (c Core) With(fields []zapcore.Field) zapcore.Core {
	clone := c.clone()
	for i := range fields {
		fields[i].AddTo(clone.enc)
	}
	return clone
}

func (c Core) Check(entry zapcore.Entry, checkedEntry *zapcore.CheckedEntry) *zapcore.CheckedEntry {
	if c.Enabled(entry.Level) {
		return checkedEntry.AddCore(entry, c)
	}
	return checkedEntry
}

func (c Core) Write(entry zapcore.Entry, fields []zapcore.Field) error {
	buf, err := c.enc.EncodeEntry(entry, fields)
	if err != nil {
		return err
	}
	_, err = c.out.Write(buf.Bytes())
	buf.Free()
	if err != nil {
		return err
	}
	if entry.Level > zapcore.ErrorLevel {
		c.Sync()
	}
	return nil
}

func (c Core) Sync() error {
	return c.out.Sync()
}

func (c Core) clone() *Core {
	return &Core{
		LevelEnabler: c.LevelEnabler,
		enc:          c.enc,
		out:          c.out,
	}
}
