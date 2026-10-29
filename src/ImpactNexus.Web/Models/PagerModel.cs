namespace ImpactNexus.Web.Models;

public class PagerModel
{
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int TotalCount { get; init; }
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;

    public static PagerModel From<T>(PagedList<T> list) => new()
    {
        Page = list.Page,
        TotalPages = list.TotalPages,
        TotalCount = list.TotalCount
    };
}
