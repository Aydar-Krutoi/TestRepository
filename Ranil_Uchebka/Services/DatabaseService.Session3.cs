using Npgsql;
using NpgsqlTypes;
using Ranil_Uchebka.Models;

namespace Ranil_Uchebka.Services;

public partial class DatabaseService
{
    public async Task<(string Name, string Unit)?> GetMaterialReferenceByArticleAsync(
        string article,
        CancellationToken ct = default)
    {
        const string sql = """
                           SELECT name, unit
                           FROM material
                           WHERE article = @article
                           """;
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("article", article);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return (reader.GetString(0), reader.GetString(1));
    }

    public async Task<(string Name, string Unit)?> GetComponentReferenceByArticleAsync(
        string article,
        CancellationToken ct = default)
    {
        const string sql = """
                           SELECT name, unit
                           FROM component
                           WHERE article = @article
                           """;
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("article", article);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return (reader.GetString(0), reader.GetString(1));
    }

    public async Task EnsureSession3SchemaAsync(CancellationToken ct = default)
    {
        const string sql = """
                           ALTER TABLE product_operation_spec
                               ADD COLUMN IF NOT EXISTS operation_description TEXT;

                           CREATE TABLE IF NOT EXISTS product_attachment (
                               attachment_id BIGSERIAL PRIMARY KEY,
                               product_name  VARCHAR(200) NOT NULL REFERENCES product (name)
                                   ON UPDATE CASCADE ON DELETE CASCADE,
                               file_name     VARCHAR(300) NOT NULL,
                               file_data     BYTEA NOT NULL
                           );

                           CREATE TABLE IF NOT EXISTS product_dimension (
                               dimension_id    BIGSERIAL PRIMARY KEY,
                               product_name    VARCHAR(200) NOT NULL REFERENCES product (name)
                                   ON UPDATE CASCADE ON DELETE CASCADE,
                               dimension_name  VARCHAR(100) NOT NULL,
                               unit            VARCHAR(30) NOT NULL,
                               dimension_value NUMERIC(14, 3) NOT NULL
                           );
                           """;
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<ProductOption>> GetProductsAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT name, dimensions FROM product ORDER BY name";
        var result = new List<ProductOption>();
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new ProductOption
            {
                Name = reader.GetString(0),
                Dimensions = reader.GetString(1)
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<string>> GetEquipmentTypesAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT name FROM equipment_type ORDER BY name";
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

    public async Task<IReadOnlyList<SpecMaterialRow>> GetSpecMaterialsAsync(string productName, CancellationToken ct = default)
    {
        const string sql = """
                           SELECT s.material_article, m.name, m.unit, s.quantity
                           FROM product_material_spec s
                           JOIN material m ON m.article = s.material_article
                           WHERE s.product_name = @product
                           ORDER BY m.name
                           """;
        var result = new List<SpecMaterialRow>();
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("product", productName);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new SpecMaterialRow
            {
                Article = reader.GetString(0),
                Name = reader.GetString(1),
                Unit = reader.GetString(2),
                Quantity = reader.GetDecimal(3)
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<SpecComponentRow>> GetSpecComponentsAsync(string productName, CancellationToken ct = default)
    {
        const string sql = """
                           SELECT s.component_article, c.name, c.unit, s.quantity
                           FROM product_component_spec s
                           JOIN component c ON c.article = s.component_article
                           WHERE s.product_name = @product
                           ORDER BY c.name
                           """;
        var result = new List<SpecComponentRow>();
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("product", productName);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new SpecComponentRow
            {
                Article = reader.GetString(0),
                Name = reader.GetString(1),
                Unit = reader.GetString(2),
                Quantity = reader.GetDecimal(3)
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<SpecAssemblyRow>> GetSpecAssembliesAsync(string productName, CancellationToken ct = default)
    {
        const string sql = """
                           SELECT child_product_name, quantity
                           FROM product_assembly_spec
                           WHERE product_name = @product
                           ORDER BY child_product_name
                           """;
        var result = new List<SpecAssemblyRow>();
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("product", productName);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new SpecAssemblyRow
            {
                ProductName = reader.GetString(0),
                Quantity = reader.GetDecimal(1)
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<ProductDimensionRow>> GetProductDimensionsAsync(string productName, CancellationToken ct = default)
    {
        await EnsureSession3SchemaAsync(ct);
        const string sql = """
                           SELECT dimension_id, dimension_name, unit, dimension_value
                           FROM product_dimension
                           WHERE product_name = @product
                           ORDER BY dimension_name
                           """;
        var result = new List<ProductDimensionRow>();
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("product", productName);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new ProductDimensionRow
            {
                DimensionId = reader.GetInt64(0),
                DimensionName = reader.GetString(1),
                Unit = reader.GetString(2),
                DimensionValue = reader.GetDecimal(3)
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<ProductAttachmentRow>> GetProductAttachmentsAsync(string productName, CancellationToken ct = default)
    {
        await EnsureSession3SchemaAsync(ct);
        const string sql = """
                           SELECT attachment_id, file_name
                           FROM product_attachment
                           WHERE product_name = @product
                           ORDER BY attachment_id
                           """;
        var result = new List<ProductAttachmentRow>();
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("product", productName);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new ProductAttachmentRow
            {
                AttachmentId = reader.GetInt64(0),
                FileName = reader.GetString(1)
            });
        }

        return result;
    }

    public async Task<byte[]?> GetProductAttachmentDataAsync(long attachmentId, string productName, CancellationToken ct = default)
    {
        const string sql = """
                           SELECT file_data
                           FROM product_attachment
                           WHERE attachment_id = @id AND product_name = @product
                           """;
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("id", attachmentId);
        cmd.Parameters.AddWithValue("product", productName);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is byte[] data ? data : null;
    }

    public async Task DeleteProductAttachmentAsync(long attachmentId, string productName, CancellationToken ct = default)
    {
        const string sql = """
                           DELETE FROM product_attachment
                           WHERE attachment_id = @id AND product_name = @product
                           """;
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("id", attachmentId);
        cmd.Parameters.AddWithValue("product", productName);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task AddProductAttachmentsAsync(
        string productName,
        IReadOnlyList<(string fileName, byte[] data)> files,
        CancellationToken ct = default)
    {
        if (files.Count == 0)
        {
            return;
        }

        await EnsureSession3SchemaAsync(ct);
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        foreach (var (fileName, data) in files)
        {
            await using var cmd = new NpgsqlCommand("""
                                                     INSERT INTO product_attachment (product_name, file_name, file_data)
                                                     VALUES (@product, @file_name, @file_data)
                                                     """, con);
            cmd.Parameters.AddWithValue("product", productName);
            cmd.Parameters.AddWithValue("file_name", fileName);
            cmd.Parameters.AddWithValue("file_data", data);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task<IReadOnlyList<SpecOperationRow>> GetSpecOperationsAsync(string productName, CancellationToken ct = default)
    {
        await EnsureSession3SchemaAsync(ct);
        const string sql = """
                           SELECT l.sequence_number, l.operation_name,
                                  COALESCE(s.equipment_type_name, ''),
                                  COALESCE(s.operation_time_min, 1),
                                  COALESCE(s.operation_description, '')
                           FROM product_operation_link l
                           LEFT JOIN product_operation_spec s
                             ON s.product_name = l.product_name
                            AND s.operation_name = l.operation_name
                            AND s.sequence_number = l.sequence_number
                           WHERE l.product_name = @product
                           ORDER BY l.sequence_number
                           """;
        var result = new List<SpecOperationRow>();
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("product", productName);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new SpecOperationRow
            {
                SequenceNumber = reader.GetInt32(0),
                OperationName = reader.GetString(1),
                EquipmentTypeName = reader.GetString(2),
                OperationTimeMin = reader.GetInt32(3),
                Description = reader.GetString(4)
            });
        }

        return result;
    }

    public async Task SaveSpecificationAsync(
        string productName,
        string dimensionsSummary,
        IReadOnlyList<ProductDimensionRow> dimensions,
        IReadOnlyList<SpecMaterialRow> materials,
        IReadOnlyList<SpecComponentRow> components,
        IReadOnlyList<SpecAssemblyRow> assemblies,
        IReadOnlyList<SpecOperationRow> operations,
        IReadOnlyList<(string fileName, byte[] data)> pendingAttachments,
        CancellationToken ct = default)
    {
        await EnsureSession3SchemaAsync(ct);
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var tx = await con.BeginTransactionAsync(ct);
        try
        {
            await using (var cmd = new NpgsqlCommand("""
                                                     INSERT INTO product (name, dimensions)
                                                     VALUES (@product, @dimensions)
                                                     ON CONFLICT (name) DO UPDATE SET dimensions = EXCLUDED.dimensions
                                                     """, con, tx))
            {
                cmd.Parameters.AddWithValue("product", productName);
                cmd.Parameters.AddWithValue("dimensions", string.IsNullOrWhiteSpace(dimensionsSummary) ? "по спецификации" : dimensionsSummary);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await using (var cmd = new NpgsqlCommand("DELETE FROM product_dimension WHERE product_name = @product", con, tx))
            {
                cmd.Parameters.AddWithValue("product", productName);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            foreach (var row in dimensions.Where(d =>
                         !string.IsNullOrWhiteSpace(d.DimensionName) &&
                         !string.IsNullOrWhiteSpace(d.Unit) &&
                         d.DimensionValue > 0))
            {
                await using var cmd = new NpgsqlCommand("""
                                                        INSERT INTO product_dimension
                                                            (product_name, dimension_name, unit, dimension_value)
                                                        VALUES (@product, @name, @unit, @value)
                                                        """, con, tx);
                cmd.Parameters.AddWithValue("product", productName);
                cmd.Parameters.AddWithValue("name", row.DimensionName.Trim());
                cmd.Parameters.AddWithValue("unit", row.Unit.Trim());
                cmd.Parameters.AddWithValue("value", row.DimensionValue);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            foreach (var sql in new[]
                     {
                         "DELETE FROM product_material_spec WHERE product_name = @product",
                         "DELETE FROM product_component_spec WHERE product_name = @product",
                         "DELETE FROM product_assembly_spec WHERE product_name = @product",
                         "DELETE FROM product_operation_link WHERE product_name = @product"
                     })
            {
                await using var cmd = new NpgsqlCommand(sql, con, tx);
                cmd.Parameters.AddWithValue("product", productName);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            foreach (var row in materials.Where(m => !string.IsNullOrWhiteSpace(m.Article) && m.Quantity > 0))
            {
                await using var cmd = new NpgsqlCommand("""
                                                        INSERT INTO product_material_spec (product_name, material_article, quantity)
                                                        VALUES (@product, @article, @qty)
                                                        """, con, tx);
                cmd.Parameters.AddWithValue("product", productName);
                cmd.Parameters.AddWithValue("article", row.Article);
                cmd.Parameters.AddWithValue("qty", row.Quantity);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            foreach (var row in components.Where(c => !string.IsNullOrWhiteSpace(c.Article) && c.Quantity > 0))
            {
                await using var cmd = new NpgsqlCommand("""
                                                        INSERT INTO product_component_spec (product_name, component_article, quantity)
                                                        VALUES (@product, @article, @qty)
                                                        """, con, tx);
                cmd.Parameters.AddWithValue("product", productName);
                cmd.Parameters.AddWithValue("article", row.Article);
                cmd.Parameters.AddWithValue("qty", row.Quantity);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            foreach (var row in assemblies.Where(a => !string.IsNullOrWhiteSpace(a.ProductName) && a.Quantity > 0 && a.ProductName != productName))
            {
                await using var cmd = new NpgsqlCommand("""
                                                        INSERT INTO product_assembly_spec (product_name, child_product_name, quantity)
                                                        VALUES (@product, @child, @qty)
                                                        """, con, tx);
                cmd.Parameters.AddWithValue("product", productName);
                cmd.Parameters.AddWithValue("child", row.ProductName);
                cmd.Parameters.AddWithValue("qty", row.Quantity);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            var seq = 1;
            foreach (var row in operations.Where(o => !string.IsNullOrWhiteSpace(o.OperationName)))
            {
                var sequence = row.SequenceNumber > 0 ? row.SequenceNumber : seq;
                await using (var link = new NpgsqlCommand("""
                                                          INSERT INTO product_operation_link (product_name, operation_name, sequence_number)
                                                          VALUES (@product, @operation, @seq)
                                                          """, con, tx))
                {
                    link.Parameters.AddWithValue("product", productName);
                    link.Parameters.AddWithValue("operation", row.OperationName);
                    link.Parameters.AddWithValue("seq", sequence);
                    await link.ExecuteNonQueryAsync(ct);
                }

                await using (var spec = new NpgsqlCommand("""
                                                          INSERT INTO product_operation_spec
                                                              (product_name, operation_name, sequence_number, equipment_type_name, operation_time_min, operation_description)
                                                          VALUES (@product, @operation, @seq, @equipment, @time, @description)
                                                          """, con, tx))
                {
                    spec.Parameters.AddWithValue("product", productName);
                    spec.Parameters.AddWithValue("operation", row.OperationName);
                    spec.Parameters.AddWithValue("seq", sequence);
                    spec.Parameters.AddWithValue("equipment", string.IsNullOrWhiteSpace(row.EquipmentTypeName) ? DBNull.Value : row.EquipmentTypeName);
                    spec.Parameters.AddWithValue("time", Math.Max(row.OperationTimeMin, 1));
                    spec.Parameters.AddWithValue("description", string.IsNullOrWhiteSpace(row.Description) ? DBNull.Value : row.Description);
                    await spec.ExecuteNonQueryAsync(ct);
                }

                seq = sequence + 1;
            }

            if (pendingAttachments.Count > 0)
            {
                foreach (var (fileName, data) in pendingAttachments)
                {
                    await using var cmd = new NpgsqlCommand("""
                                                            INSERT INTO product_attachment (product_name, file_name, file_data)
                                                            VALUES (@product, @file_name, @file_data)
                                                            """, con, tx);
                    cmd.Parameters.AddWithValue("product", productName);
                    cmd.Parameters.AddWithValue("file_name", fileName);
                    cmd.Parameters.AddWithValue("file_data", data);
                    await cmd.ExecuteNonQueryAsync(ct);
                }
            }

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<IReadOnlyList<RequirementRow>> GetRequirementEstimateAsync(string productName, CancellationToken ct = default)
    {
        const string sql = """
                           WITH RECURSIVE bom AS (
                               SELECT @product::varchar AS item, 1::numeric AS qty,
                                      ARRAY[@product::varchar] AS path, 0 AS depth
                               UNION ALL
                               SELECT pas.child_product_name, bom.qty * pas.quantity,
                                      bom.path || pas.child_product_name, bom.depth + 1
                               FROM bom
                               JOIN product_assembly_spec pas ON pas.product_name = bom.item
                               WHERE NOT (pas.child_product_name = ANY(bom.path))
                                 AND bom.depth < 32
                           ),
                           material_need AS (
                               SELECT pms.material_article AS article, SUM(bom.qty * pms.quantity) AS qty
                               FROM bom
                               JOIN product_material_spec pms ON pms.product_name = bom.item
                               GROUP BY pms.material_article
                           ),
                           component_need AS (
                               SELECT pcs.component_article AS article, SUM(bom.qty * pcs.quantity) AS qty
                               FROM bom
                               JOIN product_component_spec pcs ON pcs.product_name = bom.item
                               GROUP BY pcs.component_article
                           ),
                           material_stock_sum AS (
                               SELECT material_article AS article, SUM(quantity) AS qty FROM material_stock GROUP BY material_article
                           ),
                           component_stock_sum AS (
                               SELECT component_article AS article, SUM(quantity) AS qty FROM component_stock GROUP BY component_article
                           )
                           SELECT 'Материал', mn.article, m.name, m.unit, mn.qty,
                                  COALESCE(ms.qty, 0), m.purchase_price, COALESCE(s.delivery_days, 0)
                           FROM material_need mn
                           JOIN material m ON m.article = mn.article
                           LEFT JOIN material_stock_sum ms ON ms.article = mn.article
                           LEFT JOIN supplier s ON s.name = m.main_supplier
                           UNION ALL
                           SELECT 'Комплектующее', cn.article, c.name, c.unit, cn.qty,
                                  COALESCE(cs.qty, 0), c.purchase_price, COALESCE(s.delivery_days, 0)
                           FROM component_need cn
                           JOIN component c ON c.article = cn.article
                           LEFT JOIN component_stock_sum cs ON cs.article = cn.article
                           LEFT JOIN supplier s ON s.name = c.main_supplier
                           ORDER BY 1, 3
                           """;
        var result = new List<RequirementRow>();
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("product", productName);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new RequirementRow
            {
                Kind = reader.GetString(0),
                Article = reader.GetString(1),
                Name = reader.GetString(2),
                Unit = reader.GetString(3),
                RequiredQuantity = reader.GetDecimal(4),
                AvailableQuantity = reader.GetDecimal(5),
                PurchasePrice = reader.GetDecimal(6),
                DeliveryDays = reader.GetInt32(7)
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<GanttRow>> BuildGanttAsync(string productName, CancellationToken ct = default)
    {
        await EnsureSession3SchemaAsync(ct);
        var nodes = await GetBomNodesAsync(productName, ct);
        var operations = await GetOperationRowsForProductsAsync(nodes.Select(n => n.productName).Distinct().ToArray(), ct);
        var equipmentByType = await GetEquipmentByTypeAsync(ct);
        var equipmentAvailable = new Dictionary<string, int>(StringComparer.Ordinal);
        var productEnd = new Dictionary<string, int>(StringComparer.Ordinal);
        var result = new List<GanttRow>();

        foreach (var node in nodes.OrderByDescending(n => n.depth))
        {
            var startAfterChildren = nodes
                .Where(n => n.parentName == node.productName)
                .Select(n => productEnd.TryGetValue(n.productName, out var end) ? end : 0)
                .DefaultIfEmpty(0)
                .Max();
            var current = startAfterChildren;

            foreach (var op in operations.Where(o => o.productName == node.productName).OrderBy(o => o.sequence))
            {
                var equipment = ResolveEquipment(op.equipmentType, equipmentByType);
                var available = equipmentAvailable.TryGetValue(equipment, out var v) ? v : 0;
                var start = Math.Max(current, available);
                var row = new GanttRow
                {
                    Equipment = equipment,
                    ProductName = node.productName,
                    OperationName = op.operationName,
                    StartMinute = start,
                    DurationMinute = op.timeMin
                };
                result.Add(row);
                current = row.EndMinute;
                equipmentAvailable[equipment] = row.EndMinute;
            }

            productEnd[node.productName] = current;
        }

        return result.OrderBy(r => r.Equipment).ThenBy(r => r.StartMinute).ToList();
    }

    public async Task<IReadOnlyList<StockReportRow>> GetStockReportAsync(
        string kind,
        string? typeFilter,
        CancellationToken ct = default)
    {
        var isMaterials = kind == "materials";
        var sql = isMaterials
            ? """
              SELECT w.name, m.material_type, m.article, m.name, ms.quantity, m.unit
              FROM material_stock ms
              JOIN warehouse w ON w.warehouse_id = ms.warehouse_id
              JOIN material m ON m.article = ms.material_article
              WHERE (@type IS NULL OR m.material_type = @type)
              ORDER BY w.name, m.material_type, m.name
              """
            : """
              SELECT w.name, c.component_type, c.article, c.name, cs.quantity, c.unit
              FROM component_stock cs
              JOIN warehouse w ON w.warehouse_id = cs.warehouse_id
              JOIN component c ON c.article = cs.component_article
              WHERE (@type IS NULL OR c.component_type = @type)
              ORDER BY w.name, c.component_type, c.name
              """;
        var result = new List<StockReportRow>();
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        var typeParam = cmd.Parameters.Add("type", NpgsqlDbType.Varchar);
        typeParam.Value = string.IsNullOrWhiteSpace(typeFilter) || typeFilter == "Все типы"
            ? DBNull.Value
            : typeFilter;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new StockReportRow
            {
                WarehouseName = reader.GetString(0),
                TypeName = reader.GetString(1),
                Article = reader.GetString(2),
                Name = reader.GetString(3),
                Quantity = reader.GetDecimal(4),
                Unit = reader.GetString(5)
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<string>> GetStockTypesAsync(string kind, CancellationToken ct = default)
    {
        var sql = kind == "materials"
            ? "SELECT DISTINCT material_type FROM material ORDER BY material_type"
            : "SELECT DISTINCT component_type FROM component ORDER BY component_type";
        var result = new List<string> { "Все типы" };
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

    private async Task<IReadOnlyList<(string productName, string? parentName, int depth)>> GetBomNodesAsync(string productName, CancellationToken ct)
    {
        const string sql = """
                           WITH RECURSIVE bom AS (
                               SELECT @product::varchar AS product_name, NULL::varchar AS parent_name, 0 AS depth,
                                      ARRAY[@product::varchar] AS path
                               UNION ALL
                               SELECT pas.child_product_name, pas.product_name, bom.depth + 1,
                                      bom.path || pas.child_product_name
                               FROM bom
                               JOIN product_assembly_spec pas ON pas.product_name = bom.product_name
                               WHERE NOT (pas.child_product_name = ANY(bom.path))
                                 AND bom.depth < 32
                           )
                           SELECT product_name, parent_name, depth FROM bom
                           """;
        var result = new List<(string productName, string? parentName, int depth)>();
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("product", productName);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add((reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetInt32(2)));
        }

        return result
            .GroupBy(n => n.productName, StringComparer.Ordinal)
            .Select(g => g.OrderByDescending(n => n.depth).First())
            .ToList();
    }

    private async Task<IReadOnlyList<(string productName, int sequence, string operationName, string equipmentType, int timeMin)>>
        GetOperationRowsForProductsAsync(IReadOnlyList<string> productNames, CancellationToken ct)
    {
        const string sql = """
                           SELECT product_name, sequence_number, operation_name,
                                  COALESCE(equipment_type_name, 'Оборудование'), operation_time_min
                           FROM product_operation_spec
                           WHERE product_name = ANY(@products)
                           ORDER BY product_name, sequence_number
                           """;
        var result = new List<(string, int, string, string, int)>();
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        cmd.Parameters.AddWithValue("products", productNames.ToArray());
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add((reader.GetString(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4)));
        }

        return result;
    }

    private async Task<IReadOnlyDictionary<string, List<string>>> GetEquipmentByTypeAsync(CancellationToken ct)
    {
        const string sql = "SELECT equipment_type_name, marking FROM equipment ORDER BY equipment_type_name, marking";
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var type = reader.GetString(0);
            if (!result.TryGetValue(type, out var list))
            {
                list = [];
                result[type] = list;
            }

            list.Add(reader.GetString(1));
        }

        return result;
    }

    private static string ResolveEquipment(string equipmentType, IReadOnlyDictionary<string, List<string>> equipmentByType) =>
        equipmentByType.TryGetValue(equipmentType, out var list) && list.Count > 0
            ? list[0]
            : equipmentType;

    /// <summary>
    /// Returns true if adding <paramref name="childProduct"/> under <paramref name="parentProduct"/> would close a cycle in BOM.
    /// </summary>
    public async Task<bool> WouldCreateAssemblyCycleAsync(
        string parentProduct,
        string childProduct,
        CancellationToken ct = default)
    {
        if (string.Equals(parentProduct, childProduct, StringComparison.Ordinal))
        {
            return true;
        }

        const string sql = "SELECT product_name, child_product_name FROM product_assembly_spec";
        var childrenByProduct = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        await using var con = CreateConnection();
        await con.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, con);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var parent = reader.GetString(0);
            var child = reader.GetString(1);
            if (!childrenByProduct.TryGetValue(parent, out var list))
            {
                list = [];
                childrenByProduct[parent] = list;
            }

            list.Add(child);
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(childProduct);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current))
            {
                continue;
            }

            if (string.Equals(current, parentProduct, StringComparison.Ordinal))
            {
                return true;
            }

            if (!childrenByProduct.TryGetValue(current, out var children))
            {
                continue;
            }

            foreach (var next in children)
            {
                queue.Enqueue(next);
            }
        }

        return false;
    }
}
