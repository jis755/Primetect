using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrimeTect.Api.Contracts;
using PrimeTect.Api.Security;
using PrimeTect.Domain.Entities;
using PrimeTect.Domain.Enums;
using PrimeTect.Infrastructure.OpeningReadings;
using PrimeTect.Infrastructure.Persistence;

namespace PrimeTect.Api.Endpoints;

/// <summary>
/// Admin-only, property-scoped opening electricity reading import. Admins download a CSV or XLSX template keyed by
/// stable IDs, upload it for validation and a non-mutating preview, then confirm an atomic commit. The preview is a
/// signed, in-memory token so previewing writes no application records; the commit revalidates authorization,
/// eligibility, and versions, and applies every row in a single transaction (all or nothing).
/// </summary>
public static class OpeningReadingImportEndpoints
{
    private const string ProtectorPurpose = "PrimeTect.OpeningReadingImport.v1";
    private const string CsvContentType = "text/csv; charset=utf-8";
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string ElectricityMeterType = "Electricity";

    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapOpeningReadingImportEndpoints(this WebApplication app)
    {
        var secure = app.MapGroup("/api").RequireAuthorization();

        secure.MapGet("/properties/{propertyId:guid}/meter-setup/opening-readings/template.csv", (Delegate)DownloadTemplateCsvAsync)
            .RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));
        secure.MapGet("/properties/{propertyId:guid}/meter-setup/opening-readings/template.xlsx", (Delegate)DownloadTemplateXlsxAsync)
            .RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));
        secure.MapPost("/properties/{propertyId:guid}/meter-setup/opening-readings/preview", (Delegate)PreviewOpeningReadingsAsync)
            .RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));
        secure.MapPost("/properties/{propertyId:guid}/meter-setup/opening-readings/commit", (Delegate)CommitOpeningReadingsAsync)
            .RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));
    }

    private static async Task<IResult> DownloadTemplateCsvAsync(Guid propertyId, HttpContext httpContext, PropertyManagementDbContext db, CancellationToken cancellationToken)
    {
        var (context, error) = await BuildTemplateContextAsync(propertyId, httpContext, db, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var bytes = OpeningReadingTemplateBuilder.BuildCsv(context!);
        return Results.File(bytes, CsvContentType, TemplateFileName(context!, "csv"));
    }

    private static async Task<IResult> DownloadTemplateXlsxAsync(Guid propertyId, HttpContext httpContext, PropertyManagementDbContext db, CancellationToken cancellationToken)
    {
        var (context, error) = await BuildTemplateContextAsync(propertyId, httpContext, db, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var bytes = OpeningReadingTemplateBuilder.BuildXlsx(context!);
        return Results.File(bytes, XlsxContentType, TemplateFileName(context!, "xlsx"));
    }

    private static async Task<IResult> PreviewOpeningReadingsAsync(
        Guid propertyId,
        IFormFile? file,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        IDataProtectionProvider dataProtectionProvider,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest("Choose a CSV or XLSX template file to preview.");
        }

        if (file.Length > OpeningReadingImportLimits.MaxFileBytes)
        {
            return BadRequest($"The file is larger than the {OpeningReadingImportLimits.MaxFileBytes / (1024 * 1024)} MB limit.");
        }

        var property = await db.Properties.AsNoTracking().FirstOrDefaultAsync(item => item.Id == propertyId && item.OrganizationId == actor.OrganizationId, cancellationToken);
        if (property is null)
        {
            return Results.NotFound();
        }

        var content = await ReadUploadAsync(file, cancellationToken);
        if (content.Length > OpeningReadingImportLimits.MaxFileBytes)
        {
            return BadRequest($"The file is larger than the {OpeningReadingImportLimits.MaxFileBytes / (1024 * 1024)} MB limit.");
        }

        var fileName = SanitizeFileName(file.FileName);
        var parsed = OpeningReadingFileParser.Parse(content, fileName);
        if (parsed.FatalError is not null)
        {
            return BadRequest(parsed.FatalError);
        }

        if (parsed.Rows.Count == 0)
        {
            return BadRequest("The file has no opening-reading rows to preview.");
        }

        var evaluation = await LoadEvaluationContextAsync(db, actor, property, cancellationToken);
        var duplicateUnits = DuplicateUnitIds(parsed.Rows);
        var evaluated = parsed.Rows.Select(row => EvaluateRow(row, propertyId, evaluation, duplicateUnits)).ToList();

        var ready = evaluated.Where(row => row.Status == "Ready").ToList();
        var skipped = evaluated.Count(row => row.Status == "Skipped");
        var conflicts = evaluated.Count(row => row.Status == "Conflict");
        var errorCount = evaluated.Count(row => row.Status == "Error");
        var canCommit = ready.Count > 0 && conflicts == 0 && errorCount == 0;

        var fileHash = Hash(content);
        var previewToken = string.Empty;
        var expiresAt = DateTimeOffset.UtcNow.Add(OpeningReadingImportLimits.PreviewLifetime);

        if (canCommit)
        {
            var payload = new OpeningReadingPreviewPayload(
                OpeningReadingImportLimits.TemplateVersion,
                property.Id,
                property.Name,
                fileName,
                fileHash,
                ComputeContentHash(propertyId, parsed.Rows),
                DateTimeOffset.UtcNow,
                expiresAt,
                parsed.Rows.Count,
                ready.Select(row => new OpeningReadingPreviewPayloadRow(
                    row.Row.RowNumber,
                    row.UnitId,
                    row.UnitName,
                    row.InstallationId,
                    row.InstallationVersion,
                    row.OpeningReading!.Value,
                    row.ReadingDate!,
                    row.FirstBillingMonth,
                    row.UsesLegacyAdoption)).ToList());

            var protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
            previewToken = protector.Protect(JsonSerializer.Serialize(payload, PayloadJsonOptions));
        }

        var message = canCommit
            ? $"Validated {ready.Count} opening baseline row(s). Nothing has been imported yet; review and confirm to apply them in one transaction."
            : errorCount > 0
                ? "Some rows are invalid. Fix the file and preview again; nothing has been imported."
                : conflicts > 0
                    ? "Some rows conflict with existing readings or billing history. Nothing has been imported."
                    : "No opening readings were provided. Enter opening_reading for at least one row and preview again.";

        var rows = evaluated.Select(row => new OpeningReadingPreviewRow(
            row.Row.RowNumber,
            row.UnitId == Guid.Empty ? row.Row.UnitId : row.UnitId.ToString(),
            row.UnitName,
            row.InstallationId?.ToString(),
            row.Status,
            row.OpeningReading,
            row.ReadingDate,
            row.FirstBillingMonth,
            row.UsesLegacyAdoption,
            row.Errors,
            row.Notes)).ToList();

        return Results.Ok(new OpeningReadingPreviewResponse(
            propertyId,
            property.Name,
            fileName,
            fileHash,
            parsed.Rows.Count,
            ready.Count,
            skipped,
            conflicts,
            errorCount,
            canCommit,
            previewToken,
            expiresAt,
            message,
            rows));
    }

    private static async Task<IResult> CommitOpeningReadingsAsync(
        Guid propertyId,
        [FromForm] string? previewToken,
        [FromForm] Guid submissionKey,
        [FromForm] string? fileHash,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        IDataProtectionProvider dataProtectionProvider,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(previewToken))
        {
            return BadRequest("Preview the file before importing.");
        }

        if (submissionKey == Guid.Empty)
        {
            return BadRequest("A submission key is required to guard against duplicate imports.");
        }

        var property = await db.Properties.AsNoTracking().FirstOrDefaultAsync(item => item.Id == propertyId && item.OrganizationId == actor.OrganizationId, cancellationToken);
        if (property is null)
        {
            return Results.NotFound();
        }

        OpeningReadingPreviewPayload? payload;
        try
        {
            var protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
            payload = JsonSerializer.Deserialize<OpeningReadingPreviewPayload>(protector.Unprotect(previewToken), PayloadJsonOptions);
        }
        catch (Exception)
        {
            return BadRequest("The preview token is invalid or was tampered with. Preview the file again.");
        }

        if (payload is null || payload.PropertyId != propertyId)
        {
            return BadRequest("The preview does not belong to this property. Preview the file again.");
        }

        if (!string.IsNullOrWhiteSpace(fileHash) && !string.Equals(fileHash, payload.FileHash, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Conflict(new { message = "The file changed since it was previewed. Preview the current file again." });
        }

        if (payload.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return Results.Conflict(new { message = "This preview has expired. Preview the file again before importing." });
        }

        var existing = await db.OpeningReadingImports.AsNoTracking().FirstOrDefaultAsync(item => item.OrganizationId == actor.OrganizationId && item.SubmissionKey == submissionKey, cancellationToken);
        if (existing is not null)
        {
            return Results.Ok(BuildIdempotentResponse(payload, existing, propertyId));
        }

        if (payload.Rows.Count == 0)
        {
            return BadRequest("The preview has no opening readings to import.");
        }

        var evaluation = await LoadEvaluationContextAsync(db, actor, property, cancellationToken);
        var staleReasons = ValidatePayloadAgainstCurrentState(payload, evaluation);
        if (staleReasons.Count > 0)
        {
            return Results.Conflict(new
            {
                message = "This preview is stale because the property changed since it was validated. Preview the file again. Nothing has been imported.",
                reasons = staleReasons.Distinct(StringComparer.Ordinal).Take(10).ToArray(),
            });
        }

        var resultRows = new List<OpeningReadingImportResultRow>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var row in payload.Rows)
            {
                if (!OpeningReadingValues.TryParseDate(row.ReadingDate, out var readingDate))
                {
                    throw new InvalidOperationException("The preview contained an invalid reading date.");
                }

                if (row.InstallationId is { } installationId)
                {
                    var installation = await db.MeterInstallations.FirstOrDefaultAsync(item => item.Id == installationId && item.OrganizationId == actor.OrganizationId && item.UnitId == row.UnitId, cancellationToken)
                        ?? throw new InvalidOperationException("The submeter disappeared before the import was applied.");
                    if (installation.OpeningReading is not null)
                    {
                        throw new InvalidOperationException("The submeter gained an opening baseline before the import was applied.");
                    }

                    installation.SetOpeningBaseline(row.OpeningReading, readingDate);
                    db.MeterInstallationAuditEntries.Add(new MeterInstallationAuditEntry(
                        actor.OrganizationId, propertyId, row.UnitId, installation.Id, null, actor.UserId,
                        MeterInstallationAuditAction.OpeningBaseline, null, "Opening reading imported from template.", null,
                        JsonSerializer.Serialize(new { installation.OpeningReading, installation.OpeningReadingDate, Source = payload.FileName }), Guid.NewGuid()));
                    resultRows.Add(new OpeningReadingImportResultRow(row.RowNumber, row.UnitId.ToString(), row.UnitName, row.OpeningReading, row.ReadingDate, "Opening baseline recorded on the existing submeter."));
                }
                else
                {
                    var installation = new MeterInstallation(actor.OrganizationId, propertyId, row.UnitId, null, null, null, null, row.OpeningReading, readingDate, null, false, actor.UserId);
                    db.MeterInstallations.Add(installation);
                    db.MeterInstallationAuditEntries.Add(new MeterInstallationAuditEntry(
                        actor.OrganizationId, propertyId, row.UnitId, installation.Id, null, actor.UserId,
                        MeterInstallationAuditAction.Adopted, null, "Legacy submeter adopted to hold an imported opening baseline.", null,
                        JsonSerializer.Serialize(new { installation.OpeningReading, installation.OpeningReadingDate, installation.IdentityKnown, Source = payload.FileName }), Guid.NewGuid()));
                    resultRows.Add(new OpeningReadingImportResultRow(row.RowNumber, row.UnitId.ToString(), row.UnitName, row.OpeningReading, row.ReadingDate, "Adopted legacy submeter created with the opening baseline."));
                }
            }

            var record = new OpeningReadingImport(
                actor.OrganizationId, propertyId, actor.UserId, payload.FileName, payload.FileHash, payload.ContentHash,
                payload.FileRowCount, resultRows.Count, "Imported", submissionKey);
            db.OpeningReadingImports.Add(record);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            var prior = await db.OpeningReadingImports.AsNoTracking().FirstOrDefaultAsync(item => item.OrganizationId == actor.OrganizationId && item.SubmissionKey == submissionKey, cancellationToken);
            return prior is not null
                ? Results.Ok(BuildIdempotentResponse(payload, prior, propertyId))
                : Results.Conflict(new { message = "The import could not be completed because the property changed. Nothing has been imported." });
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Problem(detail: "The import failed and was rolled back. No opening readings were changed.", statusCode: StatusCodes.Status500InternalServerError);
        }

        return Results.Ok(new OpeningReadingCommitResponse(
            propertyId, payload.FileName, payload.FileRowCount, resultRows.Count, "Imported", false, DateTimeOffset.UtcNow,
            $"Imported {resultRows.Count} opening baseline(s) in one transaction.", resultRows));
    }

    private static IReadOnlyList<string> ValidatePayloadAgainstCurrentState(OpeningReadingPreviewPayload payload, EvaluationContext evaluation)
    {
        var reasons = new List<string>();
        foreach (var row in payload.Rows)
        {
            if (!evaluation.Units.TryGetValue(row.UnitId, out var unit) || !unit.IsEnabled || unit.IsArchived)
            {
                reasons.Add($"Unit '{row.UnitName}' is no longer an enabled unit in this property.");
                continue;
            }

            var hasInstallation = evaluation.Installations.TryGetValue(row.UnitId, out var active);
            if (row.InstallationId is { } installationId)
            {
                if (!hasInstallation || active!.Id != installationId)
                {
                    reasons.Add($"The submeter for '{row.UnitName}' changed since the preview.");
                    continue;
                }

                if (active!.Version != row.InstallationVersion)
                {
                    reasons.Add($"The submeter for '{row.UnitName}' changed since the preview.");
                    continue;
                }

                if (active.OpeningReading is not null)
                {
                    reasons.Add($"'{row.UnitName}' now has an opening baseline.");
                    continue;
                }

                if (active.ReplacedInstallationId is not null)
                {
                    reasons.Add($"The submeter for '{row.UnitName}' came from a replacement.");
                    continue;
                }
            }
            else
            {
                if (hasInstallation)
                {
                    reasons.Add($"'{row.UnitName}' now has an active submeter, so a legacy adoption is no longer valid.");
                    continue;
                }

                if (!row.UsesLegacyAdoption)
                {
                    reasons.Add($"The preview for '{row.UnitName}' is no longer valid.");
                    continue;
                }
            }

            if (OpeningReadingValues.TryParseMonth(row.FirstBillingMonth, out var firstMonth) && evaluation.CycleMonths.Any(month => month >= firstMonth))
            {
                reasons.Add($"A billing period now exists for {row.FirstBillingMonth} or later.");
            }
        }

        return reasons;
    }

    private static OpeningReadingCommitResponse BuildIdempotentResponse(OpeningReadingPreviewPayload payload, OpeningReadingImport record, Guid propertyId)
    {
        var rows = payload.Rows
            .Select(row => new OpeningReadingImportResultRow(row.RowNumber, row.UnitId.ToString(), row.UnitName, row.OpeningReading, row.ReadingDate, "Already imported."))
            .ToList();
        return new OpeningReadingCommitResponse(
            propertyId, record.FileName, record.RowCount, record.ImportedCount, record.Outcome, true, record.OccurredAtUtc,
            "This import was already applied. No new changes were made.", rows);
    }

    private static async Task<(OpeningReadingTemplateContext? Context, IResult? Error)> BuildTemplateContextAsync(Guid propertyId, HttpContext httpContext, PropertyManagementDbContext db, CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return (null, Results.Unauthorized());
        }

        var property = await db.Properties.AsNoTracking().FirstOrDefaultAsync(item => item.Id == propertyId && item.OrganizationId == actor.OrganizationId, cancellationToken);
        if (property is null)
        {
            return (null, Results.NotFound());
        }

        var evaluation = await LoadEvaluationContextAsync(db, actor, property, cancellationToken);
        var month = evaluation.DefaultMonth;
        var hasLaterCycle = evaluation.CycleMonths.Any(cycleMonth => cycleMonth >= month);
        var rows = new List<OpeningReadingTemplateRow>();

        if (!hasLaterCycle)
        {
            foreach (var unit in evaluation.Units.Values.Where(unit => unit.IsEnabled && !unit.IsArchived).OrderBy(unit => unit.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (rows.Count >= OpeningReadingImportLimits.MaxRows)
                {
                    break;
                }

                var hasInstallation = evaluation.Installations.TryGetValue(unit.Id, out var installation);
                var configured = ResolveElectricitySetting(evaluation.Configurations, unit.Id, month) is not null;
                if (!hasInstallation && !configured)
                {
                    continue;
                }

                if (hasInstallation && (installation!.OpeningReading is not null || installation.ReplacedInstallationId is not null))
                {
                    continue;
                }

                rows.Add(new OpeningReadingTemplateRow(
                    OpeningReadingImportLimits.TemplateVersion,
                    property.Id,
                    property.Name,
                    unit.Id,
                    unit.Name,
                    hasInstallation ? installation!.Id : null,
                    ElectricityMeterType,
                    string.Empty,
                    string.Empty,
                    OpeningReadingValues.FormatMonth(month)));
            }
        }

        return (new OpeningReadingTemplateContext(OpeningReadingImportLimits.TemplateVersion, property.Id, property.Name, OpeningReadingValues.FormatMonth(month), rows), null);
    }

    private static async Task<EvaluationContext> LoadEvaluationContextAsync(PropertyManagementDbContext db, RequestUser actor, Property property, CancellationToken cancellationToken)
    {
        var units = await db.Units.AsNoTracking()
            .Where(unit => unit.OrganizationId == actor.OrganizationId && unit.PropertyId == property.Id)
            .ToListAsync(cancellationToken);
        var installations = await db.MeterInstallations.AsNoTracking()
            .Where(installation => installation.OrganizationId == actor.OrganizationId && installation.PropertyId == property.Id && installation.EndedOn == null)
            .ToListAsync(cancellationToken);
        var configurations = await db.ChargeConfigurations.AsNoTracking()
            .Where(configuration => configuration.OrganizationId == actor.OrganizationId && configuration.PropertyId == property.Id)
            .ToListAsync(cancellationToken);
        var cycleMonths = await db.BillingCycles.AsNoTracking()
            .Where(cycle => cycle.OrganizationId == actor.OrganizationId && cycle.PropertyId == property.Id)
            .Select(cycle => cycle.BillingMonth)
            .ToListAsync(cancellationToken);

        var localDate = PropertyLocalDate(property.TimeZoneId);
        var defaultMonth = new DateOnly(localDate.Year, localDate.Month, 1);

        return new EvaluationContext(
            defaultMonth,
            units.ToDictionary(unit => unit.Id),
            installations.ToDictionary(installation => installation.UnitId),
            configurations,
            cycleMonths);
    }

    private static EvaluatedRow EvaluateRow(ParsedOpeningReadingRow row, Guid propertyId, EvaluationContext evaluation, HashSet<Guid> duplicateUnits)
    {
        var errors = new List<string>();
        var notes = new List<string>();

        Unit? unit = null;
        var unitId = Guid.Empty;
        if (Guid.TryParse(row.UnitId, out var parsedUnitId) && evaluation.Units.TryGetValue(parsedUnitId, out var foundUnit))
        {
            unitId = parsedUnitId;
            unit = foundUnit;
        }

        var unitName = unit?.Name ?? (row.UnitName.Length > 0 ? row.UnitName : row.UnitId);

        if (row.OpeningReading.Length == 0)
        {
            return new EvaluatedRow(row, unitId, unitName, null, null, "Skipped", null, null, row.FirstBillingMonth, false, errors, notes);
        }

        if (!int.TryParse(row.TemplateVersion, out var templateVersion) || templateVersion != OpeningReadingImportLimits.TemplateVersion)
        {
            errors.Add($"Unsupported template version '{Describe(row.TemplateVersion)}'. Download a current template.");
        }

        if (!Guid.TryParse(row.PropertyId, out var rowPropertyId) || rowPropertyId != propertyId)
        {
            errors.Add("This row belongs to a different property and cannot be imported here.");
        }

        if (!string.Equals(row.MeterType, ElectricityMeterType, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Only {ElectricityMeterType} meters are supported here (found '{Describe(row.MeterType)}').");
        }

        if (unit is null)
        {
            errors.Add("The unit_id does not match a unit in this property.");
        }
        else if (!unit.IsEnabled || unit.IsArchived)
        {
            errors.Add("This unit is not an enabled unit in this property.");
        }
        else if (duplicateUnits.Contains(unitId))
        {
            errors.Add("This unit appears more than once in the file.");
        }

        var monthOk = OpeningReadingValues.TryParseMonth(row.FirstBillingMonth, out var firstMonth);
        if (!monthOk)
        {
            errors.Add("first_billing_month must use the YYYY-MM format.");
        }

        decimal? openingReading = null;
        if (!OpeningReadingValues.TryParseReading(row.OpeningReading, out var parsedReading, out var readingError))
        {
            errors.Add($"opening_reading is invalid ({readingError}); use a non-negative number with at most {OpeningReadingImportLimits.ReadingScale} decimals.");
        }
        else
        {
            openingReading = parsedReading;
        }

        DateOnly? readingDate = null;
        if (openingReading is not null)
        {
            if (row.ReadingDate.Length == 0)
            {
                errors.Add("reading_date is required when an opening reading is provided.");
            }
            else if (!OpeningReadingValues.TryParseDate(row.ReadingDate, out var parsedDate))
            {
                errors.Add("reading_date must use the YYYY-MM-DD format.");
            }
            else if (monthOk && !IsOpeningReadingDateSupported(parsedDate, firstMonth))
            {
                errors.Add("reading_date must fall within the first_billing_month or the month immediately before it. A baseline observed after that period would span a missing period.");
            }
            else
            {
                readingDate = parsedDate;
            }
        }

        Guid? installationId = null;
        long? installationVersion = null;
        var usesLegacyAdoption = false;

        if (unit is not null && unit.IsEnabled && !unit.IsArchived)
        {
            var hasInstallation = evaluation.Installations.TryGetValue(unitId, out var active);

            if (row.MeterInstallationId.Length > 0)
            {
                if (!Guid.TryParse(row.MeterInstallationId, out var claimedId))
                {
                    errors.Add("meter_installation_id is not a valid identifier.");
                }
                else if (!hasInstallation || active!.Id != claimedId)
                {
                    errors.Add("The meter_installation_id does not match the active submeter for this unit.");
                }
                else
                {
                    installationId = active!.Id;
                    installationVersion = active.Version;
                    if (active.OpeningReading is not null)
                    {
                        errors.Add("This submeter already has an opening baseline; existing baselines are never overwritten.");
                    }

                    if (active.ReplacedInstallationId is not null)
                    {
                        errors.Add("This submeter came from a replacement, so its opening reading cannot be changed here.");
                    }
                }
            }
            else if (hasInstallation)
            {
                installationId = active!.Id;
                installationVersion = active.Version;
                errors.Add("meter_installation_id is required because this unit already has an active submeter.");
                if (active.OpeningReading is not null)
                {
                    errors.Add("This submeter already has an opening baseline; existing baselines are never overwritten.");
                }

                if (active.ReplacedInstallationId is not null)
                {
                    errors.Add("This submeter came from a replacement, so its opening reading cannot be changed here.");
                }
            }
            else
            {
                usesLegacyAdoption = true;
                notes.Add("No active submeter exists for this unit; an adopted legacy submeter (identity unknown, no serial) will be created to hold the opening baseline.");
            }

            var settingMonth = monthOk ? firstMonth : evaluation.DefaultMonth;
            var electricitySetting = ResolveElectricitySetting(evaluation.Configurations, unitId, settingMonth);
            if (electricitySetting is null && !hasInstallation)
            {
                errors.Add("This unit is not set up for electricity submetering.");
            }
            else if (electricitySetting?.Amount is null)
            {
                notes.Add("No electricity rate is configured; the opening baseline will be stored, but this unit's electricity billing stays blocked until a rate is set.");
            }

            if (monthOk && evaluation.CycleMonths.Any(cycleMonth => cycleMonth >= firstMonth))
            {
                errors.Add($"A billing period already exists for {OpeningReadingValues.FormatMonth(firstMonth)} or later, so an opening baseline can no longer be recorded safely.");
            }
        }

        var status = errors.Count > 0 ? "Error" : "Ready";
        return new EvaluatedRow(
            row,
            unitId,
            unitName,
            installationId,
            installationVersion,
            status,
            openingReading,
            readingDate is null ? null : OpeningReadingValues.FormatDate(readingDate.Value),
            monthOk ? OpeningReadingValues.FormatMonth(firstMonth) : row.FirstBillingMonth,
            usesLegacyAdoption,
            errors,
            notes);
    }

    /// <summary>
    /// The opening baseline starts the first billing period, so its observation date may fall inside that period or
    /// on the immediately preceding period boundary (for example, a 30 September reading that starts the October
    /// bill, or a 1 January reading that starts the February bill). This matches the manual opening-entry workflow,
    /// which never ties the opening date to a billing month, and the documented convention that the previous reading
    /// is dated in the period immediately before the issued bill. A reading dated after the first billing period is
    /// still rejected because it would silently span one or more missing periods.
    /// </summary>
    private static bool IsOpeningReadingDateSupported(DateOnly readingDate, DateOnly firstOfMonth)
    {
        var windowStart = firstOfMonth.AddMonths(-1);
        var windowEnd = firstOfMonth.AddMonths(1).AddDays(-1);
        return readingDate >= windowStart && readingDate <= windowEnd;
    }

    private static HashSet<Guid> DuplicateUnitIds(IReadOnlyList<ParsedOpeningReadingRow> rows)
    {
        var counts = new Dictionary<Guid, int>();
        foreach (var row in rows)
        {
            if (row.OpeningReading.Length == 0 || !Guid.TryParse(row.UnitId, out var unitId))
            {
                continue;
            }

            counts[unitId] = counts.GetValueOrDefault(unitId) + 1;
        }

        return counts.Where(pair => pair.Value > 1).Select(pair => pair.Key).ToHashSet();
    }

    private static ChargeConfiguration? ResolveElectricitySetting(IReadOnlyList<ChargeConfiguration> configurations, Guid unitId, DateOnly billingMonth)
    {
        var propertySetting = configurations
            .Where(item => item.UnitId is null && item.ChargeType == ChargeType.Electricity && item.BillingMonth <= billingMonth)
            .OrderByDescending(item => item.BillingMonth)
            .FirstOrDefault();
        var unitSetting = configurations
            .Where(item => item.UnitId == unitId && item.ChargeType == ChargeType.Electricity && item.BillingMonth <= billingMonth)
            .OrderByDescending(item => item.BillingMonth)
            .FirstOrDefault();
        var selected = unitSetting?.Mode switch
        {
            ChargeSettingMode.Disabled => null,
            ChargeSettingMode.Custom => unitSetting,
            ChargeSettingMode.Inherited => propertySetting,
            _ => propertySetting,
        };

        return selected is null || selected.Mode == ChargeSettingMode.Disabled ? null : selected;
    }

    private static DateOnly PropertyLocalDate(string timeZoneId)
    {
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateOnly.FromDateTime(DateTime.UtcNow);
        }
        catch (InvalidTimeZoneException)
        {
            return DateOnly.FromDateTime(DateTime.UtcNow);
        }
    }

    private static async Task<byte[]> ReadUploadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        await using (var stream = file.OpenReadStream())
        {
            await stream.CopyToAsync(memory, cancellationToken);
        }

        return memory.ToArray();
    }

    private static string TemplateFileName(OpeningReadingTemplateContext context, string extension)
    {
        var safeProperty = SanitizeFileName(context.PropertyName).Replace(' ', '-');
        if (safeProperty.Length > 60)
        {
            safeProperty = safeProperty[..60];
        }

        return $"opening-readings-{safeProperty}.{extension}";
    }

    private static string SanitizeFileName(string? fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty).Trim();
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Where(character => !invalid.Contains(character)).ToArray()).Trim();
        if (cleaned.Length == 0)
        {
            return "opening-readings";
        }

        return cleaned.Length <= 200 ? cleaned : cleaned[^200..];
    }

    private static string ComputeContentHash(Guid propertyId, IReadOnlyList<ParsedOpeningReadingRow> rows)
    {
        var builder = new StringBuilder();
        builder.Append(propertyId.ToString()).Append('\n');
        foreach (var row in rows)
        {
            builder.Append(string.Join('\u001f', row.TemplateVersion, row.PropertyId, row.PropertyName, row.UnitId, row.UnitName, row.MeterInstallationId, row.MeterType, row.OpeningReading, row.ReadingDate, row.FirstBillingMonth)).Append('\n');
        }

        return Hash(Encoding.UTF8.GetBytes(builder.ToString()));
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string Describe(string value) => string.IsNullOrWhiteSpace(value) ? "(blank)" : value;

    private static IResult BadRequest(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [message] });

    private sealed record EvaluationContext(
        DateOnly DefaultMonth,
        IReadOnlyDictionary<Guid, Unit> Units,
        IReadOnlyDictionary<Guid, MeterInstallation> Installations,
        IReadOnlyList<ChargeConfiguration> Configurations,
        IReadOnlyList<DateOnly> CycleMonths);

    private sealed record EvaluatedRow(
        ParsedOpeningReadingRow Row,
        Guid UnitId,
        string UnitName,
        Guid? InstallationId,
        long? InstallationVersion,
        string Status,
        decimal? OpeningReading,
        string? ReadingDate,
        string FirstBillingMonth,
        bool UsesLegacyAdoption,
        IReadOnlyList<string> Errors,
        IReadOnlyList<string> Notes);

    private sealed record OpeningReadingPreviewPayload(
        int Version,
        Guid PropertyId,
        string PropertyName,
        string FileName,
        string FileHash,
        string ContentHash,
        DateTimeOffset IssuedAtUtc,
        DateTimeOffset ExpiresAtUtc,
        int FileRowCount,
        IReadOnlyList<OpeningReadingPreviewPayloadRow> Rows);

    private sealed record OpeningReadingPreviewPayloadRow(
        int RowNumber,
        Guid UnitId,
        string UnitName,
        Guid? InstallationId,
        long? InstallationVersion,
        decimal OpeningReading,
        string ReadingDate,
        string FirstBillingMonth,
        bool UsesLegacyAdoption);
}
