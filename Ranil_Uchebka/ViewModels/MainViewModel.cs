using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ranil_Uchebka.Models;
using Ranil_Uchebka.Services;

namespace Ranil_Uchebka.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private string _currentLogin = string.Empty;
    private readonly DatabaseService _databaseService;
    private readonly RememberMeService _rememberMeService;
    private readonly PasswordPolicyService _passwordPolicyService;
    
    [ObservableProperty]
    private string _logoPath = string.Empty;

    [ObservableProperty]
    private string _screenTitle = "Управление производством конвейеров";

    [ObservableProperty]
    private bool _isAuthenticated;

    [ObservableProperty]
    private string _currentRole = string.Empty;

    [ObservableProperty]
    private string _currentUserDisplay = string.Empty;

    [ObservableProperty]
    private string _currentSection = "RoleHome";

    [ObservableProperty]
    private string _login = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _rememberMe = true;

    [ObservableProperty]
    private string _authError = string.Empty;

    [ObservableProperty]
    private string _registerLogin = string.Empty;

    [ObservableProperty]
    private string _registerPassword = string.Empty;

    [ObservableProperty]
    private string _registerFullName = string.Empty;

    [ObservableProperty]
    private string _registerError = string.Empty;

    [ObservableProperty]
    private string _registerSuccess = string.Empty;

    /// <summary>Login or Register — shown before authentication.</summary>
    [ObservableProperty]
    private string _authPage = "Login";

    [ObservableProperty]
    private WarehouseOption? _selectedWarehouse;

    [ObservableProperty]
    private MaterialItem? _selectedMaterial;

    [ObservableProperty]
    private ComponentItem? _selectedComponent;

    [ObservableProperty]
    private WorkerItem? _selectedWorker;

    [ObservableProperty]
    private long _workerId;

    [ObservableProperty]
    private string _workerFullName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WorkerBirthDateText))]
    [NotifyPropertyChangedFor(nameof(WorkerBirthDateCalendar))]
    private DateTimeOffset? _workerBirthDate = DateTimeOffset.Now.AddYears(-20);

    [ObservableProperty]
    private string _workerHomeAddress = string.Empty;

    [ObservableProperty]
    private string _workerEducation = string.Empty;

    [ObservableProperty]
    private string _workerQualification = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private int _materialsDisplayedCount;

    [ObservableProperty]
    private int _materialsTotalCount;

    [ObservableProperty]
    private decimal _materialsTotalCost;

    [ObservableProperty]
    private int _componentsDisplayedCount;

    [ObservableProperty]
    private int _componentsTotalCount;

    [ObservableProperty]
    private decimal _componentsTotalCost;

    public ObservableCollection<WarehouseOption> Warehouses { get; } = [];
    public ObservableCollection<MaterialItem> Materials { get; } = [];
    public ObservableCollection<ComponentItem> Components { get; } = [];
    public ObservableCollection<WorkerItem> Workers { get; } = [];
    public ObservableCollection<OperationOption> Operations { get; } = [];

    public bool IsLoginPage => !IsAuthenticated && AuthPage == "Login";
    public bool IsRegisterPage => !IsAuthenticated && AuthPage == "Register";
    public bool IsManager => CurrentRole == "Менеджер";
    public bool IsDirector => CurrentRole == "Директор";
    public bool IsConstructor => CurrentRole == "Конструктор";
    public bool IsMaster => CurrentRole == "Мастер";
    public bool IsCustomer => CurrentRole == "Заказчик";
    public bool CanAccessReferences => IsAuthenticated && !IsCustomer;
    public bool CanEditReferences => IsManager || IsDirector;
    public bool CanAccessWorkers => IsDirector;
    public bool CanAccessSpecifications => IsMaster;
    public bool CanAccessPlanning => IsManager;
    public bool CanAccessStockReport => IsManager || IsDirector;
    public bool IsRoleHome => CurrentSection == "RoleHome";
    public bool IsMaterialsSection => CurrentSection == "Materials";
    public bool IsComponentsSection => CurrentSection == "Components";
    public bool IsWorkersSection => CurrentSection == "Workers";
    public bool IsSpecificationsSection => CurrentSection == "Specifications";
    public bool IsPlanningSection => CurrentSection == "Planning";
    public bool IsStockReportSection => CurrentSection == "StockReport";

    public MainViewModel(
        DatabaseService databaseService,
        RememberMeService rememberMeService,
        PasswordPolicyService passwordPolicyService)
    {
        _databaseService = databaseService;
        _rememberMeService = rememberMeService;
        _passwordPolicyService = passwordPolicyService;
    }

    public async Task InitializeAsync(string logoPath)
    {
        LogoPath = logoPath;
        await LoadWarehousesAsync();
        await LoadOperationsAsync();

        var creds = await _rememberMeService.ReadAsync();
        if (creds is not null && !string.IsNullOrWhiteSpace(creds.Login) && !string.IsNullOrWhiteSpace(creds.Password))
        {
            Login = creds.Login;
            Password = creds.Password;
            RememberMe = true;
            await LoginAsync();
        }
    }

    partial void OnIsAuthenticatedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsLoginPage));
        OnPropertyChanged(nameof(IsRegisterPage));
        OnPropertyChanged(nameof(CanAccessReferences));
        OnPropertyChanged(nameof(CanEditReferences));
        OnPropertyChanged(nameof(CanAccessWorkers));
        OnPropertyChanged(nameof(CanAccessOrders));
        OnPropertyChanged(nameof(CanAccessWorkshop));
        OnPropertyChanged(nameof(CanAccessFailures));
        OnPropertyChanged(nameof(CanAccessQuality));
        OnPropertyChanged(nameof(CanAccessSpecifications));
        OnPropertyChanged(nameof(CanAccessPlanning));
        OnPropertyChanged(nameof(CanAccessStockReport));
        if (!value)
        {
            AuthPage = "Login";
        }
    }

    partial void OnAuthPageChanged(string value)
    {
        OnPropertyChanged(nameof(IsLoginPage));
        OnPropertyChanged(nameof(IsRegisterPage));
    }

    partial void OnCurrentRoleChanged(string value)
    {
        OnPropertyChanged(nameof(IsManager));
        OnPropertyChanged(nameof(IsDirector));
        OnPropertyChanged(nameof(IsConstructor));
        OnPropertyChanged(nameof(IsMaster));
        OnPropertyChanged(nameof(IsCustomer));
        OnPropertyChanged(nameof(CanAccessReferences));
        OnPropertyChanged(nameof(CanEditReferences));
        OnPropertyChanged(nameof(CanAccessWorkers));
        OnPropertyChanged(nameof(CanAccessOrders));
        OnPropertyChanged(nameof(CanAccessWorkshop));
        OnPropertyChanged(nameof(CanAccessFailures));
        OnPropertyChanged(nameof(CanAccessQuality));
        OnPropertyChanged(nameof(CanAccessSpecifications));
        OnPropertyChanged(nameof(CanAccessPlanning));
        OnPropertyChanged(nameof(CanAccessStockReport));
        OnPropertyChanged(nameof(CanViewOrderHistory));
        OnPropertyChanged(nameof(CanCreateOrder));
        OnPropertyChanged(nameof(CanCreateCustomerForOrder));
        OnPropertyChanged(nameof(CanChangeOrderStatus));
        OnPropertyChanged(nameof(CanEditCustomerLogin));
        OnPropertyChanged(nameof(ShowOrderCustomerReadOnly));
        OnPropertyChanged(nameof(HasAvailableStatusTransitions));
        OnPropertyChanged(nameof(ShowOrderStatusHelp));
        OnPropertyChanged(nameof(OrderStatusHelpText));
        OnPropertyChanged(nameof(ShowStatusCommentField));
        OnPropertyChanged(nameof(HideOrderCostAndDateFields));
        OnPropertyChanged(nameof(RoleScreenName));
    }

    partial void OnCurrentSectionChanged(string value)
    {
        OnPropertyChanged(nameof(IsRoleHome));
        OnPropertyChanged(nameof(IsMaterialsSection));
        OnPropertyChanged(nameof(IsComponentsSection));
        OnPropertyChanged(nameof(IsWorkersSection));
        OnPropertyChanged(nameof(IsOrdersSection));
        OnPropertyChanged(nameof(IsWorkshopSection));
        OnPropertyChanged(nameof(IsFailuresSection));
        OnPropertyChanged(nameof(IsQualitySection));
        OnPropertyChanged(nameof(IsSpecificationsSection));
        OnPropertyChanged(nameof(IsPlanningSection));
        OnPropertyChanged(nameof(IsStockReportSection));
    }

    partial void OnSelectedWarehouseChanged(WarehouseOption? value)
    {
        _ = RefreshReferencesAsync();
    }

    partial void OnSelectedWorkerChanged(WorkerItem? value)
    {
        if (value is null)
        {
            return;
        }

        WorkerId = value.WorkerId;
        WorkerFullName = value.FullName;
        WorkerBirthDate = new DateTimeOffset(value.BirthDate);
        WorkerHomeAddress = value.HomeAddress;
        WorkerEducation = value.Education;
        WorkerQualification = value.Qualification;
        _ = LoadWorkerOperationSelectionAsync(value.WorkerId);
    }

    private void SetStatus(string message) => StatusMessage = $"{DateTime.Now:T} - {message}";

    [RelayCommand]
    private async Task LoginAsync()
    {
        AuthError = string.Empty;
        RegisterSuccess = string.Empty;

        if (string.IsNullOrWhiteSpace(Login) || string.IsNullOrWhiteSpace(Password))
        {
            AuthError = "Введите логин и пароль.";
            return;
        }

        var session = await _databaseService.ValidateUserAsync(Login.Trim(), Password);
        if (session is null)
        {
            AuthError = "Неверный логин или пароль.";
            return;
        }

        if (RememberMe)
        {
            await _rememberMeService.SaveAsync(Login.Trim(), Password);
        }
        else
        {
            await _rememberMeService.ClearAsync();
        }

        IsAuthenticated = true;
        _currentLogin = session.Login;
        CurrentRole = session.RoleName;
        CurrentUserDisplay = string.IsNullOrWhiteSpace(session.FullName) ? session.Login : session.FullName!;
        CurrentSection = "RoleHome";
        ClearOrderEditorState();
        SetStatus($"Вход выполнен: {CurrentUserDisplay} ({CurrentRole}).");
        await RefreshReferencesAsync();
        await RefreshWorkersAsync();
    }

    [RelayCommand]
    private void OpenRegisterPage()
    {
        AuthError = string.Empty;
        RegisterError = string.Empty;
        RegisterSuccess = string.Empty;
        StatusMessage = string.Empty;
        AuthPage = "Register";
    }

    [RelayCommand]
    private void OpenLoginPage()
    {
        RegisterError = string.Empty;
        StatusMessage = string.Empty;
        AuthPage = "Login";
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _rememberMeService.ClearAsync();
        IsAuthenticated = false;
        CurrentRole = string.Empty;
        CurrentUserDisplay = string.Empty;
        CurrentSection = "RoleHome";
        Password = string.Empty;
        AuthError = string.Empty;
        AuthPage = "Login";
        ClearOrderEditorState();
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private async Task RegisterCustomerAsync()
    {
        RegisterError = string.Empty;
        RegisterSuccess = string.Empty;
        var login = RegisterLogin.Trim();
        var password = RegisterPassword;
        var fullName = RegisterFullName.Trim();

        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(fullName))
        {
            RegisterError = "Заполните все поля регистрации.";
            return;
        }

        var (isValid, message) = _passwordPolicyService.Validate(password);
        if (!isValid)
        {
            RegisterError = message;
            return;
        }

        if (await _databaseService.LoginExistsAsync(login))
        {
            RegisterError = "Такой логин уже существует.";
            return;
        }

        try
        {
            await _databaseService.RegisterCustomerAsync(login, password, fullName);
            RegisterSuccess = "Регистрация успешна. Войдите с новым логином и паролем.";
            RegisterLogin = string.Empty;
            RegisterPassword = string.Empty;
            RegisterFullName = string.Empty;
            AuthPage = "Login";
        }
        catch (Exception ex)
        {
            RegisterError = $"Ошибка регистрации: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task OpenMaterialsAsync()
    {
        if (!CanAccessReferences)
        {
            return;
        }
        CurrentSection = "Materials";
        await RefreshMaterialsAsync();
    }

    [RelayCommand]
    private async Task OpenComponentsAsync()
    {
        if (!CanAccessReferences)
        {
            return;
        }
        CurrentSection = "Components";
        await RefreshComponentsAsync();
    }

    [RelayCommand]
    private async Task OpenWorkersAsync()
    {
        if (!CanAccessWorkers)
        {
            return;
        }
        CurrentSection = "Workers";
        await RefreshWorkersAsync();
    }

    [RelayCommand]
    private void OpenRoleHome()
    {
        CurrentSection = "RoleHome";
    }

    [RelayCommand]
    private async Task RefreshReferencesAsync()
    {
        if (!CanAccessReferences)
        {
            return;
        }

        await RefreshMaterialsAsync();
        await RefreshComponentsAsync();
    }

    [RelayCommand]
    public async Task RefreshMaterialsAsync()
    {
        var warehouseId = SelectedWarehouse?.WarehouseId;
        var items = await _databaseService.GetMaterialsAsync(warehouseId);
        Materials.Clear();
        foreach (var item in items)
        {
            Materials.Add(item);
        }

        MaterialsDisplayedCount = Materials.Count;
        MaterialsTotalCount = await _databaseService.GetMaterialTotalCountAsync();
        MaterialsTotalCost = Materials.Sum(m => m.Quantity * m.PurchasePrice);
    }

    [RelayCommand]
    public async Task RefreshComponentsAsync()
    {
        var warehouseId = SelectedWarehouse?.WarehouseId;
        var items = await _databaseService.GetComponentsAsync(warehouseId);
        Components.Clear();
        foreach (var item in items)
        {
            Components.Add(item);
        }

        ComponentsDisplayedCount = Components.Count;
        ComponentsTotalCount = await _databaseService.GetComponentTotalCountAsync();
        ComponentsTotalCost = Components.Sum(c => c.Quantity * c.PurchasePrice);
    }

    [RelayCommand]
    private async Task SaveSelectedMaterialAsync()
    {
        if (!CanEditReferences || SelectedMaterial is null)
        {
            return;
        }

        await _databaseService.UpdateMaterialAsync(SelectedMaterial);
        SetStatus($"Материал {SelectedMaterial.Article} обновлён.");
        await RefreshMaterialsAsync();
    }

    [RelayCommand]
    private async Task SaveSelectedComponentAsync()
    {
        if (!CanEditReferences || SelectedComponent is null)
        {
            return;
        }

        await _databaseService.UpdateComponentAsync(SelectedComponent);
        SetStatus($"Комплектующее {SelectedComponent.Article} обновлено.");
        await RefreshComponentsAsync();
    }

    public async Task<(bool ok, string message)> DeleteSelectedMaterialAsync()
    {
        if (!CanEditReferences || SelectedMaterial is null)
        {
            return (false, "Сначала выберите материал.");
        }

        var canDelete = await _databaseService.CanDeleteMaterialAsync(SelectedMaterial.Article);
        if (!canDelete)
        {
            return (false, "Удаление возможно только при нулевом количестве.");
        }

        await _databaseService.DeleteMaterialAsync(SelectedMaterial.Article);
        SetStatus($"Материал {SelectedMaterial.Article} удалён.");
        await RefreshMaterialsAsync();
        return (true, "Удалено.");
    }

    public async Task<(bool ok, string message)> DeleteSelectedComponentAsync()
    {
        if (!CanEditReferences || SelectedComponent is null)
        {
            return (false, "Сначала выберите комплектующее.");
        }

        var canDelete = await _databaseService.CanDeleteComponentAsync(SelectedComponent.Article);
        if (!canDelete)
        {
            return (false, "Удаление возможно только при нулевом количестве.");
        }

        await _databaseService.DeleteComponentAsync(SelectedComponent.Article);
        SetStatus($"Комплектующее {SelectedComponent.Article} удалено.");
        await RefreshComponentsAsync();
        return (true, "Удалено.");
    }

    public int WorkersCount => Workers.Count;

    [RelayCommand]
    private async Task RefreshWorkersAsync()
    {
        if (!CanAccessWorkers)
        {
            return;
        }

        try
        {
            var items = await _databaseService.GetWorkersAsync();
            Workers.Clear();
            foreach (var item in items)
            {
                Workers.Add(item);
            }

            OnPropertyChanged(nameof(WorkersCount));
        }
        catch (Exception ex)
        {
            SetStatus($"Ошибка загрузки работников: {ex.Message}");
        }
    }

    [RelayCommand]
    private void NewWorker()
    {
        WorkerId = 0;
        WorkerFullName = string.Empty;
        WorkerBirthDate = DateTimeOffset.Now.AddYears(-20);
        WorkerHomeAddress = string.Empty;
        WorkerEducation = string.Empty;
        WorkerQualification = string.Empty;
        foreach (var operation in Operations)
        {
            operation.IsSelected = false;
        }
        SelectedWorker = null;
    }

    [RelayCommand]
    private async Task SaveWorkerAsync()
    {
        if (!CanAccessWorkers)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(WorkerFullName))
        {
            SetStatus("Укажите ФИО работника.");
            return;
        }

        if (WorkerBirthDate is null)
        {
            SetStatus("Укажите дату рождения.");
            return;
        }

        if (string.IsNullOrWhiteSpace(WorkerHomeAddress))
        {
            SetStatus("Укажите домашний адрес.");
            return;
        }

        if (string.IsNullOrWhiteSpace(WorkerEducation))
        {
            SetStatus("Укажите образование (например: Среднее профессиональное).");
            return;
        }

        if (string.IsNullOrWhiteSpace(WorkerQualification))
        {
            SetStatus("Укажите квалификацию (например: Слесарь 4 разряд).");
            return;
        }

        var selectedOperationIds = Operations
            .Where(o => o.IsSelected)
            .Select(o => o.OperationId)
            .ToArray();

        if (selectedOperationIds.Length == 0)
        {
            SetStatus("Отметьте хотя бы одну операцию (Заготовительная, Отрезная…).");
            return;
        }

        if (Operations.Count == 0)
        {
            SetStatus("В БД нет операций. Выполните скрипт 04_extend_schema_for_session1.sql.");
            return;
        }

        var worker = new WorkerItem
        {
            WorkerId = WorkerId,
            FullName = WorkerFullName.Trim(),
            BirthDate = WorkerBirthDate.Value.Date,
            HomeAddress = WorkerHomeAddress.Trim(),
            Education = WorkerEducation.Trim(),
            Qualification = WorkerQualification.Trim()
        };

        try
        {
            var savedId = await _databaseService.SaveWorkerAsync(worker, selectedOperationIds);
            WorkerId = savedId;
            await RefreshWorkersAsync();
            SelectedWorker = Workers.FirstOrDefault(w => w.WorkerId == savedId);
            SetStatus($"Работник сохранён. В списке: {Workers.Count}.");
        }
        catch (Exception ex)
        {
            SetStatus($"Не удалось сохранить: {ex.Message}");
        }
    }

    public async Task<(bool ok, string message)> DeleteWorkerAsync()
    {
        if (!CanAccessWorkers)
        {
            return (false, "Удалять работников может только директор.");
        }

        if (WorkerId == 0)
        {
            return (false, "Сначала выберите работника.");
        }

        await _databaseService.DeleteWorkerAsync(WorkerId);
        SetStatus("Работник удалён.");
        await RefreshWorkersAsync();
        NewWorker();
        return (true, "Удалено.");
    }

    private async Task LoadWarehousesAsync()
    {
        Warehouses.Clear();
        Warehouses.Add(new WarehouseOption { WarehouseId = 0, Name = "Все склады" });
        var items = await _databaseService.GetWarehousesAsync();
        foreach (var item in items)
        {
            Warehouses.Add(item);
        }
        SelectedWarehouse = Warehouses.FirstOrDefault();
    }

    private async Task LoadOperationsAsync()
    {
        var operations = await _databaseService.GetOperationsAsync();
        Operations.Clear();
        foreach (var operation in operations)
        {
            Operations.Add(operation);
        }
    }

    private async Task LoadWorkerOperationSelectionAsync(long workerId)
    {
        var selected = await _databaseService.GetWorkerOperationIdsAsync(workerId);
        foreach (var operation in Operations)
        {
            operation.IsSelected = selected.Contains(operation.OperationId);
        }
    }

    public string RoleScreenName =>
        CurrentRole switch
        {
            "Заказчик" => "Экран заказчика",
            "Менеджер" => "Экран менеджера",
            "Конструктор" => "Экран конструктора",
            "Мастер" => "Экран мастера",
            "Директор" => "Экран директора",
            _ => "Авторизация"
        };

    public string WorkerBirthDateText
    {
        get => WorkerBirthDate?.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
        set
        {
            if (DateTimeOffset.TryParse(value, out var dt))
            {
                WorkerBirthDate = dt;
            }
        }
    }

    public DateTime? WorkerBirthDateCalendar
    {
        get => WorkerBirthDate?.DateTime.Date;
        set => WorkerBirthDate = value.HasValue ? new DateTimeOffset(value.Value.Date) : null;
    }
}
