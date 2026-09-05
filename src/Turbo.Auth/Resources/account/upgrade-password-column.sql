-- Run after a verified backup and before enabling password-hash writes in production.
-- The application upgrades legacy plaintext passwords to hashes on successful login.
ALTER TABLE Accounts
    MODIFY COLUMN Password varchar(512) NOT NULL;
