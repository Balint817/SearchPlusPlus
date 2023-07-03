# Search++

An overly-complicated search engine mod for Muse Dash.

## Usage Guide

**First things first, I recommend saving a bunch of filters you wrote beforehand, and copy-pasting them into the game as needed, cause typing these each time is annoying.\
Alternatively, check out the `Custom tags` section.**

All advanced searches made using this mod must be preceded by a start text, which is changeable in the config.
The default value is `search:`.

**If you've ever written a single line of code (or used any \*\*\*tai site before lol), you can probably skip most of this guide**

**Search term examples:**
- `key`, such as `cinema`
- `key:value`, such as `title:"Shinsou Masui"` or `diff:11-12`

**Range syntax for difficulty filters:**
- `key:A`
- `key:A-B`
- `key:A+` or `key:A-`

Some tags may receive multiple ranges as input. (`key:"A-B C-D"`)

**`string` refers to text input**

**You can negate any condition by doing: `-key:value`**

**All tags, seperated by spaces, must pass in order for a song to pass the filter.**

### **A list of all search terms:**
- `diff:range`
	- Check for a visible difficulty in the given range
	- Supports `?` => in case the difficulty isn't a number
- `callback:range`
	- Check for callback difficulties in the given range
	- (Callback difficulty does not appear visually in-game)
	- (It is always a number)
- `hidden`
	- Check if the song has hidden
- `hidden:range`
	- Check for hidden in the given range
	- Supports `?` => in case the difficulty isn't a number
- `touhou`
	- Check if there's a touhou hidden
- `touhou:range`
	- Check for touhou hidden in the given range
	- Supports `?` => in case the difficulty isn't a number
- `bpm:range`
	- Check if bpm in json is in the given range
	- Supports `?` => in case the bpm isn't given as a number
- `cinema`
	- Check if there's a cinema
- `custom`
	- The song is a custom song
- `ranked`
	- The song is a ranked custom song
	- Has the alias `headquarters`
- `history`
	- The song was recently played (history tab)
- `new`
	- A recently added default song (new tab)
- `recent`
	- A custom that was recently added
	- The time can be changed in the config
- `recent:value`
	- returns the `x` most recent songs (given by value)
- `recent:range`
	- (where `range` is `A-B`)
	- returns the most recent songs starting from the `A`-th most recent, ending with the `B-th` recent  (given by value)
- `old:value`
	- inverse of `recent:value`
- `old:range`
	- inverse of `recent:range`
- `scene:string`
	- Checks if the scene matches the filter, can be:
		- a 1 digit number
		- 2 character string, i.e. "01", "02", etc.
		- The name of the scene, i.e. "candyland", "retrocity"
- `design:string`
	- Search for level designer
	- Has the alias `designer:string`
- `author:string`
	- Search for song author
- `title:string`
	- Search for song title
- `tag:string`
	- Search for a specific search tag (CustomAlbum's and built-in search tags)
- `any:string`
	- Short for `design:x|author:x|title:x|tag:x` (See the `AND/OR operations` section)
- `anyx:string`
	- Uses custom search tags made by MNight4
	- Also runs the same thing as `any`
- `album:string`
	- Returns songs within the given pack/album
- `unplayed`
	- No difficulty of a song has any clears
- `unplayed:range`
	- Given difficulties of a song have no clears (1-5)
	- Supports `?` => selects highest difficulty
- `fc`
	- All difficulties of a song are FC-d (full combo)
- `fc:range`
	- Given difficulties are FC-d (full combo)
	- Supports `?` => selects highest difficulty
- `acc:range`
	- All difficulties of a song have an accuracy within the given range
- `acc:"range1 range2"`
	- All difficulties of a song that are within `range2` that have an accuracy within `range1`
	- `range2` supports `?` => selects highest difficulty
- `random`
	- 1/2 chance to pass.
- `random:value`
	- 1/value chance to pass, where value >= 1
- `sort:string`
	- Applies sorting to the search results.
	- Built-in values are: "name", "uid", "diff", "acc"
- `reverse_sort:string`
	- Applies reversed sorting in comparison to `sort:string`
	- Aliases: `reverse`, `reversesort`
- `eval:string`
    - See the `Evaluate Tag` section
- `def:string`
	- See the `Custom tags` section.

**Strings:**
- Searching terms with spaces must be done in a string (quotes): `key:"value with spaces"`
- Searching quotes requires you to prefix them with a backslash: `key:"a \"quotes\" b`
	- **From this point on, this will be referred to as "escaping"**
	- **This can only be done inside strings (quoted searches)**
- Searching a backslash: `key:\`
- Searching a backslash inside a string requires you to escape it: `key:"\\"`
- Escaping any other character will make the backslash disappear:
	- `key:"\a"` is the same as `key:"a"`

**AND/OR operations**
- Seperating a tag by spaces as normal means that all tags must pass for a song to appear. This is an AND operation.
- You can seperate tags by `|` instead of spaces (`Alt Gr` + `W` on qwertz-hu)
- Doing this will make it so that only one of the connected tags have to pass in order for a song to pass. This is an OR operation.
- This way, you can make groups of tags connected with `|`, e.g. OR operations, seperated by spaces, e.g. AND operations.
- Example: `hidden:12|diff:12 bpm:200-`
- This will search for songs that either have a hidden rated 12 OR a visible difficulty rated 12, AND the song's bpm must also be below 200;

### **Evaluate Tag**
- `eval:string`
- The value inside will be evaluated as a seperate search
- The tag can be nested (e.g. `eval:"eval:tag1"`)
- Because strings are ranked highest (see `Parsing info`), the `eval` allows for further grouping of tags.
    - This essentially results in the `eval` tag being similar to parentheses.
    - Example: `eval:"tag1 tag2"|eval:"tag3 tag4"`

### **Parsing info**
- The characters `:`, `"`, `|` and spaces must be enclosed in a string to be searched.
- The previous characters, along with `\`, cannot be part of a key, (such as a custom tag)
- Keys cannot be strings.
- Order of operations:
    - Spaces bind the least tightly (executed last)
    -`|` binds more tightly than spaces, less tightly than `:`
    - `:` can only appear after a key, and `"` can only appear after `:`.
    - `:` and strings (`""`) bind more tightly than `|`.

### **Custom tags**
- `def:string`
- Similar to `eval` in some places
- You can pre-define common searches in the config file of this mod (`SearchPlusPlus.cfg`)
- This tag allows you to call upon these saved search terms.
- This tag cannot appear inside an `eval` statement, nor can it appear inside another tag definition.
- Reason:
    - tag1 = "def:tag2"
    - tag2 = "def:tag1"
    - Search: `def:"tag1"`
    - The tags referencing each other would turn into an infinite loop
    - This not only makes it impossible to evaluate, it will also freeze the game.
- This behavior can be toggled off, but make sure that you know what you're doing.

### **Aliases**
- Similar to legacy custom tags
- Unlike custom tags, aliases can be used the same way as any other tag.

### **Cascaded custom tags**
- `def:"mytag:value"`
- The value after `:` will override values assigned to tags inside the custom tags.
- For the `eval` tag, the values of the terms inside it are overwritten instead of the entire input.
- For nested `def` (if enabled):
	- If an empty value is given (`def:"mytag:"`), it'll inherit the value.
	- If a non-empty value (`def:"mytag:value"`) or no value (`def:"mytag"`) is given, the value is unaffected.

### **Custom tag parameters**
[This is a removed feature]

# And that's all he wrote.
