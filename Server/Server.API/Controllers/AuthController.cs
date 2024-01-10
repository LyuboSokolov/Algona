﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Server.Common.Requests.AuthRequests;
using Server.Data.Entites;
using Server.Data.Interfaces.Repositories;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Server.API.Controllers
{
    /// <summary>
    /// Authentication controller
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<User> userManager;
        private readonly SignInManager<User> signInManager;
        private readonly IConfiguration config;
        private readonly IUserRepository userRepository;

        public AuthController(
          UserManager<User> _userManager,
          SignInManager<User> _signInManager,
          IConfiguration _config,
          IUserRepository _userRepository)
        {
            userManager = _userManager;
            signInManager = _signInManager;
            config = _config;
            userRepository = _userRepository;
        }

        /// <summary>
        /// Authenticates a user based on the provided login credentials. This method first validates the incoming data. If the data is invalid, it responds with a 403 status code. Then attempts to find the user by email. If the user is not found, or if the password does not match, it returns a 401 status code indicating wrong email or password. Upon successful authentication, it generates a token, stores it in a secure, HTTP-only cookie, and returns the token with a 200 status code. In case of any server error during the process, a 500 status code with the error message is returned.
        /// </summary>
        /// <param name="userLogin">The login credentials of the user, including email and password.</param>
        /// <returns>Returns an HTTP response depending on the outcome of the authentication process.</returns>

        [AllowAnonymous]
        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] CreateLoginRequest userLogin)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return StatusCode(403, "Not correct data!");
                }

                var user = await userManager.FindByEmailAsync(userLogin.Email);

                if (user == null)
                {
                    return StatusCode(401, "Wrong email or password!");
                }

                if (user != null)
                {
                    var result = await signInManager.PasswordSignInAsync(user, userLogin.Password, false, false);

                    if (result.Succeeded)
                    {
                        var token = GenerateToken(userLogin);
                        this.HttpContext.Response.Cookies.Append($"{user.Id}", $"{token}", new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = true
                        });
                        return StatusCode(200, $"{token}");
                    }
                }

                return StatusCode(401, "Wrong email or password!");
            }
            catch (Exception error)
            {
                return StatusCode(500, error.Message);
            }
        }

        /// <summary>
        /// Registers a new user with the provided registration details. It first validates the incoming data and returns a 403 status code if the data is incorrect. It checks if the email is already in use, returning a 409 status code if it is. If the email is not in use, it creates a new user and attempts to register them. Upon successful registration, the user is automatically signed in, a token is generated, stored in a secure, HTTP-only cookie, and returned with a 200 status code. If the registration fails due to invalid input data, a 401 status code is returned. Any server errors during the process result in a 500 status code with the error message.
        /// </summary>
        /// <param name="userRegister">The registration details of the user, including first name, last name, email, and password.</param>
        /// <returns>Returns an HTTP response based on the outcome of the registration process.</returns>

        [AllowAnonymous]
        [HttpPost("Register")]
        public async Task<IActionResult> Register([FromBody] CreateRegisterRequest userRegister)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return StatusCode(403, "Not correct data!");
                }

                var users = await userRepository.GetAllAsync();
                var findUser = users.FirstOrDefault(x => x.Email == userRegister.Email);

                if (findUser != null)
                {
                    return StatusCode(409, "Email is already in use!");
                }

                var user = new User()
                {
                    FirstName = userRegister.FirstName,
                    LastName = userRegister.LastName,
                    Email = userRegister.Email,
                    UserName = userRegister.Email
                };

                var result = await userManager.CreateAsync(user, userRegister.Password);

                if (result.Succeeded)
                {
                    await signInManager.PasswordSignInAsync(user, userRegister.Password, false, false);
                    var token = GenerateToken(new CreateLoginRequest { Email = userRegister.Email, Password = userRegister.Password });
                    this.HttpContext.Response.Cookies.Append($"{user.Id}", $"{token}", new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true
                    });
                    return StatusCode(200, $"{token}");
                }
                return StatusCode(401, "Wrong input data!");
            }
            catch (Exception error)
            {
                return StatusCode(500, error.Message);
            }
        }

        /// <summary>
        /// Generates a JWT (JSON Web Token) for a user based on their login credentials. The token includes a single claim, which is the user's email. The issuer and audience for the token are also retrieved from the application's configuration. The token is set to expire in 30 minutes. This method returns the serialized form of the JWT.
        /// </summary>
        /// <param name="user">The user's login credentials, primarily used here for the user's email.</param>
        /// <returns>A string representing the serialized JWT.</returns>
        private string GenerateToken(CreateLoginRequest user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
            var claims = new[]
            {
                new Claim(ClaimTypes.Name,user.Email)
            };
            var token = new JwtSecurityToken(config["Jwt:Issuer"],
                config["Jwt:Audience"],
                claims,
                expires: DateTime.Now.AddMinutes(30),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
