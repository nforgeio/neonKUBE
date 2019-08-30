package main

import (
	"os"
	"time"

	"go.uber.org/zap"
	"go.uber.org/zap/zapcore"
)

// TODO: JACK NEED TO MAKE CUSTOM ENCODER TO ENCODE INTO A CLIENT
// LOG STRUCT
var (
	LogLevel = 1
)

func main() {
	// create client log
	clientLog := NewClientLog(
		time.Now(),
		zapcore.DebugLevel,
		true,
		"this is my last resort",
	)

	// create encoder
	encConfig := zapcore.EncoderConfig{
		TimeKey:      "time",
		MessageKey:   "msg",
		NameKey:      "name",
		LevelKey:     "lvl",
		EncodeTime:   syslogTimeEncoder,
		EncodeCaller: zapcore.ShortCallerEncoder,
		EncodeLevel:  zapcore.CapitalColorLevelEncoder,
	}

	// define level-handling logic
	enab := zap.LevelEnablerFunc(func(lvl zapcore.Level) bool {
		return LogLevel > 0
	})

	// lock output
	consoleDebugging := zapcore.Lock(os.Stdout)

	// create encoder
	enc := zapcore.NewConsoleEncoder(encConfig)

	// create core
	core := NewCore(
		enc,
		consoleDebugging,
		enab,
	)

	// create logger
	logger := zap.New(core)
	defer logger.Sync()

	logger.Info("Constructed a Logger", zap.Object("Client Log", clientLog))

}

func syslogTimeEncoder(t time.Time, enc zapcore.PrimitiveArrayEncoder) {
	enc.AppendString(t.Format("Jan  2 15:04:05"))
}
