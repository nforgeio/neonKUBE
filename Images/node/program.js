// This is a very simple Node.js HTTP service that returns 
// the OUTPUT environment variable is set, or else "Hello World!"

var http = require('http');

var server = http.createServer(function (request, response) {

    response.writeHead(200, { "Content-Type": "text/plain" });

    var output = process.env.OUTPUT;

    if (!output) {

        output = "Hello World!"
    }

    response.end(output + "\n");
});

server.listen(80);
console.log("Server running at http://127.0.0.1:80/");
