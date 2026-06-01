using System.Collections.Concurrent;
using GomokuServer.Domain.Models;

namespace GomokuServer.Infrastructure;

// 메모리 형태의 데이터 저장소 제공
public class GameRoomManager
{
    // 스레드 안전한 동시성 컬렉션
    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new();

    // 추가된 Room 정보 반환 메서드
    public GameRoom CreateRoom(string name, string creatorName)
    {
        var room, = new GameRoom(name, creatorName)
        _rooms.TryAdd(room.Id, room);
        return room;
    }

    public GameRoom? GetRoom(string roomId)
    {
        _rooms.TryGetValue(roomId, out var room);
        return room;
    }

    // IEnumerable 타입으로 반환하여 for each로 반복문 사용 가능하도록 지연 실행
    public IEnumerable<GameRoom> GetAllRooms()
    {
        return _rooms.Values;
    }

    public void RemoveRoom(string roomId)
    {
        _rooms.TryRemove(roomId, out _);
    }
}