using System.Collections.Concurrent;
using System.Globalization;
using EHCTelebot.Data;
using EHCTelebot.Models;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using TelegramUpdate = Telegram.Bot.Types.Update;

namespace EHCTelebot.Services;

public class TelegramUpdateHandler
{
    private readonly AppDbContext _db;
    private readonly TelegramService _telegramService;

    private static readonly ConcurrentDictionary<
        long,
        RegistrationSession>
        _sessions = new();

    public TelegramUpdateHandler(
        AppDbContext db,
        TelegramService telegramService)
    {
        _db = db;
        _telegramService = telegramService;
    }

    public async Task HandleAsync(TelegramUpdate update)
    {
        if (update.Message == null ||
            string.IsNullOrWhiteSpace(update.Message.Text))
        {
            return;
        }

        var chatId = update.Message.Chat.Id;
        var text = update.Message.Text.Trim();

        // =============================================
        // COMMANDS ALWAYS HAVE PRIORITY
        // =============================================

        if (IsCommand(text, "/start"))
        {
            await HandleStartAsync(chatId);
            return;
        }

        if (IsCommand(text, "/check"))
        {
            await HandleCheckAsync(chatId);
            return;
        }

        if (IsCommand(text, "/update"))
        {
            await HandleUpdateAsync(chatId);
            return;
        }

        // =============================================
        // ACTIVE REGISTRATION SESSION
        // =============================================

        if (_sessions.ContainsKey(chatId))
        {
            await HandleRegistrationMessageAsync(
                chatId,
                text);

            return;
        }

        // =============================================
        // NORMAL MESSAGE
        // =============================================

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ChatId == chatId);

        // User chưa đăng ký
        if (user == null)
        {
            await SendMessageAsync(
                chatId,
                "❌ Bạn chưa đăng ký thông tin.\n\n" +
                "Vui lòng gửi /start để bắt đầu đăng ký.");

            return;
        }

        // User đã đăng ký nhưng message không thuộc
        // chức năng của bot.
        await SendMessageAsync(
            chatId,
            "ℹ️ Tôi chưa có tính năng này.\n\n" +
            "Vui lòng liên hệ Vulh để anh ấy thêm tính năng.");
    }

    // =====================================================
    // /start
    // =====================================================

    private async Task HandleStartAsync(long chatId)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ChatId == chatId);

        // ---------------------------------------------
        // USER ĐÃ ĐĂNG KÝ
        // ---------------------------------------------

        if (user != null &&
            user.ChatState == (int)ChatState.Completed)
        {
            await SendMessageAsync(
                chatId,
                BuildRegisteredInfoMessage(user));

            return;
        }

        // ---------------------------------------------
        // USER CHƯA ĐĂNG KÝ
        // ---------------------------------------------

        _sessions.TryRemove(chatId, out _);

        _sessions[chatId] = new RegistrationSession
        {
            State = ChatState.WaitingForName
        };

        await SendMessageAsync(
            chatId,
            "👋 Xin chào! Tôi là EHCBot.\n\n" +
            "🤖 Bot giúp bạn:\n" +
            "• Lưu ngày bắt đầu làm việc tại EHC\n" +
            "• Tính số ngày đã làm việc tại EHC\n" +
            "• Gửi thông báo tự động mỗi ngày\n\n" +
            "📌 CÁC LỆNH\n" +
            "━━━━━━━━━━━━━━\n" +
            "/start - Bắt đầu / xem thông tin\n" +
            "/check - Xem thông tin và số ngày\n" +
            "/update - Cập nhật thông tin\n" +
            "━━━━━━━━━━━━━━\n\n" +
            "📝 Để bắt đầu đăng ký, vui lòng nhập tên của bạn:");
    }

    // =====================================================
    // /check
    // =====================================================

    private async Task HandleCheckAsync(long chatId)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ChatId == chatId);

        if (user == null ||
            user.ChatState != (int)ChatState.Completed)
        {
            await SendMessageAsync(
                chatId,
                "❌ Bạn chưa đăng ký thông tin.\n\n" +
                "Vui lòng gửi /start để bắt đầu đăng ký.");

            return;
        }

        await SendMessageAsync(
            chatId,
            BuildRegisteredInfoMessage(user));
    }

    // =====================================================
    // /update
    // =====================================================

    private async Task HandleUpdateAsync(long chatId)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ChatId == chatId);

        if (user == null ||
            user.ChatState != (int)ChatState.Completed)
        {
            await SendMessageAsync(
                chatId,
                "❌ Bạn chưa đăng ký thông tin.\n\n" +
                "Vui lòng gửi /start để bắt đầu đăng ký.");

            return;
        }

        _sessions[chatId] = new RegistrationSession
        {
            State = ChatState.WaitingForName
        };

        await SendMessageAsync(
            chatId,
            "🔄 CẬP NHẬT THÔNG TIN\n\n" +
            "Bạn sẽ nhập lại toàn bộ thông tin từ đầu.\n\n" +
            "📝 Vui lòng nhập tên mới:");
    }

    // =====================================================
    // REGISTRATION / UPDATE
    // =====================================================

    private async Task HandleRegistrationMessageAsync(
        long chatId,
        string text)
    {
        if (!_sessions.TryGetValue(
                chatId,
                out var session))
        {
            return;
        }

        // ---------------------------------------------
        // NAME
        // ---------------------------------------------

        if (session.State == ChatState.WaitingForName)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                await SendMessageAsync(
                    chatId,
                    "❌ Tên không được để trống.\n\n" +
                    "Vui lòng nhập lại:");
                return;
            }

            if (text.Length > 100)
            {
                await SendMessageAsync(
                    chatId,
                    "❌ Tên không được vượt quá 100 ký tự.\n\n" +
                    "Vui lòng nhập lại:");
                return;
            }

            session.Name = text;
            session.State = ChatState.WaitingForStartDate;

            await SendMessageAsync(
                chatId,
                "✅ Đã nhận tên.\n\n" +
                "📅 Vui lòng nhập ngày bắt đầu làm việc tại EHC.\n\n" +
                "Định dạng bắt buộc: dd/MM/yyyy\n" +
                "Ví dụ: 04/08/2025");

            return;
        }

        // ---------------------------------------------
        // START DATE
        // ---------------------------------------------

        if (session.State == ChatState.WaitingForStartDate)
        {
            var valid = DateTime.TryParseExact(
                text,
                "dd/MM/yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var startDate);

            if (!valid)
            {
                await SendMessageAsync(
                    chatId,
                    "❌ Ngày không hợp lệ.\n\n" +
                    "Vui lòng nhập đúng định dạng dd/MM/yyyy.\n" +
                    "Ví dụ: 04/08/2025");

                return;
            }

            // Không cho phép ngày tương lai.
            if (startDate.Date > DateTime.UtcNow.Date)
            {
                await SendMessageAsync(
                    chatId,
                    "❌ Ngày bắt đầu không thể nằm trong tương lai.\n\n" +
                    "Vui lòng nhập lại:");

                return;
            }

            var user = await _db.Users
                .FirstOrDefaultAsync(
                    x => x.ChatId == chatId);

            if (user == null)
            {
                // =====================================
                // REGISTRATION
                // =====================================

                user = new User
                {
                    ChatId = chatId,
                    Name = session.Name,
                    StartDate = startDate.Date,
                    ChatState = (int)ChatState.Completed,
                    IsActive = true,
                    LastNotificationDate = null
                };

                _db.Users.Add(user);
            }
            else
            {
                // =====================================
                // UPDATE
                // =====================================

                user.Name = session.Name;
                user.StartDate = startDate.Date;
                user.ChatState = (int)ChatState.Completed;
                user.IsActive = true;

                // Thông tin thay đổi → cho phép daily mới
                // trong tương lai.
                user.LastNotificationDate = null;
            }

            await _db.SaveChangesAsync();

            _sessions.TryRemove(chatId, out _);

            await SendMessageAsync(
                chatId,
                BuildRegistrationSuccessMessage(user));
        }
    }

    // =====================================================
    // MESSAGE BUILDERS
    // =====================================================

    private static string BuildRegisteredInfoMessage(User user)
    {
        var days = CalculateWorkDays(user.StartDate);

        return
            "✅ BẠN ĐÃ ĐĂNG KÝ THÔNG TIN\n\n" +
            "👤 THÔNG TIN CỦA BẠN\n" +
            "━━━━━━━━━━━━━━\n" +
            $"Tên: {user.Name}\n" +
            $"Ngày bắt đầu: {user.StartDate:dd/MM/yyyy}\n" +
            "Trạng thái: Đang nhận thông báo\n" +
            $"Số ngày làm việc tại EHC: {days} ngày\n" +
            "━━━━━━━━━━━━━━\n\n" +
            $"☀️ Bạn đã làm việc tại EHC được {days} ngày!\n" +
            "Chúc bạn một ngày làm việc hiệu quả! 💪\n\n" +
            "Bạn muốn làm gì tiếp theo?\n\n" +
            "/check - Xem thông tin và số ngày\n" +
            "/update - Cập nhật thông tin";
    }

    private static string BuildRegistrationSuccessMessage(
        User user)
    {
        var days = CalculateWorkDays(user.StartDate);

        return
            "🎉 ĐĂNG KÝ/CẬP NHẬT THÀNH CÔNG!\n\n" +
            "👤 THÔNG TIN CỦA BẠN\n" +
            "━━━━━━━━━━━━━━\n" +
            $"Tên: {user.Name}\n" +
            $"Ngày bắt đầu: {user.StartDate:dd/MM/yyyy}\n" +
            "Trạng thái: Đang nhận thông báo\n" +
            $"Số ngày làm việc tại EHC: {days} ngày\n" +
            "━━━━━━━━━━━━━━\n\n" +
            $"☀️ Bạn đã làm việc tại EHC được {days} ngày!\n" +
            "Chúc bạn một ngày làm việc hiệu quả! 💪\n\n" +
            "Bạn có thể sử dụng:\n" +
            "/check - Xem thông tin và số ngày\n" +
            "/update - Cập nhật thông tin";
    }

    // =====================================================
    // DAY CALCULATION
    // =====================================================

    private static int CalculateWorkDays(DateTime? startDate)
    {
        if (!startDate.HasValue)
        {
            return 0;
        }

        var today = DateTime.UtcNow.Date;

        var days =
            (today - startDate.Value.Date).Days + 1;

        return Math.Max(days, 1);
    }

    // =====================================================
    // COMMAND
    // =====================================================

    private static bool IsCommand(
        string text,
        string command)
    {
        var firstPart = text
            .Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        return !string.IsNullOrWhiteSpace(firstPart) &&
               firstPart.Equals(
                   command,
                   StringComparison.OrdinalIgnoreCase);
    }

    // =====================================================
    // SEND
    // =====================================================

    private async Task SendMessageAsync(
        long chatId,
        string message)
    {
        await _telegramService.Client
            .SendTextMessageAsync(
                chatId,
                message);
    }
}