using System;
using System.Collections.Generic;

namespace UavPms.Shared.Contracts.Constants;

/// <summary>
/// Shared business error codes that must be mapped to HTTP 409 Conflict across all services.
/// </summary>
public static class ConflictErrorCodes
{
    private static readonly HashSet<string> Codes = new(StringComparer.Ordinal)
    {
        "INVALID_MISSION_STATE",
        "RESOURCE_BOOKING_CONFLICT",
        "MISSION_CONCURRENCY_CONFLICT",
        "CONCURRENCY_CONFLICT",
        "ASSESSMENT_CONCURRENCY_CONFLICT",
        "IDEMPOTENCY_CONFLICT",
        "IDEMPOTENCY_KEY_REUSED",
        "DUPLICATE_MEDIA",
        "DUPLICATE_REQUEST",
        "ALERT_ALREADY_ACTIVE",
        "ALERT_ALREADY_RESOLVED",
        "EMAIL_ALREADY_EXISTS",
        "DUPLICATE_ASSIGNMENT",
        "ASSIGNEE_ALREADY_ASSIGNED",
        "DUPLICATE_CHECK_IN",
        "DUPLICATE_RESOURCE",
        "DRONE_IN_USE",
        "DRONE_NOT_READY",
        "DRONE_REGION_MISMATCH",
        "FC_ALREADY_PAIRED",
        "REQUIRED_ACCEPTANCE_PENDING"
    };

    public static bool IsConflict(string? code)
        => !string.IsNullOrWhiteSpace(code) && Codes.Contains(code);
}

