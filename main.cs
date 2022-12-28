using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public static class Program
{
  //The port we want to listen on. This can be anything from 1024 to 65535.a
  public const int Port = 3000;

  //Answering regular HTTP requests
  public static byte[] HandleRequest(HttpListenerContext ctx)
  {
    HttpListenerRequest req = ctx.Request;
    HttpListenerResponse res = ctx.Response;

    res.StatusCode = 200;
    res.ContentType = "text/plain; charset=utf-8";

    string owner = Environment.GetEnvironmentVariable("REPL_OWNER");
    string slug = Environment.GetEnvironmentVariable("REPL_SLUG");

    return Encoding.UTF8.GetBytes($"WebSocket server is available at wss://{slug}.{owner}.repl.co");
  }

  //Answering and handling WebSocket requests
  public static async Task HandleWebsocket(HttpListenerContext ctx)
  {
    HttpListenerWebSocketContext wsCtx = await ctx.AcceptWebSocketAsync(null);
    CancellationTokenSource src = new();

    WebSocket ws = wsCtx.WebSocket;

    while (ws.State == WebSocketState.Open)
    {
      byte[] received = new byte[2048];
      int offset = 0;

      while (true)
      {
        try
        {
          ArraySegment<byte> bytesReceived = new(received, offset, received.Length);

          WebSocketReceiveResult result = await ws.ReceiveAsync(bytesReceived, src.Token);
          offset += result.Count;

          if (result.EndOfMessage) break;
        }
        catch { break; };
      }

      if (offset == 0) continue;

      await ws.SendAsync(new ArraySegment<byte>(received[..offset]), WebSocketMessageType.Text, true, src.Token);
    }
  }

  public static async Task Main()
  {
    HttpListener listener = new();
    listener.Prefixes.Add($"http://*:{Port}/");
    listener.Start();

    while (true)
    {
      HttpListenerContext ctx = null;

      try
      {
        ctx = listener.GetContext();

        if (ctx.Request.IsWebSocketRequest)
        {
          await HandleWebsocket(ctx);
        }
        else
        {
          byte[] buffer = HandleRequest(ctx);

          HttpListenerResponse res = ctx.Response;
          res.ContentLength64 = buffer.Length;
          res.OutputStream.Write(buffer, 0, buffer.Length);
          res.OutputStream.Flush();
          res.OutputStream.Close();
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine($"HTTP Server Exception:\n{ex}");

        byte[] buffer = Array.Empty<byte>();
        HttpListenerResponse res = ctx.Response;
        res.StatusCode = 500;
        res.ContentLength64 = buffer.Length;
        res.OutputStream.Write(buffer, 0, buffer.Length);
        res.OutputStream.Flush();
        res.OutputStream.Close();
      }
    }
  }
}
