using EHCTelebot.Data;
using EHCTelebot.Models;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Exceptions;

namespace EHCTelebot.Services;

public class DailyNotificationService
{
    private readonly AppDbContext _db;
    private readonly TelegramService _telegramService;
    private readonly ILogger<DailyNotificationService> _logger;

    private const int DelayBetweenMessagesMs = 50;

    public DailyNotificationService(
        AppDbContext db,
        TelegramService telegramService,
        ILogger<DailyNotificationService> logger)
    {
        _db = db;
        _telegramService = telegramService;
        _logger = logger;
    }

    public async Task<DailyNotificationResult>
        SendDailyNotificationsAsync(
            CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;

        var users = await _db.Users
            .AsNoTracking()
            .Where(x =>
                x.ChatState == (int)ChatState.Completed &&
                x.IsActive &&
                x.StartDate != null &&
                x.StartDate <= today)
            .OrderBy(x => x.ChatId)
            .ToListAsync(cancellationToken);

        var result = new DailyNotificationResult
        {
            Date = today,
            TotalUsers = users.Count
        };

        _logger.LogInformation(
            "Daily job started. Date={Date}, Users={Count}",
            today,
            users.Count);

        foreach (var user in users)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var status = await ProcessUserAsync(
                user,
                today,
                cancellationToken);

            switch (status)
            {
                case DailyNotificationStatus.Sent:
                    result.Sent++;
                    break;

                case DailyNotificationStatus.AlreadyProcessed:
                    result.Skipped++;
                    break;

                case DailyNotificationStatus.Failed:
                    result.Failed++;
                    break;
            }

            await Task.Delay(
                DelayBetweenMessagesMs,
                cancellationToken);
        }

        _logger.LogInformation(
            "Daily job finished. Date={Date}, Total={Total}, Sent={Sent}, Skipped={Skipped}, Failed={Failed}",
            today,
            result.TotalUsers,
            result.Sent,
            result.Skipped,
            result.Failed);

        return result;
    }

    private async Task<DailyNotificationStatus>
        ProcessUserAsync(
            User user,
            DateTime today,
            CancellationToken cancellationToken)
    {
        // =================================================
        // 1. ATOMIC CLAIM
        // =================================================

        var claimSql =
            "INSERT INTO \"DailyNotificationLogs\" " +
            "(\"ChatId\", \"NotificationDate\", \"Status\", \"CreatedAt\") " +
            "VALUES ({0}, {1}, 'Failed', CURRENT_TIMESTAMP) " +
            "ON CONFLICT (\"ChatId\", \"NotificationDate\") DO NOTHING;";

        var affectedRows =
            await _db.Database.ExecuteSqlRawAsync(
                claimSql,
                new object[]
                {
                    user.ChatId,
                    today
                },
                cancellationToken);

        // Một trigger khác đã claim user/ngày này.
        if (affectedRows == 0)
        {
            return DailyNotificationStatus.AlreadyProcessed;
        }

        // =================================================
        // 2. CALCULATE WORK DAYS
        // =================================================

        var startDate = user.StartDate!.Value.Date;

        var workDays =
            (today - startDate).Days + 1;

        if (workDays < 1)
        {
            await MarkFailedAsync(
                user.ChatId,
                today,
                "Invalid StartDate.",
                cancellationToken);

            return DailyNotificationStatus.Failed;
        }

        if (string.IsNullOrWhiteSpace(user.Name))
        {
            await MarkFailedAsync(
                user.ChatId,
                today,
                "User name is empty.",
                cancellationToken);

            return DailyNotificationStatus.Failed;
        }

        // =================================================
        // 3. BUILD MESSAGE
        // =================================================

        var message =
            $"☀️ Chào {user.Name}!\n\n" +
            $"🎉 Chúc mừng bạn đã làm việc ở EHC được {workDays} ngày.\n\n" +
            "Chúc bạn một ngày làm việc hiệu quả! 💪";

        // =================================================
        // 4. SEND TELEGRAM
        // =================================================

        try
        {
            await _telegramService.Client
                .SendTextMessageAsync(
                    user.ChatId,
                    message);

            // =============================================
            // 5. MARK SENT
            // =============================================

            await MarkSentAsync(
                user.ChatId,
                today,
                cancellationToken);

            _logger.LogInformation(
                "Daily sent. ChatId={ChatId}, Days={Days}",
                user.ChatId,
                workDays);

            return DailyNotificationStatus.Sent;
        }
        catch (ApiRequestException ex)
        {
            await MarkFailedAsync(
                user.ChatId,
                today,
                $"Telegram {ex.ErrorCode}: {ex.Message}",
                CancellationToken.None);

            _logger.LogError(
                "Telegram error. ChatId={ChatId}, Code={Code}",
                user.ChatId,
                ex.ErrorCode);

            return DailyNotificationStatus.Failed;
        }
        catch (Exception ex)
        {
            await MarkFailedAsync(
                user.ChatId,
                today,
                ex.Message,
                CancellationToken.None);

            _logger.LogError(
                ex,
                "Unexpected notification error. ChatId={ChatId}",
                user.ChatId);

            return DailyNotificationStatus.Failed;
        }
    }

    private async Task MarkSentAsync(
        long chatId,
        DateTime today,
        CancellationToken cancellationToken)
    {
        var log = await _db.DailyNotificationLogs
            .FirstOrDefaultAsync(
                x =>
                    x.ChatId == chatId &&
                    x.NotificationDate == today,
                cancellationToken);

        if (log == null)
        {
            return;
        }

        log.Status = "Sent";
        log.SentAt = DateTime.UtcNow;
        log.ErrorMessage = null;

        await _db.SaveChangesAsync(
            cancellationToken);
    }

    private async Task MarkFailedAsync(
        long chatId,
        DateTime today,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            var log = await _db.DailyNotificationLogs
                .FirstOrDefaultAsync(
                    x =>
                        x.ChatId == chatId &&
                        x.NotificationDate == today,
                    cancellationToken);

            if (log == null)
            {
                return;
            }

            log.Status = "Failed";
            log.ErrorMessage = errorMessage;

            await _db.SaveChangesAsync(
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to update notification log. ChatId={ChatId}",
                chatId);
        }
    }
}

public class DailyNotificationResult
{
    public DateTime Date { get; set; }

    public int TotalUsers { get; set; }

    public int Sent { get; set; }

    public int Skipped { get; set; }

    public int Failed { get; set; }
}

public enum DailyNotificationStatus
{
    Sent,
    AlreadyProcessed,
    Failed
}