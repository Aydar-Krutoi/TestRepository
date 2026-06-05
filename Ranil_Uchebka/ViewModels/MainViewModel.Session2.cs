using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Npgsql;
using Ranil_Uchebka.Models;
using Ranil_Uchebka.Services;

namespace Ranil_Uchebka.ViewModels;

public partial class MainViewModel
{
    [ObservableProperty]
    private string _orderFilterKey = "all";

    [ObservableProperty]
    private OrderListItem? _selectedOrder;

    [ObservableProperty]
    private OrderEditorState _orderEditor = new();

    [ObservableProperty]
    private string _orderActionComment = string.Empty;

    [ObservableProperty]
    private WorkshopInfo? _selectedWorkshop;

    [ObservableProperty]
    private string _selectedPlanIconType = WorkshopIconTypes.Equipment;

    [ObservableProperty]
    private FilterOption? _selectedPlanIconTypeItem;

    [ObservableProperty]
    private string _failureEquipment = string.Empty;

    [ObservableProperty]
    private string _failureReason = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FailureStartDateCalendar))]
    private DateTimeOffset? _failureStartDate = DateTimeOffset.Now;

    [ObservableProperty]
    private string _failureStartTime = DateTime.Now.ToString("HH:mm");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FailureEndDateCalendar))]
    private DateTimeOffset? _failureEndDate = DateTimeOffset.Now;

    [ObservableProperty]
    private string _failureEndTime = DateTime.Now.ToString("HH:mm");

    [ObservableProperty]
    private EquipmentFailureRow? _selectedFailure;

    public DateTime? FailureStartDateCalendar
    {
        get => FailureStartDate?.DateTime.Date;
        set => FailureStartDate = value.HasValue ? new DateTimeOffset(value.Value.Date) : null;
    }

    public DateTime? FailureEndDateCalendar
    {
        get => FailureEndDate?.DateTime.Date;
        set => FailureEndDate = value.HasValue ? new DateTimeOffset(value.Value.Date) : null;
    }

    [ObservableProperty]
    private OrderListItem? _qualityOrder;

    [ObservableProperty]
    private WorkshopMarkerModel? _selectedDraftWorkshopMarker;

    [ObservableProperty]
    private string _newCustomerLogin = string.Empty;

    [ObservableProperty]
    private string _newCustomerFullName = string.Empty;

    [ObservableProperty]
    private string _newCustomerPassword = string.Empty;

    public ObservableCollection<OrderListItem> Orders { get; } = [];
    public ObservableCollection<OrderHistoryItem> OrderHistory { get; } = [];
    public ObservableCollection<UserOption> Customers { get; } = [];
    public ObservableCollection<WorkshopInfo> Workshops { get; } = [];
    public ObservableCollection<WorkshopMarkerModel> WorkshopMarkers { get; } = [];
    public ObservableCollection<WorkshopMarkerModel> DraftWorkshopMarkers { get; } = [];
    public ObservableCollection<EquipmentFailureRow> EquipmentFailures { get; } = [];
    public ObservableCollection<QualityCheckRow> QualityChecks { get; } = [];
    public ObservableCollection<string> EquipmentMarkings { get; } = [];
    public ObservableCollection<string> FailureReasons { get; } = [];

    public ObservableCollection<FilterOption> PlanIconTypeItems { get; } =
        new(WorkshopIconTypes.GetFilterOptions());

    public ObservableCollection<FilterOption> OrderFilterItems { get; } =
    [
        new() { Key = "all", DisplayName = "Все" },
        new() { Key = "new", DisplayName = "Новые" },
        new() { Key = "current", DisplayName = "Текущие" },
        new() { Key = "done", DisplayName = "Выполненные" },
        new() { Key = "rejected", DisplayName = "Отклонённые" }
    ];

    [ObservableProperty]
    private FilterOption? _selectedOrderFilter;

    [ObservableProperty]
    private UserOption? _selectedOrderCustomer;

    [ObservableProperty]
    private FilterOption? _selectedStatusTransition;

    public ObservableCollection<OrderDimensionRow> OrderDimensions { get; } = [];
    public ObservableCollection<OrderAttachmentRow> OrderAttachments { get; } = [];
    public ObservableCollection<FilterOption> AvailableStatusTransitions { get; } = [];

    public string OrderStatusHelpText { get; private set; } = string.Empty;

    private string? _preservedOrderCode;

    public bool CanAccessOrders => IsAuthenticated;
    public bool CanAccessWorkshop => IsDirector;
    public bool CanAccessFailures => IsMaster;
    public bool CanAccessQuality => IsMaster;
    public bool CanViewOrderHistory => IsDirector || IsManager;
    public bool IsOrdersSection => CurrentSection == "Orders";
    public bool IsWorkshopSection => CurrentSection == "Workshop";
    public bool IsFailuresSection => CurrentSection == "Failures";
    public bool IsQualitySection => CurrentSection == "Quality";

    public bool CanCreateOrder => IsCustomer || IsManager;
    public bool CanEditOrderFields => OrderEditor.StatusName == "Новый" && (IsCustomer || IsManager);
    public bool CanSaveOrder => CanCreateOrder && (OrderEditor.IsNew || OrderEditor.StatusName == "Новый");

    /// <summary>Заказчик на «Новый» не вводит цену и срок — их задаёт производство.</summary>
    public bool HideOrderCostAndDateFields =>
        IsCustomer && (OrderEditor.IsNew || OrderEditor.StatusName == "Новый");

    public bool ShowOrderCostAndDateFields => !HideOrderCostAndDateFields;

    public bool ShowOrderCostAndDateReadOnly => ShowOrderCostAndDateFields && IsCustomer;

    public bool CanEditOrderCostAndDate =>
        ShowOrderCostAndDateFields && !IsCustomer &&
        ((IsManager && OrderEditor.StatusName is "Новый" or "Составление спецификации") ||
         (IsConstructor && OrderEditor.StatusName == "Составление спецификации"));

    public bool CanEditOrderDescription => CanSaveOrder || CanEditOrderCostAndDate;

    public bool ShowOrderFieldsEditable => CanEditOrderDescription;

    public bool ShowOrderFieldsReadOnly => !CanEditOrderDescription && !OrderEditor.IsNew && OrderEditor.Number > 0;

    public string OrderNameDisplay =>
        string.IsNullOrWhiteSpace(OrderEditor.OrderName) ? "не указано" : OrderEditor.OrderName;

    public string OrderDescriptionDisplay =>
        string.IsNullOrWhiteSpace(OrderEditor.Description) ? "не указано" : OrderEditor.Description;

    public bool HasOrderDimensions => OrderDimensions.Count > 0;

    public bool ShowOrderDimensionsEmpty => !HasOrderDimensions;

    public bool CanEditCustomerLogin => IsManager && OrderEditor.IsNew;

    public bool ShowOrderCustomerReadOnly => !CanEditCustomerLogin && !string.IsNullOrWhiteSpace(OrderEditor.CustomerLogin);

    public bool CanCreateCustomerForOrder => IsManager && OrderEditor.IsNew;

    public string OrderCustomerDisplay
    {
        get
        {
            var login = OrderEditor.CustomerLogin;
            if (string.IsNullOrWhiteSpace(login))
            {
                return "—";
            }

            var name = SelectedOrder?.CustomerLogin == login
                ? SelectedOrder.CustomerName
                : Customers.FirstOrDefault(c => c.Login == login)?.DisplayName;
            return string.IsNullOrWhiteSpace(name) ? login : $"{login} — {name}";
        }
    }

    public bool CanSaveOrderDetails =>
        !OrderEditor.IsNew && OrderEditor.Number > 0 &&
        (CanEditOrderCostAndDate || (IsManager && OrderEditor.StatusName is "Новый" or "Составление спецификации"));

    public bool CanCustomerCancelOrder =>
        IsCustomer && !OrderEditor.IsNew && OrderEditor.Number > 0 &&
        OrderEditor.StatusName is "Новый" or "Составление спецификации" or "Подтверждение";

    public bool CanChangeOrderStatus => IsManager || IsConstructor || IsMaster || CanCustomerCancelOrder;

    public bool HasAvailableStatusTransitions => AvailableStatusTransitions.Count > 0;

    public bool ShowOrderStatusHelp =>
        CanChangeOrderStatus && !OrderEditor.IsNew && OrderEditor.Number > 0 && !HasAvailableStatusTransitions;

    public bool ShowStatusCommentField =>
        SelectedStatusTransition?.Key == "Отклонен" && !IsCustomer;

    public bool CanEditOrderAttachments => CanEditOrderDescription;

    public bool ShowOrderAttachmentsSection =>
        !OrderEditor.IsNew && OrderEditor.Number > 0 &&
        (CanEditOrderAttachments || OrderAttachments.Count > 0);

    public bool HasOrderAttachments => OrderAttachments.Count > 0;

    public bool ShowOrderAttachmentsEmpty => ShowOrderAttachmentsSection && !HasOrderAttachments;

    public string OrderCostText
    {
        get => OrderEditor.Cost?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                OrderEditor.Cost = null;
                OnPropertyChanged(nameof(OrderCostAndDateDisplay));
                return;
            }

            if (decimal.TryParse(value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var cost))
            {
                OrderEditor.Cost = cost;
                OnPropertyChanged(nameof(OrderCostAndDateDisplay));
            }
        }
    }

    public DateTimeOffset? OrderPlannedDate
    {
        get => OrderEditor.PlannedCompletionDate.HasValue
            ? new DateTimeOffset(OrderEditor.PlannedCompletionDate.Value)
            : null;
        set
        {
            OrderEditor.PlannedCompletionDate = value?.Date;
            OnPropertyChanged();
            OnPropertyChanged(nameof(OrderPlannedDateCalendar));
            OnPropertyChanged(nameof(OrderCostAndDateDisplay));
        }
    }

    public DateTime? OrderPlannedDateCalendar
    {
        get => OrderPlannedDate?.DateTime.Date;
        set => OrderPlannedDate = value.HasValue ? new DateTimeOffset(value.Value.Date) : null;
    }

    public string OrderCostAndDateDisplay
    {
        get
        {
            var cost = OrderEditor.Cost.HasValue
                ? $"{OrderEditor.Cost.Value:0.##} руб."
                : "не рассчитана";
            var date = OrderEditor.PlannedCompletionDate.HasValue
                ? OrderEditor.PlannedCompletionDate.Value.ToString("dd.MM.yyyy")
                : "не назначена";
            return $"Стоимость: {cost}   Срок: {date}";
        }
    }

    partial void OnOrderEditorChanged(OrderEditorState value) => NotifyOrderEditorUiProperties();

    partial void OnSelectedOrderChanged(OrderListItem? value)
    {
        if (value is null)
        {
            if (!OrderEditor.IsNew)
            {
                ClearOrderEditorState();
            }

            return;
        }

        _preservedOrderCode = value.OrderCode;
        if (OrderEditor.IsNew)
        {
            OrderEditor.IsNew = false;
            OrderEditor.Number = value.Number;
            OrderEditor.OrderDate = value.OrderDate;
            OrderEditor.OrderCode = value.OrderCode;
            OrderEditor.StatusName = value.StatusName;
            OrderEditor.StatusId = value.StatusId;
            NotifyOrderEditorUiProperties();
        }

        _ = LoadOrderEditorSafeAsync(value.Number, value.OrderDate);
    }

    private void ClearOrderEditorState()
    {
        OrderEditor = new OrderEditorState
        {
            IsNew = false,
            ProductName = string.Empty,
            StatusName = string.Empty
        };
        SelectedOrderCustomer = null;
        OrderActionComment = string.Empty;
        _preservedOrderCode = null;
        OrderHistory.Clear();
        SyncOrderDimensionsFromEditor();
        SyncOrderAttachmentsFromEditor();
        NotifyOrderEditorUiProperties();
    }

    partial void OnSelectedOrderCustomerChanged(UserOption? value)
    {
        if (value is not null && CanEditCustomerLogin)
        {
            OrderEditor.CustomerLogin = value.Login;
            OnPropertyChanged(nameof(OrderCustomerDisplay));
        }
    }

    partial void OnSelectedWorkshopChanged(WorkshopInfo? value)
    {
        if (value is null)
        {
            return;
        }

        _ = LoadWorkshopMarkersAsync(value.WorkshopId);
    }

    partial void OnSelectedDraftWorkshopMarkerChanged(WorkshopMarkerModel? value) =>
        OnPropertyChanged(nameof(CanRemoveWorkshopMarker));

    partial void OnSelectedPlanIconTypeItemChanged(FilterOption? value)
    {
        if (value is not null)
        {
            SelectedPlanIconType = value.Key;
        }
    }

    partial void OnSelectedOrderFilterChanged(FilterOption? value)
    {
        if (value is null)
        {
            return;
        }

        OrderFilterKey = value.Key;
        _ = RefreshOrdersAsync();
    }

    [RelayCommand]
    private async Task OpenOrdersAsync()
    {
        if (!CanAccessOrders)
        {
            return;
        }

        CurrentSection = "Orders";
        SelectedOrderFilter ??= OrderFilterItems.FirstOrDefault();
        await RefreshOrdersAsync();
        if (!IsCustomer)
        {
            await LoadCustomersAsync();
        }
    }

    [RelayCommand]
    private async Task OpenWorkshopAsync()
    {
        if (!CanAccessWorkshop)
        {
            return;
        }

        CurrentSection = "Workshop";
        SelectedPlanIconTypeItem = PlanIconTypeItems.FirstOrDefault(i => i.Key == SelectedPlanIconType)
            ?? PlanIconTypeItems.FirstOrDefault();
        await LoadWorkshopsAsync();
    }

    [RelayCommand]
    private async Task OpenFailuresAsync()
    {
        if (!CanAccessFailures)
        {
            return;
        }

        CurrentSection = "Failures";
        await RefreshFailuresAsync();
    }

    [RelayCommand]
    private async Task OpenQualityAsync()
    {
        if (!CanAccessQuality)
        {
            return;
        }

        CurrentSection = "Quality";
        await RefreshOrdersAsync();
    }

    [RelayCommand]
    private async Task RefreshOrdersAsync()
    {
        _preservedOrderCode ??= SelectedOrder?.OrderCode ?? (OrderEditor.IsNew ? null : OrderEditor.OrderCode);
        var filter = OrderFilterKey == "all" ? null : OrderFilterKey;
        var items = await _databaseService.GetOrdersAsync(CurrentRole, _currentLogin, filter);
        Orders.Clear();
        foreach (var item in items)
        {
            Orders.Add(item);
        }

        if (!string.IsNullOrWhiteSpace(_preservedOrderCode))
        {
            SelectedOrder = Orders.FirstOrDefault(o => o.OrderCode == _preservedOrderCode);
        }
    }

    [RelayCommand]
    private async Task NewOrderAsync()
    {
        if (!CanCreateOrder)
        {
            return;
        }

        var customerLogin = IsCustomer ? _currentLogin : Customers.FirstOrDefault()?.Login ?? _currentLogin;
        var orderDate = DateTime.Today;
        var code = await _databaseService.GenerateOrderCodeAsync(customerLogin, orderDate);
        var number = await _databaseService.GetNextOrderNumberAsync(orderDate);

        OrderEditor = new OrderEditorState
        {
            IsNew = true,
            Number = number,
            OrderDate = orderDate,
            OrderCode = code,
            CustomerLogin = customerLogin,
            StatusName = IsManager ? "Составление спецификации" : "Новый",
            ManagerLogin = IsManager ? _currentLogin : null,
            ProductName = "Конвейер"
        };

        var map = await _databaseService.GetOrderStatusMapAsync();
        OrderEditor.StatusId = map[OrderEditor.StatusName];
        SelectedOrder = null;
        _preservedOrderCode = null;
        SelectedOrderCustomer = Customers.FirstOrDefault(c => c.Login == customerLogin);
        NewCustomerLogin = string.Empty;
        NewCustomerFullName = string.Empty;
        NewCustomerPassword = string.Empty;
        OrderAttachments.Clear();
        SyncOrderDimensionsFromEditor();
        NotifyOrderEditorUiProperties();
    }

    [RelayCommand]
    private async Task CreateOrderCustomerAsync()
    {
        if (!CanCreateCustomerForOrder)
        {
            return;
        }

        var login = NewCustomerLogin.Trim();
        var fullName = NewCustomerFullName.Trim();
        var password = NewCustomerPassword;

        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(password))
        {
            SetStatus("Заполните логин, ФИО и пароль нового заказчика.");
            return;
        }

        var (isValid, message) = _passwordPolicyService.Validate(password);
        if (!isValid)
        {
            SetStatus(message);
            return;
        }

        if (await _databaseService.LoginExistsAsync(login))
        {
            SetStatus("Такой логин уже существует.");
            return;
        }

        await _databaseService.RegisterCustomerAsync(login, password, fullName);
        await LoadCustomersAsync();
        SelectedOrderCustomer = Customers.FirstOrDefault(c => c.Login == login);
        NewCustomerLogin = string.Empty;
        NewCustomerFullName = string.Empty;
        NewCustomerPassword = string.Empty;
        SetStatus("Новый заказчик создан и выбран в заказе.");
    }

    public void AddPendingOrderAttachments(IReadOnlyList<(string fileName, byte[] data)> files)
    {
        if (!CanEditOrderAttachments)
        {
            return;
        }

        foreach (var (fileName, data) in files)
        {
            OrderAttachments.Add(new OrderAttachmentRow
            {
                AttachmentId = 0,
                FileName = fileName,
                PendingData = data
            });
        }

        OnPropertyChanged(nameof(HasOrderAttachments));
        OnPropertyChanged(nameof(ShowOrderAttachmentsEmpty));
        OnPropertyChanged(nameof(ShowOrderAttachmentsSection));
    }

    [RelayCommand]
    private async Task RemoveOrderAttachmentAsync(OrderAttachmentRow? row)
    {
        if (row is null || !CanEditOrderAttachments || !TryGetActiveOrder(out var number, out var orderDate))
        {
            return;
        }

        if (row.IsPending)
        {
            OrderAttachments.Remove(row);
        }
        else
        {
            await _databaseService.DeleteOrderAttachmentAsync(row.AttachmentId, number, orderDate);
            OrderAttachments.Remove(row);
        }

        OnPropertyChanged(nameof(HasOrderAttachments));
        OnPropertyChanged(nameof(ShowOrderAttachmentsEmpty));
        OnPropertyChanged(nameof(ShowOrderAttachmentsSection));
    }

    private void SyncOrderAttachmentsFromEditor()
    {
        OrderAttachments.Clear();
        foreach (var att in OrderEditor.Attachments)
        {
            OrderAttachments.Add(att);
        }

        OnPropertyChanged(nameof(HasOrderAttachments));
        OnPropertyChanged(nameof(ShowOrderAttachmentsEmpty));
        OnPropertyChanged(nameof(ShowOrderAttachmentsSection));
    }

    private IReadOnlyList<(string fileName, byte[] data)> CollectPendingAttachments() =>
        OrderAttachments
            .Where(a => a.IsPending && a.PendingData is not null)
            .Select(a => (a.FileName, a.PendingData!))
            .ToList();

    public async Task<(string fileName, byte[] data)?> GetOrderAttachmentForExportAsync(OrderAttachmentRow? row)
    {
        if (row is null || row.IsPending || !TryGetActiveOrder(out var number, out var orderDate))
        {
            return null;
        }

        var data = await _databaseService.GetOrderAttachmentDataAsync(row.AttachmentId, number, orderDate);
        return data is null ? null : (row.FileName, data);
    }

    [RelayCommand]
    private void AddOrderDimension()
    {
        if (!CanEditOrderDescription)
        {
            return;
        }

        OrderDimensions.Add(new OrderDimensionRow
        {
            Name = OrderDimensionRow.NameOptions[0],
            Unit = OrderDimensionRow.UnitOptions[0],
            Value = 0
        });
        SyncEditorDimensionsFromUi();
        NotifyOrderDimensionsUi();
    }

    [RelayCommand]
    private void RemoveLastOrderDimension()
    {
        if (!CanEditOrderDescription || OrderDimensions.Count == 0)
        {
            return;
        }

        OrderDimensions.RemoveAt(OrderDimensions.Count - 1);
        SyncEditorDimensionsFromUi();
        NotifyOrderDimensionsUi();
    }

    private void SyncOrderDimensionsFromEditor()
    {
        OrderDimensions.Clear();
        foreach (var dim in OrderEditor.Dimensions)
        {
            OrderDimensions.Add(new OrderDimensionRow
            {
                DimensionId = dim.DimensionId,
                Name = dim.Name,
                Unit = dim.Unit,
                Value = dim.Value
            });
        }

        NotifyOrderDimensionsUi();
    }

    private void NotifyOrderDimensionsUi()
    {
        OnPropertyChanged(nameof(HasOrderDimensions));
        OnPropertyChanged(nameof(ShowOrderDimensionsEmpty));
    }

    private void SyncEditorDimensionsFromUi()
    {
        OrderEditor.Dimensions.Clear();
        OrderEditor.Dimensions.AddRange(OrderDimensions);
    }

    private void NotifyOrderEditorUiProperties()
    {
        OnPropertyChanged(nameof(HideOrderCostAndDateFields));
        OnPropertyChanged(nameof(ShowOrderCostAndDateFields));
        OnPropertyChanged(nameof(ShowOrderCostAndDateReadOnly));
        OnPropertyChanged(nameof(CanEditOrderCostAndDate));
        OnPropertyChanged(nameof(CanEditOrderDescription));
        OnPropertyChanged(nameof(ShowOrderFieldsEditable));
        OnPropertyChanged(nameof(ShowOrderFieldsReadOnly));
        OnPropertyChanged(nameof(OrderNameDisplay));
        OnPropertyChanged(nameof(OrderDescriptionDisplay));
        NotifyOrderDimensionsUi();
        OnPropertyChanged(nameof(CanSaveOrder));
        OnPropertyChanged(nameof(CanSaveOrderDetails));
        OnPropertyChanged(nameof(CanEditCustomerLogin));
        OnPropertyChanged(nameof(ShowOrderCustomerReadOnly));
        OnPropertyChanged(nameof(OrderCustomerDisplay));
        OnPropertyChanged(nameof(CanCreateCustomerForOrder));
        OnPropertyChanged(nameof(CanChangeOrderStatus));
        OnPropertyChanged(nameof(HasAvailableStatusTransitions));
        OnPropertyChanged(nameof(ShowOrderStatusHelp));
        OnPropertyChanged(nameof(OrderStatusHelpText));
        OnPropertyChanged(nameof(ShowStatusCommentField));
        OnPropertyChanged(nameof(OrderCostText));
        OnPropertyChanged(nameof(OrderPlannedDate));
        OnPropertyChanged(nameof(OrderPlannedDateCalendar));
        OnPropertyChanged(nameof(OrderCostAndDateDisplay));
        OnPropertyChanged(nameof(CanEditOrderAttachments));
        OnPropertyChanged(nameof(ShowOrderAttachmentsSection));
        OnPropertyChanged(nameof(HasOrderAttachments));
        OnPropertyChanged(nameof(ShowOrderAttachmentsEmpty));
        OnPropertyChanged(nameof(CanCustomerCancelOrder));
        OnPropertyChanged(nameof(CanRemoveWorkshopMarker));
        RefreshAvailableTargetStatuses();
    }

    private string GetStatusTransitionDisplayName(string from, string to) =>
        IsCustomer && to == "Отклонен"
            ? "Отменить заказ"
            : OrderWorkflowService.GetTransitionDisplayName(from, to);

    private void RefreshAvailableTargetStatuses()
    {
        AvailableStatusTransitions.Clear();
        OrderStatusHelpText = string.Empty;

        if (!CanChangeOrderStatus || OrderEditor.IsNew || OrderEditor.Number <= 0)
        {
            SelectedStatusTransition = null;
            OnPropertyChanged(nameof(OrderStatusHelpText));
            OnPropertyChanged(nameof(ShowOrderStatusHelp));
            OnPropertyChanged(nameof(HasAvailableStatusTransitions));
            return;
        }

        foreach (var status in OrderWorkflowService.GetAllowedTargetStatuses(CurrentRole, OrderEditor.StatusName))
        {
            AvailableStatusTransitions.Add(new FilterOption
            {
                Key = status,
                DisplayName = GetStatusTransitionDisplayName(OrderEditor.StatusName, status)
            });
        }

        if (AvailableStatusTransitions.Count == 0)
        {
            OrderStatusHelpText =
                OrderWorkflowService.GetStatusHelpForRole(CurrentRole, OrderEditor.StatusName);
        }

        SelectedStatusTransition = AvailableStatusTransitions.FirstOrDefault();
        OnPropertyChanged(nameof(OrderStatusHelpText));
        OnPropertyChanged(nameof(ShowOrderStatusHelp));
        OnPropertyChanged(nameof(HasAvailableStatusTransitions));
        OnPropertyChanged(nameof(ShowStatusCommentField));
    }

    partial void OnSelectedStatusTransitionChanged(FilterOption? value) =>
        OnPropertyChanged(nameof(ShowStatusCommentField));

    private bool TryGetActiveOrder(out int number, out DateTime orderDate)
    {
        if (SelectedOrder is not null)
        {
            number = SelectedOrder.Number;
            orderDate = SelectedOrder.OrderDate;
            return true;
        }

        if (!OrderEditor.IsNew && OrderEditor.Number > 0)
        {
            number = OrderEditor.Number;
            orderDate = OrderEditor.OrderDate;
            return true;
        }

        number = 0;
        orderDate = default;
        return false;
    }

    [RelayCommand]
    private async Task SaveOrderAsync()
    {
        if (!CanSaveOrder)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(OrderEditor.OrderName))
        {
            SetStatus("Укажите наименование заказа.");
            return;
        }

        if (IsManager && OrderEditor.IsNew && string.IsNullOrWhiteSpace(OrderEditor.CustomerLogin))
        {
            SetStatus("Выберите заказчика.");
            return;
        }

        await PersistOrderAsync(isNewOrder: true);
    }

    [RelayCommand]
    private async Task SaveOrderDetailsAsync()
    {
        if (!CanSaveOrderDetails)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(OrderEditor.OrderName))
        {
            SetStatus("Укажите наименование заказа.");
            return;
        }

        if (IsConstructor &&
            (!OrderEditor.Cost.HasValue || !OrderEditor.PlannedCompletionDate.HasValue))
        {
            SetStatus("Укажите стоимость и плановую дату завершения.");
            return;
        }

        await PersistOrderAsync(isNewOrder: false);
    }

    private async Task PersistOrderAsync(bool isNewOrder)
    {
        try
        {
            SyncEditorDimensionsFromUi();
            var pending = CollectPendingAttachments();
            await _databaseService.SaveOrderAsync(OrderEditor, _currentLogin, pending);
            SetStatus(pending.Count > 0
                ? $"Заказ {OrderEditor.OrderCode} сохранён. Добавлено файлов: {pending.Count}."
                : $"Заказ {OrderEditor.OrderCode} сохранён.");
            if (isNewOrder)
            {
                OrderEditor.IsNew = false;
            }

            _preservedOrderCode = OrderEditor.OrderCode;
            await RefreshOrdersAsync();
            if (TryGetActiveOrder(out var number, out var orderDate))
            {
                await LoadOrderEditorAsync(number, orderDate);
            }
        }
        catch (Exception ex)
        {
            SetStatus(BuildOrderSaveErrorMessage(ex));
        }
    }

    private static string BuildOrderSaveErrorMessage(Exception ex)
    {
        if (ex is PostgresException pg)
        {
            if (pg.SqlState == PostgresErrorCodes.UniqueViolation &&
                pg.ConstraintName == "customer_order_pkey")
            {
                return "Не удалось сохранить заказ: конфликт номера заказа. Нажмите «Новый заказ» и повторите сохранение.";
            }

            return $"Не удалось сохранить заказ: {pg.MessageText}";
        }

        return $"Не удалось сохранить заказ: {ex.Message}";
    }

    [RelayCommand]
    private async Task DeleteOrderAsync()
    {
        if (SelectedOrder is null || OrderEditor.StatusName != "Новый")
        {
            SetStatus("Удалить можно только новый заказ.");
            return;
        }

        if (!IsCustomer && !IsManager)
        {
            return;
        }

        await _databaseService.DeleteOrderAsync(SelectedOrder.Number, SelectedOrder.OrderDate);
        SetStatus("Заказ удалён.");
        await RefreshOrdersAsync();
    }

    [RelayCommand]
    private async Task ApplyOrderStatusAsync()
    {
        if (SelectedStatusTransition is null)
        {
            SetStatus(string.IsNullOrWhiteSpace(OrderStatusHelpText)
                ? "Выберите новый статус в списке."
                : OrderStatusHelpText);
            return;
        }

        if (SelectedStatusTransition.Key == "Отклонен" &&
            !IsCustomer &&
            string.IsNullOrWhiteSpace(OrderActionComment))
        {
            SetStatus("Укажите причину отклонения в комментарии.");
            return;
        }

        await ChangeOrderStatusAsync(SelectedStatusTransition.Key);
    }

    private async Task ChangeOrderStatusAsync(string? newStatus)
    {
        if (string.IsNullOrWhiteSpace(newStatus) || !TryGetActiveOrder(out var number, out var orderDate))
        {
            SetStatus("Выберите заказ в списке.");
            return;
        }

        if (!OrderWorkflowService.CanRoleChangeStatus(CurrentRole, OrderEditor.StatusName, newStatus))
        {
            SetStatus($"Переход «{OrderEditor.StatusName}» → «{newStatus}» недоступен для роли «{CurrentRole}».");
            return;
        }

        if (IsConstructor && newStatus == "Подтверждение")
        {
            if (!OrderEditor.Cost.HasValue || !OrderEditor.PlannedCompletionDate.HasValue)
            {
                SetStatus("Сначала укажите стоимость и плановую дату, затем нажмите «Сохранить данные».");
                return;
            }

            SyncEditorDimensionsFromUi();
            await _databaseService.SaveOrderAsync(OrderEditor, _currentLogin, CollectPendingAttachments());
        }

        if (IsMaster && newStatus == "Готов")
        {
            if (!await _databaseService.AllQualityChecksPositiveAsync(number, orderDate))
            {
                SetStatus("Все параметры качества должны иметь оценку «+» (раздел «Качество»).");
                return;
            }
        }

        try
        {
            var writeOffMsg = await _databaseService.ChangeOrderStatusAsync(
                number,
                orderDate,
                newStatus,
                _currentLogin,
                string.IsNullOrWhiteSpace(OrderActionComment) ? null : OrderActionComment);
            var statusMsg = $"Статус изменён на «{newStatus}».";
            if (!string.IsNullOrWhiteSpace(writeOffMsg))
            {
                statusMsg += " " + writeOffMsg;
            }

            SetStatus(statusMsg);
            _preservedOrderCode = OrderEditor.OrderCode;
            await RefreshOrdersAsync();
            await LoadOrderEditorAsync(number, orderDate);
            if (CanViewOrderHistory)
            {
                await LoadOrderHistoryAsync(number, orderDate);
            }
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
        }
    }

    [RelayCommand]
    private async Task LoadOrderHistoryAsync()
    {
        if (SelectedOrder is null || !CanViewOrderHistory)
        {
            return;
        }

        await LoadOrderHistoryAsync(SelectedOrder.Number, SelectedOrder.OrderDate);
    }

    private async Task LoadOrderHistoryAsync(int number, DateTime orderDate)
    {
        var items = await _databaseService.GetOrderHistoryAsync(number, orderDate);
        OrderHistory.Clear();
        foreach (var item in items)
        {
            OrderHistory.Add(item);
        }
    }

    private async Task LoadOrderEditorAsync(int number, DateTime orderDate)
    {
        var editor = await _databaseService.GetOrderEditorAsync(number, orderDate);
        if (editor is not null)
        {
            OrderEditor = editor;
            _preservedOrderCode = editor.OrderCode;
            SelectedOrderCustomer = Customers.FirstOrDefault(c => c.Login == editor.CustomerLogin);
            SyncOrderDimensionsFromEditor();
            SyncOrderAttachmentsFromEditor();
            NotifyOrderEditorUiProperties();
            if (CanViewOrderHistory)
            {
                await LoadOrderHistoryAsync(number, orderDate);
            }
        }
    }

    private async Task LoadOrderEditorSafeAsync(int number, DateTime orderDate)
    {
        try
        {
            await LoadOrderEditorAsync(number, orderDate);
        }
        catch (Exception ex)
        {
            SetStatus($"Не удалось загрузить карточку заказа: {ex.Message}");
        }
    }

    private async Task LoadCustomersAsync()
    {
        var items = await _databaseService.GetCustomersAsync();
        Customers.Clear();
        foreach (var item in items)
        {
            Customers.Add(item);
        }
    }

    private async Task LoadWorkshopsAsync()
    {
        var items = await _databaseService.GetWorkshopsAsync();
        Workshops.Clear();
        foreach (var item in items)
        {
            Workshops.Add(item);
        }

        SelectedWorkshop = Workshops.FirstOrDefault();
    }

    private async Task LoadWorkshopMarkersAsync(long workshopId)
    {
        var items = await _databaseService.GetWorkshopMarkersAsync(workshopId);
        WorkshopMarkers.Clear();
        DraftWorkshopMarkers.Clear();
        SelectedDraftWorkshopMarker = null;
        foreach (var item in items)
        {
            WorkshopMarkers.Add(item);
            DraftWorkshopMarkers.Add(new WorkshopMarkerModel
            {
                MarkerId = item.MarkerId,
                IconType = item.IconType,
                X = item.X,
                Y = item.Y
            });
        }
    }

    [RelayCommand]
    private void AddWorkshopMarker()
    {
        var marker = new WorkshopMarkerModel
        {
            IconType = SelectedPlanIconType,
            X = 0.5,
            Y = 0.5
        };
        DraftWorkshopMarkers.Add(marker);
        SelectedDraftWorkshopMarker = marker;
    }

    public void AddWorkshopMarkerAt(string iconType, double x, double y)
    {
        var marker = new WorkshopMarkerModel
        {
            IconType = string.IsNullOrWhiteSpace(iconType) ? SelectedPlanIconType : iconType,
            X = Math.Clamp(x, 0, 1),
            Y = Math.Clamp(y, 0, 1)
        };
        DraftWorkshopMarkers.Add(marker);
        SelectedDraftWorkshopMarker = marker;
    }

    public void MoveWorkshopMarker(WorkshopMarkerModel marker, double x, double y)
    {
        marker.X = Math.Clamp(x, 0, 1);
        marker.Y = Math.Clamp(y, 0, 1);
    }

    public void SelectWorkshopMarker(WorkshopMarkerModel? marker)
    {
        SelectedDraftWorkshopMarker = marker;
    }

    [RelayCommand]
    private void RemoveSelectedWorkshopMarker()
    {
        var marker = SelectedDraftWorkshopMarker ?? DraftWorkshopMarkers.LastOrDefault();
        if (marker is not null)
        {
            DraftWorkshopMarkers.Remove(marker);
            if (ReferenceEquals(SelectedDraftWorkshopMarker, marker))
            {
                SelectedDraftWorkshopMarker = null;
            }
        }
    }

    public bool CanRemoveWorkshopMarker => SelectedDraftWorkshopMarker is not null || DraftWorkshopMarkers.Count > 0;

    [RelayCommand]
    private void ResetWorkshopDraft()
    {
        if (SelectedWorkshop is null)
        {
            return;
        }

        SelectedDraftWorkshopMarker = null;
        _ = LoadWorkshopMarkersAsync(SelectedWorkshop.WorkshopId);
    }

    [RelayCommand]
    private async Task SaveWorkshopPlanAsync()
    {
        if (SelectedWorkshop is null)
        {
            return;
        }

        await _databaseService.SaveWorkshopMarkersAsync(SelectedWorkshop.WorkshopId, DraftWorkshopMarkers.ToList());
        SetStatus("План цеха сохранён.");
        await LoadWorkshopMarkersAsync(SelectedWorkshop.WorkshopId);
    }

    private static bool TryComposeDateTime(DateTimeOffset? datePart, string timePart, out DateTime result)
    {
        result = default;
        if (datePart is null || string.IsNullOrWhiteSpace(timePart))
        {
            return false;
        }

        if (!TimeSpan.TryParse(timePart, out var time))
        {
            return false;
        }

        result = datePart.Value.Date + time;
        return true;
    }

    [RelayCommand]
    private async Task RefreshFailuresAsync()
    {
        var items = await _databaseService.GetOpenEquipmentFailuresAsync();
        EquipmentFailures.Clear();
        foreach (var item in items)
        {
            EquipmentFailures.Add(item);
        }

        var markings = await _databaseService.GetEquipmentMarkingsAsync();
        EquipmentMarkings.Clear();
        foreach (var m in markings)
        {
            EquipmentMarkings.Add(m);
        }

        var reasons = await _databaseService.GetFailureReasonsAsync();
        FailureReasons.Clear();
        foreach (var r in reasons)
        {
            FailureReasons.Add(r);
        }

        FailureEquipment = EquipmentMarkings.FirstOrDefault() ?? string.Empty;
        FailureReason = FailureReasons.FirstOrDefault() ?? string.Empty;
        var now = DateTime.Now;
        FailureStartDate = new DateTimeOffset(now.Date);
        FailureStartTime = now.ToString("HH:mm");
        FailureEndDate = new DateTimeOffset(now.Date);
        FailureEndTime = now.ToString("HH:mm");
    }

    [RelayCommand]
    private async Task RegisterFailureAsync()
    {
        if (string.IsNullOrWhiteSpace(FailureEquipment) || string.IsNullOrWhiteSpace(FailureReason))
        {
            SetStatus("Заполните поля сбоя.");
            return;
        }

        if (!TryComposeDateTime(FailureStartDate, FailureStartTime, out var startedAt))
        {
            SetStatus("Укажите дату и время начала в формате ЧЧ:ММ.");
            return;
        }

        await _databaseService.RegisterEquipmentFailureAsync(
            FailureEquipment,
            FailureReason,
            startedAt,
            _currentLogin);
        SetStatus("Сбой зарегистрирован.");
        await RefreshFailuresAsync();
    }

    [RelayCommand]
    private async Task EndFailureAsync()
    {
        if (SelectedFailure is null || SelectedFailure.EndedAt is not null)
        {
            SetStatus("Выберите открытый сбой.");
            return;
        }

        if (!TryComposeDateTime(FailureEndDate, FailureEndTime, out var endedAt))
        {
            SetStatus("Укажите дату и время завершения в формате ЧЧ:ММ.");
            return;
        }

        if (endedAt < SelectedFailure.StartedAt)
        {
            SetStatus("Время завершения не может быть раньше начала.");
            return;
        }

        await _databaseService.EndEquipmentFailureAsync(SelectedFailure.FailureId, endedAt);
        SetStatus("Сбой закрыт.");
        await RefreshFailuresAsync();
    }

    [RelayCommand]
    private void SetQualityPlus(QualityCheckRow row) => row.IsPositive = true;

    [RelayCommand]
    private void SetQualityMinus(QualityCheckRow row) => row.IsPositive = false;

    [RelayCommand]
    private async Task LoadQualityForOrderAsync()
    {
        if (QualityOrder is null)
        {
            return;
        }

        var items = await _databaseService.GetQualityChecksForOrderAsync(QualityOrder.Number, QualityOrder.OrderDate);
        QualityChecks.Clear();
        foreach (var item in items)
        {
            QualityChecks.Add(item);
        }
    }

    [RelayCommand]
    private async Task SaveQualityChecksAsync()
    {
        if (QualityOrder is null)
        {
            return;
        }

        await _databaseService.SaveQualityChecksAsync(
            QualityOrder.Number,
            QualityOrder.OrderDate,
            QualityChecks.ToList(),
            _currentLogin);
        SetStatus("Контроль качества сохранён.");
    }

    [RelayCommand]
    private async Task CompleteProductionAsync()
    {
        if (QualityOrder is null)
        {
            return;
        }

        await SaveQualityChecksAsync();
        if (!await _databaseService.AllQualityChecksPositiveAsync(QualityOrder.Number, QualityOrder.OrderDate))
        {
            SetStatus("Все параметры должны иметь оценку «+».");
            return;
        }

        try
        {
            await _databaseService.ChangeOrderStatusAsync(
                QualityOrder.Number,
                QualityOrder.OrderDate,
                "Контроль",
                _currentLogin,
                null);
            SetStatus("Заказ переведён в «Контроль».");
            await RefreshOrdersAsync();
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
        }
    }

    [RelayCommand]
    private async Task MarkOrderReadyAsync()
    {
        if (QualityOrder is null)
        {
            return;
        }

        try
        {
            await _databaseService.ChangeOrderStatusAsync(
                QualityOrder.Number,
                QualityOrder.OrderDate,
                "Готов",
                _currentLogin,
                null);
            SetStatus("Заказ готов.");
            await RefreshOrdersAsync();
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
        }
    }
}
