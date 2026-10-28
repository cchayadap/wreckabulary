# Preview depth independent source review

STATUS: COMPLETE — no blocking source finding; native matched rendering remains pending.

Reviewed the exact one-file diff, complete stage constructor/disposal and manifest. Manifest SHA256 `e916f4edcd4cea440be6c5a91dfdcbc45ee3a031e2f234fc36ac1bf2a7b017f6`; candidate SHA256 `9345dc60d4f3558c46f56945b114522d785bc51d75b9fe1bf4c6963a043dc0c6` both match.

The only functional changes are owned preview key soft shadows and fill intensity 0.7 to 0.3. Both lights belong to PreviewRenderUtility's verified preview scene; cleanup and failure cleanup retain Preview.Cleanup plus scene closure. The patch does not mutate RenderSettings.sun, pipeline assets, gameplay lights, materials, cameras, course data or persistent assets.

This is a safe bounded comparison candidate, not evidence of improved imagery. URP main-light selection and preview rendering determine whether soft shadows actually appear. Root must compare the same saved camera/seed and check contact depth, dark silhouettes and lost detail. Any subsequent thumbnail import must use fresh source fingerprints/receipts. No native or image generation ran in this review.
