using D47.Core.Capabilities;

namespace D47.Core.Configuration;

public static class ConfigurationForms
{
    extension(SettingsService settings)
    {
        public bool HoldsSecret(SettingRow row) => settings.SecretNameFor(row) is { } name && settings.HoldsSecret(name);
    }
}
