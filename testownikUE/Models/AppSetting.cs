using System;

namespace testownikUE.Models;

public class AppSetting
{
    //Key-value pairs dla ustawień użytkownika
    public int Id { get; set; }
    public string SettingKey { get; set; } = string.Empty;
    public string SettingValue { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
}

