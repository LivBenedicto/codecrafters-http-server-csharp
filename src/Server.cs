using System.ComponentModel;
using System.IO.Compression;
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
    {HttpStatusCode.NotFound, $"404 Not Found{rn}{rn}"},
    {HttpStatusCode.Created, $"201 Created{rn}{rn}"}
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
    var (method, path, httpVersion) = (firstSlipt[0], firstSlipt[1], firstSlipt[2]);
    
    // Host: localhost:4221\r\nUser-Agent: curl/7.64.1\r\nAccept: */*\r\n\r\n
    var (host, userAgent, fileContent, acceptEncoding) = (string.Empty, string.Empty, string.Empty, string.Empty);
    foreach (string item in rows)
    {
        if (item.StartsWith("Host", StringComparison.OrdinalIgnoreCase)) host = item.Split(": ").Last();
        else if (item.StartsWith("User-Agent", StringComparison.OrdinalIgnoreCase)) userAgent = item.Split(": ").Last();
        else if (item.StartsWith("Content-Length", StringComparison.OrdinalIgnoreCase)) { int contentLenght =  int.Parse(item.Split(": ").Last()); fileContent = rows.Last()[..contentLenght]; }
        else if (item.StartsWith("Accept-Encoding", StringComparison.OrdinalIgnoreCase)) acceptEncoding = item.Split(": ").Last();
    }

    // Console.WriteLine($"1# -> Method: {method}, Path: {path}, HttpVersion: {httpVersion}, Host: {host}, UserAgent: {userAgent}");
    // Console.WriteLine($"2# -> FileContent: {fileContent}");
    Console.WriteLine($"3# -> AcceptEncoding: {acceptEncoding}");
    
    HttpRequest? request = new(method, path, httpVersion, host, userAgent, fileContent, acceptEncoding);

    return request;
}

byte[] Response(HttpRequest request)
{
    byte[] response = new byte[2048];
    string httpVersion = request.HttpVersion;

    response = request.Path switch {
        "/" => response = ASCIIEncoding.UTF8.GetBytes($"{request.HttpVersion} 200 OK\r\n\r\n"),
        
        { } when request.Path.StartsWith("/echo", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(request.AcceptEncoding) => response = BuildResponse(httpVersion, httpStatus[HttpStatusCode.Ok], request.Path.ToLower().Replace("/echo/", ""), request.Path.Replace("/echo/", "").Length, dContentType[ContentType.Text], request.AcceptEncoding),
        
        { } when request.Path.StartsWith("/echo", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(request.AcceptEncoding) => response = CompressionEcho(request),

        { } when request.Path.StartsWith("/user-agent", StringComparison.OrdinalIgnoreCase) => response = BuildResponse(httpVersion, httpStatus[HttpStatusCode.Ok], request.UserAgent, request.UserAgent.Length, dContentType[ContentType.Text], request.AcceptEncoding),
        
        { } when request.Method.Equals("GET") && request.Path.StartsWith("/files", StringComparison.OrdinalIgnoreCase) => response = ExistsFile(request),

        { } when request.Method.Equals("POST") && request.Path.StartsWith("/files", StringComparison.OrdinalIgnoreCase)  => response = SaveFile(request),
        
        _ => response = BuildHttpStatusResponse(httpVersion, httpStatus[HttpStatusCode.NotFound])
    };

    Console.WriteLine($"Response:{rn}{UTF8Encoding.ASCII.GetString(response)}{rn}End Response");
    
    return response;
}

byte[] CompressionEcho(HttpRequest request)
{
    Console.WriteLine("CompressionEcho");

    string[] acceptEncodingList = request.AcceptEncoding.Split(",");
    string? acceptEncoding = acceptEncodingList.Where(x => x.Contains("gzip")).FirstOrDefault();
    
    string message = request.Path.ToLower().Replace("/echo/", "");
    int messageLength = message.Length;
    byte[] compressed = new byte[2048];

    if(!string.IsNullOrEmpty(acceptEncoding))
    {
        byte[] acceptEncodingBytes = Encoding.UTF8.GetBytes(acceptEncoding);
        using var memoryStream = new MemoryStream();
        using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
        {
            gzipStream.Write(acceptEncodingBytes, 0, acceptEncodingBytes.Length);
            gzipStream.Flush();
            gzipStream.Close();
        }
        compressed = memoryStream.ToArray();
        messageLength = compressed.Length;
        message = string.Empty;

        Console.WriteLine($"messageLength: {compressed.Length}");
    }
    
    // response = $"HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Encoding: gzip\r\nContent-Length: {compressed.Length}\r\n\r\n" + compressed;
    byte[] buildResponse = BuildResponse(request.HttpVersion, httpStatus[HttpStatusCode.Ok], message, messageLength, dContentType[ContentType.Text], acceptEncoding);
    byte[] response = [..buildResponse, ..compressed];

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

byte[] ExistsFile(HttpRequest request)
{
    byte[] response = BuildHttpStatusResponse(request.HttpVersion, httpStatus[HttpStatusCode.NotFound]);

    string filePath = HandleFile(request.Path);
    string? fileContent = string.Empty;

    if (File.Exists(filePath))
    {
        fileContent = File.ReadAllText(filePath);
        
        response = BuildResponse(request.HttpVersion, httpStatus[HttpStatusCode.Ok], fileContent, fileContent.Length, dContentType[ContentType.File], null);
    }
    
    return response;
}

byte[] SaveFile(HttpRequest request) 
{
    System.Console.WriteLine("save file");
    
    string filePath = HandleFile(request.Path);

    File.WriteAllText(filePath, request.FileContent);
    byte[] response =  BuildHttpStatusResponse(request.HttpVersion, httpStatus[HttpStatusCode.Created]);

    return response;
}
#endregion

#region Build Responses
// HTTP/1.1 200 OK\r\nContent-Encoding: gzip\r\nContent-Type: text/plain\r\nContent-Length: 12\r\n\r\nfoobar/1.2.3
byte[] BuildResponse(string httpVersion, string httpStatusCode, string message, int messageLength, string contentType, string? acceptEncoding)
{ 
    StringBuilder response = new($"{httpVersion} {httpStatusCode}");

    if(!string.IsNullOrEmpty(acceptEncoding))
        response.Append($"content-encoding: {acceptEncoding}{rn}");
    
    response.Append($"content-type: {contentType}{rn}content-length: {messageLength}{rn}{rn}{message}");

    string responseString = response.ToString();

    return ASCIIEncoding.UTF8.GetBytes(responseString);
}

byte[] BuildHttpStatusResponse(string httpVersion, string httpStatus) => ASCIIEncoding.UTF8.GetBytes($"{httpVersion} {httpStatus}");
#endregion

#region Types
enum HttpStatusCode { Ok = 200, Created = 201, NotFound = 404 }

enum ContentType { [Description("text/plain")] Text = 1 , [Description("application/octet-stream")] File = 2 }

record HttpRequest(string Method, string Path, string HttpVersion, string Host, string UserAgent, string FileContent, string AcceptEncoding);

#endregion