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

If any of the dropped files have different rosters, the filenames and character names will be shown, to make it easy to spot the outliers and separate them.
Separate your files to all have the same roster and drop those in to get the analysis.

### Results Tab
This simply shows your team in standard roster order and what they scored in each race.  These cells can be directly copy/pasted into other spreadsheets if you want.

### Analysis Tab
This compares your umas against eachother to see who the high and low performers are, and their consistency.  The scores shown here are normalized to remove Ace, Streak, Opponent, and Support bonuses so everyone is on an even level (as much as possible).

It will also show how the trimmed average of your non-aces compare to the aces, to see if you should consider swapping ace roles.

It will also highlight when you have 2 or more umas running the same style, reducing your chances for positioning bonuses.

### Skills Tab
This shows a breakdown of all skills for a selected uma and what their actual activation rates are, including your actual expected points per race.

This can show you skills that are not proccing very often, which is especially important for ults.

### Tracks Tab
This tab breaks down the selected uma's performance on every different track they ran on.  This is especially useful for seeing if you are running out of stamina on the longest races in your category.
