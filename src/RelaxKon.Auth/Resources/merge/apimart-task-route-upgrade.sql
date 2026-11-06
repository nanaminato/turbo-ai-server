-- Apply once before starting the updated service. Stop writes first; MySQL DDL implicitly commits.
-- Historical receipts remain unpinned. No foreign key: deleting a supplier must not erase ownership.
ALTER TABLE FeatureAccessTasks ADD COLUMN SupplierKeyId INT NULL AFTER AccountId;
