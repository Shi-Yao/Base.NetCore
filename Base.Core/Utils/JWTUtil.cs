using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Base.Core.Utils
{
    public class JWTUtil
    {
        public readonly IConfiguration _configuration;

        public JWTUtil(IConfiguration configuration)
        {
            this._configuration = configuration;
        }

        /// <summary>
        /// 產生JWT token
        /// </summary>
        /// <param name="userId">使用者ID</param>
        /// <param name="expireMinutes">到期時間</param>
        /// <returns></returns>
        public string GenerateToken(string userId, string tokenKey, int expireMinutes = 30)
        {
            var key = Encoding.ASCII.GetBytes(tokenKey);
            var claims = new List<Claim>();

            claims.Add(new Claim(JwtRegisteredClaimNames.Sub, userId));
            claims.Add(new Claim(JwtRegisteredClaimNames.Iss, _configuration.GetSection("JwtSettings:Issuer").Value));
            claims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())); // JWT ID

            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            claims.Add(new Claim(ClaimTypes.Role, "Users"));

            var userClaimsIdentity = new ClaimsIdentity(claims);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = userClaimsIdentity,
                Expires = DateTime.Now.AddMinutes(expireMinutes),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            // Generate a JWT securityToken, than get the serialized Token result (string)
            var tokenHandler = new JwtSecurityTokenHandler();
            var securityToken = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(securityToken);
        }

        public string GetAccessToken(string userId, int expireMinutes = 30)
        {
            return GenerateToken(userId, _configuration.GetValue<string>("JwtSettings:AccessKey"), expireMinutes);
        }
    }
}
