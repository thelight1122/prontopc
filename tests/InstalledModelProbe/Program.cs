using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

// Read-only probe: use the installed GenAI managed/native libraries and model.
// This isolates loading; it does not prove the installed app's full repair flow.
if (args.Length < 1 || args.Length > 2 || !Directory.Exists(args[0]))
{
    Console.Error.WriteLine("Usage: InstalledModelProbe.exe <ProntoPC InstallLocation> [--preload]");
    return 2;
}
string root = Path.GetFullPath(args[0]);
string model = Path.Combine(root, "Assets", "models", "gemma-3-1b-it-formed-v0.2.1");
Console.WriteLine($"UTC={DateTimeOffset.UtcNow:O} PROCESS_X64={Environment.Is64BitProcess} RUNTIME={Environment.Version}");
Console.WriteLine($"INSTALL={root}");
foreach (string file in new[] { "genai_config.json", "model.onnx", "model.onnx.data", "tokenizer.json", "aegis-formation-manifest.json" })
{
    var info = new FileInfo(Path.Combine(model, file));
    Console.WriteLine($"MODEL_FILE={file} EXISTS={info.Exists} BYTES={(info.Exists ? info.Length : 0)}");
}
try
{
    bool preload = args.Length == 2 && args[1] == "--preload";
    Console.WriteLine($"MODE={(preload ? "explicit-preload" : "direct-genai")} ");
    // Process-local search path; does not modify package files, ACLs or system policy.
    if (!Win32.SetDllDirectory(root))
        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "SetDllDirectory failed");
    foreach (string file in new[] { "onnxruntime.dll", "onnxruntime-genai.dll" })
    {
        string path = Path.Combine(root, file);
        using var stream = File.OpenRead(path);
        Console.WriteLine($"NATIVE={file} SHA256={Convert.ToHexString(SHA256.HashData(stream))}");
        // Resolve dependencies from this installed library's directory and system directories.
        if (preload)
        {
            nint handle = Win32.LoadLibraryEx(path, 0, 0x100 | 0x1000);
            if (handle == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), $"LoadLibraryEx failed: {file}");
        }
    }
    var assembly = Assembly.LoadFrom(Path.Combine(root, "Microsoft.ML.OnnxRuntimeGenAI.dll"));
    NativeLibrary.SetDllImportResolver(assembly, (name, _, _) =>
    {
        string path = Path.Combine(root, name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? name : name + ".dll");
        return File.Exists(path) ? NativeLibrary.Load(path) : 0;
    });
    Console.WriteLine($"GENAI={assembly.FullName} STAGE=model");
    using var loaded = (IDisposable)Activator.CreateInstance(assembly.GetType("Microsoft.ML.OnnxRuntimeGenAI.Model", true)!, model)!;
    Console.WriteLine("MODEL_LOAD=PASS STAGE=tokenizer");
    using var tokenizer = (IDisposable)Activator.CreateInstance(assembly.GetType("Microsoft.ML.OnnxRuntimeGenAI.Tokenizer", true)!, loaded)!;
    Console.WriteLine("TOKENIZER_LOAD=PASS");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine("LOAD_FAILURE=" + ex);
    return 1;
}

static class Win32
{
    [DllImport("kernel32.dll", EntryPoint = "SetDllDirectoryW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetDllDirectory(string path);
    [DllImport("kernel32.dll", EntryPoint = "LoadLibraryExW", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint LoadLibraryEx(string path, nint file, uint flags);
}
