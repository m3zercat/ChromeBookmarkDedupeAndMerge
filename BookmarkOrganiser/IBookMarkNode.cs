using System;

namespace BookmarkOrganiser
{
    public interface IBookMarkNode
    {
        string Title { get; }
        int Depth { get; }
        DateTime CreatedDate { get; }
        BookMarkFolder Parent { get; }
    }
}