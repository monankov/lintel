# Lintel.Settings

Typed settings stored in JSON or XML files. Unlike `IOptions<T>`, the application can also **change** the settings
at run time and save them back to the file. Properties can be encrypted.

Use it for settings that an administrator edits in the application (mail server, limits, texts). For settings that
only change with a deployment, the standard `IConfiguration` and `IOptions<T>` are enough.

## Install

```
dotnet add package Lintel.Settings
```

## Read and change settings

```csharp
using Lintel.Settings;

public sealed class SmtpSettings
{
    public string Host { get; set; } = "";

    [Option(DefaultValue = 25)]
    public int Port { get; set; }

    [Option(Alias = "user")]
    public string? UserName { get; set; }
}

builder.Services.AddConfiguration()
    .AddJson<SmtpSettings>(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "smtp.json"));
```

```csharp
public sealed class MailService(IConfiguration<SmtpSettings> configuration, IConfigurator<SmtpSettings> configurator)
{
    public string Host => configuration.Settings.Host;                     // read

    public void ChangeHost(string host) => configurator.Configure(s => s.Host = host); // change and save
}
```

`AddXml<T>` stores the settings in XML, and `AddInMemory<T>` keeps them only in memory (for tests).
The file must exist: a missing file throws `FileNotFoundException`. A change is written to a temporary file first and
then moved over the original, so readers never see a half-written file.

## Options

| Option | Meaning |
|---|---|
| `[Option(Alias = "...")]` | another name of the property in the file |
| `[Option(DefaultValue = ...)]` | the value when the file has none |
| `[Option(Protected = true)]` | the string is encrypted in the file with ASP.NET Core Data Protection; set `DataProtector` in the provider options |
| `[Option(EncryptionPassphrase = "...")]` | the string is encrypted with a passphrase in the code (the Structr format, for existing files only) |

Settings are cached and read again when the file changes. Turn the cache off with
`AddJson<T>(path, o => o.Cache = false)`.

## Lintel

Lintel is a set of small libraries for .NET 10 applications. See the other `Lintel.*` packages.
