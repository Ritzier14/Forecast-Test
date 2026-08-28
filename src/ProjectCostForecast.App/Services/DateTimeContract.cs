using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ProjectCostForecast.App.Services;

/// <summary>
/// Supplies the current instant and the NZ business date without making
/// workflows depend on ambient machine time.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }

    DateTimeOffset NewZealandNow { get; }

    DateOnly TodayInNewZealand { get; }
}

public sealed class SystemClock : IClock
{
    public static SystemClock Instance { get; } = new();

    public SystemClock()
    {
    }

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateTimeOffset NewZealandNow => DateTimeContract.ToNewZealand(UtcNow);

    public DateOnly TodayInNewZealand => DateTimeContract.ToNewZealandDate(UtcNow);
}

/// <summary>
/// A deterministic clock for tests and replayable audit/snapshot operations.
/// </summary>
public sealed class FixedClock : IClock
{
    public FixedClock(DateTimeOffset utcNow)
    {
        UtcNow = DateTimeContract.NormalizeUtc(utcNow);
    }

    public DateTimeOffset UtcNow { get; }

    public DateTimeOffset NewZealandNow => DateTimeContract.ToNewZealand(UtcNow);

    public DateOnly TodayInNewZealand => DateTimeContract.ToNewZealandDate(UtcNow);
}

/// <summary>
/// Defines the date/time contract at the persistence boundary:
/// <list type="bullet">
/// <item><description>NZ business dates are <see cref="DateOnly"/> values.</description></item>
/// <item><description>Display times are converted to Pacific/Auckland and formatted with en-NZ.</description></item>
/// <item><description>Durable instants are UTC <see cref="DateTimeOffset"/> values.</description></item>
/// </list>
/// </summary>
public static class DateTimeContract
{
    public const string NewZealandTimeZoneId = "Pacific/Auckland";
    public const string UtcPersistenceFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

    private static readonly Regex ExplicitOffsetPattern = new(
        @"(?:Z|[+-]\d{2}:?\d{2})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static TimeZoneInfo NewZealandTimeZone { get; } = ResolveNewZealandTimeZone();

    public static CultureInfo NewZealandDisplayCulture { get; } = CultureInfo.GetCultureInfo("en-NZ");

    public static DateTimeOffset NormalizeUtc(DateTimeOffset instant)
    {
        return instant.ToUniversalTime();
    }

    public static DateTimeOffset FromDateTime(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => new DateTimeOffset(value, TimeSpan.Zero),
            DateTimeKind.Local => new DateTimeOffset(value).ToUniversalTime(),
            _ => FromNewZealandLocal(value)
        };
    }

    /// <summary>
    /// Converts an offset-free legacy local timestamp using the application's
    /// NZ business timezone. Ambiguous fall-back times use the standard offset;
    /// invalid spring-forward times are rejected instead of silently shifting.
    /// </summary>
    public static DateTimeOffset FromNewZealandLocal(DateTime value)
    {
        var local = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        if (NewZealandTimeZone.IsInvalidTime(local))
        {
            throw new ArgumentException(
                $"The NZ local timestamp '{value:O}' falls inside the daylight-saving gap.",
                nameof(value));
        }

        var offset = NewZealandTimeZone.IsAmbiguousTime(local)
            ? NewZealandTimeZone.GetAmbiguousTimeOffsets(local).Min()
            : NewZealandTimeZone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    public static DateTimeOffset ToNewZealand(DateTimeOffset instant)
    {
        return TimeZoneInfo.ConvertTime(NormalizeUtc(instant), NewZealandTimeZone);
    }

    public static DateOnly ToNewZealandDate(DateTimeOffset instant)
    {
        return DateOnly.FromDateTime(ToNewZealand(instant).DateTime);
    }

    public static string FormatUtc(DateTimeOffset instant)
    {
        return NormalizeUtc(instant).ToString(UtcPersistenceFormat, CultureInfo.InvariantCulture);
    }

    public static string FormatNewZealand(DateTimeOffset instant, string format = "g")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        return ToNewZealand(instant).ToString(format, NewZealandDisplayCulture);
    }

    public static string FormatBusinessDate(DateOnly date, string format = "d MMM yyyy")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        return date.ToString(format, NewZealandDisplayCulture);
    }

    public static DateTimeOffset ParsePersistedInstant(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException("A durable timestamp cannot be empty.");
        }

        var trimmed = value.Trim();
        if (HasExplicitOffset(trimmed))
        {
            if (DateTimeOffset.TryParse(
                    trimmed,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces,
                    out var explicitInstant))
            {
                return NormalizeUtc(explicitInstant);
            }
        }
        else if (DateTime.TryParse(
                     trimmed,
                     CultureInfo.InvariantCulture,
                     DateTimeStyles.AllowWhiteSpaces,
                     out var legacyLocal))
        {
            return FromNewZealandLocal(legacyLocal);
        }

        throw new FormatException($"The durable timestamp '{trimmed}' is not a valid ISO timestamp.");
    }

    public static void AddJsonConverters(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Converters.Any(converter => converter is UtcDateTimeOffsetJsonConverter))
        {
            options.Converters.Add(new UtcDateTimeOffsetJsonConverter());
        }

        if (!options.Converters.Any(converter => converter is NullableUtcDateTimeOffsetJsonConverter))
        {
            options.Converters.Add(new NullableUtcDateTimeOffsetJsonConverter());
        }
    }

    internal static IClock FromLegacyUtcFactory(Func<DateTime> utcNow)
    {
        ArgumentNullException.ThrowIfNull(utcNow);
        return new DelegateClock(utcNow);
    }

    private static bool HasExplicitOffset(string value)
    {
        return ExplicitOffsetPattern.IsMatch(value);
    }

    private static TimeZoneInfo ResolveNewZealandTimeZone()
    {
        foreach (var id in new[] { "New Zealand Standard Time", NewZealandTimeZoneId })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // Try the identifier used by the other platform family.
            }
            catch (InvalidTimeZoneException)
            {
                // Try the identifier used by the other platform family.
            }
        }

        throw new InvalidOperationException("The system does not provide the Pacific/Auckland timezone required by the date/time contract.");
    }

    private sealed class DelegateClock : IClock
    {
        private readonly Func<DateTime> _utcNow;

        public DelegateClock(Func<DateTime> utcNow)
        {
            _utcNow = utcNow;
        }

        public DateTimeOffset UtcNow
        {
            get
            {
                // The compatibility delegate has always been named utcNow;
                // treat its wall-clock value as UTC even when an older caller
                // omitted DateTimeKind.Utc.
                var value = DateTime.SpecifyKind(_utcNow(), DateTimeKind.Utc);
                return new DateTimeOffset(value, TimeSpan.Zero);
            }
        }

        public DateTimeOffset NewZealandNow => ToNewZealand(UtcNow);

        public DateOnly TodayInNewZealand => ToNewZealandDate(UtcNow);
    }
}

public sealed class UtcDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("A durable timestamp must be an ISO timestamp string.");
        }

        try
        {
            return DateTimeContract.ParsePersistedInstant(reader.GetString() ?? string.Empty);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            throw new JsonException("The durable timestamp is not valid.", exception);
        }
    }

    public override void Write(
        Utf8JsonWriter writer,
        DateTimeOffset value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(DateTimeContract.FormatUtc(value));
    }
}

public sealed class NullableUtcDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset?>
{
    public override DateTimeOffset? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("A durable timestamp must be an ISO timestamp string or null.");
        }

        try
        {
            return DateTimeContract.ParsePersistedInstant(reader.GetString() ?? string.Empty);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            throw new JsonException("The durable timestamp is not valid.", exception);
        }
    }

    public override void Write(
        Utf8JsonWriter writer,
        DateTimeOffset? value,
        JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(DateTimeContract.FormatUtc(value.Value));
    }
}
