using System.Text.Json;

namespace AlertaRio.Infrastructure.Providers;

public sealed record InaTimeSupport(
    int Years, int Months, int Days, int Hours, int Minutes, int Seconds, int Milliseconds)
{
    public bool IsInstantaneous =>
        Years == 0 && Months == 0 && Days == 0 && Hours == 0 &&
        Minutes == 0 && Seconds == 0 && Milliseconds == 0;
}

public sealed record InaSeriesCandidate(
    int ExternalSeriesId, int ExternalStationId, int NetworkId,
    int? VariableId, string VariableCode, string? VariableName,
    int? ProcedureId, string ProcedureName, int UnitId, string Unit,
    InaTimeSupport TimeSupport, string? CatalogStartRaw, string? CatalogEndRaw)
{
    public bool IsInstantaneous => TimeSupport.IsInstantaneous;
}

// Series remain unapproved until source rights, unit meaning and cadence are reviewed.
public static class InaSeriesCandidateParser
{
    public static IReadOnlyList<InaSeriesCandidate> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var rows = root.ValueKind switch
        {
            JsonValueKind.Array => root,
            JsonValueKind.Object when root.TryGetProperty("rows", out var wrappedRows)
                && wrappedRows.ValueKind == JsonValueKind.Array => wrappedRows,
            _ => throw new JsonException("Unexpected INA series wrapper.")
        };

        var candidates = new List<InaSeriesCandidate>();
        var ids = new HashSet<int>();
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object ||
                !row.TryGetProperty("estacion", out var station) ||
                station.ValueKind != JsonValueKind.Object)
                throw new JsonException("Invalid INA series row.");
            if (!IsPublic(station)) continue;

            if (!TryString(row, "tipo", out var kind) || kind != "puntual" ||
                !TryInt(row, "id", out var seriesId) ||
                !TryInt(station, "id", out var stationId) ||
                !station.TryGetProperty("red", out var network) ||
                !TryInt(network, "id", out var networkId) ||
                !row.TryGetProperty("var", out var variable) ||
                variable.ValueKind != JsonValueKind.Object ||
                !TryOptionalInt(variable, "id", out var variableId) ||
                !TryString(variable, "var", out var variableCode) ||
                !TryOptionalString(variable, "nombre", out var variableName) ||
                !variable.TryGetProperty("timeSupport", out var support) ||
                !TryTimeSupport(support, out var timeSupport) ||
                !row.TryGetProperty("procedimiento", out var procedure) ||
                procedure.ValueKind != JsonValueKind.Object ||
                !TryOptionalInt(procedure, "id", out var procedureId) ||
                !TryString(procedure, "nombre", out var procedureName) ||
                !row.TryGetProperty("unidades", out var units) ||
                units.ValueKind != JsonValueKind.Object ||
                !TryInt(units, "id", out var unitId) ||
                !TryString(units, "abrev", out var unit) ||
                !TryDateRange(row, out var startRaw, out var endRaw) ||
                !ids.Add(seriesId))
                throw new JsonException("Public INA series lacks required metadata.");

            candidates.Add(new InaSeriesCandidate(seriesId, stationId, networkId,
                variableId, variableCode, variableName, procedureId,
                procedureName, unitId, unit, timeSupport!,
                startRaw, endRaw));
        }

        return candidates;
    }

    private static bool IsPublic(JsonElement station) =>
        station.TryGetProperty("public", out var visibility) &&
        visibility.ValueKind == JsonValueKind.True &&
        station.TryGetProperty("red", out var network) &&
        network.ValueKind == JsonValueKind.Object &&
        network.TryGetProperty("public", out var networkVisibility) &&
        networkVisibility.ValueKind == JsonValueKind.True;

    private static bool TryTimeSupport(JsonElement support, out InaTimeSupport? value)
    {
        value = null;
        if (support.ValueKind != JsonValueKind.Object ||
            !TryInt(support, "years", out var years) ||
            !TryInt(support, "months", out var months) ||
            !TryInt(support, "days", out var days) ||
            !TryInt(support, "hours", out var hours) ||
            !TryInt(support, "minutes", out var minutes) ||
            !TryInt(support, "seconds", out var seconds) ||
            !TryInt(support, "milliseconds", out var milliseconds) ||
            years < 0 || months < 0 || days < 0 || hours < 0 ||
            minutes < 0 || seconds < 0 || milliseconds < 0)
            return false;
        value = new InaTimeSupport(years, months, days, hours, minutes, seconds,
            milliseconds);
        return true;
    }

    private static bool TryDateRange(
        JsonElement row, out string? startRaw, out string? endRaw)
    {
        startRaw = null;
        endRaw = null;
        if (!row.TryGetProperty("date_range", out var range) ||
            range.ValueKind == JsonValueKind.Null)
            return true;
        if (range.ValueKind != JsonValueKind.Object)
            return false;
        return TryNullableString(range, "timestart", out startRaw) &&
               TryNullableString(range, "timeend", out endRaw);
    }

    private static bool TryNullableString(JsonElement parent, string name, out string? value)
    {
        value = null;
        if (!parent.TryGetProperty(name, out var field) || field.ValueKind == JsonValueKind.Null)
            return true;
        if (field.ValueKind != JsonValueKind.String)
            return false;
        value = field.GetString();
        return true;
    }

    private static bool TryString(JsonElement parent, string name, out string value)
    {
        value = string.Empty;
        if (!parent.TryGetProperty(name, out var field) || field.ValueKind != JsonValueKind.String)
            return false;
        value = field.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryOptionalString(JsonElement parent, string name, out string? value)
    {
        value = null;
        if (!parent.TryGetProperty(name, out var field) || field.ValueKind == JsonValueKind.Null)
            return true;
        if (field.ValueKind != JsonValueKind.String)
            return false;
        value = field.GetString();
        return true;
    }

    private static bool TryOptionalInt(JsonElement parent, string name, out int? value)
    {
        value = null;
        if (!parent.TryGetProperty(name, out var field) || field.ValueKind == JsonValueKind.Null)
            return true;
        if (field.ValueKind != JsonValueKind.Number || !field.TryGetInt32(out var parsed))
            return false;
        value = parsed;
        return true;
    }

    private static bool TryInt(JsonElement parent, string name, out int value)
    {
        value = 0;
        return parent.ValueKind == JsonValueKind.Object &&
               parent.TryGetProperty(name, out var field) &&
               field.ValueKind == JsonValueKind.Number && field.TryGetInt32(out value);
    }
}
