using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using GomokuClient.Web.Services;
using GomokuGame.Proto;

namespace GomokuClient.Web.Pages;

public class GameModel : PageModel
{
    private readonly GrpcGameClient _gameClient;
    private readonly ILogger<GameModel> _logger;

    [BindProperty(SupportsGet = true)]
    public string RoomId { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string PlayerName { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public bool IsPlayer1 { get; set; }

    [BindProperty(SupportsGet = true)]
    public string PlayerId { get; set; } = string.Empty;

    public GameModel(GrpcGameClient gameClient, ILogger<GameModel> logger)
    {
        _gameClient = gameClient;
        _logger = logger;
    }

    public IActionResult OnGet()
    {
        if (string.IsNullOrEmpty(RoomId) || string.IsNullOrEmpty(PlayerName) || string.IsNullOrEmpty(PlayerId))
        {
            return RedirectToPage("Index");
        }

        _gameClient.CurrentRoomId = RoomId;
        _gameClient.PlayerName = PlayerName;
        _gameClient.IsPlayer1 = IsPlayer1;
        _gameClient.PlayerId = PlayerId;

        return Page();
    }

    public async Task<IActionResult> OnGetGameState(string roomId)
    {
        try
        {
            var gameState = await _gameClient.GetGameStateAsync(roomId);
            return new JsonResult(new { gameState });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "게임 상태 조회 실패");
            return new JsonResult(new { gameState = (GameState?)null });
        }
    }

    public async Task<IActionResult> OnPostPlaceStone([FromBody] PlaceStoneModel model)
    {
        try
        {
            var gameState = await _gameClient.PlaceStoneAsync(model.RoomId, model.PlayerId, model.Row, model.Col);
            return new JsonResult(new {
                success = gameState != null,
                gameState,
                message = gameState != null ? "돌을 놓았습니다." : "돌을 놓을 수 없습니다."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "돌 놓기 실패");
            return new JsonResult(new { success = false, message = "돌 놓기 중 오류가 발생했습니다." });
        }
    }

    public async Task<IActionResult> OnPostSurrender([FromBody] SurrenderModel model)
    {
        try
        {
            var gameState = await _gameClient.SurrenderAsync(model.RoomId, model.PlayerName);
            return new JsonResult(new {
                success = gameState != null,
                gameState
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "항복 실패");
            return new JsonResult(new { success = false });
        }
    }
}

public class PlaceStoneModel
{
    public string RoomId { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public int Row { get; set; }
    public int Col { get; set; }
}

public class SurrenderModel
{
    public string RoomId { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
}