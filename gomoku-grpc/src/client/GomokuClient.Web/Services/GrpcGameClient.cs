using Grpc.Net.Client;
using GomokuGame.Proto;

namespace GomokuClient.Web.Services;

public class GrpcGameClient : IDisposable
{
    private readonly GameService.GameServiceClient _client;
    private readonly GrpcChannel _channel;
    
    // 현재 로그인한 플레이어 정보
    public string? PlayerId { get; set; }
    public string? PlayerName { get; set; }
    public string? CurrentRoomId { get; set; }
    public bool IsPlayer1 { get; set; }

    public GrpcGameClient(string serverUrl = "http://localhost:5224")
    {
        // 개발 환경에서 자체 서명 인증서 허용
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        
        _channel = GrpcChannel.ForAddress(serverUrl, new GrpcChannelOptions
        {
            HttpHandler = handler
        });
        _client = new GameService.GameServiceClient(_channel);
    }

    public async Task<CreateRoomResponse> CreateRoomAsync(string roomName, string creatorName)
    {
        var response = await _client.CreateRoomAsync(new CreateRoomRequest
        {
            RoomName = roomName,
            CreatorName = creatorName
        });
        
        PlayerId = response.Creator.PlayerId;
        PlayerName = creatorName;
        CurrentRoomId = response.RoomId;
        IsPlayer1 = true;
        
        return response;
    }

    public async Task<RoomList> GetRoomListAsync()
    {
        return await _client.GetRoomListAsync(new Empty());
    }

    public async Task<JoinRoomResponse?> JoinRoomAsync(string roomId, string playerName)
    {
        try
        {
            var response = await _client.JoinRoomAsync(new JoinRoomRequest
            {
                RoomId = roomId,
                PlayerName = playerName
            });
            
            PlayerId = response.Joiner?.PlayerId;
            PlayerName = playerName;
            CurrentRoomId = roomId;
            IsPlayer1 = response.IsPlayer;
            
            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"방 입장 실패: {ex.Message}");
            return null;
        }
    }

    public async Task<GameState?> PlaceStoneAsync(string roomId, string playerId, int row, int col)
    {
        try
        {
            return await _client.PlaceStoneAsync(new PlaceStoneRequest
            {
                RoomId = roomId,
                PlayerId = playerId,
                Position = new Position { Row = row, Col = col }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"돌 놓기 실패: {ex.Message}");
            return null;
        }
    }

    public async Task<GameState?> SurrenderAsync(string roomId, string playerName)
    {
        try
        {
            return await _client.SurrenderAsync(new JoinRoomRequest
            {
                RoomId = roomId,
                PlayerName = playerName
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"항복 실패: {ex.Message}");
            return null;
        }
    }

    // stones 포함한 전체 게임 상태 조회
    public async Task<GameState?> GetGameStateAsync(string roomId)
    {
        try
        {
            return await _client.GetGameStateAsync(new RoomInfo { RoomId = roomId });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"게임 상태 조회 실패: {ex.Message}");
            return null;
        }
    }

    // SSE 엔드포인트에서 사용할 WatchGame 서버 스트리밍 호출
    public Grpc.Core.AsyncServerStreamingCall<GameState> WatchGameStream(string roomId, CancellationToken cancellationToken = default)
    {
        return _client.WatchGame(new RoomInfo { RoomId = roomId }, cancellationToken: cancellationToken);
    }

    public void Dispose()
    {
        _channel.Dispose();
    }
}