# Quest experiment data export

The Quest app saves one sensor row per second after tracking calibration. In VR, a participant opens the report with a short Y press, selects RATE, and presses 1–5 after a timed work block. That action labels the pending rows and appends them to `manufacturing_trials_v3.csv`. Unrated rows are saved separately as `manufacturing_unlabeled_v3.csv` when the app pauses or closes. Height and arm reach are stored in `worker_profile_v1.json`; each sensor row also copies the current profile ID and values.

Connect and authorize the Quest, then run `Tools/Pull-QuestMLData.ps1`. Each pull is preserved under `Recordings/Quest/FactoryMLSnapshots/<timestamp>/FactoryML`. The script also updates Excel-compatible UTF-8 CSVs in `Recordings/Quest/FactoryMLCombined`:

- `manufacturing_trials_v3.csv`: labeled sensor samples for training and analysis.
- `manufacturing_unlabeled_v3.csv`: samples without a participant rating; do not train on these as if feedback were known.
- `agent_events_v3.csv`: proposal, approval, and outcome events.
- `work_blocks_summary.csv`: one row per labeled session/trial, including participant ID, rating, duration, initial/final speed and table height, and completion counter. Created only when labeled trials exist.

The merge can be rerun with `Tools/Merge-QuestMLData.ps1`. Repeated pulls of the same Quest file are deduplicated by session, trial and sample time. A conflicting duplicate or changed CSV schema stops the merge; originals remain in snapshots. Excel can open the combined CSVs directly. The raw snapshot files should be retained for reproducibility.

For multiple participants on one headset, finish and rate the current person's last work block, close the app, keep USB connected, and run `Tools/Start-NextQuestParticipant.ps1` **between** people. It first stops the app and exports a dated snapshot, then archives (does not delete) the previous on-headset profile. On next launch the app creates a new anonymous profile ID; the next person must enter their own height and arm reach before working. This process needs a real-device check before a team experiment. Do not use one profile for multiple people: it invalidates person-separated evaluation. Keep a separate consented mapping of anonymous participant IDs to session IDs if your protocol requires it; do not put names in exported CSVs.

The app does not currently announce the end of a work block; use an external timer and rate each block before changing conditions.
