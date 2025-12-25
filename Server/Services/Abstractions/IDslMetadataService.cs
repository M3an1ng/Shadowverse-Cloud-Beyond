using ShadowverseBeyond.Server.Dtos.Dsl;

namespace ShadowverseBeyond.Server.Services.Abstractions
{
    public interface IDslMetadataService
    {
        DslMetadataDto GetDslMetadata();
    }
}