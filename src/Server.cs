using System.Net;
using System.Net.Sockets;
using System.Text;

TcpListener server = new TcpListener(IPAddress.Any, 4221);
server.Start();
Console.WriteLine("Starting server");

const string rn = "\r\n"; //Environment.NewLine
// HTTP/1.1 404 Not Found\r\n\r\n
Dictionary<HttpStatusCode, string> httpStatus = new()
{
    {HttpStatusCode.Ok, $"200 OK{rn}" },
    {HttpStatusCode.NotFound, $"404 Not Found{rn}{rn}" }
};


try
{
    while(true)
    {
        TcpClient tcpClient = await server.AcceptTcpClientAsync();
        _ = Task.Run(() => HandleClient(tcpClient));
    }
}
catch (Exception e) { Console.WriteLine(e.Message); }

server.Stop();
Console.WriteLine("Stoping server");

server.Dispose();


#region complementary role
void HandleClient(TcpClient client)
{
    Stream stream = client.GetStream();
    HttpRequest request = Request(stream);

    byte[] response = Response(request);
    stream.Write(response, 0, response.Length);

    client.Close();
}

HttpRequest Request(Stream stream)
{
    byte[] responseBuffer = new byte[1024];
    _ = stream.Read(responseBuffer);
    
    // GET /index.html HTTP/1.1\r\nHost: localhost:4221\r\nUser-Agent: curl/7.64.1\r\nAccept: */*\r\n\r\n
    string[] rows = ASCIIEncoding.UTF8.GetString(responseBuffer).Split($"{rn}");

    // GET /index.html HTTP/1.1
    string[] firstSlipt = rows[0].Split(" ");
    var (method, path, HttpVersion) = (firstSlipt[0], firstSlipt[1], firstSlipt[2]);
    Console.WriteLine($"1# -> Method: {method}, Path: {path}, HTTP HttpVersion: {HttpVersion}");
    
    // Host: localhost:4221\r\nUser-Agent: curl/7.64.1\r\nAccept: */*\r\n\r\n
    var (host, userAgent, accept) = (string.Empty, string.Empty, string.Empty);
    foreach (string item in rows)
    {
        if (item.StartsWith("Host", StringComparison.OrdinalIgnoreCase)) host = item.Split(": ").Last();
        else if (item.StartsWith("User-Agent", StringComparison.OrdinalIgnoreCase)) userAgent = item.Split(": ").Last();
    }
    Console.WriteLine($"2# -> Host: {host}, UserAgent: {userAgent}, Accept: {accept}");
    
    HttpRequest? request = new(method, path, HttpVersion, host, userAgent, accept);

    return request;
}

byte[] Response(HttpRequest request)
{
    string body = string.Empty;
    string httpVersion = request.HttpVersion;
    string contentType = "text/plain";

    body = request.Path switch {
        "/" => body = httpStatus[HttpStatusCode.Ok] + rn,
        
        { } when request.Path.StartsWith("/echo", StringComparison.OrdinalIgnoreCase) => body = BuildResponse(httpVersion, httpStatus[HttpStatusCode.Ok], request.Path.ToLower().Replace("/echo/", ""), contentType),

        { } when request.Path.StartsWith("/user-agent", StringComparison.OrdinalIgnoreCase) => body = BuildResponse(httpVersion, httpStatus[HttpStatusCode.Ok], request.UserAgent, contentType),

        {} when request.Path.StartsWith("/files", StringComparison.OrdinalIgnoreCase) => body = HandleFile(request),

        _ => body = httpStatus[HttpStatusCode.NotFound]
    };

    Console.WriteLine($"Response:{rn}{body}{rn}End Response");
    
    byte[] response = ASCIIEncoding.UTF8.GetBytes(body);

    return response;
}

string HandleFile(HttpRequest request) //string httpVersion, string filePath
{
    string response = httpStatus[HttpStatusCode.NotFound];

    // GET /files/<filename>
    string[] arguments = Environment.GetCommandLineArgs();
    string directory = arguments[2];
    string filePath = $"{directory}/{request.Path.Split("/").Last()}";
    Console.WriteLine($"File Path: {filePath}");
    
    if (File.Exists(filePath))
    {
        string? fileContent = File.ReadAllText(filePath);
        response = BuildResponse(request.HttpVersion, httpStatus[HttpStatusCode.Ok], fileContent, "application/octet-stream");
    }

    Console.WriteLine($"HandleFile response: {response}");
    return response;
}

// HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 12\r\n\r\nfoobar/1.2.3
string BuildResponse(string httpVersion, string httpStatusCode, string message, string contentType) => $"{httpVersion} {httpStatusCode}Content-Type: {contentType}{rn}Content-Length: {message.Length}{rn}{rn}{message}";

enum HttpStatusCode { Ok = 200, NotFound = 404 }

record HttpRequest(string Method, string Path, string HttpVersion, string Host, string UserAgent, string Accept);
#endregion