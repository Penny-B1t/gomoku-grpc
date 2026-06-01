namespace GomokuServer.Domain.Models;

public class Player 
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; }
    public int PlayerNumber { get; set; }
    public DateTime ConnectedAt { get; } = DateTime.UtcNow;

    // 생성자 함수
    public Player(string name, int playerNumber)
    {
        Name = name;
        PlayerNumber = playerNumber;
    }
}

