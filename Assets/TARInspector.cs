// Basic TAR Archive Implementation Inspector Usage Example
// --------------------------------------------------------
// Adrian Clark
// adrian.clark@canterbury.ac.nz
// --------------------------------------------------------
// First Release Nov 2020


using System;
using System.IO;

using System.Collections.Generic;

using UnityEditor;

using UnityEngine;
using UnityEngine.UIElements;

public class TARExampleWindow : EditorWindow
{
    // Keep track of the current TAR archive filename
    static string tarFileName;

    // Keep track of the current TAR Archive and its File Entries
    TAR tarArchive;
    List<TAR.TARMetaData> tarEntries;

    [MenuItem("Window/Show TAR Example Window")]
    public static void OpenDemoManual()
    {
        // Show open file panel for TAR Archive to open
        string filename = EditorUtility.OpenFilePanel("Select TAR Archive to Open", "", "");

        // If the file exists
        if (!string.IsNullOrEmpty(filename) && File.Exists(filename))
        {
            // Store the filename, enable the window (sometimes this doesn't
            // get called automatically) and show the window
            tarFileName = filename;
            GetWindow<TARExampleWindow>().OnEnable();
            GetWindow<TARExampleWindow>().Show();
        }
    }

    public void OnEnable()
    {
        // Check that we have a valid TAR Archive filename
        if (string.IsNullOrEmpty(tarFileName) || !File.Exists(tarFileName))
            return;

        // Check if the extension is .tgz or .gz
        if (Path.GetExtension(tarFileName).Equals(".tgz", StringComparison.InvariantCultureIgnoreCase) ||
            Path.GetExtension(tarFileName).Equals(".gz", StringComparison.InvariantCultureIgnoreCase))
            // If so, open it as a compressed TGZ file
            tarArchive = TAR.LoadTGZFile(tarFileName);
        else
            // Otherwise open it as an uncompressed TAR file
            tarArchive = TAR.LoadTARFile(tarFileName);

        //Create a list to store entries
        tarEntries = new List<TAR.TARMetaData>();

        //Loop through all the entries
        foreach (TAR.TARMetaData entry in tarArchive.Entries)
        {
            // And if it's a normal file
            if (entry.GetTypeFlags() == TAR.TARMetaData.TypeFlags.NormalFile)
            {
                //Add it to the list of entries we're tracking
                tarEntries.Add(entry);
            }
        }

        // The "makeItem" function will be called as needed
        // when the ListView needs more items to render
        Func<VisualElement> makeItem = () => {

            // We'll use a reverse row flex direction, as we want to
            // Keep the buttons a standard size, but have the label flexgrow
            VisualElement ve = new VisualElement();
            ve.style.flexDirection = FlexDirection.RowReverse;
            ve.style.flexShrink = 0;

            // Create the Label
            Label label = new Label("FILENAME");
            label.style.flexGrow = 1f;
            label.style.flexShrink = 0f;
            label.style.flexBasis = 0f;

            // Create the Extract button
            Button extract_Button = new Button(() => { Extract(label.text); } );
            extract_Button.text = "EXTRACT";
            extract_Button.style.width = 100;

            // Create the Remove Button
            Button remove_Button = new Button(() => { Remove(label.text); });
            remove_Button.text = "REMOVE";
            remove_Button.style.width = 100;

            // Add the items in reverse order
            ve.Add(remove_Button);
            ve.Add(extract_Button);
            ve.Add(label);
            return ve;
        };

        // As the user scrolls through the list, the ListView object
        // will recycle elements created by the "makeItem"
        // and invoke the "bindItem" callback to associate
        // the element with the matching data item (specified as an index in the list)

        // In this case, we get the last item, as the label is the last thing we add
        Action<VisualElement, int> bindItem = (e, i) => { (new List<VisualElement>(e.Children())[2] as Label).text = tarEntries[i].fileName; };

        // Provide the list view with an explict height for every row
        // so it can calculate how many items to actually display
        const int itemHeight = 15;

        // Create the list view
        var listView = new ListView(tarEntries, itemHeight, makeItem, bindItem);
        listView.selectionType = SelectionType.None;
        listView.style.flexGrow = 0.95f;

        // Remove any remaining items in the window
        rootVisualElement.Clear();

        // Create our button to add Files
        Button add_Button = new Button(() => { AddFile(); });
        add_Button.text = "ADD FILE";

        // Add our title, list view, and add button
        rootVisualElement.Add(new Label(tarFileName + " Contents"));
        rootVisualElement.Add(listView);
        rootVisualElement.Add(add_Button);
    }


    // This function extracts a file from our TAR Archive
    void Extract(string tarEntryFileName)
    {
        // Pop up a save file dialog for where to extract the file to
        string filename = EditorUtility.SaveFilePanel("Select Where to Extract File", "", tarEntryFileName, "");

        //If we have a valid filename, extract it
        if (!string.IsNullOrEmpty(filename)) 
            tarArchive.ExtractFile(tarEntryFileName, filename);
    }

    // This function removes a file from our TAR Archive
    // We cannot do in place overwrites, so we will request the name of a new
    // Archive for where to save it to
    void Remove(string tarEntryFileName)
    {
        // Get a temporary filename by appending "_modified" to the existing filename
        string tmpFilename = Path.GetFileNameWithoutExtension(tarFileName) + "_modified" + Path.GetExtension(tarFileName);

        // Pop up a save file dialog for where to save our modified TAR to
        string filename = EditorUtility.SaveFilePanel("Select Name of Output Archive", "", tmpFilename, "");

        // If we have a valid filename
        if (!string.IsNullOrEmpty(filename)) {
            // Check if the extension is .tgz or .gz, and if so compress the new file
            bool doCompress = false;
            if (Path.GetExtension(filename).Equals(".tgz", StringComparison.InvariantCultureIgnoreCase) ||
                Path.GetExtension(filename).Equals(".gz", StringComparison.InvariantCultureIgnoreCase))
                doCompress = true;

            // Remove the file from this archive and save a copy
            tarArchive.RemoveFile(tarEntryFileName, filename, doCompress);
        }
    }

    // This function adds a file to our TAR Archive
    // We cannot do in place overwrites, so we will request the name of a new
    // Archive for where to save it to
    void AddFile()
    {
        // Pop up an open file dialog for what file to add to the archive
        string filenameIn = EditorUtility.OpenFilePanel("Select File to Add to Archive", "", "");

        // If we're adding a valid file
        if (!string.IsNullOrEmpty(filenameIn) && File.Exists(filenameIn))
        {

            // Get a temporary filename by appending "_modified" to the existing filename
            string tmpFilename = Path.GetFileNameWithoutExtension(tarFileName) + "_modified" + Path.GetExtension(tarFileName);

            // Pop up a save file dialog for where to save our modified TAR to
            string filenameOut = EditorUtility.SaveFilePanel("Select Name of Output Archive", "", tmpFilename, "");

            // If we have a valid filename to save to 
            if (!string.IsNullOrEmpty(filenameOut))
            {
                // Check if the extension is .tgz or .gz, and if so compress the new file
                bool doCompress = false;
                if (Path.GetExtension(filenameOut).Equals(".tgz", StringComparison.InvariantCultureIgnoreCase) ||
                    Path.GetExtension(filenameOut).Equals(".gz", StringComparison.InvariantCultureIgnoreCase))
                    doCompress = true;

                // Remove the file from this archive and save a copy
                tarArchive.AddFile(filenameIn, filenameOut, doCompress);
            }
        }
    }
}
