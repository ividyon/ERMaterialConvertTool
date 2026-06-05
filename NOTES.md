Big overhaul for comfort and functionality.

* Changing the FLVER version, and editing FLVER contents, are now separate actions.
* It is now possible to perform multiple actions on the same file, or even different files, without quitting the program.
* Hardened the save/load process to avoid producing garbled files. Also lets you pick a separate source and destination file.
* It is now possible to edit materials one-by-one without touching the others.
* Added a bulk processing mode for automatically converting all materials which have matching shaders in ER.
* For material conversions where the target has less UVs (and other layout members) than source, you can now select which to discard.
* Added a warning, and a filter, for materials applied to skinned meshes, to avoid selecting incompatible ones.
* 
* Updated to latest SoulsFormatsNEXT which brings some small fixes.