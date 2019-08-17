# ChromeBookmarkDedupeAndMerge
Tool to dedupe, and merge bookmarks from chrome

I've "hacked" this tool together quickly as I just wanted to use it.

It is designed to work on a html export of bookmarks from chrome. I also assume you're primarily using the Bookmarks bar though it should work for all.

## What does it do?

* It will attempt to remove the shallowest duplicate of any bookmarked links - leaving the ones nested deepest in a folder - assuming this means they're more organised (big assumption for sure).
* It then moves all bookmarks for domains which it cannot resolve dns entries for into a folder in the bookmark bar called 'Removed DNS' - it appends the original date the bookmark was created to the end of the title of the bookmark.
* It then merges folders - chrome's bookmark system allows you to have multiple folders of the same name in within any other folder. My bookmarks got in this state because at one time I had a sync tool which decided to duplicate all my bookmarks several times over and I didn't notice for quite some time.
* It then removes any empty bookmark folders.

In order to use the tool you will have to change the hardcoded path I've left in the code.
* First you export the bookmarks from chrome
* Then edit the path to point at your new export file and run the tool - all being well it will work and output a "modded" file next to your original file.
* You can then go into chrome and import the modded bookmark file. If everything looks ok you can then delete the original bookmark structure you have and drag all the imported bookmarks to the top level.

It's not ideal, but if you have a lot of duplicate bookmarks and or folders, or even just a lot of dead links, it might save you some time.
