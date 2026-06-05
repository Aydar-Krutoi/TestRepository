using Ranil_Uchebka.Services;

namespace Ranil_Uchebka.LogicTests;

public sealed class OrderWorkflowServiceTests
{
    [Fact]
    public void Manager_CanMove_FromPodtverzhdenie_ToZakupka()
    {
        var result = OrderWorkflowService.CanRoleChangeStatus("Менеджер", "Подтверждение", "Закупка");

        Assert.True(result);
    }

    [Fact]
    public void Manager_CanMove_FromZakupka_ToProizvodstvo()
    {
        var result = OrderWorkflowService.CanRoleChangeStatus("Менеджер", "Закупка", "Производство");

        Assert.True(result);
    }

    [Fact]
    public void Master_CannotMove_FromZakupka_ToProizvodstvo()
    {
        var result = OrderWorkflowService.CanRoleChangeStatus("Мастер", "Закупка", "Производство");

        Assert.False(result);
    }

    [Fact]
    public void Master_CanMove_FromProizvodstvo_ToKontrol()
    {
        var result = OrderWorkflowService.CanRoleChangeStatus("Мастер", "Производство", "Контроль");

        Assert.True(result);
    }

    [Fact]
    public void Customer_CanCancel_BeforeZakupka()
    {
        var result = OrderWorkflowService.CanRoleChangeStatus("Заказчик", "Подтверждение", "Отклонен");

        Assert.True(result);
    }

    [Fact]
    public void Customer_CannotCancel_AfterZakupka()
    {
        var result = OrderWorkflowService.CanRoleChangeStatus("Заказчик", "Закупка", "Отклонен");

        Assert.False(result);
    }

    [Fact]
    public void StatusesForFilterNew_ReturnsEarlyOrderStages()
    {
        var statuses = OrderWorkflowService.StatusesForFilter(OrderWorkflowService.FilterNew);

        Assert.Equal(new[] { "Новый", "Составление спецификации", "Подтверждение" }, statuses);
    }

    [Fact]
    public void AllowedStatuses_ForManagerFromNew_AreAcceptAndReject()
    {
        var statuses = OrderWorkflowService.GetAllowedTargetStatuses("Менеджер", "Новый");

        Assert.Equal(new[] { "Отклонен", "Составление спецификации" }, statuses);
    }
}
