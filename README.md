# UmaParser

A janky app for analyzing Team Trials scores.

## Prereqs
You will need to have a tool installed that can capture your Team Trials results,
such as Ayaliz's HorseACT: https://github.com/ayaliz/horseACT
(After setting it up, you will need to enable TT race saves in the settings)

If you have the global Steam client installed, the app will try to read up-to-date
names and skill data from the master mdb.  You can use the menu to browse for the mdb if
you want to use it from a different location.  If you do not have an mdb available, it
will fall back to hard coded data from the time of release.

## Usage
Drag and drop one or more .json files containing TT results onto the app.  Every time you drop
it will reset everything, so the best use is to drop a stack of races at once.

### Team Trials
The main use of this app is for analyzing Team Trials scores over a sufficient sample size to
get an idea of who might be underperforming and is a candidate for replacing.

For full analysis, drop multiple Team Trials files that all contain the same team members.
Any files with different teams will disable some of the analysis, but the Results tab will help
identify which files are different.

### Champions Meet, Room Match, and Practice Room
These files can also be dropped in (but not mixed with Team Trials).  They will show skill and
track data for all local player umas found in the files.  Duplicate veterans are differentiated
by their rating score.


### Results Tab
If you drop multiple Team Trials captures and they all contain the exact same roster, the
Results tab will show individual and total scores in a normalized order grid for easy copy-paste into your
favorite spreadsheet application.
(for example, @harubanana's spreadsheet which was the inspiration for this tool: https://docs.google.com/spreadsheets/d/18NIXEu4MCYM5yRaQwRx5fSQxP9oarDotrn3oQHf2K94/edit?gid=733213549#gid=733213549)

If any of the dropped files have different rosters, the Results tab will show the filenames and character
names will be shown instead, with any outliers (different umas, different running style, etc) highlighted.
The Team Analysis tab will not be available, and the Skills/Tracks tabs will be limited to showing
stats only for umas that are common to all of the files.  This allows you to examine larger
samples for individual umas who have been on the team for a while even if other umas have
changed.

### Team Analysis Tab
This tab is only active when you drop Team Trials files that all have the same team members.
It shows different statistics on performance of each uma, with the scores normalized (bonuses
removed) to make for more consistent comparison.

If two or more umas are using the same running style in the same team, this will be highlighted.

### Skills Tab
This tab provides detailed information for an uma's skill activations across multiple races.
This can be used to identify skills that are not proccing frequently enough, and which skills are
underperforming in terms of average points per race.

Select a specific uma from the dropdown.  The columns can be sorted.

### Tracks Tab
This tab provides detailed breakdowns of the uma's performance on individual tracks.  This can
be used to identify tracks that are causing problems, especially if your uma is running out
of stamina on the longer ones.
