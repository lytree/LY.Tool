namespace LYBox.Plugin.Shared.CommandLine;

/// <summary>Optional generated module hook for CLI command registrars.</summary>
public interface IGeneratedPluginCliModule
{
    IReadOnlyList<PluginCliRegistrarDescriptor> CliRegistrars { get; }
}

public sealed class PluginCliRegistrarDescriptor
{
    public PluginCliRegistrarDescriptor(
        Type registrarType,
        Func<IServiceProvider, IPluginCommandRegistrar> createRegistrar)
    {
        RegistrarType = registrarType ?? throw new ArgumentNullException(nameof(registrarType));
        CreateRegistrar = createRegistrar ?? throw new ArgumentNullException(nameof(createRegistrar));
    }

    public Type RegistrarType { get; }
    public Func<IServiceProvider, IPluginCommandRegistrar> CreateRegistrar { get; }
}
