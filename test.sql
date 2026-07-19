INSERT INTO Batches (Id, Name, ProfileName, TotalCount, CreatedAt, IsDeleted, RowVersion, RouterId) 
VALUES ('11111111-1111-1111-1111-111111111111', 'TestBatch', 'TestProfile', 1, '2026-07-01T00:00:00.0000000', 0, X'00', NULL);

INSERT INTO Vouchers (Id, Username, Password, Price, ProfileName, BatchId, Status, SyncStatus, CreatedAt, IsDeleted, RowVersion, RouterId)
VALUES ('22222222-2222-2222-2222-222222222222', 'TestUser', 'TestPass', '10', 'TestProfile', '11111111-1111-1111-1111-111111111111', 0, 0, '2026-07-01T00:00:00.0000000', 0, X'00', NULL);
