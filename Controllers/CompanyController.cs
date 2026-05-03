using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SecureERP2.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CompanyController : ControllerBase
    {
        private readonly ERPDbContext _context;

        public CompanyController(ERPDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult GetCompanies()
        {
            return Ok(_context.Companies.ToList());
        }

        [HttpPost]
        public async Task<IActionResult> CreateCompany([FromBody] CreateCompanyRequest request)
        {
            try
            {
                // Validate company code uniqueness
                var existingCompany = await _context.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == request.CompanyCode);

                if (existingCompany != null)
                {
                    return BadRequest(new { error = "Company code already exists" });
                }

                var company = new Company
                {
                    CompanyCode = request.CompanyCode,
                    CompanyName = request.CompanyName,
                    Description = request.Description,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Companies.Add(company);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetCompanies), new { id = company.Id }, company);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to create company", details = ex.Message });
            }
        }

        [HttpPost("{companyId}/users")]
        public async Task<IActionResult> CreateUserForCompany(int companyId, [FromBody] CreateUserRequest request)
        {
            try
            {
                // Verify company exists
                var company = await _context.Companies.FindAsync(companyId);
                if (company == null)
                {
                    return NotFound(new { error = "Company not found" });
                }

                // Validate username uniqueness
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == request.Username);

                if (existingUser != null)
                {
                    return BadRequest(new { error = "Username already exists" });
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
                    CompanyId = companyId,
                    RoleId = 1, // Default to Admin role (assuming RoleId 1 = Admin)
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetCompanies), new { id = user.Id }, new { 
                    userId = user.Id,
                    username = user.Username,
                    companyId = user.CompanyId,
                    companyName = company.CompanyName
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to create user", details = ex.Message });
            }
        }
    }

    public class CreateCompanyRequest
    {
        public string CompanyCode { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class CreateUserRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
    }
}
