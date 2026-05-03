using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SecureERP2.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly ERPDbContext _context;

        public UsersController(ERPDbContext context)
        {
            _context = context;
        }

        // 🌐 PRODUCTION-SAFE SAAS: Create user within current tenant
        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequestWithRoleId request)
        {
            try
            {
                // 🌐 Get current user's CompanyId from JWT
                var currentCompanyId = GetCurrentCompanyId();
                if (!currentCompanyId.HasValue)
                {
                    return BadRequest(new { error = "CompanyId not found in JWT token" });
                }

                // Validate username uniqueness within company
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == request.Username && u.CompanyId == currentCompanyId.Value);

                if (existingUser != null)
                {
                    return BadRequest(new { error = "Username already exists in this company" });
                }

                // Hash password
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

                var user = new User
                {
                    Username = request.Username,
                    PasswordHash = passwordHash,
                    Email = request.Email,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    PhoneNumber = request.PhoneNumber,
                    Department = request.Department,
                    CompanyId = currentCompanyId.Value, // 🌐 PRODUCTION-SAFE: Auto-assign from current user
                    RoleId = request.RoleId, // Use RoleId from request
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetUsers), new { id = user.Id }, new { 
                    userId = user.Id,
                    username = user.Username,
                    companyId = user.CompanyId,
                    message = "User created successfully in your company"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to create user", details = ex.Message });
            }
        }

        // Get users for current company only
        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            try
            {
                var currentCompanyId = GetCurrentCompanyId();
                if (!currentCompanyId.HasValue)
                {
                    return BadRequest(new { error = "CompanyId not found in JWT token" });
                }

                var users = await _context.Users
                    .Where(u => u.CompanyId == currentCompanyId.Value)
                    .Select(u => new {
                        u.Id,
                        u.Username,
                        u.Email,
                        u.FirstName,
                        u.LastName,
                        u.IsActive,
                        u.CreatedAt,
                        u.LastLoginAt
                    })
                    .ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve users", details = ex.Message });
            }
        }

        // 🌐 Get current CompanyId from JWT token
        private int? GetCurrentCompanyId()
        {
            if (User.Claims.FirstOrDefault(c => c.Type == "CompanyId")?.Value is string companyIdStr)
            {
                if (int.TryParse(companyIdStr, out int companyId))
                {
                    return companyId;
                }
            }
            return null;
        }
    }
}
