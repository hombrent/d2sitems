This is a .net program that creates text file descriptions of what is inside diablo 2 save files.

The purpose is to make it easier to search for specific items across all your saves/mules.

It creates 2 files per d2s file.  One is a .txt file designed to be nice for humans to read.  The other is a json file to make it easier for computers to search.



How to install/use.



Download and install the .net 10.0 or higher sdk for your computer.

- https://dotnet.microsoft.com/en-us/download/dotnet/10.0



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

Then you can search the files for what you are looking for.

- cd "c:\\Users\\YourUsername\\Saved Games\\Diablo II Resurrected"
- findstr "Enigma" \*.txt

( Or you can get as fancy as you want in searching with tools like grep or json parsers )


To build a new exe file that can be run directly, you can run:

- dotnet publish -r win-x64 -c Release /p:PublishSingleFile=true --self-contained true



This uses the D2SSharp project. Thanks to ResurrectedTrader.  https://github.com/levinium/D2RExtractor

This is vibe coded using claude.

