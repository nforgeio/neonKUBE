.PHONY: test bins clean
default: test

# export to the path
export PATH := $(GOPATH)/bin:$(PATH)

# Project, test, source, and build paths
PROJECT_ROOT = github.com/loopieio/cadence-proxy
BUILD_DIR := $(NF_ROOT)/Build
COMPRESSED_DIR := $(NF_ROOT)/Lib/Neon.Cadence/Resources
APP_DIR=./cmd/cadenceproxy
LOG_DIR=./test/logs

# test dirs
NEON_TEST_DIR := $(NF_ROOT)/Test/Test.Neon.Cadence/bin/Debug/netcoreapp3.0
MESSAGES_TEST_DIR=./internal/messages
ENDPOINTS_TEST_DIR=./internal/endpoints

# TEST_ENDPOINTS is a flag (1 for true, 0 for false) that 
# indicates if the endpoints package should be tested. 
# In order to test this package, you need a cadence-server
# listening on host:port 0.0.0.0:7933.  Otherwise, the test
# will fail
TEST_ENDPOINTS=1
TEST_DIRS := $(MESSAGES_TEST_DIR) \
	$(ENDPOINTS_TEST_DIR)

# Gather all source code		
ALL_SRC := $(shell find ./cmd/cadenceproxy -name "*.go")

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
	rm -f test/endpoints.test
	rm -f test/messages.test
	rm -f $(LOG_DIR)/test.log
	echo $(TEST_DIRS)

	# test messages package
	go test -v -o test/messages.test -coverprofile $(LOG_DIR)/messages-cover.out $(MESSAGES_TEST_DIR) | tee -a $(LOG_DIR)/test.log
	go tool cover -html=$(LOG_DIR)/messages-cover.out -o $(LOG_DIR)/messages-cover.html

# test endpoints package (check to make sure the TEST_ENDPOINTS flag is the desired value)
ifeq ($(TEST_ENDPOINTS), 1)
		go test -v -o test/endpoints.test -coverprofile $(LOG_DIR)/endpoints-cover.out $(ENDPOINTS_TEST_DIR) | tee -a $(LOG_DIR)/test.log
		go tool cover -html=$(LOG_DIR)/endpoints-cover.out -o $(LOG_DIR)/endpoints-cover.html
endif
	
clean:
	rm -Rf bin/*
	rm -f test/messages.test
	rm -f test/endpoints.test

	rm -f $(LOG_DIR)/test.log
	rm -f $(LOG_DIR)/messages-cover.out
	rm -f $(LOG_DIR)/messages-cover.html
	rm -f $(LOG_DIR)/endpoints-cover.out
	rm -f $(LOG_DIR)/endpoints-cover.html

	rm -f $(BUILD_DIR)/$(WIN)
	rm -f $(BUILD_DIR)/$(LINUX)
	rm -f $(BUILD_DIR)/$(OSX)
	rm -f $(COMPRESSED_DIR)/$(WIN).gz
	rm -f $(COMPRESSED_DIR)/$(LINUX).gz
	rm -f $(COMPRESSED_DIR)/$(OSX).gz
	rm -f $(NEON_TEST_DIR)/cadence-proxy.exe
