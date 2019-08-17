using System;
using System.Collections.Generic;
using System.Linq;

namespace BookmarkOrganiser
{
    public class BookMarkFolder : IBookMarkNode
    {
        public BookMarkFolder(string title, int depth, DateTime createdDate, DateTime lastModifiedDate, BookMarkFolder parent, bool personalToolbarFolder = false, IEnumerable<IBookMarkNode> children = null)
        {
            Title = title;
            Depth = depth;
            CreatedDate = createdDate;
            LastModifiedDate = lastModifiedDate;
            Parent = parent;
            PersonalToolbarFolder = personalToolbarFolder;
            Children = new List<IBookMarkNode>(children ?? new IBookMarkNode[0]);
            FullTitle = $"{Parent?.FullTitle}/{Title}";
        }

        public string Title { get; }
        public int Depth { get; }
        public DateTime CreatedDate { get; }
        public DateTime LastModifiedDate { get; }
        public BookMarkFolder Parent { get; }
        public bool PersonalToolbarFolder { get; }
        public List<IBookMarkNode> Children { get; }
        public string FullTitle { get; }
        public override string ToString()
        {
            return FullTitle;
        }
    }
}