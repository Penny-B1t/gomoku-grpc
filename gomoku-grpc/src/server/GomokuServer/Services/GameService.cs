using Grpc.Core;
using GomokuGame.Proto;
using GomokuServer.Domain.Services;
using GomokuServer.Infrastructure;

namespace GomokuServer.Services;

public class GameService : GomokuGame.Proto.GameService.GameServiceBase
{
    private readonly GameRoomManager _roomManager;
    private readonly GameLogicService _gameLogic;
    private readonly ILogger<GameService> _logger;

    // 실시간 스트림 관리를 위한 자료형 
    // IServerStreamWriter<ExampleResponse> 기본 형태에 string 키(방 ID)를 추가한 형태
    private static readonly Dictionary<string, List<IServerStreamWriter<GameState>>> _watchers = new();

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
                PlayerId  = room.Player1.Id,
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
        if(room == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "방을 찾을 수 없습니다."));
        }

        if(!room.TryAddPlayer(request.PlayerName, out var player))
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

    public override Task<GameState> PlaceStone(PlaceStoneRequest request, ServerCallContext context)
    {
        var room = _roomManager.GetRoom(request.RoomId);
        if(room == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "방을 찾을 수 없습니다."));
        }

        var (success, message) = _gameLogic.PlaceStone(room, request.Position.Row, request.Position.Col, request.PlayerId);

        if(!success)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, message));
        }

        // 룸의 상태를 보고 승자ID를 결정합니다.
        var gameState = _gameLogic.GetGameState(room, room.Status == GameStatus.Finished ? request.PlayerId : null);
        
        // 현재 room에 존재하는 사용자들에게 실시간으로 게임 진행 상황을 전달합니다.
        // return은 현재 사용자에게 NotifyWatchers는 나머지 사용자들에게 정보 전달
        NotifyWatchers(request.RoomId, gameState);

        return Task.FromResult(gameState);
    }

    // 소켓 서버에서 socket을 저장하는 것과 동일한 역할을 합니다.
    // 하지만 grpc는 IServerStreamWriter<>를 사용합니다.
     public override async Task WatchGame(RoomInfo request, IServerStreamWriter<GameState> responseStream, ServerCallContext context)
     {
        if(!_watchers.ContainsKey(request.RoomId))
        {
            _watchers[request.RoomId] = new List<IServerStreamWriter<GameState>>();
        }
        _watchers[request.RoomId].Add(responseStream);

        // context 사용자의 연결 환경 정보를 확인하여 연결이 끊어지면 리스트에서 제거합니다.
        while (!context.CancellationToken.IsCancellationRequested)
        {
            await Task.Delay(1000);
        }

        _watchers[request.RoomId].Remove(responseStream);
     }

    public override Task<GameState> Surrender(JoinRoomRequest request, ServerCallContext context)
    {
        var room = _roomManager.GetRoom(request.RoomId);
        if(room == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "방을 찾을 수 없습니다."));
        }

        room.Status =  GameStatus.Finished;
        string winnerId = room.Player1.Name == request.PlayerName 
            ? (room.Player2?.Id ?? string.Empty) 
            : room.Player1.Id;
        room.WinnerId = winnerId;

        var gameState = _gameLogic.GetGameState(room, winnerId);
        NotifyWatchers(request.RoomId, gameState);

        return Task.FromResult(gameState);
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
                    NotifyWatchers(request.RoomId, gameState);
                }
                
            }
        }

    private async void NotifyWatchers(string roomId, GameState gameState)
    {
        if (_watchers.ContainsKey(roomId))
        {
            var deadWriters = new List<IServerStreamWriter<GameState>>();
            foreach (var watcher in _watchers[roomId])
            {
                try
                {
                    await watcher.WriteAsync(gameState);
                }
                catch
                {
                    deadWriters.Add(watcher);
                }
            }
            foreach (var dead in deadWriters)
                _watchers[roomId].Remove(dead);
        }
    }


} 