# UmaParser

A janky app for analyzing Team Trials scores.

## Prereqs
You will need to have a tool installed that can capture your Team Trials results, such as Ayaliz's HorseACT: https://github.com/ayaliz/horseACT
(HorseACT will save TT races but by default this is disabled, you will have to enable it after setup).

Other tools capable of saving TT responses in json or raw msgpack .bin formats should also work, but I haven't tested them.

The tool will try to read the game database from the default location to keep character and skill names up to date.  If you have the database in a different location, use the menu to browse for it.
If you don't have the game database at all, the app has hardcoded data as a fallback but may be out of date.

## Usage
Drag and drop one or more .json files containing TT results onto the app.  Every time you drop it will reset everything, so the best use is to drop a stack of races at once.

If all of the results use the same roster, it will display all individual scores for every character in a normalized order.  These cells can be copy-pasted into spreadsheets for analysis.
(for example, @harubanana's spreadsheet which was the inspiration for this tool: https://docs.google.com/spreadsheets/d/18NIXEu4MCYM5yRaQwRx5fSQxP9oarDotrn3oQHf2K94/edit?gid=733213549#gid=733213549

If any of the dropped files have different rosters, the filenames and character names will be shown instead, to make it easy to spot the outliers and separate them.

Besides the individual scores in table format, there are some built in analysis tools too:

The Analyze tab shows averaged normalized scores (opponent, support, and ace bonuses removed) to help see which characters are performing better or worse.

It will also show hints if you have any characters that might be better for the ace position, but take this with a grain of salt if you don't have a large sample size.

The Skills tab will show skill performance for individual characters.  You can sort this table by any of the columns.
This can identify skills that are underperforming, such as proccing much less frequently than expected.
