# Vehicle reload art — 0.8.24

Two built-in image_gen.imagegen edits used the accepted vehicle_pickup_guns.png sheet as their sole reference. Exact prompts are in pistol-prompt.txt and rifle-prompt.txt; provenance.json records tool, source paths, SHA-256 hashes and frame order. Both original generated PNGs are retained here and copied unchanged into Unity ArtSource. The existing Unity importer removes the cyan background and packs two bounded sprites per sheet; it does not regenerate faces or vehicle details.

Top row: weapon withdrawn with magazine below the grip/well. Bottom row: magazine seating. Runtime uses short blends from aim into the first frame, between the two authored frames, and back toward aim. Ammunition is transferred only when the existing timer completes. These are two-pose arcade animations; detailed bolt/slide operation and a full articulated reload remain future work.

Source review accepted the consistent blue pickup, wheel positions, Stefanie driving, Fernando in the rear seat and separate magazine-handling silhouettes. Runtime screenshots and Web inspection are recorded in the release verification. No original personal gear photos, new Topaz upscales, third-party game art or paid assets were used.
