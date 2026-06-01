using GomokuGame.Proto;
using GomokuServer.Domain.Models;

namespace GomokuServer.Domain.Services;

public class GameLogicService
{
    // 돌을 놓음으로써 변경된 GameRoom의 상태를 변경합니다.
    public (bool success, string error) PlaceStone(GameRoom room, int row, int col, string playerId)
    {
        if(room.Status != GameStatus.Playing)
        {
            return (false, "게임이 진행 중이 아닙니다.");
        }

        if(!room.IsPlayerTurn(playerId))
        {
            return (false, "현재 당신의 턴이 아닙니다.");
        }

        if(!room.Board.IsValidPostion(row, col))
        {
            return (false, "유효하지 않은 위치입니다.");
        }

        int playerNumber = playerId == room.Player1.Id ? 1 : 2;
        room.Board.PlaceStone(row, col, playerNumber);

        if(room.Board.CheckWin(row, col, playerNumber))
        {
            room.Status = GameStatus.Finished;
            return (true, $"Player {playerNumber} 승리!");
        }

        room.CurrentPlayerNumber = room.CurrentPlayerNumber == 1 ? 2 : 1;
        return (true, "돌이 놓였습니다.");
    }

    public GameState GetGameState(GameRoom room, string? winnerId = null)
    {
        var stones = new List<Stone>();
        for (int i = 0; i < room.Board.Size; i++)
        {
            for (int j = 0; j < room.Board.Size; j++)
            {
                int cell = room.Board.GetCell(i, j);
                if(cell != 0)
                {
                    stones.Add(new Stone
                    {
                        Position = new Position { Row = i, Col = j },
                        PlayerNumber = cell
                    });
                }

            }
        }

        return new GameState
        {
            Stones = { stones },
            CurrentPlayer = room.CurrentPlayerNumber,
            Status = room.Status,
            WinnerId = winnerId ?? string.Empty,
            BoardSize = room.Board.Size
        }
    }
}