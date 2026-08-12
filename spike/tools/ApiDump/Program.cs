using System.Reflection;
var dir = args[0]; var pattern = args[1];
var files = Directory.GetFiles(dir, "*.dll")
  .Concat(Directory.GetFiles(@"C:\Program Files\dotnet\shared\Microsoft.NETCore.App\10.0.10", "*.dll"))
  .GroupBy(Path.GetFileName).Select(g=>g.First()).ToArray();
var mlc = new MetadataLoadContext(new PathAssemblyResolver(files));
foreach (var f in files.Where(x => Path.GetFileName(x).StartsWith("Avalonia"))) {
  Assembly a; try { a = mlc.LoadFromAssemblyPath(f); } catch { continue; }
  Type[] types; try { types = a.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(t=>t!=null).ToArray(); }
  foreach (var t in types) {
    if (t?.FullName == null || !t.FullName.Equals(pattern, StringComparison.Ordinal)) continue;
    Console.WriteLine($"[{a.GetName().Name}] {t.FullName}  (public={t.IsPublic})");
    foreach (var i in t.GetInterfaces()) Console.WriteLine($"   inherits {i.FullName}");
    foreach (var m in t.GetMembers(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly)) {
      if (m is MethodInfo mi) Console.WriteLine($"   {(mi.IsPublic?"public   ":mi.IsAssembly?"internal ":mi.IsFamily?"protected":"private  ")} {mi.Name}");
      else if (m is PropertyInfo pi) Console.WriteLine($"   property  {pi.Name}");
    }
  }
}
