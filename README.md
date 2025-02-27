# HDD-Ping v2.0

A tool to "ping" hard drives to prevent spin-downs and head parking

--------

This application is a Windows tray-only application that reads a random sector from selected hard drives in a given interval.

The only difference from the original version is the random sector read instead of writing and deleting a random file. **Direct disk read requires admin privilege.**

--------

Functions:

- Auto-discovery of drives on startup
- Stores interval and selected drives in a serialized file
- Re-enables settings of a found configuration file
- Auto-fixes the settings by removing selected drives that are not available upon start
- Includes three icons that change depending on the state
- Those states are
  * `1`: disabled (gray, no drive selected),
  * `2`: writing (red for 5 secs when timer elapses)
  * `3`: active-idle (green, waiting for a timer to elapse to read a random sector from selected drives)
 
--------

License: Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International (CC BY-NC-SA 4.0)
