using GomokuClient.Web.Services;
using Google.Protobuf;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddSingleton<GrpcGameClient>();
builder.Services.AddAntiforgery();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();

// SSE: 브라우저가 WatchGame 서버 스트리밍을 구독하는 엔드포인트
app.MapGet("/api/game/watch", async (string roomId, GrpcGameClient gameClient, HttpContext context) =>
{
    context.Response.ContentType = "text/event-stream";
    context.Response.Headers["Cache-Control"] = "no-cache";
    context.Response.Headers["X-Accel-Buffering"] = "no";

    var formatter = new JsonFormatter(JsonFormatter.Settings.Default);
    var ct = context.RequestAborted;

    try
    {
        using var stream = gameClient.WatchGameStream(roomId);
        while (await stream.ResponseStream.MoveNext(ct))
        {
            var json = formatter.Format(stream.ResponseStream.Current);
            await context.Response.WriteAsync($"data: {json}\n\n", ct);
            await context.Response.Body.FlushAsync(ct);
        }
    }
    catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Cancelled) { }
    catch (OperationCanceledException) { }
});

app.Run();