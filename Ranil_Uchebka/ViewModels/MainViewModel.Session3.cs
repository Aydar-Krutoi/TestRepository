using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ranil_Uchebka.Models;

namespace Ranil_Uchebka.ViewModels;

public partial class MainViewModel
{
    [ObservableProperty]
    private ProductOption? _selectedSpecProduct;

    [ObservableProperty]
    private string _specProductName = string.Empty;

    [ObservableProperty]
    private string _newSpecDimensionName = string.Empty;

    [ObservableProperty]
    private string _newSpecDimensionUnit = string.Empty;

    [ObservableProperty]
    private string _newSpecDimensionValue = string.Empty;

    [ObservableProperty]
    private string _newSpecMaterialArticle = string.Empty;

    [ObservableProperty]
    private string _newSpecMaterialQuantity = "1";

    [ObservableProperty]
    private string _newSpecComponentArticle = string.Empty;

    [ObservableProperty]
    private string _newSpecComponentQuantity = "1";

    [ObservableProperty]
    private ProductOption? _newSpecAssemblyProduct;

    [ObservableProperty]
    private string _newSpecAssemblyQuantity = "1";

    [ObservableProperty]
    private string _newSpecOperationName = string.Empty;

    [ObservableProperty]
    private string _newSpecOperationTime = "10";

    [ObservableProperty]
    private string _newSpecOperationDescription = string.Empty;

    [ObservableProperty]
    private string? _newSpecEquipmentType;

    [ObservableProperty]
    private OrderListItem? _planningOrder;

    [ObservableProperty]
    private FilterOption? _selectedStockReportKind;

    [ObservableProperty]
    private string? _selectedStockReportType;

    public ObservableCollection<ProductOption> Products { get; } = [];
    public ObservableCollection<string> EquipmentTypes { get; } = [];
    public ObservableCollection<SpecMaterialRow> SpecMaterials { get; } = [];
    public ObservableCollection<SpecComponentRow> SpecComponents { get; } = [];
    public ObservableCollection<SpecAssemblyRow> SpecAssemblies { get; } = [];
    public ObservableCollection<SpecOperationRow> SpecOperations { get; } = [];
    public ObservableCollection<ProductDimensionRow> SpecDimensions { get; } = [];
    public ObservableCollection<ProductAttachmentRow> SpecAttachments { get; } = [];
    public ObservableCollection<RequirementRow> RequirementRows { get; } = [];
    public ObservableCollection<GanttRow> GanttRows { get; } = [];
    public ObservableCollection<StockReportRow> StockReportRows { get; } = [];
    public ObservableCollection<StockReportWarehouseGroup> StockReportGroups { get; } = [];
    public ObservableCollection<string> StockReportTypes { get; } = [];

    public ObservableCollection<FilterOption> StockReportKindItems { get; } =
    [
        new() { Key = "materials", DisplayName = "Материалы" },
        new() { Key = "components", DisplayName = "Комплектующие" }
    ];

    public decimal RequirementTotalCost => RequirementRows.Sum(r => r.Cost);
    public int RequirementDeliveryDays => RequirementRows.Where(r => r.MissingQuantity > 0).Select(r => r.DeliveryDays).DefaultIfEmpty(0).Max();
    public int ProductionTimeMinutes => GanttRows.Select(r => r.EndMinute).DefaultIfEmpty(0).Max();
    public int TotalOrderTimeMinutes => ProductionTimeMinutes + RequirementDeliveryDays * 24 * 60;
    public decimal StockReportTotalQuantity => StockReportRows.Sum(r => r.Quantity);

    private bool _stockReportLoading;

    partial void OnSelectedStockReportKindChanged(FilterOption? value)
    {
        if (value is not null && !_stockReportLoading && IsStockReportSection)
        {
            _ = LoadStockReportTypesAsync();
        }
    }

    partial void OnSelectedStockReportTypeChanged(string? value)
    {
        if (SelectedStockReportKind is not null && !_stockReportLoading && IsStockReportSection && value is not null)
        {
            _ = RefreshStockReportAsync();
        }
    }

    partial void OnSelectedSpecProductChanged(ProductOption? value)
    {
        if (value is not null)
        {
            _ = LoadSpecificationAsync(value.Name);
        }
    }

    partial void OnPlanningOrderChanged(OrderListItem? value)
    {
        if (value is not null)
        {
            _ = CalculateOrderPlanningAsync();
        }
    }

    [RelayCommand]
    private async Task OpenSpecificationsAsync()
    {
        if (!CanAccessSpecifications)
        {
            return;
        }

        CurrentSection = "Specifications";
        await LoadProductsAndEquipmentAsync();
        SelectedSpecProduct ??= Products.FirstOrDefault(p => p.Name == "Конвейер") ?? Products.FirstOrDefault();
    }

    [RelayCommand]
    private async Task OpenPlanningAsync()
    {
        if (!CanAccessPlanning)
        {
            return;
        }

        CurrentSection = "Planning";
        await RefreshOrdersAsync();
        PlanningOrder ??= Orders.FirstOrDefault();
        if (PlanningOrder is not null)
        {
            await CalculateOrderPlanningAsync();
        }
    }

    [RelayCommand]
    private async Task OpenPlanningForSelectedOrderAsync()
    {
        if (!CanAccessPlanning || SelectedOrder is null)
        {
            SetStatus("Выберите заказ в списке.");
            return;
        }

        CurrentSection = "Planning";
        PlanningOrder = SelectedOrder;
        await CalculateOrderPlanningAsync();
    }

    [RelayCommand]
    private async Task OpenStockReportAsync()
    {
        if (!CanAccessStockReport)
        {
            return;
        }

        CurrentSection = "StockReport";
        SelectedStockReportKind ??= StockReportKindItems.FirstOrDefault();
        await LoadStockReportTypesAsync();
    }

    private async Task LoadProductsAndEquipmentAsync()
    {
        await _databaseService.EnsureSession3SchemaAsync();
        Products.Clear();
        foreach (var product in await _databaseService.GetProductsAsync())
        {
            Products.Add(product);
        }

        EquipmentTypes.Clear();
        foreach (var type in await _databaseService.GetEquipmentTypesAsync())
        {
            EquipmentTypes.Add(type);
        }

        NewSpecEquipmentType ??= EquipmentTypes.FirstOrDefault();
    }

    private async Task LoadSpecificationAsync(string productName)
    {
        await LoadProductsAndEquipmentAsync();
        SpecProductName = productName;

        SpecDimensions.Clear();
        var dimensions = await _databaseService.GetProductDimensionsAsync(productName);
        if (dimensions.Count > 0)
        {
            foreach (var row in dimensions)
            {
                SpecDimensions.Add(row);
            }
        }
        else
        {
            var product = Products.FirstOrDefault(p => p.Name == productName);
            if (product is not null && !string.IsNullOrWhiteSpace(product.Dimensions) &&
                !string.Equals(product.Dimensions, "по спецификации", StringComparison.OrdinalIgnoreCase))
            {
                SpecDimensions.Add(new ProductDimensionRow
                {
                    DimensionName = product.Dimensions,
                    Unit = "—",
                    DimensionValue = 0
                });
            }
        }

        SpecAttachments.Clear();
        foreach (var attachment in await _databaseService.GetProductAttachmentsAsync(productName))
        {
            SpecAttachments.Add(attachment);
        }

        SpecMaterials.Clear();
        foreach (var row in await _databaseService.GetSpecMaterialsAsync(productName))
        {
            SpecMaterials.Add(row);
        }

        SpecComponents.Clear();
        foreach (var row in await _databaseService.GetSpecComponentsAsync(productName))
        {
            SpecComponents.Add(row);
        }

        SpecAssemblies.Clear();
        foreach (var row in await _databaseService.GetSpecAssembliesAsync(productName))
        {
            SpecAssemblies.Add(row);
        }

        SpecOperations.Clear();
        foreach (var row in await _databaseService.GetSpecOperationsAsync(productName))
        {
            SpecOperations.Add(row);
        }
    }

    [RelayCommand]
    private async Task SaveSpecificationAsync()
    {
        if (!CanAccessSpecifications || string.IsNullOrWhiteSpace(SpecProductName))
        {
            SetStatus("Выберите или укажите изделие.");
            return;
        }

        await _databaseService.SaveSpecificationAsync(
            SpecProductName.Trim(),
            BuildDimensionsSummary(),
            SpecDimensions.ToList(),
            SpecMaterials.ToList(),
            SpecComponents.ToList(),
            SpecAssemblies.ToList(),
            SpecOperations.ToList(),
            SpecAttachments
                .Where(a => a.IsPending && a.PendingData is not null)
                .Select(a => (a.FileName, a.PendingData!))
                .ToList());
        SetStatus("Спецификация сохранена.");
        await LoadSpecificationAsync(SpecProductName.Trim());
        await LoadProductsAndEquipmentAsync();
    }

    private string BuildDimensionsSummary() =>
        SpecDimensions.Count == 0
            ? "по спецификации"
            : string.Join("; ", SpecDimensions.Select(d =>
                d.DimensionValue > 0
                    ? $"{d.DimensionName} = {d.DimensionValue} {d.Unit}"
                    : d.DimensionName));

    [RelayCommand]
    private async Task RefreshSpecificationAsync()
    {
        if (SelectedSpecProduct is not null)
        {
            await LoadSpecificationAsync(SelectedSpecProduct.Name);
        }
    }

    [RelayCommand]
    private async Task AddSpecMaterial()
    {
        var article = NewSpecMaterialArticle.Trim();
        if (string.IsNullOrWhiteSpace(article))
        {
            SetStatus("Укажите артикул материала.");
            return;
        }

        if (!decimal.TryParse(NewSpecMaterialQuantity.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var qty) || qty <= 0)
        {
            SetStatus("Укажите количество материала.");
            return;
        }

        var material = await _databaseService.GetMaterialReferenceByArticleAsync(article);
        if (material is null)
        {
            SetStatus($"Материал с артикулом «{article}» не найден.");
            return;
        }

        var existing = SpecMaterials.FirstOrDefault(m =>
            string.Equals(m.Article, article, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Quantity += qty;
        }
        else
        {
            SpecMaterials.Add(new SpecMaterialRow
            {
                Article = article,
                Name = material.Value.Name,
                Unit = material.Value.Unit,
                Quantity = qty
            });
        }

        NewSpecMaterialArticle = string.Empty;
        NewSpecMaterialQuantity = "1";
    }

    [RelayCommand]
    private void RemoveLastSpecMaterial()
    {
        if (SpecMaterials.Count > 0) SpecMaterials.RemoveAt(SpecMaterials.Count - 1);
    }

    [RelayCommand]
    private async Task AddSpecComponent()
    {
        var article = NewSpecComponentArticle.Trim();
        if (string.IsNullOrWhiteSpace(article))
        {
            SetStatus("Укажите артикул комплектующего.");
            return;
        }

        if (!decimal.TryParse(NewSpecComponentQuantity.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var qty) || qty <= 0)
        {
            SetStatus("Укажите количество комплектующего.");
            return;
        }

        var component = await _databaseService.GetComponentReferenceByArticleAsync(article);
        if (component is null)
        {
            SetStatus($"Комплектующее с артикулом «{article}» не найдено.");
            return;
        }

        var existing = SpecComponents.FirstOrDefault(c =>
            string.Equals(c.Article, article, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Quantity += qty;
        }
        else
        {
            SpecComponents.Add(new SpecComponentRow
            {
                Article = article,
                Name = component.Value.Name,
                Unit = component.Value.Unit,
                Quantity = qty
            });
        }

        NewSpecComponentArticle = string.Empty;
        NewSpecComponentQuantity = "1";
    }

    [RelayCommand]
    private void RemoveLastSpecComponent()
    {
        if (SpecComponents.Count > 0) SpecComponents.RemoveAt(SpecComponents.Count - 1);
    }

    [RelayCommand]
    private async Task AddSpecAssemblyAsync()
    {
        if (NewSpecAssemblyProduct is null ||
            !decimal.TryParse(NewSpecAssemblyQuantity.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var qty) || qty <= 0)
        {
            SetStatus("Выберите сборочную единицу и количество.");
            return;
        }

        if (string.Equals(NewSpecAssemblyProduct.Name, SpecProductName, StringComparison.Ordinal))
        {
            SetStatus("Нельзя добавить изделие в состав самого себя.");
            return;
        }

        if (await _databaseService.WouldCreateAssemblyCycleAsync(SpecProductName, NewSpecAssemblyProduct.Name))
        {
            SetStatus("Такая сборка создаст цикл в спецификации (изделие уже входит в состав выбранной детали).");
            return;
        }

        SpecAssemblies.Add(new SpecAssemblyRow { ProductName = NewSpecAssemblyProduct.Name, Quantity = qty });
        NewSpecAssemblyQuantity = "1";
    }

    [RelayCommand]
    private void RemoveLastSpecAssembly()
    {
        if (SpecAssemblies.Count > 0) SpecAssemblies.RemoveAt(SpecAssemblies.Count - 1);
    }

    [RelayCommand]
    private void AddSpecOperation()
    {
        if (!int.TryParse(NewSpecOperationTime, out var time) || time <= 0 || string.IsNullOrWhiteSpace(NewSpecOperationName))
        {
            SetStatus("Укажите операцию и время.");
            return;
        }

        SpecOperations.Add(new SpecOperationRow
        {
            SequenceNumber = SpecOperations.Count + 1,
            OperationName = NewSpecOperationName.Trim(),
            EquipmentTypeName = NewSpecEquipmentType ?? string.Empty,
            OperationTimeMin = time,
            Description = NewSpecOperationDescription.Trim()
        });
        NewSpecOperationName = string.Empty;
        NewSpecOperationTime = "10";
        NewSpecOperationDescription = string.Empty;
    }

    [RelayCommand]
    private void RemoveLastSpecOperation()
    {
        if (SpecOperations.Count > 0) SpecOperations.RemoveAt(SpecOperations.Count - 1);
    }

    [RelayCommand]
    private void AddSpecDimension()
    {
        if (string.IsNullOrWhiteSpace(NewSpecDimensionName) || string.IsNullOrWhiteSpace(NewSpecDimensionUnit))
        {
            SetStatus("Укажите наименование замера и единицу измерения.");
            return;
        }

        if (!decimal.TryParse(NewSpecDimensionValue.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var value) || value <= 0)
        {
            SetStatus("Укажите положительное значение замера.");
            return;
        }

        SpecDimensions.Add(new ProductDimensionRow
        {
            DimensionName = NewSpecDimensionName.Trim(),
            Unit = NewSpecDimensionUnit.Trim(),
            DimensionValue = value
        });
        NewSpecDimensionName = string.Empty;
        NewSpecDimensionUnit = string.Empty;
        NewSpecDimensionValue = string.Empty;
    }

    [RelayCommand]
    private void RemoveLastSpecDimension()
    {
        if (SpecDimensions.Count > 0)
        {
            SpecDimensions.RemoveAt(SpecDimensions.Count - 1);
        }
    }

    public void AddPendingSpecAttachments(IReadOnlyList<(string fileName, byte[] data)> files)
    {
        if (!CanAccessSpecifications)
        {
            return;
        }

        foreach (var (fileName, data) in files)
        {
            SpecAttachments.Add(new ProductAttachmentRow
            {
                AttachmentId = 0,
                FileName = fileName,
                PendingData = data
            });
        }

        SetStatus($"Добавлено чертежей: {files.Count}. Сохраните спецификацию для записи в БД.");
    }

    [RelayCommand]
    private async Task RemoveSpecAttachmentAsync(ProductAttachmentRow? row)
    {
        if (row is null || !CanAccessSpecifications || string.IsNullOrWhiteSpace(SpecProductName))
        {
            return;
        }

        if (row.IsPending)
        {
            SpecAttachments.Remove(row);
        }
        else
        {
            await _databaseService.DeleteProductAttachmentAsync(row.AttachmentId, SpecProductName.Trim());
            SpecAttachments.Remove(row);
        }

        SetStatus("Чертеж удалён из спецификации.");
    }

    public async Task<(string fileName, byte[] data)?> GetProductAttachmentForExportAsync(ProductAttachmentRow? row)
    {
        if (row is null || string.IsNullOrWhiteSpace(SpecProductName))
        {
            return null;
        }

        if (row.IsPending && row.PendingData is not null)
        {
            return (row.FileName, row.PendingData);
        }

        if (!row.CanExport)
        {
            return null;
        }

        var data = await _databaseService.GetProductAttachmentDataAsync(row.AttachmentId, SpecProductName.Trim());
        return data is null ? null : (row.FileName, data);
    }

    [RelayCommand]
    private async Task CalculateOrderPlanningAsync()
    {
        if (PlanningOrder is null)
        {
            SetStatus("Выберите заказ для расчёта.");
            return;
        }

        try
        {
            RequirementRows.Clear();
            foreach (var row in await _databaseService.GetRequirementEstimateAsync(PlanningOrder.ProductName))
            {
                RequirementRows.Add(row);
            }

            GanttRows.Clear();
            foreach (var row in await _databaseService.BuildGanttAsync(PlanningOrder.ProductName))
            {
                GanttRows.Add(row);
            }

            NotifyPlanningTotals();
            OnPropertyChanged(nameof(GanttRowsSnapshot));

            if (RequirementRows.Count == 0 && GanttRows.Count == 0)
            {
                SetStatus(
                    $"Для изделия «{PlanningOrder.ProductName}» нет спецификации или в сборках есть цикл. " +
                    "Проверьте раздел «Спецификации» (мастер): материалы, сборки, операции.");
            }
            else
            {
                SetStatus("Расчёт материалов, доставки и диаграммы Ганта выполнен.");
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Ошибка расчёта: {ex.Message}");
        }
    }

    public IReadOnlyList<GanttRow> GanttRowsSnapshot => GanttRows.ToList();

    private void NotifyPlanningTotals()
    {
        OnPropertyChanged(nameof(RequirementTotalCost));
        OnPropertyChanged(nameof(RequirementDeliveryDays));
        OnPropertyChanged(nameof(ProductionTimeMinutes));
        OnPropertyChanged(nameof(TotalOrderTimeMinutes));
    }

    private async Task LoadStockReportTypesAsync()
    {
        if (SelectedStockReportKind is null || _stockReportLoading)
        {
            return;
        }

        _stockReportLoading = true;
        try
        {
            StockReportTypes.Clear();
            foreach (var type in await _databaseService.GetStockTypesAsync(SelectedStockReportKind.Key))
            {
                StockReportTypes.Add(type);
            }

            SelectedStockReportType = StockReportTypes.FirstOrDefault();
            await RefreshStockReportCoreAsync();
        }
        finally
        {
            _stockReportLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshStockReportAsync()
    {
        if (SelectedStockReportKind is null || _stockReportLoading)
        {
            return;
        }

        _stockReportLoading = true;
        try
        {
            await RefreshStockReportCoreAsync();
        }
        finally
        {
            _stockReportLoading = false;
        }
    }

    private async Task RefreshStockReportCoreAsync()
    {
        if (SelectedStockReportKind is null)
        {
            return;
        }

        try
        {
            StockReportRows.Clear();
            StockReportGroups.Clear();
            foreach (var row in await _databaseService.GetStockReportAsync(SelectedStockReportKind.Key, SelectedStockReportType))
            {
                StockReportRows.Add(row);
            }

            foreach (var group in StockReportRows.GroupBy(r => r.WarehouseName).OrderBy(g => g.Key))
            {
                var rows = group.OrderBy(r => r.TypeName).ThenBy(r => r.Name).ToList();
                StockReportGroups.Add(new StockReportWarehouseGroup
                {
                    WarehouseName = group.Key,
                    TotalQuantity = rows.Sum(r => r.Quantity),
                    Rows = rows
                });
            }

            OnPropertyChanged(nameof(StockReportTotalQuantity));
            SetStatus("Отчёт по остаткам обновлён.");
        }
        catch (Exception ex)
        {
            SetStatus($"Ошибка загрузки отчёта: {ex.Message}");
        }
    }

    public string BuildSpecificationPrintText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Спецификация изделия: {SpecProductName}");
        sb.AppendLine("Размеры:");
        foreach (var row in SpecDimensions)
        {
            sb.AppendLine(row.DimensionValue > 0
                ? $"- {row.DimensionName} = {row.DimensionValue} {row.Unit}"
                : $"- {row.DimensionName}");
        }

        sb.AppendLine("Чертежи:");
        foreach (var row in SpecAttachments)
        {
            sb.AppendLine($"- {row.DisplayText}");
        }

        sb.AppendLine();
        sb.AppendLine("Материалы:");
        foreach (var row in SpecMaterials) sb.AppendLine($"- {row.Article} {row.Name} — {row.Quantity} {row.Unit}");
        sb.AppendLine("Комплектующие:");
        foreach (var row in SpecComponents) sb.AppendLine($"- {row.Article} {row.Name} — {row.Quantity} {row.Unit}");
        sb.AppendLine("Сборочные единицы:");
        foreach (var row in SpecAssemblies) sb.AppendLine($"- {row.ProductName} — {row.Quantity}");
        sb.AppendLine("Операции:");
        foreach (var row in SpecOperations) sb.AppendLine($"{row.SequenceNumber}. {row.OperationName}, {row.EquipmentTypeName}, {row.OperationTimeMin} мин. {row.Description}");
        return sb.ToString();
    }

    public string BuildPlanningPrintText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Расчёт заказа: {PlanningOrder?.OrderCode} ({PlanningOrder?.OrderName})");
        sb.AppendLine($"Себестоимость: {RequirementTotalCost:0.##}; доставка: {RequirementDeliveryDays} дн.; производство: {ProductionTimeMinutes} мин.");
        sb.AppendLine();
        sb.AppendLine("Материалы и комплектующие:");
        foreach (var row in RequirementRows)
        {
            sb.AppendLine($"{row.Kind} {row.Article} {row.Name}: нужно {row.RequiredQuantity}, есть {row.AvailableQuantity}, не хватает {row.MissingQuantity}, стоимость {row.Cost:0.##}");
        }
        sb.AppendLine();
        sb.AppendLine("Диаграмма Ганта:");
        foreach (var row in GanttRows)
        {
            sb.AppendLine($"{row.Equipment}: {row.Display}");
        }

        return sb.ToString();
    }

    public string BuildStockReportPrintText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Отчёт по остаткам: {SelectedStockReportKind?.DisplayName}, тип: {SelectedStockReportType}");
        foreach (var group in StockReportGroups)
        {
            sb.AppendLine();
            sb.AppendLine($"Склад: {group.WarehouseName}; итог: {group.TotalQuantity:0.###}");
            foreach (var row in group.Rows)
            {
                sb.AppendLine($"- {row.TypeName}; {row.Article}; {row.Name}; {row.Quantity} {row.Unit}");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"Общий итог: {StockReportTotalQuantity:0.###}");
        return sb.ToString();
    }
}
