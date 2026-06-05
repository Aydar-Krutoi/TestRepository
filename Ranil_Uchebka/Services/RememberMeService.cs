using System.Text.Json;
using Ranil_Uchebka.Models;

namespace Ranil_Uchebka.Services;

public sealed class RememberMeService
{
    private readonly string _filePath;

    public RememberMeService()
    {
        var appDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Ranil_Uchebka");
        Directory.CreateDirectory(appDir);
        _filePath = Path.Combine(appDir, "remember_me.json");
    }

    public async Task<RememberCredentials?> ReadAsync()
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        try
        {
            var text = await File.ReadAllTextAsync(_filePath);
            return JsonSerializer.Deserialize<RememberCredentials>(text);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveAsync(string login, string password)
    {
        var payload = new RememberCredentials
        {
            Login = login,
            Password = password
        };
        var json = JsonSerializer.Serialize(payload);
        await File.WriteAllTextAsync(_filePath, json);
    }

    public Task ClearAsync()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }

        return Task.CompletedTask;
    }
}
