using Microsoft.Data.Sqlite;

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
        var hasClosedByUserId = ColumnExists(connection, "register_sessions", "closed_by_user_id");

        using var command = connection.CreateCommand();
        command.CommandText = hasClosedByUserId
            ? @"
            SELECT id, user_id, opening_amount_cents, opening_note, opened_at, closed_at, closing_amount_cents, closing_note, closed_by_user_id
            FROM register_sessions
            WHERE closed_at IS NULL
            LIMIT 1;"
            : @"
            SELECT id, user_id, opening_amount_cents, opening_note, opened_at, closed_at, closing_amount_cents, closing_note
            FROM register_sessions
            WHERE closed_at IS NULL
            LIMIT 1;";

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new RegisterSession
            {
                Id = Convert.ToInt64(reader.GetValue(0)),
                UserId = Convert.ToInt64(reader.GetValue(1)),
                OpeningAmountCents = Convert.ToInt64(reader.GetValue(2)),
                OpeningNote = reader.IsDBNull(3) ? null : reader.GetString(3),
                OpenedAt = reader.GetString(4),
                ClosedAt = reader.IsDBNull(5) ? null : reader.GetString(5),
                ClosingAmountCents = reader.IsDBNull(6) ? null : Convert.ToInt64(reader.GetValue(6)),
                ClosingNote = reader.IsDBNull(7) ? null : reader.GetString(7),
                ClosedByUserId = hasClosedByUserId && !reader.IsDBNull(8) ? Convert.ToInt64(reader.GetValue(8)) : null
            };
        }

        return null;
    }

    public IReadOnlyList<RegisterSessionAssignment> GetSessionAssignments(long sessionId)
    {
        using var connection = _factory.OpenConnection();
        if (!TableExists(connection, "register_session_assignments"))
        {
            return Array.Empty<RegisterSessionAssignment>();
        }

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, session_id, user_id, started_at, ended_at, note
            FROM register_session_assignments
            WHERE session_id = @session_id
            ORDER BY started_at ASC, id ASC;";
        command.Parameters.AddWithValue("@session_id", sessionId);

        using var reader = command.ExecuteReader();
        var assignments = new List<RegisterSessionAssignment>();
        while (reader.Read())
        {
            assignments.Add(new RegisterSessionAssignment
            {
                Id = Convert.ToInt64(reader.GetValue(0)),
                SessionId = Convert.ToInt64(reader.GetValue(1)),
                UserId = Convert.ToInt64(reader.GetValue(2)),
                StartedAt = reader.GetString(3),
                EndedAt = reader.IsDBNull(4) ? null : reader.GetString(4),
                Note = reader.IsDBNull(5) ? null : reader.GetString(5)
            });
        }

        return assignments;
    }

    public RegisterSessionReport? GetActiveSessionReport()
    {
        var activeSession = GetActiveSession();
        return activeSession is null ? null : GetSessionReport(activeSession.Id);
    }

    public IReadOnlyList<RegisterSessionHistoryEntry> GetSessionHistory(int limit = 0)
    {
        using var connection = _factory.OpenConnection();
        var hasClosedByUserId = ColumnExists(connection, "register_sessions", "closed_by_user_id");
        var limitClause = limit > 0 ? "\n            LIMIT @limit" : string.Empty;

        using var command = connection.CreateCommand();
        command.CommandText = hasClosedByUserId
            ? $@"
            SELECT id, user_id, opening_amount_cents, opening_note, opened_at, closed_at, closing_amount_cents, closing_note, closed_by_user_id
            FROM register_sessions
            ORDER BY COALESCE(closed_at, opened_at) DESC, id DESC{limitClause};"
            : $@"
            SELECT id, user_id, opening_amount_cents, opening_note, opened_at, closed_at, closing_amount_cents, closing_note
            FROM register_sessions
            ORDER BY COALESCE(closed_at, opened_at) DESC, id DESC{limitClause};";
        if (limit > 0)
        {
            command.Parameters.AddWithValue("@limit", limit);
        }

        using var reader = command.ExecuteReader();
        var results = new List<RegisterSessionHistoryEntry>();
        while (reader.Read())
        {
            var session = new RegisterSession
            {
                Id = Convert.ToInt64(reader.GetValue(0)),
                UserId = Convert.ToInt64(reader.GetValue(1)),
                OpeningAmountCents = Convert.ToInt64(reader.GetValue(2)),
                OpeningNote = reader.IsDBNull(3) ? null : reader.GetString(3),
                OpenedAt = reader.GetString(4),
                ClosedAt = reader.IsDBNull(5) ? null : reader.GetString(5),
                ClosingAmountCents = reader.IsDBNull(6) ? null : Convert.ToInt64(reader.GetValue(6)),
                ClosingNote = reader.IsDBNull(7) ? null : reader.GetString(7),
                ClosedByUserId = hasClosedByUserId && !reader.IsDBNull(8) ? Convert.ToInt64(reader.GetValue(8)) : null
            };

            results.Add(BuildSessionHistoryEntry(connection, session));
        }

        return results;
    }

    public RegisterPeriodReport GetPeriodReport(DateTime periodStartLocal, DateTime periodEndLocal)
    {
        var startLocal = periodStartLocal.Date;
        var endLocal = periodEndLocal.Date;
        if (endLocal < startLocal)
        {
            (startLocal, endLocal) = (endLocal, startLocal);
        }

        var startUtc = startLocal.ToUniversalTime().ToString("O");
        var endUtc = endLocal.AddDays(1).ToUniversalTime().ToString("O");
        using var connection = _factory.OpenConnection();

        var report = new RegisterPeriodReport
        {
            PeriodStartLocal = startLocal,
            PeriodEndLocal = endLocal
        };

        using (var sessionCommand = connection.CreateCommand())
        {
            sessionCommand.CommandText = @"
                SELECT
                    COUNT(*),
                    COALESCE(SUM(CASE WHEN closed_at IS NULL THEN 1 ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN closed_at IS NOT NULL THEN 1 ELSE 0 END), 0),
                    COALESCE(SUM(opening_amount_cents), 0),
                    COALESCE(SUM(CASE WHEN closing_amount_cents IS NOT NULL THEN closing_amount_cents ELSE 0 END), 0)
                FROM register_sessions
                WHERE COALESCE(closed_at, opened_at) >= @start_utc
                  AND COALESCE(closed_at, opened_at) < @end_utc;";
            sessionCommand.Parameters.AddWithValue("@start_utc", startUtc);
            sessionCommand.Parameters.AddWithValue("@end_utc", endUtc);

            using var reader = sessionCommand.ExecuteReader();
            if (reader.Read())
            {
                report.SessionCount = Convert.ToInt32(Convert.ToInt64(reader.GetValue(0)));
                report.OpenSessionCount = Convert.ToInt32(Convert.ToInt64(reader.GetValue(1)));
                report.ClosedSessionCount = Convert.ToInt32(Convert.ToInt64(reader.GetValue(2)));
                report.OpeningFloatCents = Convert.ToInt64(reader.GetValue(3));
                report.CountedCashCents = Convert.ToInt64(reader.GetValue(4));
            }
        }

        using (var salesCommand = connection.CreateCommand())
        {
            salesCommand.CommandText = @"
                SELECT
                    COUNT(*),
                    COALESCE(SUM(total_cents), 0),
                    COALESCE(SUM(tax_cents), 0),
                    COALESCE(SUM(CASE WHEN UPPER(payment_type) = 'CASH' THEN total_cents ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN UPPER(payment_type) IN ('CARD', 'CREDIT', 'DEBIT') THEN total_cents ELSE 0 END), 0)
                FROM sales
                WHERE created_at >= @start_utc
                  AND created_at < @end_utc;";
            salesCommand.Parameters.AddWithValue("@start_utc", startUtc);
            salesCommand.Parameters.AddWithValue("@end_utc", endUtc);

            using var reader = salesCommand.ExecuteReader();
            if (reader.Read())
            {
                report.SaleCount = Convert.ToInt32(Convert.ToInt64(reader.GetValue(0)));
                report.GrossSalesCents = Convert.ToInt64(reader.GetValue(1));
                report.TaxCents = Convert.ToInt64(reader.GetValue(2));
                report.CashSalesCents = Convert.ToInt64(reader.GetValue(3));
                report.CardSalesCents = Convert.ToInt64(reader.GetValue(4));
            }
        }

        using (var adjustmentCommand = connection.CreateCommand())
        {
            adjustmentCommand.CommandText = @"
                SELECT COALESCE(SUM(CASE WHEN adjustment_type = @cash_in THEN amount_cents ELSE -amount_cents END), 0)
                FROM register_cash_adjustments
                WHERE created_at >= @start_utc
                  AND created_at < @end_utc;";
            adjustmentCommand.Parameters.AddWithValue("@cash_in", CashAdjustmentTypeIn);
            adjustmentCommand.Parameters.AddWithValue("@start_utc", startUtc);
            adjustmentCommand.Parameters.AddWithValue("@end_utc", endUtc);
            report.NetCashAdjustmentCents = Convert.ToInt64(adjustmentCommand.ExecuteScalar() ?? 0);
        }

        report.ExpectedCashCents = report.OpeningFloatCents + report.CashSalesCents + report.NetCashAdjustmentCents;
        report.CashVarianceCents = report.CountedCashCents - report.ExpectedCashCents;
        report.CashierSummaries = GetPeriodCashierSummaries(connection, startUtc, endUtc);
        return report;
    }

    public RegisterSessionReport? GetSessionReport(long sessionId)
    {
        using var connection = _factory.OpenConnection();
        var session = GetSession(connection, sessionId);
        if (session is null)
        {
            return null;
        }

        var sessionSalesClause = BuildSessionSalesClause(connection, session);
        var report = new RegisterSessionReport
        {
            SessionId = session.Id,
            OpenedByUserId = session.UserId,
            ClosedByUserId = session.ClosedByUserId,
            OpenedAt = session.OpenedAt,
            ClosedAt = session.ClosedAt,
            OpeningAmountCents = session.OpeningAmountCents,
            ClosingAmountCents = session.ClosingAmountCents,
            OpeningNote = session.OpeningNote,
            ClosingNote = session.ClosingNote
        };

        using (var totalsCommand = connection.CreateCommand())
        {
            totalsCommand.CommandText = $@"
                SELECT
                    COUNT(*),
                    COALESCE(SUM(total_cents), 0),
                    COALESCE(SUM(tax_cents), 0),
                    COALESCE(SUM(CASE WHEN payment_type = 'Cash' COLLATE NOCASE THEN total_cents ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN payment_type IN ('Card', 'Credit', 'Debit') COLLATE NOCASE THEN total_cents ELSE 0 END), 0)
                FROM sales
                WHERE {sessionSalesClause};";
            AddSessionSalesParameters(totalsCommand, session);

            using var reader = totalsCommand.ExecuteReader();
            if (reader.Read())
            {
                report.SaleCount = Convert.ToInt32(Convert.ToInt64(reader.GetValue(0)));
                report.GrossSalesCents = Convert.ToInt64(reader.GetValue(1));
                report.TaxCents = Convert.ToInt64(reader.GetValue(2));
                report.CashSalesCents = Convert.ToInt64(reader.GetValue(3));
                report.CardSalesCents = Convert.ToInt64(reader.GetValue(4));
            }
        }

        using (var adjustmentCommand = connection.CreateCommand())
        {
            adjustmentCommand.CommandText = @"
                SELECT COALESCE(SUM(
                    CASE adjustment_type
                        WHEN 'IN' THEN amount_cents
                        ELSE -amount_cents
                    END), 0)
                FROM register_cash_adjustments
                WHERE session_id = @session_id;";
            adjustmentCommand.Parameters.AddWithValue("@session_id", sessionId);
            report.NetCashAdjustmentCents = Convert.ToInt64(adjustmentCommand.ExecuteScalar());
        }

        report.ExpectedCashCents = report.OpeningAmountCents + report.CashSalesCents + report.NetCashAdjustmentCents;

        if (TableExists(connection, "register_session_assignments"))
        {
            using var intervalsCommand = connection.CreateCommand();
            intervalsCommand.CommandText = @"
                SELECT user_id, started_at, ended_at, note
                FROM register_session_assignments
                WHERE session_id = @session_id
                ORDER BY started_at ASC, id ASC;";
            intervalsCommand.Parameters.AddWithValue("@session_id", sessionId);

            using var intervalReader = intervalsCommand.ExecuteReader();
            while (intervalReader.Read())
            {
                report.CashierIntervals.Add(new RegisterSessionCashierInterval
                {
                    UserId = Convert.ToInt64(intervalReader.GetValue(0)),
                    StartedAt = intervalReader.GetString(1),
                    EndedAt = intervalReader.IsDBNull(2) ? null : intervalReader.GetString(2),
                    Note = intervalReader.IsDBNull(3) ? null : intervalReader.GetString(3)
                });
            }
        }

        using (var cashierSummaryCommand = connection.CreateCommand())
        {
            cashierSummaryCommand.CommandText = $@"
                SELECT
                    cashier_user_id,
                    COALESCE(NULLIF(TRIM(cashier_name), ''), '(unspecified)') AS cashier_name,
                    COUNT(*),
                    COALESCE(SUM(total_cents), 0),
                    COALESCE(SUM(CASE WHEN payment_type = 'Cash' COLLATE NOCASE THEN total_cents ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN payment_type IN ('Card', 'Credit', 'Debit') COLLATE NOCASE THEN total_cents ELSE 0 END), 0)
                FROM sales
                WHERE {sessionSalesClause}
                GROUP BY cashier_user_id, cashier_name
                ORDER BY SUM(total_cents) DESC, cashier_name ASC;";
            AddSessionSalesParameters(cashierSummaryCommand, session);

            using var cashierReader = cashierSummaryCommand.ExecuteReader();
            while (cashierReader.Read())
            {
                report.CashierSummaries.Add(new RegisterSessionCashierSummary
                {
                    CashierUserId = cashierReader.IsDBNull(0) ? null : Convert.ToInt64(cashierReader.GetValue(0)),
                    CashierName = cashierReader.IsDBNull(1) ? string.Empty : cashierReader.GetString(1),
                    SaleCount = Convert.ToInt32(cashierReader.GetValue(2)),
                    GrossSalesCents = Convert.ToInt64(cashierReader.GetValue(3)),
                    CashSalesCents = Convert.ToInt64(cashierReader.GetValue(4)),
                    CardSalesCents = Convert.ToInt64(cashierReader.GetValue(5))
                });
            }
        }

        using (var adjustmentListCommand = connection.CreateCommand())
        {
            adjustmentListCommand.CommandText = @"
                SELECT user_id, adjustment_type, amount_cents, reason, created_at
                FROM register_cash_adjustments
                WHERE session_id = @session_id
                ORDER BY created_at DESC, id DESC;";
            adjustmentListCommand.Parameters.AddWithValue("@session_id", sessionId);

            using var adjustmentReader = adjustmentListCommand.ExecuteReader();
            while (adjustmentReader.Read())
            {
                report.CashAdjustments.Add(new RegisterSessionAdjustmentEntry
                {
                    UserId = adjustmentReader.IsDBNull(0) ? null : Convert.ToInt64(adjustmentReader.GetValue(0)),
                    IsCashIn = string.Equals(adjustmentReader.GetString(1), CashAdjustmentTypeIn, StringComparison.OrdinalIgnoreCase),
                    AmountCents = Convert.ToInt64(adjustmentReader.GetValue(2)),
                    Reason = adjustmentReader.IsDBNull(3) ? string.Empty : adjustmentReader.GetString(3),
                    CreatedAt = adjustmentReader.GetString(4)
                });
            }
        }

        return report;
    }

    public long OpenRegister(long userId, long openingAmountCents, string? openingNote)
    {
        using var connection = _factory.OpenConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            using var checkCommand = connection.CreateCommand();
            checkCommand.Transaction = transaction;
            checkCommand.CommandText = "SELECT COUNT(*) FROM register_sessions WHERE closed_at IS NULL;";
            if (Convert.ToInt64(checkCommand.ExecuteScalar()) > 0)
            {
                using var findCommand = connection.CreateCommand();
                findCommand.Transaction = transaction;
                findCommand.CommandText = "SELECT id FROM register_sessions WHERE closed_at IS NULL LIMIT 1;";
                var existingId = Convert.ToInt64(findCommand.ExecuteScalar());
                EnsureCashierAssignment(connection, transaction, existingId, userId, openingNote);
                transaction.Commit();
                return existingId;
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
            EnsureCashierAssignment(connection, transaction, newId, userId, openingNote);
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
        if (activeSession == null)
        {
            return null;
        }

        using var connection = _factory.OpenConnection();
        var sessionSalesClause = BuildSessionSalesClause(connection, activeSession);

        using var summaryCommand = connection.CreateCommand();
        summaryCommand.CommandText = $@"
            SELECT COUNT(*), COALESCE(SUM(total_cents), 0)
            FROM sales
            WHERE {sessionSalesClause};";
        AddSessionSalesParameters(summaryCommand, activeSession);

        var orderCount = 0;
        long sessionTotal = 0;
        using (var summaryReader = summaryCommand.ExecuteReader())
        {
            if (summaryReader.Read())
            {
                orderCount = Convert.ToInt32(summaryReader.GetValue(0));
                sessionTotal = Convert.ToInt64(summaryReader.GetValue(1));
            }
        }

        using var cashCommand = connection.CreateCommand();
        cashCommand.CommandText = $@"
            SELECT COALESCE(SUM(total_cents), 0)
            FROM sales
            WHERE payment_type = 'Cash' COLLATE NOCASE
              AND {sessionSalesClause};";
        AddSessionSalesParameters(cashCommand, activeSession);
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
                    AmountCents = Convert.ToInt64(adjustmentReader.GetValue(1)),
                    Reason = adjustmentReader.IsDBNull(2) ? string.Empty : adjustmentReader.GetString(2)
                });
            }
        }

        using var cardCommand = connection.CreateCommand();
        cardCommand.CommandText = $@"
            SELECT COALESCE(SUM(total_cents), 0)
            FROM sales
            WHERE payment_type IN ('Card', 'Credit', 'Debit') COLLATE NOCASE
              AND {sessionSalesClause};";
        AddSessionSalesParameters(cardCommand, activeSession);
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
            EnsureActiveSessionExists(connection, transaction, sessionId);
            EnsureCashierAssignment(connection, transaction, sessionId, userId, null);

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

    public void EnsureCashierAssignment(long sessionId, long? userId, string? note = null)
    {
        if (!userId.HasValue || userId.Value <= 0)
        {
            return;
        }

        using var connection = _factory.OpenConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            EnsureActiveSessionExists(connection, transaction, sessionId);
            EnsureCashierAssignment(connection, transaction, sessionId, userId, note);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void CloseActiveCashierAssignments(long sessionId)
    {
        using var connection = _factory.OpenConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            EnsureActiveSessionExists(connection, transaction, sessionId);
            CloseOpenAssignments(connection, transaction, sessionId, DateTime.UtcNow.ToString("O"));
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void CloseRegister(long sessionId, long closingAmountCents, string? closingNote, long? closedByUserId = null)
    {
        using var connection = _factory.OpenConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            EnsureActiveSessionExists(connection, transaction, sessionId);

            var closedAtUtc = DateTime.UtcNow.ToString("O");
            CloseOpenAssignments(connection, transaction, sessionId, closedAtUtc);

            var hasClosedByUserId = ColumnExists(connection, "register_sessions", "closed_by_user_id");
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = hasClosedByUserId
                ? @"
            UPDATE register_sessions
            SET closed_at = @closed_at,
                closing_amount_cents = @closing_amount,
                closing_note = @closing_note,
                closed_by_user_id = @closed_by_user_id
            WHERE id = @id AND closed_at IS NULL;"
                : @"
            UPDATE register_sessions
            SET closed_at = @closed_at,
                closing_amount_cents = @closing_amount,
                closing_note = @closing_note
            WHERE id = @id AND closed_at IS NULL;";

            command.Parameters.AddWithValue("@id", sessionId);
            command.Parameters.AddWithValue("@closed_at", closedAtUtc);
            command.Parameters.AddWithValue("@closing_amount", closingAmountCents);
            command.Parameters.AddWithValue("@closing_note", (object?)closingNote ?? DBNull.Value);
            if (hasClosedByUserId)
            {
                command.Parameters.AddWithValue("@closed_by_user_id", (object?)closedByUserId ?? DBNull.Value);
            }

            var rows = command.ExecuteNonQuery();
            if (rows == 0)
            {
                throw new InvalidOperationException("Failed to close register. The session may already be closed or does not exist.");
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static RegisterSession? GetSession(SqliteConnection connection, long sessionId)
    {
        var hasClosedByUserId = ColumnExists(connection, "register_sessions", "closed_by_user_id");

        using var command = connection.CreateCommand();
        command.CommandText = hasClosedByUserId
            ? @"
            SELECT id, user_id, opening_amount_cents, opening_note, opened_at, closed_at, closing_amount_cents, closing_note, closed_by_user_id
            FROM register_sessions
            WHERE id = @session_id
            LIMIT 1;"
            : @"
            SELECT id, user_id, opening_amount_cents, opening_note, opened_at, closed_at, closing_amount_cents, closing_note
            FROM register_sessions
            WHERE id = @session_id
            LIMIT 1;";
        command.Parameters.AddWithValue("@session_id", sessionId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new RegisterSession
        {
            Id = Convert.ToInt64(reader.GetValue(0)),
            UserId = Convert.ToInt64(reader.GetValue(1)),
            OpeningAmountCents = Convert.ToInt64(reader.GetValue(2)),
            OpeningNote = reader.IsDBNull(3) ? null : reader.GetString(3),
            OpenedAt = reader.GetString(4),
            ClosedAt = reader.IsDBNull(5) ? null : reader.GetString(5),
            ClosingAmountCents = reader.IsDBNull(6) ? null : Convert.ToInt64(reader.GetValue(6)),
            ClosingNote = reader.IsDBNull(7) ? null : reader.GetString(7),
            ClosedByUserId = hasClosedByUserId && !reader.IsDBNull(8) ? Convert.ToInt64(reader.GetValue(8)) : null
        };
    }

    private static RegisterSessionHistoryEntry BuildSessionHistoryEntry(SqliteConnection connection, RegisterSession session)
    {
        var sessionSalesClause = BuildSessionSalesClause(connection, session);

        using var totalsCommand = connection.CreateCommand();
        totalsCommand.CommandText = $@"
            SELECT
                COUNT(*),
                COALESCE(SUM(total_cents), 0)
            FROM sales
            WHERE {sessionSalesClause};";
        AddSessionSalesParameters(totalsCommand, session);

        using var reader = totalsCommand.ExecuteReader();
        var saleCount = 0;
        long grossSalesCents = 0;
        if (reader.Read())
        {
            saleCount = Convert.ToInt32(Convert.ToInt64(reader.GetValue(0)));
            grossSalesCents = Convert.ToInt64(reader.GetValue(1));
        }

        return new RegisterSessionHistoryEntry
        {
            SessionId = session.Id,
            OpenedByUserId = session.UserId,
            ClosedByUserId = session.ClosedByUserId,
            OpenedAt = session.OpenedAt,
            ClosedAt = session.ClosedAt,
            OpeningAmountCents = session.OpeningAmountCents,
            ClosingAmountCents = session.ClosingAmountCents,
            SaleCount = saleCount,
            GrossSalesCents = grossSalesCents
        };
    }

    private static List<RegisterSessionCashierSummary> GetPeriodCashierSummaries(SqliteConnection connection, string startUtc, string endUtc)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT
                cashier_user_id,
                cashier_name,
                COUNT(*),
                COALESCE(SUM(total_cents), 0),
                COALESCE(SUM(CASE WHEN UPPER(payment_type) = 'CASH' THEN total_cents ELSE 0 END), 0),
                COALESCE(SUM(CASE WHEN UPPER(payment_type) IN ('CARD', 'CREDIT', 'DEBIT') THEN total_cents ELSE 0 END), 0)
            FROM sales
            WHERE created_at >= @start_utc
              AND created_at < @end_utc
            GROUP BY cashier_user_id, cashier_name
            ORDER BY COALESCE(SUM(total_cents), 0) DESC;";
        command.Parameters.AddWithValue("@start_utc", startUtc);
        command.Parameters.AddWithValue("@end_utc", endUtc);

        using var reader = command.ExecuteReader();
        var results = new List<RegisterSessionCashierSummary>();
        while (reader.Read())
        {
            results.Add(new RegisterSessionCashierSummary
            {
                CashierUserId = reader.IsDBNull(0) ? null : Convert.ToInt64(reader.GetValue(0)),
                CashierName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                SaleCount = Convert.ToInt32(Convert.ToInt64(reader.GetValue(2))),
                GrossSalesCents = Convert.ToInt64(reader.GetValue(3)),
                CashSalesCents = Convert.ToInt64(reader.GetValue(4)),
                CardSalesCents = Convert.ToInt64(reader.GetValue(5))
            });
        }

        return results;
    }

    private static string BuildSessionSalesClause(SqliteConnection connection, RegisterSession session)
    {
        return ColumnExists(connection, "sales", "register_session_id")
            ? session.ClosedAt is null
                ? "(register_session_id = @session_id OR (register_session_id IS NULL AND created_at >= @opened_at))"
                : "(register_session_id = @session_id OR (register_session_id IS NULL AND created_at >= @opened_at AND created_at < @closed_at))"
            : session.ClosedAt is null
                ? "created_at >= @opened_at"
                : "created_at >= @opened_at AND created_at < @closed_at";
    }

    private static void AddSessionSalesParameters(SqliteCommand command, RegisterSession session)
    {
        command.Parameters.AddWithValue("@session_id", session.Id);
        command.Parameters.AddWithValue("@opened_at", session.OpenedAt);
        if (session.ClosedAt is not null)
        {
            command.Parameters.AddWithValue("@closed_at", session.ClosedAt);
        }
    }

    private static void EnsureActiveSessionExists(SqliteConnection connection, SqliteTransaction transaction, long sessionId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
            SELECT COUNT(*)
            FROM register_sessions
            WHERE id = @session_id AND closed_at IS NULL;";
        command.Parameters.AddWithValue("@session_id", sessionId);

        if (Convert.ToInt64(command.ExecuteScalar()) == 0)
        {
            throw new InvalidOperationException("The active register session is no longer available.");
        }
    }

    private static void EnsureCashierAssignment(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long sessionId,
        long? userId,
        string? note)
    {
        if (!userId.HasValue || userId.Value <= 0 || !TableExists(connection, "register_session_assignments"))
        {
            return;
        }

        using var activeAssignmentCommand = connection.CreateCommand();
        activeAssignmentCommand.Transaction = transaction;
        activeAssignmentCommand.CommandText = @"
            SELECT id, user_id
            FROM register_session_assignments
            WHERE session_id = @session_id AND ended_at IS NULL
            ORDER BY started_at DESC, id DESC
            LIMIT 1;";
        activeAssignmentCommand.Parameters.AddWithValue("@session_id", sessionId);

        using var reader = activeAssignmentCommand.ExecuteReader();
        if (reader.Read())
        {
            var currentUserId = Convert.ToInt64(reader.GetValue(1));
            if (currentUserId == userId.Value)
            {
                return;
            }
        }
        reader.Close();

        var nowUtc = DateTime.UtcNow.ToString("O");
        CloseOpenAssignments(connection, transaction, sessionId, nowUtc);

        using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText = @"
            INSERT INTO register_session_assignments (session_id, user_id, started_at, note)
            VALUES (@session_id, @user_id, @started_at, @note);";
        insertCommand.Parameters.AddWithValue("@session_id", sessionId);
        insertCommand.Parameters.AddWithValue("@user_id", userId.Value);
        insertCommand.Parameters.AddWithValue("@started_at", nowUtc);
        insertCommand.Parameters.AddWithValue("@note", string.IsNullOrWhiteSpace(note) ? DBNull.Value : note.Trim());
        insertCommand.ExecuteNonQuery();
    }

    private static void CloseOpenAssignments(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long sessionId,
        string closedAtUtc)
    {
        if (!TableExists(connection, "register_session_assignments"))
        {
            return;
        }

        using var closeAssignmentsCommand = connection.CreateCommand();
        closeAssignmentsCommand.Transaction = transaction;
        closeAssignmentsCommand.CommandText = @"
            UPDATE register_session_assignments
            SET ended_at = @ended_at
            WHERE session_id = @session_id AND ended_at IS NULL;";
        closeAssignmentsCommand.Parameters.AddWithValue("@session_id", sessionId);
        closeAssignmentsCommand.Parameters.AddWithValue("@ended_at", closedAtUtc);
        closeAssignmentsCommand.ExecuteNonQuery();
    }

    private static bool ColumnExists(SqliteConnection connection, string tableName, string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table' AND name = @table_name;";
        command.Parameters.AddWithValue("@table_name", tableName);
        return Convert.ToInt64(command.ExecuteScalar()) > 0;
    }
}
