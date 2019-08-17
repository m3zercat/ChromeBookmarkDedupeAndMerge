using System;
using System.Collections.Generic;

namespace BookmarkOrganiser
{
    public class BookMark : IBookMarkNode
    {
        public BookMark(string title, string icon, string link, int depth, DateTime createdDate, BookMarkFolder parent)
        {
            Title = title;
            Icon = icon;
            Link = link;
            Depth = depth;
            CreatedDate = createdDate;
            Parent = parent;
        }

        public string Title { get; }
        public string Icon { get; }
        public string Link { get; }
        public int Depth { get; }
        public DateTime CreatedDate { get; }
        public BookMarkFolder Parent { get; }
        public override string ToString()
        {
            return $"{Parent?.FullTitle}/{Title}";
        }
    }
}