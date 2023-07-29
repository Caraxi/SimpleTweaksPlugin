# Changelog

## 1.8.9.0
***General Changes***
- Added an option to opt out of analytics
    - Note:
      - No analytics are currently being collected.
      - This is a preemptive opt out for the future.

***New Tweaks***
- **`Auto Focus Recipe Search`** - Automatically focus the recipe search when opening the crafting log.

- **`Custom Timestamp Format`** - Customize the timestamps displayed on chat messages.

- **`Housing Lottery Timer`** - Show the time remaining until the current lottery period ends in the timers window.

- **`Zoomed Chat Customization`** - Allows customization of the size and position of the zoomed chat view.


***Tweak Changes***
- **`Chat Name Colours`**
  - Added option to give all undefined characters the same colour.
  - Added per channel configuration for colouring sender name and/or names in messages.

- **`Custom Free Company Tags`**
  - Added support for full RGB colours.
  - Added an icon viewer for supported icons.

- **`Data Centre on Title Screen`** - Added option to show the selected service account.

- **`Hide quality bar while crafting NO-HQ item.`** - Show quality bar for expert recipes.

- **`Market Enhancements`** - Return of the Lazy Tax/Profitable highlighting

- **`Shield on HP Bar`** - Fixed tweak not working after relogging.

- **`Target Castbar Countdown`** - Add option to disable on primary target


## 1.8.8.2
***Tweak Changes***
- **`Expanded Currency Display`** - Fixed positioning of gil display moving when scale is anything other than 100%

## 1.8.8.1
***Tweak Changes***
- **`Chat Name Colours`** - Fixed Chat2 exploding with new colour system. Tweak will still not work in Chat2, but it will not explode.

- **`Expanded Currency Display`** - Attempting to avoid gil addon getting thrown around when layout changes.

## 1.8.8.0
***Tweak Changes***
- **`Chat Name Colours`**
  - Fixed colour display when in party.
  - Extended range of possible colours.

- **`Custom Time Formats`**
  - Fixed tooltip when hovering clocks
  - Returned 'click to change clock' feature from base game.

- **`Expanded Currency Display`**
  - Added option for adjustable spacing in horizontal layouts.
  - Added option to display in a grid.
  - Added option to set the position of a currency individually.
  - Added tooltips when mouse is over the currency icons.

- **`Hide Job Gauge`** - Fixed 'Show In Duty' option not working in some duties.

## 1.8.7.3
***Tweak Changes***
- **`Duty List Background`** - Prevent crash when using Aestetician.

- **`Track Gacha Items`** - Added 'Platinum Triad Card'


## 1.8.7.2
***Tweak Changes***
- **`Custom Free Company Tags`** - Removed 'Hide in Duty' option from Wanderer. This is now a vanilla game option.

- **`Disable Auto Chat Inputs`** - Fixed game crash in 6.4

- **`Estate List Command`** - Fixed tweak not working in 6.4

- **`Hide Job Gauge`** - Fixed 'Show while weapon is drawn' option not working.

- **`Improved Duty Finder Settings`** - Fixed tweak not working in 6.4


## 1.8.7.1
***General Changes***
- General fixes for 6.4

***Tweak Changes***
- **`Duty List Background`** - Improved tweak stability

- **`Hide Tooltips in Combat`** - Added support for crossbar hints.


## 1.8.7.0
***New Tweaks***
- **`Duty List Background`** - Adds a configurable background to the Duty List *(MidoriKami)*

- **`Hide Target Circle`** - Allow hiding the target circle while not in combat or dungeons. *(darkarchon)*


***Tweak Changes***
- **`Bait Command`** - Fixed tweak not enabling when starting the game.

- **`Custom Free Company Tags`** - Added option to display FC tags on a separate line to character name.

- **`Show Painting Preview`** - Fixed extra spacing being added above the preview image.

- **`Track Faded Orchestrion Rolls`** - Fixed tweak not functioning at all.


## 1.8.6.1
***Tweak Changes***
- **`Color Duty Roulette Names`** - Adds ability to select individual roulettes for recoloring.

- **`Hide Tooltips in Combat`** - Improved logic to attempt to reduce settings getting stuck in incorrect state.

- **`Tooltip Tweaks`** - Yet another attempt at fixing crashes.


## 1.8.6.0
***General Changes***
- General fixes for 6.38

***New Tweaks***
- **`April Fools 2023`** - Re-enable the April Fools 2023 Features


***Tweak Changes***
- **`Enhanced Loot Window`** - Removed Window Lock Feature, 'Lock Window Position' tweak has returned.

- **`Screenshot Improvements`** - Added experimental option to use ReShade for screenshots.


## 1.8.5.3
- Removed April Fools joke due to potential crash.

## 1.8.5.2
- Made April Fools Joke Stupid

## 1.8.5.1
***Tweak Changes***
- **`Character Window Job Switcher`** - Fixed tweak not working on DoH without desynthesis unlocked.

- **`Screenshot Improvements`** - Renamed from 'High Resolution Screenshots' to 'Screenshot Improvements'

- **`Tooltip Tweaks`** - Added additional protections to attempt to reduce crashing. Please report any crashes you believe may be related to tooltips.


## 1.8.5.0
***General Changes***
- Added command to open the config window to a specific tweak. (/tweaks find [id])

***New Tweaks***
- **`Color Duty Roulette Names`** - Colors Duty Roulette names to indicate their completion status *(MidoriKami)*

- **`Lock Window Positions`** - Allows locking the position of almost any UI window.


***Tweak Changes***
- **`Always Yes`** - Added an option to default cursor to the checkbox when one exists.

- **`Block Targeting Treasure Hunt Enemies`** - Fixed incorrect blocking targeting of Alexandrite Map targets.

- **`Screenshot Improvements`**
  - Added option to hide game UI for screenshots.
  - Added option to remove the FFXIV Copyright from screenshots.


## 1.8.4.0
***New Tweaks***
- **`Hide quality bar while crafting NO-HQ item.`** - Hides the quality bar in the Synthesis window while crafting an item that can not be HQ or Collectable.


***Tweak Changes***
- **`Expanded Currency Display`** - Added support for Collectibles

- **`Fade Unavailable Actions`**
  - Tweak now only applies to combat actions
  - Properly resets hotbar state on unload/disable

- **`Improved Duty Finder Settings`** - Fixed UI displaying on wrong monitor in specific circumstances. *(Aireil)*

- **`Set Option Command`**
  - Fixed issues when using gamepad mode
  - Re-added accidentally remove gamepad mode option
  - Added 'LimitMouseToGameWindow' and 'CharacterDisplayLimit'
  - Fixed 'DisplayNameSize' using incorrect values

- **`Simplified Equipment Job Display`** - Fixed tweak for Japanese clients.


## 1.8.3.2
***New Tweaks***
- **`Echo Story Selection`** - When given multiple choices during quests, print the selected option to chat. *(MidoriKami)*


***Tweak Changes***
- **`Fade Unavailable Actions`**
  - Tweak now only applies to the icon image itself and not the entire button
  - Add option to apply transparency to the slot frame of the icon
  - Add option to apply to sync'd skills only

- **`Set Option Command`** - Improved reliability through patches


## 1.8.3.1
***New Tweaks***
- **`Fade Unavailable Actions`** - Instead of darkening icons, makes them transparent when unavailable *(MidoriKami)*


***Tweak Changes***
- **`Expanded Currency Display`** - Use configured format culture for number display, should fix French issue

- **`Target Castbar Countdown`** - Add TopRight option for displaying countdown


## 1.8.3.0
***General Changes***
- Added a changelog
- Fixed graphical issue when resizing windows on clear blue theme.

***New Tweaks***
- **`Echo Party Finder`** - Prints Party Finder description to chat upon joining a group. *(MidoriKami)*

- **`Expanded Currency Display`** - Allows you to display extra currencies. *(MidoriKami)*

- **`Extended Macro Icons`** - Allow using specific Icon IDs when using '/macroicon # id' inside of a macro.

- **`Hide Unwanted Banners`** - Hide information banners such as 'Venture Complete', or 'Levequest Accepted' *(MidoriKami)*

- **`Improved Duty Finder Settings`** - Turn the duty finder settings into buttons. *(Aireil)*

- **`Improved Interruptable Castbars`** - Displays an icon next to interruptable castbars *(MidoriKami)*

- **`Keyboard Gaming Mode`** - Block Alt-Tab and other keys to keep you in the game. *(KazWolfe)*

- **`Simplified Equipment Job Display`** - Hides classes from equipment tooltips when their jobs are unlocked.

- **`SystemConfig in Group Pose`** - Allows the use of the /systemconfig command while in gpose.

- **`Target Castbar Countdown`** - Displays time remaining on targets ability cast. *(MidoriKami)*

- **`Track Gacha Items`** - Adds the collectable checkmark to gacha items, such as Triple Triad card packs, when all potential items have been obtained.


***Tweak Changes***
- **`Enhanced Loot Window`**
  - Rebuilt tweak to use images
  - Fixed tweak not checking armory and equipped items
  - Added 'Lock Loot Window' feature

- **`Fix '/target' command`** - Fixed tweak not working in french. *(Aireil)*

- **`Screenshot Improvements`** - Added option to hide dalamud UI for screenshot.


## 1.8.2.1
***New Tweaks***
- **`Enhanced Loot Window`** - Marks unobtainable and already unlocked items in the loot window. *(MidoriKami)*

- **`Keep Windows Open`** - Prevents certain windows from hiding under specific circumstances.


***Tweak Changes***
- **`Improved Crafting Log`** - Fixed a potential crash in specific circumstances.


## 1.8.2.0
***General Changes***
- Now using the Dalamud Window system.
  ESC will now close Simple Tweaks windows.

***New Tweaks***
- **`Dismiss Minion Command`** - Adds a command to dismiss your current minion. /minionaway

- **`House Lights Command`** - Adds a command to control lighting in your own housing areas.

- **`Limit Break Adjustments`** - Simple customization of the limit break bars

- **`Screenshot Improvements`** - Allows taking higher resolution screenshots, Hiding Dalamud & Game UIs and removing the copyright notice from screenshots. *(NotNite)*

- **`Sticky Chat`** - Sets chat channel when you use temporary chat messages. *(MidoriKami)*


***Tweak Changes***
- **`Time Until GP Max`** - Added an option to display time in Eorzean Hours *(peterberbec)*


## 1.8.1.2
***Tweak Changes***
- **`Parameter Bar Adjustments`** - Fixed positioning of HP bar.


## 1.8.1.1
***Tweak Changes***
- **`Adjust Equipment Positions`** - Fixed widget display when using standard UI quality.

- **`Estate List Command`** - Now allows partial matching of friend names.

- **`Improved Crafting Action Tooltips`** - Fixed tweak not disabling correctly.

- **`Parameter Bar Adjustments`** - Added option to center HP bar when MP bar is hidden.

- **`Target HP`** - Added option to align text to the left.


