# HDD-Ping v2.0

A tool to "ping" hard drives to prevent spin-downs and head parking

--------

This application is a Windows tray-only application that reads a random sector from selected hard drives in a given interval.

**Requires admin privilege (to direct read disks).**

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
  * `3`: active-idle (green, waiting for a timer to elapse to write random file to selected drives)
 
--------

License: Creative Commons Attribution-NonCommercial 3.0 Unported (CC BY-NC 3.0)
