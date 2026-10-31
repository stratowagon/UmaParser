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

### Performance Tab
Stacked base-score bars for the Team Trials umas in the drop. A consistent 15-uma roster
still works. A mixed set works too: every trained uma who appears in any loaded file gets a
row, and that row's average uses only the trials that uma actually ran. Replacing one or two
umas leaves the new and the old ones on the same list.

Each bar is that uma's average score with support, opponent, and ace bonuses removed, split
into finish place and win margin, white / gold / unique skills, strong start, and style lead
plus other extras. Under each bar, a thin line shows the P25–P75 range, with a median tick,
min/max dots, and a P90 marker. Rows start weakest-first. The sort box can order by average,
median, max, P75, or P90, ascending or descending, and that choice is remembered in
settings.json. Hover a row for the average, median, P90, and the segment split. Skill proc
rates stay on the Skills tab.

The name includes the uma's rating when the capture has one, for example `Oguri Cap [18,317]`.
That number is the rank score. HorseACT Team Trials files put it on each race horse as
`trained_chara_data.rankScore`. A file can also carry `rank_score` on the race horse or on
`user_trained_chara_array_copy`. The average itself stays off the row; hover still shows it.

A row is one trained uma, one running style, and one distance. The same uma in two styles,
or in two distances, is two rows. Each row's race count and stats use only the trials that
match that style and distance. Moving between ace and support does not split the row when
the id, style, and distance stay the same; the ace bonus is already removed from the base
score. The line under the name ends with how many of those trials there were
(`Sprint · Late · 12 races`). Two different builds of the same character stay separate as
well.

A short sample is marked on that race count. With the defaults, fewer than 10 races is red,
10 through 19 are yellow, and 20 or more has no mark. Both cutoffs are editable under
Options → Performance and are stored in settings.json. A missing setting uses 20 and 10.

A bar at the left of the row means that uma, in that running style and distance, is on the
current lineup: the roster in the newest file. Replaced umas, and the same uma's other styles
or distances, stay visible without the bar. The dashed line is the mean of those current
rows' average base scores, so a tryout is compared with the team in the newest file. When
every file has the same roster, styles, and distances, that line is the average of all 15.

### Skills Tab
This tab provides detailed information for an uma's skill activations across multiple races.
This can be used to identify skills that are not proccing frequently enough, which can be useful
for both Team Trials and Champions Meet.

Any uma in the dropped files can be selected from the dropdown, including non-TT files.

### Tracks Tab
This tab provides detailed breakdowns of the uma's performance on individual tracks.  For TT,
this requires a decent sample size to be useful because of the random track selection.
