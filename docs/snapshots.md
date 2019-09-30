# Snapshots

Every 5 seconds Alpha server initiates a checkpoint. It saves the entire state on the disk in 
a serialized format (snapshot) and calculates the hash of it. After that, it creates a special 
quantum containing the snapshot hash and puts it at the top of the incoming quantum queue.

When an auditor receives the snapshot quantum, it executes the same checkpoint routine, saves the
snapshot locally, and compares the hash with the received one. In case of a hash mismatch, 
an auditor considers the constellation state corrupted and falls out of consensus.

All quanta that were handled since the most recent snapshot are stored in auditor's memory 
between the snapshots. In case of a connection loss or destroyed consensus, those quanta are 
immediately persisted on the disk locally to prevent potential operations loss. 
In such a situation, each auditor sends all known quanta to Alpha after the restart of 
the constellation, which then broadcasts them back to auditors, replaying all operations 
since the last snapshot one by one.
