using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace BookmarkOrganiser
{
    public static class Program
    {
        public static StringComparison ComparisonCulture = StringComparison.InvariantCultureIgnoreCase;

        const string docType = "<!DOCTYPE NETSCAPE-Bookmark-file-1>";
        private const string metaType = "<META HTTP-EQUIV=\"Content-Type\" CONTENT=\"text/html; charset=UTF-8\">";

        private static async Task Main()
        {
            try
            {
                const string fileName = "C:\\Users\\matt\\Documents\\bookmarks_17_08_2019.html";
                var bookmarksText = File.ReadAllLines(fileName);
                if (bookmarksText.Length < 1)
                    throw new InvalidOperationException("File contains no lines");
                if (!string.Equals(bookmarksText[0].Trim(), docType, ComparisonCulture))
                    throw new InvalidOperationException(
                        $"Expected the first line of the file to contain '{docType}'.{Environment.NewLine}Maybe the file is invalid or the format has been changed.");
                var progress = await bookmarksText
                    .Skip(1)
                    .FilterComments()
                    .ParseBookMarks()
                    .RemoveShallowestDuplicates()
                    .MoveNoLongerFoundDomains();

                progress
                    .MergeFolders()
                    .RemoveEmptyFolders()
                    .SaveToFile($"{fileName}.modded.html");
            }
            catch (Exception e)
            {
                if (Debugger.IsAttached)
                    Debugger.Break();
                Console.Error.WriteLine(e.Message);
            }

            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }
            else
            {
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
            }
        }
        
        private static IEnumerable<string> FilterComments(this IEnumerable<string> source)
        {
            using (var enumerator = source.GetEnumerator())
            {
                var inComment = false;
                while (enumerator.MoveNext())
                {
                    var line = enumerator.Current;
                    if (FilterCommentsOnLine(ref inComment, ref line))
                    {
                        yield return line;
                    }
                }
            }
        }

        private static bool FilterCommentsOnLine(ref bool inComment, ref string line)
        {
            while (true)
            {
                if (line == null)
                {
                    Debugger.Break();
                    return false;
                }

                if (inComment)
                {
                    var uncommentIndex = line.IndexOf("-->", ComparisonCulture);
                    if (uncommentIndex == -1) return false; // we don't uncomment in this line
                    inComment = false; // we exit the comment
                    if (line.Length - 3 == uncommentIndex)
                    {
                        return false; // but it was the end of the line, so there's nothing to return
                    }

                    line = line.Substring(uncommentIndex + 3);
                    continue;
                }


                var commentIndex = line.IndexOf("<!--", ComparisonCulture);
                if (commentIndex == -1) return true; // no comment found
                inComment = true;
                if (commentIndex == 0) // comment at start of line
                {
                    continue;
                }

                var beforeComment = line.Substring(0, commentIndex);

                if (FilterCommentsOnLine(ref inComment, ref line))
                {
                    line = beforeComment + line;
                }
                else
                {
                    line = beforeComment;
                }

                return true;
            }
        }

        private static BookMarkFolder ParseBookMarks(this IEnumerable<string> content)
        {
            using (var enumerator = content.GetEnumerator())
            {
                var currentFolder = (BookMarkFolder)null;
                var parsingFolder = (BookMarkFolder)null;
                while (enumerator.MoveNext())
                {
                    var line = enumerator.Current?.Trim();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    if (currentFolder == null)
                    {

                        if (line.StartsWith("<H1>", ComparisonCulture))
                        {
                            parsingFolder = ParseInitialFolder(line);
                            continue;
                        }

                        if (line.StartsWith("<META", ComparisonCulture)
                            || line.StartsWith("<TITLE>", ComparisonCulture))
                            continue;

                    }
                    else
                    {

                        if (line.StartsWith("<DT><A ", ComparisonCulture))
                        {
                            currentFolder.Children.Add(ParseBookMark(line, currentFolder));
                            continue;
                        }

                        if (line.StartsWith("<DT><H3", ComparisonCulture))
                        {
                            parsingFolder = ParseFolder(line, currentFolder);
                            continue;
                        }

                        if (string.Equals("</DL><p>", line, ComparisonCulture))
                        {
                            if (currentFolder.Parent == null)
                                return currentFolder;
                            currentFolder = currentFolder.Parent;
                            continue;
                        }
                    }

                    if (string.Equals("<DL><p>", line, ComparisonCulture) && parsingFolder != null)
                    {
                        currentFolder?.Children.Add(parsingFolder);
                        currentFolder = parsingFolder;
                        parsingFolder = null;
                        continue;
                    }

                    if (currentFolder == null)
                    {
                        Console.Error.WriteLine($"Invalid line?: {line}");
                        if (Debugger.IsAttached)
                            Debugger.Break();
                        continue;
                    }

                    Console.Error.WriteLine($"Unrecognised line: {line}");
                    if (Debugger.IsAttached)
                        Debugger.Break();
                }
            }

            return null;
        }

        private static BookMark ParseBookMark(string line, BookMarkFolder containingFolder)
        {
            var (innerText, attributes) = GetNode($"{line}</DT>", "DT", "A");
            var link = attributes.TryGetValue("HREF", out var hrefStr) ? hrefStr : null;
            var icon = attributes.TryGetValue("ICON", out var iconStr) ? iconStr : null;
            var addDate = attributes.TryGetValue("ADD_DATE", out var unixDateStr) ? ParseUnixDate(unixDateStr) : ParseUnixDate("0");
            return new BookMark(innerText, icon, link, containingFolder.Depth + 1, addDate, containingFolder);
        }

        private static DateTime ParseUnixDate(string unixDate)
        {
            try
            {
                var l = long.Parse(unixDate);
                return DateTime.UnixEpoch.AddSeconds(l);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"While parsing unix time from '{unixDate}': {exception.Message}");
                if (Debugger.IsAttached)
                    Debugger.Break();
                return DateTime.UnixEpoch;
            }
        }

        private static BookMarkFolder ParseInitialFolder(string line)
        {
            var title = GetNode(line, "H1").InnerText;
            return new BookMarkFolder(title, 0, DateTime.MinValue, DateTime.MinValue, null);
        }

        private static BookMarkFolder ParseFolder(string line, BookMarkFolder containingFolder)
        {
            var (innerText, attributes) = GetNode($"{line}</DT>", "DT", "H3");
            var createdDate = attributes.TryGetValue("ADD_DATE", out var unixDateStr1) ? ParseUnixDate(unixDateStr1) : ParseUnixDate("0");
            var lastModifiedDate = attributes.TryGetValue("LAST_MODIFIED", out var unixDateStr2) ? ParseUnixDate(unixDateStr2) : ParseUnixDate("0");
            var personalToolbarFolder = attributes.TryGetValue("PERSONAL_TOOLBAR_FOLDER", out var personalToolbarFolderStr) && bool.Parse(personalToolbarFolderStr);
            return new BookMarkFolder(innerText, containingFolder.Depth + 1, createdDate, lastModifiedDate, containingFolder, personalToolbarFolder);
        }

        private static (string InnerText, IDictionary<string, string> Attributes) GetNode(string line, string firstElement, params string[] otherElements)
        {
            var elements = otherElements.ToList();
            elements.Insert(0, firstElement);

            var lastElement = elements.Last();
            foreach (var elem in elements)
            {
                line = line.Trim();
                if (elem == lastElement)
                {
                    if (line.StartsWith($"<{elem}", ComparisonCulture) && line.EndsWith($"</{elem}>", ComparisonCulture))
                    {
                        line = line.Substring(elem.Length + 1, (line.Length - (2 * elem.Length)) - 4);
                        var innerText = string.Empty;
                        var keyStart = -1;
                        var keyEnd = -1;
                        var valueStart = -1;
                        var attributes = new Dictionary<string, string>();
                        for (var i = 0; i < line.Length; i++)
                        {
                            if /* we aren't in a value */ (valueStart < 0)
                            {
                                if (line[i] == '=')
                                {
                                    keyEnd = i;
                                    continue;
                                }

                                if (line[i] == '>')
                                {
                                    innerText = line.Substring(i + 1);
                                    break;
                                }

                                if (keyStart < 0)
                                {
                                    keyStart = i;
                                    continue;
                                }
                            }

                            if /* key hasn't ended yet or this character isn't a quote */ (keyEnd <= keyStart || line[i] != '"')
                                continue;

                            /* this is a quote so then */
                            if /* a value was started */ (valueStart > 0)
                            {
                                var key = line.Substring(keyStart, keyEnd - keyStart).Trim();
                                var value = line.Substring(valueStart, i - valueStart).Trim();
                                attributes.Add(key, value);
                                keyStart = -1;
                                keyEnd = -1;
                                valueStart = -1;
                            }
                            else /* a value was not started yet */
                            {
                                valueStart = i + 1; // start with the char after the "
                            }
                        }
                        return (innerText, attributes);
                    }
                }
                else
                {
                    var match = new Regex($"^<{elem}>(.*)</{elem}>$").Match(line);
                    if (!match.Success)
                        throw new Exception("Fallback parse failed!");
                    var innerText = match.Groups[1].Value;
                    line = innerText;
                }
            }

            throw new Exception("Fallback parse failed!");
        }

        private static BookMarkFolder RemoveShallowestDuplicates(this BookMarkFolder bookMarks)
        {
            var flattened = bookMarks.FlattenBookMarks().ToList();
            var bookMarksToRemove = flattened
                .OfType<BookMark>()
                .GroupBy(bm => bm.Link)
                .Where(grp => grp.Count() > 1)
                .Select(g => g.OrderBy(b => b.Depth).ToList())
                .SelectMany(g => g.Take(g.Count - 1));

            foreach (var nodeToRemove in bookMarksToRemove)
            {
                Console.WriteLine($"Removing BookMark '{nodeToRemove.Title}' from '{nodeToRemove.Parent.FullTitle}'");
                nodeToRemove.Parent.Children.Remove(nodeToRemove);
            }
            return bookMarks;
        }

        private static async Task<BookMarkFolder> MoveNoLongerFoundDomains(this BookMarkFolder bookMarks)
        {
            var flattened = bookMarks.FlattenBookMarks().ToList();
            var bookMarksToResolve = flattened
                .OfType<BookMark>()
                .Select(GetHostWithBookMark)
                .Where(h => h.Host != null)
                .ToArray();

            var locker = new object();
            var dict = new Dictionary<string, Task<bool>>();
            Parallel.For(0, bookMarksToResolve.Length, (i) =>
            {
                var (host, bookMark, _) = bookMarksToResolve[i];
                if (!dict.ContainsKey(host))
                {
                    lock (locker)
                    {
                        if (!dict.ContainsKey(host))
                        {
                            dict[host] = HostExists(host);
                        }
                    }
                }
                bookMarksToResolve[i] = (host, bookMark, dict[host]);
            });

            var resolvedBookMarks = await Task.WhenAll(bookMarksToResolve.Select(async x => (BookMark: x.BookMark, Invalid: !await x.Valid)));

            var bookMarksToRemove = resolvedBookMarks.Where(x => x.Invalid).Select(x => x.BookMark).ToList();

            if (!bookMarksToRemove.Any())
                return bookMarks;

            var bookMarkBar = bookMarks.Children.OfType<BookMarkFolder>().Single(bmf => bmf.FullTitle == "/Bookmarks/Bookmarks bar");
            var otherBookMarkFolder = new BookMarkFolder("Removed DNS", 2, DateTime.Now, DateTime.Now, bookMarkBar);
            bookMarkBar.Children.Add(otherBookMarkFolder);

            foreach (var bookMarkToRemove in bookMarksToRemove)
            {
                Console.WriteLine($"Moving BookMark '{bookMarkToRemove}' as cannot resolve '{bookMarkToRemove.Link}'");
                bookMarkToRemove.Parent.Children.Remove(bookMarkToRemove);
                var newChild = new BookMark($"{bookMarkToRemove.Title} - Originally from {bookMarkToRemove.CreatedDate:yyyy-MM-dd}", bookMarkToRemove.Icon, bookMarkToRemove.Link, otherBookMarkFolder.Depth+1, DateTime.Now, otherBookMarkFolder);
                otherBookMarkFolder.Children.Add(newChild);
            }

            return bookMarks;
        }

        private static async Task<bool> HostExists(string host)
        {
            try
            {
                var hostEntry = await Dns.GetHostEntryAsync(host);
                var result = hostEntry.Aliases.Any() || hostEntry.AddressList.Any();
                if(!result && Debugger.IsAttached)
                    Debugger.Break();
                return result;
            }
            catch (SocketException se) when (
                string.Equals(se.Message, "No such host is known", StringComparison.CurrentCultureIgnoreCase)
                || string.Equals(se.Message, "The requested name is valid, but no data of the requested type was found", StringComparison.CurrentCultureIgnoreCase)
                )
            {
                return false;
            }
            catch (SocketException se2)
            {
                if(Debugger.IsAttached)
                    Debugger.Break();
                throw;
            }
        }

        private static (string Host, BookMark BookMark, Task<bool> Valid) GetHostWithBookMark(BookMark bookMark)
        {
            if (!Uri.TryCreate(bookMark.Link, UriKind.Absolute, out var uri) || uri.IsFile || uri.HostNameType != UriHostNameType.Dns)
            {
                return (null, bookMark, Task.FromResult(true));
            }
            return (uri.Host.ToLowerInvariant(), bookMark, null);
        }

        private static BookMarkFolder RemoveEmptyFolders(this BookMarkFolder bookMarks)
        {
            var flattened = bookMarks.FlattenBookMarks().ToList();
            var bookMarkFoldersToRemove = flattened
                .OfType<BookMarkFolder>()
                .Where(f => !HasBookMarks(f))
                .ToList();

            foreach (var nodeToRemove in bookMarkFoldersToRemove)
            {
                Console.WriteLine($"Removing Folder '{nodeToRemove.FullTitle}'");
                nodeToRemove.Parent?.Children.Remove(nodeToRemove);
            }
            return bookMarks;
        }

        private static BookMarkFolder MergeFolders(this BookMarkFolder bookMarks)
        {
            var flattened = bookMarks.FlattenBookMarks().ToList();
            var bookMarkFoldersToMerge = flattened
                .OfType<BookMarkFolder>()
                .GroupBy(f => f.FullTitle)
                .Where(g => g.Count() > 1)
                .OrderByDescending(g => g.Key.Length)
                .ToList();

            foreach (var mergeTargets in bookMarkFoldersToMerge.Select(x => x.ToList()))
            {
                var survivor = mergeTargets.First();
                Console.WriteLine($"Merging Folders '{survivor.FullTitle}'");
                foreach (var mergeTarget in mergeTargets.Skip(1))
                {
                    survivor.Children.AddRange(mergeTarget.Children);
                    mergeTarget.Children.Clear();
                    mergeTarget.Parent.Children.Remove(mergeTarget);
                }
            }
            return bookMarks;
        }

        public static void SaveToFile(this BookMarkFolder bookMarkFolder, string filePath)
        {
            var fileBuilder = new StringBuilder();
            fileBuilder.AppendLine(docType);
            fileBuilder.AppendLine("<!-- This is an automatically generated file.\r\n" +
                                   "     It will be read and overwritten.\r\n" +
                                   "     DO NOT EDIT! -->");
            fileBuilder.AppendLine(metaType);
            fileBuilder.AppendLine($"<TITLE>{bookMarkFolder.Title}</TITLE>");
            fileBuilder.AppendLine($"<H1>{bookMarkFolder.Title}</H1>");
            RenderBookMarkFolder(fileBuilder, bookMarkFolder);
            File.WriteAllText(filePath, fileBuilder.ToString());
        }

        private static void RenderBookMarkFolder(StringBuilder fileBuilder, BookMarkFolder bookMarkFolder)
        {
            var indent = CreateIndent(bookMarkFolder);
            fileBuilder.AppendLine($"{indent}<DL><p>");
            foreach (var node in bookMarkFolder.Children)
            {
                switch (node)
                {
                    case BookMark bookMark:
                        fileBuilder.AppendLine(RenderBookMark(bookMark));
                        break;
                    case BookMarkFolder childBookMarkFolder:
                        fileBuilder.AppendLine(RenderBookMarkFolderHeading(childBookMarkFolder));
                        RenderBookMarkFolder(fileBuilder, childBookMarkFolder);
                        break;
                    default:
                        throw new Exception($"Unrecognised node type '{node?.GetType().Name}'");
                }
            }
            fileBuilder.AppendLine($"{indent}</DL><p>");
        }

        private static string RenderBookMarkFolderHeading(BookMarkFolder bookMarkFolder)
        {
            var indent = CreateIndent(bookMarkFolder);
            var personalToolbarFolder = bookMarkFolder.PersonalToolbarFolder ? $" PERSONAL_TOOLBAR_FOLDER=\"true\"" : null;
            return $"{indent}<DT><H3 ADD_DATE=\"{bookMarkFolder.CreatedDate.ToUnixSeconds()}\" LAST_MODIFIED=\"{bookMarkFolder.LastModifiedDate.ToUnixSeconds()}\"{personalToolbarFolder}>{bookMarkFolder.Title}</H3>";
        }

        private static string RenderBookMark(BookMark bookMark)
        {
            var indent = CreateIndent(bookMark);
            var iconAttribute = string.IsNullOrWhiteSpace(bookMark.Icon) ? null : $" ICON=\"{bookMark.Icon}\"";
            return $"{indent}<DT><A HREF=\"{bookMark.Link}\" ADD_DATE=\"{bookMark.CreatedDate.ToUnixSeconds()}\"{iconAttribute}>{bookMark.Title}</A>";
        }

        private static string CreateIndent(IBookMarkNode bookMarkFolder)
        {
            return new string(' ', bookMarkFolder.Depth * 4);
        }

        private static bool HasBookMarks(BookMarkFolder bookMarkFolder)
        {
            return bookMarkFolder.Children.OfType<BookMark>().Any() // it has bookmarks
                   || bookMarkFolder.Children.OfType<BookMarkFolder>().Any(HasBookMarks); // one of it's sub folders has bookmarks
        }

        private static IEnumerable<IBookMarkNode> FlattenBookMarks(this BookMarkFolder bookMark)
        {
            yield return bookMark;
            foreach (var childBookMark in bookMark.Children.SelectMany(x => x is BookMarkFolder y ? FlattenBookMarks(y) : new[] { x }))
            {
                yield return childBookMark;
            }
        }

        private static long ToUnixSeconds(this DateTime dateTime)
        {
            return (long)Math.Round(dateTime.Subtract(DateTime.UnixEpoch).TotalSeconds);
        }
    }
}
