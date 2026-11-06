-- First administrator: fill in a REAL mailbox, import this file, then use "Forgot password".
-- No dotnet command and no password hash generation are required.
-- Configure PasswordReset SMTP settings before requesting the email.
SET @admin_username = 'admin';
SET @admin_email = 'REPLACE_WITH_YOUR_REAL_EMAIL';

-- Serialize with other bootstrap commands; never overwrite any existing account.
SET @bootstrap_lock = CONCAT('turbo-bootstrap-', LEFT(UPPER(SHA2(LOWER(DATABASE()), 256)), 40));
SELECT GET_LOCK(@bootstrap_lock, 10) INTO @bootstrap_locked;
START TRANSACTION;
INSERT INTO Accounts (Username, Email, Password, SecurityStamp)
SELECT @admin_username, @admin_email, '', REPLACE(UUID(), '-', '')
WHERE @bootstrap_locked = 1
  AND @admin_email <> 'REPLACE_WITH_YOUR_REAL_EMAIL'
  AND @admin_email LIKE '%@%' AND CHAR_LENGTH(@admin_email) <= 50
  AND CHAR_LENGTH(@admin_username) BETWEEN 3 AND 20
  AND (SELECT COUNT(*) FROM Roles WHERE Code IN ('admin', 'user', 'vip')) = 3
  AND NOT EXISTS (SELECT 1 FROM Accounts WHERE Username = @admin_username OR Email = @admin_email)
  AND NOT EXISTS (SELECT 1 FROM AccountRoles ar JOIN Roles r ON r.RoleId = ar.RoleId WHERE r.Code = 'admin');
SET @created_admin_id = IF(ROW_COUNT() = 1, LAST_INSERT_ID(), NULL);
INSERT INTO AccountRoles (AccountId, RoleId)
SELECT @created_admin_id, RoleId FROM Roles
WHERE @created_admin_id IS NOT NULL AND Code IN ('admin', 'user', 'vip');
COMMIT;
SELECT RELEASE_LOCK(@bootstrap_lock) INTO @bootstrap_released;
SELECT @created_admin_id AS CreatedAdminId,
       IF(@created_admin_id IS NULL, 'Not created: check email, roles, lock or existing administrator.',
          'Created. Use Forgot password with this username and email to set a password.') AS Result;
