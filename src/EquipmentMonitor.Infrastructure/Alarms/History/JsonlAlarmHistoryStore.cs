using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;
using EquipmentMonitor.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Serilog;

namespace EquipmentMonitor.Infrastructure.Alarms.History;

/// <summary>
/// 报警历史的 JSONL 实现：按天滚动、追加写、查询时按 Id 折叠到最新状态。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么是事件流而不是覆盖写</b>：确认/恢复都追加一条同 Id 的新行，
/// 查询时取最后一行。这样即使写到一半断电，也只会丢最后一条，不会破坏历史文件。
/// </para>
/// <para>
/// <b>单写者</b>：内部 <see cref="SemaphoreSlim"/> 串行化写入；失败只记日志，不影响报警主流程。
/// </para>
/// <para>
/// 二期换 SQLite 时替换本类即可（表存每个 Id 的最新状态，用 UPDATE/UPSERT）。
/// </para>
/// </remarks>
public sealed class JsonlAlarmHistoryStore : IAlarmHistoryStore
{
    private static readonly ILogger Logger = Log.ForContext<JsonlAlarmHistoryStore>();

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly Regex FileNamePattern = new(
        @"^alarms-(?<date>\d{8})\.jsonl$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly AlarmOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    /// <summary>创建 JSONL 历史存储。</summary>
    /// <param name="options">报警配置。</param>
    /// <param name="timeProvider">时间源；测试可注入固定时间。</param>
    public JsonlAlarmHistoryStore(
        IOptions<AlarmOptions> options,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _timeProvider = timeProvider ?? TimeProvider.System;

        CleanupExpiredFiles();
    }

    /// <summary>历史目录（测试用）。</summary>
    public string DirectoryPath => ResolveDirectory();

    /// <inheritdoc />
    public async Task AppendAsync(AlarmRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        try
        {
            var directory = ResolveDirectory();
            Directory.CreateDirectory(directory);

            var now = _timeProvider.GetUtcNow();
            var file = Path.Combine(directory, $"alarms-{now:yyyyMMdd}.jsonl");
            var line = JsonSerializer.Serialize(record, SerializerOptions) + Environment.NewLine;

            await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await File.AppendAllTextAsync(file, line, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _writeGate.Release();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // 历史写失败不得影响报警主流程。
            Logger.Warning(ex, "写报警历史失败：{AlarmId}", record.Id);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AlarmRecord>> QueryAsync(
        AlarmHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = Math.Clamp(query.Limit, 1, 5000);
        var latest = new Dictionary<string, AlarmRecord>(StringComparer.Ordinal);
        var directory = ResolveDirectory();

        if (!Directory.Exists(directory))
        {
            return [];
        }

        foreach (var file in Directory.GetFiles(directory, "alarms-*.jsonl").OrderBy(f => f))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string[] lines;

            try
            {
                lines = await File.ReadAllLinesAsync(file, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "读取报警历史文件失败：{File}", file);
                continue;
            }

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var record = JsonSerializer.Deserialize<AlarmRecord>(line, SerializerOptions);

                    if (record is not null && !string.IsNullOrWhiteSpace(record.Id))
                    {
                        // 后写入的行覆盖先写入的行：同 Id 只保留最新状态。
                        latest[record.Id] = record;
                    }
                }
                catch (JsonException ex)
                {
                    // 单行损坏不影响整个历史文件；文件是追加写的，尾部半行是断电的典型表现。
                    Logger.Warning(ex, "跳过损坏的报警历史行：{File}", file);
                }
            }
        }

        return latest.Values
            .Where(record => Matches(record, query))
            .OrderByDescending(record => record.RaisedAt)
            .Take(limit)
            .ToArray();
    }

    private static bool Matches(AlarmRecord record, AlarmHistoryQuery query)
    {
        if (query.From is { } from && record.RaisedAt < from)
        {
            return false;
        }

        if (query.To is { } to && record.RaisedAt > to)
        {
            return false;
        }

        if (query.MinimumLevel is { } minimum && record.Level < minimum)
        {
            return false;
        }

        if (query.State is { } state && record.State != state)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.SourceId) &&
            !string.Equals(record.SourceId, query.SourceId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.ElementId) &&
            !string.Equals(record.ElementId, query.ElementId, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private string ResolveDirectory()
    {
        var configured = string.IsNullOrWhiteSpace(_options.HistoryDirectory)
            ? "logs/alarms"
            : _options.HistoryDirectory;

        return Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(AppContext.BaseDirectory, configured);
    }

    private void CleanupExpiredFiles()
    {
        try
        {
            var directory = ResolveDirectory();

            if (!Directory.Exists(directory) || _options.HistoryRetentionDays <= 0)
            {
                return;
            }

            var cutoff = _timeProvider.GetUtcNow().UtcDateTime.Date.AddDays(-_options.HistoryRetentionDays);

            foreach (var file in Directory.GetFiles(directory, "alarms-*.jsonl"))
            {
                var match = FileNamePattern.Match(Path.GetFileName(file));

                if (!match.Success ||
                    !DateTime.TryParseExact(
                        match.Groups["date"].Value,
                        "yyyyMMdd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var date))
                {
                    continue;
                }

                if (date < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex)
        {
            // 清理失败不影响历史读写。
            Logger.Warning(ex, "清理过期报警历史文件失败。");
        }
    }
}
