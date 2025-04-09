using Microsoft.EntityFrameworkCore;
using VideoPlayWebApplication.Models;

namespace VideoPlayWebApplication.Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<Video> Videos { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
    }
}
