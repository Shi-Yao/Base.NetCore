using Base.Core.Utils;
using Base.NetCoreAPI.Dtos.Request;
using Base.NetCoreAPI.Dtos.Response;
using Microsoft.AspNetCore.Mvc;

namespace Base.NetCoreAPI.Controllers
{
    [ApiController]
    [Route("/Login")]
    public class LoginController : Controller
    {
        private readonly JWTUtil _jwt;
        public LoginController(JWTUtil jwt)
        {
            _jwt = jwt;
        }

        [HttpPost]
        public async Task<TokenResponseDto> GetToken(TokenRequestDto requestDto)
        {
            TokenResponseDto tokenResponseDto = new TokenResponseDto();
            tokenResponseDto.UserId = "TEST";
            tokenResponseDto.AccessToken = _jwt.GetAccessToken(requestDto.UserId);
            return tokenResponseDto;
        }
    }
}
