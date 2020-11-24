# BasicTARImplementation
A Basic implementation of TAR files in C#, including functionality to add, remove and extract files

<b>Overview</b><br/>
This project is a basic implementation of TAR files in C# including functionality to add, remove and extract files. It is not a complete implementation by any means, but it can handle the basic examples and is relatively simple, with all the core code being contained in a single file. There is also an additional TARInspector file which allows for exploration of TAR file within the Unity editor, including adding, removing and extracting files from a standard Unity Window.

<b>Usage</b><br/>
To use within Unity, under the Window menu select "Show TAR Example Window". Select the TAR, TGZ or TAR.GZ file you want to read. A window will pop up with the files, and you can extract/add/remove files as you please. The library does not support overwriting the original TAR file (to keep memory usage down it only reads the content of the files contained in the TAR Archive when modifications are made), so if you are adding or removing files you will need to chose a different file name. To overwrite, you could simply move this file over the original after modifications are complete.

This has been tested and is working on Unity 2019, but should work on older and newer versions.

To use outside Unity, the interface for the TAR.cs file is relatively straight forward - see TARInspector.cs for information.

<b>Suggested Improvements</b><br/>
For larger projects, it can be a bit slow to generate the TAR archive, and currently no feedback is provided. A cancelable progress bar window would be helpful here.
Add sanity checking so the user doesn't accidentally try and overwrite the original, or do anything else dangerous
More complete implementation of the TAR archive is possible

