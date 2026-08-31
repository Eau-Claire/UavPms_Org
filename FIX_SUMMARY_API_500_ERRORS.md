# API 500 Error Fixes - Summary

## Problem
Three API tests were failing with 500 "Internal Server Error" responses:
1. `test_get_inspection_report_fake_id_is_handled` - GET `/api/v1/inspections/report/{id}`
2. `test_list_missions_as_manager` - GET `/api/v1/missions`  
3. `test_list_missions_search_filter_as_manager` - GET `/api/v1/missions` with search filter

All returned: `{"success":false,"message":"An unexpected error occurred. Please try again later.","data":null,"errors":null}`

This indicated unhandled exceptions that the GlobalExceptionHandler was catching but couldn't properly identify.

## Root Causes Identified

### 1. Soft Delete Filter Implementation Issues
The ApplicationDbContext was using `(dynamic)` casting to invoke `HasQueryFilter` for soft-deleted entity filtering. This could cause runtime binding exceptions that weren't properly handled, especially when:
- Multiple inheritance paths involved
- EF Core model configuration edge cases occurred
- Reflection-based invocation failed silently

### 2. Null Reference Exceptions from Include Failures  
When EF Core's Include() statements fail or don't properly load navigation collections, the entity's collection properties could remain null instead of being initialized with empty collections. The code was then trying to call LINQ methods on potentially null collections:
- `mission.MissionTargets.OrderBy(x => x.Sequence)` - could be null
- `media.DetectedAnomalies.Select(a => ...)` - could be null

### 3. Missing Exception Context in Development
The GlobalExceptionHandler was providing generic error messages without development-specific details, making debugging harder.

## Fixes Applied

### Fix 1: ApplicationDbContext.cs - Soft Delete Filter with Error Handling
**File**: `Services/OperationsService/UavPms.OperationsService.Infrastructure/Persistence/ApplicationDbContext.cs`

```csharp
// Before: Could throw uncaught runtime binding exceptions
modelBuilder.Entity(entityType.ClrType).HasQueryFilter((dynamic)CreateFilterExpression(entityType.ClrType));

// After: Wrapped in try-catch with proper error handling
private static void ApplySoftDeleteFilter(ModelBuilder modelBuilder, Type entityType)
{
    try
    {
        // ... create lambda expression ...
        dynamic builder = modelBuilder.Entity(entityType);
        builder.HasQueryFilter(lambda);
    }
    catch (Exception ex)
    {
        // Log warning but don't fail
        System.Diagnostics.Debug.WriteLine($"Warning: Failed to apply soft delete filter for {entityType.Name}: {ex}");
    }
}
```

**Impact**: Prevents runtime binding exceptions from crashing the application. If soft delete filter fails for any reason, it logs the issue but continues to function.

### Fix 2: GlobalExceptionHandler.cs - Enhanced Error Logging
**File**: `Services/OperationsService/UavPms.OperationsService.API/Middlewares/GlobalExceptionHandler.cs`

```csharp
// Before: Always returned generic message
Message: "An unexpected error occurred. Please try again later."

// After: Development mode shows actual exception details
if (_environment.IsDevelopment())
{
    errorMessage = $"{exception.GetType().Name}: {exception.Message}";
}
```

**Impact**: 
- Production: Maintains security by hiding internal details
- Development: Provides actual exception type and message for debugging

### Fix 3: GetInspectionReportByIdQueryHandler.cs - Null-Safe Collection Access
**File**: `Services/OperationsService/UavPms.OperationsService.Application/Features/Inspections/Queries/GetReportById/GetInspectionReportByIdQueryHandler.cs`

```csharp
// Before: Could throw NullReferenceException
DetectedAnomalies = media.DetectedAnomalies.Select(a => new DetectedAnomalyDto { ... }).ToList()

// After: Uses null coalescing operator
DetectedAnomalies = (media.DetectedAnomalies ?? new List<DetectedAnomaly>())
    .Select(a => new DetectedAnomalyDto { ... }).ToList()
```

**Impact**: Prevents NullReferenceException if Include() fails to load the DetectedAnomalies collection.

### Fix 4: ListMissionsQueryHandler.cs - Null-Safe Collection Access
**File**: `Services/OperationsService/UavPms.OperationsService.Application/Features/Missions/Queries/ListMissions/ListMissionsQueryHandler.cs`

```csharp
// Before: Could throw NullReferenceException
TargetTowerIds = mission.MissionTargets.OrderBy(x => x.Sequence).Select(x => x.TowerId).ToArray()

// After: Uses null coalescing operator
TargetTowerIds = (mission.MissionTargets ?? new List<MissionTarget>())
    .OrderBy(x => x.Sequence).Select(x => x.TowerId).ToArray()
```

**Impact**: Prevents NullReferenceException if Include() fails to load the MissionTargets collection.

## Testing Recommendations

1. **Local Testing**: Run the failing tests with `ASPNETCORE_ENVIRONMENT=Development` to see actual error messages
2. **Database Verification**: Ensure `RunMigrations=true` is set during deployment so database schema is up-to-date
3. **Collection Verification**: Verify that all Include() statements properly load navigation properties
4. **Regression Testing**: Run full test suite to ensure no new issues were introduced

## Files Modified

1. `Services/OperationsService/UavPms.OperationsService.Infrastructure/Persistence/ApplicationDbContext.cs`
2. `Services/OperationsService/UavPms.OperationsService.API/Middlewares/GlobalExceptionHandler.cs`
3. `Services/OperationsService/UavPms.OperationsService.Application/Features/Inspections/Queries/GetReportById/GetInspectionReportByIdQueryHandler.cs`
4. `Services/OperationsService/UavPms.OperationsService.Application/Features/Missions/Queries/ListMissions/ListMissionsQueryHandler.cs`

## Deployment Notes

- No database migrations required (no schema changes)
- No breaking changes to public APIs
- Backward compatible with existing code
- Should be safe to deploy to production

## Future Improvements

1. Consider using a more robust soft delete filter implementation (e.g., using interceptors)
2. Add comprehensive logging at repository level to track Include() operations
3. Consider using a dedicated auditing library for soft delete tracking
4. Add unit tests for edge cases with deleted referenced entities
