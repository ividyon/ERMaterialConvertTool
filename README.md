[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)

# ERMaterialConvertTool
A quick tool for ELDEN RING modding to convert one FLVER (model file) material to another.

Also converts FLVERs from other games to ELDEN RING FLVERs I guess.

## Credits

* All SoulsFormatsNEXT contributors
* *Meowmaritus* - FLVER2 Material Info Banks
* *ivi* - Author

## License

This work is licensed under GPL v3. View the implications for your forks [here](https://www.tldrlegal.com/license/gnu-general-public-license-v3-gpl-3).

ERMaterialConvertTool is built using the following licensed works:
* [SoulsFormats](https://github.com/JKAnderson/SoulsFormats/tree/er) by JKAnderson (see [License](licenses/LICENSE-SoulsFormats.md))
* [PromptPlus](https://github.com/FRACerqueira/PromptPlus), Copyright 2021 @ Fernando Cerqueira (see [License](licenses/LICENSE-PromptPlus.md))

## Changelog

### 1.1.0.1

* Downgraded PromptPlus to regain filter functionality.
* Removed some superfluous prompts for decision.

### 1.1.0.0

Big overhaul for comfort and functionality.

* Changing the FLVER version, and editing FLVER contents, are now separate actions.
* It is now possible to perform multiple actions on the same file, or even different files, without quitting the program.
* Hardened the save/load process to avoid producing garbled files. Also lets you pick a separate source and destination file.
* It is now possible to edit materials one-by-one without touching the others.
* Added a bulk processing mode for automatically converting all materials which have matching shaders in ER.
* For material conversions where the target has less UVs (and other layout members) than source, you can now select which to discard.
* Added a warning, and a filter, for materials applied to skinned meshes, to avoid selecting incompatible ones.
* Updated to latest SoulsFormatsNEXT which brings some small fixes.

### 1.0.0.1

* Fixed an issue with material indices missing on pre-ELDEN RING FLVERs.

### 1.0.0.0

* Initial release.
