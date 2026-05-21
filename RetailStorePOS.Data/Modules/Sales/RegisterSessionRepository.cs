namespace RetailStorePOS.Data.Modules.Sales;

public sealed class RegisterSessionRepository
{
    private const string CashAdjustmentTypeIn = "IN";
    private const string CashAdjustmentTypeOut = "OUT";
    private readonly SqliteConnectionFactory _factory;

    public RegisterSessionRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public RegisterSession? GetActiveSession()
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, user_id, opening_amount_cents, opening_note, opened_at, closed_at, closing_amount_cents, closing_note
            FROM register_sessions
            WHERE closed_at IS NULL
            LIMIT 1;";

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new RegisterSession
            {
                Id = reader.GetInt64(0),
                UserId = reader.GetInt64(1),
                OpeningAmountCents = reader.GetInt64(2),
                OpeningNote = reader.IsDBNull(3) ? null : reader.GetString(3),
                OpenedAt = reader.GetString(4),
                ClosedAt = reader.IsDBNull(5) ? null : reader.GetString(5),
                ClosingAmountCents = reader.IsDBNull(6) ? null : reader.GetInt64(6),
                ClosingNote = reader.IsDBNull(7) ? null : reader.GetString(7)
            };
        }

        return null;
    }

    public long OpenRegister(long userId, long openingAmountCents, string? openingNote)
    {
        using var connection = _factory.OpenConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            // Ensure no other active session
            using var checkCommand = connection.CreateCommand();
            checkCommand.Transaction = transaction;
            checkCommand.CommandText = "SELECT COUNT(*) FROM register_sessions WHERE closed_at IS NULL;";
            if (Convert.ToInt64(checkCommand.ExecuteScalar()) > 0)
            {
                // Already have an active session, return its ID
                using var findCommand = connection.CreateCommand();
                findCommand.Transaction = transaction;
                findCommand.CommandText = "SELECT id FROM register_sessions WHERE closed_at IS NULL LIMIT 1;";
                var existingId = findCommand.ExecuteScalar();
                transaction.Commit();
                return Convert.ToInt64(existingId);
            }

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
                INSERT INTO register_sessions (user_id, opening_amount_cents, opening_note, opened_at)
                VALUES (@user_id, @opening_amount, @opening_note, @opened_at);
                SELECT last_insert_rowid();";

            command.Parameters.AddWithValue("@user_id", userId);
            command.Parameters.AddWithValue("@opening_amount", openingAmountCents);
            command.Parameters.AddWithValue("@opening_note", (object?)openingNote ?? DBNull.Value);
            command.Parameters.AddWithValue("@opened_at", DateTime.UtcNow.ToString("O"));

            var newId = Convert.ToInt64(command.ExecuteScalar());
            transaction.Commit();
            return newId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
    public RegisterCloseSummary? GetActiveCloseSummary()
    {
        var activeSession = GetActiveSession();
        if (activeSession == null) return null;

        using var connection = _factory.OpenConnection();

        using var summaryCommand = connection.CreateCommand();
        summaryCommand.CommandText = @"
            SELECT COUNT(*), COALESCE(SUM(total_cents), 0)
            FROM sales
            WHERE created_at >= @opened_at;";
        summaryCommand.Parameters.AddWithValue("@opened_at", activeSession.OpenedAt);

        var orderCount = 0;
        long sessionTotal = 0;
        using (var summaryReader = summaryCommand.ExecuteReader())
        {
            if (summaryReader.Read())
            {
                orderCount = summaryReader.GetInt32(0);
                sessionTotal = summaryReader.GetInt64(1);
            }
        }

        // Sum cash sales since opened_at
        using var cashCommand = connection.CreateCommand();
        cashCommand.CommandText = @"
            SELECT COALESCE(SUM(total_cents), 0)
            FROM sales
            WHERE payment_type = 'Cash' COLLATE NOCASE AND created_at >= @opened_at;";
        cashCommand.Parameters.AddWithValue("@opened_at", activeSession.OpenedAt);
        long cashTotal = Convert.ToInt64(cashCommand.ExecuteScalar());

        using var adjustmentCommand = connection.CreateCommand();
        adjustmentCommand.CommandText = @"
            SELECT COALESCE(SUM(
                CASE adjustment_type
                    WHEN 'IN' THEN amount_cents
                    ELSE -amount_cents
                END), 0)
            FROM register_cash_adjustments
            WHERE session_id = @session_id;";
        adjustmentCommand.Parameters.AddWithValue("@session_id", activeSession.Id);
        long netCashAdjustmentTotal = Convert.ToInt64(adjustmentCommand.ExecuteScalar());

        var cashAdjustments = new List<RegisterCashAdjustmentSummary>();
        using var adjustmentListCommand = connection.CreateCommand();
        adjustmentListCommand.CommandText = @"
            SELECT adjustment_type, amount_cents, reason
            FROM register_cash_adjustments
            WHERE session_id = @session_id
            ORDER BY created_at DESC, id DESC;";
        adjustmentListCommand.Parameters.AddWithValue("@session_id", activeSession.Id);

        using (var adjustmentReader = adjustmentListCommand.ExecuteReader())
        {
            while (adjustmentReader.Read())
            {
                cashAdjustments.Add(new RegisterCashAdjustmentSummary
                {
                    IsCashIn = string.Equals(
                        adjustmentReader.GetString(0),
                        CashAdjustmentTypeIn,
                        StringComparison.OrdinalIgnoreCase),
                    AmountCents = adjustmentReader.GetInt64(1),
                    Reason = adjustmentReader.IsDBNull(2) ? string.Empty : adjustmentReader.GetString(2)
                });
            }
        }

        // Sum card sales since opened_at
        using var cardCommand = connection.CreateCommand();
        cardCommand.CommandText = @"
            SELECT COALESCE(SUM(total_cents), 0)
            FROM sales
            WHERE payment_type IN ('Card', 'Credit', 'Debit') COLLATE NOCASE AND created_at >= @opened_at;";
        cardCommand.Parameters.AddWithValue("@opened_at", activeSession.OpenedAt);
        long cardTotal = Convert.ToInt64(cardCommand.ExecuteScalar());

        return new RegisterCloseSummary
        {
            SessionId = activeSession.Id,
            UserId = activeSession.UserId,
            OpenedAt = activeSession.OpenedAt,
            OrderCount = orderCount,
            SessionTotalCents = sessionTotal,
            OpeningAmountCents = activeSession.OpeningAmountCents,
            NetCashAdjustmentCents = netCashAdjustmentTotal,
            ExpectedCashCents = activeSession.OpeningAmountCents + cashTotal + netCashAdjustmentTotal,
            ExpectedCardCents = cardTotal,
            CashAdjustments = cashAdjustments
        };
    }

    public void AddCashAdjustment(long sessionId, long? userId, bool isCashIn, long amountCents, string reason)
    {
        if (amountCents <= 0)
        {
            throw new InvalidOperationException("Enter an amount greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("Enter a reason for this cash adjustment.");
        }

        using var connection = _factory.OpenConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            using var activeSessionCommand = connection.CreateCommand();
            activeSessionCommand.Transaction = transaction;
            activeSessionCommand.CommandText = @"
                SELECT COUNT(*)
                FROM register_sessions
                WHERE id = @session_id AND closed_at IS NULL;";
            activeSessionCommand.Parameters.AddWithValue("@session_id", sessionId);

            if (Convert.ToInt64(activeSessionCommand.ExecuteScalar()) == 0)
            {
                throw new InvalidOperationException("The active register session is no longer available.");
            }

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
                INSERT INTO register_cash_adjustments (
                    session_id,
                    user_id,
                    adjustment_type,
                    amount_cents,
                    reason,
                    created_at)
                VALUES (
                    @session_id,
                    @user_id,
                    @adjustment_type,
                    @amount_cents,
                    @reason,
                    @created_at);";

            command.Parameters.AddWithValue("@session_id", sessionId);
            command.Parameters.AddWithValue("@user_id", (object?)userId ?? DBNull.Value);
            command.Parameters.AddWithValue("@adjustment_type", isCashIn ? CashAdjustmentTypeIn : CashAdjustmentTypeOut);
            command.Parameters.AddWithValue("@amount_cents", amountCents);
            command.Parameters.AddWithValue("@reason", reason.Trim());
            command.Parameters.AddWithValue("@created_at", DateTime.UtcNow.ToString("O"));

            command.ExecuteNonQuery();
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void CloseRegister(long sessionId, long closingAmountCents, string? closingNote)
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE register_sessions
            SET closed_at = @closed_at,
                closing_amount_cents = @closing_amount,
                closing_note = @closing_note
            WHERE id = @id AND closed_at IS NULL;";

        command.Parameters.AddWithValue("@id", sessionId);
        command.Parameters.AddWithValue("@closed_at", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("@closing_amount", closingAmountCents);
        command.Parameters.AddWithValue("@closing_note", (object?)closingNote ?? DBNull.Value);

        var rows = command.ExecuteNonQuery();
        if (rows == 0)
        {
            throw new InvalidOperationException("Failed to close register. The session may already be closed or does not exist.");
        }
    }
}
