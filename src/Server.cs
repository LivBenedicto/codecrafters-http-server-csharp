using System.ComponentModel;
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

Dictionary<ContentType, string> dContentType = new()
{
    {ContentType.Text, "text/plain"},
    {ContentType.File, "application/octet-stream"}
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
    Console.WriteLine(ASCIIEncoding.UTF8.GetString(responseBuffer) + rn);

    // GET /index.html HTTP/1.1
    string[] firstSlipt = rows[0].Split(" ");
    var (method, path, HttpVersion) = (firstSlipt[0], firstSlipt[1], firstSlipt[2]);
    
    // Host: localhost:4221\r\nUser-Agent: curl/7.64.1\r\nAccept: */*\r\n\r\n
    var (host, userAgent, fileContent) = (string.Empty, string.Empty, string.Empty);
    foreach (string item in rows)
    {
        if (item.StartsWith("Host", StringComparison.OrdinalIgnoreCase)) host = item.Split(": ").Last();
        else if (item.StartsWith("User-Agent", StringComparison.OrdinalIgnoreCase)) userAgent = item.Split(": ").Last();
        else if (item.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) fileContent = rows.Last();
    }

    Console.WriteLine($"1# -> Method: {method}, Path: {path}, HTTP HttpVersion: {HttpVersion} Host: {host}, UserAgent: {userAgent}");
    Console.WriteLine($"2# -> FileContent: {fileContent}");
    
    HttpRequest? request = new(method, path, HttpVersion, host, userAgent, fileContent);

    return request;
}

byte[] Response(HttpRequest request)
{
    string body = string.Empty;
    string httpVersion = request.HttpVersion;

    body = request.Path switch {
        "/" => body = $"{httpVersion} {httpStatus[HttpStatusCode.Ok]}{rn}",
        
        { } when request.Path.StartsWith("/echo", StringComparison.OrdinalIgnoreCase) => body = BuildResponse(httpVersion, httpStatus[HttpStatusCode.Ok], request.Path.ToLower().Replace("/echo/", ""), dContentType[ContentType.Text]),

        { } when request.Path.StartsWith("/user-agent", StringComparison.OrdinalIgnoreCase) => body = BuildResponse(httpVersion, httpStatus[HttpStatusCode.Ok], request.UserAgent, dContentType[ContentType.Text]),

        { } when request.Path.StartsWith("/files", StringComparison.OrdinalIgnoreCase) => body = ExistsFile(request),

        { } when request.Method.Contains("POST") => body = SaveFile(request),

        _ => body = $"{httpVersion} {httpStatus[HttpStatusCode.NotFound]}"
    };

    Console.WriteLine($"Response:{rn}{body}{rn}End Response");
    
    byte[] response = ASCIIEncoding.UTF8.GetBytes(body);

    return response;
}

string HandleFile(string requestPath)
{
    // /files/<filename>
    string[] arguments = Environment.GetCommandLineArgs();
    string directory = arguments[2];
    string filePath = $"{directory}{requestPath.Split("/").Last()}";
    Console.WriteLine($"File Path: {filePath}");
    
    return filePath;
}

string ExistsFile(HttpRequest request)
{
    string response = $"{request.HttpVersion} {httpStatus[HttpStatusCode.NotFound]}";

    string filePath = HandleFile(request.Path);
    string? fileContent = string.Empty;

    if (File.Exists(filePath))
    {
        fileContent = File.ReadAllText(filePath);
        
        response = BuildResponse(request.HttpVersion, httpStatus[HttpStatusCode.Ok], fileContent, dContentType[ContentType.File]);
    }
    
    return response;
}

string SaveFile(HttpRequest request) 
{
    System.Console.WriteLine("save file");
    string response = $"{request.HttpVersion} {httpStatus[HttpStatusCode.NotFound]}";
    
    string filePath = HandleFile(request.Path);
    string fileContent = request.FileContent;

    if (!File.Exists(filePath))
        File.Create(filePath);
    
    if (!string.IsNullOrEmpty(fileContent))
    {
        File.WriteAllText(filePath, fileContent);
        response = BuildResponse(request.HttpVersion, httpStatus[HttpStatusCode.Created], fileContent, dContentType[ContentType.File]);
    }

    return response;
}

// HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 12\r\n\r\nfoobar/1.2.3
string BuildResponse(string httpVersion, string httpStatusCode, string message, string contentType) => $"{httpVersion} {httpStatusCode}Content-Type: {contentType}{rn}Content-Length: {message.Length}{rn}{rn}{message}";

#endregion

#region Types
enum HttpStatusCode { Ok = 200, Created = 201, NotFound = 404 }

enum ContentType { [Description("text/plain")] Text = 1 , [Description("application/octet-stream")] File = 2 }

record HttpRequest(string Method, string Path, string HttpVersion, string Host, string UserAgent, string FileContent);

#endregion