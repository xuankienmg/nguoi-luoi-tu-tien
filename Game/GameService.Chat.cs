namespace VanDao.Game;

/// <summary>Kênh Thế giới: GameService chỉ đẩy tin hệ thống; đọc/gửi tin người chơi ở ServerChat.</summary>
public partial class GameService
{
    /// <summary>Chỉ online Server (serverChat được DI cấp). Tin lưu cùng lần lưu nhân vật.</summary>
    private void ChatSystem(string text)
    {
        if (serverChat is not null) ServerChat.AddSystem(db, text, DateTime.UtcNow);
    }
}
