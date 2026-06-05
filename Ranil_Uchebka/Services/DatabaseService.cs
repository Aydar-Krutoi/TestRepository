using Npgsql;
using NpgsqlTypes;
using Ranil_Uchebka.Models;

namespace Ranil_Uchebka.Services;

public partial class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(string connectionString)
    {
        _connectionString = connectionString;
    }

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    public async Task<AppUserSession?> ValidateUserAsync(string login, string password, CancellationToken ct = default)
    {
        const string sql = """
                           SELECT login, role_name, full_name
                           FROM app_user
                           WHERE login = @login AND password = @password
                           LIMIT 1
                           """;
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("login", login);
        cmd.Parameters.AddWithValue("password", password);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return new AppUserSession
        {
            Login = reader.GetString(0),
            RoleName = reader.GetString(1),
            FullName = reader.IsDBNull(2) ? null : reader.GetString(2)
        };
    }

    public async Task<bool> LoginExistsAsync(string login, CancellationToken ct = default)
    {
        const string sql = "SELECT EXISTS (SELECT 1 FROM app_user WHERE login = @login)";
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("login", login);
        return (bool)(await cmd.ExecuteScalarAsync(ct) ?? false);
    }

    public async Task RegisterCustomerAsync(string login, string password, string fullName, CancellationToken ct = default)
    {
        const string sql = """
                           INSERT INTO app_user (login, password, role_name, full_name)
                           VALUES (@login, @password, 'Заказчик', @full_name)
                           """;
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("login", login);
        cmd.Parameters.AddWithValue("password", password);
        cmd.Parameters.AddWithValue("full_name", fullName);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<WarehouseOption>> GetWarehousesAsync(CancellationToken ct = default)
    {
        const string sql = """
                           SELECT warehouse_id, name
                           FROM warehouse
                           ORDER BY name
                           """;
        var result = new List<WarehouseOption>();
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new WarehouseOption
            {
                WarehouseId = reader.GetInt64(0),
                Name = reader.GetString(1)
            });
        }

        return result;
    }

    public async Task<int> GetMaterialTotalCountAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT COUNT(*) FROM material";
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task<int> GetComponentTotalCountAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT COUNT(*) FROM component";
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task<IReadOnlyList<MaterialItem>> GetMaterialsAsync(long? warehouseId, CancellationToken ct = default)
    {
        if (warehouseId == 0)
        {
            warehouseId = null;
        }

        const string sql = """
                           SELECT
                               m.article,
                               m.name,
                               COALESCE(ms.quantity, 0),
                               m.unit,
                               m.purchase_price,
                               m.main_supplier,
                               s.delivery_days,
                               COALESCE(w.warehouse_id, 0),
                               COALESCE(w.name, 'N/A'),
                               m.material_type,
                               m.gost,
                               m.length
                           FROM material m
                           LEFT JOIN material_stock ms ON ms.material_article = m.article
                           LEFT JOIN warehouse w ON w.warehouse_id = ms.warehouse_id
                           LEFT JOIN supplier s ON s.name = m.main_supplier
                           WHERE (@warehouse_id IS NULL OR ms.warehouse_id = @warehouse_id)
                           ORDER BY m.article
                           """;
        var result = new List<MaterialItem>();
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.Add(CreateWarehouseIdParameter(warehouseId));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new MaterialItem
            {
                Article = reader.GetString(0),
                Name = reader.GetString(1),
                Quantity = reader.GetDecimal(2),
                Unit = reader.GetString(3),
                PurchasePrice = reader.GetDecimal(4),
                MainSupplier = reader.IsDBNull(5) ? null : reader.GetString(5),
                DeliveryDays = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                WarehouseId = reader.GetInt64(7),
                WarehouseName = reader.GetString(8),
                MaterialType = reader.GetString(9),
                Gost = reader.IsDBNull(10) ? null : reader.GetString(10),
                Length = reader.IsDBNull(11) ? null : reader.GetDecimal(11)
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<ComponentItem>> GetComponentsAsync(long? warehouseId, CancellationToken ct = default)
    {
        if (warehouseId == 0)
        {
            warehouseId = null;
        }

        const string sql = """
                           SELECT
                               c.article,
                               c.name,
                               COALESCE(cs.quantity, 0),
                               c.unit,
                               c.purchase_price,
                               c.weight,
                               c.main_supplier,
                               s.delivery_days,
                               COALESCE(w.warehouse_id, 0),
                               COALESCE(w.name, 'N/A'),
                               c.component_type
                           FROM component c
                           LEFT JOIN component_stock cs ON cs.component_article = c.article
                           LEFT JOIN warehouse w ON w.warehouse_id = cs.warehouse_id
                           LEFT JOIN supplier s ON s.name = c.main_supplier
                           WHERE (@warehouse_id IS NULL OR cs.warehouse_id = @warehouse_id)
                           ORDER BY c.article
                           """;
        var result = new List<ComponentItem>();
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.Add(CreateWarehouseIdParameter(warehouseId));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new ComponentItem
            {
                Article = reader.GetString(0),
                Name = reader.GetString(1),
                Quantity = reader.GetDecimal(2),
                Unit = reader.GetString(3),
                PurchasePrice = reader.GetDecimal(4),
                Weight = reader.GetDecimal(5),
                MainSupplier = reader.IsDBNull(6) ? null : reader.GetString(6),
                DeliveryDays = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                WarehouseId = reader.GetInt64(8),
                WarehouseName = reader.GetString(9),
                ComponentType = reader.GetString(10)
            });
        }

        return result;
    }

    public async Task<bool> CanDeleteMaterialAsync(string article, CancellationToken ct = default)
    {
        const string sql = "SELECT quantity <= 0 FROM material WHERE article = @article";
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("article", article);
        var scalar = await cmd.ExecuteScalarAsync(ct);
        return scalar is bool canDelete && canDelete;
    }

    public async Task<bool> CanDeleteComponentAsync(string article, CancellationToken ct = default)
    {
        const string sql = "SELECT quantity <= 0 FROM component WHERE article = @article";
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("article", article);
        var scalar = await cmd.ExecuteScalarAsync(ct);
        return scalar is bool canDelete && canDelete;
    }

    public async Task UpdateMaterialAsync(MaterialItem item, CancellationToken ct = default)
    {
        const string sqlUpdateMaterial = """
                                         UPDATE material
                                         SET name = @name,
                                             unit = @unit,
                                             purchase_price = @purchase_price,
                                             material_type = @material_type,
                                             gost = @gost,
                                             length = @length
                                         WHERE article = @article
                                         """;
        const string sqlUpsertStock = """
                                      INSERT INTO material_stock (warehouse_id, material_article, quantity)
                                      VALUES (@warehouse_id, @article, @quantity)
                                      ON CONFLICT (warehouse_id, material_article)
                                      DO UPDATE SET quantity = EXCLUDED.quantity
                                      """;
        const string sqlSyncTotal = """
                                    UPDATE material m
                                    SET quantity = (
                                        SELECT COALESCE(SUM(ms.quantity), 0)
                                        FROM material_stock ms
                                        WHERE ms.material_article = m.article
                                    )
                                    WHERE m.article = @article
                                    """;
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var tx = await con.BeginTransactionAsync(ct);
        try
        {
            await using (var cmd = new NpgsqlCommand(sqlUpdateMaterial, con, tx))
            {
                cmd.Parameters.AddWithValue("article", item.Article);
                cmd.Parameters.AddWithValue("name", item.Name);
                cmd.Parameters.AddWithValue("unit", item.Unit);
                cmd.Parameters.AddWithValue("purchase_price", item.PurchasePrice);
                cmd.Parameters.AddWithValue("material_type", item.MaterialType);
                cmd.Parameters.AddWithValue("gost", (object?)item.Gost ?? DBNull.Value);
                cmd.Parameters.AddWithValue("length", (object?)item.Length ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await using (var cmd = new NpgsqlCommand(sqlUpsertStock, con, tx))
            {
                cmd.Parameters.AddWithValue("warehouse_id", item.WarehouseId);
                cmd.Parameters.AddWithValue("article", item.Article);
                cmd.Parameters.AddWithValue("quantity", item.Quantity);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await using (var cmd = new NpgsqlCommand(sqlSyncTotal, con, tx))
            {
                cmd.Parameters.AddWithValue("article", item.Article);
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

    public async Task UpdateComponentAsync(ComponentItem item, CancellationToken ct = default)
    {
        const string sqlUpdateComponent = """
                                          UPDATE component
                                          SET name = @name,
                                              unit = @unit,
                                              purchase_price = @purchase_price,
                                              component_type = @component_type,
                                              weight = @weight
                                          WHERE article = @article
                                          """;
        const string sqlUpsertStock = """
                                      INSERT INTO component_stock (warehouse_id, component_article, quantity)
                                      VALUES (@warehouse_id, @article, @quantity)
                                      ON CONFLICT (warehouse_id, component_article)
                                      DO UPDATE SET quantity = EXCLUDED.quantity
                                      """;
        const string sqlSyncTotal = """
                                    UPDATE component c
                                    SET quantity = (
                                        SELECT COALESCE(SUM(cs.quantity), 0)
                                        FROM component_stock cs
                                        WHERE cs.component_article = c.article
                                    )
                                    WHERE c.article = @article
                                    """;
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var tx = await con.BeginTransactionAsync(ct);
        try
        {
            await using (var cmd = new NpgsqlCommand(sqlUpdateComponent, con, tx))
            {
                cmd.Parameters.AddWithValue("article", item.Article);
                cmd.Parameters.AddWithValue("name", item.Name);
                cmd.Parameters.AddWithValue("unit", item.Unit);
                cmd.Parameters.AddWithValue("purchase_price", item.PurchasePrice);
                cmd.Parameters.AddWithValue("component_type", item.ComponentType);
                cmd.Parameters.AddWithValue("weight", item.Weight);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await using (var cmd = new NpgsqlCommand(sqlUpsertStock, con, tx))
            {
                cmd.Parameters.AddWithValue("warehouse_id", item.WarehouseId);
                cmd.Parameters.AddWithValue("article", item.Article);
                cmd.Parameters.AddWithValue("quantity", item.Quantity);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await using (var cmd = new NpgsqlCommand(sqlSyncTotal, con, tx))
            {
                cmd.Parameters.AddWithValue("article", item.Article);
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

    public async Task DeleteMaterialAsync(string article, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM material WHERE article = @article";
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("article", article);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteComponentAsync(string article, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM component WHERE article = @article";
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("article", article);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<WorkerItem>> GetWorkersAsync(CancellationToken ct = default)
    {
        const string sql = """
                           SELECT
                               w.worker_id,
                               w.full_name,
                               w.birth_date,
                               w.home_address,
                               w.education,
                               w.qualification,
                               COALESCE(STRING_AGG(po.name, ', ' ORDER BY po.name), '')
                           FROM production_worker w
                           LEFT JOIN worker_operation wo ON wo.worker_id = w.worker_id
                           LEFT JOIN production_operation po ON po.operation_id = wo.operation_id
                           GROUP BY w.worker_id, w.full_name, w.birth_date, w.home_address, w.education, w.qualification
                           ORDER BY w.full_name
                           """;
        var result = new List<WorkerItem>();
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new WorkerItem
            {
                WorkerId = reader.GetInt64(0),
                FullName = reader.GetString(1),
                BirthDate = reader.GetDateTime(2),
                HomeAddress = reader.GetString(3),
                Education = reader.GetString(4),
                Qualification = reader.GetString(5),
                OperationsCsv = reader.GetString(6)
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<OperationOption>> GetOperationsAsync(CancellationToken ct = default)
    {
        const string sql = """
                           SELECT operation_id, name
                           FROM production_operation
                           ORDER BY name
                           """;
        var result = new List<OperationOption>();
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new OperationOption
            {
                OperationId = reader.GetInt64(0),
                Name = reader.GetString(1)
            });
        }

        return result;
    }

    public async Task<IReadOnlyCollection<long>> GetWorkerOperationIdsAsync(long workerId, CancellationToken ct = default)
    {
        const string sql = """
                           SELECT operation_id
                           FROM worker_operation
                           WHERE worker_id = @worker_id
                           """;
        var ids = new HashSet<long>();
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("worker_id", workerId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            ids.Add(reader.GetInt64(0));
        }

        return ids;
    }

    public async Task<long> SaveWorkerAsync(
        WorkerItem worker,
        IReadOnlyCollection<long> operationIds,
        CancellationToken ct = default)
    {
        const string insertSql = """
                                 INSERT INTO production_worker (full_name, birth_date, home_address, education, qualification)
                                 VALUES (@full_name, @birth_date, @home_address, @education, @qualification)
                                 RETURNING worker_id
                                 """;
        const string updateSql = """
                                 UPDATE production_worker
                                 SET full_name = @full_name,
                                     birth_date = @birth_date,
                                     home_address = @home_address,
                                     education = @education,
                                     qualification = @qualification
                                 WHERE worker_id = @worker_id
                                 """;
        const string deleteLinksSql = "DELETE FROM worker_operation WHERE worker_id = @worker_id";
        const string insertLinkSql = """
                                     INSERT INTO worker_operation (worker_id, operation_id)
                                     VALUES (@worker_id, @operation_id)
                                     ON CONFLICT (worker_id, operation_id) DO NOTHING
                                     """;

        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var tx = await con.BeginTransactionAsync(ct);
        try
        {
            var workerId = worker.WorkerId;
            if (workerId == 0)
            {
                await using var insertCmd = new NpgsqlCommand(insertSql, con, tx);
                insertCmd.Parameters.AddWithValue("full_name", worker.FullName);
                insertCmd.Parameters.AddWithValue("birth_date", worker.BirthDate.Date);
                insertCmd.Parameters.AddWithValue("home_address", worker.HomeAddress);
                insertCmd.Parameters.AddWithValue("education", worker.Education);
                insertCmd.Parameters.AddWithValue("qualification", worker.Qualification);
                workerId = Convert.ToInt64(await insertCmd.ExecuteScalarAsync(ct));
            }
            else
            {
                await using var updateCmd = new NpgsqlCommand(updateSql, con, tx);
                updateCmd.Parameters.AddWithValue("worker_id", workerId);
                updateCmd.Parameters.AddWithValue("full_name", worker.FullName);
                updateCmd.Parameters.AddWithValue("birth_date", worker.BirthDate.Date);
                updateCmd.Parameters.AddWithValue("home_address", worker.HomeAddress);
                updateCmd.Parameters.AddWithValue("education", worker.Education);
                updateCmd.Parameters.AddWithValue("qualification", worker.Qualification);
                await updateCmd.ExecuteNonQueryAsync(ct);
            }

            await using (var deleteLinksCmd = new NpgsqlCommand(deleteLinksSql, con, tx))
            {
                deleteLinksCmd.Parameters.AddWithValue("worker_id", workerId);
                await deleteLinksCmd.ExecuteNonQueryAsync(ct);
            }

            foreach (var operationId in operationIds)
            {
                await using var insertLinkCmd = new NpgsqlCommand(insertLinkSql, con, tx);
                insertLinkCmd.Parameters.AddWithValue("worker_id", workerId);
                insertLinkCmd.Parameters.AddWithValue("operation_id", operationId);
                await insertLinkCmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return workerId;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task DeleteWorkerAsync(long workerId, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM production_worker WHERE worker_id = @worker_id";
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("worker_id", workerId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static NpgsqlParameter CreateWarehouseIdParameter(long? warehouseId) =>
        new("warehouse_id", NpgsqlDbType.Bigint)
        {
            Value = warehouseId ?? (object)DBNull.Value
        };
}
