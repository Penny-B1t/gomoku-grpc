namespace GomokuServer.Domain.Models;

public class GameBoard
{
    // 0: 빈 돌, 1: 흑돌, 2: 백돌
    private readonly int[,] _board;
    public int Size { get; }

    // 생성자 함수
    public GameBoard(int size = 15)
    {
        Size = size;
        _board = new int[size, size];
    }

    // 중심이 0, 0 인 x y 좌표가 아닌 배열을 통한 x,y 좌표
    // 다음과 같이 구현 시 첫번째 인자를 [0,0] 기준으로 Size 만큼 
    public bool IsValidPostion(int row, int col)
    {
        return row >= 0 && row < Size && col >= 0 && col < Size && _board[row, col] == 0;
    }

    // 배열 내에 Value를 사용자id로 설정
    public bool PlaceStone(int row, int col, int PlayerNumber)
    {
        // 이동 가능한 구역인지 확인 및 조기 탈출 
        if(!IsValidPostion(row,col))
        {
            return false;
        }

        _board[row, col] = PlayerNumber;
        return true;
    }

    //
    public int GetCell(int row, int col)
    {
        return _board[row, col];
    }

    // 돌을 놓았을 당시 사용자가 이겼는지 여부 확인
    public bool CheckWin(int row, int col, int playerNumber)
    {
        // 4개 방향축: 세로(1,0) · 가로(0,1) · 주대각선 \ (1,1) · 반대각선 / (1,-1)
        // 각 축은 아래에서 (dx,dy)와 (-dx,-dy) 양방향으로 스캔되므로 8방향을 모두 포함한다.
        int[] dx = { 1, 0, 1, 1 };
        int[] dy = { 0, 1, 1, -1 };

        for ( int i = 0; i < 4; i++)
        {
            // 현재 검사 중심에 놓인 돌 1개 포함
            int count = 1;

            // 축을 기준으로 양방향 검사
            count += CountDirection(row, col, dx[i], dy[i], playerNumber);
            count += CountDirection(row, col, -dx[i], -dy[i], playerNumber);

            if(count >= 5)
            {
                return true;
            }
        }

        return false;
    }

    private int CountDirection(int row, int col, int dx, int dy, int playerNumber)
    {
        int count = 0;
        row += dx;
        col += dy;

        // 조건에 만족하는 경우에만 이동 진행 
        while(row >= 0 && row < Size && col >= 0 && col < Size && _board[row, col] == playerNumber)
        {
            count++;
            row += dx;
            col += dy;
        }
        return count;
    }

    public List<(int row, int col)> GetEmptyCells()
    {
        var emptyCells = new List<(int row, int col)>();
        for (int i = 0; i < Size; i++)
            for (int j = 0; j < Size; j++)
                if (_board[i, j] == 0)
                    emptyCells.Add((i, j));
        return emptyCells;
    }

}