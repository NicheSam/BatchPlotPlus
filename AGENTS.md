# Output behavior boundary

Read `docs/output-behavior-policy.md` before editing batch plotting or DWG splitting.

The user requires the output behavior to remain at the AutoCAD 1.4.4 plug-in baseline (Git `43db0bf`). Only GUI/UX changes are authorized by default. Do not change frame detection, selection, coordinates, ordering, filenames, copies, output contents or setting behavior without separate explicit user approval. Bug-fix framing does not waive this boundary.

Run `python tools/test_upgrade_contract.py` after relevant changes. Never regenerate `tools/output-baseline.json` merely to make a changed algorithm pass.
