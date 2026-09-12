using System.Collections.Concurrent;
using System.Globalization;
using EHCTelebot.Data;
using EHCTelebot.Models;
using Telegram.Bot;
using TelegramUpdate = Telegram.Bot.Types.Update;

namespace EHCTelebot.Services;

public class TelegramUpdateHandler
{
    private readonly AppDbContext _db;
    private readonly TelegramService _telegramService;

    // Lưu trạng thái đăng ký dở dang trong memory.
    // Không lưu dữ liệu dở dang vào database.
    private static readonly ConcurrentDictionary<long, RegistrationSession>
        _registrationSessions = new();

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

        // =========================
        // COMMAND
        // =========================

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
            await HandleUpdateCommandAsync(chatId);
            return;
        }

        // =========================
        // USER ĐANG ĐĂNG KÝ
        // =========================

        if (_registrationSessions.TryGetValue(chatId, out _))
        {
            await HandleRegistrationMessageAsync(chatId, text);
            return;
        }

        // =========================
        // USER CHƯA ĐĂNG KÝ
        // =========================

        var user = await _db.Users.FindAsync(chatId);

        if (user == null)
        {
            await SendMessageAsync(
                chatId,
                "❌ Bạn chưa đăng ký thông tin.\n\n" +
                "Vui lòng gửi /start để bắt đầu đăng ký.");

            return;
        }

        // =========================
        // USER ĐÃ ĐĂNG KÝ
        // =========================

        await SendMessageAsync(
            chatId,
            "ℹ️ Xin lỗi. Hiện tại tôi chưa có tính năng này.\n\n" +
            "Vui lòng liên hệ Vulh để anh ấy thêm tính năng."+
            "📌 Các lệnh hiện tại có thể dùng:\n" +
            "/start - Bắt đầu hoặc xem thông tin\n" +
            "/check - Xem thông tin và số ngày làm việc\n" +
            "/update - Cập nhật lại thông tin\n\n"
            );
    }

    // =========================================================
    // /start
    // =========================================================

    private async Task HandleStartAsync(long chatId)
    {
        var user = await _db.Users.FindAsync(chatId);

        // User đã đăng ký
        if (user != null && user.ChatState == (int)ChatState.Completed)
        {
            var message = BuildRegisteredInfoMessage(user);

            await SendMessageAsync(chatId, message);

            return;
        }

        // Xóa session cũ nếu có
        _registrationSessions.TryRemove(chatId, out _);

        // Tạo session mới
        _registrationSessions[chatId] = new RegistrationSession
        {
            State = ChatState.WaitingForName
        };

        await SendMessageAsync(
            chatId,
            "👋 Xin chào! Tôi là EHCBot.\n\n" +
            "🤖 Bot giúp bạn:\n" +
            "• Lưu ngày bắt đầu làm việc tại EHC\n" +
            "• Tính số ngày đã làm việc tại EHC\n" +
            "• Gửi thông báo tự động lúc 08:00 mỗi ngày\n\n" +
            "📌 Các lệnh:\n" +
            "/start - Bắt đầu hoặc xem thông tin\n" +
            "/check - Xem thông tin và số ngày làm việc\n" +
            "/update - Cập nhật lại thông tin\n\n" +
            "━━━━━━━━━━━━━━\n\n" +
            "📝 Để đăng ký, vui lòng nhập tên của bạn:");
    }

    // =========================================================
    // /check
    // =========================================================

    private async Task HandleCheckAsync(long chatId)
    {
        var user = await _db.Users.FindAsync(chatId);

        if (user == null ||
            user.ChatState != (int)ChatState.Completed)
        {
            await SendMessageAsync(
                chatId,
                "❌ Bạn chưa đăng ký thông tin.\n\n" +
                "Vui lòng gửi /start để bắt đầu đăng ký.");

            return;
        }

        var message = BuildRegisteredInfoMessage(user);

        await SendMessageAsync(chatId, message);
    }

    // =========================================================
    // /update
    // =========================================================

    private async Task HandleUpdateCommandAsync(long chatId)
    {
        var user = await _db.Users.FindAsync(chatId);

        if (user == null ||
            user.ChatState != (int)ChatState.Completed)
        {
            await SendMessageAsync(
                chatId,
                "❌ Bạn chưa đăng ký thông tin.\n\n" +
                "Vui lòng gửi /start để bắt đầu đăng ký.");

            return;
        }

        // Bắt đầu lại từ đầu.
        _registrationSessions[chatId] = new RegistrationSession
        {
            State = ChatState.WaitingForName
        };

        await SendMessageAsync(
            chatId,
            "🔄 Cập nhật thông tin\n\n" +
            "Bạn sẽ nhập lại toàn bộ thông tin từ đầu.\n\n" +
            "📝 Vui lòng nhập tên mới:");
    }

    // =========================================================
    // REGISTRATION
    // =========================================================

    private async Task HandleRegistrationMessageAsync(
        long chatId,
        string text)
    {
        if (!_registrationSessions.TryGetValue(
                chatId,
                out var session))
        {
            return;
        }

        // -------------------------
        // Nhập tên
        // -------------------------

        if (session.State == ChatState.WaitingForName)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                await SendMessageAsync(
                    chatId,
                    "❌ Tên không được để trống.\n\n" +
                    "Vui lòng nhập lại tên của bạn:");

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
                "📅 Bây giờ hãy nhập ngày bắt đầu làm việc tại EHC.\n\n" +
                "Định dạng bắt buộc: dd/MM/yyyy\n" +
                "Ví dụ: 04/08/2025");

            return;
        }

        // -------------------------
        // Nhập ngày
        // -------------------------

        if (session.State == ChatState.WaitingForStartDate)
        {
            var isValidDate = DateTime.TryParseExact(
                text,
                "dd/MM/yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var startDate);

            if (!isValidDate)
            {
                await SendMessageAsync(
                    chatId,
                    "❌ Ngày không hợp lệ.\n\n" +
                    "Vui lòng nhập đúng định dạng dd/MM/yyyy.\n" +
                    "Ví dụ: 04/08/2025");

                return;
            }

            // Không cho ngày bắt đầu ở tương lai.
            if (startDate.Date > DateTime.UtcNow.Date)
            {
                await SendMessageAsync(
                    chatId,
                    "❌ Ngày bắt đầu không thể là ngày trong tương lai.\n\n" +
                    "Vui lòng nhập lại:");

                return;
            }

            var user = await _db.Users.FindAsync(chatId);

            if (user == null)
            {
                // ĐĂNG KÝ MỚI
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
                // UPDATE
                user.Name = session.Name;
                user.StartDate = startDate.Date;
                user.ChatState = (int)ChatState.Completed;
                user.IsActive = true;

                // Reset ngày notification vì thông tin đăng ký đã thay đổi.
                user.LastNotificationDate = null;
            }

            await _db.SaveChangesAsync();

            // Registration hoàn thành → xóa session khỏi memory.
            _registrationSessions.TryRemove(chatId, out _);

            var message = BuildRegistrationSuccessMessage(user);

            await SendMessageAsync(chatId, message);
        }
    }

    // =========================================================
    // BUILD MESSAGE
    // =========================================================

    private static string BuildRegisteredInfoMessage(User user)
    {
        var days = CalculateWorkDays(user.StartDate);

        return
            "✅ Bạn đã đăng ký thông tin.\n\n" +
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
            "/check - Xem lại thông tin và số ngày\n" +
            "/update - Cập nhật lại thông tin";
    }

    private static string BuildRegistrationSuccessMessage(User user)
    {
        var days = CalculateWorkDays(user.StartDate);

        return
            "🎉 ĐĂNG KÝ THÀNH CÔNG!\n\n" +
            "👤 THÔNG TIN CỦA BẠN\n" +
            "━━━━━━━━━━━━━━\n" +
            $"Tên: {user.Name}\n" +
            $"Ngày bắt đầu: {user.StartDate:dd/MM/yyyy}\n" +
            "Trạng thái: Đang nhận thông báo\n" +
            $"Số ngày làm việc tại EHC: {days} ngày\n" +
            "━━━━━━━━━━━━━━\n\n" +
            $"☀️ Chúc mừng bạn đã làm việc tại EHC được {days} ngày!\n" +
            "Chúc bạn một ngày làm việc hiệu quả! 💪\n\n" +
            "Bạn có thể dùng:\n" +
            "/check - Xem thông tin và số ngày\n" +
            "/update - Cập nhật lại thông tin";
    }

    // =========================================================
    // WORKING DAYS
    // =========================================================

    private static int CalculateWorkDays(DateTime? startDate)
    {
        if (!startDate.HasValue)
        {
            return 0;
        }

        // Tạm dùng UTC Date ở development.
        // Khi làm daily notification production,
        // ta sẽ chuẩn hóa toàn bộ sang Asia/Ho_Chi_Minh.
        var today = DateTime.UtcNow.Date;

        var days = (today - startDate.Value.Date).Days + 1;

        return Math.Max(days, 1);
    }

    // =========================================================
    // COMMAND CHECK
    // =========================================================

    private static bool IsCommand(
        string text,
        string command)
    {
        var firstPart = text
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(firstPart))
        {
            return false;
        }

        return firstPart.Equals(
            command,
            StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================
    // SEND MESSAGE
    // =========================================================

    private async Task SendMessageAsync(
        long chatId,
        string message)
    {
        await _telegramService.Client.SendTextMessageAsync(
            chatId,
            message);
    }
}