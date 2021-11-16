# Snapshots

Every 5 seconds or 50 000 quanta each server (independently from each other) initiates batch update. All 
changes that were made (quanta, constellation settings, payment provider cursors, account states, orderbooks) 
are stored in the batch and queued for saving. After all quanta in the batch have majority of signatures, the 
batch is saved to DB.

All quanta that were handled since the most recent persistence are stored in auditor's memory. In case of a 
connection loss or destroyed consensus, those quanta are immediately persisted on the disk locally to prevent 
potential operations loss. In such a situation, each auditor sends all known quanta to Alpha after the restart
 of the constellation, which then broadcasts them back to auditors. After alpha and auditors synced quanta 
constellation switches to `Ready` state.
