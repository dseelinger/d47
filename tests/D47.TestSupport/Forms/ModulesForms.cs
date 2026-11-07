namespace D47.Core.Modules;

public static class ModuleForms
{
    extension(ModuleInbox inbox)
    {
        public IReadOnlyList<ModuleNotice> Drain() => inbox.Drain<ModuleNotice>();
    }
}
