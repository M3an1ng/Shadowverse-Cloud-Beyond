using System.Reflection;
using System.Text.Json;
using ShadowverseBeyond.Server.Services;

internal static class AiEngineIdentityTests
{
    private const BindingFlags Hidden = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly Type Engine = typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiEngineRulesIdentity")!;
    private static object Read(string path) => Engine.GetMethod("Read", Hidden)!.Invoke(null, [path])!;
    private static T Value<T>(object build, string name) => (T)build.GetType().GetProperty(name)!.GetValue(build)!;
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    public static void Separation()
    {
        var source = Engine.GetNestedType("Source", BindingFlags.NonPublic)!;
        object Doc(string name, string hash) => Activator.CreateInstance(source, [name, "sha256", hash])!;
        string Key((string Name, string Hash)[] rows, string[]? options = null, string refs = "refs", string runtime = "8.0")
        {
            var array = Array.CreateInstance(source, rows.Length);
            for (var i = 0; i < rows.Length; i++) array.SetValue(Doc(rows[i].Name, rows[i].Hash), i);
            return (string)Engine.GetMethod("InputsFingerprint", Hidden)!.Invoke(null,
                [array, options ?? ["compiler-version", "same", "define", "RELEASE", "source-file-count", "2"], refs, runtime])!;
        }
        (string, string)[] original = [("Domain/Rules.cs", "rules"), ("Services/AiService.cs", "old-policy")];
        var baseline = Key(original);
        Check(Key([("Domain/Rules.cs", "rules"), ("Services/AiService.cs", "new-policy"), ("Services/AiNewPolicy.cs", "new-file")],
            ["define", "RELEASE", "compiler-version", "same", "source-file-count", "3"]) == baseline,
            "AI-only change or source count expired native rule identity");
        Check(Key(original.Reverse().ToArray()) == baseline, "document enumeration order expired rule identity");
        Check(Key([("Domain/Rules.cs", "new-rules"), ("Services/AiService.cs", "old-policy")]) != baseline, "changed resolution code reused rule identity");
        Check(Key([.. original, ("Domain/NewRule.cs", "new")]) != baseline, "new rule source ignored");
        Check(Key([.. original, ("obj/GeneratedRule.g.cs", "generated")]) != baseline, "generated rule source ignored");
        Check(Key([.. original, ("Controllers/GameController.cs", "endpoint")]) != baseline, "non-AI endpoint excluded");
        Check(Key(original, ["compiler-version", "same", "define", "DIFFERENT"]) != baseline
            && Key(original, ["compiler-version", "different", "define", "RELEASE"]) != baseline
            && Key(original, refs: "new-dependency") != baseline && Key(original, runtime: "8.1") != baseline,
            "compiler, conditional symbols, dependency or runtime change reused identity");
        var rejected = false;
        try { Key(original, ["define", "RELEASE"]); } catch (TargetInvocationException ex) when (ex.InnerException is InvalidDataException) { rejected = true; }
        Check(rejected, "incomplete compiler evidence accepted");

        var path = typeof(AiService).Assembly.Location;
        var current = Read(path);
        Check(Value<bool>(current, "VerifiedInputs") && Value<int>(current, "RuleDocuments") > 700,
            "real server symbols not verified; identity silently fell back to whole module");
        var legacy=(string)Engine.GetMethod("LegacyFingerprint",Hidden)!.Invoke(null,[Value<string>(current,"Module")])!;
        bool Equivalent(string recorded,string active)=>(bool)Engine.GetMethod("Equivalent",Hidden)!.Invoke(null,[recorded,active])!;
        Check(!Equivalent(legacy,Value<string>(current,"Fingerprint")),"unproven MVID history was aliased automatically");
        Check((bool)Engine.GetMethod("ProveLegacyBuild",Hidden)!.Invoke(null,[path])!,"matching bound build could not prove old identity");
        Check(Equivalent(legacy,Value<string>(current,"Fingerprint"))&&!Equivalent(legacy,"different-current-rule")
            &&!Equivalent("unverified-previous-build",Value<string>(current,"Fingerprint")),"legacy proof bypassed native rule boundary");
        var directory = Path.Combine(Path.GetTempPath(), "ai-rule-symbols-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var copy = Path.Combine(directory, Path.GetFileName(path));
            File.Copy(path, copy);
            var missing = Read(copy);
            Check(!(bool)Engine.GetMethod("ProveLegacyBuild",Hidden)!.Invoke(null,[copy])!,"missing symbols proved old-rule equivalence");
            Check(!Value<bool>(missing, "VerifiedInputs") && Value<string>(missing, "Module") == Value<string>(current, "Module")
                && Value<string>(missing, "Fingerprint") != Value<string>(current, "Fingerprint"), "missing symbols claimed verified reusable rules");
            File.Copy(Path.ChangeExtension(typeof(AiEngineIdentityTests).Assembly.Location, ".pdb"), Path.ChangeExtension(copy, ".pdb"));
            var wrong = Read(copy);
            Check(!Value<bool>(wrong, "VerifiedInputs") && Value<string>(wrong, "Fingerprint") == Value<string>(missing, "Fingerprint"),
                "wrong symbols escaped conservative exact-module fallback");
            File.Copy(Path.ChangeExtension(path, ".pdb"), Path.ChangeExtension(copy, ".pdb"), true);
            var matched = Read(copy);
            Check(Value<bool>(matched, "VerifiedInputs") && Value<string>(matched, "Fingerprint") == Value<string>(current, "Fingerprint"),
                "relocation of the same verified build expired rule identity");
        }
        finally { foreach (var file in Directory.EnumerateFiles(directory)) File.Delete(file); Directory.Delete(directory); }
        Console.WriteLine("PASS rule/compiler input separation; bound symbols, missing/mismatched fallback and relocation; real provider calls=0");
    }

    public static void GuideInputs()
    {
        var method = typeof(AiStrategyPlanner).GetMethod("GuideIdentity", Hidden)!;
        string Key(string prompt = "prompt", string principles = "core", string lines = "verified", string refs = "refs",
            string engine = "native-rules", string model = "model", string provider = "provider") =>
            (string)method.Invoke(null, [new { engine }, "deck", model, provider, prompt, refs, lines, principles])!;
        var original = Key();
        Check(Key() == original && Key(prompt: "changed") != original && Key(principles: "changed") != original
            && Key(lines: "changed") != original && Key(refs: "changed") != original && Key(engine: "changed") != original
            && Key(model: "changed") != original && Key(provider: "changed") != original,
            "guide cache ignored changed prompt/principle/route/rules/provider/model inputs");
    }

    public static void CompareBuilds(string output, string[] paths)
    {
        var builds = paths.Select(path => new { path, build = Read(path) }).ToArray();
        Check(builds.All(row => Value<bool>(row.build, "VerifiedInputs")), "cross-build identity lacks matched symbols");
        Check(builds.Length >= 2 && Value<string>(builds[0].build, "Module") != Value<string>(builds[1].build, "Module")
            && Value<string>(builds[0].build, "Fingerprint") == Value<string>(builds[1].build, "Fingerprint"),
            "actual AI-only rebuild did not preserve native rule fingerprint");
        foreach (var row in builds.Skip(2)) Check(Value<string>(row.build, "Fingerprint") != Value<string>(builds[0].build, "Fingerprint"),
            "actual rule/compiler change reused old fingerprint");
        var proven=(bool)Engine.GetMethod("ProveLegacyBuild",Hidden)!.Invoke(null,[paths[0]])!;
        File.WriteAllText(output, JsonSerializer.Serialize(new { paidCalls = 0, executesInputAssemblies = false, builds,
            currentServerLegacyProof=proven,
            policyOnlyReusesRules = true, changedRulesInvalidated = builds.Length - 2,
            oldMvidOnlyHistoryAutoAliased = false }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"PASS {builds.Length} bound cross-build identities; pure policy preserved, rule/compiler changes expired");
    }
}
