using GomokuGame.Proto;

namespace GomokuServer.Domain.Models;

public class GameRoom
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } // 방 이름
    public Player Player1 { get; private set; } // 흑돌
    public Player Player2 { get; private set; } // 백돌
    public GameBoard Board { get; }
    public GameStatus Status { get; set; } = GameStatus.Waiting;
    public int CurrentPlayerNumber { get; set; } = 1; // 1부터 시작 (흑돌)
    public DateTime CreatedAt { get; } = DateTime.UtcNow; 

    public GameRoom(string name, string creatorName, int boradSize = 15)
    {
        Name = name;
        Player1 = new Player(creatorName, 1);
        Board = new GameBoard(boradSize)
    }

    // 추가된 사용자 정보 반환
    public bool TryAddPlayer(string playerName, out Player? player)
    {
        player = null
        // 방이 대기 상태가 아니면서 플레이어가 비어있지 않은 경우 조기 탈출
        if( Status != GameStatus.Waiting || Player2 != nul)
        {
            return false
        }

        player = new Player(playerName, 2);
        Player2 = player;
        return true;
    }

    // 돌 놓기 요청한 사용자가 현재 자신의 차례인지 확인
    public bool IsPlayerTurn(string playerId)
    {
        return (CurrentPlayerNumber == 1 && Player1?.Id == playerId) ||
               (CurrentPlayerNumber == 2 && Player2?.Id == playerId);
    }

    // 상대방 플레이어 정보 반환
    public Player? GetOpponent(string playerId)
    {
        if (Player1?.Id == playerId) return Player2;
        if (Player2?.Id == playerId) return Player1;
        return null;
    }
}