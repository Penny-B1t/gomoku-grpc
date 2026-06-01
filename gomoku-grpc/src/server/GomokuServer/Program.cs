using GomokuServer.Domain.Services;
using GomokuServer.Infrastructure;
using GomokuServer.Services;

var builder = WebApplication.CreateBuilder(args);

// 서비스 등록
builder.Services.AddGrpc();
builder.Services.AddSingleton<GameRoomManager>();
builder.Services.AddSingleton<GameLogicService>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureEndpointDefaults(listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });
});

var app = builder.Build();

app.UseCors();
app.MapGrpcService<GameService>();
app.MapGet("/", () => "gRPC 오목 게임 서버가 실행 중입니다.");

app.Run();