# AppSettings Runtime Refresh (No Service Restart)

This guide shows how to refresh configuration values at runtime without restarting the service.

Scenarios covered:

- Reload `appsettings.json` on change.
- Use DI with `IOptionsMonitor<T>` to get latest values.
- Pull a value from Windows Registry and update configuration at runtime.

---

## 1) Reload `appsettings.json` automatically

In your host setup, enable reload on change:

```csharp
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();
```

This reloads config when the file changes on disk.

---

## 2) Use `IOptionsMonitor<T>` to get updated values

Create options:

```csharp
public sealed class MySettings
{
    public string Source { get; init; } = "";
    public string Target { get; init; } = "";
}
```

Register:

```csharp
builder.Services.Configure<MySettings>(builder.Configuration.GetSection("MySettings"));
```

Consume:

```csharp
public sealed class MyService
{
    private readonly IOptionsMonitor<MySettings> _settings;

    public MyService(IOptionsMonitor<MySettings> settings)
    {
        _settings = settings;
    }

    public string GetTarget() => _settings.CurrentValue.Target;
}
```

`IOptionsMonitor<T>` always returns the latest values after reload.

---

## 3) Read from Windows Registry and update config at runtime

Use a custom configuration source that reads from registry and reloads on a timer:

```csharp
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Microsoft.Win32;

public sealed class RegistryConfigurationSource : IConfigurationSource
{
    public string RegistryPath { get; init; } = "";
    public string ValueName { get; init; } = "";
    public string ConfigKey { get; init; } = "";
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(10);

    public IConfigurationProvider Build(IConfigurationBuilder builder)
        => new RegistryConfigurationProvider(this);
}

public sealed class RegistryConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly RegistryConfigurationSource _source;
    private readonly CancellationTokenSource _cts = new();
    private string? _lastValue;

    public RegistryConfigurationProvider(RegistryConfigurationSource source)
    {
        _source = source;
    }

    public override void Load()
    {
        ReadValue();
        _ = PollAsync();
    }

    private void ReadValue()
    {
        using var key = Registry.LocalMachine.OpenSubKey(_source.RegistryPath);
        var value = key?.GetValue(_source.ValueName)?.ToString();

        if (!string.Equals(value, _lastValue, StringComparison.Ordinal))
        {
            _lastValue = value;
            Data[_source.ConfigKey] = value ?? "";
            OnReload();
        }
    }

    private async Task PollAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            await Task.Delay(_source.PollInterval, _cts.Token).ConfigureAwait(false);
            ReadValue();
        }
    }

    public void Dispose() => _cts.Cancel();
}
```

Register the source:

```csharp
builder.Configuration.Add(new RegistryConfigurationSource
{
    RegistryPath = @"SOFTWARE\\MyApp",
    ValueName = "TargetValue",
    ConfigKey = "MySettings:Target",
    PollInterval = TimeSpan.FromSeconds(5)
});
```

Now `IOptionsMonitor<MySettings>` will see the registry value updates without restart.

---

## 4) Example flow (source → target)

1. Set registry value `HKLM\SOFTWARE\MyApp\TargetValue`.
2. The registry provider refreshes `MySettings:Target`.
3. `IOptionsMonitor<MySettings>` exposes the new `Target` immediately.

---

## Notes

- Registry access may require elevated privileges.
- Keep `PollInterval` reasonable to avoid load.
- Use `IOptionsMonitor<T>` for runtime updates; `IOptions<T>` is static.
