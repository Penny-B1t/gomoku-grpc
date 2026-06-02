using System.Collections.Concurrent;
using GomokuGame.Proto;
using GomokuServer.Domain.Services;
using GomokuServer.Infrastructure;
using Grpc.Core;

namespace GomokuServer.Services;

public class GameService : GomokuGame.Proto.GameService.GameServiceBase
{
    private readonly GameRoomManager _roomManager;
    private readonly GameLogicService _gameLogic;
    private readonly ILogger<GameService> _logger;

    // 실시간 스트림 관리를 위한 자료형 
    // IServerStreamWriter<ExampleResponse> 기본 형태에 string 키(방 ID)를 추가한 형태
    private static readonly ConcurrentDictionary<string, List<IServerStreamWriter<GameState>>> _watchers = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _roomLocks = new();

    public GameService(GameRoomManager roomManager, GameLogicService gameLogic, ILogger<GameService> logger)
    {
        _roomManager = roomManager;
        _gameLogic = gameLogic;
        _logger = logger;
    }

    // 생성한 방의 정보를 Task로 반환
    // unary : 단항 통신
    public override Task<CreateRoomResponse> CreateRoom(CreateRoomRequest request, ServerCallContext context)
    {
        var room = _roomManager.CreateRoom(request.RoomName, request.CreatorName);
        _logger.LogInformation($"방 생성: {room.Id} - {room.Name}");

        return Task.FromResult(new CreateRoomResponse
        {
            RoomId = room.Id,
            Creator = new PlayerInfo
            {
                PlayerId = room.Player1.Id,
                PlayerName = room.Player1.Name
            }
        });
    }

    public override Task<RoomList> GetRoomList(Empty request, ServerCallContext context)
    {
        var roomList = new RoomList();

        foreach (var room in _roomManager.GetAllRooms())
        {
            roomList.Rooms.Add(new RoomInfo
            {
                RoomId = room.Id,
                RoomName = room.Name,
                PlayerCount = room.Player2 == null ? 1 : 2,
                Status = room.Status
            });
        }

        return Task.FromResult(roomList);
    }

    public override Task<JoinRoomResponse> JoinRoom(JoinRoomRequest request, ServerCallContext context)
    {
        var room = _roomManager.GetRoom(request.RoomId);
        if (room == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "방을 찾을 수 없습니다."));
        }

        if (!room.TryAddPlayer(request.PlayerName, out var player))
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "방에 입장할 수 없습니다."));
        }

        room.Status = GameStatus.Playing;
        _logger.LogInformation($"{player!.Name}님이 {room.Name}에 입장했습니다.");

        return Task.FromResult(new JoinRoomResponse
        {
            RoomId = room.Id,
            Opponent = new PlayerInfo
            {
                PlayerId = room.Player1.Id,
                PlayerName = room.Player1.Name
            },
            BoardSize = room.Board.Size,
            IsPlayer = false,
            Joiner = new PlayerInfo
            {
                PlayerId = player.Id,
                PlayerName = player.Name
            }
        });
    }

    public override Task<GameState> GetGameState(RoomInfo request, ServerCallContext context)
    {
        var room = _roomManager.GetRoom(request.RoomId);
        if (room == null)
            throw new RpcException(new Status(StatusCode.NotFound, "방을 찾을 수 없습니다."));

        return Task.FromResult(_gameLogic.GetGameState(room));
    }

    public override async Task<GameState> PlaceStone(PlaceStoneRequest request, ServerCallContext context)
    {
        var room = _roomManager.GetRoom(request.RoomId);
        if (room == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "방을 찾을 수 없습니다."));
        }

        var (success, message) = _gameLogic.PlaceStone(room, request.Position.Row, request.Position.Col, request.PlayerId);

        if (!success)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, message));
        }

        // 룸의 상태를 보고 승자ID를 결정합니다.
        var gameState = _gameLogic.GetGameState(room, room.Status == GameStatus.Finished ? request.PlayerId : null);

        // 현재 room에 존재하는 사용자들에게 실시간으로 게임 진행 상황을 전달합니다.
        // return은 현재 사용자에게 NotifyWatchers는 나머지 사용자들에게 정보 전달
        await NotifyAllWatcherAsync(request.RoomId, gameState);

        return gameState;
    }

    public override async Task WatchGame(RoomInfo request, IServerStreamWriter<GameState> responseStream, ServerCallContext context)
    {
        AddWatcher(request.RoomId, responseStream);

        try
        {
            // 불필요한 쓰레드 할당 및 점유 방지를 위해 1초 폴링 루프 대신, 취소 토큰이 트리거될 때까지 무한히 비동기 대기 (자원 소모 최소화)
            await Task.Delay(Timeout.InfiniteTimeSpan, context.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation($"클라이언트 연결이 끊어졌습니다.");
        }
        finally
        {
            // 모든 상황에서 안전하게 제거되도록 보장
            RemoveWatcher(request.RoomId, responseStream);
        }
    }

    public override async Task<GameState> Surrender(JoinRoomRequest request, ServerCallContext context)
    {
        var room = _roomManager.GetRoom(request.RoomId) ?? throw new RpcException(new Status(StatusCode.NotFound, "방을 찾을 수 없습니다."));
        room.Status = GameStatus.Finished;
        string winnerId = room.Player1.Name == request.PlayerName
            ? room.Player2!.Id
            : room.Player1.Id;
        room.WinnerId = winnerId;

        var gameState = _gameLogic.GetGameState(room, winnerId);
        await NotifyAllWatcherAsync(request.RoomId, gameState);

        return gameState;
    }

    // 양방향 스트리밍 (실시간 플레이)
    public override async Task PlayGame(IAsyncStreamReader<PlaceStoneRequest> requestStream,
        IServerStreamWriter<GameState> responseStream, ServerCallContext context)
    {
        await foreach (var request in requestStream.ReadAllAsync())
        {
            var room = _roomManager.GetRoom(request.RoomId);
            if (room == null) continue;

            var (success, _) = _gameLogic.PlaceStone(room, request.Position.Row, request.Position.Col, request.PlayerId);

            if (success)
            {
                var winnerId = room.Status == GameStatus.Finished ? request.PlayerId : null;
                var gameState = _gameLogic.GetGameState(room, winnerId);
                await responseStream.WriteAsync(gameState);
                await NotifyAllWatcherAsync(request.RoomId, gameState);
            }

        }
    }

    public void AddWatcher(string roomId, IServerStreamWriter<GameState> writer)
    {
        _watchers.AddOrUpdate(
            roomId,
            new List<IServerStreamWriter<GameState>>() { writer },
            (_, list) =>
            {
                lock (list)
                {
                    list.Add(writer);
                }

                return list;
            });

        _logger.LogDebug("방 {RoomId}에 감시자 추가 (현재: {Count})", roomId, _watchers[roomId].Count);
    }

    public void RemoveWatcher(string roomId, IServerStreamWriter<GameState> writer)
    {
        if (_watchers.TryGetValue(roomId, out var watchers))
        {
            lock (watchers)
            {
                watchers.Remove(writer);
            }
            _logger.LogDebug("방 {RoomId}에서 감시자 제거 (남은: {Count})",
                roomId, watchers.Count);
        }

    }

    public async Task NotifyAllWatcherAsync(string roomId, GameState gameState)
    {
        if (!_watchers.TryGetValue(roomId, out var watchers))
            return;

        List<IServerStreamWriter<GameState>> snapshot;

        lock (watchers)
        {
            snapshot = watchers.ToList();
        }

        if (snapshot.Count == 0)
            return;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // 모든 감시자에게 병렬(Concurrent)로 알림을 전송하여 하나의 느린 감시자가 다른 감시자 전송을 막지 않게 합니다.
        var tasks = snapshot.Select(async writer =>
        {
            bool success = await TryNotifyWatcherAsync(writer, gameState, roomId, cts.Token);
            return (Writer: writer, Success: success);
        }).ToList();

        var results = await Task.WhenAll(tasks);
        var removeList = results.Where(r => !r.Success).Select(r => r.Writer).ToList();

        if (removeList.Count > 0)
        {
            lock (watchers)
            {
                foreach (var dead in removeList)
                    watchers.Remove(dead);
            }

            _logger.LogInformation("방 {RoomId}에서 {Count}명의 연결 끊긴 감시자 제거", roomId, removeList.Count);
        }
    }

    private async Task<bool> TryNotifyWatcherAsync(
        IServerStreamWriter<GameState> writer,
        GameState gameState,
        string roomId,
        CancellationToken cancellationToken)
    {
        try
        {
            // WriteAsync 메서드 매개변수로 직접 CancellationToken을 넘겨 소켓 수준에서 전송을 중단시킴
            await writer.WriteAsync(gameState, cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("방 {RoomId} 감시자 알림 전송 타임아웃 (5초 초과)", roomId);
            return false;
        }
    }

}