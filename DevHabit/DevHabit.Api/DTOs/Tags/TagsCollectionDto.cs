using DevHabit.Api.DTOs.Common;

namespace DevHabit.Api.DTOs.Tags;

public sealed record TagsCollectionDto : ICollectionResponse<TagDto>, ILinksResponse
{
    public List<TagDto> Items { get; init; }
    public List<LinkDto> Links { get; set; }
}
