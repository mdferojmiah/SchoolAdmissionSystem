using Microsoft.EntityFrameworkCore;

namespace SchoolAdmission.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
}
