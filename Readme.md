This is a .net program that creates text file descriptions of what is inside diablo 2 save files.

The purpose is to make it easier to search for specific items across all your saves/mules.

It creates 2 files per d2s file.  One is a .txt file designed to be nice for humans to read.  The other is a json file to make it easier for computers to search.


How to install/use.

Download and install the .net 10.0 or higher sdk for your computer.
    https://dotnet.microsoft.com/en-us/download/dotnet/10.0

Clone/download this repo
    git clone THIS_REPO_URL
    cd d2sitems

Install the D2SSharp Library
    dotnet add package D2SSharp
    (I am not sure if you need to do this, or if it will happen automatically)

We need some game files to interpret the saves.  You are going to need to extract the files.
Use the D2RExtractor tool at https://github.com/levinium/D2RExtractor

copy the game data excel folder into d2sitems/game_files/default/

You should also be able to put files from a mod here.

To run on a single d2s file, from inside the d2sitems directory, run
    dotnet run charname.d2s

To run on all of your saved games:
    dotnet run "c:\Users\YourUsername\Saved Games\Diablo II Resurrected"

You should be able to run this on your entire directory periodically - all the files will update to the current contents.

Then you can search the files for what you are looking for.
    cd "c:\Users\YourUsername\Saved Games\Diablo II Resurrected"
    findstr "Enigma" *.txt
( Or get as fancy as you want in searching with tools like grep or json parsers )




This uses the D2SSharp project.
https://github.com/levinium/D2RExtractor

This is vibe coded using claude.
