using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ShadowverseBeyond.Server.Services;

// A scoring/prompt edit is not a change to card resolution. Use bound compiler inputs,
// never source files from the deployment directory or a caller-provided compatibility claim.
internal static class AiEngineRulesIdentity
{
    internal sealed record Source(string Name, string Algorithm, string Checksum);
    internal sealed record Build(string Fingerprint, string Module, bool VerifiedInputs, int RuleDocuments, string? FallbackReason);
    private static readonly Guid OptionsKind = new("b5feec05-8cd0-4a83-96da-466284bb4bd8");
    private static readonly Guid ReferencesKind = new("7e4d4708-096e-4c5c-aeda-cb10ba6a740d");
    private static readonly Lazy<Build> CurrentBuild = new(() => Read(typeof(AiEngineRulesIdentity).Assembly.Location));
    internal static Build Current => CurrentBuild.Value;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string,byte> ProvenLegacy = new(StringComparer.Ordinal);
    internal static int ProvenLegacyCount => ProvenLegacy.Count;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string,bool> CheckedBuilds = new(StringComparer.Ordinal);
    internal static string LegacyFingerprint(string module) => Digest(JsonSerializer.SerializeToUtf8Bytes(new { module, schema = 1 }));
    internal static bool ProveLegacyBuild(string path) => CheckedBuilds.GetOrAdd(path, candidate => {
        var old = Read(candidate);
        if (!old.VerifiedInputs || !Current.VerifiedInputs || old.Fingerprint != Current.Fingerprint) return false;
        ProvenLegacy.TryAdd(LegacyFingerprint(old.Module), 0);
        return true;
    });
    internal static bool Equivalent(string recorded,string current) => recorded == current
        || current == Current.Fingerprint && Current.VerifiedInputs && ProvenLegacy.ContainsKey(recorded);

    private static bool IsPolicy(string name) => name.StartsWith("Services/Ai", StringComparison.Ordinal)
        || name is "Services/IAiService.cs" or "Services/LocalAiTrainingService.cs" or "Controllers/LocalAiTrainingController.cs";

    internal static string InputsFingerprint(Source[] documents, string[] options, string references, string runtime)
    {
        if (documents.Length == 0 || options.Length % 2 != 0 || string.IsNullOrEmpty(references))
            throw new InvalidDataException("Incomplete compiler inputs");
        var settings = new SortedDictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < options.Length; index += 2)
        {
            // Adding an AI file changes this count. Every non-policy document is hashed below.
            if (options[index] == "source-file-count") continue;
            settings.Add(options[index], options[index + 1]);
        }
        if (!settings.ContainsKey("compiler-version") || !settings.ContainsKey("define"))
            throw new InvalidDataException("Compiler settings unavailable");
        var rules = documents.Where(doc => !IsPolicy(doc.Name)).OrderBy(doc => doc.Name, StringComparer.Ordinal).ToArray();
        if (rules.Length == 0 || rules.Select(doc => doc.Name).Distinct(StringComparer.Ordinal).Count() != rules.Length
            || rules.Any(doc => string.IsNullOrEmpty(doc.Name) || doc.Checksum.Length == 0 || doc.Algorithm.Length == 0))
            throw new InvalidDataException("Rule document checksums unavailable");
        return Digest(JsonSerializer.SerializeToUtf8Bytes(new { schema = 2, rules, settings, references, runtime }));
    }

    internal static Build Read(string assemblyPath)
    {
        var module = Guid.Empty;
        try
        {
            using var image = File.OpenRead(assemblyPath);
            using var pe = new PEReader(image);
            var metadata = pe.GetMetadataReader();
            module = metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
            var views = pe.ReadDebugDirectory().Where(entry => entry.Type == DebugDirectoryEntryType.CodeView).ToArray();
            if (views.Length != 1) throw new InvalidDataException("Portable symbols not bound");
            var codeView = pe.ReadCodeViewDebugDirectoryData(views[0]);
            using var symbols = File.OpenRead(Path.ChangeExtension(assemblyPath, ".pdb"));
            using var provider = MetadataReaderProvider.FromPortablePdbStream(symbols);
            var reader = provider.GetMetadataReader();
            var id = reader.DebugMetadataHeader?.Id.ToArray();
            if (id is not { Length: 20 } || new Guid(id.AsSpan(0, 16)) != codeView.Guid
                || System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(id.AsSpan(16)) != views[0].Stamp)
                throw new InvalidDataException("Portable symbols mismatch");
            var documents = new List<Source>();
            foreach (var handle in reader.Documents)
            {
                var doc = reader.GetDocument(handle);
                var path = reader.GetString(doc.Name).Replace('\\', '/');
                var start = path.IndexOf("/Server/", StringComparison.OrdinalIgnoreCase);
                // Unknown/generated sources are included too. Ignore only explicitly named policy files.
                var name = start < 0 ? path : path[(start + 8)..];
                var hash = reader.GetBlobBytes(doc.Hash);
                if (doc.HashAlgorithm.IsNil || hash.Length == 0) throw new InvalidDataException("Source checksum missing");
                documents.Add(new(name, reader.GetGuid(doc.HashAlgorithm).ToString("N"), Convert.ToHexString(hash)));
            }
            string[]? options = null;
            string? references = null;
            foreach (var handle in reader.CustomDebugInformation)
            {
                var item = reader.GetCustomDebugInformation(handle);
                var kind = reader.GetGuid(item.Kind);
                if (kind == OptionsKind)
                {
                    if (options != null) throw new InvalidDataException("Ambiguous compilation settings");
                    options = Encoding.UTF8.GetString(reader.GetBlobBytes(item.Value)).TrimEnd('\0').Split('\0');
                }
                if (kind == ReferencesKind)
                {
                    if (references != null) throw new InvalidDataException("Ambiguous compilation references");
                    references = Digest(reader.GetBlobBytes(item.Value));
                }
            }
            var fingerprint = InputsFingerprint(documents.ToArray(), options ?? [], references ?? "", Environment.Version.ToString());
            return new(fingerprint, module.ToString("N"), true, documents.Count(doc => !IsPolicy(doc.Name)), null);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or BadImageFormatException
            or InvalidOperationException or ArgumentException)
        {
            // No usable symbols: preserve the former exact-module boundary. Never assume rule equivalence.
            if (module == Guid.Empty) module = typeof(AiEngineRulesIdentity).Module.ModuleVersionId;
            return new(LegacyFingerprint(module.ToString("N")),
                module.ToString("N"), false, 0, error.GetType().Name);
        }
    }

    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
