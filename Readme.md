This is a .net program that creates json descriptions of what is inside diablo 2 save files.

The purpose is to make it easier to search for specific items across all your saves/mules.

For Sets, Runewords and Uniques, it calculates a "Perfection Score" - that rates how well the item does in it's stats that have a range.
It does not take the base defense of armor into account, but it does take enhanced defense into account.  It does not weight the value of diffent stats.  This score might not match what we humans value in items, but it should detect perfect/anti-perfect items and gives a very rough estimate on how good it is.


How to install/use.



Download and install the .net 10.0 or higher sdk for your computer.

- https://dotnet.microsoft.com/en-us/download/dotnet/10.0

Download and install Python on your computer

- https://www.python.org/downloads/windows/



Clone/download this repo

- git clone https://github.com/hombrent/d2sitems
- cd d2sitems

I do not think you actually need to install the D2SSharp Library or if it happens automatically for you, but if you do need to:

- dotnet add package D2SSharp

We need some game files to interpret the saves.  You are going to need to extract the files.
Use the D2RExtractor tool at https://github.com/levinium/D2RExtractor
By default, the tool will look in the default game location to find unpacked game files.

You can specify the location of an "excel" directory with the --excel parameter, to parse files for a mod.

To run on a single d2s file, from inside the d2sitems directory, run

- dotnet run charname.d2s

To run on all of your saved games:

- dotnet run "c:\\Users\\YourUsername\\Saved Games\\Diablo II Resurrected"

To run on all of your saved games in the default saved game location ( C:\Users\YourUsername\Saved Games\Diablo II Resurrected) :

- dotnet run 

To run on all files in an alternative location:

- dotnet run directory\path

You can run this on your entire directory periodically - it will overwrite the old files to represent the currrent contents of your characters



You can search for items with the find_items.py script.

- python find_item.py --help




This uses the D2SSharp project. Thanks to ResurrectedTrader.  https://github.com/levinium/D2RExtractor

This is vibe coded using claude.



This is to serve as note to self.  I don't yet know how to build a working standalone exe file.
    To build a new exe file that can be run directly, you can run:
    - dotnet publish -r win-x64 -c Release /p:PublishSingleFile=true --self-contained true
    Then copy bin/Release/net10.0/win-x64/d2sitems.exe to the main directory or wherever you want it.

