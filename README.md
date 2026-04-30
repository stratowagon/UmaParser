# UmaParser

A janky app for viewing Team Trials scores in an easy format to paste into spreadsheets.

Use: Drag and drop one or more team trials results json files (from HorseACT).

If all of the results use the same roster, it will display all individual scores for every character in a normalized order (Sprint Ace first).  These cells can be copy-pasted into spreadsheets for analysis.

There is also a total row for the total score for each result file.

If any of the dropped files have a different roster, the filenames and character names will be shown instead, to make it easy to spot the differences.

You can also drop .bin files from CarrotJuicer or other tools that capture raw msgpack responses.  These will be converted and saved as .json versions in the same folder.

Current known issues:
* Character names are hardcoded from a db dump, so they need frequent updating until I can bolt in an actual SQLite connector to get them from the live game data.
* The parser may not differentiate between different versions of the same character (like regular Oguri vs. Xmas Oguri).
* Totals require deriving the opponent bonus from other bonuses, so may be a tiny bit off due to rounding errors.
* Code is messy because I was reverse engineering on the fly.
