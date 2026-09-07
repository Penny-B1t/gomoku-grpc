using GomokuServer.Domain.Models;

namespace GomokuServer.Tests;

/// <summary>
/// GameBoard.CheckWin 의 승리 판정 검증.
/// 특히 4개 방향축(세로·가로·주대각선 \ ·반대각선 /)이 모두 독립적으로
/// 검사되는지, 꺾인 형태가 승리로 오인되지 않는지를 회귀 테스트로 고정한다.
/// </summary>
public class GameBoardCheckWinTests
{
    private const int P1 = 1;
    private const int P2 = 2;

    /// <summary>지정한 좌표들에 같은 색 돌을 놓고, 마지막 좌표 기준으로 승리 여부를 반환.</summary>
    private static bool PlaceAndCheck(int player, params (int row, int col)[] stones)
    {
        var board = new GameBoard(15);
        foreach (var (r, c) in stones)
        {
            Assert.True(board.PlaceStone(r, c, player), $"({r},{c}) 착수 실패");
        }
        var (lr, lc) = stones[^1];
        return board.CheckWin(lr, lc, player);
    }

    // ---------------------------------------------------------------
    // 직선 5목 → 승리
    // ---------------------------------------------------------------

    [Fact]
    public void Horizontal_FiveInRow_IsWin()
        => Assert.True(PlaceAndCheck(P1, (7, 3), (7, 4), (7, 5), (7, 6), (7, 7)));

    [Fact]
    public void Vertical_FiveInRow_IsWin()
        => Assert.True(PlaceAndCheck(P1, (3, 7), (4, 7), (5, 7), (6, 7), (7, 7)));

    /// <summary>
    /// 주대각선 '\' (좌상단→우하단). 방향 벡터 정의 오류로 이 축이 검사되지
    /// 않던 버그의 회귀 테스트.
    /// </summary>
    [Fact]
    public void MainDiagonal_Backslash_FiveInRow_IsWin()
        => Assert.True(PlaceAndCheck(P1, (2, 2), (3, 3), (4, 4), (5, 5), (6, 6)));

    /// <summary>반대각선 '/' (좌하단→우상단).</summary>
    [Fact]
    public void AntiDiagonal_Slash_FiveInRow_IsWin()
        => Assert.True(PlaceAndCheck(P1, (6, 2), (5, 3), (4, 4), (3, 5), (2, 6)));

    [Theory]
    [InlineData(1, 0)]   // 세로
    [InlineData(0, 1)]   // 가로
    [InlineData(1, 1)]   // 주대각선 \
    [InlineData(1, -1)]  // 반대각선 /
    public void EveryAxis_FiveInRow_IsWin(int dr, int dc)
    {
        int startR = 7 - 2 * dr;
        int startC = 7 - 2 * dc;
        var stones = new (int, int)[5];
        for (int i = 0; i < 5; i++)
            stones[i] = (startR + i * dr, startC + i * dc);

        Assert.True(PlaceAndCheck(P1, stones));
    }

    [Fact]
    public void WinDetected_WhenPlacedStoneIsInTheMiddleOfTheLine()
    {
        // 양옆이 이미 채워진 상태에서 가운데 칸을 메워 5목 완성
        var board = new GameBoard(15);
        foreach (var (r, c) in new[] { (7, 3), (7, 4), (7, 6), (7, 7) })
            board.PlaceStone(r, c, P1);

        Assert.False(board.CheckWin(7, 4, P1));      // 아직 미완성
        board.PlaceStone(7, 5, P1);
        Assert.True(board.CheckWin(7, 5, P1));       // 가운데 메움 → 승리
    }

    [Fact]
    public void WinDetected_AtBoardEdge()
        => Assert.True(PlaceAndCheck(P1, (0, 0), (0, 1), (0, 2), (0, 3), (0, 4)));

    // ---------------------------------------------------------------
    // 승리가 아닌 경우
    // ---------------------------------------------------------------

    [Fact]
    public void FourInRow_IsNotWin()
        => Assert.False(PlaceAndCheck(P1, (7, 3), (7, 4), (7, 5), (7, 6)));

    /// <summary>
    /// ㄱ자(L자) 형태: 가로 4 + 세로 4가 코너를 공유해도 승리가 아니다.
    /// 각 축은 독립 계산되므로 가로 4, 세로 4로만 잡히고 합산되지 않는다.
    /// </summary>
    [Fact]
    public void BentShape_LShape_IsNotWin()
    {
        var board = new GameBoard(15);
        foreach (var (r, c) in new[]
        {
            (7, 2), (7, 3), (7, 4), (7, 5),   // 가로 4
            (4, 5), (5, 5), (6, 5),            // 세로 3 (코너 (7,5) 공유 → 세로로도 4)
        })
            board.PlaceStone(r, c, P1);

        Assert.False(board.CheckWin(7, 5, P1));   // 코너
        Assert.False(board.CheckWin(4, 5, P1));   // 세로 끝
    }

    [Fact]
    public void FiveWithAGap_IsNotWin()
    {
        // X X X X . X  → 연속이 아니므로 승리 아님
        var board = new GameBoard(15);
        foreach (var (r, c) in new[] { (7, 3), (7, 4), (7, 5), (7, 6), (7, 8) })
            board.PlaceStone(r, c, P1);

        Assert.False(board.CheckWin(7, 8, P1));
    }

    [Fact]
    public void LineBlockedByOpponent_IsNotWin()
    {
        var board = new GameBoard(15);
        board.PlaceStone(7, 3, P1);
        board.PlaceStone(7, 4, P1);
        board.PlaceStone(7, 5, P2);   // 상대 돌이 중간을 끊음
        board.PlaceStone(7, 6, P1);
        board.PlaceStone(7, 7, P1);
        board.PlaceStone(7, 8, P1);

        Assert.False(board.CheckWin(7, 8, P1));
    }

    [Fact]
    public void OpponentFiveInRow_IsNotWinForCurrentPlayer()
    {
        var board = new GameBoard(15);
        foreach (var (r, c) in new[] { (7, 3), (7, 4), (7, 5), (7, 6), (7, 7) })
            board.PlaceStone(r, c, P2);

        Assert.False(board.CheckWin(7, 7, P1));   // P1 기준으로는 승리 아님
        Assert.True(board.CheckWin(7, 7, P2));
    }

    // ---------------------------------------------------------------
    // 규칙 명세: 현재 구현은 자유 오목(장목 허용)
    // ---------------------------------------------------------------

    [Fact]
    public void SixInRow_Overline_IsWin_FreestyleRule()
    {
        // count >= 5 이므로 6목 이상도 승리로 처리된다(자유 오목).
        // 렌주 규칙 도입 시 이 테스트를 반전시켜야 한다.
        Assert.True(PlaceAndCheck(P1, (7, 2), (7, 3), (7, 4), (7, 5), (7, 6), (7, 7)));
    }
}
