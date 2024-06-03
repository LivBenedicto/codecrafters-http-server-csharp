using System.Net;
using System.Net.Sockets;
using System.Text;

TcpListener server = new TcpListener(IPAddress.Any, 4221);
server.Start();

try
{
    Socket socket = await server.AcceptSocketAsync();
    
    byte[] responseBuffer = new byte[1024];
    int _ = await socket.ReceiveAsync(responseBuffer);
    string rn = "\r\n"; //Environment.NewLine
    string[] rows = ASCIIEncoding.UTF8.GetString(responseBuffer).Split($"{rn}");
    Console.WriteLine($"Request:{rn}{ASCIIEncoding.UTF8.GetString(responseBuffer)}{rn}End Request");

    // GET /index.html HTTP/1.1
    string[] firstSlipt = rows[0].Split(" ");
    var (method, path, version) = (firstSlipt[0], firstSlipt[1], firstSlipt[2]);
    Console.WriteLine($"Method: {method}, Path: {path}, HTTP Version: {version}");

    // HTTP/1.1 404 Not Found\r\n\r\n
    string okResponse = $"{version} 200 OK{rn}";
    string notFoundResponse = $"{version} 404 Not Found{rn}{rn}";

    string response = string.Empty;
    string pathMessage = string.Empty;

    switch (path)
    {
        case "/":
            response = okResponse+rn;
            break;
        
        case { } when path.StartsWith("/echo"):
            // HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 3\r\n\r\nabc
            pathMessage = path.Replace("/echo/", "");
            response = $"{okResponse}Content-Type: text/plain{rn}Content-Length: {pathMessage.Length}{rn}{rn}{pathMessage}";
            break;

        case { } when path.StartsWith("/user-agent"):
            // HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 12\r\n\r\nfoobar/1.2.3
            pathMessage = path.Replace("/user-agent", "");
            response = $"{okResponse}Content-Type: text/plain{rn}Content-Length: {pathMessage.Length}{rn}{rn}{pathMessage}";
            break;

        default:
            // GET /index.html HTTP/1.1\r\nHost: localhost:4221\r\nUser-Agent: curl/7.64.1\r\nAccept: */*\r\n\r\n
            response = notFoundResponse;
            break;
    }

    Console.WriteLine($"Response:{rn}{response}{rn}End Response");

    await socket.SendAsync(ASCIIEncoding.UTF8.GetBytes(response));
}
catch (Exception e) { Console.WriteLine(e.Message); }

server.Stop();
server.Dispose();