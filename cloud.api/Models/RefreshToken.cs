namespace cloud.api.Models
{
    public class RefreshToken
    {
        public string UserId { get; set; }
        public DateOnly ExpiryDate { get; set; }

        public string Token { get; set; }
    }
}
