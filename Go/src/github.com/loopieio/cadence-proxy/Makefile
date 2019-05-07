.PHONY: test bins clean neon-library-test
default: test

# export to the path
export PATH := $(GOPATH)/bin:$(PATH)

# Project, test, source, and build paths
PROJECT_ROOT = github.com/loopieio/cadence-proxy
BUILD_DIR := $(NF_ROOT)/Build/
COMPRESSED_DIR := $(NF_ROOT)/Lib/Neon.Cadence/Resources
APP_DIR=./cmd/cadenceproxy
TEST_DIRS=./cmd/cadenceproxy/messages
ALL_SRC := $(shell find ./cmd/cadenceproxy -name "*.go")
NEON_TEST_DIR := $(NF_ROOT)/Test/Test.Neon.Cadence/bin/Debug/netcoreapp3.0

# programs in the /cmd dir to build
PROGS = cadenceproxy

# executable names
WIN := cadence-proxy.win.exe
LINUX := cadence-proxy.linux
OSX := cadence-proxy.osx

# Use dep to update and grab source
# for dependencies
dep-ensured:
	dep ensure

cadence-proxy-windows: dep-ensured $(ALL_SRC)
	env GOOS=windows GOARCH=amd64 CGO_ENABLED=1 go build -i -ldflags="-w -s" -race -v -o bin/$(WIN) $(APP_DIR)/*.go

cadence-proxy-linux: dep-ensured $(ALL_SRC)
	env GOOS=linux GOARCH=amd64 go build -i -ldflags="-w -s" -v -o bin/$(LINUX) $(APP_DIR)/*.go

cadence-proxy-osx: dep-ensured $(ALL_SRC)
	env GOOS=darwin GOARCH=amd64 go build -i -ldflags="-w -s" -v -o bin/$(OSX) $(APP_DIR)/*.go

cadenceproxy: cadence-proxy-linux \
	cadence-proxy-windows \
	cadence-proxy-osx

bins: cadenceproxy \

test: bins
	@rm -f test/cadence-proxy.test
	@rm -f test/logs/test.log
	@echo $(TEST_DIRS)
	@for dir in $(TEST_DIRS); do \
		go test -race -v -o test/cadence-proxy.test "$$dir" | tee -a test/logs/test.log; \
	done;
	
clean:
	rm -Rf bin/*
	rm -f test/cadence-proxy.test
	rm -f test/logs/test.log
	rm -f $(BUILD_DIR)/$(WIN)
	rm -f $(BUILD_DIR)/$(LINUX)
	rm -f $(BUILD_DIR)/$(OSX)
	rm -f $(COMPRESSED_DIR)/$(WIN).gz
	rm -f $(COMPRESSED_DIR)/$(LINUX).gz
	rm -f $(COMPRESSED_DIR)/$(OSX).gz
	rm -f $(NEON_TEST_DIR)/cadence-proxy.exe
