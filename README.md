# UmaParser

Advanced performance analysis for uma trainers.  The main goal is to give better visibility into
detailed performance data for Team Trials, to help identify where  to best target your efforts
in improving your team.

Some of the features are also useful for CMs, Room matches, and Practice rooms.

## Prereqs
You will need to have a tool to collect json API captures of races to use this.
One example is [HorseACT](https://github.com/ayaliz/horseACT), but other tools may work as well
as long as they preserve the same json structure.

If you have the global Steam client installed, this app will try to read up-to-date
names and skill data from the master mdb at the default location or a custom location.
Otherwise it will use the embedded fallback master data, which may not be up-to-date.

## Usage
Drag and drop one or more .json files containing TT results onto the app.  Every time you drop
it will reset everything, so the best use is to drop a stack of races at once.

### Team Trials
For full analysis, drop multiple Team Trials files that all contain the same team members.
Any files with different teams will disable some of the analysis, but the Results tab will help
identify which files are different.

The header of each column in the Results tab will show the full filename for that column.

### Champions Meet, Room Match, and Practice Room
These files can also be dropped in (but not mixed with Team Trials).  They will show skill and
track data for all local player umas found in the files.  Duplicate veterans are differentiated
by their rating score.


### Results Tab
If you drop multiple Team Trials captures and they all contain the exact same roster, the
Results tab will show individual and total scores in a fixed roster-order grid for easy
copying into any spreadsheet.

If there are any differences in the rosters (different umas, changed running style, different team
assignment) then the Results tab will show the filenames in the header and character names in the
rows, with any outliers highlighted.  This allows you to still use the Results tab to identify
which files are different.  Uma moves within the same team (including ace swaps) are OK.

In both cases, the columns are sorted by file timestamps, and the full filenames are visible as
tooltips on the headers.

### Team Analysis Tab
This tab is for comparing the performance of all 15 umas in your Team Trials squad across
the sample set.  All scores on this tab are not including bonuses to make the comparisons
more consistent.

This tab is only enabled for Team Trials files when they all have the same roster.

All scores on this tab are normalized by removing support, opponent, and ace bonuses to make
comparisons consistent.  Each column header has a tooltip to explain what it is showing.

The "Ace Delta" column is comparing the umas within one team to their ace to see if you might
be better off swapping aces.  However this requires a decently large sample size to be meaningful,
so take the recommendations with a grain of salt.

### Skills Tab
This tab provides detailed information for an uma's skill activations across multiple races.
This can be used to identify skills that are not proccing frequently enough, which can be useful
for both Team Trials and Champions Meet.

Any uma in the dropped files can be selected from the dropdown, including non-TT files.

### Tracks Tab
This tab provides detailed breakdowns of the uma's performance on individual tracks.  For TT,
this requires a decent sample size to be useful because of the random track selection.
