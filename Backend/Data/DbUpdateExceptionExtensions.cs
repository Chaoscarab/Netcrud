using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Backend.Data;

public static class DbUpdateExceptionExtensions
{
    private const string UniqueViolation = "23505";

    public static bool IsUniqueViolation(this DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: UniqueViolation };
}
