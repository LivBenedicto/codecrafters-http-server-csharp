using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using static System.Net.WebRequestMethods;

TcpListener server = new TcpListener(IPAddress.Any, 4221);
server.Start();

try
{
    Socket socket = await server.AcceptSocketAsync();
    
    // GET /index.html HTTP/1.1\r\nHost: localhost:4221\r\nUser-Agent: curl/7.64.1\r\nAccept: */*\r\n\r\n
    byte[] responseBuffer = new byte[1024];
    int _ = await socket.ReceiveAsync(responseBuffer);
    string[] rows = ASCIIEncoding.UTF8.GetString(responseBuffer).Split("\r\n");

    // GET /index.html HTTP/1.1
    string[] firstSlipt = rows[0].Split(" ");
    var (method, path, version) = (firstSlipt[0], firstSlipt[1], firstSlipt[2]);
    Console.WriteLine($"Method: {method}, Path: {path}, HTTP Version: {version}");

    // HTTP/1.1 404 Not Found\r\n\r\n
    string okResponse = $"{version} 200 OK\r\n\r\n";
    string notFoundResponse = $"{version} 404 Not Found\r\n\r\n";

    string response = path == "/" ? okResponse : notFoundResponse;
    Console.WriteLine($"Response: {response}");

    await socket.SendAsync(ASCIIEncoding.UTF8.GetBytes(response));
}
catch (Exception e) { Console.WriteLine(e.Message); }

server.Stop();
server.Dispose();