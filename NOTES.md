* Fixed a critical issue with batch processing where each FLVER would only process a single material matching a certain shader, not all of them.
* Fixed a critical issue with batch processing where all meshes in all FLVERs would be processed for each matching buffer layout group, not just relevant meshes in relevant FLVERs.
* Dramatically sped up batch processing.
* Batch converting materials to ER shaders will now state which materials could not be converted in which FLVERs.
* Converting the same shader will no longer lose FLVER texture properties like file name overrides.
* Cleaned up the batch processing options to be only "Select files" and "Select all in folder + subfolders".
* Added the "List used MATBINs" operation to processing a single FLVER.
* Dramatically sped up batch processing and fixed issues where FLVERs and meshes were needlessly processed several times, which led to massive slowdown and also crashes.