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
        room.Board.

        
        
    }
}