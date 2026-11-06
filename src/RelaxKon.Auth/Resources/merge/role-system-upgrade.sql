-- Run once with application writes stopped. MySQL DDL implicitly commits.
-- Validate before running: SELECT Name,COUNT(*) FROM Roles GROUP BY Name HAVING COUNT(*)>1;
-- Also check names are nonblank and CHAR_LENGTH(Name)<=64. Preserve exact legacy names as codes.
-- The temporary CHECK guard aborts before persistent DDL if data needs manual repair.
CREATE TEMPORARY TABLE turbo_role_upgrade_guard (Valid int NOT NULL CHECK (Valid=1));
INSERT INTO turbo_role_upgrade_guard SELECT IF(
 EXISTS(SELECT 1 FROM Roles WHERE Name IS NULL OR TRIM(Name)='' OR CHAR_LENGTH(Name)>64)
 OR EXISTS(SELECT 1 FROM Roles GROUP BY Name HAVING COUNT(*)>1)
 OR EXISTS(SELECT 1 FROM AccountRoles GROUP BY AccountId,RoleId HAVING COUNT(*)>1),0,1);
DROP TEMPORARY TABLE turbo_role_upgrade_guard;
ALTER TABLE Roles ADD Code varchar(64) NULL AFTER RoleId, ADD Description varchar(256) NULL AFTER Name;
UPDATE Roles SET Code=Name;
ALTER TABLE Roles MODIFY Code varchar(64) NOT NULL, MODIFY Name varchar(64) NOT NULL, ADD CONSTRAINT UX_Roles_Code UNIQUE(Code);
ALTER TABLE AccountRoles ADD CONSTRAINT UX_AccountRoles_AccountId_RoleId UNIQUE(AccountId,RoleId);

