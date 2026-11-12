using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using ShadowverseBeyond.Server.Services;

internal static class AiBuildInputAudit
{
    internal static void Run(string output,string[] paths)
    {
        var type=typeof(AiService).Assembly.GetType("ShadowverseBeyond.Server.Services.AiEngineRulesIdentity")!;
        object Read(string path){using var fs=File.OpenRead(path);using var pe=new PEReader(fs);using var sf=File.OpenRead(Path.ChangeExtension(path,".pdb"));using var provider=MetadataReaderProvider.FromPortablePdbStream(sf);var reader=provider.GetMetadataReader();
            var docs=reader.Documents.Select(h=>{var d=reader.GetDocument(h);var name=reader.GetString(d.Name).Replace('\\','/');var i=name.IndexOf("/Server/",StringComparison.OrdinalIgnoreCase);name=i<0?name:name[(i+8)..];return new{name,hash=Convert.ToHexString(reader.GetBlobBytes(d.Hash))};}).ToArray();
            var extra=reader.CustomDebugInformation.Select(h=>{var d=reader.GetCustomDebugInformation(h);return new{kind=reader.GetGuid(d.Kind).ToString(),value=Convert.ToHexString(reader.GetBlobBytes(d.Value)),text=Encoding.UTF8.GetString(reader.GetBlobBytes(d.Value))};}).Where(d=>d.kind is "b5feec05-8cd0-4a83-96da-466284bb4bd8" or "7e4d4708-096e-4c5c-aeda-cb10ba6a740d").ToArray();
            return new{path,identity=type.GetMethod("Read",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[path]),documents=docs,extra};}
        File.WriteAllText(output,JsonSerializer.Serialize(paths.Select(Read),new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("Read bound compiler inputs without executing input assemblies");
    }
}
