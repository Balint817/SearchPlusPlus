# Search++

An overly-complicated search engine mod for Muse Dash.

## Usage Guide

**First things first, I recommend saving a bunch of filters you wrote beforehand, and copy-pasting them into the game as needed, cause typing these each time is annoying**

All advanced searches made using this mod must be preceded by `search:`

**If you've ever written a single line of code (or used any \*\*\*tai site before lol), you can probably skip most of this guide**

**2 types of search terms:**
- `key`, such as `cinema`
- `key:value`, such as `title:"Shinsou Masui"` or `diff:11-12`
- some can be both

**Range syntax for difficulty filters:**
- `key:A`
- `key:A-B`
- `key:A+` or `key:A-`

**You can negate any condition by doing: `-key:value`**

**All tags, seperated by spaces, must pass in order for a song to pass the filter.**

**A list of all search terms:**
- `diff:range` => check for visible difficulty in the given range, supports `?`
- `bpm:range` => if bpm in json is in the given range, supports `?`
- `hidden` => if song has hidden
- `hidden:range` => check for hidden in the given range, supports `?`
- `touhou` => if there's a touhou hidden
- `cinema` => if there's a cinema
- `scene` => if the scene matches the filter, can be:
	- a 1 digit number
	- 2 character string, i.e. "01", "02", etc.
	- the name of the scene
- `design` => search for level designer
- `author` => search for song author
- `title` => search for song title
- `tag` => search for a specific search tag (CustomAlbum's search tags)
- `any` => searches `design`, `author`, `title`, `tag` to see if any matches
- `anyx`
	- uses custom search tags made by MNight4
	- also runs the same thing as `any`

Putting `?` in a range will search for values that cannot be converted to a number, such as when the difficulty of a song is `?`.

**Strings:**
- searching terms with spaces must be done in a string: `key:"value with spaces"`
- searching quotes requires you to prefix them with a backslash: `key:"a \"quotes\" b`
	- **from this point on, this will be referred to as "escaping a character"**
	- **this can only be done inside strings (quoted searches)**
- searching a backslash: `key:\`
- searching a backslash inside a string requires you to escape it: `key:"\\"`
- escaping any other character will make the backslash disappear:
	- `key:"\a"` is the same as `key:"a"`

**OR/AND operations**
- Seperating a tag by spaces as normal means that all tags must pass for a song to appear. This is an AND operation.
- You can seperate tags by `|` instead of spaces (`Alt Gr` + `W` on qwertz-hu)
- Doing this will make it so that only one of the connected tags have to pass in order for a song to pass. This is an OR operation.
- This way, you can make groups of tags connected with `|`, e.g. OR operations, seperated by spaces, e.g. AND operations.
- Example: `hidden:12|diff:12 bpm:100+`
- This will search for songs that either have a hidden rated 12 OR a visible difficulty rated 12, AND the song's bpm must also be above 100;


# And that's all he wrote.
