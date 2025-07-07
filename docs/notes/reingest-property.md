# Reingest Property


The reingest poerty controls whether an asset should **always** be reingested, regardless of if the asset is tracked or not.  This has several permutations, which are as follows: 

if "reingest" = true
* If asset is in CanvasPaintings already AND for same manifest = create batch, no "manifest" no need to PATCH
* If asset is in CanvasPaintings already for a different manifest = create batch, no "manifest" AND PATCH
* If asset is not CanvasPaintings already AND in DLCS = create batch with "manifest" no need to PATCH
* If asset is not CanvasPaintings already AND NOT in DLCS = create batch with "manifest" no need to PATCH
       
if "reingest" = false
* If asset is in CanvasPaintings already AND for same manifest = no-op
* If asset is in CanvasPaintings already for a different manifest = PATCH
* If asset is not CanvasPaintings already AND in DLCS = PATCH
* If asset is not CanvasPaintings already AND NOT in DLCS = create batch with "manifest"