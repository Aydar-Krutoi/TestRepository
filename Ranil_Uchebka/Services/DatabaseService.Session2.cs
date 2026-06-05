using Npgsql;
using NpgsqlTypes;
using Ranil_Uchebka.Models;

namespace Ranil_Uchebka.Services;

public partial class DatabaseService
{
    public async Task<IReadOnlyDictionary<string, long>> GetOrderStatusMapAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT name, status_id FROM order_status";
        var map = new Dictionary<string, long>(StringComparer.Ordinal);
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            map[reader.GetString(0)] = reader.GetInt64(1);
        }

        return map;
    }

    public async Task<IReadOnlyList<OrderListItem>> GetOrdersAsync(
        string role,
        string login,
        string? statusFilter,
        CancellationToken ct = default)
    {
        var statuses = statusFilter is null
            ? OrderWorkflowService.AllStatuses
            : OrderWorkflowService.StatusesForFilter(statusFilter);

        var sql = """
                  SELECT
                      o.number,
                      o.order_date,
                      COALESCE(o.order_code, ''),
                      o.order_name,
                      s.name,
                      s.status_id,
                      o.cost,
                      o.customer_login,
                      COALESCE(cu.full_name, o.customer_login),
                      o.planned_completion_date,
                      o.manager_login,
                      mu.full_name,
                      o.product_name
                  FROM customer_order o
                  JOIN order_status s ON s.status_id = o.current_status_id
                  LEFT JOIN app_user cu ON cu.login = o.customer_login
                  LEFT JOIN app_user mu ON mu.login = o.manager_login
                  WHERE s.name = ANY(@statuses)
                  """;

        if (role == "Заказчик")
        {
            sql += " AND o.customer_login = @login";
        }
        else if (role == "Менеджер")
        {
            sql += """
                     AND (
                         s.name = 'Новый'
                         OR o.manager_login = @login
                         OR EXISTS (
                             SELECT 1
                             FROM order_status_history h
                             WHERE h.order_number = o.number
                               AND h.order_date = o.order_date
                               AND h.changed_by_login = @login
                         )
                     )
                     """;
        }
        else if (role == "Конструктор")
        {
            sql += " AND s.name = 'Составление спецификации'";
        }
        else if (role == "Мастер")
        {
            sql += " AND s.name IN ('Производство', 'Контроль')";
        }

        sql += " ORDER BY o.order_date DESC, o.number DESC";

        var result = new List<OrderListItem>();
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("statuses", statuses);
        if (role is "Заказчик" or "Менеджер")
        {
            cmd.Parameters.AddWithValue("login", login);
        }

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new OrderListItem
            {
                Number = reader.GetInt32(0),
                OrderDate = reader.GetDateTime(1),
                OrderCode = reader.GetString(2),
                OrderName = reader.GetString(3),
                StatusName = reader.GetString(4),
                StatusId = reader.GetInt64(5),
                Cost = reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                CustomerLogin = reader.GetString(7),
                CustomerName = reader.GetString(8),
                PlannedCompletionDate = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                ManagerLogin = reader.IsDBNull(10) ? null : reader.GetString(10),
                ManagerName = reader.IsDBNull(11) ? null : reader.GetString(11),
                ProductName = reader.GetString(12)
            });
        }

        return result;
    }

    public async Task<OrderEditorState?> GetOrderEditorAsync(int number, DateTime orderDate, CancellationToken ct = default)
    {
        const string sql = """
                           SELECT
                               o.order_code, o.order_name, o.product_name, o.customer_login,
                               o.manager_login, o.cost, o.planned_completion_date, o.description,
                               s.name, s.status_id
                           FROM customer_order o
                           JOIN order_status s ON s.status_id = o.current_status_id
                           WHERE o.number = @number AND o.order_date = @order_date
                           """;
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("number", number);
        cmd.Parameters.AddWithValue("order_date", orderDate.Date);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        var state = new OrderEditorState
        {
            Number = number,
            OrderDate = orderDate.Date,
            OrderCode = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
            OrderName = reader.GetString(1),
            ProductName = reader.GetString(2),
            CustomerLogin = reader.GetString(3),
            ManagerLogin = reader.IsDBNull(4) ? null : reader.GetString(4),
            Cost = reader.IsDBNull(5) ? null : reader.GetDecimal(5),
            PlannedCompletionDate = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
            Description = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
            StatusName = reader.GetString(8),
            StatusId = reader.GetInt64(9),
            IsNew = false
        };

        await reader.CloseAsync();

        const string dimSql = """
                              SELECT dimension_id, dimension_name, unit, dimension_value
                              FROM order_dimension
                              WHERE order_number = @number AND order_date = @order_date
                              """;
        await using (var dimCmd = new NpgsqlCommand(dimSql, con))
        {
            dimCmd.Parameters.AddWithValue("number", number);
            dimCmd.Parameters.AddWithValue("order_date", orderDate.Date);
            await using var dimReader = await dimCmd.ExecuteReaderAsync(ct);
            while (await dimReader.ReadAsync(ct))
            {
                state.Dimensions.Add(new OrderDimensionRow
                {
                    DimensionId = dimReader.GetInt64(0),
                    Name = dimReader.GetString(1),
                    Unit = dimReader.GetString(2),
                    Value = dimReader.GetDecimal(3)
                });
            }
        }

        const string attSql = """
                              SELECT attachment_id, file_name
                              FROM order_attachment
                              WHERE order_number = @number AND order_date = @order_date
                              ORDER BY attachment_id
                              """;
        await using (var attCmd = new NpgsqlCommand(attSql, con))
        {
            attCmd.Parameters.AddWithValue("number", number);
            attCmd.Parameters.AddWithValue("order_date", orderDate.Date);
            await using var attReader = await attCmd.ExecuteReaderAsync(ct);
            while (await attReader.ReadAsync(ct))
            {
                state.Attachments.Add(new OrderAttachmentRow
                {
                    AttachmentId = attReader.GetInt64(0),
                    FileName = attReader.GetString(1)
                });
            }
        }

        return state;
    }

    public async Task<IReadOnlyList<OrderAttachmentRow>> GetOrderAttachmentsAsync(
        int number,
        DateTime orderDate,
        CancellationToken ct = default)
    {
        const string sql = """
                           SELECT attachment_id, file_name
                           FROM order_attachment
                           WHERE order_number = @number AND order_date = @order_date
                           ORDER BY attachment_id
                           """;
        var result = new List<OrderAttachmentRow>();
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("number", number);
        cmd.Parameters.AddWithValue("order_date", orderDate.Date);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new OrderAttachmentRow
            {
                AttachmentId = reader.GetInt64(0),
                FileName = reader.GetString(1)
            });
        }

        return result;
    }

    public async Task DeleteOrderAttachmentAsync(
        long attachmentId,
        int number,
        DateTime orderDate,
        CancellationToken ct = default)
    {
        const string sql = """
                           DELETE FROM order_attachment
                           WHERE attachment_id = @id
                             AND order_number = @number
                             AND order_date = @order_date
                           """;
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("id", attachmentId);
        cmd.Parameters.AddWithValue("number", number);
        cmd.Parameters.AddWithValue("order_date", orderDate.Date);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<byte[]?> GetOrderAttachmentDataAsync(
        long attachmentId,
        int number,
        DateTime orderDate,
        CancellationToken ct = default)
    {
        const string sql = """
                           SELECT file_data
                           FROM order_attachment
                           WHERE attachment_id = @id
                             AND order_number = @number
                             AND order_date = @order_date
                           """;
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("id", attachmentId);
        cmd.Parameters.AddWithValue("number", number);
        cmd.Parameters.AddWithValue("order_date", orderDate.Date);
        var bytes = await cmd.ExecuteScalarAsync(ct);
        return bytes as byte[];
    }

    public async Task<string> GenerateOrderCodeAsync(string customerLogin, DateTime orderDate, CancellationToken ct = default)
    {
        const string userSql = "SELECT full_name FROM app_user WHERE login = @login";
        const string countSql = """
                                SELECT COUNT(*) FROM customer_order
                                WHERE customer_login = @login
                                """;
        await using var con = CreateConnection();
        await con.OpenAsync(ct);

        string fullName = customerLogin;
        await using (var userCmd = new NpgsqlCommand(userSql, con))
        {
            userCmd.Parameters.AddWithValue("login", customerLogin);
            var nameObj = await userCmd.ExecuteScalarAsync(ct);
            if (nameObj is string s && !string.IsNullOrWhiteSpace(s))
            {
                fullName = s;
            }
        }

        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var surnameChar = parts.Length > 0 && parts[0].Length > 0 ? char.ToUpper(parts[0][0]) : '_';
        var nameChar = parts.Length > 1 && parts[1].Length > 0 ? char.ToUpper(parts[1][0]) : '_';

        int seq;
        await using (var countCmd = new NpgsqlCommand(countSql, con))
        {
            countCmd.Parameters.AddWithValue("login", customerLogin);
            seq = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct)) + 1;
        }

        if (seq > 99)
        {
            seq = ((seq - 1) % 99) + 1;
        }

        return $"{surnameChar}{nameChar}{orderDate:yyyyMMdd}{seq:00}";
    }

    public async Task<int> GetNextOrderNumberAsync(DateTime orderDate, CancellationToken ct = default)
    {
        const string sql = """
                           SELECT COALESCE(MAX(number), 0) + 1
                           FROM customer_order
                           WHERE order_date = @order_date
                           """;
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("order_date", orderDate.Date);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task SaveOrderAsync(
        OrderEditorState order,
        string changedByLogin,
        IReadOnlyList<(string fileName, byte[] data)> newAttachments,
        CancellationToken ct = default)
    {
        var statusMap = await GetOrderStatusMapAsync(ct);
        var statusId = statusMap[order.StatusName];

        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var tx = await con.BeginTransactionAsync(ct);

        try
        {
            if (order.IsNew)
            {
                const string insertSql = """
                                         INSERT INTO customer_order (
                                             number, order_date, order_name, product_name, customer_login,
                                             manager_login, cost, planned_completion_date, order_code,
                                             description, current_status_id)
                                         VALUES (
                                             @number, @order_date, @order_name, @product_name, @customer_login,
                                             @manager_login, @cost, @planned_completion_date, @order_code,
                                             @description, @status_id)
                                         """;
                await using var insertCmd = new NpgsqlCommand(insertSql, con, tx);
                insertCmd.Parameters.AddWithValue("number", order.Number);
                insertCmd.Parameters.AddWithValue("order_date", order.OrderDate.Date);
                insertCmd.Parameters.AddWithValue("order_name", order.OrderName);
                insertCmd.Parameters.AddWithValue("product_name", order.ProductName);
                insertCmd.Parameters.AddWithValue("customer_login", order.CustomerLogin);
                insertCmd.Parameters.AddWithValue("manager_login", (object?)order.ManagerLogin ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("cost", (object?)order.Cost ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("planned_completion_date", (object?)order.PlannedCompletionDate?.Date ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("order_code", order.OrderCode);
                insertCmd.Parameters.AddWithValue("description", order.Description);
                insertCmd.Parameters.AddWithValue("status_id", statusId);
                await insertCmd.ExecuteNonQueryAsync(ct);

                await AddStatusHistoryAsync(con, tx, order.Number, order.OrderDate, statusId, changedByLogin, null, ct);
            }
            else
            {
                const string updateSql = """
                                         UPDATE customer_order
                                         SET order_name = @order_name,
                                             cost = @cost,
                                             planned_completion_date = @planned_completion_date,
                                             description = @description
                                         WHERE number = @number AND order_date = @order_date
                                         """;
                await using var updateCmd = new NpgsqlCommand(updateSql, con, tx);
                updateCmd.Parameters.AddWithValue("order_name", order.OrderName);
                updateCmd.Parameters.AddWithValue("cost", (object?)order.Cost ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("planned_completion_date", (object?)order.PlannedCompletionDate?.Date ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("description", order.Description);
                updateCmd.Parameters.AddWithValue("number", order.Number);
                updateCmd.Parameters.AddWithValue("order_date", order.OrderDate.Date);
                await updateCmd.ExecuteNonQueryAsync(ct);
            }

            const string deleteDims = """
                                      DELETE FROM order_dimension
                                      WHERE order_number = @number AND order_date = @order_date
                                      """;
            await using (var delCmd = new NpgsqlCommand(deleteDims, con, tx))
            {
                delCmd.Parameters.AddWithValue("number", order.Number);
                delCmd.Parameters.AddWithValue("order_date", order.OrderDate.Date);
                await delCmd.ExecuteNonQueryAsync(ct);
            }

            const string insertDim = """
                                     INSERT INTO order_dimension (order_number, order_date, dimension_name, unit, dimension_value)
                                     VALUES (@number, @order_date, @name, @unit, @value)
                                     """;
            foreach (var dim in order.Dimensions)
            {
                await using var dimCmd = new NpgsqlCommand(insertDim, con, tx);
                dimCmd.Parameters.AddWithValue("number", order.Number);
                dimCmd.Parameters.AddWithValue("order_date", order.OrderDate.Date);
                dimCmd.Parameters.AddWithValue("name", dim.Name);
                dimCmd.Parameters.AddWithValue("unit", dim.Unit);
                dimCmd.Parameters.AddWithValue("value", dim.Value);
                await dimCmd.ExecuteNonQueryAsync(ct);
            }

            const string insertFile = """
                                      INSERT INTO order_attachment (order_number, order_date, file_name, file_data)
                                      VALUES (@number, @order_date, @file_name, @file_data)
                                      """;
            foreach (var (fileName, data) in newAttachments)
            {
                await using var fileCmd = new NpgsqlCommand(insertFile, con, tx);
                fileCmd.Parameters.AddWithValue("number", order.Number);
                fileCmd.Parameters.AddWithValue("order_date", order.OrderDate.Date);
                fileCmd.Parameters.AddWithValue("file_name", fileName);
                fileCmd.Parameters.AddWithValue("file_data", data);
                await fileCmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task DeleteOrderAsync(int number, DateTime orderDate, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM customer_order WHERE number = @number AND order_date = @order_date";
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("number", number);
        cmd.Parameters.AddWithValue("order_date", orderDate.Date);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<string?> ChangeOrderStatusAsync(
        int number,
        DateTime orderDate,
        string newStatus,
        string changedByLogin,
        string? comment,
        CancellationToken ct = default)
    {
        var statusMap = await GetOrderStatusMapAsync(ct);
        var statusId = statusMap[newStatus];

        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var tx = await con.BeginTransactionAsync(ct);
        string? writeOffMessage = null;
        try
        {
            const string getCurrent = """
                                      SELECT s.name, o.product_name
                                      FROM customer_order o
                                      JOIN order_status s ON s.status_id = o.current_status_id
                                      WHERE o.number = @number AND o.order_date = @order_date
                                      """;
            string currentStatus;
            string productName;
            await using (var getCmd = new NpgsqlCommand(getCurrent, con, tx))
            {
                getCmd.Parameters.AddWithValue("number", number);
                getCmd.Parameters.AddWithValue("order_date", orderDate.Date);
                await using var reader = await getCmd.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                {
                    throw new InvalidOperationException("Заказ не найден.");
                }

                currentStatus = reader.GetString(0);
                productName = reader.GetString(1);
            }

            if (!OrderWorkflowService.CanTransition(currentStatus, newStatus))
            {
                throw new InvalidOperationException($"Нельзя сменить статус {currentStatus} на {newStatus}.");
            }

            if (currentStatus == "Закупка" && newStatus == "Производство")
            {
                var writeOff = await WriteOffMaterialsForOrderAsync(con, tx, productName, ct);
                writeOffMessage = writeOff.MaterialLines + writeOff.ComponentLines == 0
                    ? $"Списание не выполнено: для изделия «{productName}» нет спецификации материалов/комплектующих."
                    : $"Списано со склада: {writeOff.MaterialLines} поз. материалов, {writeOff.ComponentLines} поз. комплектующих.";
            }

            var assignManager = currentStatus == "Новый" && newStatus == "Составление спецификации";

            const string update = """
                                  UPDATE customer_order
                                  SET current_status_id = @status_id,
                                      manager_login = CASE
                                          WHEN @assign_manager THEN @login
                                          ELSE manager_login
                                      END,
                                      rejection_reason = CASE WHEN @status_name = 'Отклонен' THEN @comment ELSE rejection_reason END
                                  WHERE number = @number AND order_date = @order_date
                                  """;
            await using (var updateCmd = new NpgsqlCommand(update, con, tx))
            {
                updateCmd.Parameters.AddWithValue("status_id", statusId);
                updateCmd.Parameters.AddWithValue("assign_manager", assignManager);
                updateCmd.Parameters.AddWithValue("login", changedByLogin);
                updateCmd.Parameters.AddWithValue("status_name", newStatus);
                updateCmd.Parameters.AddWithValue("comment", (object?)comment ?? DBNull.Value);
                updateCmd.Parameters.AddWithValue("number", number);
                updateCmd.Parameters.AddWithValue("order_date", orderDate.Date);
                await updateCmd.ExecuteNonQueryAsync(ct);
            }

            await AddStatusHistoryAsync(con, tx, number, orderDate, statusId, changedByLogin, comment, ct);
            await tx.CommitAsync(ct);
            return writeOffMessage;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private static async Task AddStatusHistoryAsync(
        NpgsqlConnection con,
        NpgsqlTransaction tx,
        int number,
        DateTime orderDate,
        long statusId,
        string changedByLogin,
        string? comment,
        CancellationToken ct)
    {
        const string sql = """
                           INSERT INTO order_status_history
                               (order_number, order_date, status_id, changed_by_login, comment)
                           VALUES (@number, @order_date, @status_id, @login, @comment)
                           """;
        await using var cmd = new NpgsqlCommand(sql, con, tx);
        cmd.Parameters.AddWithValue("number", number);
        cmd.Parameters.AddWithValue("order_date", orderDate.Date);
        cmd.Parameters.AddWithValue("status_id", statusId);
        cmd.Parameters.AddWithValue("login", changedByLogin);
        cmd.Parameters.AddWithValue("comment", (object?)comment ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<WriteOffResult> WriteOffMaterialsForOrderAsync(
        NpgsqlConnection con,
        NpgsqlTransaction tx,
        string productName,
        CancellationToken ct)
    {
        const string sql = """
                           WITH RECURSIVE bom AS (
                               SELECT @product_name::varchar AS item, 1::numeric AS qty,
                                      ARRAY[@product_name::varchar] AS path, 0 AS depth
                               UNION ALL
                               SELECT pas.child_product_name, bom.qty * pas.quantity,
                                      bom.path || pas.child_product_name, bom.depth + 1
                               FROM bom
                               JOIN product_assembly_spec pas ON pas.product_name = bom.item
                               WHERE NOT (pas.child_product_name = ANY(bom.path))
                                 AND bom.depth < 32
                           ),
                           material_need AS (
                               SELECT pms.material_article AS article, SUM(bom.qty * pms.quantity) AS need_qty
                               FROM bom
                               JOIN product_material_spec pms ON pms.product_name = bom.item
                               GROUP BY pms.material_article
                           ),
                           component_need AS (
                               SELECT pcs.component_article AS article, SUM(bom.qty * pcs.quantity) AS need_qty
                               FROM bom
                               JOIN product_component_spec pcs ON pcs.product_name = bom.item
                               GROUP BY pcs.component_article
                           )
                           SELECT 'M' AS kind, article, need_qty FROM material_need
                           UNION ALL
                           SELECT 'C', article, need_qty FROM component_need
                           """;

        await using var cmd = new NpgsqlCommand(sql, con, tx);
        cmd.Parameters.AddWithValue("product_name", productName);
        var materials = new List<(string article, decimal qty)>();
        var components = new List<(string article, decimal qty)>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var kind = reader.GetString(0);
                var article = reader.GetString(1);
                var qty = reader.GetDecimal(2);
                if (kind == "M")
                {
                    materials.Add((article, qty));
                }
                else
                {
                    components.Add((article, qty));
                }
            }
        }

        const string warehouseSql = """
                                    SELECT warehouse_id FROM warehouse WHERE name = 'Основной склад' LIMIT 1
                                    """;
        long warehouseId;
        await using (var whCmd = new NpgsqlCommand(warehouseSql, con, tx))
        {
            warehouseId = Convert.ToInt64(await whCmd.ExecuteScalarAsync(ct));
        }

        const string updateMat = """
                                 UPDATE material_stock
                                 SET quantity = GREATEST(quantity - @qty, 0)
                                 WHERE warehouse_id = @wid AND material_article = @article
                                 """;
        foreach (var (article, qty) in materials)
        {
            await using var u = new NpgsqlCommand(updateMat, con, tx);
            u.Parameters.AddWithValue("qty", qty);
            u.Parameters.AddWithValue("wid", warehouseId);
            u.Parameters.AddWithValue("article", article);
            await u.ExecuteNonQueryAsync(ct);
        }

        const string updateComp = """
                                  UPDATE component_stock
                                  SET quantity = GREATEST(quantity - @qty, 0)
                                  WHERE warehouse_id = @wid AND component_article = @article
                                  """;
        foreach (var (article, qty) in components)
        {
            await using var u = new NpgsqlCommand(updateComp, con, tx);
            u.Parameters.AddWithValue("qty", qty);
            u.Parameters.AddWithValue("wid", warehouseId);
            u.Parameters.AddWithValue("article", article);
            await u.ExecuteNonQueryAsync(ct);
        }

        return new WriteOffResult(materials.Count, components.Count);
    }

    public async Task<IReadOnlyList<OrderHistoryItem>> GetOrderHistoryAsync(
        int number,
        DateTime orderDate,
        CancellationToken ct = default)
    {
        const string sql = """
                           SELECT h.changed_at, s.name, h.changed_by_login, h.comment
                           FROM order_status_history h
                           JOIN order_status s ON s.status_id = h.status_id
                           WHERE h.order_number = @number AND h.order_date = @order_date
                           ORDER BY h.changed_at DESC
                           """;
        var result = new List<OrderHistoryItem>();
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("number", number);
        cmd.Parameters.AddWithValue("order_date", orderDate.Date);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new OrderHistoryItem
            {
                ChangedAt = reader.GetDateTime(0),
                StatusName = reader.GetString(1),
                ChangedBy = reader.IsDBNull(2) ? null : reader.GetString(2),
                Comment = reader.IsDBNull(3) ? null : reader.GetString(3)
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<UserOption>> GetCustomersAsync(CancellationToken ct = default)
    {
        const string sql = """
                           SELECT login, COALESCE(full_name, login)
                           FROM app_user
                           WHERE role_name = 'Заказчик'
                           ORDER BY full_name
                           """;
        var result = new List<UserOption>();
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new UserOption { Login = reader.GetString(0), DisplayName = reader.GetString(1) });
        }

        return result;
    }

    public async Task<IReadOnlyList<WorkshopInfo>> GetWorkshopsAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT workshop_id, name, plan_file_name FROM workshop ORDER BY name";
        var result = new List<WorkshopInfo>();
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new WorkshopInfo
            {
                WorkshopId = reader.GetInt64(0),
                Name = reader.GetString(1),
                PlanFileName = reader.GetString(2)
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<WorkshopMarkerModel>> GetWorkshopMarkersAsync(long workshopId, CancellationToken ct = default)
    {
        const string sql = """
                           SELECT marker_id, icon_type, pos_x, pos_y
                           FROM workshop_plan_marker
                           WHERE workshop_id = @wid
                           """;
        var result = new List<WorkshopMarkerModel>();
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("wid", workshopId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new WorkshopMarkerModel
            {
                MarkerId = reader.GetInt64(0),
                IconType = reader.GetString(1),
                X = reader.GetDouble(2),
                Y = reader.GetDouble(3)
            });
        }

        return result;
    }

    public async Task SaveWorkshopMarkersAsync(long workshopId, IReadOnlyList<WorkshopMarkerModel> markers, CancellationToken ct = default)
    {
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var tx = await con.BeginTransactionAsync(ct);
        try
        {
            await using (var del = new NpgsqlCommand("DELETE FROM workshop_plan_marker WHERE workshop_id = @wid", con, tx))
            {
                del.Parameters.AddWithValue("wid", workshopId);
                await del.ExecuteNonQueryAsync(ct);
            }

            const string ins = """
                               INSERT INTO workshop_plan_marker (workshop_id, icon_type, pos_x, pos_y)
                               VALUES (@wid, @type, @x, @y)
                               """;
            foreach (var m in markers)
            {
                await using var cmd = new NpgsqlCommand(ins, con, tx);
                cmd.Parameters.AddWithValue("wid", workshopId);
                cmd.Parameters.AddWithValue("type", m.IconType);
                cmd.Parameters.AddWithValue("x", m.X);
                cmd.Parameters.AddWithValue("y", m.Y);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<IReadOnlyList<string>> GetEquipmentMarkingsAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT marking FROM equipment ORDER BY marking";
        var result = new List<string>();
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }

    public async Task<IReadOnlyList<string>> GetFailureReasonsAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT name FROM equipment_failure_reason ORDER BY name";
        var result = new List<string>();
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }

    public async Task RegisterEquipmentFailureAsync(
        string equipmentMarking,
        string reasonName,
        DateTime startedAt,
        string registeredBy,
        CancellationToken ct = default)
    {
        const string sql = """
                           INSERT INTO equipment_failure (equipment_marking, reason_id, started_at, registered_by_login)
                           SELECT @marking, r.reason_id, @started, @login
                           FROM equipment_failure_reason r
                           WHERE r.name = @reason
                           """;
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("marking", equipmentMarking);
        cmd.Parameters.AddWithValue("started", startedAt);
        cmd.Parameters.AddWithValue("login", registeredBy);
        cmd.Parameters.AddWithValue("reason", reasonName);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task EndEquipmentFailureAsync(long failureId, DateTime endedAt, CancellationToken ct = default)
    {
        const string sql = "UPDATE equipment_failure SET ended_at = @ended WHERE failure_id = @id AND ended_at IS NULL";
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("ended", endedAt);
        cmd.Parameters.AddWithValue("id", failureId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<EquipmentFailureRow>> GetOpenEquipmentFailuresAsync(CancellationToken ct = default)
    {
        const string sql = """
                           SELECT f.failure_id, f.equipment_marking, r.name, f.started_at, f.ended_at, f.registered_by_login
                           FROM equipment_failure f
                           JOIN equipment_failure_reason r ON r.reason_id = f.reason_id
                           ORDER BY f.started_at DESC
                           LIMIT 100
                           """;
        var result = new List<EquipmentFailureRow>();
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new EquipmentFailureRow
            {
                FailureId = reader.GetInt64(0),
                EquipmentMarking = reader.GetString(1),
                ReasonName = reader.GetString(2),
                StartedAt = reader.GetDateTime(3),
                EndedAt = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                RegisteredBy = reader.IsDBNull(5) ? null : reader.GetString(5)
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<QualityCheckRow>> GetQualityChecksForOrderAsync(
        int number,
        DateTime orderDate,
        CancellationToken ct = default)
    {
        const string sql = """
                           SELECT qp.parameter_id, qp.name, q.is_positive, q.comment
                           FROM quality_parameter qp
                           LEFT JOIN order_quality_check q
                               ON q.parameter_id = qp.parameter_id
                              AND q.order_number = @number
                              AND q.order_date = @order_date
                           ORDER BY qp.name
                           """;
        var result = new List<QualityCheckRow>();
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("number", number);
        cmd.Parameters.AddWithValue("order_date", orderDate.Date);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new QualityCheckRow
            {
                ParameterId = reader.GetInt64(0),
                ParameterName = reader.GetString(1),
                IsPositive = reader.IsDBNull(2) ? null : reader.GetBoolean(2),
                Comment = reader.IsDBNull(3) ? string.Empty : reader.GetString(3)
            });
        }

        return result;
    }

    public async Task SaveQualityChecksAsync(
        int number,
        DateTime orderDate,
        IReadOnlyList<QualityCheckRow> checks,
        string checkedBy,
        CancellationToken ct = default)
    {
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var tx = await con.BeginTransactionAsync(ct);
        try
        {
            const string upsert = """
                                  INSERT INTO order_quality_check
                                      (order_number, order_date, parameter_id, is_positive, comment, checked_by_login)
                                  VALUES (@number, @order_date, @pid, @pos, @comment, @login)
                                  ON CONFLICT (order_number, order_date, parameter_id)
                                  DO UPDATE SET is_positive = EXCLUDED.is_positive,
                                                comment = EXCLUDED.comment,
                                                checked_at = NOW(),
                                                checked_by_login = EXCLUDED.checked_by_login
                                  """;
            foreach (var c in checks.Where(c => c.IsPositive.HasValue))
            {
                await using var cmd = new NpgsqlCommand(upsert, con, tx);
                cmd.Parameters.AddWithValue("number", number);
                cmd.Parameters.AddWithValue("order_date", orderDate.Date);
                cmd.Parameters.AddWithValue("pid", c.ParameterId);
                cmd.Parameters.AddWithValue("pos", c.IsPositive!.Value);
                cmd.Parameters.AddWithValue("comment", string.IsNullOrWhiteSpace(c.Comment) ? DBNull.Value : c.Comment);
                cmd.Parameters.AddWithValue("login", checkedBy);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<bool> AllQualityChecksPositiveAsync(int number, DateTime orderDate, CancellationToken ct = default)
    {
        const string sql = """
                           SELECT COUNT(*) FILTER (WHERE q.is_positive IS DISTINCT FROM TRUE),
                                  COUNT(*)
                           FROM quality_parameter qp
                           LEFT JOIN order_quality_check q
                               ON q.parameter_id = qp.parameter_id
                              AND q.order_number = @number
                              AND q.order_date = @order_date
                           """;
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("number", number);
        cmd.Parameters.AddWithValue("order_date", orderDate.Date);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        var bad = reader.GetInt64(0);
        var total = reader.GetInt64(1);
        return total > 0 && bad == 0;
    }
}
