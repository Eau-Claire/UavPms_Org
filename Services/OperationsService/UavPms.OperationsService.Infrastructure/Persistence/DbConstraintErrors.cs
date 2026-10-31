using Microsoft.EntityFrameworkCore;
using Npgsql;
using UavPms.OperationsService.Application.Common.Exceptions;

namespace UavPms.OperationsService.Infrastructure.Persistence;

/// <summary>
/// Turns PostgreSQL constraint violations raised on save into business errors, so a race lost to a database
/// constraint returns 409 with a code instead of 500.
/// </summary>
public static class DbConstraintErrors
{
    public const string ExclusionViolation = "23P01";
    public const string UniqueViolation = "23505";
    public const string DeadlockDetected = "40P01";
    public const string SerializationFailure = "40001";

    public static PostgresException? FindPostgresException(Exception? exception)
    {
        for (var e = exception; e != null; e = e.InnerException)
        {
            if (e is PostgresException pg) return pg;
        }
        return null;
    }

    public static bool IsUniqueViolation(DbUpdateException exception, string constraintFragment) =>
        FindPostgresException(exception) is { SqlState: UniqueViolation } pg
        && (pg.ConstraintName ?? string.Empty).Contains(constraintFragment, StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns the business error for a known constraint violation, or null.</summary>
    public static BusinessRuleException? Translate(DbUpdateException exception)
    {
        if (exception is DbUpdateConcurrencyException) return null;
        var pg = FindPostgresException(exception);
        if (pg == null) return null;

        var constraint = pg.ConstraintName ?? string.Empty;
        var table = pg.TableName ?? string.Empty;

        if (pg.SqlState == ExclusionViolation)
        {
            var resource = constraint.Contains("drone", StringComparison.OrdinalIgnoreCase) ? "UAV" : "person";
            return new BusinessRuleException("RESOURCE_BOOKING_CONFLICT",
                $"The {resource} already has an Active booking that overlaps this window.", exception);
        }

        if (pg.SqlState is DeadlockDetected or SerializationFailure)
            return new BusinessRuleException("CONCURRENCY_CONFLICT", "A concurrent change was made; please retry.", exception);

        if (pg.SqlState != UniqueViolation) return null;

        bool Is(string fragment) =>
            constraint.Contains(fragment, StringComparison.OrdinalIgnoreCase) || table.Equals(fragment, StringComparison.OrdinalIgnoreCase);

        if (constraint.Contains("IdempotencyKey", StringComparison.OrdinalIgnoreCase))
            return new BusinessRuleException("IDEMPOTENCY_KEY_REUSED", "This idempotency key was already used.", exception);
        if (constraint.Contains("PreMissionAssessmentId", StringComparison.OrdinalIgnoreCase))
            return new BusinessRuleException("ASSESSMENT_ALREADY_COMPLETED", "A mission was already created from this assessment.", exception);
        if (Is("MissionAssignments"))
            return new BusinessRuleException("DUPLICATE_ASSIGNMENT", "The user already has an active assignment in this mission.", exception);
        if (Is("MissionCheckIns"))
            return new BusinessRuleException("DUPLICATE_CHECK_IN", "The user has already checked in.", exception);
        if (Is("DroneHandovers"))
            return new BusinessRuleException("DUPLICATE_HANDOVER", "The UAV already has an open handover.", exception);

        return new BusinessRuleException("DUPLICATE_RESOURCE", "The record already exists.", exception);
    }
}
