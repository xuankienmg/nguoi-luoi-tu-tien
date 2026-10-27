namespace VanDao.Data;

public class OnlineRequest
{
    public Guid OwnerId { get; set; }
    public Guid RequestId { get; set; }
    public string Fingerprint { get; set; } = "";
    public string Response { get; set; } = "";
    public int Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
