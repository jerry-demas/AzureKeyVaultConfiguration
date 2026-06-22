using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Cbiz.SharedPackages;
using CBIZ.SharedPackages;
using CBIZ.SharedPackages.AzureKeyVaultConfiguration;
using ConsoleTest;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;


// See https://aka.ms/new-console-template for more information
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<OptionConfig>().BindConfiguration(nameof(OptionConfig));
builder.Services.AddOptions<Test>().BindConfiguration(nameof(Test));
builder.AddAzureKeyVaultAsConfig();
builder.Services.AddTransient<ReloadTestIOptionsMonitor>();
builder.Services.AddTransient<ReloadTestIOptionsSnapshot>();
builder.Services.AddTransient<ReloadTestIOptions>();
builder.Services.AddLogging();

using IHost host = builder.Build();

using (IServiceScope serviceScope = host.Services.CreateScope())
{
    var config = serviceScope.ServiceProvider.GetRequiredService<IOptions<Test>>();
    var keyVaultSecretsManager = serviceScope.ServiceProvider.GetRequiredService<AzureKeyVaultSecretsManager>();

    Console.WriteLine($"Initial Config from key vault - test1: {config.Value.test1}");
    Console.WriteLine($"Initial Config from key vault - test2: {config.Value.test2}");

    Console.WriteLine();
    Console.WriteLine("Get Secrets Manually");
    string test1 = "";
    keyVaultSecretsManager.GetSecretAsync("test1").WhenAwaited()
        .WhenFailure((_, ex) => Console.WriteLine($"Failed to get secret: {ex.Message}"))
        .OnSuccess((secret) =>
        {
            Console.WriteLine($"Get Secret Manually - test1 - {secret}");
            test1 = secret;
        });

    string test2 = "";
    keyVaultSecretsManager.GetSecretAsync("test--test2").WhenAwaited()
        .WhenFailure((_, ex) => Console.WriteLine($"Failed to get secret: {ex.Message}"))
        .OnSuccess((secret) =>
        {
            Console.WriteLine($"Get Secret Manually - test--test2 - {secret}");
            test2 = secret;
        });

    (await keyVaultSecretsManager.GetSecretAsync("notExist"))
    .WhenFailure((_, ex) => Console.WriteLine($"Failed to get secret: {ex.Message}"))
    .OnSuccess((secret) => Console.WriteLine($"notExist: {secret}"));


    Console.WriteLine();
    Console.WriteLine("Set Secrets Manually");
    (await keyVaultSecretsManager.SetSecretAsync("test1", $"test - {DateTime.Now.ToShortTimeString()}"))
        .WhenFailure((_, ex) => Console.WriteLine($"Failed to set secret: {ex.Message}"));


    (await keyVaultSecretsManager.SetSecretAsync("test--test2", $"test - {DateTime.Now.ToShortTimeString()}"))
        .WhenFailure((_, ex) => Console.WriteLine($"Failed to set secret: {ex.Message}"));

    Console.WriteLine();
    Console.WriteLine("Get Secrets Manually to show change");
    //either Chaining
    (await keyVaultSecretsManager.GetSecretAsync("test1"))
        .WhenFailure((_, ex) => Console.WriteLine($"Failed to get secret: {ex.Message}"))
        .OnSuccess((secret) => Console.WriteLine($"test1: {secret}"));

    //either Match
    (await keyVaultSecretsManager.GetSecretAsync("test2"))
        .Match(
            forFailure: (_, ex) => Console.WriteLine($"Failed to get secret: {ex.Message}"),
            forSuccess: (secret) => Console.WriteLine($"test2: {secret}")
         );

    //either if
    var result = (await keyVaultSecretsManager.GetSecretAsync("test--test2"));
    if( result.HasFailure)
    {
        Console.WriteLine($"Failed to get secret: {result.Failure.Message}");
    }
    else
    {
        Console.WriteLine($"test--test2: {result.Value}");
    }

    Console.WriteLine();
    Console.WriteLine("Get Secrets Manually - other vault by index");
    (await keyVaultSecretsManager.GetSecretAsync(1, "MessageLoopOptions--MessagePrefix"))
        .WhenFailure((_, ex) => Console.WriteLine($"Failed to get secret: {ex.Message}"))
        .OnSuccess((secret) => Console.WriteLine($"MessageLoopOptions--MessagePrefix: {secret}"));

    Console.WriteLine();
    Console.WriteLine("Get Secrets Manually - other vault by name");
    (await keyVaultSecretsManager.GetSecretAsync("kv-appsdev-testapp1", "MessageLoopOptions--MessagePrefix"))
        .WhenFailure((_, ex) => Console.WriteLine($"Failed to get secret: {ex.Message}"))
        .OnSuccess((secret) => Console.WriteLine($"MessageLoopOptions--MessagePrefix: {secret}"));


    //Reload test
    Console.WriteLine();
    Console.WriteLine("Show Secrets refreshing in IOptions");
    var reloadTestIOptionsMonitor = serviceScope.ServiceProvider.GetRequiredService<ReloadTestIOptionsMonitor>();
    var reloadTestIOptionsSnapshot = serviceScope.ServiceProvider.GetRequiredService<ReloadTestIOptionsSnapshot>();
    var reloadTestIOptions = serviceScope.ServiceProvider.GetRequiredService<ReloadTestIOptions>();

    reloadTestIOptionsMonitor.Test("ReloadTestIOptionsMonitor - BeforeRefresh -- doesn't see test--test2 changed above, refresh hasn't occured");
    await Task.Delay(180000);
     reloadTestIOptionsMonitor.Test("ReloadTestIOptionsMonitor - AfterRefresh - same scope but still updates");

    reloadTestIOptionsSnapshot.Test("ReloadTestIOptionsSnapshot - After Refresh - still old values as IOptionsSnapshot is cached per scope");
    reloadTestIOptions.Test("ReloadTestIOptions - After Refresh - still old values as IOptions is cached per app start");
}

using (IServiceScope serviceScope = host.Services.CreateScope())
{
    var reloadTestIOptionsSnapshot = serviceScope.ServiceProvider.GetRequiredService<ReloadTestIOptionsSnapshot>();
    var reloadTestIOptions = serviceScope.ServiceProvider.GetRequiredService<ReloadTestIOptions>();

    reloadTestIOptionsSnapshot.Test("ReloadTestIOptionsSnapshot - After Refresh, new scope - new values as IOptionsSnapshot is cached per scope");
    reloadTestIOptions.Test("ReloadTestIOptions - After Refresh, new scope - still old values as IOptions is cached per app start");
}
using (IServiceScope serviceScope = host.Services.CreateScope())
{
    var keyVaultSecretsManager = serviceScope.ServiceProvider.GetRequiredService<AzureKeyVaultSecretsManager>();
    var cancellationToken = new CancellationToken();
    
    Console.WriteLine();
    (await keyVaultSecretsManager.GetSecretAsync("kv-appsdev-testapp1", "MessageLoopOptions--MessagePrefix", cancellationToken))
       .WhenFailure((_, ex) => Console.WriteLine($"Failed to get secret: {ex.Message}"))
       .OnSuccess((secret) => Console.WriteLine($"Get with a CancellationToken - MessageLoopOptions--MessagePrefix: {secret}"));
}

public class ReloadTestIOptionsMonitor
{
    private readonly IOptionsMonitor<Test> _config;
    
    public ReloadTestIOptionsMonitor(IOptionsMonitor<Test> config)
    {
        _config = config;
    }

    public void Test(string header)
    {
        Console.WriteLine();
        Console.WriteLine($"{header} - {DateTime.Now.ToShortTimeString()}");
        Console.WriteLine($"ReloadTestIOptionsMonitor test1:{_config.CurrentValue.test1}");
        Console.WriteLine($"ReloadTestIOptionsMonitor test2:{_config.CurrentValue.test2}");
    }
}

public class ReloadTestIOptionsSnapshot
{
    private readonly Test _config;

    public ReloadTestIOptionsSnapshot(IOptionsSnapshot<Test> config)
    {
        _config = config.Value;
    }

    public void Test(string header)
    {
        Console.WriteLine();
        Console.WriteLine($"{header} - {DateTime.Now.ToShortTimeString()}");
        Console.WriteLine($"ReloadTestIOptionsSnapshot test1:{_config.test1}");
        Console.WriteLine($"ReloadTestIOptionsSnapshot test2:{_config.test2}");
    }
}

public class ReloadTestIOptions
{
    private readonly Test _config;

    public ReloadTestIOptions(IOptions<Test> config)
    {
        _config = config.Value;
    }

    public void Test(string header)
    {
        Console.WriteLine();
        Console.WriteLine($"{header} - {DateTime.Now.ToShortTimeString()}");
        Console.WriteLine($"ReloadTestIOptions test1:{_config.test1}");
        Console.WriteLine($"ReloadTestIOptions test2:{_config.test2}");
    }
}