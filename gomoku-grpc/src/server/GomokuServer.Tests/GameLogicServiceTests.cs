using GomokuGame.Proto;
using GomokuServer.Domain.Models;
using GomokuServer.Domain.Services;

namespace GomokuServer.Tests;

/// <summary>
/// GameLogicService.PlaceStone 의 턴·상태·좌표 검증과 승패 확정 흐름 테스트.
/// </summary>
public class GameLogicServiceTests
{
    private readonly GameLogicService _logic = new();

    /// <summary>두 명이 입장해 진행 중(Playing)인 방을 만든다. (Player1=흑=선공)</summary>
    private static GameRoom NewPlayingRoom(out string p1, out string p2)
    {
        var room = new GameRoom("test-room", "black");
        room.TryAddPlayer("white", out _);
        room.Status = GameStatus.Playing;
        p1 = room.Player1.Id;
        p2 = room.Player2!.Id;
        return room;
    }

    [Fact]
    public void PlaceStone_WhenNotYourTurn_Fails()
    {
        var room = NewPlayingRoom(out _, out var p2);

        var (success, error) = _logic.PlaceStone(room, 7, 7, p2);   // 선공은 p1인데 p2가 시도

        Assert.False(success);
        Assert.Contains("턴", error);
    }

    [Fact]
    public void PlaceStone_ValidMove_SwitchesTurn()
    {
        var room = NewPlayingRoom(out var p1, out _);

        var (success, _) = _logic.PlaceStone(room, 7, 7, p1);

        Assert.True(success);
        Assert.Equal(2, room.CurrentPlayerNumber);
    }

    [Fact]
    public void PlaceStone_OnOccupiedCell_Fails()
    {
        var room = NewPlayingRoom(out var p1, out var p2);
        _logic.PlaceStone(room, 7, 7, p1);

        var (success, _) = _logic.PlaceStone(room, 7, 7, p2);   // 같은 칸

        Assert.False(success);
    }

    [Fact]
    public void PlaceStone_OutOfBounds_Fails()
    {
        var room = NewPlayingRoom(out var p1, out _);

        var (success, _) = _logic.PlaceStone(room, 15, 0, p1);   // 15x15 → 인덱스 15는 범위 밖

        Assert.False(success);
    }

    [Fact]
    public void PlaceStone_WhenGameNotPlaying_Fails()
    {
        var room = NewPlayingRoom(out var p1, out _);
        room.Status = GameStatus.Finished;

        var (success, _) = _logic.PlaceStone(room, 7, 7, p1);

        Assert.False(success);
    }

    [Fact]
    public void PlaceStone_CompletingMainDiagonal_FinishesGameWithWinner()
    {
        var room = NewPlayingRoom(out var p1, out var p2);

        // p1: 주대각선 (0,0)~(4,4) / p2: 세로 (1,0)~(4,0) — 번갈아 착수
        Play(room, p1, 0, 0);
        Play(room, p2, 1, 0);
        Play(room, p1, 1, 1);
        Play(room, p2, 2, 0);
        Play(room, p1, 2, 2);
        Play(room, p2, 3, 0);
        Play(room, p1, 3, 3);
        Play(room, p2, 4, 0);

        var (success, message) = _logic.PlaceStone(room, 4, 4, p1);   // 대각선 5목 완성

        Assert.True(success);
        Assert.Contains("승리", message);
        Assert.Equal(GameStatus.Finished, room.Status);
        Assert.Equal(p1, room.WinnerId);
    }

    private void Play(GameRoom room, string playerId, int r, int c)
    {
        var (success, error) = _logic.PlaceStone(room, r, c, playerId);
        Assert.True(success, $"착수 실패 ({r},{c}): {error}");
    }
}
