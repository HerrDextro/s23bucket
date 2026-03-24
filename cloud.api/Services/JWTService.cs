using cloud.api.Models;
using cloud.api.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace cloud.api.Services
{
    public class JWTService
    {
        IOptions<JWTSettings> _settings;
        public JWTService(IOptions<JWTSettings> settings)
        {
            _settings = settings;
        }
        public string GenerateToken(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim("favorite_phonk_track", "GAU-8_Remix") // Custom claim! How to access: var track = user.FindFirst("favorite_phonk_track")?.Value;
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Value.SecretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_settings.Value.ExpiryInMinutes),
                Issuer = _settings.Value.Issuer,
                Audience = _settings.Value.Audience,
                SigningCredentials = creds
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }
        public string GenerateClientToken(string requestId)
        {
            var claims = new List<Claim>{
                new Claim("request_id", requestId)
            };
            
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Value.SecretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_settings.Value.ExternalClientExpiryInMinutes),
                Issuer = _settings.Value.Issuer,
                Audience = _settings.Value.Audience,
                SigningCredentials = creds
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }
    }
}
