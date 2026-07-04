using Memesploding.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memesploding.Api.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260517001000_EnsureEmailPasswordAuthSchema")]
    public partial class EnsureEmailPasswordAuthSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE users
                ADD COLUMN IF NOT EXISTS "PasswordHash" character varying(500);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE users
                DROP COLUMN IF EXISTS "PasswordHash";
                """);
        }
    }
}
